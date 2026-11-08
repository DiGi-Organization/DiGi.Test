using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the year built maintenance actions resolve to the routes their clients call (DiGi.GIS.WebAPI#50).
        /// </summary>
        [Fact]
        public void YearBuiltDataController_Maintenance_Routes()
        {
            Assert.Equal("gis/yearbuiltdata/removeitemsbycountyids", DiGi.WebAPI.Query.Path<YearBuiltDataController>(nameof(YearBuiltDataController.RemoveItemsByCountyIdsAsync)));
            Assert.Equal("gis/yearbuiltdata/removepredictedyearbuiltsbycountyids", DiGi.WebAPI.Query.Path<YearBuiltDataController>(nameof(YearBuiltDataController.RemovePredictedYearBuiltsByCountyIdsAsync)));
            Assert.Equal("gis/yearbuiltdata/removeuseryearbuilt", DiGi.WebAPI.Query.Path<YearBuiltDataController>(nameof(YearBuiltDataController.RemoveUserYearBuiltAsync)));
            Assert.Equal("gis/yearbuiltdata/removeuseryearbuiltsbycountyids", DiGi.WebAPI.Query.Path<YearBuiltDataController>(nameof(YearBuiltDataController.RemoveUserYearBuiltsByCountyIdsAsync)));
            Assert.Equal("gis/yearbuiltdata/updatebuildingdatabycountyids", DiGi.WebAPI.Query.Path<YearBuiltDataController>(nameof(YearBuiltDataController.UpdateBuildingDataByCountyIdsAsync)));
            Assert.Equal("gis/yearbuiltdata/predictedyearbuiltruns", DiGi.WebAPI.Query.Path<YearBuiltDataController>(nameof(YearBuiltDataController.GetPredictedYearBuiltRunsAsync)));
        }

        /// <summary>
        /// Verifies every deny branch of every key-protected year built maintenance action (<c>Coding - WebAPI Simple Authorization.md</c> §5): a missing key, a wrong key, enforcement disabled, an unconfigured file and a disabled feature flag all answer a bodiless 401, and nothing reaches the database.
        /// <para>The delete actions are gated by <see cref="GISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData"/> - a flag of its own, so a host accepting year built updates does not accept deletes - and the recompute by <see cref="GISWebAPIConfigurationFileWatcher.AllowUpdateBuildingData"/>; a prediction run removal asking for the recompute needs both. The converters are built on unreachable connection data, so a gate that let a request through would surface as a 500, not a 401.</para>
        /// </summary>
        [Fact]
        public async Task YearBuiltDataController_Maintenance_DenyBranches()
        {
            const string key = "xunit-secret";

            string[] flags_All =
            [
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateBuildingData)}=true",
            ];

            // key supplied => configuration lines
            List<Tuple<string?, string[]>> cases =
            [
                new(null, [$"Enabled=true", $"Key=\"{key}\"", .. flags_All]),
                new("wrong", [$"Enabled=true", $"Key=\"{key}\"", .. flags_All]),
                new(key, [$"Enabled=false", $"Key=\"{key}\"", .. flags_All]),
                new(key, []),
            ];

            foreach (Tuple<string?, string[]> @case in cases)
            {
                await AssertMaintenanceUnauthorizedAsync(@case.Item2, @case.Item1, true, true, true);
            }

            // Authorized key, one flag missing at a time.
            await AssertMaintenanceUnauthorizedAsync([$"Enabled=true", $"Key=\"{key}\"", $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData)}=true", $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateBuildingData)}=true"], key, true, false, false);
            await AssertMaintenanceUnauthorizedAsync([$"Enabled=true", $"Key=\"{key}\"", $"{nameof(GISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData)}=true"], key, false, true, false);

            static async Task AssertMaintenanceUnauthorizedAsync(string[] lines, string? key, bool delete, bool recompute, bool removeRunWithRecompute)
            {
                string path = System.IO.Path.GetTempFileName();
                try
                {
                    System.IO.File.WriteAllLines(path, lines);

                    using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                    YearBuiltDataController controller = UnreachableYearBuiltDataController(gISWebAPIConfigurationFileWatcher);

                    if (delete)
                    {
                        Assert.IsType<UnauthorizedResult>(await controller.RemoveItemsByCountyIdsAsync(null, [1], key: key));
                        Assert.IsType<UnauthorizedResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [1], 1, key: key));
                        Assert.IsType<UnauthorizedResult>(await controller.RemoveUserYearBuiltsByCountyIdsAsync(["reference"], [1], key: key));
                    }

                    if (recompute)
                    {
                        Assert.IsType<UnauthorizedResult>(await controller.UpdateBuildingDataByCountyIdsAsync(null, [1], key: key));
                    }

                    if (removeRunWithRecompute)
                    {
                        Assert.IsType<UnauthorizedResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [1], 1, updateBuildingData: true, key: key));
                    }
                }
                finally
                {
                    System.IO.File.Delete(path);
                }
            }
        }

        /// <summary>
        /// Verifies that a prediction run removal asking for the building data recompute is refused before anything is written when only the delete flag is set.
        /// </summary>
        [Fact]
        public async Task YearBuiltDataController_RemovePredictedYearBuilts_RecomputeNeedsBuildingDataFlag()
        {
            string path = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllLines(path, ["Open=true", $"{nameof(GISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData)}=true"]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                YearBuiltDataController controller = UnreachableYearBuiltDataController(gISWebAPIConfigurationFileWatcher);

                Assert.IsType<UnauthorizedResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [1], 1, updateBuildingData: true));
                Assert.IsType<UnauthorizedResult>(await controller.UpdateBuildingDataByCountyIdsAsync(null, [1]));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies the parameter checks of the year built maintenance actions, which run after both gates pass and before any database call: no county part, a negative command timeout, a limit outside 1-10000, more than 10000 references, a missing or out-of-range stamp, a delete of non-empty objects with no references, and a moderation withdrawal with no references all answer 400.
        /// <para>Then a valid request on a converter that cannot connect answers 500 - the operation could not run, which is never reported as success or as "nothing found".</para>
        /// </summary>
        [Fact]
        public async Task YearBuiltDataController_Maintenance_Validation()
        {
            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                YearBuiltDataController controller = new(gISWebAPIConfigurationFileWatcher, new YearBuiltDataPostgreSQLConverter(null), new Building2DPostgreSQLConverter(null), new AdministrativeAreal2DPostgreSQLConverter(null), new BuildingDataPostgreSQLConverter(null));

                string[] references_OverLimit = new string[10001];
                Array.Fill(references_OverLimit, "reference");

                Assert.IsType<BadRequestResult>(await controller.RemoveItemsByCountyIdsAsync(null, null));
                Assert.IsType<BadRequestResult>(await controller.RemoveItemsByCountyIdsAsync(null, []));
                Assert.IsType<BadRequestResult>(await controller.RemoveItemsByCountyIdsAsync(null, [1], commandTimeout: -1));
                Assert.IsType<BadRequestObjectResult>(await controller.RemoveItemsByCountyIdsAsync(null, [1], limit: 0));
                Assert.IsType<BadRequestObjectResult>(await controller.RemoveItemsByCountyIdsAsync(null, [1], limit: 10001));
                Assert.IsType<BadRequestObjectResult>(await controller.RemoveItemsByCountyIdsAsync(references_OverLimit, [1]));
                Assert.IsType<BadRequestObjectResult>(await controller.RemoveItemsByCountyIdsAsync(null, [1], emptyOnly: false));
                Assert.IsType<BadRequestObjectResult>(await controller.RemoveItemsByCountyIdsAsync([], [1], emptyOnly: false));

                Assert.IsType<BadRequestResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [], 1));
                Assert.IsType<BadRequestObjectResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [1], null));
                Assert.IsType<BadRequestObjectResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [1], -1));
                Assert.IsType<BadRequestObjectResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [1], DateTime.MaxValue.Ticks + 1));
                Assert.IsType<BadRequestObjectResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [1], 1, limit: 0));

                Assert.IsType<BadRequestResult>(await controller.RemoveUserYearBuiltsByCountyIdsAsync(null, [1]));
                Assert.IsType<BadRequestResult>(await controller.RemoveUserYearBuiltsByCountyIdsAsync([], [1]));
                Assert.IsType<BadRequestResult>(await controller.RemoveUserYearBuiltsByCountyIdsAsync(["reference"], null));
                Assert.IsType<BadRequestObjectResult>(await controller.RemoveUserYearBuiltsByCountyIdsAsync(references_OverLimit, [1]));

                Assert.IsType<BadRequestResult>(await controller.UpdateBuildingDataByCountyIdsAsync(null, []));
                Assert.IsType<BadRequestResult>(await controller.UpdateBuildingDataByCountyIdsAsync(null, [1], commandTimeout: -1));

                Assert.IsType<BadRequestResult>(await controller.GetPredictedYearBuiltRunsAsync(null));
                Assert.IsType<BadRequestResult>(await controller.GetPredictedYearBuiltRunsAsync([1], commandTimeout: -1));

                // Past every check, the converters cannot connect: the operation did not run, and says so.
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.RemoveItemsByCountyIdsAsync(null, [1])).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.RemovePredictedYearBuiltsByCountyIdsAsync(null, [1], 1)).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.RemoveUserYearBuiltsByCountyIdsAsync(["reference"], [1])).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.UpdateBuildingDataByCountyIdsAsync(null, [1])).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.GetPredictedYearBuiltRunsAsync([1])).StatusCode);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies the user-token withdrawal of one's own year built entry: no token answers 401, a disabled <c>AllowUpdateYearBuiltData</c> 400 (the same order as <c>setuseryearbuilt</c>), a body without the county part or the reference 400, and a valid request whose county parts cannot be read 500.
        /// </summary>
        [Fact]
        public async Task YearBuiltDataController_RemoveUserYearBuilt_UserToken()
        {
            SecurityKeyManager securityKeyManager = new();
            _ = securityKeyManager.Generate();
            TokenRevocationStore tokenRevocationStore = new();
            string token = CreateToken(securityKeyManager, "reviewer@example.com");

            using (GISWebAPIConfigurationFileWatcher watcher = new(ConfigurationFilePath()))
            {
                YearBuiltDataController controller_NoToken = new(watcher, new YearBuiltDataPostgreSQLConverter(null), new Building2DPostgreSQLConverter(null), new AdministrativeAreal2DPostgreSQLConverter(null), new BuildingDataPostgreSQLConverter(null), securityKeyManager, tokenRevocationStore);
                controller_NoToken.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
                Assert.IsType<UnauthorizedResult>(await controller_NoToken.RemoveUserYearBuiltAsync(new Classes.Parameter.UserYearBuiltParameter() { CountyId = 1, Reference = "reference" }));

                DefaultHttpContext httpContext = new();
                httpContext.Request.Headers.Authorization = "Bearer " + token;

                YearBuiltDataController controller = new(watcher, new YearBuiltDataPostgreSQLConverter(null), new Building2DPostgreSQLConverter(null), new AdministrativeAreal2DPostgreSQLConverter(null), new BuildingDataPostgreSQLConverter(null), securityKeyManager, tokenRevocationStore);
                controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

                Assert.IsType<BadRequestResult>(await controller.RemoveUserYearBuiltAsync(null));
                Assert.IsType<BadRequestResult>(await controller.RemoveUserYearBuiltAsync(new Classes.Parameter.UserYearBuiltParameter() { Reference = "reference" }));
                Assert.IsType<BadRequestResult>(await controller.RemoveUserYearBuiltAsync(new Classes.Parameter.UserYearBuiltParameter() { CountyId = 1, Reference = " " }));
                Assert.Equal(500, Assert.IsType<ObjectResult>(await controller.RemoveUserYearBuiltAsync(new Classes.Parameter.UserYearBuiltParameter() { CountyId = 1, Reference = "reference" })).StatusCode);
            }

            using (GISWebAPIConfigurationFileWatcher watcher_Disabled = new(DisabledYearBuiltFlagPath()))
            {
                DefaultHttpContext httpContext = new();
                httpContext.Request.Headers.Authorization = "Bearer " + token;

                YearBuiltDataController controller = new(watcher_Disabled, new YearBuiltDataPostgreSQLConverter(null), new Building2DPostgreSQLConverter(null), new AdministrativeAreal2DPostgreSQLConverter(null), new BuildingDataPostgreSQLConverter(null), securityKeyManager, tokenRevocationStore);
                controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

                Assert.IsType<BadRequestResult>(await controller.RemoveUserYearBuiltAsync(new Classes.Parameter.UserYearBuiltParameter() { CountyId = 1, Reference = "reference" }));
            }
        }

        /// <summary>
        /// Verifies that the configuration watcher reads <see cref="GISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData"/>, that a missing value denies, and that it is independent of <see cref="GISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData"/>.
        /// </summary>
        [Fact]
        public void GISWebAPIConfigurationFileWatcher_AllowDeleteYearBuiltData()
        {
            string path = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllLines(path, [$"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData)}=true"]);
                using (GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path))
                {
                    Assert.True(gISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData);
                    Assert.False(gISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData);
                }

                System.IO.File.WriteAllLines(path, [$"{nameof(GISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData)}=true"]);
                using (GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path))
                {
                    Assert.True(gISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData);
                    Assert.False(gISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData);
                }
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Pins the binding defect of <c>tablebybuildingdatabyreferencesparameter</c> (DiGi.GIS.WebAPI#49): with nullable reference types enabled, ASP.NET treats a non-nullable reference-typed property as implicitly required, so <c>"ColumnUniqueIds": null</c> answered 400 although the schema said nullable and the action reads null as "all columns". The property must be annotated nullable - on the subdivision parameter as well, which carried the same declaration.
        /// </summary>
        [Fact]
        public void BuildingDataParameters_ColumnUniqueIds_Nullable()
        {
            NullabilityInfoContext nullabilityInfoContext = new();

            foreach (Type type in new Type[] { typeof(BuildingDataByReferencesParameter), typeof(BuildingDataBySubdivisionIdsParameter) })
            {
                PropertyInfo? propertyInfo = type.GetProperty(nameof(BuildingDataByReferencesParameter.ColumnUniqueIds));
                Assert.NotNull(propertyInfo);
                Assert.Equal(NullabilityState.Nullable, nullabilityInfoContext.Create(propertyInfo).WriteState);
            }
        }

        /// <summary>
        /// Verifies that the year built maintenance client wrappers refuse a missing manager or an empty set of county parts without sending anything.
        /// </summary>
        [Fact]
        public async Task YearBuiltDataController_Maintenance_ClientValidation()
        {
            GISWebAPIManager? gisWebAPIManager_Null = null;

            Assert.Null(await gisWebAPIManager_Null.RemoveYearBuiltDatasAsync([1]));
            Assert.Null(await gisWebAPIManager_Null.RemovePredictedYearBuiltsAsync([1], 1));
            Assert.Null(await gisWebAPIManager_Null.RemoveUserYearBuiltsAsync([1], ["reference"]));
            Assert.Null(await gisWebAPIManager_Null.UpdateBuildingDataYearBuiltAsync([1]));
            Assert.Null(await gisWebAPIManager_Null.PredictedYearBuiltRunsAsync([1]));

            GISWebAPIManager gISWebAPIManager = new(null);
            Assert.Null(await gISWebAPIManager.RemoveYearBuiltDatasAsync([]));
            Assert.Null(await gISWebAPIManager.RemovePredictedYearBuiltsAsync(null, 1));
            Assert.Null(await gISWebAPIManager.RemoveUserYearBuiltsAsync([1], []));
            Assert.Null(await gISWebAPIManager.UpdateBuildingDataYearBuiltAsync([]));
            Assert.Null(await gISWebAPIManager.PredictedYearBuiltRunsAsync([]));
        }

        /// <summary>
        /// Builds a year built data controller whose converters point at a dead loopback port, so any request that passes the gates fails at the database rather than silently succeeding.
        /// </summary>
        /// <param name="gISWebAPIConfigurationFileWatcher">The configuration watcher of the controller.</param>
        /// <returns>The controller.</returns>
        private static YearBuiltDataController UnreachableYearBuiltDataController(GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher)
        {
            DiGi.PostgreSQL.Classes.ConnectionData connectionData = new("127.0.0.1", "user", "pass", "db", 1);

            YearBuiltDataController result = new(gISWebAPIConfigurationFileWatcher, new YearBuiltDataPostgreSQLConverter(connectionData), new Building2DPostgreSQLConverter(connectionData), new AdministrativeAreal2DPostgreSQLConverter(connectionData), new BuildingDataPostgreSQLConverter(connectionData));
            result.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            return result;
        }
    }
}
