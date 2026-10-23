using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
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
        /// Pins the query parameter names of <c>gis/buildingsolarradiation/solarradiationbybuildingmodelid</c> - the contract <c>DiGi.GIS.WebAPI.UI</c> sends without any compile-time link - and that every one of them binds as nullable, so an omitted parameter reaches the action as null and is refused instead of meaning <c>default</c> (<c>Coding - WebAPI Contracts.md</c> §1-2). No <c>epwfileid</c> remains.
        /// <para>Reference: DiGi.GIS.WebAPI#67.</para>
        /// </summary>
        [Fact]
        public void BuildingSolarRadiationController_QueryParameterNames()
        {
            MethodInfo? methodInfo = typeof(BuildingSolarRadiationController).GetMethod(nameof(BuildingSolarRadiationController.GetSolarRadiationByBuildingModelIdAsync));
            Assert.NotNull(methodInfo);

            List<string> names = [];
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
            {
                if (parameterInfo.GetCustomAttribute<FromQueryAttribute>() is not FromQueryAttribute fromQueryAttribute)
                {
                    continue;
                }

                Assert.NotNull(fromQueryAttribute.Name);
                names.Add(fromQueryAttribute.Name);
                Assert.True(Nullable.GetUnderlyingType(parameterInfo.ParameterType) is not null, $"Query parameter '{fromQueryAttribute.Name}' is not nullable.");
            }

            string[] names_Expected = ["id", "countyid", "radius", "solverversion", "shadingfactors"];
            Assert.Equal(names_Expected, names);
        }

        /// <summary>
        /// Verifies that the read answers 400 for each missing or invalid parameter - no id, no county, no or a non-positive or non-finite radius, no solver version - and never reaches the database (the converter has no connection).
        /// <para>Reference: DiGi.GIS.WebAPI#67.</para>
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_Get_MissingParameter()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, null);

            Assert.IsType<BadRequestResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(null, 1, 50, 1, null));
            Assert.IsType<BadRequestResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(0, 1, 50, 1, null));
            Assert.IsType<BadRequestResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(7, null, 50, 1, null));
            Assert.IsType<BadRequestResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(7, 1, null, 1, null));
            Assert.IsType<BadRequestResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(7, 1, 0, 1, null));
            Assert.IsType<BadRequestResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(7, 1, double.NaN, 1, null));
            Assert.IsType<BadRequestResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(7, 1, 50, null, null));
        }

        /// <summary>
        /// Verifies that the write answers 401 without a user token - checked first, so not even a valid body or an enabled flag gets further - and writes nothing (the converter has no connection).
        /// <para>Reference: DiGi.GIS.WebAPI#67.</para>
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_Set_Unauthorized()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, null);

            Assert.IsType<UnauthorizedResult>(await controller.SetSolarRadiationAsync(ScratchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-API", "forged@example.com").ToJsonObject()));
        }

        /// <summary>
        /// Verifies that a valid user token with <c>AllowUpdateSolarRadiationData=false</c> answers 400: the token check passes and the flag check denies, so the order (token before flag) is what produces 400 rather than 401.
        /// <para>Reference: DiGi.GIS.WebAPI#67.</para>
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_Set_FlagDisabled()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(false));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, "reviewer@example.com");

            Assert.IsType<BadRequestResult>(await controller.SetSolarRadiationAsync(ScratchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-API", null).ToJsonObject()));
        }

        /// <summary>
        /// Verifies that the write refuses, with 400 and before any database call, each body it cannot store: none, one of another DiGi type, no reference, an undefined weather source, no weather source id, a period that ends before it starts, no surfaces, and a surface whose shading factors are not a whole reference year.
        /// <para>Reference: DiGi.GIS.WebAPI#67.</para>
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_Set_InvalidBody()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, "reviewer@example.com");

            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation = ScratchSurfaceSolarRadiation(Constants.SolarRadiation.HourCount);
            DateTimeOffset dateTimeOffset = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

            List<JsonObject?> jsonObjects =
            [
                null,
                surfaceSolarRadiation.ToJsonObject(),
                ScratchSolarRadiationResult(countyId_SolarRadiation, null, null).ToJsonObject(),
                new GIS.Classes.BuildingSolarRadiationResult("XUNIT-SR-API", countyId_SolarRadiation, 990311001L, 50, 1, GIS.Enums.WeatherSourceType.Undefined, "17", null, null, null, null, [surfaceSolarRadiation]).ToJsonObject(),
                new GIS.Classes.BuildingSolarRadiationResult("XUNIT-SR-API", countyId_SolarRadiation, 990311001L, 50, 1, GIS.Enums.WeatherSourceType.EPW, " ", null, null, null, null, [surfaceSolarRadiation]).ToJsonObject(),
                new GIS.Classes.BuildingSolarRadiationResult("XUNIT-SR-API", countyId_SolarRadiation, 990311001L, 50, 1, GIS.Enums.WeatherSourceType.Live, "station-1", dateTimeOffset, dateTimeOffset.AddHours(-1), null, null, [surfaceSolarRadiation]).ToJsonObject(),
                new GIS.Classes.BuildingSolarRadiationResult("XUNIT-SR-API", countyId_SolarRadiation, 990311001L, 50, 1, GIS.Enums.WeatherSourceType.EPW, "17", null, null, null, null, []).ToJsonObject(),
                new GIS.Classes.BuildingSolarRadiationResult("XUNIT-SR-API", countyId_SolarRadiation, 990311001L, 50, 1, GIS.Enums.WeatherSourceType.EPW, "17", null, null, null, null, [surfaceSolarRadiation, ScratchSurfaceSolarRadiation(Constants.SolarRadiation.HourCount - 1)]).ToJsonObject(),
            ];

            for (int i = 0; i < jsonObjects.Count; i++)
            {
                Assert.True(await controller.SetSolarRadiationAsync(jsonObjects[i]) is BadRequestResult, $"Body {i} was not refused with 400.");
            }
        }

        /// <summary>
        /// Verifies the write and the read end to end on the throwaway test databases: a body carrying a forged <c>UserEmail</c> and an old <c>CreatedAt</c> is stored under the part its reference resolves to, with the token's email and the time of the write; it reads back with byte-identical shading factors, or with none when they are not asked for; and an unknown key answers 204.
        /// <para>Medium test (two live database servers): it seeds a <c>building_2d</c> row under the scratch part 990311 and drops both scratch partitions before and after. Reference: DiGi.GIS.WebAPI#67.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationController_Set_EmailFromToken_Get_RoundTrip()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, connectionData_Main, connectionData_Storage, "owner@example.com");

            await CleanupSolarRadiationScratchAsync(connectionData_Main, connectionData_Storage);
            try
            {
                await using (NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
                {
                    Assert.NotNull(npgsqlConnection);
                    await npgsqlConnection.OpenAsync();

                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D(npgsqlConnection));
                    Assert.True(await PostgreSQL.Create.TableAsync_Building2D_Partition(npgsqlConnection, countyId_SolarRadiation));
                    await ExecuteSolarRadiationAsync(npgsqlConnection, $"INSERT INTO {PostgreSQL.Constants.TableName.Building2D} (county_id, reference) VALUES ({countyId_SolarRadiation}, 'XUNIT-SR-API');");
                }

                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult = ScratchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-API", "forged@example.com");
                DateTimeOffset dateTimeOffset_Before = DateTimeOffset.UtcNow.AddSeconds(-1);

                OkObjectResult okObjectResult = Assert.IsType<OkObjectResult>(await controller.SetSolarRadiationAsync(buildingSolarRadiationResult.ToJsonObject()));
                Assert.IsType<long>(okObjectResult.Value);

                ContentResult contentResult = Assert.IsType<ContentResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(990311001L, countyId_SolarRadiation, 50, 1, true));
                Assert.Equal("application/json", contentResult.ContentType);
                GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult_Read = Core.Convert.ToDiGi<GIS.Classes.BuildingSolarRadiationResult>(contentResult.Content)?.FirstOrDefault();
                Assert.NotNull(buildingSolarRadiationResult_Read);

                Assert.Equal("owner@example.com", buildingSolarRadiationResult_Read.UserEmail);
                Assert.NotNull(buildingSolarRadiationResult_Read.CreatedAt);
                Assert.True(buildingSolarRadiationResult_Read.CreatedAt >= dateTimeOffset_Before, "CreatedAt was taken from the body, not stamped by the server.");
                Assert.Equal(countyId_SolarRadiation, buildingSolarRadiationResult_Read.CountyId);
                Assert.Equal(GIS.Enums.WeatherSourceType.EPW, buildingSolarRadiationResult_Read.WeatherSourceType);
                Assert.Equal("17", buildingSolarRadiationResult_Read.WeatherSourceId);

                List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations = buildingSolarRadiationResult.SurfaceSolarRadiations!;
                List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations_Read = buildingSolarRadiationResult_Read.SurfaceSolarRadiations;
                Assert.NotNull(surfaceSolarRadiations_Read);
                Assert.Equal(surfaceSolarRadiations.Count, surfaceSolarRadiations_Read.Count);
                for (int i = 0; i < surfaceSolarRadiations.Count; i++)
                {
                    Assert.Equal(surfaceSolarRadiations[i].Irradiation, surfaceSolarRadiations_Read[i].Irradiation);
                    Assert.Equal(surfaceSolarRadiations[i].ShadingFactors, surfaceSolarRadiations_Read[i].ShadingFactors);
                }

                ContentResult contentResult_Summary = Assert.IsType<ContentResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(990311001L, countyId_SolarRadiation, 50, 1, null));
                GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult_Summary = Core.Convert.ToDiGi<GIS.Classes.BuildingSolarRadiationResult>(contentResult_Summary.Content)?.FirstOrDefault();
                Assert.NotNull(buildingSolarRadiationResult_Summary);
                Assert.All(buildingSolarRadiationResult_Summary.SurfaceSolarRadiations!, x => Assert.Null(x.ShadingFactors));

                Assert.IsType<NoContentResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(990311001L, countyId_SolarRadiation, 50, 2, null));
                Assert.IsType<NoContentResult>(await controller.GetSolarRadiationByBuildingModelIdAsync(990311002L, countyId_SolarRadiation, 50, 1, null));
            }
            finally
            {
                await CleanupSolarRadiationScratchAsync(connectionData_Main, connectionData_Storage);
            }
        }

        /// <summary>
        /// Verifies on the throwaway main test database that <c>gis/epwfile/item</c> answers the <c>DiGi-EPWFile-Id</c> header with the id of the row whose file the body holds, and a body byte-identical to the file as the converter reads it.
        /// <para>The Warsaw fixture is stored as a scratch row (<c>XUNIT-EPW-*</c>) far outside Poland, so the nearest row to that point is the scratch row, and removed before and after. Medium test (a live database server). Reference: DiGi.GIS.WebAPI#67.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task EPWFileController_Item_IdHeader()
        {
            const double x = -9999999;
            const double y = -9999999;

            (ConnectionData connectionData_Main, _) = SolarRadiationTestConnectionDatas();

            EPW.Classes.EPWFile? ePWFile_Fixture = EPW.Modify.Read(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw"));
            Assert.NotNull(ePWFile_Fixture);

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();
            Assert.True(await PostgreSQL.Create.TableAsync_EPWFile(npgsqlConnection));

            string commandText_Cleanup = $"DELETE FROM {PostgreSQL.Constants.TableName.EPWFile} WHERE name LIKE 'XUNIT-EPW-%';";
            await ExecuteSolarRadiationAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                int id;
                await using (NpgsqlCommand npgsqlCommand = new($"INSERT INTO {PostgreSQL.Constants.TableName.EPWFile} (name, x, y, object) VALUES ('XUNIT-EPW-2', @x, @y, @object) RETURNING id;", npgsqlConnection))
                {
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("x", NpgsqlDbType.Double) { Value = x });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("y", NpgsqlDbType.Double) { Value = y });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = ePWFile_Fixture.ToJsonObject()!.ToJsonString() });
                    id = (int)(await npgsqlCommand.ExecuteScalarAsync())!;
                }

                PostgreSQL.Classes.EPWFilePostgreSQLConverter ePWFilePostgreSQLConverter = new(connectionData_Main);

                using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
                EPWFileController controller = new(watcher, ePWFilePostgreSQLConverter);
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                ContentResult contentResult = Assert.IsType<ContentResult>(await controller.GetEPWFileAsync(x, y));
                Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), controller.HttpContext.Response.Headers[Constants.Header.EPWFileId].ToString());
                Assert.Equal((await ePWFilePostgreSQLConverter.GetEPWFileAsync(x, y))?.ToJsonObject()?.ToJsonString(), contentResult.Content);
            }
            finally
            {
                await ExecuteSolarRadiationAsync(npgsqlConnection, commandText_Cleanup);
            }
        }

        /// <summary>
        /// Builds a stored solar radiation controller, connected to the given test databases or to none, with a request carrying a bearer token for <paramref name="email"/> or no token.
        /// </summary>
        /// <param name="watcher">The configuration watcher.</param>
        /// <param name="connectionData_Main">The main test database, or null for no connection.</param>
        /// <param name="connectionData_Storage">The storage test database, or null for no connection.</param>
        /// <param name="email">The email of the signed-in user, or null for an anonymous request.</param>
        /// <returns>The controller.</returns>
        private static BuildingSolarRadiationController SolarRadiationController(GISWebAPIConfigurationFileWatcher watcher, ConnectionData? connectionData_Main, ConnectionData? connectionData_Storage, string? email)
        {
            SecurityKeyManager securityKeyManager = new();
            _ = securityKeyManager.Generate();
            TokenRevocationStore tokenRevocationStore = new();

            DefaultHttpContext httpContext = new();
            if (email is not null)
            {
                httpContext.Request.Headers.Authorization = "Bearer " + CreateToken(securityKeyManager, email);
            }

            BuildingSolarRadiationController controller = new(watcher, new PostgreSQL.Classes.BuildingSolarRadiationPostgreSQLConverter(connectionData_Storage), new PostgreSQL.Classes.Building2DPostgreSQLConverter(connectionData_Main), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(connectionData_Main), securityKeyManager, tokenRevocationStore);
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        /// <summary>
        /// Writes a configuration file that opens the key tier and sets <c>AllowUpdateSolarRadiationData</c>.
        /// </summary>
        /// <param name="allowUpdateSolarRadiationData">The value of the flag.</param>
        /// <returns>The path of the file.</returns>
        private static string SolarRadiationConfigurationFilePath(bool allowUpdateSolarRadiationData)
        {
            string result = System.IO.Path.GetTempFileName();
            System.IO.File.WriteAllLines(result,
            [
                $"{nameof(GISWebAPIConfigurationFileWatcher.Open)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateSolarRadiationData)}={(allowUpdateSolarRadiationData ? "true" : "false")}",
            ]);
            return result;
        }

        /// <summary>
        /// Builds a scratch surface with the given number of quantized shading factors.
        /// </summary>
        /// <param name="hourCount">The number of shading factors.</param>
        /// <returns>The surface.</returns>
        private static GIS.Classes.SurfaceSolarRadiation ScratchSurfaceSolarRadiation(int hourCount)
        {
            byte[] shadingFactors = new byte[hourCount];
            for (int i = 0; i < shadingFactors.Length; i++)
            {
                shadingFactors[i] = (byte)((i * 7) % 256);
            }

            return new GIS.Classes.SurfaceSolarRadiation("b1a5c1de-0000-4000-8000-000000000001", 12.5, new Geometry.Spatial.Classes.Vector3D(0, -1, 0), 0.8, 0.6, 650.25, 400.125, 200.0625, 50.0625, 900.5, 8128.125, shadingFactors);
        }

        /// <summary>
        /// Builds a scratch stored solar radiation result of building model 990311001 with two surfaces, an EPW weather source and an old creation time.
        /// </summary>
        /// <param name="countyId">The county part named in the body.</param>
        /// <param name="reference">The reference of the building.</param>
        /// <param name="userEmail">The user email the body claims.</param>
        /// <returns>The result.</returns>
        private static GIS.Classes.BuildingSolarRadiationResult ScratchSolarRadiationResult(int countyId, string? reference, string? userEmail)
        {
            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_1 = ScratchSurfaceSolarRadiation(Constants.SolarRadiation.HourCount);
            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_2 = new("b1a5c1de-0000-4000-8000-000000000002", 30, new Geometry.Spatial.Classes.Vector3D(0, 0, 1), 1, 1, 1000, 600, 300, 100, 1200, 30000, new byte[Constants.SolarRadiation.HourCount]);

            return new GIS.Classes.BuildingSolarRadiationResult(reference, countyId, 990311001L, 50, 1, GIS.Enums.WeatherSourceType.EPW, "17", null, null, userEmail, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), [surfaceSolarRadiation_1, surfaceSolarRadiation_2]);
        }
    }
}
