using DiGi.Analytical.Building.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Creates a building model extruded from a footprint given by its corners, at ground level, with storeys of 3 m.
        /// </summary>
        /// <param name="storeys">The number of storeys.</param>
        /// <param name="point2Ds">The corners of the footprint.</param>
        /// <returns>The building model.</returns>
        private static BuildingModel PartyWalls_BuildingModel(ushort storeys, params Point2D[] point2Ds)
        {
            PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(Geometry.Spatial.Create.Plane(0.0)!, point2Ds);
            Assert.NotNull(polygonalFace3D);

            BuildingModel? buildingModel = Create.BuildingModel(polygonalFace3D, storeys, 3.0);
            Assert.NotNull(buildingModel);

            return buildingModel;
        }

        /// <summary>
        /// Sums the shared area of the given party walls.
        /// </summary>
        /// <param name="partyWallResults">The party walls.</param>
        /// <returns>The total shared area [m2].</returns>
        private static double PartyWalls_Area(List<PartyWallResult>? partyWallResults)
        {
            Assert.NotNull(partyWallResults);

            double result = 0;
            foreach (PartyWallResult partyWallResult in partyWallResults)
            {
                result += partyWallResult.Area;
            }

            return result;
        }

        /// <summary>
        /// Tests <see cref="DiGi.Analytical.Building.Query.PartyWalls"/> on two touching boxes and on a terrace of three (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// <para>Two 10 x 10 m boxes of one 3 m storey sharing the line x = 10 share 30 m2, seen from either side, and the result names the neighbour model and its wall. In a terrace of three the middle building has two party walls and the end buildings one each. A model passed in its own neighbour list is skipped.</para>
        /// </summary>
        [Fact]
        public void Query_PartyWalls_TouchingBoxes()
        {
            BuildingModel buildingModel_A = PartyWalls_BuildingModel(1, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            BuildingModel buildingModel_B = PartyWalls_BuildingModel(1, new Point2D(10, 0), new Point2D(20, 0), new Point2D(20, 10), new Point2D(10, 10));
            BuildingModel buildingModel_C = PartyWalls_BuildingModel(1, new Point2D(20, 0), new Point2D(30, 0), new Point2D(30, 10), new Point2D(20, 10));

            List<PartyWallResult>? partyWallResults_A = DiGi.Analytical.Building.Query.PartyWalls(buildingModel_A, [buildingModel_A, buildingModel_B]);
            Assert.NotNull(partyWallResults_A);
            Assert.Single(partyWallResults_A);
            Assert.Equal(30.0, partyWallResults_A[0].Area, 6);
            Assert.Equal(buildingModel_B.Guid, partyWallResults_A[0].NeighbourBuildingModel?.Guid);

            System.Guid guid_NeighbourComponent = partyWallResults_A[0].NeighbourComponent!.Guid;
            Assert.Contains(buildingModel_B.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!, x => x.Guid == guid_NeighbourComponent);
            Assert.Contains(buildingModel_A.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!, x => x.Guid == partyWallResults_A[0].Component!.Guid);

            Assert.Equal(30.0, PartyWalls_Area(DiGi.Analytical.Building.Query.PartyWalls(buildingModel_B, [buildingModel_A])), 6);

            // Terrace of three.
            List<BuildingModel> buildingModels = [buildingModel_A, buildingModel_B, buildingModel_C];
            List<PartyWallResult>? partyWallResults_Middle = DiGi.Analytical.Building.Query.PartyWalls(buildingModel_B, buildingModels);
            Assert.NotNull(partyWallResults_Middle);
            Assert.Equal(2, partyWallResults_Middle.Count);
            Assert.Equal(60.0, PartyWalls_Area(partyWallResults_Middle), 6);
            Assert.Single(DiGi.Analytical.Building.Query.PartyWalls(buildingModel_A, buildingModels)!);
            Assert.Single(DiGi.Analytical.Building.Query.PartyWalls(buildingModel_C, buildingModels)!);

            // No neighbours, no party walls; a null model gives null.
            Assert.Empty(DiGi.Analytical.Building.Query.PartyWalls(buildingModel_A, [])!);
            Assert.Null(DiGi.Analytical.Building.Query.PartyWalls(null, buildingModels));
        }

        /// <summary>
        /// Tests that <see cref="DiGi.Analytical.Building.Query.PartyWalls"/> returns only the part both walls cover (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// <para>A three-storey building next to a one-storey one shares the lower 3 m band only, 30 m2, seen from either side. A neighbour shifted by 4 m along the shared line shares 6 m x 3 m = 18 m2.</para>
        /// <para>Two overlapping footprints - the same building filed twice, shifted by 4 m - have coplanar walls on the same side; they face the same way, so they are no party wall.</para>
        /// </summary>
        [Fact]
        public void Query_PartyWalls_PartialOverlap()
        {
            BuildingModel buildingModel_High = PartyWalls_BuildingModel(3, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            BuildingModel buildingModel_Low = PartyWalls_BuildingModel(1, new Point2D(10, 0), new Point2D(20, 0), new Point2D(20, 10), new Point2D(10, 10));

            Assert.Equal(30.0, PartyWalls_Area(DiGi.Analytical.Building.Query.PartyWalls(buildingModel_High, [buildingModel_Low])), 6);
            Assert.Equal(30.0, PartyWalls_Area(DiGi.Analytical.Building.Query.PartyWalls(buildingModel_Low, [buildingModel_High])), 6);

            BuildingModel buildingModel_Shifted = PartyWalls_BuildingModel(1, new Point2D(10, 4), new Point2D(20, 4), new Point2D(20, 14), new Point2D(10, 14));
            Assert.Equal(18.0, PartyWalls_Area(DiGi.Analytical.Building.Query.PartyWalls(buildingModel_High, [buildingModel_Shifted])), 6);

            Assert.Empty(DiGi.Analytical.Building.Query.PartyWalls(buildingModel_Low, [buildingModel_Shifted])!);
        }

        /// <summary>
        /// Tests the distance and angle limits of <see cref="DiGi.Analytical.Building.Query.PartyWalls"/> just inside and just outside (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// <para>With the default 0.2 m a gap of 0.19 m still makes a party wall and a gap of 0.21 m does not. A neighbour wall at about 2.9 degrees to the wall is parallel enough, one at about 8.5 degrees is not, although both lie within 0.2 m of the plane.</para>
        /// </summary>
        [Fact]
        public void Query_PartyWalls_Tolerance()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(1, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));

            BuildingModel buildingModel_Inside = PartyWalls_BuildingModel(1, new Point2D(10.19, 0), new Point2D(20, 0), new Point2D(20, 10), new Point2D(10.19, 10));
            BuildingModel buildingModel_Outside = PartyWalls_BuildingModel(1, new Point2D(10.21, 0), new Point2D(20, 0), new Point2D(20, 10), new Point2D(10.21, 10));
            Assert.Equal(30.0, PartyWalls_Area(DiGi.Analytical.Building.Query.PartyWalls(buildingModel, [buildingModel_Inside])), 6);
            Assert.Empty(DiGi.Analytical.Building.Query.PartyWalls(buildingModel, [buildingModel_Outside])!);

            BuildingModel buildingModel_Parallel = PartyWalls_BuildingModel(1, new Point2D(10, 0), new Point2D(20, 0), new Point2D(20, 1), new Point2D(10.05, 1));
            BuildingModel buildingModel_Skew = PartyWalls_BuildingModel(1, new Point2D(10, 0), new Point2D(20, 0), new Point2D(20, 1), new Point2D(10.15, 1));
            Assert.True(PartyWalls_Area(DiGi.Analytical.Building.Query.PartyWalls(buildingModel, [buildingModel_Parallel])) > 2.9);
            Assert.Empty(DiGi.Analytical.Building.Query.PartyWalls(buildingModel, [buildingModel_Skew])!);
        }

        /// <summary>
        /// Tests <see cref="DiGi.Analytical.Building.Modify.SplitPartyWalls"/> (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// <para>A wall shared over 18 of its 30 m2 is split in two: the external 12 m2 keeps the identifier of the wall and no party-wall mark, the shared 18 m2 becomes a new wall marked with the neighbour model, both bound the space, and the wall area of the model is unchanged. A wall shared whole is not split; it keeps its identifier and is marked. Null or empty input changes nothing.</para>
        /// </summary>
        [Fact]
        public void Modify_SplitPartyWalls()
        {
            static double Area(DiGi.Analytical.Building.Interfaces.IWall wall)
            {
                return DiGi.Analytical.Building.Query.Surface3D(wall) is Geometry.Spatial.Interfaces.IPolygonalFace3D polygonalFace3D ? polygonalFace3D.GetArea() : double.NaN;
            }

            static System.Guid? PartyWall(DiGi.Analytical.Building.Interfaces.IWall wall)
            {
                return wall is DiGi.Analytical.Building.Classes.Component component && component.TryGetValue(new Core.Parameter.Classes.EnumParameterDefinition(DiGi.Analytical.Building.Enums.ComponentParameter.PartyWall), out System.Guid guid) ? guid : null;
            }

            static double WallArea(BuildingModel buildingModel_Temp)
            {
                double result = 0;
                foreach (DiGi.Analytical.Building.Interfaces.IWall wall in buildingModel_Temp.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!)
                {
                    result += Area(wall);
                }

                return result;
            }

            BuildingModel buildingModel = PartyWalls_BuildingModel(1, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            BuildingModel buildingModel_Shifted = PartyWalls_BuildingModel(1, new Point2D(10, 4), new Point2D(20, 4), new Point2D(20, 14), new Point2D(10, 14));

            List<PartyWallResult>? partyWallResults = DiGi.Analytical.Building.Query.PartyWalls(buildingModel, [buildingModel_Shifted]);
            PartyWallResult partyWallResult = Assert.Single(partyWallResults!);
            System.Guid guid_Wall = partyWallResult.Component!.Guid;

            int count_Before = buildingModel.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!.Count;
            double area_Before = WallArea(buildingModel);

            Assert.True(DiGi.Analytical.Building.Modify.SplitPartyWalls(buildingModel, partyWallResults));

            List<DiGi.Analytical.Building.Interfaces.IWall> walls = buildingModel.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!;
            Assert.Equal(count_Before + 1, walls.Count);
            Assert.Equal(area_Before, WallArea(buildingModel), 6);

            DiGi.Analytical.Building.Interfaces.IWall wall_External = walls.Find(x => x.Guid == guid_Wall)!;
            Assert.NotNull(wall_External);
            Assert.Equal(12.0, Area(wall_External), 6);
            Assert.Null(PartyWall(wall_External));

            List<DiGi.Analytical.Building.Interfaces.IWall> walls_Party = walls.FindAll(x => PartyWall(x) is not null);
            DiGi.Analytical.Building.Interfaces.IWall wall_Party = Assert.Single(walls_Party);
            Assert.NotEqual(guid_Wall, wall_Party.Guid);
            Assert.Equal(18.0, Area(wall_Party), 6);
            Assert.Equal(buildingModel_Shifted.Guid, PartyWall(wall_Party));

            Assert.Single(buildingModel.GetSpaces(wall_External)!);
            Assert.Single(buildingModel.GetSpaces(wall_Party)!);

            // A wall shared whole keeps its identifier and is marked.
            BuildingModel buildingModel_A = PartyWalls_BuildingModel(1, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            BuildingModel buildingModel_B = PartyWalls_BuildingModel(1, new Point2D(10, 0), new Point2D(20, 0), new Point2D(20, 10), new Point2D(10, 10));
            List<PartyWallResult>? partyWallResults_Whole = DiGi.Analytical.Building.Query.PartyWalls(buildingModel_A, [buildingModel_B]);
            System.Guid guid_Whole = Assert.Single(partyWallResults_Whole!).Component!.Guid;
            int count_Whole = buildingModel_A.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!.Count;

            Assert.True(DiGi.Analytical.Building.Modify.SplitPartyWalls(buildingModel_A, partyWallResults_Whole));
            List<DiGi.Analytical.Building.Interfaces.IWall> walls_Whole = buildingModel_A.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!;
            Assert.Equal(count_Whole, walls_Whole.Count);
            Assert.Equal(buildingModel_B.Guid, PartyWall(walls_Whole.Find(x => x.Guid == guid_Whole)!));

            Assert.False(DiGi.Analytical.Building.Modify.SplitPartyWalls(null, partyWallResults_Whole));
            Assert.False(DiGi.Analytical.Building.Modify.SplitPartyWalls(buildingModel_A, []));
        }

        /// <summary>
        /// Tests that rebuilding the shells of an extruded model replaces its components instead of adding to them (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// <para>An extruded model bounds its spaces with <see cref="CurveWall"/>s, which <see cref="BuildingModelShellsUpdater"/> rebuilds as <see cref="SurfaceWall"/>s under the same identifiers. The model keys components by type and identifier, so the curve walls used to stay beside their rebuilt copies: a 6 m box split at 3 m held 12 walls instead of 8.</para>
        /// </summary>
        [Fact]
        public void BuildingModelShellsUpdater_ReplacesRebuiltType()
        {
            BuildingModel buildingModel = PartyWalls_BuildingModel(1, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            Assert.Contains(buildingModel.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!, x => x is CurveWall);

            PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(Geometry.Spatial.Create.Plane(0.0)!, [new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);
            BuildingModel? buildingModel_High = Create.BuildingModel(polygonalFace3D, 1, 6.0);
            Assert.NotNull(buildingModel_High);
            Assert.Equal(4, buildingModel_High.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!.Count);

            Assert.True(DiGi.Analytical.Building.Modify.TrySplit(buildingModel_High, 3.0));

            List<DiGi.Analytical.Building.Interfaces.IWall> walls = buildingModel_High.GetComponents<DiGi.Analytical.Building.Interfaces.IWall>()!;
            Assert.Equal(8, walls.Count);
            Assert.DoesNotContain(walls, x => x is CurveWall);
        }

        /// <summary>
        /// Tests the serialization of <see cref="PartyWallResult"/>: a JSON round trip, a copy and <c>SerializationCheck</c>, with every member populated (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// </summary>
        [Fact]
        public void PartyWallResult()
        {
            BuildingModel buildingModel_A = PartyWalls_BuildingModel(1, new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));
            BuildingModel buildingModel_B = PartyWalls_BuildingModel(1, new Point2D(10, 0), new Point2D(20, 0), new Point2D(20, 10), new Point2D(10, 10));

            List<PartyWallResult>? partyWallResults = DiGi.Analytical.Building.Query.PartyWalls(buildingModel_A, [buildingModel_B]);
            Assert.NotNull(partyWallResults);
            PartyWallResult partyWallResult = Assert.Single(partyWallResults);
            Assert.NotNull(partyWallResult.Component);
            Assert.NotNull(partyWallResult.NeighbourBuildingModel);
            Assert.NotNull(partyWallResult.NeighbourComponent);
            Assert.Single(partyWallResult.PolygonalFace3Ds!);

            string? json = Core.Convert.ToSystem_String(partyWallResult);
            PartyWallResult? partyWallResult_Json = Core.Convert.ToDiGi<PartyWallResult>(json)?.Find(x => x is not null);
            Assert.NotNull(partyWallResult_Json);
            Assert.Equal(partyWallResult.Area, partyWallResult_Json.Area, 9);
            Assert.Equal(partyWallResult.NeighbourComponent!.Guid, partyWallResult_Json.NeighbourComponent!.Guid);
            Assert.Equal(30.0, partyWallResult_Json.PolygonalFace3Ds![0].GetArea(), 6);

            PartyWallResult partyWallResult_Copy = new(partyWallResult);
            Assert.Equal(json, Core.Convert.ToSystem_String(partyWallResult_Copy));

            Core.xUnit.Query.SerializationCheck(partyWallResult);
        }
    }
}
