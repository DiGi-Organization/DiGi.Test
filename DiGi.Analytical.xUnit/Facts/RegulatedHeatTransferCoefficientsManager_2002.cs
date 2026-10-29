using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Enums;
using DiGi.Analytical.Building.HVAC.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the 2002 stage of the U-value limits against the act, Annex 2 of Dz.U. 2002 nr 75 poz. 690 (DiGi-Organization/DiGi.Analytical#12).
        /// <para>The act was published on 15 June 2002 and applies from 16 December 2002. A floor on ground is limited by a minimum sum of thermal resistances of 1.5 (m2K)/W (point 1.4), so it is held as a minimum thermal resistance and never as a heat transfer coefficient - the manager used to store 0.67 and 0.60 W/(m2K) as U values. The wall, roof and basement ceiling limits of a residential building come from the single-family building table, which their descriptions state.</para>
        /// </summary>
        [Fact]
        public void RegulatedHeatTransferCoefficientsManager_2002()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();

            RegulatedHeatTransferCoefficients_2002 regulatedHeatTransferCoefficients_2002 = Assert.IsType<RegulatedHeatTransferCoefficients_2002>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2002, 12, 16)));
            Assert.Same(regulatedHeatTransferCoefficients_2002, regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2008, 12, 31)));
            Assert.IsType<RegulatedHeatTransferCoefficients_2009>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(2009, 1, 1)));

            RegulationAct? regulationAct = regulatedHeatTransferCoefficients_2002.RegulationAct;
            Assert.NotNull(regulationAct);
            Assert.Equal(new DateTime(2002, 6, 15), regulationAct.PublicationDate);
            Assert.Equal(new DateTime(2002, 12, 16), regulationAct.EffectiveDate);

            // Every U(max) of the act's single-family, residential window and public building tables.
            (ExternalPartitionType_2002 ExternalPartitionType_2002, double Value)[] values =
            [
                (ExternalPartitionType_2002.ResidentialBuilding_Wall_Multilayer, 0.30),
                (ExternalPartitionType_2002.ResidentialBuilding_Wall_Solid, 0.50),
                (ExternalPartitionType_2002.ResidentialBuilding_Roof, 0.30),
                (ExternalPartitionType_2002.ResidentialBuilding_Floor_OverUnheatedSpaces, 0.60),
                (ExternalPartitionType_2002.ResidentialBuilding_Window_ZoneA, 2.6),
                (ExternalPartitionType_2002.ResidentialBuilding_Window_ZoneB, 2.0),
                (ExternalPartitionType_2002.ResidentialBuilding_Window_Skylight, 2.0),
                (ExternalPartitionType_2002.ResidentialBuilding_Door, 2.6),
                (ExternalPartitionType_2002.PublicBuilding_Wall_Solid, 0.45),
                (ExternalPartitionType_2002.PublicBuilding_Wall_WithWindowAndDoor, 0.55),
                (ExternalPartitionType_2002.PublicBuilding_Wall_WithBaconyBrackets, 0.65),
                (ExternalPartitionType_2002.PublicBuilding_Roof, 0.30),
                (ExternalPartitionType_2002.PublicBuilding_Floor_OverUnheatedSpaces, 0.60),
                (ExternalPartitionType_2002.PublicBuilding_Window, 2.3),
                (ExternalPartitionType_2002.PublicBuilding_Window_Skylight, 2.0),
                (ExternalPartitionType_2002.PublicBuilding_Door, 2.6),
            ];

            foreach ((ExternalPartitionType_2002 externalPartitionType_2002, double value) in values)
            {
                Assert.True(regulatedHeatTransferCoefficients_2002.TryGetValue(externalPartitionType_2002, out double value_Stored), $"{externalPartitionType_2002}: no U(max)");
                Assert.True(value_Stored == value, $"{externalPartitionType_2002}: U(max) {value_Stored} instead of {value}");
            }

            Assert.Equal(values.Length, regulatedHeatTransferCoefficients_2002.GetEnums().Count);

            // A floor on ground has a minimum thermal resistance, not a U(max).
            ExternalPartitionType_2002[] externalPartitionType_2002s_Floor = [ExternalPartitionType_2002.ResidentialBuilding_Floor, ExternalPartitionType_2002.PublicBuilding_Floor];
            foreach (ExternalPartitionType_2002 externalPartitionType_2002 in externalPartitionType_2002s_Floor)
            {
                Assert.False(regulatedHeatTransferCoefficients_2002.TryGetValue(externalPartitionType_2002, out double value), $"{externalPartitionType_2002}: U(max) {value} stored for a floor on ground");
                Assert.True(regulatedHeatTransferCoefficients_2002.TryGetMinimumThermalResistance(externalPartitionType_2002, out double minimumThermalResistance));
                Assert.Equal(1.5, minimumThermalResistance);
            }

            // The name-based access of the interface keeps the two apart.
            IRegulatedHeatTransferCoefficients regulatedHeatTransferCoefficients = regulatedHeatTransferCoefficients_2002;
            string name_Floor = Core.Query.Description(ExternalPartitionType_2002.ResidentialBuilding_Floor) ?? string.Empty;
            Assert.DoesNotContain(name_Floor, regulatedHeatTransferCoefficients.GetNames());
            Assert.Equal(2, regulatedHeatTransferCoefficients.GetMinimumThermalResistanceNames().Count);
            Assert.Contains(name_Floor, regulatedHeatTransferCoefficients.GetMinimumThermalResistanceNames());
            Assert.False(regulatedHeatTransferCoefficients.TryGetValue(name_Floor, out _));
            Assert.True(regulatedHeatTransferCoefficients.TryGetMinimumThermalResistance(name_Floor, out double minimumThermalResistance_Name));
            Assert.Equal(1.5, minimumThermalResistance_Name);

            // The single-family scope is stated, and the "residential" prefix the portal filters on is kept.
            ExternalPartitionType_2002[] externalPartitionType_2002s_SingleFamily = [ExternalPartitionType_2002.ResidentialBuilding_Wall_Multilayer, ExternalPartitionType_2002.ResidentialBuilding_Wall_Solid, ExternalPartitionType_2002.ResidentialBuilding_Roof, ExternalPartitionType_2002.ResidentialBuilding_Floor_OverUnheatedSpaces];
            foreach (ExternalPartitionType_2002 externalPartitionType_2002 in externalPartitionType_2002s_SingleFamily)
            {
                string? description = Core.Query.Description(externalPartitionType_2002);
                Assert.NotNull(description);
                Assert.StartsWith("Residential building (single-family)", description);
            }

            // Both maps survive a copy and a JSON round trip.
            RegulatedHeatTransferCoefficients_2002 regulatedHeatTransferCoefficients_2002_Copy = new(regulatedHeatTransferCoefficients_2002);
            Assert.True(regulatedHeatTransferCoefficients_2002_Copy.TryGetMinimumThermalResistance(ExternalPartitionType_2002.PublicBuilding_Floor, out double minimumThermalResistance_Copy));
            Assert.Equal(1.5, minimumThermalResistance_Copy);
            Assert.Equal(0.45, regulatedHeatTransferCoefficients_2002_Copy[ExternalPartitionType_2002.PublicBuilding_Wall_Solid]);
            Assert.Equal(new DateTime(2002, 6, 15), regulatedHeatTransferCoefficients_2002_Copy.RegulationAct?.PublicationDate);

            RegulatedHeatTransferCoefficients_2002? regulatedHeatTransferCoefficients_2002_Clone = Core.Query.Clone(regulatedHeatTransferCoefficients_2002);
            Assert.NotNull(regulatedHeatTransferCoefficients_2002_Clone);
            Assert.True(regulatedHeatTransferCoefficients_2002_Clone.TryGetMinimumThermalResistance(ExternalPartitionType_2002.ResidentialBuilding_Floor, out double minimumThermalResistance_Clone));
            Assert.Equal(1.5, minimumThermalResistance_Clone);
            Assert.False(regulatedHeatTransferCoefficients_2002_Clone.TryGetValue(ExternalPartitionType_2002.ResidentialBuilding_Floor, out _));

            Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficients_2002);
            Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficientsManager);
        }
    }
}
