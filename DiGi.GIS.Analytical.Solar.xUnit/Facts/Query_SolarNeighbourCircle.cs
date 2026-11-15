using DiGi.Analytical.Building.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Solar.Classes;
using System;
using System.Collections.Generic;

namespace DiGi.GIS.Analytical.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the neighbour circle of a solve, <see cref="Query.SolarNeighbourCircle"/>: centred on the centre of the building's bounding box and reaching the radius beyond its farthest corner, so a 10 m box at 50 m reaches 50 + √50 m; radius 0 is the box's own bounding circle, and a missing model answers null (DiGi-Organization/DiGi.GIS.PostgreSQL#136).
        /// </summary>
        [Fact]
        public void Query_SolarNeighbourCircle()
        {
            BuildingModel buildingModel = SolarFixture_Box();

            Circle2D? circle2D = buildingModel.SolarNeighbourCircle(50);
            Assert.NotNull(circle2D);
            Assert.NotNull(circle2D.Center);
            Assert.Equal(SolarFixture_Origin.X + 5, circle2D.Center.X, 6);
            Assert.Equal(SolarFixture_Origin.Y + 5, circle2D.Center.Y, 6);
            Assert.Equal(50 + Math.Sqrt(50), circle2D.Radius, 6);

            Assert.Equal(Math.Sqrt(50), buildingModel.SolarNeighbourCircle(Constants.Default.SolarRadiusIsolated)!.Radius, 6);
            Assert.Null(((BuildingModel?)null).SolarNeighbourCircle(50));
        }

        /// <summary>
        /// Verifies the caster count a solve's ceiling is checked against, <see cref="Query.CasterTriangleCount"/>: the isolated box's only shading-only component is its floor (two triangles) - its walls and roof receive - and a neighbour adds its own triangulated faces; a missing model counts 0.
        /// </summary>
        [Fact]
        public void Query_CasterTriangleCount()
        {
            (ShadingModel shadingModel_Isolated, Dictionary<string, Vector3D> _) = SolarFixture_IsolatedShadingModel(SolarFixture_Box());
            Assert.Equal(2, shadingModel_Isolated.CasterTriangleCount());

            BuildingModel buildingModel = SolarFixture_Box();
            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            BuildingModel buildingModel_Neighbour = SolarFixture_Prism([new Point2D(20, 0), new Point2D(30, 0), new Point2D(30, 10), new Point2D(20, 10)], 10, new Point2D(25, 5));
            ShadingModel? shadingModel = DiGi.Analytical.Building.Solar.Convert.ToSolar(buildingModel, [buildingModel_Neighbour], x => new Core.Classes.GuidReference(x).ToString() is string reference && normals.ContainsKey(reference));
            Assert.NotNull(shadingModel);

            // The neighbour's four walls, roof and floor: six rectangles, twelve triangles, plus the box's floor.
            Assert.Equal(14, shadingModel.CasterTriangleCount());
            Assert.Equal(0, ((ShadingModel?)null).CasterTriangleCount());
        }
    }
}
