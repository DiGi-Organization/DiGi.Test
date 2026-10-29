using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Enums;
using DiGi.Analytical.Building.HVAC.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the 1997 stage of the U-value limits, the k(max) annex the 1997 amendment (Dz.U. 1997 nr 132 poz. 878) added to the 1994 technical conditions (DiGi-Organization/DiGi.Analytical#14).
        /// <para>The stage applies from 29 April 1998, six months after its publication on 28 October 1997, until the 2002 regulation applies from 16 December 2002; before it the manager knows no limits. It holds every k(max) of the single-family and public building tables - roof windows fall under the single-family window rows, so there is no separate roof window limit - and the floor-on-ground R(min) of 1.5 (m2K)/W apart from the U values.</para>
        /// </summary>
        [Fact]
        public void RegulatedHeatTransferCoefficientsManager_1997()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();

            // Stage boundaries.
            Assert.Null(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(1998, 4, 28)));
            RegulatedHeatTransferCoefficients_1997 regulatedHeatTransferCoefficients_1997 = Assert.IsType<RegulatedHeatTransferCoefficients_1997>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(1998, 4, 29)));
            Assert.Same(regulatedHeatTransferCoefficients_1997, regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2000, 1, 1)));
            Assert.Same(regulatedHeatTransferCoefficients_1997, regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2002, 12, 15)));
            Assert.IsType<RegulatedHeatTransferCoefficients_2002>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2002, 12, 16)));

            // The public-authority lookup has no earlier date for this act.
            Assert.Null(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(1998, 4, 28), true));
            Assert.Same(regulatedHeatTransferCoefficients_1997, regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(1998, 4, 29), true));

            RegulationAct? regulationAct = regulatedHeatTransferCoefficients_1997.RegulationAct;
            Assert.NotNull(regulationAct);
            Assert.Equal(new DateTime(1997, 10, 28), regulationAct.PublicationDate);
            Assert.Equal(new DateTime(1998, 4, 29), regulationAct.EffectiveDate);
            Assert.Null(regulationAct.PublicAuthorityEffectiveDate);
            Assert.Equal("Dz.U. 1997 nr 132 poz. 878", regulationAct.Reference);

            // Every k(max) of the annex's single-family and public building tables.
            (ExternalPartitionType_1997 ExternalPartitionType_1997, double Value)[] values =
            [
                (ExternalPartitionType_1997.ResidentialBuilding_Wall_Multilayer, 0.30),
                (ExternalPartitionType_1997.ResidentialBuilding_Wall_Solid, 0.50),
                (ExternalPartitionType_1997.ResidentialBuilding_Roof, 0.30),
                (ExternalPartitionType_1997.ResidentialBuilding_Floor_OverUnheatedSpaces, 0.60),
                (ExternalPartitionType_1997.ResidentialBuilding_Window_ZoneA, 2.6),
                (ExternalPartitionType_1997.ResidentialBuilding_Window_ZoneB, 2.0),
                (ExternalPartitionType_1997.ResidentialBuilding_Door, 2.6),
                (ExternalPartitionType_1997.PublicBuilding_Wall_Solid, 0.45),
                (ExternalPartitionType_1997.PublicBuilding_Wall_WithWindowAndDoor, 0.55),
                (ExternalPartitionType_1997.PublicBuilding_Wall_WithBalconyBrackets, 0.65),
                (ExternalPartitionType_1997.PublicBuilding_Roof, 0.30),
                (ExternalPartitionType_1997.PublicBuilding_Floor_OverUnheatedSpaces, 0.60),
                (ExternalPartitionType_1997.PublicBuilding_Window, 2.3),
                (ExternalPartitionType_1997.PublicBuilding_Door, 2.6),
            ];

            foreach ((ExternalPartitionType_1997 externalPartitionType_1997, double value) in values)
            {
                Assert.True(regulatedHeatTransferCoefficients_1997.TryGetValue(externalPartitionType_1997, out double value_Stored), $"{externalPartitionType_1997}: no k(max)");
                Assert.True(value_Stored == value, $"{externalPartitionType_1997}: k(max) {value_Stored} instead of {value}");
            }

            Assert.Equal(values.Length, regulatedHeatTransferCoefficients_1997.GetEnums().Count);

            // A floor on ground has a minimum thermal resistance, not a k(max).
            ExternalPartitionType_1997[] externalPartitionType_1997s_Floor = [ExternalPartitionType_1997.ResidentialBuilding_Floor, ExternalPartitionType_1997.PublicBuilding_Floor];
            foreach (ExternalPartitionType_1997 externalPartitionType_1997 in externalPartitionType_1997s_Floor)
            {
                Assert.False(regulatedHeatTransferCoefficients_1997.TryGetValue(externalPartitionType_1997, out double value), $"{externalPartitionType_1997}: k(max) {value} stored for a floor on ground");
                Assert.True(regulatedHeatTransferCoefficients_1997.TryGetMinimumThermalResistance(externalPartitionType_1997, out double minimumThermalResistance));
                Assert.Equal(1.5, minimumThermalResistance);
            }

            IRegulatedHeatTransferCoefficients regulatedHeatTransferCoefficients = regulatedHeatTransferCoefficients_1997;
            Assert.Equal(values.Length, regulatedHeatTransferCoefficients.GetNames().Count);
            Assert.Equal(2, regulatedHeatTransferCoefficients.GetMinimumThermalResistanceNames().Count);

            // Every residential row is single-family, keeping the "residential" prefix the portal filters on.
            foreach (string name in regulatedHeatTransferCoefficients.GetNames())
            {
                Assert.True(name.StartsWith("Residential building (single-family)") || name.StartsWith("Public building"), name);
            }

            // Both maps survive a copy and a JSON round trip.
            RegulatedHeatTransferCoefficients_1997 regulatedHeatTransferCoefficients_1997_Copy = new(regulatedHeatTransferCoefficients_1997);
            Assert.Equal(2.0, regulatedHeatTransferCoefficients_1997_Copy[ExternalPartitionType_1997.ResidentialBuilding_Window_ZoneB]);
            Assert.True(regulatedHeatTransferCoefficients_1997_Copy.TryGetMinimumThermalResistance(ExternalPartitionType_1997.ResidentialBuilding_Floor, out double minimumThermalResistance_Copy));
            Assert.Equal(1.5, minimumThermalResistance_Copy);
            Assert.Equal(new DateTime(1997, 10, 28), regulatedHeatTransferCoefficients_1997_Copy.RegulationAct?.PublicationDate);
            Assert.NotSame(regulatedHeatTransferCoefficients_1997.RegulationAct, regulatedHeatTransferCoefficients_1997_Copy.RegulationAct);

            string? json = Core.Convert.ToSystem_String(regulatedHeatTransferCoefficients_1997);
            Assert.NotNull(json);
            RegulatedHeatTransferCoefficients_1997? regulatedHeatTransferCoefficients_1997_Json = Core.Convert.ToDiGi<RegulatedHeatTransferCoefficients_1997>(json)?.FirstOrDefault();
            Assert.NotNull(regulatedHeatTransferCoefficients_1997_Json);
            Assert.Equal(0.65, regulatedHeatTransferCoefficients_1997_Json[ExternalPartitionType_1997.PublicBuilding_Wall_WithBalconyBrackets]);
            Assert.True(regulatedHeatTransferCoefficients_1997_Json.TryGetMinimumThermalResistance(ExternalPartitionType_1997.PublicBuilding_Floor, out double minimumThermalResistance_Json));
            Assert.Equal(1.5, minimumThermalResistance_Json);
            Assert.Equal(new DateTime(1998, 4, 29), regulatedHeatTransferCoefficients_1997_Json.RegulationAct?.EffectiveDate);

            Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficients_1997);
            Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficientsManager);
        }
    }
}
