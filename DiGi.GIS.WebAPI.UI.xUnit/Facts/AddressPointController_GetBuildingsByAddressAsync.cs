using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DiGi.GIS.WebAPI.UI.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the address search relay (#76): the request it sends pins the query names of the upstream contract (<c>text</c>, <c>housenumber</c>, <c>countyid</c>, <c>limit</c>), which has no compile-time link and is ignored silently when misspelled (Coding - WebAPI Contracts, section 1).
        /// <para>It also tests the status contract: a 200 body passes through byte-identical, the upstream 404 is the page's 204, an answered failure is a 502, no answer at all is a 503, and a text that is too short or a limit out of range is a 400.</para>
        /// </summary>
        [Fact]
        public async Task AddressPointController_GetBuildingsByAddressAsync()
        {
            const string matchesBody = "[{\"_type\":\"DiGi.GIS.Classes.AddressPointMatch\",\"Reference\":\"1\"}]";

            // A full request: the exact upstream names, the text trimmed, and the body relayed unchanged.
            StubWebApi stubWebApi_200 = StubWebApi.Answer(HttpStatusCode.OK, matchesBody);
            IActionResult result_200 = await new AddressPointController(new StubHttpClientFactory(stubWebApi_200)).GetBuildingsByAddressAsync(" Marszalkowska ", "12", 3, 5);
            ContentResult contentResult_200 = Assert.IsType<ContentResult>(result_200);
            Assert.Equal(matchesBody, contentResult_200.Content);
            Assert.Equal("application/json", contentResult_200.ContentType);

            HttpRequestMessage? request_200 = stubWebApi_200.LastRequest;
            Assert.NotNull(request_200);
            Assert.Equal(HttpMethod.Get, request_200!.Method);
            Assert.EndsWith("/gis/addresspoint/buildingsbyaddress", request_200.RequestUri!.AbsolutePath);
            Assert.Equal("?text=Marszalkowska&housenumber=12&countyid=3&limit=5", request_200.RequestUri.Query);

            // Optional parameters are left out when not given.
            StubWebApi stubWebApi_Text = StubWebApi.Answer(HttpStatusCode.OK, matchesBody);
            await new AddressPointController(new StubHttpClientFactory(stubWebApi_Text)).GetBuildingsByAddressAsync("Marszalkowska", null, null, null);
            Assert.Equal("?text=Marszalkowska", stubWebApi_Text.LastRequest!.RequestUri!.Query);

            // A text that is missing or too short, and a limit out of range, are the caller's fault.
            AddressPointController addressPointController = new(new StubHttpClientFactory(StubWebApi.Answer(HttpStatusCode.OK, matchesBody)));
            Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync(null, null, null, null));
            Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync("ab", null, null, null));
            Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync("Marszalkowska", null, null, 0));
            Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync("Marszalkowska", null, null, 101));

            // The upstream's empty answer (404) is the page's 204 "no matches".
            IActionResult result_404 = await new AddressPointController(new StubHttpClientFactory(StubWebApi.Answer(HttpStatusCode.NotFound, string.Empty))).GetBuildingsByAddressAsync("Marszalkowska", null, null, null);
            Assert.IsType<NoContentResult>(result_404);

            // An answered failure is a refusal, not an absence.
            IActionResult result_500 = await new AddressPointController(new StubHttpClientFactory(StubWebApi.Answer(HttpStatusCode.InternalServerError, "failed"))).GetBuildingsByAddressAsync("Marszalkowska", null, null, null);
            Assert.Equal(502, Assert.IsType<StatusCodeResult>(result_500).StatusCode);

            // A service that answered nothing at all is a 503.
            IActionResult result_Refused = await new AddressPointController(new StubHttpClientFactory(StubWebApi.Refuse())).GetBuildingsByAddressAsync("Marszalkowska", null, null, null);
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(result_Refused).StatusCode);

            // Exactly one public constructor, or every action 500s at activation (Coding - WebAPI Contracts, section 5).
            Assert.Single(typeof(AddressPointController).GetConstructors());
        }
    }
}
