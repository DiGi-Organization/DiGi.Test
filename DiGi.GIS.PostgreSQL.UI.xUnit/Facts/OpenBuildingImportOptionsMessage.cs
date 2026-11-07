using DiGi.GIS.PostgreSQL.Classes;
using DiGi.Geometry.Planar.Classes;
using System;
using System.IO;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the validation the options dialog and the wrapper ask before a run starts (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#38): every rule the core task enforces, in the order the operator meets them, each named in the message rather than left to a failed run's log.
        /// <para>The quadkey gate is asserted in both directions - required for the Microsoft dataset, and not required for a dataset whose reader never takes one - because a gate that fired for every dataset would make the other five unstartable. Since DiGi-Organization/DiGi.GIS.PostgreSQL#132 it is per file: chunks in their own <c>quadkey=</c> folders need no typed quadkey, and two chunks under one quadkey are refused.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImportOptionsMessage()
        {
            string path = Path.Combine(Path.GetTempPath(), $"xunit-open-building-options-{Guid.NewGuid():N}.geojsonl");
            string directory_Root = Path.Combine(Path.GetTempPath(), $"xunit-open-building-options-{Guid.NewGuid():N}");
            string directory_A = Path.Combine(directory_Root, "quadkey=120213101");
            string directory_B = Path.Combine(directory_Root, "quadkey=120213103");
            string path_A = Path.Combine(directory_A, "part-a.csv.gz");
            string path_B = Path.Combine(directory_B, "part-b.csv.gz");

            try
            {
                File.WriteAllBytes(path, [0x7b]);

                PostgreSQLOpenBuildingImportTaskOptions Options() => new()
                {
                    DatasetKey = "microsoft",
                    SourcePaths = [path],
                    Quadkey = "03210",
                    CountyIds = [4816],
                    Version = "TST-1",
                    DownloadDate = new DateTime(2026, 11, 6)
                };

                // A complete Microsoft release scoped by county list: nothing is missing, so no message.
                Assert.Null(Query.OpenBuildingImportOptionsMessage(Options()));

                // A release without a quadkey is still fine for a dataset whose reader never takes one.
                PostgreSQLOpenBuildingImportTaskOptions options_Eubucco = Options();
                options_Eubucco.DatasetKey = "eubucco";
                options_Eubucco.Quadkey = null;
                Assert.Null(Query.OpenBuildingImportOptionsMessage(options_Eubucco));

                // No options at all.
                Assert.Equal("No options were given.", Query.OpenBuildingImportOptionsMessage(null));

                // No dataset, and a dataset that names none.
                Assert.Equal("No dataset was chosen.", Query.OpenBuildingImportOptionsMessage(new PostgreSQLOpenBuildingImportTaskOptions()));

                PostgreSQLOpenBuildingImportTaskOptions options_Unknown = Options();
                options_Unknown.DatasetKey = "unknown";
                Assert.Contains("names no open building dataset", Query.OpenBuildingImportOptionsMessage(options_Unknown));

                // The two columns the register keys on.
                PostgreSQLOpenBuildingImportTaskOptions options_NoVersion = Options();
                options_NoVersion.Version = null;
                Assert.Contains("needs a version", Query.OpenBuildingImportOptionsMessage(options_NoVersion));

                PostgreSQLOpenBuildingImportTaskOptions options_NoDownloadDate = Options();
                options_NoDownloadDate.DownloadDate = null;
                Assert.Contains("needs a download date", Query.OpenBuildingImportOptionsMessage(options_NoDownloadDate));

                // The source files.
                PostgreSQLOpenBuildingImportTaskOptions options_NoSourcePaths = Options();
                options_NoSourcePaths.SourcePaths = null;
                Assert.Equal("No source file was named.", Query.OpenBuildingImportOptionsMessage(options_NoSourcePaths));

                PostgreSQLOpenBuildingImportTaskOptions options_MissingFile = Options();
                options_MissingFile.SourcePaths = [Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))];
                Assert.Contains("does not exist", Query.OpenBuildingImportOptionsMessage(options_MissingFile));

                // The quadkey of every Microsoft file (DiGi-Organization/DiGi.GIS.PostgreSQL#132): a file outside a
                // quadkey folder needs the typed one.
                PostgreSQLOpenBuildingImportTaskOptions options_NoQuadkey = Options();
                options_NoQuadkey.Quadkey = null;
                Assert.Contains("quadkey", Query.OpenBuildingImportOptionsMessage(options_NoQuadkey));

                // Two chunks in their own quadkey folders need no typed quadkey; two chunks under one quadkey are
                // refused, because their quadkey:index identifiers would collide.
                Directory.CreateDirectory(directory_A);
                Directory.CreateDirectory(directory_B);
                File.WriteAllBytes(path_A, [0x7b]);
                File.WriteAllBytes(path_B, [0x7b]);

                PostgreSQLOpenBuildingImportTaskOptions options_Folders = Options();
                options_Folders.SourcePaths = [path_A, path_B];
                options_Folders.Quadkey = null;
                Assert.Null(Query.OpenBuildingImportOptionsMessage(options_Folders));

                PostgreSQLOpenBuildingImportTaskOptions options_Shared = Options();
                options_Shared.SourcePaths = [path, path_A];
                options_Shared.Quadkey = "120213101";
                Assert.Contains("collide", Query.OpenBuildingImportOptionsMessage(options_Shared));

                // Exactly one scope form: neither is refused, and so is both - which would leave it ambiguous
                // which form truncated the partitions.
                const string message_Scope = "The scope has to be a county list or a bounding box, exactly one of the two.";

                PostgreSQLOpenBuildingImportTaskOptions options_NoScope = Options();
                options_NoScope.CountyIds = null;
                Assert.Equal(message_Scope, Query.OpenBuildingImportOptionsMessage(options_NoScope));

                PostgreSQLOpenBuildingImportTaskOptions options_BothScopes = Options();
                options_BothScopes.BoundingBox2D = new BoundingBox2D(new Point2D(629000, 519000), new Point2D(631000, 521000));
                Assert.Equal(message_Scope, Query.OpenBuildingImportOptionsMessage(options_BothScopes));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (Directory.Exists(directory_Root))
                {
                    Directory.Delete(directory_Root, true);
                }
            }
        }
    }
}
