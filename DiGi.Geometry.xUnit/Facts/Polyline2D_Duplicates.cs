using DiGi.Geometry.Planar.Classes;
using System.Collections.Generic;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a polyline created from points which repeat their predecessor drops the repeats.
        /// <para>A repeated point contributes a segment of no length, which has no direction. A point just inside the tolerance from its predecessor is treated as a repeat and removed, while one just outside it is kept.</para>
        /// </summary>
        [Fact]
        public void Polyline2D_Create_ConsecutiveDuplicateRemoved()
        {
            Polyline2D? polyline2D = Planar.Create.Polyline2D([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 0), new Point2D(10, 10)]);

            Assert.NotNull(polyline2D);

            List<Point2D>? point2Ds = polyline2D.GetPoints();

            Assert.NotNull(point2Ds);
            Assert.Equal(3, point2Ds.Count);
            Assert.Equal(new Point2D(0, 0), point2Ds[0]);
            Assert.Equal(new Point2D(10, 0), point2Ds[1]);
            Assert.Equal(new Point2D(10, 10), point2Ds[2]);

            double tolerance = 0.01;

            Polyline2D? polyline2D_Inside = Planar.Create.Polyline2D([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10 + tolerance * 0.5, 0), new Point2D(10, 10)], tolerance);

            Assert.NotNull(polyline2D_Inside);

            List<Point2D>? point2Ds_Inside = polyline2D_Inside.GetPoints();

            Assert.NotNull(point2Ds_Inside);
            Assert.Equal(3, point2Ds_Inside.Count);

            Polyline2D? polyline2D_Outside = Planar.Create.Polyline2D([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10 + tolerance * 2, 0), new Point2D(10, 10)], tolerance);

            Assert.NotNull(polyline2D_Outside);

            List<Point2D>? point2Ds_Outside = polyline2D_Outside.GetPoints();

            Assert.NotNull(point2Ds_Outside);
            Assert.Equal(4, point2Ds_Outside.Count);
        }

        /// <summary>
        /// Tests that a polyline which returns to its starting point keeps both its first and its last point.
        /// <para>A polyline is open, so its last point is not compared with its first. Only a ring drops a repeated closing position, which is the job of <see cref="Planar.Create.Polygon2D(IEnumerable{Point2D?}?, double)"/>.</para>
        /// </summary>
        [Fact]
        public void Polyline2D_Create_FirstAndLastKept()
        {
            Polyline2D? polyline2D = Planar.Create.Polyline2D([new Point2D(0, 0), new Point2D(10, 0), new Point2D(0, 0)]);

            Assert.NotNull(polyline2D);

            List<Point2D>? point2Ds = polyline2D.GetPoints();

            Assert.NotNull(point2Ds);
            Assert.Equal(3, point2Ds.Count);
            Assert.Equal(new Point2D(0, 0), point2Ds[0]);
            Assert.Equal(new Point2D(0, 0), point2Ds[2]);
        }

        /// <summary>
        /// Tests that the constructor stores the points exactly as given, leaving a repeated point in place.
        /// <para>Constructors carry no calculation, so removing repeated points is the job of <see cref="Planar.Create.Polyline2D(IEnumerable{Point2D?}?, double)"/> alone. This pins that split: a caller who already holds clean points pays nothing for the constructor, and a caller who does not knows to reach for the factory.</para>
        /// </summary>
        [Fact]
        public void Polyline2D_Constructor_KeepsPointsAsGiven()
        {
            Polyline2D polyline2D = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 0), new Point2D(10, 10)]);

            List<Point2D>? point2Ds = polyline2D.GetPoints();

            Assert.NotNull(point2Ds);
            Assert.Equal(4, point2Ds.Count);
        }

        /// <summary>
        /// Tests that a polyline which only reaches two positions by repeating a point is rejected rather than turned into a line of no length.
        /// <para>The count is checked after the repeats are removed, so the guard measures distinct points instead of positions. A single point, an empty collection and a null collection are rejected as well.</para>
        /// </summary>
        [Fact]
        public void Polyline2D_Create_TooShort()
        {
            // One point written as two positions.
            Assert.Null(Planar.Create.Polyline2D([new Point2D(5, 5), new Point2D(5, 5)]));

            Assert.Null(Planar.Create.Polyline2D([new Point2D(5, 5)]));
            Assert.Null(Planar.Create.Polyline2D([]));
            Assert.Null(Planar.Create.Polyline2D((IEnumerable<Point2D?>?)null));
        }
    }
}
