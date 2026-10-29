using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Enums;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the functionality and serialization of the <see cref="RegulatedHeatTransferCoefficientsManager"/> class by adding regulated heat transfer coefficients for a specific regulation act and verifying the result.
        /// </summary>
        [Fact]
        public void RegulatedHeatTransferCoefficientsManager()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = new();

            RegulationAct regulationAct = new(new DateTime(2002, 6, 15), new DateTime(2002, 12, 16), "Dz.U. 2002 nr 75 poz. 690", "Rozporządzenie Ministra Infrastruktury z dnia 12 kwietnia 2002 r. w sprawie warunków technicznych, jakim powinny odpowiadać budynki i ich usytuowanie", null);

            RegulatedHeatTransferCoefficients_2002 regulatedHeatTransferCoefficients_2002 = new RegulatedHeatTransferCoefficients_2002(regulationAct);
            regulatedHeatTransferCoefficients_2002[ExternalPartitionType_2002.ResidentialBuilding_Wall_Multilayer] = 0.3;
            regulatedHeatTransferCoefficients_2002[ExternalPartitionType_2002.ResidentialBuilding_Wall_Solid] = 0.5;
            regulatedHeatTransferCoefficients_2002[ExternalPartitionType_2002.ResidentialBuilding_Roof] = 0.3;
            regulatedHeatTransferCoefficients_2002.SetMinimumThermalResistance(ExternalPartitionType_2002.ResidentialBuilding_Floor, 1.5);

            regulatedHeatTransferCoefficientsManager.Add(regulatedHeatTransferCoefficients_2002);

            Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficientsManager);
        }
    }
}