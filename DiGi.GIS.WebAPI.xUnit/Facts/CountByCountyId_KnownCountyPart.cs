using DiGi.Analytical.Building.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies DiGi-Organization/DiGi.GIS.WebAPI#69 on the terrain, building model and building count actions: a known county part answers 200 whatever it holds (0 when it has no partition), an identifier that is not a county part answers 404, and a negative <c>commandtimeout</c> still answers 400.
        /// <para>Runs against the throwaway main test database (<c>GIS_PostgreSQL_Main_Test.conf</c>) and takes the first county part (<c>type_id = 2</c>) found there; skipped when the configuration is missing, names a host database, or the table is missing or holds no county. Writes nothing.</para>
        /// <para>Medium test (a live database server). The 204 case is covered by the unchanged branch and by the live check on the deployed host.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task CountByCountyId_KnownCountyPart()
        {
            (ConnectionData connectionData_Main, _) = SolarRadiationTestConnectionDatas();

            int? countyId = null;
            await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
            {
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();

                await using NpgsqlCommand npgsqlCommand_Exists = new($"SELECT to_regclass('{PostgreSQL.Constants.TableName.AdministrativeAreal2D}') IS NOT NULL;", npgsqlConnection);
                if (await npgsqlCommand_Exists.ExecuteScalarAsync() is true)
                {
                    await using NpgsqlCommand npgsqlCommand = new($"SELECT id FROM {PostgreSQL.Constants.TableName.AdministrativeAreal2D} WHERE type_id = 2 ORDER BY id LIMIT 1;", npgsqlConnection);
                    if (await npgsqlCommand.ExecuteScalarAsync() is int id)
                    {
                        countyId = id;
                    }
                }
            }

            Skip.If(countyId is null, "The main test database holds no county part (or has no administrative_areal_2d table).");

            const int countyId_Unknown = 999999999;

            PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter administrativeAreal2DPostgreSQLConverter = new(connectionData_Main);

            TerrainController terrainController = new(new PostgreSQL.Classes.TerrainPointPostgreSQLConverter(connectionData_Main), administrativeAreal2DPostgreSQLConverter);
            terrainController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            Assert.IsNotType<NotFoundResult>(await terrainController.GetCountByCountyIdAsync(countyId!.Value));
            Assert.IsType<NotFoundResult>(await terrainController.GetCountByCountyIdAsync(countyId_Unknown));
            Assert.IsType<BadRequestResult>(await terrainController.GetCountByCountyIdAsync(countyId.Value, commandTimeout: -1));

            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);

                BuildingModelController buildingModelController = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingModelPostgreSQLConverter(connectionData_Main, BuildingModelDetailLevel.Component), new PostgreSQL.Classes.Building2DPostgreSQLConverter(connectionData_Main), administrativeAreal2DPostgreSQLConverter);
                buildingModelController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                Assert.IsNotType<NotFoundResult>(await buildingModelController.GetCountByCountyIdAsync(countyId.Value));
                Assert.IsType<NotFoundResult>(await buildingModelController.GetCountByCountyIdAsync(countyId_Unknown));
                Assert.IsType<BadRequestResult>(await buildingModelController.GetCountByCountyIdAsync(countyId.Value, -1));

                BuildingController buildingController = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingPostgreSQLConverter(connectionData_Main), new PostgreSQL.Classes.Building2DPostgreSQLConverter(connectionData_Main), administrativeAreal2DPostgreSQLConverter);
                buildingController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                Assert.IsNotType<NotFoundResult>(await buildingController.GetCountAsync(countyId.Value));
                Assert.IsType<NotFoundResult>(await buildingController.GetCountAsync(countyId_Unknown));
                Assert.IsType<BadRequestResult>(await buildingController.GetCountAsync(countyId.Value, commandTimeout: -1));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
