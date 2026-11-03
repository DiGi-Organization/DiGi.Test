using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Query.IsOrtoBlank(byte[])"/> as the <c>isBlank</c> argument of <see cref="GIS.Query.OrtoImageProbesAsync"/> against the live GUGiK WMS.
        /// <para>The square and times are those of the <c>OrtoImage_Warszawa_*</c> fixtures (<see cref="IsOrtoBlank"/>): 1995 has no imagery and answers uniform white, 2023 answers content. The URL the fixtures came from is <see cref="GIS.Query.Url_OrtoImage"/> of the same arguments.</para>
        /// <para>Long: the service answered one 512 px tile in 37 s on 2026-11-03 and the probe sends four requests, so the run took 58 s.</para>
        /// </summary>
        [LongFact]
        public async Task OrtoImageProbesAsync_IsOrtoBlank()
        {
            BoundingBox2D boundingBox2D = new(new Point2D(637000, 486000), new Point2D(637256, 486256));
            int width = 512;

            double? scaleDenominator = GIS.Query.OrtoImageScaleDenominator(boundingBox2D, width);
            Assert.NotNull(scaleDenominator);
            Assert.True(scaleDenominator.Value <= GIS.Constants.WebService.OrtoMaxScaleDenominator / 2);

            string? url = GIS.Query.Url_OrtoImage(boundingBox2D, GIS.Constants.WebService.StandardResolutionTime, GIS.Constants.WebService.StandardResolutionTimeLayer, "2023", width, width);
            Assert.NotNull(url);

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

            List<OrtoImageProbe>? ortoImageProbes = await GIS.Query.OrtoImageProbesAsync(httpClient, boundingBox2D, GIS.Constants.WebService.StandardResolutionTime, GIS.Constants.WebService.StandardResolutionTimeLayer, ["1995", "2023"], width, Query.IsOrtoBlank, 2, TimeSpan.FromSeconds(2));

            Assert.NotNull(ortoImageProbes);
            Assert.Equal(2, ortoImageProbes.Count);
            Assert.Equal(OrtoImageProbeStatus.Blank, ortoImageProbes[0].OrtoImageProbeStatus);
            Assert.Null(ortoImageProbes[0].ContentHash);
            Assert.Equal(OrtoImageProbeStatus.Image, ortoImageProbes[1].OrtoImageProbeStatus);
            Assert.NotNull(ortoImageProbes[1].ContentHash);

            byte[] bytes = await httpClient.GetByteArrayAsync(url);
            Assert.False(Query.IsOrtoBlank(bytes));
        }
    }
}
