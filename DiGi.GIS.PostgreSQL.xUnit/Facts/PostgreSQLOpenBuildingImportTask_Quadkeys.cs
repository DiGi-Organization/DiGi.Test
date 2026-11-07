using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway main test database, that one Microsoft run over two chunks published under different quadkeys stores every footprint once, under its own chunk's quadkey (DiGi-Organization/DiGi.GIS.PostgreSQL#132).
        /// <para>Before #132 the run handed a single typed quadkey to every file, and the reader restarts the feature index per file, so the two chunks' <c>quadkey:index</c> source identifiers collided and every row's provenance named one chunk. The fact writes two crafted chunks of two features each into <c>quadkey=&lt;digits&gt;</c> folders, runs the task with no typed quadkey and asserts four rows, four distinct source identifiers - two per quadkey - and the provenance quadkey of each row. A second run with two files sharing a typed quadkey is refused before anything is written. The scratch county, its partition, the register row and the crafted files are removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PostgreSQLOpenBuildingImportTask_Quadkeys()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            string reference = $"xunit-open-building-{Guid.NewGuid():N}";
            string code = $"TST{Guid.NewGuid():N}";
            string version = $"TST-{Guid.NewGuid():N}";
            string version_Refused = $"TST-{Guid.NewGuid():N}";
            string directory_Root = Path.Combine(Path.GetTempPath(), $"xunit-open-building-{Guid.NewGuid():N}");
            string path_A = Path.Combine(directory_Root, "quadkey=0321001", "part-a.geojsonl");
            string path_B = Path.Combine(directory_Root, "quadkey=0321002", "part-b.geojsonl");
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection, reference, code);

                Point3D? point3D = GIS.Convert.ToEPSG4326(new Point2D(openBuilding_ScratchX, openBuilding_ScratchY));
                Assert.NotNull(point3D);

                double halfSize = 0.00005;
                string longitude_Min = (point3D.X - halfSize).ToString(CultureInfo.InvariantCulture);
                string longitude_Max = (point3D.X + halfSize).ToString(CultureInfo.InvariantCulture);
                string latitude_Min = (point3D.Y - halfSize).ToString(CultureInfo.InvariantCulture);
                string latitude_Max = (point3D.Y + halfSize).ToString(CultureInfo.InvariantCulture);

                string Feature(int index) => string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"type\":\"Feature\",\"properties\":{{\"height\":{0}}},\"geometry\":{{\"type\":\"Polygon\",\"coordinates\":[[[{1},{2}],[{3},{2}],[{3},{4}],[{1},{4}],[{1},{2}]]]}}}}",
                    10 + index,
                    longitude_Min,
                    latitude_Min,
                    longitude_Max,
                    latitude_Max);

                Directory.CreateDirectory(Path.GetDirectoryName(path_A)!);
                Directory.CreateDirectory(Path.GetDirectoryName(path_B)!);
                File.WriteAllLines(path_A, [Feature(0), Feature(1)]);
                File.WriteAllLines(path_B, [Feature(0), Feature(1)]);

                string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Assert.NotNull(directory);

                PostgreSQLConfigurationFile? configurationFile = DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(Path.Combine(directory!, fileName_PostgreSQL_Main_Test));
                Assert.NotNull(configurationFile);

                GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
                Assert.True(gISPostgreSQLConverterManager.Add(new OpenBuildingPostgreSQLConverter(connectionData_Main), configurationFile));
                Assert.True(gISPostgreSQLConverterManager.Add(new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main), configurationFile));

                // One run, two chunks, no typed quadkey: each file is read under its own folder's quadkey.
                PostgreSQLOpenBuildingImportTask postgreSQLOpenBuildingImportTask = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOpenBuildingImportTaskOptions = new PostgreSQLOpenBuildingImportTaskOptions
                    {
                        DatasetKey = "microsoft",
                        SourcePaths = [path_A, path_B],
                        CountyIds = [countyId.Value],
                        Version = version,
                        DownloadDate = DateTime.Today,
                    }
                };

                await RunAsync(postgreSQLOpenBuildingImportTask);

                Assert.Equal(4L, postgreSQLOpenBuildingImportTask.RowCount);
                Assert.Equal(0L, postgreSQLOpenBuildingImportTask.RejectionCount);

                string tableName = Constants.TableName.Microsoft_Building2D;
                Assert.Equal(4L, await ScalarAsync(npgsqlConnection, $"SELECT count(DISTINCT source_id) FROM {tableName} WHERE county_id = {countyId.Value};"));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {tableName} WHERE county_id = {countyId.Value} AND source_id LIKE '0321001:%' AND provenance->>'quadkey' = '0321001';"));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {tableName} WHERE county_id = {countyId.Value} AND source_id LIKE '0321002:%' AND provenance->>'quadkey' = '0321002';"));

                // Two files under one typed quadkey (neither in a quadkey folder) would collide: the run is
                // refused before the partition is touched, so the four rows above survive it.
                string path_PlainA = Path.Combine(directory_Root, "plain-a.geojsonl");
                string path_PlainB = Path.Combine(directory_Root, "plain-b.geojsonl");
                File.WriteAllLines(path_PlainA, [Feature(0)]);
                File.WriteAllLines(path_PlainB, [Feature(0)]);

                PostgreSQLOpenBuildingImportTask postgreSQLOpenBuildingImportTask_Refused = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOpenBuildingImportTaskOptions = new PostgreSQLOpenBuildingImportTaskOptions
                    {
                        DatasetKey = "microsoft",
                        SourcePaths = [path_PlainA, path_PlainB],
                        Quadkey = "03210",
                        CountyIds = [countyId.Value],
                        Version = version_Refused,
                        DownloadDate = DateTime.Today,
                    }
                };

                TaskCompletionSource<bool> taskCompletionSource = new();
                postgreSQLOpenBuildingImportTask_Refused.Stopped += (object? sender, EventArgs e) => taskCompletionSource.TrySetResult(true);
                postgreSQLOpenBuildingImportTask_Refused.Start();
                await taskCompletionSource.Task;

                Assert.False(postgreSQLOpenBuildingImportTask_Refused.IsSucceeded);
                Assert.Equal(4L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "microsoft", [countyId.Value]));
                Assert.Equal(0L, await OpenBuildingPostgreSQLConverter.GetReleaseCountAsync(npgsqlConnection, "microsoft", version_Refused, DateTime.Today));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, [reference], [version, version_Refused], countyId, "microsoft");

                if (Directory.Exists(directory_Root))
                {
                    Directory.Delete(directory_Root, true);
                }
            }
        }
    }
}
