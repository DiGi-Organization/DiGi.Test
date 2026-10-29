using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Core.Parameter.Classes;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the declared values of opening and physical component constructions (DiGi-Organization/DiGi.Analytical#8): every <see cref="OpeningConstructionParameter"/> and <see cref="PhysicalComponentConstructionParameter"/> is set, read back, kept by a copy and by a JSON round trip.
        /// <para>Whole-number values (Uf 2.0, U 1.0) are included on purpose: a whole-number double written to text parses back as an integer, and must still read back as a double.</para>
        /// </summary>
        [Fact]
        public void ConstructionParameter()
        {
            WindowConstruction windowConstruction = new("Double-glazed timber window", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));

            (OpeningConstructionParameter OpeningConstructionParameter, double Value)[] values =
            [
                (OpeningConstructionParameter.ThermalTransmittance, 2.6),
                (OpeningConstructionParameter.GlazingThermalTransmittance, 2.8),
                (OpeningConstructionParameter.FrameThermalTransmittance, 2.0),
                (OpeningConstructionParameter.GlazingLinearThermalTransmittance, 0.04),
                (OpeningConstructionParameter.TotalSolarEnergyTransmittance, 0.75),
            ];

            foreach ((OpeningConstructionParameter openingConstructionParameter, double value) in values)
            {
                Assert.True(windowConstruction.SetValue(openingConstructionParameter, value), $"{openingConstructionParameter} not set");
            }

            WallConstruction wallConstruction = new() { Name = "Full brick 450 mm" };
            Assert.True(wallConstruction.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 1.0));

            WindowConstruction? windowConstruction_Json = Core.Convert.ToDiGi<WindowConstruction>(Core.Convert.ToSystem_String(windowConstruction) ?? string.Empty)?.FirstOrDefault();
            Assert.NotNull(windowConstruction_Json);

            WindowConstruction windowConstruction_Copy = new(windowConstruction);
            WindowConstruction[] windowConstructions = [windowConstruction, windowConstruction_Copy, windowConstruction_Json];

            foreach ((OpeningConstructionParameter openingConstructionParameter, double value) in values)
            {
                foreach (WindowConstruction windowConstruction_Temp in windowConstructions)
                {
                    Assert.True(windowConstruction_Temp.TryGetValue(new EnumParameterDefinition(openingConstructionParameter), out double value_Stored), $"{openingConstructionParameter}: missing");
                    Assert.Equal(value, value_Stored);
                }
            }

            // A value outside the declared range is refused.
            Assert.False(windowConstruction.SetValue(OpeningConstructionParameter.TotalSolarEnergyTransmittance, 1.5));

            // An unset value is absent, so a missing declaration cannot be mistaken for 0.
            WindowConstruction windowConstruction_Empty = new("Empty", OpeningFixture_Rectangle(0, 0, 1, 1));
            Assert.False(windowConstruction_Empty.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.ThermalTransmittance), out double _));

            WallConstruction? wallConstruction_Json = Core.Convert.ToDiGi<WallConstruction>(Core.Convert.ToSystem_String(wallConstruction) ?? string.Empty)?.FirstOrDefault();
            Assert.NotNull(wallConstruction_Json);
            Assert.True(wallConstruction_Json.TryGetValue(new EnumParameterDefinition(PhysicalComponentConstructionParameter.ThermalTransmittance), out double thermalTransmittance));
            Assert.Equal(1.0, thermalTransmittance);
            Assert.True(new WallConstruction(wallConstruction).TryGetValue(new EnumParameterDefinition(PhysicalComponentConstructionParameter.ThermalTransmittance), out double thermalTransmittance_Copy));
            Assert.Equal(1.0, thermalTransmittance_Copy);

            Core.xUnit.Query.SerializationCheck(windowConstruction);
            Core.xUnit.Query.SerializationCheck(wallConstruction);
        }
    }
}
