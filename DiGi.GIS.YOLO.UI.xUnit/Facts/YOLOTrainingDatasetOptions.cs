using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that YOLOTrainingDatasetOptions keeps every value it is given, survives its string form, clones identically, and that its copy constructor copies every member.
        /// <para>Every member is set to something other than its default. <c>SerializationCheck</c> compares the JSON of the instance with the JSON of its clone and never runs the copy constructor, so the copy constructor is asserted member by member here as well.</para>
        /// </summary>
        [Fact]
        public void YOLOTrainingDatasetOptions()
        {
            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions = new()
            {
                Confidence = 0.25,
                CountOnly = true,
                CountyIds = [73485, 73482],
                ExclusionBoundingBoxes = [new Classes.YOLOExclusionBoundingBox(1000, 2000, 1100, 2100)],
                ExclusionBuffer = 42.5,
                HoldoutDenominator = 7,
                LabelCheckOverlayCount = 5,
                LabelCheckSampleSize = 50,
                LegacyCutoff = new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.FromHours(2)),
                LegacyReferencesFilePath = @"C:\data\legacy.tsv",
                MaxConcurrentRequests = 3,
                ModelPath = @"C:\YOLO\models\model_train8.pt",
                Offset = 2.5,
                OutputDirectory = @"C:\YOLO\dataset",
                PythonPath = @"C:\Python\python.exe",
                ReferenceBatchSize = 5000,
                ReferenceDuplicateLimit = 500,
                ReportsDirectory = @"C:\reports",
                Resume = false,
                Seed = 42,
                ValidateWeight = 0.2,
                WeightsPaths = [@"C:\YOLO\models\model.pt", @"C:\YOLO\models\model_train9_fresh.pt"],
                WorkingDirectory = @"C:\YOLO\work",
                Years = new Core.Classes.Range<int>(2010, 2024)
            };

            void AssertMembers(Classes.YOLOTrainingDatasetOptions? yOLOTrainingDatasetOptions_Actual)
            {
                Assert.NotNull(yOLOTrainingDatasetOptions_Actual);
                Assert.Equal(yOLOTrainingDatasetOptions.Confidence, yOLOTrainingDatasetOptions_Actual!.Confidence);
                Assert.Equal(yOLOTrainingDatasetOptions.CountOnly, yOLOTrainingDatasetOptions_Actual.CountOnly);
                Assert.Equal(yOLOTrainingDatasetOptions.CountyIds!.OrderBy(x => x), yOLOTrainingDatasetOptions_Actual.CountyIds!.OrderBy(x => x));
                Assert.NotNull(yOLOTrainingDatasetOptions_Actual.ExclusionBoundingBoxes);
                Assert.Equal(yOLOTrainingDatasetOptions.ExclusionBoundingBoxes!.Count, yOLOTrainingDatasetOptions_Actual.ExclusionBoundingBoxes!.Count);
                Assert.Equal(yOLOTrainingDatasetOptions.ExclusionBoundingBoxes![0].MinX, yOLOTrainingDatasetOptions_Actual.ExclusionBoundingBoxes![0].MinX);
                Assert.Equal(yOLOTrainingDatasetOptions.ExclusionBoundingBoxes![0].MinY, yOLOTrainingDatasetOptions_Actual.ExclusionBoundingBoxes![0].MinY);
                Assert.Equal(yOLOTrainingDatasetOptions.ExclusionBoundingBoxes![0].MaxX, yOLOTrainingDatasetOptions_Actual.ExclusionBoundingBoxes![0].MaxX);
                Assert.Equal(yOLOTrainingDatasetOptions.ExclusionBoundingBoxes![0].MaxY, yOLOTrainingDatasetOptions_Actual.ExclusionBoundingBoxes![0].MaxY);
                Assert.Equal(yOLOTrainingDatasetOptions.ExclusionBuffer, yOLOTrainingDatasetOptions_Actual.ExclusionBuffer);
                Assert.Equal(yOLOTrainingDatasetOptions.HoldoutDenominator, yOLOTrainingDatasetOptions_Actual.HoldoutDenominator);
                Assert.Equal(yOLOTrainingDatasetOptions.LabelCheckOverlayCount, yOLOTrainingDatasetOptions_Actual.LabelCheckOverlayCount);
                Assert.Equal(yOLOTrainingDatasetOptions.LabelCheckSampleSize, yOLOTrainingDatasetOptions_Actual.LabelCheckSampleSize);
                Assert.Equal(yOLOTrainingDatasetOptions.LegacyCutoff, yOLOTrainingDatasetOptions_Actual.LegacyCutoff);
                Assert.Equal(yOLOTrainingDatasetOptions.LegacyCutoff.Offset, yOLOTrainingDatasetOptions_Actual.LegacyCutoff.Offset);
                Assert.Equal(yOLOTrainingDatasetOptions.LegacyReferencesFilePath, yOLOTrainingDatasetOptions_Actual.LegacyReferencesFilePath);
                Assert.Equal(yOLOTrainingDatasetOptions.MaxConcurrentRequests, yOLOTrainingDatasetOptions_Actual.MaxConcurrentRequests);
                Assert.Equal(yOLOTrainingDatasetOptions.ModelPath, yOLOTrainingDatasetOptions_Actual.ModelPath);
                Assert.Equal(yOLOTrainingDatasetOptions.Offset, yOLOTrainingDatasetOptions_Actual.Offset);
                Assert.Equal(yOLOTrainingDatasetOptions.OutputDirectory, yOLOTrainingDatasetOptions_Actual.OutputDirectory);
                Assert.Equal(yOLOTrainingDatasetOptions.PythonPath, yOLOTrainingDatasetOptions_Actual.PythonPath);
                Assert.Equal(yOLOTrainingDatasetOptions.ReferenceBatchSize, yOLOTrainingDatasetOptions_Actual.ReferenceBatchSize);
                Assert.Equal(yOLOTrainingDatasetOptions.ReferenceDuplicateLimit, yOLOTrainingDatasetOptions_Actual.ReferenceDuplicateLimit);
                Assert.Equal(yOLOTrainingDatasetOptions.ReportsDirectory, yOLOTrainingDatasetOptions_Actual.ReportsDirectory);
                Assert.Equal(yOLOTrainingDatasetOptions.Resume, yOLOTrainingDatasetOptions_Actual.Resume);
                Assert.Equal(yOLOTrainingDatasetOptions.Seed, yOLOTrainingDatasetOptions_Actual.Seed);
                Assert.Equal(yOLOTrainingDatasetOptions.ValidateWeight, yOLOTrainingDatasetOptions_Actual.ValidateWeight);
                Assert.Equal(yOLOTrainingDatasetOptions.WeightsPaths, yOLOTrainingDatasetOptions_Actual.WeightsPaths);
                Assert.Equal(yOLOTrainingDatasetOptions.WorkingDirectory, yOLOTrainingDatasetOptions_Actual.WorkingDirectory);
                Assert.NotNull(yOLOTrainingDatasetOptions_Actual.Years);
                Assert.Equal(yOLOTrainingDatasetOptions.Years!.Min, yOLOTrainingDatasetOptions_Actual.Years!.Min);
                Assert.Equal(yOLOTrainingDatasetOptions.Years.Max, yOLOTrainingDatasetOptions_Actual.Years.Max);
            }

            string? json = Core.Convert.ToSystem_String(yOLOTrainingDatasetOptions);
            Assert.False(string.IsNullOrWhiteSpace(json));

            AssertMembers(Core.Convert.ToDiGi<Classes.YOLOTrainingDatasetOptions>(json)?.FirstOrDefault());

            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions_Copy = new(yOLOTrainingDatasetOptions);
            AssertMembers(yOLOTrainingDatasetOptions_Copy);

            // The copy owns its collections.
            yOLOTrainingDatasetOptions_Copy.WeightsPaths!.Add("x");
            yOLOTrainingDatasetOptions_Copy.CountyIds!.Add(1);
            yOLOTrainingDatasetOptions_Copy.ExclusionBoundingBoxes!.Add(new Classes.YOLOExclusionBoundingBox(0, 0, 1, 1));
            Assert.Equal(2, yOLOTrainingDatasetOptions.WeightsPaths!.Count);
            Assert.Equal(2, yOLOTrainingDatasetOptions.CountyIds!.Count);
            Assert.Single(yOLOTrainingDatasetOptions.ExclusionBoundingBoxes!);

            Core.xUnit.Query.SerializationCheck(yOLOTrainingDatasetOptions);

            // The defaults a caller without a file runs with: nothing is scoped, and the rule values are the regressor&apos;s.
            Classes.YOLOTrainingDatasetOptions yOLOTrainingDatasetOptions_Default = new();
            Assert.Null(yOLOTrainingDatasetOptions_Default.CountyIds);
            Assert.Null(yOLOTrainingDatasetOptions_Default.OutputDirectory);
            Assert.False(yOLOTrainingDatasetOptions_Default.CountOnly);
            Assert.Equal(5, yOLOTrainingDatasetOptions_Default.HoldoutDenominator);
            Assert.Equal(new DateTimeOffset(2025, 5, 23, 0, 0, 0, TimeSpan.Zero), yOLOTrainingDatasetOptions_Default.LegacyCutoff);
            Assert.Equal(1, yOLOTrainingDatasetOptions_Default.Offset);
            Assert.Equal(0.1, yOLOTrainingDatasetOptions_Default.Confidence);

            // The exclusion defaults: an empty list keeps today's behaviour, and the buffer is the neighbourhood.
            Assert.NotNull(yOLOTrainingDatasetOptions_Default.ExclusionBoundingBoxes);
            Assert.Empty(yOLOTrainingDatasetOptions_Default.ExclusionBoundingBoxes!);
            Assert.Equal(100, yOLOTrainingDatasetOptions_Default.ExclusionBuffer);
        }

        /// <summary>
        /// Verifies that the committed YOLOTrainingDatasetOptions template names exactly the members the options class declares, reads back, names no county, and counts before it builds.
        /// <para>A key the class does not declare is dropped in silence, so a misspelt template member reads as the default. The template counts by default because a first run should report what a build would cost before it spends it.</para>
        /// </summary>
        [Fact]
        public void YOLOTrainingDatasetOptions_Template()
        {
            string? directory_Files = Core.xUnit.Query.FilesDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Files));

            DirectoryInfo? directoryInfo_Workspace = Directory.GetParent(directory_Files!)?.Parent;
            Assert.NotNull(directoryInfo_Workspace);

            string path_Template = Path.Combine(directoryInfo_Workspace!.FullName, "DiGi.GIS.YOLO.UI", "files", Constants.FileName.YOLOTrainingDatasetOptions);
            Assert.True(File.Exists(path_Template), $"The committed options template was not found at '{path_Template}'.");

            List<string> names_Member = [];
            foreach (PropertyInfo propertyInfo in typeof(Classes.YOLOTrainingDatasetOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (propertyInfo.CanRead && propertyInfo.CanWrite)
                {
                    names_Member.Add(propertyInfo.Name);
                }
            }

            JsonObject? jsonObject = JsonNode.Parse(File.ReadAllText(path_Template)) as JsonObject;
            Assert.NotNull(jsonObject);

            List<string> names_Template = [.. jsonObject!.Select(x => x.Key)];

            foreach (string name in names_Template)
            {
                Assert.True(names_Member.Contains(name), $"The template names '{name}', which is not a member of {nameof(Classes.YOLOTrainingDatasetOptions)}.");
            }

            foreach (string name in names_Member)
            {
                Assert.True(names_Template.Contains(name), $"{nameof(Classes.YOLOTrainingDatasetOptions)} declares '{name}', which the template does not name.");
            }

            Classes.YOLOTrainingDatasetOptions? yOLOTrainingDatasetOptions = Query.YOLOTrainingDatasetOptions(path_Template);
            Assert.NotNull(yOLOTrainingDatasetOptions);
            Assert.True(yOLOTrainingDatasetOptions!.CountyIds is null || yOLOTrainingDatasetOptions.CountyIds.Count == 0);
            Assert.True(yOLOTrainingDatasetOptions.CountOnly);
            Assert.Equal(new DateTimeOffset(2025, 5, 23, 0, 0, 0, TimeSpan.Zero), yOLOTrainingDatasetOptions.LegacyCutoff);
            Assert.Equal(5, yOLOTrainingDatasetOptions.HoldoutDenominator);
            Assert.NotNull(yOLOTrainingDatasetOptions.ExclusionBoundingBoxes);
            Assert.Empty(yOLOTrainingDatasetOptions.ExclusionBoundingBoxes!);
            Assert.Equal(100, yOLOTrainingDatasetOptions.ExclusionBuffer);
        }
    }
}
