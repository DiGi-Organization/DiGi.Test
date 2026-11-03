using System;
using System.IO;
using System.Reflection;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the blank check on two real GUGiK tiles of the same 256 m square in central Warsaw (EPSG:2180 BBOX 637000,486000,637256,486256 at 512 px, scale denominator about 1 786, far below half of the service limit).
        /// <para><c>OrtoImage_Warszawa_1995_Blank.jpg</c> was requested with <c>TIME=1995</c>, for which the square has no imagery; the service answered HTTP 200 with uniform white. <c>OrtoImage_Warszawa_2023_Content.jpg</c> is the same square with <c>TIME=2023</c>. Both came from <see cref="GIS.Constants.WebService.StandardResolutionTime"/> on 2026-11-03.</para>
        /// <para>Missing and undecodable payloads are not blank: the probe classifies them as exceptions first.</para>
        /// </summary>
        [Fact]
        public void IsOrtoBlank()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string? path_Blank = Core.xUnit.Query.FilePath(assembly, "OrtoImage_Warszawa_1995_Blank.jpg");
            string? path_Content = Core.xUnit.Query.FilePath(assembly, "OrtoImage_Warszawa_2023_Content.jpg");
            Assert.NotNull(path_Blank);
            Assert.NotNull(path_Content);

            Assert.True(Query.IsOrtoBlank(File.ReadAllBytes(path_Blank)));
            Assert.False(Query.IsOrtoBlank(File.ReadAllBytes(path_Content)));

            Assert.False(Query.IsOrtoBlank(null));
            Assert.False(Query.IsOrtoBlank([]));
            Assert.False(Query.IsOrtoBlank(System.Text.Encoding.UTF8.GetBytes("<ServiceExceptionReport/>")));

            Func<byte[], bool> isBlank = Query.IsOrtoBlank;
            Assert.True(isBlank(File.ReadAllBytes(path_Blank)));
        }
    }
}
