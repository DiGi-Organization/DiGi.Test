using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="UnitController"/> endpoints return BadRequest when provided invalid parameters.
        /// </summary>
        [Fact]
        public async Task UnitController_Validation_AnswersBadRequest()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                UnitController controller = new(gISWebAPIConfigurationFileWatcher, new UnitPostgreSQLConverter(null), new AdministrativeAreal2DPostgreSQLConverter(null), new StatisticalDataCollectionPostgreSQLConverter(null));

                Assert.IsType<BadRequestResult>(await controller.GetItemByIdAsync(null));
                Assert.IsType<BadRequestResult>(await controller.GetItemByIdAsync(string.Empty));
                Assert.IsType<BadRequestResult>(await controller.GetItemByIdAsync("   "));
                Assert.IsType<BadRequestResult>(await controller.GetMatchAsync(null, null, null));
                Assert.IsType<BadRequestResult>(await controller.GetMatchAsync(null, AdministrativeArealType.Undefined, null));
                Assert.IsType<BadRequestResult>(await controller.GetComplianceAsync(null));
                Assert.IsType<BadRequestResult>(await controller.GetComplianceAsync(AdministrativeArealType.Undefined));
                Assert.IsType<BadRequestResult>(await controller.GetStatisticalDataCollectionAsync(null, null, null));
                Assert.IsType<BadRequestResult>(await controller.GetStatisticalDataCollectionAsync(null, AdministrativeArealType.Undefined, null));
                Assert.IsType<BadRequestResult>(await controller.GetStatisticalDataCollectionAsync(null, null, 0));
                Assert.IsType<BadRequestResult>(await controller.GetStatisticalDataCollectionAsync(null, null, 1, commandTimeout: -1));
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies that <see cref="UnitController"/> write endpoints return Unauthorized when write permissions are disabled in the configuration.
        /// </summary>
        [Fact]
        public async Task UnitController_DisabledUpdates_AnswersUnauthorized()
        {
            string path = Path.GetTempFileName();

            try
            {
                File.WriteAllLines(path,
                [
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateUnit)}=false",
                ]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                UnitController controller = new(gISWebAPIConfigurationFileWatcher, new UnitPostgreSQLConverter(null), new AdministrativeAreal2DPostgreSQLConverter(null), new StatisticalDataCollectionPostgreSQLConverter(null));

                Assert.IsType<UnauthorizedResult>(await controller.UpdateItemsAsync(new JsonArray(), "code"));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
