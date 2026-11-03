using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the paged orthophoto index pull against a scripted WFS: every page is read, each page is asked for exactly once on success, a transient 503 is retried, a service exception or a page that never advances does not truncate silently, and the hits count is parsed.
        /// <para>The non-advancing guard was shown to fail: with the guard removed, the scripted service that ignores <c>STARTINDEX</c> made the pull loop until the fact timed out.</para>
        /// </summary>
        [Fact]
        public async Task OrtoSheetsAsync()
        {
            const string typeName = "gugik:SkorowidzOrtofomapy2020";
            const int featureCount = 7;
            const int pageSize = 3;

            // 7 features in pages of 3: the third page carries 1 and ends the pull.
            Facts.ElevationHttpMessageHandler elevationHttpMessageHandler = new((string url, int attempt) =>
            {
                if (url.Contains("RESULTTYPE=hits"))
                {
                    return Response("<?xml version='1.0' encoding=\"UTF-8\" ?><wfs:FeatureCollection xmlns:wfs=\"http://www.opengis.net/wfs\" numberOfFeatures=\"7\"></wfs:FeatureCollection>");
                }

                int startIndex = int.Parse(Regex.Match(url, "STARTINDEX=([0-9]+)").Groups[1].Value, CultureInfo.InvariantCulture);
                if (startIndex == 3 && attempt == 1)
                {
                    return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                }

                return Response(FeatureCollection(startIndex, Math.Min(pageSize, featureCount - startIndex)));
            });

            using (HttpClient httpClient = new(elevationHttpMessageHandler))
            {
                Assert.Equal(featureCount, await Query.OrtoSheetCountAsync(httpClient, typeName, null, 0, TimeSpan.Zero));

                List<Classes.OrtoSheet>? ortoSheets = await Query.OrtoSheetsAsync(httpClient, typeName, null, pageSize, 2, TimeSpan.Zero);
                Assert.NotNull(ortoSheets);
                Assert.Equal(featureCount, ortoSheets.Count);
                for (int i = 0; i < featureCount; i++)
                {
                    Assert.Equal("S-" + i.ToString(CultureInfo.InvariantCulture), ortoSheets[i].SheetId);
                }

                // hits + pages at 0, 3 (503 then success) and 6.
                Assert.Equal(5, elevationHttpMessageHandler.Count);
                Assert.Equal(2, elevationHttpMessageHandler.CountByUrl(Query.Url_OrtoSheets(typeName, 3, pageSize)!));
            }

            // A service that ignores STARTINDEX answers the first page forever.
            Facts.ElevationHttpMessageHandler elevationHttpMessageHandler_Stuck = new((string url, int attempt) => Response(FeatureCollection(0, pageSize)));
            using (HttpClient httpClient = new(elevationHttpMessageHandler_Stuck))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => Query.OrtoSheetsAsync(httpClient, typeName, null, pageSize, 0, TimeSpan.Zero));
                Assert.Equal(2, elevationHttpMessageHandler_Stuck.Count);
            }

            // A service exception on the second page discards the whole pull rather than returning the first page.
            Facts.ElevationHttpMessageHandler elevationHttpMessageHandler_Exception = new((string url, int attempt) => url.Contains("STARTINDEX=0") ? Response(FeatureCollection(0, pageSize)) : Response("<?xml version=\"1.0\"?><ows:ExceptionReport xmlns:ows=\"http://www.opengis.net/ows\"/>"));
            using (HttpClient httpClient = new(elevationHttpMessageHandler_Exception))
            {
                Assert.Null(await Query.OrtoSheetsAsync(httpClient, typeName, null, pageSize, 0, TimeSpan.Zero));
            }

            // A persistent 503 fails after the retries.
            Facts.ElevationHttpMessageHandler elevationHttpMessageHandler_Unavailable = new((string url, int attempt) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            using (HttpClient httpClient = new(elevationHttpMessageHandler_Unavailable))
            {
                Assert.Null(await Query.OrtoSheetsAsync(httpClient, typeName, null, pageSize, 2, TimeSpan.Zero));
                Assert.Equal(3, elevationHttpMessageHandler_Unavailable.Count);
                Assert.Null(await Query.OrtoSheetCountAsync(httpClient, typeName, null, 0, TimeSpan.Zero));
            }

            using (CancellationTokenSource cancellationTokenSource = new())
            {
                cancellationTokenSource.Cancel();
                using HttpClient httpClient = new(elevationHttpMessageHandler);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Query.OrtoSheetsAsync(httpClient, typeName, null, pageSize, 0, TimeSpan.Zero, cancellationToken: cancellationTokenSource.Token));
            }

            static HttpResponseMessage Response(string content)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content, Encoding.UTF8, "text/xml") };
            }

            static string FeatureCollection(int startIndex, int count)
            {
                StringBuilder stringBuilder = new();
                stringBuilder.Append("<?xml version='1.0' encoding=\"UTF-8\" ?><wfs:FeatureCollection xmlns:gugik=\"http://www.gugik.gov.pl\" xmlns:gml=\"http://www.opengis.net/gml\" xmlns:wfs=\"http://www.opengis.net/wfs\">");
                for (int i = startIndex; i < startIndex + count; i++)
                {
                    string index = i.ToString(CultureInfo.InvariantCulture);
                    stringBuilder.Append("<gml:featureMember><gugik:SkorowidzOrtofomapy2020><gugik:godlo>S-").Append(index).Append("</gugik:godlo><gugik:akt_rok>2020</gugik:akt_rok><gugik:akt_data>2020-06-01</gugik:akt_data><gugik:url_do_pobrania>https://example.org/").Append(index).Append(".tif</gugik:url_do_pobrania></gugik:SkorowidzOrtofomapy2020></gml:featureMember>");
                }

                stringBuilder.Append("</wfs:FeatureCollection>");
                return stringBuilder.ToString();
            }
        }
    }
}
