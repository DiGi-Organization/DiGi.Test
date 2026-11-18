using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        private const int countyId_CentroidsByCountyId = 990397;
        private const int countyId_CentroidsByCountyId_Sibling = 990398;
        private const int countyId_CentroidsByCountyId_Empty = 990399;

        /// <summary>
        /// Verifies <c>gis/building2d/centroidsbycountyid</c> end to end on the throwaway main test database, for a scratch county part with no Subdivision child.
        /// <para>The part holds a usable row, a row with a NULL bounding-box component, a row with a NaN one and a row with an empty reference (<c>reference</c> is <c>NOT NULL</c>, so an empty one stands for the missing reference); a sibling part holds one more row. The answer is the usable row alone, with the part's id and coordinates at 0.01 m; its reference set equals the server-side solve's read of the same part filtered as the solve filters it; it parses back through <see cref="Convert.ToDiGi_Building2DCentroids"/> under the invariant culture and <c>pl-PL</c>; <c>centroidsbyadministrativeareal2Did</c> does not answer that building - the reason the route exists; and a part with no rows answers four empty arrays.</para>
        /// <para>Medium test (a live database server): the scratch partitions are dropped before and after. Reference: DiGi-Organization/DiGi.GIS.WebAPI#97.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task Building2DController_CentroidsByCountyId_TestDatabase()
        {
            (ConnectionData connectionData_Main, _) = SolarRadiationTestConnectionDatas();

            string path = ConfigurationFilePath();
            await CleanupCentroidsByCountyIdScratchAsync(connectionData_Main);
            try
            {
                await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
                {
                    Assert.NotNull(npgsqlConnection);
                    await npgsqlConnection.OpenAsync();

                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D(npgsqlConnection));
                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D_Partition(npgsqlConnection, countyId_CentroidsByCountyId));
                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D_Partition(npgsqlConnection, countyId_CentroidsByCountyId_Sibling));
                    await ExecuteSolarRadiationAsync(npgsqlConnection, $@"
                        INSERT INTO {PostgreSQL.Constants.TableName.Building2D} (county_id, reference, min_x, min_y, max_x, max_y) VALUES
                            ({countyId_CentroidsByCountyId}, 'XUNIT-97-A', 100, 200, 110.026, 210.018),
                            ({countyId_CentroidsByCountyId}, 'XUNIT-97-NULLBOX', NULL, 200, 110, 210),
                            ({countyId_CentroidsByCountyId}, 'XUNIT-97-NAN', 'NaN', 200, 110, 210),
                            ({countyId_CentroidsByCountyId}, '', 100, 200, 110, 210),
                            ({countyId_CentroidsByCountyId_Sibling}, 'XUNIT-97-Q', 100, 200, 110, 210);");
                }

                Building2DPostgreSQLConverter building2DPostgreSQLConverter = new(connectionData_Main);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                Building2DController building2DController = new(gISWebAPIConfigurationFileWatcher, building2DPostgreSQLConverter, new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main))
                {
                    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
                };

                ContentResult contentResult = Assert.IsType<ContentResult>(await building2DController.GetCentroidsByCountyIdAsync(countyId_CentroidsByCountyId));
                Assert.Equal("application/json", contentResult.ContentType);
                JsonObject? jsonObject = JsonNode.Parse(contentResult.Content!) as JsonObject;
                Assert.NotNull(jsonObject);

                string[] references_Expected = ["XUNIT-97-A"];
                Assert.Equal(references_Expected, jsonObject["References"]!.AsArray().Select(x => x!.GetValue<string>()));
                Assert.Equal([countyId_CentroidsByCountyId], jsonObject["CountyIds"]!.AsArray().Select(x => x!.GetValue<int>()));
                Assert.Equal([105.01], jsonObject["X"]!.AsArray().Select(x => x!.GetValue<double>()));
                Assert.Equal([205.01], jsonObject["Y"]!.AsArray().Select(x => x!.GetValue<double>()));

                // The server-side solve reads the same rows and drops blank references and non-finite centres itself.
                List<Building2DCentroid>? building2DCentroids_Solve = await building2DPostgreSQLConverter.GetBuilding2DCentroidsByCountyIdAsync(countyId_CentroidsByCountyId);
                Assert.NotNull(building2DCentroids_Solve);
                Assert.Equal(2, building2DCentroids_Solve.Count);
                List<string> references_Solve = [.. building2DCentroids_Solve.Where(x => !string.IsNullOrWhiteSpace(x.Reference) && double.IsFinite(x.X) && double.IsFinite(x.Y)).Select(x => x.Reference!).OrderBy(x => x, StringComparer.Ordinal)];
                Assert.Equal(references_Expected, references_Solve);

                CultureInfo cultureInfo = CultureInfo.CurrentCulture;
                try
                {
                    foreach (string name in new string[] { string.Empty, "pl-PL" })
                    {
                        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

                        List<Building2DCentroid>? building2DCentroids = (JsonNode.Parse(contentResult.Content!) as JsonObject).ToDiGi_Building2DCentroids();
                        Assert.NotNull(building2DCentroids);
                        Building2DCentroid building2DCentroid = Assert.Single(building2DCentroids);
                        Assert.Equal("XUNIT-97-A", building2DCentroid.Reference);
                        Assert.Equal(countyId_CentroidsByCountyId, building2DCentroid.CountyId);
                        Assert.Equal(105.01, building2DCentroid.X);
                        Assert.Equal(205.01, building2DCentroid.Y);
                    }
                }
                finally
                {
                    CultureInfo.CurrentCulture = cultureInfo;
                }

                // The route by administrative area resolves the part through its Subdivision children; this part has none.
                IActionResult actionResult_AdministrativeAreal2D = await building2DController.GetCentroidsByAdministrativeAreal2DIdAsync(countyId_CentroidsByCountyId);
                if (actionResult_AdministrativeAreal2D is ContentResult contentResult_AdministrativeAreal2D)
                {
                    Assert.DoesNotContain("XUNIT-97-A", contentResult_AdministrativeAreal2D.Content, StringComparison.Ordinal);
                }
                else
                {
                    Assert.IsType<NotFoundResult>(actionResult_AdministrativeAreal2D);
                }

                ContentResult contentResult_Empty = Assert.IsType<ContentResult>(await building2DController.GetCentroidsByCountyIdAsync(countyId_CentroidsByCountyId_Empty));
                Assert.Equal("{\"References\":[],\"CountyIds\":[],\"X\":[],\"Y\":[]}", contentResult_Empty.Content);
            }
            finally
            {
                System.IO.File.Delete(path);
                await CleanupCentroidsByCountyIdScratchAsync(connectionData_Main);
            }
        }

        /// <summary>
        /// Verifies that <c>gis/building2d/centroidsbycountyid</c> answers 404 - not 500, not four empty arrays - when the <c>building_2d</c> table does not exist (<c>Coding - PostgreSQL.md</c>: a missing table answers 404).
        /// <para>The converter is pointed at the throwaway storage test database, which holds no <c>building_2d</c>; the fact skips when it does. Medium test (a live database server). Reference: DiGi-Organization/DiGi.GIS.WebAPI#97.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task Building2DController_CentroidsByCountyId_MissingTable()
        {
            (_, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage))
            {
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();

                await using NpgsqlCommand npgsqlCommand = new($"SELECT to_regclass('{PostgreSQL.Constants.TableName.Building2D}') IS NULL;", npgsqlConnection);
                Skip.IfNot(await npgsqlCommand.ExecuteScalarAsync() is true, $"The storage test database holds a {PostgreSQL.Constants.TableName.Building2D} table, so it cannot stand for a missing one.");
            }

            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                Building2DController building2DController = new(gISWebAPIConfigurationFileWatcher, new Building2DPostgreSQLConverter(connectionData_Storage), new AdministrativeAreal2DPostgreSQLConverter(null))
                {
                    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
                };

                Assert.IsType<NotFoundResult>(await building2DController.GetCentroidsByCountyIdAsync(countyId_CentroidsByCountyId));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Drops the scratch <c>building_2d</c> partitions of the <c>centroidsbycountyid</c> facts on the main test database. Called before writing and again in <c>finally</c>.
        /// </summary>
        /// <param name="connectionData_Main">The main test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task CleanupCentroidsByCountyIdScratchAsync(ConnectionData connectionData_Main)
        {
            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();
            foreach (int countyId in new int[] { countyId_CentroidsByCountyId, countyId_CentroidsByCountyId_Sibling, countyId_CentroidsByCountyId_Empty })
            {
                await ExecuteSolarRadiationAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.Building2D}_{countyId};");
            }
        }
    }
}
