using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the classification of time-enabled WMS responses in the mandatory order exception, blank, content hash, against a scripted service.
        /// <para>Two times serving the same image share a hash; a blank that turns into content on the second request is an image; a persistent blank and an exception carry no hash, so no number of blanks can form a duplicate pair.</para>
        /// <para>The scale guard is asserted on both sides of its boundary (half of <see cref="Constants.WebService.OrtoMaxScaleDenominator"/>): a 4 km tile passes at 605 px and is refused at 604 px. It was shown to fail: with the check removed, the 200 px request was sent.</para>
        /// </summary>
        [Fact]
        public async Task OrtoImageProbesAsync()
        {
            byte[] bytes_Image_A = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
            byte[] bytes_Image_B = [0xFF, 0xD8, 0xFF, 0xE0, 4, 5, 6];
            byte[] bytes_Blank = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0];
            byte[] bytes_Exception = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><ServiceExceptionReport><ServiceException code=\"LayerNotDefined\"/></ServiceExceptionReport>");

            ElevationHttpMessageHandler elevationHttpMessageHandler = new((string url, int attempt) =>
            {
                string time = Regex.Match(url, "TIME=([0-9-]+)").Groups[1].Value;
                byte[] bytes = time switch
                {
                    "2015" => bytes_Image_A,
                    "2016" => bytes_Image_A,
                    "2017" => attempt == 1 ? bytes_Blank : bytes_Image_B,
                    "2018" => bytes_Blank,
                    "2019" => bytes_Exception,
                    "2020" => bytes_Blank,
                    _ => [],
                };

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            });

            static bool IsBlank(byte[] bytes)
            {
                return bytes.Length > 4 && bytes[4] == 0;
            }

            BoundingBox2D boundingBox2D = new(new Point2D(500000, 300000), new Point2D(501000, 301000));

            using HttpClient httpClient = new(elevationHttpMessageHandler);

            List<Classes.OrtoImageProbe>? ortoImageProbes = await Query.OrtoImageProbesAsync(httpClient, boundingBox2D, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, ["2015", "2016", "2017", "2018", "2019", "2020"], 256, IsBlank, 0, TimeSpan.Zero);
            Assert.NotNull(ortoImageProbes);
            Assert.Equal(6, ortoImageProbes.Count);

            Assert.Equal(OrtoImageProbeStatus.Image, ortoImageProbes[0].OrtoImageProbeStatus);
            Assert.Equal(OrtoImageProbeStatus.Image, ortoImageProbes[1].OrtoImageProbeStatus);
            Assert.Equal(ortoImageProbes[0].ContentHash, ortoImageProbes[1].ContentHash);
            Assert.Equal(Query.ContentHash(bytes_Image_A), ortoImageProbes[0].ContentHash);

            Assert.Equal(OrtoImageProbeStatus.Image, ortoImageProbes[2].OrtoImageProbeStatus);
            Assert.NotEqual(ortoImageProbes[0].ContentHash, ortoImageProbes[2].ContentHash);

            Assert.Equal(OrtoImageProbeStatus.Blank, ortoImageProbes[3].OrtoImageProbeStatus);
            Assert.Null(ortoImageProbes[3].ContentHash);
            Assert.Equal(OrtoImageProbeStatus.Exception, ortoImageProbes[4].OrtoImageProbeStatus);
            Assert.Null(ortoImageProbes[4].ContentHash);
            Assert.Equal(OrtoImageProbeStatus.Blank, ortoImageProbes[5].OrtoImageProbeStatus);
            Assert.Null(ortoImageProbes[5].ContentHash);

            // Blanks are asked for twice before they count; an image and an exception once.
            Assert.Equal(1, elevationHttpMessageHandler.CountByUrl(Query.Url_OrtoImage(boundingBox2D, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, "2015", 256, 256)!));
            Assert.Equal(2, elevationHttpMessageHandler.CountByUrl(Query.Url_OrtoImage(boundingBox2D, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, "2018", 256, 256)!));
            Assert.Equal(1, elevationHttpMessageHandler.CountByUrl(Query.Url_OrtoImage(boundingBox2D, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, "2019", 256, 256)!));

            Assert.False(Query.IsOrtoImage(bytes_Exception));
            Assert.True(Query.IsOrtoImage(bytes_Image_A));
            Assert.True(Query.IsOrtoImage([0x89, 0x50, 0x4E, 0x47, 0x0D]));
            Assert.False(Query.IsOrtoImage(null));

            BoundingBox2D boundingBox2D_Large = new(new Point2D(500000, 300000), new Point2D(504000, 304000));
            int count = elevationHttpMessageHandler.Count;

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Query.OrtoImageProbesAsync(httpClient, boundingBox2D_Large, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, ["2015"], 200, IsBlank, 0, TimeSpan.Zero));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Query.OrtoImageProbesAsync(httpClient, boundingBox2D_Large, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, ["2015"], 604, IsBlank, 0, TimeSpan.Zero));
            Assert.Equal(count, elevationHttpMessageHandler.Count);

            List<Classes.OrtoImageProbe>? ortoImageProbes_Boundary = await Query.OrtoImageProbesAsync(httpClient, boundingBox2D_Large, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, ["2015"], 605, IsBlank, 0, TimeSpan.Zero);
            Assert.Equal(OrtoImageProbeStatus.Image, ortoImageProbes_Boundary?[0].OrtoImageProbeStatus);

            Assert.Null(await Query.OrtoImageProbesAsync(httpClient, boundingBox2D, Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, ["2015"], 256, null, 0, TimeSpan.Zero));
        }
    }
}
