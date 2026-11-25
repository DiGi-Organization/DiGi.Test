using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.PostgreSQL.UI.Enums;
using DiGi.PostgreSQL.Classes;
using DiGi.UI.WPF.Interfaces;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the open building match row is registered once, on the server side only, and builds the tray wrapper (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#57).
        /// <para>The registration decides whether the row appears at all: a task added under the wrong mode builds and tests green while being invisible in the tab an operator opens. Nothing here reaches a database - the row is registered, never started.</para>
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks_UIOpenBuildingMatchTask()
        {
            List<IVisualBackgroundTask>? visualBackgroundTasks_Server = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_Server);

            IVisualBackgroundTask visualBackgroundTask = Assert.Single(visualBackgroundTasks_Server, x => x.TypeName == typeof(UIOpenBuildingMatchTask).Name);
            Assert.Equal("Match open buildings to BDOT10k", visualBackgroundTask.Name);

            List<IVisualBackgroundTask>? visualBackgroundTasks_Client = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_Client);
            Assert.DoesNotContain(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIOpenBuildingMatchTask).Name);

            List<IVisualBackgroundTask>? visualBackgroundTasks_NoManager = Create.VisualBackgroundTasks(null, null, null, Mode.ServerAndClient);
            Assert.NotNull(visualBackgroundTasks_NoManager);
            Assert.DoesNotContain(visualBackgroundTasks_NoManager, x => x.TypeName == typeof(UIOpenBuildingMatchTask).Name);
        }

        /// <summary>
        /// Verifies that the wrapper refuses options the dialog would have refused before the core task touches any database (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#57): GHS-OBAT, a share above one and a missing scope each end the run as failed, and with the dialog shown a manager without converters refuses before any window is asked for.
        /// <para>The manager holds no converter, so a check that let the options through would fail later inside the core task for another reason; the core task's run counters staying at zero and no report being written prove the refusal came first.</para>
        /// </summary>
        [Fact]
        public async Task UIOpenBuildingMatchTask_RefusesBeforeTheRun()
        {
            string path_Report = Path.Combine(AppContext.BaseDirectory, "OpenBuilding_Match_Report.txt");
            if (File.Exists(path_Report))
            {
                File.Delete(path_Report);
            }

            PostgreSQLOpenBuildingMatchTaskOptions[] optionsSets =
            [
                new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [1], DatasetKeys = ["ghs_obat"] },
                new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [1], OpenBuildingMatchParameters = new OpenBuildingMatchParameters(minOverlapShare: 1.5) },
                new PostgreSQLOpenBuildingMatchTaskOptions(),
            ];

            foreach (PostgreSQLOpenBuildingMatchTaskOptions options in optionsSets)
            {
                TestOpenBuildingMatchTask task = new(new GISPostgreSQLConverterManager()) { PostgreSQLOpenBuildingMatchTaskOptions = options, SkipOptionsDialog = true };

                Assert.False(await task.RunAsync(new Progress<long>(), CancellationToken.None));
                Assert.Empty(task.OpenBuildingMatchResults);
                Assert.Equal(0L, task.RowCount);
            }

            // With the dialog: no converter, so no window is asked for and nothing runs.
            TestOpenBuildingMatchTask task_Dialog = new(new GISPostgreSQLConverterManager());
            Assert.False(await task_Dialog.RunAsync(new Progress<long>(), CancellationToken.None));

            Assert.False(File.Exists(path_Report));
        }

        /// <summary>
        /// Verifies the tray wrapper end to end on the throwaway test databases (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#57): a scratch county part with one BDOT10k building on Main and an EUBUCCO footprint over it on Storage, matched through <see cref="UIOpenBuildingMatchTask"/> with the dialog skipped. The run stores one one-to-one link and writes a report naming the dataset, the stored links and the group line; a second run with the links not stored writes nothing and reports that.
        /// <para>The scratch square lies west and south of Poland (EPSG:2180 99500, 99500), so no real county part of the test database reaches it or its margin. The manager is built from the test configuration files, so the fact writes the throwaway databases and never the host's. Everything written is removed in <c>finally</c>. Medium test (two live databases).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task UIOpenBuildingMatchTask_EndToEnd()
        {
            (ConnectionData connectionData_Main, PostgreSQLConfigurationFile configurationFile, _) = TestConnectionData_Main_Test();
            (ConnectionData connectionData_Storage, PostgreSQLConfigurationFile configurationFile_Storage, _) = TestConnectionData_Storage_Test();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection_Storage);
            await npgsqlConnection_Storage.OpenAsync();

            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            Assert.True(gISPostgreSQLConverterManager.Add(new OpenBuildingPostgreSQLConverter(connectionData_Storage), configurationFile_Storage));
            Assert.True(gISPostgreSQLConverterManager.Add(new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main), configurationFile));
            Assert.True(gISPostgreSQLConverterManager.Add(new Building2DPostgreSQLConverter(connectionData_Main), configurationFile));

            AdministrativeAreal2DPostgreSQLConverter? administrativeAreal2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<AdministrativeAreal2DPostgreSQLConverter>();
            Assert.NotNull(administrativeAreal2DPostgreSQLConverter);

            string reference_County = $"I57_OPEN_BUILDING_MATCH_{Guid.NewGuid():N}";
            string code = $"57{Guid.NewGuid():N}";
            string version = $"TST-{Guid.NewGuid():N}";
            string path_Report = Path.Combine(AppContext.BaseDirectory, "OpenBuilding_Match_Report.txt");

            const double x_Scratch = 99500;
            const double y_Scratch = 99500;

            int? countyId = null;

            try
            {
                AdministrativeAreal2D administrativeAreal2D = AdministrativeAreal2D_CountyPart(reference_County, code, "open building match part", x_Scratch, y_Scratch, 1000);

                HashSet<int>? ids = await administrativeAreal2DPostgreSQLConverter.UpdateAsync([administrativeAreal2D]);
                Assert.NotNull(ids);
                Assert.Single(ids);

                countyId = await GetAdministrativeAreal2DIdAsync(npgsqlConnection, reference_County);

                // BDOT10k on Main: one 10 m building.
                PolygonalFace2D? polygonalFace2D_Building = PolygonalFace2D_Square(x_Scratch + 100, y_Scratch + 100, 10);
                Assert.NotNull(polygonalFace2D_Building);

                Assert.True(await new Building2DPostgreSQLConverter(connectionData_Main).CreateTableAsync());
                Assert.True(await PostgreSQL.Create.TableAsync_Building2D_Partition(npgsqlConnection, countyId.Value));

                GIS.Classes.Building2D building2D = new(Guid.NewGuid(), "i57-b1", polygonalFace2D_Building, 1, null, null, []);
                await using (NpgsqlCommand npgsqlCommand_Building = new($"INSERT INTO {PostgreSQL.Constants.TableName.Building2D} (county_id, reference, min_x, min_y, max_x, max_y, object) VALUES (@county_id, 'i57-b1', @min_x, @min_y, @max_x, @max_y, @object::jsonb);", npgsqlConnection))
                {
                    npgsqlCommand_Building.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId.Value });
                    npgsqlCommand_Building.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = x_Scratch + 100 });
                    npgsqlCommand_Building.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = y_Scratch + 100 });
                    npgsqlCommand_Building.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = x_Scratch + 110 });
                    npgsqlCommand_Building.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = y_Scratch + 110 });
                    npgsqlCommand_Building.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Text) { Value = building2D.ToJsonObject()?.ToJsonString() });
                    await npgsqlCommand_Building.ExecuteNonQueryAsync();
                }

                // EUBUCCO on Storage: the same square, the stored object reduced to the geometry the match reads.
                Assert.True(await OpenBuildingPostgreSQLConverter.CreateTablesAsync(npgsqlConnection_Storage));
                int? datasetId = await OpenBuildingPostgreSQLConverter.UpsertReleaseAsync(npgsqlConnection_Storage, new BuildingDatasetRelease("eubucco", version, DateTime.Today, licence: "ODbL-1.0"));
                Assert.NotNull(datasetId);
                Assert.True(await PostgreSQL.Create.TableAsync_OpenBuilding2D_Partition(npgsqlConnection_Storage, PostgreSQL.Constants.TableName.EUBUCCO_Building2D, countyId.Value));

                JsonObject jsonObject_Footprint = new() { ["SourceId"] = "i57-e1", ["SourceSrid"] = 2180, ["Geometry"] = polygonalFace2D_Building.ToJsonObject() };
                await using (NpgsqlCommand npgsqlCommand_Footprint = new($"INSERT INTO {PostgreSQL.Constants.TableName.EUBUCCO_Building2D} (county_id, dataset_id, source_id, primary_source, min_x, min_y, max_x, max_y, object) VALUES (@county_id, @dataset_id, 'i57-e1', 'register', @min_x, @min_y, @max_x, @max_y, @object::jsonb);", npgsqlConnection_Storage))
                {
                    npgsqlCommand_Footprint.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId.Value });
                    npgsqlCommand_Footprint.Parameters.Add(new NpgsqlParameter("dataset_id", NpgsqlDbType.Integer) { Value = datasetId.Value });
                    npgsqlCommand_Footprint.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = x_Scratch + 100 });
                    npgsqlCommand_Footprint.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = y_Scratch + 100 });
                    npgsqlCommand_Footprint.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = x_Scratch + 110 });
                    npgsqlCommand_Footprint.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = y_Scratch + 110 });
                    npgsqlCommand_Footprint.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Text) { Value = jsonObject_Footprint.ToJsonString() });
                    await npgsqlCommand_Footprint.ExecuteNonQueryAsync();
                }

                // 1. Stored.
                TestOpenBuildingMatchTask task = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOpenBuildingMatchTaskOptions = new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [countyId.Value], DatasetKeys = ["eubucco"] },
                    SkipOptionsDialog = true
                };

                Assert.True(await task.RunAsync(new Progress<long>(), CancellationToken.None));
                Assert.Equal(0, task.FailedGroupCount);
                Assert.Equal(1L, task.RowCount);

                OpenBuildingMatchSummaryResult total = Assert.Single(task.OpenBuildingMatchSummaryResults, x => x.PrimarySource is null);
                Assert.Equal(1L, total.OneToOneCount);
                Assert.Equal(1.0, total.IoUMedian!.Value, 9);
                Assert.Equal(1L, await OpenBuildingMatchLinkCountAsync(npgsqlConnection_Storage, countyId.Value));

                Assert.True(File.Exists(path_Report));
                string report = await File.ReadAllTextAsync(path_Report);
                Assert.Contains("Datasets: eubucco", report, StringComparison.Ordinal);
                Assert.Contains("Links: stored", report, StringComparison.Ordinal);
                Assert.Contains($"  eubucco | {countyId.Value} | {datasetId.Value} | 1 | 1 | 0 / 0 | 0 | 1 | 0 | yes", report, StringComparison.Ordinal);
                Assert.Contains("Succeeded: True", report, StringComparison.Ordinal);

                // 2. Not stored: the link table keeps the one row, and the report says nothing was written.
                await using (NpgsqlCommand npgsqlCommand_Clear = new($"DELETE FROM {PostgreSQL.Constants.TableName.BuildingDatasetMatch} WHERE county_id = @county_id;", npgsqlConnection_Storage))
                {
                    npgsqlCommand_Clear.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId.Value });
                    await npgsqlCommand_Clear.ExecuteNonQueryAsync();
                }

                TestOpenBuildingMatchTask task_Sensitivity = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOpenBuildingMatchTaskOptions = new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [countyId.Value], DatasetKeys = ["eubucco"], Persist = false },
                    SkipOptionsDialog = true
                };

                Assert.True(await task_Sensitivity.RunAsync(new Progress<long>(), CancellationToken.None));
                Assert.Equal(0L, await OpenBuildingMatchLinkCountAsync(npgsqlConnection_Storage, countyId.Value));
                Assert.Single(task_Sensitivity.OpenBuildingMatchSummaryResults, x => x.PrimarySource is null && x.OneToOneCount == 1);
                Assert.Contains("Links: not stored", await File.ReadAllTextAsync(path_Report), StringComparison.Ordinal);
            }
            finally
            {
                if (countyId is int countyId_Temp)
                {
                    await using (NpgsqlCommand npgsqlCommand_Storage = new($"DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.BuildingDatasetMatch}_{countyId_Temp}; DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.EUBUCCO_Building2D}_{countyId_Temp}; DELETE FROM {PostgreSQL.Constants.TableName.BuildingDataset} WHERE dataset_key = 'eubucco' AND version = @version;", npgsqlConnection_Storage))
                    {
                        npgsqlCommand_Storage.Parameters.AddWithValue("version", version);
                        await npgsqlCommand_Storage.ExecuteNonQueryAsync();
                    }

                    await using (NpgsqlCommand npgsqlCommand_Main = new($"DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.Building2D}_{countyId_Temp};", npgsqlConnection))
                    {
                        await npgsqlCommand_Main.ExecuteNonQueryAsync();
                    }

                    await administrativeAreal2DPostgreSQLConverter.RemoveAsync([countyId_Temp]);
                }

                if (File.Exists(path_Report))
                {
                    File.Delete(path_Report);
                }
            }
        }

        /// <summary>
        /// Counts the stored link rows of one county part.
        /// </summary>
        /// <param name="npgsqlConnection_Storage">The open connection to the throwaway storage test database.</param>
        /// <param name="countyId">The county part.</param>
        /// <returns>The number of link rows.</returns>
        private static async Task<long> OpenBuildingMatchLinkCountAsync(NpgsqlConnection npgsqlConnection_Storage, int countyId)
        {
            await using NpgsqlCommand npgsqlCommand = new($"SELECT count(*) FROM {PostgreSQL.Constants.TableName.BuildingDatasetMatch} WHERE county_id = @county_id;", npgsqlConnection_Storage);
            npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId });

            return (long)(await npgsqlCommand.ExecuteScalarAsync() ?? 0L);
        }

        /// <summary>
        /// The match wrapper with its protected execution exposed, so a fact can run it and read the result.
        /// </summary>
        private sealed class TestOpenBuildingMatchTask : UIOpenBuildingMatchTask
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TestOpenBuildingMatchTask"/> class.
            /// </summary>
            /// <param name="gISPostgreSQLConverterManager">The manager holding the PostgreSQL converters.</param>
            public TestOpenBuildingMatchTask(GISPostgreSQLConverterManager gISPostgreSQLConverterManager)
                : base(gISPostgreSQLConverterManager)
            {
            }

            /// <summary>
            /// Executes the task, exposing the execution the base class keeps protected.
            /// </summary>
            /// <param name="progress">The progress reporter the task reports the link rows to.</param>
            /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
            /// <returns>A task representing the run; its result is the task's own.</returns>
            public Task<bool> RunAsync(IProgress<long> progress, CancellationToken cancellationToken)
            {
                return base.ExecuteAsync(progress, cancellationToken);
            }
        }
    }
}
