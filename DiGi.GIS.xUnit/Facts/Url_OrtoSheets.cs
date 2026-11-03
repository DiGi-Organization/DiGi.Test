using DiGi.Geometry.Planar.Classes;
using System.Globalization;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the exact shape of the orthophoto index and image URLs: the mandatory WFS <c>VERSION=1.1.0</c>, paging parameters, the easting-first <c>BBOX</c> without a CRS suffix, the WMS 1.1.0 pin, and culture-invariant numbers.
        /// <para>Asserted again under <c>pl-PL</c>, where a culture-sensitive number would put a comma inside the box.</para>
        /// </summary>
        [Fact]
        public void Url_OrtoSheets()
        {
            BoundingBox2D boundingBox2D = new(new Point2D(316700.5, 546500.25), new Point2D(316800, 546600));

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                foreach (string name in new string[] { "en-US", "pl-PL" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);

                    Assert.Equal("gugik:SkorowidzOrtofomapy2005", Query.OrtoSheetTypeName(2005));

                    Assert.Equal(
                        "https://mapy.geoportal.gov.pl/wss/service/PZGIK/ORTO/WFS/Skorowidze?SERVICE=WFS&VERSION=1.1.0&REQUEST=GetFeature&TYPENAME=gugik:SkorowidzOrtofomapy2005&SRSNAME=EPSG:2180&MAXFEATURES=5000&STARTINDEX=10000&BBOX=316700.5,546500.25,316800,546600",
                        Query.Url_OrtoSheets(Query.OrtoSheetTypeName(2005), 10000, Constants.WebService.OrtoSheetPageSize, boundingBox2D));

                    Assert.Equal(
                        "https://mapy.geoportal.gov.pl/wss/service/PZGIK/ORTO/WFS/Skorowidze?SERVICE=WFS&VERSION=1.1.0&REQUEST=GetFeature&TYPENAME=gugik:SkorowidzOrtofomapy1998&SRSNAME=EPSG:2180&RESULTTYPE=hits",
                        Query.Url_OrtoSheetCount(Query.OrtoSheetTypeName(1998)));

                    Assert.Equal(
                        "https://mapy.geoportal.gov.pl/wss/service/PZGIK/ORTO/WMS/HighResolutionTime?SERVICE=WMS&VERSION=1.1.0&REQUEST=GetMap&LAYERS=Image&STYLES=&FORMAT=image%2Fjpeg&EXCEPTIONS=application%2Fvnd.ogc.se_xml&TIME=2023-04-22&SRS=EPSG:2180&WIDTH=256&HEIGHT=256&BBOX=316700.5,546500.25,316800,546600",
                        Query.Url_OrtoImage(boundingBox2D, Constants.WebService.HighResolutionTime, Constants.WebService.HighResolutionTimeLayer, "2023-04-22", 256, 256));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }

            Assert.Null(Query.Url_OrtoSheets(null, 0, 1));
            Assert.Null(Query.Url_OrtoSheets("x", -1, 1));
            Assert.Null(Query.Url_OrtoSheets("x", 0, 0));
            Assert.Null(Query.Url_OrtoImage(boundingBox2D, Constants.WebService.HighResolutionTime, Constants.WebService.HighResolutionTimeLayer, "2023", 0, 256));
        }
    }
}
