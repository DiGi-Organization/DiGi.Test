using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Creates a vertical face in the plane x = <paramref name="x"/>, spanning y and z, optionally with a rectangular hole.
        /// </summary>
        /// <param name="x">The x of the plane at y = 0.</param>
        /// <param name="slope">The change of x per metre of y, which tilts the face about the z axis.</param>
        /// <param name="y_Min">The lowest y.</param>
        /// <param name="y_Max">The highest y.</param>
        /// <param name="z_Max">The height.</param>
        /// <param name="hole">The hole as (y_Min, z_Min, y_Max, z_Max), or null.</param>
        /// <returns>The face.</returns>
        private static PolygonalFace3D CoplanarIntersection_Face(double x, double slope, double y_Min, double y_Max, double z_Max, (double, double, double, double)? hole = null)
        {
            Point3D Point(double y, double z) => new(x + (slope * y), y, z);

            Polygon3D? polygon3D = Spatial.Create.Polygon3D([Point(y_Min, 0), Point(y_Max, 0), Point(y_Max, z_Max), Point(y_Min, z_Max)]);
            Assert.NotNull(polygon3D);

            List<IPolygonal3D>? polygonal3Ds_Internal = null;
            if (hole is (double, double, double, double) hole_Value)
            {
                Polygon3D? polygon3D_Hole = Spatial.Create.Polygon3D([Point(hole_Value.Item1, hole_Value.Item2), Point(hole_Value.Item3, hole_Value.Item2), Point(hole_Value.Item3, hole_Value.Item4), Point(hole_Value.Item1, hole_Value.Item4)]);
                Assert.NotNull(polygon3D_Hole);
                polygonal3Ds_Internal = [polygon3D_Hole];
            }

            PolygonalFace3D? polygonalFace3D = Spatial.Create.PolygonalFace3D(polygon3D, polygonal3Ds_Internal);
            Assert.NotNull(polygonalFace3D);

            return polygonalFace3D;
        }

        /// <summary>
        /// Sums the area of the given faces.
        /// </summary>
        /// <param name="polygonalFace3Ds">The faces.</param>
        /// <returns>The total area.</returns>
        private static double CoplanarIntersection_Area(List<PolygonalFace3D>? polygonalFace3Ds)
        {
            Assert.NotNull(polygonalFace3Ds);

            double result = 0;
            foreach (PolygonalFace3D polygonalFace3D in polygonalFace3Ds)
            {
                result += polygonalFace3D.GetArea();
            }

            return result;
        }

        /// <summary>
        /// Tests <see cref="Spatial.Query.CoplanarIntersection"/> on vertical faces of 10 m x 3 m in the plane x = 10.
        /// <para>A face shifted by 4 m along y shares 6 m x 3 m = 18 m2, whichever way the two face; the result lies in the plane of the first face. With a 0.2 m limit a face 0.19 m away still overlaps and one 0.21 m away does not. A face at about 2.9 degrees is parallel enough with a 5 degree limit, one at about 8.5 degrees is not, although both stay within 0.2 m. A face meeting the first along an edge only, or with a hole over part of the overlap, gives nothing or the overlap less the hole. Null gives null.</para>
        /// </summary>
        [Fact]
        public void CoplanarIntersection()
        {
            const double maxDistance = 0.2;
            const double maxAngle = 5.0 * System.Math.PI / 180.0;

            PolygonalFace3D polygonalFace3D = CoplanarIntersection_Face(10, 0, 0, 10, 3);

            List<PolygonalFace3D>? polygonalFace3Ds_Shifted = Spatial.Query.CoplanarIntersection(polygonalFace3D, CoplanarIntersection_Face(10, 0, 4, 14, 3), maxDistance, maxAngle);
            Assert.Equal(18.0, CoplanarIntersection_Area(polygonalFace3Ds_Shifted), 6);
            Assert.True(polygonalFace3D.Plane!.Coplanar(polygonalFace3Ds_Shifted![0].Plane, 1e-9));

            PolygonalFace3D polygonalFace3D_Reversed = CoplanarIntersection_Face(10, 0, 4, 14, 3);
            polygonalFace3D_Reversed.Inverse();
            Assert.Equal(18.0, CoplanarIntersection_Area(Spatial.Query.CoplanarIntersection(polygonalFace3D, polygonalFace3D_Reversed, maxDistance, maxAngle)), 6);

            Assert.Equal(30.0, CoplanarIntersection_Area(Spatial.Query.CoplanarIntersection(polygonalFace3D, CoplanarIntersection_Face(10.19, 0, 0, 10, 3), maxDistance, maxAngle)), 6);
            Assert.Empty(Spatial.Query.CoplanarIntersection(polygonalFace3D, CoplanarIntersection_Face(10.21, 0, 0, 10, 3), maxDistance, maxAngle)!);

            Assert.True(CoplanarIntersection_Area(Spatial.Query.CoplanarIntersection(polygonalFace3D, CoplanarIntersection_Face(10, 0.05, 0, 1, 3), maxDistance, maxAngle)) > 2.9);
            Assert.Empty(Spatial.Query.CoplanarIntersection(polygonalFace3D, CoplanarIntersection_Face(10, 0.15, 0, 1, 3), maxDistance, maxAngle)!);

            Assert.Empty(Spatial.Query.CoplanarIntersection(polygonalFace3D, CoplanarIntersection_Face(10, 0, 10, 20, 3), maxDistance, maxAngle)!);
            Assert.Equal(28.0, CoplanarIntersection_Area(Spatial.Query.CoplanarIntersection(polygonalFace3D, CoplanarIntersection_Face(10, 0, 0, 10, 3, (2, 1, 4, 2)), maxDistance, maxAngle)), 6);

            Assert.Null(Spatial.Query.CoplanarIntersection(null, polygonalFace3D, maxDistance, maxAngle));
            Assert.Null(Spatial.Query.CoplanarIntersection(polygonalFace3D, null, maxDistance, maxAngle));
        }
    }
}
