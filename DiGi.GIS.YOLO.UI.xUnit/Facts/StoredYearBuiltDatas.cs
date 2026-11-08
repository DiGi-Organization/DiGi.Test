using DiGi.GIS.Classes;
using DiGi.GIS.WebAPI.Classes;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the stored history read keeps every row of a reference, reports the references of a page it could not read, and asks for the fallback read on every page.
        /// <para>The Legacy rule looks at every user entry of every row, so a read that kept the first row per reference - as the prediction pipeline&apos;s read does - would miss an early entry on a second row. And the references of a failed page have to come back as failed, not as absent: absent reads as &quot;nothing stored&quot;, which is a clean building.</para>
        /// </summary>
        [Fact]
        public async Task StoredYearBuiltDatas()
        {
            Dictionary<string, short> years = Years(20);

            YearBuiltData yearBuiltData_1 = new("REF_00");
            Assert.True(yearBuiltData_1.SetUserYearBuilt(new UserYearBuilt(1990, GIS.Enums.YearBuiltRelation.Exact, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "a")));

            YearBuiltData yearBuiltData_2 = new("REF_00");
            Assert.True(yearBuiltData_2.SetUserYearBuilt(new UserYearBuilt(1991)));

            string body = Core.Convert.ToSystem_String(new List<YearBuiltData> { yearBuiltData_1, yearBuiltData_2 }) ?? string.Empty;

            int bulkReadCount = 0;
            List<string> queries = [];
            StubHttpClientFactory stubHttpClientFactory = new(request =>
            {
                if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/gis/yearbuiltdata/itemsbyreferences")
                {
                    queries.Add(request.RequestUri.Query);

                    bulkReadCount++;
                    if (bulkReadCount == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(body, Encoding.UTF8, "application/json")
                        };
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Database read failed.")
                };
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            List<string> references_Failed = [];
            Dictionary<string, List<YearBuiltData>>? yearBuiltDatas_ByReference = await Query.StoredYearBuiltDatasAsync(gISWebAPIManager, 1, years.Keys, 10, references_Failed, null, default);

            Assert.NotNull(yearBuiltDatas_ByReference);
            Assert.Single(yearBuiltDatas_ByReference!);
            Assert.Equal(2, yearBuiltDatas_ByReference!["REF_00"].Count);

            Assert.Equal(10, references_Failed.Count);
            for (int i = 10; i < 20; i++)
            {
                Assert.Contains(string.Format("REF_{0:D2}", i), references_Failed);
            }

            Assert.Equal(2, queries.Count);
            Assert.All(queries, query => Assert.Contains("fallbackbyreference=true", query, StringComparison.OrdinalIgnoreCase));
            Assert.All(queries, query => Assert.Contains("countyid=1", query, StringComparison.OrdinalIgnoreCase));
        }
    }
}
