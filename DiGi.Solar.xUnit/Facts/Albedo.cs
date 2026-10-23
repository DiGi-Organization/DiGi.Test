using System.Reflection;

namespace DiGi.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the ground reflectance resolution rules, covering the missing marker, the zero-valued column that weather records without an albedo field produce, out-of-range values and the snow-covered override.
        /// </summary>
        [Fact]
        public void Albedo()
        {
            // Missing marker used by weather files.
            Assert.Equal(Solar.Constants.Albedo.Default, Query.Albedo(999, false), 10);

            // Not supplied at all.
            Assert.Equal(Solar.Constants.Albedo.Default, Query.Albedo(null, false), 10);

            // A weather record carrying no albedo column reports zero rather than the missing
            // marker, and taking that at face value would remove the ground-reflected component.
            Assert.Equal(Solar.Constants.Albedo.Default, Query.Albedo(0, false), 10);

            // Out of range.
            Assert.Equal(Solar.Constants.Albedo.Default, Query.Albedo(1.5, false), 10);
            Assert.Equal(Solar.Constants.Albedo.Default, Query.Albedo(-0.3, false), 10);
            Assert.Equal(Solar.Constants.Albedo.Default, Query.Albedo(double.NaN, false), 10);

            // A usable value is returned unchanged.
            Assert.Equal(0.35, Query.Albedo(0.35, false), 10);
            Assert.Equal(1.0, Query.Albedo(1.0, false), 10);

            // Snow on the ground overrides any supplied albedo, including a missing one.
            Assert.Equal(Solar.Constants.Albedo.Snow, Query.Albedo(0.35, true), 10);
            Assert.Equal(Solar.Constants.Albedo.Snow, Query.Albedo(999, true), 10);
            Assert.Equal(Solar.Constants.Albedo.Snow, Query.Albedo(null, true), 10);
        }

        /// <summary>
        /// Tests the albedo resolution of a Warsaw weather year whose snow depth is a filler, hour by hour, month by month, so the snow regression cannot return silently.
        /// <para>The IWEC Warsaw file reports a constant 3.0 cm of lying snow for every hour from April to November, a filler value rather than a measurement (DiGi-Organization/DiGi.Solar#2). That file is ASHRAE-copyrighted and no longer in the repository (DiGi-Organization/DiGi.Test#4), so the filler is written into the IMGW typical year of the same station (ministry data, CC0 1.0) by <see cref="EPWLines_SnowFiller"/>. The snow-depth column is therefore not a reliable indicator of lying snow, the caller's decision for this file is no snow cover, and no month may resolve to the snow reflectance. The filler value itself is pinned as the anchor the regression was measured against.</para>
        /// </summary>
        [Fact]
        public void Albedo_WarsawEpw()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(System.IO.File.Exists(path));

            string[] lines = EPWLines_SnowFiller(System.IO.File.ReadAllLines(path!));

            Dictionary<int, List<double>> snowDepths_ByMonth = [];
            Dictionary<int, List<double>> albedos_ByMonth = [];
            foreach (string line in lines)
            {
                string[] values = line.Split(',');
                if (values.Length < 35)
                {
                    continue;
                }

                if (!int.TryParse(values[0], out int year) || !int.TryParse(values[1], out int month) || month is < 1 or > 12)
                {
                    continue;
                }

                if (!double.TryParse(values[30], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double snowDepth)
                    || !double.TryParse(values[32], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double albedo))
                {
                    continue;
                }

                if (!snowDepths_ByMonth.TryGetValue(month, out List<double>? snowDepths))
                {
                    snowDepths = [];
                    snowDepths_ByMonth[month] = snowDepths;
                }

                if (!albedos_ByMonth.TryGetValue(month, out List<double>? albedos))
                {
                    albedos = [];
                    albedos_ByMonth[month] = albedos;
                }

                snowDepths.Add(snowDepth);
                albedos.Add(albedo);
            }

            Assert.Equal(12, snowDepths_ByMonth.Count);
            Assert.Equal(8760, snowDepths_ByMonth.Values.Sum(snowDepths => snowDepths.Count));

            // The snow-depth column of this file is not a reliable snow indicator, so the caller's
            // decision for it is no snow cover. No month may resolve to the snow reflectance.
            for (int month = 1; month <= 12; month++)
            {
                List<double> snowDepths = snowDepths_ByMonth[month];
                List<double> albedos = albedos_ByMonth[month];
                Assert.NotEmpty(snowDepths);
                Assert.Equal(snowDepths.Count, albedos.Count);
                Assert.DoesNotContain(Solar.Constants.Albedo.Snow, albedos.Select(albedo => Query.Albedo(albedo, false)));
            }

            // Pin the filler value the regression was measured against: a constant 3.0 cm of lying
            // snow for every hour from April to November.
            for (int month = 4; month <= 11; month++)
            {
                Assert.All(snowDepths_ByMonth[month], snowDepth => Assert.Equal(3.0, snowDepth));
            }
        }

        /// <summary>
        /// Writes the IWEC Warsaw snow filler - a constant 3.0 cm of lying snow for every hour from April to November - into the hourly records of an EPW file's lines.
        /// </summary>
        /// <param name="lines">The lines of an EPW file.</param>
        /// <returns>The lines, with the snow depth (field 31, index 30) of every April-November record set to 3.0.</returns>
        private static string[] EPWLines_SnowFiller(string[] lines)
        {
            string[] result = new string[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                string[] values = lines[i].Split(',');
                if (values.Length >= 35 && int.TryParse(values[0], out _) && int.TryParse(values[1], out int month) && month is >= 4 and <= 11)
                {
                    values[30] = "3.0";
                    result[i] = string.Join(",", values);
                    continue;
                }

                result[i] = lines[i];
            }

            return result;
        }
    }
}
