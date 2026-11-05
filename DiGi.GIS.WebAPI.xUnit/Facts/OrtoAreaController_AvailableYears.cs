using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the documented null-to-empty contract of <c>availableyearsbyboundingbox</c>: with the session and bounding box accepted but the sheet read unable to answer (a converter on null ConnectionData returns null), the route responds 200 with an empty epoch list - a missing result is no coverage, not a failure.
        /// <para>Anything but a 200 <see cref="OkObjectResult"/> carrying a <c>List&lt;short&gt;</c> here would mean a null sheet read leaked into a 400/404/500, or that the empty case changed shape and the relay would stop parsing the epoch picker.</para>
        /// </summary>
        [Fact]
        public async Task OrtoAreaController_AvailableYears()
        {
            string path = OrtoAreaConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OrtoAreaController controller = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher, supplyToken: true);

                IActionResult actionResult = await controller.AvailableYearsByBoundingBoxAsync(0, 0, 100, 100);

                OkObjectResult okObjectResult = Assert.IsType<OkObjectResult>(actionResult);
                Assert.Equal(200, okObjectResult.StatusCode);

                List<short> years = Assert.IsType<List<short>>(okObjectResult.Value);
                Assert.Empty(years);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
