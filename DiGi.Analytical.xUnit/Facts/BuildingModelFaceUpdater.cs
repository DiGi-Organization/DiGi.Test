using DiGi.Analytical.Building;
using DiGi.Analytical.Building.Classes;
using DiGi.Core.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the functionality of the <see cref="BuildingModelFaceUpdater"/> to ensure that a face within a <see cref="BuildingModel"/> is correctly updated with new geometry.
        /// </summary>
        [Fact]
        public void BuildingModelFaceUpdater()
        {
            Plane? plane = Geometry.Spatial.Create.Plane(0.0);

            PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(plane,
            [
                new Geometry.Planar.Classes.Point2D(0, 0),
                new Geometry.Planar.Classes.Point2D(0, 10),
                new Geometry.Planar.Classes.Point2D(10, 10),
                new Geometry.Planar.Classes.Point2D(10, 0)
            ]);

            FaceFloor faceFloor = new(polygonalFace3D);

            Assert.NotNull(faceFloor.Geometry);

            BuildingModel buildingModel = new();
            buildingModel.Update(faceFloor);

            PolygonalFace3D? polygonalFace3D_New = Geometry.Spatial.Create.PolygonalFace3D(plane,
            [
                            new Geometry.Planar.Classes.Point2D(0, 0),
                            new Geometry.Planar.Classes.Point2D(0, 5),
                            new Geometry.Planar.Classes.Point2D(5, 5),
                            new Geometry.Planar.Classes.Point2D(5, 0)
            ]);

            GuidReference guidReference = new(faceFloor);

            Classes.Face face = new(guidReference, polygonalFace3D_New);

            BuildingModelFaceUpdater buildingModelFaceUpdater = new(buildingModel)
            {
                Face = face
            };

            Assert.True(buildingModelFaceUpdater.Update(out Building.Interfaces.IComponent? component));

            Assert.NotNull(component);

            FaceFloor? faceFloor_Updated = buildingModel.GetObject<FaceFloor>(guidReference);

            IPolygonalFace3D? polygonalFace3D_Component = faceFloor_Updated?.Geometry3D<IPolygonalFace3D>();

            Assert.NotNull(polygonalFace3D_Component);

            if (polygonalFace3D_Component is null)
            {
                return;
            }

            Assert.True(Core.Query.AlmostEquals(25, polygonalFace3D_Component.GetArea()));
        }
    }
}