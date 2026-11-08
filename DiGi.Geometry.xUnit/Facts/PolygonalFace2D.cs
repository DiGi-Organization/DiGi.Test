using DiGi.Geometry.Planar;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using System.Diagnostics;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Create.PolygonalFace2D(IPolygonal2D, IEnumerable{IPolygonal2D}, double)"/> keeps internal edges located inside the external edge and discards internal edges located outside of it.
        /// </summary>
        [Fact]
        public void PolygonalFace2D_Create_InternalEdgeFiltering()
        {
            Polygon2D Square(double x, double y, double size)
            {
                return new Polygon2D([new Point2D(x, y), new Point2D(x + size, y), new Point2D(x + size, y + size), new Point2D(x, y + size)]);
            }

            Polygon2D externalEdge = Square(0, 0, 10);
            Polygon2D internalEdge_Inside = Square(2, 2, 1);
            Polygon2D internalEdge_Outside = Square(20, 20, 1);

            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(externalEdge, [internalEdge_Inside, internalEdge_Outside]);
            Assert.NotNull(polygonalFace2D);

            List<IPolygonal2D>? internalEdges = polygonalFace2D.InternalEdges;
            Assert.NotNull(internalEdges);
            Assert.Single(internalEdges);
            Assert.Equal(99.0, polygonalFace2D.GetArea(), 3);
        }

        /// <summary>
        /// Verifies that <see cref="Create.PolygonalFace2D(IPolygonal2D, IEnumerable{IPolygonal2D}, double)"/> removes internal edges that are nested inside or overlapping a larger internal edge, keeping only the largest one of each group.
        /// </summary>
        [Fact]
        public void PolygonalFace2D_Create_NestedInternalEdges()
        {
            Polygon2D Square(double x, double y, double size)
            {
                return new Polygon2D([new Point2D(x, y), new Point2D(x + size, y), new Point2D(x + size, y + size), new Point2D(x, y + size)]);
            }

            Polygon2D externalEdge = Square(0, 0, 20);
            Polygon2D internalEdge_Large = Square(2, 2, 6);
            Polygon2D internalEdge_Nested = Square(3, 3, 1);
            Polygon2D internalEdge_Overlapping = Square(7, 7, 2);
            Polygon2D internalEdge_Separate = Square(12, 12, 2);

            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(externalEdge, [internalEdge_Nested, internalEdge_Separate, internalEdge_Large, internalEdge_Overlapping]);
            Assert.NotNull(polygonalFace2D);

            List<IPolygonal2D>? internalEdges = polygonalFace2D.InternalEdges;
            Assert.NotNull(internalEdges);
            Assert.Equal(2, internalEdges.Count);
            Assert.Equal(400.0 - 36.0 - 4.0, polygonalFace2D.GetArea(), 3);
        }

        /// <summary>
        /// Verifies the tolerance boundary behavior of <see cref="Create.PolygonalFace2D(IPolygonal2D, IEnumerable{IPolygonal2D}, double)"/>: an internal edge farther from the external boundary than the tolerance is kept, while an internal edge closer than the tolerance is discarded.
        /// </summary>
        [Fact]
        public void PolygonalFace2D_Create_ToleranceBoundary()
        {
            double tolerance = 0.01;

            Polygon2D externalEdge = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);

            Polygon2D internalEdge_JustInside = new([new Point2D(0.02, 0.02), new Point2D(1, 0.02), new Point2D(1, 1), new Point2D(0.02, 1)]);
            Polygon2D internalEdge_TooClose = new([new Point2D(0.005, 2), new Point2D(1, 2), new Point2D(1, 3), new Point2D(0.005, 3)]);

            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(externalEdge, [internalEdge_JustInside, internalEdge_TooClose], tolerance);
            Assert.NotNull(polygonalFace2D);

            List<IPolygonal2D>? internalEdges = polygonalFace2D.InternalEdges;
            Assert.NotNull(internalEdges);
            Assert.Single(internalEdges);
        }

        /// <summary>
        /// Verifies the <see cref="Create.PolygonalFace2D(Point2D[])"/> overload: fewer than three points returns null and a valid triangle returns a face with the expected area.
        /// </summary>
        [Fact]
        public void PolygonalFace2D_Create_Points()
        {
            Assert.Null(Create.PolygonalFace2D((Point2D[]?)null));
            Assert.Null(Create.PolygonalFace2D(new Point2D(0, 0), new Point2D(4, 0)));

            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(new Point2D(0, 0), new Point2D(4, 0), new Point2D(0, 3));
            Assert.NotNull(polygonalFace2D);
            Assert.Equal(6.0, polygonalFace2D.GetArea(), 3);
            Assert.Null(polygonalFace2D.InternalEdges);
        }

        /// <summary>
        /// Verifies that <see cref="Create.PolygonalFace2Ds(IEnumerable{Segment2D}, double)"/> polygonizes segments into faces: fewer than three segments returns null, a closed square yields one face, and a square with a nested square yields a face with a hole plus the inner face.
        /// </summary>
        [Fact]
        public void PolygonalFace2Ds_Segment2Ds()
        {
            List<Segment2D> Square(double x, double y, double size)
            {
                return
                [
                    new Segment2D(new Point2D(x, y), new Point2D(x + size, y)),
                    new Segment2D(new Point2D(x + size, y), new Point2D(x + size, y + size)),
                    new Segment2D(new Point2D(x + size, y + size), new Point2D(x, y + size)),
                    new Segment2D(new Point2D(x, y + size), new Point2D(x, y))
                ];
            }

            List<Segment2D> segment2Ds = Square(0, 0, 10);

            Assert.Null(Create.PolygonalFace2Ds(segment2Ds.GetRange(0, 2)));

            List<PolygonalFace2D>? polygonalFace2Ds = Create.PolygonalFace2Ds(segment2Ds);
            Assert.NotNull(polygonalFace2Ds);
            Assert.Single(polygonalFace2Ds);
            Assert.Equal(100.0, polygonalFace2Ds[0].GetArea(), 3);

            segment2Ds.AddRange(Square(3, 3, 4));

            polygonalFace2Ds = Create.PolygonalFace2Ds(segment2Ds);
            Assert.NotNull(polygonalFace2Ds);
            Assert.Equal(2, polygonalFace2Ds.Count);
            Assert.Equal(100.0, polygonalFace2Ds.Sum(x => x.GetArea()), 3);

            PolygonalFace2D? polygonalFace2D_Hole = polygonalFace2Ds.Find(x => x.InternalEdges != null && x.InternalEdges.Count == 1);
            Assert.NotNull(polygonalFace2D_Hole);
            Assert.Equal(84.0, polygonalFace2D_Hole.GetArea(), 3);
        }

        /// <summary>
        /// Verifies that <see cref="Create.PolygonalFace2Ds(IEnumerable{IPolygonal2D}, double)"/> produces the same faces as the segment-based overload fed with the segments of the same polygons.
        /// <para>This pins the equivalence between the direct ring conversion and the historical segment-explosion path for overlapping input polygons.</para>
        /// </summary>
        [Fact]
        public void PolygonalFace2Ds_Polygonal2Ds()
        {
            Assert.Null(Create.PolygonalFace2Ds((IEnumerable<IPolygonal2D>?)null));

            Polygon2D polygon2D_1 = new([new Point2D(0, 0), new Point2D(4, 0), new Point2D(4, 4), new Point2D(0, 4)]);
            Polygon2D polygon2D_2 = new([new Point2D(2, 2), new Point2D(6, 2), new Point2D(6, 6), new Point2D(2, 6)]);

            List<IPolygonal2D> polygonal2Ds = [polygon2D_1, polygon2D_2];

            List<PolygonalFace2D>? polygonalFace2Ds = Create.PolygonalFace2Ds(polygonal2Ds);
            Assert.NotNull(polygonalFace2Ds);

            List<Segment2D>? segment2Ds = Query.Segments(polygonal2Ds);
            Assert.NotNull(segment2Ds);

            List<PolygonalFace2D>? polygonalFace2Ds_Segments = Create.PolygonalFace2Ds(segment2Ds);
            Assert.NotNull(polygonalFace2Ds_Segments);

            Assert.Equal(polygonalFace2Ds_Segments.Count, polygonalFace2Ds.Count);

            List<double> areas = polygonalFace2Ds.ConvertAll(x => x.GetArea());
            List<double> areas_Segments = polygonalFace2Ds_Segments.ConvertAll(x => x.GetArea());
            areas.Sort();
            areas_Segments.Sort();

            for (int i = 0; i < areas.Count; i++)
            {
                Assert.Equal(areas_Segments[i], areas[i], 3);
            }

            Assert.Equal(28.0, areas.Sum(), 3);
        }

        /// <summary>
        /// Verifies the serialization round-trip and clone path of <see cref="PolygonalFace2D"/> built with an external edge and one internal edge.
        /// </summary>
        [Fact]
        public void PolygonalFace2D_SerializationCheck()
        {
            Polygon2D externalEdge = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);
            Polygon2D internalEdge = new([new Point2D(2, 2), new Point2D(4, 2), new Point2D(4, 4), new Point2D(2, 4)]);

            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(externalEdge, [internalEdge]);
            Assert.NotNull(polygonalFace2D);

            Assert.NotNull(polygonalFace2D.ExternalEdge);
            Assert.NotNull(polygonalFace2D.InternalEdges);
            Assert.Single(polygonalFace2D.InternalEdges);
            Assert.Equal(96.0, polygonalFace2D.GetArea(), 3);

            DiGi.Core.xUnit.Query.SerializationCheck(polygonalFace2D);
        }

        /// <summary>
        /// Verifies both the correctness and the performance of <see cref="Create.PolygonalFace2D(IPolygonal2D, IEnumerable{IPolygonal2D}, double)"/> and <see cref="Create.PolygonalFace2Ds(IEnumerable{IPolygonal2D}, double)"/> on a large number of internal edge candidates.
        /// <para>Covers the bounding box prefilter for the inside check, the descending area sort, and the pairwise containment deduplication.</para>
        /// </summary>
        [Fact]
        public void PolygonalFace2D_Create_Performance()
        {
            Polygon2D Square(double x, double y, double size)
            {
                return new Polygon2D([new Point2D(x, y), new Point2D(x + size, y), new Point2D(x + size, y + size), new Point2D(x, y + size)]);
            }

            Polygon2D externalEdge = new([new Point2D(0, 0), new Point2D(1000, 0), new Point2D(1000, 1000), new Point2D(0, 1000)]);

            // 400 disjoint holes inside, each with a nested duplicate to deduplicate, plus 400 holes outside
            List<IPolygonal2D> internalEdges = [];
            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    double x = 10 + (i * 45);
                    double y = 10 + (j * 45);

                    internalEdges.Add(Square(x, y, 10));
                    internalEdges.Add(Square(x + 2, y + 2, 2));
                    internalEdges.Add(Square(x + 2000, y + 2000, 10));
                }
            }

            // Warm up / JIT compile
            _ = Create.PolygonalFace2D(Square(0, 0, 10), [Square(2, 2, 1)]);

            Stopwatch stopwatch = Stopwatch.StartNew();
            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(externalEdge, internalEdges);
            stopwatch.Stop();

            Assert.NotNull(polygonalFace2D);

            List<IPolygonal2D>? internalEdges_Result = polygonalFace2D.InternalEdges;
            Assert.NotNull(internalEdges_Result);
            Assert.Equal(400, internalEdges_Result.Count);
            Assert.Equal(1000000.0 - (400 * 100.0), polygonalFace2D.GetArea(), 3);

            Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"PolygonalFace2D creator performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");

            // 400 disjoint squares converted to faces through the NTS polygonization path
            List<IPolygonal2D> polygonal2Ds = [];
            for (int i = 0; i < 20; i++)
            {
                for (int j = 0; j < 20; j++)
                {
                    polygonal2Ds.Add(Square(i * 20, j * 20, 10));
                }
            }

            stopwatch.Restart();
            List<PolygonalFace2D>? polygonalFace2Ds = Create.PolygonalFace2Ds(polygonal2Ds);
            stopwatch.Stop();

            Assert.NotNull(polygonalFace2Ds);
            Assert.Equal(400, polygonalFace2Ds.Count);
            Assert.Equal(400 * 100.0, polygonalFace2Ds.Sum(x => x.GetArea()), 3);

            Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"PolygonalFace2Ds creator performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies that <see cref="PolygonalFace2D.InRange(Point2D, double)"/> reports a point on the external ring as in range when the face has a hole - the hole loop used to overwrite the external-ring answer, so such a point was out of range only on faces with holes (DiGi-Organization/DiGi.Geometry#5).
        /// </summary>
        [Fact]
        public void PolygonalFace2D_InRange_OnOutlineWithHole()
        {
            double tolerance = 0.01;

            Polygon2D externalEdge = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);
            Polygon2D internalEdge = new([new Point2D(4, 4), new Point2D(6, 4), new Point2D(6, 6), new Point2D(4, 6)]);

            PolygonalFace2D? polygonalFace2D_Hole = Create.PolygonalFace2D(externalEdge, [internalEdge]);
            PolygonalFace2D? polygonalFace2D_Solid = Create.PolygonalFace2D(externalEdge);
            Assert.NotNull(polygonalFace2D_Hole);
            Assert.NotNull(polygonalFace2D_Solid);

            Point2D point2D_OnOutline = new(5, 0.005);
            Point2D point2D_OnRim = new(4.005, 5);
            Point2D point2D_InHole = new(5, 5);

            Assert.True(polygonalFace2D_Solid.InRange(point2D_OnOutline, tolerance));
            Assert.True(polygonalFace2D_Hole.InRange(point2D_OnOutline, tolerance));
            Assert.True(polygonalFace2D_Hole.InRange(point2D_OnRim, tolerance));
            Assert.False(polygonalFace2D_Hole.InRange(point2D_InHole, tolerance));
            Assert.False(polygonalFace2D_Hole.Inside(point2D_OnOutline, tolerance));
        }
    }
}