using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.GIS.YOLO.UI.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a score-only run over a chunked county folder writes what it writes over the same county as one flat folder (DiGi-Organization/DiGi.GIS.YOLO.UI#30).
        /// <para>The chunked folder is what a finished chunked run leaves with its imagery removed: two markers, the joined <c>results.bbrf</c>, and no image folder. The markers are then the only record of which buildings the detector was handed, so the detection write must still cover all three fixture buildings and the predicted year clear must still cover only <c>0208</c>, the one it never fired on - exactly as the flat folder of <see cref="RunYearBuiltPredictions_ClearPredictedYearBuilt_NeverDetected"/> does. The image count comes from the markers.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_Chunks_ScoreOnly()
        {
            int countyId = 73485;

            async Task<(YearBuiltPredictionResult?, List<Table>)> RunAsync(string directory_Scratch)
            {
                List<Table> tables = [];
                GISWebAPIManager gISWebAPIManager = new(CreateStubFactory(tables));

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

                return (yearBuiltPredictionResult, tables);
            }

            List<string> References(Table table)
            {
                Assert.True(table.TryGetColumn("Reference", out Column? column_Reference));

                List<string> result = [];
                for (int i = 0; i < table.RowCount; i++)
                {
                    if (table.GetRow(i) is Row row && row.TryGetValue(column_Reference!.Index, out string? reference) && reference is not null)
                    {
                        result.Add(reference);
                    }
                }

                result.Sort(StringComparer.Ordinal);
                return result;
            }

            (Table?, Table?) Split(List<Table> tables)
            {
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

                return (table_Detection, table_Clear);
            }

            //The flat folder, as written before chunks
            string directory_Flat = SetupScratchDirectory(nameof(RunYearBuiltPredictions_Chunks_ScoreOnly), countyId, "Flat");
            (YearBuiltPredictionResult? yearBuiltPredictionResult_Flat, List<Table> tables_Flat) = await RunAsync(directory_Flat);

            //The same county as a finished chunked run leaves it
            string directory_Chunked = SetupScratchDirectory(nameof(RunYearBuiltPredictions_Chunks_ScoreOnly), countyId, "Chunked");
            string directory_County = Path.Combine(directory_Chunked, countyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.Delete(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages), true);

            string directory_Chunks = Path.Combine(directory_County, Constants.DirectoryName.PredictionChunks);
            string modelSHA256 = new('a', 64);
            Assert.True(new Classes.YearBuiltPredictionChunk(0, ["0207", "0208"], ["0207", "0208"], 2, modelSHA256, 0.1, DateTimeOffset.Now).WriteYearBuiltPredictionChunk(Path.Combine(directory_Chunks, "000000" + Constants.FileName.PredictionChunkExtension)));
            Assert.True(new Classes.YearBuiltPredictionChunk(1, ["0209"], ["0209"], 1, modelSHA256, 0.1, DateTimeOffset.Now).WriteYearBuiltPredictionChunk(Path.Combine(directory_Chunks, "000001" + Constants.FileName.PredictionChunkExtension)));

            //Not a marker: a file whose name is not a chunk index is ignored
            File.WriteAllText(Path.Combine(directory_Chunks, "notes" + Constants.FileName.PredictionChunkExtension), "{}");

            (YearBuiltPredictionResult? yearBuiltPredictionResult_Chunked, List<Table> tables_Chunked) = await RunAsync(directory_Chunked);

            Assert.NotNull(yearBuiltPredictionResult_Flat);
            Assert.NotNull(yearBuiltPredictionResult_Chunked);
            Assert.Empty(yearBuiltPredictionResult_Chunked!.FailedStepNames);
            Assert.Equal(3, yearBuiltPredictionResult_Chunked.ImageCount);
            Assert.Equal(yearBuiltPredictionResult_Flat!.ImageCount, yearBuiltPredictionResult_Chunked.ImageCount);

            Assert.Equal(2, tables_Chunked.Count);
            Assert.Equal(tables_Flat.Count, tables_Chunked.Count);

            (Table? table_Detection_Flat, Table? table_Clear_Flat) = Split(tables_Flat);
            (Table? table_Detection_Chunked, Table? table_Clear_Chunked) = Split(tables_Chunked);

            Assert.NotNull(table_Detection_Chunked);
            Assert.NotNull(table_Clear_Chunked);
            Assert.Equal(["0207", "0208", "0209"], References(table_Detection_Chunked!));
            Assert.Equal(["0208"], References(table_Clear_Chunked!));

            Assert.Equal(References(table_Detection_Flat!), References(table_Detection_Chunked!));
            Assert.Equal(References(table_Clear_Flat!), References(table_Clear_Chunked!));
        }

        /// <summary>
        /// Verifies that a score-only run over a chunked county folder without a joined results file is refused rather than scored (DiGi-Organization/DiGi.GIS.YOLO.UI#30).
        /// <para>A chunked run joins the results only once every chunk is scored, and removes the previous join before it starts, so a missing results file beside markers is a run that stopped part way. Its markers name only the chunks it got through, and scoring them would write the county as if it held those buildings only.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_Chunks_Unfinished()
        {
            int countyId = 73485;

            string directory_Scratch = SetupScratchDirectory(nameof(RunYearBuiltPredictions_Chunks_Unfinished), countyId, "Unfinished");
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Directory.Delete(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages), true);
            File.Delete(Path.Combine(directory_County, Constants.FileName.PredictionResults));

            string directory_Chunks = Path.Combine(directory_County, Constants.DirectoryName.PredictionChunks);
            Assert.True(new Classes.YearBuiltPredictionChunk(0, ["0207", "0208"], ["0207", "0208"], 2, new string('a', 64), 0.1, DateTimeOffset.Now).WriteYearBuiltPredictionChunk(Path.Combine(directory_Chunks, "000000" + Constants.FileName.PredictionChunkExtension)));

            List<Table> tables = [];
            GISWebAPIManager gISWebAPIManager = new(CreateStubFactory(tables));

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                Years = new Core.Classes.Range<int>(2020, 2021),
                CleanScratchDirectory = true,
                ExportImages = false,
                RunPrediction = false,
                Score = false,
                UpdateDetections = true,
                UpdatePredictedYearBuilt = true,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Contains(nameof(DiGi.YOLO.Create.BoundingBoxResultFile), yearBuiltPredictionResult!.FailedStepNames);
            Assert.Empty(tables);

            //A failed county keeps its scratch, so a re-run can finish it
            Assert.True(Directory.Exists(directory_Chunks));
        }

        /// <summary>
        /// Verifies that a county is exported into bounded chunk folders and never into one flat folder, and that a re-run with another chunk size re-splits the folders rather than scoring a building in two of them (DiGi-Organization/DiGi.GIS.YOLO.UI#30).
        /// <para>Five buildings at a chunk size of two make three folders holding two, two and one building, with nothing written to the flat <c>images</c> folder. A resumed re-run at a chunk size of three keeps the imagery of the buildings still in each chunk, exports the ones that moved in, and removes the ones that moved out. The detector is off, so the fact needs no interpreter; the run then has no results to read and fails that step, which is the degenerate export-only case and is asserted as such.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_Chunks_Export()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoDatas_BoundingBox2D_OrtoDatas.json");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            GIS.Classes.OrtoDatas? ortoDatas = Core.Convert.ToDiGi<GIS.Classes.OrtoDatas>((Core.Classes.Path)path_Fixture!)?.FirstOrDefault();
            Assert.NotNull(ortoDatas);
            Assert.False(string.IsNullOrWhiteSpace(ortoDatas!.Reference));

            string reference_Fixture = ortoDatas.Reference!;
            string json_Item = Core.Convert.ToSystem_String((Core.Interfaces.ISerializableObject)ortoDatas) ?? string.Empty;

            List<string> references = [.. new string[] { "E", "C", "A", "D", "B" }.Select(x => reference_Fixture + x)];
            string json_References = Core.Convert.ToSystem_String(references.ConvertAll(x => new PostgreSQL.Classes.OrtoDatasReference { Reference = x, CountyId = countyId })) ?? string.Empty;

            int itemRequestCount = 0;
            StubHttpClientFactory stubHttpClientFactory = new((request) =>
            {
                string requestUrl = request.RequestUri?.ToString() ?? string.Empty;
                if (requestUrl.Contains("itembyreference", StringComparison.OrdinalIgnoreCase))
                {
                    System.Threading.Interlocked.Increment(ref itemRequestCount);

                    string? reference = references.Find(x => requestUrl.Contains("reference=" + x, StringComparison.Ordinal));
                    if (reference is null)
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.NoContent);
                    }

                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json_Item.Replace("\"" + reference_Fixture + "\"", "\"" + reference + "\""), System.Text.Encoding.UTF8, "application/json") };
                }

                if (requestUrl.Contains("ortodatas", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json_References, System.Text.Encoding.UTF8, "application/json") };
                }

                //The county rows: unreadable, so the scope check is skipped with a warning
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
            });

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_Chunks_Export));
            if (Directory.Exists(directory_Scratch))
            {
                Directory.Delete(directory_Scratch, true);
            }

            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string directory_Chunks = Path.Combine(directory_County, Constants.DirectoryName.PredictionChunks);

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            async Task<YearBuiltPredictionResult?> RunAsync(int chunkSize)
            {
                YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
                {
                    CountyIds = [countyId],
                    ScratchDirectory = directory_Scratch,
                    ChunkSize = chunkSize,
                    CleanScratchDirectory = false,
                    ExportImages = true,
                    MaxConcurrentRequests = 2,
                    Resume = true,
                    RunPrediction = false,
                    Score = false,
                    UpdateDetections = false,
                    UpdatePredictedYearBuilt = false,
                    UpdateYearBuiltData = false
                };

                return await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);
            }

            List<string> ChunkReferences(int index)
            {
                string directory_Chunk = Path.Combine(directory_Chunks, index.ToString(Constants.FileName.PredictionChunkFormat, System.Globalization.CultureInfo.InvariantCulture));

                List<string> result = [.. Query.PredictionImageReferences(directory_Chunk)];
                result.Sort(StringComparer.Ordinal);
                return result;
            }

            YearBuiltPredictionResult? yearBuiltPredictionResult = await RunAsync(2);
            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Contains(nameof(DiGi.YOLO.Create.BoundingBoxResultFile), yearBuiltPredictionResult!.FailedStepNames);
            Assert.DoesNotContain(nameof(Modify.ExportPredictionImagesAsync), yearBuiltPredictionResult.FailedStepNames);

            Assert.False(Directory.Exists(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages)), "No image may be written to the flat folder.");
            Assert.Equal(5, itemRequestCount);

            Assert.Equal([reference_Fixture + "A", reference_Fixture + "B"], ChunkReferences(0));
            Assert.Equal([reference_Fixture + "C", reference_Fixture + "D"], ChunkReferences(1));
            Assert.Equal([reference_Fixture + "E"], ChunkReferences(2));

            int imageCount_Building = Directory.GetFiles(Path.Combine(directory_Chunks, "000002"), "*.jpeg").Length;
            Assert.True(imageCount_Building > 0);
            Assert.Equal(2 * imageCount_Building, Directory.GetFiles(Path.Combine(directory_Chunks, "000000"), "*.jpeg").Length);

            //Re-split at three: C moves into chunk 0 and E into chunk 1; only those two are fetched again
            yearBuiltPredictionResult = await RunAsync(3);
            Assert.NotNull(yearBuiltPredictionResult);

            Assert.Equal([reference_Fixture + "A", reference_Fixture + "B", reference_Fixture + "C"], ChunkReferences(0));
            Assert.Equal([reference_Fixture + "D", reference_Fixture + "E"], ChunkReferences(1));
            Assert.Equal(7, itemRequestCount);
        }

        /// <summary>
        /// Verifies that a chunk size below one refuses the run before any request is sent (DiGi-Organization/DiGi.GIS.YOLO.UI#30).
        /// <para>Clamping it to one, the way <see cref="Classes.YearBuiltPredictionPipelineOptions.BatchSize"/> is clamped, would start the detector once per building.</para>
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task RunYearBuiltPredictions_ChunkSize_Refused()
        {
            foreach (int chunkSize in new int[] { 0, -1 })
            {
                int requestCount = 0;
                StubHttpClientFactory stubHttpClientFactory = new((request) =>
                {
                    System.Threading.Interlocked.Increment(ref requestCount);
                    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                });

                GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

                YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
                {
                    CountyIds = [73485],
                    ScratchDirectory = Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, nameof(RunYearBuiltPredictions_ChunkSize_Refused)),
                    ChunkSize = chunkSize,
                    RunPrediction = false,
                    Score = false
                };

                YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

                Assert.NotNull(yearBuiltPredictionResult);
                Assert.Contains(nameof(Classes.YearBuiltPredictionPipelineOptions.ChunkSize), yearBuiltPredictionResult!.FailedStepNames);
                Assert.Equal(0, requestCount);
            }
        }
    }
}
