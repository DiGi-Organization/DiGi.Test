using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Enums;
using DiGi.Analytical.Building.HVAC.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the date from which each stage of the U-value limits of the technical conditions applies (DiGi-Organization/DiGi.Analytical#11).
        /// <para>The 2013 amendment (Dz.U. 2013 poz. 926) set three stages from 1 January 2014, 2017 and 2021; the 2017 amendment (Dz.U. 2017 poz. 2285) replaced Annex 2 and moved the last stage to 31 December 2020, with a door limit of 1.3 W/(m2K). The stages apply from these dates, not from the effective date of a later act or of the 2022 consolidated text (Dz.U. 2022 poz. 1225) - the dates the manager used before: 2018-01-01 for the 2017 stage and 2022-06-23 for the last one.</para>
        /// </summary>
        [Fact]
        public void RegulatedHeatTransferCoefficientsManager_StageDates()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();

            // Before the 2014 stage the 2009 regulation applies.
            Assert.IsType<RegulatedHeatTransferCoefficients_2009>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2013, 12, 31)));

            // Wall limit by design date: 0.25 from 2014, 0.23 from 1 January 2017, 0.20 from 31 December 2020.
            (DateTime DateTime, double Wall)[] walls =
            [
                (new DateTime(2014, 1, 1), 0.25),
                (new DateTime(2016, 12, 31), 0.25),
                (new DateTime(2017, 1, 1), 0.23),
                (new DateTime(2017, 6, 1), 0.23),
                (new DateTime(2018, 1, 1), 0.23),
                (new DateTime(2020, 12, 30), 0.23),
                (new DateTime(2020, 12, 31), 0.20),
                (new DateTime(2021, 6, 1), 0.20),
                (new DateTime(2022, 6, 23), 0.20),
                (new DateTime(2026, 1, 1), 0.20),
            ];

            foreach ((DateTime dateTime, double wall) in walls)
            {
                RegulatedHeatTransferCoefficients_2014 regulatedHeatTransferCoefficients = Assert.IsType<RegulatedHeatTransferCoefficients_2014>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime));
                Assert.True(regulatedHeatTransferCoefficients[ExternalPartitionType_2014.Wall] == wall, $"{dateTime:yyyy-MM-dd}: wall {regulatedHeatTransferCoefficients[ExternalPartitionType_2014.Wall]} instead of {wall}");
            }

            // The 2017 stage belongs to the 2013 amendment.
            IRegulatedHeatTransferCoefficients? regulatedHeatTransferCoefficients_2017 = regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2017, 1, 1));
            Assert.Equal("Dz.U. 2013 poz. 926", regulatedHeatTransferCoefficients_2017?.RegulationAct?.Reference);
            Assert.Equal(new DateTime(2017, 1, 1), regulatedHeatTransferCoefficients_2017?.RegulationAct?.EffectiveDate);

            // The last stage was set by the 2017 amendment, with every limit of its Annex 2.
            RegulatedHeatTransferCoefficients_2014 regulatedHeatTransferCoefficients_2021 = Assert.IsType<RegulatedHeatTransferCoefficients_2014>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2020, 12, 31)));
            Assert.Equal("Dz.U. 2017 poz. 2285", regulatedHeatTransferCoefficients_2021.RegulationAct?.Reference);
            Assert.Equal(new DateTime(2020, 12, 31), regulatedHeatTransferCoefficients_2021.RegulationAct?.EffectiveDate);
            Assert.Equal(0.20, regulatedHeatTransferCoefficients_2021[ExternalPartitionType_2014.Wall]);
            Assert.Equal(0.15, regulatedHeatTransferCoefficients_2021[ExternalPartitionType_2014.Roof]);
            Assert.Equal(0.25, regulatedHeatTransferCoefficients_2021[ExternalPartitionType_2014.Floor_OverUnheatedSpaces]);
            Assert.Equal(0.30, regulatedHeatTransferCoefficients_2021[ExternalPartitionType_2014.Floor]);
            Assert.Equal(0.9, regulatedHeatTransferCoefficients_2021[ExternalPartitionType_2014.Window]);
            Assert.Equal(1.1, regulatedHeatTransferCoefficients_2021[ExternalPartitionType_2014.Window_Skylight]);
            Assert.Equal(1.3, regulatedHeatTransferCoefficients_2021[ExternalPartitionType_2014.Door]);
        }

        /// <summary>
        /// Pins the earlier date of the last stage of the U-value limits for public-authority buildings (DiGi-Organization/DiGi.Analytical#11): from 1 January 2019 for a building occupied and owned by a judicial body, a prosecutor's office or a public administration body, against 31 December 2020 for any other building (Dz.U. 2017 poz. 2285, Annex 2). The date survives a JSON round trip.
        /// </summary>
        [Fact]
        public void RegulatedHeatTransferCoefficientsManager_PublicAuthority()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();

            (DateTime DateTime, double Wall, double Wall_PublicAuthority)[] walls =
            [
                (new DateTime(2016, 12, 31), 0.25, 0.25),
                (new DateTime(2018, 12, 31), 0.23, 0.23),
                (new DateTime(2019, 1, 1), 0.23, 0.20),
                (new DateTime(2019, 6, 1), 0.23, 0.20),
                (new DateTime(2020, 12, 31), 0.20, 0.20),
            ];

            foreach ((DateTime dateTime, double wall, double wall_PublicAuthority) in walls)
            {
                RegulatedHeatTransferCoefficients_2014 regulatedHeatTransferCoefficients = Assert.IsType<RegulatedHeatTransferCoefficients_2014>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime, false));
                RegulatedHeatTransferCoefficients_2014 regulatedHeatTransferCoefficients_PublicAuthority = Assert.IsType<RegulatedHeatTransferCoefficients_2014>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime, true));
                Assert.True(regulatedHeatTransferCoefficients[ExternalPartitionType_2014.Wall] == wall, $"{dateTime:yyyy-MM-dd}: wall {regulatedHeatTransferCoefficients[ExternalPartitionType_2014.Wall]} instead of {wall}");
                Assert.True(regulatedHeatTransferCoefficients_PublicAuthority[ExternalPartitionType_2014.Wall] == wall_PublicAuthority, $"{dateTime:yyyy-MM-dd}, public authority: wall {regulatedHeatTransferCoefficients_PublicAuthority[ExternalPartitionType_2014.Wall]} instead of {wall_PublicAuthority}");
            }

            // The one-argument lookup keeps answering for any other building.
            Assert.Same(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2019, 6, 1), false), regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2019, 6, 1)));

            RegulationAct? regulationAct = regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2021, 1, 1))?.RegulationAct;
            Assert.NotNull(regulationAct);
            Assert.Equal(new DateTime(2019, 1, 1), regulationAct.PublicAuthorityEffectiveDate);

            Core.xUnit.Query.SerializationCheck(regulationAct);
            RegulationAct? regulationAct_Clone = Core.Query.Clone(regulationAct);
            Assert.Equal(regulationAct.PublicAuthorityEffectiveDate, regulationAct_Clone?.PublicAuthorityEffectiveDate);
            Assert.Equal(regulationAct.EffectiveDate, regulationAct_Clone?.EffectiveDate);

            Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficientsManager);
        }
    }
}
