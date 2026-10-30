using DiGi.Geometry.Planar.Classes;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the <see cref="Planar.Query.InRange(Planar.Interfaces.IPolygonalFace2D?, Planar.Interfaces.IPolygonalFace2D?, double)"/> overload for polygonal faces.
        /// <para>Touching and overlapping faces are in range, a gap of 0.19 m is within 0.2 m and one of 0.21 m is not, and a face lying inside another is in range although their outlines are 2 m apart - the case a distance between outlines misses. Null is never in range.</para>
        /// </summary>
        [Fact]
        public void InRange_PolygonalFace2D()
        {
            static PolygonalFace2D Rectangle(double x_Min, double y_Min, double x_Max, double y_Max)
            {
                PolygonalFace2D? polygonalFace2D = Planar.Create.PolygonalFace2D(new Point2D(x_Min, y_Min), new Point2D(x_Max, y_Min), new Point2D(x_Max, y_Max), new Point2D(x_Min, y_Max));
                Assert.NotNull(polygonalFace2D);
                return polygonalFace2D;
            }

            PolygonalFace2D polygonalFace2D = Rectangle(0, 0, 10, 10);

            Assert.True(Planar.Query.InRange(polygonalFace2D, Rectangle(10, 0, 20, 10), 0.2));
            Assert.True(Planar.Query.InRange(polygonalFace2D, Rectangle(5, 5, 15, 15), 0.2));
            Assert.True(Planar.Query.InRange(polygonalFace2D, Rectangle(10.19, 0, 20, 10), 0.2));
            Assert.False(Planar.Query.InRange(polygonalFace2D, Rectangle(10.21, 0, 20, 10), 0.2));
            Assert.True(Planar.Query.InRange(polygonalFace2D, Rectangle(2, 2, 8, 8), 0.2));
            Assert.False(Planar.Query.InRange(polygonalFace2D, Rectangle(100, 100, 110, 110), 0.2));
            Assert.False(Planar.Query.InRange(null, polygonalFace2D, 0.2));
        }
    }
}
