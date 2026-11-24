using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the run report of the tray wrapper breaks its rejection figure down per <see cref="UpdateRejectionReason"/> and lists the sample source ids under each reason, so the report explains a run's rejections instead of only counting them (DiGi-Organization/DiGi.GIS.PostgreSQL#172).
        /// <para>A crafted Microsoft line-delimited GeoJSON chunk with three features goes through <see cref="UIOpenBuildingImportTask"/> with <see cref="UIOpenBuildingImportTask.SkipOptionsDialog"/> set: two valid features inside the scratch county square and one around 5E, 50N, whose reprojection falls outside the Polish domain. The run therefore stores two rows and rejects exactly one with <see cref="UpdateRejectionReason.GeometryRejected"/>, and the report the run writes must carry that one reason line with its count, no line for any other reason, and an indented sample id beneath it - the count equal to the task's own rejection tally, so the breakdown adds up to the parent line. The manager is built from the test configuration file, so the fact writes the throwaway database and never the host's. The scratch county, the register row, the partition, the crafted file and the report are all removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task UIOpenBuildingImportTask_ReportRejectionTally()
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

            AdministrativeAreal2DPostgreSQLConverter? administrativeAreal2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<AdministrativeAreal2DPostgreSQLConverter>();
            Assert.NotNull(administrativeAreal2DPostgreSQLConverter);

            const string reference_County = "I172_OPEN_BUILDING_COUNTY";
            const string code = "172";
            string version = $"TST-{Guid.NewGuid():N}";

            string sourcePath = Path.Combine(Path.GetTempPath(), $"xunit-open-building-ui-tally-{Guid.NewGuid():N}.geojsonl");
            string path_Report = Path.Combine(AppContext.BaseDirectory, "OpenBuilding_Import_Report_microsoft.txt");

            int? countyId = null;

            try
            {
                AdministrativeAreal2D administrativeAreal2D = AdministrativeAreal2D_CountyPart(reference_County, code, "open building part", 630000, 520000, 1000);

                HashSet<int>? ids = await administrativeAreal2DPostgreSQLConverter.UpdateAsync([administrativeAreal2D]);
                Assert.NotNull(ids);
                Assert.Single(ids);

                countyId = await GetAdministrativeAreal2DIdAsync(npgsqlConnection, reference_County);

                // Two valid features around the EPSG:4326 position of the county centre, one rejected feature
                // around 5E, 50N: its reprojection lands far west of the Polish domain, so the load path
                // refuses it before any county is asked.
                Point3D? point3D = GIS.Convert.ToEPSG4326(new Point2D(630500, 520500));
                Assert.NotNull(point3D);

                string Feature(int index, string longitude_Min, string latitude_Min, string longitude_Max, string latitude_Max) => string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"type\":\"Feature\",\"properties\":{{\"height\":{0},\"capture\":\"2019-07\"}},\"geometry\":{{\"type\":\"Polygon\",\"coordinates\":[[[{1},{2}],[{3},{2}],[{3},{4}],[{1},{4}],[{1},{2}]]]}}}}",
                    10 + index,
                    longitude_Min,
                    latitude_Min,
                    longitude_Max,
                    latitude_Max);

                double halfSize = 0.00005;
                string longitude_Min = (point3D.X - halfSize).ToString(CultureInfo.InvariantCulture);
                string latitude_Min = (point3D.Y - halfSize).ToString(CultureInfo.InvariantCulture);
                string longitude_Max = (point3D.X + halfSize).ToString(CultureInfo.InvariantCulture);
                string latitude_Max = (point3D.Y + halfSize).ToString(CultureInfo.InvariantCulture);

                double halfSize_Outside = 0.0001;
                string longitude_Min_Outside = (5.0 - halfSize_Outside).ToString(CultureInfo.InvariantCulture);
                string latitude_Min_Outside = (50.0 - halfSize_Outside).ToString(CultureInfo.InvariantCulture);
                string longitude_Max_Outside = (5.0 + halfSize_Outside).ToString(CultureInfo.InvariantCulture);
                string latitude_Max_Outside = (50.0 + halfSize_Outside).ToString(CultureInfo.InvariantCulture);

                File.WriteAllLines(sourcePath,
                [
                    Feature(0, longitude_Min, latitude_Min, longitude_Max, latitude_Max),
                    Feature(1, longitude_Min, latitude_Min, longitude_Max, latitude_Max),
                    Feature(2, longitude_Min_Outside, latitude_Min_Outside, longitude_Max_Outside, latitude_Max_Outside),
                ]);

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

                bool succeeded = await task.RunAsync(new Progress<long>(), CancellationToken.None);
                Assert.True(succeeded);

                Assert.Equal(2L, task.RowCount);
                Assert.Equal(0L, task.FilteredCount);
                Assert.Equal(1L, task.RejectionCount);

                // The task carries the tally, and it names the one reason of the mix - complete, and only
                // for the reasons the run really raised.
                Assert.Equal(1L, task.RejectionCounts.GetValueOrDefault(UpdateRejectionReason.GeometryRejected));
                Assert.Single(task.RejectionCounts);
                Assert.Equal(task.RejectionCount, task.RejectionCounts.Values.Sum());

                // The report states the same figure and breaks it down: one reason line, the count the parent
                // line carries, no line for any other reason, and an indented sample id beneath it.
                Assert.True(File.Exists(path_Report));
                string[] lines = await File.ReadAllLinesAsync(path_Report);

                int index_Parent = Array.FindIndex(lines, x => x.StartsWith("Rejected before the database:", StringComparison.Ordinal));
                Assert.True(index_Parent >= 0, "the report carries no rejection line.");
                Assert.Equal($"Rejected before the database: {task.RejectionCount}", lines[index_Parent]);

                int index_Reason = Array.FindIndex(lines, x => x.StartsWith($"  {Core.Query.Description(UpdateRejectionReason.GeometryRejected)}:", StringComparison.Ordinal));
                Assert.True(index_Reason >= 0, "the report carries no line for the reason the run rejected with.");
                Assert.Equal($"  Geometry Rejected: {task.RejectionCounts[UpdateRejectionReason.GeometryRejected]}", lines[index_Reason]);

                foreach (UpdateRejectionReason updateRejectionReason in System.Enum.GetValues<UpdateRejectionReason>())
                {
                    if (updateRejectionReason != UpdateRejectionReason.GeometryRejected)
                    {
                        Assert.DoesNotContain(lines, x => x.StartsWith($"  {Core.Query.Description(updateRejectionReason)}:", StringComparison.Ordinal));
                    }
                }

                // The sample ids sit one level deeper than their reason line.
                Assert.True(index_Reason + 1 < lines.Length, "the report ends before the sample ids of the reason.");
                Assert.StartsWith("    ", lines[index_Reason + 1]);
            }
            finally
            {
                if (countyId is not null)
                {
                    if (await OpenBuildingRegisterIdAsync(npgsqlConnection_Storage, version) is long)
                    {
                        await using NpgsqlCommand npgsqlCommand_Delete = new($"DELETE FROM {PostgreSQL.Constants.TableName.BuildingDataset} WHERE dataset_key = 'microsoft' AND version = @version;", npgsqlConnection_Storage);
                        npgsqlCommand_Delete.Parameters.AddWithValue("version", version);
                        await npgsqlCommand_Delete.ExecuteNonQueryAsync();
                    }

                    string? tableName = PostgreSQL.Query.OpenBuildingTableName("microsoft");
                    Assert.NotNull(tableName);

                    await using NpgsqlCommand npgsqlCommand_Drop = new($"DROP TABLE IF EXISTS {tableName}_{countyId.Value};", npgsqlConnection_Storage);
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

            // Reads one release's register row id back from the database - the figure the report's register
            // line and the cleanup above both read, and null when the run never registered one.
            static async Task<long?> OpenBuildingRegisterIdAsync(NpgsqlConnection npgsqlConnection, string version)
            {
                await using NpgsqlCommand npgsqlCommand = new($"SELECT id FROM {PostgreSQL.Constants.TableName.BuildingDataset} WHERE dataset_key = 'microsoft' AND version = @version;", npgsqlConnection);
                npgsqlCommand.Parameters.AddWithValue("version", version);

                object? result = await npgsqlCommand.ExecuteScalarAsync();
                return result is null || result is DBNull ? null : System.Convert.ToInt64(result, CultureInfo.CurrentCulture);
            }
        }
    }
}
