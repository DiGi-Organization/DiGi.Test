using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the label write's feature gate: with a valid token and <c>AllowUpdateOrtoAreaLabel=false</c>, <c>POST label</c> answers the gate's bare 400 (a message-less <see cref="BadRequestResult"/>), while <c>POST objects</c> under the same configuration runs straight past - there is no flag on it - to its own body validation and area check.
        /// <para>The distinction is carried by the result type, not only the status: the gate fires before body validation, so even a fully valid label body still answers the bare <see cref="BadRequestResult"/> a missing body would also produce - and if the flag were checked later, the valid body would instead reach the area read and answer a message-bearing 400. The objects route, which has no flag, turns the same null body into the message-bearing "Body is missing." 400, proving only the label write is gated. A configuration file without the key at all denies too, matching the property's documented missing-key-denies contract.</para>
        /// </summary>
        [Fact]
        public async Task OrtoAreaController_Gate()
        {
            string path = OrtoAreaConfigurationFilePath(allowUpdateOrtoAreaLabel: false);

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OrtoAreaController controller = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher, supplyToken: true);

                // A fully valid label body still answers the message-less gate 400: the flag fires before body
                // validation and before any store read.
                Classes.Parameter.OrtoAreaLabelParameter ortoAreaLabelParameter = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, EpochYear = 2024, State = 0, ObjectId = "object-id" };
                Assert.IsType<BadRequestResult>(await controller.SetLabelAsync(ortoAreaLabelParameter));

                // Null body with the flag off is still the bare gate 400 - a body-missing refusal would have been
                // the message-bearing BadRequestObjectResult instead.
                Assert.IsType<BadRequestResult>(await controller.SetLabelAsync(null));

                // objects carries no flag under the very same configuration: the null body reaches the body check
                // and answers its message, which the gate could never produce.
                BadRequestObjectResult badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.CreateObjectAsync(null));
                Assert.Contains("Body is missing", Assert.IsType<string>(badRequestObjectResult.Value));

                // And with a full body it runs past every gate down to the area read, answering "initialize first".
                Classes.Parameter.OrtoAreaObjectParameter ortoAreaObjectParameter = new() { X_1 = 0, Y_1 = 0, X_2 = 100, Y_2 = 100, Polygon = OrtoAreaPolygonJson([0, 0], [10, 0], [10, 10], [0, 10]) };
                badRequestObjectResult = Assert.IsType<BadRequestObjectResult>(await controller.CreateObjectAsync(ortoAreaObjectParameter));
                Assert.Contains("initialize it first", Assert.IsType<string>(badRequestObjectResult.Value));

                // A configuration file without the AllowUpdateOrtoAreaLabel line at all denies as well - the flag's
                // documented missing-key-denies contract, pinned against the shared project configuration.
                string path_MissingKey = ConfigurationFilePath();

                try
                {
                    using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher_MissingKey = new(path_MissingKey);
                    OrtoAreaController controller_MissingKey = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher_MissingKey, supplyToken: true);

                    Assert.IsType<BadRequestResult>(await controller_MissingKey.SetLabelAsync(ortoAreaLabelParameter));
                }
                finally
                {
                    System.IO.File.Delete(path_MissingKey);
                }
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
