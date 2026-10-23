using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Solar.Classes;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The EPSG:2180 position of the solar fixtures' box: central Warsaw, where <c>GIS.Analytical.Modify.UpdateBuildingInformation</c> stamps coordinates and the IWEC fixture's station is.
        /// </summary>
        private static readonly Point2D SolarFixture_Origin = new(638000, 486000);

        /// <summary>
        /// Builds a clean closed box building of one space - west, east, south and north walls, a floor and a flat roof - standing on the ground at <paramref name="x"/>, <paramref name="y"/>.
        /// <para>X is easting and Y northing, so the walls face the four cardinal directions. The south wall is optionally split into vertical strips, each a receiver of its own, to push the receiver count above a limit.</para>
        /// </summary>
        /// <param name="x">The easting of the box's south-west corner, in metres.</param>
        /// <param name="y">The northing of the box's south-west corner, in metres.</param>
        /// <param name="size">The edge length of the box, in metres.</param>
        /// <param name="westStoredInward">When true the west wall is stored with its inward normal (+x), as many downloaded models store their walls.</param>
        /// <param name="southStripCount">The number of vertical strips the south wall is made of.</param>
        /// <returns>The six (or more) components, in the order west, east, north, floor, roof, then the south strips.</returns>
        private static List<IComponent> SolarFixture_BoxComponents(double x, double y, double size, bool westStoredInward = false, int southStripCount = 1)
        {
            Vector3D westNormal = westStoredInward ? new Vector3D(1, 0, 0) : new Vector3D(-1, 0, 0);
            Point3D westOrigin = westStoredInward ? new Point3D(x, y + size, size) : new Point3D(x, y, size);

            List<IComponent> result =
            [
                SolarFixture_Wall(westOrigin, westNormal, size, size),
                SolarFixture_Wall(new Point3D(x + size, y + size, size), new Vector3D(1, 0, 0), size, size),
                SolarFixture_Wall(new Point3D(x, y + size, size), new Vector3D(0, 1, 0), size, size),
                SolarFixture_Floor(new Point3D(x, y + size, 0), new Vector3D(0, 0, -1), size),
                SolarFixture_Roof(new Point3D(x, y, size), new Vector3D(0, 0, 1), size),
            ];

            // The south wall's local first axis runs west from its origin, its second downwards.
            double width = size / southStripCount;
            for (int i = 0; i < southStripCount; i++)
            {
                result.Add(SolarFixture_Wall(new Point3D(x + size - (i * width), y, size), new Vector3D(0, -1, 0), width, size));
            }

            return result;
        }

        /// <summary>
        /// Builds a building model of one space bounded by the given components.
        /// </summary>
        /// <param name="components">The components; each is assigned to the model's single space.</param>
        /// <param name="location">A point inside the space.</param>
        /// <returns>The building model, not yet stamped with coordinates.</returns>
        private static BuildingModel SolarFixture_BuildingModel(IEnumerable<IComponent> components, Point3D location)
        {
            BuildingModel buildingModel = new();
            Space space = new(location, "Space 1");
            Assert.True(buildingModel.Update(space));

            foreach (IComponent component in components)
            {
                Assert.True(buildingModel.Assign(component, space));
            }

            return buildingModel;
        }

        /// <summary>
        /// Builds the stamped 10 m box of the solar facts at <see cref="SolarFixture_Origin"/>.
        /// </summary>
        /// <param name="westStoredInward">When true the west wall is stored with its inward normal.</param>
        /// <returns>The box, stamped with its coordinates and time zone as the controller stamps a downloaded model.</returns>
        private static BuildingModel SolarFixture_Box(bool westStoredInward = false)
        {
            BuildingModel buildingModel = SolarFixture_BuildingModel(SolarFixture_BoxComponents(SolarFixture_Origin.X, SolarFixture_Origin.Y, 10, westStoredInward), new Point3D(SolarFixture_Origin.X + 5, SolarFixture_Origin.Y + 5, 5));
            Assert.True(GIS.Analytical.Modify.UpdateBuildingInformation(buildingModel));

            return buildingModel;
        }

        /// <summary>
        /// Reads the IMGW Warsaw weather file of <c>DiGi.Test/files</c>: the typical year of Warszawa-Okęcie (WMO 123750; ministry data, CC0 1.0), the station <c>gis/epwfile/item</c> serves for Warsaw. It replaced the ASHRAE-copyrighted IWEC file of the same station (DiGi-Organization/DiGi.Test#4); see <see cref="SolarFixture_EPWFile_SnowFiller"/> for that file's filler snow depth.
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
        /// Reads the IMGW Warsaw weather file with the snow filler of the IWEC Warsaw file written into it: a constant 3.0 cm of lying snow for every hour from April to November (DiGi-Organization/DiGi.Solar#2).
        /// <para>The IWEC file carried that filler and is ASHRAE-copyrighted, so it is no longer in the repository (DiGi-Organization/DiGi.Test#4); the filler is reproduced on the open IMGW file of the same station instead.</para>
        /// </summary>
        /// <returns>The EPW file.</returns>
        private static EPWFile SolarFixture_EPWFile_SnowFiller()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));

            string[] lines = System.IO.File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] values = lines[i].Split(',');
                if (values.Length >= 35 && int.TryParse(values[0], out _) && int.TryParse(values[1], out int month) && month is >= 4 and <= 11)
                {
                    values[30] = "3.0";
                    lines[i] = string.Join(",", values);
                }
            }

            string path_Temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + ".epw");
            try
            {
                System.IO.File.WriteAllLines(path_Temp, lines);

                EPWFile? ePWFile = EPW.Modify.Read(path_Temp);
                Assert.NotNull(ePWFile);

                return ePWFile;
            }
            finally
            {
                System.IO.File.Delete(path_Temp);
            }
        }

        /// <summary>
        /// The solver options the controller solves with.
        /// </summary>
        /// <returns>New options at <see cref="Constants.Default.SolarAngleTolerance"/>.</returns>
        private static ShadingSolverOptions SolarFixture_ShadingSolverOptions()
        {
            return new ShadingSolverOptions() { AngleTolerance = Constants.Default.SolarAngleTolerance };
        }

        /// <summary>
        /// Finds the result of the receiver whose outward normal is closest to a direction.
        /// </summary>
        /// <param name="surfaceSolarRadiationResults">The results.</param>
        /// <param name="normals">The outward normals of the receivers, by reference.</param>
        /// <param name="direction">The direction, as a unit vector.</param>
        /// <returns>The result.</returns>
        private static Classes.SurfaceSolarRadiationResult SolarFixture_Result(List<Classes.SurfaceSolarRadiationResult> surfaceSolarRadiationResults, Dictionary<string, Vector3D> normals, Vector3D direction)
        {
            Classes.SurfaceSolarRadiationResult? result = null;
            double dotProduct_Max = double.MinValue;
            foreach (Classes.SurfaceSolarRadiationResult surfaceSolarRadiationResult in surfaceSolarRadiationResults)
            {
                Assert.NotNull(surfaceSolarRadiationResult.Reference);
                Assert.True(normals.TryGetValue(surfaceSolarRadiationResult.Reference, out Vector3D? normal));

                double dotProduct = normal * direction;
                if (dotProduct > dotProduct_Max)
                {
                    dotProduct_Max = dotProduct;
                    result = surfaceSolarRadiationResult;
                }
            }

            Assert.NotNull(result);
            Assert.True(dotProduct_Max > 0.99, $"No receiver faces {direction}.");

            return result;
        }

        private static PolygonalFace3D SolarFixture_Face(Plane plane, params Point2D[] point2Ds)
        {
            PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(plane, point2Ds);
            Assert.NotNull(polygonalFace3D);

            return polygonalFace3D;
        }

        private static FaceFloor SolarFixture_Floor(Point3D origin, Vector3D normal, double size)
        {
            FaceFloor? faceFloor = DiGi.Analytical.Building.Create.FaceFloor(SolarFixture_Face(new Plane(origin, normal), new Point2D(0, 0), new Point2D(size, 0), new Point2D(size, size), new Point2D(0, size)));
            Assert.NotNull(faceFloor);

            return faceFloor;
        }

        private static SurfaceRoof SolarFixture_Roof(Point3D origin, Vector3D normal, double size)
        {
            SurfaceRoof? surfaceRoof = DiGi.Analytical.Building.Create.SurfaceRoof(SolarFixture_Face(new Plane(origin, normal), new Point2D(0, 0), new Point2D(size, 0), new Point2D(size, size), new Point2D(0, size)));
            Assert.NotNull(surfaceRoof);

            return surfaceRoof;
        }

        private static SurfaceWall SolarFixture_Wall(Point3D origin, Vector3D normal, double width, double height)
        {
            SurfaceWall? surfaceWall = DiGi.Analytical.Building.Create.SurfaceWall(SolarFixture_Face(new Plane(origin, normal), new Point2D(0, 0), new Point2D(width, 0), new Point2D(width, height), new Point2D(0, height)));
            Assert.NotNull(surfaceWall);

            return surfaceWall;
        }

        /// <summary>
        /// Builds a flat roof shaped as a regular polygon with many corners, whose triangulation alone is above a caster triangle limit: an n-gon triangulates into n - 2 triangles.
        /// </summary>
        /// <param name="center">The centre of the polygon.</param>
        /// <param name="radius">The radius of the polygon, in metres.</param>
        /// <param name="count">The number of corners.</param>
        /// <returns>The roof.</returns>
        private static SurfaceRoof SolarFixture_PolygonRoof(Point3D center, double radius, int count)
        {
            Point2D[] point2Ds = new Point2D[count];
            for (int i = 0; i < count; i++)
            {
                double angle = 2 * Math.PI * i / count;
                point2Ds[i] = new Point2D(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }

            SurfaceRoof? surfaceRoof = DiGi.Analytical.Building.Create.SurfaceRoof(SolarFixture_Face(new Plane(center, new Vector3D(0, 0, 1)), point2Ds));
            Assert.NotNull(surfaceRoof);

            return surfaceRoof;
        }
    }
}
