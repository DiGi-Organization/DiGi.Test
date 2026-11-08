using DiGi.Geometry.Planar;
using DiGi.Geometry.Planar.Classes;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the difference operation between two <see cref="PolygonalFace2D"/> objects to verify that the resulting geometry and its area are calculated correctly.
        /// </summary>
        [Fact]
        public void Difference()
        {
            PolygonalFace2D? polygonalFace2D_1 = DiGi.Core.Convert.ToDiGi<PolygonalFace2D>("{\"_type\":\"DiGi.Geometry.Planar.Classes.PolygonalFace2D,DiGi.Geometry\",\"ExternalEdge\":{\"_type\":\"DiGi.Geometry.Planar.Classes.Rectangle2D,DiGi.Geometry\",\"Height\":10.753366914694197,\"HeightDirection\":{\"_type\":\"DiGi.Geometry.Planar.Classes.Vector2D,DiGi.Geometry\",\"X\":0.09950371901616256,\"Y\":0.9950371902104728},\"Origin\":{\"_type\":\"DiGi.Geometry.Planar.Classes.Point2D,DiGi.Geometry\",\"X\":621017.1408910891,\"Y\":520324.1189108909},\"Width\":13.958381704287603},\"InternalEdges\":null}")?.FirstOrDefault();
            Assert.NotNull(polygonalFace2D_1);

            PolygonalFace2D? polygonalFace2D_2 = DiGi.Core.Convert.ToDiGi<PolygonalFace2D>("{\"_type\":\"DiGi.Geometry.Planar.Classes.PolygonalFace2D,DiGi.Geometry\",\"ExternalEdge\":{\"_type\":\"DiGi.Geometry.Planar.Classes.Polygon2D,DiGi.Geometry\",\"Points\":[{\"_type\":\"DiGi.Geometry.Planar.Classes.Point2D,DiGi.Geometry\",\"X\":621018.21,\"Y\":520334.81},{\"_type\":\"DiGi.Geometry.Planar.Classes.Point2D,DiGi.Geometry\",\"X\":621032.1,\"Y\":520333.43},{\"_type\":\"DiGi.Geometry.Planar.Classes.Point2D,DiGi.Geometry\",\"X\":621031.03,\"Y\":520322.73},{\"_type\":\"DiGi.Geometry.Planar.Classes.Point2D,DiGi.Geometry\",\"X\":621026.16,\"Y\":520323.22},{\"_type\":\"DiGi.Geometry.Planar.Classes.Point2D,DiGi.Geometry\",\"X\":621026.4,\"Y\":520325.61},{\"_type\":\"DiGi.Geometry.Planar.Classes.Point2D,DiGi.Geometry\",\"X\":621017.38,\"Y\":520326.51},{\"_type\":\"DiGi.Geometry.Planar.Classes.Point2D,DiGi.Geometry\",\"X\":621018.21,\"Y\":520334.81}]},\"InternalEdges\":null}")?.FirstOrDefault();
            Assert.NotNull(polygonalFace2D_2);

            List<PolygonalFace2D>? polygonalFace2Ds = Query.Difference(polygonalFace2D_1, polygonalFace2D_2);
            Assert.NotNull(polygonalFace2Ds);
            Assert.True(polygonalFace2Ds.Count == 1);

            PolygonalFace2D? polygonalFace2D = polygonalFace2Ds.FirstOrDefault();
            Assert.NotNull(polygonalFace2D);

            Assert.True(DiGi.Core.Query.AlmostEquals(polygonalFace2D_1.GetArea() - polygonalFace2D_2.GetArea(), polygonalFace2D.GetArea(), DiGi.Core.Constants.Tolerance.MacroDistance));
        }

        /// <summary>
        /// Tests that subtracting a disjoint geometry from another returns the original geometry itself.
        /// </summary>
        [Fact]
        public void Difference_Disjoint()
        {
            Polygon2D p1 = new([new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)]);
            Polygon2D p2 = new([new Point2D(5, 5), new Point2D(7, 5), new Point2D(7, 7), new Point2D(5, 7)]);

            // Test 1: IPolygonal2D Difference
            var result = Query.Difference(p1, p2);
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(4.0, result[0].GetArea(), 4);

            // Test 2: PolygonalFace2D Difference
            var face1 = p1.ToNTS_Polygon().ToDiGi();
            var face2 = p2.ToNTS_Polygon().ToDiGi();
            var faceResult = Query.Difference(face1, face2);
            Assert.NotNull(faceResult);
            Assert.Single(faceResult);
            Assert.Equal(4.0, faceResult[0].GetArea(), 4);
        }

        /// <summary>
        /// Tests that subtracting a topologically equal geometry from another returns an empty result.
        /// </summary>
        [Fact]
        public void Difference_TopologicallyEqual()
        {
            Polygon2D p1 = new([new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)]);
            Polygon2D p2 = new([new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)]);

            var result = Query.Difference(p1, p2);
            Assert.NotNull(result);
            Assert.Empty(result);

            var face1 = p1.ToNTS_Polygon().ToDiGi();
            var face2 = p2.ToNTS_Polygon().ToDiGi();
            var faceResult = Query.Difference(face1, face2);
            Assert.NotNull(faceResult);
            Assert.Empty(faceResult);
        }
    }
}