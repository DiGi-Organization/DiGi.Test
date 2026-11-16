using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the 400 contract of DiGi-Organization/DiGi.GIS.WebAPI#54 on <c>GET gis/buildingdata/uniquevalues</c>:
        /// a blank <c>columnuniqueid</c> is a client error, and the converter throws <see cref="DiGi.PostgreSQL.Table.Classes.InvalidFilterException"/>
        /// before any connection is created, so the action must answer 400 with a ProblemDetails body rather than the
        /// generic 500 it answered before the mapping existed. No database is needed for this path.
        /// </summary>
        [Fact]
        public async Task BuildingDataController_UniqueValues_BlankColumn_Answers400()
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

                IActionResult actionResult = await controller.GetUniqueValuesAsync(string.Empty, null);

                ObjectResult objectResult = Assert.IsType<ObjectResult>(actionResult);
                Assert.Equal(400, objectResult.StatusCode);
                ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
                Assert.False(string.IsNullOrWhiteSpace(problemDetails.Detail));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Pins the 400 contract of DiGi-Organization/DiGi.GIS.WebAPI#54 on the aggregate and unique-values reads:
        /// a target <c>ColumnUniqueId</c> the stored table does not hold is a client error, so each of
        /// <c>aggregatesummary/singlevalue</c>, <c>aggregatesummary/multivalue</c>, <c>histogramsummary</c> and
        /// <c>uniquevalues</c> must answer 400 with a ProblemDetails body naming the column, not the generic 500
        /// they answered before the mapping existed.
        /// <para>The data goes into a scratch table through <see cref="ScratchBuildingDataPostgreSQLConverter"/> on the local test database
        /// named by <c>DiGi.Test/user files/GIS_PostgreSQL_Main_Test.conf</c>; the fact returns without asserting when that conf is
        /// absent or the database is unreachable, and removes the scratch table and its column metadata in <c>finally</c>.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_InvalidTargetColumn_Answers400()
        {
            InvalidFilterFixture? fixture = await InvalidFilterFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                IActionResult singlevalue = await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter
                {
                    ColumnUniqueId = "no_such_column",
                    SinglevalueAggregateFunction = SinglevalueAggregateFunction.Count,
                    CountyId = InvalidFilterFixture.CountyId_Data,
                });
                AssertInvalidFilter(singlevalue, "no_such_column");

                IActionResult multivalue = await fixture.Controller.GetMultivalueAggregateSummaryAsync(new MultivalueAggregateRequestParameter
                {
                    ColumnUniqueId = "no_such_column",
                    MultivalueAggregateFunction = MultivalueAggregateFunction.SplitDistinctCount,
                    CountyId = InvalidFilterFixture.CountyId_Data,
                });
                AssertInvalidFilter(multivalue, "no_such_column");

                IActionResult histogram = await fixture.Controller.GetHistogramSummaryAsync(new HistogramRequestParameter
                {
                    ColumnUniqueId = "no_such_column",
                    BucketCount = 10,
                    CountyId = InvalidFilterFixture.CountyId_Data,
                });
                AssertInvalidFilter(histogram, "no_such_column");

                IActionResult uniqueValues = await fixture.Controller.GetUniqueValuesAsync("no_such_column", InvalidFilterFixture.CountyId_Data);
                AssertInvalidFilter(uniqueValues, "no_such_column");
            }
        }

        /// <summary>
        /// Pins the 400 contract of DiGi-Organization/DiGi.GIS.WebAPI#54 on the filtered reads: a <c>FilterGroup</c>
        /// condition on a column the stored table does not hold is a client error, so <c>aggregatesummary/singlevalue</c>,
        /// <c>tablebyfiltergroup</c> and <c>uniquevaluesbycolumnuniqueidparameter</c> must answer 400 with a ProblemDetails
        /// body naming the column and the operator, not the generic 500 they answered before the mapping existed.
        /// <para>Scratch table on the local test database; returns without asserting when the conf is absent or the database is unreachable.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_InvalidFilterColumn_Answers400()
        {
            InvalidFilterFixture? fixture = await InvalidFilterFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            DiGi.PostgreSQL.Table.Classes.FilterGroup filterGroup = new()
            {
                FilterConditions = [new DiGi.PostgreSQL.Table.Classes.FilterCondition() { ColumnUniqueId = "no_such_filter_column", FilterOperator = FilterOperator.Equals, Value = "x" }],
            };

            await using (fixture)
            {
                IActionResult singlevalue = await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter
                {
                    ColumnUniqueId = "floor_area",
                    SinglevalueAggregateFunction = SinglevalueAggregateFunction.Count,
                    CountyId = InvalidFilterFixture.CountyId_Data,
                    FilterGroup = filterGroup,
                });
                AssertInvalidFilter(singlevalue, "no_such_filter_column", FilterOperator.Equals.ToString());

                IActionResult tableByFilterGroup = await fixture.Controller.GetTableByFilterGroupAsync(new BuildingDataByFilterGroupParameter
                {
                    ColumnUniqueIds = ["reference"],
                    FilterGroup = filterGroup,
                });
                AssertInvalidFilter(tableByFilterGroup, "no_such_filter_column", FilterOperator.Equals.ToString());

                IActionResult uniqueValues = await fixture.Controller.GetUniqueValuesByColumnUniqueIdParameterAsync(new UniqueValuesByColumnUniqueIdParameter
                {
                    ColumnUniqueId = "reference",
                    FilterGroup = filterGroup,
                });
                AssertInvalidFilter(uniqueValues, "no_such_filter_column", FilterOperator.Equals.ToString());
            }
        }

        /// <summary>
        /// Pins the 400 contract of DiGi-Organization/DiGi.GIS.WebAPI#54 for an unconvertible filter value:
        /// <c>Equals</c> with the string <c>"abc"</c> on the numeric <c>floor_area</c> column is a client error, so
        /// <c>aggregatesummary/singlevalue</c> must answer 400 with a ProblemDetails body naming the column and the
        /// operator, not the generic 500 it answered before the mapping existed (and not the silent <c>0</c> the deployed
        /// build still answers through the <c>1=0</c> fallback).
        /// <para>Scratch table on the local test database; returns without asserting when the conf is absent or the database is unreachable.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_UnconvertibleFilterValue_Answers400()
        {
            InvalidFilterFixture? fixture = await InvalidFilterFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                DiGi.PostgreSQL.Table.Classes.FilterGroup filterGroup = new()
                {
                    FilterConditions = [new DiGi.PostgreSQL.Table.Classes.FilterCondition() { ColumnUniqueId = "floor_area", FilterOperator = FilterOperator.Equals, Value = "abc" }],
                };

                IActionResult actionResult = await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter
                {
                    ColumnUniqueId = "floor_area",
                    SinglevalueAggregateFunction = SinglevalueAggregateFunction.Count,
                    CountyId = InvalidFilterFixture.CountyId_Data,
                    FilterGroup = filterGroup,
                });

                AssertInvalidFilter(actionResult, "floor_area", FilterOperator.Equals.ToString());
            }
        }

        /// <summary>
        /// Pins that a valid filter over an empty partition keeps its documented answer and is never confused with the
        /// 400 client error of DiGi-Organization/DiGi.GIS.WebAPI#54: <c>aggregatesummary/singlevalue</c> (<c>Count</c>) answers
        /// 200 with <c>0</c>, and <c>uniquevalues</c> answers the documented 404 empty answer for a scope that holds nothing.
        /// <para>Scratch table on the local test database; returns without asserting when the conf is absent or the database is unreachable.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_ValidFilterEmptyPartition_NeverAnswers400()
        {
            InvalidFilterFixture? fixture = await InvalidFilterFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                // A valid filter that matches nothing in the empty partition: Count is 0, a 200 with a "0" body.
                DiGi.PostgreSQL.Table.Classes.FilterGroup filterGroup = new()
                {
                    FilterConditions = [new DiGi.PostgreSQL.Table.Classes.FilterCondition() { ColumnUniqueId = "reference", FilterOperator = FilterOperator.Equals, Value = "SCRATCH-IF-999" }],
                };

                IActionResult singlevalue = await fixture.Controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter
                {
                    ColumnUniqueId = "floor_area",
                    SinglevalueAggregateFunction = SinglevalueAggregateFunction.Count,
                    CountyId = InvalidFilterFixture.CountyId_Empty,
                    FilterGroup = filterGroup,
                });

                ContentResult singlevalueContent = Assert.IsType<ContentResult>(singlevalue);
                // A ContentResult with a null StatusCode executes as 200 (ContentResultExecutor: statusCode = result.StatusCode ?? 200).
                Assert.True(singlevalueContent.StatusCode is null or 200, $"Unexpected status code: {singlevalueContent.StatusCode}");
                Assert.Equal("0", singlevalueContent.Content);

                // A valid column over the empty partition: the documented empty answer is 404, never 400.
                IActionResult uniqueValues = await fixture.Controller.GetUniqueValuesAsync("reference", InvalidFilterFixture.CountyId_Empty);
                Assert.IsType<NotFoundResult>(uniqueValues);
            }
        }

        /// <summary>
        /// Pins that the transient failure mapping is untouched by the 400 mapping of DiGi-Organization/DiGi.GIS.WebAPI#54:
        /// a dead loopback host makes the converter's OpenAsync throw a genuine transient NpgsqlException on
        /// <c>histogramsummary</c>, which must still answer 503 with the Retry-After header — the catch order keeps
        /// the transient branch ahead of both the invalid-filter and the generic branches.
        /// <para>Medium test: runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long; the sibling dead-host
        /// fact measured 2.1 s.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingDataController_TransientFailure_HistogramSummary_Answers503()
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

                ObjectResult objectResult = Assert.IsType<ObjectResult>(await controller.GetHistogramSummaryAsync(new HistogramRequestParameter
                {
                    ColumnUniqueId = "floor_area",
                    BucketCount = 10,
                    CountyId = 10365,
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
        /// Asserts the DiGi-Organization/DiGi.GIS.WebAPI#54 400 contract: a ProblemDetails body whose detail names the
        /// offending column — and the operator, where the case carries one.
        /// </summary>
        /// <param name="actionResult">The action result to assert.</param>
        /// <param name="columnUniqueId">The column the detail must name.</param>
        /// <param name="filterOperator">The operator the detail must name; <c>null</c> when the case carries none.</param>
        private static void AssertInvalidFilter(IActionResult actionResult, string columnUniqueId, string? filterOperator = null)
        {
            ObjectResult objectResult = Assert.IsType<ObjectResult>(actionResult);
            Assert.Equal(400, objectResult.StatusCode);

            ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            string? detail = problemDetails.Detail;
            Assert.False(string.IsNullOrWhiteSpace(detail));
            Assert.Contains(columnUniqueId, detail);
            if (filterOperator is not null)
            {
                Assert.Contains(filterOperator, detail);
            }
        }

        /// <summary>
        /// Pushes a scratch building-data table (reference, county, numeric measure) into the local test database and
        /// hands back a live <see cref="BuildingDataController"/> wired to it, so the DiGi-Organization/DiGi.GIS.WebAPI#54
        /// 400 contract is exercised through the real action over the real converter. Returns <c>null</c> — and the fact
        /// returns without asserting — when the conf is absent or the local test database is unreachable, in which case
        /// nothing was written. A push that fails after the table exists cleans up what it wrote and fails loudly.
        /// <para>Removes the scratch table and its column metadata on disposal, so the base is left as found.</para>
        /// </summary>
        private sealed class InvalidFilterFixture : System.IAsyncDisposable
        {
            /// <summary>The county that holds the scratch rows.</summary>
            public const int CountyId_Data = 910001;

            /// <summary>The sibling county that holds none of the scratch rows.</summary>
            public const int CountyId_Empty = 910002;

            private readonly ConnectionData connectionData;
            private readonly string path_Watcher;
            private readonly string tableName;
            private readonly GISWebAPIConfigurationFileWatcher watcher;

            /// <summary>Gets the controller wired to the scratch table.</summary>
            public BuildingDataController Controller { get; }

            private InvalidFilterFixture(ConnectionData connectionData, string path_Watcher, string tableName, GISWebAPIConfigurationFileWatcher watcher, BuildingDataController controller)
            {
                this.connectionData = connectionData;
                this.path_Watcher = path_Watcher;
                this.tableName = tableName;
                this.watcher = watcher;
                this.Controller = controller;
            }

            /// <summary>
            /// Creates the scratch fixture, or <c>null</c> when the local test database is not available.
            /// </summary>
            public static async Task<InvalidFilterFixture?> TryCreateAsync()
            {
                string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(Assembly.GetExecutingAssembly());
                string? path_Conf = directory_UserFiles is null ? null : Path.Combine(directory_UserFiles, "GIS_PostgreSQL_Main_Test.conf");
                if (path_Conf is null || !File.Exists(path_Conf))
                {
                    return null;
                }

                ConnectionData? connectionData = DiGi.PostgreSQL.Create.ConnectionData(DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(path_Conf));
                if (connectionData is null)
                {
                    return null;
                }

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

                Table table = new();
                table.AddColumn(new ExtendedColumn("County Id", typeof(int), null, null));
                table.AddColumn(new ExtendedColumn("Reference", typeof(string), null, null));
                table.AddColumn(new ExtendedColumn("Floor area", typeof(double), null, null));
                for (int i = 0; i < 3; i++)
                {
                    table.AddRow([CountyId_Data, $"SCRATCH-IF-{i:D3}", 100.0 + i]);
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
                InvalidFilterFixture fixture = new(connectionData, path_Watcher, scratchBuildingDataPostgreSQLConverter.TableName, watcher, controller);
                try
                {
                    if (!await scratchBuildingDataPostgreSQLConverter.PushAsync(table))
                    {
                        throw new System.InvalidOperationException($"PushAsync did not complete for the scratch table {scratchBuildingDataPostgreSQLConverter.TableName}.");
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
