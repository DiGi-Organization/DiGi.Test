using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
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
        /// Pins the query parameter names of <c>gis/buildingheatdemand/summariesbycountyids</c>: <c>countyids</c>, <c>methodversion</c>, <c>yearbuiltbounds</c>, <c>method</c>, <c>inputvariant</c> (DiGi-Organization/DiGi.GIS.WebAPI#104) and <c>commandtimeout</c>. A renamed <c>[FromQuery]</c> is silently ignored and leaves the parameter at its default, so a production measure of a heat-demand run would summarise every county without saying so (<c>Coding - WebAPI Contracts.md</c> §1).
        /// <para>Reference: DiGi.GIS.WebAPI#101.</para>
        /// </summary>
        [Fact]
        public void BuildingHeatDemandController_Summaries_QueryParameterNames()
        {
            MethodInfo? methodInfo = typeof(BuildingHeatDemandController).GetMethod(nameof(BuildingHeatDemandController.GetSummariesByCountyIdsAsync));
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

            Assert.Equal(["countyids", "methodversion", "yearbuiltbounds", "method", "inputvariant", "commandtimeout"], names);
        }

        /// <summary>
        /// Pins the query parameter names of <c>gis/buildingheatdemand/byreferences</c>: <c>countyid</c>, <c>references</c>, <c>fallbackbyreference</c> and <c>commandtimeout</c>. A renamed <c>[FromQuery]</c> is silently ignored (<c>Coding - WebAPI Contracts.md</c> §1).
        /// <para>Reference: DiGi-Organization/DiGi.GIS.WebAPI#104.</para>
        /// </summary>
        [Fact]
        public void BuildingHeatDemandController_ByReferences_QueryParameterNames()
        {
            MethodInfo? methodInfo = typeof(BuildingHeatDemandController).GetMethod(nameof(BuildingHeatDemandController.GetByReferencesAsync));
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

            Assert.Equal(["countyid", "references", "fallbackbyreference", "commandtimeout"], names);
        }

        /// <summary>
        /// Verifies the failure answers of the heat-demand summary read without a database: a negative <c>commandtimeout</c> answers 400, non-ascending <c>yearbuiltbounds</c> answers 400, and a converter that cannot reach its database answers 500 - never an empty list, which would read as "nothing stored" (<c>Coding - WebAPI Contracts.md</c>).
        /// <para>Reference: DiGi.GIS.WebAPI#101.</para>
        /// </summary>
        [Fact]
        public async Task BuildingHeatDemandController_Summaries_Failures()
        {
            BuildingHeatDemandController controller = new(new BuildingSolarRadiationPostgreSQLConverter(null));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            Assert.IsType<BadRequestResult>(await controller.GetSummariesByCountyIdsAsync([1], null, null, commandTimeout: -1));

            Assert.IsType<BadRequestResult>(await controller.GetSummariesByCountyIdsAsync([1], null, [1967, 1946], commandTimeout: 30));

            Assert.IsType<BadRequestResult>(await controller.GetSummariesByCountyIdsAsync([1], null, null, method: -1, commandTimeout: 30));
            Assert.IsType<BadRequestResult>(await controller.GetSummariesByCountyIdsAsync([1], null, null, inputVariant: -1, commandTimeout: 30));

            ObjectResult objectResult = Assert.IsType<ObjectResult>(await controller.GetSummariesByCountyIdsAsync([1], null, null, commandTimeout: 30));
            Assert.Equal(500, objectResult.StatusCode);
        }

        /// <summary>
        /// Verifies the failure answers of the heat-demand comparison read without a database (DiGi-Organization/DiGi.GIS.WebAPI#104): a missing <c>countyid</c>, no reference, only blank references, more than 1000 distinct references and a negative <c>commandtimeout</c> answer 400; a converter that cannot reach its database answers 500 - never an empty list.
        /// </summary>
        [Fact]
        public async Task BuildingHeatDemandController_ByReferences_Failures()
        {
            BuildingHeatDemandController controller = new(new BuildingSolarRadiationPostgreSQLConverter(null));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            Assert.IsType<BadRequestResult>(await controller.GetByReferencesAsync(null, ["A"]));
            Assert.IsType<BadRequestResult>(await controller.GetByReferencesAsync(1, null));
            Assert.IsType<BadRequestResult>(await controller.GetByReferencesAsync(1, []));
            Assert.IsType<BadRequestResult>(await controller.GetByReferencesAsync(1, [" ", ""]));
            Assert.IsType<BadRequestResult>(await controller.GetByReferencesAsync(1, [.. Enumerable.Range(0, 1001).Select(x => "R" + x)]));
            Assert.IsType<BadRequestResult>(await controller.GetByReferencesAsync(1, ["A"], commandTimeout: -1));

            // 1000 distinct references, a duplicate among them, are accepted - and fail only at the missing database.
            ObjectResult objectResult = Assert.IsType<ObjectResult>(await controller.GetByReferencesAsync(1, [.. Enumerable.Range(0, 1000).Select(x => "R" + x), "R0"]));
            Assert.Equal(500, objectResult.StatusCode);
        }

        /// <summary>
        /// Verifies the heat-demand summary read end to end on the throwaway Storage test database: one stored result under the scratch part 990311 answers 200 with one entry for that part, method version, building type and band; a part holding nothing answers 200 with <c>[]</c>.
        /// <para>Medium test (one live database server): it drops and recreates the <c>building_heat_demand</c> table and partition, then reads the stored summary back. The quality counts and energy values depend on the stored <c>object</c> JSONB, which this fact controls directly. Reference: DiGi.GIS.WebAPI#101.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingHeatDemandController_Summaries_RoundTrip()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            BuildingHeatDemandController controller = new(new BuildingSolarRadiationPostgreSQLConverter(connectionData_Storage));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            await CleanupHeatDemandScratchAsync(connectionData_Storage);
            try
            {
                await SeedHeatDemandScratchAsync(connectionData_Storage);

                ContentResult contentResult = Assert.IsType<ContentResult>(await controller.GetSummariesByCountyIdsAsync([countyId_SolarRadiation], null, null, commandTimeout: 30));
                Assert.Equal("application/json", contentResult.ContentType);

                List<BuildingHeatDemandCountyResult>? buildingHeatDemandCountyResults = Core.Convert.ToDiGi<BuildingHeatDemandCountyResult>(contentResult.Content);
                Assert.NotNull(buildingHeatDemandCountyResults);
                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult = Assert.Single(buildingHeatDemandCountyResults);
                Assert.Equal(countyId_SolarRadiation, buildingHeatDemandCountyResult.CountyId);
                Assert.Equal(1, buildingHeatDemandCountyResult.Method);
                Assert.Equal(1, buildingHeatDemandCountyResult.MethodVersion);
                Assert.Equal(1, buildingHeatDemandCountyResult.InputVariant);
                Assert.Equal(1, buildingHeatDemandCountyResult.BuildingCount);
                Assert.Equal(0, buildingHeatDemandCountyResult.ShadedCount);
                Assert.Equal(1, buildingHeatDemandCountyResult.UnshadedCount);
                Assert.Equal(12000.0, buildingHeatDemandCountyResult.HeatingEnergyNeed);
                Assert.Equal(120.0, buildingHeatDemandCountyResult.HeatingEnergyNeedPerArea);
                Assert.Equal(8000.0, buildingHeatDemandCountyResult.DesignHeatLoad);
                Assert.NotNull(buildingHeatDemandCountyResult.FirstCreatedAt);
                Assert.NotNull(buildingHeatDemandCountyResult.LastCreatedAt);

                ContentResult contentResult_Empty = Assert.IsType<ContentResult>(await controller.GetSummariesByCountyIdsAsync([countyId_SolarRadiation + 1], null, null, commandTimeout: 30));
                Assert.Equal("[]", contentResult_Empty.Content);

                // The second stored method (Undefined, 0) is summarised only on request, never mixed in (DiGi-Organization/DiGi.GIS.WebAPI#104).
                ContentResult contentResult_Method = Assert.IsType<ContentResult>(await controller.GetSummariesByCountyIdsAsync([countyId_SolarRadiation], null, null, method: 0, commandTimeout: 30));
                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_Method = Assert.Single(Core.Convert.ToDiGi<BuildingHeatDemandCountyResult>(contentResult_Method.Content)!);
                Assert.Equal((0, 1, 7000.0), (buildingHeatDemandCountyResult_Method.Method, buildingHeatDemandCountyResult_Method.InputVariant, buildingHeatDemandCountyResult_Method.HeatingEnergyNeed));
                Assert.Equal("[]", Assert.IsType<ContentResult>(await controller.GetSummariesByCountyIdsAsync([countyId_SolarRadiation], null, null, inputVariant: 0, commandTimeout: 30)).Content);

                // The comparison route returns both, ordered by method and labelled by the key each is stored under - though the stored JSON names neither.
                ContentResult contentResult_References = Assert.IsType<ContentResult>(await controller.GetByReferencesAsync(countyId_SolarRadiation, ["XUNIT-HD-API", "XUNIT-HD-MISSING"]));
                Assert.Equal("application/json", contentResult_References.ContentType);
                JsonArray jsonArray = JsonNode.Parse(contentResult_References.Content!)!.AsArray();
                Assert.Equal(
                    [(0, 1, 1, 7000.0), (1, 1, 1, 12000.0)],
                    jsonArray.Select(x => ((int)x!["HeatDemandResult"]!["HeatDemandMethod"]!, (int)x["HeatDemandResult"]!["MethodVersion"]!, (int)x["HeatDemandInputVariant"]!, (double)x["HeatDemandResult"]!["HeatingEnergyNeed"]!)).ToList());

                // A sibling county part finds them only through the fallback.
                Assert.Equal("[]", Assert.IsType<ContentResult>(await controller.GetByReferencesAsync(countyId_SolarRadiation + 1, ["XUNIT-HD-API"])).Content);
                Assert.Equal(2, JsonNode.Parse(Assert.IsType<ContentResult>(await controller.GetByReferencesAsync(countyId_SolarRadiation + 1, ["XUNIT-HD-API"], fallbackByReference: true)).Content!)!.AsArray().Count);
            }
            finally
            {
                await CleanupHeatDemandScratchAsync(connectionData_Storage);
            }
        }

        /// <summary>
        /// Drops the <c>building_heat_demand</c> table and all its partitions on the storage test database. Called before seeding and again in <c>finally</c>.
        /// </summary>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task CleanupHeatDemandScratchAsync(ConnectionData connectionData_Storage)
        {
            await using NpgsqlConnection npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage)!;
            await npgsqlConnection.OpenAsync();
            await ExecuteHeatDemandAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.BuildingHeatDemand} CASCADE;");
        }

        /// <summary>
        /// Creates the <c>building_heat_demand</c> parent table with the current schema, the scratch partition, and one scratch row.
        /// </summary>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task SeedHeatDemandScratchAsync(ConnectionData connectionData_Storage)
        {
            await using NpgsqlConnection npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage)!;
            await npgsqlConnection.OpenAsync();

            await ExecuteHeatDemandAsync(npgsqlConnection, $@"
                CREATE TABLE {PostgreSQL.Constants.TableName.BuildingHeatDemand} (
                    id BIGINT GENERATED ALWAYS AS IDENTITY,
                    county_id INT NOT NULL,
                    building_model_id BIGINT NOT NULL,
                    reference TEXT NOT NULL,
                    method SMALLINT NOT NULL,
                    method_version INT NOT NULL,
                    input_variant SMALLINT NOT NULL,
                    weather_source_type INT NOT NULL,
                    weather_source_id TEXT,
                    solar_source INT NOT NULL,
                    solar_solver_version INT,
                    solar_radius DOUBLE PRECISION,
                    construction_flavour INT NOT NULL,
                    construction_library_version TEXT,
                    heating_energy_need DOUBLE PRECISION,
                    heating_energy_need_per_area DOUBLE PRECISION,
                    design_heat_load DOUBLE PRECISION,
                    ""object"" JSONB NOT NULL,
                    created_at timestamptz DEFAULT now(),
                    PRIMARY KEY (id, county_id)
                ) PARTITION BY LIST (county_id);");

            await ExecuteHeatDemandAsync(npgsqlConnection, $"CREATE TABLE {PostgreSQL.Constants.TableName.BuildingHeatDemand}_{countyId_SolarRadiation} PARTITION OF {PostgreSQL.Constants.TableName.BuildingHeatDemand} FOR VALUES IN ({countyId_SolarRadiation});");

            await ExecuteHeatDemandAsync(npgsqlConnection, $@"
                INSERT INTO {PostgreSQL.Constants.TableName.BuildingHeatDemand}
                    (county_id, building_model_id, reference, method, method_version, input_variant, weather_source_type, solar_source, construction_flavour,
                     heating_energy_need, heating_energy_need_per_area, design_heat_load, ""object"", created_at)
                VALUES
                    ({countyId_SolarRadiation}, 990311001, 'XUNIT-HD-API', 1, 1, 1, 0, 2, 0,
                     12000, 120, 8000, '{{""YearBuilt"":1975,""BuildingType"":0,""HeatDemandResult"":{{""HeatingEnergyNeed"":12000,""ExcludedComponents"":[],""GlazingFallbackCount"":0,""MissingIrradiationCount"":0}}}}'::jsonb, '2026-11-18T12:00:00Z'),
                    ({countyId_SolarRadiation}, 990311001, 'XUNIT-HD-API', 0, 1, 1, 0, 2, 0,
                     7000, 70, 5000, '{{""YearBuilt"":1975,""BuildingType"":0,""HeatDemandResult"":{{""HeatingEnergyNeed"":7000,""ExcludedComponents"":[],""GlazingFallbackCount"":0,""MissingIrradiationCount"":0}}}}'::jsonb, '2026-11-18T12:00:00Z');");
        }

        /// <summary>
        /// Executes one statement and discards its result.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to execute through.</param>
        /// <param name="commandText">The statement to execute.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task ExecuteHeatDemandAsync(NpgsqlConnection npgsqlConnection, string commandText)
        {
            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            await npgsqlCommand.ExecuteNonQueryAsync();
        }
    }
}
