using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.GIS.YOLO.UI.Classes;
using DiGi.GIS.YOLO.UI.Enums;
using DiGi.UI.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the exclusion fields of the YOLO training options window round-trip (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#39): a previous run's buffer and boxes are shown on opening, and what is typed reaches the options the dataset build reads once OK is pressed.
        /// <para>The window opens with a 250 m buffer and one box, shows both, then the box text is replaced by two pasted T30 rows and the buffer is cleared. After OK the options carry the two boxes and the class default buffer, and the caller's instance still holds its original single box. A second window opened with no options shows the class default and an empty box list - today's behaviour. The OK click follows <see cref="YOLOTrainingOptionsWindow_Ok"/>: valid inputs only, and the DialogResult refusal of a never-shown window is swallowed after every assignment has been made.</para>
        /// </summary>
        [Fact]
        public void YOLOTrainingOptionsWindow_Exclusion()
        {
            Exception? exception = null;
            string? buffer_Shown = null;
            string? boxes_Shown = null;
            string? buffer_Default = null;
            string? boxes_Default = null;
            YOLOTrainingRunOptions? yOLOTrainingRunOptions_Written = null;

            YOLOTrainingRunOptions yOLOTrainingRunOptions = new()
            {
                StartWeightsPath = @"C:\YOLO\model.pt",
                RunName = "train11",
                ProjectDirectory = @"C:\YOLO\runs",
                Steps = [YOLOTrainingStep.Dataset],
                DatasetOptions = new YOLOTrainingDatasetOptions()
                {
                    CountyIds = [22138],
                    OutputDirectory = @"C:\YOLO\dataset",
                    ExclusionBuffer = 250,
                    ExclusionBoundingBoxes = [new YOLOExclusionBoundingBox(637739.56, 486870.79, 638739.56, 487870.79)]
                }
            };

            Thread thread = new(() =>
            {
                try
                {
                    List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                    [
                        new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
                    ];

                    YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow = new(YOLOTrainingScenario.Retrain, yOLOTrainingRunOptions, administrativeAreal2DReferences, null);

                    TextBoxControl textBoxControl_Buffer = (TextBoxControl)yOLOTrainingOptionsWindow.FindName("TextBoxControl_ExclusionBuffer")!;
                    System.Windows.Controls.TextBox textBox_Boxes = (System.Windows.Controls.TextBox)yOLOTrainingOptionsWindow.FindName("TextBox_ExclusionBoundingBoxes")!;

                    buffer_Shown = textBoxControl_Buffer.Value;
                    boxes_Shown = textBox_Boxes.Text;

                    textBoxControl_Buffer.Value = " ";
                    textBox_Boxes.Text = "637739.56\t486870.79\t638739.56\t487870.79\r\n637814.37\t469460.41\t638814.37\t470460.41\r\n";

                    try
                    {
                        ((System.Windows.Controls.Button)yOLOTrainingOptionsWindow.FindName("Button_OK")!).RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    catch (InvalidOperationException)
                    {
                        // DialogResult refuses a window that was never shown as a dialog - by then every
                        // assignment the handler makes has already been made.
                    }

                    yOLOTrainingRunOptions_Written = yOLOTrainingOptionsWindow.YOLOTrainingRunOptions;

                    YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow_Default = new(YOLOTrainingScenario.Retrain, null, administrativeAreal2DReferences, null);
                    buffer_Default = ((TextBoxControl)yOLOTrainingOptionsWindow_Default.FindName("TextBoxControl_ExclusionBuffer")!).Value;
                    boxes_Default = ((System.Windows.Controls.TextBox)yOLOTrainingOptionsWindow_Default.FindName("TextBox_ExclusionBoundingBoxes")!).Text;
                }
                catch (Exception exception_Temp)
                {
                    exception = exception_Temp;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(exception);

            // Shown on opening as stored.
            Assert.Equal("250", buffer_Shown);
            Assert.Equal("637739.56; 486870.79; 638739.56; 487870.79", boxes_Shown);

            // Written on OK.
            YOLOTrainingDatasetOptions? yOLOTrainingDatasetOptions_Written = yOLOTrainingRunOptions_Written?.DatasetOptions;
            Assert.NotNull(yOLOTrainingDatasetOptions_Written);
            Assert.Equal(new YOLOTrainingDatasetOptions().ExclusionBuffer, yOLOTrainingDatasetOptions_Written!.ExclusionBuffer);
            Assert.Equal(2, yOLOTrainingDatasetOptions_Written.ExclusionBoundingBoxes!.Count);
            Assert.Equal(637814.37, yOLOTrainingDatasetOptions_Written.ExclusionBoundingBoxes[1].MinX);
            Assert.Equal(470460.41, yOLOTrainingDatasetOptions_Written.ExclusionBoundingBoxes[1].MaxY);

            // The caller's instance is untouched.
            Assert.Equal(250, yOLOTrainingRunOptions.DatasetOptions!.ExclusionBuffer);
            Assert.Single(yOLOTrainingRunOptions.DatasetOptions.ExclusionBoundingBoxes!);

            // No options: the class default and no boxes, as before #39.
            Assert.Equal(new YOLOTrainingDatasetOptions().ExclusionBuffer.ToString("R", System.Globalization.CultureInfo.InvariantCulture), buffer_Default);
            Assert.Equal(string.Empty, boxes_Default);
        }
    }
}
