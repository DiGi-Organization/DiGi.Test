using DiGi.Analytical.Building.Classes;
using DiGi.EPW.Classes;
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
        /// Verifies <see cref="Query.MonthlyIrradiations(SolarHourSeries?, GIS.Classes.SurfaceSolarRadiation?)"/> against the period totals of the shaded box (DiGi-Organization/DiGi.GIS.Analytical#14):
        /// <para>- the 12 months of each surface add up to the <c>Irradiation</c> a rebuild from the same stored factors gives - the same hours, factors and math, summed per month;</para>
        /// <para>- they differ from the solved <c>Irradiation</c>, which used the exact factors, by no more than the quantization of the stored bytes (0.5/255 of the open-sky irradiation) and, for a shaded surface, by more than 0, so the comparison above is not trivially the solved total;</para>
        /// <para>- with no factors and visibilities of 1 the months add up to <c>IrradiationUnshaded</c>, which does not depend on the factors.</para>
        /// </summary>
        [MediumFact]
        public void Query_MonthlyIrradiations()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            BuildingModel buildingModel = SolarFixture_Box();
            (ShadingModel shadingModel, Dictionary<string, Vector3D> normals) = Create_SurfaceSolarRadiations_ShadedBox(buildingModel);
            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations = shadingModel.SurfaceSolarRadiations(normals, ePWFile, Create.SolarShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiations);

            GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult = new("B7", 1465, 1, 50, Constants.Default.SolarSolverVersion, GIS.Enums.WeatherSourceType.EPW, "16", null, null, null, null, surfaceSolarRadiations);
            BuildingInformation buildingInformation = buildingModel.BuildingInformation;

            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations_Rebuilt = buildingSolarRadiationResult.SurfaceSolarRadiations(buildingInformation, ePWFile);
            Assert.NotNull(surfaceSolarRadiations_Rebuilt);

            SolarHourSeries? solarHourSeries = ((Weather.Classes.Weather)ePWFile).SolarHourSeries(buildingInformation.Coordinates, buildingInformation.UTC);
            Assert.NotNull(solarHourSeries);

            int count_Quantized = 0;
            for (int i = 0; i < surfaceSolarRadiations.Count; i++)
            {
                GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation = surfaceSolarRadiations[i];

                double[]? monthlyIrradiations = solarHourSeries.MonthlyIrradiations(surfaceSolarRadiation);
                Assert.NotNull(monthlyIrradiations);
                Assert.Equal(12, monthlyIrradiations.Length);
                Assert.All(monthlyIrradiations, x => Assert.True(x >= 0));

                double sum = monthlyIrradiations.Sum();
                Assert.Equal(surfaceSolarRadiations_Rebuilt[i].Irradiation, sum, 9);

                double difference = Math.Abs(sum - surfaceSolarRadiation.Irradiation);
                Assert.True(difference <= 0.5 / 255 * surfaceSolarRadiation.IrradiationUnshaded, $"{surfaceSolarRadiation.Reference}: monthly sum {sum}, solved {surfaceSolarRadiation.Irradiation} kWh/m²");
                if (difference > 1e-9)
                {
                    count_Quantized++;
                }

                double[]? monthlyIrradiations_Unshaded = solarHourSeries.MonthlyIrradiations(surfaceSolarRadiation.Area, surfaceSolarRadiation.Normal, 1, 1, null);
                Assert.NotNull(monthlyIrradiations_Unshaded);
                Assert.Equal(surfaceSolarRadiation.IrradiationUnshaded, monthlyIrradiations_Unshaded.Sum(), 9);
            }

            Assert.True(count_Quantized > 0, "No surface differs from its solved total, so the rebuilt comparison could be the solved one.");

            // The roof gets more in June than in December in Warsaw.
            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_Roof = surfaceSolarRadiations.First(x => x.Normal!.Z > 0.9);
            double[]? monthlyIrradiations_Roof = solarHourSeries.MonthlyIrradiations(surfaceSolarRadiation_Roof);
            Assert.NotNull(monthlyIrradiations_Roof);
            Assert.True(monthlyIrradiations_Roof[5] > 3 * monthlyIrradiations_Roof[11]);

            Assert.Null(solarHourSeries.MonthlyIrradiations(null));
            Assert.Null(((SolarHourSeries?)null).MonthlyIrradiations(surfaceSolarRadiation_Roof));
            Assert.Null(solarHourSeries.MonthlyIrradiations(0, surfaceSolarRadiation_Roof.Normal, 1, 1, null));
            Assert.Null(solarHourSeries.MonthlyIrradiations(1, surfaceSolarRadiation_Roof.Normal, 1, 1, new double[10]));
        }
    }
}
