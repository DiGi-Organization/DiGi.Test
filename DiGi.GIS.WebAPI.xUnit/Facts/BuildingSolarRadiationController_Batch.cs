using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The second scratch county part of the batch solar radiation write facts, beside <see cref="countyId_SolarRadiation"/>. Only a partition key in the throwaway test databases.
        /// </summary>
        private const int countyId_SolarRadiation_Batch = 990312;

        /// <summary>
        /// Pins the query parameter names of <c>gis/buildingsolarradiation/setsolarradiations</c> (<c>Coding - WebAPI Contracts.md</c> §1): the results travel in the body, and only the timeout in the query. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public void BuildingSolarRadiationController_SetBatch_QueryParameterNames()
        {
            MethodInfo? methodInfo = typeof(BuildingSolarRadiationController).GetMethod(nameof(BuildingSolarRadiationController.SetSolarRadiationsAsync));
            Assert.NotNull(methodInfo);

            List<string> names = [];
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
            {
                if (parameterInfo.GetCustomAttribute<FromQueryAttribute>() is FromQueryAttribute fromQueryAttribute)
                {
                    Assert.NotNull(fromQueryAttribute.Name);
                    names.Add(fromQueryAttribute.Name);
                }
            }

            string[] names_Expected = ["commandtimeout"];
            Assert.Equal(names_Expected, names);
            Assert.NotNull(methodInfo.GetParameters()[0].GetCustomAttribute<FromBodyAttribute>());
        }

        /// <summary>
        /// Verifies that the batch write answers 401 without a user token, before the flag or the body are looked at, as the single write does. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_SetBatch_Unauthorized()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, null);

            Assert.IsType<UnauthorizedResult>(await controller.SetSolarRadiationsAsync([ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L).ToJsonObject()]));
        }

        /// <summary>
        /// Verifies that a valid user token with <c>AllowUpdateSolarRadiationData=false</c> answers 400 on the batch write, as on the single write: the token passes and the flag denies. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_SetBatch_FlagDisabled()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(false));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, "reviewer@example.com");

            Assert.IsType<BadRequestResult>(await controller.SetSolarRadiationsAsync([ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L).ToJsonObject()]));
        }

        /// <summary>
        /// Verifies that the batch write refuses the whole request with 400, before any database call, for a negative timeout, no body, an empty one, one over <see cref="Constants.SolarRadiation.MaximumBatchCount"/>, an element that is not an object, one of another DiGi type, one the single write would refuse among valid ones, and two results with one key. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_SetBatch_InvalidBody()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, "reviewer@example.com");

            Assert.IsType<BadRequestResult>(await controller.SetSolarRadiationsAsync([ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L).ToJsonObject()], -1));
            Assert.IsType<BadRequestResult>(await controller.SetSolarRadiationsAsync(null));
            Assert.IsType<BadRequestResult>(await controller.SetSolarRadiationsAsync([]));

            JsonArray jsonArray_Oversized = [];
            for (int i = 0; i <= Constants.SolarRadiation.MaximumBatchCount; i++)
            {
                jsonArray_Oversized.Add(new JsonObject());
            }

            Assert.IsType<BadRequestResult>(await controller.SetSolarRadiationsAsync(jsonArray_Oversized));

            List<JsonArray> jsonArrays =
            [
                [ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L).ToJsonObject(), JsonValue.Create(7)],
                [ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L).ToJsonObject(), ScratchSurfaceSolarRadiation(Constants.SolarRadiation.HourCount).ToJsonObject()],
                [ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L).ToJsonObject(), ScratchBatchSolarRadiationResult(countyId_SolarRadiation, null, 990311102L).ToJsonObject()],
                [ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L).ToJsonObject(), ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B2", 990311101L).ToJsonObject()],
                [ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L).ToJsonObject(), ScratchRadiusSolarRadiationResult("XUNIT-SR-B2", 990311102L, -0.1).ToJsonObject()],
            ];

            for (int i = 0; i < jsonArrays.Count; i++)
            {
                Assert.True(await controller.SetSolarRadiationsAsync(jsonArrays[i]) is BadRequestResult, $"Body {i} was not refused with 400.");
            }
        }

        /// <summary>
        /// Verifies the batch write end to end on the throwaway test databases.
        /// <para>Three results over two county parts, each carrying a forged <c>UserEmail</c> and an old <c>CreatedAt</c>, answer 200 with 3; each reads back under its own part with the token's email, one creation time for the whole request, and byte-identical shading factors. A second request whose second reference no part holds answers 404 and stores nothing - not even its first result, whose reference resolves.</para>
        /// <para>Medium test (two live database servers): it seeds <c>building_2d</c> rows under the scratch parts 990311 and 990312 and drops all four scratch partitions before and after. Reference: DiGi.GIS.WebAPI#93.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationController_SetBatch_RoundTrip()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));

            await CleanupSolarRadiationBatchScratchAsync(connectionData_Main, connectionData_Storage);
            try
            {
                await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
                {
                    Assert.NotNull(npgsqlConnection);
                    await npgsqlConnection.OpenAsync();

                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D(npgsqlConnection));
                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D_Partition(npgsqlConnection, countyId_SolarRadiation));
                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D_Partition(npgsqlConnection, countyId_SolarRadiation_Batch));
                    await ExecuteSolarRadiationAsync(npgsqlConnection, $"INSERT INTO {PostgreSQL.Constants.TableName.Building2D} (county_id, reference) VALUES ({countyId_SolarRadiation}, 'XUNIT-SR-B1'), ({countyId_SolarRadiation_Batch}, 'XUNIT-SR-B2'), ({countyId_SolarRadiation_Batch}, 'XUNIT-SR-B3'), ({countyId_SolarRadiation}, 'XUNIT-SR-B4');");
                }

                List<GIS.Classes.BuildingSolarRadiationResult> buildingSolarRadiationResults =
                [
                    ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B1", 990311101L),
                    ScratchBatchSolarRadiationResult(countyId_SolarRadiation_Batch, "XUNIT-SR-B2", 990311102L),
                    ScratchBatchSolarRadiationResult(countyId_SolarRadiation_Batch, "XUNIT-SR-B3", 990311103L),
                ];

                JsonArray jsonArray = [];
                foreach (GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult in buildingSolarRadiationResults)
                {
                    jsonArray.Add(buildingSolarRadiationResult.ToJsonObject());
                }

                DateTimeOffset dateTimeOffset_Before = DateTimeOffset.UtcNow.AddSeconds(-1);

                BuildingSolarRadiationController controller = SolarRadiationController(watcher, connectionData_Main, connectionData_Storage, "owner@example.com");
                OkObjectResult okObjectResult = Assert.IsType<OkObjectResult>(await controller.SetSolarRadiationsAsync(jsonArray));
                Assert.Equal(3, Assert.IsType<int>(okObjectResult.Value));

                HashSet<DateTimeOffset?> createdAts = [];
                foreach (GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult in buildingSolarRadiationResults)
                {
                    ContentResult contentResult = Assert.IsType<ContentResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(buildingSolarRadiationResult.BuildingModelId, buildingSolarRadiationResult.CountyId, 50, 1, true));
                    GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult_Read = Core.Convert.ToDiGi<GIS.Classes.BuildingSolarRadiationResult>(contentResult.Content)?.FirstOrDefault();
                    Assert.NotNull(buildingSolarRadiationResult_Read);

                    Assert.Equal(buildingSolarRadiationResult.Reference, buildingSolarRadiationResult_Read.Reference);
                    Assert.Equal(buildingSolarRadiationResult.CountyId, buildingSolarRadiationResult_Read.CountyId);
                    Assert.Equal("owner@example.com", buildingSolarRadiationResult_Read.UserEmail);
                    Assert.True(buildingSolarRadiationResult_Read.CreatedAt >= dateTimeOffset_Before, "CreatedAt was taken from the body, not stamped by the server.");
                    createdAts.Add(buildingSolarRadiationResult_Read.CreatedAt);

                    List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations = buildingSolarRadiationResult.SurfaceSolarRadiations!;
                    List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations_Read = buildingSolarRadiationResult_Read.SurfaceSolarRadiations;
                    Assert.NotNull(surfaceSolarRadiations_Read);
                    Assert.Equal(surfaceSolarRadiations.Count, surfaceSolarRadiations_Read.Count);
                    for (int i = 0; i < surfaceSolarRadiations.Count; i++)
                    {
                        Assert.Equal(surfaceSolarRadiations[i].ShadingFactors, surfaceSolarRadiations_Read[i].ShadingFactors);
                    }
                }

                Assert.Single(createdAts);

                // A reference no part holds refuses the request, and the result before it, which would resolve, is not stored either.
                JsonArray jsonArray_Unresolved =
                [
                    ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-B4", 990311104L).ToJsonObject(),
                    ScratchBatchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-MISSING", 990311105L).ToJsonObject(),
                ];

                BuildingSolarRadiationController controller_Unresolved = SolarRadiationController(watcher, connectionData_Main, connectionData_Storage, "owner@example.com");
                Assert.IsType<NotFoundResult>(await controller_Unresolved.SetSolarRadiationsAsync(jsonArray_Unresolved));
                Assert.IsType<NoContentResult>(await controller_Unresolved.GetSolarRadiationByBuildingModelIdAsync(990311104L, countyId_SolarRadiation, 50, 1, null));
            }
            finally
            {
                await CleanupSolarRadiationBatchScratchAsync(connectionData_Main, connectionData_Storage);
            }
        }

        /// <summary>
        /// Verifies on the throwaway test databases that the isolated building - radius 0, a variant the server-side solve stores (DiGi.GIS.PostgreSQL#136) - goes through the API like any other radius: the single write and the batch write each store one, and the read by building model id answers both at radius 0.
        /// <para>Before DiGi.GIS.WebAPI#93 every one of these calls answered 400, so a client-side solve could not store the isolated variant. Medium test (two live database servers): it seeds <c>building_2d</c> rows under the scratch part 990311 and drops the scratch partitions before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationController_Set_IsolatedRadius()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));

            await CleanupSolarRadiationBatchScratchAsync(connectionData_Main, connectionData_Storage);
            try
            {
                await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
                {
                    Assert.NotNull(npgsqlConnection);
                    await npgsqlConnection.OpenAsync();

                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D(npgsqlConnection));
                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D_Partition(npgsqlConnection, countyId_SolarRadiation));
                    await ExecuteSolarRadiationAsync(npgsqlConnection, $"INSERT INTO {PostgreSQL.Constants.TableName.Building2D} (county_id, reference) VALUES ({countyId_SolarRadiation}, 'XUNIT-SR-I1'), ({countyId_SolarRadiation}, 'XUNIT-SR-I2');");
                }

                BuildingSolarRadiationController controller = SolarRadiationController(watcher, connectionData_Main, connectionData_Storage, "owner@example.com");

                Assert.IsType<OkObjectResult>(await controller.SetSolarRadiationAsync(ScratchRadiusSolarRadiationResult("XUNIT-SR-I1", 990311301L, 0).ToJsonObject()));

                OkObjectResult okObjectResult = Assert.IsType<OkObjectResult>(await controller.SetSolarRadiationsAsync([ScratchRadiusSolarRadiationResult("XUNIT-SR-I2", 990311302L, 0).ToJsonObject()]));
                Assert.Equal(1, Assert.IsType<int>(okObjectResult.Value));

                foreach ((long buildingModelId, string reference) in new (long, string)[] { (990311301L, "XUNIT-SR-I1"), (990311302L, "XUNIT-SR-I2") })
                {
                    ContentResult contentResult = Assert.IsType<ContentResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(buildingModelId, countyId_SolarRadiation, 0, 1, null));
                    GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult_Read = Core.Convert.ToDiGi<GIS.Classes.BuildingSolarRadiationResult>(contentResult.Content)?.FirstOrDefault();
                    Assert.NotNull(buildingSolarRadiationResult_Read);
                    Assert.Equal(reference, buildingSolarRadiationResult_Read.Reference);
                    Assert.Equal(0, buildingSolarRadiationResult_Read.Radius);
                }

                // The isolated variant is its own key: nothing is stored for the same building at 50 m.
                Assert.IsType<NoContentResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(990311301L, countyId_SolarRadiation, 50, 1, null));
            }
            finally
            {
                await CleanupSolarRadiationBatchScratchAsync(connectionData_Main, connectionData_Storage);
            }
        }

        /// <summary>
        /// Builds a scratch result filed under <see cref="countyId_SolarRadiation"/> at the given radius, with one surface carrying a whole reference year of shading factors.
        /// </summary>
        /// <param name="reference">The building reference.</param>
        /// <param name="buildingModelId">The id of the building model.</param>
        /// <param name="radius">The neighbour radius, in m; 0 for the isolated building.</param>
        /// <returns>The result.</returns>
        private static GIS.Classes.BuildingSolarRadiationResult ScratchRadiusSolarRadiationResult(string reference, long buildingModelId, double radius)
        {
            return new GIS.Classes.BuildingSolarRadiationResult(reference, countyId_SolarRadiation, buildingModelId, radius, 1, GIS.Enums.WeatherSourceType.EPW, "17", null, null, null, null, [ScratchSurfaceSolarRadiation(Constants.SolarRadiation.HourCount)]);
        }

        /// <summary>
        /// Builds a scratch result for the batch write facts: one surface with a whole reference year of shading factors, a forged user email and an old creation time, both of which the server must replace.
        /// </summary>
        /// <param name="countyId">The county part the body names.</param>
        /// <param name="reference">The building reference, or null for a result the write refuses.</param>
        /// <param name="buildingModelId">The id of the building model.</param>
        /// <returns>The result.</returns>
        private static GIS.Classes.BuildingSolarRadiationResult ScratchBatchSolarRadiationResult(int countyId, string? reference, long buildingModelId)
        {
            return new GIS.Classes.BuildingSolarRadiationResult(reference, countyId, buildingModelId, 50, 1, GIS.Enums.WeatherSourceType.EPW, "17", null, null, "forged@example.com", new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), [ScratchSurfaceSolarRadiation(Constants.SolarRadiation.HourCount)]);
        }

        /// <summary>
        /// Drops the scratch partitions of the batch write facts - those of <see cref="countyId_SolarRadiation"/> and of <see cref="countyId_SolarRadiation_Batch"/>, on both test databases.
        /// </summary>
        /// <param name="connectionData_Main">The main test database.</param>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task CleanupSolarRadiationBatchScratchAsync(ConnectionData connectionData_Main, ConnectionData connectionData_Storage)
        {
            await CleanupSolarRadiationScratchAsync(connectionData_Main, connectionData_Storage);

            await using (NpgsqlConnection? npgsqlConnection_Main = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
            {
                Assert.NotNull(npgsqlConnection_Main);
                await npgsqlConnection_Main.OpenAsync();
                await ExecuteSolarRadiationAsync(npgsqlConnection_Main, $"DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.Building2D}_{countyId_SolarRadiation_Batch};");
            }

            await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection_Storage);
            await npgsqlConnection_Storage.OpenAsync();
            await ExecuteSolarRadiationAsync(npgsqlConnection_Storage, $"DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.BuildingSolarRadiation}_{countyId_SolarRadiation_Batch};");
        }
    }
}
