using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Building.Solar;
using DiGi.Core.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Solar.Classes;
using System.Collections.Generic;
using System.Reflection;

namespace DiGi.GIS.Analytical.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The EPSG:2180 position of the solar fixtures: central Warsaw, where <c>GIS.Analytical.Modify.UpdateBuildingInformation</c> stamps coordinates and the IMGW fixture's station is.
        /// </summary>
        private static readonly Point2D SolarFixture_Origin = new(638000, 486000);

        /// <summary>
        /// Builds a stamped prism building of one space over a footprint: one wall per footprint edge, a floor and a flat roof, every face built from its 3D corners with its outward normal.
        /// <para>X is easting and Y northing. The footprint is given counter-clockwise, relative to <see cref="SolarFixture_Origin"/>, so the outward normal of the edge from (x1, y1) to (x2, y2) is (y2 - y1, x1 - x2, 0).</para>
        /// </summary>
        /// <param name="footprint">The footprint corners, counter-clockwise, in metres from <see cref="SolarFixture_Origin"/>.</param>
        /// <param name="height">The height of the prism, in metres.</param>
        /// <param name="location">A point inside the footprint, relative to <see cref="SolarFixture_Origin"/>.</param>
        /// <returns>The building model, stamped with its coordinates and time zone as a downloaded model is.</returns>
        private static BuildingModel SolarFixture_Prism(Point2D[] footprint, double height, Point2D location)
        {
            List<Point2D> point2Ds = [];
            foreach (Point2D point2D in footprint)
            {
                point2Ds.Add(new Point2D(SolarFixture_Origin.X + point2D.X, SolarFixture_Origin.Y + point2D.Y));
            }

            List<IComponent> components = [];
            for (int i = 0; i < point2Ds.Count; i++)
            {
                Point2D point2D_1 = point2Ds[i];
                Point2D point2D_2 = point2Ds[(i + 1) % point2Ds.Count];

                Vector3D normal = new Vector3D(point2D_2.Y - point2D_1.Y, point2D_1.X - point2D_2.X, 0).Unit!;
                PolygonalFace3D polygonalFace3D = SolarFixture_Face(normal, [new Point3D(point2D_1.X, point2D_1.Y, 0), new Point3D(point2D_2.X, point2D_2.Y, 0), new Point3D(point2D_2.X, point2D_2.Y, height), new Point3D(point2D_1.X, point2D_1.Y, height)]);

                SurfaceWall? surfaceWall = DiGi.Analytical.Building.Create.SurfaceWall(polygonalFace3D);
                Assert.NotNull(surfaceWall);
                components.Add(surfaceWall);
            }

            List<Point3D> point3Ds_Roof = [];
            List<Point3D> point3Ds_Floor = [];
            foreach (Point2D point2D in point2Ds)
            {
                point3Ds_Roof.Add(new Point3D(point2D.X, point2D.Y, height));
                point3Ds_Floor.Insert(0, new Point3D(point2D.X, point2D.Y, 0));
            }

            SurfaceRoof? surfaceRoof = DiGi.Analytical.Building.Create.SurfaceRoof(SolarFixture_Face(new Vector3D(0, 0, 1), point3Ds_Roof));
            Assert.NotNull(surfaceRoof);
            components.Add(surfaceRoof);

            FaceFloor? faceFloor = DiGi.Analytical.Building.Create.FaceFloor(SolarFixture_Face(new Vector3D(0, 0, -1), point3Ds_Floor));
            Assert.NotNull(faceFloor);
            components.Add(faceFloor);

            BuildingModel buildingModel = new();
            Space space = new(new Point3D(SolarFixture_Origin.X + location.X, SolarFixture_Origin.Y + location.Y, height / 2), "Space 1");
            Assert.True(buildingModel.Update(space));

            foreach (IComponent component in components)
            {
                Assert.True(buildingModel.Assign(component, space));
            }

            Assert.True(Modify.UpdateBuildingInformation(buildingModel));

            return buildingModel;
        }

        /// <summary>
        /// Builds the stamped 10 m box of the solar facts: a convex building, whose surfaces neither shade nor block the view of one another.
        /// </summary>
        /// <returns>The box.</returns>
        private static BuildingModel SolarFixture_Box()
        {
            return SolarFixture_Prism([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)], 10, new Point2D(5, 5));
        }

        /// <summary>
        /// Builds a stamped 10 m high L-shaped building: a 24 × 20 m rectangle with its north-east 14 × 8 m corner removed. Its two inner walls - the north-facing one at y = 12 (140 m²) and the east-facing one at x = 10 (80 m²) - face the other wing, so the building shades and blocks the sky of its own surfaces there.
        /// <para>The sides have different lengths, so walls of one direction are told apart by area: south 240 m², east 120 m², outer north 100 m², west 200 m², roof 368 m².</para>
        /// </summary>
        /// <returns>The L-shaped building.</returns>
        private static BuildingModel SolarFixture_LShape()
        {
            return SolarFixture_Prism([new Point2D(0, 0), new Point2D(24, 0), new Point2D(24, 12), new Point2D(10, 12), new Point2D(10, 20), new Point2D(0, 20)], 10, new Point2D(5, 5));
        }

        /// <summary>
        /// Builds the shading model of a building alone - no surroundings, the building's walls and roofs as receivers - as a solve for <see cref="Constants.Default.SolarRadiusIsolated"/> builds it.
        /// </summary>
        /// <param name="buildingModel">The stamped building model.</param>
        /// <returns>A fresh shading model and the outward normals of its receivers.</returns>
        private static (ShadingModel ShadingModel, Dictionary<string, Vector3D> Normals) SolarFixture_IsolatedShadingModel(BuildingModel buildingModel)
        {
            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            ShadingModel? shadingModel = buildingModel.ToSolar(null, x => new GuidReference(x).ToString() is string reference && normals.ContainsKey(reference));
            Assert.NotNull(shadingModel);

            return (shadingModel, normals);
        }

        /// <summary>
        /// Reads the IMGW Warsaw weather file of <c>DiGi.Test/files</c>: the typical year of Warszawa-Okęcie (WMO 123750; ministry data, CC0 1.0).
        /// </summary>
        /// <returns>The EPW file.</returns>
        private static EPWFile SolarFixture_EPWFile()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));

            EPWFile? ePWFile = EPW.Modify.Read(path);
            Assert.NotNull(ePWFile);

            return ePWFile;
        }

        /// <summary>
        /// Finds the one surface that faces a direction and has a given area.
        /// </summary>
        /// <param name="surfaceSolarRadiations">The surfaces.</param>
        /// <param name="direction">The direction, as a unit vector.</param>
        /// <param name="area">The area the surface must have, in m², to tell apart walls of one direction.</param>
        /// <returns>The surface.</returns>
        private static GIS.Classes.SurfaceSolarRadiation SolarFixture_Surface(List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations, Vector3D direction, double area)
        {
            GIS.Classes.SurfaceSolarRadiation? result = null;
            foreach (GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation in surfaceSolarRadiations)
            {
                Vector3D? normal = surfaceSolarRadiation.Normal;
                Assert.NotNull(normal);
                if (normal * direction > 0.99 && System.Math.Abs(surfaceSolarRadiation.Area - area) < 1e-6)
                {
                    Assert.Null(result);
                    result = surfaceSolarRadiation;
                }
            }

            Assert.NotNull(result);
            return result;
        }

        private static PolygonalFace3D SolarFixture_Face(Vector3D normal, List<Point3D> point3Ds)
        {
            Polygon3D? polygon3D = Geometry.Spatial.Create.Polygon3D(normal, point3Ds);
            Assert.NotNull(polygon3D);

            PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(polygon3D);
            Assert.NotNull(polygonalFace3D);

            return polygonalFace3D;
        }
    }
}
