using DiGi.GIS.Classes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the results of a county's chunks joined by <see cref="Modify.AssemblePredictionResults(IEnumerable{string}?, string?)"/> say exactly what one pass over the same images said (DiGi-Organization/DiGi.GIS.YOLO.UI#30).
        /// <para>The flat fixture is split by building into three chunk files - the middle one absent, as the detector leaves it for a chunk with no imagery, and the last one without a final line break. The joined file must equal the fixture line for line and parse into the same per-building predictions, and joining twice must rewrite the file rather than double it.</para>
        /// </summary>
        [Fact]
        public void AssemblePredictionResults()
        {
            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "YOLO_Prediction.bbrf");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory_Reports));

            string directory = Path.Combine(directory_Reports!, nameof(AssemblePredictionResults));
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);

            List<string> lines_Fixture = [.. File.ReadAllLines(path_Fixture!).Where(x => !string.IsNullOrWhiteSpace(x))];
            Assert.NotEmpty(lines_Fixture);

            List<string> lines_First = [.. lines_Fixture.Where(x => x.StartsWith("0207_", StringComparison.CurrentCulture))];
            List<string> lines_Last = [.. lines_Fixture.Where(x => !x.StartsWith("0207_", StringComparison.CurrentCulture))];
            Assert.NotEmpty(lines_First);
            Assert.NotEmpty(lines_Last);

            string path_First = Path.Combine(directory, "000000" + Constants.FileName.PredictionChunkResultsExtension);
            string path_Middle = Path.Combine(directory, "000001" + Constants.FileName.PredictionChunkResultsExtension);
            string path_Last = Path.Combine(directory, "000002" + Constants.FileName.PredictionChunkResultsExtension);

            File.WriteAllText(path_First, string.Join("\n", lines_First) + "\n");
            File.WriteAllText(path_Last, string.Join("\n", lines_Last));

            string path_Results = Path.Combine(directory, Constants.FileName.PredictionResults);

            for (int i = 0; i < 2; i++)
            {
                Assert.True(Modify.AssemblePredictionResults([path_First, path_Middle, path_Last], path_Results));
                Assert.False(File.Exists(path_Results + ".tmp"));

                List<string> lines_Assembled = [.. File.ReadAllLines(path_Results)];
                Assert.Equal(lines_Fixture, lines_Assembled);
            }

            List<Building2DYearBuiltPredictions>? building2DYearBuiltPredictions_Fixture = GIS.YOLO.Create.Building2DYearBuiltPredictions(DiGi.YOLO.Create.BoundingBoxResultFile(path_Fixture));
            List<Building2DYearBuiltPredictions>? building2DYearBuiltPredictions_Assembled = GIS.YOLO.Create.Building2DYearBuiltPredictions(DiGi.YOLO.Create.BoundingBoxResultFile(path_Results));

            Assert.NotNull(building2DYearBuiltPredictions_Fixture);
            Assert.NotNull(building2DYearBuiltPredictions_Assembled);
            Assert.Equal(building2DYearBuiltPredictions_Fixture!.Select(x => x.Reference).OrderBy(x => x), building2DYearBuiltPredictions_Assembled!.Select(x => x.Reference).OrderBy(x => x));
            Assert.Equal(building2DYearBuiltPredictions_Fixture.Sum(x => x.Years?.Count ?? 0), building2DYearBuiltPredictions_Assembled.Sum(x => x.Years?.Count ?? 0));

            //A county of chunks with no imagery at all joins into an empty file rather than no file
            string path_Empty = Path.Combine(directory, "empty.bbrf");
            Assert.True(Modify.AssemblePredictionResults([path_Middle], path_Empty));
            Assert.True(File.Exists(path_Empty));
            Assert.Equal(0, new FileInfo(path_Empty).Length);

            Assert.False(Modify.AssemblePredictionResults(null, path_Results));
            Assert.False(Modify.AssemblePredictionResults([path_First], null));
        }
    }
}
