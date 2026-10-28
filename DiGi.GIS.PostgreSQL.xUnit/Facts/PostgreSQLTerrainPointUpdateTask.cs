using DiGi.GIS.PostgreSQL.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Samples one county end to end, then samples it again and checks that the second run stores nothing.
        /// <para>Skipped by default: it needs the PostgreSQL configuration files beside the test assembly and it calls the live GUGiK elevation service. Set <c>countyId</c> to a small county before running it, and expect the first run to take as long as one request per point allows.</para>
        /// <para>The second run is the point of the fact. It stores nothing only if the grid, the tiling and the reading back of what is already stored all agree - a grid that is anchored differently on the second run, a tile boundary that overlaps its neighbour, or a stored point that is not recognised as a node would each show up here as points written twice.</para>
        /// </summary>
        [Fact(Skip = "Requires the PostgreSQL configuration files and the live GUGiK elevation service.")]
        public async Task PostgreSQLTerrainPointUpdateTask_Integration()
        {
            // Set to a small county before running. Sampling a large one at this grid size takes hours.
            int countyId = 2405;
            double gridSize = 100;

            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager();
            Assert.NotNull(gISPostgreSQLConverterManager);

            using HttpClient httpClient = new();

            long pointCount_First = await RunAsync(gISPostgreSQLConverterManager, httpClient, countyId, gridSize);
            long pointCount_Second = await RunAsync(gISPostgreSQLConverterManager, httpClient, countyId, gridSize);

            Assert.True(pointCount_First > 0, "The first run should store points.");
            Assert.Equal(0, pointCount_Second);

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            File.WriteAllText(Path.Combine(directory_Reports!, "TerrainPointUpdate.txt"), $"County {countyId} at grid size {gridSize}: first run stored {pointCount_First} points, second run stored {pointCount_Second}.");
        }

        /// <summary>
        /// Samples a county held in several polygon parts, named by one of its parts, and checks that each partition holds only the territory of its own part.
        /// <para>Skipped by default: it needs the PostgreSQL configuration files beside the test assembly and it calls the live GUGiK elevation service.</para>
        /// <para>This is the reproduction of DiGi.GIS.PostgreSQL#66. The task used to hand every part of a multi-part county the whole county's subdivisions, sample the whole county bounding box and file the result under the part it was walking - so every partition held the entire county: identical counts, identical extents, disjoint write windows. Here 2405 (będziński) is sampled by naming 76984 alone; if the two partitions' extents come back the same, the county was stored once per part again.</para>
        /// </summary>
        [Fact(Skip = "Requires the PostgreSQL configuration files and the live GUGiK elevation service.")]
        public async Task PostgreSQLTerrainPointUpdateTask_CountyPartRouting()
        {
            // 2405 (będziński) is held as two parts: 76984 and 76989. Naming one of them must reach both.
            List<int> countyIds = [76984, 76989];
            double gridSize = 100;

            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager();
            Assert.NotNull(gISPostgreSQLConverterManager);

            TerrainPointPostgreSQLConverter? terrainPointPostgreSQLConverter = gISPostgreSQLConverterManager!.GetPostgreSQLConverter<TerrainPointPostgreSQLConverter>();
            Assert.NotNull(terrainPointPostgreSQLConverter);

            using HttpClient httpClient = new();

            long pointCount = await RunAsync(gISPostgreSQLConverterManager!, httpClient, 76984, gridSize);
            Assert.True(pointCount > 0, "The first run should store points.");

            List<TerrainPointCountyResult>? terrainPointCountyResults = await terrainPointPostgreSQLConverter!.GetSummariesByCountyIdsAsync(countyIds);
            Assert.NotNull(terrainPointCountyResults);

            List<TerrainPointCountyResult> terrainPointCountyResults_Local = terrainPointCountyResults;
            Assert.Equal(2, terrainPointCountyResults_Local.Count);

            // The parts are disjoint pieces of one county, so a partition that reports the other part's
            // extent alongside its own is holding territory filed under the wrong part. The duplicated
            // past reported the same six numbers for every part of the code.
            Assert.NotEqual(terrainPointCountyResults_Local[0].MinX, terrainPointCountyResults_Local[1].MinX);
            Assert.NotEqual(terrainPointCountyResults_Local[0].MaxX, terrainPointCountyResults_Local[1].MaxX);
            Assert.NotEqual(terrainPointCountyResults_Local[0].MinY, terrainPointCountyResults_Local[1].MinY);
            Assert.NotEqual(terrainPointCountyResults_Local[0].MaxY, terrainPointCountyResults_Local[1].MaxY);

            // Every part received its own territory, so walking the sibling stores nothing.
            long pointCount_Second = await RunAsync(gISPostgreSQLConverterManager!, httpClient, 76989, gridSize);
            Assert.Equal(0, pointCount_Second);

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            File.WriteAllText(Path.Combine(directory_Reports!, "TerrainPointUpdate_CountyPartRouting.txt"), $"County 2405 at grid size {gridSize}: run over 76984 stored {pointCount} points, run over 76989 stored {pointCount_Second}; partitions 76984 and 76989 report extents {terrainPointCountyResults_Local[0].MinX}-{terrainPointCountyResults_Local[0].MaxX} x {terrainPointCountyResults_Local[0].MinY}-{terrainPointCountyResults_Local[0].MaxY} and {terrainPointCountyResults_Local[1].MinX}-{terrainPointCountyResults_Local[1].MaxX} x {terrainPointCountyResults_Local[1].MinY}-{terrainPointCountyResults_Local[1].MaxY}.");
        }

        /// <summary>
        /// Runs the task once over one county and returns the number of points it stored.
        /// </summary>
        /// <param name="gISPostgreSQLConverterManager">The converter manager holding the connections.</param>
        /// <param name="httpClient">The HTTP client used to reach the elevation service.</param>
        /// <param name="countyId">The county to sample.</param>
        /// <param name="gridSize">The spacing of the sampling grid.</param>
        /// <returns>The number of points stored.</returns>
        private static async Task<long> RunAsync(GISPostgreSQLConverterManager gISPostgreSQLConverterManager, HttpClient httpClient, int countyId, double gridSize)
        {
            PostgreSQLTerrainPointUpdateTask postgreSQLTerrainPointUpdateTask = new(httpClient, gISPostgreSQLConverterManager)
            {
                PostgreSQLTerrainPointUpdateOptions = new PostgreSQLTerrainPointUpdateOptions()
                {
                    CountyIds = [countyId],
                    GridSize = gridSize
                }
            };

            // The base class starts the work without handing back anything to await, so completion is taken from the event it raises.
            TaskCompletionSource<bool> taskCompletionSource = new();
            postgreSQLTerrainPointUpdateTask.Stopped += (object? sender, EventArgs e) => taskCompletionSource.TrySetResult(true);

            postgreSQLTerrainPointUpdateTask.Start();

            await taskCompletionSource.Task;

            Assert.Null(postgreSQLTerrainPointUpdateTask.Exception);
            Assert.Equal(0, postgreSQLTerrainPointUpdateTask.FailedBatchCount);
            Assert.Equal(0, postgreSQLTerrainPointUpdateTask.UnresolvedPointCount);
            Assert.True(postgreSQLTerrainPointUpdateTask.IsSucceeded);

            return postgreSQLTerrainPointUpdateTask.PointCount;
        }
    }
}
