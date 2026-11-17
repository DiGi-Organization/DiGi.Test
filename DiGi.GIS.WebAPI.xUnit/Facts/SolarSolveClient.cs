using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies how <see cref="Modify.SetBuildingSolarRadiationsAsync"/> splits and sends a batch, against a scripted server.
        /// <para>Three results over two county parts, with a one-byte memory size, go as three requests - one result each, the parts in the order first met, the timeout in the query and the token as a bearer header - and the answer is the sum of the counts stored. Each body is a JSON array of DiGi JSON, <c>_type</c> kept, that reads back member by member, shading factors included. With the default memory size the same results go as two requests, never mixing parts. Reference: DiGi.GIS.WebAPI#93.</para>
        /// </summary>
        [Fact]
        public async Task SetBuildingSolarRadiationsAsync_SplitsByPartAndMemory()
        {
            GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_A1 = ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-A1", 990311401L);
            GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_B1 = ScratchBatchSolarRadiationResult(countyId_SolarRadiation_Batch, "XUNIT-SR-B1", 990311402L);
            GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_A2 = ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-A2", 990311403L);
            List<GIS.Classes.BuildingSolarRadiationResult> buildingSolarRadiationResults = [buildingSolarRadiationResult_A1, buildingSolarRadiationResult_B1, buildingSolarRadiationResult_A2];

            // One result per request.
            ScriptedHttpMessageHandler scriptedHttpMessageHandler = new((path, count) => SolarSolveClientResponse(HttpStatusCode.OK, "1"));
            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveClientManager(scriptedHttpMessageHandler);
            using (serviceProvider)
            {
                int? stored = await gISWebAPIManager.SetBuildingSolarRadiationsAsync(buildingSolarRadiationResults, "token-93", serializableObjectsPostOptions: new SerializableObjectsPostOptions() { BatchMemorySize = 1 });
                Assert.Equal(3, stored);
            }

            (string PathAndQuery, string? Body)[] requests = [.. scriptedHttpMessageHandler.Requests];
            Assert.Equal(3, requests.Length);
            Assert.All(scriptedHttpMessageHandler.Authorizations, authorization => Assert.Equal("Bearer token-93", authorization));

            GIS.Classes.BuildingSolarRadiationResult[] buildingSolarRadiationResults_Expected = [buildingSolarRadiationResult_A1, buildingSolarRadiationResult_A2, buildingSolarRadiationResult_B1];
            for (int i = 0; i < requests.Length; i++)
            {
                Assert.EndsWith("/setsolarradiations?commandtimeout=600", requests[i].PathAndQuery, StringComparison.OrdinalIgnoreCase);

                JsonArray jsonArray = Assert.IsType<JsonArray>(JsonNode.Parse(requests[i].Body!));
                JsonObject jsonObject = Assert.IsType<JsonObject>(Assert.Single(jsonArray));
                Assert.True(jsonObject.ContainsKey("_type"), "The body lost the DiGi type marker.");

                GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult_Sent = Core.Create.SerializableObject<GIS.Classes.BuildingSolarRadiationResult>(jsonObject);
                Assert.NotNull(buildingSolarRadiationResult_Sent);

                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_Expected = buildingSolarRadiationResults_Expected[i];
                Assert.Equal(buildingSolarRadiationResult_Expected.Reference, buildingSolarRadiationResult_Sent.Reference);
                Assert.Equal(buildingSolarRadiationResult_Expected.CountyId, buildingSolarRadiationResult_Sent.CountyId);
                Assert.Equal(buildingSolarRadiationResult_Expected.BuildingModelId, buildingSolarRadiationResult_Sent.BuildingModelId);
                Assert.Equal(buildingSolarRadiationResult_Expected.Radius, buildingSolarRadiationResult_Sent.Radius);
                Assert.Equal(buildingSolarRadiationResult_Expected.SolverVersion, buildingSolarRadiationResult_Sent.SolverVersion);
                Assert.Equal(buildingSolarRadiationResult_Expected.WeatherSourceType, buildingSolarRadiationResult_Sent.WeatherSourceType);
                Assert.Equal(buildingSolarRadiationResult_Expected.WeatherSourceId, buildingSolarRadiationResult_Sent.WeatherSourceId);
                Assert.Equal(buildingSolarRadiationResult_Expected.UserEmail, buildingSolarRadiationResult_Sent.UserEmail);
                Assert.Equal(buildingSolarRadiationResult_Expected.CreatedAt, buildingSolarRadiationResult_Sent.CreatedAt);
                Assert.Equal(buildingSolarRadiationResult_Expected.SurfaceSolarRadiations!.Count, buildingSolarRadiationResult_Sent.SurfaceSolarRadiations!.Count);
                Assert.Equal(buildingSolarRadiationResult_Expected.SurfaceSolarRadiations[0].ShadingFactors, buildingSolarRadiationResult_Sent.SurfaceSolarRadiations[0].ShadingFactors);
                Assert.Equal(buildingSolarRadiationResult_Expected.SurfaceSolarRadiations[0].Irradiation, buildingSolarRadiationResult_Sent.SurfaceSolarRadiations[0].Irradiation);
            }

            // The default memory size holds a part in one request; the parts still go apart.
            ScriptedHttpMessageHandler scriptedHttpMessageHandler_Default = new((path, count) => SolarSolveClientResponse(HttpStatusCode.OK, count == 1 ? "2" : "1"));
            (GISWebAPIManager gISWebAPIManager_Default, ServiceProvider serviceProvider_Default) = SolarSolveClientManager(scriptedHttpMessageHandler_Default);
            using (serviceProvider_Default)
            {
                Assert.Equal(3, await gISWebAPIManager_Default.SetBuildingSolarRadiationsAsync(buildingSolarRadiationResults, "token-93"));
            }

            int[] counts = [.. scriptedHttpMessageHandler_Default.Requests.Select(request => Assert.IsType<JsonArray>(JsonNode.Parse(request.Body!)).Count)];
            int[] counts_Expected = [2, 1];
            Assert.Equal(counts_Expected, counts);
        }

        /// <summary>
        /// Verifies that <see cref="Modify.SetBuildingSolarRadiationsAsync"/> stops at the first request the server refuses, or answers with a count other than the one sent, and answers null - not the count stored before it - without sending the requests after it. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task SetBuildingSolarRadiationsAsync_StopsAtFailure()
        {
            List<GIS.Classes.BuildingSolarRadiationResult> buildingSolarRadiationResults =
            [
                ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-A1", 990311401L),
                ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-A2", 990311402L),
                ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-A3", 990311403L),
            ];

            SerializableObjectsPostOptions serializableObjectsPostOptions = new() { BatchMemorySize = 1, RetryCount = 0 };

            ScriptedHttpMessageHandler scriptedHttpMessageHandler = new((path, count) => count == 1 ? SolarSolveClientResponse(HttpStatusCode.OK, "1") : SolarSolveClientResponse(HttpStatusCode.InternalServerError, "Stored solar radiation write failed; nothing was written."));
            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveClientManager(scriptedHttpMessageHandler);
            using (serviceProvider)
            {
                Assert.Null(await gISWebAPIManager.SetBuildingSolarRadiationsAsync(buildingSolarRadiationResults, "token-93", serializableObjectsPostOptions: serializableObjectsPostOptions));
            }

            Assert.Equal(2, scriptedHttpMessageHandler.Requests.Count);

            ScriptedHttpMessageHandler scriptedHttpMessageHandler_Short = new((path, count) => SolarSolveClientResponse(HttpStatusCode.OK, "0"));
            (GISWebAPIManager gISWebAPIManager_Short, ServiceProvider serviceProvider_Short) = SolarSolveClientManager(scriptedHttpMessageHandler_Short);
            using (serviceProvider_Short)
            {
                Assert.Null(await gISWebAPIManager_Short.SetBuildingSolarRadiationsAsync(buildingSolarRadiationResults, "token-93", serializableObjectsPostOptions: serializableObjectsPostOptions));
            }

            Assert.Single(scriptedHttpMessageHandler_Short.Requests);

            Assert.Null(await gISWebAPIManager.SetBuildingSolarRadiationsAsync(buildingSolarRadiationResults, " "));
            Assert.Equal(0, await gISWebAPIManager.SetBuildingSolarRadiationsAsync([], "token-93"));
        }

        /// <summary>
        /// Verifies <see cref="Query.BuildingModelsByCountyIdAsync"/> against a scripted server: the query names the part, the cursor, the page size and the timeout; a page with <c>DiGi-Next-Cursor</c> answers its models and the next cursor; an empty last page answers no cursor; a cursor that does not advance and a failed page answer null rather than a short walk. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task BuildingModelsByCountyIdAsync_Page()
        {
            string json_Page = Core.Convert.ToSystem_String(new List<DiGi.Analytical.Building.Classes.BuildingModel>() { BuildingModel(), BuildingModel() }) ?? throw new InvalidOperationException("Building models could not be serialized");

            ScriptedHttpMessageHandler scriptedHttpMessageHandler = new((path, count) =>
            {
                HttpResponseMessage httpResponseMessage = count switch
                {
                    1 => SolarSolveClientResponse(HttpStatusCode.OK, json_Page),
                    2 => SolarSolveClientResponse(HttpStatusCode.OK, "[]"),
                    3 => SolarSolveClientResponse(HttpStatusCode.OK, json_Page),
                    _ => SolarSolveClientResponse(HttpStatusCode.InternalServerError, "Database read failed."),
                };

                if (count is 1 or 3)
                {
                    httpResponseMessage.Headers.Add(Constants.Header.NextCursor, "42");
                }

                return httpResponseMessage;
            });

            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveClientManager(scriptedHttpMessageHandler);
            using (serviceProvider)
            {
                (List<DiGi.Analytical.Building.Classes.BuildingModel> BuildingModels, long? IdAfter)? page_First = await gISWebAPIManager.BuildingModelsByCountyIdAsync(countyId_SolarRadiation, limit: 2);
                Assert.NotNull(page_First);
                Assert.Equal(2, page_First.Value.BuildingModels.Count);
                Assert.Equal(42L, page_First.Value.IdAfter);

                (List<DiGi.Analytical.Building.Classes.BuildingModel> BuildingModels, long? IdAfter)? page_Last = await gISWebAPIManager.BuildingModelsByCountyIdAsync(countyId_SolarRadiation, 42, 2);
                Assert.NotNull(page_Last);
                Assert.Empty(page_Last.Value.BuildingModels);
                Assert.Null(page_Last.Value.IdAfter);

                Assert.Null(await gISWebAPIManager.BuildingModelsByCountyIdAsync(countyId_SolarRadiation, 42, 2));
                Assert.Null(await gISWebAPIManager.BuildingModelsByCountyIdAsync(countyId_SolarRadiation, 42, 2));

                Assert.Null(await gISWebAPIManager.BuildingModelsByCountyIdAsync(0));
                Assert.Null(await gISWebAPIManager.BuildingModelsByCountyIdAsync(countyId_SolarRadiation, limit: Constants.BuildingModel.MaximumItemsByCountyIdLimit + 1));
            }

            string pathAndQuery_First = scriptedHttpMessageHandler.Requests.First().PathAndQuery;
            Assert.Contains("/itemsbycountyid?", pathAndQuery_First, StringComparison.OrdinalIgnoreCase);
            Assert.Contains($"countyid={countyId_SolarRadiation}", pathAndQuery_First, StringComparison.Ordinal);
            Assert.Contains("idafter=0", pathAndQuery_First, StringComparison.Ordinal);
            Assert.Contains("limit=2", pathAndQuery_First, StringComparison.Ordinal);
            Assert.Contains("commandtimeout=600", pathAndQuery_First, StringComparison.Ordinal);
            Assert.Equal(4, scriptedHttpMessageHandler.Requests.Count);
        }

        /// <summary>
        /// Verifies the EPW and skip-existing reads against a scripted server: <see cref="Query.EPWFileByIdAsync"/> reads the file of an id and answers null for a 404; <see cref="Query.EPWFileLocationsAsync"/> reads every station and an empty list as empty; <see cref="Query.BuildingSolarRadiationReferencesAsync"/> sends the part, radius 0 and the solver version and reads the references. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task SolarSolveClient_EPWFileAndReferences()
        {
            EPW.Classes.EPWFile? ePWFile_Fixture = EPW.Modify.Read(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw"));
            Assert.NotNull(ePWFile_Fixture);
            string json_EPWFile = ePWFile_Fixture.ToJsonObject()!.ToJsonString();
            string json_Locations = Core.Convert.ToSystem_String(new List<EPWFileLocationResult>() { new(1, 10.5, 20.5), new(2, 30.5, 40.5) }) ?? throw new InvalidOperationException("Locations could not be serialized");

            ScriptedHttpMessageHandler scriptedHttpMessageHandler = new((path, count) =>
            {
                if (path.EndsWith("/itembyid", StringComparison.OrdinalIgnoreCase))
                {
                    return count == 1 ? SolarSolveClientResponse(HttpStatusCode.OK, json_EPWFile) : new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                if (path.EndsWith("/locations", StringComparison.OrdinalIgnoreCase))
                {
                    return SolarSolveClientResponse(HttpStatusCode.OK, count == 1 ? json_Locations : "[]");
                }

                if (path.EndsWith("/referencesbycountyid", StringComparison.OrdinalIgnoreCase))
                {
                    return SolarSolveClientResponse(HttpStatusCode.OK, "[\"XUNIT-SR-R2\",\"XUNIT-SR-R1\"]");
                }

                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveClientManager(scriptedHttpMessageHandler);
            using (serviceProvider)
            {
                EPW.Classes.EPWFile? ePWFile = await gISWebAPIManager.EPWFileByIdAsync(7);
                Assert.NotNull(ePWFile);
                Assert.Equal(json_EPWFile, ePWFile.ToJsonObject()!.ToJsonString());
                Assert.Null(await gISWebAPIManager.EPWFileByIdAsync(8));
                Assert.Null(await gISWebAPIManager.EPWFileByIdAsync(0));

                List<EPWFileLocationResult>? ePWFileLocationResults = await gISWebAPIManager.EPWFileLocationsAsync();
                Assert.NotNull(ePWFileLocationResults);
                Assert.Equal(2, ePWFileLocationResults.Count);
                Assert.Equal(2, ePWFileLocationResults[1].Id);
                Assert.Equal(40.5, ePWFileLocationResults[1].Y);

                List<EPWFileLocationResult>? ePWFileLocationResults_Empty = await gISWebAPIManager.EPWFileLocationsAsync();
                Assert.NotNull(ePWFileLocationResults_Empty);
                Assert.Empty(ePWFileLocationResults_Empty);

                HashSet<string>? references = await gISWebAPIManager.BuildingSolarRadiationReferencesAsync(countyId_SolarRadiation, 0, 1);
                Assert.NotNull(references);
                Assert.True(references.SetEquals(["XUNIT-SR-R1", "XUNIT-SR-R2"]));

                Assert.Null(await gISWebAPIManager.BuildingSolarRadiationReferencesAsync(countyId_SolarRadiation, -1, 1));
            }

            string pathAndQuery_ById = scriptedHttpMessageHandler.Requests.First().PathAndQuery;
            Assert.EndsWith("/itembyid?id=7", pathAndQuery_ById, StringComparison.OrdinalIgnoreCase);

            string pathAndQuery_References = scriptedHttpMessageHandler.Requests.Last().PathAndQuery;
            Assert.Contains($"countyid={countyId_SolarRadiation}", pathAndQuery_References, StringComparison.Ordinal);
            Assert.Contains("radius=0", pathAndQuery_References, StringComparison.Ordinal);
            Assert.Contains("solverversion=1", pathAndQuery_References, StringComparison.Ordinal);
        }

        /// <summary>
        /// Builds a GIS Web API manager whose GIS client answers from the given scripted handler, and the service provider that owns the client.
        /// </summary>
        /// <param name="scriptedHttpMessageHandler">The handler answering every request.</param>
        /// <returns>The manager and the service provider; the caller disposes the provider.</returns>
        private static (GISWebAPIManager GISWebAPIManager, ServiceProvider ServiceProvider) SolarSolveClientManager(ScriptedHttpMessageHandler scriptedHttpMessageHandler)
        {
            ServiceCollection serviceCollection = new();
            serviceCollection.AddHttpClient(Constants.Name.Client.GIS, httpClient => httpClient.BaseAddress = new Uri("https://localhost/")).ConfigurePrimaryHttpMessageHandler(() => scriptedHttpMessageHandler);
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            return (new GISWebAPIManager(serviceProvider.GetRequiredService<IHttpClientFactory>()), serviceProvider);
        }

        /// <summary>
        /// Builds a scripted JSON answer.
        /// </summary>
        /// <param name="httpStatusCode">The status of the answer.</param>
        /// <param name="content">The body of the answer.</param>
        /// <returns>The answer.</returns>
        private static HttpResponseMessage SolarSolveClientResponse(HttpStatusCode httpStatusCode, string content)
        {
            return new HttpResponseMessage(httpStatusCode) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
        }
    }
}
