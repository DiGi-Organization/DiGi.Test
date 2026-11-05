using System.IO;
using System.Reflection;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Checks that the dependency-free <see cref="GIS.Query.IsOrtoJpegBlank(byte[])"/> (DiGi.GIS, used on the API host, DiGi.GIS.WebAPI#76) agrees with the pixel-based <see cref="Query.IsOrtoBlank(byte[])"/> on every orthophoto fixture.
        /// <para>Fixtures: the Warsaw 1995 blank and 2023 content tiles, the partly covered Słubice 2024 tile (black no-data fill beside imagery), and the two synthetic flat JPEGs on either side of the tolerance.</para>
        /// </summary>
        [Fact]
        public void IsOrtoJpegBlank_Parity()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string[] fileNames = ["OrtoImage_Warszawa_1995_Blank.jpg", "OrtoImage_Warszawa_2023_Content.jpg", "OrtoImage_Slubice_2024_Partial.jpg", "OrtoImage_Flat_Spread7.jpg", "OrtoImage_Flat_Spread9.jpg"];
            foreach (string fileName in fileNames)
            {
                string? path = Core.xUnit.Query.FilePath(assembly, fileName);
                Assert.NotNull(path);

                byte[] bytes = File.ReadAllBytes(path);
                Assert.True(Query.IsOrtoBlank(bytes) == GIS.Query.IsOrtoJpegBlank(bytes), fileName);
            }
        }
    }
}
