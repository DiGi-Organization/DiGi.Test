using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Energy;
using DiGi.Analytical.Building.Energy.Classes;
using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Analytical.Building.Regulation.Enums;
using System.Globalization;
using System.Reflection;

namespace DiGi.EnergyPlus.xUnit
{
    public partial class Facts
    {
        private static readonly TypologyData energyPlus_TypologyData = new(0.3, 0.1, FloorBoundary.Soil, 0.0, RoofType.Flat, 0.5, 0.9, 0, "test");

        /// <summary>
        /// Tests <see cref="Convert.ToEnergyPlus_String(BuildingModel?, EPW.Classes.EPWFile?, MonthlyClimate?, TypologyData?, HeatDemandResult?, IEnumerable{BuildingModel?}?, double)"/> on the in-code box (DiGi-Organization/DiGi.EnergyPlus#1): one zone with the calculation's volume and floor area, six shell surfaces plus the thermal-bridge surface, two windows on the south wall, and every steady-state value equal to the monthly calculation's.
        /// <para>The wall's layers (R 2.846) fall short of its declared U 0.30, so a massless layer is added outermost and the layers follow in DiGi's order; the roof's layers (R 5.075) exceed its declared U 0.25, so they are scaled down. Either way the resistance between the faces is 1/U − R_si − R_se. The ground floor faces the monthly external temperature through U = H_g / A, the thermal-bridge surface carries H_tb, and the gains, the ventilation flow and the set-point are the calculation's. The text is the same under pl-PL, and is written to the reports directory as <c>EnergyPlus_Box.idf</c>.</para>
        /// </summary>
        [Fact]
        public void ToEnergyPlus_String_Box()
        {
            (BuildingModel buildingModel, Dictionary<string, Guid> guids) = EnergyPlus_Model(false, false, false, false);
            MonthlyClimate monthlyClimate = EnergyPlus_Climate();

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(monthlyClimate, energyPlus_TypologyData);
            Assert.NotNull(heatDemandResult);

            string? text = buildingModel.ToEnergyPlus_String(null, monthlyClimate, energyPlus_TypologyData, heatDemandResult, null);
            Assert.NotNull(text);

            Assert.Null(buildingModel.ToEnergyPlus_String(null, monthlyClimate, energyPlus_TypologyData, null, null));
            Assert.Null(buildingModel.ToEnergyPlus_String(null, null, energyPlus_TypologyData, heatDemandResult, null));
            Assert.Null(Convert.ToEnergyPlus_String(null, null, monthlyClimate, energyPlus_TypologyData, heatDemandResult, null));

            // Kept for a run by hand: the box is the first input EnergyPlus is run on (DiGi-Organization/DiGi.EnergyPlus#1).
            string? path_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(path_Reports);
            File.WriteAllText(Path.Combine(path_Reports, "EnergyPlus_Box.idf"), text);

            List<(string ClassName, List<string> Fields)> objects = EnergyPlus_Objects(text);

            Assert.Equal(Constants.Default.Version, objects.Single(x => x.ClassName == "Version").Fields[0]);

            List<string> fields_Zone = objects.Single(x => x.ClassName == "Zone").Fields;
            Assert.Equal(Constants.Name.Zone, fields_Zone[0]);
            Assert.Equal(240.0, EnergyPlus_Double(fields_Zone[8]), 9);
            Assert.Equal(80.0, EnergyPlus_Double(fields_Zone[9]), 9);

            // Six faces of the shell and the thermal-bridge surface; the floor and the thermal bridges face other-side coefficients.
            List<List<string>> surfaces = objects.FindAll(x => x.ClassName == "BuildingSurface:Detailed").ConvertAll(x => x.Fields);
            Assert.Equal(7, surfaces.Count);
            Assert.Equal(5, surfaces.Count(x => x[5] == "Outdoors"));
            Assert.Equal(2, surfaces.Count(x => x[5] == "OtherSideCoefficients"));
            Assert.Empty(surfaces.FindAll(x => x[5] == "Adiabatic"));

            List<string> fields_Floor = EnergyPlus_Object(objects, "BuildingSurface:Detailed", guids["Floor"].ToString("D"));
            Assert.Equal("Floor", fields_Floor[1]);
            Assert.Equal(Constants.Name.GroundOtherSideCoefficients, fields_Floor[6]);
            Assert.Equal("NoSun", fields_Floor[7]);

            // Origin at the lowest corner: the floor lies at zero height.
            Assert.Equal(4, int.Parse(fields_Floor[10], CultureInfo.InvariantCulture));
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(0.0, EnergyPlus_Double(fields_Floor[11 + (3 * i) + 2]), 12);
            }

