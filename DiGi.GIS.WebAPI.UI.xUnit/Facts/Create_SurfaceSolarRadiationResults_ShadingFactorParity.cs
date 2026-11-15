using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Solar;
using DiGi.Core.Classes;
using DiGi.EPW;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.WebAPI.UI.Classes;
using DiGi.Solar.Classes;
using DiGi.Solar.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that the bulk shading factor read-out (<c>ShadingModel.TryGetShadingFactors</c>, DiGi.GIS.WebAPI.UI#63) gives the beam irradiation the cloning per-receiver read-out gave, on the box behind the 60 m screen of <see cref="Create_SurfaceSolarRadiationResults_Surroundings"/>.
        /// <para>The reference is the former aggregation, rebuilt here: <c>GetShadingSolverResults</c> per receiver, shaded area over face area clamped to [0, 1], and the beam of <c>SolarPowerResult_ByShadingFactor</c> summed over the same EPW hours. Two bounds, not the max alone: the 95th percentile and a loose max of the per-hour beam difference between the two read-outs over every receiver and hour, and each receiver's annual beam against the served result. Diffuse and ground do not depend on the shading factor and are left to the other facts.</para>
        /// <para>The screen shades the south wall at most daytime hours, so a wrong lookup or a wrong clamp bound changes its beam. Measured: 19 168 shaded receiver-hours, no raw factor outside [0, 1], and every annual beam equal to the reference bit for bit, so a clamp that is only missing cannot be observed on this fixture - it guards against an overshoot of a few ulps.</para>
        /// <para>Medium test (7.7 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiationResults_ShadingFactorParity()
        {
            BuildingModel buildingModel = SolarFixture_Box();
            EPWFile ePWFile = SolarFixture_EPWFile();

            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            ShadingModel? shadingModel = buildingModel.ToSolar(null, x => new GuidReference(x).ToString() is string reference && normals.ContainsKey(reference));
            Assert.NotNull(shadingModel);

            PolygonalFace3D? polygonalFace3D_Screen = Geometry.Spatial.Create.PolygonalFace3D(new Plane(new Point3D(SolarFixture_Origin.X + 5, SolarFixture_Origin.Y - 5, 20), new Vector3D(0, 1, 0)), [new Point2D(-40, -40), new Point2D(40, -40), new Point2D(40, 40), new Point2D(-40, 40)]);
            Assert.NotNull(polygonalFace3D_Screen);
            Assert.True(shadingModel.Update(new ShadingElement("Screen", polygonalFace3D_Screen, true)));

            // Solves the model in place: its stored results are what both read-outs read.
            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults = shadingModel.SurfaceSolarRadiationResults(normals, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults);
            Assert.Equal(normals.Count, surfaceSolarRadiationResults.Count);

            // The EPW hours, sampled as the aggregation samples them.
            List<(DateTime DateTime, double GlobalHorizontal, double DirectNormal, double DiffuseHorizontal, double Albedo)> hours = [];
            HashSet<DateTime> dateTimes_Unique = [];
            IList<DataRecord>? dataRecords = ePWFile.DataRecords;
            Assert.NotNull(dataRecords);
            foreach (DataRecord dataRecord in dataRecords)
            {
                float? globalHorizontalRadiation = dataRecord.GlobalHorizontalRadiationValue();
                float? directNormalRadiation = dataRecord.DirectNormalRadiationValue();
                float? diffuseHorizontalRadiation = dataRecord.DiffuseHorizontalRadiationValue();
                DateTime? dateTime = dataRecord.DateTime.SolarReferenceDateTime();
                if (globalHorizontalRadiation is null || directNormalRadiation is null || diffuseHorizontalRadiation is null || dateTime is null || !dateTimes_Unique.Add(dateTime.Value))
                {
                    continue;
                }

                hours.Add((dateTime.Value, globalHorizontalRadiation.Value, directNormalRadiation.Value, diffuseHorizontalRadiation.Value, Solar.Query.Albedo(dataRecord.AlbedoValue(), false)));
            }

            List<double> differences_Hour = [];
            int count_Shaded = 0;
            int count_OutOfRange = 0;

            List<IShadingElement>? shadingElements = shadingModel.GetShadingElements<IShadingElement>(false);
            Assert.NotNull(shadingElements);
            foreach (IShadingElement shadingElement in shadingElements)
            {
                if (shadingElement.Reference is not string reference || !normals.TryGetValue(reference, out Vector3D? normal) || normal is null)
                {
                    continue;
                }

                IPolygonalFace3D? polygonalFace3D = shadingElement.PolygonalFace3D;
                Assert.NotNull(polygonalFace3D);
                double area = polygonalFace3D.GetArea();

                // The former read-out: cloned results, shaded area over face area.
                List<IShadingSolverResult>? shadingSolverResults = shadingModel.GetShadingSolverResults<IShadingSolverResult>(shadingElement);
                Assert.NotNull(shadingSolverResults);
                Dictionary<DateTime, double> areas_Shaded = [];
                foreach (IShadingSolverResult shadingSolverResult in shadingSolverResults)
                {
                    areas_Shaded[shadingSolverResult.DateTime] = shadingSolverResult.Area;
                }

                Assert.True(shadingModel.TryGetShadingFactors(shadingElement, out Dictionary<DateTime, double>? shadingFactors));
                Assert.NotNull(shadingFactors);

                double beam_Reference = 0;
                foreach ((DateTime dateTime, double globalHorizontal, double directNormal, double diffuseHorizontal, double albedo) in hours)
                {
                    Vector3D? sunDirection_Day = Solar.Query.SunDirection(shadingModel, dateTime, false);
                    bool sunUp = sunDirection_Day is not null;

                    double shadingFactor = 0;
                    if (sunUp)
                    {
                        if (!areas_Shaded.TryGetValue(dateTime, out double area_Shaded))
                        {
                            continue;
                        }

                        double shadingFactor_Raw = area_Shaded / area;
                        if (shadingFactor_Raw < 0 || shadingFactor_Raw > 1)
                        {
                            count_OutOfRange++;
                        }

                        shadingFactor = Math.Clamp(shadingFactor_Raw, 0, 1);
                        if (shadingFactor > 0)
                        {
                            count_Shaded++;
                        }
                    }

                    IrradianceResult? irradianceResult = Solar.Create.IrradianceResult(normal, Solar.Query.SunDirection(shadingModel, dateTime, true), globalHorizontal, sunUp ? directNormal : 0, diffuseHorizontal, albedo);
                    if (irradianceResult is null)
                    {
                        continue;
                    }

                    SolarPowerResult? solarPowerResult = Solar.Create.SolarPowerResult_ByShadingFactor(irradianceResult, area, shadingFactor);
                    Assert.NotNull(solarPowerResult);

                    double beam_Hour = solarPowerResult.UnshadedArea * irradianceResult.Beam;
                    beam_Reference += beam_Hour;

                    // The same hour through the bulk read-out.
                    double beam_Hour_Bulk = beam_Hour;
                    if (sunUp)
                    {
                        Assert.True(shadingFactors.TryGetValue(dateTime, out double shadingFactor_Bulk));
                        SolarPowerResult? solarPowerResult_Bulk = Solar.Create.SolarPowerResult_ByShadingFactor(irradianceResult, area, Math.Clamp(shadingFactor_Bulk, 0, 1));
                        Assert.NotNull(solarPowerResult_Bulk);
                        beam_Hour_Bulk = solarPowerResult_Bulk.UnshadedArea * irradianceResult.Beam;
                    }

                    differences_Hour.Add(Math.Abs(beam_Hour_Bulk - beam_Hour));
                }

                beam_Reference = beam_Reference / 1000 / area;

                SurfaceSolarRadiationResult surfaceSolarRadiationResult = surfaceSolarRadiationResults.Single(x => x.Reference == reference);
                Assert.True(Math.Abs(surfaceSolarRadiationResult.Beam - beam_Reference) <= 1e-6 * Math.Max(1, beam_Reference), $"{reference}: beam {surfaceSolarRadiationResult.Beam} kWh/m², reference {beam_Reference} kWh/m²");
            }

            // The fixture has to exercise shade, or the parity proves nothing.
            Assert.True(count_Shaded > 500, $"{count_Shaded} shaded receiver-hours");

            differences_Hour.Sort();
            Assert.NotEmpty(differences_Hour);
            double difference_P95 = differences_Hour[(int)Math.Floor(0.95 * (differences_Hour.Count - 1))];
            Assert.True(difference_P95 <= 1e-9, $"95th percentile of the per-hour beam difference {difference_P95} W, {count_OutOfRange} raw factors outside [0, 1]");
            Assert.True(differences_Hour[^1] <= 1e-6, $"max per-hour beam difference {differences_Hour[^1]} W");
        }
    }
}
