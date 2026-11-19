using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Energy;
using DiGi.Analytical.Classes;
using DiGi.Geometry.Core.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reproduces the volume defect of <see cref="Building.Energy.Query.HeatedVolume(BuildingModel?, double)"/> (DiGi-Organization/DiGi.Analytical#22): a 13 x 15 x 3 m box whose floor is built on <c>Create.Plane(z)</c> with the footprint in map coordinates - plane origin (0, 0, 90), about 800 km from the box - and whose shell is open by less than 1 % of its area must still give the box's volume.
        /// <para>The west wall stops 6 cm below the roof, which leaves 0.9 m2 of uncancelled horizontal normal area - 0.16 % of the 557.1 m2 shell, inside the closure window the method accepts - and no vertical residual. The reference point of the unmodified method is the plane origin of the first face, so exactly that horizontal residual is levered out over the 800 km to the map origin and the method returns a volume two orders of magnitude above the box. The fact pins the box's own arithmetic (the five full-height faces plus the shortened west wall), so it fails on the unmodified code and passes after the fix.</para>
        /// <para>The guards keep the fixture honest: the shell's first face carries the map-origin plane, the residual is neither zero nor outside the 1 % window, and the box sits more than 700 km from the map origin.</para>
        /// </summary>
        [Fact]
        public void HeatedVolume_FarPlaneOrigin()
        {
            double gap = 0.06;
            BuildingModel buildingModel = HeatedVolume_Box(629671, 489137, 90, 13, 15, 3, gap);

            Shell? shell = buildingModel.GetExternalShell(Side.External);
            Assert.NotNull(shell);

            List<Face>? faces = shell.PolygonalFaces;
            Assert.NotNull(faces);
            Assert.Equal(6, faces.Count);

            // Guard: the first face is the floor on Create.Plane(z), whose plane origin is the map origin - the defect needs exactly this.
            Point3D? point3D_Origin = faces[0].Plane?.Origin;
            Assert.NotNull(point3D_Origin);
            Assert.Equal(0.0, point3D_Origin.X, 3);
            Assert.Equal(0.0, point3D_Origin.Y, 3);
            Assert.Equal(90.0, point3D_Origin.Z, 3);

            // Guard: the shell is open, and open by less than the 1 % of its area the closure test accepts.
            double area_Total = 0.0;
            double x = 0.0;
            double y = 0.0;
            double z = 0.0;
            foreach (Face face in faces)
            {
                Plane? plane = face.Plane;
                Assert.NotNull(plane);
                Vector3D? vector3D_Normal = plane.Normal?.Unit;
                Assert.NotNull(vector3D_Normal);
                double area = face.GetArea();
                Assert.False(double.IsNaN(area));

                area_Total += area;
                x += area * vector3D_Normal.X;
                y += area * vector3D_Normal.Y;
                z += area * vector3D_Normal.Z;
            }

            double residual = System.Math.Sqrt((x * x) + (y * y) + (z * z));
            Assert.True(residual > 0, "The shell is closed, so the fixture does not exercise the open-shell path.");
            Assert.True(residual < 0.01 * area_Total, "The shell is open by more than 1 % of its area, so the method must reject it and the fixture proves nothing.");

            // Guard: the box lies more than 700 km from the map origin, so a map-origin reference point is a lever arm.
            BoundingBox3D? boundingBox3D = shell.GetBoundingBox();
            Assert.NotNull(boundingBox3D);
            double distance = System.Math.Sqrt((boundingBox3D.Min.X * boundingBox3D.Min.X) + (boundingBox3D.Min.Y * boundingBox3D.Min.Y));
            Assert.True(distance > 700000, "The box is too close to the map origin to reproduce the defect.");

            // Five full-height faces contribute 97.5 m3 each (area x half the extent / 3); the shortened west wall contributes 15 x (3 - gap) x 6.5 / 3.
            double expected = 487.5 + ((15.0 * (3.0 - gap)) * 6.5 / 3.0);

            double volume = buildingModel.HeatedVolume();
            Assert.False(double.IsNaN(volume), "The shell is open by less than 1 % of its area, so the method accepts it rather than falling back.");
            Assert.Equal(expected, volume, 6);
            Assert.True(volume <= boundingBox3D.GetVolume());
        }

        /// <summary>
        /// Tests the closure guard of <see cref="Building.Energy.Query.HeatedVolume(BuildingModel?, double)"/> (DiGi-Organization/DiGi.Analytical#22) on both of its stages: the area screen and the bound on what the residual can cost.
        /// <para>The box of <see cref="HeatedVolume_FarPlaneOrigin"/> carries the gap in the west wall top. Just inside the bound - 0.115 m, a residual of 1.725 m2 levered over the shell's own half diagonal - the method returns the box's volume; just outside - 0.117 m, the same 0.33 % of area, so the area screen still passes - it returns <see cref="double.NaN"/> and the caller falls back. A gap of 0.5 m puts the residual past the 1 % area screen itself, and a null or empty model answers <see cref="double.NaN"/> as well.</para>
        /// </summary>
        [Fact]
        public void HeatedVolume_ResidualBound()
        {
            BuildingModel buildingModel_Inside = HeatedVolume_Box(629671, 489137, 90, 13, 15, 3, 0.115);
            double volume = buildingModel_Inside.HeatedVolume();
            Assert.False(double.IsNaN(volume));
            Assert.Equal(487.5 + ((15.0 * (3.0 - 0.115)) * 6.5 / 3.0), volume, 6);

            BuildingModel buildingModel_Outside = HeatedVolume_Box(629671, 489137, 90, 13, 15, 3, 0.117);
            Assert.True(double.IsNaN(buildingModel_Outside.HeatedVolume()), "The residual is past the bound but inside the 1 % area window, so only the bound may reject it.");

            BuildingModel buildingModel_Open = HeatedVolume_Box(629671, 489137, 90, 13, 15, 3, 0.5);
            Assert.True(double.IsNaN(buildingModel_Open.HeatedVolume()), "A residual of 7.5 m2 exceeds the 1 % of the shell area, so the area screen rejects it.");

            Assert.True(double.IsNaN(((BuildingModel?)null).HeatedVolume()));
            Assert.True(double.IsNaN(new BuildingModel().HeatedVolume()));
        }

        /// <summary>
        /// Builds the box of <see cref="HeatedVolume_FarPlaneOrigin"/> and <see cref="HeatedVolume_ResidualBound"/>: a floor whose plane is <c>Create.Plane(elevation)</c> - its origin is (0, 0, elevation) and the footprint is projected onto it from map coordinates, exactly as the GIS extrusion builds it - plus the roof and four walls wound outward from their own points, the west wall shortened by the gap so its top edge no longer meets the roof.
        /// </summary>
        /// <param name="x">The map x coordinate of the footprint corner.</param>
        /// <param name="y">The map y coordinate of the footprint corner.</param>
        /// <param name="elevation">The base elevation [m]; the floor plane's origin is (0, 0, elevation).</param>
        /// <param name="length_X">The extent in x [m].</param>
        /// <param name="length_Y">The extent in y [m].</param>
        /// <param name="height">The height [m].</param>
        /// <param name="gap">How far the west wall stops below the roof [m]; zero closes the shell.</param>
        /// <returns>The model, one space bound by the six components.</returns>
        private static BuildingModel HeatedVolume_Box(double x, double y, double elevation, double length_X, double length_Y, double height, double gap)
        {
            double xMax = x + length_X;
            double yMax = y + length_Y;

            Plane? plane = Geometry.Spatial.Create.Plane(elevation);
            Assert.NotNull(plane);

            PolygonalFace3D? polygonalFace3D_Floor = Geometry.Spatial.Create.PolygonalFace3D(plane, [new Point2D(x, y), new Point2D(xMax, y), new Point2D(xMax, yMax), new Point2D(x, yMax)]);
            Assert.NotNull(polygonalFace3D_Floor);

            // The floor ring is whatever the plane's own axes project the footprint to; the rest of the box follows the ring they actually hold.
            List<Point3D>? point3Ds_Floor = polygonalFace3D_Floor.ExternalEdge?.GetPoints();
            Assert.NotNull(point3Ds_Floor);
            Assert.Equal(4, point3Ds_Floor.Count);

            double xMin = point3Ds_Floor.Min(point3D => point3D.X);
            double xMax_Ring = point3Ds_Floor.Max(point3D => point3D.X);
            double yMin = point3Ds_Floor.Min(point3D => point3D.Y);
            double yMax_Ring = point3Ds_Floor.Max(point3D => point3D.Y);
            double zMin = point3Ds_Floor.Min(point3D => point3D.Z);
            double zMax = zMin + height;

            PolygonalFace3D? polygonalFace3D_Roof = BuildingModel_Footprints_Face([new Point3D(xMin, yMin, zMax), new Point3D(xMax_Ring, yMin, zMax), new Point3D(xMax_Ring, yMax_Ring, zMax), new Point3D(xMin, yMax_Ring, zMax)]);
            PolygonalFace3D? polygonalFace3D_South = BuildingModel_Footprints_Face([new Point3D(xMin, yMin, zMin), new Point3D(xMax_Ring, yMin, zMin), new Point3D(xMax_Ring, yMin, zMax), new Point3D(xMin, yMin, zMax)]);
            PolygonalFace3D? polygonalFace3D_East = BuildingModel_Footprints_Face([new Point3D(xMax_Ring, yMin, zMin), new Point3D(xMax_Ring, yMax_Ring, zMin), new Point3D(xMax_Ring, yMax_Ring, zMax), new Point3D(xMax_Ring, yMin, zMax)]);
            PolygonalFace3D? polygonalFace3D_North = BuildingModel_Footprints_Face([new Point3D(xMax_Ring, yMax_Ring, zMin), new Point3D(xMin, yMax_Ring, zMin), new Point3D(xMin, yMax_Ring, zMax), new Point3D(xMax_Ring, yMax_Ring, zMax)]);
            PolygonalFace3D? polygonalFace3D_West = BuildingModel_Footprints_Face([new Point3D(xMin, yMax_Ring, zMin), new Point3D(xMin, yMin, zMin), new Point3D(xMin, yMin, zMax - gap), new Point3D(xMin, yMax_Ring, zMax - gap)]);

            Assert.NotNull(polygonalFace3D_Roof);
            Assert.NotNull(polygonalFace3D_South);
            Assert.NotNull(polygonalFace3D_East);
            Assert.NotNull(polygonalFace3D_North);
            Assert.NotNull(polygonalFace3D_West);

            FaceFloor faceFloor = new(polygonalFace3D_Floor);
            SurfaceRoof surfaceRoof = new(polygonalFace3D_Roof);
            SurfaceWall surfaceWall_South = new(polygonalFace3D_South);
            SurfaceWall surfaceWall_East = new(polygonalFace3D_East);
            SurfaceWall surfaceWall_North = new(polygonalFace3D_North);
            SurfaceWall surfaceWall_West = new(polygonalFace3D_West);

            Space space = new(new Point3D((xMin + xMax_Ring) / 2.0, (yMin + yMax_Ring) / 2.0, (zMin + zMax) / 2.0), "Space");

            BuildingModel buildingModel = new();

            // The floor is bound first, so its relation - and with it the map-origin face - leads the shell's face list.
            Assert.True(buildingModel.Assign(faceFloor, space));
            Assert.True(buildingModel.Assign(surfaceRoof, space));
            Assert.True(buildingModel.Assign(surfaceWall_South, space));
            Assert.True(buildingModel.Assign(surfaceWall_East, space));
            Assert.True(buildingModel.Assign(surfaceWall_North, space));
            Assert.True(buildingModel.Assign(surfaceWall_West, space));

            return buildingModel;
        }
    }
}
