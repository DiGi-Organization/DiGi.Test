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
        /// Re-solves every footprint of the committed gis12-2026 labelling sample over the threshold sweep thinnessRatio 0.85/0.90/0.95 crossed with scoreFactor 0.60/0.70/0.80 and reports, per cell, the class shares, the change of each share against the default (0.90/0.70), and the accuracy and Cohen's kappa against the manual labels of the first annotator, excluding and counting items that resolve to <see cref="BuildingShape.Undefined"/>; the default cell is verified against the accuracy recorded in the fixture, which ties the sweep to <see cref="BuildingShapeSolver_Accuracy"/>. Writes the sensitivity section of the report to <c>user files/reports/BuildingShapeSolver_Accuracy.md</c>.
        /// <para>thinnessRatio also sets the notch aspect and rectangularity filter (1 - thinnessRatio), so the sweep moves both gates; areaFactor, aspectRatioFactor and rectangleThinnessRatioFactor stay at their defaults.</para>
        /// <para>Medium test (2.4 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void BuildingShapeSolver_Sensitivity()
        {
            static string Format4(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
            static string Format1(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);
            static string FormatDelta(double value) => (value * 100).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
            static string FormatCell(double thinnessRatio, double scoreFactor) => string.Create(CultureInfo.InvariantCulture, $"{thinnessRatio:0.00}/{scoreFactor:0.00}");

            JsonObject fixture = LoadBuildingShapeSample();

            JsonNode? itemsNode = fixture["Items"];
            Assert.NotNull(itemsNode);
            JsonArray items = itemsNode.AsArray();

            JsonNode? expectedNode = fixture["Expected"];
            Assert.NotNull(expectedNode);
            JsonObject expected = expectedNode.AsObject();

            JsonObject? annotators = fixture["Annotators"]?.AsObject();
            Assert.NotNull(annotators);
            Assert.NotEmpty(annotators!);

            string annotatorCode = annotators!.Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal).First();

            // Load every footprint and the first annotator's label once; the sweep re-solves this list per cell.
            List<Building2D> building2Ds = new();
            List<BuildingShape> references_Full = new();

            foreach (JsonNode? itemNode in items)
            {
                Assert.NotNull(itemNode);
                JsonObject item = itemNode.AsObject();

                Building2D? building2D = Core.Convert.ToDiGi<Building2D>(item["Building2D"]?.ToJsonString())?.FirstOrDefault();
                Assert.NotNull(building2D);
                building2Ds.Add(building2D!);

                JsonNode? labelNode = item["Labels"]?[annotatorCode];
                Assert.NotNull(labelNode);
                references_Full.Add(ParseBuildingShape(labelNode!.GetValue<string>()));
            }

            Assert.Equal(items.Count, building2Ds.Count);
            Assert.Equal(items.Count, references_Full.Count);

            double[] thinnessRatios = [0.85, 0.9, 0.95];
            double[] scoreFactors = [0.6, 0.7, 0.8];

            List<BuildingShapeSensitivityCell> cells = new();

            foreach (double thinnessRatio in thinnessRatios)
            {
                foreach (double scoreFactor in scoreFactors)
                {
                    List<BuildingShape> predictions_All = new();
                    foreach (Building2D building2D in building2Ds)
                    {
                        BuildingShapeSolver buildingShapeSolver = new(thinnessRatio: thinnessRatio, scoreFactor: scoreFactor)
                        {
                            Input = building2D
                        };
                        Assert.True(buildingShapeSolver.Solve());
                        predictions_All.Add(buildingShapeSolver.Output);
                    }

                    List<BuildingShape> references = new();
                    List<BuildingShape> predictions = new();
                    int undefinedCount = 0;

                    for (int index = 0; index < items.Count; index++)
                    {
                        if (references_Full[index] == BuildingShape.Undefined || predictions_All[index] == BuildingShape.Undefined)
                        {
                            undefinedCount++;
                            continue;
                        }

                        references.Add(references_Full[index]);
                        predictions.Add(predictions_All[index]);
                    }

                    Assert.Equal(items.Count, references.Count + undefinedCount);

                    double? accuracy = Query.Accuracy(references, predictions);
                    Assert.NotNull(accuracy);
                    double? cohenKappa = Query.CohenKappa(references, predictions);
                    Assert.NotNull(cohenKappa);

                    Dictionary<BuildingShape, int> counts = new();
                    foreach (BuildingShape prediction in predictions)
                    {
                        counts[prediction] = counts.TryGetValue(prediction, out int value) ? value + 1 : 1;
                    }

                    Assert.Equal(references.Count, counts.Values.Sum());

                    // The default cell (the BuildingShapeSolver constructor defaults) must reproduce the accuracy recorded in the fixture.
                    if (thinnessRatio == 0.9 && scoreFactor == 0.7)
                    {
                        JsonObject? recorded = expected[$"SolverVs{annotatorCode}"]?.AsObject();
                        Assert.NotNull(recorded);
                        Assert.Equal(recorded!["Count"]!.GetValue<int>(), references.Count);
                        Assert.Equal(recorded["UndefinedCount"]!.GetValue<int>(), undefinedCount);
                        Assert.Equal(recorded["Accuracy"]!.GetValue<double>(), accuracy!.Value, 12);
                        Assert.Equal(recorded["CohenKappa"]!.GetValue<double>(), cohenKappa!.Value, 12);
                    }

                    cells.Add(new BuildingShapeSensitivityCell(thinnessRatio, scoreFactor, accuracy!.Value, cohenKappa!.Value, references.Count, undefinedCount, counts));
                }
            }

            Assert.Equal(9, cells.Count);

            BuildingShapeSensitivityCell defaultCell = cells.First(x => x.ThinnessRatio == 0.9 && x.ScoreFactor == 0.7);
            List<BuildingShapeSensitivityCell> sweepCells = cells.Where(x => !(x.ThinnessRatio == 0.9 && x.ScoreFactor == 0.7)).ToList();
            Assert.Equal(8, sweepCells.Count);

            BuildingShape[] classOrder = Enum.GetValues<BuildingShape>().Where(x => x != BuildingShape.Undefined).OrderBy(x => (int)x).ToArray();

            // Report.
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"## Threshold sensitivity (generated {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)})");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"- Sweep: thinnessRatio 0.85/0.90/0.95 x scoreFactor 0.60/0.70/0.80 (defaults 0.90/0.70) over {items.Count} footprints, labels of annotator {annotatorCode}.");
            stringBuilder.AppendLine("- thinnessRatio also sets the notch aspect and rectangularity filter (1 - thinnessRatio), so each row of the sweep moves both the circularity/square gate and the notch filter together; areaFactor 0.5, aspectRatioFactor 0.3 and rectangleThinnessRatioFactor 0.2 stay fixed.");
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"- The default cell (0.90/0.70) reproduces the recorded accuracy {Format4(defaultCell.Accuracy)} and kappa {Format4(defaultCell.CohenKappa)}.");
            stringBuilder.AppendLine();

            stringBuilder.AppendLine("| thinnessRatio | scoreFactor | Evaluated | Undefined | Accuracy | Cohen's kappa |");
            stringBuilder.AppendLine("|---:|---:|---:|---:|---:|---:|");

            foreach (BuildingShapeSensitivityCell cell in Enumerable.Repeat(defaultCell, 1).Concat(sweepCells))
            {
                bool isDefault = cell.ThinnessRatio == 0.9 && cell.ScoreFactor == 0.7;
                string scoreLabel = isDefault
                    ? string.Create(CultureInfo.InvariantCulture, $"{cell.ScoreFactor:0.00} (default)")
                    : string.Create(CultureInfo.InvariantCulture, $"{cell.ScoreFactor:0.00}");

                stringBuilder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {cell.ThinnessRatio:0.00} | {scoreLabel} | {cell.Evaluated} | {cell.UndefinedCount} | {Format4(cell.Accuracy)} | {Format4(cell.CohenKappa)} |"));
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Class shares of the solver output; **Default** is the absolute share at 0.90/0.70, every other column is the change against it in percentage points.");
            stringBuilder.AppendLine();

            StringBuilder headerBuilder = new();
            headerBuilder.Append("| Class | Default |");
            foreach (BuildingShapeSensitivityCell cell in sweepCells)
            {
                headerBuilder.Append(string.Create(CultureInfo.InvariantCulture, $" Δ({cell.ThinnessRatio:0.00}/{cell.ScoreFactor:0.00}) |"));
            }
            stringBuilder.AppendLine(headerBuilder.ToString());
            stringBuilder.AppendLine("|---|---:|" + string.Concat(sweepCells.Select(_ => "---:|")));

            foreach (BuildingShape buildingShape in classOrder)
            {
                double defaultShare = defaultCell.Counts.TryGetValue(buildingShape, out int defaultCount) ? defaultCount / (double)defaultCell.Evaluated : 0;

                StringBuilder rowBuilder = new();
                rowBuilder.Append(CultureInfo.CurrentCulture, $"| {buildingShape} | {Format1(defaultShare * 100)}% |");

                foreach (BuildingShapeSensitivityCell cell in sweepCells)
                {
                    double share = cell.Counts.TryGetValue(buildingShape, out int value) ? value / (double)cell.Evaluated : 0;
                    rowBuilder.Append(CultureInfo.CurrentCulture, $" {FormatDelta(share - defaultShare)} |");
                }

                stringBuilder.AppendLine(rowBuilder.ToString());
            }

            stringBuilder.AppendLine();

            List<BuildingShapeSensitivityCell> allCells = Enumerable.Repeat(defaultCell, 1).Concat(sweepCells).ToList();
            BuildingShapeSensitivityCell bestCell = allCells.MaxBy(x => x.Accuracy)!;
            BuildingShapeSensitivityCell worstCell = allCells.MinBy(x => x.Accuracy)!;
            BuildingShapeSensitivityCell bestKappaCell = allCells.MaxBy(x => x.CohenKappa)!;

            stringBuilder.AppendLine("- Accuracy across the nine cells: best "
                + $"{Format4(bestCell.Accuracy)} at {FormatCell(bestCell.ThinnessRatio, bestCell.ScoreFactor)}, worst {Format4(worstCell.Accuracy)} at {FormatCell(worstCell.ThinnessRatio, worstCell.ScoreFactor)}, default {Format4(defaultCell.Accuracy)}.");
            stringBuilder.AppendLine("- Cohen's kappa across the nine cells: best "
                + $"{Format4(bestKappaCell.CohenKappa)} at {FormatCell(bestKappaCell.ThinnessRatio, bestKappaCell.ScoreFactor)}, default {Format4(defaultCell.CohenKappa)}.");

            double largestChange = 0;
            BuildingShape largestChangeShape = BuildingShape.Undefined;
            BuildingShapeSensitivityCell largestChangeCell = defaultCell;

            foreach (BuildingShapeSensitivityCell cell in sweepCells)
            {
                foreach (BuildingShape buildingShape in classOrder)
                {
                    double defaultShare = defaultCell.Counts.TryGetValue(buildingShape, out int defaultCount) ? defaultCount / (double)defaultCell.Evaluated : 0;
                    double share = cell.Counts.TryGetValue(buildingShape, out int value) ? value / (double)cell.Evaluated : 0;

                    if (Math.Abs(share - defaultShare) > Math.Abs(largestChange))
                    {
                        largestChange = share - defaultShare;
                        largestChangeShape = buildingShape;
                        largestChangeCell = cell;
                    }
                }
            }

            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"- Largest class-share change: {largestChangeShape} {FormatDelta(largestChange)} pp at {FormatCell(largestChangeCell.ThinnessRatio, largestChangeCell.ScoreFactor)}.");

            UpdateBuildingShapeSampleReport(nameof(BuildingShapeSolver_Sensitivity), stringBuilder.ToString());
        }
    }
}
