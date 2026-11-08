using DiGi.Analytical.Building;
using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Classes;
using DiGi.Core.Parameter.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;
using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using Regulation = DiGi.Analytical.Building.Regulation;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        private static (BuildingModel BuildingModel, Building2D Building2D) OpeningGeneration_Box(double width, double depth, ushort storeys, BuildingSpecificFunction buildingSpecificFunction)
        {
            Building2D building2D = Building2D_Rectangle(width, depth, storeys, BuildingGeneralFunction.residential_buildings, buildingSpecificFunction);
            BuildingModel? buildingModel = Create.BuildingModel(building2D, 0, 3.0);
            Assert.NotNull(buildingModel);
            return (buildingModel, building2D);
        }

        private static OpeningGenerationInput OpeningGeneration_Input(short? yearBuilt, Point2D? addressPoint = null, IEnumerable<Polyline2D>? roads = null)
        {
            return new OpeningGenerationInput(yearBuilt, YearBuiltSource.User, addressPoint, Regulation.Enums.ClimateZone.Undefined, null, null, roads);
        }

        private static string? OpeningGeneration_Value(IOpening opening, OpeningParameter openingParameter)
        {
            return opening is Opening opening_Parametrized && opening_Parametrized.TryGetValue(new EnumParameterDefinition(openingParameter), out string? value) ? value : null;
        }

        /// <summary>
        /// Tests the serialization and copying of <see cref="OpeningGenerationOptions"/>, <see cref="OpeningGenerationInput"/> and <see cref="OpeningGenerationResult"/>, and <see cref="Create.OpeningGenerationOptions"/> for every regime (DiGi-Organization/DiGi.GIS.Analytical#2).
        /// </summary>
        [Fact]
        public void OpeningGeneration_Serialization()
        {
            foreach (Regulation.Enums.BuildingRegulation buildingRegulation in Enum.GetValues<Regulation.Enums.BuildingRegulation>())
            {
                OpeningGenerationOptions openingGenerationOptions_Regime = Create.OpeningGenerationOptions(buildingRegulation);
                Assert.NotNull(openingGenerationOptions_Regime.RoomMixes);
                Core.xUnit.Query.SerializationCheck(openingGenerationOptions_Regime);
            }

            Assert.Equal(0.1, Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_1928).DaylightRatio);
            Assert.Equal(0.125, Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_1994).DaylightRatio);
            Assert.True(Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2002).MaximumGlazedAreaSingleFamilyOnly);
            Assert.Equal(2.0, Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2002).MaximumGlazedAreaThermalTransmittance);
            Assert.Equal(1.5, Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2009).MaximumGlazedAreaThermalTransmittance);
            Assert.Equal(0.9, Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2014).MaximumGlazedAreaThermalTransmittance);
            Assert.Null(Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2017).MaximumGlazedAreaThermalTransmittance);
            Assert.Null(Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_1980).MaximumGlazedAreaThermalTransmittance);

            OpeningGenerationOptions openingGenerationOptions = Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2009);
            openingGenerationOptions.ConstructionFlavour = Regulation.Enums.ConstructionFlavour.Regulation;
            openingGenerationOptions.StaircaseSpacing = 12.5;
            openingGenerationOptions.FireSeparationDistances![FireClass.IN] = 9;
            openingGenerationOptions.FireSeparationHeatRelease = 2200;
            openingGenerationOptions.ObstructionInnerCityFactor = 0.5;
            openingGenerationOptions.RoadFrontageDistance = 6.0;
            openingGenerationOptions.AssignEnvelopeConstructions = false;
            OpeningGenerationOptions openingGenerationOptions_Copy = new(openingGenerationOptions);
            Assert.Equal(Core.Convert.ToSystem_String(openingGenerationOptions), Core.Convert.ToSystem_String(openingGenerationOptions_Copy));
            Assert.False(openingGenerationOptions_Copy.AssignEnvelopeConstructions);
            Assert.Equal(12.5, openingGenerationOptions_Copy.StaircaseSpacing);
            Assert.Equal(6.0, openingGenerationOptions_Copy.RoadFrontageDistance);
            Assert.Equal(9, openingGenerationOptions_Copy.FireSeparationDistances![FireClass.IN]);
            Assert.Equal(2200, openingGenerationOptions_Copy.FireSeparationHeatRelease);
            Assert.Equal(0.5, openingGenerationOptions_Copy.ObstructionInnerCityFactor);
            Assert.Equal(0.25, openingGenerationOptions_Copy.RoomMixes![Regulation.Enums.BuildingType.SingleFamily][Regulation.Enums.RoomUse.Kitchen]);
            Core.xUnit.Query.SerializationCheck(openingGenerationOptions);

            OpeningGenerationOptions openingGenerationOptions_WT2002 = Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2002);
            Assert.Equal(8, openingGenerationOptions_WT2002.FireSeparationDistances![FireClass.ZL]);
            Assert.Equal(15, openingGenerationOptions_WT2002.FireSeparationDistancePMMid);
            Assert.Equal(20, openingGenerationOptions_WT2002.FireSeparationDistancePMHigh);
            Assert.Null(openingGenerationOptions_WT2002.FireSeparationHeatRelease);
            Assert.Equal(60, openingGenerationOptions_WT2002.FireSeparationHalfStripAngle);
            Assert.Equal(120, openingGenerationOptions_WT2002.FireSeparationNoApplyAngle);
            Assert.Equal(60, openingGenerationOptions_WT2002.ObstructionSectorAngle);
            Assert.Equal(35, openingGenerationOptions_WT2002.ObstructionMaxDistance);
            Assert.Equal(10, openingGenerationOptions_WT2002.ObstructionSlenderDistance);
            Assert.Null(openingGenerationOptions_WT2002.ObstructionInnerCityFactor);
            Assert.Equal(4.0, openingGenerationOptions_WT2002.RoadFrontageDistance);
            Assert.Equal(1.0, openingGenerationOptions_WT2002.FrontageLineSetbackTolerance);
            Assert.Equal(3, openingGenerationOptions_WT2002.FrontageLineMinimumFronts);

            OpeningGenerationInput openingGenerationInput = new(1975, YearBuiltSource.Prediction, new Point2D(5, -3), Regulation.Enums.ClimateZone.IV, "OWT-67", openingGenerationOptions, [new Polyline2D([new Point2D(0, 0), new Point2D(5, 5)])]);
            Assert.Equal((short)1975, openingGenerationInput.YearBuilt);
            Assert.Equal(YearBuiltSource.Prediction, openingGenerationInput.YearBuiltSource);
            Assert.Equal(-3, openingGenerationInput.AddressPoint!.Y);
            Assert.Equal(Regulation.Enums.ClimateZone.IV, openingGenerationInput.ClimateZone);
            Assert.Equal("OWT-67", openingGenerationInput.PanelSystem);
            Assert.Equal(12.5, openingGenerationInput.OpeningGenerationOptions!.StaircaseSpacing);
            Assert.NotNull(openingGenerationInput.Roads);
            Assert.Single(openingGenerationInput.Roads!);
            Assert.Equal(5, openingGenerationInput.Roads![0].GetPoints()![1].Y);
            Assert.Equal(Core.Convert.ToSystem_String(openingGenerationInput), Core.Convert.ToSystem_String(new OpeningGenerationInput(openingGenerationInput)));
            Core.xUnit.Query.SerializationCheck(openingGenerationInput);
            Assert.NotNull(new OpeningGenerationInput(openingGenerationInput).Roads);

            Guid guid = Guid.NewGuid();
            OpeningGenerationResult openingGenerationResult = new(
                "REF",
                Regulation.Enums.BuildingRegulation.PL_1980,
                1975,
                YearBuiltSource.Prediction,
                Regulation.Enums.ConstructionFlavour.TypicalAsBuilt,
                Regulation.Enums.BuildingType.ApartmentBlock,
                "sizes",
                "constructions",
                StoreySource.Building2D,
                123.5,
                guid,
                FrontWallRule.AddressPoint,
                [new OpeningGenerationStorey(0, 0, 2.8, false, 10, 8, 4)],
                new Dictionary<Guid, WallOpeningRestriction>() { [guid] = WallOpeningRestriction.None, [Guid.NewGuid()] = WallOpeningRestriction.PartyWall },
                2,
                1,
                3,
                new Dictionary<Guid, string>() { [guid] = "FrontageLine.0.1.0.0" },
                new Dictionary<Guid, Regulation.Enums.ConstructionRole>() { [guid] = Regulation.Enums.ConstructionRole.PartyWall });
            Assert.Equal(2.0, openingGenerationResult.Storeys![0].Shortfall);
            Assert.Equal(guid, openingGenerationResult.FrontWall);
            Assert.Equal(2, openingGenerationResult.WallOpeningRestrictions!.Count);
            Assert.Equal("FrontageLine.0.1.0.0", openingGenerationResult.FrontageLines![guid]);
            Assert.Equal(Regulation.Enums.ConstructionRole.PartyWall, openingGenerationResult.MissingConstructionRoles![guid]);
            OpeningGenerationResult openingGenerationResult_Copy = new(openingGenerationResult);
            Assert.Equal(Core.Convert.ToSystem_String(openingGenerationResult), Core.Convert.ToSystem_String(openingGenerationResult_Copy));
            Assert.Equal("FrontageLine.0.1.0.0", openingGenerationResult_Copy.FrontageLines![guid]);
            Assert.Equal(Regulation.Enums.ConstructionRole.PartyWall, openingGenerationResult_Copy.MissingConstructionRoles![guid]);
            Assert.Equal(3, openingGenerationResult_Copy.DroppedOpeningCount);
            Core.xUnit.Query.SerializationCheck(openingGenerationResult);

            Guid guid_Wall = Guid.NewGuid();
            WallOpeningResult wallOpeningResult = new(new GuidReference(null, guid_Wall), WallOpeningRestriction.FireSeparation, 7.9, 8, 11.9, 12);
            Assert.Equal(guid_Wall, wallOpeningResult.Wall!.Guid);
            Assert.Equal(WallOpeningRestriction.FireSeparation, wallOpeningResult.Restriction);
            Assert.Equal(7.9, wallOpeningResult.FireSeparationDistance!.Value);
            Assert.Equal(8, wallOpeningResult.FireSeparationRequired!.Value);
            Assert.Equal(11.9, wallOpeningResult.ObstructionDistance!.Value);
            Assert.Equal(12, wallOpeningResult.ObstructionRequired!.Value);
            WallOpeningResult wallOpeningResult_Copy = new(wallOpeningResult);
            Assert.Equal(Core.Convert.ToSystem_String(wallOpeningResult), Core.Convert.ToSystem_String(wallOpeningResult_Copy));
            Assert.Equal(guid_Wall, wallOpeningResult_Copy.Wall!.Guid);
            Core.xUnit.Query.SerializationCheck(wallOpeningResult);

            WallOpeningResult wallOpeningResult_Empty = new(null, WallOpeningRestriction.None, null, null, null, null);
            Assert.Null(wallOpeningResult_Empty.Wall);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult_Empty.Restriction);
            Core.xUnit.Query.SerializationCheck(wallOpeningResult_Empty);
        }

        /// <summary>
        /// Tests <see cref="Query.StoreyBands"/> on a split model, an unsplit model with and without a storey count, and a model with neither a count nor a storey height (DiGi-Organization/DiGi.GIS.Analytical#2).
        /// </summary>
        [Fact]
        public void Query_StoreyBands()
        {
            BuildingModel buildingModel_Split = OpeningGeneration_Box(10, 10, 3, BuildingSpecificFunction.single_family_building).BuildingModel;
            List<Range<double>>? bands = buildingModel_Split.StoreyBands(3, 2.9, out StoreySource storeySource);
            Assert.NotNull(bands);
            Assert.Equal(StoreySource.Spaces, storeySource);
            Assert.Equal(3, bands.Count);
            Assert.Equal(3.0, bands[1].Min, 6);

            PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(Geometry.Spatial.Create.Plane(0.0)!, [new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);
            BuildingModel? buildingModel_Unsplit = Create.BuildingModel(polygonalFace3D, 1, 9.0);
            Assert.NotNull(buildingModel_Unsplit);

            bands = buildingModel_Unsplit.StoreyBands(3, 2.9, out storeySource);
            Assert.Equal(StoreySource.Building2D, storeySource);
            Assert.Equal(3, bands!.Count);
            Assert.Equal(6.0, bands[2].Min, 6);

            bands = buildingModel_Unsplit.StoreyBands(0, 2.9, out storeySource);
            Assert.Equal(StoreySource.Estimated, storeySource);
            Assert.Equal(3, bands!.Count);

            bands = buildingModel_Unsplit.StoreyBands(0, double.NaN, out storeySource);
            Assert.Equal(StoreySource.Single, storeySource);
            Assert.Single(bands!);

            Assert.Null(((BuildingModel?)null).StoreyBands(3, 2.9, out storeySource));
            Assert.Equal(StoreySource.Undefined, storeySource);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> on a 3-storey single-family box of 2015: the daylight target is met on every storey, sills, heads and the interstorey strip stay inside their storeys, every opening is validly hosted and traced, the entrance is on the wall nearest the address point, and the windows carry the library U-value (DiGi-Organization/DiGi.GIS.Analytical#2).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_SingleFamily()
        {
            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(12, 9, 3, BuildingSpecificFunction.single_family_building);

            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015, new Point2D(6, -5)));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(Regulation.Enums.BuildingRegulation.PL_WT2009, openingGenerationResult.BuildingRegulation);
            Assert.Equal(Regulation.Enums.BuildingType.SingleFamily, openingGenerationResult.BuildingType);
            Assert.Equal(StoreySource.Spaces, openingGenerationResult.StoreySource);
            Assert.Equal(FrontWallRule.AddressPoint, openingGenerationResult.FrontWallRule);
            Assert.Equal(1, openingGenerationResult.DoorCount);
            Assert.Equal(0, openingGenerationResult.DoorsBelowMinimumCount);
            Assert.Equal(0, openingGenerationResult.DroppedOpeningCount);

            List<OpeningGenerationStorey> openingGenerationStoreys = openingGenerationResult.Storeys!;
            Assert.Equal(3, openingGenerationStoreys.Count);
            foreach (OpeningGenerationStorey openingGenerationStorey in openingGenerationStoreys)
            {
                Assert.True(openingGenerationStorey.TargetGlazedArea > 0);
                Assert.Equal(0.0, openingGenerationStorey.Shortfall);
                Assert.False(openingGenerationStorey.HeightMismatch);
            }

            // The front wall faces the address point at y = -5, so the door stands on y = 0.
            List<IDoor> doors = buildingModel.GetOpenings<IDoor>() ?? [];
            Assert.Single(doors);
            IDoorConstruction? doorConstruction = buildingModel.GetDoorConstruction(doors[0]);
            Assert.NotNull(doors[0].Surface3D(doorConstruction)?.GetBoundingBox());
            BoundingBox3D boundingBox3D_Door = doors[0].Surface3D(doorConstruction)!.GetBoundingBox()!;
            Assert.Equal(0.0, boundingBox3D_Door.Max!.Y, 6);
            Assert.Equal(0.0, boundingBox3D_Door.Min!.Z, 6);

            List<IWindow> windows = buildingModel.GetOpenings<IWindow>() ?? [];
            Assert.NotEmpty(windows);
            List<IWall> walls = buildingModel.GetComponents<IWall>() ?? [];
            foreach (IWindow window in windows)
            {
                IWindowConstruction? windowConstruction = buildingModel.GetWindowConstruction(window);
                Assert.NotNull(windowConstruction);
                IWall? wall = buildingModel.GetComponent<IWall>(window);
                Assert.NotNull(wall);
                Assert.True(wall.IsValidHost(window, windowConstruction));

                BoundingBox3D boundingBox3D = window.Surface3D(windowConstruction)!.GetBoundingBox()!;
                double bottom = Math.Floor((boundingBox3D.Min!.Z + 1e-6) / 3.0) * 3.0;
                Assert.True(boundingBox3D.Max!.Z <= bottom + 3.0 - 0.3 + 1e-6, $"head {boundingBox3D.Max.Z} above the storey from {bottom}");
                if (bottom > 0)
                {
                    Assert.True(boundingBox3D.Min.Z - bottom >= 0.85 - 1e-6, $"sill {boundingBox3D.Min.Z - bottom} on the storey from {bottom}");
                }

                Assert.Equal("PL_WT2009", OpeningGeneration_Value(window, OpeningParameter.BuildingRegulation));
                Assert.Equal("User", OpeningGeneration_Value(window, OpeningParameter.YearBuiltSource));
                Assert.Equal("Window", OpeningGeneration_Value(window, OpeningParameter.OpeningKind));
                Assert.Equal(Modify.GenerateOpenings_GeneratedBy, OpeningGeneration_Value(window, OpeningParameter.GeneratedBy));
                Assert.True(windowConstruction.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.ThermalTransmittance), out double thermalTransmittance));
                Assert.Equal(1.1, thermalTransmittance);
            }

            // Windows stack, and the interstorey strip holds between a head and the sill above it on the same wall line.
            List<BoundingBox3D> boundingBox3Ds = windows.ConvertAll(x => x.Surface3D(buildingModel.GetWindowConstruction(x))!.GetBoundingBox()!);
            int count_Stacked = 0;
            foreach (BoundingBox3D boundingBox3D_Lower in boundingBox3Ds)
            {
                foreach (BoundingBox3D boundingBox3D_Upper in boundingBox3Ds)
                {
                    bool overlapX = boundingBox3D_Upper.Min!.X < boundingBox3D_Lower.Max!.X - 1e-6 && boundingBox3D_Upper.Max!.X > boundingBox3D_Lower.Min!.X + 1e-6;
                    bool overlapY = boundingBox3D_Upper.Min.Y < boundingBox3D_Lower.Max.Y + 1e-6 && boundingBox3D_Upper.Max.Y > boundingBox3D_Lower.Min.Y - 1e-6;
                    bool sameLine = Math.Abs(boundingBox3D_Upper.Min.X - boundingBox3D_Upper.Max.X) < 1e-6 ? Math.Abs(boundingBox3D_Upper.Min.X - boundingBox3D_Lower.Min.X) < 1e-6 && overlapY : Math.Abs(boundingBox3D_Upper.Min.Y - boundingBox3D_Lower.Min.Y) < 1e-6 && overlapX;
                    if (!sameLine || boundingBox3D_Upper.Min.Z <= boundingBox3D_Lower.Max!.Z)
                    {
                        continue;
                    }

                    count_Stacked++;
                    Assert.True(boundingBox3D_Upper.Min.Z - boundingBox3D_Lower.Max.Z >= 0.8 - 1e-6, $"strip {boundingBox3D_Upper.Min.Z - boundingBox3D_Lower.Max.Z}");
                }
            }

            Assert.True(count_Stacked > 0);

            // The room mix: living-room, kitchen and bathroom windows all appear.
            HashSet<string?> roomUses = [.. windows.Select(x => OpeningGeneration_Value(x, OpeningParameter.RoomUse))];
            Assert.Contains("LivingRoom", roomUses);
            Assert.Contains("Kitchen", roomUses);
            Assert.Contains("Bathroom", roomUses);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> on an unsplit 9 m model: with a storey count of 3 it places windows in three bands on each wall, with no count it estimates three bands from the stock's storey height, and a 6 m single band is reported as a height mismatch (DiGi-Organization/DiGi.GIS.Analytical#2).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_StoreyBands()
        {
            PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(Geometry.Spatial.Create.Plane(0.0)!, [new Point2D(0, 0), new Point2D(12, 0), new Point2D(12, 9), new Point2D(0, 9)]);

            BuildingModel? buildingModel = Create.BuildingModel(polygonalFace3D, 1, 9.0);
            Assert.NotNull(buildingModel);
            Building2D building2D = Building2D_Rectangle(12, 9, 3, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building);
            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(StoreySource.Building2D, openingGenerationResult.StoreySource);
            Assert.Equal(3, openingGenerationResult.Storeys!.Count);

            HashSet<double> sills = [];
            foreach (IWindow window in buildingModel.GetOpenings<IWindow>() ?? [])
            {
                sills.Add(Math.Floor(window.Surface3D(buildingModel.GetWindowConstruction(window))!.GetBoundingBox()!.Min!.Z / 3.0));
            }

            Assert.Equal(3, sills.Count);

            buildingModel = Create.BuildingModel(polygonalFace3D, 1, 9.0);
            openingGenerationResult = buildingModel!.GenerateOpenings(Building2D_Rectangle(12, 9, 0, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building), OpeningGeneration_Input(2015));
            Assert.Equal(StoreySource.Estimated, openingGenerationResult!.StoreySource);
            Assert.Equal(3, openingGenerationResult.Storeys!.Count);

            buildingModel = Create.BuildingModel(polygonalFace3D, 1, 6.0);
            openingGenerationResult = buildingModel!.GenerateOpenings(Building2D_Rectangle(12, 9, 1, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building), OpeningGeneration_Input(2015));
            Assert.True(openingGenerationResult!.Storeys!.Single().HeightMismatch);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> on a multi-family building of 2015 and a 10-storey apartment block: staircase doors per staircase spacing with staircase windows above, and the high sill above 25 m (DiGi-Organization/DiGi.GIS.Analytical#2).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_MultiFamily()
        {
            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(30, 12, 4, BuildingSpecificFunction.multi_family_building);
            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(FrontWallRule.LongestEdge, openingGenerationResult.FrontWallRule);
            Assert.Equal(2, openingGenerationResult.DoorCount);

            List<IWindow> windows = buildingModel.GetOpenings<IWindow>() ?? [];
            Assert.Equal(2 * 3, windows.Count(x => OpeningGeneration_Value(x, OpeningParameter.OpeningKind) == "StaircaseWindow"));

            (buildingModel, building2D) = OpeningGeneration_Box(30, 12, 10, BuildingSpecificFunction.multi_family_building);
            openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015));
            Assert.Equal(Regulation.Enums.BuildingType.ApartmentBlock, openingGenerationResult!.BuildingType);
            foreach (IWindow window in buildingModel.GetOpenings<IWindow>() ?? [])
            {
                double bottom = window.Surface3D(buildingModel.GetWindowConstruction(window))!.GetBoundingBox()!.Min!.Z;
                double floor = Math.Floor((bottom + 1e-6) / 3.0) * 3.0;
                if (floor > 25.0)
                {
                    Assert.True(bottom - floor >= 1.1 - 1e-6, $"sill {bottom - floor} on the storey from {floor}");
                }
            }
        }

        /// <summary>
        /// Tests that <see cref="Modify.GenerateOpenings"/> places no opening on the shared parts of a terrace split with <see cref="DiGi.Analytical.Building.Modify.SplitPartyWalls"/>, reports them, and gives the middle house the terraced type (DiGi-Organization/DiGi.GIS.Analytical#2).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_PartyWalls()
        {
            BuildingModel buildingModel_A = PartyWalls_BuildingModel(2, new Point2D(0, 0), new Point2D(6, 0), new Point2D(6, 10), new Point2D(0, 10));
            BuildingModel buildingModel_B = PartyWalls_BuildingModel(2, new Point2D(6, 0), new Point2D(12, 0), new Point2D(12, 10), new Point2D(6, 10));
            BuildingModel buildingModel_C = PartyWalls_BuildingModel(2, new Point2D(12, 0), new Point2D(18, 0), new Point2D(18, 10), new Point2D(12, 10));

            List<PartyWallResult>? partyWallResults = DiGi.Analytical.Building.Query.PartyWalls(buildingModel_B, [buildingModel_A, buildingModel_C]);
            Assert.True(DiGi.Analytical.Building.Modify.SplitPartyWalls(buildingModel_B, partyWallResults));

            Building2D building2D = Building2D_Rectangle(6, 10, 2, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building);
            OpeningGenerationResult? openingGenerationResult = buildingModel_B.GenerateOpenings(building2D, OpeningGeneration_Input(2015));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(Regulation.Enums.BuildingType.Terraced, openingGenerationResult.BuildingType);
            Assert.Contains(WallOpeningRestriction.PartyWall, openingGenerationResult.WallOpeningRestrictions!.Values);

            foreach (IWindow window in buildingModel_B.GetOpenings<IWindow>() ?? [])
            {
                IWall? wall = buildingModel_B.GetComponent<IWall>(window);
                Assert.False(wall is Component component && component.TryGetValue(new EnumParameterDefinition(ComponentParameter.PartyWall), out Guid _), "a window on a party wall");
            }

            Assert.NotEmpty(buildingModel_B.GetOpenings<IWindow>() ?? []);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> on a 1975 apartment block: the W-70 sizes on the 0.60 m façade module, no bathroom window (the set lists it as windowless), the TABULA window U of 2.6 and g of 0.75; a user year of 2015 changes the regime parameter; and the same input gives the same openings (DiGi-Organization/DiGi.GIS.Analytical#2, two items moved from #6).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_PanelBlock()
        {
            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(36, 12, 5, BuildingSpecificFunction.multi_family_building);
            BuildingModel buildingModel_Copy = new(buildingModel);

            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, new OpeningGenerationInput(1977, YearBuiltSource.Prediction, null, Regulation.Enums.ClimateZone.Undefined, null, null));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(Regulation.Enums.BuildingType.ApartmentBlock, openingGenerationResult.BuildingType);
            Assert.Contains("W-70", openingGenerationResult.OpeningSizeSetSource);

            List<IWindow> windows = [.. (buildingModel.GetOpenings<IWindow>() ?? []).Where(x => OpeningGeneration_Value(x, OpeningParameter.OpeningKind) == "Window")];
            Assert.NotEmpty(windows);
            Assert.DoesNotContain(windows, x => OpeningGeneration_Value(x, OpeningParameter.RoomUse) == "Bathroom");

            int count_OnModule = 0;
            foreach (IWindow window in windows)
            {
                IWindowConstruction? windowConstruction = buildingModel.GetWindowConstruction(window);
                Assert.True(windowConstruction!.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.ThermalTransmittance), out double thermalTransmittance));
                Assert.Equal(2.6, thermalTransmittance);
                Assert.True(windowConstruction.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.TotalSolarEnergyTransmittance), out double totalSolarEnergyTransmittance));
                Assert.Equal(0.75, totalSolarEnergyTransmittance);
                Assert.Equal("PL_1961", OpeningGeneration_Value(window, OpeningParameter.BuildingRegulation));
                Assert.Equal("Prediction", OpeningGeneration_Value(window, OpeningParameter.YearBuiltSource));

                // The building is a multiple of the module long and deep, so a window on the module has an edge at k x 0.6 - 0.0175 or k x 0.6 + 0.0175 along its wall.
                BoundingBox3D boundingBox3D = window.Surface3D(windowConstruction)!.GetBoundingBox()!;
                bool alongX = boundingBox3D.Max!.X - boundingBox3D.Min!.X > boundingBox3D.Max.Y - boundingBox3D.Min.Y;
                double min = alongX ? boundingBox3D.Min.X : boundingBox3D.Min.Y;
                double max = alongX ? boundingBox3D.Max.X : boundingBox3D.Max.Y;
                static bool OnModule(double value)
                {
                    double remainder = value - (Math.Round(value / 0.6) * 0.6);
                    return Math.Abs(remainder) < 1e-6;
                }

                if (OnModule(min - 0.0175) || OnModule(max + 0.0175))
                {
                    count_OnModule++;
                }
            }

            Assert.True(count_OnModule == windows.Count, $"{count_OnModule} of {windows.Count} windows on the 0.60 m module");

            // The same input gives the same openings.
            buildingModel_Copy.GenerateOpenings(building2D, new OpeningGenerationInput(1977, YearBuiltSource.Prediction, null, Regulation.Enums.ClimateZone.Undefined, null, null));
            Assert.Equal(Core.Convert.ToSystem_String(buildingModel), Core.Convert.ToSystem_String(buildingModel_Copy));

            // A user year of 2015 selects another regime.
            (BuildingModel buildingModel_2015, Building2D building2D_2015) = OpeningGeneration_Box(36, 12, 5, BuildingSpecificFunction.multi_family_building);
            buildingModel_2015.GenerateOpenings(building2D_2015, OpeningGeneration_Input(2015));
            Assert.All(buildingModel_2015.GetOpenings<IWindow>() ?? [], x => Assert.Equal("PL_WT2009", OpeningGeneration_Value(x, OpeningParameter.BuildingRegulation)));
        }

        /// <summary>
        /// Tests that <see cref="Modify.GenerateOpenings"/> reports a shortfall rather than failing when a deep single-storey building cannot reach the daylight target on its façades (DiGi-Organization/DiGi.GIS.Analytical#2).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_Shortfall()
        {
            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(40, 40, 1, BuildingSpecificFunction.multi_family_building);
            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015));
            Assert.NotNull(openingGenerationResult);
            OpeningGenerationStorey openingGenerationStorey = openingGenerationResult.Storeys!.Single();
            Assert.True(openingGenerationStorey.Shortfall > 0, $"target {openingGenerationStorey.TargetGlazedArea}, achieved {openingGenerationStorey.AchievedGlazedArea}");
            Assert.True(openingGenerationStorey.WindowCount > 0);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> against neighbours (DiGi-Organization/DiGi.GIS.Analytical#9): a wall 7.9 m from a facing neighbour carries no opening at all and reports <see cref="WallOpeningRestriction.FireSeparation"/> while the other walls and the entrance are generated, and a wall with a 12 m neighbour at 9 m keeps its entrance door but gets no window of a room for people, reporting <see cref="WallOpeningRestriction.Obstruction"/>.
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_Neighbours()
        {
            // § 271: the east wall faces a neighbour 7.9 m away and stays blank.
            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(10, 10, 3, BuildingSpecificFunction.single_family_building);
            IWall wall_East = WallOpeningResults_Wall(buildingModel, x: 10);
            List<BuildingModel> buildingModels = [WallOpeningResults_Neighbour(1, true, new Point2D(12.9, 5), new Point2D(17.9, 0), new Point2D(27.9, 0), new Point2D(27.9, 10), new Point2D(17.9, 10))];

            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015), buildingModels);
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(WallOpeningRestriction.FireSeparation, openingGenerationResult!.WallOpeningRestrictions![wall_East.Guid]);
            Assert.NotEmpty(buildingModel.GetOpenings<IWindow>() ?? []);
            Assert.Equal(1, openingGenerationResult.DoorCount);
            foreach (IOpening opening in buildingModel.GetOpenings<IOpening>() ?? [])
            {
                Assert.False(buildingModel.GetComponent<IWall>(opening)?.Guid == wall_East.Guid, "an opening on the fire-separated wall");
            }

            // § 13: the north wall has a 12 m neighbour at 9 m; it keeps the entrance door but gets no room windows.
            (buildingModel, building2D) = OpeningGeneration_Box(10, 10, 3, BuildingSpecificFunction.single_family_building);
            IWall wall_North = WallOpeningResults_Wall(buildingModel, y: 10);
            buildingModels = [PartyWalls_BuildingModel(4, new Point2D(0, 19), new Point2D(10, 19), new Point2D(10, 29), new Point2D(0, 29))];

            openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015, new Point2D(5, 15)), buildingModels);
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(WallOpeningRestriction.Obstruction, openingGenerationResult!.WallOpeningRestrictions![wall_North.Guid]);

            foreach (IWindow window in buildingModel.GetOpenings<IWindow>() ?? [])
            {
                Assert.False(buildingModel.GetComponent<IWall>(window)?.Guid == wall_North.Guid, "a window on the obstructed wall");
            }

            Assert.NotEmpty(buildingModel.GetOpenings<IWindow>() ?? []);
            List<IDoor> doors = buildingModel.GetOpenings<IDoor>() ?? [];
            Assert.Single(doors);
            Assert.Equal(wall_North.Guid, buildingModel.GetComponent<IWall>(doors[0])!.Guid);
        }
    }
}
