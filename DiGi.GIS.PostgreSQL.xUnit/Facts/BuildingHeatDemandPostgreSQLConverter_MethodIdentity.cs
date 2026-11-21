using DiGi.GIS.Analytical.Energy.Classes;
using DiGi.GIS.Analytical.Energy.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Energy.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HeatDemandMethod = DiGi.Analytical.Building.Energy.Enums.HeatDemandMethod;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="BuildingHeatDemandPostgreSQLConverter"/> keys results by method, method version and input variant (DiGi-Organization/DiGi.GIS.PostgreSQL#152, Heat Demand Method Identity DiGi-Organization/DiGi.GIS.Analytical#19), on the throwaway storage test database.
        /// <para>One building is written under Monthly/Generated, under a second method and under a second input variant: three rows side by side. <c>Undefined</c> (0) stands in for the second method and input variant, which do not exist yet - an integer that names no member reads back from the JSON as <c>Undefined</c>, so 0 is the one other value that round-trips. The default read, the references a resumed run steps over and the county summary see only Monthly/Generated; a read for the other method or input variant sees only its own; the comparison read returns all three, each labelled by its own method, version and input variant.</para>
        /// <para>Medium test (a live database server): it writes the scratch partitions of 990311 and 990312 and drops them before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingHeatDemandPostgreSQLConverter_MethodIdentity()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            BuildingHeatDemandPostgreSQLConverter buildingHeatDemandPostgreSQLConverter = new(connectionData_Storage);

            await CleanupBuildingHeatDemandScratchAsync(connectionData_Storage);
            try
            {
                HeatDemandMethod heatDemandMethod_Other = HeatDemandMethod.Undefined;
                HeatDemandInputVariant heatDemandInputVariant_Other = HeatDemandInputVariant.Undefined;

                BuildingHeatDemandResult buildingHeatDemandResult_Monthly = ScratchBuildingHeatDemandResult("XUNIT-HD-ID", 990311021L, 1, 12000, 120);
                BuildingHeatDemandResult buildingHeatDemandResult_Method = MethodIdentity_Result(ScratchBuildingHeatDemandResult("XUNIT-HD-ID", 990311021L, 1, 7000, 70), heatDemandMethod_Other, HeatDemandInputVariant.Generated);
                BuildingHeatDemandResult buildingHeatDemandResult_Variant = MethodIdentity_Result(ScratchBuildingHeatDemandResult("XUNIT-HD-ID", 990311021L, 1, 9000, 90), HeatDemandMethod.Monthly, heatDemandInputVariant_Other);
                Assert.Equal(HeatDemandMethod.Monthly, buildingHeatDemandResult_Monthly.HeatDemandResult?.HeatDemandMethod);
                Assert.Equal(HeatDemandInputVariant.Generated, buildingHeatDemandResult_Monthly.HeatDemandInputVariant);
                Assert.Equal(heatDemandMethod_Other, buildingHeatDemandResult_Method.HeatDemandResult?.HeatDemandMethod);
                Assert.Equal(heatDemandInputVariant_Other, buildingHeatDemandResult_Variant.HeatDemandInputVariant);

                // Three keys, three rows; writing one again replaces its row.
                Assert.Equal(3, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, [buildingHeatDemandResult_Monthly, buildingHeatDemandResult_Method, buildingHeatDemandResult_Variant]));
                Assert.Equal(1, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, [buildingHeatDemandResult_Method]));

                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();
                Assert.Equal(3L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Energy.Constants.TableName.BuildingHeatDemand} WHERE county_id = {countyId_HeatDemand_A} AND building_model_id = 990311021;"));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Energy.Constants.TableName.BuildingHeatDemand} WHERE county_id = {countyId_HeatDemand_A} AND building_model_id = 990311021 AND method = 0 AND input_variant = 1;"));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Energy.Constants.TableName.BuildingHeatDemand} WHERE county_id = {countyId_HeatDemand_A} AND building_model_id = 990311021 AND method = 1 AND input_variant = 0;"));

                // The default read is Monthly/Generated; each other read sees only its own method or input variant.
                Assert.Equal(12000, Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-ID"], countyId_HeatDemand_A))!).HeatDemandResult?.HeatingEnergyNeed);
                Assert.Equal(7000, Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-ID"], countyId_HeatDemand_A, heatDemandMethod: heatDemandMethod_Other))!).HeatDemandResult?.HeatingEnergyNeed);
                Assert.Equal(9000, Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-ID"], countyId_HeatDemand_A, heatDemandInputVariant: heatDemandInputVariant_Other))!).HeatDemandResult?.HeatingEnergyNeed);
                Assert.Empty((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-ID"], countyId_HeatDemand_A, heatDemandMethod: heatDemandMethod_Other, heatDemandInputVariant: heatDemandInputVariant_Other))!);

                // The fallback to a sibling county part keeps the filter.
                Assert.Equal(7000, Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-ID"], countyId_HeatDemand_B, fallbackByReference: true, heatDemandMethod: heatDemandMethod_Other))!).HeatDemandResult?.HeatingEnergyNeed);

                // A resumed run steps over a building only for its own method and input variant.
                Assert.Equal(["XUNIT-HD-ID"], (await buildingHeatDemandPostgreSQLConverter.GetReferencesAsync(countyId_HeatDemand_A, methodVersion: 1))!);
                Assert.Equal(["XUNIT-HD-ID"], (await buildingHeatDemandPostgreSQLConverter.GetReferencesAsync(countyId_HeatDemand_A, heatDemandMethod: heatDemandMethod_Other))!);
                Assert.Empty((await buildingHeatDemandPostgreSQLConverter.GetReferencesAsync(countyId_HeatDemand_A, heatDemandMethod: heatDemandMethod_Other, heatDemandInputVariant: heatDemandInputVariant_Other))!);

                // The comparison read returns all three, labelled; a sibling part finds them only through the fallback.
                List<BuildingHeatDemandResult>? buildingHeatDemandResults_All = await buildingHeatDemandPostgreSQLConverter.GetAllItemsByReferencesAsync(["XUNIT-HD-ID", "XUNIT-HD-MISSING"], countyId_HeatDemand_A);
                Assert.NotNull(buildingHeatDemandResults_All);
                Assert.Equal(3, buildingHeatDemandResults_All.Count);
                Assert.Equal(
                    [(heatDemandMethod_Other, 1, HeatDemandInputVariant.Generated, 7000.0), (HeatDemandMethod.Monthly, 1, heatDemandInputVariant_Other, 9000.0), (HeatDemandMethod.Monthly, 1, HeatDemandInputVariant.Generated, 12000.0)],
                    buildingHeatDemandResults_All.ConvertAll(x => (x.HeatDemandResult!.HeatDemandMethod, x.HeatDemandResult.MethodVersion, x.HeatDemandInputVariant, x.HeatDemandResult.HeatingEnergyNeed)));
                Assert.Empty((await buildingHeatDemandPostgreSQLConverter.GetAllItemsByReferencesAsync(["XUNIT-HD-ID"], countyId_HeatDemand_B))!);
                Assert.Equal(3, (await buildingHeatDemandPostgreSQLConverter.GetAllItemsByReferencesAsync(["XUNIT-HD-ID"], countyId_HeatDemand_B, fallbackByReference: true))!.Count);

                // The summary never mixes them: by default Monthly/Generated only, each other one on request.
                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult = Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetSummariesByCountyIdsAsync([countyId_HeatDemand_A]))!);
                Assert.Equal((1, 1, 1, 12000.0), (buildingHeatDemandCountyResult.Method, buildingHeatDemandCountyResult.InputVariant, (int)buildingHeatDemandCountyResult.BuildingCount, buildingHeatDemandCountyResult.HeatingEnergyNeed));
                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_Method = Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetSummariesByCountyIdsAsync([countyId_HeatDemand_A], heatDemandMethod: heatDemandMethod_Other))!);
                Assert.Equal((0, 1, 7000.0), (buildingHeatDemandCountyResult_Method.Method, buildingHeatDemandCountyResult_Method.InputVariant, buildingHeatDemandCountyResult_Method.HeatingEnergyNeed));
                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_Variant = Assert.Single((await npgsqlConnection.BuildingHeatDemandCountyResultsAsync([countyId_HeatDemand_A], inputVariant: 0))!);
                Assert.Equal((1, 0, 9000.0), (buildingHeatDemandCountyResult_Variant.Method, buildingHeatDemandCountyResult_Variant.InputVariant, buildingHeatDemandCountyResult_Variant.HeatingEnergyNeed));
            }
            finally
            {
                await CleanupBuildingHeatDemandScratchAsync(connectionData_Storage);
            }
        }

        /// <summary>
        /// Verifies that <see cref="Energy.Create.TableAsync_BuildingHeatDemand"/> migrates a table created before the method and the input variant (DiGi-Organization/DiGi.GIS.PostgreSQL#152), on the throwaway storage test database.
        /// <para>A table of the old schema - key <c>(county_id, building_model_id, method_version)</c>, no <c>method</c> or <c>input_variant</c> - holding two rows is migrated: both columns exist, NOT NULL and without a default, every row holds 1 and 1, the old key is gone and the new one is there. A second call changes nothing. A write for a migrated row's key replaces it; a write of another input variant is a new row.</para>
        /// <para>Medium test (a live database server): it drops the <c>building_heat_demand</c> parent table before and leaves it recreated in the current schema, as the other heat demand facts do.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task TableAsync_BuildingHeatDemand_Migration()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();
            string tableName = Energy.Constants.TableName.BuildingHeatDemand;

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            try
            {
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {tableName} CASCADE;");
                await ExecuteAsync(npgsqlConnection, $@"
                    CREATE TABLE {tableName} (
                        id BIGINT GENERATED ALWAYS AS IDENTITY,
                        county_id INT NOT NULL,
                        building_model_id BIGINT NOT NULL,
                        reference TEXT NOT NULL,
                        method_version INT NOT NULL,
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
                        object JSONB NOT NULL,
                        created_at timestamptz DEFAULT now(),
                        PRIMARY KEY (id, county_id)
                    ) PARTITION BY LIST (county_id);
                    CREATE UNIQUE INDEX idx_{tableName}_key ON {tableName} (county_id, building_model_id, method_version) NULLS NOT DISTINCT;
                    CREATE TABLE {tableName}_{countyId_HeatDemand_A} PARTITION OF {tableName} FOR VALUES IN ({countyId_HeatDemand_A});");

                // Two rows of the old schema, their JSON without the method and the input variant, as stored before.
                foreach ((long buildingModelId, int methodVersion) in new (long, int)[] { (990311031L, 1), (990311031L, 2) })
                {
                    JsonObject jsonObject = ScratchBuildingHeatDemandResult("XUNIT-HD-MIG", buildingModelId, methodVersion, 5000, 50).ToJsonObject()!;
                    Assert.True(jsonObject.Remove(nameof(BuildingHeatDemandResult.HeatDemandInputVariant)));
                    Assert.True(((JsonObject)jsonObject[nameof(BuildingHeatDemandResult.HeatDemandResult)]!).Remove(nameof(DiGi.Analytical.Building.Energy.Classes.HeatDemandResult.HeatDemandMethod)));

                    await using NpgsqlCommand npgsqlCommand = new($"INSERT INTO {tableName} (county_id, building_model_id, reference, method_version, weather_source_type, solar_source, construction_flavour, heating_energy_need, object) VALUES ({countyId_HeatDemand_A}, {buildingModelId}, 'XUNIT-HD-MIG', {methodVersion}, 0, 2, 0, 5000, @object::jsonb);", npgsqlConnection);
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlTypes.NpgsqlDbType.Text) { Value = jsonObject.ToJsonString() });
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }

                Assert.True(await Energy.Create.TableAsync_BuildingHeatDemand(npgsqlConnection));
                Assert.True(await Energy.Create.TableAsync_BuildingHeatDemand(npgsqlConnection));

                foreach (string column in new string[] { "method", "input_variant" })
                {
                    Assert.Equal("NO", await ScalarAsync(npgsqlConnection, $"SELECT is_nullable FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{tableName}' AND column_name = '{column}';"));
                    Assert.Null(await ScalarAsync(npgsqlConnection, $"SELECT column_default FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{tableName}' AND column_name = '{column}';"));
                }

                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {tableName} WHERE method = 1 AND input_variant = 1;"));
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM pg_indexes WHERE schemaname = current_schema() AND indexname = 'idx_{tableName}_key';"));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM pg_indexes WHERE schemaname = current_schema() AND indexname = 'idx_{tableName}_identity';"));

                // The migrated rows read as Monthly/Generated, by column and by JSON.
                BuildingHeatDemandPostgreSQLConverter buildingHeatDemandPostgreSQLConverter = new(connectionData_Storage);
                BuildingHeatDemandResult buildingHeatDemandResult_Read = Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-MIG"], countyId_HeatDemand_A))!);
                Assert.Equal(2, buildingHeatDemandResult_Read.HeatDemandResult?.MethodVersion);
                Assert.Equal(HeatDemandMethod.Monthly, buildingHeatDemandResult_Read.HeatDemandResult?.HeatDemandMethod);
                Assert.Equal(HeatDemandInputVariant.Generated, buildingHeatDemandResult_Read.HeatDemandInputVariant);

                // A write for a migrated key replaces its row; another input variant is a new row.
                Assert.Equal(1, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, [ScratchBuildingHeatDemandResult("XUNIT-HD-MIG", 990311031L, 1, 5500, 55)]));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {tableName} WHERE building_model_id = 990311031;"));
                Assert.Equal(1, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, [MethodIdentity_Result(ScratchBuildingHeatDemandResult("XUNIT-HD-MIG", 990311031L, 1, 6000, 60), HeatDemandMethod.Monthly, HeatDemandInputVariant.Undefined)]));
                Assert.Equal(3L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {tableName} WHERE building_model_id = 990311031;"));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {tableName} CASCADE;");
                Assert.True(await Energy.Create.TableAsync_BuildingHeatDemand(npgsqlConnection));
            }
        }

        /// <summary>
        /// Gives a scratch result another method and input variant, through its JSON - Undefined included.
        /// </summary>
        /// <param name="buildingHeatDemandResult">The scratch result.</param>
        /// <param name="heatDemandMethod">The method of its heat demand.</param>
        /// <param name="heatDemandInputVariant">Its input variant.</param>
        /// <returns>The result with the method and the input variant.</returns>
        private static BuildingHeatDemandResult MethodIdentity_Result(BuildingHeatDemandResult buildingHeatDemandResult, HeatDemandMethod heatDemandMethod, HeatDemandInputVariant heatDemandInputVariant)
        {
            JsonObject jsonObject = buildingHeatDemandResult.ToJsonObject()!;
            jsonObject[nameof(BuildingHeatDemandResult.HeatDemandInputVariant)] = (int)heatDemandInputVariant;
            ((JsonObject)jsonObject[nameof(BuildingHeatDemandResult.HeatDemandResult)]!)[nameof(DiGi.Analytical.Building.Energy.Classes.HeatDemandResult.HeatDemandMethod)] = (int)heatDemandMethod;
            return new BuildingHeatDemandResult(jsonObject);
        }
    }
}
