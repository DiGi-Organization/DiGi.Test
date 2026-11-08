using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.WebAPI.UI.Controllers;
using DiGi.GIS.WebAPI.UI.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <c>GET /typology/buildingcount</c>, the solve's pre-flight count the area view shows while it waits (DiGi.GIS.WebAPI.UI#51).
        /// <para>A county sums <c>countbycountyid</c> over every part <c>idsbycode</c> names. A municipality counts its county's parts and is flagged as clipped. The country is not counted. An area naming no parts counts 0. A part count that cannot be read is a 503, as is a failed part resolution. A missing identifier or type, or a missing code for any area but the country, is a 400.</para>
        /// </summary>
        [Fact]
        public async Task TypologyController_GetBuildingCountAsync()
        {
            // A county of two parts, 1 234 buildings each.
            ScriptedWebApi scriptedWebApi = new((request, body) => Answer(request.RequestUri!.AbsolutePath.EndsWith("/idsbycode", StringComparison.CurrentCulture) ? "[78238,78244]" : "1234"));
            TypologyAreaCountViewModel typologyAreaCountViewModel = Count(await Controller(scriptedWebApi).GetBuildingCountAsync(78238, "2412", AdministrativeArealType.County));
            Assert.Equal(2468, typologyAreaCountViewModel.Count);
            Assert.Equal(2, typologyAreaCountViewModel.CountyPartCount);
            Assert.Equal(Constants.Default.BuildingSolveCeiling, typologyAreaCountViewModel.Ceiling);
            Assert.False(typologyAreaCountViewModel.Clipped);
            Assert.Equal(2, scriptedWebApi.Requests.Count(x => x.RequestUri!.AbsolutePath.EndsWith("/countbycountyid", StringComparison.CurrentCulture)));

            // A municipality reads its county's parts: the count is an upper bound, flagged as clipped.
            TypologyAreaCountViewModel typologyAreaCountViewModel_Municipality = Count(await Controller(new ScriptedWebApi((request, body) => Answer(request.RequestUri!.AbsolutePath.EndsWith("/idsbycode", StringComparison.CurrentCulture) ? "[78238]" : "500"))).GetBuildingCountAsync(78241, "2412022", AdministrativeArealType.Municipality));
            Assert.Equal(500, typologyAreaCountViewModel_Municipality.Count);
            Assert.True(typologyAreaCountViewModel_Municipality.Clipped);

            // The country is not counted, and nothing is requested.
            ScriptedWebApi scriptedWebApi_Country = new((request, body) => Answer("[]"));
            TypologyAreaCountViewModel typologyAreaCountViewModel_Country = Count(await Controller(scriptedWebApi_Country).GetBuildingCountAsync(1, "10", AdministrativeArealType.Country));
            Assert.Null(typologyAreaCountViewModel_Country.Count);
            Assert.Empty(scriptedWebApi_Country.Requests);

            // An area naming no parts (the upstream's 404) counts 0.
            TypologyAreaCountViewModel typologyAreaCountViewModel_None = Count(await Controller(new ScriptedWebApi((request, body) => new HttpResponseMessage(HttpStatusCode.NotFound))).GetBuildingCountAsync(5, "9999", AdministrativeArealType.County));
            Assert.Equal(0, typologyAreaCountViewModel_None.Count);
            Assert.Equal(0, typologyAreaCountViewModel_None.CountyPartCount);

            // A part count that cannot be read, or a failed resolution, is a 503.
            IActionResult actionResult_CountFailed = await Controller(new ScriptedWebApi((request, body) => request.RequestUri!.AbsolutePath.EndsWith("/idsbycode", StringComparison.CurrentCulture) ? Answer("[78238]") : new HttpResponseMessage(HttpStatusCode.InternalServerError))).GetBuildingCountAsync(78238, "2412", AdministrativeArealType.County);
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(actionResult_CountFailed).StatusCode);

            IActionResult actionResult_PartsFailed = await Controller(new ScriptedWebApi((request, body) => new HttpResponseMessage(HttpStatusCode.InternalServerError))).GetBuildingCountAsync(78238, "2412", AdministrativeArealType.County);
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(actionResult_PartsFailed).StatusCode);

            // A missing identifier, type or code is a 400.
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetBuildingCountAsync(0, "2412", AdministrativeArealType.County));
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetBuildingCountAsync(78238, "2412", null));
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetBuildingCountAsync(78238, "2412", AdministrativeArealType.Undefined));
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetBuildingCountAsync(78238, " ", AdministrativeArealType.County));
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetBuildingCountAsync(78238, null, AdministrativeArealType.Municipality));

            static TypologyAreaCountViewModel Count(IActionResult actionResult)
            {
                return Assert.IsType<TypologyAreaCountViewModel>(Assert.IsType<OkObjectResult>(actionResult).Value);
            }
        }

        // A 200 answer with a body, as the GIS Web API answers.
        private static HttpResponseMessage Answer(string body)
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }

        // The controller under test on a scripted GIS Web API, wired as Program.cs wires it.
        private static TypologyController Controller(ScriptedWebApi scriptedWebApi)
        {
            return new TypologyController(new ScriptedHttpClientFactory(scriptedWebApi), NullLogger<TypologyController>.Instance);
        }

        // The IHttpClientFactory for a ScriptedWebApi: every CreateClient answers a client on the script.
        private sealed class ScriptedHttpClientFactory : IHttpClientFactory
        {
            private readonly ScriptedWebApi scriptedWebApi;

            public ScriptedHttpClientFactory(ScriptedWebApi scriptedWebApi)
            {
                this.scriptedWebApi = scriptedWebApi;
            }

            public HttpClient CreateClient(string name)
            {
                return new HttpClient(this.scriptedWebApi, false);
            }
        }
    }
}
