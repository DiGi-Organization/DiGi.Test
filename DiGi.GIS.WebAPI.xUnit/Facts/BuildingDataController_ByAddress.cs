using DiGi.Core.IO.Table.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.PostgreSQL;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the wire contract of DiGi-Organization/DiGi.GIS.WebAPI#85 on <c>gis/buildingdata/tablebyreference</c> and the new <c>gis/buildingdata/byaddress</c>: the query parameter names each action binds, their order, the cancellation token last, and the nullability that makes an omitted parameter detectable instead of <c>default(T)</c> (<c>Coding - WebAPI Contracts.md</c> section 1).
        /// <para>A client reaches the API over HTTP only, so a renamed <c>[FromQuery(Name = ...)]</c> is answered by ASP.NET Core with 200 and an unfiltered result - the names are asserted here, not derived.</para>
        /// </summary>
        [Fact]
        public void BuildingDataController_AddressEndpoints_QueryParameterNames()
        {
            Dictionary<string, string[]> names_ByMethodName = new()
            {
                [nameof(BuildingDataController.GetTableByReferenceAsync)] = ["reference", "countyid", "columns", "commandtimeout"],
                [nameof(BuildingDataController.GetByAddressAsync)] = ["text", "housenumber", "countyid", "limit", "columns", "commandtimeout"],
            };

            foreach (KeyValuePair<string, string[]> keyValuePair in names_ByMethodName)
            {
                MethodInfo? methodInfo = typeof(BuildingDataController).GetMethod(keyValuePair.Key);
                Assert.NotNull(methodInfo);

                List<string> names = [];
                foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
                {
                    FromQueryAttribute? fromQueryAttribute = parameterInfo.GetCustomAttribute<FromQueryAttribute>();
                    if (fromQueryAttribute?.Name is string name)
                    {
                        names.Add(name);
                    }
                }

                Assert.Equal(keyValuePair.Value, names);

                ParameterInfo[] parameterInfos = methodInfo.GetParameters();
                Assert.Equal(typeof(System.Threading.CancellationToken), parameterInfos[^1].ParameterType);
            }

            Assert.Equal("gis/buildingdata/tablebyreference", DiGi.WebAPI.Query.Path<BuildingDataController>(nameof(BuildingDataController.GetTableByReferenceAsync)));
            Assert.Equal("gis/buildingdata/byaddress", DiGi.WebAPI.Query.Path<BuildingDataController>(nameof(BuildingDataController.GetByAddressAsync)));

            MethodInfo? methodInfo_ByAddress = typeof(BuildingDataController).GetMethod(nameof(BuildingDataController.GetByAddressAsync));
            Assert.NotNull(methodInfo_ByAddress);

            NullabilityInfoContext nullabilityInfoContext = new();
            foreach (ParameterInfo parameterInfo in methodInfo_ByAddress.GetParameters())
            {
                if (parameterInfo.Name == "text")
                {
                    Assert.Equal(NullabilityState.Nullable, nullabilityInfoContext.Create(parameterInfo).ReadState);
                }

                if (parameterInfo.Name == "limit")
                {
                    Assert.NotNull(Nullable.GetUnderlyingType(parameterInfo.ParameterType));
                }

                if (parameterInfo.Name == "columns")
                {
                    Assert.Equal(typeof(List<string>), parameterInfo.ParameterType);
                }
            }
        }

        /// <summary>
        /// Verifies that <c>byaddress</c> refuses every bad caller value before a connection is opened - a missing, blank or letterless text, a limit outside 1-100, an omitted limit, a non-positive county id, a negative timeout - and that a projection which cannot be checked because no database answers is refused loudly (500) instead of read silently.
        /// <para>The no-database path also pins the difference between the two endpoints: <c>tablebyreference</c> without a projection keeps its historical path and answers 404 when no table can be read, while a projection that could not be validated answers 500.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_ByAddress_Guards_AnswerBeforeTheDatabase()
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

                Assert.IsType<BadRequestResult>(await controller.GetByAddressAsync(null, null, null, 20));
                Assert.IsType<BadRequestResult>(await controller.GetByAddressAsync(" ", null, null, 20));
                Assert.IsType<BadRequestResult>(await controller.GetByAddressAsync("!!!", null, null, 20));

                Assert.IsType<BadRequestResult>(await controller.GetByAddressAsync("Warszawa", null, null, 0));
                Assert.IsType<BadRequestResult>(await controller.GetByAddressAsync("Warszawa", null, null, 101));
                Assert.IsType<BadRequestResult>(await controller.GetByAddressAsync("Warszawa", null, null, limit: null));

                Assert.IsType<BadRequestResult>(await controller.GetByAddressAsync("Warszawa", null, 0, 20));
                Assert.IsType<BadRequestResult>(await controller.GetByAddressAsync("Warszawa", null, null, 20, null, -1));

                ObjectResult objectResult_ByAddress = Assert.IsType<ObjectResult>(await controller.GetByAddressAsync("Warszawa", null, null, 20, ["floor_area"]));
                Assert.Equal(500, objectResult_ByAddress.StatusCode);

                ObjectResult objectResult_Table = Assert.IsType<ObjectResult>(await controller.GetTableByReferenceAsync("SCRATCH-000", null, ["floor_area"]));
                Assert.Equal(500, objectResult_Table.StatusCode);

                Assert.IsType<NotFoundResult>(await controller.GetTableByReferenceAsync("SCRATCH-000"));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// End to end on the local test database: <c>tablebyreference</c> projects exactly the columns the caller names (validated like a filter column, 400 naming an id the table does not hold, and the comma form accepted), and <c>byaddress</c> answers the matched address plus the projected building data of each match in one call, read from the county part each match carries.
        /// <para>The fixture writes a scratch <c>building_data</c> table (County Id, Reference, Floor area, Storeys) under scratch county part 910401 and two address points of a street nothing else holds: one whose building has a row, one whose building has none. The default projection resolves to the two stored columns only - <c>floor_area</c> and <c>storeys</c> - and the five default columns the scratch table does not hold are dropped, never refused. Returns without asserting when the conf is absent or the database is unreachable (DiGi-Organization/DiGi.GIS.WebAPI#85).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingDataController_ByAddress_ScopedReadsAndProjection()
        {
            ByAddressFixture? fixture = await ByAddressFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                BuildingDataController controller = fixture.Controller;

                // A projection is checked before the read: an unknown id is refused naming it, an entry naming no id too.
                IActionResult unknownColumns = await controller.GetTableByReferenceAsync(ByAddressFixture.Reference_With_Data, ByAddressFixture.CountyId_Data, ["no_such_column"]);
                ObjectResult objectResult_Unknown = Assert.IsType<ObjectResult>(unknownColumns);
                Assert.Equal(400, objectResult_Unknown.StatusCode);
                ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult_Unknown.Value);
                Assert.Contains("no_such_column", problemDetails.Detail);

                IActionResult emptyId = await controller.GetTableByReferenceAsync(ByAddressFixture.Reference_With_Data, ByAddressFixture.CountyId_Data, ["floor_area,"]);
                ObjectResult objectResult_Empty = Assert.IsType<ObjectResult>(emptyId);
                Assert.Equal(400, objectResult_Empty.StatusCode);

                // The repeated-key and the comma form name the same projection, and the answer carries the projected
                // columns plus the two the read is keyed by (reference, and county_id because the read is scoped).
                IActionResult commaForm = await controller.GetTableByReferenceAsync(ByAddressFixture.Reference_With_Data, ByAddressFixture.CountyId_Data, ["floor_area, storeys"]);
                ContentResult contentResult_Comma = Assert.IsType<ContentResult>(commaForm);
                Assert.Contains("Floor area", contentResult_Comma.Content);
                Assert.Contains("Storeys", contentResult_Comma.Content);
                Assert.Contains("County Id", contentResult_Comma.Content);
                Assert.Contains("100", contentResult_Comma.Content);

                IActionResult narrow = await controller.GetTableByReferenceAsync(ByAddressFixture.Reference_With_Data, ByAddressFixture.CountyId_Data, ["floor_area"]);
                ContentResult contentResult_Narrow = Assert.IsType<ContentResult>(narrow);
                Assert.Contains("Floor area", contentResult_Narrow.Content);
                Assert.DoesNotContain("Storeys", contentResult_Narrow.Content);
                Assert.Contains("100", contentResult_Narrow.Content);

                // One call: the matched addresses with the projected building data of each matched building.
                IActionResult byAddress = await controller.GetByAddressAsync(ByAddressFixture.StreetName, null, null, columns: null, commandTimeout: 600);
                ContentResult contentResult_ByAddress = Assert.IsType<ContentResult>(byAddress);
                JsonArray? jsonArray = JsonNode.Parse(contentResult_ByAddress.Content ?? string.Empty) as JsonArray;
                Assert.NotNull(jsonArray);
                Assert.Equal(2, jsonArray.Count);

                // Rank order: house number 1 before house number 3 of the same street.
                JsonObject? itemWithData = jsonArray[0] as JsonObject;
                JsonObject? itemWithoutData = jsonArray[1] as JsonObject;
                Assert.NotNull(itemWithData);
                Assert.NotNull(itemWithoutData);

                JsonObject? matchWithData = itemWithData["AddressPointMatch"] as JsonObject;
                Assert.NotNull(matchWithData);
                Assert.Equal(ByAddressFixture.Reference_With_Data, matchWithData["Reference"]?.GetValue<string>());
                Assert.Equal(ByAddressFixture.CountyId_Data, matchWithData["CountyId"]?.GetValue<int>());

                JsonObject? buildingData_With = itemWithData["BuildingData"] as JsonObject;
                Assert.NotNull(buildingData_With);
                Assert.Equal(ByAddressFixture.CountyId_Data, buildingData_With["county_id"]?.GetValue<int>());
                Assert.Equal(ByAddressFixture.Reference_With_Data, buildingData_With["reference"]?.GetValue<string>());
                Assert.Equal(ByAddressFixture.FloorArea, buildingData_With["floor_area"]?.GetValue<double>());
                Assert.Equal(ByAddressFixture.Storeys, buildingData_With["storeys"]?.GetValue<int>());

                // The default columns the scratch table does not hold were dropped rather than refused.
                Assert.Null(buildingData_With["total_area"]);
                Assert.Null(buildingData_With["subdivision_name"]);

                JsonObject? matchWithoutData = itemWithoutData["AddressPointMatch"] as JsonObject;
                Assert.NotNull(matchWithoutData);
                Assert.Equal(ByAddressFixture.Reference_Without_Data, matchWithoutData["Reference"]?.GetValue<string>());
                Assert.Equal(ByAddressFixture.CountyId_Data, matchWithoutData["CountyId"]?.GetValue<int>());

                // A building with no building data row answers every projected key as null, in the shape documented.
                JsonObject? buildingData_Without = itemWithoutData["BuildingData"] as JsonObject;
                Assert.NotNull(buildingData_Without);
                Assert.Equal(ByAddressFixture.CountyId_Data, buildingData_Without["county_id"]?.GetValue<int>());
                Assert.Equal("null", buildingData_Without["floor_area"]?.ToJsonString() ?? "null");
                Assert.Equal("null", buildingData_Without["storeys"]?.ToJsonString() ?? "null");

                // The caller's own projection reaches byaddress too, strictly.
                IActionResult byAddress_Narrow = await controller.GetByAddressAsync(ByAddressFixture.StreetName, null, null, columns: ["floor_area"], commandTimeout: 600);
                ContentResult contentResult_ByAddress_Narrow = Assert.IsType<ContentResult>(byAddress_Narrow);
                JsonArray? jsonArray_Narrow = JsonNode.Parse(contentResult_ByAddress_Narrow.Content ?? string.Empty) as JsonArray;
                Assert.NotNull(jsonArray_Narrow);
                Assert.Equal(2, jsonArray_Narrow.Count);

                JsonObject? buildingData_Narrow = (jsonArray_Narrow[0] as JsonObject)?["BuildingData"] as JsonObject;
                Assert.NotNull(buildingData_Narrow);
                Assert.Equal(ByAddressFixture.FloorArea, buildingData_Narrow["floor_area"]?.GetValue<double>());
                Assert.Null(buildingData_Narrow["storeys"]);

                // An unknown column in the caller's projection is refused before anything is read.
                IActionResult byAddress_Unknown = await controller.GetByAddressAsync(ByAddressFixture.StreetName, null, null, columns: ["no_such_column"], commandTimeout: 600);
                ObjectResult objectResult_Unknown_ByAddress = Assert.IsType<ObjectResult>(byAddress_Unknown);
                Assert.Equal(400, objectResult_Unknown_ByAddress.StatusCode);
            }
        }

        /// <summary>
        /// A scratch building-data table and two scratch address points of one street, on the local test database, handed to a live <see cref="BuildingDataController"/> wired to both converters.
        /// <para>Returns <c>null</c> - and the fact returns without asserting - when the conf is absent or the database is unreachable, in which case nothing was written. Seeding that fails after the connection opened fails loudly rather than skipping. Removes the address points, the scratch table and its column metadata on disposal.</para>
        /// </summary>
        private sealed class ByAddressFixture : System.IAsyncDisposable
        {
            /// <summary>The scratch county part that holds the scratch rows.</summary>
            public const int CountyId_Data = 910401;

            /// <summary>The building whose row is stored.</summary>
            public const string Reference_With_Data = "XUNIT-BYADDRESS-BLDG-1";

            /// <summary>The matched building whose row does not exist.</summary>
            public const string Reference_Without_Data = "XUNIT-BYADDRESS-BLDG-NONE";

            /// <summary>The street nothing else in the test database holds.</summary>
            public const string StreetName = "Byaddressowa";

            /// <summary>The stored floor area of the building that has a row.</summary>
            public const double FloorArea = 100.0;

            /// <summary>The stored storey count of the building that has a row.</summary>
            public const int Storeys = 4;

            private readonly ConnectionData connectionData;
            private readonly string path_Watcher;
            private readonly string tableName;
            private readonly GISWebAPIConfigurationFileWatcher watcher;

            /// <summary>Gets the controller wired to the scratch table and the address point table.</summary>
            public BuildingDataController Controller { get; }

            private ByAddressFixture(ConnectionData connectionData, string path_Watcher, string tableName, GISWebAPIConfigurationFileWatcher watcher, BuildingDataController controller)
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
            public static async Task<ByAddressFixture?> TryCreateAsync()
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

                // A clean base: a table left behind by a crashed run must not make these rows appear twice.
                await DiGi.PostgreSQL.Modify.RemoveTableAsync(connectionData, scratchBuildingDataPostgreSQLConverter.TableName);

                Table table = new();
                table.AddColumn(new ExtendedColumn("County Id", typeof(int), null, null));
                table.AddColumn(new ExtendedColumn("Reference", typeof(string), null, null));
                table.AddColumn(new ExtendedColumn("Floor area", typeof(double), null, null));
                table.AddColumn(new ExtendedColumn("Storeys", typeof(int), null, null));
                table.AddRow([CountyId_Data, Reference_With_Data, FloorArea, Storeys]);

                string path_Watcher = ConfigurationFilePath();
                GISWebAPIConfigurationFileWatcher watcher = new(path_Watcher);
                BuildingDataController controller = new(
                    watcher,
                    scratchBuildingDataPostgreSQLConverter,
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AddressPointPostgreSQLConverter(connectionData));

                ByAddressFixture fixture = new(connectionData, path_Watcher, scratchBuildingDataPostgreSQLConverter.TableName, watcher, controller);
                try
                {
                    if (!await scratchBuildingDataPostgreSQLConverter.PushAsync(table))
                    {
                        throw new InvalidOperationException($"PushAsync did not complete for the scratch table {scratchBuildingDataPostgreSQLConverter.TableName}.");
                    }

                    await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                    if (npgsqlConnection is null)
                    {
                        throw new InvalidOperationException("The test database connection could not be built.");
                    }
                    await npgsqlConnection.OpenAsync();

                    // The address points: one whose building has a row, one whose building has none, both of a street
                    // nothing else holds, so the search reaches them without a county filter.
                    if (!await npgsqlConnection.TableAsync_AddressPoint())
                    {
                        throw new InvalidOperationException("The address point table could not be created.");
                    }

                    await DeleteAddressPointsAsync(npgsqlConnection);

                    DateTimeOffset version = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    AddressPoint addressPoint_With = new(new Point2D(-9999999, -9999999), StreetName, null, "1", "Warszawa", null, null, "XUNIT-BYADDRESS-1", version);
                    AddressPoint addressPoint_Without = new(new Point2D(-9999999, -9999999), StreetName, null, "3", "Warszawa", null, null, "XUNIT-BYADDRESS-2", version);

                    AddressPointMatch addressPointMatch_With = new(addressPoint_With, AddressPointMatchType.Inside, Reference_With_Data, Guid.NewGuid(), 0.0, null);
                    AddressPointMatch addressPointMatch_Without = new(addressPoint_Without, AddressPointMatchType.Inside, Reference_Without_Data, Guid.NewGuid(), 0.0, null);

                    AddressPointPostgreSQLConverter addressPointPostgreSQLConverter = new(connectionData);
                    if (await addressPointPostgreSQLConverter.UpdateAsync(CountyId_Data, [addressPointMatch_With, addressPointMatch_Without]) != 2L)
                    {
                        throw new InvalidOperationException("The scratch address points could not be written.");
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
            /// Removes the scratch address points, the scratch table and its column metadata, and deletes the configuration file.
            /// </summary>
            public async ValueTask DisposeAsync()
            {
                await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(this.connectionData))
                {
                    if (npgsqlConnection is not null)
                    {
                        await npgsqlConnection.OpenAsync();
                        await DeleteAddressPointsAsync(npgsqlConnection);
                    }
                }

                await DiGi.PostgreSQL.Modify.RemoveTableAsync(this.connectionData, this.tableName);

                await using (NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(this.connectionData))
                {
                    if (npgsqlConnection_Cleanup is not null)
                    {
                        await npgsqlConnection_Cleanup.OpenAsync();
                        await using NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection_Cleanup);
                        npgsqlCommand.Parameters.AddWithValue("tableName", this.tableName);
                        await npgsqlCommand.ExecuteNonQueryAsync();
                    }
                }

                this.watcher.Dispose();
                File.Delete(this.path_Watcher);
            }

            private static async Task DeleteAddressPointsAsync(NpgsqlConnection npgsqlConnection)
            {
                await using NpgsqlCommand npgsqlCommand = new($"DELETE FROM {DiGi.GIS.PostgreSQL.Constants.TableName.AddressPoint} WHERE reference_id LIKE 'XUNIT-BYADDRESS-%';", npgsqlConnection);
                await npgsqlCommand.ExecuteNonQueryAsync();
            }
        }
    }
}
