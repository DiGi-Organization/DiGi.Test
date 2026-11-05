using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the dependency-free blank check on real GUGiK tiles and on synthetic flat JPEGs (DiGi.GIS.WebAPI#76).
        /// <para><c>OrtoImage_Warszawa_1995_Blank.jpg</c> and <c>OrtoImage_Warszawa_2023_Content.jpg</c> are the pair the pixel-based DiGi.GIS.Emgu.CV check is pinned on. <c>OrtoImage_Slubice_2024_Partial.jpg</c> (BBOX 197000,506000,198000,507000, 256 px, <c>TIME=2024</c>, fetched 2026-11-05) is the Oder at Słubice: the sheet ends mid-tile and the service fills the rest black, so it is mostly empty but not blank.</para>
        /// <para><c>OrtoImage_Flat_Spread7.jpg</c> and <c>OrtoImage_Flat_Spread9.jpg</c> are hand-built baseline greyscale JPEGs of two flat 8 × 8 blocks (levels 200/207 and 200/209, quantisation 1): the tolerance boundary of <see cref="Constants.OrtoBlank.Tolerance"/> just inside and just outside. A progressive SOF, a truncated scan, a service exception and missing bytes are never blank.</para>
        /// <para>Ends with a timing assertion: content is rejected at its first non-flat block, a blank costs one pass over its small scan.</para>
        /// </summary>
        [Fact]
        public void IsOrtoJpegBlank()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            byte[] bytes_Blank = File.ReadAllBytes(Core.xUnit.Query.FilePath(assembly, "OrtoImage_Warszawa_1995_Blank.jpg")!);
            byte[] bytes_Content = File.ReadAllBytes(Core.xUnit.Query.FilePath(assembly, "OrtoImage_Warszawa_2023_Content.jpg")!);
            byte[] bytes_Partial = File.ReadAllBytes(Core.xUnit.Query.FilePath(assembly, "OrtoImage_Slubice_2024_Partial.jpg")!);
            byte[] bytes_Spread7 = File.ReadAllBytes(Core.xUnit.Query.FilePath(assembly, "OrtoImage_Flat_Spread7.jpg")!);
            byte[] bytes_Spread9 = File.ReadAllBytes(Core.xUnit.Query.FilePath(assembly, "OrtoImage_Flat_Spread9.jpg")!);

            Assert.True(bytes_Blank.IsOrtoImage());
            Assert.True(bytes_Blank.IsOrtoJpegBlank());
            Assert.False(bytes_Content.IsOrtoJpegBlank());
            Assert.False(bytes_Partial.IsOrtoJpegBlank());

            Assert.Equal(8, Constants.OrtoBlank.Tolerance);
            Assert.True(bytes_Spread7.IsOrtoJpegBlank());
            Assert.False(bytes_Spread9.IsOrtoJpegBlank());
            Assert.True(bytes_Spread9.IsOrtoJpegBlank(9));
            Assert.False(bytes_Spread7.IsOrtoJpegBlank(6));

            byte[] bytes_Progressive = (byte[])bytes_Blank.Clone();
            int index_Sof = -1;
            for (int i = 2; i < bytes_Progressive.Length - 1; i++)
            {
                if (bytes_Progressive[i] == 0xFF && bytes_Progressive[i + 1] == 0xC0)
                {
                    index_Sof = i;
                    break;
                }
            }

            Assert.True(index_Sof > 0);
            bytes_Progressive[index_Sof + 1] = 0xC2;
            Assert.False(bytes_Progressive.IsOrtoJpegBlank());

            byte[] bytes_Truncated = new byte[bytes_Blank.Length / 2];
            Array.Copy(bytes_Blank, bytes_Truncated, bytes_Truncated.Length);
            Assert.False(bytes_Truncated.IsOrtoJpegBlank());

            Assert.False(Encoding.UTF8.GetBytes("<ServiceExceptionReport/>").IsOrtoJpegBlank());
            Assert.False(Query.IsOrtoJpegBlank(null));
            Assert.False(Array.Empty<byte>().IsOrtoJpegBlank());
            Assert.False(bytes_Blank.IsOrtoJpegBlank(-1));

            Assert.True(bytes_Blank.IsOrtoJpegBlank());
            Assert.False(bytes_Content.IsOrtoJpegBlank());

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 200; i++)
            {
                Assert.True(bytes_Blank.IsOrtoJpegBlank());
                Assert.False(bytes_Content.IsOrtoJpegBlank());
            }

            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"200 blank + 200 content checks took {stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
