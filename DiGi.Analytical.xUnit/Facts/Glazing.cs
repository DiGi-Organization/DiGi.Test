using DiGi.Analytical.Building;
using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Classes;
using DiGi.Analytical.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        private static TransparentMaterial Glazing_Glass(double emissivity_Internal, double emissivity_External)
        {
            return new TransparentMaterial("Float glass") { Conductivity = 1.0, Emissivity = new SurfaceValue(emissivity_Internal, emissivity_External) };
        }

        private static FluidMaterial Glazing_Air()
        {
            return new FluidMaterial("Air") { Conductivity = 0.02496, Density = 1.232, DynamicViscosity = 1.761e-5, SpecificHeat = 1008.0, MolarMass = 28.97 };
        }

        private static FluidMaterial Glazing_Argon()
        {
            return new FluidMaterial("Argon") { Conductivity = 0.01684, Density = 1.699, DynamicViscosity = 2.164e-5, SpecificHeat = 519.0, MolarMass = 39.948 };
        }

        /// <summary>
        /// Tests the gas mixture factory and the temperature dependence of fluid properties (DiGi-Organization/DiGi.Analytical#8): a 90 % argon / 10 % air mixture takes the volume-fraction weighted properties; fractions not adding up to 1, a zero fraction or a null gas give null; at the reference temperature the stored values come back, elsewhere they follow the temperature coefficients and the ideal-gas density.
        /// </summary>
        [Fact]
        public void Create_FluidMaterial_Mixture()
        {
            FluidMaterial fluidMaterial_Air = Glazing_Air();
            FluidMaterial fluidMaterial_Argon = Glazing_Argon();

            FluidMaterial? fluidMaterial_Mixture = Create.FluidMaterial("Argon 90 %", [(fluidMaterial_Argon, 0.9), (fluidMaterial_Air, 0.1)]);
            Assert.NotNull(fluidMaterial_Mixture);
            Assert.Equal("Argon 90 %", fluidMaterial_Mixture.Name);
            Assert.Equal((0.9 * 0.01684) + (0.1 * 0.02496), fluidMaterial_Mixture.Conductivity, 12);
            Assert.Equal((0.9 * 1.699) + (0.1 * 1.232), fluidMaterial_Mixture.Density, 12);
            Assert.Equal((0.9 * 2.164e-5) + (0.1 * 1.761e-5), fluidMaterial_Mixture.DynamicViscosity, 15);
            Assert.Equal((0.9 * 519.0) + (0.1 * 1008.0), fluidMaterial_Mixture.SpecificHeat, 9);
            Assert.Equal((0.9 * 39.948) + (0.1 * 28.97), fluidMaterial_Mixture.MolarMass, 9);

            Assert.Null(Create.FluidMaterial("Not 100 %", [(fluidMaterial_Argon, 0.9), (fluidMaterial_Air, 0.2)]));
            Assert.Null(Create.FluidMaterial("Zero fraction", [(fluidMaterial_Argon, 1.0), (fluidMaterial_Air, 0.0)]));
            Assert.Null(Create.FluidMaterial("Null gas", [(null!, 1.0)]));
            Assert.Null(Create.FluidMaterial("Empty", []));
            Assert.Null(Create.FluidMaterial("Nothing", null));

            // The stored values hold at 283 K; elsewhere they follow the coefficients and the ideal gas.
            FluidMaterial fluidMaterial = new("Air") { Conductivity = 0.025, Density = 1.25, DynamicViscosity = 1.75e-5, SpecificHeat = 1005.0, ConductivityTemperatureCoefficient = 7.76e-5, DynamicViscosityTemperatureCoefficient = 4.94e-8, SpecificHeatTemperatureCoefficient = 0.012324 };
            (double conductivity, double density, double dynamicViscosity, double specificHeat) = fluidMaterial.FluidProperties(Analytical.Classes.FluidMaterial.ReferenceTemperature);
            Assert.Equal(0.025, conductivity, 15);
            Assert.Equal(1.25, density, 15);
            Assert.Equal(1.75e-5, dynamicViscosity, 15);
            Assert.Equal(1005.0, specificHeat, 12);

            (double conductivity_303, double density_303, double dynamicViscosity_303, double specificHeat_303) = fluidMaterial.FluidProperties(303.0);
            Assert.Equal(0.025 + (20 * 7.76e-5), conductivity_303, 15);
            Assert.Equal(1.25 * 283.0 / 303.0, density_303, 12);
            Assert.Equal(1.75e-5 + (20 * 4.94e-8), dynamicViscosity_303, 15);
            Assert.Equal(1005.0 + (20 * 0.012324), specificHeat_303, 9);

            Assert.True(double.IsNaN(fluidMaterial.FluidProperties(0.0).Conductivity));
            Assert.True(double.IsNaN(((FluidMaterial?)null).FluidProperties(283.0).Density));
        }

        /// <summary>
        /// Tests the EN 673 glazing thermal transmittance Ug (DiGi-Organization/DiGi.Analytical#8).
        /// <para>Single 4 mm glass: 1/Ug = 1/23 + 0.004/1.0 + 1/8, Ug ≈ 5.80. Double 4-16 air-4 uncoated: ≈ 2.74 by hand. Double 4-16 argon-4 with a low-e coating (ε 0.03) on face 3: ≈ 1.07. A triple unit with two such spaces is lower still. Upward heat flow (horizontal glazing) convects more than vertical glazing, downward heat flow not at all. A layer sequence that is not glass-gas-glass, an infrared-transparent layer, a missing emissivity or a gas without data give NaN. ISO 6946 refuses a transparent layer.</para>
        /// </summary>
        [Fact]
        public void Query_GlazingThermalTransmittance()
        {
            TransparentMaterial transparentMaterial_Uncoated = Glazing_Glass(0.837, 0.837);
            TransparentMaterial transparentMaterial_LowE = Glazing_Glass(0.837, 0.03);
            FluidMaterial fluidMaterial_Air = Glazing_Air();
            FluidMaterial fluidMaterial_Argon = Glazing_Argon();

            static (IStructureLayer StructureLayer, IMaterial? Material) Layer(double thickness, IMaterial? material)
            {
                return (new StructureLayer(0, thickness), material);
            }

            double ug_Single = Building.Query.GlazingThermalTransmittance([Layer(0.004, transparentMaterial_Uncoated)]);
            Assert.Equal(1.0 / ((1.0 / 23.0) + 0.004 + (1.0 / 8.0)), ug_Single, 12);
            Assert.Equal(5.80, ug_Single, 2);

            double ug_Air = Building.Query.GlazingThermalTransmittance([Layer(0.004, transparentMaterial_Uncoated), Layer(0.016, fluidMaterial_Air), Layer(0.004, transparentMaterial_Uncoated)]);
            Assert.InRange(ug_Air, 2.70, 2.78);

            (IStructureLayer StructureLayer, IMaterial? Material)[] layers_Argon = [Layer(0.004, transparentMaterial_Uncoated), Layer(0.016, fluidMaterial_Argon), Layer(0.004, transparentMaterial_LowE)];
            double ug_Argon = Building.Query.GlazingThermalTransmittance(layers_Argon);
            Assert.InRange(ug_Argon, 1.03, 1.10);

            // Triple glazing with a low-e coating in each space, on faces 2 and 5.
            double ug_Triple = Building.Query.GlazingThermalTransmittance([Layer(0.004, Glazing_Glass(0.03, 0.837)), Layer(0.016, fluidMaterial_Argon), Layer(0.004, transparentMaterial_Uncoated), Layer(0.016, fluidMaterial_Argon), Layer(0.004, transparentMaterial_LowE)]);
            Assert.InRange(ug_Triple, 0.45, 0.70);
            Assert.True(ug_Triple < ug_Argon);

            // Heat flowing upward convects more, downward not at all.
            double ug_Upward = Building.Query.GlazingThermalTransmittance(layers_Argon, HeatFlowDirection.Upward);
            double ug_Downward = Building.Query.GlazingThermalTransmittance(layers_Argon, HeatFlowDirection.Downward);
            Assert.True(ug_Upward > ug_Argon, $"upward {ug_Upward} not above vertical {ug_Argon}");
            Assert.True(ug_Downward <= ug_Argon, $"downward {ug_Downward} above vertical {ug_Argon}");

            // A gas mixture is a gas like any other.
            FluidMaterial? fluidMaterial_Mixture = Create.FluidMaterial("Argon 90 %", [(fluidMaterial_Argon, 0.9), (fluidMaterial_Air, 0.1)]);
            double ug_Mixture = Building.Query.GlazingThermalTransmittance([Layer(0.004, transparentMaterial_Uncoated), Layer(0.016, fluidMaterial_Mixture), Layer(0.004, transparentMaterial_LowE)]);
            Assert.True(ug_Mixture > ug_Argon && ug_Mixture < ug_Air);

            // Not glass-gas-glass, infrared-transparent, no emissivity, no gas data: NaN.
            Assert.True(double.IsNaN(Building.Query.GlazingThermalTransmittance([Layer(0.004, transparentMaterial_Uncoated), Layer(0.004, transparentMaterial_Uncoated), Layer(0.004, transparentMaterial_Uncoated)])));
            Assert.True(double.IsNaN(Building.Query.GlazingThermalTransmittance([Layer(0.016, fluidMaterial_Air)])));
            Assert.True(double.IsNaN(Building.Query.GlazingThermalTransmittance([Layer(0.004, transparentMaterial_Uncoated), Layer(0.016, fluidMaterial_Air)])));
            Assert.True(double.IsNaN(Building.Query.GlazingThermalTransmittance([Layer(0.004, new TransparentMaterial("Film") { Conductivity = 0.2, Emissivity = new SurfaceValue(0.8, 0.8), InfraredTransmittance = 0.6 })])));
            Assert.True(double.IsNaN(Building.Query.GlazingThermalTransmittance([Layer(0.004, new TransparentMaterial("No emissivity") { Conductivity = 1.0 })])));
            Assert.True(double.IsNaN(Building.Query.GlazingThermalTransmittance([Layer(0.004, transparentMaterial_Uncoated), Layer(0.016, new FluidMaterial("Unknown gas")), Layer(0.004, transparentMaterial_Uncoated)])));
            Assert.True(double.IsNaN(Building.Query.GlazingThermalTransmittance(layers_Argon, HeatFlowDirection.Undefined)));
            Assert.True(double.IsNaN(Building.Query.GlazingThermalTransmittance(null)));

            // ISO 6946 does not apply to glazing.
            Assert.True(double.IsNaN(Building.Query.ThermalTransmittance(layers_Argon, HeatFlowDirection.Horizontal)));
        }

        /// <summary>
        /// Tests the window thermal transmittance Uw with Ug calculated per EN 673 from the pane layers when it is not declared (DiGi-Organization/DiGi.Analytical#8): Uw = (Ag·Ug + Af·Uf + lg·Ψg) / (Ag + Af) with the calculated Ug, the same through the cluster and the building model; a declared Ug wins over the layers.
        /// </summary>
        [Fact]
        public void Query_ThermalTransmittance_WindowLayers()
        {
            TransparentMaterial transparentMaterial_Uncoated = Glazing_Glass(0.837, 0.837);
            TransparentMaterial transparentMaterial_LowE = Glazing_Glass(0.837, 0.03);
            FluidMaterial fluidMaterial_Argon = Glazing_Argon();
            OpaqueMaterial opaqueMaterial_PVC = new("PVC profile") { Conductivity = 0.17 };

            WindowConstruction windowConstruction = new("PVC window, 4-16Ar-4 low-e", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.FrameThermalTransmittance, 1.3));
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.GlazingLinearThermalTransmittance, 0.04));

            ConstructionRelationCluster constructionRelationCluster = new([windowConstruction, transparentMaterial_Uncoated, transparentMaterial_LowE, fluidMaterial_Argon, opaqueMaterial_PVC]);
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Pane, transparentMaterial_Uncoated, 0.004));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Pane, fluidMaterial_Argon, 0.016));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Pane, transparentMaterial_LowE, 0.004));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Frame, opaqueMaterial_PVC, 0.07));

            double ug = Building.Query.GlazingThermalTransmittance([(new StructureLayer(0, 0.004), transparentMaterial_Uncoated), (new StructureLayer(1, 0.016), fluidMaterial_Argon), (new StructureLayer(2, 0.004), transparentMaterial_LowE)]);
            double uw_Expected = ((1.3 * ug) + (0.5 * 1.3) + (4.6 * 0.04)) / 1.8;

            Assert.Equal(uw_Expected, constructionRelationCluster.ThermalTransmittance(windowConstruction), 12);
            Assert.Equal(uw_Expected, windowConstruction.ThermalTransmittance(ug), 12);

            // Without the layers there is no Ug and no declared Uw.
            Assert.True(double.IsNaN(windowConstruction.ThermalTransmittance()));

            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Update(constructionRelationCluster, windowConstruction));
            Assert.Equal(uw_Expected, buildingModel.ThermalTransmittance(windowConstruction), 12);

            // A declared Ug wins over the layers.
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.GlazingThermalTransmittance, 1.0));
            Assert.Equal(((1.3 * 1.0) + (0.5 * 1.3) + (4.6 * 0.04)) / 1.8, constructionRelationCluster.ThermalTransmittance(windowConstruction), 12);
            Assert.Equal(windowConstruction.ThermalTransmittance(), constructionRelationCluster.ThermalTransmittance(windowConstruction), 12);
        }
    }
}
