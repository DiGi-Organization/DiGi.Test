using DiGi.GIS.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the exclusion rule of a training dataset build: a building whose footprint envelope, grown by <c>ExclusionBuffer</c>, touches or crosses an exclusion box is dropped before the holdout and the seeded split - inside the box, within the buffer and exactly on the edge alike - and appears in no manifest row, no split and no image request; a box beyond the buffer excludes nothing, and an empty list reproduces the split of unconfigured options exactly.
        /// <para>The stub footprints every reference with the same fixture footprint, so one box excludes the whole county part; the box coordinates are read off that footprint&apos;s envelope, computed the way the builder computes it. The kept run with an active box must show two footprint requests - the exclusion read plus the build&apos;s own read - while the empty-list run shows one, which is what makes the empty list a no-op rather than merely a run that excludes nothing.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task AppendYOLOTrainingDataset_Exclusion()
        {
            List<string> references_Train = References(false, 2);
            List<string> references_Test = References(true, 1);

            string reference_A = references_Train[0];
            string reference_D = references_Train[1];
            string reference_B = references_Test[0];

            Dictionary<int, Dictionary<string, short>> labels = new()
            {
                [1] = new() { [reference_A] = 2014, [reference_B] = 2014, [reference_D] = 2014 }
            };

            TrainingDatasetStub trainingDatasetStub = new(labels, FixtureOrtoDatas);

            string directory = TrainingDatasetDirectory(nameof(AppendYOLOTrainingDataset_Exclusion));
            string path_Legacy = LegacyReferencesFile(directory, [reference_A]);

            GISWebAPIManager gisWebAPIManager = new(new StubHttpClientFactory(trainingDatasetStub.Answer));

            BoundingBox2D boundingBox2D_Envelope = FixtureFootprintEnvelope();
            double minX = boundingBox2D_Envelope.Min.X;
            double minY = boundingBox2D_Envelope.Min.Y;
            double maxX = boundingBox2D_Envelope.Max.X;
            double maxY = boundingBox2D_Envelope.Max.Y;

            // One run of the builder: fresh options, fresh dataset folder, and how many footprint requests it made.
            async Task<(Classes.YOLOTrainingDatasetResult Result, int FootprintRequests)> RunAsync(string name, List<Classes.YOLOExclusionBoundingBox>? yOLOExclusionBoundingBoxes, double exclusionBuffer)
            {
                Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions = new()
                {
                    CountyIds = [1],
                    OutputDirectory = Path.Combine(directory, name),
                    LegacyReferencesFilePath = path_Legacy
                };

                // Null means the members stay unset, so the run is exactly what the class defaults describe.
                if (yOLOExclusionBoundingBoxes is not null)
                {
                    yOLOTrainingDatasetOptions.ExclusionBoundingBoxes = yOLOExclusionBoundingBoxes;
                    yOLOTrainingDatasetOptions.ExclusionBuffer = exclusionBuffer;
                }

                trainingDatasetStub.OrtoDatasReferences.Clear();
                int footprintRequests = trainingDatasetStub.Building2DRequestCount;

                Classes.YOLOTrainingDatasetResult? result = await gisWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
                Assert.NotNull(result);
                Assert.Empty(result!.FailedStepNames);

                return (result, trainingDatasetStub.Building2DRequestCount - footprintRequests);
            }

            // A kept run leaves every reference in the manifest, under the categories of the seeded split.
            Dictionary<string, DiGi.YOLO.Enums.Category> Categories(string name)
            {
                List<Classes.DatasetReference>? datasetReferences = Query.DatasetReferences(Path.Combine(directory, name, Constants.FileName.DatasetReferences));
                Assert.NotNull(datasetReferences);
                return datasetReferences!.ToDictionary(x => x.Reference!, x => x.Category, StringComparer.Ordinal);
            }

            // A dropped run leaves a dataset with nothing in it: no manifest row, no image.
            void AssertExcluded(string name, Classes.YOLOTrainingDatasetResult result, int footprintRequests)
            {
                Classes.YOLOTrainingDatasetCount total = result.Total!;
                Assert.Equal(3, total.LabelledCount);
                Assert.Equal(3, total.ExcludedCount);
                Assert.Equal(0, total.BuildingCount);
                Assert.Equal(0, total.TrainCount + total.ValidateCount + total.TestCount);
                Assert.Equal(0, total.ImageCount);
                Assert.Empty(trainingDatasetStub.OrtoDatasReferences);
                Assert.Equal(1, footprintRequests);

                string directory_Dataset = Path.Combine(directory, name);
                List<Classes.DatasetReference>? datasetReferences = Query.DatasetReferences(Path.Combine(directory_Dataset, Constants.FileName.DatasetReferences));
                Assert.NotNull(datasetReferences);
                Assert.Empty(datasetReferences!);
                Assert.Empty(Directory.GetFiles(directory_Dataset, "*.jpeg", SearchOption.AllDirectories));
            }

            // Inside the box: dropped, with no buffer needed.
            (Classes.YOLOTrainingDatasetResult result_Inside, int requests_Inside) = await RunAsync("inside", [new Classes.YOLOExclusionBoundingBox(minX - 10, minY - 10, maxX + 10, maxY + 10)], 0);
            AssertExcluded("inside", result_Inside, requests_Inside);

            // Outside the box but within the 100 m buffer: dropped as well.
            (Classes.YOLOTrainingDatasetResult result_Buffer, int requests_Buffer) = await RunAsync("within_buffer", [new Classes.YOLOExclusionBoundingBox(maxX + 50, minY - 10, maxX + 250, maxY + 10)], 100);
            AssertExcluded("within_buffer", result_Buffer, requests_Buffer);

            // The grown envelope reaching the box edge exactly still counts: an edge that only touches is inside.
            (Classes.YOLOTrainingDatasetResult result_Edge, int requests_Edge) = await RunAsync("edge_touching", [new Classes.YOLOExclusionBoundingBox(maxX + 100, minY - 10, maxX + 300, maxY + 10)], 100);
            AssertExcluded("edge_touching", result_Edge, requests_Edge);

            // A box clear of the buffer excludes nothing: the split and the imagery are the ordinary ones.
            (Classes.YOLOTrainingDatasetResult result_Beyond, int requests_Beyond) = await RunAsync("beyond_buffer", [new Classes.YOLOExclusionBoundingBox(maxX + 200, minY - 10, maxX + 400, maxY + 10)], 100);
            Classes.YOLOTrainingDatasetCount total_Beyond = result_Beyond.Total!;
            Assert.Equal(0, total_Beyond.ExcludedCount);
            Assert.Equal(3, total_Beyond.BuildingCount);
            Assert.Equal(1, total_Beyond.TestCount);
            Assert.Equal(2, total_Beyond.TrainCount + total_Beyond.ValidateCount);
            Assert.Equal(2, requests_Beyond);

            // An explicitly empty list reads no footprint for the check - only the build's own read - and drops nothing.
            (Classes.YOLOTrainingDatasetResult result_Empty, int requests_Empty) = await RunAsync("empty_list", [], 100);
            Classes.YOLOTrainingDatasetCount total_Empty = result_Empty.Total!;
            Assert.Equal(0, total_Empty.ExcludedCount);
            Assert.Equal(3, total_Empty.BuildingCount);
            Assert.Equal(1, requests_Empty);

            // Options that never mention the exclusion: identical to the empty list - today's behaviour.
            (Classes.YOLOTrainingDatasetResult result_Default, int requests_Default) = await RunAsync("default", null, 0);
            Classes.YOLOTrainingDatasetCount total_Default = result_Default.Total!;
            Assert.Equal(0, total_Default.ExcludedCount);
            Assert.Equal(3, total_Default.BuildingCount);
            Assert.Equal(1, total_Default.TestCount);
            Assert.Equal(1, requests_Default);

            // The same references, under the same categories, in all three kept runs.
            Dictionary<string, DiGi.YOLO.Enums.Category> categories_Beyond = Categories("beyond_buffer");
            Assert.Equal(3, categories_Beyond.Count);
            Assert.Equal(1, categories_Beyond.Count(x => x.Value == DiGi.YOLO.Enums.Category.Test));

            Dictionary<string, DiGi.YOLO.Enums.Category> categories_Empty = Categories("empty_list");
            Dictionary<string, DiGi.YOLO.Enums.Category> categories_Default = Categories("default");
            foreach (KeyValuePair<string, DiGi.YOLO.Enums.Category> keyValuePair in categories_Beyond)
            {
                Assert.Equal(keyValuePair.Value, categories_Empty[keyValuePair.Key]);
                Assert.Equal(keyValuePair.Value, categories_Default[keyValuePair.Key]);
            }
        }

        /// <summary>
        /// Verifies a counting run with an exclusion box over both county parts: <c>CountOnly</c> reports the excluded count per county part - the labelled buildings stay counted, every split and estimate drops to zero - and the audit requests no orthophoto, writes nothing, and reads exactly one footprint page per county part.
        /// </summary>
        [Fact]
        public async Task AppendYOLOTrainingDataset_ExclusionCountOnly()
        {
            List<string> references_Train = References(false, 4);
            List<string> references_Test = References(true, 2);

            Dictionary<int, Dictionary<string, short>> labels = new()
            {
                [1] = new()
                {
                    [references_Train[0]] = 1990,
                    [references_Train[1]] = 1990,
                    [references_Test[0]] = 1990,
                    [references_Test[1]] = 1990
                },
                [2] = new()
                {
                    [references_Train[2]] = 1990,
                    [references_Train[3]] = 1990
                }
            };

            TrainingDatasetStub trainingDatasetStub = new(labels, FixtureOrtoDatas);

            string directory = TrainingDatasetDirectory(nameof(AppendYOLOTrainingDataset_ExclusionCountOnly));
            string directory_Dataset = Path.Combine(directory, "dataset");

            BoundingBox2D boundingBox2D_Envelope = FixtureFootprintEnvelope();

            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions = new()
            {
                CountyIds = [1, 2],
                OutputDirectory = directory_Dataset,
                LegacyReferencesFilePath = LegacyReferencesFile(directory, []),
                CountOnly = true,
                ExclusionBoundingBoxes = [new Classes.YOLOExclusionBoundingBox(boundingBox2D_Envelope.Min.X - 10, boundingBox2D_Envelope.Min.Y - 10, boundingBox2D_Envelope.Max.X + 10, boundingBox2D_Envelope.Max.Y + 10)]
            };

            GISWebAPIManager gisWebAPIManager = new(new StubHttpClientFactory(trainingDatasetStub.Answer));

            Classes.YOLOTrainingDatasetResult? result = await gisWebAPIManager.AppendYOLOTrainingDatasetAsync(yOLOTrainingDatasetOptions);
            Assert.NotNull(result);
            Assert.Empty(result!.FailedStepNames);
            Assert.True(result.CountOnly);

            Classes.YOLOTrainingDatasetCount count_1 = result.YOLOTrainingDatasetCounts.Single(x => x.CountyId == 1);
            Classes.YOLOTrainingDatasetCount count_2 = result.YOLOTrainingDatasetCounts.Single(x => x.CountyId == 2);

            // County part 1: four labelled, four excluded, nothing split - and the estimate counts the exclusion's
            // own footprint page on top of a build that would now make none.
            Assert.Equal(4, count_1.LabelledCount);
            Assert.Equal(4, count_1.ExcludedCount);
            Assert.Equal(0, count_1.BuildingCount);
            Assert.Equal(0, count_1.TrainCount + count_1.ValidateCount + count_1.TestCount);
            Assert.Equal(1, count_1.EstimatedRequestCount);

            // County part 2: two labelled, two excluded.
            Assert.Equal(2, count_2.LabelledCount);
            Assert.Equal(2, count_2.ExcludedCount);
            Assert.Equal(0, count_2.BuildingCount);
            Assert.Equal(1, count_2.EstimatedRequestCount);

            Assert.Equal(6, result.Total!.ExcludedCount);

            // The audit asked for no orthophoto, wrote nothing, and read one footprint page per county part.
            Assert.Empty(trainingDatasetStub.OrtoDatasReferences);
            Assert.False(Directory.Exists(directory_Dataset));
            Assert.Equal(2, trainingDatasetStub.Building2DRequestCount);
        }

        /// <summary>
        /// Answers the envelope of the shared stub footprint, computed the way the builder computes it before every exclusion test.
        /// </summary>
        /// <returns>The footprint envelope.</returns>
        private static BoundingBox2D FixtureFootprintEnvelope()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoDatas_BoundingBox2D_Building2D.json");
            Assert.False(string.IsNullOrWhiteSpace(path));

            Building2D? building2D = Core.Convert.ToDiGi<Building2D>((Core.Classes.Path)path!)?.FirstOrDefault();
            Assert.NotNull(building2D);

            BoundingBox2D? boundingBox2D = GIS.Create.Building2DGeometryCalculationResult(building2D!)?.BoundingBox;
            Assert.NotNull(boundingBox2D);

            return boundingBox2D!;
        }
    }
}
