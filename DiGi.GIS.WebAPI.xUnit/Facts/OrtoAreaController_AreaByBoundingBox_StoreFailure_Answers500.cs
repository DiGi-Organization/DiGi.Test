using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the store-failure contract of <c>areabyboundingbox</c>: with a valid token and bounding box, the never-stored area reaches the create path, its upsert answers null (a converter on null ConnectionData cannot store), and the route answers 500 with the creation message - never a quiet 200 carrying an area that was not persisted.
        /// <para>A 200 here would hand the caller sampled cells that no other session would ever read back, breaking the create-or-get idempotency the endpoint promises; the observed shape is the plain <see cref="ObjectResult"/> at 500 that the store-failure branch returns.</para>
        /// </summary>
        [Fact]
        public async Task OrtoAreaController_AreaByBoundingBox_StoreFailure_Answers500()
        {
            string path = OrtoAreaConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OrtoAreaController controller = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher, supplyToken: true);

                IActionResult actionResult = await controller.AreaByBoundingBoxAsync(0, 0, 100, 100);

                ObjectResult objectResult = Assert.IsType<ObjectResult>(actionResult);
                Assert.Equal(500, objectResult.StatusCode);
                Assert.Contains("could not be created", Assert.IsType<string>(objectResult.Value));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
