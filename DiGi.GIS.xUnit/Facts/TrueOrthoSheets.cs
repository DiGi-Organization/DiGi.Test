using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests reading true orthophoto sheets from the two captured <c>GetFeatureInfo</c> answers of the true orthophoto WMS for one Warsaw point (2026-11-03): the attributes from the HTML script, the easting-first footprints from the GML boxes paired by position, and the type <see cref="OrtoType.TrueOrtho"/>.
        /// <para>Also covers: an uncovered point (empty list, not null), a service exception (null), unequal sheet counts leaving the footprints null rather than mispaired, the scripted two-request pull with a 503 retry and a failed GML request, the culture-invariant URL, and a true orthophoto and a standard sheet with equal identifier, date and resolution staying in separate duplicate groups.</para>
        /// <para>The count guard was shown to fail: with it removed, the one-box answer paired the first sheet with the other sheet's box.</para>
        /// </summary>
        [Fact]
        public async Task TrueOrthoSheets()
        {
            string html = File.ReadAllText(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "TrueOrthoSheets_Warszawa.html")!);
            string gml = File.ReadAllText(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "TrueOrthoSheets_Warszawa.gml")!);
            string html_Empty = File.ReadAllText(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "TrueOrthoSheets_Empty.html")!);
            string gml_Empty = File.ReadAllText(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "TrueOrthoSheets_Empty.gml")!);
            const string serviceException = "<?xml version='1.0' encoding=\"UTF-8\" standalone=\"no\" ?><ServiceExceptionReport version=\"1.1.0\"><ServiceException code=\"LayerNotDefined\">msWMSLoadGetMapParams(): WMS server error.</ServiceException></ServiceExceptionReport>";

            DateTimeOffset probeDate = new(2026, 11, 3, 14, 0, 0, TimeSpan.Zero);

            List<Classes.OrtoSheet>? ortoSheets = Convert.ToDiGi_OrtoSheets(html, gml, probeDate);
            Assert.NotNull(ortoSheets);
            Assert.Equal(2, ortoSheets.Count);

            Classes.OrtoSheet ortoSheet_2023 = ortoSheets[0];
            Assert.Equal("N-34-138-D-d-2-4-2", ortoSheet_2023.SheetId);
            Assert.Equal(Constants.WebService.TrueOrthoIndexLayer, ortoSheet_2023.TypeName);
            Assert.Equal(2023, ortoSheet_2023.Year);
            Assert.Equal(new DateTimeOffset(2023, 4, 21, 0, 0, 0, TimeSpan.Zero), ortoSheet_2023.Date);
            Assert.Equal(OrtoDatePrecision.Day, ortoSheet_2023.DatePrecision);
            Assert.Equal(0.05, ortoSheet_2023.PixelSize);
            Assert.Equal(OrtoColorMode.Undefined, ortoSheet_2023.ColorMode);
            Assert.Equal("Zdj. cyfrowe", ortoSheet_2023.Source);
            Assert.Equal("PL-1992", ortoSheet_2023.CoordinateSystem);
            Assert.Equal("1:2500", ortoSheet_2023.ArchiveModule);
            Assert.Equal("DFT.7201.014.2023", ortoSheet_2023.ApplicationNumber);
            Assert.Null(ortoSheet_2023.SheetFilled);
            Assert.Equal("https://opendata.geoportal.gov.pl/DaneFotogrametryczne/TrueOrtho/81584/81584_1443064_N-34-138-D-d-2-4-2.tif", ortoSheet_2023.DownloadUrl);
            Assert.Null(ortoSheet_2023.RegisterDate);
            Assert.Null(ortoSheet_2023.FileSize);
            Assert.Equal(OrtoType.TrueOrtho, ortoSheet_2023.OrtoType);
            Assert.Equal(probeDate, ortoSheet_2023.ProbeDate);
            Assert.False(Query.OrtoObstacles(ortoSheet_2023).HasFlag(OrtoObstacle.TrueOrthoUnknown));

            // gml:coordinates "635991.180000,466958.230000 637093.770000,468145.600000": easting first, no swap.
            BoundingBox2D? boundingBox2D_2023 = ortoSheet_2023.Footprint?.GetBoundingBox();
            Assert.NotNull(boundingBox2D_2023);
            Assert.Equal(635991.18, boundingBox2D_2023.Min.X, 6);
            Assert.Equal(466958.23, boundingBox2D_2023.Min.Y, 6);
            Assert.Equal(637093.77, boundingBox2D_2023.Max.X, 6);
            Assert.Equal(468145.60, boundingBox2D_2023.Max.Y, 6);

            // The second sheet is the 1:5 000 one, so its box is the larger: pairing by position holds.
            Classes.OrtoSheet ortoSheet_2025 = ortoSheets[1];
            Assert.Equal("N-34-138-D-d-2-4", ortoSheet_2025.SheetId);
            Assert.Equal("1:5000", ortoSheet_2025.ArchiveModule);
            Assert.Equal(634920.49, ortoSheet_2025.Footprint!.GetBoundingBox()!.Min.X, 6);
            Assert.True(ortoSheet_2025.Footprint!.GetArea() > 3 * ortoSheet_2023.Footprint!.GetArea());

            Assert.Empty(Convert.ToDiGi_OrtoSheets(html_Empty, gml_Empty)!);
            Assert.Null(Convert.ToDiGi_OrtoSheets(serviceException, gml));
            Assert.Null(Convert.ToDiGi_OrtoSheets((string?)null, gml));

            List<Classes.OrtoSheet>? ortoSheets_NoGml = Convert.ToDiGi_OrtoSheets(html, null);
            Assert.Equal(2, ortoSheets_NoGml?.Count);
            Assert.All(ortoSheets_NoGml!, x => Assert.Null(x.Footprint));

            // One box for two sheets cannot be paired safely.
            string gml_OneBox = string.Concat(gml.AsSpan(0, gml.IndexOf("<Skorowidze_feature>", gml.IndexOf("</Skorowidze_feature>", StringComparison.Ordinal), StringComparison.Ordinal)), "</Skorowidze_layer></msGMLOutput>");
            Assert.All(Convert.ToDiGi_OrtoSheets(html, gml_OneBox)!, x => Assert.Null(x.Footprint));

            Point2D point2D = new(636500, 467500);

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

                Assert.Equal(
                    "https://mapy.geoportal.gov.pl/wss/service/PZGIK/ORTO/WMS/TrueOrtho?SERVICE=WMS&VERSION=1.1.0&REQUEST=GetFeatureInfo&LAYERS=Skorowidze&QUERY_LAYERS=Skorowidze&STYLES=&SRS=EPSG:2180&WIDTH=101&HEIGHT=101&X=50&Y=50&FEATURE_COUNT=50&INFO_FORMAT=text%2Fhtml&BBOX=636449.5,467449.5,636550.5,467550.5",
                    Query.Url_TrueOrthoSheets(point2D, Constants.WebService.TrueOrthoInfoFormatHtml));
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }

            Assert.Null(Query.Url_TrueOrthoSheets(point2D, Constants.WebService.TrueOrthoInfoFormatHtml, 0));

            // The HTML request answers 503 once; then HTML and GML are read once each.
            Facts.ElevationHttpMessageHandler elevationHttpMessageHandler = new((string url, int attempt) =>
            {
                if (url.Contains("INFO_FORMAT=text%2Fhtml"))
                {
                    return attempt == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Response(html);
                }

                return Response(gml);
            });

            using (HttpClient httpClient = new(elevationHttpMessageHandler))
            {
                List<Classes.OrtoSheet>? ortoSheets_Async = await Query.TrueOrthoSheetsAsync(httpClient, point2D, 2, TimeSpan.Zero, probeDate);
                Assert.Equal(2, ortoSheets_Async?.Count);
                Assert.NotNull(ortoSheets_Async![1].Footprint);
                Assert.Equal(3, elevationHttpMessageHandler.Count);
            }

            // A failed GML request keeps the sheets, without footprints.
            Facts.ElevationHttpMessageHandler elevationHttpMessageHandler_NoGml = new((string url, int attempt) => url.Contains("INFO_FORMAT=text%2Fhtml") ? Response(html) : new HttpResponseMessage(HttpStatusCode.NotFound));
            using (HttpClient httpClient = new(elevationHttpMessageHandler_NoGml))
            {
                List<Classes.OrtoSheet>? ortoSheets_NoGmlAsync = await Query.TrueOrthoSheetsAsync(httpClient, point2D, 0, TimeSpan.Zero);
                Assert.Equal(2, ortoSheets_NoGmlAsync?.Count);
                Assert.All(ortoSheets_NoGmlAsync!, x => Assert.Null(x.Footprint));
                Assert.All(ortoSheets_NoGmlAsync!, x => Assert.NotNull(x.ProbeDate));
            }

            // A failed HTML request is a failed read.
            Facts.ElevationHttpMessageHandler elevationHttpMessageHandler_NoHtml = new((string url, int attempt) => new HttpResponseMessage(HttpStatusCode.NotFound));
            using (HttpClient httpClient = new(elevationHttpMessageHandler_NoHtml))
            {
                Assert.Null(await Query.TrueOrthoSheetsAsync(httpClient, point2D, 0, TimeSpan.Zero));
            }

            Assert.Null(await Query.TrueOrthoSheetsAsync(null, point2D, 0, TimeSpan.Zero));

            // Same identifier, date and resolution as a standard sheet: still a different image.
            Classes.OrtoSheet ortoSheet_Standard = new(ortoSheet_2025.SheetId, "SkorowidzOrtofomapy2025", 2025, ortoSheet_2025.Date, OrtoDatePrecision.Day, ortoSheet_2025.PixelSize, OrtoColorMode.RGB, "Zdj. cyfrowe", "PL-1992", "1:5000", null, true, "https://opendata.geoportal.gov.pl/ortofotomapa/1/1_1_N-34-138-D-d-2-4.tif", null, null, ortoSheet_2025.Footprint, OrtoType.Standard, null);
            Assert.Equal(2, Query.OrtoSheetDuplicateGroups([ortoSheet_2025, ortoSheet_Standard])!.Count);

            static HttpResponseMessage Response(string content)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8) };
            }
        }
    }
}
