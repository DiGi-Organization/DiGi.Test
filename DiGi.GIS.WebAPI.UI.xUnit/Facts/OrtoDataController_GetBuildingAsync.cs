using System.Net;
using System.Threading.Tasks;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.UI.Classes;
using DiGi.GIS.WebAPI.UI.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the status contract of the Orto Data direct-mode building read (#48): an unknown reference stays the 404 the page reads as "Building not found.", a service that answered nothing at all is a 503, and an answered fault is the status the service answered with.
        /// <para>Before the fix every one of those collapsed through <c>Building2DReferenceAsync</c> into a 404, so an outage was reported to the reviewer as a building that does not exist.</para>
        /// </summary>
        [Fact]
        public async Task GetBuildingAsync()
        {
            // The upstream's "no such building" is the page's 404, with the wording that names the absence.
            RouteStubWebApi routeStubWebApi_404 = new RouteStubWebApi().Answer("building2Dreferencebyreference", HttpStatusCode.NotFound, string.Empty);
            IActionResult result_404 = await CreateOrtoDataController(routeStubWebApi_404).GetBuildingAsync(1465, "unknown");
            Assert.IsType<NotFoundResult>(result_404);

            // A service that answered nothing at all is a 503, never a 404 - the reported symptom.
            IActionResult result_Unreachable = await CreateOrtoDataController(new RouteStubWebApi().Refuse("building2Dreferencebyreference")).GetBuildingAsync(1465, "3020");
            StatusCodeResult statusCodeResult_Unreachable = Assert.IsType<StatusCodeResult>(result_Unreachable);
            Assert.Equal(503, statusCodeResult_Unreachable.StatusCode);

            // An answered fault is mirrored, so the page can name it rather than read it as an absence.
            RouteStubWebApi routeStubWebApi_500 = new RouteStubWebApi().Answer("building2Dreferencebyreference", HttpStatusCode.InternalServerError, "the service failed");
            IActionResult result_500 = await CreateOrtoDataController(routeStubWebApi_500).GetBuildingAsync(1465, "3020");
            StatusCodeResult statusCodeResult_500 = Assert.IsType<StatusCodeResult>(result_500);
            Assert.Equal(500, statusCodeResult_500.StatusCode);

            // A known reference is read and composed with its photo years and any recorded answer.
            Building2DReference building2DReference = new() { Id = 123, SubdivisionId = 5, CountyId = 1465, Reference = "3020" };
            string buildingBody = Core.Convert.ToSystem_String(building2DReference) ?? string.Empty;
            RouteStubWebApi routeStubWebApi_200 = new RouteStubWebApi()
                .Answer("building2Dreferencebyreference", HttpStatusCode.OK, buildingBody)
                .Answer("yearsbyreference", HttpStatusCode.OK, "[1990,2005]");
            IActionResult result_200 = await CreateOrtoDataController(routeStubWebApi_200).GetBuildingAsync(1465, "3020");
            OkObjectResult okObjectResult_200 = Assert.IsType<OkObjectResult>(result_200);
            OrtoDataBuildingResponse ortoDataBuildingResponse_200 = Assert.IsType<OrtoDataBuildingResponse>(okObjectResult_200.Value);
            Assert.Equal(1465, ortoDataBuildingResponse_200.CountyId);
            Assert.Equal("3020", ortoDataBuildingResponse_200.Reference);
            Assert.NotNull(ortoDataBuildingResponse_200.Years);
            Assert.Equal(2, ortoDataBuildingResponse_200.Years.Count);
        }

        /// <summary>
        /// Tests that the building details panel still renders without its context record when the reference read fails (#48): the refusal is not this application's error, and the panel's subject - the year built data - is untouched by it.
        /// </summary>
        [Fact]
        public async Task GetBuildingAsync_PanelWithoutReferenceRecord()
        {
            DiGi.GIS.Classes.YearBuiltData yearBuiltData = new("3020");
            string yearBuiltDataBody = Core.Convert.ToSystem_String(yearBuiltData) ?? string.Empty;

            RouteStubWebApi routeStubWebApi = new RouteStubWebApi()
                .Answer("yearbuiltdata/itemsbyreference", HttpStatusCode.OK, yearBuiltDataBody)
                .Refuse("building2Dreferencebyreference");

            YearBuiltDataController controller = new(new HandlerHttpClientFactory(routeStubWebApi));
            IActionResult result = await controller.GetItemByReferenceAsync("3020", 1465);

            Assert.IsType<PartialViewResult>(result);
        }
    }
}
