using DiGi.Analytical.Building.Regulation.Enums;
using DiGi.GIS.Analytical.Energy.Classes;
using DiGi.GIS.Analytical.Energy.Enums;
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
        /// Verifies <see cref="Query.BuildingHeatDemandCountyResultsAsync"/> on the throwaway storage test database (DiGi.GIS.PostgreSQL#146): one row per county part, method version, building type and year-built band, with counts, sums, mean, quantiles and quality counts matching values computed by hand.
        /// <para>Part 990311, method version 1 - single-family: A 1950 shaded, 200 kWh/(m2a), two excluded components; C 1960 unshaded, 180; B 1975 unshaded, 150, three glazing fallbacks, one surface without irradiation and <c>ExcludedComponents</c> rewritten to JSON null; D no year built, 100. Multi-family: E 2005 unshaded, 80, two glazing fallbacks; F 2005 shaded, 90, one excluded component. Method version 2: one single-family building. With the bounds 1946, 1967, 1986 the single-family rows are 1946-1966 (A, C), 1967-1985 (B) and the no-year band (D) last; the multi-family row is 1986 up (E, F). Without bounds every year is band 0. The method-version filter, a named part without results, the Energy converter's delegation and a missing table (an empty list) are covered too.</para>
        /// <para>Medium test (a live database server): it writes the scratch partitions of 990311 and 990312 and drops them before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingHeatDemandCountyResults()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            BuildingHeatDemandPostgreSQLConverter buildingHeatDemandPostgreSQLConverter = new(connectionData_Storage);

            await CleanupBuildingHeatDemandScratchAsync(connectionData_Storage);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();

                List<BuildingHeatDemandResult> buildingHeatDemandResults =
                [
                    SummaryBuildingHeatDemandResult("XUNIT-HDS-A", 990311101L, 1, BuildingType.SingleFamily, 1950, HeatDemandSolarSource.Shaded, 200, 20000, 10000, ["x", "y"], 0, 0),
                    SummaryBuildingHeatDemandResult("XUNIT-HDS-B", 990311102L, 1, BuildingType.SingleFamily, 1975, HeatDemandSolarSource.Unshaded, 150, 15000, 8000, ["w"], 3, 1),
                    SummaryBuildingHeatDemandResult("XUNIT-HDS-C", 990311103L, 1, BuildingType.SingleFamily, 1960, HeatDemandSolarSource.Unshaded, 180, 18000, 9000, null, 0, 0),
                    SummaryBuildingHeatDemandResult("XUNIT-HDS-D", 990311104L, 1, BuildingType.SingleFamily, null, HeatDemandSolarSource.Unshaded, 100, 10000, 5000, null, 0, 0),
                    SummaryBuildingHeatDemandResult("XUNIT-HDS-E", 990311105L, 1, BuildingType.MultiFamily, 2005, HeatDemandSolarSource.Unshaded, 80, 80000, 40000, null, 2, 0),
                    SummaryBuildingHeatDemandResult("XUNIT-HDS-F", 990311106L, 1, BuildingType.MultiFamily, 2005, HeatDemandSolarSource.Shaded, 90, 90000, 45000, ["z"], 0, 0),
                    SummaryBuildingHeatDemandResult("XUNIT-HDS-A", 990311101L, 2, BuildingType.SingleFamily, 1950, HeatDemandSolarSource.Shaded, 300, 30000, 11000, null, 0, 0),
                ];
                Assert.Equal(7, await buildingHeatDemandPostgreSQLConverter.UpdateAsync(countyId_HeatDemand_A, buildingHeatDemandResults));

                // A stored JSON null must count as no excluded component, not fail the summary.
                await ExecuteAsync(npgsqlConnection, $"UPDATE {Constants.TableName.BuildingHeatDemand} SET object = jsonb_set(object, '{{HeatDemandResult,ExcludedComponents}}', 'null'::jsonb) WHERE county_id = {countyId_HeatDemand_A} AND building_model_id = 990311102;");
                Assert.Equal("null", (await ScalarAsync(npgsqlConnection, $"SELECT jsonb_typeof(object->'HeatDemandResult'->'ExcludedComponents') FROM {Constants.TableName.BuildingHeatDemand} WHERE county_id = {countyId_HeatDemand_A} AND building_model_id = 990311102;"))?.ToString());

                // With bounds, method version 1.
                List<BuildingHeatDemandCountyResult>? buildingHeatDemandCountyResults = await npgsqlConnection.BuildingHeatDemandCountyResultsAsync([countyId_HeatDemand_A, countyId_HeatDemand_B], 1, [1986, 1946, 1967, 1946]);
                Assert.NotNull(buildingHeatDemandCountyResults);
                Assert.Equal(4, buildingHeatDemandCountyResults.Count);
                Assert.All(buildingHeatDemandCountyResults, x => Assert.Equal(countyId_HeatDemand_A, x.CountyId));
                Assert.All(buildingHeatDemandCountyResults, x => Assert.Equal(1, x.MethodVersion));
                Assert.Equal([(BuildingType.SingleFamily, (int?)1), (BuildingType.SingleFamily, 2), (BuildingType.SingleFamily, null), (BuildingType.MultiFamily, 3)], buildingHeatDemandCountyResults.Select(x => (x.BuildingType, x.YearBuiltBandIndex)));

                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_1946 = buildingHeatDemandCountyResults[0];
                Assert.Equal((short?)1946, buildingHeatDemandCountyResult_1946.YearBuiltMin);
                Assert.Equal((short?)1966, buildingHeatDemandCountyResult_1946.YearBuiltMax);
                Assert.Equal(2L, buildingHeatDemandCountyResult_1946.BuildingCount);
                Assert.Equal(1L, buildingHeatDemandCountyResult_1946.ShadedCount);
                Assert.Equal(1L, buildingHeatDemandCountyResult_1946.UnshadedCount);
                Assert.Equal(38000, buildingHeatDemandCountyResult_1946.HeatingEnergyNeed);
                Assert.Equal(190, buildingHeatDemandCountyResult_1946.HeatingEnergyNeedPerArea, 9);
                Assert.Equal(182, buildingHeatDemandCountyResult_1946.HeatingEnergyNeedPerArea_P10, 9);
                Assert.Equal(190, buildingHeatDemandCountyResult_1946.HeatingEnergyNeedPerArea_P50, 9);
                Assert.Equal(198, buildingHeatDemandCountyResult_1946.HeatingEnergyNeedPerArea_P90, 9);
                Assert.Equal(19000, buildingHeatDemandCountyResult_1946.DesignHeatLoad);
                Assert.Equal(1L, buildingHeatDemandCountyResult_1946.ExcludedComponentBuildingCount);
                Assert.Equal(2L, buildingHeatDemandCountyResult_1946.ExcludedComponentCount);
                Assert.Equal(0L, buildingHeatDemandCountyResult_1946.GlazingFallbackBuildingCount);
                Assert.Equal(0L, buildingHeatDemandCountyResult_1946.MissingIrradiationBuildingCount);
                Assert.Equal(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero), buildingHeatDemandCountyResult_1946.FirstCreatedAt);
                Assert.Equal(buildingHeatDemandCountyResult_1946.FirstCreatedAt, buildingHeatDemandCountyResult_1946.LastCreatedAt);

                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_1967 = buildingHeatDemandCountyResults[1];
                Assert.Equal((short?)1967, buildingHeatDemandCountyResult_1967.YearBuiltMin);
                Assert.Equal((short?)1985, buildingHeatDemandCountyResult_1967.YearBuiltMax);
                Assert.Equal(1L, buildingHeatDemandCountyResult_1967.BuildingCount);
                Assert.Equal(1L, buildingHeatDemandCountyResult_1967.UnshadedCount);
                Assert.Equal(0L, buildingHeatDemandCountyResult_1967.ExcludedComponentBuildingCount);
                Assert.Equal(0L, buildingHeatDemandCountyResult_1967.ExcludedComponentCount);
                Assert.Equal(1L, buildingHeatDemandCountyResult_1967.GlazingFallbackBuildingCount);
                Assert.Equal(3L, buildingHeatDemandCountyResult_1967.GlazingFallbackCount);
                Assert.Equal(1L, buildingHeatDemandCountyResult_1967.MissingIrradiationBuildingCount);
                Assert.Equal(150, buildingHeatDemandCountyResult_1967.HeatingEnergyNeedPerArea_P50, 9);

                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_NoYear = buildingHeatDemandCountyResults[2];
                Assert.Null(buildingHeatDemandCountyResult_NoYear.YearBuiltMin);
                Assert.Null(buildingHeatDemandCountyResult_NoYear.YearBuiltMax);
                Assert.Equal(1L, buildingHeatDemandCountyResult_NoYear.BuildingCount);
                Assert.Equal(10000, buildingHeatDemandCountyResult_NoYear.HeatingEnergyNeed);

                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_MultiFamily = buildingHeatDemandCountyResults[3];
                Assert.Equal((short?)1986, buildingHeatDemandCountyResult_MultiFamily.YearBuiltMin);
                Assert.Null(buildingHeatDemandCountyResult_MultiFamily.YearBuiltMax);
                Assert.Equal(2L, buildingHeatDemandCountyResult_MultiFamily.BuildingCount);
                Assert.Equal(1L, buildingHeatDemandCountyResult_MultiFamily.ShadedCount);
                Assert.Equal(170000, buildingHeatDemandCountyResult_MultiFamily.HeatingEnergyNeed);
                Assert.Equal(85, buildingHeatDemandCountyResult_MultiFamily.HeatingEnergyNeedPerArea, 9);
                Assert.Equal(85000, buildingHeatDemandCountyResult_MultiFamily.DesignHeatLoad);
                Assert.Equal(1L, buildingHeatDemandCountyResult_MultiFamily.ExcludedComponentCount);
                Assert.Equal(1L, buildingHeatDemandCountyResult_MultiFamily.GlazingFallbackBuildingCount);
                Assert.Equal(2L, buildingHeatDemandCountyResult_MultiFamily.GlazingFallbackCount);

                // Without bounds every year is band 0; every method version, one row each.
                List<BuildingHeatDemandCountyResult>? buildingHeatDemandCountyResults_All = await npgsqlConnection.BuildingHeatDemandCountyResultsAsync([countyId_HeatDemand_A]);
                Assert.NotNull(buildingHeatDemandCountyResults_All);
                Assert.Equal([(1, BuildingType.SingleFamily, (int?)0, 3L), (1, BuildingType.SingleFamily, null, 1L), (1, BuildingType.MultiFamily, 0, 2L), (2, BuildingType.SingleFamily, 0, 1L)], buildingHeatDemandCountyResults_All.Select(x => (x.MethodVersion, x.BuildingType, x.YearBuiltBandIndex, x.BuildingCount)));
                Assert.All(buildingHeatDemandCountyResults_All, x => Assert.Null(x.YearBuiltMin));
                Assert.All(buildingHeatDemandCountyResults_All, x => Assert.Null(x.YearBuiltMax));

                // The method-version filter, and a named part without results.
                BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_Version2 = Assert.Single((await npgsqlConnection.BuildingHeatDemandCountyResultsAsync([countyId_HeatDemand_A], 2))!);
                Assert.Equal(300, buildingHeatDemandCountyResult_Version2.HeatingEnergyNeedPerArea, 9);
                Assert.Empty((await npgsqlConnection.BuildingHeatDemandCountyResultsAsync([countyId_HeatDemand_B]))!);

                // The Energy converter delegates to the same summary.
                List<BuildingHeatDemandCountyResult>? buildingHeatDemandCountyResults_Converter = await buildingHeatDemandPostgreSQLConverter.GetSummariesByCountyIdsAsync([countyId_HeatDemand_A], 1, [1946, 1967, 1986]);
                Assert.NotNull(buildingHeatDemandCountyResults_Converter);
                Assert.Equal(buildingHeatDemandCountyResults.Select(x => (x.BuildingType, x.YearBuiltBandIndex, x.BuildingCount, x.HeatingEnergyNeed)), buildingHeatDemandCountyResults_Converter.Select(x => (x.BuildingType, x.YearBuiltBandIndex, x.BuildingCount, x.HeatingEnergyNeed)));

                // A missing table answers an empty list: the session looks for it in a schema that does not hold it.
                await ExecuteAsync(npgsqlConnection, "SET search_path TO xunit_no_such_schema;");
                Assert.Empty((await npgsqlConnection.BuildingHeatDemandCountyResultsAsync(null))!);
                await ExecuteAsync(npgsqlConnection, "RESET search_path;");

                Assert.Null(await ((NpgsqlConnection?)null).BuildingHeatDemandCountyResultsAsync(null));
            }
            finally
            {
                await CleanupBuildingHeatDemandScratchAsync(connectionData_Storage);
            }
        }

        /// <summary>
        /// Verifies that a heat demand county summary carries every member through the constructor, the copy constructor and a JSON round trip, and passes <c>SerializationCheck</c> (DiGi.GIS.PostgreSQL#146). Every member holds a distinct value, so a member dropped by a constructor fails here; the open ends of a band stay null.
        /// </summary>
        [Fact]
        public void BuildingHeatDemandCountyResult_Serialization()
        {
            DateTimeOffset firstCreatedAt = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
            DateTimeOffset lastCreatedAt = new(2026, 11, 18, 13, 41, 0, TimeSpan.Zero);
            BuildingHeatDemandCountyResult buildingHeatDemandCountyResult = new(55417, 1, BuildingType.Terraced, 2, 1967, 1985, 1000, 300, 700, 1.5e7, 151.5, 92.5, 140.25, 233.75, 9.5e6, 12, 34, 56, 78, 9, firstCreatedAt, lastCreatedAt);

            BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_Copy = new(buildingHeatDemandCountyResult);
            BuildingHeatDemandCountyResult? buildingHeatDemandCountyResult_Json = Core.Convert.ToDiGi<BuildingHeatDemandCountyResult>(Core.Convert.ToSystem_String(buildingHeatDemandCountyResult))?.FirstOrDefault();
            Assert.NotNull(buildingHeatDemandCountyResult_Json);

            foreach (BuildingHeatDemandCountyResult x in new BuildingHeatDemandCountyResult[] { buildingHeatDemandCountyResult_Copy, buildingHeatDemandCountyResult_Json })
            {
                Assert.Equal(55417, x.CountyId);
                Assert.Equal(1, x.MethodVersion);
                Assert.Equal(BuildingType.Terraced, x.BuildingType);
                Assert.Equal(2, x.YearBuiltBandIndex);
                Assert.Equal((short?)1967, x.YearBuiltMin);
                Assert.Equal((short?)1985, x.YearBuiltMax);
                Assert.Equal(1000L, x.BuildingCount);
                Assert.Equal(300L, x.ShadedCount);
                Assert.Equal(700L, x.UnshadedCount);
                Assert.Equal(1.5e7, x.HeatingEnergyNeed);
                Assert.Equal(151.5, x.HeatingEnergyNeedPerArea);
                Assert.Equal(92.5, x.HeatingEnergyNeedPerArea_P10);
                Assert.Equal(140.25, x.HeatingEnergyNeedPerArea_P50);
                Assert.Equal(233.75, x.HeatingEnergyNeedPerArea_P90);
                Assert.Equal(9.5e6, x.DesignHeatLoad);
                Assert.Equal(12L, x.ExcludedComponentBuildingCount);
                Assert.Equal(34L, x.ExcludedComponentCount);
                Assert.Equal(56L, x.GlazingFallbackBuildingCount);
                Assert.Equal(78L, x.GlazingFallbackCount);
                Assert.Equal(9L, x.MissingIrradiationBuildingCount);
                Assert.Equal(firstCreatedAt, x.FirstCreatedAt);
                Assert.Equal(lastCreatedAt, x.LastCreatedAt);
            }

            Core.xUnit.Query.SerializationCheck(buildingHeatDemandCountyResult);

            BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_Open = new(55417, 1, BuildingType.SingleFamily, null, null, null, 1, 0, 1, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0, 0, 0, 0, 0, null, null);
            BuildingHeatDemandCountyResult buildingHeatDemandCountyResult_Open_Copy = new(buildingHeatDemandCountyResult_Open);
            Assert.Null(buildingHeatDemandCountyResult_Open_Copy.YearBuiltBandIndex);
            Assert.Null(buildingHeatDemandCountyResult_Open_Copy.YearBuiltMin);
            Assert.True(double.IsNaN(buildingHeatDemandCountyResult_Open_Copy.HeatingEnergyNeedPerArea_P50));
            Core.xUnit.Query.SerializationCheck(buildingHeatDemandCountyResult_Open);
        }

        /// <summary>
        /// Builds a scratch building heat demand result for the summary fact, carrying the values the summary groups and counts by.
        /// </summary>
        /// <param name="reference">The reference of the scratch building.</param>
        /// <param name="buildingModelId">The id of the scratch building model.</param>
        /// <param name="methodVersion">The method version of the heat demand.</param>
        /// <param name="buildingType">The building type the calculation used.</param>
        /// <param name="yearBuilt">The year built, or null for none.</param>
        /// <param name="heatDemandSolarSource">Where the irradiation came from.</param>
        /// <param name="heatingEnergyNeedPerArea">The annual energy need per reference floor area, in kWh/m².</param>
        /// <param name="heatingEnergyNeed">The annual energy need for space heating, in kWh.</param>
        /// <param name="designHeatLoad">The design heat load, in W.</param>
        /// <param name="excludedComponents">The components excluded for an unknown thermal transmittance, or null.</param>
        /// <param name="glazingFallbackCount">The number of glazing fallbacks.</param>
        /// <param name="missingIrradiationCount">The number of surfaces without irradiation.</param>
        /// <returns>The result.</returns>
        private static BuildingHeatDemandResult SummaryBuildingHeatDemandResult(string reference, long buildingModelId, int methodVersion, BuildingType buildingType, short? yearBuilt, HeatDemandSolarSource heatDemandSolarSource, double heatingEnergyNeedPerArea, double heatingEnergyNeed, double designHeatLoad, List<string>? excludedComponents, int glazingFallbackCount, int missingIrradiationCount)
        {
            DiGi.Analytical.Building.Energy.Classes.HeatDemandResult heatDemandResult = new(heatingEnergyNeed, heatingEnergyNeedPerArea, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, 0, 100, 300, 20, designHeatLoad, designHeatLoad, 0, "scratch", DiGi.Analytical.Building.Energy.Enums.HeatDemandMethod.Monthly, methodVersion, null, excludedComponents, glazingFallbackCount, missingIrradiationCount);
            return new BuildingHeatDemandResult(reference, countyId_HeatDemand_A, buildingModelId, GIS.Enums.WeatherSourceType.EPW, "17", heatDemandSolarSource, null, null, 0, 0, ConstructionFlavour.TypicalAsBuilt, "1.7.1", "TABULA", buildingType, yearBuilt, yearBuilt is null ? null : GIS.Enums.YearBuiltSource.User, null, DiGi.GIS.Analytical.Energy.Enums.HeatDemandInputVariant.Generated, new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero), heatDemandResult);
        }
    }
}
