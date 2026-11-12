using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the five annotator routes of <c>gis/buildingshape</c> answer 401 without a valid user token, before any parameter or store check runs, and that the sample load answers 401 without the access key (DiGi.GIS.WebAPI#80).
        /// <para>The annotator routes deny with a null sample key and a null label body, proving the session gate runs before validation; a malformed bearer value denies like an absent one. The sample load runs under an enforced key (<c>Open=false</c>): no key and a wrong key both answer 401, even with a valid user token, because the sample load is authorized by the key alone.</para>
        /// </summary>
        [Fact]
        public async Task BuildingShapeController_Auth()
        {
            string path = BuildingShapeConfigurationFilePath(open: false);

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);

                BuildingShapeController buildingShapeController = CreateBuildingShapeController(gISWebAPIConfigurationFileWatcher, supplyToken: false);

                Assert.IsType<UnauthorizedResult>(await buildingShapeController.NextSampleAsync(null));
                Assert.IsType<UnauthorizedResult>(await buildingShapeController.ProgressAsync(null));
                Assert.IsType<UnauthorizedResult>(await buildingShapeController.SetLabelAsync(null));
                Assert.IsType<UnauthorizedResult>(await buildingShapeController.LabelsAsync(null));
                Assert.IsType<UnauthorizedResult>(await buildingShapeController.SampleItemsAsync(null));

                BuildingShapeController buildingShapeController_Malformed = CreateBuildingShapeController(gISWebAPIConfigurationFileWatcher, supplyToken: true, rawToken: "not-a-valid-token");
                Assert.IsType<UnauthorizedResult>(await buildingShapeController_Malformed.NextSampleAsync("gis12-2026"));

                // The sample load is key-authorized: a valid session is not enough.
                BuildingShapeController buildingShapeController_Token = CreateBuildingShapeController(gISWebAPIConfigurationFileWatcher, supplyToken: true);
                Assert.IsType<UnauthorizedResult>(await buildingShapeController_Token.CreateSampleAsync("gis12-2026", [1465], 500, 12));
                Assert.IsType<UnauthorizedResult>(await buildingShapeController_Token.CreateSampleAsync("gis12-2026", [1465], 500, 12, key: "wrong-key"));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
