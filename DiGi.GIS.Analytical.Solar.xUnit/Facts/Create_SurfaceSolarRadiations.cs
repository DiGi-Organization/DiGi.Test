using DiGi.Analytical.Building.Classes;
using DiGi.Core.Classes;
using DiGi.Core.Enums;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar.Classes;
using DiGi.Solar.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.Analytical.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the stored form of a solve, <c>Create.SurfaceSolarRadiations(ShadingModel, …)</c>: one surface per receiver, each with a shading factor for every hour of the solar reference year and none missing, night hours 0, and a screen south of the box really shading it.
        /// <para>Moved with the solve pipeline from DiGi.GIS.WebAPI.UI (DiGi-Organization/DiGi.GIS.Analytical#13); the parity with the host's own summary is covered there (DiGi.GIS.WebAPI.UI#74).</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiations()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            (ShadingModel shadingModel, Dictionary<string, Vector3D> normals) = Create_SurfaceSolarRadiations_ShadedBox(SolarFixture_Box());
            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations = shadingModel.SurfaceSolarRadiations(normals, ePWFile, Create.SolarShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiations);
            Assert.Equal(normals.Count, surfaceSolarRadiations.Count);

            int count_Shaded = 0;
            foreach (GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation in surfaceSolarRadiations)
            {
                Assert.NotNull(surfaceSolarRadiation.Normal);
                Assert.True(surfaceSolarRadiation.Irradiation > 0);
                Assert.Equal(surfaceSolarRadiation.Irradiation * surfaceSolarRadiation.Area, surfaceSolarRadiation.Energy, 6);

                byte[]? shadingFactors = surfaceSolarRadiation.ShadingFactors;
                Assert.NotNull(shadingFactors);
                Assert.Equal(Constants.Default.SolarReferenceHourCount, shadingFactors.Length);
                count_Shaded += shadingFactors.Count(x => x > 0);

                // Midnight on 1 January is a night hour.
                Assert.Equal(0, shadingFactors[0]);
            }

            Assert.True(count_Shaded > 500, $"{count_Shaded} shaded receiver-hours");
        }

        /// <summary>
        /// Verifies the rebuild of a stored result for another weather series, <c>Create.SurfaceSolarRadiations(BuildingSolarRadiationResult, BuildingInformation, Weather)</c>: from the stored uint8 factors, normals and visibilities alone - no shading model - it reproduces the solved irradiation of every surface within 0.5 %, and it is refused, not guessed, when a stored surface has no factors.
        /// <para>Reference: DiGi.GIS.WebAPI.UI#73, #74.</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiations_Rebuild()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            BuildingModel buildingModel = SolarFixture_Box();
            (ShadingModel shadingModel, Dictionary<string, Vector3D> normals) = Create_SurfaceSolarRadiations_ShadedBox(buildingModel);
            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations = shadingModel.SurfaceSolarRadiations(normals, ePWFile, Create.SolarShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiations);

            GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult = new("B7", 1465, 1, 50, Constants.Default.SolarSolverVersion, GIS.Enums.WeatherSourceType.EPW, "16", null, null, null, null, surfaceSolarRadiations);
            BuildingInformation buildingInformation = buildingModel.BuildingInformation;

            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations_Rebuilt = buildingSolarRadiationResult.SurfaceSolarRadiations(buildingInformation, (Weather.Classes.Weather)ePWFile);
            Assert.NotNull(surfaceSolarRadiations_Rebuilt);
            Assert.Equal(surfaceSolarRadiations.Count, surfaceSolarRadiations_Rebuilt.Count);

            for (int i = 0; i < surfaceSolarRadiations.Count; i++)
            {
                GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation = surfaceSolarRadiations[i];
                GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_Rebuilt = surfaceSolarRadiations_Rebuilt[i];
                Assert.Equal(surfaceSolarRadiation.Reference, surfaceSolarRadiation_Rebuilt.Reference);
                Assert.True(Math.Abs(surfaceSolarRadiation_Rebuilt.Irradiation - surfaceSolarRadiation.Irradiation) <= 0.005 * surfaceSolarRadiation.Irradiation, $"{surfaceSolarRadiation.Reference}: rebuilt {surfaceSolarRadiation_Rebuilt.Irradiation} kWh/m², solved {surfaceSolarRadiation.Irradiation} kWh/m²");
                Assert.Equal(surfaceSolarRadiation.IrradiationUnshaded, surfaceSolarRadiation_Rebuilt.IrradiationUnshaded, 9);
                Assert.Equal(surfaceSolarRadiation.ShadingFactors, surfaceSolarRadiation_Rebuilt.ShadingFactors);
            }

            // A stored surface without factors cannot be rebuilt.
            List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations_NoFactors = [new(surfaceSolarRadiations[0].Reference, 10, surfaceSolarRadiations[0].Normal, 1, 1, 0, 0, 0, 0, 0, 0, null)];
            GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_NoFactors = new("B7", 1465, 1, 50, Constants.Default.SolarSolverVersion, GIS.Enums.WeatherSourceType.EPW, "16", null, null, null, null, surfaceSolarRadiations_NoFactors);
            Assert.Null(buildingSolarRadiationResult_NoFactors.SurfaceSolarRadiations(buildingInformation, ePWFile));
        }

        /// <summary>
        /// Verifies that the hourly series both the solve and the rebuild aggregate through reads a missing albedo (the sentinel 999) as the default albedo, a measured one as it is, skips a record missing a radiation reading, keeps the first record of each reference hour, and refuses the unstamped (0, 0) location.
        /// <para>Reference: DiGi.Weather#2, DiGi.GIS.WebAPI.UI#74.</para>
        /// </summary>
        [Fact]
        public void Create_SolarHourSeries_MissingValues()
        {
            DateTime dateTime = new(1992, 6, 21, 13, 0, 0, DateTimeKind.Unspecified);
            Weather.Classes.Weather weather = new(
            [
                Create_SolarHourSeries_Record(dateTime, 800, 999),
                Create_SolarHourSeries_Record(dateTime.AddHours(1), 700, 0.3f),
                Create_SolarHourSeries_Record(dateTime.AddHours(2), 9999, 0.3f),
                Create_SolarHourSeries_Record(dateTime.AddHours(1), 600, 0.4f),
            ]);

            Coordinates? coordinates = GIS.Query.Coordinates(new Point2D(SolarFixture_Origin.X, SolarFixture_Origin.Y));
            Assert.NotNull(coordinates);

            SolarHourSeries? solarHourSeries = weather.SolarHourSeries(coordinates, UTC.Plus0100);
            Assert.NotNull(solarHourSeries);
            Assert.Equal(2, solarHourSeries.Count);
            Assert.Equal(DiGi.Solar.Query.Albedo(null, false), solarHourSeries.Albedos[0]);
            Assert.Equal(0.3f, solarHourSeries.Albedos[1], 6);
            Assert.Equal(700, solarHourSeries.GlobalHorizontalRadiations[1]);
            Assert.All(solarHourSeries.SunUps, Assert.True);

            Assert.Null(weather.SolarHourSeries(new Coordinates(0, 0), UTC.Plus0100));
            Assert.Null(((Weather.Classes.Weather?)null).SolarHourSeries(coordinates, UTC.Plus0100));
        }

        // A summer weather record with the given global radiation and albedo; the direct and diffuse readings are present.
        private static Weather.Classes.WeatherRecord Create_SolarHourSeries_Record(DateTime dateTime, float globalHorizontalRadiation, float albedo)
        {
            return new Weather.Classes.WeatherRecord(dateTime, null, 25, 10, 50, 101325, 0, 0, 350, globalHorizontalRadiation, 600, 150, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, 0, 0, 0, 0, albedo, 0, 0);
        }

        // The building alone, shaded by a screen south of it, as a fresh shading model: the solver writes its results into
        // the model, so every solve of a fact gets its own.
        private static (ShadingModel ShadingModel, Dictionary<string, Vector3D> Normals) Create_SurfaceSolarRadiations_ShadedBox(BuildingModel buildingModel)
        {
            (ShadingModel shadingModel, Dictionary<string, Vector3D> normals) = SolarFixture_IsolatedShadingModel(buildingModel);

            PolygonalFace3D? polygonalFace3D_Screen = Geometry.Spatial.Create.PolygonalFace3D(new Plane(new Point3D(SolarFixture_Origin.X + 5, SolarFixture_Origin.Y - 5, 20), new Vector3D(0, 1, 0)), [new Point2D(-40, -40), new Point2D(40, -40), new Point2D(40, 40), new Point2D(-40, 40)]);
            Assert.NotNull(polygonalFace3D_Screen);
            Assert.True(shadingModel.Update(new ShadingElement("Screen", polygonalFace3D_Screen, true)));

            return (shadingModel, normals);
        }
    }
}
