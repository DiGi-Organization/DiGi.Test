using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DiGi.GIS.WebAPI.UI.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the epoch years read behind the page's epoch cards (issue #84): a 200 year array is parsed into the list the cards bind, a 200 whose body is not a year array is a 502 content fault rather than an empty list - which would read on the page as "no epochs cover this area" - and a missing corner is a 400 answered before any upstream request is spent on a zero-area query.
        /// </summary>
        [Fact]
        public async Task OrtoDataController_AreaAvailableYears_ParsesAndGuards()
        {
            const string path = "/gis/ortoarea/availableyearsbyboundingbox";

            // A 200 year array becomes exactly those years.
            RouteStubWebApi routeStubWebApi_Years = new RouteStubWebApi().Answer(path, HttpStatusCode.OK, "[1995,2008]");
            IActionResult result_Years = await InvokeOrtoAreaActionAsync(OrtoAreaAction.AvailableYears, routeStubWebApi_Years, true, null);
            OkObjectResult okObjectResult_Years = Assert.IsType<OkObjectResult>(result_Years);
            List<short> years = Assert.IsType<List<short>>(okObjectResult_Years.Value);
            Assert.Equal(new short[] { 1995, 2008 }, years);

            // A 200 whose body is not a year array is the fault it is: 502, never [].
            RouteStubWebApi routeStubWebApi_Fault = new RouteStubWebApi().Answer(path, HttpStatusCode.OK, "{\"years\":[1995]}");
            IActionResult result_Fault = await InvokeOrtoAreaActionAsync(OrtoAreaAction.AvailableYears, routeStubWebApi_Fault, true, null);
            Assert.Equal(502, Assert.IsType<StatusCodeResult>(result_Fault).StatusCode);

            // A missing corner: 400 before any upstream request - a zero-area query must never be sent.
            RouteStubWebApi routeStubWebApi_Missing = new RouteStubWebApi().Answer(path, HttpStatusCode.OK, "[]");
            OrtoDataController ortoDataController_Missing = CreateOrtoDataController(routeStubWebApi_Missing, true, null);
            IActionResult result_Missing = await ortoDataController_Missing.GetAreaAvailableYearsAsync(null, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2);
            Assert.IsType<BadRequestResult>(result_Missing);
            Assert.True(routeStubWebApi_Missing.LastRequest is null, $"the missing corner must be refused before any upstream request, but the stub saw {routeStubWebApi_Missing.LastRequest?.RequestUri}.");
        }
    }
}
