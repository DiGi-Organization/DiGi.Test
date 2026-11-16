using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
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
        /// Verifies <c>POST gis/BuildingData/tablebybuildingdatabypagingparameter</c> in physical order end to end, through the real action over building data the fact pushes itself (DiGi.GIS.WebAPI#40).
        /// <para><b>Physical order.</b> A walk of a 30-row part in 4-row pages, following <c>DiGi-Next-Cursor</c>, returns every row once and never a row of the sibling part. It takes 8 requests, every one but the last carrying the header.</para>
        /// <para><b>Fallback.</b> <c>PhysicalOrder</c> with a <c>Cursor</c> that is a reference rather than a physical position answers the reference-ordered page after that reference, without the header. That is what a client gets from a server older than PostgreSQL 14, and what lets it page on by the last row's reference.</para>
        /// <para><b>Default.</b> A request without the flag is answered exactly as before: reference order, no header.</para>
        /// <para>The data goes into a scratch table through <see cref="ScratchBuildingDataPostgreSQLConverter"/> on the local test database named by <c>DiGi.Test/user files/GIS_PostgreSQL_Main.conf</c>. The fact returns without asserting when that conf is absent, and removes the scratch table and its column metadata in <c>finally</c>.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_PhysicalOrder()
        {
            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(Assembly.GetExecutingAssembly());
            string? path_Conf = directory_UserFiles is null ? null : Path.Combine(directory_UserFiles, "GIS_PostgreSQL_Main.conf");
            if (path_Conf is null || !File.Exists(path_Conf))
            {
                return;
            }

            ConnectionData? connectionData = DiGi.PostgreSQL.Create.ConnectionData(DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(path_Conf));
            Assert.NotNull(connectionData);

            ScratchBuildingDataPostgreSQLConverter scratchBuildingDataPostgreSQLConverter = new(connectionData);

            const int countyId_A = 900011;
            const int countyId_B = 900012;

            Table table = new();
            table.AddColumn(new ExtendedColumn("Reference", typeof(string), null, null));
            table.AddColumn(new ExtendedColumn("County Id", typeof(int), null, null));
            table.AddColumn(new ExtendedColumn("Floor area", typeof(double), null, null));
            table.AddColumn(new ExtendedColumn("Scratch padding", typeof(string), null, null));

            for (int i = 0; i < 30; i++)
            {
                table.AddRow([$"SCRATCH-A-{i:D3}", countyId_A, 100.0 + i, Padding()]);
            }

            for (int i = 0; i < 5; i++)
            {
                table.AddRow([$"SCRATCH-B-{i:D3}", countyId_B, 200.0 + i, Padding()]);
            }

            string path_Watcher = ConfigurationFilePath();

            try
            {
                Assert.True(await scratchBuildingDataPostgreSQLConverter.PushAsync(table));

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path_Watcher);
                BuildingDataController controller = new(gISWebAPIConfigurationFileWatcher, scratchBuildingDataPostgreSQLConverter, new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null), new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));

                // Physical order, following the header.
                HashSet<string> references = [];
                string? cursor = null;
                int requests = 0;
                while (true)
                {
                    (List<string> references_Page, string? header) = await RequestAsync(new() { CountyId = countyId_A, ColumnUniqueIds = ["floor_area"], PageSize = 4, Cursor = cursor, PhysicalOrder = true });
                    requests++;

                    foreach (string reference in references_Page)
                    {
                        Assert.StartsWith("SCRATCH-A-", reference);
                        Assert.True(references.Add(reference), $"{reference} was returned twice.");
                    }

                    if (header is null)
                    {
                        break;
                    }

                    Assert.Equal(4, references_Page.Count);
                    Assert.Matches(@"^\(\d+,\d+\)$", header);
                    cursor = header;
                    Assert.True(requests < 100, "The walk did not end.");
                }

                Assert.Equal(30, references.Count);
                Assert.Equal(8, requests);

                // Fallback: a reference as cursor is not a physical position, so the page comes back in reference order, headerless.
                (List<string> references_Fallback, string? header_Fallback) = await RequestAsync(new() { CountyId = countyId_A, PageSize = 4, Cursor = "SCRATCH-A-009", PhysicalOrder = true });
                Assert.Null(header_Fallback);
                Assert.Equal(["SCRATCH-A-010", "SCRATCH-A-011", "SCRATCH-A-012", "SCRATCH-A-013"], references_Fallback);

                // Default: reference order, headerless, as before the flag existed.
                (List<string> references_Default, string? header_Default) = await RequestAsync(new() { CountyId = countyId_A, PageSize = 4 });
                Assert.Null(header_Default);
                Assert.Equal(["SCRATCH-A-000", "SCRATCH-A-001", "SCRATCH-A-002", "SCRATCH-A-003"], references_Default);

                // A part with no rows: an empty page, no header, in either order.
                (List<string> references_Empty, string? header_Empty) = await RequestAsync(new() { CountyId = 900019, PageSize = 4, PhysicalOrder = true });
                Assert.Empty(references_Empty);
                Assert.Null(header_Empty);

                // One request, its references and the DiGi-Next-Cursor header; a fresh HttpContext each time so no header carries over.
                async Task<(List<string> References, string? Header)> RequestAsync(BuildingDataByPagingParameter buildingDataByPagingParameter)
                {
                    controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                    IActionResult actionResult = await controller.GetTableByBuildingDataByPagingParameterAsync(buildingDataByPagingParameter);
                    ContentResult contentResult = Assert.IsType<ContentResult>(actionResult);

                    JsonObject jsonObject = JsonNode.Parse(contentResult.Content!)!.AsObject();
                    JsonArray columns = jsonObject["Columns"]!.AsArray();
                    int index_Reference = columns.Select((x, i) => (Name: x?["Name"]?.GetValue<string>(), Index: i)).Single(x => x.Name == "Reference").Index;

                    List<string> result = [.. jsonObject["Rows"]!.AsArray().Select(x => x![index_Reference]!.GetValue<string>())];

                    string? header = controller.HttpContext.Response.Headers.TryGetValue(Constants.Header.NextCursor, out Microsoft.Extensions.Primitives.StringValues stringValues) ? stringValues.ToString() : null;
                    return (result, header);
                }
            }
            finally
            {
                File.Delete(path_Watcher);

                await DiGi.PostgreSQL.Modify.RemoveTableAsync(connectionData, scratchBuildingDataPostgreSQLConverter.TableName);

                await using NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection_Cleanup is not null)
                {
                    await npgsqlConnection_Cleanup.OpenAsync();
                    await using NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection_Cleanup);
                    npgsqlCommand.Parameters.AddWithValue("tableName", scratchBuildingDataPostgreSQLConverter.TableName);
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }
            }

            // 1 792 hex characters: under the 2 kB threshold at which PostgreSQL compresses or moves a value out of line,
            // so a handful of rows fill a heap block and a part spans several blocks.
            static string Padding()
            {
                return string.Concat(Enumerable.Range(0, 56).Select(_ => Guid.NewGuid().ToString("N")));
            }
        }
    }
}
