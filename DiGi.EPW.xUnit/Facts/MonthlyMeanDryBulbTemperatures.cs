using DiGi.EPW.Classes;
using System;
using System.Globalization;
using System.Reflection;

namespace DiGi.EPW.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Weather.Query.MonthlyMeanDryBulbTemperatures(Weather.Classes.Weather?)"/> on the Warsaw EPW file against a hand-sum of its text (DiGi-Organization/DiGi.Weather#4): each month's mean equals the mean of the dry bulb column over the data lines whose EPW month column is that month, hour 24 of the last day included.
        /// <para>The text is read independently of the EPW reader. The record of hour 24 of a month's last day is stamped 00:00 of the next month, so a query grouping by the stamp's month would fail every month here.</para>
        /// </summary>
        [Fact]
        public void MonthlyMeanDryBulbTemperatures_Warsaw()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));

            double[] sums = new double[12];
            int[] counts = new int[12];
            foreach (string line in System.IO.File.ReadAllLines(path))
            {
                string[] tokens = line.Split(',');
                if (tokens.Length < 35 || !int.TryParse(tokens[0], out _))
                {
                    continue;
                }

                int month = int.Parse(tokens[1], CultureInfo.InvariantCulture);
                sums[month - 1] += float.Parse(tokens[6], CultureInfo.InvariantCulture);
                counts[month - 1]++;
            }

            EPWFile? ePWFile = Modify.Read(path);
            Assert.NotNull(ePWFile);

            double[]? monthlyMeans = Weather.Query.MonthlyMeanDryBulbTemperatures(ePWFile);
            Assert.NotNull(monthlyMeans);
            Assert.Equal(12, monthlyMeans.Length);

            for (int i = 0; i < 12; i++)
            {
                Assert.True(counts[i] > 0);
                Assert.Equal(sums[i] / counts[i], monthlyMeans[i], 9);
            }

            Assert.True(monthlyMeans[0] < monthlyMeans[6], "January is not colder than July in Warsaw, so the fixture is not what the fact assumes.");
        }
    }
}
