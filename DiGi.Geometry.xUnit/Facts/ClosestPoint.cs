using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the functionality of finding the closest point using the <see cref="Planar.Query.ClosestPoint(Point2D, Point2D, Point2D, bool)"/> method across various scenarios and constraints.
        /// </summary>
        [Fact]
        public void ClosestPoint()
        {
            Point2D point2D_1 = new(1, 10);
            Point2D point2D_2 = new(0, 0);
            Point2D point2D_3 = new(0, 9);

            Point2D? point2D_Closest;

            point2D_Closest = Planar.Query.ClosestPoint(point2D_1, point2D_2, point2D_3, true);
            Assert.NotNull(point2D_Closest);

            if (point2D_Closest is not null)
            {
                Assert.Equal(point2D_Closest, new Point2D(0, 9));
            }

            point2D_Closest = Planar.Query.ClosestPoint(point2D_1, point2D_2, point2D_3, false);
            Assert.NotNull(point2D_Closest);

            if (point2D_Closest is not null)
            {
                Assert.Equal(point2D_Closest, new Point2D(0, 10));
            }

            point2D_Closest = Planar.Query.ClosestPoint(point2D_1, point2D_2, point2D_3, true, false);
            Assert.NotNull(point2D_Closest);

            if (point2D_Closest is not null)
            {
                Assert.Equal(point2D_Closest, new Point2D(0, 10));
            }

            point2D_Closest = Planar.Query.ClosestPoint(new Point2D(1, -1), point2D_2, point2D_3, true, false);
            Assert.NotNull(point2D_Closest);

            if (point2D_Closest is not null)
            {
                Assert.Equal(point2D_Closest, new Point2D(0, 0));
            }

            point2D_Closest = Planar.Query.ClosestPoint(point2D_1, point2D_2, point2D_3, false, true);
            Assert.NotNull(point2D_Closest);

            if (point2D_Closest is not null)
            {
                Assert.Equal(point2D_Closest, new Point2D(0, 9));
            }

            Point3D point3D_1 = new(1, 10, 0);
            Point3D point3D_2 = new(0, 0, 0);
            Point3D point3D_3 = new(0, 9, 0);

            Point3D? point3D_Closest;

            point3D_Closest = Spatial.Query.ClosestPoint(point3D_1, point3D_2, point3D_3, true);
            Assert.NotNull(point3D_Closest);

            if (point3D_Closest is not null)
            {
                Assert.Equal(point3D_Closest, new Point3D(0, 9, 0));
            }

            point3D_Closest = Spatial.Query.ClosestPoint(point3D_1, point3D_2, point3D_3, false);
            Assert.NotNull(point3D_Closest);

            if (point3D_Closest is not null)
            {
                Assert.Equal(point3D_Closest, new Point3D(0, 10, 0));
            }

            point3D_Closest = Spatial.Query.ClosestPoint(point3D_1, point3D_2, point3D_3, true, false);
            Assert.NotNull(point3D_Closest);

            if (point3D_Closest is not null)
            {
                Assert.Equal(point3D_Closest, new Point3D(0, 10, 0));
            }

            point3D_Closest = Spatial.Query.ClosestPoint(new Point3D(1, -1, 0), point3D_2, point3D_3, true, false);
            Assert.NotNull(point3D_Closest);

            if (point3D_Closest is not null)
            {
                Assert.Equal(point3D_Closest, new Point3D(0, 0, 0));
            }

            point3D_Closest = Spatial.Query.ClosestPoint(point3D_1, point3D_2, point3D_3, false, true);
            Assert.NotNull(point3D_Closest);

            if (point3D_Closest is not null)
            {
                Assert.Equal(point3D_Closest, new Point3D(0, 9, 0));
            }
        }
    }
}