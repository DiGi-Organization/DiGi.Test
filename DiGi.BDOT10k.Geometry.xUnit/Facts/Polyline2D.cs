using DiGi.Geometry.Planar.Classes;
using DiGi.GML.Classes;
using System.Collections.Generic;

namespace DiGi.BDOT10k.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Convert.ToDiGi(AbstractCurve?)"/> converts a single-segment curve, the shape every BDOT10k road has, into a polyline with its vertices in order.
        /// </summary>
        [Fact]
        public void Polyline2D_Curve()
        {
            Curve curve = new()
            {
                segments = [new LineStringSegment() { posList = [0, 0, 10, 0, 10, 10] }]
            };

            Polyline2D? polyline2D = curve.ToDiGi();

            Assert.NotNull(polyline2D);

            List<Point2D>? point2Ds = polyline2D.GetPoints();
            Assert.NotNull(point2Ds);
            Assert.Equal(3, point2Ds.Count);
            Assert.Equal(new Point2D(0, 0), point2Ds[0]);
            Assert.Equal(new Point2D(10, 0), point2Ds[1]);
            Assert.Equal(new Point2D(10, 10), point2Ds[2]);
            Assert.Equal(20, polyline2D.Length, 9);
        }

        /// <summary>
        /// Tests that the segments of a multi-segment curve are concatenated in order, and that the point two segments share appears once.
        /// <para>Without dropping the repeated shared point the polyline would carry a segment of no length, which has no direction.</para>
        /// </summary>
        [Fact]
        public void Polyline2D_CurveSegments_SharedEndpoint()
        {
            Curve curve = new()
            {
                segments =
                [
                    new LineStringSegment() { posList = [0, 0, 10, 0] },
                    new LineStringSegment() { posList = [10, 0, 10, 10] },
                ]
            };

            Polyline2D? polyline2D = curve.ToDiGi();

            Assert.NotNull(polyline2D);

            List<Point2D>? point2Ds = polyline2D.GetPoints();
            Assert.NotNull(point2Ds);
            Assert.Equal(3, point2Ds.Count);
            Assert.Equal(new Point2D(10, 0), point2Ds[1]);

            List<Segment2D>? segment2Ds = polyline2D.GetSegments();
            Assert.NotNull(segment2Ds);
            Assert.Equal(2, segment2Ds.Count);
            foreach (Segment2D segment2D in segment2Ds)
            {
                Assert.True(segment2D.Length > 0);
            }
        }

        /// <summary>
        /// Tests that a curve whose points all coincide returns null rather than a polyline of no length.
        /// </summary>
        [Fact]
        public void Polyline2D_DegenerateLine()
        {
            Curve curve = new()
            {
                segments = [new LineStringSegment() { posList = [5, 5, 5, 5] }]
            };

            Assert.Null(curve.ToDiGi());
        }

        /// <summary>
        /// Tests that <see cref="Convert.ToDiGi(AbstractCurve?)"/> returns null for a null curve, a curve without segments, and a segment with an empty or odd-length coordinate list.
        /// </summary>
        [Fact]
        public void Polyline2D_InvalidInput()
        {
            AbstractCurve? abstractCurve_Null = null;
            Assert.Null(abstractCurve_Null.ToDiGi());

            Curve curve_NoSegments = new();
            Assert.Null(curve_NoSegments.ToDiGi());

            Curve curve_Empty = new()
            {
                segments = [new LineStringSegment() { posList = [] }]
            };
            Assert.Null(curve_Empty.ToDiGi());

            Curve curve_Odd = new()
            {
                segments = [new LineStringSegment() { posList = [0, 0, 10] }]
            };
            Assert.Null(curve_Odd.ToDiGi());
        }
    }
}
