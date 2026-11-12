using DiGi.GIS.Enums;
using System.Collections.Generic;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The ten reference labels of the hand-computed three-class example shared by the classification metric facts: five Square, three L and two O.
        /// </summary>
        private static readonly BuildingShape[] buildingShapes_Reference = [BuildingShape.Square, BuildingShape.Square, BuildingShape.Square, BuildingShape.Square, BuildingShape.Square, BuildingShape.L, BuildingShape.L, BuildingShape.L, BuildingShape.O, BuildingShape.O];

        /// <summary>
        /// The ten predictions of the hand-computed example, paired by position with <see cref="buildingShapes_Reference"/>: seven agree (3 Square, 2 L, 2 O).
        /// </summary>
        private static readonly BuildingShape[] buildingShapes_Prediction = [BuildingShape.Square, BuildingShape.Square, BuildingShape.Square, BuildingShape.L, BuildingShape.O, BuildingShape.L, BuildingShape.L, BuildingShape.Square, BuildingShape.O, BuildingShape.O];

        /// <summary>
        /// Verifies the confusion matrix of the hand-computed example cell by cell, that it is square over every value occurring on either side with zero cells present, and that mismatched lengths or a null side answer null.
        /// </summary>
        [Fact]
        public void ConfusionMatrix()
        {
            Dictionary<BuildingShape, Dictionary<BuildingShape, int>>? confusionMatrix = Query.ConfusionMatrix(buildingShapes_Reference, buildingShapes_Prediction);
            Assert.NotNull(confusionMatrix);

            Assert.Equal(3, confusionMatrix.Count);
            foreach (Dictionary<BuildingShape, int> row in confusionMatrix.Values)
            {
                Assert.Equal(3, row.Count);
            }

            Assert.Equal(3, confusionMatrix[BuildingShape.Square][BuildingShape.Square]);
            Assert.Equal(1, confusionMatrix[BuildingShape.Square][BuildingShape.L]);
            Assert.Equal(1, confusionMatrix[BuildingShape.Square][BuildingShape.O]);
            Assert.Equal(1, confusionMatrix[BuildingShape.L][BuildingShape.Square]);
            Assert.Equal(2, confusionMatrix[BuildingShape.L][BuildingShape.L]);
            Assert.Equal(0, confusionMatrix[BuildingShape.L][BuildingShape.O]);
            Assert.Equal(0, confusionMatrix[BuildingShape.O][BuildingShape.Square]);
            Assert.Equal(0, confusionMatrix[BuildingShape.O][BuildingShape.L]);
            Assert.Equal(2, confusionMatrix[BuildingShape.O][BuildingShape.O]);

            Assert.Null(Query.ConfusionMatrix(buildingShapes_Reference, [BuildingShape.Square]));
            Assert.Null(Query.ConfusionMatrix(null, buildingShapes_Prediction));

            Dictionary<BuildingShape, Dictionary<BuildingShape, int>>? confusionMatrix_Empty = Query.ConfusionMatrix<BuildingShape>([], []);
            Assert.NotNull(confusionMatrix_Empty);
            Assert.Empty(confusionMatrix_Empty);
        }

        /// <summary>
        /// Verifies the overall accuracy of the hand-computed example (7 of 10), and that empty, mismatched or null input answers null.
        /// </summary>
        [Fact]
        public void Accuracy()
        {
            double? accuracy = Query.Accuracy(buildingShapes_Reference, buildingShapes_Prediction);
            Assert.NotNull(accuracy);
            Assert.Equal(0.7, accuracy.Value, 12);

            Assert.Null(Query.Accuracy<BuildingShape>([], []));
            Assert.Null(Query.Accuracy(buildingShapes_Reference, [BuildingShape.Square]));
            Assert.Null(Query.Accuracy(buildingShapes_Reference, null));
        }

        /// <summary>
        /// Verifies per-class precision of the hand-computed example (Square 3/4, L 2/3, O 2/3), and that a class never predicted answers null.
        /// </summary>
        [Fact]
        public void Precision()
        {
            Assert.Equal(0.75, Query.Precision(buildingShapes_Reference, buildingShapes_Prediction, BuildingShape.Square)!.Value, 12);
            Assert.Equal(2.0 / 3.0, Query.Precision(buildingShapes_Reference, buildingShapes_Prediction, BuildingShape.L)!.Value, 12);
            Assert.Equal(2.0 / 3.0, Query.Precision(buildingShapes_Reference, buildingShapes_Prediction, BuildingShape.O)!.Value, 12);

            Assert.Null(Query.Precision(buildingShapes_Reference, buildingShapes_Prediction, BuildingShape.X));
            Assert.Null(Query.Precision(buildingShapes_Reference, [BuildingShape.Square], BuildingShape.Square));
        }

        /// <summary>
        /// Verifies per-class recall of the hand-computed example (Square 3/5, L 2/3, O 2/2), and that a class absent from the reference answers null.
        /// </summary>
        [Fact]
        public void Recall()
        {
            Assert.Equal(0.6, Query.Recall(buildingShapes_Reference, buildingShapes_Prediction, BuildingShape.Square)!.Value, 12);
            Assert.Equal(2.0 / 3.0, Query.Recall(buildingShapes_Reference, buildingShapes_Prediction, BuildingShape.L)!.Value, 12);
            Assert.Equal(1.0, Query.Recall(buildingShapes_Reference, buildingShapes_Prediction, BuildingShape.O)!.Value, 12);

            Assert.Null(Query.Recall(buildingShapes_Reference, buildingShapes_Prediction, BuildingShape.X));
            Assert.Null(Query.Recall(buildingShapes_Reference, [BuildingShape.Square], BuildingShape.Square));
        }

        /// <summary>
        /// Verifies Cohen's kappa of the hand-computed example: p_o = 0.7, p_e = 0.5·0.4 + 0.3·0.3 + 0.2·0.3 = 0.35, so κ = 0.35 / 0.65 = 7/13.
        /// <para>Also: identical labels give 1 (including the single-class case where p_e = 1), complete disagreement between two balanced classes gives -1, and empty, mismatched or null input answers null.</para>
        /// </summary>
        [Fact]
        public void CohenKappa()
        {
            double? cohenKappa = Query.CohenKappa(buildingShapes_Reference, buildingShapes_Prediction);
            Assert.NotNull(cohenKappa);
            Assert.Equal(7.0 / 13.0, cohenKappa.Value, 12);

            Assert.Equal(1.0, Query.CohenKappa(buildingShapes_Reference, buildingShapes_Reference)!.Value, 12);
            Assert.Equal(1.0, Query.CohenKappa([BuildingShape.Square, BuildingShape.Square], [BuildingShape.Square, BuildingShape.Square])!.Value, 12);
            Assert.Equal(-1.0, Query.CohenKappa([BuildingShape.Square, BuildingShape.L], [BuildingShape.L, BuildingShape.Square])!.Value, 12);

            Assert.Null(Query.CohenKappa<BuildingShape>([], []));
            Assert.Null(Query.CohenKappa(buildingShapes_Reference, [BuildingShape.Square]));
            Assert.Null(Query.CohenKappa(null, buildingShapes_Prediction));
        }
    }
}
