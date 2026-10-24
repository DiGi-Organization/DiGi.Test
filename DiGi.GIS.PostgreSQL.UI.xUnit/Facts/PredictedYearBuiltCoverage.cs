using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;
using System.Globalization;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Query.PredictedYearBuiltCoverageMarker(double?)"/> just below, exactly at and just above <see cref="Constants.YearBuiltPrediction.CoverageMarkerThreshold"/>, and with no data.
        /// <para>The threshold is strictly exceeded, so exactly 10 % is not marked; an unknown or non-numeric share is not marked either, because no marker must never be read as "not run" when the truth is "not known". Asserted under pl-PL as well, where a culture-formatted percentage would read <c>(&gt;10 %)</c>.</para>
        /// </summary>
        [Fact]
        public void Query_PredictedYearBuiltCoverageMarker()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                foreach (string name in new string[] { "en-US", "pl-PL" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);

                    Assert.Null(Query.PredictedYearBuiltCoverageMarker(0.0999));
                    Assert.Null(Query.PredictedYearBuiltCoverageMarker(Constants.YearBuiltPrediction.CoverageMarkerThreshold));
                    Assert.Equal("(>10%)", Query.PredictedYearBuiltCoverageMarker(0.1001));
                    Assert.Equal("(>10%)", Query.PredictedYearBuiltCoverageMarker(1.0));
                    Assert.Null(Query.PredictedYearBuiltCoverageMarker(0.0));
                    Assert.Null(Query.PredictedYearBuiltCoverageMarker(null));
                    Assert.Null(Query.PredictedYearBuiltCoverageMarker(double.NaN));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }

        /// <summary>
        /// Verifies that <see cref="Query.PredictedYearBuiltCoverageFactors"/> divides a part's largest run, not the sum of its runs, by its building count, and leaves out a part it cannot measure.
        /// <para>County 5 as production held it: an old run on 10 437 objects and the current one on 33 584, out of 33 687 buildings. A run does not replace the one before it, so summing would read 131 %. A part with no building count, or a count of zero, is left out rather than given a share.</para>
        /// </summary>
        [Fact]
        public void Query_PredictedYearBuiltCoverageFactors()
        {
            List<PredictedYearBuiltRunResult?> predictedYearBuiltRunResults =
            [
                new PredictedYearBuiltRunResult(5, 638841013078773778, null, 10437),
                new PredictedYearBuiltRunResult(5, 639276436977538222, "first-confident-detection@0.5", 33584),
                new PredictedYearBuiltRunResult(204, 639276436977538222, "first-confident-detection@0.5", 2000),
                new PredictedYearBuiltRunResult(310, 639276436977538222, null, 100),
                new PredictedYearBuiltRunResult(311, 639276436977538222, null, 100),
                null
            ];

            Dictionary<int, long> building2DCounts = new()
            {
                [5] = 33687,
                [204] = 30583,
                [311] = 0
            };

            Dictionary<int, double> coverageFactors = Query.PredictedYearBuiltCoverageFactors(predictedYearBuiltRunResults, building2DCounts);

            Assert.Equal(2, coverageFactors.Count);
            Assert.Equal(33584.0 / 33687.0, coverageFactors[5], 12);
            Assert.Equal(2000.0 / 30583.0, coverageFactors[204], 12);
            Assert.False(coverageFactors.ContainsKey(310));
            Assert.False(coverageFactors.ContainsKey(311));

            Assert.Equal("(>10%)", Query.PredictedYearBuiltCoverageMarker(coverageFactors[5]));
            Assert.Null(Query.PredictedYearBuiltCoverageMarker(coverageFactors[204]));

            Assert.Empty(Query.PredictedYearBuiltCoverageFactors(null, building2DCounts));
            Assert.Empty(Query.PredictedYearBuiltCoverageFactors(predictedYearBuiltRunResults, null));
        }
    }
}
