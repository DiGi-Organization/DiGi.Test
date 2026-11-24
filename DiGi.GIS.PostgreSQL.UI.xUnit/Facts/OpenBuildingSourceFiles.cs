using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the file types the open building import dialog offers for each dataset (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#55).
        /// <para>The Microsoft release is a tree of gzipped <c>part-*.csv.gz</c> files, so its type accepts <c>gz</c> as well as <c>geojsonl</c> - the case the dialog refused on the first production run. GHS-OBAT lists its GeoPackage alone, because the release's CSV holds the same rows and importing both would duplicate every one. The key is matched without regard to case or surrounding white space, and an unknown or missing key answers the wildcard, which makes the filter All Files alone rather than a duplicate entry.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingFileType_Datasets()
        {
            string[] extensions_Microsoft = Query.OpenBuildingFileType("microsoft").Extensions;
            Assert.Contains("gz", extensions_Microsoft);
            Assert.Contains("geojsonl", extensions_Microsoft);

            Assert.Equal(["gpkg"], Query.OpenBuildingFileType("ghs_obat").Extensions);
            Assert.Equal(["gpkg"], Query.OpenBuildingFileType("eubucco").Extensions);
            Assert.Equal(["gpkg"], Query.OpenBuildingFileType("dbsm").Extensions);
            Assert.Equal(["parquet"], Query.OpenBuildingFileType("overture").Extensions);
            Assert.Equal(["pbf"], Query.OpenBuildingFileType("osm").Extensions);

            // Case and surrounding white space do not matter.
            Assert.Equal(extensions_Microsoft, Query.OpenBuildingFileType("  MICROSOFT ").Extensions);

            // No known dataset: every file.
            Assert.Equal(["*"], Query.OpenBuildingFileType(null).Extensions);
            Assert.Equal(["*"], Query.OpenBuildingFileType("zzz").Extensions);

            // The dialog filter: the dataset's own entry first, then All Files; an unknown key gets All Files alone.
            Assert.Equal("Microsoft building footprints (GeoJSON sequence, gzipped or plain) (*.gz;*.geojsonl)|*.gz;*.geojsonl|All Files (*.*)|*.*", Query.OpenBuildingFileFilter("microsoft"));
            Assert.Equal("EUBUCCO GeoPackage (*.gpkg)|*.gpkg|All Files (*.*)|*.*", Query.OpenBuildingFileFilter("eubucco"));
            Assert.Equal("All Files (*.*)|*.*", Query.OpenBuildingFileFilter("zzz"));
            Assert.Equal("All Files (*.*)|*.*", Query.OpenBuildingFileFilter(null));
        }

        /// <summary>
        /// Verifies that Add folder collects exactly the release files below a folder and nothing else (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#55).
        /// <para>A tree shaped like the Microsoft release is built in a scratch folder under the reports directory: two <c>quadkey=&lt;digits&gt;</c> folders each holding a gzipped part (one with an upper-case extension), a plain GeoJSON sequence nested one level deeper, and files that must not be picked - the catalog <c>dataset-links.csv</c> beside the folders and a text report inside one of them. The Microsoft read returns the three release files in ordinal order and neither stray file; GHS-OBAT in a folder holding both its GeoPackage and its CSV returns the GeoPackage alone; an unknown key returns every file; a folder that does not exist, or no folder, answers null. The scratch folder is removed in <c>finally</c>.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingSourcePaths_Tree()
        {
            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(directory_Reports);

            string directory = Path.Combine(directory_Reports, $"open-building-source-files-{Guid.NewGuid():N}");

            try
            {
                string path_A = Path.Combine(directory, "quadkey=120213032", "part-00129.c000.csv.gz");
                string path_B = Path.Combine(directory, "quadkey=120213033", "part-00078.c000.CSV.GZ");
                string path_C = Path.Combine(directory, "quadkey=120213033", "extra", "chunk.geojsonl");
                string path_Catalog = Path.Combine(directory, "dataset-links.csv");
                string path_Report = Path.Combine(directory, "quadkey=120213032", "report.txt");

                foreach (string path in new string[] { path_A, path_B, path_C, path_Catalog, path_Report })
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, "x");
                }

                List<string>? paths_Microsoft = Query.OpenBuildingSourcePaths("microsoft", directory);
                Assert.NotNull(paths_Microsoft);

                List<string> paths_Expected = [path_A, path_B, path_C];
                paths_Expected.Sort(StringComparer.Ordinal);
                Assert.Equal(paths_Expected, paths_Microsoft);

                Assert.DoesNotContain(path_Catalog, paths_Microsoft);
                Assert.DoesNotContain(path_Report, paths_Microsoft);

                // GHS-OBAT: the GeoPackage only, never the CSV of the same rows.
                string directory_Ghs = Path.Combine(directory, "ghs");
                Directory.CreateDirectory(directory_Ghs);
                string path_Gpkg = Path.Combine(directory_Ghs, "GHS_OBAT_POL.gpkg");
                File.WriteAllText(path_Gpkg, "x");
                File.WriteAllText(Path.Combine(directory_Ghs, "GHS_OBAT_POL.csv"), "x");
                Assert.Equal([path_Gpkg], Query.OpenBuildingSourcePaths("ghs_obat", directory_Ghs));

                // An unknown key takes every file.
                List<string>? paths_Unknown = Query.OpenBuildingSourcePaths("zzz", directory);
                Assert.NotNull(paths_Unknown);
                Assert.Contains(path_Catalog, paths_Unknown);
                Assert.Contains(path_Report, paths_Unknown);

                // No folder, or one that is not there: null, not an empty list.
                Assert.Null(Query.OpenBuildingSourcePaths("microsoft", Path.Combine(directory, "no-such-folder")));
                Assert.Null(Query.OpenBuildingSourcePaths("microsoft", null));
                Assert.Null(Query.OpenBuildingSourcePaths("microsoft", " "));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
