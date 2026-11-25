using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.IO.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The lower-left corner of the matching fact's scratch county square, in EPSG:2180: west and south of Poland, so no real county part of the test database - and none of its buildings - reaches the square or its margin, and the fact's footprints meet only each other.
        /// </summary>
        private const double openBuildingMatch_ScratchX = 99500;

        /// <summary>
        /// The lower-left corner of the matching fact's scratch county square, in EPSG:2180.
        /// </summary>
        private const double openBuildingMatch_ScratchY = 99500;

        /// <summary>
        /// The edge of the matching fact's scratch county square, in metres.
        /// </summary>
        private const double openBuildingMatch_ScratchSize = 1000;

        /// <summary>
        /// Verifies, on the throwaway test databases (the scratch county and its BDOT10k buildings on Main, the open building footprints and the links on Storage), that the matching task stores the links of every dataset, that the stored summaries equal the in-memory ones, and that each dataset's snapshot is replaced alone (DiGi.GIS.PostgreSQL#127).
        /// <para>Three register buildings; EUBUCCO with a footprint shifted 0.9 m, a split building and a commission, OSM with two exact footprints. The task run stores 5 EUBUCCO and 3 OSM link rows; the county-scoped and box-scoped summaries read from the database equal <see cref="Query.OpenBuildingMatchSummaries"/> over the same footprints matched in memory. Then: re-running EUBUCCO alone leaves the OSM rows untouched (the <c>TRUNCATE</c> trap - every dataset shares a county's partition); a run with <c>Persist = false</c> and other parameters writes nothing and returns its own summaries; and re-importing EUBUCCO into the county deletes its links in the import's transaction while the OSM links stay. Everything written is removed before and after. The figures describe the test databases, not production. Medium test (two live databases).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuildingMatch_Task_StoresAndReplacesPerDataset()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Assert.NotNull(directory);

            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager(directory, fileName_PostgreSQL_Main_Test, fileName_PostgreSQL_Storage_Test);
            Assert.NotNull(gISPostgreSQLConverterManager);

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection_Storage);
            await npgsqlConnection_Storage.OpenAsync();

            string reference = $"xunit-open-building-match-{Guid.NewGuid():N}";
            string code = $"TSTM{Guid.NewGuid():N}";
            string version = $"TST-{Guid.NewGuid():N}";
            string version_Reimport = $"TST-{Guid.NewGuid():N}";
            int? countyId = null;

            try
            {
                countyId = await OpenBuildingMatch_ScratchCountyIdAsync(npgsqlConnection, reference, code);

                // BDOT10k on Main.
                Assert.True(await new Building2DPostgreSQLConverter(connectionData_Main).CreateTableAsync());
                Assert.True(await Create.TableAsync_Building2D_Partition(npgsqlConnection, countyId.Value));
                await OpenBuildingMatch_InsertBuilding2DAsync(npgsqlConnection, countyId.Value, "b1", 0, 0, 10, 10);
                await OpenBuildingMatch_InsertBuilding2DAsync(npgsqlConnection, countyId.Value, "b2", 100, 0, 10, 10);
                await OpenBuildingMatch_InsertBuilding2DAsync(npgsqlConnection, countyId.Value, "b3", 200, 0, 20, 10);

                // The two datasets on Storage.
                Assert.True(await OpenBuildingPostgreSQLConverter.CreateTablesAsync(npgsqlConnection_Storage));

                int? datasetId_EUBUCCO = await OpenBuildingPostgreSQLConverter.UpsertReleaseAsync(npgsqlConnection_Storage, new BuildingDatasetRelease("eubucco", version, DateTime.Today, licence: "ODbL-1.0"));
                int? datasetId_OSM = await OpenBuildingPostgreSQLConverter.UpsertReleaseAsync(npgsqlConnection_Storage, new BuildingDatasetRelease("osm", version, DateTime.Today, licence: "ODbL-1.0"));
                Assert.NotNull(datasetId_EUBUCCO);
                Assert.NotNull(datasetId_OSM);

                Assert.True(await Create.TableAsync_OpenBuilding2D_Partition(npgsqlConnection_Storage, Constants.TableName.EUBUCCO_Building2D, countyId.Value));
                Assert.True(await Create.TableAsync_OpenBuilding2D_Partition(npgsqlConnection_Storage, Constants.TableName.OSM_Building2D, countyId.Value));

                await OpenBuildingMatch_InsertFootprintAsync(npgsqlConnection_Storage, Constants.TableName.EUBUCCO_Building2D, countyId.Value, datasetId_EUBUCCO.Value, "e1", "register", 0.9, 0, 10, 10);
                await OpenBuildingMatch_InsertFootprintAsync(npgsqlConnection_Storage, Constants.TableName.EUBUCCO_Building2D, countyId.Value, datasetId_EUBUCCO.Value, "e2", "osm", 400, 0, 10, 10);
                await OpenBuildingMatch_InsertFootprintAsync(npgsqlConnection_Storage, Constants.TableName.EUBUCCO_Building2D, countyId.Value, datasetId_EUBUCCO.Value, "e3a", "register", 200, 0, 10, 10);
                await OpenBuildingMatch_InsertFootprintAsync(npgsqlConnection_Storage, Constants.TableName.EUBUCCO_Building2D, countyId.Value, datasetId_EUBUCCO.Value, "e3b", "osm", 210, 0, 10, 10);
                await OpenBuildingMatch_InsertFootprintAsync(npgsqlConnection_Storage, Constants.TableName.OSM_Building2D, countyId.Value, datasetId_OSM.Value, "o1", "osm", 0, 0, 10, 10);
                await OpenBuildingMatch_InsertFootprintAsync(npgsqlConnection_Storage, Constants.TableName.OSM_Building2D, countyId.Value, datasetId_OSM.Value, "o2", "osm", 100, 0, 10, 10);

                // 1. Both datasets matched and stored.
                PostgreSQLOpenBuildingMatchTask postgreSQLOpenBuildingMatchTask = new(gISPostgreSQLConverterManager);
                postgreSQLOpenBuildingMatchTask.PostgreSQLOpenBuildingMatchTaskOptions = new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [countyId.Value], DatasetKeys = ["eubucco", "osm"] };
                await OpenBuildingMatch_RunAsync(postgreSQLOpenBuildingMatchTask, true);

                Assert.Equal(0, postgreSQLOpenBuildingMatchTask.FailedGroupCount);
                Assert.Equal(2, postgreSQLOpenBuildingMatchTask.OpenBuildingMatchResults.Count);
                Assert.All(postgreSQLOpenBuildingMatchTask.OpenBuildingMatchResults, x => Assert.True(x.Persisted));
                Assert.All(postgreSQLOpenBuildingMatchTask.OpenBuildingMatchResults, x => Assert.Equal(0L, x.MarginReachCount));
                Assert.Equal(8L, postgreSQLOpenBuildingMatchTask.RowCount);

                Assert.Equal(5L, await OpenBuildingMatch_LinkCountAsync(npgsqlConnection_Storage, countyId.Value, datasetId_EUBUCCO.Value));
                Assert.Equal(3L, await OpenBuildingMatch_LinkCountAsync(npgsqlConnection_Storage, countyId.Value, datasetId_OSM.Value));

                // 2. The stored summaries equal the in-memory ones over the same footprints, by county and by box.
                OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Storage);
                Building2DPostgreSQLConverter building2DPostgreSQLConverter = new(connectionData_Main);
                BoundingBox2D boundingBox2D_County = new(new Point2D(openBuildingMatch_ScratchX, openBuildingMatch_ScratchY), new Point2D(openBuildingMatch_ScratchX + openBuildingMatch_ScratchSize, openBuildingMatch_ScratchY + openBuildingMatch_ScratchSize));

                foreach ((string datasetKey, int datasetId) in new (string, int)[] { ("eubucco", datasetId_EUBUCCO.Value), ("osm", datasetId_OSM.Value) })
                {
                    List<OpenBuildingMatchItem>? openBuildingMatchItems_Dataset = await openBuildingPostgreSQLConverter.GetOpenBuildingMatchItemsAsync(datasetKey, [countyId.Value]);
                    List<OpenBuildingMatchItem>? openBuildingMatchItems_BDOT = await building2DPostgreSQLConverter.GetOpenBuildingMatchItemsAsync([countyId.Value]);
                    Assert.NotNull(openBuildingMatchItems_Dataset);
                    Assert.NotNull(openBuildingMatchItems_BDOT);
                    Assert.Equal(3, openBuildingMatchItems_BDOT.Count);
                    Assert.All(openBuildingMatchItems_BDOT, x => Assert.NotNull(x.Geometry));
                    Assert.All(openBuildingMatchItems_Dataset, x => Assert.NotNull(x.Geometry));

                    OpenBuildingMatchParameters openBuildingMatchParameters = new();
                    List<OpenBuildingMatch>? openBuildingMatches = Query.OpenBuildingMatches(openBuildingMatchItems_Dataset, openBuildingMatchItems_BDOT, openBuildingMatchParameters, new HashSet<int>() { countyId.Value });
                    List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_Memory = Query.OpenBuildingMatchSummaries(openBuildingMatches, datasetKey, datasetId, openBuildingMatchParameters, null);
                    List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_County = await openBuildingPostgreSQLConverter.GetMatchSummariesByCountyIdsAsync(datasetKey, [countyId.Value]);
                    List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_Box = await openBuildingPostgreSQLConverter.GetMatchSummariesAsync(datasetKey, boundingBox2D_County);

                    OpenBuildingMatch_AssertSummariesEqual(openBuildingMatchSummaryResults_Memory, openBuildingMatchSummaryResults_County);
                    OpenBuildingMatch_AssertSummariesEqual(openBuildingMatchSummaryResults_Memory, openBuildingMatchSummaryResults_Box);
                }

                List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_EUBUCCO = await openBuildingPostgreSQLConverter.GetMatchSummariesByCountyIdsAsync("eubucco", [countyId.Value]);
                Assert.NotNull(openBuildingMatchSummaryResults_EUBUCCO);
                OpenBuildingMatchSummaryResult total_EUBUCCO = openBuildingMatchSummaryResults_EUBUCCO[0];
                Assert.Null(total_EUBUCCO.PrimarySource);
                Assert.Equal(3L, total_EUBUCCO.BDOTCount);
                Assert.Equal(4L, total_EUBUCCO.DatasetCount);
                Assert.Equal(1L, total_EUBUCCO.OneToOneCount);
                Assert.Equal(1L, total_EUBUCCO.ManyToOneCount);
                Assert.Equal(1L, total_EUBUCCO.OmissionCount);
                Assert.Equal(1L, total_EUBUCCO.CommissionCount);
                Assert.Equal(0.9, total_EUBUCCO.DxMean!.Value, 6);
                Assert.Equal(0.0, total_EUBUCCO.DyMean!.Value, 6);
                Assert.NotNull(total_EUBUCCO.OpenBuildingMatchParameters);
                Assert.NotNull(total_EUBUCCO.ComputedAt);

                // A box that holds none of the representative points reads no link.
                List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_Empty = await openBuildingPostgreSQLConverter.GetMatchSummariesAsync("eubucco", new BoundingBox2D(new Point2D(openBuildingMatch_ScratchX + 900, openBuildingMatch_ScratchY + 900), new Point2D(openBuildingMatch_ScratchX + 950, openBuildingMatch_ScratchY + 950)));
                Assert.NotNull(openBuildingMatchSummaryResults_Empty);
                Assert.Empty(openBuildingMatchSummaryResults_Empty);
                Assert.Null(await openBuildingPostgreSQLConverter.GetMatchSummariesAsync("ghs_obat", boundingBox2D_County));
                Assert.Null(await openBuildingPostgreSQLConverter.GetMatchSummariesAsync("no-such-dataset", boundingBox2D_County));

                // 3. EUBUCCO alone again: the OSM snapshot in the same county partition is not touched.
                string? computedAt_OSM = await ScalarAsync(npgsqlConnection_Storage, $"SELECT min(computed_at)::text FROM {Constants.TableName.BuildingDatasetMatch} WHERE county_id = {countyId.Value} AND dataset_id = {datasetId_OSM.Value};") as string;
                Assert.NotNull(computedAt_OSM);

                PostgreSQLOpenBuildingMatchTask postgreSQLOpenBuildingMatchTask_EUBUCCO = new(gISPostgreSQLConverterManager);
                postgreSQLOpenBuildingMatchTask_EUBUCCO.PostgreSQLOpenBuildingMatchTaskOptions = new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [countyId.Value], DatasetKeys = ["eubucco"] };
                await OpenBuildingMatch_RunAsync(postgreSQLOpenBuildingMatchTask_EUBUCCO, true);

                Assert.Equal(5L, await OpenBuildingMatch_LinkCountAsync(npgsqlConnection_Storage, countyId.Value, datasetId_EUBUCCO.Value));
                Assert.Equal(3L, await OpenBuildingMatch_LinkCountAsync(npgsqlConnection_Storage, countyId.Value, datasetId_OSM.Value));
                Assert.Equal(computedAt_OSM, await ScalarAsync(npgsqlConnection_Storage, $"SELECT min(computed_at)::text FROM {Constants.TableName.BuildingDatasetMatch} WHERE county_id = {countyId.Value} AND dataset_id = {datasetId_OSM.Value};") as string);

                // 4. A sensitivity run that does not persist: nothing written, its own summaries returned.
                PostgreSQLOpenBuildingMatchTask postgreSQLOpenBuildingMatchTask_Sensitivity = new(gISPostgreSQLConverterManager);
                postgreSQLOpenBuildingMatchTask_Sensitivity.PostgreSQLOpenBuildingMatchTaskOptions = new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [countyId.Value], DatasetKeys = ["eubucco"], OpenBuildingMatchParameters = new OpenBuildingMatchParameters(minArea: 1000), Persist = false };
                await OpenBuildingMatch_RunAsync(postgreSQLOpenBuildingMatchTask_Sensitivity, true);

                Assert.All(postgreSQLOpenBuildingMatchTask_Sensitivity.OpenBuildingMatchResults, x => Assert.False(x.Persisted));
                Assert.Equal(5L, await OpenBuildingMatch_LinkCountAsync(npgsqlConnection_Storage, countyId.Value, datasetId_EUBUCCO.Value));
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection_Storage, $"SELECT count(*) FROM {Constants.TableName.BuildingDatasetMatch} WHERE county_id = {countyId.Value} AND match_class = 8;"));

                OpenBuildingMatchSummaryResult total_Sensitivity = Assert.Single(postgreSQLOpenBuildingMatchTask_Sensitivity.OpenBuildingMatchSummaryResults, x => x.PrimarySource is null);
                Assert.Equal(3L, total_Sensitivity.BDOTExcludedCount);
                Assert.Equal(4L, total_Sensitivity.DatasetExcludedCount);
                Assert.Equal(0L, total_Sensitivity.BDOTCount);

                // 5. Re-importing EUBUCCO into the county removes its links in the import's own transaction; OSM keeps its links.
                List<AdministrativeAreal2D>? counties = await AdministrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DsByAdministrativeArealType(npgsqlConnection, Enums.AdministrativeArealType.County);
                Assert.NotNull(counties);

                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(
                    OpenBuilding_Stream([], default),
                    new BuildingDatasetRelease("eubucco", version_Reimport, DateTime.Today, licence: "ODbL-1.0"),
                    [countyId.Value],
                    counties,
                    cancellationToken: default);
                Assert.NotNull(openBuildingImportResult);

                Assert.Equal(0L, await ScalarAsync(npgsqlConnection_Storage, $@"SELECT count(*) FROM {Constants.TableName.BuildingDatasetMatch} m JOIN {Constants.TableName.BuildingDataset} d ON d.id = m.dataset_id WHERE m.county_id = {countyId.Value} AND d.dataset_key = 'eubucco';"));
                Assert.Equal(3L, await OpenBuildingMatch_LinkCountAsync(npgsqlConnection_Storage, countyId.Value, datasetId_OSM.Value));
            }
            finally
            {
                await OpenBuildingMatch_CleanupAsync(npgsqlConnection, npgsqlConnection_Storage, reference, [version, version_Reimport], countyId);
            }
        }

        /// <summary>
        /// Asserts that two summary lists describe the same figures: same rows in the same order, equal counts, and rates and measures equal to 1e-9.
        /// </summary>
        /// <param name="openBuildingMatchSummaryResults_Expected">The expected summaries.</param>
        /// <param name="openBuildingMatchSummaryResults_Actual">The summaries under test.</param>
        private static void OpenBuildingMatch_AssertSummariesEqual(List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_Expected, List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_Actual)
        {
            Assert.NotNull(openBuildingMatchSummaryResults_Expected);
            Assert.NotNull(openBuildingMatchSummaryResults_Actual);
            Assert.Equal(openBuildingMatchSummaryResults_Expected.Count, openBuildingMatchSummaryResults_Actual.Count);

            static void Close(double? expected, double? actual)
            {
                Assert.Equal(expected.HasValue, actual.HasValue);
                if (expected.HasValue && actual.HasValue)
                {
                    Assert.Equal(expected.Value, actual.Value, 9);
                }
            }

            for (int i = 0; i < openBuildingMatchSummaryResults_Expected.Count; i++)
            {
                OpenBuildingMatchSummaryResult expected = openBuildingMatchSummaryResults_Expected[i];
                OpenBuildingMatchSummaryResult actual = openBuildingMatchSummaryResults_Actual[i];

                Assert.Equal(expected.DatasetId, actual.DatasetId);
                Assert.Equal(expected.PrimarySource, actual.PrimarySource);
                Assert.Equal(expected.BDOTCount, actual.BDOTCount);
                Assert.Equal(expected.DatasetCount, actual.DatasetCount);
                Assert.Equal(expected.BDOTExcludedCount, actual.BDOTExcludedCount);
                Assert.Equal(expected.DatasetExcludedCount, actual.DatasetExcludedCount);
                Assert.Equal(expected.OneToOneCount, actual.OneToOneCount);
                Assert.Equal(expected.OneToOneWeakCount, actual.OneToOneWeakCount);
                Assert.Equal(expected.OneToManyCount, actual.OneToManyCount);
                Assert.Equal(expected.ManyToOneCount, actual.ManyToOneCount);
                Assert.Equal(expected.ManyToManyCount, actual.ManyToManyCount);
                Assert.Equal(expected.OmissionCount, actual.OmissionCount);
                Assert.Equal(expected.CommissionCount, actual.CommissionCount);
                Close(expected.OmissionRate, actual.OmissionRate);
                Close(expected.CommissionRate, actual.CommissionRate);
                Close(expected.IoUMean, actual.IoUMean);
                Close(expected.IoUMedian, actual.IoUMedian);
                Close(expected.IoUP10, actual.IoUP10);
                Close(expected.AreaRatioMedian, actual.AreaRatioMedian);
                Close(expected.CentroidDistanceMedian, actual.CentroidDistanceMedian);
                Close(expected.DxMean, actual.DxMean);
                Close(expected.DxMedian, actual.DxMedian);
                Close(expected.DyMean, actual.DyMean);
                Close(expected.DyMedian, actual.DyMedian);
            }
        }

        /// <summary>
        /// Counts the stored link rows of one register row in one county part.
        /// </summary>
        /// <param name="npgsqlConnection_Storage">The open connection to the throwaway storage test database.</param>
        /// <param name="countyId">The county part.</param>
        /// <param name="datasetId">The register row.</param>
        /// <returns>The number of link rows.</returns>
        private static async Task<long> OpenBuildingMatch_LinkCountAsync(NpgsqlConnection npgsqlConnection_Storage, int countyId, int datasetId)
        {
            object? result = await ScalarAsync(npgsqlConnection_Storage, $"SELECT count(*) FROM {Constants.TableName.BuildingDatasetMatch} WHERE county_id = {countyId} AND dataset_id = {datasetId};");
            Assert.NotNull(result);

            return System.Convert.ToInt64(result, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Starts a matching task, waits for it to stop and asserts it completed without an exception with the expected outcome.
        /// </summary>
        /// <param name="postgreSQLOpenBuildingMatchTask">The task to run.</param>
        /// <param name="isSucceeded">The outcome the task must report.</param>
        /// <returns>A task that completes when the run has finished and its outcome has been asserted.</returns>
        private static async Task OpenBuildingMatch_RunAsync(PostgreSQLOpenBuildingMatchTask postgreSQLOpenBuildingMatchTask, bool isSucceeded)
        {
            TaskCompletionSource<bool> taskCompletionSource = new();
            postgreSQLOpenBuildingMatchTask.Stopped += (object? sender, EventArgs e) => taskCompletionSource.TrySetResult(true);

            postgreSQLOpenBuildingMatchTask.Start();

            await taskCompletionSource.Task;

            Assert.Null(postgreSQLOpenBuildingMatchTask.Exception);
            Assert.Equal(isSucceeded, postgreSQLOpenBuildingMatchTask.IsSucceeded);
        }

        /// <summary>
        /// Inserts the matching fact's scratch county part on Main: a one-kilometre square outside Poland, typed as a county, after removing any square a previous run of this fact left behind.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="reference">The guid-based reference of the row.</param>
        /// <param name="code">The guid-based code of the row, so the county group of the part is the part alone.</param>
        /// <returns>The identifier of the inserted county part.</returns>
        private static async Task<int> OpenBuildingMatch_ScratchCountyIdAsync(NpgsqlConnection npgsqlConnection, string reference, string code)
        {
            await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference LIKE 'xunit-open-building-match-%';");

            IPolygonal2D_Square(openBuildingMatch_ScratchX, openBuildingMatch_ScratchY, openBuildingMatch_ScratchSize, out Polygon2D polygon2D);

            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(polygonalFace2D);

            GIS.Classes.AdministrativeDivision administrativeDivision = new(Guid.NewGuid(), reference, code, polygonalFace2D, GIS.Enums.AdministrativeDivisionType.county, "xunit open building match scratch county");

            string? objectJson = administrativeDivision.ToJsonObject()?.ToJsonString();
            Assert.NotNull(objectJson);

            string commandText = $@"INSERT INTO {Constants.TableName.AdministrativeAreal2D} (reference, code, name, type_id, min_x, min_y, max_x, max_y, object)
                VALUES (@reference, @code, 'xunit open building match scratch county', @type_id, @min_x, @min_y, @max_x, @max_y, @object::jsonb) RETURNING id;";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = reference });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("code", NpgsqlDbType.Text) { Value = code });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("type_id", NpgsqlDbType.Integer) { Value = (int)Enums.AdministrativeArealType.County });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchX });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchY });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchX + openBuildingMatch_ScratchSize });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchY + openBuildingMatch_ScratchSize });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Text) { Value = objectJson });

            object? result = await npgsqlCommand.ExecuteScalarAsync();
            Assert.NotNull(result);

            return System.Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Inserts one rectangular BDOT10k building into the scratch county's partition of <c>building_2d</c> on Main, offset from the scratch square's corner, in the stored object shape the production rows have.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="countyId">The scratch county part.</param>
        /// <param name="reference">The reference of the building.</param>
        /// <param name="x">The X of the lower-left corner relative to the scratch corner.</param>
        /// <param name="y">The Y of the lower-left corner relative to the scratch corner.</param>
        /// <param name="width">The width in metres.</param>
        /// <param name="height">The height in metres.</param>
        /// <returns>A task that completes when the row is written.</returns>
        private static async Task OpenBuildingMatch_InsertBuilding2DAsync(NpgsqlConnection npgsqlConnection, int countyId, string reference, double x, double y, double width, double height)
        {
            PolygonalFace2D polygonalFace2D = OpenBuildingMatch_ScratchFace(x, y, width, height);
            GIS.Classes.Building2D building2D = new(Guid.NewGuid(), reference, polygonalFace2D, 1, null, null, []);

            string? objectJson = building2D.ToJsonObject()?.ToJsonString();
            Assert.NotNull(objectJson);

            string commandText = $@"INSERT INTO {Constants.TableName.Building2D} (county_id, reference, min_x, min_y, max_x, max_y, object)
                VALUES (@county_id, @reference, @min_x, @min_y, @max_x, @max_y, @object::jsonb);";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = reference });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchX + x });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchY + y });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchX + x + width });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchY + y + height });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Text) { Value = objectJson });

            await npgsqlCommand.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Inserts one rectangular open building footprint into the scratch county's partition of a dataset table on Storage, in the stored object shape the import writes (an EPSG:2180 <see cref="Footprint"/>).
        /// </summary>
        /// <param name="npgsqlConnection_Storage">The open connection to the throwaway storage test database.</param>
        /// <param name="tableName">The footprint table.</param>
        /// <param name="countyId">The scratch county part.</param>
        /// <param name="datasetId">The register row.</param>
        /// <param name="sourceId">The source identifier of the footprint.</param>
        /// <param name="primarySource">The normalised primary source.</param>
        /// <param name="x">The X of the lower-left corner relative to the scratch corner.</param>
        /// <param name="y">The Y of the lower-left corner relative to the scratch corner.</param>
        /// <param name="width">The width in metres.</param>
        /// <param name="height">The height in metres.</param>
        /// <returns>A task that completes when the row is written.</returns>
        private static async Task OpenBuildingMatch_InsertFootprintAsync(
            NpgsqlConnection npgsqlConnection_Storage,
            string tableName,
            int countyId,
            int datasetId,
            string sourceId,
            string primarySource,
            double x,
            double y,
            double width,
            double height)
        {
            PolygonalFace2D polygonalFace2D = OpenBuildingMatch_ScratchFace(x, y, width, height);
            Footprint footprint = new(sourceId, 2180, polygonalFace2D, new JsonObject(), null, null);

            string? objectJson = footprint.ToJsonObject()?.ToJsonString();
            Assert.NotNull(objectJson);

            string commandText = $@"INSERT INTO {tableName} (county_id, dataset_id, source_id, primary_source, min_x, min_y, max_x, max_y, object)
                VALUES (@county_id, @dataset_id, @source_id, @primary_source, @min_x, @min_y, @max_x, @max_y, @object::jsonb);";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection_Storage);
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("dataset_id", NpgsqlDbType.Integer) { Value = datasetId });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("source_id", NpgsqlDbType.Text) { Value = sourceId });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("primary_source", NpgsqlDbType.Text) { Value = primarySource });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchX + x });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchY + y });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchX + x + width });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = openBuildingMatch_ScratchY + y + height });
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Text) { Value = objectJson });

            await npgsqlCommand.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Builds an axis-aligned rectangular face offset from the scratch square's corner.
        /// </summary>
        /// <param name="x">The X of the lower-left corner relative to the scratch corner.</param>
        /// <param name="y">The Y of the lower-left corner relative to the scratch corner.</param>
        /// <param name="width">The width in metres.</param>
        /// <param name="height">The height in metres.</param>
        /// <returns>The face.</returns>
        private static PolygonalFace2D OpenBuildingMatch_ScratchFace(double x, double y, double width, double height)
        {
            double x_Min = openBuildingMatch_ScratchX + x;
            double y_Min = openBuildingMatch_ScratchY + y;

            Polygon2D polygon2D = new([new Point2D(x_Min, y_Min), new Point2D(x_Min + width, y_Min), new Point2D(x_Min + width, y_Min + height), new Point2D(x_Min, y_Min + height)]);
            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(polygonalFace2D);

            return polygonalFace2D;
        }

        /// <summary>
        /// Removes everything the matching fact wrote: the scratch county's partitions of the link table, of both dataset tables and of <c>building_2d</c>, the fact's register rows and the scratch county part.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="npgsqlConnection_Storage">The open connection to the throwaway storage test database.</param>
        /// <param name="reference">The reference of the scratch county part.</param>
        /// <param name="versions">The versions of the register rows to remove.</param>
        /// <param name="countyId">The scratch county part, when one was inserted.</param>
        /// <returns>A task that completes when the cleanup has run.</returns>
        private static async Task OpenBuildingMatch_CleanupAsync(NpgsqlConnection npgsqlConnection, NpgsqlConnection npgsqlConnection_Storage, string reference, string[] versions, int? countyId)
        {
            if (countyId is int countyId_Temp)
            {
                await ExecuteAsync(npgsqlConnection_Storage, $@"
                    DROP TABLE IF EXISTS {Constants.TableName.BuildingDatasetMatch}_{countyId_Temp};
                    DROP TABLE IF EXISTS {Constants.TableName.EUBUCCO_Building2D}_{countyId_Temp};
                    DROP TABLE IF EXISTS {Constants.TableName.OSM_Building2D}_{countyId_Temp};");

                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.Building2D}_{countyId_Temp};");
            }

            if (await ScalarAsync(npgsqlConnection_Storage, $"SELECT to_regclass('{Constants.TableName.BuildingDataset}') IS NOT NULL;") is bool registerExists && registerExists)
            {
                foreach (string version in versions)
                {
                    await ExecuteAsync(npgsqlConnection_Storage, $"DELETE FROM {Constants.TableName.BuildingDataset} WHERE version = '{version}';");
                }
            }

            await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference = '{reference}';");
        }
    }
}
