using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="OrtoDatasFromDatabasePostTask"/> initializes with expected default property values and implements expected task interfaces.
        /// </summary>
        [Fact]
        public void OrtoDatasFromDatabasePostTask_Defaults()
        {
            GISWebAPIManager gISWebAPIManager = new(null);
            OrtoDatasFromDatabasePostTask task = new(gISWebAPIManager);

            Assert.NotNull(task);
            Assert.Equal(5, task.Count);
            Assert.NotNull(task.OrtoDatasBuilding2DOptions);
            Assert.IsAssignableFrom<OrtoDatasPostTask>(task);
            Assert.IsAssignableFrom<Interfaces.IGISWebAPIObject>(task);
        }

        /// <summary>
        /// Verifies that a 404 from the Building2D fetch is treated as an empty batch: the run moves on to the next claim and completes instead of stopping.
        /// <para>A queue entry can outlive its building_2d row, and the Building2D endpoint then answers 404 for the batch. The task used to rethrow that and die on the first such batch, so "Upload OrtoDatas from database" failed after 0 s. With <see cref="OrtoDatasFromDatabasePostTask.MaxConsecutiveFailureCount"/> at 1, counting the 404 as a failure would also stop the run, so the fact covers that too.</para>
        /// </summary>
        [Fact]
        public async Task OrtoDatasFromDatabasePostTask_Building2DNotFound()
        {
            Building2DReference building2DReference = new() { Id = 1, CountyId = 5, Reference = "PL.PZGIK.BDOT10k.BUBDA.0000.0000000" };
            List<Building2DReference> building2DReferences = [building2DReference];
            string json_Claimed = Core.Convert.ToSystem_String(building2DReferences) ?? throw new InvalidOperationException("Building2DReferences could not be serialized");

            ScriptedHttpMessageHandler scriptedHttpMessageHandler = new((path, count) =>
            {
                if (path.EndsWith("/nextbuilding2dreferences", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(count == 1 ? json_Claimed : "[]", Encoding.UTF8, "application/json") };
                }

                if (path.EndsWith("/itemsbybuilding2dreferences", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"title\":\"Not Found\",\"status\":404}", Encoding.UTF8, "application/problem+json") };
                }

                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            ServiceCollection serviceCollection = new();
            serviceCollection.AddHttpClient(Constants.Name.Client.GIS, httpClient => httpClient.BaseAddress = new Uri("https://localhost/")).ConfigurePrimaryHttpMessageHandler(() => scriptedHttpMessageHandler);
            serviceCollection.AddHttpClient(Constants.Name.Client.Geoportal).ConfigurePrimaryHttpMessageHandler(() => scriptedHttpMessageHandler);
            using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            GISWebAPIManager gISWebAPIManager = new(serviceProvider.GetRequiredService<IHttpClientFactory>());
            OrtoDatasFromDatabasePostTaskProbe ortoDatasFromDatabasePostTaskProbe = new(gISWebAPIManager) { MaxConsecutiveFailureCount = 1 };

            bool succeeded = await ortoDatasFromDatabasePostTaskProbe.RunAsync();

            Assert.True(succeeded);
            Assert.Equal(2, scriptedHttpMessageHandler.Count("/nextbuilding2dreferences"));

            // Once under the queued part, once by reference alone - neither finds it.
            Assert.Equal(2, scriptedHttpMessageHandler.Count("/itemsbybuilding2dreferences"));
            Assert.Equal(0, scriptedHttpMessageHandler.Count("/acknowledgebuilding2dreferences"));
        }

        /// <summary>
        /// Verifies that a queue entry whose building has moved to a sibling county part is still downloaded, uploaded naming every part of its county, and acknowledged.
        /// <para>Production held 58 648 entries queued under part 25886 (tomaszowski, 1016) whose buildings are now under its sibling 25887. The Building2D fetch is keyed by part and answered 404 for every one of them, so a run showed "Running (0)" for as long as it took to claim them all, and stored nothing. The task now looks a missing entry up by its reference alone, and uploads with every part sharing the county code: a single part id is written verbatim, which would have filed the orthodata under the part the building is not in.</para>
        /// </summary>
        [Fact]
        public async Task OrtoDatasFromDatabasePostTask_Building2DMovedToSiblingPart()
        {
            const string reference = "2C9F6DBF-A62C-9ED8-E053-CA2BA8C03F9D";

            List<Building2DReference> building2DReferences = [new() { Id = 7261307, CountyId = 25886, Reference = reference }];
            string json_Claimed = Core.Convert.ToSystem_String(building2DReferences) ?? throw new InvalidOperationException("Building2DReferences could not be serialized");

            Geometry.Planar.Classes.PolygonalFace2D polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(new Geometry.Planar.Classes.Polygon2D([new Geometry.Planar.Classes.Point2D(0, 0), new Geometry.Planar.Classes.Point2D(10, 0), new Geometry.Planar.Classes.Point2D(10, 10), new Geometry.Planar.Classes.Point2D(0, 10)]), null) ?? throw new InvalidOperationException("PolygonalFace2D could not be created");
            List<GIS.Classes.Building2D> building2Ds = [new(Guid.NewGuid(), reference, polygonalFace2D, 1, null, null, [])];
            string json_Building2Ds = Core.Convert.ToSystem_String(building2Ds) ?? throw new InvalidOperationException("Building2Ds could not be serialized");

            List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
            [
                new() { Id = 25886, Code = "1016", Name = "tomaszowski", AdministrativeArealType = PostgreSQL.Enums.AdministrativeArealType.County },
                new() { Id = 25887, Code = "1016", Name = "tomaszowski", AdministrativeArealType = PostgreSQL.Enums.AdministrativeArealType.County },
                new() { Id = 5, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = PostgreSQL.Enums.AdministrativeArealType.County }
            ];
            string json_AdministrativeAreal2DReferences = Core.Convert.ToSystem_String(administrativeAreal2DReferences) ?? throw new InvalidOperationException("AdministrativeAreal2DReferences could not be serialized");

            ScriptedHttpMessageHandler scriptedHttpMessageHandler = new((path, count) =>
            {
                if (path.EndsWith("/administrativeareal2dreferencesbyadministrativearealtype", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json_AdministrativeAreal2DReferences, Encoding.UTF8, "application/json") };
                }

                if (path.EndsWith("/nextbuilding2dreferences", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(count == 1 ? json_Claimed : "[]", Encoding.UTF8, "application/json") };
                }

                if (path.EndsWith("/itemsbybuilding2dreferences", StringComparison.OrdinalIgnoreCase))
                {
                    // Not under the queued part; found once asked by reference alone.
                    return count == 1
                        ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"title\":\"Not Found\",\"status\":404}", Encoding.UTF8, "application/problem+json") }
                        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json_Building2Ds, Encoding.UTF8, "application/json") };
                }

                if (path.EndsWith("/updateitemsbycountyids", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/acknowledgebuilding2dreferences", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                // Geoportal imagery: none, which still yields an OrtoDatas for the building.
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            ServiceCollection serviceCollection = new();
            serviceCollection.AddHttpClient(Constants.Name.Client.GIS, httpClient => httpClient.BaseAddress = new Uri("https://localhost/")).ConfigurePrimaryHttpMessageHandler(() => scriptedHttpMessageHandler);
            serviceCollection.AddHttpClient(Constants.Name.Client.Geoportal).ConfigurePrimaryHttpMessageHandler(() => scriptedHttpMessageHandler);
            using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            GISWebAPIManager gISWebAPIManager = new(serviceProvider.GetRequiredService<IHttpClientFactory>());
            OrtoDatasFromDatabasePostTaskProbe ortoDatasFromDatabasePostTaskProbe = new(gISWebAPIManager) { MaxConsecutiveFailureCount = 1 };

            bool succeeded = await ortoDatasFromDatabasePostTaskProbe.RunAsync();

            Assert.True(succeeded);

            List<(string PathAndQuery, string? Body)> requests = [.. scriptedHttpMessageHandler.Requests];
            List<(string PathAndQuery, string? Body)> requests_Building2D = requests.FindAll(x => x.PathAndQuery.Contains("/itemsbybuilding2dreferences", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(2, requests_Building2D.Count);
            Assert.Contains("\"CountyId\":25886", requests_Building2D[0].Body);
            Assert.Contains("\"CountyId\":null", requests_Building2D[1].Body);

            (string PathAndQuery, string? Body) request_Update = Assert.Single(requests, x => x.PathAndQuery.Contains("/updateitemsbycountyids", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("countyids=25886", request_Update.PathAndQuery);
            Assert.Contains("countyids=25887", request_Update.PathAndQuery);
            Assert.DoesNotContain("countyids=5", request_Update.PathAndQuery);
            Assert.Contains(reference, request_Update.Body);

            (string PathAndQuery, string? Body) request_Acknowledge = Assert.Single(requests, x => x.PathAndQuery.Contains("/acknowledgebuilding2dreferences", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("[7261307]", request_Acknowledge.Body);
        }
    }
}
