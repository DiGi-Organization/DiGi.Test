using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway main test database, the import task itself end to end, once scoped by county list and once by bounding box (DiGi.GIS.PostgreSQL#125).
        /// <para>The fact writes a crafted Microsoft line-delimited GeoJSON chunk with two features inside the scratch county square, so the task runs its real path: option validation, the reader it dispatches for the dataset, the sibling widening of a named county, the SQL bounding-box prefilter that resolves a pilot box to its counties, the reprojection with the run's transformation, filing, binary COPY and the register - both runs storing two rows with <c>primary_source = 'microsoft'</c>, the second run replacing the first's snapshot in the same partition, and each run keeping its own register row whose id the task surfaces as <see cref="PostgreSQLOpenBuildingImportTask.DatasetId"/> (DiGi.GIS.PostgreSQL#131). The manager is built from the test configuration file, so the fact writes the throwaway database and never the host's. The scratch county, both partitions, both register rows and the crafted file are removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PostgreSQLOpenBuildingImportTask_BothScopes()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            string reference = $"xunit-open-building-{Guid.NewGuid():N}";
            string code = $"TST{Guid.NewGuid():N}";
            string version_County = $"TST-{Guid.NewGuid():N}";
            string version_Box = $"TST-{Guid.NewGuid():N}";
            string sourcePath = Path.Combine(Path.GetTempPath(), $"xunit-open-building-{Guid.NewGuid():N}.geojsonl");
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection, reference, code);

                // A crafted Microsoft chunk: two features around the EPSG:4326 position of the scratch centre,
                // one line-delimited GeoJSON feature per line, invariant-culture coordinates.
                Point3D? point3D = GIS.Convert.ToEPSG4326(new Point2D(openBuilding_ScratchX, openBuilding_ScratchY));
                Assert.NotNull(point3D);

                double halfSize = 0.00005;
                string longitude_Min = (point3D.X - halfSize).ToString(CultureInfo.InvariantCulture);
                string longitude_Max = (point3D.X + halfSize).ToString(CultureInfo.InvariantCulture);
                string latitude_Min = (point3D.Y - halfSize).ToString(CultureInfo.InvariantCulture);
                string latitude_Max = (point3D.Y + halfSize).ToString(CultureInfo.InvariantCulture);

                string Feature(int index) => string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"type\":\"Feature\",\"properties\":{{\"height\":{0},\"capture\":\"2019-07\"}},\"geometry\":{{\"type\":\"Polygon\",\"coordinates\":[[[{1},{2}],[{3},{2}],[{3},{4}],[{1},{4}],[{1},{2}]]]}}}}",
                    10 + index,
                    longitude_Min,
                    latitude_Min,
                    longitude_Max,
                    latitude_Max);

                File.WriteAllLines(sourcePath, [Feature(0), Feature(1)]);

                // The manager is built from the test configuration file: the fact writes the throwaway
                // database, never the host's (the task would otherwise read the host conf beside the assembly).
                string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Assert.NotNull(directory);

                PostgreSQLConfigurationFile? configurationFile = DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(Path.Combine(directory!, fileName_PostgreSQL_Main_Test));
                Assert.NotNull(configurationFile);

                GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
                Assert.True(gISPostgreSQLConverterManager.Add(new OpenBuildingPostgreSQLConverter(connectionData_Main), configurationFile));
                Assert.True(gISPostgreSQLConverterManager.Add(new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main), configurationFile));

                // Run 1: county-list scope. The task widens the named part to its siblings, opens the Microsoft
                // reader for the chunk and stores both features.
                PostgreSQLOpenBuildingImportTask postgreSQLOpenBuildingImportTask_County = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOpenBuildingImportTaskOptions = new PostgreSQLOpenBuildingImportTaskOptions
                    {
                        DatasetKey = "microsoft",
                        SourcePaths = [sourcePath],
                        Quadkey = "03210",
                        CountyIds = [countyId.Value],
                        Version = version_County,
                        DownloadDate = DateTime.Today,
                        Licence = "MS-FT-ODbL",
                        FileName = "xunit-chunk.geojsonl",
                        Sha256 = "xunit-sha",
                    }
                };

                // No run has completed on the task, so the register row id is still absent.
                Assert.Null(postgreSQLOpenBuildingImportTask_County.DatasetId);

                await RunAsync(postgreSQLOpenBuildingImportTask_County);

                Assert.Equal(2L, postgreSQLOpenBuildingImportTask_County.RowCount);
                Assert.Equal(0L, postgreSQLOpenBuildingImportTask_County.FilteredCount);
                Assert.Equal(0L, postgreSQLOpenBuildingImportTask_County.RejectionCount);
                Assert.Equal(2L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "microsoft", [countyId.Value]));

                // The result carries the register row id (DiGi.GIS.PostgreSQL#131): null before the run,
                // and after it equal to the row the register holds for this release.
                Assert.NotNull(postgreSQLOpenBuildingImportTask_County.DatasetId);
                Assert.Equal(await RegisterIdAsync(version_County), (long?)postgreSQLOpenBuildingImportTask_County.DatasetId);

                // Run 2: bounding-box scope over the same square. The SQL prefilter resolves the pilot box to
                // the same county, and the replacement of the snapshot leaves exactly this run's two rows.
                PostgreSQLOpenBuildingImportTask postgreSQLOpenBuildingImportTask_Box = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOpenBuildingImportTaskOptions = new PostgreSQLOpenBuildingImportTaskOptions
                    {
                        DatasetKey = "microsoft",
                        SourcePaths = [sourcePath],
                        Quadkey = "03210",
                        BoundingBox2D = new BoundingBox2D(
                            new Point2D(openBuilding_ScratchX - openBuilding_ScratchHalfSize, openBuilding_ScratchY - openBuilding_ScratchHalfSize),
                            new Point2D(openBuilding_ScratchX + openBuilding_ScratchHalfSize, openBuilding_ScratchY + openBuilding_ScratchHalfSize)),
                        Version = version_Box,
                        DownloadDate = DateTime.Today,
                    }
                };

                await RunAsync(postgreSQLOpenBuildingImportTask_Box);

                Assert.Equal(2L, postgreSQLOpenBuildingImportTask_Box.RowCount);
                Assert.Equal(0L, postgreSQLOpenBuildingImportTask_Box.FilteredCount);
                Assert.Equal(0L, postgreSQLOpenBuildingImportTask_Box.RejectionCount);
                Assert.Equal(2L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "microsoft", [countyId.Value]));

                // The second run registers its own release and answers its own row id (DiGi.GIS.PostgreSQL#131).
                Assert.NotNull(postgreSQLOpenBuildingImportTask_Box.DatasetId);
                Assert.NotEqual(postgreSQLOpenBuildingImportTask_County.DatasetId, postgreSQLOpenBuildingImportTask_Box.DatasetId);
                Assert.Equal(await RegisterIdAsync(version_Box), (long?)postgreSQLOpenBuildingImportTask_Box.DatasetId);

                // The rows carry the normalised primary source of the chunk's provenance, and both releases
                // keep their own register row.
                string commandText = $@"SELECT count(*) FROM {Constants.TableName.Microsoft_Building2D}
                    WHERE county_id = {countyId.Value} AND primary_source = 'register';";
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection, commandText));

                commandText = $@"SELECT count(*) FROM {Constants.TableName.Microsoft_Building2D}
                    WHERE county_id = {countyId.Value} AND primary_source = 'microsoft';";
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, commandText));

                Assert.Equal(1L, await OpenBuildingPostgreSQLConverter.GetReleaseCountAsync(npgsqlConnection, "microsoft", version_County, DateTime.Today));
                Assert.Equal(1L, await OpenBuildingPostgreSQLConverter.GetReleaseCountAsync(npgsqlConnection, "microsoft", version_Box, DateTime.Today));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, [reference], [version_County, version_Box], countyId, "microsoft");

                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }
            }

            // Reads one release's register row id back from the database - the figure the task's DatasetId
            // property has to match (DiGi.GIS.PostgreSQL#131). The fact only ever asks for releases it
            // created with a unique version, so the key resolves to their row.
            async Task<long?> RegisterIdAsync(string version)
            {
                object? result = await ScalarAsync(npgsqlConnection, $"SELECT id FROM {Constants.TableName.BuildingDataset} WHERE dataset_key = 'microsoft' AND version = '{version}' AND download_date = DATE '{DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}';");
                return result is null || result is DBNull ? null : System.Convert.ToInt64(result, CultureInfo.CurrentCulture);
            }
        }

        /// <summary>
        /// Starts an import task and waits for it to stop, then asserts it completed without an exception and reported success.
        /// </summary>
        /// <param name="postgreSQLOpenBuildingImportTask">The task to run.</param>
        /// <returns>A task that completes when the run has finished and its outcome has been asserted.</returns>
        private static async Task RunAsync(PostgreSQLOpenBuildingImportTask postgreSQLOpenBuildingImportTask)
        {
            // The base class starts the work without handing back anything to await, so completion is taken
            // from the event it raises.
            TaskCompletionSource<bool> taskCompletionSource = new();
            postgreSQLOpenBuildingImportTask.Stopped += (object? sender, EventArgs e) => taskCompletionSource.TrySetResult(true);

            postgreSQLOpenBuildingImportTask.Start();

            await taskCompletionSource.Task;

            Assert.Null(postgreSQLOpenBuildingImportTask.Exception);
            Assert.True(postgreSQLOpenBuildingImportTask.IsSucceeded);
        }
    }
}
