using System.Globalization;
using System.IO;
using System.Threading;

namespace DiGi.YOLO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.PredictArguments(Classes.YOLOPredictionOptions?)"/> gives default options the command line of earlier runs plus --workers 2, and adds a setting that changes the detections only when it was asked for.
        /// <para>The defaults must reproduce the detections of earlier runs, so the exact string is asserted. An image size that is not a positive multiple of 32 must not appear, a negative worker count must be passed as 0, and the numbers must stay invariant under a culture with a decimal comma.</para>
        /// </summary>
        [Fact]
        public void PredictArguments()
        {
            string workingDirectory = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_Work");

            Classes.YOLOPredictionOptions yOLOPredictionOptions = new()
            {
                ModelPath = @"C:\YOLO\models\model.pt",
                OutputPath = @"C:\YOLO\output\results.bbrf",
                SourceDirectory = @"C:\YOLO\input",
                WorkingDirectory = workingDirectory
            };

            string arguments_Default = string.Format(CultureInfo.CurrentCulture, "\"{0}\" --model \"C:\\YOLO\\models\\model.pt\" --source \"C:\\YOLO\\input\" --output \"C:\\YOLO\\output\\results.bbrf\" --conf 0.1 --batch 32 --workers 2", Path.Combine(workingDirectory, "predict.py"));
            Assert.Equal(arguments_Default, Query.PredictArguments(yOLOPredictionOptions));

            CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("pl-PL");

                Classes.YOLOPredictionOptions yOLOPredictionOptions_Set = new(yOLOPredictionOptions)
                {
                    BatchSize = 64,
                    Confidence = 0.25,
                    HalfPrecision = true,
                    ImageSize = 320,
                    Workers = 4
                };

                string? arguments_Set = Query.PredictArguments(yOLOPredictionOptions_Set);
                Assert.NotNull(arguments_Set);
                Assert.EndsWith(" --conf 0.25 --batch 64 --imgsz 320 --half --workers 4", arguments_Set);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = cultureInfo;
            }

            foreach (int imageSize in new int[] { 0, -32, 300 })
            {
                Classes.YOLOPredictionOptions yOLOPredictionOptions_ImageSize = new(yOLOPredictionOptions) { ImageSize = imageSize };
                Assert.Equal(arguments_Default, Query.PredictArguments(yOLOPredictionOptions_ImageSize));
            }

            Classes.YOLOPredictionOptions yOLOPredictionOptions_Invalid = new(yOLOPredictionOptions) { BatchSize = 0, Workers = -2 };
            Assert.Equal(arguments_Default.Replace(" --workers 2", " --workers 0"), Query.PredictArguments(yOLOPredictionOptions_Invalid));

            Assert.Null(Query.PredictArguments(new Classes.YOLOPredictionOptions(yOLOPredictionOptions) { WorkingDirectory = null }));
            Assert.Null(Query.PredictArguments(new Classes.YOLOPredictionOptions(yOLOPredictionOptions) { ModelPath = " " }));
            Assert.Null(Query.PredictArguments(null));
        }
    }
}
