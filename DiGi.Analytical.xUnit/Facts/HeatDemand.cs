using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Energy;
using DiGi.Analytical.Building.Energy.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.HVAC;
using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Building.Regulation;
using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Analytical.Building.Regulation.Enums;
using DiGi.Analytical.Classes;
using DiGi.Core.Parameter.Classes;
using DiGi.Geometry.Spatial.Classes;
using System.Globalization;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        private static readonly double[] heatDemand_ExternalTemperatures = [-2.0, -1.0, 3.0, 8.0, 13.0, 16.0, 18.0, 18.0, 14.0, 9.0, 4.0, 0.0];

        private static readonly double[] heatDemand_IrradiationsSouth = [30.0, 45.0, 75.0, 90.0, 95.0, 90.0, 95.0, 95.0, 80.0, 60.0, 32.0, 22.0];

        private static readonly double[] heatDemand_IrradiationsRoof = [20.0, 35.0, 70.0, 110.0, 150.0, 155.0, 160.0, 140.0, 95.0, 55.0, 22.0, 14.0];

        /// <summary>
        /// Tests <see cref="Building.Energy.Create.HeatDemandResult(BuildingModel?, MonthlyClimate?, TypologyData?, HeatDemandOptions?, double)"/> on an in-code box (DiGi-Organization/DiGi.Analytical#21): every heat transfer coefficient, the internal heat capacity, the areas and one month of the balance are asserted exactly against the box's own arithmetic.
        /// <para>The 10 x 8 x 3 m box has one space, layered walls, roof and floor with declared U (0.30, 0.25 and 0.35), and two 1.2 x 1.5 m windows (Uw 1.3, g 0.6) in the south wall: one with a 1.0 x 1.3 m pane, one without panes, which takes (1 − F_f)·A_w and is counted once. The floor lies on the ground, so its declared 0.35 must not appear: H_g follows PN-EN ISO 13370 from its layers. Only the south wall and the roof have irradiation, so three surfaces are counted as missing.</para>
        /// </summary>
        [Fact]
        public void HeatDemand_Box()
        {
            (BuildingModel buildingModel, Dictionary<string, Guid> guids) = HeatDemand_Model(true, 5.0);

            TypologyData typologyData = new(0.3, 0.1, FloorBoundary.Soil, 0.0, RoofType.Flat, 0.5, 0.9, 0, "test");
            MonthlyClimate monthlyClimate = HeatDemand_Climate(guids, true);

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(monthlyClimate, typologyData);
            Assert.NotNull(heatDemandResult);

            double area_Windows = 2 * 1.2 * 1.5;
            double area_WallSouth = (10 * 3) - area_Windows;
            double area_Walls = area_WallSouth + (8 * 3) + (10 * 3) + (8 * 3);
            double area_Roof = 10 * 8;
            double area_Floor = 10 * 8;

            Assert.Equal(80.0, heatDemandResult.ReferenceFloorArea, 9);
            Assert.Equal(240.0, heatDemandResult.HeatedVolume, 9);
            Assert.Equal(20.0, heatDemandResult.InternalTemperature, 12);

            double directHeatTransferCoefficient = (0.30 * area_Walls) + (0.25 * area_Roof) + (1.3 * area_Windows);
            Assert.Equal(directHeatTransferCoefficient, heatDemandResult.DirectHeatTransferCoefficient, 9);

            double area_HeatLoss = area_Walls + area_Roof + area_Windows + area_Floor;
            Assert.Equal(0.1 * area_HeatLoss, heatDemandResult.ThermalBridgeHeatTransferCoefficient, 9);

            // PN-EN ISO 13370 from the floor's layers (R_f = 0.1 / 0.04 + 0.1 / 2.0), the walls' total thickness (0.1 + 0.25 + 0.015 m) and the 36 m perimeter.
            double groundThermalTransmittance = Building.Energy.Query.GroundThermalTransmittance(area_Floor, 36.0, 0.365, 2.55, 2.0);
            Assert.Equal(groundThermalTransmittance * area_Floor, heatDemandResult.GroundHeatTransferCoefficient, 9);
            Assert.NotEqual(0.35 * area_Floor, heatDemandResult.GroundHeatTransferCoefficient, 3);
            Assert.Equal(0.0, heatDemandResult.UnheatedSpaceHeatTransferCoefficient, 12);

            // H_ve = 1200 · (0.3 l/(s m2) · 80 m2 / 1000 + 0.3 1/h · 240 m3 / 3600)
            Assert.Equal(1200.0 * ((0.3 * 80.0 / 1000.0) + (0.3 * 240.0 / 3600.0)), heatDemandResult.VentilationHeatTransferCoefficient, 9);

            // κ from the inside: walls 15 mm plaster + 85 mm brick, roof and floor 100 mm concrete.
            double arealHeatCapacity_Wall = (1600.0 * 1000.0 * 0.015) + (1800.0 * 880.0 * 0.085);
            double arealHeatCapacity_Concrete = 2400.0 * 1000.0 * 0.1;
            double internalHeatCapacity = (arealHeatCapacity_Wall * area_Walls) + (arealHeatCapacity_Concrete * area_Roof) + (arealHeatCapacity_Concrete * area_Floor);
            Assert.Equal(internalHeatCapacity, heatDemandResult.InternalHeatCapacity, 3);

            double heatTransferCoefficient = heatDemandResult.DirectHeatTransferCoefficient + heatDemandResult.ThermalBridgeHeatTransferCoefficient + heatDemandResult.GroundHeatTransferCoefficient + heatDemandResult.VentilationHeatTransferCoefficient;
            Assert.Equal(internalHeatCapacity / 3600.0 / heatTransferCoefficient, heatDemandResult.TimeConstant, 9);

            Assert.Equal(1, heatDemandResult.GlazingFallbackCount);
            Assert.Equal(3, heatDemandResult.MissingIrradiationCount);
            Assert.Empty(heatDemandResult.ExcludedComponents ?? ["missing"]);
            Assert.Equal(Building.Energy.Constants.Default.HeatDemandMethodVersion, heatDemandResult.MethodVersion);

            // January: solar gains of both windows and both irradiated opaque surfaces, less the sky radiation of every external element.
            double area_Glazed = (1.0 * 1.3) + ((1.0 - 0.3) * 1.2 * 1.5);
            double solarHeatGain = (0.9 * 0.6 * area_Glazed * heatDemand_IrradiationsSouth[0]) + (0.6 * 0.04 * 0.30 * area_WallSouth * heatDemand_IrradiationsSouth[0]) + (0.6 * 0.04 * 0.25 * area_Roof * heatDemand_IrradiationsRoof[0]);
            List<double>? solarHeatGains = heatDemandResult.SolarHeatGains;
            Assert.NotNull(solarHeatGains);
            Assert.Equal(solarHeatGain, solarHeatGains[0], 9);

            double skyCoefficient = (0.5 * 0.04 * ((0.30 * area_Walls) + (1.3 * area_Windows))) + (1.0 * 0.04 * 0.25 * area_Roof);
            List<double>? skyRadiationHeatTransfers = heatDemandResult.SkyRadiationHeatTransfers;
            Assert.NotNull(skyRadiationHeatTransfers);
            Assert.Equal(skyCoefficient * 4.5 * 11.0 * 744.0 / 1000.0, skyRadiationHeatTransfers[0], 9);

            double heatTransfer = heatTransferCoefficient * (20.0 - heatDemand_ExternalTemperatures[0]) * 744.0 / 1000.0;
            double heatGain = (5.0 * 80.0 * 744.0 / 1000.0) + solarHeatGain - skyRadiationHeatTransfers[0];
            double a = 1.0 + (heatDemandResult.TimeConstant / 15.0);
            double gainUtilisationFactor = Building.Energy.Query.UtilisationFactor(heatGain / heatTransfer, a);

            List<double>? heatingEnergyNeeds = heatDemandResult.HeatingEnergyNeeds;
            Assert.NotNull(heatingEnergyNeeds);
            Assert.Equal(12, heatingEnergyNeeds.Count);
            Assert.Equal(heatTransfer - (gainUtilisationFactor * heatGain), heatingEnergyNeeds[0], 9);
            Assert.Equal(heatingEnergyNeeds.Sum(), heatDemandResult.HeatingEnergyNeed, 9);
            Assert.Equal(heatDemandResult.HeatingEnergyNeed / 80.0, heatDemandResult.HeatingEnergyNeedPerArea, 9);

            // In July the gains exceed the losses (γ > 1): the monthly method leaves a small need, far below January's.
            List<double>? heatBalanceRatios = heatDemandResult.HeatBalanceRatios;
            Assert.NotNull(heatBalanceRatios);
            Assert.True(heatBalanceRatios[6] > 1.0);
            Assert.True(heatingEnergyNeeds[6] < 0.1 * heatingEnergyNeeds[0]);

            // Design heat load at -20 °C with the ground corrected by f_g1 · f_g2.
            double designHeatLoad = (heatDemandResult.DirectHeatTransferCoefficient + heatDemandResult.ThermalBridgeHeatTransferCoefficient + (1.45 * ((20.0 - 7.6) / 40.0) * heatDemandResult.GroundHeatTransferCoefficient) + heatDemandResult.VentilationHeatTransferCoefficient) * 40.0;
            Assert.Equal(designHeatLoad, heatDemandResult.DesignHeatLoad, 9);
            Assert.Equal(heatDemandResult.VentilationHeatTransferCoefficient * 40.0, heatDemandResult.DesignVentilationHeatLoss, 9);
            Assert.Equal(designHeatLoad - heatDemandResult.DesignVentilationHeatLoss, heatDemandResult.DesignTransmissionHeatLoss, 9);

            Dictionary<string, string>? sources = heatDemandResult.Sources;
            Assert.NotNull(sources);
            Assert.Equal("external shell", sources["HeatedVolume"]);
            Assert.StartsWith("typology data", sources["ThermalBridgingSurcharge"]);
        }

        /// <summary>
        /// Tests that with no gains every month's need is the whole heat transfer, Q_H,nd,m = H·(θ_int − θ_e,m)·t_m, exactly (DiGi-Organization/DiGi.Analytical#21).
        /// <para>The box has no internal gain, the climate no irradiation and the sky radiation is switched off, so η plays no part.</para>
        /// </summary>
        [Fact]
        public void HeatDemand_ZeroGains()
        {
            (BuildingModel buildingModel, Dictionary<string, Guid> guids) = HeatDemand_Model(true, 0.0);

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(HeatDemand_Climate(guids, false), null, new HeatDemandOptions() { IncludeSkyRadiation = false });
            Assert.NotNull(heatDemandResult);
            Assert.Equal(5, heatDemandResult.MissingIrradiationCount);

            double heatTransferCoefficient = heatDemandResult.DirectHeatTransferCoefficient + heatDemandResult.ThermalBridgeHeatTransferCoefficient + heatDemandResult.GroundHeatTransferCoefficient + heatDemandResult.UnheatedSpaceHeatTransferCoefficient + heatDemandResult.VentilationHeatTransferCoefficient;

            List<double>? heatingEnergyNeeds = heatDemandResult.HeatingEnergyNeeds;
            Assert.NotNull(heatingEnergyNeeds);
            for (int i = 0; i < 12; i++)
            {
                Assert.Equal(heatTransferCoefficient * (20.0 - heatDemand_ExternalTemperatures[i]) * Building.Energy.Query.MonthDuration(i + 1) / 1000.0, heatingEnergyNeeds[i], 9);
            }

            // Without typology data the options' fallbacks apply and say so.
            Dictionary<string, string>? sources = heatDemandResult.Sources;
            Assert.NotNull(sources);
            Assert.StartsWith("option", sources["ThermalBridgingSurcharge"]);
        }

        /// <summary>
        /// Tests that a component whose U is not known is excluded and listed, never counted as 0 (DiGi-Organization/DiGi.Analytical#21): against the same box with the east wall known, H_D falls by exactly U·A of that wall and H_tb by ΔU_tb·A, and the wall's identifier is the only one listed.
        /// </summary>
        [Fact]
        public void HeatDemand_UnknownThermalTransmittance()
        {
            (BuildingModel buildingModel_Known, Dictionary<string, Guid> guids_Known) = HeatDemand_Model(true, 5.0);
            (BuildingModel buildingModel_Unknown, Dictionary<string, Guid> guids_Unknown) = HeatDemand_Model(false, 5.0);

            TypologyData typologyData = new(0.3, 0.1, FloorBoundary.Soil, 0.0, RoofType.Flat, 0.5, 0.9, 0, "test");

            HeatDemandResult? heatDemandResult_Known = buildingModel_Known.HeatDemandResult(HeatDemand_Climate(guids_Known, true), typologyData);
            HeatDemandResult? heatDemandResult_Unknown = buildingModel_Unknown.HeatDemandResult(HeatDemand_Climate(guids_Unknown, true), typologyData);
            Assert.NotNull(heatDemandResult_Known);
            Assert.NotNull(heatDemandResult_Unknown);

            Assert.Equal([guids_Unknown["East"].ToString("D")], heatDemandResult_Unknown.ExcludedComponents);
            Assert.Equal(heatDemandResult_Known.DirectHeatTransferCoefficient - (0.30 * 24.0), heatDemandResult_Unknown.DirectHeatTransferCoefficient, 9);
            Assert.Equal(heatDemandResult_Known.ThermalBridgeHeatTransferCoefficient - (0.1 * 24.0), heatDemandResult_Unknown.ThermalBridgeHeatTransferCoefficient, 9);
            Assert.True(heatDemandResult_Unknown.HeatingEnergyNeed < heatDemandResult_Known.HeatingEnergyNeed);
        }

        /// <summary>
        /// Tests the floor over an unheated cellar (DiGi-Organization/DiGi.Analytical#21): H_U = b·A / (1/U_f + R_add) with U_f from the floor's layers, so TABULA's additional resistance is counted once and the floor's declared U (0.35) plays no part; the floor has no ground loss.
        /// </summary>
        [Fact]
        public void HeatDemand_Cellar()
        {
            (BuildingModel buildingModel, Dictionary<string, Guid> guids) = HeatDemand_Model(true, 5.0);

            TypologyData typologyData = new(0.3, 0.1, FloorBoundary.Cellar, 0.3, RoofType.Flat, 0.5, 0.9, 0, "test");
            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(HeatDemand_Climate(guids, true), typologyData);
            Assert.NotNull(heatDemandResult);

            // U_f = 1 / (R_si 0.17 + 0.1 / 0.04 + 0.1 / 2.0 + R_se 0.04) for a downward heat flow.
            double thermalTransmittance_Floor = 1.0 / (0.17 + 2.55 + 0.04);
            Assert.Equal(0.5 * 80.0 / ((1.0 / thermalTransmittance_Floor) + 0.3), heatDemandResult.UnheatedSpaceHeatTransferCoefficient, 9);
            Assert.NotEqual(0.5 * 80.0 / ((1.0 / 0.35) + 0.3), heatDemandResult.UnheatedSpaceHeatTransferCoefficient, 3);
            Assert.Equal(0.0, heatDemandResult.GroundHeatTransferCoefficient, 12);
        }

        /// <summary>
        /// Tests the box built from the TABULA constructions of a single-family house of 1993-2002, <c>PL.N.SFH.05.Gen.ReEx.001</c> (DiGi-Organization/DiGi.Analytical#21): with the embedded library's TypicalAsBuilt wall, roof and floor constructions and the set's window Uw and g, every heat transfer term other than the floor is TABULA's U·A, the thermal bridges are the set's ΔU_tb on the envelope area, and the floor's declared TABULA U is not what H_g uses.
        /// </summary>
        [Fact]
        public void HeatDemand_Tabula()
        {
            ConstructionLibrary? constructionLibrary = Building.Regulation.Query.EmbeddedConstructionLibrary_PL();
            Assert.NotNull(constructionLibrary);

            ConstructionSet? constructionSet = constructionLibrary.GetConstructionSet(new DateTimeOffset(1995, 6, 1, 0, 0, 0, TimeSpan.Zero), BuildingType.SingleFamily, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
            Assert.NotNull(constructionSet);
            Assert.Contains("PL.N.SFH.05.Gen.ReEx.001", constructionSet.Source);

            TypologyData? typologyData = constructionSet.TypologyData;
            Assert.NotNull(typologyData);

            IWallConstruction? wallConstruction = constructionLibrary.GetConstruction(constructionSet, ConstructionRole.ExternalWall) as IWallConstruction;
            IRoofConstruction? roofConstruction = constructionLibrary.GetConstruction(constructionSet, ConstructionRole.Roof) as IRoofConstruction;
            IFloorConstruction? floorConstruction = constructionLibrary.GetConstruction(constructionSet, ConstructionRole.GroundFloor) as IFloorConstruction;
            IOpeningConstruction? openingConstruction_Library = constructionLibrary.GetConstruction(constructionSet, ConstructionRole.Window) as IOpeningConstruction;
            Assert.NotNull(wallConstruction);
            Assert.NotNull(roofConstruction);
            Assert.NotNull(floorConstruction);
            Assert.NotNull(openingConstruction_Library);

            static double Declared(IConstruction construction, Enum @enum)
            {
                Assert.True(construction.TryGetValue(new EnumParameterDefinition(@enum), out double value));
                return value;
            }

            double thermalTransmittance_Wall = Declared(wallConstruction, PhysicalComponentConstructionParameter.ThermalTransmittance);
            double thermalTransmittance_Roof = Declared(roofConstruction, PhysicalComponentConstructionParameter.ThermalTransmittance);
            double thermalTransmittance_Floor = Declared(floorConstruction, PhysicalComponentConstructionParameter.ThermalTransmittance);
            double thermalTransmittance_Window = Declared(openingConstruction_Library, OpeningConstructionParameter.ThermalTransmittance);
            double totalSolarEnergyTransmittance = Declared(openingConstruction_Library, OpeningConstructionParameter.TotalSolarEnergyTransmittance);
            Assert.Equal(0.4, thermalTransmittance_Wall, 9);
            Assert.Equal(0.4, thermalTransmittance_Roof, 9);
            Assert.Equal(1.0, thermalTransmittance_Floor, 9);
            Assert.Equal(1.6, thermalTransmittance_Window, 9);

            List<(IComponent Component, Vector3D Normal)> components = BuildingModelGetExternalShell_Box(0, 0, 0, 10, 8, 3, []);
            Space space = new(new Point3D(5, 4, 1.5), "Space");

            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Update(constructionLibrary.ConstructionRelationCluster, wallConstruction));
            Assert.True(buildingModel.Update(constructionLibrary.ConstructionRelationCluster, roofConstruction));
            Assert.True(buildingModel.Update(constructionLibrary.ConstructionRelationCluster, floorConstruction));
            foreach ((IComponent component, Vector3D _) in components)
            {
                Assert.True(buildingModel.Assign(component, space));
                Assert.True(component switch
                {
                    IFloor floor => buildingModel.Assign(floor, floorConstruction),
                    IRoof roof => buildingModel.Assign(roof, roofConstruction),
                    IWall wall => buildingModel.Assign(wall, wallConstruction),
                    _ => false,
                });
            }

            WindowConstruction windowConstruction = new("TABULA window", OpeningFixture_Rectangle(0, 0, 1.23, 1.48, (0.1, 0.1, 1.13, 1.38)));
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.ThermalTransmittance, thermalTransmittance_Window));
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.TotalSolarEnergyTransmittance, totalSolarEnergyTransmittance));
            Assert.True(buildingModel.Assign((IPhysicalComponent)components[2].Component, new Window(OpeningFixture_Plane(2, 0, 0.8)), windowConstruction));

            Assert.True(buildingModel.AssignInternalCondition(HeatDemand_InternalCondition(5.0)));

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(new MonthlyClimate(heatDemand_ExternalTemperatures, -20.0, 7.6, null, "test"), typologyData);
            Assert.NotNull(heatDemandResult);
            Assert.Empty(heatDemandResult.ExcludedComponents ?? ["missing"]);

            double area_Window = 1.23 * 1.48;
            double area_Walls = (2 * ((10 * 3) + (8 * 3))) - area_Window;
            Assert.Equal((thermalTransmittance_Wall * area_Walls) + (thermalTransmittance_Roof * 80.0) + (thermalTransmittance_Window * area_Window), heatDemandResult.DirectHeatTransferCoefficient, 9);
            Assert.Equal(typologyData.ThermalBridgingSurcharge * (area_Walls + area_Window + 80.0 + 80.0), heatDemandResult.ThermalBridgeHeatTransferCoefficient, 9);
            Assert.NotEqual(thermalTransmittance_Floor * 80.0, heatDemandResult.GroundHeatTransferCoefficient, 3);
            Assert.True(heatDemandResult.GroundHeatTransferCoefficient > 0);
        }

        /// <summary>
        /// Tests <see cref="Building.Energy.Query.UtilisationFactor(double, double)"/> (DiGi-Organization/DiGi.Analytical#21): γ = 1 gives a / (a + 1), γ just off 1 stays continuous with it, γ → 0 gives 1, a large γ gives 1/γ asymptotically and a negative γ gives 1/γ.
        /// </summary>
        [Fact]
        public void HeatDemand_UtilisationFactor()
        {
            double a = 1.0 + (30.0 / 15.0);

            Assert.Equal(a / (a + 1.0), Building.Energy.Query.UtilisationFactor(1.0, a), 12);
            Assert.Equal(a / (a + 1.0), Building.Energy.Query.UtilisationFactor(1.0 + 1e-6, a), 5);
            Assert.Equal(a / (a + 1.0), Building.Energy.Query.UtilisationFactor(1.0 - 1e-6, a), 5);
            Assert.Equal(1.0, Building.Energy.Query.UtilisationFactor(1e-9, a), 9);
            Assert.Equal(1.0 / 1e6, Building.Energy.Query.UtilisationFactor(1e6, a), 9);
            Assert.Equal(1.0 / -0.5, Building.Energy.Query.UtilisationFactor(-0.5, a), 12);

            double gainLossRatio = 0.6;
            Assert.Equal((1.0 - Math.Pow(gainLossRatio, a)) / (1.0 - Math.Pow(gainLossRatio, a + 1.0)), Building.Energy.Query.UtilisationFactor(gainLossRatio, a), 12);

            Assert.True(double.IsNaN(Building.Energy.Query.UtilisationFactor(double.NaN, a)));
            Assert.True(double.IsNaN(Building.Energy.Query.UtilisationFactor(0.5, 0.0)));
        }

        /// <summary>
        /// Tests both branches of <see cref="Building.Energy.Query.GroundThermalTransmittance(double, double, double, double, double)"/> (DiGi-Organization/DiGi.Analytical#21), just inside and just outside d_t = B′.
        /// <para>A = 16 m2 and P = 8 m give B′ = 4 m; with w = 0 and λ_g = 2 W/(mK), d_t = 2·(0.17 + R_f + 0.04), so R_f = B′·(1 ∓ 0.001) / 2 − 0.21 puts d_t on either side of B′. Each side must follow its own formula and not the other one.</para>
        /// </summary>
        [Fact]
        public void HeatDemand_GroundThermalTransmittance()
        {
            const double area = 16.0;
            const double exposedPerimeter = 8.0;
            const double characteristicDimension = 4.0;

            static double Uninsulated(double equivalentThickness)
            {
                return 2.0 * 2.0 / ((Math.PI * characteristicDimension) + equivalentThickness) * Math.Log((Math.PI * characteristicDimension / equivalentThickness) + 1.0);
            }

            static double Insulated(double equivalentThickness)
            {
                return 2.0 / ((0.457 * characteristicDimension) + equivalentThickness);
            }

            double equivalentThickness_Inside = characteristicDimension * (1.0 - 0.001);
            double thermalTransmittance_Inside = Building.Energy.Query.GroundThermalTransmittance(area, exposedPerimeter, 0.0, (equivalentThickness_Inside / 2.0) - 0.21, 2.0);
            Assert.Equal(Uninsulated(equivalentThickness_Inside), thermalTransmittance_Inside, 12);
            Assert.NotEqual(Insulated(equivalentThickness_Inside), thermalTransmittance_Inside, 6);

            double equivalentThickness_Outside = characteristicDimension * (1.0 + 0.001);
            double thermalTransmittance_Outside = Building.Energy.Query.GroundThermalTransmittance(area, exposedPerimeter, 0.0, (equivalentThickness_Outside / 2.0) - 0.21, 2.0);
            Assert.Equal(Insulated(equivalentThickness_Outside), thermalTransmittance_Outside, 12);
            Assert.NotEqual(Uninsulated(equivalentThickness_Outside), thermalTransmittance_Outside, 6);

            Assert.Equal(0.0, Building.Energy.Query.GroundThermalTransmittance(area, 0.0, 0.0, 1.0, 2.0), 12);
            Assert.True(double.IsNaN(Building.Energy.Query.GroundThermalTransmittance(0.0, exposedPerimeter, 0.0, 1.0, 2.0)));
            Assert.True(double.IsNaN(Building.Energy.Query.GroundThermalTransmittance(area, exposedPerimeter, 0.0, double.NaN, 2.0)));
        }

        /// <summary>
        /// Tests <see cref="Building.Regulation.Query.DesignOutdoorTemperature(ClimateZone)"/> and <see cref="Building.Regulation.Query.AnnualMeanOutdoorTemperature(ClimateZone)"/> for every zone and for <see cref="ClimateZone.Undefined"/> (DiGi-Organization/DiGi.Analytical#21).
        /// </summary>
        [Fact]
        public void HeatDemand_ClimateZoneTemperatures()
        {
            List<(ClimateZone ClimateZone, double Design, double AnnualMean)> values =
            [
                (ClimateZone.I, -16.0, 7.7),
                (ClimateZone.II, -18.0, 7.9),
                (ClimateZone.III, -20.0, 7.6),
                (ClimateZone.IV, -22.0, 6.9),
                (ClimateZone.V, -24.0, 5.5),
            ];

            foreach ((ClimateZone climateZone, double design, double annualMean) in values)
            {
                Assert.Equal(design, Building.Regulation.Query.DesignOutdoorTemperature(climateZone));
                Assert.Equal(annualMean, Building.Regulation.Query.AnnualMeanOutdoorTemperature(climateZone));
            }

            Assert.True(double.IsNaN(Building.Regulation.Query.DesignOutdoorTemperature(ClimateZone.Undefined)));
            Assert.True(double.IsNaN(Building.Regulation.Query.AnnualMeanOutdoorTemperature(ClimateZone.Undefined)));
        }

        /// <summary>
        /// Tests <see cref="Building.Regulation.Query.ConstructionRoles(BuildingModel?, FloorBoundary, double)"/> on two stacked storeys (DiGi-Organization/DiGi.Analytical#21), the rules of DiGi.GIS.Analytical's envelope assignment.
        /// <para>The lowest floor is GroundFloor on soil and FloorOverUnheated over a cellar, the slab between the storeys IntermediateFloor, a wall marked as a party wall PartyWall, a wall bounding both storeys InternalWall, the other walls ExternalWall and the flat top roof FlatRoof. A roof the external shell does not carry - one bounding no space - gets no role at all.</para>
        /// </summary>
        [Fact]
        public void HeatDemand_ConstructionRoles()
        {
            List<(IComponent Component, Vector3D Normal)> components_Lower = BuildingModelGetExternalShell_Box(0, 0, 0, 10, 10, 3, []);
            List<(IComponent Component, Vector3D Normal)> components_Upper = BuildingModelGetExternalShell_Box(0, 0, 3, 10, 10, 6, []);

            Space space_Lower = new(new Point3D(5, 5, 1.5), "Lower");
            Space space_Upper = new(new Point3D(5, 5, 4.5), "Upper");

            // The lower storey has no roof of its own: the floor of the upper storey is the slab both spaces share.
            BuildingModel buildingModel = new();
            for (int i = 0; i < components_Lower.Count; i++)
            {
                if (i != 1)
                {
                    Assert.True(buildingModel.Assign(components_Lower[i].Component, space_Lower));
                }
            }

            for (int i = 0; i < components_Upper.Count; i++)
            {
                Assert.True(i == 0 ? buildingModel.Assign(components_Upper[i].Component, space_Lower, space_Upper) : buildingModel.Assign(components_Upper[i].Component, space_Upper));
            }

            // The west wall of the lower storey is shared with a neighbour.
            Component component_Party = (Component)components_Lower[5].Component;
            Assert.True(component_Party.SetValue(ComponentParameter.PartyWall, Guid.NewGuid()));
            Assert.True(buildingModel.Update(component_Party));

            SurfaceWall surfaceWall_Internal = new(BuildingModel_Footprints_Face([new Point3D(5, 0, 0), new Point3D(5, 10, 0), new Point3D(5, 10, 3), new Point3D(5, 0, 3)]));
            Assert.True(buildingModel.Assign(surfaceWall_Internal, space_Lower, space_Upper));

            SurfaceRoof surfaceRoof_Stray = new(BuildingModel_Footprints_Face([new Point3D(20, 0, 3), new Point3D(30, 0, 3), new Point3D(30, 10, 3), new Point3D(20, 10, 3)]));
            Assert.True(buildingModel.Update(surfaceRoof_Stray));

            Dictionary<Guid, ConstructionRole>? constructionRoles = buildingModel.ConstructionRoles(FloorBoundary.Soil);
            Assert.NotNull(constructionRoles);

            Assert.Equal(ConstructionRole.GroundFloor, constructionRoles[components_Lower[0].Component.Guid]);
            Assert.Equal(ConstructionRole.IntermediateFloor, constructionRoles[components_Upper[0].Component.Guid]);
            Assert.False(constructionRoles.ContainsKey(components_Lower[1].Component.Guid));
            Assert.Equal(ConstructionRole.FlatRoof, constructionRoles[components_Upper[1].Component.Guid]);
            Assert.Equal(ConstructionRole.PartyWall, constructionRoles[component_Party.Guid]);
            Assert.Equal(ConstructionRole.InternalWall, constructionRoles[surfaceWall_Internal.Guid]);
            Assert.False(constructionRoles.ContainsKey(surfaceRoof_Stray.Guid));

            foreach (int i in new[] { 2, 3, 4 })
            {
                Assert.Equal(ConstructionRole.ExternalWall, constructionRoles[components_Lower[i].Component.Guid]);
            }

            for (int i = 2; i < components_Upper.Count; i++)
            {
                Assert.Equal(ConstructionRole.ExternalWall, constructionRoles[components_Upper[i].Component.Guid]);
            }

            Dictionary<Guid, ConstructionRole>? constructionRoles_Cellar = buildingModel.ConstructionRoles(FloorBoundary.Cellar);
            Assert.NotNull(constructionRoles_Cellar);
            Assert.Equal(ConstructionRole.FloorOverUnheated, constructionRoles_Cellar[components_Lower[0].Component.Guid]);

            Assert.Null(Building.Regulation.Query.ConstructionRoles(null, FloorBoundary.Soil));
        }

        /// <summary>
        /// Tests the serialization and copying of <see cref="MonthlyClimate"/>, <see cref="HeatDemandOptions"/> and <see cref="HeatDemandResult"/> (DiGi-Organization/DiGi.Analytical#21): a string round trip, <see cref="Core.xUnit.Query.SerializationCheck"/> and a member-by-member <c>new T(source)</c> assertion; the identifier keys of the irradiation dictionary are asserted again under pl-PL.
        /// </summary>
        [Fact]
        public void HeatDemand_Serialization()
        {
            (BuildingModel buildingModel, Dictionary<string, Guid> guids) = HeatDemand_Model(true, 5.0);

            MonthlyClimate monthlyClimate = HeatDemand_Climate(guids, true);
            MonthlyClimate monthlyClimate_Copy = new(monthlyClimate);
            Assert.Equal(monthlyClimate.ExternalTemperatures, monthlyClimate_Copy.ExternalTemperatures);
            Assert.Equal(monthlyClimate.DesignExternalTemperature, monthlyClimate_Copy.DesignExternalTemperature);
            Assert.Equal(monthlyClimate.AnnualMeanExternalTemperature, monthlyClimate_Copy.AnnualMeanExternalTemperature);
            Assert.Equal(monthlyClimate.Source, monthlyClimate_Copy.Source);
            Assert.Equal(monthlyClimate.SurfaceIrradiations, monthlyClimate_Copy.SurfaceIrradiations);
            Core.xUnit.Query.SerializationCheck(monthlyClimate);

            MonthlyClimate? monthlyClimate_Json = Core.Convert.ToDiGi<MonthlyClimate>(Core.Convert.ToSystem_String(monthlyClimate))?.FirstOrDefault();
            Assert.NotNull(monthlyClimate_Json);
            Assert.Equal(monthlyClimate.SurfaceIrradiations, monthlyClimate_Json.SurfaceIrradiations);

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
                MonthlyClimate? monthlyClimate_Polish = Core.Convert.ToDiGi<MonthlyClimate>(Core.Convert.ToSystem_String(HeatDemand_Climate(guids, true)))?.FirstOrDefault();
                Assert.NotNull(monthlyClimate_Polish);
                Assert.True(monthlyClimate_Polish.SurfaceIrradiations?.ContainsKey(guids["South"].ToString("D")));
                Assert.Equal(heatDemand_IrradiationsSouth[2], monthlyClimate_Polish.SurfaceIrradiations?[guids["South"].ToString("D")][2]);
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }

            HeatDemandOptions heatDemandOptions = new()
            {
                UtilisationNumericalParameter = 1.1,
                UtilisationReferenceTimeConstant = 16.0,
                GroundThermalConductivity = 1.5,
                GroundWallThickness = 0.4,
                OpaqueSolarAbsorptance = 0.5,
                ExternalEmissivity = 0.8,
                SkyTemperatureDifference = 9.0,
                WallSkyFormFactor = 0.4,
                RoofSkyFormFactor = 0.9,
                NonPerpendicularIncidenceFactor = 0.85,
                ThermalBridgingSurcharge = 0.05,
                FrameAreaFraction = 0.25,
                InternalHeatCapacityMaximumThickness = 0.08,
                InsulationThermalConductivity = 0.06,
                IncludeOpaqueSolarGains = false,
                IncludeSkyRadiation = false,
            };

            HeatDemandOptions heatDemandOptions_Copy = new(heatDemandOptions);
            Assert.Equal(heatDemandOptions.UtilisationNumericalParameter, heatDemandOptions_Copy.UtilisationNumericalParameter);
            Assert.Equal(heatDemandOptions.UtilisationReferenceTimeConstant, heatDemandOptions_Copy.UtilisationReferenceTimeConstant);
            Assert.Equal(heatDemandOptions.GroundThermalConductivity, heatDemandOptions_Copy.GroundThermalConductivity);
            Assert.Equal(heatDemandOptions.GroundWallThickness, heatDemandOptions_Copy.GroundWallThickness);
            Assert.Equal(heatDemandOptions.OpaqueSolarAbsorptance, heatDemandOptions_Copy.OpaqueSolarAbsorptance);
            Assert.Equal(heatDemandOptions.ExternalEmissivity, heatDemandOptions_Copy.ExternalEmissivity);
            Assert.Equal(heatDemandOptions.SkyTemperatureDifference, heatDemandOptions_Copy.SkyTemperatureDifference);
            Assert.Equal(heatDemandOptions.WallSkyFormFactor, heatDemandOptions_Copy.WallSkyFormFactor);
            Assert.Equal(heatDemandOptions.RoofSkyFormFactor, heatDemandOptions_Copy.RoofSkyFormFactor);
            Assert.Equal(heatDemandOptions.NonPerpendicularIncidenceFactor, heatDemandOptions_Copy.NonPerpendicularIncidenceFactor);
            Assert.Equal(heatDemandOptions.ThermalBridgingSurcharge, heatDemandOptions_Copy.ThermalBridgingSurcharge);
            Assert.Equal(heatDemandOptions.FrameAreaFraction, heatDemandOptions_Copy.FrameAreaFraction);
            Assert.Equal(heatDemandOptions.InternalHeatCapacityMaximumThickness, heatDemandOptions_Copy.InternalHeatCapacityMaximumThickness);
            Assert.Equal(heatDemandOptions.InsulationThermalConductivity, heatDemandOptions_Copy.InsulationThermalConductivity);
            Assert.Equal(heatDemandOptions.IncludeOpaqueSolarGains, heatDemandOptions_Copy.IncludeOpaqueSolarGains);
            Assert.Equal(heatDemandOptions.IncludeSkyRadiation, heatDemandOptions_Copy.IncludeSkyRadiation);
            Core.xUnit.Query.SerializationCheck(heatDemandOptions);

            HeatDemandOptions? heatDemandOptions_Json = Core.Convert.ToDiGi<HeatDemandOptions>(Core.Convert.ToSystem_String(heatDemandOptions))?.FirstOrDefault();
            Assert.NotNull(heatDemandOptions_Json);
            Assert.Equal(0.06, heatDemandOptions_Json.InsulationThermalConductivity);
            Assert.False(heatDemandOptions_Json.IncludeSkyRadiation);

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(monthlyClimate, new TypologyData(0.3, 0.1, FloorBoundary.Soil, 0.0, RoofType.Flat, 0.5, 0.9, 0, "test"));
            Assert.NotNull(heatDemandResult);

            HeatDemandResult heatDemandResult_Copy = new(heatDemandResult);
            Assert.Equal(heatDemandResult.HeatingEnergyNeed, heatDemandResult_Copy.HeatingEnergyNeed);
            Assert.Equal(heatDemandResult.HeatingEnergyNeedPerArea, heatDemandResult_Copy.HeatingEnergyNeedPerArea);
            Assert.Equal(heatDemandResult.HeatingEnergyNeeds, heatDemandResult_Copy.HeatingEnergyNeeds);
            Assert.Equal(heatDemandResult.TransmissionHeatTransfers, heatDemandResult_Copy.TransmissionHeatTransfers);
            Assert.Equal(heatDemandResult.VentilationHeatTransfers, heatDemandResult_Copy.VentilationHeatTransfers);
            Assert.Equal(heatDemandResult.InternalHeatGains, heatDemandResult_Copy.InternalHeatGains);
            Assert.Equal(heatDemandResult.SolarHeatGains, heatDemandResult_Copy.SolarHeatGains);
            Assert.Equal(heatDemandResult.SkyRadiationHeatTransfers, heatDemandResult_Copy.SkyRadiationHeatTransfers);
            Assert.Equal(heatDemandResult.HeatBalanceRatios, heatDemandResult_Copy.HeatBalanceRatios);
            Assert.Equal(heatDemandResult.GainUtilisationFactors, heatDemandResult_Copy.GainUtilisationFactors);
            Assert.Equal(heatDemandResult.DirectHeatTransferCoefficient, heatDemandResult_Copy.DirectHeatTransferCoefficient);
            Assert.Equal(heatDemandResult.ThermalBridgeHeatTransferCoefficient, heatDemandResult_Copy.ThermalBridgeHeatTransferCoefficient);
            Assert.Equal(heatDemandResult.GroundHeatTransferCoefficient, heatDemandResult_Copy.GroundHeatTransferCoefficient);
            Assert.Equal(heatDemandResult.UnheatedSpaceHeatTransferCoefficient, heatDemandResult_Copy.UnheatedSpaceHeatTransferCoefficient);
            Assert.Equal(heatDemandResult.VentilationHeatTransferCoefficient, heatDemandResult_Copy.VentilationHeatTransferCoefficient);
            Assert.Equal(heatDemandResult.InternalHeatCapacity, heatDemandResult_Copy.InternalHeatCapacity);
            Assert.Equal(heatDemandResult.TimeConstant, heatDemandResult_Copy.TimeConstant);
            Assert.Equal(heatDemandResult.ReferenceFloorArea, heatDemandResult_Copy.ReferenceFloorArea);
            Assert.Equal(heatDemandResult.HeatedVolume, heatDemandResult_Copy.HeatedVolume);
            Assert.Equal(heatDemandResult.InternalTemperature, heatDemandResult_Copy.InternalTemperature);
            Assert.Equal(heatDemandResult.DesignHeatLoad, heatDemandResult_Copy.DesignHeatLoad);
            Assert.Equal(heatDemandResult.DesignTransmissionHeatLoss, heatDemandResult_Copy.DesignTransmissionHeatLoss);
            Assert.Equal(heatDemandResult.DesignVentilationHeatLoss, heatDemandResult_Copy.DesignVentilationHeatLoss);
            Assert.Equal(heatDemandResult.Method, heatDemandResult_Copy.Method);
            Assert.Equal(heatDemandResult.MethodVersion, heatDemandResult_Copy.MethodVersion);
            Assert.Equal(heatDemandResult.Sources, heatDemandResult_Copy.Sources);
            Assert.Equal(heatDemandResult.ExcludedComponents, heatDemandResult_Copy.ExcludedComponents);
            Assert.Equal(heatDemandResult.GlazingFallbackCount, heatDemandResult_Copy.GlazingFallbackCount);
            Assert.Equal(heatDemandResult.MissingIrradiationCount, heatDemandResult_Copy.MissingIrradiationCount);
            Core.xUnit.Query.SerializationCheck(heatDemandResult);

            HeatDemandResult? heatDemandResult_Json = Core.Convert.ToDiGi<HeatDemandResult>(Core.Convert.ToSystem_String(heatDemandResult))?.FirstOrDefault();
            Assert.NotNull(heatDemandResult_Json);
            Assert.Equal(heatDemandResult.HeatingEnergyNeeds, heatDemandResult_Json.HeatingEnergyNeeds);
            Assert.Equal(heatDemandResult.Sources, heatDemandResult_Json.Sources);
            Assert.Equal(heatDemandResult.GlazingFallbackCount, heatDemandResult_Json.GlazingFallbackCount);
        }

        /// <summary>
        /// Builds the 10 x 8 x 3 m box of the heat-demand facts: one space under a usage-like internal condition (20 °C, 0.3 l/(s m2) outside air, 0.3 1/h infiltration), layered walls (insulation, brick, plaster; declared U 0.30), roof (insulation, concrete; 0.25) and floor (insulation, concrete; 0.35), and two windows in the south wall, one with a pane and one without.
        /// </summary>
        /// <param name="eastWallKnown">Whether the east wall gets the wall construction; otherwise it gets one with neither a declared U nor layers.</param>
        /// <param name="internalGain">The total internal gain [W/m2].</param>
        /// <returns>The model and the identifiers of its south wall, east wall and roof.</returns>
        private static (BuildingModel BuildingModel, Dictionary<string, Guid> Guids) HeatDemand_Model(bool eastWallKnown, double internalGain)
        {
            OpaqueMaterial opaqueMaterial_Insulation = new("Insulation") { Conductivity = 0.04, Density = 30, SpecificHeat = 1400 };
            OpaqueMaterial opaqueMaterial_Brick = new("Brick") { Conductivity = 0.77, Density = 1800, SpecificHeat = 880 };
            OpaqueMaterial opaqueMaterial_Plaster = new("Plaster") { Conductivity = 0.7, Density = 1600, SpecificHeat = 1000 };
            OpaqueMaterial opaqueMaterial_Concrete = new("Concrete") { Conductivity = 2.0, Density = 2400, SpecificHeat = 1000 };

            WallConstruction wallConstruction = new() { Name = "Wall" };
            RoofConstruction roofConstruction = new() { Name = "Roof" };
            FloorConstruction floorConstruction = new() { Name = "Floor" };
            Assert.True(wallConstruction.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 0.30));
            Assert.True(roofConstruction.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 0.25));
            Assert.True(floorConstruction.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 0.35));

            ConstructionRelationCluster constructionRelationCluster = new([opaqueMaterial_Insulation, opaqueMaterial_Brick, opaqueMaterial_Plaster, opaqueMaterial_Concrete, wallConstruction, roofConstruction, floorConstruction]);
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Insulation, 0.1));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Brick, 0.25));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Plaster, 0.015));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, roofConstruction, opaqueMaterial_Insulation, 0.2));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, roofConstruction, opaqueMaterial_Concrete, 0.15));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, floorConstruction, opaqueMaterial_Insulation, 0.1));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, floorConstruction, opaqueMaterial_Concrete, 0.1));

            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Update(constructionRelationCluster, wallConstruction));
            Assert.True(buildingModel.Update(constructionRelationCluster, roofConstruction));
            Assert.True(buildingModel.Update(constructionRelationCluster, floorConstruction));

            WallConstruction wallConstruction_Unknown = new() { Name = "Unknown" };
            Assert.True(buildingModel.Update(wallConstruction_Unknown));

            List<(IComponent Component, Vector3D Normal)> components = BuildingModelGetExternalShell_Box(0, 0, 0, 10, 8, 3, []);
            Space space = new(new Point3D(5, 4, 1.5), "Space");

            for (int i = 0; i < components.Count; i++)
            {
                IComponent component = components[i].Component;
                Assert.True(buildingModel.Assign(component, space));
                Assert.True(component switch
                {
                    IFloor floor => buildingModel.Assign(floor, floorConstruction),
                    IRoof roof => buildingModel.Assign(roof, roofConstruction),
                    IWall wall => buildingModel.Assign(wall, i == 3 && !eastWallKnown ? wallConstruction_Unknown : wallConstruction),
                    _ => false,
                });
            }

            WindowConstruction windowConstruction_Pane = new("Window with pane", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));
            WindowConstruction windowConstruction_NoPane = new("Window without panes", OpeningFixture_Rectangle(0, 0, 1.2, 1.5));
            foreach (WindowConstruction windowConstruction in new[] { windowConstruction_Pane, windowConstruction_NoPane })
            {
                Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.ThermalTransmittance, 1.3));
                Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.TotalSolarEnergyTransmittance, 0.6));
            }

            IPhysicalComponent physicalComponent_South = (IPhysicalComponent)components[2].Component;
            Assert.True(buildingModel.Assign(physicalComponent_South, new Window(OpeningFixture_Plane(2, 0, 0.8)), windowConstruction_Pane));
            Assert.True(buildingModel.Assign(physicalComponent_South, new Window(OpeningFixture_Plane(6, 0, 0.8)), windowConstruction_NoPane));

            Assert.True(buildingModel.AssignInternalCondition(HeatDemand_InternalCondition(internalGain)));

            return (buildingModel, new Dictionary<string, Guid>()
            {
                ["South"] = components[2].Component.Guid,
                ["East"] = components[3].Component.Guid,
                ["Roof"] = components[1].Component.Guid,
            });
        }

        /// <summary>
        /// Builds the internal condition of the heat-demand facts: a 20 °C lower limit, the given total internal gain, 0.3 1/h infiltration and 0.3 l/(s m2) outside air.
        /// </summary>
        /// <param name="internalGain">The total internal gain [W/m2].</param>
        /// <returns>The internal condition.</returns>
        private static Building.Classes.InternalCondition HeatDemand_InternalCondition(double internalGain)
        {
            Building.Classes.InternalCondition internalCondition = new("Test");

            Thermostat thermostat = new();
            thermostat[ThermostatProfileType.LowerLimit] = new Profile([20.0]);

            InternalGain internalGain_HVAC = new();
            internalGain_HVAC[InternalGainProfileType.TotalInternalGain] = new Profile([internalGain]);
            internalGain_HVAC[InternalGainProfileType.Infiltration] = new Profile([0.3]);

            Assert.True(internalCondition.SetThermostat(thermostat));
            Assert.True(internalCondition.SetInternalGain(internalGain_HVAC));
            Assert.True(internalCondition.SetSystemProperties(new SystemProperties() { OutsideAirPerArea = 0.3 }));

            return internalCondition;
        }

        /// <summary>
        /// Builds the monthly climate of the heat-demand facts: fixed monthly temperatures, zone III design values and, optionally, irradiation on the south wall and the roof only.
        /// </summary>
        /// <param name="guids">The identifiers returned by <see cref="HeatDemand_Model(bool, double)"/>.</param>
        /// <param name="irradiation">Whether the south wall and the roof get irradiation.</param>
        /// <returns>The monthly climate.</returns>
        private static MonthlyClimate HeatDemand_Climate(Dictionary<string, Guid> guids, bool irradiation)
        {
            Dictionary<string, List<double>>? surfaceIrradiations = irradiation ? new Dictionary<string, List<double>>()
            {
                [guids["South"].ToString("D")] = [.. heatDemand_IrradiationsSouth],
                [guids["Roof"].ToString("D")] = [.. heatDemand_IrradiationsRoof],
            } : null;

            return new MonthlyClimate(heatDemand_ExternalTemperatures, -20.0, 7.6, surfaceIrradiations, "test climate");
        }
    }
}
