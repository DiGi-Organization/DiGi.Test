using DiGi.Geometry.Planar.Classes;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the overload of <see cref="Planar.Query.Difference(PolygonalFace2D?, IEnumerable{PolygonalFace2D?}?, double)"/> that subtracts several faces at once.
        /// <para>Two overlapping cutters and one far away leave the 10 x 10 square less their union: 100 - 30 - 30 + 10 = 50. A cutter with a hole leaves the hole. With no cutter in reach the face comes back whole, a cutter covering it all leaves nothing, and null inputs give null. The result equals subtracting the cutters one by one.</para>
        /// </summary>
        [Fact]
        public void Difference_PolygonalFace2Ds()
        {
            static PolygonalFace2D Rectangle(double x_Min, double y_Min, double x_Max, double y_Max)
            {
                PolygonalFace2D? polygonalFace2D = Planar.Create.PolygonalFace2D(new Point2D(x_Min, y_Min), new Point2D(x_Max, y_Min), new Point2D(x_Max, y_Max), new Point2D(x_Min, y_Max));
                Assert.NotNull(polygonalFace2D);
                return polygonalFace2D;
            }

            static double Area(List<PolygonalFace2D>? polygonalFace2Ds)
            {
                Assert.NotNull(polygonalFace2Ds);
                double result = 0;
                foreach (PolygonalFace2D polygonalFace2D in polygonalFace2Ds)
                {
                    result += polygonalFace2D.GetArea();
                }

                return result;
            }

            PolygonalFace2D polygonalFace2D = Rectangle(0, 0, 10, 10);
            List<PolygonalFace2D> polygonalFace2Ds_Cutting = [Rectangle(0, 0, 3, 10), Rectangle(2, 0, 5, 10), Rectangle(100, 100, 110, 110)];

            // Overlapping cutters [0, 3] and [2, 5] remove [0, 5] x [0, 10].
            Assert.Equal(50.0, Area(Planar.Query.Difference(polygonalFace2D, polygonalFace2Ds_Cutting)), 6);

            List<PolygonalFace2D> polygonalFace2Ds_OneByOne = [polygonalFace2D];
            foreach (PolygonalFace2D polygonalFace2D_Cutting in polygonalFace2Ds_Cutting)
            {
                List<PolygonalFace2D> polygonalFace2Ds_Next = [];
                foreach (PolygonalFace2D polygonalFace2D_Temp in polygonalFace2Ds_OneByOne)
                {
                    polygonalFace2Ds_Next.AddRange(Planar.Query.Difference(polygonalFace2D_Temp, polygonalFace2D_Cutting)!);
                }

                polygonalFace2Ds_OneByOne = polygonalFace2Ds_Next;
            }

            Assert.Equal(Area(polygonalFace2Ds_OneByOne), Area(Planar.Query.Difference(polygonalFace2D, polygonalFace2Ds_Cutting)), 6);

            // A cutter with a hole leaves the hole.
            PolygonalFace2D? polygonalFace2D_Holed = Planar.Create.PolygonalFace2D(Rectangle(0, 0, 10, 10).ExternalEdge, [Rectangle(4, 4, 6, 6).ExternalEdge!]);
            Assert.NotNull(polygonalFace2D_Holed);
            Assert.Equal(4.0, Area(Planar.Query.Difference(polygonalFace2D, [polygonalFace2D_Holed])), 6);

            Assert.Equal(100.0, Area(Planar.Query.Difference(polygonalFace2D, [Rectangle(100, 100, 110, 110)])), 6);
            Assert.Equal(100.0, Area(Planar.Query.Difference(polygonalFace2D, [])), 6);
            Assert.Empty(Planar.Query.Difference(polygonalFace2D, [Rectangle(-1, -1, 11, 11)])!);
            Assert.Null(Planar.Query.Difference(null, polygonalFace2Ds_Cutting));
            Assert.Null(Planar.Query.Difference(polygonalFace2D, (IEnumerable<PolygonalFace2D?>?)null));
        }
    }
}
