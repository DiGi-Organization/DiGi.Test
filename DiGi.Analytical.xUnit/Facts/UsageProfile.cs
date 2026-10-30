using DiGi.Analytical.Building.HVAC.Enums;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests Building.HVAC.Create.UsageProfile against Dz.U. 2015 poz. 376, Annex 1, Tables 23-27, the heating temperatures of the technical conditions (§ 134 ust. 2) and the ASHRAE 90.1 data that fills the gaps (DiGi-Organization/DiGi.Analytical#9).
        /// <para>The office internal gain at the standard P1 = 0.6, β = 0.3 is the 5.68 W/m2 of Table 26; every other β-dependent type takes the ASHRAE occupancy hours a week of Tables B103 / 168, which for the ASHRAE office is 48.6 / 168 = 0.289 against the methodology's 0.3.</para>
        /// <para>Every value of every profile has a source tag, and the uncited ones are exactly the residential floor area per person and the production ventilation and hot-water correction.</para>
        /// </summary>
        [Fact]
        public void UsageProfile()
        {
            const double tolerance = 1e-6;

            Assert.Null(Building.HVAC.Create.UsageProfile(UsageType.Undefined));

            // Type, heating set-point, outside airflow, V_Wi, k_R, constant q_int (NaN when it depends on β), ASHRAE hours a week (NaN for the constant types).
            (UsageType UsageType, double HeatingSetPoint, double OutsideAirPerArea, double DomesticHotWater, double DomesticHotWaterCorrection, double InternalHeatGain, double Hours)[] expectations =
            [
                (UsageType.SingleFamily, 20.0, 0.31, 1.40, 0.90, 6.8, double.NaN),
                (UsageType.MultiFamily, 20.0, 0.32, 2.00, 0.90, 7.1, double.NaN),
                (UsageType.CollectiveResidence, 20.0, 0.42, 3.75, 0.60, double.NaN, 96.4),
                (UsageType.Office, 20.0, 0.56, 0.35, 0.70, 5.68, double.NaN),
                (UsageType.Education, 20.0, 0.56, 0.80, 0.55, double.NaN, 36.0),
                (UsageType.Healthcare, 20.0, 0.42, 6.50, 1.00, 8.0, double.NaN),
                (UsageType.Gastronomy, 20.0, 0.42, 2.50, 0.80, double.NaN, 49.75),
                (UsageType.Sport, 16.0, 0.42, 0.25, 0.50, double.NaN, 50.0),
                (UsageType.Retail, 20.0, 0.33, 0.60, 0.78, double.NaN, 46.3),
                (UsageType.Storage, 12.0, 0.08, 0.10, 0.70, double.NaN, 35.2),
            ];

            Dictionary<UsageType, (double Use, double Off)> internalHeatGains_Beta = new()
            {
                [UsageType.CollectiveResidence] = (6.0, 2.0),
                [UsageType.Education] = (12.0, 1.0),
                [UsageType.Gastronomy] = (10.0, 1.0),
                [UsageType.Sport] = (9.0, 1.0),
                [UsageType.Retail] = (10.0, 1.0),
                [UsageType.Storage] = (2.0, 1.0),
            };

            foreach ((UsageType usageType, double heatingSetPoint, double outsideAirPerArea, double domesticHotWater, double domesticHotWaterCorrection, double internalHeatGain, double hours) in expectations)
            {
                Building.HVAC.Classes.UsageProfile? usageProfile = Building.HVAC.Create.UsageProfile(usageType);
                Assert.NotNull(usageProfile);
                Assert.Equal(usageType, usageProfile.UsageType);
                Assert.Equal(heatingSetPoint, usageProfile.HeatingSetPoint, tolerance);
                Assert.Equal(outsideAirPerArea, usageProfile.OutsideAirPerArea, tolerance);
                Assert.Equal(domesticHotWater, usageProfile.DomesticHotWater, tolerance);
                Assert.Equal(domesticHotWaterCorrection, usageProfile.DomesticHotWaterCorrection, tolerance);
                Assert.Equal(230.0 * 0.29307107, usageProfile.PersonGain, tolerance);
                Assert.Equal(190.0 * 0.29307107, usageProfile.PersonLatentGain, tolerance);
                Assert.Null(usageProfile.InternalHeatGainProfile);

                if (double.IsNaN(hours))
                {
                    Assert.Equal(internalHeatGain, usageProfile.InternalHeatGain, tolerance);
                }
                else
                {
                    double beta = hours / 168.0;
                    (double use, double off) = internalHeatGains_Beta[usageType];
                    Assert.Equal(beta, usageProfile.Beta, tolerance);
                    Assert.Equal((use * beta) + (off * (1.0 - beta)), usageProfile.InternalHeatGain, tolerance);
                    Assert.StartsWith("ASHRAE-90.1: Table B103", usageProfile.Sources![nameof(Building.HVAC.Classes.UsageProfile.Beta)]);
                }
            }

            // Office: the methodology's own β, and P1 overridable.
            Building.HVAC.Classes.UsageProfile? usageProfile_Office = Building.HVAC.Create.UsageProfile(UsageType.Office);
            Assert.Equal(0.3, usageProfile_Office!.Beta, tolerance);
            Assert.StartsWith("PL-376", usageProfile_Office.Sources![nameof(Building.HVAC.Classes.UsageProfile.Beta)]);
            Assert.Equal(20.0 * 0.3 + 2.0 * 0.7, Building.HVAC.Create.UsageProfile(UsageType.Office, officeShare: 1.0)!.InternalHeatGain, tolerance);

            // A given β overrides, and is tagged as given.
            Building.HVAC.Classes.UsageProfile? usageProfile_Given = Building.HVAC.Create.UsageProfile(UsageType.Retail, beta: 0.5);
            Assert.Equal(5.5, usageProfile_Given!.InternalHeatGain, tolerance);
            Assert.StartsWith("given", usageProfile_Given.Sources![nameof(Building.HVAC.Classes.UsageProfile.Beta)]);

            // Constant types have no β.
            Assert.True(double.IsNaN(Building.HVAC.Create.UsageProfile(UsageType.SingleFamily)!.Beta));

            // Metered multi-family hot water.
            Assert.Equal(1.60, Building.HVAC.Create.UsageProfile(UsageType.MultiFamily, meteredHotWater: true)!.DomesticHotWater, tolerance);

            // Production from ASHRAE light manufacturing.
            Building.HVAC.Classes.UsageProfile? usageProfile_Production = Building.HVAC.Create.UsageProfile(UsageType.Production);
            Assert.NotNull(usageProfile_Production);
            Assert.Equal(16.0, usageProfile_Production.HeatingSetPoint, tolerance);
            Assert.Equal(48.6 / 168.0, usageProfile_Production.Beta, tolerance);
            Assert.Equal(0.9975, usageProfile_Production.InternalHeatGain, 1e-3);
            Assert.Equal(0.0788, usageProfile_Production.DomesticHotWater, 1e-3);
            Assert.Equal(750.0 * 0.09290304, usageProfile_Production.FloorAreaPerPerson, tolerance);
            Assert.Equal(0.08, usageProfile_Production.OutsideAirPerArea, tolerance);

            // Every value is tagged; the uncited ones are exactly the listed set.
            string[] names =
            [
                nameof(Building.HVAC.Classes.UsageProfile.HeatingSetPoint),
                nameof(Building.HVAC.Classes.UsageProfile.InternalHeatGain),
                nameof(Building.HVAC.Classes.UsageProfile.OutsideAirPerArea),
                nameof(Building.HVAC.Classes.UsageProfile.DomesticHotWater),
                nameof(Building.HVAC.Classes.UsageProfile.DomesticHotWaterCorrection),
                nameof(Building.HVAC.Classes.UsageProfile.FloorAreaPerPerson),
                nameof(Building.HVAC.Classes.UsageProfile.PersonGain),
                nameof(Building.HVAC.Classes.UsageProfile.PersonLatentGain),
            ];

            string[] tags = ["PL-376", "PL-WT", "ASHRAE-90.1", "DEFAULT"];

            HashSet<string> defaults = [];
            foreach (UsageType usageType in System.Enum.GetValues(typeof(UsageType)).Cast<UsageType>().Where(x => x != UsageType.Undefined))
            {
                Building.HVAC.Classes.UsageProfile? usageProfile = Building.HVAC.Create.UsageProfile(usageType, hourly: true);
                Assert.NotNull(usageProfile);

                Dictionary<string, string>? sources = usageProfile.Sources;
                Assert.NotNull(sources);
                Assert.False(string.IsNullOrWhiteSpace(usageProfile.Source));

                List<string> names_Expected = [.. names];
                if (!double.IsNaN(usageProfile.Beta))
                {
                    names_Expected.Add(nameof(Building.HVAC.Classes.UsageProfile.Beta));
                }

                if (usageProfile.InternalHeatGainProfile is not null)
                {
                    names_Expected.Add(nameof(Building.HVAC.Classes.UsageProfile.InternalHeatGainProfile));
                }

                Assert.Equal(names_Expected.OrderBy(x => x), sources.Keys.OrderBy(x => x));

                foreach (KeyValuePair<string, string> keyValuePair in sources)
                {
                    Assert.Contains(tags, x => keyValuePair.Value.StartsWith(x));
                    if (keyValuePair.Value.StartsWith("DEFAULT"))
                    {
                        defaults.Add(usageType + "." + keyValuePair.Key);
                    }
                }
            }

            Assert.Equal(["MultiFamily.FloorAreaPerPerson", "Production.DomesticHotWaterCorrection", "Production.OutsideAirPerArea", "SingleFamily.FloorAreaPerPerson"], defaults.OrderBy(x => x));
        }

        /// <summary>
        /// Tests the weekly internal gain profile of Building.HVAC.Create.UsageProfile with hourly: 168 values from Monday 0:00 whose mean is the monthly value exactly, the ASHRAE occupancy shape scaled to the β used, and none for the constant types (DiGi-Organization/DiGi.Analytical#9).
        /// </summary>
        [Fact]
        public void UsageProfile_Hourly()
        {
            foreach (UsageType usageType in new UsageType[] { UsageType.Office, UsageType.Education, UsageType.Gastronomy, UsageType.Sport, UsageType.Retail, UsageType.CollectiveResidence, UsageType.Storage })
            {
                Building.HVAC.Classes.UsageProfile? usageProfile = Building.HVAC.Create.UsageProfile(usageType, hourly: true);
                List<double>? values = usageProfile?.InternalHeatGainProfile;
                Assert.NotNull(values);
                Assert.Equal(168, values.Count);
                Assert.Equal(usageProfile!.InternalHeatGain, values.Average(), 1e-9);
                Assert.All(values, x => Assert.True(x >= 0));
            }

            // Office at 3:00 on Monday is the off-use gain, 2 P1 + 1 (1 - P1) = 1.6 W/m2.
            Assert.Equal(1.6, Building.HVAC.Create.UsageProfile(UsageType.Office, hourly: true)!.InternalHeatGainProfile![3], 1e-9);

            foreach (UsageType usageType in new UsageType[] { UsageType.SingleFamily, UsageType.MultiFamily, UsageType.Healthcare, UsageType.Production })
            {
                Assert.Null(Building.HVAC.Create.UsageProfile(usageType, hourly: true)!.InternalHeatGainProfile);
            }
        }

        /// <summary>
        /// Tests the serialization and the copy of Building.HVAC.Classes.UsageProfile, populated with a weekly profile and its sources (DiGi-Organization/DiGi.Analytical#9).
        /// </summary>
        [Fact]
        public void UsageProfile_Serialization()
        {
            Building.HVAC.Classes.UsageProfile? usageProfile = Building.HVAC.Create.UsageProfile(UsageType.Retail, hourly: true);
            Assert.NotNull(usageProfile);

            string? json = Core.Convert.ToSystem_String(usageProfile);
            Building.HVAC.Classes.UsageProfile? usageProfile_Json = Core.Convert.ToDiGi<Building.HVAC.Classes.UsageProfile>(json)?.FirstOrDefault();
            Assert.NotNull(usageProfile_Json);
            Assert.Equal(usageProfile.InternalHeatGain, usageProfile_Json.InternalHeatGain);
            Assert.Equal(usageProfile.InternalHeatGainProfile, usageProfile_Json.InternalHeatGainProfile);
            Assert.Equal(usageProfile.Sources, usageProfile_Json.Sources);
            Assert.Equal(UsageType.Retail, usageProfile_Json.UsageType);

            Core.xUnit.Query.SerializationCheck(usageProfile);

            Building.HVAC.Classes.UsageProfile usageProfile_Copy = new(usageProfile);
            Assert.Equal(Core.Convert.ToSystem_String(usageProfile), Core.Convert.ToSystem_String(usageProfile_Copy));

            Building.HVAC.Classes.SystemProperties systemProperties = new() { OutsideAirPerArea = 0.56, DomesticHotWater = 0.245, MetabolicRate = 67.4 };
            Building.HVAC.Classes.SystemProperties? systemProperties_Json = Core.Convert.ToDiGi<Building.HVAC.Classes.SystemProperties>(Core.Convert.ToSystem_String(systemProperties))?.FirstOrDefault();
            Assert.Equal(0.56, systemProperties_Json!.OutsideAirPerArea);
            Assert.Equal(0.56, new Building.HVAC.Classes.SystemProperties(systemProperties).OutsideAirPerArea);
            Core.xUnit.Query.SerializationCheck(systemProperties);
        }
    }
}
