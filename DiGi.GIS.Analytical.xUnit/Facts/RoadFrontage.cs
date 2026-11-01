using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
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
        /// Creates a building model from a footprint spanning the two corners, at ground level, with storeys of 3 m (DiGi-Organization/DiGi.GIS.Analytical#10).
        /// </summary>
        /// <param name="minX">The minimum x of the footprint.</param>
        /// <param name="minY">The minimum y of the footprint.</param>
        /// <param name="maxX">The maximum x of the footprint.</param>
        /// <param name="maxY">The maximum y of the footprint.</param>
        /// <param name="storeys">The number of storeys.</param>
        /// <returns>The building model and its 2D building.</returns>
        private static (BuildingModel BuildingModel, Building2D Building2D) RoadFrontage_Box(double minX, double minY, double maxX, double maxY, ushort storeys)
        {
            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(new Point2D(minX, minY), new Point2D(maxX, minY), new Point2D(maxX, maxY), new Point2D(minX, maxY));
            Assert.NotNull(polygonalFace2D);

            Building2D building2D = new(Guid.NewGuid(), Guid.NewGuid().ToString(), polygonalFace2D, storeys, null, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]);
            BuildingModel? buildingModel = Create.BuildingModel(building2D, 0, 3.0);
            Assert.NotNull(buildingModel);

            return (buildingModel, building2D);
        }

        /// <summary>
        /// Counts the windows of a model hosted by a wall lying on a plan line: the walls whose bounding box sits on x = <paramref name="x"/> or on y = <paramref name="y"/>.
        /// </summary>
        /// <param name="buildingModel">The building model.</param>
        /// <param name="x">The x of the line the walls stand on, or null to match on y only.</param>
        /// <param name="y">The y of the line the walls stand on, or null to match on x only.</param>
        /// <returns>The number of windows.</returns>
        private static int RoadFrontage_Windows(BuildingModel buildingModel, double? x = null, double? y = null)
        {
            int result = 0;
            foreach (IWindow window in buildingModel.GetOpenings<IWindow>() ?? [])
            {
                IWall? wall = buildingModel.GetComponent<IWall>(window);
                BoundingBox3D? boundingBox3D = wall?.GetBoundingBox();
                if (boundingBox3D is null)
                {
                    continue;
                }

                double centreX = (boundingBox3D.Min!.X + boundingBox3D.Max.X) / 2;
                double centreY = (boundingBox3D.Min.Y + boundingBox3D.Max.Y) / 2;
                if ((x is null || Math.Abs(centreX - x.Value) < 1e-3) && (y is null || Math.Abs(centreY - y.Value) < 1e-3))
                {
                    result++;
                }
            }

            return result;
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> against roads under § 12 ust. 10: a road 2 m off the east wall reports that wall as <see cref="WallOpeningRestriction.RoadFrontage"/> while the other walls stay unrestricted, a road beyond <c>RoadFrontageDistance</c> reports nothing, and a road inside the lot behind the walls' outward sides marks nothing (DiGi-Organization/DiGi.GIS.Analytical#10).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_RoadFrontage()
        {
            Polyline2D polyline2D_East = new([new Point2D(14, -10), new Point2D(14, 19)]);

            (BuildingModel buildingModel, Building2D building2D) = OpeningGeneration_Box(12, 9, 3, BuildingSpecificFunction.single_family_building);
            OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015, new Point2D(6, -5), [polyline2D_East]));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(WallOpeningRestriction.RoadFrontage, openingGenerationResult!.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, x: 12).Guid]);
            Assert.Equal(WallOpeningRestriction.None, openingGenerationResult.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, x: 0).Guid]);
            Assert.Equal(WallOpeningRestriction.None, openingGenerationResult.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, y: 0).Guid]);
            Assert.Equal(WallOpeningRestriction.None, openingGenerationResult.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, y: 9).Guid]);

            // 4.5 m off: past the facing strip, so nothing is reported.
            Polyline2D polyline2D_Far = new([new Point2D(16.5, -10), new Point2D(16.5, 19)]);
            (buildingModel, building2D) = OpeningGeneration_Box(12, 9, 3, BuildingSpecificFunction.single_family_building);
            openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015, new Point2D(6, -5), [polyline2D_Far]));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(WallOpeningRestriction.None, openingGenerationResult!.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, x: 12).Guid]);

            // A road inside the lot: close to every wall, but behind all of their outward sides.
            Polyline2D polyline2D_Inner = new([new Point2D(3, 2), new Point2D(9, 2)]);
            (buildingModel, building2D) = OpeningGeneration_Box(12, 9, 3, BuildingSpecificFunction.single_family_building);
            openingGenerationResult = buildingModel.GenerateOpenings(building2D, OpeningGeneration_Input(2015, new Point2D(6, -5), [polyline2D_Inner]));
            Assert.NotNull(openingGenerationResult);
            Assert.Equal(WallOpeningRestriction.None, openingGenerationResult!.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, x: 12).Guid]);
            Assert.Equal(WallOpeningRestriction.None, openingGenerationResult.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, x: 0).Guid]);
            Assert.Equal(WallOpeningRestriction.None, openingGenerationResult.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, y: 0).Guid]);
            Assert.Equal(WallOpeningRestriction.None, openingGenerationResult.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel, y: 9).Guid]);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> on a corner building with roads on its east and south walls: both road walls report <see cref="WallOpeningRestriction.RoadFrontage"/> and both gain windows from the street-façade weight against a run without roads, while the entrance stays on the wall nearest the address point and no frontage line is reported without neighbours (DiGi-Organization/DiGi.GIS.Analytical#10).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_CornerRoadWeight()
        {
            // The daylight ratio is lowered so the walls keep spare capacity: with the default
            // target this fixture saturates every wall, and overflow redistribution would mask
            // what the weight did to the quotas.
            OpeningGenerationOptions openingGenerationOptions = new() { DaylightRatio = 0.05, StreetFacadeWeights = new Dictionary<Regulation.Enums.BuildingType, double>() { [Regulation.Enums.BuildingType.SingleFamily] = 10.0 } };
            Polyline2D polyline2D_East = new([new Point2D(32, -10), new Point2D(32, 30)]);
            Polyline2D polyline2D_South = new([new Point2D(-10, -2), new Point2D(40, -2)]);

            (BuildingModel buildingModel_With, Building2D building2D_With) = RoadFrontage_Box(0, 0, 30, 20, 3);
            OpeningGenerationResult? result_With = buildingModel_With.GenerateOpenings(building2D_With, new OpeningGenerationInput(2015, YearBuiltSource.User, new Point2D(15, 25), Regulation.Enums.ClimateZone.Undefined, null, openingGenerationOptions, [polyline2D_East, polyline2D_South]));
            Assert.NotNull(result_With);

            (BuildingModel buildingModel_Without, Building2D building2D_Without) = RoadFrontage_Box(0, 0, 30, 20, 3);
            OpeningGenerationResult? result_Without = buildingModel_Without.GenerateOpenings(building2D_Without, new OpeningGenerationInput(2015, YearBuiltSource.User, new Point2D(15, 25), Regulation.Enums.ClimateZone.Undefined, null, openingGenerationOptions));
            Assert.NotNull(result_Without);

            // Both road walls gain the street-façade weight they do not have without roads,
            // and the east wall beats its same-length unweighted counterpart in the same run.
            int windows_East_With = RoadFrontage_Windows(buildingModel_With, x: 30);
            int windows_West_With = RoadFrontage_Windows(buildingModel_With, x: 0);
            int windows_South_With = RoadFrontage_Windows(buildingModel_With, y: 0);
            Assert.True(windows_East_With > RoadFrontage_Windows(buildingModel_Without, x: 30), $"east {windows_East_With} with roads not above the run without roads");
            Assert.True(windows_South_With > RoadFrontage_Windows(buildingModel_Without, y: 0), $"south {windows_South_With} with roads not above the run without roads");
            Assert.True(windows_East_With > windows_West_With, $"east {windows_East_With} not above west {windows_West_With}");

            Assert.Equal(WallOpeningRestriction.RoadFrontage, result_With!.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel_With, x: 30).Guid]);
            Assert.Equal(WallOpeningRestriction.RoadFrontage, result_With.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel_With, y: 0).Guid]);
            Assert.Equal(WallOpeningRestriction.None, result_With.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel_With, x: 0).Guid]);
            Assert.Equal(WallOpeningRestriction.None, result_With.WallOpeningRestrictions![WallOpeningResults_Wall(buildingModel_With, y: 20).Guid]);

            // The entrance stays on the wall nearest the address point at y = 25: the north wall.
            Assert.Equal(FrontWallRule.AddressPoint, result_With.FrontWallRule);
            List<IDoor> doors = buildingModel_With.GetOpenings<IDoor>() ?? [];
            Assert.Single(doors);
            IWall? wall_Door = buildingModel_With.GetComponent<IWall>(doors[0]);
            Assert.NotNull(wall_Door);
            Assert.Equal(result_With.FrontWall, wall_Door!.Guid);
            BoundingBox3D? boundingBox3D_Wall = wall_Door.GetBoundingBox();
            Assert.NotNull(boundingBox3D_Wall);
            Assert.Equal(20.0, boundingBox3D_Wall!.Max!.Y, 3);

            // A single building can never form a frontage line.
            Assert.Null(result_With.FrontageLines);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> on a row of three aligned fronts along one road: the subject's road walls are reported as one frontage line under a key derived from the geometry alone - a second identical run gives the same key with different wall identifiers - and a third front set back beyond <c>FrontageLineSetbackTolerance</c> drops the line below the minimum (DiGi-Organization/DiGi.GIS.Analytical#10).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_FrontageLine()
        {
            Polyline2D polyline2D_Road = new([new Point2D(-5, -2), new Point2D(40, -2)]);

            (BuildingModel buildingModel_A, Building2D _) = RoadFrontage_Box(0, 0, 10, 8, 3);
            (BuildingModel buildingModel_B, Building2D building2D_B) = RoadFrontage_Box(12, 0, 22, 8, 3);
            (BuildingModel buildingModel_C, Building2D _) = RoadFrontage_Box(24, 0, 34, 8, 3);

            OpeningGenerationResult? openingGenerationResult = buildingModel_B.GenerateOpenings(building2D_B, OpeningGeneration_Input(2015, new Point2D(17, -5), [polyline2D_Road]), [buildingModel_A, buildingModel_C]);
            Assert.NotNull(openingGenerationResult);
            Dictionary<Guid, string>? frontageLines = openingGenerationResult!.FrontageLines;
            Assert.NotNull(frontageLines);

            // The three south walls of the subject, and nothing else, share one key.
            int count_South = 0;
            foreach (IWall wall in buildingModel_B.GetComponents<IWall>() ?? [])
            {
                BoundingBox3D? boundingBox3D = wall.GetBoundingBox();
                Assert.NotNull(boundingBox3D);
                if (Math.Abs(boundingBox3D!.Min!.Y) < 1e-3 && Math.Abs(boundingBox3D.Max.Y) < 1e-3)
                {
                    count_South++;
                    Assert.True(frontageLines.ContainsKey(wall.Guid));
                }
            }

            Assert.Equal(3, count_South);
            Assert.Equal(3, frontageLines!.Count);
            string? key = null;
            foreach (string value in frontageLines.Values)
            {
                key ??= value;
                Assert.Equal(key, value);
            }

            Assert.NotNull(key);

            // The same geometry with fresh models: the key is derived from the line, not from any identifier.
            (BuildingModel buildingModel_A2, Building2D _) = RoadFrontage_Box(0, 0, 10, 8, 3);
            (BuildingModel buildingModel_B2, Building2D building2D_B2) = RoadFrontage_Box(12, 0, 22, 8, 3);
            (BuildingModel buildingModel_C2, Building2D _) = RoadFrontage_Box(24, 0, 34, 8, 3);
            OpeningGenerationResult? openingGenerationResult_2 = buildingModel_B2.GenerateOpenings(building2D_B2, OpeningGeneration_Input(2015, new Point2D(17, -5), [polyline2D_Road]), [buildingModel_A2, buildingModel_C2]);
            Assert.NotNull(openingGenerationResult_2);
            Assert.NotNull(openingGenerationResult_2!.FrontageLines);
            Assert.Equal(3, openingGenerationResult_2.FrontageLines!.Count);
            Assert.All(openingGenerationResult_2.FrontageLines.Values, value => Assert.Equal(key, value));

            // The third front set back 1.5 m: only two fronts align, below the minimum of three.
            (BuildingModel buildingModel_A3, Building2D _) = RoadFrontage_Box(0, 0, 10, 8, 3);
            (BuildingModel buildingModel_B3, Building2D building2D_B3) = RoadFrontage_Box(12, 0, 22, 8, 3);
            (BuildingModel buildingModel_C3, Building2D _) = RoadFrontage_Box(24, 1.5, 34, 9.5, 3);
            OpeningGenerationResult? openingGenerationResult_3 = buildingModel_B3.GenerateOpenings(building2D_B3, OpeningGeneration_Input(2015, new Point2D(17, -5), [polyline2D_Road]), [buildingModel_A3, buildingModel_C3]);
            Assert.NotNull(openingGenerationResult_3);
            Assert.Null(openingGenerationResult_3!.FrontageLines);
        }

        /// <summary>
        /// Tests <see cref="Modify.GenerateOpenings"/> without roads: a null road list and an empty one give byte-identical results, neither carries a <see cref="WallOpeningRestriction.RoadFrontage"/> nor a frontage line, so a caller passing no road context gets the phase A behaviour unchanged (DiGi-Organization/DiGi.GIS.Analytical#10).
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_NoRoads_Equal()
        {
            Building2D building2D = Building2D_Rectangle(12, 9, 3, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building);

            BuildingModel? buildingModel = Create.BuildingModel(building2D, 0, 3.0);
            Assert.NotNull(buildingModel);
            BuildingModel buildingModel_Copy = new(buildingModel!);

            OpeningGenerationResult? result_Null = buildingModel!.GenerateOpenings(building2D, OpeningGeneration_Input(2015, new Point2D(6, -5)));
            OpeningGenerationResult? result_Empty = buildingModel_Copy.GenerateOpenings(building2D, OpeningGeneration_Input(2015, new Point2D(6, -5), []));
            Assert.NotNull(result_Null);
            Assert.NotNull(result_Empty);
            Assert.Null(result_Null!.FrontageLines);
            Assert.Null(result_Empty!.FrontageLines);
            Assert.DoesNotContain(result_Null.WallOpeningRestrictions!.Values, x => x == WallOpeningRestriction.RoadFrontage);
            Assert.DoesNotContain(result_Empty.WallOpeningRestrictions!.Values, x => x == WallOpeningRestriction.RoadFrontage);
            Assert.Equal(Core.Convert.ToSystem_String(result_Null), Core.Convert.ToSystem_String(result_Empty));
        }
    }
}
