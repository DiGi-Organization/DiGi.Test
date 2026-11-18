using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
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
        /// <summary>
        /// The X coordinate of the scratch origin of the <see cref="TerrainController_DensitiesByCountyIds_TestDatabase"/> fact; far from every other scratch area.
        /// </summary>
        private const double densities_X = -6980000;

        /// <summary>
        /// The Y coordinate of the scratch origin of the <see cref="TerrainController_DensitiesByCountyIds_TestDatabase"/> fact.
        /// </summary>
        private const double densities_Y = -6980000;

        /// <summary>
        /// The reference prefix of every scratch <c>administrative_areal_2d</c> row the <see cref="TerrainController_DensitiesByCountyIds_TestDatabase"/> fact writes.
        /// </summary>
        private const string densities_ReferencePrefix = "XUNIT-98-";

        /// <summary>
        /// Verifies <c>gis/terrain/densitiesbycountyids</c> end to end on the throwaway main and storage test databases: every county part is measured against the area a sampling run samples in it - its own outline as far as the county's subdivisions cover it (DiGi-Organization/DiGi.GIS.WebAPI#98).
        /// <para>County A is one 1000 m square tiled by two half subdivisions, with a 200 m square nested in the first and a duplicate coinciding with the second (as a district coincides with its part of a city) - summing every subdivision measured 1 540 000 m². County B is two parts sharing one code, 1000 m and 500 m squares each tiled by its own subdivision - widening a part to its siblings measured both as 1 250 000 m². County D is a 1000 m square whose one subdivision covers 60 % of it, the water or land outside every subdivision a run never samples - 600 000 m², not the outline. County C has no subdivision and no point and is reported with its whole outline (a 400 m square) and a count of zero; it used to be left out. A subdivision identifier and an unknown identifier are not reported. The answer keeps the request order and its figures equal <see cref="PostgreSQL.Create.TerrainPointDensityResult(int, long, double, double?)"/> of the stored count and the expected area.</para>
        /// <para>Medium test (a live database server): the scratch rows and terrain partitions are removed before the write and in <c>finally</c>.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task TerrainController_DensitiesByCountyIds_TestDatabase()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            await CleanupDensitiesScratchAsync(connectionData_Main, connectionData_Storage);
            try
            {
                int countyId_A;
                int countyId_B1;
                int countyId_B2;
                int countyId_C;
                int countyId_D;
                int subdivisionId_A1;
                await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
                {
                    Assert.NotNull(npgsqlConnection);
                    await npgsqlConnection.OpenAsync();
                    Assert.True(await PostgreSQL.Create.TableAsync_AdministrativeArea2D(npgsqlConnection));

                    countyId_A = await Densities_InsertAsync(npgsqlConnection, "A", "9981", AdministrativeArealType.County, null, 0, 0, 1000, 1000);
                    countyId_B1 = await Densities_InsertAsync(npgsqlConnection, "B1", "9982", AdministrativeArealType.County, null, 2000, 0, 1000, 1000);
                    countyId_B2 = await Densities_InsertAsync(npgsqlConnection, "B2", "9982", AdministrativeArealType.County, null, 4000, 0, 500, 500);
                    countyId_C = await Densities_InsertAsync(npgsqlConnection, "C", "9983", AdministrativeArealType.County, null, 6000, 0, 400, 400);
                    countyId_D = await Densities_InsertAsync(npgsqlConnection, "D", "9984", AdministrativeArealType.County, null, 8000, 0, 1000, 1000);

                    subdivisionId_A1 = await Densities_InsertAsync(npgsqlConnection, "A-S1", "9981011", AdministrativeArealType.Subdivision, countyId_A, 0, 0, 500, 1000);
                    await Densities_InsertAsync(npgsqlConnection, "A-S2", "9981012", AdministrativeArealType.Subdivision, countyId_A, 500, 0, 500, 1000);
                    await Densities_InsertAsync(npgsqlConnection, "A-S1-N", "9981013", AdministrativeArealType.Subdivision, countyId_A, 100, 100, 200, 200);
                    await Densities_InsertAsync(npgsqlConnection, "A-S2-D", "9981014", AdministrativeArealType.Subdivision, countyId_A, 500, 0, 500, 1000);
                    await Densities_InsertAsync(npgsqlConnection, "D-S1", "9984011", AdministrativeArealType.Subdivision, countyId_D, 8000, 0, 600, 1000);
                    await Densities_InsertAsync(npgsqlConnection, "B1-S1", "9982011", AdministrativeArealType.Subdivision, countyId_B1, 2000, 0, 1000, 1000);
                    await Densities_InsertAsync(npgsqlConnection, "B2-S1", "9982021", AdministrativeArealType.Subdivision, countyId_B2, 4000, 0, 500, 500);
                }

                Dictionary<int, long> counts_ByCountyId = new() { [countyId_A] = 100, [countyId_B1] = 100, [countyId_B2] = 25, [countyId_D] = 60 };
                await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage))
                {
                    Assert.NotNull(npgsqlConnection);
                    await npgsqlConnection.OpenAsync();
                    Assert.True(await PostgreSQL.Create.TableAsync_TerrainPoint(npgsqlConnection));

                    foreach (KeyValuePair<int, long> keyValuePair in counts_ByCountyId)
                    {
                        Assert.True(await PostgreSQL.Create.TableAsync_TerrainPoint_Partition(npgsqlConnection, keyValuePair.Key));
                        await ExecuteSolarRadiationAsync(npgsqlConnection, string.Format(CultureInfo.InvariantCulture, "INSERT INTO {0} (county_id, x, y, z) SELECT {1}, {2} + i, {3}, 0 FROM generate_series(1, {4}) AS i;", PostgreSQL.Constants.TableName.TerrainPoint, keyValuePair.Key, densities_X, densities_Y, keyValuePair.Value));
                    }
                }

                TerrainController terrainController = new(new TerrainPointPostgreSQLConverter(connectionData_Storage), new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main))
                {
                    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
                };

                ContentResult contentResult = Assert.IsType<ContentResult>(await terrainController.GetDensitiesByCountyIdsAsync([countyId_B2, countyId_A, subdivisionId_A1, countyId_D, countyId_B1, countyId_C, int.MaxValue], 100));
                Assert.Equal("application/json", contentResult.ContentType);

                List<TerrainPointDensityResult> terrainPointDensityResults = [];
                foreach (JsonNode? jsonNode in Assert.IsType<JsonArray>(JsonNode.Parse(contentResult.Content!)))
                {
                    terrainPointDensityResults.Add(new TerrainPointDensityResult(Assert.IsType<JsonObject>(jsonNode)));
                }

                Dictionary<int, double> areas_Expected = new() { [countyId_A] = 1000000, [countyId_B1] = 1000000, [countyId_B2] = 250000, [countyId_C] = 160000, [countyId_D] = 600000 };
                foreach (TerrainPointDensityResult terrainPointDensityResult in terrainPointDensityResults)
                {
                    double area_Expected = areas_Expected[terrainPointDensityResult.CountyId];
                    Assert.True(Math.Abs(terrainPointDensityResult.Area - area_Expected) <= area_Expected * 1e-9, $"County {terrainPointDensityResult.CountyId}: Area {terrainPointDensityResult.Area.ToString(CultureInfo.InvariantCulture)}, expected the sampled area {area_Expected.ToString(CultureInfo.InvariantCulture)}.");

                    counts_ByCountyId.TryGetValue(terrainPointDensityResult.CountyId, out long count);
                    Assert.Equal(count, terrainPointDensityResult.Count);

                    TerrainPointDensityResult? terrainPointDensityResult_Expected = PostgreSQL.Create.TerrainPointDensityResult(terrainPointDensityResult.CountyId, count, terrainPointDensityResult.Area, 100);
                    Assert.NotNull(terrainPointDensityResult_Expected);
                    Assert.Equal(terrainPointDensityResult_Expected.Density, terrainPointDensityResult.Density);
                    Assert.Equal(terrainPointDensityResult_Expected.SpacingEquivalent, terrainPointDensityResult.SpacingEquivalent);
                    Assert.Equal(terrainPointDensityResult_Expected.ExpectedDensity, terrainPointDensityResult.ExpectedDensity);
                    Assert.Equal(terrainPointDensityResult_Expected.Completeness, terrainPointDensityResult.Completeness);
                }

                Assert.Equal([countyId_B2, countyId_A, countyId_D, countyId_B1, countyId_C], terrainPointDensityResults.Select(x => x.CountyId));
            }
            finally
            {
                await CleanupDensitiesScratchAsync(connectionData_Main, connectionData_Storage);
            }
        }

        /// <summary>
        /// Removes the scratch rows of the <see cref="TerrainController_DensitiesByCountyIds_TestDatabase"/> fact: the terrain partitions of its county parts on the storage test database, then its <c>administrative_areal_2d</c> rows on the main test database. Called before writing and again in <c>finally</c>.
        /// </summary>
        /// <param name="connectionData_Main">The main test database.</param>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task CleanupDensitiesScratchAsync(ConnectionData connectionData_Main, ConnectionData connectionData_Storage)
        {
            await using NpgsqlConnection? npgsqlConnection_Main = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection_Main);
            await npgsqlConnection_Main.OpenAsync();
            Assert.True(await PostgreSQL.Create.TableAsync_AdministrativeArea2D(npgsqlConnection_Main));

            List<int> countyIds = [];
            await using (NpgsqlCommand npgsqlCommand = new($"SELECT id FROM {PostgreSQL.Constants.TableName.AdministrativeAreal2D} WHERE reference LIKE '{densities_ReferencePrefix}%' AND type_id = {(int)AdministrativeArealType.County};", npgsqlConnection_Main))
            {
                await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
                while (await npgsqlDataReader.ReadAsync())
                {
                    countyIds.Add(npgsqlDataReader.GetInt32(0));
                }
            }

            await using (NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage))
            {
                Assert.NotNull(npgsqlConnection_Storage);
                await npgsqlConnection_Storage.OpenAsync();
                foreach (int countyId in countyIds)
                {
                    await ExecuteSolarRadiationAsync(npgsqlConnection_Storage, $"DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.TerrainPoint}_{countyId};");
                }
            }

            await ExecuteSolarRadiationAsync(npgsqlConnection_Main, $"DELETE FROM {PostgreSQL.Constants.TableName.AdministrativeAreal2D} WHERE reference LIKE '{densities_ReferencePrefix}%';");
        }

        /// <summary>
        /// Inserts one scratch <c>administrative_areal_2d</c> row of the <see cref="TerrainController_DensitiesByCountyIds_TestDatabase"/> fact: a rectangle relative to the scratch origin, a county storing an <see cref="GIS.Classes.AdministrativeDivision"/> or a subdivision storing an <see cref="GIS.Classes.AdministrativeSubdivision"/> filed under the given county part.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="suffix">The part of the reference after the scratch prefix.</param>
        /// <param name="code">The code of the row.</param>
        /// <param name="administrativeArealType">The type the row is stored under: county or subdivision.</param>
        /// <param name="countyId">The county part a subdivision is filed under; null for a county.</param>
        /// <param name="x">The X offset of the lower left corner from the scratch origin.</param>
        /// <param name="y">The Y offset of the lower left corner from the scratch origin.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        /// <returns>The identifier of the inserted row.</returns>
        private static async Task<int> Densities_InsertAsync(NpgsqlConnection npgsqlConnection, string suffix, string code, AdministrativeArealType administrativeArealType, int? countyId, double x, double y, double width, double height)
        {
            string reference = densities_ReferencePrefix + suffix;
            double x_Min = densities_X + x;
            double y_Min = densities_Y + y;
            Polygon2D polygon2D = new([new Point2D(x_Min, y_Min), new Point2D(x_Min + width, y_Min), new Point2D(x_Min + width, y_Min + height), new Point2D(x_Min, y_Min + height)]);
            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(polygonalFace2D);

            GIS.Classes.AdministrativeAreal2D administrativeAreal2D = administrativeArealType == AdministrativeArealType.Subdivision
                ? new GIS.Classes.AdministrativeSubdivision(Guid.NewGuid(), reference, code, polygonalFace2D, GIS.Enums.AdministrativeSubdivisionType.part_of_city, reference, null)
                : new GIS.Classes.AdministrativeDivision(Guid.NewGuid(), reference, code, polygonalFace2D, GIS.Enums.AdministrativeDivisionType.county, reference);
            string? objectJson = administrativeAreal2D.ToJsonObject()?.ToJsonString();
            Assert.NotNull(objectJson);

            string commandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO {0} (reference, code, name, type_id, min_x, min_y, max_x, max_y, county_id, object) VALUES ('{1}', '{2}', '{1}', {3}, {4}, {5}, {6}, {7}, {8}, '{9}') RETURNING id;",
                PostgreSQL.Constants.TableName.AdministrativeAreal2D,
                reference,
                code,
                (int)administrativeArealType,
                x_Min,
                y_Min,
                x_Min + width,
                y_Min + height,
                countyId.HasValue ? countyId.Value.ToString(CultureInfo.InvariantCulture) : "NULL",
                objectJson);

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            object? result = await npgsqlCommand.ExecuteScalarAsync();
            Assert.NotNull(result);
            return System.Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
    }
}
