using DiGi.GIS.WebAPI.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that options narrowing the year range below the model's trained range are refused before any county is read.
        /// <para>The stub states the model's contract (2008..2025) and the options ask for 2008..2020, so the projection drops the columns the model was fitted on and every prediction would silently degrade. The refusal must name the feature contract, never reach the predictor, and name both what the options ask for and what the model needs.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictionsFeatureContract_Narrowed()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965, years: new DiGi.Core.Classes.Range<int>(2008, 2025), radiuses: [200, 400, 600, 1000]);

            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunFeatureContract(nameof(RunYearBuiltPredictionsFeatureContract_Narrowed), years: new DiGi.Core.Classes.Range<int>(2008, 2020), radiuses: null, yearBuiltPredictorStub: yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(0, yearBuiltPredictorStub.CallCount);
            Assert.Equal(0, yearBuiltPredictionResult!.BuildingCount);

            //The refusal is named after the feature contract, and the message names both the options' range and the model's range
            Assert.Contains(nameof(IO.Query.YearBuiltPredictionInputColumnNames), yearBuiltPredictionResult.FailedStepNames);
            Assert.Contains("2020", string.Join(" ", yearBuiltPredictionResult.Messages));
            Assert.Contains("2025", string.Join(" ", yearBuiltPredictionResult.Messages));
        }

        /// <summary>
        /// Verifies that options widening the year range above the model's trained range run rather than being refused, and that the surplus is reported.
        /// <para>Widening is harmless - the extra columns are read and then ignored, because the model has no member for them - so the run must proceed and the operator must be told which features are surplus.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictionsFeatureContract_Widened()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965, years: new DiGi.Core.Classes.Range<int>(2008, 2025), radiuses: [200, 400, 600, 1000]);

            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunFeatureContract(nameof(RunYearBuiltPredictionsFeatureContract_Widened), years: new DiGi.Core.Classes.Range<int>(2005, 2025), radiuses: null, yearBuiltPredictorStub: yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);

            Assert.DoesNotContain(nameof(IO.Query.YearBuiltPredictionInputColumnNames), yearBuiltPredictionResult.FailedStepNames);
            Assert.Contains("ignored", string.Join(" ", yearBuiltPredictionResult.Messages));
        }

        /// <summary>
        /// Verifies that options that are wider on one end and narrower on the other are refused, and that the surplus is reported alongside the narrowing.
        /// <para>2000..2020 against a 2008..2025 model is wider below and narrower above: the widening is harmless and the narrowing is not, so the run is refused and the operator is told about both directions.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictionsFeatureContract_Mixed()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965, years: new DiGi.Core.Classes.Range<int>(2008, 2025), radiuses: [200, 400, 600, 1000]);

            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunFeatureContract(nameof(RunYearBuiltPredictionsFeatureContract_Mixed), years: new DiGi.Core.Classes.Range<int>(2000, 2020), radiuses: null, yearBuiltPredictorStub: yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(0, yearBuiltPredictorStub.CallCount);

            Assert.Contains(nameof(IO.Query.YearBuiltPredictionInputColumnNames), yearBuiltPredictionResult.FailedStepNames);
            Assert.Contains("ignored", string.Join(" ", yearBuiltPredictionResult.Messages));
        }

        /// <summary>
        /// Verifies that options narrowing the radiuses below the model's are refused, on the column-name set rather than a range comparison.
        /// <para>Dropping a radius removes its radial ratio features, so the projection no longer carries everything the model was fitted on.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictionsFeatureContract_RadiusesNarrowed()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965, years: new DiGi.Core.Classes.Range<int>(2008, 2025), radiuses: [200, 400, 600, 1000]);

            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunFeatureContract(nameof(RunYearBuiltPredictionsFeatureContract_RadiusesNarrowed), years: null, radiuses: [200, 400, 600], yearBuiltPredictorStub: yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(0, yearBuiltPredictorStub.CallCount);

            Assert.Contains(nameof(IO.Query.YearBuiltPredictionInputColumnNames), yearBuiltPredictionResult.FailedStepNames);
        }

        /// <summary>
        /// Verifies that a predictor that states no contract leaves the old behaviour in place: the options are not checked against it, so a narrowed range is not refused.
        /// <para>This is the gate that keeps stubs and third-party predictors unchanged - the check is driven by the predictor stating a contract, not by the options.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictionsFeatureContract_NoContract()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965);

            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunFeatureContract(nameof(RunYearBuiltPredictionsFeatureContract_NoContract), years: new DiGi.Core.Classes.Range<int>(2008, 2020), radiuses: null, yearBuiltPredictorStub: yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);

            Assert.DoesNotContain(nameof(IO.Query.YearBuiltPredictionInputColumnNames), yearBuiltPredictionResult.FailedStepNames);
        }

        /// <summary>
        /// Drives the orchestrator over the stored detection fixture with the named feature-contract options, so the contract check can be exercised without ML.NET, a model file or a database.
        /// </summary>
        /// <param name="name">The name of the calling fact, used as the scratch directory so the runs cannot share state.</param>
        /// <param name="years">The year range the options name, or null for the default.</param>
        /// <param name="radiuses">The radiuses the options name, or null for the default.</param>
        /// <param name="yearBuiltPredictorStub">The predictor handed to the run, so the caller can assert whether it was reached.</param>
        /// <returns>A task returning the result of the run.</returns>
        private static async Task<Classes.YearBuiltPredictionResult?> RunFeatureContract(string name, DiGi.Core.Classes.Range<int>? years, List<double>? radiuses, YearBuiltPredictorStub yearBuiltPredictorStub)
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, name);
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString(CultureInfo.CurrentCulture));
            Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            GISWebAPIManager gISWebAPIManager = new(null);

            Classes.YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [countyId],
                ScratchDirectory = directory_Scratch,
                ExportImages = false,
                RunPrediction = false,
                Score = true,
                UpdateDetections = false,
                UpdatePredictedYearBuilt = false,
                UpdateYearBuiltData = false,
                Years = years,
                Radiuses = radiuses
            };

            return await gISWebAPIManager.RunYearBuiltPredictionsAsync(yearBuiltPredictorStub, yearBuiltPredictionPipelineOptions);
        }
    }
}