            // Windows: U the declared Uw, SHGC = g·A_g / A_w with the pane face, or (1 − F_f)·A_w without panes.
            List<List<string>> fenestrations = objects.FindAll(x => x.ClassName == "FenestrationSurface:Detailed").ConvertAll(x => x.Fields);
            Assert.Equal(2, fenestrations.Count);
            Assert.All(fenestrations, x => Assert.Equal("Window", x[1]));
            Assert.All(fenestrations, x => Assert.Equal(guids["South"].ToString("D"), x[3]));

            List<string> fields_Pane = EnergyPlus_Object(objects, "FenestrationSurface:Detailed", guids["Window Pane"].ToString("D"));
            List<string> fields_GlazingPane = EnergyPlus_Object(objects, "WindowMaterial:SimpleGlazingSystem", EnergyPlus_Object(objects, "Construction", fields_Pane[2])[1]);
            Assert.Equal(1.3, EnergyPlus_Double(fields_GlazingPane[1]), 9);
            Assert.Equal(0.6 * 1.0 * 1.3 / (1.2 * 1.5), EnergyPlus_Double(fields_GlazingPane[2]), 9);

            List<string> fields_NoPane = EnergyPlus_Object(objects, "FenestrationSurface:Detailed", guids["Window NoPane"].ToString("D"));
            List<string> fields_GlazingNoPane = EnergyPlus_Object(objects, "WindowMaterial:SimpleGlazingSystem", EnergyPlus_Object(objects, "Construction", fields_NoPane[2])[1]);
            Assert.Equal(0.6 * (1.0 - 0.3), EnergyPlus_Double(fields_GlazingNoPane[2]), 9);

            // Wall: a massless adjustment outermost, then the layers in DiGi's order, outermost first.
            string constructionName_Wall = EnergyPlus_Object(objects, "BuildingSurface:Detailed", guids["South"].ToString("D"))[2];
            List<string> materials_Wall = EnergyPlus_Object(objects, "Construction", constructionName_Wall).Skip(1).ToList();
            Assert.Equal(4, materials_Wall.Count);
            Assert.Equal("Material:NoMass", objects.Single(x => x.ClassName.StartsWith("Material", StringComparison.Ordinal) && x.Fields[0] == materials_Wall[0]).ClassName);
            Assert.StartsWith("Insulation 100 mm", materials_Wall[1]);
            Assert.StartsWith("Brick 250 mm", materials_Wall[2]);
            Assert.StartsWith("Plaster 15 mm", materials_Wall[3]);
            Assert.Equal((1.0 / 0.30) - 0.13 - 0.04, EnergyPlus_ThermalResistance(objects, constructionName_Wall), 9);

            // Roof: the layers' resistance exceeds the declared U's, so it is scaled down and both layers keep their mass.
            string constructionName_Roof = EnergyPlus_Object(objects, "BuildingSurface:Detailed", guids["Roof"].ToString("D"))[2];
            List<string> materials_Roof = EnergyPlus_Object(objects, "Construction", constructionName_Roof).Skip(1).ToList();
            Assert.Equal(2, materials_Roof.Count);
            Assert.All(materials_Roof, x => Assert.Equal("Material", objects.Single(y => y.ClassName.StartsWith("Material", StringComparison.Ordinal) && y.Fields[0] == x).ClassName));
            Assert.Equal((1.0 / 0.25) - 0.10 - 0.04, EnergyPlus_ThermalResistance(objects, constructionName_Roof), 9);

            // Ground: U = H_g / A to the monthly mean external temperature, the outside face held at it (no R_se).
            Assert.Equal((80.0 / heatDemandResult.GroundHeatTransferCoefficient) - 0.17, EnergyPlus_ThermalResistance(objects, fields_Floor[2]), 9);
            List<string> fields_Ground = EnergyPlus_Object(objects, "Schedule:Compact", "Monthly External Temperature");
            Assert.Equal(-2.0, EnergyPlus_Double(fields_Ground[5]), 12);
            Assert.Equal(0.0, EnergyPlus_Double(fields_Ground[^1]), 12);

