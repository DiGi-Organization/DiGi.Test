using DiGi.Unit.Enums;

namespace DiGi.Unit.xUnit
{
    /// <summary>
    /// Contains unit tests for verifying the expansion of SI units, correct bug fixes, and conversions between milli and micro units.
    /// </summary>
    public partial class Facts
    {
        /// <summary>
        /// Tests the conversion functions for the new SI base units and derived units, as well as testing critical bug fixes and milli-to-micro conversions.
        /// </summary>
        [Fact]
        public void TryConvertNewUnits()
        {
            bool bool_Converted;
            double? double_Value;

            // 1. Verify bug fix: Unit.To should convert from base to target (e.g. 1 m -> 1000 mm)
            Classes.Unit unit_Milimeter = new(LengthUnit.Milimeter);
            double double_ToResult = unit_Milimeter.To(1.0);
            Assert.Equal(1000.0, double_ToResult, 5);

            double double_FromResult = unit_Milimeter.From(1000.0);
            Assert.Equal(1.0, double_FromResult, 5);

            // 2. Verify bug fix: AngleUnit.Degree factor
            bool_Converted = Query.TryConvert(180.0, AngleUnit.Degree, AngleUnit.Radian, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(Math.PI, double_Value.Value, 5);

            bool_Converted = Query.TryConvert(Math.PI, AngleUnit.Radian, AngleUnit.Degree, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(180.0, double_Value.Value, 5);

            // 3. Verify bug fix: DensityUnit.GramPerCubicCentimeter factor
            bool_Converted = Query.TryConvert(1.0, DensityUnit.GramPerCubicCentimeter, DensityUnit.KilogramPerCubicMeter, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // 4. Verify new MassUnit conversions
            bool_Converted = Query.TryConvert(1.5, MassUnit.Kilogram, MassUnit.Gram, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1500.0, double_Value.Value, 5);

            // 5. Verify direct conversions between Milli and Micro units (as requested)
            // Mass: 1 Milligram to Micrograms
            bool_Converted = Query.TryConvert(1.0, MassUnit.Milligram, MassUnit.Microgram, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            bool_Converted = Query.TryConvert(1000.0, MassUnit.Microgram, MassUnit.Milligram, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1.0, double_Value.Value, 5);

            // Electric Current: 1 Milliampere to Microamperes
            bool_Converted = Query.TryConvert(1.0, ElectricCurrentUnit.Milliampere, ElectricCurrentUnit.Microampere, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // Amount of Substance: 1 Millimole to Micromoles
            bool_Converted = Query.TryConvert(1.0, AmountOfSubstanceUnit.Millimole, AmountOfSubstanceUnit.Micromole, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // Luminous Intensity: 1 Millicandela to Microcandelas
            bool_Converted = Query.TryConvert(1.0, LuminousIntensityUnit.Millicandela, LuminousIntensityUnit.Microcandela, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // Force: 1 Millinewton to Micronewtons
            bool_Converted = Query.TryConvert(1.0, ForceUnit.Millinewton, ForceUnit.Micronewton, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // Pressure: 1 Millipascal to Micropascals
            bool_Converted = Query.TryConvert(1.0, PressureUnit.Millipascal, PressureUnit.Micropascal, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // Energy: 1 Millijoule to Microjoules
            bool_Converted = Query.TryConvert(1.0, EnergyUnit.Millijoule, EnergyUnit.Microjoule, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // Power: 1 Milliwatt to Microwatts
            bool_Converted = Query.TryConvert(1.0, PowerUnit.Milliwatt, PowerUnit.Microwatt, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // Electric Potential: 1 Millivolt to Microvolts
            bool_Converted = Query.TryConvert(1.0, ElectricPotentialUnit.Millivolt, ElectricPotentialUnit.Microvolt, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // Electric Resistance: 1 Milliohm to Microohms
            bool_Converted = Query.TryConvert(1.0, ElectricResistanceUnit.Milliohm, ElectricResistanceUnit.Microohm, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // 6. Verify other derived unit categories (Speed, Frequency, Acceleration, Illuminance)
            bool_Converted = Query.TryConvert(72.0, SpeedUnit.KilometerPerHour, SpeedUnit.MeterPerSecond, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(20.0, double_Value.Value, 5);

            bool_Converted = Query.TryConvert(1.0, FrequencyUnit.Kilohertz, FrequencyUnit.Hertz, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            bool_Converted = Query.TryConvert(1.0, AccelerationUnit.StandardGravity, AccelerationUnit.MeterPerSecondSquared, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(9.80665, double_Value.Value, 5);

            bool_Converted = Query.TryConvert(10.0, IlluminanceUnit.Lux, IlluminanceUnit.Lux, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(10.0, double_Value.Value, 5);

            // 7. Verify ElectricConductanceUnit conversions
            bool_Converted = Query.TryConvert(1.0, ElectricConductanceUnit.Siemens, ElectricConductanceUnit.Millisiemens, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            bool_Converted = Query.TryConvert(1.0, ElectricConductanceUnit.Millisiemens, ElectricConductanceUnit.Microsiemens, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // 8. Verify ElectricConductivityUnit conversions
            bool_Converted = Query.TryConvert(1.0, ElectricConductivityUnit.SiemensPerMeter, ElectricConductivityUnit.MillisiemensPerMeter, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            bool_Converted = Query.TryConvert(1.0, ElectricConductivityUnit.MillisiemensPerCentimeter, ElectricConductivityUnit.SiemensPerMeter, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(0.1, double_Value.Value, 5);

            // 9. Verify inverse conversion between Siemens (Conductance) and Ohm (Resistance)
            // 500 mS = 0.5 S -> 1 / 0.5 S = 2 Ohms
            bool_Converted = Query.TryConvert(500.0, ElectricConductanceUnit.Millisiemens, ElectricResistanceUnit.Ohm, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(2.0, double_Value.Value, 5);

            // 10 Ohms -> 1 / 10 Ohm = 0.1 S = 100 mS
            bool_Converted = Query.TryConvert(10.0, ElectricResistanceUnit.Ohm, ElectricConductanceUnit.Millisiemens, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(100.0, double_Value.Value, 5);

            // 2 Siemens -> 1 / 2 S = 0.5 Ohms
            bool_Converted = Query.TryConvert(2.0, ElectricConductanceUnit.Siemens, ElectricResistanceUnit.Ohm, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(0.5, double_Value.Value, 5);

            // 10. Verify inverse conversion between Time and Frequency
            // 0.02 s -> 1 / 0.02 s = 50 Hz
            bool_Converted = Query.TryConvert(0.02, TimeUnit.Second, FrequencyUnit.Hertz, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(50.0, double_Value.Value, 5);

            // 1 kHz -> 1 / 1000 Hz = 0.001 s
            bool_Converted = Query.TryConvert(1.0, FrequencyUnit.Kilohertz, TimeUnit.Second, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(0.001, double_Value.Value, 5);

            // 11. Verify ThermalResistanceUnit conversions
            bool_Converted = Query.TryConvert(1.0, ThermalResistanceUnit.SquareMeterKelvinPerWatt, ThermalResistanceUnit.SquareMeterDegreeCelsiusPerWatt, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1.0, double_Value.Value, 5);

            // 12. Verify ThermalTransmittanceUnit conversions
            bool_Converted = Query.TryConvert(1.0, ThermalTransmittanceUnit.WattPerSquareMeterKelvin, ThermalTransmittanceUnit.WattPerSquareMeterDegreeCelsius, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1.0, double_Value.Value, 5);

            // 13. Verify inverse conversion between ThermalResistance (R-value) and ThermalTransmittance (U-value)
            // R = 0.2 m2K/W -> U = 1 / 0.2 = 5.0 W/m2K
            bool_Converted = Query.TryConvert(0.2, ThermalResistanceUnit.SquareMeterKelvinPerWatt, ThermalTransmittanceUnit.WattPerSquareMeterKelvin, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(5.0, double_Value.Value, 5);

            // U = 2.0 W/m2K -> R = 1 / 2.0 = 0.5 m2K/W
            bool_Converted = Query.TryConvert(2.0, ThermalTransmittanceUnit.WattPerSquareMeterKelvin, ThermalResistanceUnit.SquareMeterKelvinPerWatt, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(0.5, double_Value.Value, 5);

            // 14. Verify WavenumberUnit conversions
            bool_Converted = Query.TryConvert(1.0, WavenumberUnit.ReciprocalCentimeter, WavenumberUnit.ReciprocalMeter, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(100.0, double_Value.Value, 5);

            // 15. Verify inverse conversion between Length (wavelength) and Wavenumber (spatial frequency)
            // 0.01 m -> 1 / 0.01 = 100 1/m = 1 1/cm
            bool_Converted = Query.TryConvert(0.01, LengthUnit.Meter, WavenumberUnit.ReciprocalCentimeter, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1.0, double_Value.Value, 5);

            // 100 1/m -> 1 / 100 = 0.01 m
            bool_Converted = Query.TryConvert(100.0, WavenumberUnit.ReciprocalMeter, LengthUnit.Meter, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(0.01, double_Value.Value, 5);

            // 16. Verify StiffnessUnit conversions
            bool_Converted = Query.TryConvert(1.0, StiffnessUnit.KilonewtonPerMeter, StiffnessUnit.NewtonPerMeter, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);

            // 17. Verify ComplianceUnit conversions
            bool_Converted = Query.TryConvert(1.0, ComplianceUnit.MeterPerKilonewton, ComplianceUnit.MeterPerNewton, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(0.001, double_Value.Value, 5);

            // 18. Verify inverse conversion between Stiffness and Compliance
            // k = 1000 N/m -> c = 1 / 1000 = 0.001 m/N
            bool_Converted = Query.TryConvert(1000.0, StiffnessUnit.NewtonPerMeter, ComplianceUnit.MeterPerNewton, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(0.001, double_Value.Value, 5);

            // c = 0.001 m/N -> k = 1 / 0.001 = 1000 N/m
            bool_Converted = Query.TryConvert(0.001, ComplianceUnit.MeterPerNewton, StiffnessUnit.NewtonPerMeter, out double_Value);
            Assert.True(bool_Converted);
            Assert.NotNull(double_Value);
            Assert.Equal(1000.0, double_Value.Value, 5);
        }
    }
}