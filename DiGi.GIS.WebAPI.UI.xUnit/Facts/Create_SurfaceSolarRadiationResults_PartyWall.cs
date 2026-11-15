using DiGi.Analytical.Building.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.WebAPI.UI.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a wall fully covered by an adjacent building receives no solar radiation - no beam, no sky diffuse, no ground-reflected - while the building's other surfaces are unaffected (DiGi-Organization/DiGi.GIS.WebAPI.UI#61).
        /// <para>The 10 m box is solved twice: alone, and with an identical neighbour building to its west whose east wall lies on the box's west wall. Before #61 the covered wall kept its full diffuse and ground-reflected radiation (on gis.digiproject.uk, 436.5 of 445.0 kWh/m² for such a wall), because only the beam was shaded.</para>
        /// <para>The neighbour lies in or behind the planes of the east, north and south walls and below the roof's plane, so those surfaces must give identical results and keep an open view. The covered wall keeps its open-sky irradiation.</para>
        /// <para>Medium test (5.9 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiationResults_PartyWall()
        {
            BuildingModel buildingModel = SolarFixture_Box();
            EPWFile ePWFile = SolarFixture_EPWFile();

            BuildingModel buildingModel_Neighbour = SolarFixture_BuildingModel(SolarFixture_BoxComponents(SolarFixture_Origin.X - 10, SolarFixture_Origin.Y, 10), new Point3D(SolarFixture_Origin.X - 5, SolarFixture_Origin.Y + 5, 5));
            Assert.True(GIS.Analytical.Modify.UpdateBuildingInformation(buildingModel_Neighbour));

            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults = buildingModel.SurfaceSolarRadiationResults(null, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults);

            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults_Neighbour = buildingModel.SurfaceSolarRadiationResults([buildingModel_Neighbour], ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults_Neighbour);
            Assert.Equal(surfaceSolarRadiationResults.Count, surfaceSolarRadiationResults_Neighbour.Count);

            SurfaceSolarRadiationResult west = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(-1, 0, 0));
            SurfaceSolarRadiationResult west_Neighbour = SolarFixture_Result(surfaceSolarRadiationResults_Neighbour, normals, new Vector3D(-1, 0, 0));

            Assert.True(west.Irradiation > 300, $"west alone {west.Irradiation}");
            Assert.True(west_Neighbour.Irradiation < 1, $"west covered: {west_Neighbour.Irradiation} (beam {west_Neighbour.Beam}, diffuse {west_Neighbour.Diffuse}, ground {west_Neighbour.Ground})");
            Assert.True(west_Neighbour.SkyVisibility <= 0.01, $"west sky visibility {west_Neighbour.SkyVisibility}");
            Assert.True(west_Neighbour.GroundVisibility <= 0.01, $"west ground visibility {west_Neighbour.GroundVisibility}");
            Assert.Equal(west.IrradiationUnshaded, west_Neighbour.IrradiationUnshaded, 9);

            foreach (Vector3D direction in new Vector3D[] { new(1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1) })
            {
                SurfaceSolarRadiationResult open = SolarFixture_Result(surfaceSolarRadiationResults, normals, direction);
                SurfaceSolarRadiationResult open_Neighbour = SolarFixture_Result(surfaceSolarRadiationResults_Neighbour, normals, direction);

                Assert.Equal(open.Irradiation, open_Neighbour.Irradiation, 9);
                Assert.Equal(1.0, open_Neighbour.SkyVisibility);
                Assert.Equal(1.0, open_Neighbour.GroundVisibility);
            }
        }
    }
}
