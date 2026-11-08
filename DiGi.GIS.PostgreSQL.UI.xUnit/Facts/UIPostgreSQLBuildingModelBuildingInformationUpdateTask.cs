using DiGi.Analytical.Building.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Analytical;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.PostgreSQL.UI.Enums;
using DiGi.PostgreSQL.Classes;
using DiGi.UI.WPF.Interfaces;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The task under test with its execution exposed and its options dialog skipped, so a fact drives a run through the properties rather than through a window a test host cannot open.
        /// </summary>
        private sealed class TestTask : UIPostgreSQLBuildingModelBuildingInformationUpdateTask
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TestTask"/> class.
            /// </summary>
            /// <param name="gISPostgreSQLConverterManager">The manager holding the PostgreSQL converters.</param>
            public TestTask(GISPostgreSQLConverterManager gISPostgreSQLConverterManager)
                : base(gISPostgreSQLConverterManager)
            {
            }

            /// <summary>
            /// Executes the task, exposing the execution the base class keeps protected.
            /// </summary>
            /// <param name="progress">The progress reporter the task reports the scanned rows to.</param>
            /// <param name="cancellationToken">The cancellation token to observe.</param>
            /// <returns>A task representing the run. The result is the task's own: true when no part failed.</returns>
            public Task<bool> RunAsync(IProgress<long> progress, CancellationToken cancellationToken)
            {
                return base.ExecuteAsync(progress, cancellationToken);
            }
        }

        /// <summary>
        /// Tests that the BuildingInformation backfill task is offered on the server tab under the name the operator sees, and nowhere else.
        /// <para>The task reads the stored building models through the storage-database converter and writes them back, so it belongs on the server side beside the model task it completes, and a missing converter manager has to leave it out rather than produce a row that throws when it is clicked.</para>
        /// <para>The registration is what decides whether the row appears, so the registration is what is asserted: constructing the task proves nothing about the tab an operator opens. The row is registered dry-run, and the label is derived from <c>DryRun</c> rather than written beside it, so a row that stamps fifteen million rows cannot end up labelled as a report - the fact pins the label of the state the registration sets.</para>
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks_UIPostgreSQLBuildingModelBuildingInformationUpdateTask()
        {
            // Nothing here reaches a database - the task is registered, never started.
            List<IVisualBackgroundTask>? visualBackgroundTasks_Server = DiGi.GIS.PostgreSQL.UI.Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_Server);
            Assert.Contains(visualBackgroundTasks_Server, x => x.TypeName == typeof(UIPostgreSQLBuildingModelBuildingInformationUpdateTask).Name && x.Name == "Report BuildingModels missing BuildingInformation (dry run, writes nothing)");

            // The client tab holds the tasks driven by the Web API; this one is not among them.
            List<IVisualBackgroundTask>? visualBackgroundTasks_Client = DiGi.GIS.PostgreSQL.UI.Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_Client);
            Assert.DoesNotContain(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIPostgreSQLBuildingModelBuildingInformationUpdateTask).Name);

            // Without a converter manager there is nothing to read the stored models with, so the row must not be offered at all.
            List<IVisualBackgroundTask>? visualBackgroundTasks_NoManager = DiGi.GIS.PostgreSQL.UI.Create.VisualBackgroundTasks(null, null, null, Mode.ServerAndClient);
            Assert.NotNull(visualBackgroundTasks_NoManager);
            Assert.Empty(visualBackgroundTasks_NoManager);
        }

        /// <summary>
        /// Verifies the whole task against a database: a dry run changes no row and writes the report, the real run stamps exactly the row that carries no coordinates, leaves the already-correct and the rejected rows alone, checkpoints both parts, and a resumed run updates nothing.
        /// <para>Two scratch county parts of one code hold three stored models built through the stamping leaf: one whose <c>BuildingInformation</c> was cleared afterwards (the row a national backfill exists for), one stamped at creation, and one whose footprint sits at the EPSG:2180 origin and converts outside Poland. The parts are seeded into <c>administrative_areal_2d</c> and the models into <c>building_model_component</c> through the converters the application itself uses, and removed in <see langword="finally"/>.</para>
        /// <para>Skipped by default: it writes to a database. Point <c>GIS_PostgreSQL_Main.conf</c> and <c>GIS_PostgreSQL_Storage.conf</c> at scratch databases before running.</para>
        /// </summary>
        [Fact(Skip = "Writes to a database. Point GIS_PostgreSQL_Main.conf and GIS_PostgreSQL_Storage.conf at scratch databases before running.")]
        public async Task UIPostgreSQLBuildingModelBuildingInformationUpdateTask_DryRunRealRunResume()
        {
            const string code = "99";

            const string reference_County_A = "I17_COUNTY_PART_A";
            const string reference_County_B = "I17_COUNTY_PART_B";

            const string reference_Model_A1 = "I17_MODEL_A1";
            const string reference_Model_A2 = "I17_MODEL_A2";
            const string reference_Model_B1 = "I17_MODEL_B1";

            const string fileName_CSV = "BuildingModels_BuildingInformation.csv";
            const string fileName_Checkpoint = "BuildingModels_BuildingInformation_Checkpoint.txt";
            const string fileName_Summary = "BuildingModels_BuildingInformation_Summary.txt";

            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = DiGi.GIS.PostgreSQL.Create.GISPostgreSQLConverterManager();
            Assert.NotNull(gISPostgreSQLConverterManager);

            AdministrativeAreal2DPostgreSQLConverter? administrativeAreal2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<AdministrativeAreal2DPostgreSQLConverter>();
            Assert.NotNull(administrativeAreal2DPostgreSQLConverter);

            BuildingModelPostgreSQLConverter? buildingModelPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<BuildingModelPostgreSQLConverter>();
            Assert.NotNull(buildingModelPostgreSQLConverter);

            ConnectionData? connectionData = administrativeAreal2DPostgreSQLConverter.ConnectionData;
            Assert.NotNull(connectionData);

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
            Assert.NotNull(npgsqlConnection);

            await npgsqlConnection.OpenAsync();

            // Two parts of one code, far apart, so the two partitions never share a row. The code is never
            // consulted by the scope - the county ids are - so a scratch code is fine.
            AdministrativeAreal2D administrativeAreal2D_A = AdministrativeAreal2D_CountyPart(reference_County_A, code, "part A", 0, 0, 100);
            AdministrativeAreal2D administrativeAreal2D_B = AdministrativeAreal2D_CountyPart(reference_County_B, code, "part B", 1000, 0, 100);

            HashSet<int>? ids = await administrativeAreal2DPostgreSQLConverter.UpdateAsync([administrativeAreal2D_A, administrativeAreal2D_B]);
            Assert.NotNull(ids);
            Assert.Equal(2, ids.Count);

            int countyId_A = await GetAdministrativeAreal2DIdAsync(npgsqlConnection, reference_County_A);
            int countyId_B = await GetAdministrativeAreal2DIdAsync(npgsqlConnection, reference_County_B);

            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            try
            {
                // The footprint known to convert into Poland, from the sliver fixture of the analytical facts.
                PolygonalFace2D? polygonalFace2D_Warsaw = Geometry.Planar.Create.PolygonalFace2D(new Point2D(331032.97, 539122.27), new Point2D(331030.36, 539122.98), new Point2D(331030.79, 539124.54), new Point2D(331033.39, 539123.84));
                Assert.NotNull(polygonalFace2D_Warsaw);

                // A footprint at the EPSG:2180 origin: its centre converts outside the Polish range, which is
                // the state the stamping method refuses and the run must report as rejected, not stamp.
                PolygonalFace2D? polygonalFace2D_Origin = PolygonalFace2D_Square(0, 0, 10);
                Assert.NotNull(polygonalFace2D_Origin);

                GIS.Classes.Building2D building2D_A1 = new(Guid.NewGuid(), reference_Model_A1, polygonalFace2D_Warsaw, 1, null, null, []);
                GIS.Classes.Building2D building2D_A2 = new(Guid.NewGuid(), reference_Model_A2, polygonalFace2D_Warsaw, 1, null, null, []);
                GIS.Classes.Building2D building2D_B1 = new(Guid.NewGuid(), reference_Model_B1, polygonalFace2D_Origin, 1, null, null, []);

                DiGi.Analytical.Building.Classes.BuildingModel? buildingModel_Analytical_A1 = DiGi.GIS.Analytical.Create.BuildingModel(building2D_A1);
                Assert.NotNull(buildingModel_Analytical_A1);

                DiGi.Analytical.Building.Classes.BuildingModel? buildingModel_Analytical_A2 = DiGi.GIS.Analytical.Create.BuildingModel(building2D_A2);
                Assert.NotNull(buildingModel_Analytical_A2);

                DiGi.Analytical.Building.Classes.BuildingModel? buildingModel_Analytical_B1 = DiGi.GIS.Analytical.Create.BuildingModel(building2D_B1);
                Assert.NotNull(buildingModel_Analytical_B1);

                // A2 came out of the stamping leaf, so it is already at its target values: the AlreadyCorrect state.
                Assert.True(buildingModel_Analytical_A2.IsLocated());

                // B1 is the model the leaf cannot locate: its footprint converts outside Poland, so it is left unlocated.
                Assert.False(buildingModel_Analytical_B1.IsLocated());

                // A1 is the row the backfill exists for: the same model with its information cleared to the unlocated default.
                buildingModel_Analytical_A1.BuildingInformation = new DiGi.Analytical.Building.Classes.BuildingInformation();
                Assert.False(buildingModel_Analytical_A1.IsLocated());

                BuildingModel buildingModel_Row_A1 = new()
                {
                    CountyId = countyId_A,
                    UniqueId = buildingModel_Analytical_A1.UniqueId,
                    Reference = reference_Model_A1,
                    Object = buildingModel_Analytical_A1.ToJsonObject()
                };

                BuildingModel buildingModel_Row_A2 = new()
                {
                    CountyId = countyId_A,
                    UniqueId = buildingModel_Analytical_A2.UniqueId,
                    Reference = reference_Model_A2,
                    Object = buildingModel_Analytical_A2.ToJsonObject()
                };

                BuildingModel buildingModel_Row_B1 = new()
                {
                    CountyId = countyId_B,
                    UniqueId = buildingModel_Analytical_B1.UniqueId,
                    Reference = reference_Model_B1,
                    Object = buildingModel_Analytical_B1.ToJsonObject()
                };

                PostgreSQLUpdateResult? result_A = await buildingModelPostgreSQLConverter.UpdateAsync([buildingModel_Row_A1, buildingModel_Row_A2]);
                Assert.NotNull(result_A);
                Assert.Equal(2, result_A.Ids.Count);
                Assert.True(buildingModel_Row_A1.Id > 0);
                Assert.True(buildingModel_Row_A2.Id > 0);

                PostgreSQLUpdateResult? result_B = await buildingModelPostgreSQLConverter.UpdateAsync([buildingModel_Row_B1]);
                Assert.NotNull(result_B);
                Assert.Single(result_B.Ids);
                Assert.True(buildingModel_Row_B1.Id > 0);

                long count_A = await buildingModelPostgreSQLConverter.GetCountAsync(countyId_A);
                long count_B = await buildingModelPostgreSQLConverter.GetCountAsync(countyId_B);
                Assert.Equal(2, count_A);
                Assert.Equal(1, count_B);

                Directory.CreateDirectory(directory);

                string path_CSV = Path.Combine(directory, fileName_CSV);
                string path_Checkpoint = Path.Combine(directory, fileName_Checkpoint);
                string path_Summary = Path.Combine(directory, fileName_Summary);

                TestTask task_DryRun = new(gISPostgreSQLConverterManager)
                {
                    CountyIds = [countyId_A, countyId_B],
                    BuildingModelDetailLevel = BuildingModelDetailLevel.Component,
                    DryRun = true,
                    BatchSize = 500,
                    CommandTimeout = 120,
                    Resume = true,
                    ReportDirectory = directory,
                    SkipOptionsDialog = true
                };

                bool succeeded_DryRun = await task_DryRun.RunAsync(new Progress<long>(), CancellationToken.None);
                Assert.True(succeeded_DryRun);

                // Nothing was stamped: the cleared model is still cleared, the stamped one is still in place, and the origin model is still unlocated.
                DiGi.Analytical.Building.Classes.BuildingModel model_A1_DryRun = await GetAnalyticalModelAsync(buildingModelPostgreSQLConverter, buildingModel_Row_A1.Id, countyId_A);
                Assert.False(model_A1_DryRun.IsLocated());

                DiGi.Analytical.Building.Classes.BuildingModel model_A2_DryRun = await GetAnalyticalModelAsync(buildingModelPostgreSQLConverter, buildingModel_Row_A2.Id, countyId_A);
                Assert.True(model_A2_DryRun.IsLocated());

                DiGi.Analytical.Building.Classes.BuildingModel model_B1_DryRun = await GetAnalyticalModelAsync(buildingModelPostgreSQLConverter, buildingModel_Row_B1.Id, countyId_B);
                Assert.False(model_B1_DryRun.IsLocated());

                Assert.Equal(count_A, await buildingModelPostgreSQLConverter.GetCountAsync(countyId_A));
                Assert.Equal(count_B, await buildingModelPostgreSQLConverter.GetCountAsync(countyId_B));

                // The report the dry run leaves behind: the rejected row is the only one listed, the to-update row is counted, not written, and no row was touched at all.
                Assert.True(File.Exists(path_CSV));
                string[] lines_CSV_DryRun = await File.ReadAllLinesAsync(path_CSV);
                Assert.Equal(2, lines_CSV_DryRun.Length);
                Assert.Equal("Code;CountyId;Table;Id;Reference;Status;Reason", lines_CSV_DryRun[0]);
                Assert.Contains(reference_Model_B1, lines_CSV_DryRun[1]);
                Assert.Contains("Rejected", lines_CSV_DryRun[1]);
                Assert.Contains("OutsidePolishRange", lines_CSV_DryRun[1]);

                // A dry run neither reads nor writes the checkpoint: a real run after it must still touch every part.
                Assert.False(File.Exists(path_Checkpoint));

                Assert.True(File.Exists(path_Summary));
                string[] lines_Summary_DryRun = await File.ReadAllLinesAsync(path_Summary);
                Assert.Contains($"building_model_component;{code};{countyId_A};2;1;1;0;0", lines_Summary_DryRun);
                Assert.Contains($"building_model_component;{code};{countyId_B};1;0;0;1;0", lines_Summary_DryRun);
                Assert.Contains("(all tables);3;1;1;1;0", lines_Summary_DryRun);

                TestTask task_RealRun = new(gISPostgreSQLConverterManager)
                {
                    CountyIds = [countyId_A, countyId_B],
                    BuildingModelDetailLevel = BuildingModelDetailLevel.Component,
                    DryRun = false,
                    BatchSize = 500,
                    CommandTimeout = 120,
                    Resume = true,
                    ReportDirectory = directory,
                    SkipOptionsDialog = true
                };

                bool succeeded_RealRun = await task_RealRun.RunAsync(new Progress<long>(), CancellationToken.None);
                Assert.True(succeeded_RealRun);

                // The cleared model is stamped: WGS 84 coordinates inside Poland and the fixed Polish standard-time offset.
                DiGi.Analytical.Building.Classes.BuildingModel model_A1_Stamped = await GetAnalyticalModelAsync(buildingModelPostgreSQLConverter, buildingModel_Row_A1.Id, countyId_A);
                Assert.True(model_A1_Stamped.IsLocated());

                DiGi.Analytical.Building.Classes.BuildingInformation buildingInformation_A1 = model_A1_Stamped.BuildingInformation;
                Assert.NotNull(buildingInformation_A1.Coordinates);
                Assert.True(buildingInformation_A1.Coordinates!.Latitude >= 48.9 && buildingInformation_A1.Coordinates.Latitude <= 55.0);
                Assert.True(buildingInformation_A1.Coordinates.Longitude >= 14.05 && buildingInformation_A1.Coordinates.Longitude <= 24.25);
                Assert.Equal(DiGi.Core.Enums.UTC.Plus0100, buildingInformation_A1.UTC);

                // The other two rows are exactly as the dry run left them: the run stamps one JSON key of one row, no more.
                DiGi.Analytical.Building.Classes.BuildingModel model_A2_RealRun = await GetAnalyticalModelAsync(buildingModelPostgreSQLConverter, buildingModel_Row_A2.Id, countyId_A);
                Assert.True(model_A2_RealRun.IsLocated());

                DiGi.Analytical.Building.Classes.BuildingModel model_B1_RealRun = await GetAnalyticalModelAsync(buildingModelPostgreSQLConverter, buildingModel_Row_B1.Id, countyId_B);
                Assert.False(model_B1_RealRun.IsLocated());

                // The run updates in place: the per-part row counts are unchanged.
                Assert.Equal(2, await buildingModelPostgreSQLConverter.GetCountAsync(countyId_A));
                Assert.Equal(1, await buildingModelPostgreSQLConverter.GetCountAsync(countyId_B));

                // Both parts are checkpointed, one line per part, so an interrupted run resumes rather than restarts.
                Assert.True(File.Exists(path_Checkpoint));
                string[] lines_Checkpoint = await File.ReadAllLinesAsync(path_Checkpoint);
                Assert.Equal(2, lines_Checkpoint.Length);
                Assert.Contains($"building_model_component;{countyId_A}", lines_Checkpoint);
                Assert.Contains($"building_model_component;{countyId_B}", lines_Checkpoint);

                // The summary the real run leaves behind: one updated, one already correct, one rejected.
                string[] lines_Summary_RealRun = await File.ReadAllLinesAsync(path_Summary);
                Assert.Contains($"building_model_component;{code};{countyId_A};2;1;1;0;0", lines_Summary_RealRun);
                Assert.Contains($"building_model_component;{code};{countyId_B};1;0;0;1;0", lines_Summary_RealRun);
                Assert.Contains("(all tables);3;1;1;1;0", lines_Summary_RealRun);

                // The resumed run walks no row: both parts are in the checkpoint, so nothing is scanned, updated or reported.
                TestTask task_SecondRun = new(gISPostgreSQLConverterManager)
                {
                    CountyIds = [countyId_A, countyId_B],
                    BuildingModelDetailLevel = BuildingModelDetailLevel.Component,
                    DryRun = false,
                    BatchSize = 500,
                    CommandTimeout = 120,
                    Resume = true,
                    ReportDirectory = directory,
                    SkipOptionsDialog = true
                };

                bool succeeded_SecondRun = await task_SecondRun.RunAsync(new Progress<long>(), CancellationToken.None);
                Assert.True(succeeded_SecondRun);

                string[] lines_Summary_SecondRun = await File.ReadAllLinesAsync(path_Summary);
                Assert.Contains("(all tables);0;0;0;0;0", lines_Summary_SecondRun);

                // No row was scanned, so the report carries the header only.
                string[] lines_CSV_SecondRun = await File.ReadAllLinesAsync(path_CSV);
                Assert.Single(lines_CSV_SecondRun);

                // The stamping is still in place - the resume skipped the parts rather than re-reading them.
                DiGi.Analytical.Building.Classes.BuildingModel model_A1_SecondRun = await GetAnalyticalModelAsync(buildingModelPostgreSQLConverter, buildingModel_Row_A1.Id, countyId_A);
                Assert.True(model_A1_SecondRun.IsLocated());
            }
            finally
            {
                // The models first, then the parts they are filed under.
                await buildingModelPostgreSQLConverter.RemoveAsync([reference_Model_A1, reference_Model_A2], countyId_A);
                await buildingModelPostgreSQLConverter.RemoveAsync([reference_Model_B1], countyId_B);
                await administrativeAreal2DPostgreSQLConverter.RemoveAsync([countyId_A, countyId_B]);

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        /// <summary>
        /// Reads one stored row back from the building model table and deserializes it to the analytical model it holds.
        /// </summary>
        /// <param name="buildingModelPostgreSQLConverter">The converter reading the table.</param>
        /// <param name="id">The identifier of the row.</param>
        /// <param name="countyId">The identifier of the county partition the row is filed under.</param>
        /// <returns>The analytical model the row stores.</returns>
        private static async Task<DiGi.Analytical.Building.Classes.BuildingModel> GetAnalyticalModelAsync(BuildingModelPostgreSQLConverter buildingModelPostgreSQLConverter, long id, int countyId)
        {
            List<BuildingModel>? items = await buildingModelPostgreSQLConverter.GetItemsByIdsAsync([id], countyId);
            Assert.NotNull(items);
            Assert.Single(items);

            DiGi.Analytical.Building.Classes.BuildingModel? model = items[0].ToDiGi();
            Assert.NotNull(model);

            return model;
        }

        /// <summary>
        /// Reads the identifier of an <c>administrative_areal_2d</c> row by its unique reference.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to read through.</param>
        /// <param name="reference">The reference of the row.</param>
        /// <returns>The row's identifier.</returns>
        private static async Task<int> GetAdministrativeAreal2DIdAsync(NpgsqlConnection npgsqlConnection, string reference)
        {
            await using NpgsqlCommand npgsqlCommand = new($"SELECT id FROM {DiGi.GIS.PostgreSQL.Constants.TableName.AdministrativeAreal2D} WHERE reference = @reference", npgsqlConnection);
            npgsqlCommand.Parameters.AddWithValue("reference", reference);

            object? result = await npgsqlCommand.ExecuteScalarAsync();
            Assert.NotNull(result);

            return (int)result;
        }

        /// <summary>
        /// Builds a county polygon part as a square with a unique reference, the stored object a county row of the table carries, and the stored extent the row carries.
        /// </summary>
        /// <param name="reference">The unique reference of the part.</param>
        /// <param name="code">The county code shared by every part of the county.</param>
        /// <param name="name">The name of the part.</param>
        /// <param name="x">The X coordinate of the lower left corner.</param>
        /// <param name="y">The Y coordinate of the lower left corner.</param>
        /// <param name="size">The edge length of the square.</param>
        /// <returns>The county row.</returns>
        private static AdministrativeAreal2D AdministrativeAreal2D_CountyPart(string reference, string code, string name, double x, double y, double size)
        {
            PolygonalFace2D? polygonalFace2D = PolygonalFace2D_Square(x, y, size);
            Assert.NotNull(polygonalFace2D);

            GIS.Classes.AdministrativeDivision administrativeDivision = new(Guid.NewGuid(), reference, code, polygonalFace2D, GIS.Enums.AdministrativeDivisionType.county, name);

            return new AdministrativeAreal2D()
            {
                Code = code,
                Reference = reference,
                Name = name,
                AdministrativeArealType = AdministrativeArealType.County,
                BoundingBox2D = new BoundingBox2D(new Point2D(x, y), new Point2D(x + size, y + size)),
                UniqueId = administrativeDivision.UniqueId,
                Object = administrativeDivision.ToJsonObject()
            };
        }

        /// <summary>
        /// Builds a square polygonal face.
        /// </summary>
        /// <param name="x">The X coordinate of the lower left corner.</param>
        /// <param name="y">The Y coordinate of the lower left corner.</param>
        /// <param name="size">The edge length of the square.</param>
        /// <returns>The square face.</returns>
        private static PolygonalFace2D? PolygonalFace2D_Square(double x, double y, double size)
        {
            List<Point2D> point2Ds =
            [
                new Point2D(x, y),
                new Point2D(x + size, y),
                new Point2D(x + size, y + size),
                new Point2D(x, y + size)
            ];

            return Geometry.Planar.Create.PolygonalFace2D(new Polygon2D(point2Ds));
        }
    }
}
