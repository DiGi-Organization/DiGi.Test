using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="GISWebAPIConfigurationFileWatcher.AllowUpdateOccupancyData"/> reflects configuration settings and defaults to false when absent.
        /// </summary>
        [Fact]
        public void GISWebAPIConfigurationFileWatcher_AllowUpdateOccupancyData()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(path,
                [
                    "Enabled=true",
                    "Key=\"key\"",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateOccupancyData)}=true",
                ]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher_Enabled = new(path);
                Assert.True(gISWebAPIConfigurationFileWatcher_Enabled.AllowUpdateOccupancyData);

                File.WriteAllLines(path,
                [
                    "Enabled=true",
                    "Key=\"key\"",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateOccupancyData)}=false",
                ]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher_Disabled = new(path);
                Assert.False(gISWebAPIConfigurationFileWatcher_Disabled.AllowUpdateOccupancyData);

                File.WriteAllLines(path,
                [
                    "Enabled=true",
                    "Key=\"key\"",
                ]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher_Default = new(path);
                Assert.False(gISWebAPIConfigurationFileWatcher_Default.AllowUpdateOccupancyData);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Verifies that <see cref="OccupancyDataController"/> write endpoints return BadRequest when write permissions are disabled in the configuration.
        /// </summary>
        [Fact]
        public async Task OccupancyDataController_DisabledUpdates_AnswersBadRequest()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(path,
                [
                    $"{nameof(GISWebAPIConfigurationFileWatcher.Open)}=true",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateOccupancyData)}=false",
                ]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OccupancyDataController occupancyDataController = new(
                    gISWebAPIConfigurationFileWatcher,
                    new PostgreSQL.Classes.Building2DOccupancyDataPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DOccupancyDataPostgreSQLConverter(null),
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));

                Assert.IsType<BadRequestResult>(await occupancyDataController.AdministrativeAreal2DUpdateItemsAsync(new JsonArray()));
                Assert.IsType<BadRequestResult>(await occupancyDataController.Building2DUpdateItemsAsync(new JsonArray(), "code"));
                Assert.IsType<BadRequestResult>(await occupancyDataController.Building2DUpdateItemsByCountyIdsAsync(new JsonArray(), [1]));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Verifies that <see cref="OccupancyDataController"/> write endpoints are independent from <see cref="GISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData"/>.
        /// </summary>
        [Fact]
        public async Task OccupancyDataController_FlagIndependence()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(path,
                [
                    $"{nameof(GISWebAPIConfigurationFileWatcher.Open)}=true",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateOccupancyData)}=false",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData)}=true",
                ]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OccupancyDataController occupancyDataController = new(
                    gISWebAPIConfigurationFileWatcher,
                    new PostgreSQL.Classes.Building2DOccupancyDataPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DOccupancyDataPostgreSQLConverter(null),
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));

                Assert.IsType<BadRequestResult>(await occupancyDataController.AdministrativeAreal2DUpdateItemsAsync(new JsonArray()));
                Assert.IsType<BadRequestResult>(await occupancyDataController.Building2DUpdateItemsAsync(new JsonArray(), "code"));
                Assert.IsType<BadRequestResult>(await occupancyDataController.Building2DUpdateItemsByCountyIdsAsync(new JsonArray(), [1]));

                YearBuiltDataController yearBuiltDataController = new(
                    gISWebAPIConfigurationFileWatcher,
                    new PostgreSQL.Classes.YearBuiltDataPostgreSQLConverter(null),
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null));

                Assert.IsType<NoContentResult>(await yearBuiltDataController.UpdateItemsAsync([], "code"));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Verifies that <see cref="OccupancyDataController.GetBuilding2DSnapshotByCountyIdAsync"/> and <see cref="OccupancyDataController.GetBuilding2DSubdivisionSnapshotByCountyIdAsync"/> refuse a negative command timeout (and the second an omitted county) with HTTP 400 before any database access.
        /// </summary>
        [Fact]
        public async Task OccupancyDataController_SnapshotByCountyId_NegativeTimeout_AnswersBadRequest()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(path, [$"{nameof(GISWebAPIConfigurationFileWatcher.Open)}=true"]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OccupancyDataController occupancyDataController = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.Building2DOccupancyDataPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DOccupancyDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));

                Assert.IsType<BadRequestResult>(await occupancyDataController.GetBuilding2DSnapshotByCountyIdAsync(null, -1));
                Assert.IsType<BadRequestResult>(await occupancyDataController.GetBuilding2DSubdivisionSnapshotByCountyIdAsync(null));
                Assert.IsType<BadRequestResult>(await occupancyDataController.GetBuilding2DSubdivisionSnapshotByCountyIdAsync(1, -1));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// A transient database failure is a "retry can succeed" class, so it must answer 503 with a Retry-After header rather than the uniform 500.
        /// <para>Drives a real transient failure through the real stack with no server: a dead loopback host makes the converter's OpenAsync throw a genuine NpgsqlException, which Npgsql classifies IsTransient = true. All four read actions of <see cref="OccupancyDataController"/> are asserted, so a 503 mapping added to one action and forgotten in a sibling cannot pass.</para>
        /// <para>Red before the 503 mapping existed (the path answered 500); green after.</para>
        /// <para>Medium test (4.1 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task OccupancyDataController_TransientDatabaseFailure_Answers503()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OccupancyDataController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.Building2DOccupancyDataPostgreSQLConverter(new DiGi.PostgreSQL.Classes.ConnectionData("127.0.0.1", "user", "pass", "db", 1)), new PostgreSQL.Classes.AdministrativeAreal2DOccupancyDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                AssertTransient503(controller, await controller.GetBuilding2DDuplicateReferencesAsync(null));
                AssertTransient503(controller, await controller.GetBuilding2DDuplicatesCountAsync(null));
                AssertTransient503(controller, await controller.GetBuilding2DSnapshotByCountyIdAsync(null));
                AssertTransient503(controller, await controller.GetBuilding2DSubdivisionSnapshotByCountyIdAsync(1));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
