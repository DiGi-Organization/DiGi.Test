using System;
using System.Collections.Generic;

namespace DiGi.Weather.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Query.DryBulbTemperatureValue(Classes.WeatherRecord?)"/> (DiGi-Organization/DiGi.Weather#4): null for a null record, for the <see cref="Constants.MissingValue.DryBulbTemperature"/> marker (99.9) and for NaN; the reading otherwise, 99.8 just below the marker, 0 and −40 included.
        /// </summary>
        [Fact]
        public void Query_DryBulbTemperatureValue()
        {
            Assert.Null(Query.DryBulbTemperatureValue(null));
            Assert.Null(MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 1, 1, 0, 0), Constants.MissingValue.DryBulbTemperature).DryBulbTemperatureValue());
            Assert.Null(MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 1, 1, 0, 0), float.NaN).DryBulbTemperatureValue());

            Assert.Equal(99.8f, MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 1, 1, 0, 0), 99.8f).DryBulbTemperatureValue());
            Assert.Equal(0f, MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 1, 1, 0, 0), 0f).DryBulbTemperatureValue());
            Assert.Equal(-40f, MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 1, 1, 0, 0), -40f).DryBulbTemperatureValue());
        }

        /// <summary>
        /// Tests that <see cref="Query.MonthlyMeanDryBulbTemperatures(Classes.Weather?)"/> assigns a record to the month of the interval ending at its stamp (DiGi-Organization/DiGi.Weather#4): a record stamped 1 February 00:00 (EPW hour 24 of 31 January) counts in January, one stamped 1 January 00:00 of the next year (hour 24 of 31 December) in December, and one stamped 1 February 01:00 in February.
        /// <para>Grouping by <c>DateTime.Month</c> would give January −1.0, February 7.5 and December −1.0 here instead of 3.0, 10.0 and −4.0 (checked against such a version: it fails this fact). A month without records is NaN.</para>
        /// </summary>
        [Fact]
        public void MonthlyMeanDryBulbTemperatures_EndOfHour()
        {
            List<Classes.WeatherRecord> weatherRecords =
            [
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 1, 1, 0, 0), 1f),
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 15, 12, 0, 0), 3f),
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 2, 1, 0, 0, 0), 5f),
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 2, 1, 1, 0, 0), 10f),
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 12, 15, 12, 0, 0), -1f),
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2027, 1, 1, 0, 0, 0), -7f),
            ];

            double[]? monthlyMeans = new Classes.Weather(weatherRecords).MonthlyMeanDryBulbTemperatures();
            Assert.NotNull(monthlyMeans);
            Assert.Equal(12, monthlyMeans.Length);

            Assert.Equal(3.0, monthlyMeans[0], 12);
            Assert.Equal(10.0, monthlyMeans[1], 12);
            Assert.Equal(-4.0, monthlyMeans[11], 12);
            for (int i = 2; i < 11; i++)
            {
                Assert.True(double.IsNaN(monthlyMeans[i]));
            }
        }

        /// <summary>
        /// Tests that <see cref="Query.MonthlyMeanDryBulbTemperatures(Classes.Weather?)"/> skips a missing reading (DiGi-Organization/DiGi.Weather#4): adding a January record holding <see cref="Constants.MissingValue.DryBulbTemperature"/> and one holding NaN leaves every month exactly as it is without them, while adding a valid one changes January, so the comparison can fail. A null weather and a weather without records give null.
        /// </summary>
        [Fact]
        public void MonthlyMeanDryBulbTemperatures_MissingValue()
        {
            List<Classes.WeatherRecord> weatherRecords =
            [
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 10, 6, 0, 0), -3.5f),
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 20, 14, 0, 0), 2.25f),
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 7, 5, 15, 0, 0), 24f),
            ];

            double[]? monthlyMeans = new Classes.Weather(weatherRecords).MonthlyMeanDryBulbTemperatures();
            Assert.NotNull(monthlyMeans);
            Assert.Equal((-3.5 + 2.25) / 2.0, monthlyMeans[0], 12);

            List<Classes.WeatherRecord> weatherRecords_Missing =
            [
                .. weatherRecords,
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 25, 8, 0, 0), Constants.MissingValue.DryBulbTemperature),
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 26, 8, 0, 0), float.NaN),
            ];

            double[]? monthlyMeans_Missing = new Classes.Weather(weatherRecords_Missing).MonthlyMeanDryBulbTemperatures();
            Assert.NotNull(monthlyMeans_Missing);
            Assert.Equal(monthlyMeans, monthlyMeans_Missing);

            List<Classes.WeatherRecord> weatherRecords_Valid =
            [
                .. weatherRecords,
                MonthlyMeanDryBulbTemperatures_Record(new DateTime(2026, 1, 25, 8, 0, 0), 99.8f),
            ];

            double[]? monthlyMeans_Valid = new Classes.Weather(weatherRecords_Valid).MonthlyMeanDryBulbTemperatures();
            Assert.NotNull(monthlyMeans_Valid);
            Assert.NotEqual(monthlyMeans[0], monthlyMeans_Valid[0]);

            Assert.Null(Query.MonthlyMeanDryBulbTemperatures(null));
            Assert.Null(new Classes.Weather().MonthlyMeanDryBulbTemperatures());
        }

        /// <summary>
        /// Builds a weather record with the given time stamp and dry bulb temperature; every other field is a fixed reading.
        /// </summary>
        /// <param name="dateTime">The time stamp, the end of the interval the record covers.</param>
        /// <param name="dryBulbTemperature">The dry bulb temperature [°C].</param>
        /// <returns>The weather record.</returns>
        private static Classes.WeatherRecord MonthlyMeanDryBulbTemperatures_Record(DateTime dateTime, float dryBulbTemperature)
        {
            return new Classes.WeatherRecord(dateTime, null, dryBulbTemperature, 0, 50, 101325, 0, 0, 300, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, 0, 0, 0, 0, 0.2f, 0, 0);
        }
    }
}
