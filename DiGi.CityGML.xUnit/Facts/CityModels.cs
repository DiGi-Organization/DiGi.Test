using DiGi.CityGML.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace DiGi.CityGML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a CityGML zip archive is walked and parsed into the expected city model content.
        /// <para>Locks the baseline behaviour so the entry-filter and stream-disposal changes in the walker do not regress the archive path.</para>
        /// </summary>
        [Fact]
        public void CityModels_FromZip()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "2862_CityGML.zip");

            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path));

            List<CityModel>? cityModels = Create.CityModels(path);

            Assert.NotNull(cityModels);
            Assert.Single(cityModels);
            Assert.Equal(3, BuildingCount(cityModels));
        }

        /// <summary>
        /// Tests that a single CityGML file passed directly - not wrapped in a zip - is parsed rather than reported as a failed walk.
        /// <para>Guards the walker's single-file branch, which previously fell through to a false result and made the file-based load return null.</para>
        /// </summary>
        [Fact]
        public void CityModels_FromSingleGmlFile()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "2862_N-34-77-D-b-1-1.gml");

            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path));

            List<CityModel>? cityModels = Create.CityModels(path);

            Assert.NotNull(cityModels);
            Assert.Single(cityModels);
            Assert.Equal(3, BuildingCount(cityModels));
        }

        /// <summary>
        /// Tests that a stored (uncompressed) zip entry is parsed by the walker.
        /// <para>A stored entry opens as a non-deflate stream, so the previous stream-type gate silently skipped it; the archive is built at test time to avoid committing a second binary fixture.</para>
        /// </summary>
        [Fact]
        public void CityModels_FromStoredZip()
        {
            string? path_Gml = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "2862_N-34-77-D-b-1-1.gml");

            Assert.False(string.IsNullOrWhiteSpace(path_Gml));
            Assert.True(File.Exists(path_Gml));

            string path_Zip = Path.Combine(Path.GetTempPath(), string.Format(CultureInfo.CurrentCulture, "DiGi_CityGML_Stored_{0}.zip", System.Guid.NewGuid()));

            try
            {
                using (FileStream fileStream = new(path_Zip, FileMode.Create))
                using (ZipArchive zipArchive = new(fileStream, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry("2862_N-34-77-D-b-1-1.gml", CompressionLevel.NoCompression);

                    using Stream stream = zipArchiveEntry.Open();
                    using FileStream fileStream_Gml = new(path_Gml!, FileMode.Open, FileAccess.Read);

                    fileStream_Gml.CopyTo(stream);
                }

                List<CityModel>? cityModels = Create.CityModels(path_Zip);

                Assert.NotNull(cityModels);
                Assert.Single(cityModels);
                Assert.Equal(3, BuildingCount(cityModels));
            }
            finally
            {
                if (File.Exists(path_Zip))
                {
                    File.Delete(path_Zip);
                }
            }
        }

        private static int BuildingCount(IEnumerable<CityModel>? cityModels)
        {
            if (cityModels is null)
            {
                return 0;
            }

            int result = 0;
            foreach (CityModel cityModel in cityModels)
            {
                IEnumerable<Building>? buildings = cityModel?.Buildings;
                if (buildings is null)
                {
                    continue;
                }

                foreach (Building building in buildings)
                {
                    result++;
                }
            }

            return result;
        }
    }
}
