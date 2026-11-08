using DiGi.Geometry.Planar;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Reflection;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the union operation of two <see cref="Triangle2D"/> objects sharing a common edge results in a single <see cref="Polygon2D"/> with an area equal to the sum of the individual triangle areas.
        /// </summary>
        [Fact]
        public void Union()
        {
            Triangle2D triangle2D_1 = new((0, 0), (10, 0), (0, 10));
            Triangle2D triangle2D_2 = new((10, 10), (10, 0), (0, 10));

            List<Polygon2D>? polygon2Ds = Query.Union(triangle2D_1, triangle2D_2);
            Assert.NotNull(polygon2Ds);
            Assert.True(polygon2Ds.Count == 1);

            Assert.True(DiGi.Core.Query.AlmostEquals(polygon2Ds[0].GetArea(), triangle2D_1.GetArea() + triangle2D_2.GetArea(), DiGi.Core.Constants.Tolerance.MacroDistance));
        }

        /// <summary>
        /// Tests that unioning two disjoint geometries returns both geometries in the result list.
        /// </summary>
        [Fact]
        public void Union_Disjoint()
        {
            Polygon2D p1 = new([new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)]);
            Polygon2D p2 = new([new Point2D(5, 5), new Point2D(7, 5), new Point2D(7, 7), new Point2D(5, 7)]);

            // Test 1: Polygon2D Union
            var result = Query.Union(p1, p2);
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal(8.0, result.Sum(x => x.GetArea()), 4);

            // Test 2: PolygonalFace2D Union
            var face1 = p1.ToNTS_Polygon().ToDiGi();
            var face2 = p2.ToNTS_Polygon().ToDiGi();
            var faceResult = Query.Union(face1, face2);
            Assert.NotNull(faceResult);
            Assert.Equal(2, faceResult.Count);
            Assert.Equal(8.0, faceResult.Sum(x => x.GetArea()), 4);
        }

        /// <summary>
        /// Tests that unioning geometries with some invalid ones resolves correctly.
        /// </summary>
        [Fact]
        public void Union_InvalidPolygons()
        {
            // Create a self-intersecting polygon (invalid)
            Polygon2D invalidPoly = new([
                new Point2D(0, 0),
                new Point2D(2, 0),
                new Point2D(0, 2),
                new Point2D(2, 2)
            ]);

            Polygon2D poly2 = new([
                new Point2D(1, 0),
                new Point2D(3, 0),
                new Point2D(3, 2),
                new Point2D(1, 2)
            ]);

            // Just calling Union should handle it via individual geometry fixes and complete successfully
            var result = Query.Union<IPolygonal2D>([invalidPoly, poly2]);
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        /// <summary>
        /// Tests that passing empty or single collections returns empty or single result.
        /// </summary>
        [Fact]
        public void Union_EmptyAndSingle()
        {
            List<Polygon2D> emptyList = [];
            var resultEmpty = Query.Union(emptyList);
            Assert.NotNull(resultEmpty);
            Assert.Empty(resultEmpty);

            Polygon2D poly = new([new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)]);
            var resultSingle = Query.Union([poly]);
            Assert.NotNull(resultSingle);
            Assert.Single(resultSingle);
            Assert.Equal(4.0, resultSingle[0].GetArea(), 4);
        }

        /// <summary>
        /// Tests unioning a larger set of overlapping polygons (cascaded union).
        /// </summary>
        [Fact]
        public void Union_CascadedOverlap()
        {
            Polygon2D p1 = new([new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)]);
            Polygon2D p2 = new([new Point2D(1, 0), new Point2D(3, 0), new Point2D(3, 2), new Point2D(1, 2)]);
            Polygon2D p3 = new([new Point2D(2, 0), new Point2D(4, 0), new Point2D(4, 2), new Point2D(2, 2)]);
            Polygon2D p4 = new([new Point2D(3, 0), new Point2D(5, 0), new Point2D(5, 2), new Point2D(3, 2)]);

            var result = Query.Union([p1, p2, p3, p4]);
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(10.0, result[0].GetArea(), 4);
        }

        /// <summary>
        /// Tests that unioning two topologically equal geometries returns a single geometry.
        /// </summary>
        [Fact]
        public void Union_TopologicallyEqual()
        {
            Polygon2D p1 = new([new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)]);
            Polygon2D p2 = new([new Point2D(0, 0), new Point2D(2, 0), new Point2D(2, 2), new Point2D(0, 2)]);

            var result = Query.Union(p1, p2);
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(4.0, result[0].GetArea(), 4);
        }

        /// <summary>
        /// Documents the hole-handling fork that any change to the shading union must account for.
        /// <para>A square ring (outer 6x6 with a central 2x2 void) is assembled from four rectangles. The <see cref="Polygon2D"/> union keeps external edges only, so the void is filled (area 36); the <see cref="PolygonalFace2D"/> union preserves the void (area 32).</para>
        /// <para>The shading solver currently uses the external-edge path, so it over-counts shaded area for ring-shaped shadows. This test pins both behaviours so a switch between them is a deliberate, visible change.</para>
        /// </summary>
        [Fact]
        public void Union_HoleHandling_Fork()
        {
            Polygon2D polygon2D_Bottom = new([new Point2D(0, 0), new Point2D(6, 0), new Point2D(6, 2), new Point2D(0, 2)]);
            Polygon2D polygon2D_Top = new([new Point2D(0, 4), new Point2D(6, 4), new Point2D(6, 6), new Point2D(0, 6)]);
            Polygon2D polygon2D_Left = new([new Point2D(0, 2), new Point2D(2, 2), new Point2D(2, 4), new Point2D(0, 4)]);
            Polygon2D polygon2D_Right = new([new Point2D(4, 2), new Point2D(6, 2), new Point2D(6, 4), new Point2D(4, 4)]);

            List<Polygon2D> polygon2Ds = [polygon2D_Bottom, polygon2D_Top, polygon2D_Left, polygon2D_Right];

            // Polygon2D union keeps external edges only -> the central void is filled.
            List<Polygon2D>? polygon2Ds_Union = Query.Union(polygon2Ds);
            Assert.NotNull(polygon2Ds_Union);
            Assert.Equal(36.0, polygon2Ds_Union.Sum(x => x.GetArea()), 3);

            // PolygonalFace2D union preserves the void.
            List<IPolygonalFace2D> polygonalFace2Ds = [];
            foreach (Polygon2D polygon2D in polygon2Ds)
            {
                PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(polygon2D);
                Assert.NotNull(polygonalFace2D);
                polygonalFace2Ds.Add(polygonalFace2D);
            }

            List<PolygonalFace2D>? polygonalFace2Ds_Union = Query.Union(polygonalFace2Ds);
            Assert.NotNull(polygonalFace2Ds_Union);
            Assert.Equal(32.0, polygonalFace2Ds_Union.Sum(x => x.GetArea()), 3);
        }

        /// <summary>
        /// Pins the area-conservation property the shading post-processing relies on: rebuilding <see cref="PolygonalFace2D"/> faces from a union result via <see cref="Create.PolygonalFace2Ds(IEnumerable{IPolygonal2D}, double)"/> must not change the total area.
        /// <para>The shading solver runs this re-polygonization pass after the union. This test is the oracle for any change that collapses the union and face-creation into a single pass.</para>
        /// </summary>
        [Fact]
        public void Union_FaceCreation_PreservesArea()
        {
            Triangle2D triangle2D_1 = new((0, 0), (4, 0), (0, 4));
            Triangle2D triangle2D_2 = new((4, 4), (4, 0), (0, 4));
            Triangle2D triangle2D_3 = new((1, 1), (3, 1), (1, 3));

            List<Triangle2D> triangle2Ds = [triangle2D_1, triangle2D_2, triangle2D_3];

            List<Polygon2D>? polygon2Ds_Union = Query.Union(triangle2Ds);
            Assert.NotNull(polygon2Ds_Union);
            double area_Union = polygon2Ds_Union.Sum(x => x.GetArea());

            // Triangles 1 and 2 tile a 4x4 square; triangle 3 lies inside it.
            Assert.Equal(16.0, area_Union, 3);

            List<PolygonalFace2D>? polygonalFace2Ds = Create.PolygonalFace2Ds(polygon2Ds_Union.ConvertAll(x => (IPolygonal2D)x), DiGi.Core.Constants.Tolerance.Distance);
            Assert.NotNull(polygonalFace2Ds);
            double area_Faces = polygonalFace2Ds.Sum(x => x.GetArea());

            Assert.Equal(area_Union, area_Faces, 3);
        }

        /// <summary>
        /// Verifies that <see cref="Query.Union(IEnumerable{IPolygonalFace2D}?, double)"/> recovers with its snap-rounding fallback when NetTopologySuite throws a <c>TopologyException</c>, instead of returning <c>null</c>.
        /// <para>The fixture is the input reported in DiGi-Organization/DiGi.Geometry#8: the 190 shadow polygons (as WKT) of the east wall (plane origin (64, 18, 0)) of the 12 x 12 benchmark building grid at 2026-06-26 04:00, whose union fails in <c>UnaryUnionOp</c> with a robustness error and succeeds at 112.402 m^2 under snap-rounding.</para>
        /// </summary>
        [Fact]
        public void Union_TopologyException()
        {
            string? path = DiGi.Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "Union_TopologyException.wkt");
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(System.IO.File.Exists(path));

            WKTReader reader = new();
            List<PolygonalFace2D> polygonalFace2Ds = [];
            foreach (string line in System.IO.File.ReadAllLines(path!))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                Polygon? polygon = reader.Read(line) as Polygon;
                Assert.NotNull(polygon);

                PolygonalFace2D? polygonalFace2D = polygon.ToDiGi();
                Assert.NotNull(polygonalFace2D);
                polygonalFace2Ds.Add(polygonalFace2D);
            }

            Assert.Equal(190, polygonalFace2Ds.Count);

            List<PolygonalFace2D>? polygonalFace2Ds_Union = Query.Union(polygonalFace2Ds);
            Assert.NotNull(polygonalFace2Ds_Union);
            Assert.Equal(112.402, polygonalFace2Ds_Union.Sum(x => x.GetArea()), 3);
        }
    }
}