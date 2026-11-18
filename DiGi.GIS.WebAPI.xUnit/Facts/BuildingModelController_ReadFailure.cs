using DiGi.Analytical.Building.Enums;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <c>gis/buildingmodel/itemsbyreferences</c> and <c>itemsbycircle</c> answer 500 when the database cannot be reached, and not 404 - which their clients read as "no model stored" and "no neighbour". A client-side solar solve would otherwise count every receiver as not solvable, or solve without neighbours, instead of failing the county part (DiGi.GIS.PostgreSQL.UI#48).
        /// </summary>
        [Fact]
        public async Task BuildingModelController_ReadFailure_Answers500()
        {
            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingModelController buildingModelController = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingModelPostgreSQLConverter(null, BuildingModelDetailLevel.Component), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null))
                {
                    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
                };

                ObjectResult objectResult_References = Assert.IsType<ObjectResult>(await buildingModelController.GetItemsByReferencesAsync(["XUNIT-48"], 1));
                Assert.Equal(StatusCodes.Status500InternalServerError, objectResult_References.StatusCode);

                ObjectResult objectResult_Circle = Assert.IsType<ObjectResult>(await buildingModelController.GetItemsByCircleAsync(638000, 486000, 50, null));
                Assert.Equal(StatusCodes.Status500InternalServerError, objectResult_Circle.StatusCode);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
