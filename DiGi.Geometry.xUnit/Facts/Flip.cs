using DiGi.Geometry.Core.Enums;
using DiGi.Geometry.Spatial;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Enums;
using DiGi.Geometry.Spatial.Interfaces;
using System.Globalization;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Planar{T}.Flip(Spatial.Enums.SpatialAxis, Spatial.Enums.SpatialAxis)"/> reverses the normal of a <see cref="PolygonalFace3D"/> and leaves the face where it is.
        /// <para>The face is a trapezoid, so that a mirror of its 2D geometry about the plane's own X axis - which is what flipping the plane alone amounts to - is visible: a rectangle centred on its plane origin maps onto itself under that mirror and would hide the defect. Every point of the flipped ring has to coincide with a point of the original ring, and the ring has to keep its vertex count.</para>
        /// </summary>
        [Fact]
        public void PolygonalFace3D_Flip()
        {
            PolygonalFace3D? polygonalFace3D = Flip_Face(new Point3D(0, 0, 0), new Point3D(10, 0, 0), new Point3D(7, 0, 4), new Point3D(1, 0, 2));
            Assert.NotNull(polygonalFace3D);

            Spatial.Classes.Vector3D? normal_Before = polygonalFace3D.Plane?.Normal;
            List<Point3D>? point3Ds_Before = polygonalFace3D.ExternalEdge?.GetPoints();
            Assert.NotNull(normal_Before);
            Assert.NotNull(point3Ds_Before);
            Assert.Equal(4, point3Ds_Before.Count);

            Assert.True(polygonalFace3D.Flip());

            Spatial.Classes.Vector3D? normal_After = polygonalFace3D.Plane?.Normal;
            List<Point3D>? point3Ds_After = polygonalFace3D.ExternalEdge?.GetPoints();
            Assert.NotNull(normal_After);
            Assert.NotNull(point3Ds_After);

            Assert.True(normal_Before * normal_After < -0.999, "The normal was not reversed.");
            Flip_AssertSamePoints(point3Ds_Before, point3Ds_After);

            // A second flip is the identity.
            Assert.True(polygonalFace3D.Flip());
            Assert.True(normal_Before * polygonalFace3D.Plane?.Normal > 0.999);
            Flip_AssertSamePoints(point3Ds_Before, polygonalFace3D.ExternalEdge?.GetPoints());
        }

        /// <summary>
        /// Tests that <see cref="Polyhedron{TPolygonalFace3D}.SetNormal(int, Side, double)"/>, through <see cref="PolyhedronNormalizationUpdater{TPolyhedron}"/>, turns an inward face outward without moving it.
        /// <para>The solid is a gabled house: the two gable walls are pentagons, whose apex makes them asymmetric about the horizontal line through their centroid, and the front gable is stored with its normal pointing into the house. After the update every face is outward, the solid is still closed at the default tolerance, and every ring holds exactly the points it held before.</para>
        /// </summary>
        [Fact]
        public void Polyhedron_SetNormal_Flip()
        {
            List<PolygonalFace3D?> polygonalFace3Ds =
            [
                // Floor, outward (down).
                Flip_Face(new Point3D(0, 0, 0), new Point3D(0, 6, 0), new Point3D(10, 6, 0), new Point3D(10, 0, 0)),
                // Front gable at y = 0, wound so that its stored normal points inward (+y).
                Flip_Face(new Point3D(0, 0, 0), new Point3D(0, 0, 3), new Point3D(5, 0, 5), new Point3D(10, 0, 3), new Point3D(10, 0, 0)),
                // Back gable at y = 6, outward (+y).
                Flip_Face(new Point3D(0, 6, 0), new Point3D(10, 6, 0), new Point3D(10, 6, 3), new Point3D(5, 6, 5), new Point3D(0, 6, 3)),
                // Side walls, outward.
                Flip_Face(new Point3D(0, 0, 0), new Point3D(0, 0, 3), new Point3D(0, 6, 3), new Point3D(0, 6, 0)),
                Flip_Face(new Point3D(10, 0, 0), new Point3D(10, 6, 0), new Point3D(10, 6, 3), new Point3D(10, 0, 3)),
                // Roof slopes, outward.
                Flip_Face(new Point3D(0, 0, 3), new Point3D(5, 0, 5), new Point3D(5, 6, 5), new Point3D(0, 6, 3)),
                Flip_Face(new Point3D(5, 0, 5), new Point3D(10, 0, 3), new Point3D(10, 6, 3), new Point3D(5, 6, 5)),
            ];

            List<IPolygonalFace3D> polygonalFace3Ds_Temp = [];
            foreach (PolygonalFace3D? polygonalFace3D in polygonalFace3Ds)
            {
                Assert.NotNull(polygonalFace3D);
                polygonalFace3Ds_Temp.Add(polygonalFace3D);
            }

            Polyhedron? polyhedron = Create.Polyhedron(polygonalFace3Ds_Temp);
            Assert.NotNull(polyhedron);
            Assert.Equal(7, polyhedron.Count);
            Assert.True(polyhedron.IsClosed());

            List<List<Point3D>?> rings_Before = polyhedron.PolygonalFaces!.ConvertAll(x => x.ExternalEdge?.GetPoints());

            Assert.True(polyhedron.GetNormal(1)!.Y > 0.999, "The front gable is not stored inward, so the fixture proves nothing.");

            PolyhedronNormalizationUpdater<Polyhedron> polyhedronNormalizationUpdater = new(Side.External, null, null)
            {
                Value = polyhedron
            };
            Assert.False(polyhedronNormalizationUpdater.Normalized());
            Assert.True(polyhedronNormalizationUpdater.Update());
            Assert.True(polyhedronNormalizationUpdater.Normalized());

            Assert.True(polyhedron.GetNormal(1)!.Y < -0.999, "The front gable was not turned outward.");
            Assert.True(polyhedron.IsClosed(), "Turning the gable outward opened the solid, so the face was moved rather than flipped.");

            for (int i = 0; i < polyhedron.Count; i++)
            {
                Flip_AssertSamePoints(rings_Before[i], polyhedron.PolygonalFaces![i].ExternalEdge?.GetPoints());
            }
        }

        private static PolygonalFace3D? Flip_Face(params Point3D[] point3Ds)
        {
            Polygon3D? polygon3D = Create.Polygon3D(point3Ds);
            return polygon3D is null ? null : new PolygonalFace3D(polygon3D);
        }

        private static void Flip_AssertSamePoints(List<Point3D>? point3Ds_Expected, List<Point3D>? point3Ds_Actual)
        {
            Assert.NotNull(point3Ds_Expected);
            Assert.NotNull(point3Ds_Actual);
            Assert.Equal(point3Ds_Expected.Count, point3Ds_Actual.Count);

            foreach (Point3D point3D in point3Ds_Actual)
            {
                double distance = point3Ds_Expected.Min(x => x.Distance(point3D));
                Assert.True(distance < DiGi.Core.Constants.Tolerance.Distance, string.Format(CultureInfo.CurrentCulture, "The point {0} moved by {1} when the face was flipped.", point3D, distance));
            }
        }
    }
}
