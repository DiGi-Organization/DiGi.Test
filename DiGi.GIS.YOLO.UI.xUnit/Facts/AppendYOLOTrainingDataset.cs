using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.YOLO.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies a training dataset build from a stubbed Web API: the labelling rule, the holdout, cross-part de-duplication, the manifest, the dataset&apos;s class list and path, resume, the clean-up of a half-written building, refusals of a folder that is not a dataset, and a reproducible split.
        /// <para>Three labelled buildings share the fixture footprint and its eight orthophoto years (2008 to 2023) with a label of 2014, so each gives two negative images (2008, 2010) with empty label files and six positive ones with one class 0 box. One of them is held out and must be in Test only. A fourth reference is filed under both county parts with conflicting labels; it is built once, under the lower part, and the conflict is counted. An unlabelled reference is never requested.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task AppendYOLOTrainingDataset_Build()
        {
            List<string> references_Train = References(false, 2);
            List<string> references_Test = References(true, 1);

            string reference_A = references_Train[0];
            string reference_D = references_Train[1];
            string reference_B = references_Test[0];
            string reference_Unlabelled = "UNLABELLED";

            Dictionary<int, Dictionary<string, short>> labels = new()
            {
                [1] = new() { [reference_A] = 2014, [reference_B] = 2014, [reference_D] = 2014 },
                [2] = new() { [reference_D] = 2000 }
            };

            TrainingDatasetStub trainingDatasetStub = new(labels, FixtureOrtoDatas);

            string directory = TrainingDatasetDirectory(nameof(AppendYOLOTrainingDataset_Build));
            string directory_Dataset = Path.Combine(directory, "dataset");

            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions = new()
            {
                CountyIds = [2, 1],
                OutputDirectory = directory_Dataset,
                LegacyReferencesFilePath = LegacyReferencesFile(directory, [reference_A]),
                MaxConcurrentRequests = 2
            };

            GISWebAPIManager gISWebAPIManager = new(new StubHttpClientFactory(trainingDatasetStub.Answer));

            Classes.YOLOTrainingDatasetResult? result = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
            Assert.NotNull(result);
            Assert.Empty(result!.FailedStepNames);
            Assert.False(result.CountOnly);

            Classes.YOLOTrainingDatasetCount count_1 = result.YOLOTrainingDatasetCounts.Single(x => x.CountyId == 1);
            Classes.YOLOTrainingDatasetCount count_2 = result.YOLOTrainingDatasetCounts.Single(x => x.CountyId == 2);
            Assert.Equal(3, count_1.BuildingCount);
            Assert.Equal(0, count_2.BuildingCount);
            Assert.Equal(1, count_2.DuplicateReferenceCount);
            Assert.Equal(1, count_2.LabelConflictCount);
            Assert.Equal(1, count_1.TestCount);
            Assert.Equal(2, count_1.TrainCount + count_1.ValidateCount);

            Classes.YOLOTrainingDatasetCount total = result.Total!;
            Assert.Equal(24, total.ImageCount);
            Assert.Equal(18, total.PositiveImageCount);
            Assert.Equal(6, total.NegativeImageCount);
            Assert.Equal(0, total.DroppedBoxCount);
            Assert.Equal(3, trainingDatasetStub.OrtoDatasReferences.Count);
            Assert.DoesNotContain(reference_Unlabelled, trainingDatasetStub.OrtoDatasReferences);

            // The manifest.
            List<Classes.DatasetReference>? datasetReferences = Query.DatasetReferences(Path.Combine(directory_Dataset, Constants.FileName.DatasetReferences));
            Assert.NotNull(datasetReferences);
            Assert.Equal(3, datasetReferences!.Count);

            Classes.DatasetReference datasetReference_A = datasetReferences.Single(x => x.Reference == reference_A);
            Classes.DatasetReference datasetReference_B = datasetReferences.Single(x => x.Reference == reference_B);
            Classes.DatasetReference datasetReference_D = datasetReferences.Single(x => x.Reference == reference_D);
            Assert.Equal(DiGi.YOLO.Enums.Category.Test, datasetReference_B.Category);
            Assert.NotEqual(DiGi.YOLO.Enums.Category.Test, datasetReference_A.Category);
            Assert.NotEqual(DiGi.YOLO.Enums.Category.Test, datasetReference_D.Category);
            Assert.Equal(Enums.LegacySource.Tsv, datasetReference_A.LegacySource);
            Assert.Equal(Enums.LegacySource.None, datasetReference_B.LegacySource);
            Assert.Equal(1, datasetReference_D.CountyId);
            Assert.Equal(2014, datasetReference_D.Label);

            // The dataset: class 0 is Building, the path is absolute, and the held-out building is in Test only.
            YOLOModel? yOLOModel = DiGi.YOLO.Modify.Read(Path.Combine(directory_Dataset, DiGi.YOLO.Constants.FileName.Conf));
            Assert.NotNull(yOLOModel);
            Assert.Equal(0, yOLOModel!.LabelIndex(Constants.LabelName.Building));
            Assert.True(Path.IsPathRooted(yOLOModel.Directory));
            Assert.Equal(Path.GetFullPath(directory_Dataset).TrimEnd('\\'), Path.GetFullPath(yOLOModel.Directory!).TrimEnd('\\'), StringComparer.OrdinalIgnoreCase);

            string directory_Test = yOLOModel.GetDirectory_Images(DiGi.YOLO.Enums.Category.Test)!;
            Assert.Equal(8, Directory.GetFiles(directory_Test, $"{reference_B}_*.jpeg").Length);
            Assert.Empty(Directory.GetFiles(directory_Test, $"{reference_A}_*.jpeg"));

            string directory_Images_A = yOLOModel.GetDirectory_Images(datasetReference_A.Category)!;
            string directory_Labels_A = yOLOModel.GetDirectory_Labels(datasetReference_A.Category)!;
            Assert.Equal(8, Directory.GetFiles(directory_Images_A, $"{reference_A}_*.jpeg").Length);

            // Before the label year: registered, empty label file. At the label year: one class 0 box inside the image.
            Assert.Equal(string.Empty, File.ReadAllText(Path.Combine(directory_Labels_A, $"{reference_A}_2010.txt")).Trim());

            string[] lines_2014 = File.ReadAllLines(Path.Combine(directory_Labels_A, $"{reference_A}_2014.txt")).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            string line_2014 = Assert.Single(lines_2014);
            string[] values = line_2014.Split(' ');
            Assert.Equal("0", values[0]);
            foreach (string value in values.Skip(1))
            {
                double number = double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.InRange(number, 0, 1);
            }

            // Resume: a half-written building left by a stopped run is removed, nothing complete is requested again.
            string path_Stray_Image = Path.Combine(directory_Images_A, "STRAY_2010.jpeg");
            string path_Stray_Label = Path.Combine(directory_Labels_A, "STRAY_2010.txt");
            File.Copy(Path.Combine(directory_Images_A, $"{reference_A}_2010.jpeg"), path_Stray_Image);
            File.WriteAllText(path_Stray_Label, string.Empty);

            trainingDatasetStub.OrtoDatasReferences.Clear();

            Classes.YOLOTrainingDatasetResult? result_Resume = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
            Assert.NotNull(result_Resume);
            Assert.Empty(result_Resume!.FailedStepNames);
            Assert.Equal(3, result_Resume.Total!.ResumedCount);
            Assert.Equal(0, result_Resume.Total.ImageCount);
            Assert.Empty(trainingDatasetStub.OrtoDatasReferences);
            Assert.False(File.Exists(path_Stray_Image));
            Assert.False(File.Exists(path_Stray_Label));
            Assert.Equal(24, Directory.GetFiles(Path.Combine(directory_Dataset, DiGi.YOLO.Constants.DirectoryName.Images), "*.jpeg", SearchOption.AllDirectories).Length);
            Assert.Equal(3, Query.DatasetReferences(Path.Combine(directory_Dataset, Constants.FileName.DatasetReferences))!.Count);

            // The labels survive the rewrite at the end of the resumed run.
            Assert.Single(File.ReadAllLines(Path.Combine(directory_Labels_A, $"{reference_A}_2014.txt")), x => !string.IsNullOrWhiteSpace(x));

            // With Resume off, an existing dataset is refused.
            yOLOTrainingDatasetOptions.Resume = false;
            Classes.YOLOTrainingDatasetResult? result_NoResume = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
            Assert.Contains(nameof(Classes.YOLOTrainingDatasetOptions.Resume), result_NoResume!.FailedStepNames);
            yOLOTrainingDatasetOptions.Resume = true;

            // A folder that holds something other than a dataset is not written into.
            string directory_Foreign = Path.Combine(directory, "foreign");
            Directory.CreateDirectory(directory_Foreign);
            File.WriteAllText(Path.Combine(directory_Foreign, "notes.txt"), "x");
            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions_Foreign = new(yOLOTrainingDatasetOptions) { OutputDirectory = directory_Foreign };
            Classes.YOLOTrainingDatasetResult? result_Foreign = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions_Foreign);
            Assert.Contains(nameof(Classes.YOLOTrainingDatasetOptions.OutputDirectory), result_Foreign!.FailedStepNames);
            Assert.False(Directory.Exists(Path.Combine(directory_Foreign, DiGi.YOLO.Constants.DirectoryName.Images)));

            // The same labels and seed give the same split in a fresh dataset.
            string directory_Again = Path.Combine(directory, "dataset_again");
            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions_Again = new(yOLOTrainingDatasetOptions) { OutputDirectory = directory_Again };
            Assert.NotNull(await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions_Again));

            List<Classes.DatasetReference> datasetReferences_Again = Query.DatasetReferences(Path.Combine(directory_Again, Constants.FileName.DatasetReferences))!;
            foreach (Classes.DatasetReference datasetReference in datasetReferences)
            {
                Assert.Equal(datasetReference.Category, datasetReferences_Again.Single(x => x.Reference == datasetReference.Reference).Category);
            }

            // Relative output is refused before anything is read.
            Assert.Null(await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(new Classes.YOLOTrainingDatasetOptions(yOLOTrainingDatasetOptions) { OutputDirectory = "relative" }));
        }

        /// <summary>
        /// Verifies the image rules of one building: one photo per year - the first of the year - and identical photo bytes under two years kept once when their labels agree and dropped both when they conflict.
        /// <para>With a label of 2014: two photos of 2010 keep the first; the same bytes filed under 2012 and 2016 would put the building in a photo that is also labelled as not having it, so both go; the same bytes under 2017 and 2019 agree, so 2017 is kept. Two images are written.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task AppendYOLOTrainingDataset_Duplicates()
        {
            string reference = References(false, 1)[0];

            OrtoDatas ortoDatas_Fixture = FixtureOrtoDatas(reference);
            List<OrtoData> ortoDatas_Source = [.. ortoDatas_Fixture];
            Assert.Equal(8, ortoDatas_Source.Count);

            OrtoData OrtoData(int year, int month, int index)
            {
                return new OrtoData(new DateTime(year, month, 1), ortoDatas_Source[index].Bytes, ortoDatas_Source[index].Scale, ortoDatas_Source[index].Location);
            }

            OrtoDatas ortoDatas = new(reference, [OrtoData(2010, 1, 0), OrtoData(2010, 6, 1), OrtoData(2012, 1, 2), OrtoData(2016, 1, 2), OrtoData(2017, 1, 3), OrtoData(2019, 1, 3)]);

            Dictionary<int, Dictionary<string, short>> labels = new()
            {
                [1] = new() { [reference] = 2014 }
            };

            TrainingDatasetStub trainingDatasetStub = new(labels, x => ortoDatas);

            string directory = TrainingDatasetDirectory(nameof(AppendYOLOTrainingDataset_Duplicates));
            string directory_Dataset = Path.Combine(directory, "dataset");

            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions = new()
            {
                CountyIds = [1],
                OutputDirectory = directory_Dataset,
                LegacyReferencesFilePath = LegacyReferencesFile(directory, [])
            };

            GISWebAPIManager gISWebAPIManager = new(new StubHttpClientFactory(trainingDatasetStub.Answer));

            Classes.YOLOTrainingDatasetResult? result = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
            Assert.NotNull(result);
            Assert.Empty(result!.FailedStepNames);

            Classes.YOLOTrainingDatasetCount total = result.Total!;
            Assert.Equal(2, total.ImageCount);
            Assert.Equal(1, total.SameYearImageCount);
            Assert.Equal(2, total.IdenticalImageDroppedCount);
            Assert.Equal(1, total.IdenticalImageMergedCount);
            Assert.Equal(1, total.NegativeImageCount);
            Assert.Equal(1, total.PositiveImageCount);

            string[] fileNames = [.. Directory.GetFiles(Path.Combine(directory_Dataset, DiGi.YOLO.Constants.DirectoryName.Images), "*.jpeg", SearchOption.AllDirectories).Select(Path.GetFileName).OrderBy(x => x, StringComparer.Ordinal)!];
            string[] fileNames_Expected = [$"{reference}_2010.jpeg", $"{reference}_2017.jpeg"];
            Assert.Equal(fileNames_Expected, fileNames);

            // The 2010 image is the first photo of the year, saved through the shared encoder.
            string path_Expected = Path.Combine(directory, "expected_2010.jpeg");
            Assert.True(Modify.SavePredictionImage(ortoDatas_Source[0], path_Expected, out _, out _));
            string path_2010 = Directory.GetFiles(Path.Combine(directory_Dataset, DiGi.YOLO.Constants.DirectoryName.Images), $"{reference}_2010.jpeg", SearchOption.AllDirectories).Single();
            Assert.Equal(File.ReadAllBytes(path_Expected), File.ReadAllBytes(path_2010));
        }

        /// <summary>
        /// Verifies a counting run: it requests no footprint and no orthophoto, writes nothing, reports the split, the Legacy agreement table and bounded entries of the Test buildings, the cross-part duplicates of the named parts, and an estimate - and that a history it cannot read in full makes the Test buildings Legacy-unknown instead of clean.
        /// </summary>
        [Fact]
        public async Task AppendYOLOTrainingDataset_CountOnly()
        {
            List<string> references_Train = References(false, 2);
            List<string> references_Test = References(true, 2);

            Dictionary<int, Dictionary<string, short>> labels = new()
            {
                [1] = new() { [references_Train[0]] = 1990, [references_Train[1]] = 1990, [references_Test[0]] = 1990, [references_Test[1]] = 1990 }
            };

            // The first held-out building carries an undated bounded entry: Legacy by timestamp, and a bounded entry.
            YearBuiltData yearBuiltData = new(references_Test[0]);
            Assert.True(yearBuiltData.SetUserYearBuilt(new UserYearBuilt(1990, GIS.Enums.YearBuiltRelation.AtOrBefore)));

            TrainingDatasetStub trainingDatasetStub = new(labels, FixtureOrtoDatas)
            {
                YearBuiltDatas = [yearBuiltData],
                Building2DReferenceDuplicates = [new PostgreSQL.Classes.Building2DReferenceDuplicate("X", 2, [1, 9]), new PostgreSQL.Classes.Building2DReferenceDuplicate("Y", 2, [5, 6])]
            };

            string directory = TrainingDatasetDirectory(nameof(AppendYOLOTrainingDataset_CountOnly));
            string directory_Dataset = Path.Combine(directory, "dataset");

            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions = new()
            {
                CountyIds = [1],
                OutputDirectory = directory_Dataset,
                LegacyReferencesFilePath = LegacyReferencesFile(directory, [references_Test[1], references_Train[0]]),
                CountOnly = true
            };

            GISWebAPIManager gISWebAPIManager = new(new StubHttpClientFactory(trainingDatasetStub.Answer));

            Classes.YOLOTrainingDatasetResult? result = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
            Assert.NotNull(result);
            Assert.Empty(result!.FailedStepNames);
            Assert.True(result.CountOnly);

            Assert.Empty(trainingDatasetStub.OrtoDatasReferences);
            Assert.Equal(0, trainingDatasetStub.Building2DRequestCount);
            Assert.False(Directory.Exists(directory_Dataset));

            Classes.YOLOTrainingDatasetCount count = Assert.Single(result.YOLOTrainingDatasetCounts);
            Assert.Equal(4, count.LabelledCount);
            Assert.Equal(4, count.BuildingCount);
            Assert.Equal(2, count.TestCount);
            Assert.Equal(1, count.LegacyTimestampCount);
            Assert.Equal(1, count.LegacyTsvCount);
            Assert.Equal(0, count.LegacyBothCount);
            Assert.Equal(0, count.LegacyNoneCount);
            Assert.Equal(1, count.BoundedEntryCount);
            Assert.Equal(1, count.ReferenceDuplicateCount);
            Assert.Equal(4 * Constants.Count.ImagePerBuilding_Estimate, count.EstimatedImageCount);
            Assert.Equal(count.EstimatedImageCount * Constants.Count.ImageByte_Estimate, count.EstimatedByteCount);
            Assert.Equal(5, count.EstimatedRequestCount);

            // The history cannot be read: the county part's Test buildings are unknown, never clean, and the step says so.
            trainingDatasetStub.YearBuiltDataStatusCode = HttpStatusCode.InternalServerError;

            Classes.YOLOTrainingDatasetResult? result_Failed = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
            Assert.NotNull(result_Failed);
            Assert.Contains(nameof(Query.StoredYearBuiltDatasAsync), result_Failed!.FailedStepNames);
            Assert.Equal(2, result_Failed.Total!.LegacyUnknownCount);
            Assert.Equal(0, result_Failed.Total.LegacyNoneCount + result_Failed.Total.LegacyTsvCount + result_Failed.Total.LegacyTimestampCount + result_Failed.Total.LegacyBothCount);

            // A label page that cannot be read refuses the county rather than labelling part of it.
            trainingDatasetStub.YearBuiltDataStatusCode = null;
            trainingDatasetStub.LabelStatusCode = HttpStatusCode.InternalServerError;

            Classes.YOLOTrainingDatasetResult? result_Labels = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
            Assert.Contains(nameof(Query.UserYearBuiltsAsync), result_Labels!.FailedStepNames);
            Assert.Equal(0, result_Labels.Total!.BuildingCount);

            // No legacy list: refused, never read as an empty one.
            trainingDatasetStub.LabelStatusCode = null;
            Classes.YOLOTrainingDatasetResult? result_NoList = await gISWebAPIManager.AppendYOLOTrainingDatasetAsync(new Classes.YOLOTrainingDatasetOptions(yOLOTrainingDatasetOptions) { LegacyReferencesFilePath = Path.Combine(directory, "missing.tsv") });
            Assert.Contains(nameof(Query.LegacyReferences), result_NoList!.FailedStepNames);
        }

        /// <summary>
        /// Answers the Web API calls a training dataset build makes, from in-memory labels, histories and orthophotos, and records what was asked.
        /// </summary>
        private class TrainingDatasetStub
        {
            private readonly Dictionary<int, Dictionary<string, short>> labels;
            private readonly Func<string, OrtoDatas?> ortoDatas;
            private readonly Building2D building2D_Fixture;

            /// <summary>
            /// Initializes a new instance of the <see cref="TrainingDatasetStub"/> class.
            /// </summary>
            /// <param name="labels">The label year of each reference, by county part.</param>
            /// <param name="ortoDatas">The orthophotos of a reference.</param>
            public TrainingDatasetStub(Dictionary<int, Dictionary<string, short>> labels, Func<string, OrtoDatas?> ortoDatas)
            {
                this.labels = labels;
                this.ortoDatas = ortoDatas;

                string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoDatas_BoundingBox2D_Building2D.json");
                building2D_Fixture = Core.Convert.ToDiGi<Building2D>((Core.Classes.Path)path!)!.First();
            }

            /// <summary>
            /// Gets the cross-part duplicates the duplicates endpoint answers with.
            /// </summary>
            public List<PostgreSQL.Classes.Building2DReferenceDuplicate> Building2DReferenceDuplicates { get; set; } = [];

            /// <summary>
            /// Gets the number of footprint requests made.
            /// </summary>
            public int Building2DRequestCount { get; private set; }

            /// <summary>
            /// Gets or sets the status the label pages are answered with instead of the labels, or null for the labels.
            /// </summary>
            public HttpStatusCode? LabelStatusCode { get; set; }

            /// <summary>
            /// Gets the references whose orthophotos were requested.
            /// </summary>
            public List<string> OrtoDatasReferences { get; } = [];

            /// <summary>
            /// Gets or sets the stored histories the history endpoint answers with.
            /// </summary>
            public List<YearBuiltData> YearBuiltDatas { get; set; } = [];

            /// <summary>
            /// Gets or sets the status the history endpoint is answered with instead of the histories, or null for the histories.
            /// </summary>
            public HttpStatusCode? YearBuiltDataStatusCode { get; set; }

            /// <summary>
            /// Answers one request.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <returns>The response.</returns>
            public HttpResponseMessage Answer(HttpRequestMessage request)
            {
                string path = request.RequestUri?.AbsolutePath.ToLowerInvariant() ?? string.Empty;

                if (path.EndsWith("/buildingdata/tablebybuildingdatabypagingparameter", StringComparison.Ordinal))
                {
                    if (LabelStatusCode is HttpStatusCode httpStatusCode_Label)
                    {
                        return new HttpResponseMessage(httpStatusCode_Label) { Content = new StringContent("failed") };
                    }

                    JsonObject? jsonObject = JsonNode.Parse(ReadBody(request)) as JsonObject;
                    int countyId = jsonObject?["CountyId"]?.GetValue<int>() ?? 0;

                    labels.TryGetValue(countyId, out Dictionary<string, short>? labels_County);
                    return Json(LabelTableJson(labels_County ?? []));
                }

                if (path.EndsWith("/yearbuiltdata/itemsbyreferences", StringComparison.Ordinal))
                {
                    if (YearBuiltDataStatusCode is HttpStatusCode httpStatusCode_YearBuiltData)
                    {
                        return new HttpResponseMessage(httpStatusCode_YearBuiltData) { Content = new StringContent("failed") };
                    }

                    HashSet<string> references = ReferencesOf(request);
                    List<YearBuiltData> yearBuiltDatas = [.. YearBuiltDatas.Where(x => references.Contains(x.Reference!))];
                    return yearBuiltDatas.Count == 0 ? new HttpResponseMessage(HttpStatusCode.NoContent) : Json(Core.Convert.ToSystem_String(yearBuiltDatas) ?? string.Empty);
                }

                if (path.EndsWith("/building2d/itemsbyreferences", StringComparison.Ordinal))
                {
                    lock (this)
                    {
                        Building2DRequestCount++;
                    }

                    List<Building2D> building2Ds = [];
                    foreach (string reference in ReferencesOf(request))
                    {
                        building2Ds.Add(new Building2D(Guid.NewGuid(), reference, building2D_Fixture.PolygonalFace2D, 1, null, null, []));
                    }

                    return Json(Core.Convert.ToSystem_String(building2Ds) ?? string.Empty);
                }

                if (path.EndsWith("/building2d/referenceduplicates", StringComparison.Ordinal))
                {
                    return Json(Core.Convert.ToSystem_String(Building2DReferenceDuplicates) ?? "[]");
                }

                if (path.EndsWith("/ortodatas/itembyreference", StringComparison.Ordinal))
                {
                    string reference = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query)["reference"] ?? string.Empty;

                    lock (this)
                    {
                        if (!OrtoDatasReferences.Contains(reference))
                        {
                            OrtoDatasReferences.Add(reference);
                        }
                    }

                    OrtoDatas? ortoDatas_Reference = ortoDatas(reference);
                    return ortoDatas_Reference is null ? new HttpResponseMessage(HttpStatusCode.NoContent) : Json(Core.Convert.ToSystem_String((Core.Interfaces.ISerializableObject)ortoDatas_Reference) ?? string.Empty);
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            private static HashSet<string> ReferencesOf(HttpRequestMessage request)
            {
                HashSet<string> result = new(StringComparer.Ordinal);
                if (JsonNode.Parse(ReadBody(request)) is JsonArray jsonArray)
                {
                    foreach (JsonNode? jsonNode in jsonArray)
                    {
                        if (jsonNode?.ToString() is string reference)
                        {
                            result.Add(reference);
                        }
                    }
                }

                return result;
            }

            private static HttpResponseMessage Json(string json)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        }

        /// <summary>
        /// Builds the JSON a label page is answered with: the reference and the stored user year built of each building.
        /// </summary>
        /// <param name="labels">The label year of each reference.</param>
        /// <returns>The serialized table.</returns>
        private static string LabelTableJson(Dictionary<string, short> labels)
        {
            List<Column> columns = [IO.Constants.Column.Reference, IO.Constants.Column.UserYearBuilt];

            Table table = new(columns);
            foreach (KeyValuePair<string, short> keyValuePair in labels.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                table.AddRow([keyValuePair.Key, (ushort)keyValuePair.Value]);
            }

            JsonSerializerOptions jsonSerializerOptions = new();
            jsonSerializerOptions.Converters.Add(new TableConverter<Table, Column, Row>());

            return JsonSerializer.Serialize(table, jsonSerializerOptions);
        }

        /// <summary>
        /// Answers the fixture orthophotos - eight years, 2008 to 2023, of one 320 by 320 pixel crop - filed under the given reference.
        /// </summary>
        /// <param name="reference">The reference to file them under.</param>
        /// <returns>The orthophotos.</returns>
        private static OrtoDatas FixtureOrtoDatas(string reference)
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoDatas_BoundingBox2D_OrtoDatas.json");
            Assert.False(string.IsNullOrWhiteSpace(path));

            OrtoDatas? ortoDatas = Core.Convert.ToDiGi<OrtoDatas>((Core.Classes.Path)path!)?.FirstOrDefault();
            Assert.NotNull(ortoDatas);

            return new OrtoDatas(reference, ortoDatas!);
        }

        /// <summary>
        /// Answers references the holdout rule puts in Test, or keeps out of it, so a fact can name buildings of either kind.
        /// </summary>
        /// <param name="holdout">True for held-out references.</param>
        /// <param name="count">The number of references.</param>
        /// <returns>The references.</returns>
        private static List<string> References(bool holdout, int count)
        {
            List<string> result = [];
            for (int i = 0; result.Count < count; i++)
            {
                string reference = string.Format(System.Globalization.CultureInfo.InvariantCulture, "REF_{0:D4}", i);
                if (IO.Query.Holdout(reference) == holdout)
                {
                    result.Add(reference);
                }
            }

            return result;
        }

        /// <summary>
        /// Answers an emptied reports folder for one fact.
        /// </summary>
        /// <param name="name">The name of the fact.</param>
        /// <returns>The folder.</returns>
        private static string TrainingDatasetDirectory(string name)
        {
            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory = Path.Combine(directory_Reports!, name);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>
        /// Writes a legacy reference list naming the given references.
        /// </summary>
        /// <param name="directory">The folder to write it to.</param>
        /// <param name="references">The references it names.</param>
        /// <returns>The path of the list.</returns>
        private static string LegacyReferencesFile(string directory, IEnumerable<string> references)
        {
            string path = Path.Combine(directory, "legacy.tsv");

            List<string> lines = ["Reference\tYear Built"];
            lines.AddRange(references.Select(x => x + "\t1990"));
            File.WriteAllLines(path, lines);

            return path;
        }
    }
}
