using DiGi.Analytical.Building.Classes;
using DiGi.EPW;
using DiGi.EPW.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.WebAPI.UI.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the annual solar radiation of an unobstructed box in Warsaw over the IMGW year: one result per external wall and roof (the floor faces the soil and is no receiver), a flat roof that receives the annual global horizontal irradiation, and walls ranked by orientation.
        /// <para>A flat roof sees the whole sky and no ground, and nothing stands above it, so its irradiation is the beam on the horizontal plus the diffuse horizontal - the global horizontal sum of the file, within 3 % for the file's own inconsistency between the three columns. The south wall receives more than the east and west walls, which receive more than the north wall. Shading can only take radiation away, so no surface exceeds its unshaded twin, and the parts add up.</para>
        /// <para>Medium test (2.0 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiationResults()
        {
            BuildingModel buildingModel = SolarFixture_Box();
            EPWFile ePWFile = SolarFixture_EPWFile();

            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults = buildingModel.SurfaceSolarRadiationResults(null, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults);

            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            // Four walls and the roof; the floor is not a receiver.
            Assert.Equal(5, normals.Count);
            Assert.Equal(5, surfaceSolarRadiationResults.Count);

            double globalHorizontalIrradiation = 0;
            IList<DataRecord>? dataRecords = ePWFile.DataRecords;
            Assert.NotNull(dataRecords);
            foreach (DataRecord dataRecord in dataRecords)
            {
                globalHorizontalIrradiation += dataRecord.GlobalHorizontalRadiationValue() ?? 0;
            }

            globalHorizontalIrradiation /= 1000;

            SurfaceSolarRadiationResult surfaceSolarRadiationResult_Roof = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(0, 0, 1));
            Assert.InRange(surfaceSolarRadiationResult_Roof.Irradiation, globalHorizontalIrradiation * 0.97, globalHorizontalIrradiation * 1.03);
            Assert.Equal(0, surfaceSolarRadiationResult_Roof.Ground, 9);

            double south = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(0, -1, 0)).Irradiation;
            double east = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(1, 0, 0)).Irradiation;
            double west = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(-1, 0, 0)).Irradiation;
            double north = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(0, 1, 0)).Irradiation;

            Assert.True(south > east && south > west, $"south {south}, east {east}, west {west}");
            Assert.True(east > north && west > north, $"east {east}, west {west}, north {north}");

            foreach (SurfaceSolarRadiationResult surfaceSolarRadiationResult in surfaceSolarRadiationResults)
            {
                Assert.True(surfaceSolarRadiationResult.Irradiation <= surfaceSolarRadiationResult.IrradiationUnshaded + 1e-9);
                Assert.Equal(surfaceSolarRadiationResult.Irradiation, surfaceSolarRadiationResult.Beam + surfaceSolarRadiationResult.Diffuse + surfaceSolarRadiationResult.Ground, 9);
                Assert.Equal(surfaceSolarRadiationResult.Energy, surfaceSolarRadiationResult.Irradiation * surfaceSolarRadiationResult.Area, 6);
                Assert.Equal(100, surfaceSolarRadiationResult.Area, 6);
            }
        }
    }
}
