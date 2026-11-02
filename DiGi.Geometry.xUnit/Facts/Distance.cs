namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests Distance(ISegmentable2D, ISegmentable2D, out Point2D?, out Point2D?, double) for parallel segments: it must return the perpendicular distance with both closest points set instead of NaN (DiGi-Organization/DiGi.Geometry#11).
        /// </summary>
        [Fact]
        public void Distance_ParallelSegments()
        {
            double tolerance = DiGi.Core.Constants.Tolerance.Distance;

            Planar.Classes.Segment2D segment2D = new(new Planar.Classes.Point2D(12, 4.5), new Planar.Classes.Point2D(12, 5));

            List<Planar.Classes.Point2D> point2Ds_Road = [new Planar.Classes.Point2D(14, -10), new Planar.Classes.Point2D(14, 19)];
            Planar.Classes.Polyline2D polyline2D = new(point2Ds_Road);
            Planar.Classes.Segment2D segment2D_Road = new(point2Ds_Road[0], point2Ds_Road[1]);

            double distance = Planar.Query.Distance(segment2D, polyline2D, out Planar.Classes.Point2D? point2D_Closest1, out Planar.Classes.Point2D? point2D_Closest2, tolerance);

            Assert.Equal(2.0, distance, 9);
            Assert.NotNull(point2D_Closest1);
            Assert.NotNull(point2D_Closest2);
            Assert.True(segment2D.On(point2D_Closest1, tolerance));
            Assert.True(segment2D_Road.On(point2D_Closest2, tolerance));
            Assert.Equal(12.0, point2D_Closest1.X, 9);
            Assert.Equal(14.0, point2D_Closest2.X, 9);
            Assert.Equal(2.0, point2D_Closest1.Distance(point2D_Closest2), 9);
        }

        /// <summary>
        /// Tests Distance(ISegmentable2D, ISegmentable2D, out Point2D?, out Point2D?, double) for non-parallel, non-intersecting segments: the closest pair must be the true minimum rather than the independently clamped line-intersection parameters (DiGi-Organization/DiGi.Geometry#11).
        /// </summary>
        [Fact]
        public void Distance_NonParallelNonIntersecting()
        {
            double tolerance = DiGi.Core.Constants.Tolerance.Distance;

            Planar.Classes.Segment2D segment2D_1 = new(new Planar.Classes.Point2D(0, 0), new Planar.Classes.Point2D(10, 0));
            Planar.Classes.Segment2D segment2D_2 = new(new Planar.Classes.Point2D(5, 1), new Planar.Classes.Point2D(15, 5));

            double distance = Planar.Query.Distance(segment2D_1, segment2D_2, out Planar.Classes.Point2D? point2D_Closest1, out Planar.Classes.Point2D? point2D_Closest2, tolerance);

            Assert.Equal(1.0, distance, 9);
            Assert.NotNull(point2D_Closest1);
            Assert.NotNull(point2D_Closest2);
            Assert.True(segment2D_1.On(point2D_Closest1, tolerance));
            Assert.True(segment2D_2.On(point2D_Closest2, tolerance));
            Assert.Equal(5.0, point2D_Closest1.X, 9);
            Assert.Equal(0.0, point2D_Closest1.Y, 9);
            Assert.Equal(5.0, point2D_Closest2.X, 9);
            Assert.Equal(1.0, point2D_Closest2.Y, 9);
        }

        /// <summary>
        /// Tests Distance(ISegmentable2D, ISegmentable2D, out Point2D?, out Point2D?, double) for crossing segments (zero distance at the crossing point) and for collinear overlapping segments (zero distance, which returned NaN before the fix).
        /// </summary>
        [Fact]
        public void Distance_IntersectingAndCollinear()
        {
            double tolerance = DiGi.Core.Constants.Tolerance.Distance;

            Planar.Classes.Segment2D segment2D = new(new Planar.Classes.Point2D(0, 0), new Planar.Classes.Point2D(10, 0));

            Planar.Classes.Segment2D segment2D_Crossing = new(new Planar.Classes.Point2D(5, -5), new Planar.Classes.Point2D(5, 5));
            double distance = Planar.Query.Distance(segment2D, segment2D_Crossing, out Planar.Classes.Point2D? point2D_Closest1, out Planar.Classes.Point2D? point2D_Closest2, tolerance);

            Assert.Equal(0.0, distance, 9);
            Assert.NotNull(point2D_Closest1);
            Assert.NotNull(point2D_Closest2);
            Assert.Equal(0.0, point2D_Closest1.Distance(point2D_Closest2), 9);
            Assert.Equal(5.0, point2D_Closest1.X, 9);
            Assert.Equal(0.0, point2D_Closest1.Y, 9);

            Planar.Classes.Segment2D segment2D_Collinear = new(new Planar.Classes.Point2D(5, 0), new Planar.Classes.Point2D(15, 0));
            distance = Planar.Query.Distance(segment2D, segment2D_Collinear, out point2D_Closest1, out point2D_Closest2, tolerance);

            Assert.Equal(0.0, distance, 9);
            Assert.NotNull(point2D_Closest1);
            Assert.NotNull(point2D_Closest2);
            Assert.True(segment2D.On(point2D_Closest1, tolerance));
            Assert.True(segment2D_Collinear.On(point2D_Closest2, tolerance));
            Assert.Equal(0.0, point2D_Closest1.Distance(point2D_Closest2), 9);
        }

        /// <summary>
        /// Tests Distance(ISegmentable2D, ISegmentable2D, out Point2D?, out Point2D?, double) for a zero-length segment: the point-to-segment distance must be returned instead of NaN.
        /// </summary>
        [Fact]
        public void Distance_DegenerateSegment()
        {
            double tolerance = DiGi.Core.Constants.Tolerance.Distance;

            Planar.Classes.Segment2D segment2D = new(new Planar.Classes.Point2D(0, 0), new Planar.Classes.Point2D(10, 0));
            Planar.Classes.Segment2D segment2D_Degenerate = new(new Planar.Classes.Point2D(3, 4), new Planar.Classes.Point2D(3, 4));

            double distance = Planar.Query.Distance(segment2D, segment2D_Degenerate, out Planar.Classes.Point2D? point2D_Closest1, out Planar.Classes.Point2D? point2D_Closest2, tolerance);

            Assert.Equal(4.0, distance, 9);
            Assert.NotNull(point2D_Closest1);
            Assert.NotNull(point2D_Closest2);
            Assert.Equal(3.0, point2D_Closest1.X, 9);
            Assert.Equal(0.0, point2D_Closest1.Y, 9);
            Assert.Equal(3.0, point2D_Closest2.X, 9);
            Assert.Equal(4.0, point2D_Closest2.Y, 9);
        }

        /// <summary>
        /// Tests Distance(ISegmentable2D, ISegmentable2D, out Point2D?, out Point2D?, double) at the intersection-tolerance boundary: a gap inside the tolerance still resolves to zero distance, a gap clear of the tolerance returns the exact positive gap.
        /// </summary>
        [Fact]
        public void Distance_ToleranceBoundary()
        {
            double tolerance = DiGi.Core.Constants.Tolerance.Distance;

            Planar.Classes.Segment2D segment2D = new(new Planar.Classes.Point2D(0, 0), new Planar.Classes.Point2D(10, 0));

            double gap_Inside = tolerance * 0.5;
            Planar.Classes.Segment2D segment2D_Inside = new(new Planar.Classes.Point2D(5, gap_Inside), new Planar.Classes.Point2D(5, gap_Inside + 1));
            double distance = Planar.Query.Distance(segment2D, segment2D_Inside, out _, out _, tolerance);

            Assert.Equal(0.0, distance, 9);

            double gap_Outside = tolerance * 2;
            Planar.Classes.Segment2D segment2D_Outside = new(new Planar.Classes.Point2D(5, gap_Outside), new Planar.Classes.Point2D(5, gap_Outside + 1));
            distance = Planar.Query.Distance(segment2D, segment2D_Outside, out Planar.Classes.Point2D? point2D_Closest1, out Planar.Classes.Point2D? point2D_Closest2, tolerance);

            Assert.Equal(gap_Outside, distance, 9);
            Assert.NotNull(point2D_Closest1);
            Assert.NotNull(point2D_Closest2);
            Assert.Equal(5.0, point2D_Closest1.X, 9);
            Assert.Equal(0.0, point2D_Closest1.Y, 9);
            Assert.Equal(5.0, point2D_Closest2.X, 9);
            Assert.Equal(gap_Outside, point2D_Closest2.Y, 9);
        }
    }
}
