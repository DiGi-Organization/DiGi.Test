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
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// How many shuffled request orders <see cref="TerrainController_DensitiesByCountyIds_ParallelDeterminism"/> runs the densities read with (DiGi-Organization/DiGi.GIS.WebAPI#105).
        /// </summary>
        private const int densities_ShuffledRuns = 10;

        /// <summary>
        /// The seed of the shuffled request orders, so a failure reproduces with the same sequence of orders (DiGi-Organization/DiGi.GIS.WebAPI#105).
        /// </summary>
        private const int densities_ShuffledSeed = 20261121;

        /// <summary>
        /// Verifies that <c>gis/terrain/densitiesbycountyids</c> answers the same figures in the request order under every order of its parts, once the parts are measured in parallel (DiGi-Organization/DiGi.GIS.WebAPI#105).
        /// <para>The fixture is the sibling fact's, under its reference prefix: county A is one 1000 m square tiled by two half subdivisions with a nested and a coinciding duplicate, county B is two parts sharing one code, county D is a 1000 m square covered 60 % by its subdivision, county C has no subdivision. Each run shuffles the seven identifiers (a seed, so a failure repeats), sends them, and requires the response to carry exactly the counties that produce a figure, in that run's order, with the analytic areas of the sibling fact to a 1e-9 relative tolerance and their stored counts - a worker writing the wrong slot, an order slip or a figure moved by a concurrent reader fails it.</para>
        /// <para>Closes with the same controller asked under an already-canceled token: cancellation must leave as an <see cref="OperationCanceledException"/>, never as a status a caller could read as an answer.</para>
        /// <para>Medium test (a live database server): the scratch rows and terrain partitions are removed before the write and in <c>finally</c>.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task TerrainController_DensitiesByCountyIds_ParallelDeterminism()
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

                Dictionary<int, double> areas_Expected = new() { [countyId_A] = 1000000, [countyId_B1] = 1000000, [countyId_B2] = 250000, [countyId_C] = 160000, [countyId_D] = 600000 };
                List<int> countyIds = [countyId_B2, countyId_A, subdivisionId_A1, countyId_D, countyId_B1, countyId_C, int.MaxValue];

                Random random = new(densities_ShuffledSeed);
                for (int run = 0; run < densities_ShuffledRuns; run++)
                {
                    List<int> request = [.. countyIds];
                    for (int i = request.Count - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        (request[i], request[j]) = (request[j], request[i]);
                    }

                    ContentResult contentResult = Assert.IsType<ContentResult>(await terrainController.GetDensitiesByCountyIdsAsync(request, 100));
                    Assert.Equal("application/json", contentResult.ContentType);

                    List<TerrainPointDensityResult> terrainPointDensityResults = [];
                    foreach (JsonNode? jsonNode in Assert.IsType<JsonArray>(JsonNode.Parse(contentResult.Content!)))
                    {
                        terrainPointDensityResults.Add(new TerrainPointDensityResult(Assert.IsType<JsonObject>(jsonNode)));
                    }

                    // The counties that produce a figure, in the order this run asked for them - a subdivision
                    // identifier and an unknown identifier are asked about and never reported.
                    List<int> countyIds_Expected = [.. request.Where(x => areas_Expected.ContainsKey(x))];
                    Assert.Equal(countyIds_Expected, terrainPointDensityResults.Select(x => x.CountyId));

                    Assert.Equal(areas_Expected.Count, terrainPointDensityResults.Count);
                    foreach (TerrainPointDensityResult terrainPointDensityResult in terrainPointDensityResults)
                    {
                        double area_Expected = areas_Expected[terrainPointDensityResult.CountyId];
                        Assert.True(Math.Abs(terrainPointDensityResult.Area - area_Expected) <= area_Expected * 1e-9, string.Format(CultureInfo.InvariantCulture, "Run {0}, county {1}: Area {2}, expected the sampled area {3}.", run, terrainPointDensityResult.CountyId, terrainPointDensityResult.Area, area_Expected));

                        counts_ByCountyId.TryGetValue(terrainPointDensityResult.CountyId, out long count);
                        Assert.Equal(count, terrainPointDensityResult.Count);
                    }
                }

                // Cancellation still leaves through the action's own chain as an exception, never as a status a
                // caller could read as an answer (the parallel restructure must not swallow it either).
                using CancellationTokenSource cancellationTokenSource = new();
                cancellationTokenSource.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => terrainController.GetDensitiesByCountyIdsAsync([countyId_A], 100, cancellationToken: cancellationTokenSource.Token));
            }
            finally
            {
                await CleanupDensitiesScratchAsync(connectionData_Main, connectionData_Storage);
            }
        }
    }
}
