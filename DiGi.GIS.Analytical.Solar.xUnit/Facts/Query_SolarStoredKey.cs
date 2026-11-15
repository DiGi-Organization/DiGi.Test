using DiGi.Analytical.Building.Classes;
using System;
using System.Collections.Generic;

namespace DiGi.GIS.Analytical.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the helpers that key a stored solar radiation result: the radius rounds to 0.1 m (midpoints away from zero) and the isolated radius is its own key, the building model id is positive, stable for one model - also after its JSON round trip - and different for a regenerated model, and the reference hours run 1 January 00:30 to 31 December 23:30 with indexes 0 to 8 759 and none outside the year.
        /// <para>Reference: DiGi.GIS.WebAPI.UI#74, DiGi-Organization/DiGi.GIS.Analytical#13.</para>
        /// </summary>
        [Fact]
        public void Query_SolarStoredKey()
        {
            Assert.Equal(50.0, Query.SolarRadiusKey(50.0));
            Assert.Equal(50.0, Query.SolarRadiusKey(50.04));
            Assert.Equal(50.1, Query.SolarRadiusKey(50.05));
            Assert.Equal(0.0, Query.SolarRadiusKey(0.04));
            Assert.Equal(Query.SolarRadiusKey(37.25), Query.SolarRadiusKey(37.25));
            Assert.Equal(0.0, Constants.Default.SolarRadiusIsolated);
            Assert.Equal(Constants.Default.SolarRadiusIsolated, Query.SolarRadiusKey(Constants.Default.SolarRadiusIsolated));

            BuildingModel buildingModel = SolarFixture_Box();
            long? buildingModelId = buildingModel.SolarBuildingModelId();
            Assert.NotNull(buildingModelId);
            Assert.True(buildingModelId > 0);
            Assert.Equal(buildingModelId, buildingModel.SolarBuildingModelId());

            // The id is the first eight Guid bytes read little-endian with the sign bit cleared, as the stored rows were keyed by the host.
            Assert.Equal(System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(buildingModel.Guid.ToByteArray()) & long.MaxValue, buildingModelId);

            BuildingModel? buildingModel_Read = Core.Convert.ToDiGi<BuildingModel>(Core.Convert.ToSystem_String(new List<BuildingModel>() { buildingModel }))?[0];
            Assert.NotNull(buildingModel_Read);
            Assert.Equal(buildingModelId, buildingModel_Read.SolarBuildingModelId());

            Assert.NotEqual(buildingModelId, SolarFixture_Box().SolarBuildingModelId());
            Assert.Null(((BuildingModel?)null).SolarBuildingModelId());

            List<DateTime> dateTimes = Query.SolarReferenceDateTimes();
            Assert.Equal(Constants.Default.SolarReferenceHourCount, dateTimes.Count);
            Assert.Equal(new DateTime(Constants.Default.SolarReferenceYear, 1, 1, 0, 30, 0), dateTimes[0]);
            Assert.Equal(new DateTime(Constants.Default.SolarReferenceYear, 12, 31, 23, 30, 0), dateTimes[^1]);
            for (int i = 0; i < dateTimes.Count; i++)
            {
                Assert.Equal(i, dateTimes[i].SolarReferenceHourIndex());
            }

            Assert.Equal(1, new DateTime(1992, 1, 1, 2, 0, 0).SolarReferenceDateTime()!.Value.SolarReferenceHourIndex());
            Assert.Null(new DateTime(Constants.Default.SolarReferenceYear + 1, 1, 1, 0, 30, 0).SolarReferenceHourIndex());
            Assert.Null(new DateTime(Constants.Default.SolarReferenceYear - 1, 12, 31, 23, 30, 0).SolarReferenceHourIndex());
        }

        /// <summary>
        /// Tests the mapping of EPW hour-ending time stamps to the mid-hour instants of the reference year the sun is sampled at.
        /// <para>Hour 1 of 1 January (01:00) covers 00:00-01:00 and maps to 00:30. Hour 24 of 31 December is stamped 1 January 00:00 of the next year and must still map to 31 December 23:30 of the reference year. The 29 February hours of a leap year are skipped, including hour 24, which is stamped 1 March 00:00.</para>
        /// </summary>
        [Fact]
        public void Query_SolarReferenceDateTime()
        {
            int year = Constants.Default.SolarReferenceYear;
            Assert.False(DateTime.IsLeapYear(year));

            Assert.Equal(new DateTime(year, 1, 1, 0, 30, 0), Query.SolarReferenceDateTime(EPW.Convert.ToSystem_DateTime(1995, 1, 1, 1, 0)));
            Assert.Equal(new DateTime(year, 12, 31, 23, 30, 0), Query.SolarReferenceDateTime(EPW.Convert.ToSystem_DateTime(2002, 12, 31, 24, 0)));
            Assert.Equal(new DateTime(year, 6, 21, 11, 30, 0), Query.SolarReferenceDateTime(EPW.Convert.ToSystem_DateTime(1988, 6, 21, 12, 60)));

            Assert.Null(Query.SolarReferenceDateTime(EPW.Convert.ToSystem_DateTime(2004, 2, 29, 12, 0)));
            Assert.Null(Query.SolarReferenceDateTime(EPW.Convert.ToSystem_DateTime(2004, 2, 29, 24, 0)));
            Assert.Equal(new DateTime(year, 3, 1, 0, 30, 0), Query.SolarReferenceDateTime(EPW.Convert.ToSystem_DateTime(2004, 3, 1, 1, 0)));
        }

        /// <summary>
        /// Verifies the stored form of a building's result: it carries the building's reference, the given radius - the isolated radius included - and <see cref="Constants.Default.SolarSolverVersion"/>, and it is refused when the model has no reference or a surface has no whole year of shading factors.
        /// </summary>
        [Fact]
        public void Create_BuildingSolarRadiationResult()
        {
            BuildingModel buildingModel = SolarFixture_Box();
            byte[] shadingFactors = new byte[Constants.Default.SolarReferenceHourCount];
            List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations = [new("S1", 10, new Geometry.Spatial.Classes.Vector3D(0, -1, 0), 1, 1, 500, 300, 150, 50, 520, 5000, shadingFactors)];

            Assert.Null(buildingModel.BuildingSolarRadiationResult(1465, Constants.Default.SolarRadiusIsolated, 16, surfaceSolarRadiations));

            Assert.True(buildingModel.SetValue(Enums.BuildingModelParameter.Reference, "B7", new Core.Parameter.Classes.SetValueSettings(true, false)));
            GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult = buildingModel.BuildingSolarRadiationResult(1465, Constants.Default.SolarRadiusIsolated, 16, surfaceSolarRadiations);
            Assert.NotNull(buildingSolarRadiationResult);
            Assert.Equal("B7", buildingSolarRadiationResult.Reference);
            Assert.Equal(1465, buildingSolarRadiationResult.CountyId);
            Assert.Equal(buildingModel.SolarBuildingModelId(), buildingSolarRadiationResult.BuildingModelId);
            Assert.Equal(Constants.Default.SolarRadiusIsolated, buildingSolarRadiationResult.Radius);
            Assert.Equal(Constants.Default.SolarSolverVersion, buildingSolarRadiationResult.SolverVersion);
            Assert.Equal(GIS.Enums.WeatherSourceType.EPW, buildingSolarRadiationResult.WeatherSourceType);
            Assert.Equal("16", buildingSolarRadiationResult.WeatherSourceId);

            List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations_Short = [new("S1", 10, new Geometry.Spatial.Classes.Vector3D(0, -1, 0), 1, 1, 500, 300, 150, 50, 520, 5000, new byte[10])];
            Assert.Null(buildingModel.BuildingSolarRadiationResult(1465, 50, 16, surfaceSolarRadiations_Short));
            Assert.Null(buildingModel.BuildingSolarRadiationResult(1465, 50, 16, []));
        }
    }
}
