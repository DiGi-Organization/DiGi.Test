using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.IO.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The number of synthetic footprints the parallel facts stream: several batches of the import's conversion, so the workers really run side by side.
        /// </summary>
        private const int count_Parallel_Footprints = 25000;

        /// <summary>
        /// Verifies that the open building import stores, counts, samples and registers the same at any degree of parallelism (DiGi.GIS.PostgreSQL#170): the conversion runs on several workers, and the batches are consumed in read order by one writer.
        /// <para>Two kinds of input, each imported at degree one and at degree four into the throwaway Storage database: a synthetic stream of <see cref="count_Parallel_Footprints"/> footprints - EPSG:4326 and EPSG:3035 squares across Poland, multi-face collections, null footprints and footprints outside the country - and the committed sample of every reader (DBSM, EUBUCCO, Overture, Microsoft, GHS-OBAT as CSV and GeoPackage, OSM PBF) read through the task's own <see cref="PostgreSQLOpenBuildingImportTask.Footprints"/>. The counties are a grid of sixteen 200 km squares over Poland, held in memory as the task would hand them over, with every other square in scope, so the runs file, filter and reject.</para>
        /// <para>Per input the two runs agree on the rows stored (county, source identifier and object, compared as sorted lists), the stored, out-of-scope and rejected counts, the capped rejection sample in order, the per-rejection-reason tally (DiGi.GIS.PostgreSQL#172), and the register's attribute list. The scratch partitions and register rows are dropped in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuildingImport_ParallelParity()
        {
            (ConnectionData _, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection_Storage);
            await npgsqlConnection_Storage.OpenAsync();
            Assert.True(await OpenBuildingPostgreSQLConverter.CreateTablesAsync(npgsqlConnection_Storage), "the open building tables could not be created on the throwaway Storage test database.");

            (List<AdministrativeAreal2D> counties, HashSet<int> countyIds_Scope) = OpenBuildingParallel_Counties();
            List<string> versions = [];
            HashSet<string> datasetKeys = [];

            OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Storage);

            async Task ParityAsync(string datasetKey, Func<OpenBuildingSourcePosition, IAsyncEnumerable<Footprint>> footprints, bool expectRows)
            {
                datasetKeys.Add(datasetKey);

                List<(OpenBuildingImportResult Result, List<string> Rows, string? AttributeNames)> runs = [];
                foreach (int maxDegreeOfParallelism in new int[] { 1, 4 })
                {
                    string version = $"TST-{Guid.NewGuid():N}";
                    versions.Add(version);

                    OpenBuildingSourcePosition openBuildingSourcePosition = new();
                    OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(
                        footprints(openBuildingSourcePosition),
                        new BuildingDatasetRelease(datasetKey, version, DateTime.Today),
                        countyIds_Scope,
                        counties,
                        sourcePosition: openBuildingSourcePosition,
                        maxDegreeOfParallelism: maxDegreeOfParallelism,
                        cancellationToken: default);

                    Assert.NotNull(openBuildingImportResult);
                    Assert.NotNull(openBuildingImportResult.DatasetId);

                    runs.Add((openBuildingImportResult, await OpenBuildingParallel_RowsAsync(npgsqlConnection_Storage, datasetKey, openBuildingImportResult.DatasetId.Value), (await ScalarAsync(npgsqlConnection_Storage, $"SELECT attribute_names::text FROM {Constants.TableName.BuildingDataset} WHERE id = {openBuildingImportResult.DatasetId.Value};"))?.ToString()));
                }

                (OpenBuildingImportResult result_1, List<string> rows_1, string? attributeNames_1) = runs[0];
                (OpenBuildingImportResult result_4, List<string> rows_4, string? attributeNames_4) = runs[1];

                if (expectRows)
                {
                    Assert.True(result_1.RowCount > 0, $"{datasetKey}: the run stored nothing, so the comparison proves nothing.");
                }

                Assert.Equal(result_1.RowCount, result_4.RowCount);
                Assert.Equal(result_1.FilteredCount, result_4.FilteredCount);
                Assert.Equal(result_1.RejectionCount, result_4.RejectionCount);
                Assert.Equal(result_1.RowCount, rows_1.Count);
                Assert.Equal(rows_1, rows_4);
                Assert.Equal(attributeNames_1, attributeNames_4);
                Assert.Equal(result_1.Rejections.Select(x => (x.Reference, x.UpdateRejectionReason)), result_4.Rejections.Select(x => (x.Reference, x.UpdateRejectionReason)));

                // The tally is kept by the consuming writer alone, so it cannot differ by degree either;
                // sorted by reason, because the enumeration order of the two dictionaries is not the contract.
                Assert.Equal(
                    result_1.RejectionCounts.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)),
                    result_4.RejectionCounts.OrderBy(x => x.Key).Select(x => (x.Key, x.Value)));
            }

            try
            {
                // The synthetic stream: rows in and out of scope, rejections of every kind, several batches.
                await ParityAsync("eubucco", _ => OpenBuildingParallel_Footprints(count_Parallel_Footprints, null, default), true);

                // Every reader, through the task's own concatenated stream.
                Assembly assembly = Assembly.GetExecutingAssembly();
                (string DatasetKey, string FileName)[] fixtures =
                [
                    ("dbsm", "DBSM_Sample.gpkg"),
                    ("eubucco", "EUBUCCO_Sample.gpkg"),
                    ("overture", "Overture_Sample.parquet"),
                    ("microsoft", "Microsoft_Sample.csv.gz"),
                    ("ghs_obat", "GHS_OBAT_Sample.csv"),
                    ("ghs_obat", "GHS_OBAT_Sample.gpkg"),
                    ("osm", "OSM_Buildings_Sample.osm.pbf"),
                ];

                foreach ((string datasetKey, string fileName) in fixtures)
                {
                    string? path = Core.xUnit.Query.FilePath(assembly, fileName);
                    Assert.NotNull(path);

                    string? tableName = Query.OpenBuildingTableName(datasetKey);
                    Assert.NotNull(tableName);

                    PostgreSQLOpenBuildingImportTaskOptions options = new()
                    {
                        DatasetKey = datasetKey,
                        SourcePaths = [path],
                    };

                    Dictionary<string, string>? quadkeys = datasetKey == "microsoft" ? new() { [path] = "120213101" } : null;

                    await ParityAsync(datasetKey, openBuildingSourcePosition => PostgreSQLOpenBuildingImportTask.Footprints(tableName, options, quadkeys, [], openBuildingSourcePosition, default)!, false);
                }
            }
            finally
            {
                await OpenBuildingParallel_CleanupAsync(npgsqlConnection_Storage, datasetKeys, countyIds_Scope, versions);
            }
        }

        /// <summary>
        /// Verifies that stopping a parallel open building import leaves the previous snapshot exactly as it was, and that a worker failing mid-run fails the run without committing anything (DiGi.GIS.PostgreSQL#170).
        /// <para>A first release is imported whole. A second release of the same scope is then started at degree four twice: once with a stream that cancels the run after three batches - the import throws <see cref="OperationCanceledException"/> - and once with a footprint whose attribute cannot be serialized, which throws inside a worker - the import answers null. After each, the partitions hold the first release's rows, row for row, and the second release has no register row. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuildingImport_ParallelStopAndFailure()
        {
            (ConnectionData _, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection_Storage);
            await npgsqlConnection_Storage.OpenAsync();
            Assert.True(await OpenBuildingPostgreSQLConverter.CreateTablesAsync(npgsqlConnection_Storage), "the open building tables could not be created on the throwaway Storage test database.");

            (List<AdministrativeAreal2D> counties, HashSet<int> countyIds_Scope) = OpenBuildingParallel_Counties();
            string version_1 = $"TST-{Guid.NewGuid():N}";
            string version_2 = $"TST-{Guid.NewGuid():N}";

            OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Storage);

            try
            {
                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(
                    OpenBuildingParallel_Footprints(5000, null, default),
                    new BuildingDatasetRelease("eubucco", version_1, DateTime.Today),
                    countyIds_Scope,
                    counties,
                    maxDegreeOfParallelism: 4,
                    cancellationToken: default);

                Assert.NotNull(openBuildingImportResult);
                Assert.True(openBuildingImportResult.RowCount > 0);

                long rowCount_Before = (long)(await ScalarAsync(npgsqlConnection_Storage, $"SELECT count(*) FROM {Constants.TableName.EUBUCCO_Building2D} WHERE county_id = ANY(ARRAY[{string.Join(",", countyIds_Scope)}]);"))!;
                List<string> rows_Before = await OpenBuildingParallel_RowsAsync(npgsqlConnection_Storage, "eubucco", openBuildingImportResult.DatasetId!.Value);
                Assert.Equal(openBuildingImportResult.RowCount, rowCount_Before);

                // A stop three batches in: the transaction rolls back, truncation included.
                using CancellationTokenSource cancellationTokenSource = new();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => openBuildingPostgreSQLConverter.ImportAsync(
                    OpenBuildingParallel_Footprints(count_Parallel_Footprints, (6000, cancellationTokenSource), cancellationTokenSource.Token),
                    new BuildingDatasetRelease("eubucco", version_2, DateTime.Today),
                    countyIds_Scope,
                    counties,
                    maxDegreeOfParallelism: 4,
                    cancellationToken: cancellationTokenSource.Token));

                Assert.Equal(rows_Before, await OpenBuildingParallel_RowsAsync(npgsqlConnection_Storage, "eubucco", openBuildingImportResult.DatasetId.Value));
                Assert.Equal(0L, await OpenBuildingPostgreSQLConverter.GetReleaseCountAsync(npgsqlConnection_Storage, "eubucco", version_2, DateTime.Today));

                // A worker that throws: the footprint's attribute is a number JSON cannot write, which fails only
                // when the converted row is serialized - inside the worker.
                async IAsyncEnumerable<Footprint> Failing([EnumeratorCancellation] CancellationToken cancellationToken = default)
                {
                    await foreach (Footprint footprint in OpenBuildingParallel_Footprints(7000, null, cancellationToken))
                    {
                        yield return footprint;
                    }

                    Footprint? footprint_Failing = OpenBuildingParallel_Footprint("xunit-parallel-failing", 4326, 19.0, 52.0, new JsonObject() { ["height"] = JsonValue.Create(double.NaN) });
                    Assert.NotNull(footprint_Failing);
                    yield return footprint_Failing;

                    await foreach (Footprint footprint in OpenBuildingParallel_Footprints(7000, null, cancellationToken))
                    {
                        yield return footprint;
                    }
                }

                OpenBuildingImportResult? openBuildingImportResult_Failed = await openBuildingPostgreSQLConverter.ImportAsync(
                    Failing(),
                    new BuildingDatasetRelease("eubucco", version_2, DateTime.Today),
                    countyIds_Scope,
                    counties,
                    maxDegreeOfParallelism: 4,
                    cancellationToken: default);

                Assert.Null(openBuildingImportResult_Failed);
                Assert.Equal(rows_Before, await OpenBuildingParallel_RowsAsync(npgsqlConnection_Storage, "eubucco", openBuildingImportResult.DatasetId.Value));
                Assert.Equal(0L, await OpenBuildingPostgreSQLConverter.GetReleaseCountAsync(npgsqlConnection_Storage, "eubucco", version_2, DateTime.Today));
            }
            finally
            {
                await OpenBuildingParallel_CleanupAsync(npgsqlConnection_Storage, ["eubucco"], countyIds_Scope, [version_1, version_2]);
            }
        }

        /// <summary>
        /// Builds the county parts of the parallel facts: a 4 x 4 grid of 200 km squares in EPSG:2180 covering Poland, under identifiers drawn per call so concurrent runs never share a partition, with every other square in scope.
        /// </summary>
        /// <returns>The county parts and the identifiers in scope.</returns>
        private static (List<AdministrativeAreal2D> Counties, HashSet<int> CountyIds_Scope) OpenBuildingParallel_Counties()
        {
            // High identifiers far above any real county part, and a fresh block per call.
            int id_Base = 1_900_000_000 + (Random.Shared.Next(0, 1_000_000) * 16);

            List<AdministrativeAreal2D> counties = [];
            HashSet<int> countyIds_Scope = [];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    int id = id_Base + (i * 4) + j;
                    double x = 100000 + (i * 200000);
                    double y = 100000 + (j * 200000);

                    IPolygonal2D_Square(x, y, 200000, out Polygon2D polygon2D);
                    PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
                    Assert.NotNull(polygonalFace2D);

                    GIS.Classes.AdministrativeDivision administrativeDivision = new(Guid.NewGuid(), $"xunit-parallel-{id}", $"TST{id}", polygonalFace2D, GIS.Enums.AdministrativeDivisionType.county, "xunit parallel county");

                    counties.Add(new AdministrativeAreal2D()
                    {
                        Id = id,
                        AdministrativeArealType = AdministrativeArealType.County,
                        BoundingBox2D = new BoundingBox2D(new Point2D(x, y), new Point2D(x + 200000, y + 200000)),
                        Object = administrativeDivision.ToJsonObject(),
                    });

                    if ((i + j) % 2 == 0)
                    {
                        countyIds_Scope.Add(id);
                    }
                }
            }

            return (counties, countyIds_Scope);
        }

        /// <summary>
        /// Streams the synthetic footprints of the parallel facts, the same sequence on every call: squares of 10 to 40 m in EPSG:4326 and EPSG:3035 across Poland with varying attributes, and among them multi-face collections, null footprints and footprints west of the country, which the reprojection refuses.
        /// </summary>
        /// <param name="count">The number of footprints.</param>
        /// <param name="cancellation">When set, the source cancels after yielding this many footprints - the stop a user gives mid-run.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
        /// <returns>The stream.</returns>
        private static async IAsyncEnumerable<Footprint> OpenBuildingParallel_Footprints(int count, (int Count, CancellationTokenSource CancellationTokenSource)? cancellation, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            Random random = new(170);

            for (int i = 0; i < count; i++)
            {
                if (cancellation is (int count_Cancel, CancellationTokenSource cancellationTokenSource) && i == count_Cancel)
                {
                    cancellationTokenSource.Cancel();
                }

                cancellationToken.ThrowIfCancellationRequested();

                int kind = random.Next(100);
                string sourceId = string.Create(CultureInfo.InvariantCulture, $"xunit-parallel-{i}");

                JsonObject attributes = new()
                {
                    ["height"] = random.Next(3, 60),
                    [string.Create(CultureInfo.InvariantCulture, $"key_{i % 7}")] = "value",
                };

                Footprint? footprint;
                if (kind < 2)
                {
                    // A null footprint: the reader yielded nothing usable.
                    yield return null!;
                    continue;
                }
                else if (kind < 4)
                {
                    // West of Poland: outside the EPSG:2180 domain, refused by the reprojection.
                    footprint = OpenBuildingParallel_Footprint(sourceId, 4326, 5 + random.NextDouble(), 50 + random.NextDouble(), attributes);
                }
                else if (kind < 50)
                {
                    footprint = OpenBuildingParallel_Footprint(sourceId, 4326, 14.5 + (random.NextDouble() * 9), 49.2 + (random.NextDouble() * 5.3), attributes);
                }
                else if (kind < 95)
                {
                    footprint = OpenBuildingParallel_Footprint(sourceId, 3035, 4700000 + (random.NextDouble() * 500000), 3000000 + (random.NextDouble() * 400000), attributes);
                }
                else
                {
                    // A multi-outer relation: two faces filed under the county of the larger one.
                    double x = 4700000 + (random.NextDouble() * 500000);
                    double y = 3000000 + (random.NextDouble() * 400000);

                    Footprint? footprint_1 = OpenBuildingParallel_Footprint(sourceId, 3035, x, y, attributes);
                    Footprint? footprint_2 = OpenBuildingParallel_Footprint(sourceId, 3035, x + 100, y + 50, attributes);
                    Assert.NotNull(footprint_1);
                    Assert.NotNull(footprint_2);

                    GeometryCollection2D geometryCollection2D = [(PolygonalFace2D)footprint_1.Geometry!, (PolygonalFace2D)footprint_2.Geometry!];
                    footprint = new Footprint(sourceId, 3035, geometryCollection2D, attributes, null, null);
                }

                Assert.NotNull(footprint);
                yield return footprint;
            }
        }

        /// <summary>
        /// Builds one synthetic square footprint: 20 m across in EPSG:3035, about the same in degrees in EPSG:4326.
        /// </summary>
        /// <param name="sourceId">The source identifier.</param>
        /// <param name="srid">4326 or 3035.</param>
        /// <param name="x">The centre's X (longitude or easting).</param>
        /// <param name="y">The centre's Y (latitude or northing).</param>
        /// <param name="attributes">The source attributes.</param>
        /// <returns>The footprint, or null when the square is degenerate.</returns>
        private static Footprint? OpenBuildingParallel_Footprint(string sourceId, int srid, double x, double y, JsonObject attributes)
        {
            double halfSize = srid == 4326 ? 0.0001 : 10;

            IPolygonal2D_Square(x - halfSize, y - halfSize, 2 * halfSize, out Polygon2D polygon2D);
            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);

            return polygonalFace2D is null ? null : new Footprint(sourceId, srid, polygonalFace2D, attributes, new JsonObject() { ["dataset"] = "xunit" }, null);
        }

        /// <summary>
        /// Reads the rows one release stored, as sorted <c>county|source|object</c> lines, so two runs compare as lists whatever order the rows were written in.
        /// </summary>
        /// <param name="npgsqlConnection_Storage">The throwaway Storage test database.</param>
        /// <param name="datasetKey">The dataset key of the table.</param>
        /// <param name="datasetId">The release's register row.</param>
        /// <returns>The sorted rows.</returns>
        private static async Task<List<string>> OpenBuildingParallel_RowsAsync(NpgsqlConnection npgsqlConnection_Storage, string datasetKey, int datasetId)
        {
            string? tableName = Query.OpenBuildingTableName(datasetKey);
            Assert.NotNull(tableName);

            string sourceColumn = string.Equals(tableName, Constants.TableName.GHS_OBAT_Building, StringComparison.Ordinal) ? "overture_id" : "source_id";

            List<string> result = [];
            await using NpgsqlCommand npgsqlCommand = new($"SELECT county_id, {sourceColumn}, object::text FROM {tableName} WHERE dataset_id = {datasetId};", npgsqlConnection_Storage);
            await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
            while (await npgsqlDataReader.ReadAsync())
            {
                result.Add(string.Create(CultureInfo.InvariantCulture, $"{npgsqlDataReader.GetInt32(0)}|{(npgsqlDataReader.IsDBNull(1) ? "(null)" : npgsqlDataReader.GetString(1))}|{npgsqlDataReader.GetString(2)}"));
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// Drops the scratch partitions of the grid counties and removes the GHS-OBAT rows and the register rows the parallel facts wrote.
        /// </summary>
        /// <param name="npgsqlConnection_Storage">The throwaway Storage test database.</param>
        /// <param name="datasetKeys">The dataset keys whose tables were written.</param>
        /// <param name="countyIds">The grid counties in scope.</param>
        /// <param name="versions">The versions of the register rows.</param>
        /// <returns>A task that completes when the cleanup has run.</returns>
        private static async Task OpenBuildingParallel_CleanupAsync(NpgsqlConnection npgsqlConnection_Storage, IEnumerable<string> datasetKeys, IEnumerable<int> countyIds, IEnumerable<string> versions)
        {
            List<string> commandTexts = [];

            foreach (string datasetKey in datasetKeys)
            {
                string? tableName = Query.OpenBuildingTableName(datasetKey);
                if (tableName is null)
                {
                    continue;
                }

                if (string.Equals(tableName, Constants.TableName.GHS_OBAT_Building, StringComparison.Ordinal))
                {
                    commandTexts.Add($"DELETE FROM {tableName} WHERE county_id = ANY(ARRAY[{string.Join(",", countyIds)}]);");
                    continue;
                }

                foreach (int countyId in countyIds)
                {
                    commandTexts.Add($"DROP TABLE IF EXISTS {tableName}_{countyId};");
                }
            }

            foreach (string version in versions)
            {
                commandTexts.Add($"DELETE FROM {Constants.TableName.BuildingDataset} WHERE version = '{version}';");
            }

            await ExecuteAsync(npgsqlConnection_Storage, string.Join(" ", commandTexts));
        }
    }
}
