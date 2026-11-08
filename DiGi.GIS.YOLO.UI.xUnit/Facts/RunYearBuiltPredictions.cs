using DiGi.GIS.IO.Interfaces;
using DiGi.GIS.YOLO.UI.Classes;
using DiGi.GIS.WebAPI.Classes;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that Modify.RunYearBuiltPredictionsAsync refuses a run it cannot scope, rather than starting one and discovering it halfway through.
        /// <para>The county scope has no run-everything default on purpose: the pipeline writes deployed data.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_Validation()
        {
            GISWebAPIManager gISWebAPIManager = new(null);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [73485],
                ScratchDirectory = Path.GetTempPath(),
                ExportImages = false,
                RunPrediction = false,
                Score = false,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? result_NullManager = await Modify.RunYearBuiltPredictionsAsync(null, null, yearBuiltPredictionPipelineOptions);
            Assert.Null(result_NullManager);

            YearBuiltPredictionResult? result_NullOptions = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, null);
            Assert.Null(result_NullOptions);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_NoCounty = new(yearBuiltPredictionPipelineOptions) { CountyIds = [] };
            YearBuiltPredictionResult? result_NoCounty = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions_NoCounty);
            Assert.Null(result_NoCounty);

            //A county identifier is a database row identifier, so a non-positive one names nothing
            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_BadCounty = new(yearBuiltPredictionPipelineOptions) { CountyIds = [0, -1] };
            YearBuiltPredictionResult? result_BadCounty = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions_BadCounty);
            Assert.Null(result_BadCounty);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_NoScratch = new(yearBuiltPredictionPipelineOptions) { ScratchDirectory = null };
            YearBuiltPredictionResult? result_NoScratch = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions_NoScratch);
            Assert.Null(result_NoScratch);
        }

        /// <summary>
        /// Verifies that the orchestrator turns the detections a previous run left on disk into objects and counts them, without contacting Python, ML.NET or a database.
        /// <para>Every step but the translation is turned off, so what this pins is the sequence itself: the detections are read from the deterministic scratch path the run derives from the county identifier, which is what makes a resumed run find what it left behind.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_Detections()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));
            Assert.True(File.Exists(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_Detections));
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(CultureInfo.CurrentCulture));
            Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            GISWebAPIManager gISWebAPIManager = new(null);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                ExportImages = false,
                RunPrediction = false,
                Score = false,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal([countyId], yearBuiltPredictionResult!.CountyIds);
            Assert.False(yearBuiltPredictionResult.Cancelled);
            Assert.NotNull(yearBuiltPredictionResult.RunTimestamp);

            //The fixture holds three images, two of which carry a detection and one of which carries none
            Assert.Equal(2, yearBuiltPredictionResult.BuildingCount);
            Assert.Equal(2, yearBuiltPredictionResult.DetectionCount);

            //Nothing was written, so nothing is reported written
            Assert.Equal(0, yearBuiltPredictionResult.BuildingDataUpdatedCount);
            Assert.Equal(0, yearBuiltPredictionResult.YearBuiltDataUpdatedCount);
            Assert.Equal(0, yearBuiltPredictionResult.PredictionCount);

            Core.xUnit.Query.SerializationCheck(yearBuiltPredictionResult);
        }

        /// <summary>
        /// Verifies that the scoring step is refused when no predictor was supplied, and that the refusal is reported rather than thrown.
        /// <para>The seam is optional by design - a detection-only pass needs no regressor - so asking for the scoring step without one is a stated failure of that step and not of the run.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_MissingPredictor()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_MissingPredictor));
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(CultureInfo.CurrentCulture));
            Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            GISWebAPIManager gISWebAPIManager = new(null);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                ExportImages = false,
                RunPrediction = false,
                Score = true,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(2, yearBuiltPredictionResult!.BuildingCount);
            Assert.Contains(nameof(IYearBuiltPredictor), yearBuiltPredictionResult.FailedStepNames);
            Assert.Equal(0, yearBuiltPredictionResult.PredictionCount);
        }

        /// <summary>
        /// Verifies that a predictor is accepted and the run then reaches the feature read, which is the first step that cannot be satisfied without a server.
        /// <para>What this pins is that the predictor guard passes and the failure moves on to the read, so a stub predictor is enough to exercise the sequence up to the point a database is genuinely needed.</para>
        /// <para>The model identity the predictor's readiness states is reported in the result before any county is read - the value every prediction of the run is stamped with (DiGi-Organization/DiGi.GIS.YOLO.UI#26).</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_StubPredictor()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_StubPredictor));
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(CultureInfo.CurrentCulture));
            Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            GISWebAPIManager gISWebAPIManager = new(null);
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965, modelId: "2e120f495e830f0917cace33ad21c5c4c7bb0c4e8f1711a363682932b7c0b7fa");

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                ExportImages = false,
                RunPrediction = false,
                Score = true,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(yearBuiltPredictorStub, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(2, yearBuiltPredictionResult!.BuildingCount);

            //The predictor was accepted, so the run got past it and stopped at the read instead
            Assert.DoesNotContain(nameof(IYearBuiltPredictor), yearBuiltPredictionResult.FailedStepNames);
            Assert.Contains(nameof(Query.BuildingDataTableAsync), yearBuiltPredictionResult.FailedStepNames);
            Assert.Equal(0, yearBuiltPredictorStub.CallCount);
            Assert.Equal(0, yearBuiltPredictionResult.FeatureRowCount);

            Assert.Contains("Year built predictor: 2e120f495e830f0917cace33ad21c5c4c7bb0c4e8f1711a363682932b7c0b7fa", yearBuiltPredictionResult.Messages);
        }

        /// <summary>
        /// Verifies that a predictor reporting itself unable to score is refused before any county work starts, and that the refusal carries the reason.
        /// <para>The readiness preflight sits beside the Python one: a runner without its model otherwise exports a county of imagery, fails on the first scoring batch, and the cached engine failure repeats for every county behind it. What this pins is that the refusal comes first, with the diagnostic, and that the predictor is never asked to score.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_UnrunnablePredictor()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_UnrunnablePredictor));
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(CultureInfo.CurrentCulture));
            Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            GISWebAPIManager gISWebAPIManager = new(null);
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965, runnable: false);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                ExportImages = false,
                RunPrediction = false,
                Score = true,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(yearBuiltPredictorStub, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);

            //The refusal is a preflight: nothing of the county was carried through, and the predictor was never asked to score
            Assert.Equal(0, yearBuiltPredictorStub.CallCount);
            Assert.Equal(0, yearBuiltPredictionResult!.BuildingCount);
            Assert.Equal(0, yearBuiltPredictionResult.FeatureRowCount);

            //The refusal is named after the readiness surface, and the reason it refused travels with the result
            Assert.Contains(nameof(IO.Classes.YearBuiltPredictorReadiness), yearBuiltPredictionResult.FailedStepNames);
            Assert.Contains("model", string.Join(" ", yearBuiltPredictionResult.Messages));
        }
        /// <summary>
        /// Verifies that a county whose detection results are not on disk is reported as a failed step rather than logged as a county the detector found nothing in.
        /// <para>With the detector off, the results file is the only record of which buildings it fired on - the stored detection columns are never read back for this - so a scratch directory cleaned between a detections run and a scoring run leaves a county unscoreable even though its detections are already stored.</para>
        /// <para>That used to read exactly like a county with no detections at all: an information level log, no failed step, and a run that ended green having skipped it. Only one of the two is recoverable, so they must not report identically.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_MissingResults()
        {
            int countyId = 73485;

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_MissingResults));
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(CultureInfo.CurrentCulture));

            if (Directory.Exists(directory_County))
            {
                Directory.Delete(directory_County, true);
            }

            //Laid out exactly as a run leaves a county, minus the results file
            Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));

            GISWebAPIManager gISWebAPIManager = new(null);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                CleanScratchDirectory = false,
                ExportImages = false,
                RunPrediction = false,
                Score = false,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false
            };

            YearBuiltPredictionResult? yearBuiltPredictionResult = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(0, yearBuiltPredictionResult!.BuildingCount);

            //Named after the reader that had nothing to read, and the reason travels with the result
            Assert.Contains(nameof(DiGi.YOLO.Create.BoundingBoxResultFile), yearBuiltPredictionResult.FailedStepNames);
            Assert.Contains(Constants.FileName.PredictionResults, string.Join(" ", yearBuiltPredictionResult.Messages));
        }

        /// <summary>
        /// Verifies that a county's scratch folder is removed once the run has finished with it, kept when the options ask for it to be kept, and kept when the county failed.
        /// <para>Removing it is what closes the gap the two pass workflow had: with nothing left on disk, no later run can quietly depend on what a successful county wrote there. Keeping it is what the committed split templates ask for, so both directions are pinned - a cleanup that ignored the flag would delete the detections a manual scoring pass is about to read.</para>
        /// <para>A failed county keeps its folder whatever the flag says. The export and the inference that filled it cost roughly half an hour and an hour and a half, and the feature coverage refusal - a reproducible configuration error - fires only after both have been paid for, so deleting the result of a failure turns a seconds long correction into a two hour one.</para>
        /// <para>The scratch root itself is deliberately not removed. The tray application writes the options a run was started with into it, and that is the only record of what the run was asked to do.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_CleanScratchDirectory()
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, nameof(RunYearBuiltPredictions_CleanScratchDirectory));
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(CultureInfo.CurrentCulture));

            void LayOutCounty()
            {
                Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));
                File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);
            }

            GISWebAPIManager gISWebAPIManager = new(null);

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                ExportImages = false,
                RunPrediction = false,
                Score = false,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false
            };

            //Kept: the detections a manual scoring pass would read are still there afterwards
            LayOutCounty();

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_Kept = new(yearBuiltPredictionPipelineOptions) { CleanScratchDirectory = false };
            YearBuiltPredictionResult? yearBuiltPredictionResult_Kept = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions_Kept);

            Assert.NotNull(yearBuiltPredictionResult_Kept);
            Assert.Equal(2, yearBuiltPredictionResult_Kept!.BuildingCount);
            Assert.True(Directory.Exists(directory_County));
            Assert.True(File.Exists(Path.Combine(directory_County, Constants.FileName.PredictionResults)));

            //Cleaned: the same detections were read, and nothing was left behind
            LayOutCounty();

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_Cleaned = new(yearBuiltPredictionPipelineOptions) { CleanScratchDirectory = true };
            YearBuiltPredictionResult? yearBuiltPredictionResult_Cleaned = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions_Cleaned);

            Assert.NotNull(yearBuiltPredictionResult_Cleaned);
            Assert.Equal(2, yearBuiltPredictionResult_Cleaned!.BuildingCount);
            Assert.False(Directory.Exists(directory_County));

            //The run cleans up after its county, not after the scratch directory it was given
            Assert.True(Directory.Exists(directory_Scratch));

            //Failed: the flag asks for cleanup, but the county did not come through, so what it cost to produce stays
            //on disk. Scoring with no predictor fails the county without needing a server for it.
            LayOutCounty();

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_Failed = new(yearBuiltPredictionPipelineOptions) { CleanScratchDirectory = true, Score = true };
            YearBuiltPredictionResult? yearBuiltPredictionResult_Failed = await gISWebAPIManager.RunYearBuiltPredictionsAsync(null, yearBuiltPredictionPipelineOptions_Failed);

            Assert.NotNull(yearBuiltPredictionResult_Failed);
            Assert.Contains(nameof(IYearBuiltPredictor), yearBuiltPredictionResult_Failed!.FailedStepNames);
            Assert.True(Directory.Exists(directory_County));
            Assert.True(File.Exists(Path.Combine(directory_County, Constants.FileName.PredictionResults)));

            //Cleaned up by the test rather than by the run, so a later run of this fact starts from a known state
            Directory.Delete(directory_County, true);
        }
    }
}
