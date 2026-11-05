using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that every one of the six <c>gis/ortoarea</c> actions answers 401 when no valid user token is supplied, before any body, bounding-box or store check runs.
        /// <para>The security key manager and the revocation store are present in all three variants - no header, an absent header with a body that would otherwise 400, and a malformed bearer value - so the refusal is pinned to the missing session itself rather than to a missing user extension. The two POST actions deny with a null body, proving the session gate runs before body validation; the image action denies with a valid bounding box, so its answer can never come from the cache or the WMS fetch.</para>
        /// </summary>
        [Fact]
        public async Task OrtoAreaController_Auth()
        {
            string path = OrtoAreaConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);

                OrtoAreaController controller = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher, supplyToken: false);

                Assert.IsType<UnauthorizedResult>(await controller.AvailableYearsByBoundingBoxAsync(0, 0, 100, 100));
                Assert.IsType<UnauthorizedResult>(await controller.ImageByBoundingBoxAsync(0, 0, 100, 100, 2020));
                Assert.IsType<UnauthorizedResult>(await controller.AreaByBoundingBoxAsync(0, 0, 100, 100));
                Assert.IsType<UnauthorizedResult>(await controller.CreateObjectAsync(null));
                Assert.IsType<UnauthorizedResult>(await controller.LabelsByBoundingBoxAsync(0, 0, 100, 100));
                Assert.IsType<UnauthorizedResult>(await controller.SetLabelAsync(null));

                // A malformed bearer value denies exactly like an absent one - the gate validates the token, not just its presence.
                OrtoAreaController controller_Malformed = CreateOrtoAreaController(gISWebAPIConfigurationFileWatcher, supplyToken: true, rawToken: "not-a-valid-token");

                Assert.IsType<UnauthorizedResult>(await controller_Malformed.AvailableYearsByBoundingBoxAsync(0, 0, 100, 100));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
