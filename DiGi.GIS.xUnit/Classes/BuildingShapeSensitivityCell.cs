using DiGi.GIS.Enums;
using System.Collections.Generic;

namespace DiGi.GIS.xUnit
{
    /// <summary>
    /// Holds one cell of the building shape threshold sweep: the thresholds, the accuracy and Cohen's kappa against the manual labels, the evaluated and Undefined counts, and the per-class output counts of the solver.
    /// </summary>
    internal sealed class BuildingShapeSensitivityCell
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BuildingShapeSensitivityCell"/> class.
        /// </summary>
        /// <param name="thinnessRatio">The <c>thinnessRatio</c> of the cell.</param>
        /// <param name="scoreFactor">The <c>scoreFactor</c> of the cell.</param>
        /// <param name="accuracy">The accuracy against the manual labels.</param>
        /// <param name="cohenKappa">Cohen's kappa against the manual labels.</param>
        /// <param name="evaluated">The number of items entering the metrics.</param>
        /// <param name="undefinedCount">The number of items excluded as Undefined.</param>
        /// <param name="counts">The solver output counts per class over the evaluated items.</param>
        public BuildingShapeSensitivityCell(double thinnessRatio, double scoreFactor, double accuracy, double cohenKappa, int evaluated, int undefinedCount, Dictionary<BuildingShape, int> counts)
        {
            ThinnessRatio = thinnessRatio;
            ScoreFactor = scoreFactor;
            Accuracy = accuracy;
            CohenKappa = cohenKappa;
            Evaluated = evaluated;
            UndefinedCount = undefinedCount;
            Counts = counts;
        }

        /// <summary>
        /// Gets the thinnessRatio of the cell.
        /// </summary>
        public double ThinnessRatio { get; }

        /// <summary>
        /// Gets the scoreFactor of the cell.
        /// </summary>
        public double ScoreFactor { get; }

        /// <summary>
        /// Gets the accuracy against the manual labels.
        /// </summary>
        public double Accuracy { get; }

        /// <summary>
        /// Gets Cohen's kappa against the manual labels.
        /// </summary>
        public double CohenKappa { get; }

        /// <summary>
        /// Gets the number of items entering the metrics.
        /// </summary>
        public int Evaluated { get; }

        /// <summary>
        /// Gets the number of items excluded as Undefined.
        /// </summary>
        public int UndefinedCount { get; }

        /// <summary>
        /// Gets the solver output counts per class over the evaluated items.
        /// </summary>
        public Dictionary<BuildingShape, int> Counts { get; }
    }
}
