using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.Classes;
using DiGi.PostgreSQL.Table.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the 400 contract of DiGi-Organization/DiGi.GIS.WebAPI#56 on <c>POST gis/buildingdata/aggregatesummary/batch</c>:
        /// an empty <c>Measures</c> list, a list above <see cref="Constants.BuildingData.MaximumAggregateSummariesMeasureCount"/>, and a
        /// negative <c>commandtimeout</c> are all client errors. No database is needed - the guards run before the converter.
        /// </summary>
        [Fact]
        public async Task BuildingDataController_AggregateSummariesBatch_Validation_AnswersBadRequest()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(
                    gISWebAPIConfigurationFileWatcher,
                    new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null),
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));

                Assert.IsType<BadRequestResult>(await controller.GetAggregateSummariesAsync(new AggregateSummariesRequestParameter { Measures = [] }));

                List<AggregateMeasure> measures_Oversized = [];
                for (int i = 0; i < Constants.BuildingData.MaximumAggregateSummariesMeasureCount + 1; i++)
                {
                    measures_Oversized.Add(new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Count));
                }
                Assert.IsType<BadRequestResult>(await controller.GetAggregateSummariesAsync(new AggregateSummariesRequestParameter { Measures = measures_Oversized }));

                Assert.IsType<BadRequestResult>(await controller.GetAggregateSummariesAsync(new AggregateSummariesRequestParameter { Measures = [new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Count)] }, -1));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Pins the request-body binding of DiGi-Organization/DiGi.GIS.WebAPI#56: the <c>Measures</c> list of <see cref="AggregateMeasure"/>
        /// round-trips through System.Text.Json with the web defaults the host's input formatter uses, so the action's parameter binds
        /// what a client actually sends - each measure keeping its column, its function and its position.
        /// <para>The controller facts build the parameter in code and never exercise the wire shape; this fact does, with no database.</para>
        /// </summary>
        [Fact]
        public void AggregateSummariesRequestParameter_JsonRoundTrip_PreservesMeasures()
        {
            AggregateSummariesRequestParameter parameter_Source = new()
            {
                CountyId = 10365,
                Measures =
                [
                    new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Avg),
                    new AggregateMeasure("reference", SinglevalueAggregateFunction.DistinctCount),
                ],
            };

            string json = System.Text.Json.JsonSerializer.Serialize(parameter_Source, System.Text.Json.JsonSerializerOptions.Web);
            AggregateSummariesRequestParameter? parameter_RoundTrip = System.Text.Json.JsonSerializer.Deserialize<AggregateSummariesRequestParameter>(json, System.Text.Json.JsonSerializerOptions.Web);

            Assert.NotNull(parameter_RoundTrip);
            Assert.Equal(10365, parameter_RoundTrip!.CountyId);
            List<AggregateMeasure> measures_RoundTrip = parameter_RoundTrip.Measures;
            Assert.Equal(2, measures_RoundTrip.Count);
            Assert.Equal("floor_area", measures_RoundTrip[0].ColumnUniqueId);
            Assert.Equal(SinglevalueAggregateFunction.Avg, measures_RoundTrip[0].SinglevalueAggregateFunction);
            Assert.Equal("reference", measures_RoundTrip[1].ColumnUniqueId);
            Assert.Equal(SinglevalueAggregateFunction.DistinctCount, measures_RoundTrip[1].SinglevalueAggregateFunction);
        }

        /// <summary>
        /// Pins the parity contract of DiGi-Organization/DiGi.GIS.WebAPI#56: three single-value measures over one scratch partition
        /// answer the same values as the three equivalent <c>aggregatesummary/singlevalue</c> calls, in request order, as one
        /// JSON array whose length equals the measure count.
        /// <para>Scratch table on the local test database; returns without asserting when the conf is absent or the database unreachable.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_AggregateSummariesBatch_ParityWithSinglevalueCalls()
        {
            AggregateSummariesBatchFixture? fixture = await AggregateSummariesBatchFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                // The batch: Count and Avg on floor_area, DistinctCount on reference - over the scratch partition.
                AggregateSummariesRequestParameter batchParameter = new()
                {
                    CountyId = AggregateSummariesBatchFixture.CountyId_Data,
                    Measures =
                    [
                        new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Count),
                        new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Avg),
                        new AggregateMeasure("reference", SinglevalueAggregateFunction.DistinctCount),
                    ],
                };

                ContentResult batchContent = Assert.IsType<ContentResult>(await fixture.Controller.GetAggregateSummariesAsync(batchParameter));
                string content = Assert.IsType<string>(batchContent.Content);
                JsonArray resultArray = Assert.IsType<JsonArray>(JsonNode.Parse(content));
                Assert.Equal(3, resultArray.Count);
                Assert.Equal(3, resultArray[0]!.GetValue<double>());
                Assert.Equal(101.0, resultArray[1]!.GetValue<double>());
                Assert.Equal(3, resultArray[2]!.GetValue<double>());

                // The three equivalent single calls, today's contract: the same values, one per request.
                ContentResult countContent = Assert.IsType<ContentResult>(await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter
                {
                    ColumnUniqueId = "floor_area",
                    SinglevalueAggregateFunction = SinglevalueAggregateFunction.Count,
                    CountyId = AggregateSummariesBatchFixture.CountyId_Data,
                }));
                Assert.Equal("3", countContent.Content);

                ContentResult avgContent = Assert.IsType<ContentResult>(await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter
                {
                    ColumnUniqueId = "floor_area",
                    SinglevalueAggregateFunction = SinglevalueAggregateFunction.Avg,
                    CountyId = AggregateSummariesBatchFixture.CountyId_Data,
                }));
                Assert.Equal("101", avgContent.Content);

                ContentResult distinctCountContent = Assert.IsType<ContentResult>(await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter
                {
                    ColumnUniqueId = "reference",
                    SinglevalueAggregateFunction = SinglevalueAggregateFunction.DistinctCount,
                    CountyId = AggregateSummariesBatchFixture.CountyId_Data,
                }));
                Assert.Equal("3", distinctCountContent.Content);
            }
        }

        /// <summary>
        /// Pins the single-connection (and with it single-command) guarantee of DiGi-Organization/DiGi.GIS.WebAPI#56: the batch opens
        /// exactly one database connection per request, where three equivalent single-value calls open three.
        /// <para><c>ConnectionData.ToString()</c> is called from exactly one site in the codebase - <c>DiGi.PostgreSQL.Create.NpgsqlConnection</c>,
        /// the connection factory - so a <see cref="CountingConnectionData"/> override counts created connections exactly. The control
        /// (three single calls incrementing by three) is what makes the assertion non-vacuous: a batch built by looping the single
        /// call would read three, not one.</para>
        /// <para>The connection count is the command count in this architecture: every converter call opens exactly one connection and
        /// executes exactly one SQL command (the wrapper pattern), so a batch answered from several converter calls would show up as
        /// several connections. One connection therefore proves the measures were served by one command (DiGi-Organization/DiGi.PostgreSQL#15's
        /// single <c>SELECT</c> carrying all aggregate expressions).</para>
        /// <para>Scratch table on the local test database; returns without asserting when the conf is absent or the database unreachable.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_AggregateSummariesBatch_SingleConnection()
        {
            ConnectionData? connectionData_Source = AggregateSummariesBatchFixture.ResolveConnectionData();
            if (connectionData_Source is null)
            {
                return;
            }

            CountingConnectionData countingConnectionData = new(connectionData_Source);
            AggregateSummariesBatchFixture? fixture = await AggregateSummariesBatchFixture.TryCreateAsync(countingConnectionData);
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                // The batch: one request, one connection.
                countingConnectionData.Reset();
                Assert.IsType<ContentResult>(await fixture.Controller.GetAggregateSummariesAsync(new AggregateSummariesRequestParameter
                {
                    CountyId = AggregateSummariesBatchFixture.CountyId_Data,
                    Measures =
                    [
                        new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Count),
                        new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Avg),
                        new AggregateMeasure("reference", SinglevalueAggregateFunction.DistinctCount),
                    ],
                }));
                Assert.Equal(1, countingConnectionData.ConnectionCreations);

                // The control: three single-value calls open three connections.
                countingConnectionData.Reset();
                await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter { ColumnUniqueId = "floor_area", SinglevalueAggregateFunction = SinglevalueAggregateFunction.Count, CountyId = AggregateSummariesBatchFixture.CountyId_Data });
                await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter { ColumnUniqueId = "floor_area", SinglevalueAggregateFunction = SinglevalueAggregateFunction.Avg, CountyId = AggregateSummariesBatchFixture.CountyId_Data });
                await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter { ColumnUniqueId = "reference", SinglevalueAggregateFunction = SinglevalueAggregateFunction.DistinctCount, CountyId = AggregateSummariesBatchFixture.CountyId_Data });
                Assert.Equal(3, countingConnectionData.ConnectionCreations);
            }
        }

        /// <summary>
        /// Pins the no-partial-result contract of DiGi-Organization/DiGi.GIS.WebAPI#56: a <c>Measures</c> list mixing a valid measure
        /// with one naming a column the stored table does not hold is refused with 400 naming the offending column - the whole
        /// request fails, never a partial array (DiGi-Organization/DiGi.PostgreSQL#12).
        /// <para>Scratch table on the local test database; returns without asserting when the conf is absent or the database unreachable.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_AggregateSummariesBatch_UnknownColumn_Answers400()
        {
            AggregateSummariesBatchFixture? fixture = await AggregateSummariesBatchFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                IActionResult actionResult = await fixture.Controller.GetAggregateSummariesAsync(new AggregateSummariesRequestParameter
                {
                    CountyId = AggregateSummariesBatchFixture.CountyId_Data,
                    Measures =
                    [
                        new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Count),
                        new AggregateMeasure("no_such_column", SinglevalueAggregateFunction.Avg),
                    ],
                });

                ObjectResult objectResult = Assert.IsType<ObjectResult>(actionResult);
                Assert.Equal(400, objectResult.StatusCode);
                ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
                Assert.Contains("no_such_column", problemDetails.Detail);
            }
        }

        /// <summary>
        /// Pins that the transient failure mapping of DiGi-Organization/DiGi.GIS.WebAPI#56 follows the house pattern on the batch:
        /// a dead loopback host makes the converter's OpenAsync throw a genuine transient NpgsqlException, which must answer 503
        /// with the Retry-After header.
        /// <para>Medium test: runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long; the sibling dead-host fact
        /// (BuildingDataController_TransientFailure_HistogramSummary_Answers503) measured 2.1 s.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingDataController_AggregateSummariesBatch_TransientDatabaseFailure_Answers503()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(
                    gISWebAPIConfigurationFileWatcher,
                    new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(new ConnectionData("127.0.0.1", "user", "pass", "db", 1)),
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                ObjectResult objectResult = Assert.IsType<ObjectResult>(await controller.GetAggregateSummariesAsync(new AggregateSummariesRequestParameter
                {
                    CountyId = 10365,
                    Measures = [new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Count)],
                }));

                Assert.Equal(503, objectResult.StatusCode);
                Assert.Equal("30", (string?)controller.HttpContext.Response.Headers["Retry-After"]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// A <see cref="ConnectionData"/> whose <c>ToString()</c> counts its own calls. The connection factory
        /// (<c>DiGi.PostgreSQL.Create.NpgsqlConnection</c>) is the only site in the codebase that reads the connection string, so
        /// the count is the number of connections the converter created - the single-connection guarantee of
        /// DiGi-Organization/DiGi.GIS.WebAPI#56 made observable.
        /// </summary>
        private sealed class CountingConnectionData : ConnectionData
        {
            private int connectionCreations;

            /// <summary>
            /// Initializes a new instance of the <see cref="CountingConnectionData"/> class copying every member of the source.
            /// </summary>
            /// <param name="source">The connection settings to copy.</param>
            public CountingConnectionData(ConnectionData source)
                : base(source.Host, source.Username, source.Password, source.Database, source.Port)
            {
                this.MaximumPoolSize = source.MaximumPoolSize;
                this.PoolTimeout = source.PoolTimeout;
            }

            /// <summary>Gets the number of connection creations counted so far.</summary>
            public int ConnectionCreations
            {
                get
                {
                    return Volatile.Read(ref connectionCreations);
                }
            }

            /// <summary>Resets the counter. Call immediately before the operation under assertion.</summary>
            public void Reset()
            {
                Interlocked.Exchange(ref connectionCreations, 0);
            }

            /// <summary>Counts one connection creation, then answers the connection string as usual.</summary>
            public override string ToString()
            {
                Interlocked.Increment(ref connectionCreations);
                return base.ToString();
            }
        }

        /// <summary>
        /// Pushes a scratch building-data table (county, reference, numeric measure, three rows in one partition) into the local
        /// test database and hands back a live <see cref="BuildingDataController"/> wired to it, so the DiGi-Organization/DiGi.GIS.WebAPI#56
        /// batch contract is exercised through the real action over the real converter. Returns <c>null</c> - and the fact returns
        /// without asserting - when the conf is absent or the local test database is unreachable, in which case nothing was written.
        /// <para>A fact can build the fixture on a <see cref="CountingConnectionData"/> and keep its own reference to observe connection
        /// creation. Removes the scratch table and its column metadata on disposal, so the base is left as found.</para>
        /// </summary>
        private sealed class AggregateSummariesBatchFixture : System.IAsyncDisposable
        {
            /// <summary>The county that holds the scratch rows.</summary>
            public const int CountyId_Data = 910003;

            private readonly ConnectionData connectionData;
            private readonly string path_Watcher;
            private readonly string tableName;
            private readonly GISWebAPIConfigurationFileWatcher watcher;

            /// <summary>Gets the controller wired to the scratch table.</summary>
            public BuildingDataController Controller { get; }

            private AggregateSummariesBatchFixture(ConnectionData connectionData, string path_Watcher, string tableName, GISWebAPIConfigurationFileWatcher watcher, BuildingDataController controller)
            {
                this.connectionData = connectionData;
                this.path_Watcher = path_Watcher;
                this.tableName = tableName;
                this.watcher = watcher;
                this.Controller = controller;
            }

            /// <summary>
            /// Creates the scratch fixture on the plain connection data, or <c>null</c> when the conf is absent or the local test database is not available.
            /// </summary>
            public static async Task<AggregateSummariesBatchFixture?> TryCreateAsync()
            {
                ConnectionData? connectionData = ResolveConnectionData();
                if (connectionData is null)
                {
                    return null;
                }

                return await TryCreateAsync(connectionData);
            }

            /// <summary>
            /// Creates the scratch fixture on the given connection data, or <c>null</c> when the local test database is not available.
            /// </summary>
            /// <param name="connectionData">The connection settings - plain or counting - the scratch converter is built with.</param>
            public static async Task<AggregateSummariesBatchFixture?> TryCreateAsync(ConnectionData connectionData)
            {
                // The fact returns without asserting when the local test database is unreachable.
                try
                {
                    await using NpgsqlConnection? npgsqlConnection_Probe = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                    if (npgsqlConnection_Probe is null)
                    {
                        return null;
                    }
                    await npgsqlConnection_Probe.OpenAsync();
                }
                catch (NpgsqlException)
                {
                    return null;
                }

                ScratchBuildingDataPostgreSQLConverter scratchBuildingDataPostgreSQLConverter = new(connectionData);

                DiGi.Core.IO.Table.Classes.Table table = new();
                table.AddColumn(new ExtendedColumn("County Id", typeof(int), null, null));
                table.AddColumn(new ExtendedColumn("Reference", typeof(string), null, null));
                table.AddColumn(new ExtendedColumn("Floor area", typeof(double), null, null));
                for (int i = 0; i < 3; i++)
                {
                    table.AddRow([CountyId_Data, $"SCRATCH-AB-{i:D3}", 100.0 + i]);
                }

                string path_Watcher = ConfigurationFilePath();
                GISWebAPIConfigurationFileWatcher watcher = new(path_Watcher);
                BuildingDataController controller = new(
                    watcher,
                    scratchBuildingDataPostgreSQLConverter,
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));

                // The fixture is live before the push, so a push that fails after the table exists still cleans up
                // what it wrote (and the fact fails loudly rather than skipping over a half-written base).
                AggregateSummariesBatchFixture fixture = new(connectionData, path_Watcher, scratchBuildingDataPostgreSQLConverter.TableName, watcher, controller);
                try
                {
                    if (!await scratchBuildingDataPostgreSQLConverter.PushAsync(table))
                    {
                        throw new InvalidOperationException($"PushAsync did not complete for the scratch table {scratchBuildingDataPostgreSQLConverter.TableName}.");
                    }
                }
                catch
                {
                    await fixture.DisposeAsync();
                    throw;
                }

                return fixture;
            }

            /// <summary>
            /// Resolves the local test database connection data from <c>DiGi.Test/user files/GIS_PostgreSQL_Main_Test.conf</c>, or
            /// <c>null</c> when the conf is absent - the facts then return without asserting.
            /// </summary>
            public static ConnectionData? ResolveConnectionData()
            {
                string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(Assembly.GetExecutingAssembly());
                string? path_Conf = directory_UserFiles is null ? null : System.IO.Path.Combine(directory_UserFiles, "GIS_PostgreSQL_Main_Test.conf");
                if (path_Conf is null || !File.Exists(path_Conf))
                {
                    return null;
                }

                return DiGi.PostgreSQL.Create.ConnectionData(DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(path_Conf));
            }

            /// <summary>
            /// Removes the scratch table and its column metadata, and deletes the configuration file.
            /// </summary>
            public async ValueTask DisposeAsync()
            {
                await DiGi.PostgreSQL.Modify.RemoveTableAsync(this.connectionData, this.tableName);

                await using NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(this.connectionData);
                if (npgsqlConnection_Cleanup is not null)
                {
                    await npgsqlConnection_Cleanup.OpenAsync();
                    await using NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection_Cleanup);
                    npgsqlCommand.Parameters.AddWithValue("tableName", this.tableName);
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }

                this.watcher.Dispose();
                File.Delete(this.path_Watcher);
            }
        }
    }
}
