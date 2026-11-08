using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.PostgreSQL.UI.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.UI.WPF.Interfaces;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The task under test with its execution exposed and its options dialog skipped, so a fact drives a run through the properties rather than through a window a test host cannot open.
        /// </summary>
        private sealed class TestOpenBuildingTask : UIOpenBuildingImportTask
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TestOpenBuildingTask"/> class.
            /// </summary>
            /// <param name="gISPostgreSQLConverterManager">The manager holding the PostgreSQL converters.</param>
            public TestOpenBuildingTask(GISPostgreSQLConverterManager gISPostgreSQLConverterManager)
                : base(gISPostgreSQLConverterManager)
            {
            }

            /// <summary>
            /// Executes the task, exposing the execution the base class keeps protected.
            /// </summary>
            /// <param name="progress">The progress reporter the task reports the rows written to.</param>
            /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
            /// <returns>A task representing the run. The result is the task's own: true unless the run was refused or cancelled.</returns>
            public Task<bool> RunAsync(IProgress<long> progress, CancellationToken cancellationToken)
            {
                return base.ExecuteAsync(progress, cancellationToken);
            }
        }

        /// <summary>
        /// Tests that the six open building import rows are offered on the server tab under the names the operator sees, and nowhere else (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#38).
        /// <para>The registration is what decides whether a row appears, so the registration is what is asserted: constructing the task proves nothing about the tab an operator opens. Exactly six rows are counted - one per dataset - because five would silently drop a dataset from the import and seven would offer one twice.</para>
        /// <para>The task imports through a PostgreSQL converter, so it belongs on the server side, and a missing converter manager has to leave it out rather than produce a row whose only possible outcome is that it could not connect.</para>
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks_UIOpenBuildingImportTask()
        {
            string[] names =
            [
                "Import EUBUCCO buildings",
                "Import Overture buildings",
                "Import OSM buildings",
                "Import DBSM buildings",
                "Import Microsoft buildings",
                "Import GHS-OBAT attributes"
            ];

            // Nothing here reaches a database - the rows are registered, never started.
            List<IVisualBackgroundTask>? visualBackgroundTasks_Server = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_Server);

            List<IVisualBackgroundTask> visualBackgroundTasks_OpenBuilding = [.. visualBackgroundTasks_Server.FindAll(x => x.TypeName == typeof(UIOpenBuildingImportTask).Name)];
            Assert.Equal(6, visualBackgroundTasks_OpenBuilding.Count);

            foreach (string name in names)
            {
                Assert.Contains(visualBackgroundTasks_Server, x => x.Name == name);
            }

            // The client tab holds the tasks driven by the Web API; this one imports into PostgreSQL directly
            // and is not among them.
            List<IVisualBackgroundTask>? visualBackgroundTasks_Client = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_Client);
            Assert.DoesNotContain(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIOpenBuildingImportTask).Name);

            // Without a converter manager there is nothing to import through, so no row at all.
            List<IVisualBackgroundTask>? visualBackgroundTasks_NoManager = Create.VisualBackgroundTasks(null, null, null, Mode.ServerAndClient);
            Assert.NotNull(visualBackgroundTasks_NoManager);
            Assert.Empty(visualBackgroundTasks_NoManager);
        }

        /// <summary>
        /// Verifies the tray wrapper end to end on the throwaway main test database: the options a fact built by hand go through the wrapper's own validation, the SHA-256 is computed from the source file rather than taken as given, the import stores the fixture, and the report the run writes carries the tallies and the register row id (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#38, DiGi-GIS.PostgreSQL#131).
        /// <para>The fact writes a crafted Microsoft line-delimited GeoJSON chunk with two features inside a scratch county square, exactly as the core task's own fact does, but drives it through <see cref="UIOpenBuildingImportTask"/> with <see cref="UIOpenBuildingImportTask.SkipOptionsDialog"/> set - the dialog needs a WPF application a test host has not got. The manager is built from the test configuration file, so the fact writes the throwaway database and never the host's. The scratch county, the register row, the partition, the crafted file and the report are all removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task UIOpenBuildingImportTask_EndToEnd()
        {
            (ConnectionData connectionData_Main, PostgreSQLConfigurationFile configurationFile, _) = TestConnectionData_Main_Test();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            // The manager is built from the test configuration file: the fact writes the throwaway database,
            // never the host's (the task would otherwise read the host conf beside the assembly).
            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            Assert.True(gISPostgreSQLConverterManager.Add(new OpenBuildingPostgreSQLConverter(connectionData_Main), configurationFile));
            Assert.True(gISPostgreSQLConverterManager.Add(new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main), configurationFile));

            AdministrativeAreal2DPostgreSQLConverter? administrativeAreal2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<AdministrativeAreal2DPostgreSQLConverter>();
            Assert.NotNull(administrativeAreal2DPostgreSQLConverter);

            const string reference_County = "I38_OPEN_BUILDING_COUNTY";
            const string code = "38";
            string version = $"TST-{Guid.NewGuid():N}";

            string sourcePath = Path.Combine(Path.GetTempPath(), $"xunit-open-building-ui-{Guid.NewGuid():N}.geojsonl");
            string path_Report = Path.Combine(AppContext.BaseDirectory, "OpenBuilding_Import_Report_microsoft.txt");

            int? countyId = null;

            try
            {
                // A scratch county square of 1000 m, holding the two features the chunk writes around its centre.
                AdministrativeAreal2D administrativeAreal2D = AdministrativeAreal2D_CountyPart(reference_County, code, "open building part", 630000, 520000, 1000);

                HashSet<int>? ids = await administrativeAreal2DPostgreSQLConverter.UpdateAsync([administrativeAreal2D]);
                Assert.NotNull(ids);
                Assert.Single(ids);

                countyId = await GetAdministrativeAreal2DIdAsync(npgsqlConnection, reference_County);

                // A crafted Microsoft chunk: two features around the EPSG:4326 position of the county centre,
                // one line-delimited GeoJSON feature per line, invariant-culture coordinates.
                Point3D? point3D = GIS.Convert.ToEPSG4326(new Point2D(630500, 520500));
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

                PostgreSQLOpenBuildingImportTaskOptions options = new()
                {
                    DatasetKey = "microsoft",
                    SourcePaths = [sourcePath],
                    Quadkey = "03210",
                    CountyIds = [countyId.Value],
                    Version = version,
                    DownloadDate = DateTime.Today
                };

                TestOpenBuildingTask task = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOpenBuildingImportTaskOptions = options,
                    SkipOptionsDialog = true
                };

                // Nothing was typed: the wrapper computes the digest from the file itself before the run.
                Assert.Null(options.Sha256);

                bool succeeded = await task.RunAsync(new Progress<long>(), CancellationToken.None);
                Assert.True(succeeded);

                Assert.Equal(2L, task.RowCount);
                Assert.Equal(0L, task.FilteredCount);
                Assert.Equal(0L, task.RejectionCount);
                Assert.Equal(2L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection, "microsoft", [countyId.Value]));

                // The register row id reaches the caller through the task (DiGi.GIS.PostgreSQL#131) and matches
                // the row the register now holds for this release.
                Assert.NotNull(task.DatasetId);
                Assert.Equal(await OpenBuildingRegisterIdAsync(npgsqlConnection, version), (long?)task.DatasetId);

                // The digest stored on the options is the hash of the file that was imported.
                string sha256 = System.Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(sourcePath)));
                Assert.Equal(sha256, options.Sha256);

                // The report the run leaves beside the test assembly: the release, the tallies and the
                // register row id the acceptance criterion asks for.
                Assert.True(File.Exists(path_Report));
                string[] lines = await File.ReadAllLinesAsync(path_Report);
                Assert.Contains("Dataset: microsoft", lines);
                Assert.Contains($"Version: {version}", lines);
                Assert.Contains($"SHA-256: {sha256}", lines);
                Assert.Contains("Rows written: 2", lines);
                Assert.Contains("Out of scope (filtered): 0", lines);
                Assert.Contains($"Register row id: {task.DatasetId}", lines);
                Assert.Contains("Succeeded: True", lines);
            }
            finally
            {
                // The release and its partition first, then the county part they were filed under.
                if (countyId is not null)
                {
                    if (await OpenBuildingRegisterIdAsync(npgsqlConnection, version) is long)
                    {
                        await using NpgsqlCommand npgsqlCommand_Delete = new($"DELETE FROM {PostgreSQL.Constants.TableName.BuildingDataset} WHERE dataset_key = 'microsoft' AND version = @version;", npgsqlConnection);
                        npgsqlCommand_Delete.Parameters.AddWithValue("version", version);
                        await npgsqlCommand_Delete.ExecuteNonQueryAsync();
                    }

                    string? tableName = PostgreSQL.Query.OpenBuildingTableName("microsoft");
                    Assert.NotNull(tableName);

                    await using NpgsqlCommand npgsqlCommand_Drop = new($"DROP TABLE IF EXISTS {tableName}_{countyId.Value};", npgsqlConnection);
                    await npgsqlCommand_Drop.ExecuteNonQueryAsync();

                    await administrativeAreal2DPostgreSQLConverter.RemoveAsync([countyId.Value]);
                }

                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                }

                if (File.Exists(path_Report))
                {
                    File.Delete(path_Report);
                }
            }

            // Reads one release's register row id back from the database - the figure both the task's
            // DatasetId property and the report have to match, and null when the run never registered one.
            static async Task<long?> OpenBuildingRegisterIdAsync(NpgsqlConnection npgsqlConnection, string version)
            {
                await using NpgsqlCommand npgsqlCommand = new($"SELECT id FROM {PostgreSQL.Constants.TableName.BuildingDataset} WHERE dataset_key = 'microsoft' AND version = @version;", npgsqlConnection);
                npgsqlCommand.Parameters.AddWithValue("version", version);

                object? result = await npgsqlCommand.ExecuteScalarAsync();
                return result is null || result is DBNull ? null : System.Convert.ToInt64(result);
            }
        }
    }
}
