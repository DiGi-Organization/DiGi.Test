using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.WebAPI.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a county whose stored detection features are wholly absent is refused rather than scored.
        /// <para>This is the failure the check exists for: the features come from the building data, so a county that has never had a detection write scores against ninety zeroes and the regressor answers an ordinary looking year. Nothing downstream can tell that apart from a real prediction, so the run has to stop here.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_FeatureCoverage_Refused()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965);
            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunWithStubbedFeatures(nameof(RunYearBuiltPredictions_FeatureCoverage_Refused), [IO.Constants.YearBuiltPredictionFeatureGroup.Detection, IO.Constants.YearBuiltPredictionFeatureGroup.Population], yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);

            //The detections were still read off disk - it is the scoring that is refused, not the run
            Assert.Equal(2, yearBuiltPredictionResult!.BuildingCount);

            Assert.Contains(nameof(IO.Query.UnpopulatedColumnNames), yearBuiltPredictionResult.FailedStepNames);

            //Refused before the model saw anything, and nothing was carried forward as a prediction
            Assert.Equal(0, yearBuiltPredictorStub.CallCount);
            Assert.Equal(0, yearBuiltPredictionResult.PredictionCount);

            //The message has to name the group and the run that fills it, or an operator learns only that the run failed
            Assert.NotNull(yearBuiltPredictionResult.Messages);
            Assert.Contains(yearBuiltPredictionResult.Messages!, x => x.Contains(IO.Constants.YearBuiltPredictionFeatureGroup.Detection) && x.Contains("UpdateDetections"));
        }

        /// <summary>
        /// Verifies that a county whose stored features are populated is scored, so the check refuses an empty feature table without standing in the way of an ordinary run.
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_FeatureCoverage_Scored()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965);
            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunWithStubbedFeatures(nameof(RunYearBuiltPredictions_FeatureCoverage_Scored), [], yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(2, yearBuiltPredictionResult!.BuildingCount);

            Assert.DoesNotContain(nameof(IO.Query.UnpopulatedColumnNames), yearBuiltPredictionResult.FailedStepNames);

            Assert.Equal(1, yearBuiltPredictorStub.CallCount);
            Assert.Equal(2, yearBuiltPredictionResult.FeatureRowCount);
            Assert.Equal(2, yearBuiltPredictionResult.PredictionCount);
        }

        /// <summary>
        /// Verifies that a county whose detection features are present but whose population columns are empty is scored, not refused, when the predictor states it requires only the detection group.
        /// <para>This is the fix for the guard drifting from the predictor (DiGi-Organization/DiGi.GIS.YOLO.UI#27): the first-detection heuristic reads the detection confidence columns and nothing else, so an empty population group - a county with no Statistical update yet - warns rather than refusing. The historical default, where the guard still requires the population group, is exercised by <see cref="RunYearBuiltPredictions_FeatureCoverage_Refused"/>.</para>
        /// </summary>
        [Fact]
        public async Task RunYearBuiltPredictions_FeatureCoverage_PopulationAbsent_Scored()
        {
            YearBuiltPredictorStub yearBuiltPredictorStub = new(1965, requiredFeatureGroups: [IO.Constants.YearBuiltPredictionFeatureGroup.Detection]);
            Classes.YearBuiltPredictionResult? yearBuiltPredictionResult = await RunWithStubbedFeatures(nameof(RunYearBuiltPredictions_FeatureCoverage_PopulationAbsent_Scored), [IO.Constants.YearBuiltPredictionFeatureGroup.Population], yearBuiltPredictorStub);

            Assert.NotNull(yearBuiltPredictionResult);
            Assert.Equal(2, yearBuiltPredictionResult!.BuildingCount);

            //Not refused: the group the predictor requires (detection) is present, so the empty population group only warns.
            Assert.DoesNotContain(nameof(IO.Query.UnpopulatedColumnNames), yearBuiltPredictionResult.FailedStepNames);

            //Scored: the predictor was reached and produced a prediction for every building it fired on.
            Assert.Equal(1, yearBuiltPredictorStub.CallCount);
            Assert.Equal(2, yearBuiltPredictionResult.FeatureRowCount);
            Assert.Equal(2, yearBuiltPredictionResult.PredictionCount);
        }

        /// <summary>
        /// Drives the orchestrator over the stored detection fixture with the feature read stubbed, so the coverage check can be exercised without a database.
        /// </summary>
        /// <param name="name">The name of the calling fact, used as the scratch directory so the runs cannot share state.</param>
        /// <param name="emptyGroups">The feature-group names whose columns the stubbed feature table leaves at the type default; every other column carries a value.</param>
        /// <param name="yearBuiltPredictorStub">The predictor handed to the run, so the caller can assert whether it was reached.</param>
        /// <returns>A task returning the result of the run.</returns>
        private static async Task<Classes.YearBuiltPredictionResult?> RunWithStubbedFeatures(string name, IEnumerable<string> emptyGroups, YearBuiltPredictorStub yearBuiltPredictorStub)
        {
            int countyId = 73485;

            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory_Scratch = Path.Combine(directory_Reports!, name);
            string directory_County = Path.Combine(directory_Scratch, countyId.ToString());
            Directory.CreateDirectory(Path.Combine(directory_County, Constants.DirectoryName.PredictionImages));
            File.Copy(path_Fixture!, Path.Combine(directory_County, Constants.FileName.PredictionResults), true);

            string json_Table = FeatureTableJson(["0207", "0209"], emptyGroups);

            StubHttpClientFactory stubHttpClientFactory = new(httpRequestMessage =>
            {
                string requestUri = httpRequestMessage.RequestUri?.ToString() ?? string.Empty;

                //Only the feature read is answered. The county rows are not, which leaves the run treating the
                //county as a single polygon part - the branch the orchestrator already logs a warning for.
                if (requestUri.IndexOf("buildingdata", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json_Table, Encoding.UTF8, "application/json")
                };
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);

            Classes.YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
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

            return await gISWebAPIManager.RunYearBuiltPredictionsAsync(yearBuiltPredictorStub, yearBuiltPredictionPipelineOptions);
        }

        /// <summary>
        /// Builds the JSON a stubbed feature read answers with, carrying the reference column and the whole input allow-list.
        /// </summary>
        /// <param name="references">The building references to write one row each for.</param>
        /// <param name="emptyGroups">The feature-group names whose columns carry the type default in every row; every other column carries a value.</param>
        /// <returns>The serialized table, in the form the endpoint returns it.</returns>
        private static string FeatureTableJson(IEnumerable<string> references, IEnumerable<string> emptyGroups)
        {
            Dictionary<string, List<Column>> columns_ByGroup = IO.Query.YearBuiltPredictionFeatureGroups();

            HashSet<string> names_Empty = [];
            foreach (string name_Group in emptyGroups)
            {
                foreach (Column column in columns_ByGroup[name_Group])
                {
                    names_Empty.Add(column.Name!);
                }
            }

            List<Column> columns = [IO.Constants.Column.Reference];
            columns.AddRange(IO.Query.YearBuiltPredictionInputColumns());

            Table table = new(columns);

            foreach (string reference in references)
            {
                List<object?> values = [];
                foreach (Column column in columns)
                {
                    if (column.Name == IO.Constants.Column.Reference.Name)
                    {
                        values.Add(reference);
                        continue;
                    }

                    values.Add(names_Empty.Contains(column.Name!) ? Default(column.Type) : Value(column.Type));
                }

                table.AddRow([.. values]);
            }

            JsonSerializerOptions jsonSerializerOptions = new();
            jsonSerializerOptions.Converters.Add(new TableConverter<Table, Column, Row>());

            return JsonSerializer.Serialize(table, jsonSerializerOptions);
        }

        /// <summary>
        /// Answers the type default for a column type, which is what an unwritten cell reads as.
        /// </summary>
        /// <param name="type">The column type.</param>
        /// <returns>The default value of the type, or an empty string for a text column.</returns>
        private static object? Default(Type? type)
        {
            if (type is null || type == typeof(string))
            {
                return string.Empty;
            }

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        /// <summary>
        /// Answers a value that is not the type default, so a column built from it counts as populated.
        /// </summary>
        /// <param name="type">The column type.</param>
        /// <returns>A non-default value of the type.</returns>
        private static object? Value(Type? type)
        {
            if (type is null || type == typeof(string))
            {
                return "x";
            }

            if (type == typeof(bool))
            {
                return true;
            }

            return type.IsValueType ? System.Convert.ChangeType(1, type) : "x";
        }
    }
}