            // Thermal bridges: area H_tb / U_tb, resistance 1/U_tb − R_si.
            List<string> fields_ThermalBridge = EnergyPlus_Object(objects, "BuildingSurface:Detailed", Constants.Name.ThermalBridgeSurface);
            Assert.Equal(Constants.Name.ThermalBridgeOtherSideCoefficients, fields_ThermalBridge[6]);
            double side_Y = EnergyPlus_Double(fields_ThermalBridge[11 + 1]) - EnergyPlus_Double(fields_ThermalBridge[11 + 7]);
            double side_Z = EnergyPlus_Double(fields_ThermalBridge[11 + 2]) - EnergyPlus_Double(fields_ThermalBridge[11 + 5]);
            Assert.Equal(heatDemandResult.ThermalBridgeHeatTransferCoefficient / Constants.Default.ThermalBridgeThermalTransmittance, side_Y * side_Z, 6);
            Assert.Equal((1.0 / Constants.Default.ThermalBridgeThermalTransmittance) - 0.13, EnergyPlus_ThermalResistance(objects, fields_ThermalBridge[2]), 9);

            // Loads: 5 W/m2 over 80 m2, the flow carrying H_ve, the 20 °C set-point.
            Assert.Equal(400.0, EnergyPlus_Double(EnergyPlus_Object(objects, "OtherEquipment", "Internal Gains")[5]), 6);
            Assert.Equal(heatDemandResult.VentilationHeatTransferCoefficient / 1200.0, EnergyPlus_Double(EnergyPlus_Object(objects, "ZoneVentilation:DesignFlowRate", "Ventilation and Infiltration")[4]), 9);
            Assert.Equal("Standard", EnergyPlus_Object(objects, "ZoneVentilation:DesignFlowRate", "Ventilation and Infiltration")[^1]);
            Assert.Equal(20.0, EnergyPlus_Double(EnergyPlus_Object(objects, "Schedule:Constant", "Heating Setpoint Temperature")[2]), 12);
            Assert.Single(objects, x => x.ClassName == "ZoneHVAC:IdealLoadsAirSystem");

            // Numbers are written in the invariant culture.
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
                Assert.Equal(text, buildingModel.ToEnergyPlus_String(null, monthlyClimate, energyPlus_TypologyData, heatDemandResult, null));
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }

        /// <summary>
        /// Tests the boundaries and constructions of <see cref="Convert.ToEnergyPlus_String(BuildingModel?, EPW.Classes.EPWFile?, MonthlyClimate?, TypologyData?, HeatDemandResult?, IEnumerable{BuildingModel?}?, double)"/> that the box does not reach (DiGi-Organization/DiGi.EnergyPlus#1).
        /// <para>A party wall is adiabatic and keeps its layers unchanged, for their mass. A construction with a declared U and no layers is one <c>Material:NoMass</c> of R = 1/U − R_si − R_se. A five-sided window is written as three triangles, since EnergyPlus takes three or four vertices per fenestration surface.</para>
        /// </summary>
        [Fact]
        public void ToEnergyPlus_String_Boundaries()
        {
            (BuildingModel buildingModel, Dictionary<string, Guid> guids) = EnergyPlus_Model(true, true, true, false);
            MonthlyClimate monthlyClimate = EnergyPlus_Climate();

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(monthlyClimate, energyPlus_TypologyData);
            Assert.NotNull(heatDemandResult);

            string? text = buildingModel.ToEnergyPlus_String(null, monthlyClimate, energyPlus_TypologyData, heatDemandResult, null);
            Assert.NotNull(text);

            List<(string ClassName, List<string> Fields)> objects = EnergyPlus_Objects(text);

            List<string> fields_North = EnergyPlus_Object(objects, "BuildingSurface:Detailed", guids["North"].ToString("D"));
            Assert.Equal("Adiabatic", fields_North[5]);
            List<string> materials_North = EnergyPlus_Object(objects, "Construction", fields_North[2]).Skip(1).ToList();
            Assert.Equal(3, materials_North.Count);
            Assert.StartsWith("Insulation 100 mm", materials_North[0]);
            Assert.StartsWith("Plaster 15 mm", materials_North[2]);

            List<string> fields_West = EnergyPlus_Object(objects, "BuildingSurface:Detailed", guids["West"].ToString("D"));
            Assert.Equal("Outdoors", fields_West[5]);
            List<string> materials_West = EnergyPlus_Object(objects, "Construction", fields_West[2]).Skip(1).ToList();
            Assert.Single(materials_West);
            List<string> fields_NoMass = EnergyPlus_Object(objects, "Material:NoMass", materials_West[0]);
            Assert.Equal((1.0 / 0.5) - 0.13 - 0.04, EnergyPlus_Double(fields_NoMass[2]), 9);

            List<List<string>> fenestrations_Pentagon = objects.FindAll(x => x.ClassName == "FenestrationSurface:Detailed" && x.Fields[0].StartsWith(guids["Window Pentagon"].ToString("D"), StringComparison.Ordinal)).ConvertAll(x => x.Fields);
            Assert.Equal(3, fenestrations_Pentagon.Count);
            Assert.All(fenestrations_Pentagon, x => Assert.Equal(3, int.Parse(x[8], CultureInfo.InvariantCulture)));

            double area = 0.0;
            foreach (List<string> fields in fenestrations_Pentagon)
            {
                // Triangles in the plane y = 0: area from X and Z.
                double x1 = EnergyPlus_Double(fields[9]), z1 = EnergyPlus_Double(fields[11]);
                double x2 = EnergyPlus_Double(fields[12]), z2 = EnergyPlus_Double(fields[14]);
                double x3 = EnergyPlus_Double(fields[15]), z3 = EnergyPlus_Double(fields[17]);
                area += System.Math.Abs(((x2 - x1) * (z3 - z1)) - ((x3 - x1) * (z2 - z1))) / 2.0;
            }

            Assert.Equal((1.2 * 1.0) + (0.5 * 1.2 * 0.5), area, 9);
        }

