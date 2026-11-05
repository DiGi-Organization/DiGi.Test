using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Checks that the live GUGiK time-enabled WMS still answers a missing epoch with a tile the dependency-free blank check recognises (DiGi.GIS.WebAPI#76).
        /// <para>Requests, through <see cref="Query.Url_OrtoImage"/> and <see cref="Query.ResponseBytesAsync"/>, a square west of the Oder (BBOX 194000,506000,195000,507000, outside every sheet, so the service answers HTTP 200 with uniform white) and a square in central Warsaw with <c>TIME=2024</c> (imagery). Both must be JPEGs. The first must be blank; the second must deliver content within eight requests, because the service answers a covered square blank on a share of requests (the flakiness the endpoint refetches against). Should the service ever change its empty answer, this fact - not the area-image endpoint - is where it shows.</para>
        /// <para>Medium test (calls a live external service): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task IsOrtoJpegBlank_Live()
        {
            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

            Geometry.Planar.Classes.BoundingBox2D boundingBox2D_Outside = new(new Geometry.Planar.Classes.Point2D(194000, 506000), new Geometry.Planar.Classes.Point2D(195000, 507000));
            Geometry.Planar.Classes.BoundingBox2D boundingBox2D_Warszawa = new(new Geometry.Planar.Classes.Point2D(636000, 486000), new Geometry.Planar.Classes.Point2D(637000, 487000));

            string? url_Outside = boundingBox2D_Outside.Url_OrtoImage(Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, "2024", 256, 256);
            string? url_Warszawa = boundingBox2D_Warszawa.Url_OrtoImage(Constants.WebService.StandardResolutionTime, Constants.WebService.StandardResolutionTimeLayer, "2024", 256, 256);
            Assert.NotNull(url_Outside);
            Assert.NotNull(url_Warszawa);

            byte[]? bytes_Outside = await httpClient.ResponseBytesAsync(url_Outside, 3, TimeSpan.FromSeconds(2));
            Assert.NotNull(bytes_Outside);
            Assert.True(bytes_Outside.IsOrtoImage());
            Assert.True(bytes_Outside.IsOrtoJpegBlank());

            // The covered square itself answers blank on a share of requests (6 of 20 on 2026-11-05) - the defect
            // of DiGi.GIS.WebAPI#76. Ask up to eight times; content must arrive, and every answer must be a JPEG.
            int count_Blank = 0;
            bool content = false;
            for (int i = 0; i < 8 && !content; i++)
            {
                byte[]? bytes_Warszawa = await httpClient.ResponseBytesAsync(url_Warszawa, 3, TimeSpan.FromSeconds(2));
                Assert.NotNull(bytes_Warszawa);
                Assert.True(bytes_Warszawa.IsOrtoImage());

                if (bytes_Warszawa.IsOrtoJpegBlank())
                {
                    count_Blank++;
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                else
                {
                    content = true;
                }
            }

            Assert.True(content, $"the covered square answered blank {count_Blank} times in a row");
        }
    }
}
