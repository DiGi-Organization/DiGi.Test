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
        /// Tests that a filler snow depth does not turn the ground into snow: a wall's ground-reflected irradiation uses the file's own albedo, resolved without snow cover, in every hour - never the 0.7 snow albedo.
        /// <para>The IWEC Warsaw file that <c>gis/epwfile/item</c> served for Warsaw reported lying snow for most hours of the year, a filler value (DiGi-Organization/DiGi.Solar#2). Deriving snow cover from it applied the 0.7 snow albedo almost all year and read a north wall at 658 kWh/m² instead of 410 (DiGi.GIS.WebAPI.UI#59, comment 5830021444). That file is ASHRAE-copyrighted and no longer in the repository (DiGi-Organization/DiGi.Test#4), so the fact runs on the IMGW file of the same station with the filler written into it (<see cref="SolarFixture_EPWFile_SnowFiller"/>), and first asserts the filler is there, so it keeps testing the case it exists for. A vertical wall sees half the ground, so its ground component is the sum over the hours of the global horizontal irradiation times the hour's albedo / 2.</para>
        /// <para>Medium test (2.0 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiationResults_SnowFiller()
        {
            BuildingModel buildingModel = SolarFixture_Box();
            EPWFile ePWFile = SolarFixture_EPWFile_SnowFiller();

            IList<DataRecord>? dataRecords = ePWFile.DataRecords;
            Assert.NotNull(dataRecords);

            int count_Snow = 0;
            double groundIrradiation_Expected = 0;
            foreach (DataRecord dataRecord in dataRecords)
            {
                if (dataRecord.SnowDepthValue() > 0)
                {
                    count_Snow++;
                }

                if (dataRecord.GlobalHorizontalRadiationValue() is float globalHorizontalRadiation && dataRecord.DirectNormalRadiationValue() is not null && dataRecord.DiffuseHorizontalRadiationValue() is not null && Query.SolarReferenceDateTime(dataRecord.DateTime) is not null)
                {
                    // The albedo resolved without snow cover - the decision for a file whose snow depth is a filler.
                    groundIrradiation_Expected += globalHorizontalRadiation * Solar.Query.Albedo(dataRecord.AlbedoValue(), false) * 0.5;
                }
            }

            Assert.True(count_Snow > 4000, $"The fixture reports snow for {count_Snow} hours only; it no longer carries the filler this fact is about.");

            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults = buildingModel.SurfaceSolarRadiationResults(null, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults);

            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            double ground_Expected = groundIrradiation_Expected / 1000;

            SurfaceSolarRadiationResult north = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(0, 1, 0));
            Assert.Equal(ground_Expected, north.Ground, 6);
        }
    }
}
