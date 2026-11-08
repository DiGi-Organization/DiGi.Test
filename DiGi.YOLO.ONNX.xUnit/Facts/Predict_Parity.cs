using DiGi.YOLO.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiGi.YOLO.ONNX.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the in-process ONNX path reproduces the detections the CPython path finds on the same images, within a stated tolerance.
        /// <para>This is the acceptance measure the whole in-process path exists to meet. The detector is frozen, so the ONNX graph is not allowed to be a slightly different detector - it has to be the same one, reached without an interpreter. Both paths are pointed at one directory of held images and their two result files are compared detection by detection.</para>
        /// <para>The tolerance is not zero and cannot be. One path runs an fp32 CUDA graph through torch and the other an fp32 CPU graph through ONNX Runtime, so the last bits of every number differ. A detection whose confidence sits on the reporting threshold therefore legitimately appears on one side and not the other, which is why detections inside a guard band around the threshold are left out of the count comparison instead of being counted as disagreements.</para>
        /// <para>Coordinates are bounded at a percentile rather than at a maximum, which is the one part of this worth reading carefully. A box edge is regressed in units of the feature stride, so its last bits are magnified far more than those of the single sigmoid the class score comes from - a handful of detections move by whole pixels while their confidence agrees to five decimal places. YOLOv8 makes that worse by taking each edge as a softmax expectation over sixteen bins; YOLO26, which regresses one distance per edge, shows the same tail, only shorter. Bounding the maximum would mean stating a tolerance loose enough to swallow those, and a tolerance that loose would no longer notice a real coordinate regression. Every individual detection is instead guarded by the overlap of its matched pair, which stays high however the edges wander.</para>
        /// <para>Nothing here runs unless the machine is set up for it. The 130 MB checkpoint, its ONNX export, a CPython carrying ultralytics and a directory of held images are all named in a git-ignored conf beside the test assets; without that file the fact returns, because requiring any of those on every machine running the suite is not reasonable.</para>
        /// <para>Medium test (12.9 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Predict_Parity()
        {
            //The stated tolerance, set from what 2 000 held images actually show rather than from a guess.
            //
            //The bound on coordinates is a percentile, not a maximum, and that is deliberate. Measured over 2 000
            //held images: the median deviation is 0.004 px and the 99th percentile 0.034 px, but 2 of 1 639 matched
            //detections move more than a pixel and the worst moves 3.3 px - with its confidence agreeing to five
            //decimal places and exactly one detection on each side, so it is neither a suppression tie nor a
            //systematic shift. The likely mechanism, not proven here: a box edge is regressed in units of the feature
            //stride, which magnifies its last bits far more than the single sigmoid behind the class score, and
            //YOLOv8's softmax expectation over sixteen bins (DFL) amplifies that further. The retrained YOLO26x
            //train9_fresh has no DFL (reg_max = 1) and over the same 2 000 images still moves 2 of 1 633 matched
            //detections by more than a pixel, the worst by 1.9 px, at a 0.047 px 99th percentile - the same tail,
            //shorter. A maximum would therefore have to be widened to about 4 px to pass, and a 4 px bound would no
            //longer notice a real coordinate regression against a 0.004 px median.
            //
            //What guards every single detection instead is the overlap bound below: however the edges move, a matched
            //pair has to remain the same building. The worst observed was 0.970 (train8) and 0.977 (train9_fresh).
            const double tolerance_BoxPercentile = 0.1;
            const double tolerance_Confidence = 0.01;
            const double iou_Minimum_Required = 0.95;
            const double guardBand = 0.01;
            const double agreement_Minimum = 0.995;

            //Below this many matched pairs a 99th percentile is just the largest value, which would make the
            //coordinate bound turn on whether one tail case happened to fall inside a small sample. The overlap,
            //confidence and count bounds below hold at any sample size and are asserted regardless.
            const int count_Percentile_Minimum = 100;

            Assembly assembly = Assembly.GetExecutingAssembly();

            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return;
            }

            string path_Configuration = Path.Combine(directory_UserFiles!, "DiGi.YOLO.ONNX_Parity.conf");
            if (!File.Exists(path_Configuration))
            {
                return;
            }

            Dictionary<string, string> settings = [];
            foreach (string line in File.ReadAllLines(path_Configuration))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                int index = line.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                settings[line.Substring(0, index).Trim()] = line.Substring(index + 1).Trim();
            }

            settings.TryGetValue("ModelPath", out string? path_Model);
            settings.TryGetValue("ONNXModelPath", out string? path_ModelONNX);
            settings.TryGetValue("ImageDirectory", out string? directory_Image);

            if (string.IsNullOrWhiteSpace(path_Model) || !File.Exists(path_Model) || string.IsNullOrWhiteSpace(path_ModelONNX) || !File.Exists(path_ModelONNX) || string.IsNullOrWhiteSpace(directory_Image) || !Directory.Exists(directory_Image))
            {
                return;
            }

            int count_Sample = settings.TryGetValue("SampleSize", out string? value_SampleSize) && int.TryParse(value_SampleSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count_Parsed) && count_Parsed > 0 ? count_Parsed : 25;

            string directory_Work = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_ONNX_Parity_" + Path.GetRandomFileName());

            try
            {
                string directory_Source = Path.Combine(directory_Work, "images");
                Directory.CreateDirectory(directory_Source);

                //Taken in name order rather than at random, so a disagreement can be looked at again on the same images
                List<string> paths_Image = [.. Directory.GetFiles(directory_Image!, "*.jpeg").OrderBy(x => x, StringComparer.Ordinal).Take(count_Sample)];
                if (paths_Image.Count == 0)
                {
                    return;
                }

                foreach (string path_Image in paths_Image)
                {
                    File.Copy(path_Image, Path.Combine(directory_Source, Path.GetFileName(path_Image)));
                }

                string path_Output_Python = Path.Combine(directory_Work, "python", "results.bbrf");
                string path_Output_ONNX = Path.Combine(directory_Work, "onnx", "results.bbrf");

                YOLOPredictionOptions? yOLOPredictionOptions = YOLO.Create.YOLOPredictionOptions(null, path_Model, directory_Source, path_Output_Python, Path.Combine(directory_Work, "scripts"), 0.1, 32);
                if (yOLOPredictionOptions == null)
                {
                    //No CPython carrying ultralytics on this machine, so there is nothing to compare against
                    return;
                }

                Stopwatch stopwatch_Python = Stopwatch.StartNew();
                YOLOPredictionResult? yOLOPredictionResult = YOLO.Modify.Predict(yOLOPredictionOptions);
                stopwatch_Python.Stop();

                Assert.NotNull(yOLOPredictionResult);
                Assert.True(yOLOPredictionResult!.Succeeded, string.Join(Environment.NewLine, yOLOPredictionResult.StandardError ?? []));

                Classes.YOLOONNXPredictionOptions? yOLOONNXPredictionOptions = Create.YOLOONNXPredictionOptions(path_ModelONNX, directory_Source, path_Output_ONNX, 0.1);
                Assert.NotNull(yOLOONNXPredictionOptions);

                //One image through the session first, so that the measurement below is of inference rather than of the session
                //warming up. It has to be its own directory of one file: pointed at the sample directory this would score the
                //whole set a second time, doubling the run for nothing.
                string directory_Warmup = Path.Combine(directory_Work, "warmup", "images");
                Directory.CreateDirectory(directory_Warmup);
                File.Copy(paths_Image[0], Path.Combine(directory_Warmup, Path.GetFileName(paths_Image[0])));

                Modify.Predict(Create.YOLOONNXPredictionOptions(path_ModelONNX, directory_Warmup, Path.Combine(directory_Work, "warmup", "results.bbrf"), 0.99));

                Stopwatch stopwatch_ONNX = Stopwatch.StartNew();
                Classes.YOLOONNXPredictionResult? yOLOONNXPredictionResult = Modify.Predict(yOLOONNXPredictionOptions);
                stopwatch_ONNX.Stop();

                Assert.NotNull(yOLOONNXPredictionResult);
                Assert.True(yOLOONNXPredictionResult!.Succeeded, string.Join(Environment.NewLine, yOLOONNXPredictionResult.Messages ?? []));

                BoundingBoxResultFile? boundingBoxResultFile_Python = YOLO.Create.BoundingBoxResultFile(yOLOPredictionResult);
                BoundingBoxResultFile? boundingBoxResultFile_ONNX = Create.BoundingBoxResultFile(yOLOONNXPredictionResult);

                Assert.NotNull(boundingBoxResultFile_Python);
                Assert.NotNull(boundingBoxResultFile_ONNX);

                Dictionary<string, List<BoundingBoxResult>> dictionary_Python = Grouped(boundingBoxResultFile_Python!);
                Dictionary<string, List<BoundingBoxResult>> dictionary_ONNX = Grouped(boundingBoxResultFile_ONNX!);

                static Dictionary<string, List<BoundingBoxResult>> Grouped(BoundingBoxResultFile boundingBoxResultFile)
                {
                    Dictionary<string, List<BoundingBoxResult>> result = [];

                    foreach (BoundingBoxResult boundingBoxResult in boundingBoxResultFile)
                    {
                        string? name = boundingBoxResult?.Name;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        if (!result.TryGetValue(name!, out List<BoundingBoxResult>? boundingBoxResults))
                        {
                            boundingBoxResults = [];
                            result[name!] = boundingBoxResults;
                        }

                        boundingBoxResults.Add(boundingBoxResult!);
                    }

                    return result;
                }

                static double IntersectionOverUnion(BoundingBoxResult first, BoundingBoxResult second)
                {
                    double width = Math.Min(first.X + first.Width, second.X + second.Width) - Math.Max(first.X, second.X);
                    double height = Math.Min(first.Y + first.Height, second.Y + second.Height) - Math.Max(first.Y, second.Y);

                    if (width <= 0 || height <= 0)
                    {
                        return 0;
                    }

                    double area_Intersection = width * height;
                    double area_Union = (first.Width * first.Height) + (second.Width * second.Height) - area_Intersection;

                    return area_Union <= 0 ? 0 : area_Intersection / area_Union;
                }

                int count_Image_Agreed = 0;
                int count_Matched = 0;
                int count_Unmatched = 0;
                int count_Detection_Python = 0;
                int count_Detection_ONNX = 0;
                double deviation_Box = 0;
                double deviation_Confidence = 0;

                //Kept so the worst case can be described rather than just scored. A single large number says nothing
                //about whether it is a coordinate defect or one pair of near-tied candidates the two paths broke apart
                //differently, and those want opposite responses.
                List<double> deviations_Box = [];
                double iou_Worst = 1;
                string worst = "none";

                List<string> disagreements = [];

                foreach (string path_Image in paths_Image)
                {
                    string name = Path.GetFileNameWithoutExtension(path_Image);

                    List<BoundingBoxResult> boundingBoxResults_Python = dictionary_Python.TryGetValue(name, out List<BoundingBoxResult>? values_Python) ? values_Python : [];
                    List<BoundingBoxResult> boundingBoxResults_ONNX = dictionary_ONNX.TryGetValue(name, out List<BoundingBoxResult>? values_ONNX) ? values_ONNX : [];

                    count_Detection_Python += boundingBoxResults_Python.Count;
                    count_Detection_ONNX += boundingBoxResults_ONNX.Count;

                    //A detection whose confidence sits on the reporting threshold flips between the two graphs on its last bits alone, so it is not held against either of them
                    int count_Confident_Python = boundingBoxResults_Python.Count(x => x.Confidence > 0.1 + guardBand);
                    int count_Confident_ONNX = boundingBoxResults_ONNX.Count(x => x.Confidence > 0.1 + guardBand);

                    if (count_Confident_Python == count_Confident_ONNX)
                    {
                        count_Image_Agreed++;
                    }
                    else
                    {
                        disagreements.Add(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2} |", name, count_Confident_Python, count_Confident_ONNX));
                    }

                    List<BoundingBoxResult> boundingBoxResults_Remaining = [.. boundingBoxResults_ONNX];

                    foreach (BoundingBoxResult boundingBoxResult_Python in boundingBoxResults_Python)
                    {
                        BoundingBoxResult? boundingBoxResult_Best = null;
                        double iou_Best = 0;

                        foreach (BoundingBoxResult boundingBoxResult_ONNX in boundingBoxResults_Remaining)
                        {
                            if (boundingBoxResult_ONNX.LabelIndex != boundingBoxResult_Python.LabelIndex)
                            {
                                continue;
                            }

                            double iou = IntersectionOverUnion(boundingBoxResult_Python, boundingBoxResult_ONNX);
                            if (iou > iou_Best)
                            {
                                iou_Best = iou;
                                boundingBoxResult_Best = boundingBoxResult_ONNX;
                            }
                        }

                        if (boundingBoxResult_Best == null || iou_Best <= 0.5)
                        {
                            //Only a detection that is confidently reported on the Python side is expected to have a partner
                            if (boundingBoxResult_Python.Confidence > 0.1 + guardBand)
                            {
                                count_Unmatched++;
                            }

                            continue;
                        }

                        boundingBoxResults_Remaining.Remove(boundingBoxResult_Best);
                        count_Matched++;

                        double deviation = Math.Max(Math.Max(Math.Abs(boundingBoxResult_Python.X - boundingBoxResult_Best.X), Math.Abs(boundingBoxResult_Python.Y - boundingBoxResult_Best.Y)), Math.Max(Math.Abs(boundingBoxResult_Python.Width - boundingBoxResult_Best.Width), Math.Abs(boundingBoxResult_Python.Height - boundingBoxResult_Best.Height)));

                        deviations_Box.Add(deviation);
                        iou_Worst = Math.Min(iou_Worst, iou_Best);

                        if (deviation > deviation_Box)
                        {
                            deviation_Box = deviation;
                            worst = string.Format(CultureInfo.InvariantCulture, "{0}: CPython ({1:F3}, {2:F3}, {3:F3}, {4:F3}) conf {5:F6} of {6} detections; ONNX ({7:F3}, {8:F3}, {9:F3}, {10:F3}) conf {11:F6} of {12} detections; IoU {13:F6}", name, boundingBoxResult_Python.X, boundingBoxResult_Python.Y, boundingBoxResult_Python.Width, boundingBoxResult_Python.Height, boundingBoxResult_Python.Confidence, boundingBoxResults_Python.Count, boundingBoxResult_Best.X, boundingBoxResult_Best.Y, boundingBoxResult_Best.Width, boundingBoxResult_Best.Height, boundingBoxResult_Best.Confidence, boundingBoxResults_ONNX.Count, iou_Best);
                        }

                        deviation_Confidence = Math.Max(deviation_Confidence, Math.Abs(boundingBoxResult_Python.Confidence - boundingBoxResult_Best.Confidence));
                    }
                }

                double agreement = (double)count_Image_Agreed / paths_Image.Count;

                StringBuilder stringBuilder = new();
                stringBuilder.AppendLine("# ONNX / CPython detection parity");
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Run: {0:yyyy-MM-dd HH:mm:ss}", DateTimeOffset.Now));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Checkpoint: {0}", path_Model));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "ONNX: {0}", path_ModelONNX));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Images: {0} from {1}", paths_Image.Count, directory_Image));
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("| | |");
                stringBuilder.AppendLine("|---|---|");
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Detections, CPython | {0} |", count_Detection_Python));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Detections, ONNX | {0} |", count_Detection_ONNX));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Matched pairs | {0} |", count_Matched));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Unmatched, confident | {0} |", count_Unmatched));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Images agreeing on count | {0} of {1} ({2:P2}) |", count_Image_Agreed, paths_Image.Count, agreement));
                deviations_Box.Sort();

                double Percentile(double fraction)
                {
                    return deviations_Box.Count == 0 ? 0 : deviations_Box[Math.Min(deviations_Box.Count - 1, (int)(deviations_Box.Count * fraction))];
                }

                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Median box deviation, px | {0:F6} |", Percentile(0.5)));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| 99th percentile box deviation, px | {0:F6}{1} |", Percentile(0.99), count_Matched >= count_Percentile_Minimum ? string.Empty : " (not asserted - too few matched pairs)"));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Box deviations over 1 px | {0} of {1} |", deviations_Box.Count(x => x > 1.0), deviations_Box.Count));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Worst box deviation, px | {0:F6} |", deviation_Box));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Worst matched overlap | {0:F6} |", iou_Worst));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| Worst confidence deviation | {0:F6} |", deviation_Confidence));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| CPython | {0} ms total, {1:F1} ms/image |", stopwatch_Python.ElapsedMilliseconds, (double)stopwatch_Python.ElapsedMilliseconds / paths_Image.Count));
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| ONNX | {0} ms total, {1:F1} ms/image |", stopwatch_ONNX.ElapsedMilliseconds, (double)stopwatch_ONNX.ElapsedMilliseconds / paths_Image.Count));

                stringBuilder.AppendLine();
                stringBuilder.AppendLine("## Worst matched pair");
                stringBuilder.AppendLine();
                stringBuilder.AppendLine(worst);

                if (disagreements.Count != 0)
                {
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("## Images disagreeing on detection count");
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine("| Image | CPython | ONNX |");
                    stringBuilder.AppendLine("|---|---|---|");
                    foreach (string disagreement in disagreements.Take(50))
                    {
                        stringBuilder.AppendLine(disagreement);
                    }
                }

                string? directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
                if (!string.IsNullOrWhiteSpace(directory_Reports))
                {
                    File.WriteAllText(Path.Combine(directory_Reports!, string.Format(CultureInfo.InvariantCulture, "DiGi.YOLO.ONNX_Parity_{0:yyyyMMddHHmmss}.md", DateTimeOffset.Now)), stringBuilder.ToString());
                }

                //The two result files are the evidence for anything below that does not hold, and the working directory is
                //about to be deleted, so they are kept beside the report
                if (!string.IsNullOrWhiteSpace(directory_Reports) && ((count_Matched >= count_Percentile_Minimum && Percentile(0.99) > tolerance_BoxPercentile) || iou_Worst < iou_Minimum_Required || deviation_Confidence > tolerance_Confidence || count_Unmatched != 0 || agreement < agreement_Minimum))
                {
                    File.Copy(path_Output_Python, Path.Combine(directory_Reports!, "DiGi.YOLO.ONNX_Parity_Python.bbrf"), true);
                    File.Copy(path_Output_ONNX, Path.Combine(directory_Reports!, "DiGi.YOLO.ONNX_Parity_ONNX.bbrf"), true);
                }

                Assert.True(count_Matched > 0, "The CPython path found nothing, so there was no parity to measure.");
                Assert.Equal(0, count_Unmatched);
                Assert.True(agreement >= agreement_Minimum, string.Format(CultureInfo.InvariantCulture, "Detection counts agreed on {0:P2} of images, below the stated {1:P2}.", agreement, agreement_Minimum));
                if (count_Matched >= count_Percentile_Minimum)
                {
                    Assert.True(Percentile(0.99) <= tolerance_BoxPercentile, string.Format(CultureInfo.InvariantCulture, "99th percentile box deviation {0:F6} px exceeds the stated {1:F2} px (worst {2:F6} px, median {3:F6} px).", Percentile(0.99), tolerance_BoxPercentile, deviation_Box, Percentile(0.5)));
                }
                Assert.True(iou_Worst >= iou_Minimum_Required, string.Format(CultureInfo.InvariantCulture, "A matched pair overlapped by only {0:F6}, below the stated {1:F2} - the two paths are no longer describing the same building. Worst pair: {2}", iou_Worst, iou_Minimum_Required, worst));
                Assert.True(deviation_Confidence <= tolerance_Confidence, string.Format(CultureInfo.InvariantCulture, "Worst confidence deviation {0:F6} exceeds the stated {1:F2}.", deviation_Confidence, tolerance_Confidence));
            }
            finally
            {
                if (Directory.Exists(directory_Work))
                {
                    Directory.Delete(directory_Work, true);
                }
            }
        }
    }
}
