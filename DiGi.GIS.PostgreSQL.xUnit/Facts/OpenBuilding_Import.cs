using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.IO.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The centre of the scratch county square, in EPSG:2180: inside the domain of use, so the imported footprints survive the reprojection's Polish domain check.
        /// </summary>
        private const double openBuilding_ScratchX = 630000;

        /// <summary>
        /// The centre of the scratch county square, in EPSG:2180.
        /// </summary>
        private const double openBuilding_ScratchY = 485000;

        /// <summary>
        /// The half edge of the scratch county square, in metres. The square is a kilometre across - smaller than any real county, so the smallest-covering rule files the footprints under it even when the test database also holds real counties, and it never collides with another run's scratch rows because its reference is a fresh guid per fact.
        /// </summary>
        private const double openBuilding_ScratchHalfSize = 500;

        /// <summary>
        /// Verifies, on the throwaway main test database, that an imported release's footprints land in the partition of their county part with their provenance, normalised primary source and reprojected object, and that the register row learns what the run stored (DiGi.GIS.PostgreSQL#125).
        /// <para>Two footprints filed under one guid-based scratch county part of <c>eubucco_building_2d</c>: the row count of that partition is exactly two, every row points at the release's register identifier, carries a non-null <c>provenance</c> and <c>object</c> and the normalised <c>primary_source</c> of its <c>gov-*</c> source, the register's <c>row_count</c> is two, and <c>attribute_names</c> is the union of the rows' attributes. The scratch county, its partition and the register row are removed in <c>finally</c>; the now-empty <c>eubucco_building_2d</c> table itself is schema and stays. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuilding_Import_LandsRowsInTheRightPartition()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            string reference = $"xunit-open-building-{Guid.NewGuid():N}";
            string code = $"TST{Guid.NewGuid():N}";
            string version = $"TST-{Guid.NewGuid():N}";
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection, reference, code);

                Footprint[] footprints =
                [
                    OpenBuilding_ScratchFootprint("xunit-partition-1"),
                    OpenBuilding_ScratchFootprint("xunit-partition-2"),
                ];

                OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Main);

                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(
                    OpenBuilding_Stream(footprints, default),
                    new BuildingDatasetRelease("eubucco", version, DateTime.Today, sourceUrl: "https://example.invalid/eubucco.zip", licence: "ODbL-2.0", sourceObservationDate: "2026", fileName: "eubucco_sample.gpkg", sha256: "abc123"),
                    [countyId.Value],
                    cancellationToken: default);

                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(2L, openBuildingImportResult.RowCount);
                Assert.Equal(0L, openBuildingImportResult.FilteredCount);
                Assert.Equal(0L, openBuildingImportResult.RejectionCount);

                // Rows in the partition of the scratch county part, and nowhere else.
                Assert.Equal(2L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "eubucco", [countyId.Value]));

                string commandText = $@"SELECT count(*) FROM {Constants.TableName.EUBUCCO_Building2D}
                    WHERE county_id = {countyId.Value}
                    AND dataset_id = (SELECT id FROM {Constants.TableName.BuildingDataset} WHERE dataset_key = 'eubucco' AND version = '{version}')
                    AND primary_source = 'register'
                    AND provenance IS NOT NULL
                    AND object IS NOT NULL;";
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, commandText));

                // The release knows what it stored, and which attributes its rows carried.
                string commandText_RowCount = $@"SELECT row_count FROM {Constants.TableName.BuildingDataset}
                    WHERE dataset_key = 'eubucco' AND version = '{version}';";
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, commandText_RowCount));

                string commandText_AttributeNames = $@"SELECT attribute_names::text FROM {Constants.TableName.BuildingDataset}
                    WHERE dataset_key = 'eubucco' AND version = '{version}';";
                // jsonb's text rendering separates the elements with ", ".
                Assert.Equal("[\"height\", \"name\"]", await ScalarAsync(npgsqlConnection, commandText_AttributeNames));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, [reference], [version], countyId);
            }
        }

        /// <summary>
        /// Verifies, on the throwaway main test database, the replace-snapshot semantics of the open building import: re-running a release leaves one snapshot, and a later release in the same scope replaces the earlier one's rows completely (DiGi.GIS.PostgreSQL#125).
        /// <para>The scope's partition is the snapshot unit: importing two footprints and then the same two again stores two rows rather than four, and importing a second release of one footprint into the same partition leaves that one row - the first release's rows are gone, not stacked underneath. Both releases keep their own register row, keyed by their version. The scratch county, its partition and both register rows are removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuilding_Import_RerunLeavesOneSnapshot()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            string reference = $"xunit-open-building-{Guid.NewGuid():N}";
            string code = $"TST{Guid.NewGuid():N}";
            string version_1 = $"TST-{Guid.NewGuid():N}";
            string version_2 = $"TST-{Guid.NewGuid():N}";
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection, reference, code);

                OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Main);

                BuildingDatasetRelease release_1 = new("eubucco", version_1, DateTime.Today);
                BuildingDatasetRelease release_2 = new("eubucco", version_2, DateTime.Today);

                Footprint[] footprints_Two =
                [
                    OpenBuilding_ScratchFootprint("xunit-snapshot-1"),
                    OpenBuilding_ScratchFootprint("xunit-snapshot-2"),
                ];

                Footprint[] footprints_One = [OpenBuilding_ScratchFootprint("xunit-snapshot-3")];

                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(OpenBuilding_Stream(footprints_Two, default), release_1, [countyId.Value], cancellationToken: default);
                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(2L, openBuildingImportResult.RowCount);
                Assert.Equal(2L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "eubucco", [countyId.Value]));

                // The same release again: the truncation inside the transaction replaces rather than appends.
                openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(OpenBuilding_Stream(footprints_Two, default), release_1, [countyId.Value], cancellationToken: default);
                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(2L, openBuildingImportResult.RowCount);
                Assert.Equal(2L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "eubucco", [countyId.Value]));

                // A later release in the same scope: one snapshot, of the later release only.
                openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(OpenBuilding_Stream(footprints_One, default), release_2, [countyId.Value], cancellationToken: default);
                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(1L, openBuildingImportResult.RowCount);
                Assert.Equal(1L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "eubucco", [countyId.Value]));

                string commandText = $@"SELECT count(*) FROM {Constants.TableName.EUBUCCO_Building2D}
                    WHERE county_id = {countyId.Value}
                    AND dataset_id = (SELECT id FROM {Constants.TableName.BuildingDataset} WHERE dataset_key = 'eubucco' AND version = '{version_2}');";
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, commandText));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, [reference], [version_1, version_2], countyId);
            }
        }

        /// <summary>
        /// Verifies, on the throwaway main test database, that re-importing one release keeps exactly one register row and revises its metadata instead of adding a second row (DiGi.GIS.PostgreSQL#125).
        /// <para>The register's key is <c>(dataset_key, version, download_date)</c> under a <c>NULLS NOT DISTINCT</c> index, so the same release imported twice answers a count of one, and the licence and SHA-256 of the second import replace those of the first - a re-download of the same release corrects the register rather than forking it. The scratch county, its partition and the register row are removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuilding_Import_RegisterUpsertKeepsOneRow()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            string reference = $"xunit-open-building-{Guid.NewGuid():N}";
            string code = $"TST{Guid.NewGuid():N}";
            string version = $"TST-{Guid.NewGuid():N}";
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection, reference, code);

                OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Main);

                Footprint[] footprints =
                [
                    OpenBuilding_ScratchFootprint("xunit-register-1"),
                    OpenBuilding_ScratchFootprint("xunit-register-2"),
                ];

                BuildingDatasetRelease release_1 = new("eubucco", version, DateTime.Today, licence: "ODbL-1.0", sha256: "sha-first");
                BuildingDatasetRelease release_2 = new("eubucco", version, DateTime.Today, licence: "ODbL-2.0", sha256: "sha-second");

                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(OpenBuilding_Stream(footprints, default), release_1, [countyId.Value], cancellationToken: default);
                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(2L, openBuildingImportResult.RowCount);

                openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(OpenBuilding_Stream(footprints, default), release_2, [countyId.Value], cancellationToken: default);
                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(2L, openBuildingImportResult.RowCount);

                // One row for the release, carrying the second import's metadata.
                Assert.Equal(1L, await OpenBuildingPostgreSQLConverter.GetReleaseCountAsync(npgsqlConnection, "eubucco", version, DateTime.Today));

                string commandText = $@"SELECT licence || '|' || sha256 || '|' || row_count::text FROM {Constants.TableName.BuildingDataset}
                    WHERE dataset_key = 'eubucco' AND version = '{version}';";
                Assert.Equal("ODbL-2.0|sha-second|2", await ScalarAsync(npgsqlConnection, commandText));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, [reference], [version], countyId);
            }
        }

        /// <summary>
        /// Verifies, on the throwaway main test database, that a footprint from outside the country is counted as a rejection and never written, while the footprint in scope is stored (DiGi.GIS.PostgreSQL#125).
        /// <para>The out-of-country footprint is a square around 5E, 50N: its reprojection lands far west of the Polish domain, so the reprojection refuses it - and even a domain that did not would leave no county envelope reaching it. Either way it is one rejection, named in the result, and the partition holds only the footprint that was in scope. The scratch county, its partition and the register row are removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuilding_Import_CountsOutOfCountryFootprintAsRejection()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            string reference = $"xunit-open-building-{Guid.NewGuid():N}";
            string code = $"TST{Guid.NewGuid():N}";
            string version = $"TST-{Guid.NewGuid():N}";
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection, reference, code);

                Footprint? footprint_Outside = OpenBuilding_Footprint("xunit-outside", 5.0, 50.0, 0.0001);
                Assert.NotNull(footprint_Outside);

                Footprint[] footprints = [OpenBuilding_ScratchFootprint("xunit-in-scope"), footprint_Outside];

                OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Main);

                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(
                    OpenBuilding_Stream(footprints, default),
                    new BuildingDatasetRelease("eubucco", version, DateTime.Today),
                    [countyId.Value],
                    cancellationToken: default);

                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(1L, openBuildingImportResult.RowCount);
                Assert.Equal(0L, openBuildingImportResult.FilteredCount);
                Assert.Equal(1L, openBuildingImportResult.RejectionCount);

                Rejection rejection = Assert.Single(openBuildingImportResult.Rejections);
                Assert.Equal("xunit-outside", rejection.Reference);
                Assert.Equal(Enums.UpdateRejectionReason.GeometryRejected, rejection.UpdateRejectionReason);

                // Only the in-scope footprint reached the database.
                Assert.Equal(1L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "eubucco", [countyId.Value]));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, [reference], [version], countyId);
            }
        }

        /// <summary>
        /// Inserts the scratch county part a fact imports under: a one-kilometre square at the scratch centre, typed as a county and carrying the same stored object shape the production rows do.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="reference">The guid-based reference of the row; unique, so no pre-cleanup is needed.</param>
        /// <param name="code">The guid-based code of the row; unique, so sibling widening resolves to this part alone.</param>
        /// <returns>The identifier of the inserted county part.</returns>
        private static async Task<int> OpenBuilding_ScratchCountyIdAsync(NpgsqlConnection npgsqlConnection, string reference, string code)
        {
            // The schema first, asserted here rather than hidden inside the import: if the throwaway database
            // cannot hold the tables, this names the failure instead of a null result further down.
            Assert.True(await OpenBuildingPostgreSQLConverter.CreateTablesAsync(npgsqlConnection), "the open building tables could not be created on the throwaway test database.");

            // Remove every scratch county a previous run left behind before writing this run's. All facts
            // place their square at the same coordinates, so a leftover square ties this one on area and the
            // identifier tie-break would then decide between them - filing this run's footprints under a
            // county the run does not have in scope (the convention: remove scratch rows before the write
            // and again in finally).
            await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference LIKE 'xunit-open-building-%';");

            double min_X = openBuilding_ScratchX - openBuilding_ScratchHalfSize;
            double min_Y = openBuilding_ScratchY - openBuilding_ScratchHalfSize;
            double size = openBuilding_ScratchHalfSize * 2;

            IPolygonal2D_Square(min_X, min_Y, size, out Polygon2D polygon2D);

            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(polygonalFace2D);

            GIS.Classes.AdministrativeDivision administrativeDivision = new(Guid.NewGuid(), reference, code, polygonalFace2D, GIS.Enums.AdministrativeDivisionType.county, "xunit open building scratch county");

            string? objectJson = administrativeDivision.ToJsonObject()?.ToJsonString();
            Assert.NotNull(objectJson);

            string commandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO {0} (reference, code, name, type_id, min_x, min_y, max_x, max_y, object) VALUES ('{1}', '{2}', 'xunit open building scratch county', {3}, {4}, {5}, {6}, {7}, '{8}') RETURNING id;",
                Constants.TableName.AdministrativeAreal2D,
                reference,
                code,
                (int)Enums.AdministrativeArealType.County,
                min_X,
                min_Y,
                min_X + size,
                min_Y + size,
                objectJson);

            object? result = await ScalarAsync(npgsqlConnection, commandText);
            Assert.NotNull(result);

            return System.Convert.ToInt32(result, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Removes everything a fact wrote: the scratch county's partition of the dataset's table, the fact's register rows and the scratch county part itself.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="references">The guid-based references of the scratch county parts to remove.</param>
        /// <param name="versions">The versions of the register rows to remove.</param>
        /// <param name="countyId">The identifier of the scratch county part, when one was inserted; its partition is dropped with it.</param>
        /// <param name="datasetKey">The dataset key whose table holds the partition and whose register rows are removed; defaults to EUBUCCO.</param>
        /// <returns>A task that completes when the cleanup has run.</returns>
        private static async Task OpenBuilding_CleanupAsync(NpgsqlConnection npgsqlConnection, string[] references, string[] versions, int? countyId, string datasetKey = "eubucco")
        {
            List<string> commandTexts = [];

            string? tableName = Query.OpenBuildingTableName(datasetKey);

            if (countyId is int countyId_Temp && tableName is not null && !string.Equals(tableName, Constants.TableName.GHS_OBAT_Building, StringComparison.Ordinal))
            {
                commandTexts.Add($"DROP TABLE IF EXISTS {tableName}_{countyId_Temp};");
            }

            // The register and the administrative rows are only removed when they exist: a cleanup that ran
            // after an early failure must not fail on a missing table and mask the original failure.
            if (await ScalarAsync(npgsqlConnection, $"SELECT to_regclass('{Constants.TableName.BuildingDataset}') IS NOT NULL;") is bool registerExists && registerExists)
            {
                foreach (string version in versions)
                {
                    commandTexts.Add($"DELETE FROM {Constants.TableName.BuildingDataset} WHERE dataset_key = '{datasetKey}' AND version = '{version}';");
                }
            }

            if (await ScalarAsync(npgsqlConnection, $"SELECT to_regclass('{Constants.TableName.AdministrativeAreal2D}') IS NOT NULL;") is bool administrativeExists && administrativeExists)
            {
                foreach (string reference in references)
                {
                    commandTexts.Add($"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference = '{reference}';");
                }
            }

            if (commandTexts.Count > 0)
            {
                await ExecuteAsync(npgsqlConnection, string.Join(" ", commandTexts));
            }
        }

        /// <summary>
        /// Builds the footprint a fact imports: a square small enough to sit inside the scratch county part, reprojected from the EPSG:4326 position of the scratch centre.
        /// </summary>
        /// <param name="sourceId">The identifier the source dataset gives the footprint.</param>
        /// <returns>The footprint.</returns>
        private static Footprint OpenBuilding_ScratchFootprint(string sourceId)
        {
            Point3D? point3D = GIS.Convert.ToEPSG4326(new Point2D(openBuilding_ScratchX, openBuilding_ScratchY));
            Assert.NotNull(point3D);

            Footprint? footprint = OpenBuilding_Footprint(sourceId, point3D.X, point3D.Y, 0.0001);
            Assert.NotNull(footprint);

            return footprint;
        }

        /// <summary>
        /// Wraps a materialised footprint list as the stream the import consumes.
        /// </summary>
        /// <param name="footprints">The footprints to yield.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
        /// <returns>The stream yielding the same footprints.</returns>
        private static async IAsyncEnumerable<Footprint> OpenBuilding_Stream(IEnumerable<Footprint> footprints, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // One synchronous await keeps the iterator awaitable; it completes inline, so no thread is taken
            // per row.
            await Task.CompletedTask;

            foreach (Footprint footprint in footprints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return footprint;
            }
        }
    }
}
