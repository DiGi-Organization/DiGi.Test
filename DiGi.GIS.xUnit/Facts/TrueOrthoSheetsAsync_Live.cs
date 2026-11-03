using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reads the true orthophoto sheets covering one Warsaw point from the live true orthophoto WMS and one point outside every covered city.
        /// <para>Proves the live <c>GetFeatureInfo</c> URL is accepted and both answers still parse: every sheet is <see cref="OrtoType.TrueOrtho"/>, downloads from <c>DaneFotogrametryczne/TrueOrtho/</c>, and has a footprint that contains the point (so the boxes are easting first and paired correctly). At least the two sheets of 2026-11-03 are expected; later acquisitions only add to them.</para>
        /// <para>Medium test (calls a live external service): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task TrueOrthoSheetsAsync_Live()
        {
            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(1) };

            Point2D point2D = new(636500, 467500);

            List<Classes.OrtoSheet>? ortoSheets = await Query.TrueOrthoSheetsAsync(httpClient, point2D, 3, TimeSpan.FromSeconds(2));
            Assert.NotNull(ortoSheets);
            Assert.True(ortoSheets.Count >= 2);

            foreach (Classes.OrtoSheet ortoSheet in ortoSheets)
            {
                Assert.Equal(OrtoType.TrueOrtho, ortoSheet.OrtoType);
                Assert.Contains("/DaneFotogrametryczne/TrueOrtho/", ortoSheet.DownloadUrl);
                Assert.NotNull(ortoSheet.Date);

                BoundingBox2D? boundingBox2D = ortoSheet.Footprint?.GetBoundingBox();
                Assert.NotNull(boundingBox2D);
                Assert.InRange(point2D.X, boundingBox2D.Min.X, boundingBox2D.Max.X);
                Assert.InRange(point2D.Y, boundingBox2D.Min.Y, boundingBox2D.Max.Y);
            }

            Assert.Contains(ortoSheets, x => x.SheetId == "N-34-138-D-d-2-4-2");

            List<Classes.OrtoSheet>? ortoSheets_Uncovered = await Query.TrueOrthoSheetsAsync(httpClient, new Point2D(500000, 300000), 3, TimeSpan.FromSeconds(2));
            Assert.NotNull(ortoSheets_Uncovered);
            Assert.Empty(ortoSheets_Uncovered);
        }
    }
}
