using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Walks the validation chain of <see cref="OrtoAreaController.RemoveObjectAsync"/> with a valid user token (DiGi.GIS.WebAPI.UI#85): a missing or blank <c>objectid</c> refuses first, then a non-finite or unordered bounding box, then an area that was never initialized.
        /// <para>The converters sit on null ConnectionData, so the area read answers null without a database and the last refusal is the "initialize first" 400. The removal rules themselves - drawer only, refused while another interpreter has labelled the polygon - live in the converter and are pinned against a database by <c>OrtoArea_Object_Remove</c> in DiGi.GIS.PostgreSQL.xUnit.</para>
        /// </summary>
        [Fact]
        public async Task OrtoAreaController_RemoveObject()
        {
            string path = OrtoAreaConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OrtoAreaController controller = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher, supplyToken: true);

                BadRequestObjectResult badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.RemoveObjectAsync(0, 0, 100, 100, null));
                Assert.Contains("objectid", Assert.IsType<string>(badRequestObjectResult.Value));

                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.RemoveObjectAsync(0, 0, 100, 100, " "));
                Assert.Contains("objectid", Assert.IsType<string>(badRequestObjectResult.Value));

                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.RemoveObjectAsync(double.NaN, 0, 100, 100, "object-id"));
                Assert.Contains("finite", Assert.IsType<string>(badRequestObjectResult.Value));

                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.RemoveObjectAsync(100, 0, 0, 100, "object-id"));
                Assert.Contains("finite", Assert.IsType<string>(badRequestObjectResult.Value));

                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.RemoveObjectAsync(0, 0, 100, 100, "object-id"));
                Assert.Contains("initialize", Assert.IsType<string>(badRequestObjectResult.Value));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
