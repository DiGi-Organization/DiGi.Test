using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the status contract of all seven area-mode relay actions of the Orto Data controller (issue #84): a visitor without the session cookie gets 401 before any upstream request is made, a service that answered nothing at all is a 503, and an answered fault is mirrored as the status the service answered with.
        /// <para>All three cases are walked over every action - the five GET relays, the epoch image relay and the two POST relays - because the contract is per action: a gate reordered on one of them (cookie after the guard, guard after the upstream call) would fail only on that one.</para>
        /// </summary>
        [Fact]
        public async Task OrtoDataController_AreaRelays_StatusContracts()
        {
            OrtoAreaAction[] areaActions =
            [
                OrtoAreaAction.AvailableYears,
                OrtoAreaAction.Image,
                OrtoAreaAction.Items,
                OrtoAreaAction.Buildings,
                OrtoAreaAction.Labels,
                OrtoAreaAction.Object,
                OrtoAreaAction.Label,
            ];

            foreach (OrtoAreaAction areaAction in areaActions)
            {
                // No cookie: 401, answered before a single upstream request is spent (the stub records none).
                RouteStubWebApi routeStubWebApi_NoCookie = new RouteStubWebApi().Answer(OrtoAreaAction_Path(areaAction), HttpStatusCode.OK, "[]");
                IActionResult result_NoCookie = await InvokeOrtoAreaActionAsync(areaAction, routeStubWebApi_NoCookie, false, OrtoAreaPostBody);
                Assert.True(result_NoCookie is UnauthorizedResult, $"[{areaAction}] expected 401 without the session cookie, got {result_NoCookie?.GetType().Name ?? "null"}.");
                Assert.True(routeStubWebApi_NoCookie.LastRequest is null, $"[{areaAction}] the 401 must be answered before any upstream request, but the stub saw {routeStubWebApi_NoCookie.LastRequest?.RequestUri}.");

                // A service that answered nothing at all: 503, never an absence or an empty answer.
                RouteStubWebApi routeStubWebApi_Unreachable = new RouteStubWebApi().Refuse(OrtoAreaAction_Path(areaAction));
                IActionResult result_Unreachable = await InvokeOrtoAreaActionAsync(areaAction, routeStubWebApi_Unreachable, true, OrtoAreaPostBody);
                StatusCodeResult statusCodeResult_Unreachable = Assert.IsType<StatusCodeResult>(result_Unreachable);
                Assert.True(statusCodeResult_Unreachable.StatusCode == 503, $"[{areaAction}] expected 503 when the service answers nothing, got {statusCodeResult_Unreachable.StatusCode}.");

                // An answered fault is mirrored as it was answered, so the page can name it.
                RouteStubWebApi routeStubWebApi_500 = new RouteStubWebApi().Answer(OrtoAreaAction_Path(areaAction), HttpStatusCode.InternalServerError, "the service failed");
                IActionResult result_500 = await InvokeOrtoAreaActionAsync(areaAction, routeStubWebApi_500, true, OrtoAreaPostBody);
                StatusCodeResult statusCodeResult_500 = Assert.IsType<StatusCodeResult>(result_500);
                Assert.True(statusCodeResult_500.StatusCode == 500, $"[{areaAction}] expected the mirrored 500, got {statusCodeResult_500.StatusCode}.");
            }
        }
    }
}
