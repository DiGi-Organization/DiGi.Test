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
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the open building import leaves unread a source file that cannot reach its scope, and that doing so changes nothing it stores (DiGi.GIS.PostgreSQL#170).
        /// <para>Two Microsoft chunks are written under real level-12 quadkey folders: one tile holds the scratch county part and three footprints inside it, the other lies five tiles east (about 30 km) and holds two footprints no county part of the throwaway database reaches. The same release is imported twice into the same scope - every file read, then with <see cref="PostgreSQLOpenBuildingImportTaskOptions.SkipSourcesOutOfScope"/> - and the two runs store the same rows (compared as sets of source identifiers). The far chunk is named in <see cref="PostgreSQLOpenBuildingImportTask.SkippedSourcePaths"/> and in the register row, relative to the release's shared folder, and its two footprints - rejections when the file is read - are counted nowhere when it is not: that difference is the guard that the skipped file held only footprints the run could not keep.</para>
        /// <para>The scratch county, its partition, the register rows and the chunk files are removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PostgreSQLOpenBuildingImportTask_SkipSourcesOutOfScope()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection_Storage);
            await npgsqlConnection_Storage.OpenAsync();

            string reference = $"xunit-open-building-{Guid.NewGuid():N}";
            string code = $"TST{Guid.NewGuid():N}";
            string version_All = $"TST-{Guid.NewGuid():N}";
            string version_Skip = $"TST-{Guid.NewGuid():N}";
            string directory_Root = Path.Combine(Path.GetTempPath(), $"xunit-open-building-{Guid.NewGuid():N}");
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection, reference, code);
                Assert.True(await OpenBuildingPostgreSQLConverter.CreateTablesAsync(npgsqlConnection_Storage), "the open building tables could not be created on the throwaway Storage test database.");

                Point3D? point3D = GIS.Convert.ToEPSG4326(new Point2D(openBuilding_ScratchX, openBuilding_ScratchY));
                Assert.NotNull(point3D);

                // The level-12 tile holding the scratch centre, and the tile five columns east of it.
                const int level = 12;
                double count = Math.Pow(2, level);
                double latitude_Radians = point3D.Y * Math.PI / 180;
                long x = (long)Math.Floor((point3D.X + 180) / 360 * count);
                long y = (long)Math.Floor((1 - (Math.Log(Math.Tan(latitude_Radians) + (1 / Math.Cos(latitude_Radians))) / Math.PI)) / 2 * count);

                string Quadkey(long tileX, long tileY)
                {
                    StringBuilder stringBuilder = new();
                    for (int i = level; i > 0; i--)
                    {
                        long mask = 1L << (i - 1);
                        stringBuilder.Append((char)('0' + ((tileX & mask) != 0 ? 1 : 0) + ((tileY & mask) != 0 ? 2 : 0)));
                    }

                    return stringBuilder.ToString();
                }

                string quadkey_Near = Quadkey(x, y);
                string quadkey_Far = Quadkey(x + 5, y);

                BoundingBox2D? boundingBox2D_Near = DiGi.GIS.IO.Query.QuadkeyBoundingBox2D(quadkey_Near);
                BoundingBox2D? boundingBox2D_Far = DiGi.GIS.IO.Query.QuadkeyBoundingBox2D(quadkey_Far);
                Assert.NotNull(boundingBox2D_Near);
                Assert.NotNull(boundingBox2D_Far);
                Assert.True(boundingBox2D_Near.Inside(new Point2D(point3D.X, point3D.Y)), "the scratch centre must lie in the near tile, or the fact tests nothing.");

                static string Feature(double longitude, double latitude, int index)
                {
                    const double halfSize = 0.00005;
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"type\":\"Feature\",\"properties\":{{\"height\":{0}}},\"geometry\":{{\"type\":\"Polygon\",\"coordinates\":[[[{1},{2}],[{3},{2}],[{3},{4}],[{1},{4}],[{1},{2}]]]}}}}",
                        10 + index,
                        longitude - halfSize,
                        latitude - halfSize,
                        longitude + halfSize,
                        latitude + halfSize);
                }

                string path_Near = Path.Combine(directory_Root, $"quadkey={quadkey_Near}", "part-near.geojsonl");
                string path_Far = Path.Combine(directory_Root, $"quadkey={quadkey_Far}", "part-far.geojsonl");
                Directory.CreateDirectory(Path.GetDirectoryName(path_Near)!);
                Directory.CreateDirectory(Path.GetDirectoryName(path_Far)!);

                Point2D? point2D_Far = boundingBox2D_Far.GetCentroid();
                Assert.NotNull(point2D_Far);
                File.WriteAllLines(path_Near, [Feature(point3D.X, point3D.Y, 0), Feature(point3D.X, point3D.Y, 1), Feature(point3D.X, point3D.Y, 2)]);
                File.WriteAllLines(path_Far, [Feature(point2D_Far.X, point2D_Far.Y, 0), Feature(point2D_Far.X, point2D_Far.Y, 1)]);

                string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Assert.NotNull(directory);

                PostgreSQLConfigurationFile? configurationFile = DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(Path.Combine(directory!, fileName_PostgreSQL_Main_Test));
                Assert.NotNull(configurationFile);

                PostgreSQLConfigurationFile? configurationFile_Storage = DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(Path.Combine(directory!, fileName_PostgreSQL_Storage_Test));
                Assert.NotNull(configurationFile_Storage);

                GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
                Assert.True(gISPostgreSQLConverterManager.Add(new OpenBuildingPostgreSQLConverter(connectionData_Storage), configurationFile_Storage));
                Assert.True(gISPostgreSQLConverterManager.Add(new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main), configurationFile));

                async Task<(PostgreSQLOpenBuildingImportTask Task, HashSet<string> SourceIds)> ImportAsync(string version, bool skipSourcesOutOfScope)
                {
                    PostgreSQLOpenBuildingImportTask postgreSQLOpenBuildingImportTask = new(gISPostgreSQLConverterManager)
                    {
                        PostgreSQLOpenBuildingImportTaskOptions = new PostgreSQLOpenBuildingImportTaskOptions
                        {
                            DatasetKey = "microsoft",
                            SourcePaths = [path_Near, path_Far],
                            CountyIds = [countyId.Value],
                            Version = version,
                            DownloadDate = DateTime.Today,
                            SkipSourcesOutOfScope = skipSourcesOutOfScope,
                        }
                    };

                    await RunAsync(postgreSQLOpenBuildingImportTask);

                    HashSet<string> sourceIds = [];
                    await using NpgsqlCommand npgsqlCommand = new($"SELECT source_id FROM {Constants.TableName.Microsoft_Building2D} WHERE county_id = {countyId.Value};", npgsqlConnection_Storage);
                    await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
                    while (await npgsqlDataReader.ReadAsync())
                    {
                        sourceIds.Add(npgsqlDataReader.GetString(0));
                    }

                    return (postgreSQLOpenBuildingImportTask, sourceIds);
                }

                (PostgreSQLOpenBuildingImportTask task_All, HashSet<string> sourceIds_All) = await ImportAsync(version_All, false);
                (PostgreSQLOpenBuildingImportTask task_Skip, HashSet<string> sourceIds_Skip) = await ImportAsync(version_Skip, true);

                // The same rows either way.
                Assert.Equal(3L, task_All.RowCount);
                Assert.Equal(3L, task_Skip.RowCount);
                Assert.Equal(3, sourceIds_All.Count);
                Assert.True(sourceIds_All.SetEquals(sourceIds_Skip));

                // Read whole, the far chunk's two footprints file nowhere; left unread, they are counted nowhere.
                Assert.Empty(task_All.SkippedSourcePaths);
                Assert.Equal(2L, task_All.RejectionCount + task_All.FilteredCount);
                Assert.Equal(path_Far, Assert.Single(task_Skip.SkippedSourcePaths));
                Assert.Equal(0L, task_Skip.RejectionCount + task_Skip.FilteredCount);

                // The register names the file left out, relative to the release's shared folder.
                List<BuildingDatasetRelease>? buildingDatasetReleases = await OpenBuildingPostgreSQLConverter.GetReleasesAsync(npgsqlConnection_Storage, "microsoft");
                Assert.NotNull(buildingDatasetReleases);

                BuildingDatasetRelease release_All = Assert.Single(buildingDatasetReleases, x => x.Version == version_All);
                BuildingDatasetRelease release_Skip = Assert.Single(buildingDatasetReleases, x => x.Version == version_Skip);
                Assert.Null(release_All.SkippedFileNames);
                Assert.Equal($"quadkey={quadkey_Far}/part-far.geojsonl", Assert.Single(release_Skip.SkippedFileNames!));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, npgsqlConnection_Storage, [reference], [version_All, version_Skip], countyId, "microsoft");

                if (Directory.Exists(directory_Root))
                {
                    Directory.Delete(directory_Root, true);
                }
            }
        }
    }
}
