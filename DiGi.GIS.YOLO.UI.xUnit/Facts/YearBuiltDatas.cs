using DiGi.GIS.Classes;
using DiGi.GIS.WebAPI.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the year built read goes to the bulk endpoint, one request per page of the batch size asked for, rather than one request per building.
        /// <para>The issue the read exists to fix is the per-building request itself: a county is tens of thousands of buildings, and the endpoint answers ten thousand references in one request. The pages carry the references in order at the page sizes the batch states, and every page asks for the county and for the fallback read - an omitted parameter keeps the server default, and the server default for the fallback is off.</para>
        /// </summary>
        [Fact]
        public async Task YearBuiltDatas_PagedBulkRead()
        {
            int countyId = 1;
            int referenceBatchSize = 10;
            Dictionary<string, short> years = Years(25);
            DateTimeOffset runTimestamp = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

            List<HttpRequestMessage> requests = [];
            StubHttpClientFactory stubHttpClientFactory = new(request =>
            {
                requests.Add(request);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            List<YearBuiltData> result = await Query.YearBuiltDatasAsync(gISWebAPIManager, countyId, years, runTimestamp, modelId_Regressor_Test, true, referenceBatchSize, null, default);

            List<HttpRequestMessage> bulkReads = requests.Where(request => request.RequestUri?.AbsolutePath == "/gis/yearbuiltdata/itemsbyreferences").ToList();
            List<HttpRequestMessage> singularReads = requests.Where(request => request.RequestUri?.AbsolutePath == "/gis/yearbuiltdata/itemsbyreference").ToList();

            Assert.Empty(singularReads);
            Assert.Equal(3, bulkReads.Count);
            Assert.All(bulkReads, request => Assert.Equal(HttpMethod.Post, request.Method));

            // Ten, ten and five references, in the order they were asked for.
            AssertPage(bulkReads[0], 0, 10);
            AssertPage(bulkReads[1], 10, 10);
            AssertPage(bulkReads[2], 20, 5);

            foreach (HttpRequestMessage request in bulkReads)
            {
                string query = request.RequestUri!.Query;
                Assert.Contains("countyid=1", query, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("fallbackbyreference=true", query, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(25, result.Count);
            for (int i = 0; i < 25; i++)
            {
                Assert.Equal(string.Format(CultureInfo.CurrentCulture, "REF_{0:D2}", i), result[i].Reference);
                AssertPrediction(result[i], runTimestamp);
            }
        }

        /// <summary>
        /// Verifies that a stored entry is merged rather than replaced: its identifier and its prior history survive the read, and this run&apos;s prediction is added to them.
        /// <para>The read exists so the write can address the stored row by its own identifier - a datum built fresh carries a new one and is stored <i>alongside</i> whatever the building already holds. The entry the page answers with is the one returned, unchanged except for the added prediction, and the rest of the page is fresh data as before.</para>
        /// </summary>
        [Fact]
        public async Task YearBuiltDatas_MergesStoredEntries()
        {
            int countyId = 1;
            int referenceBatchSize = 10;
            Dictionary<string, short> years = Years(20);
            DateTimeOffset runTimestamp = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

            string reference_Stored = "REF_00";
            DateTime priorPrediction = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            YearBuiltData stored = new(reference_Stored);
            Assert.True(stored.SetPredictedYearBuilt(priorPrediction, 1990));
            Guid storedGuid = stored.Guid;
            string body_Stored = Core.Convert.ToSystem_String(new List<YearBuiltData> { stored }) ?? string.Empty;

            int bulkReadCount = 0;
            StubHttpClientFactory stubHttpClientFactory = new(request =>
            {
                if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/gis/yearbuiltdata/itemsbyreferences")
                {
                    bulkReadCount++;
                    if (bulkReadCount == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(body_Stored, Encoding.UTF8, "application/json")
                        };
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            List<YearBuiltData> result = await Query.YearBuiltDatasAsync(gISWebAPIManager, countyId, years, runTimestamp, modelId_Regressor_Test, true, referenceBatchSize, null, default);

            Assert.Equal(20, result.Count);

            YearBuiltData yearBuiltData_Stored = result.First(x => x.Reference == reference_Stored);
            Assert.Equal(storedGuid, yearBuiltData_Stored.Guid);

            // The prior history is still there, and this run&apos;s prediction is the latest one.
            PredictedYearBuilt? prior = yearBuiltData_Stored.GetPredictedYearBuilt(priorPrediction);
            Assert.NotNull(prior);
            Assert.Equal(1990, prior!.Year);

            PredictedYearBuilt? latest = yearBuiltData_Stored.GetLatestPredictedYearBuilt();
            Assert.NotNull(latest);
            Assert.Equal(runTimestamp.UtcDateTime, latest!.DateTime);
            AssertPrediction(yearBuiltData_Stored, runTimestamp);
            Assert.Equal(2, yearBuiltData_Stored.GetYearBuilts<PredictedYearBuilt>()!.Count);

            // Every other reference of the page is fresh data carrying only this run&apos;s prediction.
            List<YearBuiltData> fresh = result.Where(x => x.Reference != reference_Stored).ToList();
            Assert.Equal(19, fresh.Count);
            Assert.All(fresh, x => Assert.NotEqual(storedGuid, x.Guid));
            Assert.All(fresh, x => Assert.Single(x.GetYearBuilts<PredictedYearBuilt>()!));
            Assert.All(fresh, x => AssertPrediction(x, runTimestamp));
        }

        /// <summary>
        /// Verifies that a page the endpoint cannot read is skipped rather than answered with a fresh datum for every building of it.
        /// <para>The old read caught the failure per building and wrote a fresh datum anyway, which stored a second row alongside the one that could not be read. A failed page has to leave its buildings out of the result instead: they carry no prediction this run, and a re-run merges them without duplicating anything.</para>
        /// </summary>
        [Fact]
        public async Task YearBuiltDatas_FailedPageSkipped()
        {
            int countyId = 1;
            int referenceBatchSize = 10;
            Dictionary<string, short> years = Years(20);
            DateTimeOffset runTimestamp = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

            YearBuiltData stored = new("REF_00");
            Assert.True(stored.SetPredictedYearBuilt(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), 1990));
            string body_Stored = Core.Convert.ToSystem_String(new List<YearBuiltData> { stored }) ?? string.Empty;

            int bulkReadCount = 0;
            StubHttpClientFactory stubHttpClientFactory = new(request =>
            {
                if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/gis/yearbuiltdata/itemsbyreferences")
                {
                    bulkReadCount++;
                    if (bulkReadCount == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(body_Stored, Encoding.UTF8, "application/json")
                        };
                    }
                }

                // The page read fails, and so does the per-building read the old code issues instead of it.
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("Database read failed.")
                };
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            List<YearBuiltData> result = await Query.YearBuiltDatasAsync(gISWebAPIManager, countyId, years, runTimestamp, modelId_Regressor_Test, true, referenceBatchSize, null, default);

            // Page one is read: its ten references carry the run&apos;s prediction.
            // Page two is not: none of its ten references may appear, fresh or otherwise.
            Assert.Equal(10, result.Count);

            List<string> references = result.Select(x => x.Reference!).ToList();
            for (int i = 0; i < 10; i++)
            {
                Assert.Contains(string.Format(CultureInfo.CurrentCulture, "REF_{0:D2}", i), references);
            }

            for (int i = 10; i < 20; i++)
            {
                Assert.DoesNotContain(string.Format(CultureInfo.CurrentCulture, "REF_{0:D2}", i), references);
            }
        }

        /// <summary>
        /// Verifies that a county whose year built read fails is flagged in the run result, so a step that did not complete is visible in <see cref="Classes.YearBuiltPredictionResult.FailedStepNames"/>.
        /// <para>The read answers a failure, and the buildings of the unread page must not be written as if they had been read. The run still comes back - it is the failed step name that says the year built leg did not complete.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_YearBuiltReadFailed_Flagged()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965);
            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunWithStubbedYearBuiltRead(nameof(RunYearBuiltPredictions_YearBuiltReadFailed_Flagged), HttpStatusCode.InternalServerError, yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(2, yearBuiltPredictionResult!.BuildingCount);
            Assert.Contains(nameof(Query.YearBuiltDatasAsync), yearBuiltPredictionResult.FailedStepNames);
        }

        /// <summary>
        /// Verifies that a county whose year built read answers no content is not flagged: a building with nothing stored is not a failure, and the run stores the fresh data it built.
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_YearBuiltReadNoContent_NotFlagged()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965);
            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunWithStubbedYearBuiltRead(nameof(RunYearBuiltPredictions_YearBuiltReadNoContent_NotFlagged), HttpStatusCode.NoContent, yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(2, yearBuiltPredictionResult!.BuildingCount);
            Assert.DoesNotContain(nameof(Query.YearBuiltDatasAsync), yearBuiltPredictionResult.FailedStepNames);
            Assert.Equal(2, yearBuiltPredictionResult.YearBuiltDataUpdatedCount);
        }

        /// <summary>
        /// Builds the predicted years of <paramref name="count"/> references, each carrying the same year, so a read can be asserted against.
        /// </summary>
        /// <param name="count">The number of references to build.</param>
        /// <returns>The predicted construction year of each building, by reference.</returns>
        private static Dictionary<string, short> Years(int count)
        {
            Dictionary<string, short> years = [];
            for (int i = 0; i < count; i++)
            {
                years[string.Format(CultureInfo.CurrentCulture, "REF_{0:D2}", i)] = 1975;
            }

            return years;
        }

        /// <summary>
        /// Asserts that <paramref name="yearBuiltData"/> carries this run&apos;s prediction under the run&apos;s stamp.
        /// </summary>
        /// <param name="yearBuiltData">The year built data to assert.</param>
        /// <param name="runTimestamp">The stamp the run carries.</param>
        private static void AssertPrediction(YearBuiltData yearBuiltData, DateTimeOffset runTimestamp)
        {
            PredictedYearBuilt? latest = yearBuiltData.GetLatestPredictedYearBuilt();
            Assert.NotNull(latest);
            Assert.Equal(runTimestamp.UtcDateTime, latest!.DateTime);
            Assert.Equal(1975, latest.Year);

            // Every prediction of the run carries the regressor that produced it (DiGi-Organization/DiGi.GIS.YOLO.UI#26).
            Assert.Equal(modelId_Regressor_Test, latest.ModelId);
        }

        /// <summary>
        /// The regressor identity the year built read facts stamp on their predictions - shaped like the SHA-256 of the deployed model file.
        /// </summary>
        private const string modelId_Regressor_Test = "2e120f495e830f0917cace33ad21c5c4c7bb0c4e8f1711a363682932b7c0b7fa";

        /// <summary>
        /// Verifies that a run storing no history still stamps the regressor on every fresh prediction, and that a predictor stating no identity stores predictions with none - the behaviour before DiGi-Organization/DiGi.GIS.YOLO.UI#26, kept for stubs and third-party predictors.
        /// <para>Nothing is read when history is not stored, so no request is expected at all.</para>
        /// </summary>
        [Fact]
        public async Task YearBuiltDatas_ModelId()
        {
            Dictionary<string, short> years = Years(3);
            DateTimeOffset runTimestamp = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

            int requestCount = 0;
            StubHttpClientFactory stubHttpClientFactory = new(request =>
            {
                requestCount++;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            List<YearBuiltData> result = await Query.YearBuiltDatasAsync(gISWebAPIManager, 1, years, runTimestamp, modelId_Regressor_Test, false);
            Assert.Equal(3, result.Count);
            Assert.All(result, x => AssertPrediction(x, runTimestamp));

            List<YearBuiltData> result_NoModelId = await Query.YearBuiltDatasAsync(gISWebAPIManager, 1, years, runTimestamp, null, false);
            Assert.Equal(3, result_NoModelId.Count);
            Assert.All(result_NoModelId, x => Assert.Null(x.GetLatestPredictedYearBuilt()?.ModelId));

            Assert.Equal(0, requestCount);
        }

        /// <summary>
        /// Asserts that the page a bulk read sent carries the expected slice of references, in order.
        /// </summary>
        /// <param name="request">The bulk read request to assert.</param>
        /// <param name="offset">The index of the first reference of the page in the original order.</param>
        /// <param name="count">The number of references the page carries.</param>
        private static void AssertPage(HttpRequestMessage request, int offset, int count)
        {
            JsonNode? json = JsonNode.Parse(ReadBody(request));
            JsonArray? page = json as JsonArray;
            Assert.NotNull(page);

            Assert.Equal(count, page!.Count);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(string.Format(CultureInfo.CurrentCulture, "REF_{0:D2}", offset + i), page[i]!.ToString());
            }
        }

        /// <summary>
        /// Reads the body of <paramref name="request"/> as a string, for the assertions.
        /// <para>The client gzips its request bodies, the way the deployed host receives them, so a compressed body is decompressed first.</para>
        /// </summary>
        /// <param name="request">The request whose body to read.</param>
        /// <returns>The body of the request, or an empty string when it has none.</returns>
        private static string ReadBody(HttpRequestMessage request)
        {
            if (request.Content is null)
            {
                return string.Empty;
            }

            byte[] bytes = request.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            {
                using (MemoryStream memoryStream = new(bytes))
                using (GZipStream gzipStream = new(memoryStream, CompressionMode.Decompress))
                using (MemoryStream decompressed = new())
                {
                    gzipStream.CopyTo(decompressed);
                    return Encoding.UTF8.GetString(decompressed.ToArray());
                }
            }

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Drives the orchestrator over the stored detection fixture with the feature table and the year built read stubbed, so the year built leg can be exercised without a database.
        /// </summary>
        /// <param name="name">The name of the calling fact, used as the scratch directory so the runs cannot share state.</param>
        /// <param name="yearBuiltReadStatus">The status the year built read is answered with: no content for a county with nothing stored, a server error for a page the endpoint cannot read.</param>
        /// <param name="yearBuiltPredictorStub">The predictor handed to the run.</param>
        /// <returns>A task returning the result of the run.</returns>
        private static async Task<Classes.YearBuiltPredictionResult?> RunWithStubbedYearBuiltRead(string name, HttpStatusCode yearBuiltReadStatus, YearBuiltPredictorStub yearBuiltPredictorStub)
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, name);
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(CultureInfo.CurrentCulture));
            Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            string json_Table = FeatureTableJson(["0207", "0209"], []);

            StubHttpClientFactory stubHttpClientFactory = new(httpRequestMessage =>
            {
                string path = httpRequestMessage.RequestUri?.AbsolutePath ?? string.Empty;

                // Only the feature read is answered with content, so the run reaches the year built leg.
                if (path.Contains("buildingdata", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json_Table, Encoding.UTF8, "application/json")
                    };
                }

                // The write leg is answered with no content - a success - so the count it reports is the read's.
                if (path.Contains("updateitems", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                // The year built read is the leg under test, answered as the caller states.
                if (path.Contains("itemsbyreference", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(yearBuiltReadStatus);
                }

                // The county rows are not answered, which leaves the run treating the county as a single polygon part.
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            Classes.YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                ExportImages = false,
                RunPrediction = false,
                Score = true,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = true
            };

            return await gISWebAPIManager.RunYearBuiltPredictionsAsync(yearBuiltPredictorStub, yearBuiltPredictionPipelineOptions);
        }
    }
}
