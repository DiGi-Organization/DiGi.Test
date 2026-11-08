using DiGi.Geometry.Planar;
using DiGi.Geometry.Planar.Classes;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.On(IReadOnlyList{Point2D}, Point2D, bool, double)"/> answers exactly what the segment path answers over <see cref="Create.Segment2Ds(IEnumerable{Point2D}, bool)"/> of the same ring - inside, outside, on an edge within and beyond the tolerance, on a vertex and on the closing edge - so that the allocation-free path of <see cref="Polygon2D"/>'s inherited <see cref="Segmentable2D.On(Point2D, double)"/> is a pure speed-up (DiGi-Organization/DiGi.Geometry#5).
        /// </summary>
        [Fact]
        public void On_Points_MatchesSegmentPath()
        {
            double tolerance = 0.01;

            // An L-shaped ring: the closing edge (0,6)->(0,0) is only present when closed is true.
            List<Point2D> ring = [new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 4), new Point2D(4, 4), new Point2D(4, 6), new Point2D(0, 6)];
            Polygon2D polygon2D = new(ring);
            Polyline2D polyline2D = new(ring);

            List<Point2D> probes =
            [
                new Point2D(2, 2),                 // inside
                new Point2D(20, 20),               // far outside
                new Point2D(5, 0.005),             // on the bottom edge, within tolerance
                new Point2D(5, 0.02),              // beside the bottom edge, beyond tolerance
                new Point2D(10, 4),                // on a vertex
                new Point2D(4, 5),                 // on the vertical inner edge
                new Point2D(0, 3),                 // on the closing edge (0,6)->(0,0)
                new Point2D(-0.005, 3),            // beside the closing edge, within tolerance
                new Point2D(7, 4.005),             // on the (10,4)->(4,4) edge, within tolerance
                new Point2D(7, 5),                 // in the notch, outside
            ];

            List<Segment2D>? segment2Ds_Closed = Create.Segment2Ds(ring, true);
            List<Segment2D>? segment2Ds_Open = Create.Segment2Ds(ring, false);
            Assert.NotNull(segment2Ds_Closed);
            Assert.NotNull(segment2Ds_Open);

            foreach (Point2D probe in probes)
            {
                bool expected_Closed = segment2Ds_Closed.On(probe, tolerance);
                bool expected_Open = segment2Ds_Open.On(probe, tolerance);

                Assert.Equal(expected_Closed, ring.On(probe, true, tolerance));
                Assert.Equal(expected_Open, ring.On(probe, false, tolerance));
                Assert.Equal(expected_Closed, polygon2D.On(probe, tolerance));
                Assert.Equal(expected_Open, polyline2D.On(probe, tolerance));
            }

            // The closing edge is the one that tells the two apart.
            Assert.True(ring.On(new Point2D(0, 3), true, tolerance));
            Assert.False(ring.On(new Point2D(0, 3), false, tolerance));
        }

        /// <summary>
        /// Verifies the strict-inside semantics of <see cref="Polygon2D.Inside(Point2D, double)"/> and <see cref="PolygonalFace2D.Inside(Point2D, double)"/> after the allocation-free boundary check: a point within the tolerance of the boundary is not inside, a point just beyond it is, a hole excludes its interior and its rim.
        /// </summary>
        [Fact]
        public void Inside_BoundaryWithinToleranceIsNotInside()
        {
            double tolerance = 0.01;

            Polygon2D externalEdge = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);
            Polygon2D internalEdge = new([new Point2D(4, 4), new Point2D(6, 4), new Point2D(6, 6), new Point2D(4, 6)]);
            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(externalEdge, [internalEdge]);
            Assert.NotNull(polygonalFace2D);

            Assert.True(externalEdge.Inside(new Point2D(5, 1), tolerance));
            Assert.False(externalEdge.Inside(new Point2D(5, 0.005), tolerance));
            Assert.True(externalEdge.Inside(new Point2D(5, 0.02), tolerance));
            Assert.False(externalEdge.Inside(new Point2D(10, 10), tolerance));
            Assert.False(externalEdge.Inside(new Point2D(11, 5), tolerance));

            Assert.True(polygonalFace2D.Inside(new Point2D(2, 2), tolerance));
            Assert.False(polygonalFace2D.Inside(new Point2D(5, 5), tolerance));
            Assert.False(polygonalFace2D.Inside(new Point2D(4.005, 5), tolerance));
            Assert.True(polygonalFace2D.Inside(new Point2D(3.98, 5), tolerance));
        }

        /// <summary>
        /// Verifies that <see cref="Query.Inside(IEnumerable{Point2D}, Point2D)"/> answers the same for a list, an array and a lazy sequence of the same ring, since the list is now read in place rather than copied.
        /// </summary>
        [Fact]
        public void Inside_Points_SameForListArrayAndSequence()
        {
            List<Point2D> ring = [new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 4), new Point2D(4, 4), new Point2D(4, 6), new Point2D(0, 6)];
            Point2D[] array = [.. ring];
            IEnumerable<Point2D> sequence = ring.Select(x => x);

            foreach (Point2D probe in new[] { new Point2D(2, 2), new Point2D(7, 5), new Point2D(20, 20), new Point2D(1, 5) })
            {
                bool expected = array.Inside(probe);
                Assert.Equal(expected, ring.Inside(probe));
                Assert.Equal(expected, sequence.Inside(probe));
            }

            Assert.True(ring.Inside(new Point2D(2, 2)));
            Assert.False(ring.Inside(new Point2D(7, 5)));
        }
    }
}
