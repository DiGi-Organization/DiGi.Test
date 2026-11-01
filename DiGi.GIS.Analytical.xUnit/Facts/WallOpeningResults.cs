using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Parameter.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using Regulation = DiGi.Analytical.Building.Regulation;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Creates options for a neighbour-rule fact: the lowest window sill at zero, so an obstruction height reads as an absolute elevation, and § 271 switched off by an empty distance table when only § 13 is under test.
        /// </summary>
        /// <param name="fireSeparation">True to keep § 271 in force with its default table.</param>
        /// <param name="innerCityFactor">The inner-city factor of § 13, or null for none.</param>
        /// <returns>The options.</returns>
        private static OpeningGenerationOptions WallOpeningResults_Options(bool fireSeparation, double? innerCityFactor = null)
        {
            OpeningGenerationOptions openingGenerationOptions = new() { SillHeight = 0, ObstructionInnerCityFactor = innerCityFactor };
            if (!fireSeparation)
            {
                openingGenerationOptions.FireSeparationDistances = [];
            }

            return openingGenerationOptions;
        }

        /// <summary>
        /// Creates a neighbour box at a footprint given by its corners, at ground level, optionally with openings of its own.
        /// </summary>
        /// <param name="storeys">The number of storeys.</param>
        /// <param name="generate">True to generate the neighbour's own openings first, so the § 271 ust. 9 no-openings reduction cannot apply.</param>
        /// <param name="addressPoint">The address point selecting the neighbour's front wall, or null for one west of the box.</param>
        /// <param name="point2Ds">The corners of the footprint.</param>
        /// <returns>The building model.</returns>
        private static BuildingModel WallOpeningResults_Neighbour(ushort storeys, bool generate, Point2D? addressPoint, params Point2D[] point2Ds)
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(storeys, point2Ds);
            if (generate)
            {
                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;
                foreach (Point2D point2D in point2Ds)
                {
                    minX = Math.Min(minX, point2D.X);
                    minY = Math.Min(minY, point2D.Y);
                    maxX = Math.Max(maxX, point2D.X);
                    maxY = Math.Max(maxY, point2D.Y);
                }

                Building2D building2D = Building2D_Rectangle(maxX - minX, maxY - minY, storeys, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building);
                buildingModel.GenerateOpenings(building2D, new OpeningGenerationInput(2015, YearBuiltSource.User, addressPoint ?? new Point2D(minX - 5, (minY + maxY) / 2), Regulation.Enums.ClimateZone.Undefined, null, null));
            }

            return buildingModel;
        }

        /// <summary>
        /// Gets the wall of a model lying on a plan line: the wall whose bounding box sits on x = <paramref name="x"/> or on y = <paramref name="y"/>.
        /// </summary>
        /// <param name="buildingModel">The building model.</param>
        /// <param name="x">The x of the line the wall stands on, or null to match on y only.</param>
        /// <param name="y">The y of the line the wall stands on, or null to match on x only.</param>
        /// <returns>The wall.</returns>
        private static IWall WallOpeningResults_Wall(BuildingModel buildingModel, double? x = null, double? y = null)
        {
            foreach (IWall wall in buildingModel.GetComponents<IWall>() ?? [])
            {
                BoundingBox3D? boundingBox3D = wall.GetBoundingBox();
                if (boundingBox3D is null)
                {
                    continue;
                }

                double centreX = (boundingBox3D.Min!.X + boundingBox3D.Max.X) / 2;
                double centreY = (boundingBox3D.Min.Y + boundingBox3D.Max.Y) / 2;
                if ((x is null || Math.Abs(centreX - x.Value) < 1e-3) && (y is null || Math.Abs(centreY - y.Value) < 1e-3))
                {
                    return wall;
                }
            }

            Assert.Fail($"no wall on x = {x} y = {y}");
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Gets the result of a wall from the per-wall neighbour results.
        /// </summary>
        /// <param name="wallOpeningResults">The results, or null.</param>
        /// <param name="wall">The wall whose result is wanted.</param>
        /// <returns>The result.</returns>
        private static WallOpeningResult WallOpeningResults_Result(List<WallOpeningResult>? wallOpeningResults, IWall wall)
        {
            Assert.NotNull(wallOpeningResults);
            WallOpeningResult? wallOpeningResult = wallOpeningResults!.Find(x => x.Wall?.Guid == wall.Guid);
            Assert.NotNull(wallOpeningResult);
            return wallOpeningResult!;
        }

        /// <summary>
        /// Tests <see cref="Query.WallOpeningResults(BuildingModel, IEnumerable{BuildingModel?}?, OpeningGenerationOptions, BuildingGeneralFunction?, double)"/> on § 271 between two parallel boxes: 7.9 m apart the facing wall fails the 8 m of the ZL row and 8.1 m apart it passes, the neighbour carrying its own openings so the ust. 9 reduction does not bite, and only the facing wall is touched (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_FireSeparationDistance()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_East = WallOpeningResults_Wall(buildingModel, x: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(true);
            List<BuildingModel> buildingModels = [WallOpeningResults_Neighbour(1, true, new Point2D(12.9, 5), new Point2D(17.9, 0), new Point2D(27.9, 0), new Point2D(27.9, 10), new Point2D(17.9, 10))];

            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.FireSeparation, wallOpeningResult.Restriction);
            Assert.Equal(7.9, wallOpeningResult.FireSeparationDistance!.Value, 6);
            Assert.Equal(8.0, wallOpeningResult.FireSeparationRequired!.Value, 6);

            // Every restricted wall is a piece of the east wall: only those measure the 7.9 m gap, the other lines have no facing pair.
            Assert.All(wallOpeningResults!, x => Assert.True(x.Restriction == WallOpeningRestriction.None || Math.Abs(x.FireSeparationDistance!.Value - 7.9) < 1e-6, $"{x.Restriction} at {x.FireSeparationDistance} m"));

            buildingModels = [WallOpeningResults_Neighbour(1, true, new Point2D(13.1, 5), new Point2D(18.1, 0), new Point2D(28.1, 0), new Point2D(28.1, 10), new Point2D(18.1, 10))];
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.Equal(8.1, wallOpeningResult.FireSeparationDistance!.Value, 6);
            Assert.Equal(8.0, wallOpeningResult.FireSeparationRequired!.Value, 6);
        }

        /// <summary>
        /// Tests § 271 on walls at 90 degrees: the corner pair takes half the strip, so 5 m passes where 8 m would fail and 3.5 m fails below the halved 4 m, while the parallel pair of the same fixture keeps the full distance and fails at 5 m (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_HalfStrip()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_East = WallOpeningResults_Wall(buildingModel, x: 10);
            IWall wall_North = WallOpeningResults_Wall(buildingModel, y: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(true);

            // The neighbour's front wall is its south one, so its own entrance door lands on the wall facing the pair under test.
            List<BuildingModel> buildingModels = [WallOpeningResults_Neighbour(1, true, new Point2D(15, 10), new Point2D(10, 15), new Point2D(20, 15), new Point2D(20, 25), new Point2D(10, 25))];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);

            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.Equal(5.0, wallOpeningResult.FireSeparationDistance!.Value, 6);
            Assert.Equal(4.0, wallOpeningResult.FireSeparationRequired!.Value, 6);

            // The parallel pair of the same fixture (the north wall against the neighbour's south wall) keeps the full 8 m.
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.FireSeparation, wallOpeningResult.Restriction);
            Assert.Equal(5.0, wallOpeningResult.FireSeparationDistance!.Value, 6);
            Assert.Equal(8.0, wallOpeningResult.FireSeparationRequired!.Value, 6);

            // The halved strip still binds below 4 m.
            buildingModels = [WallOpeningResults_Neighbour(1, true, new Point2D(15, 8.5), new Point2D(10, 13.5), new Point2D(20, 13.5), new Point2D(20, 23.5), new Point2D(10, 23.5))];
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.FireSeparation, wallOpeningResult.Restriction);
            Assert.Equal(3.5, wallOpeningResult.FireSeparationDistance!.Value, 6);
            Assert.Equal(4.0, wallOpeningResult.FireSeparationRequired!.Value, 6);
        }

        /// <summary>
        /// Tests that § 271 does not apply between walls forming an angle of 120 degrees or more (ust. 12 pkt 2): a rotated neighbour reaching 7.25 m from the facing wall is measured but unrestricted, the exemption reported as a distance with no requirement (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_AngleNotApplicable()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(20, 0), new Point2D(20, 20), new Point2D(0, 20));
            IWall wall_East = WallOpeningResults_Wall(buildingModel, x: 20);

            // A box rotated so that its facing wall meets the east wall at 130 degrees: its outward normal stands 50 degrees off the east wall's.
            List<BuildingModel> buildingModels = [PartyWalls_BuildingModel(1, new Point2D(20.77, -7.21), new Point2D(15.63, -13.34), new Point2D(23.29, -19.77), new Point2D(28.43, -13.64))];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, WallOpeningResults_Options(true), BuildingGeneralFunction.residential_buildings);

            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.NotNull(wallOpeningResult.FireSeparationDistance);
            Assert.InRange(wallOpeningResult.FireSeparationDistance!.Value, 7.2, 7.3);
            Assert.Null(wallOpeningResult.FireSeparationRequired);
        }

        /// <summary>
        /// Tests the § 271 ust. 9 reduction of 25 %: two blank facing walls take 6 m instead of 8 m, and an opening on the neighbour's facing wall removes the reduction, so 7 m fails at the full distance (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_NoOpenings()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_East = WallOpeningResults_Wall(buildingModel, x: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(true);

            List<BuildingModel> buildingModels = [WallOpeningResults_Neighbour(1, false, null, new Point2D(17, 0), new Point2D(27, 0), new Point2D(27, 10), new Point2D(17, 10))];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.Equal(7.0, wallOpeningResult.FireSeparationDistance!.Value, 6);
            Assert.Equal(6.0, wallOpeningResult.FireSeparationRequired!.Value, 6);

            buildingModels = [WallOpeningResults_Neighbour(1, true, new Point2D(12, 5), new Point2D(17, 0), new Point2D(27, 0), new Point2D(27, 10), new Point2D(17, 10))];
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.FireSeparation, wallOpeningResult.Restriction);
            Assert.Equal(7.0, wallOpeningResult.FireSeparationDistance!.Value, 6);
            Assert.Equal(8.0, wallOpeningResult.FireSeparationRequired!.Value, 6);
        }

        /// <summary>
        /// Tests the § 271 ust. 2 spread factors through <see cref="BuildingModelParameter.FireSpreading"/>: a blank pair at 10 m passes at the reduced 6 m, and flagging the neighbour both raises the requirement to 12 m (8 times 1.5) and removes the reduction (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_FireSpreading()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_East = WallOpeningResults_Wall(buildingModel, x: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(true);

            BuildingModel buildingModel_Neighbour = WallOpeningResults_Neighbour(1, false, null, new Point2D(20, 0), new Point2D(30, 0), new Point2D(30, 10), new Point2D(20, 10));
            List<BuildingModel> buildingModels = [buildingModel_Neighbour];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.Equal(6.0, wallOpeningResult.FireSeparationRequired!.Value, 6);

            buildingModel_Neighbour.SetValue(new EnumParameterDefinition(BuildingModelParameter.FireSpreading), true, new SetValueSettings(true, false));
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.FireSeparation, wallOpeningResult.Restriction);
            Assert.Equal(10.0, wallOpeningResult.FireSeparationDistance!.Value, 6);
            Assert.Equal(12.0, wallOpeningResult.FireSeparationRequired!.Value, 6);
        }

        /// <summary>
        /// Tests § 13 against the height of shading: a 12 m neighbour fails at 11.9 m and passes at 12.1 m from the wall, with the sill at zero so the height reads as an absolute elevation (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_ObstructionHeight()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_North = WallOpeningResults_Wall(buildingModel, y: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(false);

            List<BuildingModel> buildingModels = [PartyWalls_BuildingModel(4, new Point2D(0, 21.9), new Point2D(10, 21.9), new Point2D(10, 31.9), new Point2D(0, 31.9))];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.Obstruction, wallOpeningResult.Restriction);
            Assert.Equal(11.9, wallOpeningResult.ObstructionDistance!.Value, 6);
            Assert.Equal(12.0, wallOpeningResult.ObstructionRequired!.Value, 6);

            buildingModels = [PartyWalls_BuildingModel(4, new Point2D(0, 22.1), new Point2D(10, 22.1), new Point2D(10, 32.1), new Point2D(0, 32.1))];
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.Equal(12.1, wallOpeningResult.ObstructionDistance!.Value, 6);
            Assert.Equal(12.0, wallOpeningResult.ObstructionRequired!.Value, 6);
        }

        /// <summary>
        /// Tests the 35 m cap of § 13: a 36 m neighbour fails at 34.9 m with a requirement capped at 35 m, and at 35.1 m lies beyond the reach of either rule, so the wall is unrestricted (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_ObstructionMaxDistance()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_North = WallOpeningResults_Wall(buildingModel, y: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(false);

            List<BuildingModel> buildingModels = [PartyWalls_BuildingModel(12, new Point2D(0, 44.9), new Point2D(10, 44.9), new Point2D(10, 54.9), new Point2D(0, 54.9))];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.Obstruction, wallOpeningResult.Restriction);
            Assert.Equal(34.9, wallOpeningResult.ObstructionDistance!.Value, 6);
            Assert.Equal(35.0, wallOpeningResult.ObstructionRequired!.Value, 6);

            buildingModels = [PartyWalls_BuildingModel(12, new Point2D(0, 45.1), new Point2D(10, 45.1), new Point2D(10, 55.1), new Point2D(0, 55.1))];
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.Null(wallOpeningResult.ObstructionDistance);
        }

        /// <summary>
        /// Tests the inner-city reduction of § 13 ust. 4: the factor of 0.5 halves the requirement of a 12 m neighbour to 6 m, so the wall fails at 5.9 m and passes at 6.1 m (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_InnerCity()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_North = WallOpeningResults_Wall(buildingModel, y: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(false, 0.5);

            List<BuildingModel> buildingModels = [PartyWalls_BuildingModel(4, new Point2D(0, 15.9), new Point2D(10, 15.9), new Point2D(10, 25.9), new Point2D(0, 25.9))];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.Obstruction, wallOpeningResult.Restriction);
            Assert.Equal(5.9, wallOpeningResult.ObstructionDistance!.Value, 6);
            Assert.Equal(6.0, wallOpeningResult.ObstructionRequired!.Value, 6);

            buildingModels = [PartyWalls_BuildingModel(4, new Point2D(0, 16.1), new Point2D(10, 16.1), new Point2D(10, 26.1), new Point2D(0, 26.1))];
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.Equal(6.1, wallOpeningResult.ObstructionDistance!.Value, 6);
            Assert.Equal(6.0, wallOpeningResult.ObstructionRequired!.Value, 6);
        }

        /// <summary>
        /// Tests the 60 degree sector of § 13: a tall neighbour standing north-east of the corner is an obstruction of the east wall, which looks into the sector, but is ignored by the north wall, whose axes all point away from it (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_Sector()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_East = WallOpeningResults_Wall(buildingModel, x: 10);
            IWall wall_North = WallOpeningResults_Wall(buildingModel, y: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(false);

            List<BuildingModel> buildingModels = [PartyWalls_BuildingModel(12, new Point2D(30, 20), new Point2D(40, 20), new Point2D(40, 30), new Point2D(30, 30))];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);

            WallOpeningResult wallOpeningResult_North = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult_North.Restriction);
            Assert.Null(wallOpeningResult_North.ObstructionDistance);

            WallOpeningResult wallOpeningResult_East = WallOpeningResults_Result(wallOpeningResults, wall_East);
            Assert.Equal(WallOpeningRestriction.Obstruction, wallOpeningResult_East.Restriction);
            Assert.Equal(35.0, wallOpeningResult_East.ObstructionRequired!.Value, 6);
            Assert.True(wallOpeningResult_East.ObstructionDistance!.Value < 30, $"distance {wallOpeningResult_East.ObstructionDistance.Value}");
        }

        /// <summary>
        /// Tests § 13 ust. 3 for slender objects: a 2 m wide, 15 m tall neighbour answers to the fixed 10 m and fails at 9 m and passes at 12 m, while a 4 m wide one of the same height answers to its height and fails at 12 m (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_WallOpeningResults_Slender()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            IWall wall_North = WallOpeningResults_Wall(buildingModel, y: 10);
            OpeningGenerationOptions openingGenerationOptions = WallOpeningResults_Options(false);

            List<BuildingModel> buildingModels = [PartyWalls_BuildingModel(5, new Point2D(4, 19), new Point2D(6, 19), new Point2D(6, 23), new Point2D(4, 23))];
            List<WallOpeningResult>? wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            WallOpeningResult wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.Obstruction, wallOpeningResult.Restriction);
            Assert.Equal(9.0, wallOpeningResult.ObstructionDistance!.Value, 6);
            Assert.Equal(10.0, wallOpeningResult.ObstructionRequired!.Value, 6);

            buildingModels = [PartyWalls_BuildingModel(5, new Point2D(4, 22), new Point2D(6, 22), new Point2D(6, 26), new Point2D(4, 26))];
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.None, wallOpeningResult.Restriction);
            Assert.Equal(12.0, wallOpeningResult.ObstructionDistance!.Value, 6);
            Assert.Equal(10.0, wallOpeningResult.ObstructionRequired!.Value, 6);

            buildingModels = [PartyWalls_BuildingModel(5, new Point2D(3, 22), new Point2D(7, 22), new Point2D(7, 26), new Point2D(3, 26))];
            wallOpeningResults = buildingModel.WallOpeningResults(buildingModels, openingGenerationOptions, BuildingGeneralFunction.residential_buildings);
            wallOpeningResult = WallOpeningResults_Result(wallOpeningResults, wall_North);
            Assert.Equal(WallOpeningRestriction.Obstruction, wallOpeningResult.Restriction);
            Assert.Equal(12.0, wallOpeningResult.ObstructionDistance!.Value, 6);
            Assert.Equal(15.0, wallOpeningResult.ObstructionRequired!.Value, 6);
        }
    }
}
