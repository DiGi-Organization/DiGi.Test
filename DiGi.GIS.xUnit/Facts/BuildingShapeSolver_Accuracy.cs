using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Re-solves every footprint of the committed gis12-2026 labelling sample with the default <see cref="BuildingShapeSolver"/> and verifies each result against the solver shape stored in the fixture (solver-drift guard), then recomputes the confusion matrix, per-class precision and recall, accuracy and Cohen's kappa for every pair recorded in the fixture (the solver against each annotator, and annotator against annotator) and verifies them against the recorded values; the solver side of a pair is always the prediction, the human side the reference, and items whose side of the pair is <see cref="BuildingShape.Undefined"/> are excluded and counted. Writes the accuracy section of the report to <c>user files/reports/BuildingShapeSolver_Accuracy.md</c>.
        /// <para>Short test (0.7 s): runs under every DIGI_TEST_MAX_DURATION.</para>
        /// </summary>
        [Fact]
        public void BuildingShapeSolver_Accuracy()
        {
            JsonObject fixture = LoadBuildingShapeSample();

            JsonNode? itemsNode = fixture["Items"];
            Assert.NotNull(itemsNode);
            JsonArray items = itemsNode.AsArray();

            JsonObject sample = fixture["Sample"]!.AsObject();

            JsonNode? expectedNode = fixture["Expected"];
            Assert.NotNull(expectedNode);
            JsonObject expected = expectedNode.AsObject();
            Assert.NotEmpty(expected);

            static string Format4(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);

            // Solve every footprint with the default solver (drift guard) and collect the manual labels per annotator.
            List<BuildingShape> solverShapes_Stored = new();
            List<BuildingShape> solverShapes_Solved = new();
            Dictionary<string, List<BuildingShape>> labels = new();

            foreach (JsonNode? itemNode in items)
            {
                Assert.NotNull(itemNode);
                JsonObject item = itemNode.AsObject();

                BuildingShape solverShape = ParseBuildingShape(item["SolverShape"]?.GetValue<string>());

                Building2D? building2D = Core.Convert.ToDiGi<Building2D>(item["Building2D"]?.ToJsonString())?.FirstOrDefault();
                Assert.NotNull(building2D);

                BuildingShapeSolver buildingShapeSolver = new()
                {
                    Input = building2D
                };
                Assert.True(buildingShapeSolver.Solve());
                Assert.Equal(solverShape, buildingShapeSolver.Output);

                solverShapes_Stored.Add(solverShape);
                solverShapes_Solved.Add(buildingShapeSolver.Output);

                JsonObject? itemLabels = item["Labels"]?.AsObject();
                Assert.NotNull(itemLabels);
                Assert.NotEmpty(itemLabels);

                foreach (KeyValuePair<string, JsonNode?> labelPair in itemLabels)
                {
                    if (!labels.TryGetValue(labelPair.Key, out List<BuildingShape>? labelList))
                    {
                        labelList = new List<BuildingShape>();
                        labels[labelPair.Key] = labelList;
                    }

                    labelList.Add(ParseBuildingShape(labelPair.Value?.GetValue<string>()));
                }
            }

            // Every annotator recorded in the fixture has exactly one label per item, in item order.
            JsonObject? annotators = fixture["Annotators"]?.AsObject();
            Assert.NotNull(annotators);
            Assert.Equal(annotators!.Count, labels.Count);

            foreach (KeyValuePair<string, JsonNode?> annotatorPair in annotators)
            {
                Assert.True(labels.ContainsKey(annotatorPair.Key));
                Assert.Equal(items.Count, labels[annotatorPair.Key].Count);

                JsonObject annotator = annotatorPair.Value!.AsObject();
                Assert.Equal(annotator["Labels"]!.GetValue<int>(), labels[annotatorPair.Key].Count);
            }

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"## Accuracy against manual labels (generated {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)})");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"- Source: {fixture["Source"]?.GetValue<string>()}");
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"- Sample: {fixture["SampleKey"]?.GetValue<string>()} - {items.Count} items, seed {sample["Seed"]!.GetValue<int>()}, county part ids {string.Join(", ", sample["CountyPartIds"]!.AsArray().Select(x => x!.GetValue<int>()))}");

            foreach (KeyValuePair<string, JsonNode?> annotatorPair in annotators)
            {
                JsonObject annotator = annotatorPair.Value!.AsObject();
                stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"- Annotator {annotatorPair.Key}: {annotator["Labels"]!.GetValue<int>()} labels, {annotator["FirstLabelled"]?.GetValue<string>()} .. {annotator["LastLabelled"]?.GetValue<string>()}");
            }

            foreach (KeyValuePair<string, JsonNode?> pair in expected)
            {
                string[] sides = pair.Key.Split(new[] { "Vs" }, StringSplitOptions.None);
                Assert.Equal(2, sides.Length);

                string predictionsSide = sides.Contains("Solver") ? "Solver" : sides[0];
                string referencesSide = sides.First(x => !string.Equals(x, predictionsSide, StringComparison.Ordinal));

                List<BuildingShape> predictions_Full = predictionsSide == "Solver" ? solverShapes_Solved : labels[predictionsSide];
                List<BuildingShape> references_Full = labels[referencesSide];

                List<BuildingShape> references = new();
                List<BuildingShape> predictions = new();
                int undefinedCount = 0;

                for (int index = 0; index < items.Count; index++)
                {
                    if (references_Full[index] == BuildingShape.Undefined || predictions_Full[index] == BuildingShape.Undefined)
                    {
                        undefinedCount++;
                        continue;
                    }

                    references.Add(references_Full[index]);
                    predictions.Add(predictions_Full[index]);
                }

                JsonObject recorded = pair.Value!.AsObject();
                Assert.Equal(recorded["Count"]!.GetValue<int>(), references.Count);
                Assert.Equal(recorded["UndefinedCount"]!.GetValue<int>(), undefinedCount);

                Dictionary<BuildingShape, Dictionary<BuildingShape, int>>? confusionMatrix = Query.ConfusionMatrix(references, predictions);
                Assert.NotNull(confusionMatrix);

                double? accuracy = Query.Accuracy(references, predictions);
                Assert.NotNull(accuracy);
                Assert.Equal(recorded["Accuracy"]!.GetValue<double>(), accuracy!.Value, 12);

                double? cohenKappa = Query.CohenKappa(references, predictions);
                Assert.NotNull(cohenKappa);
                Assert.Equal(recorded["CohenKappa"]!.GetValue<double>(), cohenKappa!.Value, 12);

                JsonObject recordedPrecision = recorded["Precision"]!.AsObject();
                Assert.Equal(confusionMatrix!.Count, recordedPrecision.Count);

                JsonObject recordedRecall = recorded["Recall"]!.AsObject();
                Assert.Equal(confusionMatrix.Count, recordedRecall.Count);

                foreach (KeyValuePair<string, JsonNode?> precisionPair in recordedPrecision)
                {
                    BuildingShape buildingShape = ParseBuildingShape(precisionPair.Key);
                    Assert.True(confusionMatrix.ContainsKey(buildingShape));

                    double? precision = Query.Precision(references, predictions, buildingShape);
                    if (precisionPair.Value == null)
                    {
                        Assert.Null(precision);
                    }
                    else
                    {
                        Assert.NotNull(precision);
                        Assert.Equal(precisionPair.Value.GetValue<double>(), precision!.Value, 12);
                    }
                }

                foreach (KeyValuePair<string, JsonNode?> recallPair in recordedRecall)
                {
                    BuildingShape buildingShape = ParseBuildingShape(recallPair.Key);
                    Assert.True(confusionMatrix.ContainsKey(buildingShape));

                    double? recall = Query.Recall(references, predictions, buildingShape);
                    if (recallPair.Value == null)
                    {
                        Assert.Null(recall);
                    }
                    else
                    {
                        Assert.NotNull(recall);
                        Assert.Equal(recallPair.Value.GetValue<double>(), recall!.Value, 12);
                    }
                }

                JsonObject recordedMatrix = recorded["ConfusionMatrix"]!.AsObject();
                Assert.Equal(recordedMatrix.Count, confusionMatrix.Count);

                foreach (KeyValuePair<string, JsonNode?> rowPair in recordedMatrix)
                {
                    BuildingShape referenceShape = ParseBuildingShape(rowPair.Key);
                    Assert.True(confusionMatrix.ContainsKey(referenceShape));

                    Dictionary<BuildingShape, int> row = confusionMatrix[referenceShape];
                    JsonObject recordedRow = rowPair.Value!.AsObject();
                    Assert.Equal(recordedRow.Count, row.Count);

                    foreach (KeyValuePair<string, JsonNode?> cellPair in recordedRow)
                    {
                        BuildingShape predictionShape = ParseBuildingShape(cellPair.Key);
                        Assert.True(row.ContainsKey(predictionShape));
                        Assert.Equal(cellPair.Value!.GetValue<int>(), row[predictionShape]);
                    }
                }

                // Report: heading, counts, and the two tables of this pair.
                string title = pair.Key.Replace("Vs", " vs ", StringComparison.Ordinal);
                string referenceTitle = referencesSide == "Solver" ? "solver" : referencesSide;
                string predictionsTitle = predictionsSide == "Solver" ? "solver" : predictionsSide;

                stringBuilder.AppendLine();
                stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"### {title}");
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"- Evaluated: {references.Count} items; excluded as Undefined: {undefinedCount}");
                stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"- Accuracy: {Format4(accuracy.Value)}; Cohen's kappa: {Format4(cohenKappa.Value)}");
                stringBuilder.AppendLine();

                BuildingShape[] classOrder = Enum.GetValues<BuildingShape>().Where(x => x != BuildingShape.Undefined && confusionMatrix.ContainsKey(x)).ToArray();

                StringBuilder headerBuilder = new();
                headerBuilder.Append(CultureInfo.CurrentCulture, $"| {referenceTitle} \\ {predictionsTitle} |");
                foreach (BuildingShape buildingShape in classOrder)
                {
                    headerBuilder.Append(CultureInfo.CurrentCulture, $" {buildingShape} |");
                }
                headerBuilder.Append(" Total |");

                stringBuilder.AppendLine("**Confusion matrix** (rows = reference, columns = prediction)");
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(headerBuilder.ToString());
                stringBuilder.AppendLine("|" + "---|" + string.Concat(classOrder.Select(_ => "---:|")) + "---:|");

                foreach (BuildingShape referenceShape in classOrder)
                {
                    StringBuilder rowBuilder = new();
                    rowBuilder.Append(CultureInfo.CurrentCulture, $"| {referenceShape} |");

                    int rowTotal = 0;
                    foreach (BuildingShape predictionShape in classOrder)
                    {
                        int value = confusionMatrix[referenceShape].TryGetValue(predictionShape, out int cell) ? cell : 0;
                        rowBuilder.Append(CultureInfo.CurrentCulture, $" {value} |");
                        rowTotal += value;
                    }

                    rowBuilder.Append(CultureInfo.CurrentCulture, $" {rowTotal} |");
                    stringBuilder.AppendLine(rowBuilder.ToString());
                }

                StringBuilder totalBuilder = new();
                totalBuilder.Append("| **Total** |");
                int grandTotal = 0;
                foreach (BuildingShape predictionShape in classOrder)
                {
                    int columnTotal = classOrder.Sum(referenceShape => confusionMatrix[referenceShape].TryGetValue(predictionShape, out int cell) ? cell : 0);
                    totalBuilder.Append(CultureInfo.CurrentCulture, $" {columnTotal} |");
                    grandTotal += columnTotal;
                }
                totalBuilder.Append(CultureInfo.CurrentCulture, $" {grandTotal} |");
                stringBuilder.AppendLine(totalBuilder.ToString());
                stringBuilder.AppendLine();

                stringBuilder.AppendLine("| Class | Reference support | Precision | Recall |");
                stringBuilder.AppendLine("|---|---:|---:|---:|");

                foreach (BuildingShape buildingShape in classOrder)
                {
                    int support = references.Count(x => x == buildingShape);
                    double? precision = Query.Precision(references, predictions, buildingShape);
                    double? recall = Query.Recall(references, predictions, buildingShape);

                    stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"| {buildingShape} | {support} | {(precision.HasValue ? Format4(precision.Value) : "-")} | {(recall.HasValue ? Format4(recall.Value) : "-")} |");
                }

            }

            UpdateBuildingShapeSampleReport(nameof(BuildingShapeSolver_Accuracy), stringBuilder.ToString());
        }
    }
}
