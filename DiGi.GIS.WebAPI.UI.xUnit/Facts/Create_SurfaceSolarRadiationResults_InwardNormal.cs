using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Solar;
using DiGi.Core.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.WebAPI.UI.Classes;
using DiGi.Solar.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a wall stored with its normal pointing into the building gets the results of its outward side: a box whose west wall is stored inward gives the same results as the box stored outward.
        /// <para>A component's stored normal is its drawing orientation, and many downloaded models store their walls inward. The irradiance of a surface depends on which way it faces, so the calculation takes the outward normals of the external envelope. The fact also shows what it guards against: solving the inward box with the west wall's stored normal gives that wall the east wall's morning sun instead of its own afternoon sun.</para>
        /// <para>Medium test (6.1 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiationResults_InwardNormal()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            BuildingModel buildingModel = SolarFixture_Box(false);
            BuildingModel buildingModel_Inward = SolarFixture_Box(true);

            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults = buildingModel.SurfaceSolarRadiationResults(null, ePWFile, SolarFixture_ShadingSolverOptions());
            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults_Inward = buildingModel_Inward.SurfaceSolarRadiationResults(null, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults);
            Assert.NotNull(surfaceSolarRadiationResults_Inward);
            Assert.Equal(surfaceSolarRadiationResults.Count, surfaceSolarRadiationResults_Inward.Count);

            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Dictionary<string, Vector3D>? normals_Inward = buildingModel_Inward.SolarReceiverNormals();
            Assert.NotNull(normals);
            Assert.NotNull(normals_Inward);

            // The components differ in guid, so the surfaces are matched by the way they face.
            Vector3D[] directions = [new Vector3D(-1, 0, 0), new Vector3D(1, 0, 0), new Vector3D(0, -1, 0), new Vector3D(0, 1, 0), new Vector3D(0, 0, 1)];
            foreach (Vector3D direction in directions)
            {
                SurfaceSolarRadiationResult surfaceSolarRadiationResult = SolarFixture_Result(surfaceSolarRadiationResults, normals, direction);
                SurfaceSolarRadiationResult surfaceSolarRadiationResult_Inward = SolarFixture_Result(surfaceSolarRadiationResults_Inward, normals_Inward, direction);

                Assert.Equal(surfaceSolarRadiationResult.Irradiation, surfaceSolarRadiationResult_Inward.Irradiation, 9);
                Assert.Equal(surfaceSolarRadiationResult.Beam, surfaceSolarRadiationResult_Inward.Beam, 9);
            }

            // The stored normal of the inward west wall points east; with it, the wall would read as an east wall.
            SurfaceSolarRadiationResult west_Inward = SolarFixture_Result(surfaceSolarRadiationResults_Inward, normals_Inward, new Vector3D(-1, 0, 0));
            Assert.NotNull(west_Inward.Reference);

            ShadingModel? shadingModel = buildingModel_Inward.ToSolar(null, x => new GuidReference(x).ToString() is string reference && normals_Inward.ContainsKey(reference));
            Assert.NotNull(shadingModel);

            List<ShadingElement>? shadingElements = shadingModel.GetShadingElements<ShadingElement>(false);
            Assert.NotNull(shadingElements);
            ShadingElement? shadingElement_West = shadingElements.Find(x => x.Reference == west_Inward.Reference);
            Assert.NotNull(shadingElement_West);

            Vector3D? normal_Stored = shadingElement_West.PolygonalFace3D?.Plane?.Normal;
            Assert.NotNull(normal_Stored);
            Assert.True(normal_Stored.X > 0.99, $"The west wall's stored normal is {normal_Stored}, not inward.");

            Dictionary<string, Vector3D> normals_Stored = new(normals_Inward)
            {
                [west_Inward.Reference] = normal_Stored,
            };

            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults_Stored = shadingModel.SurfaceSolarRadiationResults(normals_Stored, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults_Stored);

            SurfaceSolarRadiationResult? west_Stored = surfaceSolarRadiationResults_Stored.Find(x => x.Reference == west_Inward.Reference);
            Assert.NotNull(west_Stored);
            Assert.True(System.Math.Abs(west_Stored.Irradiation - west_Inward.Irradiation) > 1, $"west {west_Inward.Irradiation} outward, {west_Stored.Irradiation} with the stored normal");
        }
    }
}
