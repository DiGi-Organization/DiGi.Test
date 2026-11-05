using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the shared Orto Data stub harness the area-relay facts are built on (issue #84): the one-argument controller factory - the contract the pre-existing <c>GetBuildingAsync</c> facts call unchanged since the helpers were promoted into a shared partial file - and the parameterized factory answer alike, the signed-in context reaches the stub, and the context without a session is refused 401 before the stub sees anything.
        /// </summary>
        [Fact]
        public async Task OrtoDataStub_Shared()
        {
            const string path = "/gis/ortoarea/areabyboundingbox";

            // The legacy factory: still the session-gated controller over the shared per-path stub.
            RouteStubWebApi routeStubWebApi_Legacy = new RouteStubWebApi().Answer(path, HttpStatusCode.OK, "[{}]");
            IActionResult result_Legacy = await CreateOrtoDataController(routeStubWebApi_Legacy).GetAreaItemsAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2);
            OkObjectResult okObjectResult_Legacy = Assert.IsType<OkObjectResult>(result_Legacy);
            Assert.Equal("[{}]", okObjectResult_Legacy.Value);
            Assert.NotNull(routeStubWebApi_Legacy.LastRequest);

            // The parameterized factory, signed in: the same answer through the shared invoker.
            RouteStubWebApi routeStubWebApi_SignedIn = new RouteStubWebApi().Answer(path, HttpStatusCode.OK, "[{}]");
            IActionResult result_SignedIn = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Items, routeStubWebApi_SignedIn, true, null);
            Assert.IsType<OkObjectResult>(result_SignedIn);
            Assert.NotNull(routeStubWebApi_SignedIn.LastRequest);

            // The parameterized factory, anonymous: 401 before the stub sees anything.
            RouteStubWebApi routeStubWebApi_Anonymous = new RouteStubWebApi().Answer(path, HttpStatusCode.OK, "[{}]");
            IActionResult result_Anonymous = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Items, routeStubWebApi_Anonymous, false, null);
            Assert.IsType<UnauthorizedResult>(result_Anonymous);
            Assert.Null(routeStubWebApi_Anonymous.LastRequest);
        }
    }
}
