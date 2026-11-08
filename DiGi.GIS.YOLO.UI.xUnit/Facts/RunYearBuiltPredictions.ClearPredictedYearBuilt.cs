using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.GIS.YOLO.UI.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a never-detected building's stored predicted year is cleared when the detection and the predicted year writes are both on (DiGi-Organization/DiGi.GIS.YOLO.UI#28).
        /// <para>The fixture's folder holds three buildings: <c>0207</c> and <c>0209</c> were detected, <c>0208</c> was not. With no manifest, all three are scored. The detection write covers all three (clearing <c>0208</c>'s columns), and the new predicted year clear covers only <c>0208</c> - the building the detector never fired on - leaving its predicted year NULL while the two detected buildings keep theirs.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_ClearPredictedYearBuilt_NeverDetected()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_ClearPredictedYearBuilt_NeverDetected));
            if (Directory.Exists(directory_Scratch))
            {
                Directory.Delete(directory_Scratch, true);
            }

            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string directory_Images = Path.Combine(directory_County, Constants.DirectoryName.PredictionImages);
            Directory.CreateDirectory(directory_Images);
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            List<string> fileNames = ["0207_2021.jpeg", "0208_2021.jpeg", "0209_2021.jpeg"];
            foreach (string fileName in fileNames)
            {
                File.WriteAllBytes(Path.Combine(directory_Images, fileName), []);
            }

            List<Table> tables = [];
            StubHttpClientFactory stubHttpClientFactory = CreateStubFactory(tables);

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                Years = new Core.Classes.Range<int>(2020, 2021),
                CleanScratchDirectory = false,
                ExportImages = false,
                RunPrediction = false,
                Score = false,
                UpdateDetections = true,
                UpdatePredictedYearBuilt = true,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.DoesNotContain(nameof(Modify.UpdateBuildingDataYearBuiltPredictionsAsync), yearBuiltPredictionResult!.FailedStepNames);

            //The detection write and the predicted year clear are the two writes; the detected buildings are dated nowhere because scoring is off.
            Assert.Equal(2, tables.Count);

            Table? table_Detection = null;
            Table? table_Clear = null;
            foreach (Table table in tables)
            {
                if (table.TryGetColumn("Predicted year built", out Column? column) && column is not null)
                {
                    table_Clear = table;
                }
                else
                {
                    table_Detection = table;
                }
            }

            Assert.NotNull(table_Detection);
            Assert.NotNull(table_Clear);

            //The detection write covers every scored building
            Assert.Equal(3, table_Detection!.RowCount);

            //The clearing write covers only the building the detector never fired on
            Assert.Equal(1, table_Clear!.RowCount);
            Assert.True(table_Clear.TryGetColumn("Predicted year built", out Column? column_PredictedYearBuilt));
            Assert.True(table_Clear.TryGetColumn("Reference", out Column? column_Reference));

            Row? row = table_Clear.GetRow(0);
            Assert.NotNull(row);
            Assert.True(row!.TryGetValue(column_Reference!.Index, out string? reference));
            Assert.Equal("0208", reference);
            Assert.False(row.TryGetValue(column_PredictedYearBuilt!.Index, out object? _), "The never-detected building's predicted year must be cleared, not carry a value.");

            //The cleared count is reported in the run summary
            Assert.Contains(yearBuiltPredictionResult.Messages, message => message.Contains("Cleared the predicted year built of 1 never-detected building(s)"));
        }

        /// <summary>
        /// Verifies that the predicted year clear is gated on both write flags: with the predicted year write off, or with the detection write off, a never-detected building's stored predicted year is left untouched (DiGi-Organization/DiGi.GIS.YOLO.UI#28).
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_ClearPredictedYearBuilt_FlagsOff()
        {
            (bool updateDetections, bool updatePredictedYearBuilt)[] cases = [(true, false), (false, true)];

            foreach ((bool updateDetections, bool updatePredictedYearBuilt) in cases)
            {
                string directory_Scratch = SetupScratchDirectory(nameof(RunYearBuiltPredictions_ClearPredictedYearBuilt_FlagsOff), 73485, updateDetections.ToString() + updatePredictedYearBuilt.ToString());

                List<Table> tables = [];
                StubHttpClientFactory stubHttpClientFactory = CreateStubFactory(tables);

                GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

                YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
                {
                    CountyIds = [73485],
                    ScratchDirectory = directory_Scratch,
                    Years = new Core.Classes.Range<int>(2020, 2021),
                    CleanScratchDirectory = false,
                    ExportImages = false,
                    RunPrediction = false,
                    Score = false,
                    UpdateDetections = updateDetections,
                    UpdatePredictedYearBuilt = updatePredictedYearBuilt,
                    UpdateYearBuiltData = false
                };

                YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);
                Assert.NotNull(yearBuiltPredictionResult);

                //No table carrying the predicted year column is posted, so the never-detected building's stored year is untouched
                Assert.DoesNotContain(tables, table => table.TryGetColumn("Predicted year built", out Column? column) && column is not null);
            }
        }

        /// <summary>
        /// Verifies that a manifest-narrowed run clears only its own buildings: a never-detected building outside the manifest is left untouched (DiGi-Organization/DiGi.GIS.YOLO.UI#28).
        /// <para>The fixture's folder holds <c>0207</c>, <c>0208</c> and <c>0209</c>; <c>0208</c> is the one the detector never fired on. The manifest names <c>0207</c> and <c>0209</c>, so <c>0208</c> is outside the run and its stored predicted year is not cleared.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_ClearPredictedYearBuilt_ManifestNarrowed()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_ClearPredictedYearBuilt_ManifestNarrowed));
            if (Directory.Exists(directory_Scratch))
            {
                Directory.Delete(directory_Scratch, true);
            }

            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string directory_Images = Path.Combine(directory_County, Constants.DirectoryName.PredictionImages);
            Directory.CreateDirectory(directory_Images);
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            List<string> fileNames = ["0207_2021.jpeg", "0208_2021.jpeg", "0209_2021.jpeg"];
            foreach (string fileName in fileNames)
            {
                File.WriteAllBytes(Path.Combine(directory_Images, fileName), []);
            }

            string path_Manifest = Path.Combine(directory_Scratch, Constants.FileName.DatasetReferences);
            File.WriteAllText(path_Manifest, string.Join("\n", Constants.Header.DatasetReferences, "0207\t73485\tTrain\t2019\ttrue\ttsv", "0209\t73485\tTest\t2015\ttrue\ttsv") + "\n");

            List<Table> tables = [];
            StubHttpClientFactory stubHttpClientFactory = CreateStubFactory(tables);

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                ReferencesFilePath = path_Manifest,
                Years = new Core.Classes.Range<int>(2020, 2021),
                CleanScratchDirectory = false,
                ExportImages = false,
                RunPrediction = false,
                Score = false,
                UpdateDetections = true,
                UpdatePredictedYearBuilt = true,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.DoesNotContain(nameof(Classes.YearBuiltPredictionPipelineOptions.ReferencesFilePath), yearBuiltPredictionResult!.FailedStepNames);

            //0208 is outside the manifest, so it is neither scored nor cleared; the only write is the detection write of 0207 and 0209
            Assert.Single(tables);
            Assert.False(tables[0].TryGetColumn("Predicted year built", out Column? _), "A never-detected building outside the manifest must not be cleared.");
        }

        /// <summary>
        /// Lays down the three-image fixture in a fresh scratch county folder and returns the scratch directory.
        /// </summary>
        static string SetupScratchDirectory(string testName, int countyId, string suffix)
        {
            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());

            string directory_Scratch = Path.Combine(directory_Reports!, testName + "_" + suffix);
            if (Directory.Exists(directory_Scratch))
            {
                Directory.Delete(directory_Scratch, true);
            }

            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string directory_Images = Path.Combine(directory_County, Constants.DirectoryName.PredictionImages);
            Directory.CreateDirectory(directory_Images);
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            List<string> fileNames = ["0207_2021.jpeg", "0208_2021.jpeg", "0209_2021.jpeg"];
            foreach (string fileName in fileNames)
            {
                File.WriteAllBytes(Path.Combine(directory_Images, fileName), []);
            }

            return directory_Scratch;
        }

        /// <summary>
        /// Builds a stub HTTP client factory that captures every posted table and answers the county rows read with a not found.
        /// </summary>
        static StubHttpClientFactory CreateStubFactory(List<Table> tables)
        {
            return new StubHttpClientFactory((request) =>
            {
                if (request.Method != HttpMethod.Post || request.Content is null)
                {
                    //The county rows: unreadable, so the scope check is skipped with a warning
                    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                }

                byte[] bytes = request.Content.ReadAsByteArrayAsync().Result;
                if (request.Content.Headers.ContentEncoding.Contains("gzip"))
                {
                    using MemoryStream memoryStream_Input = new(bytes);
                    using GZipStream gZipStream = new(memoryStream_Input, CompressionMode.Decompress);
                    using MemoryStream memoryStream_Output = new();
                    gZipStream.CopyTo(memoryStream_Output);
                    bytes = memoryStream_Output.ToArray();
                }

                Table? table = GIS.WebAPI.Create.Table(JsonNode.Parse(Encoding.UTF8.GetString(bytes)) as JsonObject);
                if (table is not null)
                {
                    lock (tables)
                    {
                        tables.Add(table);
                    }
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(string.Empty) };
            });
        }
    }
}
