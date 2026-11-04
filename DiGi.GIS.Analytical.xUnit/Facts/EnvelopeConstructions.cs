using DiGi.Analytical.Building;
using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Parameter.Classes;
using DiGi.Geometry.Planar.Classes;
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
        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> envelope constructions on a 4-storey multi-family box of 1975: every wall gets the TABULA wall <c>PL.Wall.ReEx.03.03</c> (U = 1.3), the flat roof gets the set's flat-roof construction (U = 0.6), the lowest floor gets the cellar floor of the record (U = 1.3), the upper floors get the layered DEFAULT IntermediateFloor of DiGi-Organization/DiGi.Analytical#18 with its layers and no declared U, no role is reported missing, and the result carries the set's source (DiGi-Organization/DiGi.GIS.Analytical#11).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_Envelope_1975()
        {
            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(30, 12, 4, BuildingSpecificFunction.multi_family_building);

            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(1975));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(Regulation.Enums.BuildingType.MultiFamily, openingGenerationResult!.BuildingType);
            Assert.Equal(Regulation.Enums.ConstructionFlavour.TypicalAsBuilt, openingGenerationResult.ConstructionFlavour);
            Assert.False(string.IsNullOrEmpty(openingGenerationResult.ConstructionSetSource));

            List<IWall> walls = buildingModel.GetComponents<IWall>() ?? [];
            Assert.NotEmpty(walls);
            foreach (IWall wall in walls)
            {
                IWallConstruction? wallConstruction = buildingModel.GetWallConstruction(wall);
                Assert.NotNull(wallConstruction);
                Assert.Equal(1.3, buildingModel.ThermalTransmittance(wallConstruction, HeatFlowDirection.Horizontal), 6);
            }

            List<IRoof> roofs = buildingModel.GetComponents<IRoof>() ?? [];
            Assert.NotEmpty(roofs);
            foreach (IRoof roof in roofs)
            {
                IRoofConstruction? roofConstruction = buildingModel.GetRoofConstruction(roof);
                Assert.NotNull(roofConstruction);
                Assert.Equal(0.6, buildingModel.ThermalTransmittance(roofConstruction, HeatFlowDirection.Horizontal), 6);
            }

            List<IFloor> floors = buildingModel.GetComponents<IFloor>() ?? [];
            Assert.Equal(4, floors.Count);
            IFloor? floor_Lowest = floors.OrderBy(x => x.GetBoundingBox()!.Min!.Z).FirstOrDefault();
            Assert.NotNull(floor_Lowest);
            foreach (IFloor floor in floors)
            {
                IFloorConstruction? floorConstruction = buildingModel.GetFloorConstruction(floor);
                if (floor.Guid == floor_Lowest!.Guid)
                {
                    // The 1975 record borders a cellar, so the lowest floor takes FloorOverUnheated - the same construction serves both roles in this set.
                    Assert.NotNull(floorConstruction);
                    Assert.Equal(1.3, buildingModel.ThermalTransmittance(floorConstruction, HeatFlowDirection.Horizontal), 6);
                }
                else
                {
                    // The DEFAULT IntermediateFloor of DiGi-Organization/DiGi.Analytical#18 arrives with its layers; it has no declared U.
                    Assert.NotNull(floorConstruction);
                    Assert.Contains("DEFAULT", floorConstruction!.Description);
                    Assert.NotEmpty(buildingModel.GetStructureLayers(floorConstruction) ?? []);
                    Assert.False(floorConstruction.TryGetValue(new EnumParameterDefinition(PhysicalComponentConstructionParameter.ThermalTransmittance), out double _));
                }
            }

            Assert.NotNull(openingGenerationResult.MissingConstructionRoles);
            Assert.Empty(openingGenerationResult.MissingConstructionRoles!);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> envelope constructions on a single-family box of 2023 with the <see cref="Regulation.Enums.ConstructionFlavour.Regulation"/> flavour: the design date is 2021 (design lag 2), so the external walls take the 0.20 W/(m2K) stage limit, the flat roof 0.15, the ground floor 0.30 and the windows 0.9 (DiGi-Organization/DiGi.GIS.Analytical#11).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_Envelope_Regulation()
        {
            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(12, 9, 3, BuildingSpecificFunction.single_family_building);

            OpeningGenerationOptions openingGenerationOptions = Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2021);
            openingGenerationOptions.ConstructionFlavour = Regulation.Enums.ConstructionFlavour.Regulation;

            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, new OpeningGenerationInput(2023, YearBuiltSource.User, new Point2D(6, -5), Regulation.Enums.ClimateZone.Undefined, null, openingGenerationOptions));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(Regulation.Enums.ConstructionFlavour.Regulation, openingGenerationResult!.ConstructionFlavour);
            Assert.Equal(Regulation.Enums.BuildingType.SingleFamily, openingGenerationResult.BuildingType);

            List<IWall> walls = buildingModel.GetComponents<IWall>() ?? [];
            Assert.NotEmpty(walls);
            foreach (IWall wall in walls)
            {
                IWallConstruction? wallConstruction = buildingModel.GetWallConstruction(wall);
                Assert.NotNull(wallConstruction);
                double thermalTransmittance = buildingModel.ThermalTransmittance(wallConstruction, HeatFlowDirection.Horizontal);
                Assert.True(thermalTransmittance <= 0.20 + 1e-9, $"wall U {thermalTransmittance}");
                Assert.Equal(0.20, thermalTransmittance, 6);
            }

            List<IRoof> roofs = buildingModel.GetComponents<IRoof>() ?? [];
            Assert.NotEmpty(roofs);
            foreach (IRoof roof in roofs)
            {
                IRoofConstruction? roofConstruction = buildingModel.GetRoofConstruction(roof);
                Assert.NotNull(roofConstruction);
                Assert.Equal(0.15, buildingModel.ThermalTransmittance(roofConstruction, HeatFlowDirection.Horizontal), 6);
            }

            // The 2009-onward single-family record borders soil, so the lowest floor takes GroundFloor; a Regulation
            // set has no IntermediateFloor, so the upper floors fall back to the DEFAULT one of the TypicalAsBuilt set
            // (DiGi-Organization/DiGi.Analytical#18).
            List<IFloor> floors = buildingModel.GetComponents<IFloor>() ?? [];
            Assert.Equal(3, floors.Count);
            IFloor? floor_Lowest = floors.OrderBy(x => x.GetBoundingBox()!.Min!.Z).FirstOrDefault();
            Assert.NotNull(floor_Lowest);
            foreach (IFloor floor in floors)
            {
                IFloorConstruction? floorConstruction = buildingModel.GetFloorConstruction(floor);
                if (floor.Guid == floor_Lowest!.Guid)
                {
                    Assert.NotNull(floorConstruction);
                    Assert.Equal(0.30, buildingModel.ThermalTransmittance(floorConstruction, HeatFlowDirection.Horizontal), 6);
                }
                else
                {
                    Assert.NotNull(floorConstruction);
                    Assert.Contains("DEFAULT", floorConstruction!.Description);
                }
            }

            Assert.NotNull(openingGenerationResult.MissingConstructionRoles);
            Assert.Empty(openingGenerationResult.MissingConstructionRoles!);

            List<IWindow> windows = buildingModel.GetOpenings<IWindow>() ?? [];
            Assert.NotEmpty(windows);
            foreach (IWindow window in windows)
            {
                IWindowConstruction? windowConstruction = buildingModel.GetWindowConstruction(window);
                Assert.NotNull(windowConstruction);
                Assert.True(windowConstruction!.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.ThermalTransmittance), out double thermalTransmittance));
                Assert.True(thermalTransmittance <= 0.9 + 1e-9, $"window U {thermalTransmittance}");
            }
        }

        /// <summary>
        /// Tests that <see cref="OpeningGenerationOptions.AssignEnvelopeConstructions"/> = false leaves every wall, roof and floor construction untouched while the openings are still placed, and that the result then carries no missing construction roles (DiGi-Organization/DiGi.GIS.Analytical#11).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_Envelope_Off()
        {
            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(30, 12, 4, BuildingSpecificFunction.multi_family_building);

            OpeningGenerationOptions openingGenerationOptions = new() { AssignEnvelopeConstructions = false };
            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, new OpeningGenerationInput(1975, YearBuiltSource.User, null, Regulation.Enums.ClimateZone.Undefined, null, openingGenerationOptions));
            Assert.NotNull(openingGenerationResult);
            Assert.Null(openingGenerationResult!.MissingConstructionRoles);

            Assert.NotEmpty(buildingModel.GetOpenings<IWindow>() ?? []);

            foreach (IWall wall in buildingModel.GetComponents<IWall>() ?? [])
            {
                Assert.Null(buildingModel.GetWallConstruction(wall));
            }

            foreach (IRoof roof in buildingModel.GetComponents<IRoof>() ?? [])
            {
                Assert.Null(buildingModel.GetRoofConstruction(roof));
            }

            foreach (IFloor floor in buildingModel.GetComponents<IFloor>() ?? [])
            {
                Assert.Null(buildingModel.GetFloorConstruction(floor));
            }
        }

        /// <summary>
        /// Tests that a wall carrying <see cref="ComponentParameter.PartyWall"/> takes <see cref="Regulation.Enums.ConstructionRole.PartyWall"/>: since DiGi-Organization/DiGi.Analytical#18 the set's DEFAULT layered party wall, not the external wall construction, and no role is reported missing, while the other external walls of the same model take the set's external wall (DiGi-Organization/DiGi.GIS.Analytical#11).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_Envelope_PartyWalls()
        {
            BuildingModel buildingModel_A = PartyWalls_BuildingModel(2, new Point2D(0, 0), new Point2D(6, 0), new Point2D(6, 10), new Point2D(0, 10));
            BuildingModel buildingModel_B = PartyWalls_BuildingModel(2, new Point2D(6, 0), new Point2D(12, 0), new Point2D(12, 10), new Point2D(6, 10));
            BuildingModel buildingModel_C = PartyWalls_BuildingModel(2, new Point2D(12, 0), new Point2D(18, 0), new Point2D(18, 10), new Point2D(12, 10));

            List<PartyWallResult>? partyWallResults = DiGi.Analytical.Building.Query.PartyWalls(buildingModel_B, [buildingModel_A, buildingModel_C]);
            Assert.True(DiGi.Analytical.Building.Modify.SplitPartyWalls(buildingModel_B, partyWallResults));

            Building2D building2D = Building2D_Rectangle(6, 10, 2, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building);
            OpeningGenerationResult? openingGenerationResult = buildingModel_B.GenerateOpenings(building2D, OpeningGeneration_Input(2015));
            Assert.NotNull(openingGenerationResult);
            Assert.NotNull(openingGenerationResult!.MissingConstructionRoles);

            Regulation.Classes.ConstructionLibrary? constructionLibrary = Regulation.Query.EmbeddedConstructionLibrary_PL();
            Assert.NotNull(constructionLibrary);
            Regulation.Classes.ConstructionSet? constructionSet = constructionLibrary.GetConstructionSet(new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero), Regulation.Enums.BuildingType.SingleFamily, Regulation.Enums.ClimateZone.III, Regulation.Enums.ConstructionFlavour.TypicalAsBuilt);
            Guid guid_PartyWall = constructionLibrary.GetConstruction(constructionSet, Regulation.Enums.ConstructionRole.PartyWall)?.Guid ?? Guid.Empty;
            Guid guid_ExternalWall = constructionLibrary.GetConstruction(constructionSet, Regulation.Enums.ConstructionRole.ExternalWall)?.Guid ?? Guid.Empty;
            Assert.NotEqual(Guid.Empty, guid_PartyWall);
            Assert.NotEqual(guid_ExternalWall, guid_PartyWall);

            int count_PartyWall = 0;
            foreach (IWall wall in buildingModel_B.GetComponents<IWall>() ?? [])
            {
                bool partyWall = wall is Component component && component.TryGetValue(new EnumParameterDefinition(ComponentParameter.PartyWall), out Guid _);
                IWallConstruction? wallConstruction = buildingModel_B.GetWallConstruction(wall);
                if (partyWall)
                {
                    count_PartyWall++;
                    Assert.Equal(guid_PartyWall, wallConstruction?.Guid);
                    Assert.NotEmpty(buildingModel_B.GetStructureLayers(wallConstruction) ?? []);
                }
                else
                {
                    Assert.Equal(guid_ExternalWall, wallConstruction?.Guid);
                }
            }

            Assert.True(count_PartyWall > 0);
            Assert.Empty(openingGenerationResult.MissingConstructionRoles!);
        }
    }
}
