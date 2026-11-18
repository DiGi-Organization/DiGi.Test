using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingModel = DiGi.Analytical.Building.Classes.BuildingModel;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the deployment check of <see cref="WebAPIBuildingSolarRadiationSolveTask"/> against a scripted GIS Web API (TODO [SolarSolveEndpointsDeployed], DiGi.GIS.PostgreSQL.UI#48): a catalogue without <c>centroidsbycountyid</c>, or one that cannot be read, ends the run after the catalogue request alone; a catalogue listing every route lets the run go on to its first read.
        /// </summary>
        [Fact]
        public async Task WebAPIBuildingSolarRadiationSolveTask_Deployment()
        {
            List<string?> routes =
            [
                DiGi.WebAPI.Query.Path<Building2DController>(nameof(Building2DController.GetCentroidsByCountyIdAsync)),
                DiGi.WebAPI.Query.Path<BuildingSolarRadiationController>(nameof(BuildingSolarRadiationController.GetReferencesByCountyIdAsync)),
                DiGi.WebAPI.Query.Path<BuildingSolarRadiationController>(nameof(BuildingSolarRadiationController.SetSolarRadiationsAsync)),
                DiGi.WebAPI.Query.Path<EPWFileController>(nameof(EPWFileController.GetEPWFileByIdAsync)),
                DiGi.WebAPI.Query.Path<EPWFileController>(nameof(EPWFileController.GetEPWFileLocationsAsync)),
            ];
            Assert.All(routes, x => Assert.False(string.IsNullOrWhiteSpace(x)));

            // The host writes the controller in its own case; the check must not care.
            string json_All = Catalogue(routes.Select(x => x!.Replace("epwfile", "EPWFile", StringComparison.Ordinal)));
            string json_Missing = Catalogue(routes.Skip(1).Select(x => x!));

            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler_Missing = new(_ => Response(HttpStatusCode.OK, json_Missing));
            Assert.False(await Run(solarSolveHttpMessageHandler_Missing));
            Assert.Equal(new string[] { "/information/endpoints" }, solarSolveHttpMessageHandler_Missing.PathAndQueries);

            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler_Unreadable = new(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            Assert.False(await Run(solarSolveHttpMessageHandler_Unreadable));
            Assert.Single(solarSolveHttpMessageHandler_Unreadable.PathAndQueries);

            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler_All = new(pathAndQuery => pathAndQuery.StartsWith("/information/", StringComparison.Ordinal) ? Response(HttpStatusCode.OK, json_All) : new HttpResponseMessage(HttpStatusCode.InternalServerError));
            Assert.False(await Run(solarSolveHttpMessageHandler_All));
            Assert.Contains(solarSolveHttpMessageHandler_All.PathAndQueries, x => x.EndsWith("/epwfile/locations", StringComparison.OrdinalIgnoreCase));

            static string Catalogue(IEnumerable<string> routeTemplates)
            {
                List<EndpointInformation> endpointInformations = [.. routeTemplates.Select(x => new EndpointInformation("X", "X", x, ["GET"], false, null, null))];
                return Core.Convert.ToSystem_String(endpointInformations) ?? throw new InvalidOperationException("The catalogue could not be serialized");
            }

            static async Task<bool> Run(SolarSolveHttpMessageHandler solarSolveHttpMessageHandler)
            {
                (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveManager(solarSolveHttpMessageHandler);
                using (serviceProvider)
                {
                    ProbeWebAPIBuildingSolarRadiationSolveTask probeWebAPIBuildingSolarRadiationSolveTask = new(gISWebAPIManager) { TokenString = "xunit-token" };
                    return await probeWebAPIBuildingSolarRadiationSolveTask.RunAsync();
                }
            }
        }

        /// <summary>
        /// Verifies the reads of <see cref="WebAPIBuildingSolarRadiationSolveTask"/> that have no client helper, against a scripted GIS Web API, under <c>pl-PL</c> so a decimal comma would show (DiGi.GIS.PostgreSQL.UI#48).
        /// <para>The receivers of a batch of 120 long references go in several requests, each with a bounded query and the county part; a 404 is a chunk with no stored model and the models of the other chunks are kept; a 500 on any chunk fails the read (null), never a shorter list. The neighbours are asked for by centre, radius and the server-side tolerance with invariant numbers; a 404 is no neighbour (empty), a 500 is null. The county parts are asked for as County by its integer and every part (<c>uniquecode=false</c>). Without a token the run refuses before reading.</para>
        /// </summary>
        [Fact]
        public async Task WebAPIBuildingSolarRadiationSolveTask_Reads()
        {
            string json_BuildingModels = Core.Convert.ToSystem_String(new List<BuildingModel>() { new() }) ?? throw new InvalidOperationException("The model could not be serialized");
            string json_Counties = Core.Convert.ToSystem_String(new List<AdministrativeAreal2DReference>() { new() { Id = 5, AdministrativeArealType = AdministrativeArealType.County } }) ?? throw new InvalidOperationException("The counties could not be serialized");

            List<string> references = [.. Enumerable.Range(0, 120).Select(x => string.Format(CultureInfo.InvariantCulture, "PL.PZGiK.994.BDOT10k.BUBD_{0:D20}", x))];

            Func<string, HttpResponseMessage> response = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler = new(x => response(x));
            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveManager(solarSolveHttpMessageHandler);

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
                using (serviceProvider)
                {
                    ProbeWebAPIBuildingSolarRadiationSolveTask probeWebAPIBuildingSolarRadiationSolveTask = new(gISWebAPIManager);

                    // Receivers: the first chunk holds one model, every other chunk none.
                    int count = 0;
                    response = _ => ++count == 1 ? Response(HttpStatusCode.OK, json_BuildingModels) : new HttpResponseMessage(HttpStatusCode.NotFound);
                    List<BuildingModel?>? buildingModels = await probeWebAPIBuildingSolarRadiationSolveTask.ReadBuildingModelsAsync(5, references);
                    Assert.NotNull(buildingModels);
                    Assert.Single(buildingModels);

                    List<string> pathAndQueries = [.. solarSolveHttpMessageHandler.PathAndQueries];
                    Assert.True(pathAndQueries.Count > 1);
                    Assert.All(pathAndQueries, x => Assert.Contains("/itemsbyreferences?", x, StringComparison.OrdinalIgnoreCase));
                    Assert.All(pathAndQueries, x => Assert.EndsWith("&countyid=5", x, StringComparison.Ordinal));
                    Assert.All(pathAndQueries, x => Assert.True(x.Length < 2500, x.Length.ToString(CultureInfo.InvariantCulture)));
                    Assert.Equal(references.Count, pathAndQueries.Sum(x => x.Split('&').Count(y => y.StartsWith("references=", StringComparison.Ordinal) || y.Contains("?references=", StringComparison.Ordinal))));

                    count = 0;
                    response = _ => ++count == 2 ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Response(HttpStatusCode.OK, json_BuildingModels);
                    Assert.Null(await probeWebAPIBuildingSolarRadiationSolveTask.ReadBuildingModelsAsync(5, references));

                    // Neighbours.
                    solarSolveHttpMessageHandler.Clear();
                    response = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
                    List<BuildingModel?>? neighbours = await probeWebAPIBuildingSolarRadiationSolveTask.ReadNeighbourBuildingModelsAsync(new Circle2D(new Point2D(638000.5, 486000.25), 120.5));
                    Assert.NotNull(neighbours);
                    Assert.Empty(neighbours);

                    string pathAndQuery_Circle = Assert.Single(solarSolveHttpMessageHandler.PathAndQueries);
                    Assert.Contains("/itemsbycircle?", pathAndQuery_Circle, StringComparison.OrdinalIgnoreCase);
                    Assert.Contains("x=638000.5", pathAndQuery_Circle, StringComparison.Ordinal);
                    Assert.Contains("y=486000.25", pathAndQuery_Circle, StringComparison.Ordinal);
                    Assert.Contains("radius=120.5", pathAndQuery_Circle, StringComparison.Ordinal);
                    Assert.Contains("tolerance=" + Core.Constants.Tolerance.Distance.ToString(CultureInfo.InvariantCulture), pathAndQuery_Circle, StringComparison.Ordinal);

                    response = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    Assert.Null(await probeWebAPIBuildingSolarRadiationSolveTask.ReadNeighbourBuildingModelsAsync(new Circle2D(new Point2D(0, 0), 10)));

                    // County parts.
                    solarSolveHttpMessageHandler.Clear();
                    response = _ => Response(HttpStatusCode.OK, json_Counties);
                    List<AdministrativeAreal2DReference>? administrativeAreal2DReferences = await probeWebAPIBuildingSolarRadiationSolveTask.ReadCountyReferencesAsync();
                    Assert.NotNull(administrativeAreal2DReferences);
                    Assert.Equal(5, Assert.Single(administrativeAreal2DReferences).Id);

                    string pathAndQuery_Counties = Assert.Single(solarSolveHttpMessageHandler.PathAndQueries);
                    Assert.Contains("administrativearealtype=" + ((int)AdministrativeArealType.County).ToString(CultureInfo.InvariantCulture), pathAndQuery_Counties, StringComparison.Ordinal);
                    Assert.Contains("uniquecode=false", pathAndQuery_Counties, StringComparison.Ordinal);

                    // No token: the run refuses at TryInitialize, after the deployment check and before any read.
                    Assert.False(probeWebAPIBuildingSolarRadiationSolveTask.IsInitialized());
                    probeWebAPIBuildingSolarRadiationSolveTask.TokenString = "xunit-token";
                    Assert.True(probeWebAPIBuildingSolarRadiationSolveTask.IsInitialized());
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }

        private static HttpResponseMessage Response(HttpStatusCode httpStatusCode, string content)
        {
            return new HttpResponseMessage(httpStatusCode) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
        }

        private static (GISWebAPIManager GISWebAPIManager, ServiceProvider ServiceProvider) SolarSolveManager(SolarSolveHttpMessageHandler solarSolveHttpMessageHandler)
        {
            ServiceCollection serviceCollection = new();
            serviceCollection.AddHttpClient(WebAPI.Constants.Name.Client.GIS, httpClient => httpClient.BaseAddress = new Uri("https://localhost/")).ConfigurePrimaryHttpMessageHandler(() => solarSolveHttpMessageHandler);
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            return (new GISWebAPIManager(serviceProvider.GetRequiredService<IHttpClientFactory>()), serviceProvider);
        }

        /// <summary>
        /// Exposes the protected reads of <see cref="WebAPIBuildingSolarRadiationSolveTask"/> and its run to the facts.
        /// </summary>
        private sealed class ProbeWebAPIBuildingSolarRadiationSolveTask : WebAPIBuildingSolarRadiationSolveTask
        {
            public ProbeWebAPIBuildingSolarRadiationSolveTask(GISWebAPIManager gISWebAPIManager)
                : base(gISWebAPIManager)
            {
            }

            public bool IsInitialized()
            {
                return TryInitialize();
            }

            public Task<List<BuildingModel?>?> ReadBuildingModelsAsync(int countyId, List<string> references)
            {
                return ReadBuildingModelsAsync(countyId, references, 30, CancellationToken.None);
            }

            public Task<List<AdministrativeAreal2DReference>?> ReadCountyReferencesAsync()
            {
                return ReadCountyReferencesAsync(30, CancellationToken.None);
            }

            public Task<List<BuildingModel?>?> ReadNeighbourBuildingModelsAsync(Circle2D circle2D)
            {
                return ReadNeighbourBuildingModelsAsync(circle2D, 30, CancellationToken.None);
            }

            public Task<bool> RunAsync()
            {
                return ExecuteAsync(new Progress<long>(), CancellationToken.None);
            }
        }

        /// <summary>
        /// Answers every request from a script of its path and query, and records them in order.
        /// </summary>
        private sealed class SolarSolveHttpMessageHandler : HttpMessageHandler
        {
            private readonly ConcurrentQueue<string> pathAndQueries = new();
            private readonly Func<string, HttpResponseMessage> response;

            public SolarSolveHttpMessageHandler(Func<string, HttpResponseMessage> response)
            {
                this.response = response;
            }

            public List<string> PathAndQueries => [.. pathAndQueries];

            public void Clear()
            {
                pathAndQueries.Clear();
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string pathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
                pathAndQueries.Enqueue(pathAndQuery);
                return Task.FromResult(response(pathAndQuery));
            }
        }
    }
}
