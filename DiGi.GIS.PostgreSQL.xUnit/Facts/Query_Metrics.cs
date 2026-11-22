using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Query.Mae(IReadOnlyList{PopulationGridComparisonItem}?)"/>: the mean absolute error over the cells that carry a census value, a masked cell left out of the mean rather than measured against a zero, and null when no cell carries a census value.
        /// </summary>
        [Fact]
        public void Query_Mae()
        {
            List<PopulationGridComparisonItem> populationGridComparisonItems =
            [
                new("a", 100, 110, 10, 1, Enums.PopulationDensityClass.VeryLow),
                new("b", 200, 180, -20, 1, Enums.PopulationDensityClass.Low),
                new("c", 300, 330, 30, 1, Enums.PopulationDensityClass.Medium),
                new("d", null, 50, null, 1, Enums.PopulationDensityClass.Masked),
            ];

            // (10 + 20 + 30) / 3 - the masked cell contributes no error of its own
            Assert.Equal(20d, Query.Mae(populationGridComparisonItems));

            Assert.Null(Query.Mae(null));
            Assert.Null(Query.Mae([]));
            Assert.Null(Query.Mae([populationGridComparisonItems[3]]));
        }

        /// <summary>
        /// Verifies <see cref="Query.Bias(IReadOnlyList{PopulationGridComparisonItem}?)"/>: the signed mean error over the cells that carry a census value, its direction preserved, and null when no cell carries a census value.
        /// </summary>
        [Fact]
        public void Query_Bias()
        {
            List<PopulationGridComparisonItem> populationGridComparisonItems =
            [
                new("a", 100, 110, 10, 1, Enums.PopulationDensityClass.VeryLow),
                new("b", 200, 180, -20, 1, Enums.PopulationDensityClass.Low),
                new("c", 300, 330, 30, 1, Enums.PopulationDensityClass.Medium),
                new("d", null, 50, null, 1, Enums.PopulationDensityClass.Masked),
            ];

            // (10 - 20 + 30) / 3 - the sign of each error survives, which an absolute error cannot show
            Assert.NotNull(Query.Bias(populationGridComparisonItems));
            Assert.Equal(20d / 3d, Query.Bias(populationGridComparisonItems)!.Value, 10);

            Assert.Null(Query.Bias(null));
            Assert.Null(Query.Bias([]));
            Assert.Null(Query.Bias([populationGridComparisonItems[3]]));
        }

        /// <summary>
        /// Verifies <see cref="Query.R2(IReadOnlyList{PopulationGridComparisonItem}?)"/>: the predictive coefficient of determination on hand-computed inputs, a score below zero for a model worse than the census mean, and null when fewer than two cells carry a census value or the census values carry no variance.
        /// </summary>
        [Fact]
        public void Query_R2()
        {
            List<PopulationGridComparisonItem> populationGridComparisonItems =
            [
                new("a", 100, 110, 10, 1, Enums.PopulationDensityClass.VeryLow),
                new("b", 200, 180, -20, 1, Enums.PopulationDensityClass.Low),
                new("c", 300, 330, 30, 1, Enums.PopulationDensityClass.Medium),
            ];

            // 1 - (10^2 + 20^2 + 30^2) / ((100 - 200)^2 + (200 - 200)^2 + (300 - 200)^2) = 1 - 1400 / 20000 = 0.93
            Assert.NotNull(Query.R2(populationGridComparisonItems));
            Assert.Equal(0.93, Query.R2(populationGridComparisonItems)!.Value, 10);

            // A model scoring zero where the census counts people is worse than the census mean and is reported below zero, not clamped
            List<PopulationGridComparisonItem> populationGridComparisonItems_Worse =
            [
                new("a", 100, 0, -100, 0, Enums.PopulationDensityClass.VeryLow),
                new("b", 200, 0, -200, 0, Enums.PopulationDensityClass.Low),
            ];

            // 1 - (100^2 + 200^2) / ((100 - 150)^2 + (200 - 150)^2) = 1 - 50000 / 5000 = -9
            Assert.NotNull(Query.R2(populationGridComparisonItems_Worse));
            Assert.Equal(-9d, Query.R2(populationGridComparisonItems_Worse)!.Value, 10);

            // Fewer than two compared cells: the variance of one census value is zero by definition
            Assert.Null(Query.R2([populationGridComparisonItems[0]]));

            // Identical census values: no variance for any model to explain
            Assert.Null(Query.R2(
            [
                new("a", 100, 110, 10, 1, Enums.PopulationDensityClass.VeryLow),
                new("b", 100, 90, -10, 1, Enums.PopulationDensityClass.VeryLow),
            ]));

            // Only masked cells: nothing comparable
            Assert.Null(Query.R2([new("a", null, 50, null, 1, Enums.PopulationDensityClass.Masked)]));
            Assert.Null(Query.R2(null));
        }
    }
}
