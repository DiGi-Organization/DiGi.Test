using DiGi.Geometry.Core;
using DiGi.Geometry.Core.Enums;
using DiGi.Geometry.Planar;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the orientation logic for polygons in both 2D and 3D spaces to ensure that clockwise and counter-clockwise orientations are correctly identified.
        /// </summary>
        [Fact]
        public void Orientation()
        {
            Polygon2D polygon2D;

            polygon2D = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);

            Assert.True(Core.Enums.Orientation.CounterClockwise == polygon2D.Orientation());

            polygon2D = new([new Point2D(0, 0), new Point2D(0, 10), new Point2D(10, 10), new Point2D(10, 0)]);

            Assert.True(Core.Enums.Orientation.Clockwise == polygon2D.Orientation());

            PolygonalFace3D? polygonalFace3D_1;
            PolygonalFace3D? polygonalFace3D_2;

            IPolygonal3D? polygonal3D_1;
            IPolygonal3D? polygonal3D_2;

            Orientation orientation_1;
            Orientation orientation_2;

            //Case 1
            polygonalFace3D_1 = Spatial.Create.PolygonalFace3D(new Plane(new Point3D(0, 0, 0), new Spatial.Classes.Vector3D(0, 0, 1)), new Point2D(0, 0), new Point2D(0, 10), new Point2D(10, 10), new Point2D(10, 0));

            polygonal3D_1 = polygonalFace3D_1?.ExternalEdge;

            Assert.NotNull(polygonal3D_1);
            if (polygonal3D_1 is null)
            {
                return;
            }

            polygonalFace3D_2 = Spatial.Create.PolygonalFace3D(new Plane(new Point3D(0, 0, 0), new Spatial.Classes.Vector3D(0, 0, 1)), new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));

            polygonal3D_2 = polygonalFace3D_2?.ExternalEdge;

            Assert.NotNull(polygonal3D_2);
            if (polygonal3D_2 is null)
            {
                return;
            }

            orientation_1 = polygonal3D_1.Orientation();
            orientation_2 = polygonal3D_2.Orientation();

            Assert.True(orientation_1.Opposite() == orientation_2);

            //Case 2
            polygonalFace3D_1 = Spatial.Create.PolygonalFace3D(new Plane(new Point3D(0, 0, 0), new Spatial.Classes.Vector3D(0, 0, 1)), new Point2D(0, 0), new Point2D(0, 10), new Point2D(10, 10), new Point2D(10, 0));

            polygonal3D_1 = polygonalFace3D_1?.ExternalEdge;

            Assert.NotNull(polygonal3D_1);
            if (polygonal3D_1 is null)
            {
                return;
            }

            polygonalFace3D_2 = Spatial.Create.PolygonalFace3D(new Plane(new Point3D(0, 0, 0), new Spatial.Classes.Vector3D(0, 0, -1)), new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10));

            polygonal3D_2 = polygonalFace3D_2?.ExternalEdge;

            Assert.NotNull(polygonal3D_2);
            if (polygonal3D_2 is null)
            {
                return;
            }

            orientation_1 = polygonal3D_1.Orientation();
            orientation_2 = polygonal3D_2.Orientation();

            Assert.True(orientation_1.Opposite() == orientation_2);

            //Case 3

            Plane plane_1 = new(new Point3D(0, 0, 0), new Spatial.Classes.Vector3D(0, 0, 1));
            List<Point2D>? point2Ds = [new Point2D(0, 0), new Point2D(0, 10), new Point2D(10, 10), new Point2D(10, 0)];

            polygonalFace3D_1 = Spatial.Create.PolygonalFace3D(plane_1, [.. point2Ds]);

            polygonal3D_1 = polygonalFace3D_1?.ExternalEdge;

            Assert.NotNull(polygonal3D_1);
            if (polygonal3D_1 is null)
            {
                return;
            }

            Plane plane_2 = new(new Point3D(0, 0, 0), new Spatial.Classes.Vector3D(0, 0, -1));

            point2Ds = point2Ds.ConvertAll(plane_1.Convert).ConvertAll(plane_2.Convert)!;

            polygonalFace3D_2 = Spatial.Create.PolygonalFace3D(plane_2, [.. point2Ds]);

            polygonal3D_2 = polygonalFace3D_2?.ExternalEdge;

            Assert.NotNull(polygonal3D_2);
            if (polygonal3D_2 is null)
            {
                return;
            }

            orientation_1 = polygonal3D_1.Orientation();
            orientation_2 = polygonal3D_2.Orientation();

            Assert.True(orientation_1.Opposite() == orientation_2);
        }
    }
}