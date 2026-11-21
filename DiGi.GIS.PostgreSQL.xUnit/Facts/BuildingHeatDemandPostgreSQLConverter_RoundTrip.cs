using DiGi.GIS.Analytical.Energy.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Energy.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The first scratch county part of the building heat demand facts. Only a partition key in the throwaway storage test database.
        /// </summary>
        private const int countyId_HeatDemand_A = 990311;

        /// <summary>
        /// The second scratch county part of the building heat demand facts, standing for a sibling polygon part of the same county.
        /// </summary>
        private const int countyId_HeatDemand_B = 990312;

        /// <summary>
        /// Verifies <see cref="BuildingHeatDemandPostgreSQLConverter"/> on the throwaway storage test database (DiGi.GIS.PostgreSQL#143):
        /// <para>The table creation is idempotent. Written results read back by reference with their values, under the county part they were written to. A second write for the same building and method version replaces the row; another method version is a second row, the higher read by default and the lower only when asked for. A read under a sibling county part finds a result only with <c>fallbackByReference</c>. The references and the per-part summary see exactly what was written, and a non-finite value is stored as a null column.</para>
        /// <para>Medium test (a live database server): it writes the scratch partitions of 990311 and 990312 and drops them before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingHeatDemandPostgreSQLConverter_RoundTrip()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            BuildingHeatDemandPostgreSQLConverter buildingHeatDemandPostgreSQLConverter = new(connectionData_Storage);

            await CleanupBuildingHeatDemandScratchAsync(connectionData_Storage);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();

                Assert.True(await Energy.Create.TableAsync_BuildingHeatDemand(npgsqlConnection));
                Assert.True(await Energy.Create.TableAsync_BuildingHeatDemand(npgsqlConnection));
                Assert.True(await Energy.Create.TableAsync_BuildingHeatDemand_Partition(npgsqlConnection, countyId_HeatDemand_A));
                Assert.True(await Energy.Create.TableAsync_BuildingHeatDemand_Partition(npgsqlConnection, countyId_HeatDemand_A));

                Assert.Empty(await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-1"], countyId_HeatDemand_A) ?? throw new InvalidOperationException("The read failed."));

                BuildingHeatDemandResult buildingHeatDemandResult_1 = ScratchBuildingHeatDemandResult("XUNIT-HD-1", 990311001L, 1, 12000, 120);
                BuildingHeatDemandResult buildingHeatDemandResult_2 = ScratchBuildingHeatDemandResult("XUNIT-HD-2", 990311002L, 1, 6000, double.NaN);
                Assert.Equal(2, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, [buildingHeatDemandResult_1, buildingHeatDemandResult_2, null]));
                Assert.Equal(0, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, []));

                List<BuildingHeatDemandResult>? buildingHeatDemandResults = await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-1", "XUNIT-HD-2", "XUNIT-HD-MISSING"], countyId_HeatDemand_A);
                Assert.NotNull(buildingHeatDemandResults);
                Assert.Equal(["XUNIT-HD-1", "XUNIT-HD-2"], buildingHeatDemandResults.Select(x => x.Reference).OrderBy(x => x));
                BuildingHeatDemandResult buildingHeatDemandResult_Read = buildingHeatDemandResults.Single(x => x.Reference == "XUNIT-HD-1");
                Assert.Equal(990311001L, buildingHeatDemandResult_Read.BuildingModelId);
                Assert.Equal(12000, buildingHeatDemandResult_Read.HeatDemandResult?.HeatingEnergyNeed);
                Assert.Equal(buildingHeatDemandResult_1.CreatedAt, buildingHeatDemandResult_Read.CreatedAt);
                Assert.Null(await ScalarAsync(npgsqlConnection, $"SELECT heating_energy_need_per_area FROM {Energy.Constants.TableName.BuildingHeatDemand} WHERE county_id = {countyId_HeatDemand_A} AND building_model_id = 990311002;"));

                // A write for the same building and method version replaces the row; another method version is a second row.
                Assert.Equal(1, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, [ScratchBuildingHeatDemandResult("XUNIT-HD-1", 990311001L, 1, 15000, 150)]));
                Assert.Equal(1, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, [ScratchBuildingHeatDemandResult("XUNIT-HD-1", 990311001L, 2, 9000, 90)]));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Energy.Constants.TableName.BuildingHeatDemand} WHERE county_id = {countyId_HeatDemand_A} AND building_model_id = 990311001;"));
                Assert.Equal(9000, Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-1"], countyId_HeatDemand_A))!).HeatDemandResult?.HeatingEnergyNeed);
                Assert.Equal(15000, Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-1"], countyId_HeatDemand_A, methodVersion: 1))!).HeatDemandResult?.HeatingEnergyNeed);

                // A sibling county part finds a result only through the fallback.
                Assert.Empty((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-2"], countyId_HeatDemand_B))!);
                Assert.Equal(6000, Assert.Single((await buildingHeatDemandPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-HD-2"], countyId_HeatDemand_B, fallbackByReference: true))!).HeatDemandResult?.HeatingEnergyNeed);

                // What a resumed run steps over, and what the summary counts.
                Assert.Equal(["XUNIT-HD-1", "XUNIT-HD-2"], (await buildingHeatDemandPostgreSQLConverter.GetReferencesAsync(countyId_HeatDemand_A, methodVersion: 1))!.OrderBy(x => x));
                Assert.Equal(["XUNIT-HD-1"], (await buildingHeatDemandPostgreSQLConverter.GetReferencesAsync(countyId_HeatDemand_A, methodVersion: 2))!);
                Assert.Empty((await buildingHeatDemandPostgreSQLConverter.GetReferencesAsync(countyId_HeatDemand_B))!);

                List<BuildingHeatDemandCountyResult>? buildingHeatDemandCountyResults = await buildingHeatDemandPostgreSQLConverter.GetSummariesByCountyIdsAsync([countyId_HeatDemand_A, countyId_HeatDemand_B]);
                Assert.NotNull(buildingHeatDemandCountyResults);
                Assert.Equal(2, buildingHeatDemandCountyResults.Count);
                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_1 = buildingHeatDemandCountyResults.Single(x => x.MethodVersion == 1);
                Assert.Equal(countyId_HeatDemand_A, buildingHeatDemandCountyResult_1.CountyId);
                Assert.Equal(2L, buildingHeatDemandCountyResult_1.BuildingCount);
                Assert.Equal(21000, buildingHeatDemandCountyResult_1.HeatingEnergyNeed);
                Assert.Equal(150, buildingHeatDemandCountyResult_1.HeatingEnergyNeedPerArea);
                Assert.Equal(1L, buildingHeatDemandCountyResults.Single(x => x.MethodVersion == 2).BuildingCount);
            }
            finally
            {
                await CleanupBuildingHeatDemandScratchAsync(connectionData_Storage);
            }
        }

        /// <summary>
        /// Verifies the batch read of <see cref="BuildingSolarRadiationPostgreSQLConverter"/> the heat demand run uses (DiGi.GIS.PostgreSQL#143): with <c>includeShadingFactors</c> every surface carries its factors byte for byte, without it none does.
        /// <para>Medium test (a live database server): it writes the scratch partition of 990301 and drops it before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationPostgreSQLConverter_GetItemsByReferences_ShadingFactors()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            BuildingSolarRadiationPostgreSQLConverter buildingSolarRadiationPostgreSQLConverter = new(connectionData_Storage);

            await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);
            try
            {
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult = ScratchBuildingSolarRadiationResult("XUNIT-SR-HD", 990301011L, "17", null, 1.0, 180, 270);
                Assert.NotNull(await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(countyId_SolarRadiation_A, buildingSolarRadiationResult));

                List<GIS.Classes.BuildingSolarRadiationResult>? buildingSolarRadiationResults_Factors = await buildingSolarRadiationPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-SR-HD"], countyId_SolarRadiation_A, radius: 50.0, includeShadingFactors: true);
                AssertBuildingSolarRadiationResult(buildingSolarRadiationResult, Assert.Single(buildingSolarRadiationResults_Factors!), true);

                List<GIS.Classes.BuildingSolarRadiationResult>? buildingSolarRadiationResults_Summary = await buildingSolarRadiationPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-SR-HD"], countyId_SolarRadiation_A, radius: 50.0);
                AssertBuildingSolarRadiationResult(buildingSolarRadiationResult, Assert.Single(buildingSolarRadiationResults_Summary!), false);
            }
            finally
            {
                await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);
            }
        }

        /// <summary>
        /// Drops the scratch partitions of the building heat demand facts on the storage test database. Called before writing and again in <c>finally</c>; the parent table is left in place, empty of scratch rows.
        /// </summary>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task CleanupBuildingHeatDemandScratchAsync(ConnectionData connectionData_Storage)
        {
            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            foreach (int countyId in new int[] { countyId_HeatDemand_A, countyId_HeatDemand_B })
            {
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Energy.Constants.TableName.BuildingHeatDemand}_{countyId};");
            }
        }

        /// <summary>
        /// Builds a scratch building heat demand result carrying the values the table's columns take.
        /// </summary>
        /// <param name="reference">The reference of the scratch building.</param>
        /// <param name="buildingModelId">The id of the scratch building model.</param>
        /// <param name="methodVersion">The method version of the heat demand.</param>
        /// <param name="heatingEnergyNeed">The annual energy need for space heating, in kWh.</param>
        /// <param name="heatingEnergyNeedPerArea">The annual energy need per reference floor area, in kWh/m².</param>
        /// <returns>The result.</returns>
        private static BuildingHeatDemandResult ScratchBuildingHeatDemandResult(string reference, long buildingModelId, int methodVersion, double heatingEnergyNeed, double heatingEnergyNeedPerArea)
        {
            DiGi.Analytical.Building.Energy.Classes.HeatDemandResult heatDemandResult = new(heatingEnergyNeed, heatingEnergyNeedPerArea, [.. Enumerable.Repeat(heatingEnergyNeed / 12, 12)], null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, 0, 100, 300, 20, 8000, 6000, 2000, "scratch", DiGi.Analytical.Building.Energy.Enums.HeatDemandMethod.Monthly, methodVersion, null, null, 0, 0);
            return new BuildingHeatDemandResult(reference, countyId_HeatDemand_A, buildingModelId, GIS.Enums.WeatherSourceType.EPW, "17", GIS.Analytical.Energy.Enums.HeatDemandSolarSource.Unshaded, null, null, 0, 0, DiGi.Analytical.Building.Regulation.Enums.ConstructionFlavour.TypicalAsBuilt, "1.7.1", "TABULA", DiGi.Analytical.Building.Regulation.Enums.BuildingType.SingleFamily, 1975, GIS.Enums.YearBuiltSource.User, null, new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero), heatDemandResult);
        }
    }
}
