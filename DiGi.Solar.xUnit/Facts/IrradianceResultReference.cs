using DiGi.Core.Classes;
using DiGi.Geometry.Spatial.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace DiGi.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reads the committed PVGIS reference series and returns one entry per daylight hour.
        /// <para>The fixture is hourly irradiance for Poznan, Poland published by the European Commission Joint Research Centre, on a horizontal plane and on a plane tilted 35 degrees facing south. See the header of the file itself for provenance.</para>
        /// </summary>
        /// <returns>The parsed reference rows, in file order.</returns>
        public static List<PVGISReferenceRow> PVGISReferenceRows()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "PVGIS_Poznan_2020.csv");
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(System.IO.File.Exists(path));

            List<PVGISReferenceRow> pVGISReferenceRows = [];
            foreach (string line in System.IO.File.ReadAllLines(path!))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.CurrentCulture))
                {
                    continue;
                }

                string[] values = line.Split(',');
                Assert.Equal(7, values.Length);

                // PVGIS stamps each hour as yyyyMMdd:HHmm in UTC.
                string text = values[0];
                System.DateTime dateTime = new(int.Parse(text.AsSpan(0, 4), CultureInfo.CurrentCulture), int.Parse(text.AsSpan(4, 2), CultureInfo.CurrentCulture), int.Parse(text.AsSpan(6, 2), CultureInfo.CurrentCulture), int.Parse(text.AsSpan(9, 2), CultureInfo.CurrentCulture), int.Parse(text.AsSpan(11, 2), CultureInfo.CurrentCulture), 0);

                pVGISReferenceRows.Add(new PVGISReferenceRow(
                    dateTime,
                    double.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(values[4], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(values[5], System.Globalization.CultureInfo.InvariantCulture),
                    double.Parse(values[6], System.Globalization.CultureInfo.InvariantCulture)));
            }

            Assert.True(pVGISReferenceRows.Count > 200, $"Reference fixture is expected to carry a usable sample. It carried {pVGISReferenceRows.Count} rows.");
            return pVGISReferenceRows;
        }

        /// <summary>
        /// Compares the solar elevation computed by <see cref="Query.SunDirection(Coordinates, Core.Enums.UTC, System.DateTime, bool)"/> against the elevation published by PVGIS for the same instants.
        /// <para>This validates the sun position model, and therefore the beam projection that depends on it, against an independent authority rather than against values derived the same way.</para>
        /// </summary>
        [Fact]
        public void IrradianceResult_PVGIS_SolarElevation()
        {
            Coordinates coordinates = new(52.4064, 16.9252);
            List<double> deviations = [];

            foreach (PVGISReferenceRow pVGISReferenceRow in PVGISReferenceRows())
            {
                Vector3D? vector3D_SunDirection = Query.SunDirection(coordinates, Core.Enums.UTC.PlusMinus0000, pVGISReferenceRow.DateTime, true);
                Assert.NotNull(vector3D_SunDirection);

                Vector3D? vector3D_Unit = vector3D_SunDirection.Unit;
                Assert.NotNull(vector3D_Unit);

                // The sun direction points away from the sun, so the sine of the elevation is the
                // negated Z ordinate.
                double sunElevationDegrees = System.Math.Asin(-vector3D_Unit.Z) * 180 / System.Math.PI;
                deviations.Add(System.Math.Abs(sunElevationDegrees - pVGISReferenceRow.SunElevationDegrees));
            }

            deviations.Sort();
            double median = deviations[deviations.Count / 2];
            double percentile99 = deviations[(int)(deviations.Count * 0.99)];
            double maximum = deviations[deviations.Count - 1];

            System.IO.File.WriteAllLines(System.IO.Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "IrradianceResult_PVGIS_SolarElevation.txt"),
            [
                "Solar elevation, DiGi.Solar Query.SunDirection versus PVGIS H_sun, degrees.",
                $"Rows compared : {deviations.Count}",
                $"Median        : {median:F4}",
                $"99th pct      : {percentile99:F4}",
                $"Maximum       : {maximum:F4}"
            ]);

            // PVGIS publishes the elevation to two decimals and stamps the hour at its satellite
            // observation time, so sub-degree agreement is the most that can be asked for.
            Assert.True(percentile99 < 0.35, $"Solar elevation 99th percentile deviation from PVGIS was {percentile99:F4} degrees.");
            Assert.True(maximum < 0.5, $"Solar elevation maximum deviation from PVGIS was {maximum:F4} degrees.");
        }

        /// <summary>
        /// Compares the ground-reflected component against the value PVGIS publishes for the same tilted plane.
        /// <para>PVGIS computes this component with the same isotropic formula and the same default ground reflectance of 0.2, so the two must agree to within the rounding PVGIS applies to its published horizontal irradiance. This is the one component where an external source can confirm the implementation exactly rather than approximately.</para>
        /// </summary>
        [Fact]
        public void IrradianceResult_PVGIS_GroundComponent()
        {
            Coordinates coordinates = new(52.4064, 16.9252);
            Vector3D vector3D_SurfaceNormal = SurfaceNormal_Tilted35South();
            List<double> deviations = [];

            foreach (PVGISReferenceRow pVGISReferenceRow in PVGISReferenceRows())
            {
                Vector3D? vector3D_SunDirection = Query.SunDirection(coordinates, Core.Enums.UTC.PlusMinus0000, pVGISReferenceRow.DateTime, true);
                Assert.NotNull(vector3D_SunDirection);

                double globalHorizontalRadiation = pVGISReferenceRow.BeamHorizontal + pVGISReferenceRow.DiffuseHorizontal;

                Classes.IrradianceResult? irradianceResult = Create.IrradianceResult(vector3D_SurfaceNormal, vector3D_SunDirection, globalHorizontalRadiation, 0, pVGISReferenceRow.DiffuseHorizontal, 0.2);
                Assert.NotNull(irradianceResult);

                deviations.Add(System.Math.Abs(irradianceResult.Ground - pVGISReferenceRow.GroundTilted35));
            }

            deviations.Sort();
            double median = deviations[deviations.Count / 2];
            double percentile99 = deviations[(int)(deviations.Count * 0.99)];
            double maximum = deviations[deviations.Count - 1];

            System.IO.File.WriteAllLines(System.IO.Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "IrradianceResult_PVGIS_GroundComponent.txt"),
            [
                "Ground-reflected irradiance, DiGi.Solar versus PVGIS Gr(i), W/m2, slope 35 deg azimuth 0.",
                $"Rows compared : {deviations.Count}",
                $"Median        : {median:F4}",
                $"99th pct      : {percentile99:F4}",
                $"Maximum       : {maximum:F4}"
            ]);

            Assert.True(median < 0.05, $"Ground component median deviation from PVGIS was {median:F4} W/m2.");
            Assert.True(percentile99 < 0.9, $"Ground component 99th percentile deviation from PVGIS was {percentile99:F4} W/m2.");
            Assert.True(maximum < 2.0, $"Ground component maximum deviation from PVGIS was {maximum:F4} W/m2.");
        }

        /// <summary>
        /// Compares the direct beam component against the value PVGIS publishes for the same tilted plane.
        /// <para>The beam component is pure geometry once the direct normal radiation is known, so this checks the incidence cosine, and with it the sun direction sign convention, against an independent authority. A sign error here does not produce a small discrepancy, it produces a total one.</para>
        /// </summary>
        [Fact]
        public void IrradianceResult_PVGIS_BeamComponent()
        {
            Coordinates coordinates = new(52.4064, 16.9252);
            Vector3D vector3D_SurfaceNormal = SurfaceNormal_Tilted35South();
            List<double> relativeDeviations = [];
            int comparedCount = 0;

            foreach (PVGISReferenceRow pVGISReferenceRow in PVGISReferenceRows())
            {
                // Below a low sun the reconstructed direct normal radiation is dominated by the
                // rounding PVGIS applies to its published horizontal beam irradiance.
                if (pVGISReferenceRow.SunElevationDegrees < 10 || pVGISReferenceRow.BeamHorizontal < 50 || pVGISReferenceRow.BeamTilted35 < 50)
                {
                    continue;
                }

                Vector3D? vector3D_SunDirection = Query.SunDirection(coordinates, Core.Enums.UTC.PlusMinus0000, pVGISReferenceRow.DateTime, true);
                Assert.NotNull(vector3D_SunDirection);

                Vector3D? vector3D_Unit = vector3D_SunDirection.Unit;
                Assert.NotNull(vector3D_Unit);

                // Reconstruct the direct normal radiation from the horizontal beam component,
                // which PVGIS reports as the direct normal projected onto the horizontal plane.
                double sineElevation = -vector3D_Unit.Z;
                double directNormalRadiation = pVGISReferenceRow.BeamHorizontal / sineElevation;

                Classes.IrradianceResult? irradianceResult = Create.IrradianceResult(vector3D_SurfaceNormal, vector3D_SunDirection, 0, directNormalRadiation, 0, 0.2);
                Assert.NotNull(irradianceResult);

                comparedCount++;
                relativeDeviations.Add(System.Math.Abs(irradianceResult.Beam - pVGISReferenceRow.BeamTilted35) / pVGISReferenceRow.BeamTilted35 * 100);
            }

            relativeDeviations.Sort();
            double median = relativeDeviations[relativeDeviations.Count / 2];
            double percentile99 = relativeDeviations[(int)(relativeDeviations.Count * 0.99)];
            double maximum = relativeDeviations[relativeDeviations.Count - 1];

            System.IO.File.WriteAllLines(System.IO.Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "IrradianceResult_PVGIS_BeamComponent.txt"),
            [
                "Direct beam irradiance, DiGi.Solar versus PVGIS Gb(i), percent of the PVGIS value, slope 35 deg azimuth 0.",
                $"Rows compared : {comparedCount}",
                $"Median        : {median:F4}",
                $"99th pct      : {percentile99:F4}",
                $"Maximum       : {maximum:F4}"
            ]);

            Assert.True(comparedCount > 100, $"Beam comparison is expected to run over a usable sample. It compared {comparedCount} rows.");
            Assert.True(median < 0.5, $"Beam component median deviation from PVGIS was {median:F4} percent.");
            Assert.True(percentile99 < 2.5, $"Beam component 99th percentile deviation from PVGIS was {percentile99:F4} percent.");

            // A percentile carries bulk agreement. This carries the guarantee that no individual
            // hour drifted into being a different answer, which is what a sign or frame error does.
            Assert.True(maximum < 5.0, $"Beam component maximum deviation from PVGIS was {maximum:F4} percent.");
        }

        /// <summary>
        /// Records how far the isotropic sky diffuse component sits from the anisotropic value PVGIS publishes, and asserts only that the difference stays within the band expected of that model choice.
        /// <para>The isotropic model deliberately ignores circumsolar brightening and horizon brightening, so it reads low against PVGIS for a sun-facing tilt. This fact exists to quantify and record that gap rather than to bound it tightly, so that a future move to an anisotropic model has a measured baseline to improve on.</para>
        /// </summary>
        [Fact]
        public void IrradianceResult_PVGIS_DiffuseModelDifference()
        {
            Coordinates coordinates = new(52.4064, 16.9252);
            Vector3D vector3D_SurfaceNormal = SurfaceNormal_Tilted35South();
            List<double> relativeDifferences = [];

            foreach (PVGISReferenceRow pVGISReferenceRow in PVGISReferenceRows())
            {
                if (pVGISReferenceRow.DiffuseTilted35 < 20)
                {
                    continue;
                }

                Vector3D? vector3D_SunDirection = Query.SunDirection(coordinates, Core.Enums.UTC.PlusMinus0000, pVGISReferenceRow.DateTime, true);
                Assert.NotNull(vector3D_SunDirection);

                Classes.IrradianceResult? irradianceResult = Create.IrradianceResult(vector3D_SurfaceNormal, vector3D_SunDirection, 0, 0, pVGISReferenceRow.DiffuseHorizontal, 0.2);
                Assert.NotNull(irradianceResult);

                relativeDifferences.Add((pVGISReferenceRow.DiffuseTilted35 - irradianceResult.Diffuse) / pVGISReferenceRow.DiffuseTilted35 * 100);
            }

            relativeDifferences.Sort();
            double median = relativeDifferences[relativeDifferences.Count / 2];
            double percentile1 = relativeDifferences[(int)(relativeDifferences.Count * 0.01)];
            double percentile99 = relativeDifferences[(int)(relativeDifferences.Count * 0.99)];

            System.IO.File.WriteAllLines(System.IO.Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "IrradianceResult_PVGIS_DiffuseModelDifference.txt"),
            [
                "Sky diffuse irradiance, PVGIS Gd(i) less the DiGi.Solar isotropic value, percent of the PVGIS value, slope 35 deg azimuth 0.",
                "A positive number means the isotropic model reads low, which is the expected direction.",
                $"Rows compared : {relativeDifferences.Count}",
                $"1st pct       : {percentile1:F4}",
                $"Median        : {median:F4}",
                $"99th pct      : {percentile99:F4}"
            ]);

            // The isotropic model is expected to read low on average for a sun-facing tilt.
            Assert.True(median > 0, $"The isotropic diffuse component is expected to read low against PVGIS. The median difference was {median:F4} percent.");
            Assert.True(median < 25, $"The isotropic diffuse component departed from PVGIS by a median of {median:F4} percent, which is beyond the band expected of the model choice.");
        }

        /// <summary>
        /// Returns the outward normal of a plane tilted 35 degrees from horizontal and facing south, matching the plane the PVGIS reference series was generated for.
        /// </summary>
        /// <returns>The outward normal, in the DiGi frame where X is east, Y is north and Z is up.</returns>
        public static Vector3D SurfaceNormal_Tilted35South()
        {
            double tilt = 35 * System.Math.PI / 180;
            return new Vector3D(0, -System.Math.Sin(tilt), System.Math.Cos(tilt));
        }
    }
}
