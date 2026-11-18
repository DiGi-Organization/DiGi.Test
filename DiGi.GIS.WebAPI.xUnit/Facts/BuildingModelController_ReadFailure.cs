using DiGi.Analytical.Building.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Collections.Generic;
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

        /// <summary>
        /// Verifies the other half of <see cref="BuildingModelController_ReadFailure_Answers500"/> on the throwaway test databases: a read that succeeds and finds nothing keeps 404 - <c>itemsbyreferences</c> for a reference absent from a scratch building model table that holds another, <c>itemsbycircle</c> for a circle far outside every stored building.
        /// <para>Medium test (two live database servers): the scratch table is created by the application's own upsert and dropped afterwards. Reference: DiGi-Organization/DiGi.GIS.WebAPI#97.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingModelController_EmptyRead_Answers404()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            ScratchBuildingModelPostgreSQLConverter scratchBuildingModelPostgreSQLConverter = new(connectionData_Storage);

            string path = ConfigurationFilePath();
            try
            {
                List<PostgreSQL.Classes.BuildingModel> buildingModels_PostgreSQL =
                [
                    new PostgreSQL.Classes.BuildingModel
                    {
                        CountyId = countyId_CentroidsByCountyId,
                        UniqueId = "xunit-97-m",
                        Reference = "XUNIT-97-M",
                        Object = BuildingModel().ToJsonObject(),
                    },
                ];

                Assert.NotNull(await scratchBuildingModelPostgreSQLConverter.UpdateAsync(buildingModels_PostgreSQL));

                await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
                {
                    Assert.NotNull(npgsqlConnection);
                    await npgsqlConnection.OpenAsync();
                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D(npgsqlConnection));
                }

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingModelController buildingModelController = new(gISWebAPIConfigurationFileWatcher, scratchBuildingModelPostgreSQLConverter, new PostgreSQL.Classes.Building2DPostgreSQLConverter(connectionData_Main), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null))
                {
                    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
                };

                Assert.IsType<ContentResult>(await buildingModelController.GetItemsByReferencesAsync(["XUNIT-97-M"], countyId_CentroidsByCountyId));
                Assert.IsType<NotFoundResult>(await buildingModelController.GetItemsByReferencesAsync(["XUNIT-97-MISSING"], countyId_CentroidsByCountyId));
                Assert.IsType<NotFoundResult>(await buildingModelController.GetItemsByCircleAsync(-9999999, -9999999, 10, null));
            }
            finally
            {
                System.IO.File.Delete(path);

                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();
                await ExecuteSolarRadiationAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {scratchBuildingModelPostgreSQLConverter.TableName};");
            }
        }
    }
}
