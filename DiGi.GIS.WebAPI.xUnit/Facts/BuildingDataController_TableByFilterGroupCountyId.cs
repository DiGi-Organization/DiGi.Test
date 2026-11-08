using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.Enums;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the county scoping of DiGi-Organization/DiGi.GIS.WebAPI#55 on <c>POST gis/buildingdata/tablebyfiltergroup</c>:
        /// an omitted <c>CountyId</c> reads across all partitions (the pre-#55 behaviour, kept for existing clients); a present
        /// <c>CountyId</c> scopes the read to that county partition; a caller <c>county_id</c> condition is left as-is and the
        /// <c>CountyId</c> condition is ANDed with it, so an agreeing pair returns that county and a conflicting pair answers empty.
        /// <para>The rows go into a scratch table through <see cref="ScratchBuildingDataPostgreSQLConverter"/> on the local test
        /// database named by <c>DiGi.Test/user files/GIS_PostgreSQL_Main_Test.conf</c>; the fact returns without asserting when
        /// that conf is absent or the database is unreachable, and removes the scratch table and its column metadata in <c>finally</c>.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_TableByFilterGroup_CountyScoping()
        {
            TableByFilterGroupCountyFixture? fixture = await TableByFilterGroupCountyFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                // Omitted CountyId and no caller county_id filter: the pre-#55 cross-partition read, both counties' rows.
                List<int> counties_None = await fixture.CountyValuesAsync(new BuildingDataByFilterGroupParameter
                {
                    FilterGroup = new DiGi.PostgreSQL.Table.Classes.FilterGroup(),
                });
                Assert.Equal(2, counties_None.Count(c => c == TableByFilterGroupCountyFixture.CountyId_A));
                Assert.Equal(2, counties_None.Count(c => c == TableByFilterGroupCountyFixture.CountyId_B));

                // A present CountyId scopes the read to that partition.
                List<int> counties_A = await fixture.CountyValuesAsync(new BuildingDataByFilterGroupParameter
                {
                    FilterGroup = new DiGi.PostgreSQL.Table.Classes.FilterGroup(),
                    CountyId = TableByFilterGroupCountyFixture.CountyId_A,
                });
                Assert.Equal(2, counties_A.Count);
                Assert.All(counties_A, county => Assert.Equal(TableByFilterGroupCountyFixture.CountyId_A, county));

                // A caller county_id condition that agrees with CountyId: both present, that county.
                List<int> counties_Agree = await fixture.CountyValuesAsync(new BuildingDataByFilterGroupParameter
                {
                    FilterGroup = fixture.CallerCountyFilter(TableByFilterGroupCountyFixture.CountyId_A),
                    CountyId = TableByFilterGroupCountyFixture.CountyId_A,
                });
                Assert.Equal(2, counties_Agree.Count);
                Assert.All(counties_Agree, county => Assert.Equal(TableByFilterGroupCountyFixture.CountyId_A, county));

                // A caller county_id condition that conflicts with CountyId: both ANDed, so nothing matches.
                List<int> counties_Conflict = await fixture.CountyValuesAsync(new BuildingDataByFilterGroupParameter
                {
                    FilterGroup = fixture.CallerCountyFilter(TableByFilterGroupCountyFixture.CountyId_B),
                    CountyId = TableByFilterGroupCountyFixture.CountyId_A,
                });
                Assert.Empty(counties_Conflict);
            }
        }

        /// <summary>
        /// Pushes scratch building-data rows into two counties on the local test database and hands back a live
        /// <see cref="BuildingDataController"/> wired to the scratch table, so the DiGi-Organization/DiGi.GIS.WebAPI#55 county
        /// scoping of <c>tablebyfiltergroup</c> is exercised through the real action over the real converter.
        /// Returns <c>null</c> - and the fact returns without asserting - when the conf is absent or the local test database is
        /// unreachable, in which case nothing was written. A push that fails after the table exists cleans up what it wrote and
        /// fails loudly.
        /// <para>Removes the scratch table and its column metadata on disposal, so the base is left as found.</para>
        /// </summary>
        private sealed class TableByFilterGroupCountyFixture : System.IAsyncDisposable
        {
            /// <summary>The first scratch county.</summary>
            public const int CountyId_A = 910101;

            /// <summary>The second scratch county.</summary>
            public const int CountyId_B = 910102;

            private readonly ConnectionData connectionData;
            private readonly string path_Watcher;
            private readonly string tableName;
            private readonly GISWebAPIConfigurationFileWatcher watcher;

            /// <summary>Gets the controller wired to the scratch table.</summary>
            public BuildingDataController Controller { get; }

            private TableByFilterGroupCountyFixture(ConnectionData connectionData, string path_Watcher, string tableName, GISWebAPIConfigurationFileWatcher watcher, BuildingDataController controller)
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
            public static async Task<TableByFilterGroupCountyFixture?> TryCreateAsync()
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

                ScratchBuildingDataPostgreSQLConverter scratch = new(connectionData);

                Table table = new();
                table.AddColumn(new ExtendedColumn("County Id", typeof(int), null, null));
                table.AddColumn(new ExtendedColumn("Reference", typeof(string), null, null));
                table.AddColumn(new ExtendedColumn("Floor area", typeof(double), null, null));
                table.AddRow([CountyId_A, "SCRATCH-C-A1", 100.0]);
                table.AddRow([CountyId_A, "SCRATCH-C-A2", 101.0]);
                table.AddRow([CountyId_B, "SCRATCH-C-B1", 200.0]);
                table.AddRow([CountyId_B, "SCRATCH-C-B2", 201.0]);

                string path_Watcher = ConfigurationFilePath();
                GISWebAPIConfigurationFileWatcher watcher = new(path_Watcher);
                BuildingDataController controller = new(
                    watcher,
                    scratch,
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));

                // The fixture is live before the push, so a push that fails after the table exists still cleans up what it wrote.
                TableByFilterGroupCountyFixture fixture = new(connectionData, path_Watcher, scratch.TableName, watcher, controller);
                try
                {
                    if (!await scratch.PushAsync(table))
                    {
                        throw new InvalidOperationException($"PushAsync did not complete for the scratch table {scratch.TableName}.");
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
            /// Builds a caller <see cref="DiGi.PostgreSQL.Table.Classes.FilterGroup"/> holding a single <c>county_id</c>
            /// <see cref="FilterOperator.Equals"/> condition on the given county - the shape a client puts in the body to scope
            /// the read by county itself.
            /// </summary>
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Called on the fixture instance the facts receive.")]
            public DiGi.PostgreSQL.Table.Classes.FilterGroup CallerCountyFilter(int countyId)
            {
                return new DiGi.PostgreSQL.Table.Classes.FilterGroup
                {
                    FilterConditions =
                    [
                        new DiGi.PostgreSQL.Table.Classes.FilterCondition
                        {
                            ColumnUniqueId = "county_id",
                            FilterOperator = FilterOperator.Equals,
                            Value = countyId,
                        },
                    ],
                };
            }

            /// <summary>
            /// Runs <c>tablebyfiltergroup</c> with the given parameter and returns the <c>County Id</c> value of every returned
            /// row, in row order.
            /// </summary>
            public async Task<List<int>> CountyValuesAsync(BuildingDataByFilterGroupParameter buildingDataByFilterGroupParameter)
            {
                IActionResult actionResult = await Controller.GetTableByFilterGroupAsync(buildingDataByFilterGroupParameter);
                ContentResult contentResult = Assert.IsType<ContentResult>(actionResult);

                JsonObject jsonObject = JsonNode.Parse(contentResult.Content!)!.AsObject();
                JsonArray columns = jsonObject["Columns"]!.AsArray();
                int index_County = columns.Select((x, i) => (Name: x?["Name"]?.GetValue<string>(), Index: i)).Single(x => x.Name == "County Id").Index;

                return [.. jsonObject["Rows"]!.AsArray().Select(row => row![index_County]!.GetValue<int>())];
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
