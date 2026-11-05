using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the order observed in <c>objects</c> when the area was never initialized: the area read happens before the upsert, and a converter on null ConnectionData answers null, so a valid body receives the message-bearing 400 telling the caller to initialize the area first - the route never reaches (and therefore never answers) its store-failure 500 without a stored area row.
        /// <para>This is the contract as the code genuinely behaves rather than the generic "0/null rows are 500" one: with no database the area-exists check is the last gate before the write, so 400-with-the-initialize message is what a caller of an uninitialized area observes, and the 500 branch stays reachable only when an area row exists and the object upsert itself fails - which this suite cannot stage without a live database (recorded as a coverage gap).</para>
        /// </summary>
        [Fact]
        public async Task OrtoAreaController_CreateObject_UnknownArea_Answers400()
        {
            string path = OrtoAreaConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OrtoAreaController controller = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher, supplyToken: true);

                Classes.Parameter.OrtoAreaObjectParameter ortoAreaObjectParameter = new()
                {
                    X_1 = 0,
                    Y_1 = 0,
                    X_2 = 100,
                    Y_2 = 100,
                    Polygon = OrtoAreaPolygonJson([0, 0], [10, 0], [10, 10], [0, 10])
                };

                IActionResult actionResult = await controller.CreateObjectAsync(ortoAreaObjectParameter);

                BadRequestObjectResult badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(actionResult);
                Assert.Contains("initialize it first", Assert.IsType<string>(badRequestObjectResult.Value));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
