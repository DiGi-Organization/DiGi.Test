using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the epoch image relay (issue #84): a 200 JPEG body is served as the page's <c>image/jpeg</c> file with its bytes unchanged, the upstream 404 - an epoch the negative cache holds no imagery for - is the page's 404 and stays distinct from a service that answered nothing (503), and a 200 that carried no bytes is a 502 content fault rather than a missing epoch.
        /// </summary>
        [Fact]
        public async Task OrtoDataController_AreaImage_Relay()
        {
            const string path = "/gis/ortoarea/imagebyboundingbox";
            string imageBody = "\u00FF\u00FE JPEG \u00E9\u00E8";

            // A 200 body: the bytes the upstream sent, as the page's image/jpeg.
            RouteStubWebApi routeStubWebApi_Image = new RouteStubWebApi().Answer(path, HttpStatusCode.OK, imageBody);
            IActionResult result_Image = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Image, routeStubWebApi_Image, true, null);
            FileContentResult fileContentResult = Assert.IsType<FileContentResult>(result_Image);
            Assert.Equal("image/jpeg", fileContentResult.ContentType);
            Assert.Equal(Encoding.UTF8.GetBytes(imageBody), fileContentResult.FileContents);

            // The upstream 404: no imagery for the epoch - the badge, not an error, and not this service's 503.
            RouteStubWebApi routeStubWebApi_404 = new RouteStubWebApi().Answer(path, HttpStatusCode.NotFound, string.Empty);
            IActionResult result_404 = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Image, routeStubWebApi_404, true, null);
            Assert.IsType<NotFoundResult>(result_404);

            // A 200 that carried no bytes: a content fault of the relay (502), kept distinct from the 404 above.
            RouteStubWebApi routeStubWebApi_Empty = new RouteStubWebApi().Answer(path, HttpStatusCode.OK, string.Empty);
            IActionResult result_Empty = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Image, routeStubWebApi_Empty, true, null);
            Assert.Equal(502, Assert.IsType<StatusCodeResult>(result_Empty).StatusCode);

            // A service that answered nothing at all: 503, never a 404.
            RouteStubWebApi routeStubWebApi_Refused = new RouteStubWebApi().Refuse(path);
            IActionResult result_Refused = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Image, routeStubWebApi_Refused, true, null);
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(result_Refused).StatusCode);
        }
    }
}
