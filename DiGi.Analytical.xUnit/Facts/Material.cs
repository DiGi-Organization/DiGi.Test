using DiGi.Analytical.Classes;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests construction, copy and serialization of <see cref="Classes.TransparentMaterial"/> with every property populated (DiGi-Organization/DiGi.Analytical#8): conductivity, solar and light transmittance, and the front/back solar reflectance, light reflectance and emissivity.
        /// <para>The copy constructor is asserted directly, because <see cref="Core.xUnit.Query.SerializationCheck"/> clones through the JSON constructor and never runs it.</para>
        /// </summary>
        [Fact]
        public void TransparentMaterial()
        {
            TransparentMaterial transparentMaterial = new("Float glass 4 mm")
            {
                Conductivity = 1.0,
                SolarTransmittance = 0.83,
                LightTransmittance = 0.90,
                SolarReflectance = new SurfaceValue(0.07, 0.08),
                LightReflectance = new SurfaceValue(0.08, 0.09),
                Emissivity = new SurfaceValue(0.84, 0.837),
                InfraredTransmittance = 0.02,
            };

            Assert.Equal(0.02, transparentMaterial.InfraredTransmittance);
            Assert.Equal(1.0, transparentMaterial.Conductivity);
            Assert.Equal(0.83, transparentMaterial.SolarTransmittance);
            Assert.Equal(0.90, transparentMaterial.LightTransmittance);
            Assert.Equal(0.08, transparentMaterial.SolarReflectance?.External);
            Assert.Equal(0.08, transparentMaterial.LightReflectance?.Internal);
            Assert.Equal(0.837, transparentMaterial.Emissivity?.External);

            TransparentMaterial transparentMaterial_Copy = new(transparentMaterial);
            Assert.Equal(transparentMaterial.Name, transparentMaterial_Copy.Name);
            Assert.Equal(transparentMaterial.Conductivity, transparentMaterial_Copy.Conductivity);
            Assert.Equal(transparentMaterial.SolarTransmittance, transparentMaterial_Copy.SolarTransmittance);
            Assert.Equal(transparentMaterial.LightTransmittance, transparentMaterial_Copy.LightTransmittance);
            Assert.Equal(transparentMaterial.SolarReflectance?.Internal, transparentMaterial_Copy.SolarReflectance?.Internal);
            Assert.Equal(transparentMaterial.LightReflectance?.External, transparentMaterial_Copy.LightReflectance?.External);
            Assert.Equal(transparentMaterial.Emissivity?.Internal, transparentMaterial_Copy.Emissivity?.Internal);
            Assert.NotSame(transparentMaterial.Emissivity, transparentMaterial_Copy.Emissivity);
            Assert.Equal(0.02, transparentMaterial_Copy.InfraredTransmittance);

            string? json = Core.Convert.ToSystem_String(transparentMaterial);
            Assert.NotNull(json);
            TransparentMaterial? transparentMaterial_Json = Core.Convert.ToDiGi<TransparentMaterial>(json)?.FirstOrDefault();
            Assert.NotNull(transparentMaterial_Json);
            Assert.Equal(0.83, transparentMaterial_Json.SolarTransmittance);
            Assert.Equal(0.09, transparentMaterial_Json.LightReflectance?.External);

            Core.xUnit.Query.SerializationCheck(transparentMaterial);
        }

        /// <summary>
        /// Tests construction, copy and serialization of <see cref="Classes.FluidMaterial"/> with every gas property populated (DiGi-Organization/DiGi.Analytical#8): conductivity, density, specific heat and dynamic viscosity.
        /// <para>The copy constructor is asserted directly, because <see cref="Core.xUnit.Query.SerializationCheck"/> clones through the JSON constructor and never runs it.</para>
        /// </summary>
        [Fact]
        public void FluidMaterial()
        {
            FluidMaterial fluidMaterial = new("Argon")
            {
                Conductivity = 0.01684,
                Density = 1.699,
                SpecificHeat = 519.0,
                DynamicViscosity = 0.0000210,
                ConductivityTemperatureCoefficient = 5.0e-5,
                DynamicViscosityTemperatureCoefficient = 6.4e-8,
                SpecificHeatTemperatureCoefficient = 0.001,
                MolarMass = 39.948,
                SpecificHeatRatio = 1.67,
            };

            FluidMaterial fluidMaterial_Copy = new(fluidMaterial);
            Assert.Equal(fluidMaterial.Name, fluidMaterial_Copy.Name);
            Assert.Equal(0.01684, fluidMaterial_Copy.Conductivity);
            Assert.Equal(1.699, fluidMaterial_Copy.Density);
            Assert.Equal(519.0, fluidMaterial_Copy.SpecificHeat);
            Assert.Equal(0.0000210, fluidMaterial_Copy.DynamicViscosity);
            Assert.Equal(5.0e-5, fluidMaterial_Copy.ConductivityTemperatureCoefficient);
            Assert.Equal(6.4e-8, fluidMaterial_Copy.DynamicViscosityTemperatureCoefficient);
            Assert.Equal(0.001, fluidMaterial_Copy.SpecificHeatTemperatureCoefficient);
            Assert.Equal(39.948, fluidMaterial_Copy.MolarMass);
            Assert.Equal(1.67, fluidMaterial_Copy.SpecificHeatRatio);

            string? json = Core.Convert.ToSystem_String(fluidMaterial);
            Assert.NotNull(json);
            FluidMaterial? fluidMaterial_Json = Core.Convert.ToDiGi<FluidMaterial>(json)?.FirstOrDefault();
            Assert.NotNull(fluidMaterial_Json);
            Assert.Equal(0.0000210, fluidMaterial_Json.DynamicViscosity);
            Assert.Equal(519.0, fluidMaterial_Json.SpecificHeat);
            Assert.Equal(39.948, fluidMaterial_Json.MolarMass);
            Assert.Equal(6.4e-8, fluidMaterial_Json.DynamicViscosityTemperatureCoefficient);

            Core.xUnit.Query.SerializationCheck(fluidMaterial);
        }

        /// <summary>
        /// Tests construction, copy and serialization of <see cref="Classes.OpaqueMaterial"/> with every property populated, including the solar and light reflectance that DiGi-Organization/DiGi.Analytical#8 made settable.
        /// <para>The copy constructor is asserted directly, because <see cref="Core.xUnit.Query.SerializationCheck"/> clones through the JSON constructor and never runs it.</para>
        /// </summary>
        [Fact]
        public void OpaqueMaterial()
        {
            OpaqueMaterial opaqueMaterial = new("Full brick")
            {
                Conductivity = 0.77,
                Density = 1800.0,
                SpecificHeat = 880.0,
                VapourDiffusionFactor = 10.0,
                SolarReflectance = new SurfaceValue(0.3, 0.35),
                LightReflectance = new SurfaceValue(0.25, 0.3),
                Emissivity = new SurfaceValue(0.9, 0.93),
            };

            OpaqueMaterial opaqueMaterial_Copy = new(opaqueMaterial);
            Assert.Equal(0.77, opaqueMaterial_Copy.Conductivity);
            Assert.Equal(1800.0, opaqueMaterial_Copy.Density);
            Assert.Equal(880.0, opaqueMaterial_Copy.SpecificHeat);
            Assert.Equal(10.0, opaqueMaterial_Copy.VapourDiffusionFactor);
            Assert.Equal(0.35, opaqueMaterial_Copy.SolarReflectance?.External);
            Assert.Equal(0.25, opaqueMaterial_Copy.LightReflectance?.Internal);
            Assert.Equal(0.93, opaqueMaterial_Copy.Emissivity?.External);

            string? json = Core.Convert.ToSystem_String(opaqueMaterial);
            Assert.NotNull(json);
            OpaqueMaterial? opaqueMaterial_Json = Core.Convert.ToDiGi<OpaqueMaterial>(json)?.FirstOrDefault();
            Assert.NotNull(opaqueMaterial_Json);
            Assert.Equal(0.3, opaqueMaterial_Json.SolarReflectance?.Internal);
            Assert.Equal(0.3, opaqueMaterial_Json.LightReflectance?.External);

            Core.xUnit.Query.SerializationCheck(opaqueMaterial);
        }
    }
}
