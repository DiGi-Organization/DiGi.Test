using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.Classes;
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
        /// Verifies that a reference manifest that does not read refuses the run before a single request is sent (DiGi-Organization/DiGi.GIS.YOLO.UI#22).
        /// <para>A filter that quietly read as no filter would export, detect and write whole counties where a few buildings were asked for, so a missing file, a file without the manifest header and a manifest with a header and no building are all refused and named by <see cref="Classes.YearBuiltPredictionPipelineOptions.ReferencesFilePath"/>.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_ReferencesFilePath_Refused()
        {
            int requestCount = 0;
            StubHttpClientFactory stubHttpClientFactory = new((request) =>
            {
                Interlocked.Increment(ref requestCount);
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            string directory = Path.Combine(Path.GetTempPath(), $"{nameof(RunYearBuiltPredictions_ReferencesFilePath_Refused)}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                string path_NoHeader = Path.Combine(directory, "no_header.tsv");
                File.WriteAllText(path_NoHeader, "0207\t73485\tTrain\t2019\ttrue\ttsv\n");

                string path_Empty = Path.Combine(directory, "empty.tsv");
                File.WriteAllText(path_Empty, Constants.Header.DatasetReferences + "\n");

                List<string> paths = [Path.Combine(directory, "missing.tsv"), path_NoHeader, path_Empty];
                foreach (string path in paths)
                {
                    YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
                    {
                        CountyIds = [73485],
                        ScratchDirectory = directory,
                        ReferencesFilePath = path,
                        ExportImages = true,
                        RunPrediction = false,
                        Score = false,
                        UpdateDetections = true
                    };

                    YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

                    Assert.NotNull(yearBuiltPredictionResult);
                    Assert.Contains(nameof(Classes.YearBuiltPredictionPipelineOptions.ReferencesFilePath), yearBuiltPredictionResult!.FailedStepNames);
                    Assert.Equal(0, yearBuiltPredictionResult.BuildingDataUpdatedCount);
                }

                Assert.Equal(0, requestCount);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Verifies the detection write of a narrowed run end to end through a stubbed endpoint: only the manifest's buildings are written, and a scored building the detector did not fire on is sent with every detection column of the range unset, so earlier weights' values on it are cleared (DiGi-Organization/DiGi.GIS.YOLO.UI#21, #22).
        /// <para>The fixture's folder holds three buildings: <c>0207</c> and <c>0209</c> were detected, <c>0208</c> was not. The manifest names <c>0207</c> and <c>0208</c>, so <c>0209</c> - imagery a wider run left behind - is scored by the detector but not written.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_ReferencesFilePath_Write()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_ReferencesFilePath_Write));
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
            File.WriteAllText(path_Manifest, string.Join("\n", Constants.Header.DatasetReferences, "0207\t73485\tTrain\t2019\ttrue\ttsv", "0208\t73485\tTest\t2015\ttrue\ttsv") + "\n");

            List<Table> tables = [];
            StubHttpClientFactory stubHttpClientFactory = new((request) =>
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
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.DoesNotContain(nameof(Classes.YearBuiltPredictionPipelineOptions.ReferencesFilePath), yearBuiltPredictionResult!.FailedStepNames);
            Assert.DoesNotContain(nameof(Modify.UpdateBuildingDataYearBuiltPredictionsAsync), yearBuiltPredictionResult.FailedStepNames);

            //0207 detected, 0208 cleared; 0209 is outside the manifest
            Assert.Equal(1, yearBuiltPredictionResult.BuildingCount);
            Assert.Equal(2, yearBuiltPredictionResult.BuildingDataUpdatedCount);

            Table table = Assert.Single(tables);
            Assert.Equal(2, table.RowCount);
            Assert.Equal(2 + (2 * 5), table.Columns.Count());

            Assert.True(table.TryGetColumn(IO.Constants.Column.Reference.Name, out Column? column_Reference));
            Assert.True(table.TryGetColumn("Prediction Confidence 2021", out Column? column_Confidence2021));

            Dictionary<string, Row> rows_ByReference = [];
            for (int i = 0; i < table.RowCount; i++)
            {
                Row? row = table.GetRow(i);
                if (row is not null && row.TryGetValue(column_Reference!.Index, out string? reference) && reference is not null)
                {
                    rows_ByReference[reference] = row;
                }
            }

            Assert.Equal(["0207", "0208"], rows_ByReference.Keys.OrderBy(x => x));
            Assert.True(rows_ByReference["0207"].TryGetValue(column_Confidence2021!.Index, out double confidence));
            Assert.True(confidence > 0.9);

            foreach (Column column in table.Columns)
            {
                if (column.Name is string name && name.StartsWith("Prediction "))
                {
                    Assert.False(rows_ByReference["0208"].TryGetValue(column.Index, out object? _), $"{name} carries a value on a building the detector did not fire on.");
                }
            }
        }

        /// <summary>
        /// Verifies that ExportPredictionImagesAsync narrowed to references fetches only the buildings that are both in the county's listing and named - a named reference the county does not hold is not requested.
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task ExportPredictionImages_References()
        {
            List<OrtoDatasReference> ortoDatasReferences =
            [
                new() { Reference = "R_A", CountyId = 1 },
                new() { Reference = "R_B", CountyId = 1 }
            ];

            string json_References = Core.Convert.ToSystem_String(ortoDatasReferences) ?? string.Empty;

            List<string> requestUris_Item = [];
            StubHttpClientFactory stubHttpClientFactory = new((request) =>
            {
                string requestUri = request.RequestUri?.ToString() ?? string.Empty;
                if (requestUri.Contains("itembyreference", StringComparison.OrdinalIgnoreCase))
                {
                    lock (requestUris_Item)
                    {
                        requestUris_Item.Add(requestUri);
                    }

                    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json_References, Encoding.UTF8, "application/json")
                };
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            string directory = Path.Combine(Path.GetTempPath(), $"{nameof(ExportPredictionImages_References)}_{Guid.NewGuid():N}");
            try
            {
                bool exported = await gISWebAPIManager.ExportPredictionImagesAsync(1, directory, resume: false, references: ["R_B", "R_X"]);
                Assert.True(exported);

                string requestUri_Item = Assert.Single(requestUris_Item);
                Assert.Contains("reference=R_B", requestUri_Item);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
