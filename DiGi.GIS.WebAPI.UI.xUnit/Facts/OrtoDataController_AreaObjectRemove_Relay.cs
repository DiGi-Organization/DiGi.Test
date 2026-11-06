using DiGi.GIS.WebAPI.UI.Controllers;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the relay of the drawn-polygon removal (DiGi.GIS.WebAPI.UI#85): <c>DELETE area/object</c> reaches <c>DELETE /gis/ortoarea/objects</c> with exactly the hand-matched query names - the four corners formatted invariantly, then <c>objectid</c> - and the session cookie as <c>Bearer test-token</c>, and it mirrors the service's status, so 403 (not the drawer) and 409 (labelled by another interpreter) reach the page as they are.
        /// <para>Without a session it answers 401, and without an identifier 400, both before any upstream request. The assertions run under pl-PL, whose decimal separator is the comma, so a culture-formatted coordinate would fail the query comparison.</para>
        /// </summary>
        [Fact]
        public async Task OrtoDataController_AreaObjectRemove_Relay()
        {
            const string objectId = "0c7f2d1e-9b9a-4d5e-8f61-3a2b1c0d9e8f";

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

                foreach (HttpStatusCode httpStatusCode in new[] { HttpStatusCode.OK, HttpStatusCode.Forbidden, HttpStatusCode.Conflict, HttpStatusCode.NotFound })
                {
                    RouteStubWebApi routeStubWebApi = new RouteStubWebApi().Answer("/gis/ortoarea/objects", httpStatusCode, string.Empty);
                    OrtoDataController ortoDataController = CreateOrtoDataController(routeStubWebApi, true, null);

                    IActionResult actionResult = await ortoDataController.RemoveAreaObjectAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2, objectId);
                    Assert.Equal((int)httpStatusCode, Assert.IsType<StatusCodeResult>(actionResult).StatusCode);

                    HttpRequestMessage? request = routeStubWebApi.LastRequest;
                    Assert.NotNull(request);
                    Assert.Equal(HttpMethod.Delete, request.Method);
                    Assert.Equal("/gis/ortoarea/objects", request.RequestUri!.AbsolutePath);
                    Assert.Equal(OrtoAreaExpectedQuery($"&objectid={objectId}"), request.RequestUri.Query);
                    Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
                }

                // No session: 401 before any upstream request.
                RouteStubWebApi routeStubWebApi_Anonymous = new RouteStubWebApi().Answer("/gis/ortoarea/objects", HttpStatusCode.OK, string.Empty);
                Assert.IsType<UnauthorizedResult>(await CreateOrtoDataController(routeStubWebApi_Anonymous, false, null).RemoveAreaObjectAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2, objectId));
                Assert.Null(routeStubWebApi_Anonymous.LastRequest);

                // No identifier or a missing corner: 400 before any upstream request.
                RouteStubWebApi routeStubWebApi_Invalid = new RouteStubWebApi().Answer("/gis/ortoarea/objects", HttpStatusCode.OK, string.Empty);
                Assert.IsType<BadRequestResult>(await CreateOrtoDataController(routeStubWebApi_Invalid, true, null).RemoveAreaObjectAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2, " "));
                Assert.IsType<BadRequestResult>(await CreateOrtoDataController(routeStubWebApi_Invalid, true, null).RemoveAreaObjectAsync(OrtoAreaX1, null, OrtoAreaX2, OrtoAreaY2, objectId));
                Assert.Null(routeStubWebApi_Invalid.LastRequest);
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }
    }
}