        /// <summary>
        /// Tests the internal mass of <see cref="Convert.ToEnergyPlus_String(BuildingModel?, EPW.Classes.EPWFile?, MonthlyClimate?, TypologyData?, HeatDemandResult?, IEnumerable{BuildingModel?}?, double)"/> on two storeys (DiGi-Organization/DiGi.EnergyPlus#1): the floor between them bounds both spaces, so it is no surface of the zone but two internal masses of its area, each the half of the construction on its side - 100 mm of insulation on the side of the first layer, 100 mm of concrete on the side of the last - so the mass is counted once. Only the lower floor faces the ground.
        /// </summary>
        [Fact]
        public void ToEnergyPlus_String_InternalMass()
        {
            (BuildingModel buildingModel, Dictionary<string, Guid> guids) = EnergyPlus_Model(false, false, false, true);
            MonthlyClimate monthlyClimate = EnergyPlus_Climate();

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(monthlyClimate, energyPlus_TypologyData);
            Assert.NotNull(heatDemandResult);

            string? text = buildingModel.ToEnergyPlus_String(null, monthlyClimate, energyPlus_TypologyData, heatDemandResult, null);
            Assert.NotNull(text);

            List<(string ClassName, List<string> Fields)> objects = EnergyPlus_Objects(text);

            List<List<string>> surfaces = objects.FindAll(x => x.ClassName == "BuildingSurface:Detailed").ConvertAll(x => x.Fields);
            Assert.Equal(11, surfaces.Count);
            Assert.Equal(9, surfaces.Count(x => x[5] == "Outdoors"));
            Assert.DoesNotContain(surfaces, x => x[0] == guids["Floor Upper"].ToString("D"));
            Assert.Equal(Constants.Name.GroundOtherSideCoefficients, EnergyPlus_Object(objects, "BuildingSurface:Detailed", guids["Floor"].ToString("D"))[6]);

            List<List<string>> internalMasses = objects.FindAll(x => x.ClassName == "InternalMass").ConvertAll(x => x.Fields);
            Assert.Equal(2, internalMasses.Count);
            Assert.All(internalMasses, x => Assert.Equal(80.0, EnergyPlus_Double(x[4]), 9));

            List<string> materials_1 = EnergyPlus_Object(objects, "Construction", internalMasses[0][1]).Skip(1).ToList();
            List<string> materials_2 = EnergyPlus_Object(objects, "Construction", internalMasses[1][1]).Skip(1).ToList();
            Assert.Equal("Insulation 100 mm", Assert.Single(materials_1));
            Assert.Equal("Concrete 100 mm", Assert.Single(materials_2));
        }
    }
}
