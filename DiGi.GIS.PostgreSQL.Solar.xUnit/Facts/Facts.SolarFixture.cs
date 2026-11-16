using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using System.Collections.Generic;
using System.Reflection;

namespace DiGi.GIS.PostgreSQL.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The EPSG:2180 position of the solar fixtures: central Warsaw, where <c>GIS.Analytical.Modify.UpdateBuildingInformation</c> stamps coordinates and the IMGW fixture's station is. The same as the solar fixtures of <c>DiGi.GIS.Analytical.Solar.xUnit</c>.
        /// </summary>
        private static readonly Point2D SolarFixture_Origin = new(638000, 486000);

        /// <summary>
        /// Builds a stamped rectangular prism building of one space: four walls, a floor and a flat roof, every face built from its 3D corners with its outward normal.
        /// </summary>
        /// <param name="reference">The <c>building_2d</c> reference the model is stamped with, which its stored result is keyed by.</param>
        /// <param name="x">The west edge, in metres east of <see cref="SolarFixture_Origin"/>.</param>
        /// <param name="y">The south edge, in metres north of <see cref="SolarFixture_Origin"/>.</param>
        /// <param name="width">The east-west length, in metres.</param>
        /// <param name="depth">The north-south length, in metres.</param>
        /// <param name="height">The height, in metres.</param>
        /// <returns>The building model, stamped with its coordinates and time zone as a downloaded model is, and with its reference.</returns>
        private static BuildingModel SolarFixture_Box(string reference, double x, double y, double width, double depth, double height)
        {
            List<Point2D> point2Ds =
            [
                new Point2D(SolarFixture_Origin.X + x, SolarFixture_Origin.Y + y),
                new Point2D(SolarFixture_Origin.X + x + width, SolarFixture_Origin.Y + y),
                new Point2D(SolarFixture_Origin.X + x + width, SolarFixture_Origin.Y + y + depth),
                new Point2D(SolarFixture_Origin.X + x, SolarFixture_Origin.Y + y + depth),
            ];

            List<IComponent> components = [];
            for (int i = 0; i < point2Ds.Count; i++)
            {
                Point2D point2D_1 = point2Ds[i];
                Point2D point2D_2 = point2Ds[(i + 1) % point2Ds.Count];

                Vector3D normal = new Vector3D(point2D_2.Y - point2D_1.Y, point2D_1.X - point2D_2.X, 0).Unit!;
                SurfaceWall? surfaceWall = DiGi.Analytical.Building.Create.SurfaceWall(SolarFixture_Face(normal, [new Point3D(point2D_1.X, point2D_1.Y, 0), new Point3D(point2D_2.X, point2D_2.Y, 0), new Point3D(point2D_2.X, point2D_2.Y, height), new Point3D(point2D_1.X, point2D_1.Y, height)]));
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
            Space space = new(new Point3D(SolarFixture_Origin.X + x + (width / 2), SolarFixture_Origin.Y + y + (depth / 2), height / 2), "Space 1");
            Assert.True(buildingModel.Update(space));

            foreach (IComponent component in components)
            {
                Assert.True(buildingModel.Assign(component, space));
            }

            Assert.True(GIS.Analytical.Modify.UpdateBuildingInformation(buildingModel));
            Assert.True(buildingModel.SetValue(GIS.Analytical.Enums.BuildingModelParameter.Reference, reference, new Core.Parameter.Classes.SetValueSettings(true, false)));

            return buildingModel;
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
        /// Gets a building model with its planar bounding box, as the batch solve hands a neighbour to the solve.
        /// </summary>
        /// <param name="buildingModel">The building model.</param>
        /// <returns>The model and its planar bounding box.</returns>
        private static (BuildingModel BuildingModel, double XMin, double YMin, double XMax, double YMax) SolarFixture_Neighbour(BuildingModel buildingModel)
        {
            BoundingBox3D? boundingBox3D = buildingModel.GetBoundingBox();
            Assert.NotNull(boundingBox3D);
            Assert.NotNull(boundingBox3D.Min);
            Assert.NotNull(boundingBox3D.Max);

            return (buildingModel, boundingBox3D.Min.X, boundingBox3D.Min.Y, boundingBox3D.Max.X, boundingBox3D.Max.Y);
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
