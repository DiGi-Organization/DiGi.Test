using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.GIS.YOLO.UI.Classes;
using DiGi.UI.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the Year Built prediction options window can be built and that it works on a copy of the options it is given, without a database or a running application.
        /// <para>The window is the only way a run started from the tray application can be scoped, and a run writes stored production data. A window whose markup failed to parse, or whose constructor threw on the county list, would not be found until someone opened it to scope a run and could not.</para>
        /// <para>What the constructor does is what this exercises: it parses the markup, wires the item naming of the county list, fills and sorts it, restores the selection from the options, and fills the scratch directory, the interpreter, the request concurrency and the scratch cleanup. A multi-part county is included because two of its pieces share a code and a name and are told apart only by the identifier the naming callback appends - and a run has to name every part, so both have to be selectable.</para>
        /// <para>The settings that are <b>not</b> on the window are asserted too, and they are much the larger half. The pipeline's six steps are settled by the handler rather than shown, because a tray run has one shape (DiGi-Organization/DiGi.GIS.YOLO.UI#8); the weights, the confidence threshold, the year range and the radiuses all decide what the regressor is handed, and a value that disagrees with what it was trained on scores without failing (DiGi-Organization/DiGi.GIS.ML#6); the working directory and the two batch sizes are not choices at all. Until OK is pressed every one of them is whatever the window was handed, which is what this asserts.</para>
        /// <para><b>It does not check what the window looks like.</b> A window is laid out by the handle it gets when it is shown, so measuring one that is never shown reports nothing, and showing one during a test run would put a dialog on screen. The controls are private to the window, so the values they end up holding cannot be read from here either - that the dialog behaves correctly on screen is still something a person has to look at once.</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_Construction()
        {
            Exception? exception = null;
            bool countiesCarried = false;
            bool pathsCarried = false;
            bool stepsCarried = false;
            bool settingsCarried = false;
            bool untouchedCarried = false;
            bool copied = false;

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [22138, 22139],
                ScratchDirectory = @"C:\YOLO\scratch",
                PythonPath = @"C:\Python\python.exe",
                ModelPath = @"C:\YOLO\models\model.pt",
                Confidence = 0.25,
                MaxConcurrentRequests = 4,
                BatchSize = 2500,
                ReferenceBatchSize = 5000,
                CleanScratchDirectory = false,
                ExportImages = true,
                RunPrediction = true,
                Score = true,
                UpdateDetections = false,
                UpdateYearBuiltData = false,
                UpdatePredictedYearBuilt = false,
                Years = new Core.Classes.Range<int>(1900, 2000),
                Radiuses = [1.0, 2.0]
            };

            Thread thread = new(() =>
            {
                try
                {
                    List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                    [
                        new() { Id = 55417, Code = "1465", Name = "m. St. Warszawa", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                        // Two pieces of one multi-part county: same code and name, told apart only by the identifier.
                        new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 22139, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
                    ];

                    YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow = new(yearBuiltPredictionPipelineOptions, administrativeAreal2DReferences);

                    YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_Held = yearBuiltPredictionsOptionsWindow.YearBuiltPredictionPipelineOptions;

                    // Until OK is pressed the window holds what it was given.
                    countiesCarried = yearBuiltPredictionPipelineOptions_Held.CountyIds is HashSet<int> countyIds && countyIds.Count == 2 && countyIds.Contains(22138) && countyIds.Contains(22139);
                    pathsCarried = yearBuiltPredictionPipelineOptions_Held.ScratchDirectory == @"C:\YOLO\scratch" && yearBuiltPredictionPipelineOptions_Held.PythonPath == @"C:\Python\python.exe";
                    stepsCarried = yearBuiltPredictionPipelineOptions_Held.ExportImages && yearBuiltPredictionPipelineOptions_Held.RunPrediction && yearBuiltPredictionPipelineOptions_Held.Score && !yearBuiltPredictionPipelineOptions_Held.UpdateDetections && !yearBuiltPredictionPipelineOptions_Held.UpdateYearBuiltData && !yearBuiltPredictionPipelineOptions_Held.UpdatePredictedYearBuilt;

                    // The request concurrency and the cleanup flag are on the window; before OK they are whatever
                    // the run was given, which is what the dialog pre-fills the controls with.
                    settingsCarried = yearBuiltPredictionPipelineOptions_Held.MaxConcurrentRequests == 4 && !yearBuiltPredictionPipelineOptions_Held.CleanScratchDirectory;

                    // The settings the window does not show survive it. Four of them have to match what the
                    // regressor was trained on rather than what a dialog was last set to; the other three are not
                    // choices the operator has.
                    untouchedCarried = yearBuiltPredictionPipelineOptions_Held.ModelPath == @"C:\YOLO\models\model.pt"
                        && yearBuiltPredictionPipelineOptions_Held.WorkingDirectory is null
                        && yearBuiltPredictionPipelineOptions_Held.Confidence == 0.25
                        && yearBuiltPredictionPipelineOptions_Held.BatchSize == 2500
                        && yearBuiltPredictionPipelineOptions_Held.ReferenceBatchSize == 5000
                        && yearBuiltPredictionPipelineOptions_Held.Years is Core.Classes.Range<int> years && years.Min == 1900 && years.Max == 2000
                        && yearBuiltPredictionPipelineOptions_Held.Radiuses is not null && yearBuiltPredictionPipelineOptions_Held.Radiuses.Count == 2 && yearBuiltPredictionPipelineOptions_Held.Radiuses[0] == 1.0 && yearBuiltPredictionPipelineOptions_Held.Radiuses[1] == 2.0;

                    // A cancelled dialog has to leave the caller's options alone, which only holds if the window
                    // took a copy rather than a reference.
                    copied = !ReferenceEquals(yearBuiltPredictionPipelineOptions, yearBuiltPredictionPipelineOptions_Held);
                    yearBuiltPredictionPipelineOptions_Held.Confidence = 0.9;
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
            Assert.True(countiesCarried);
            Assert.True(pathsCarried);
            Assert.True(stepsCarried);
            Assert.True(settingsCarried);
            Assert.True(untouchedCarried);
            Assert.True(copied);

            // The caller's own instance is untouched by the edit made to the window's copy above.
            Assert.Equal(0.25, yearBuiltPredictionPipelineOptions.Confidence);
        }

        /// <summary>
        /// Verifies that closing the Year Built prediction options window with OK writes every control into the options the window holds, and that the members the window has no control for survive it unchanged.
        /// <para>The round trip is the whole contract the run depends on: whatever the operator chose in the dialog is what the options file written beside the scratch directory carries, and a member the dialog deliberately does not settle must come through the copy exactly as it was handed in.</para>
        /// <para><b>The step flags are asserted the other way round.</b> A tray run has one shape - the full six step flow (DiGi-Organization/DiGi.GIS.YOLO.UI#8) - so the handler writes them rather than reading them, and they are handed in here every one of them <i>off</i>. Six on afterwards is the assertion; the three write flags default to false and the window is handed the previous run's options, so a handler that stopped writing them would give a run that reads a county, scores it and stores nothing while reporting that it ran.</para>
        /// <para><b>The seven settled elsewhere are asserted unchanged.</b> The weights, the confidence threshold, the year range and the radiuses have to match what the regressor was trained on (DiGi-Organization/DiGi.GIS.ML#6); the working directory and the two batch sizes are not choices. Every one is handed in as a non-default value, so a control added for one of them, or a member reset to its default on the way through, fails this rather than being discovered from predictions nothing measures.</para>
        /// <para>The click is raised on a window that was never shown, so the handler runs to its final DialogResult assignment, which throws on a window that was never shown as a dialog - everything the handler writes has already been written by then, which is what makes the thrown half harmless here. The inputs are all valid on purpose: a validation failure would put a message box on screen and block the run rather than fail it.</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_Ok()
        {
            Exception? exception = null;
            bool pathsWritten = false;
            bool numericWritten = false;
            bool stepsWritten = false;
            bool countiesCarried = false;
            bool untouchedSurvived = false;

            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                CountyIds = [22138, 22139],
                // Every step is handed in OFF, including the three that default that way. A tray run has one
                // shape and the handler writes it, so the assertion below is that all six came back ON having
                // been given the opposite - which is what a run that silently stored nothing would fail.
                ExportImages = false,
                RunPrediction = false,
                Score = false,
                Resume = false,
                UpdateDetections = false,
                UpdateYearBuiltData = false,
                UpdatePredictedYearBuilt = false,
                // Settled outside the dialog, and non-default here so that a control added for one of them, or a
                // reset on the way through, is caught by the click below.
                ModelPath = @"C:\YOLO\models\model.pt",
                WorkingDirectory = @"C:\YOLO\working",
                Confidence = 0.25,
                BatchSize = 2500,
                ReferenceBatchSize = 5000,
                Years = new Core.Classes.Range<int>(1900, 2000),
                Radiuses = [1.0, 2.0]
            };

            Thread thread = new(() =>
            {
                try
                {
                    List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                    [
                        new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 22139, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
                    ];

                    YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow = new(yearBuiltPredictionPipelineOptions, administrativeAreal2DReferences);

                    ((TextBoxControl)yearBuiltPredictionsOptionsWindow.FindName("TextBoxControl_ScratchDirectory")!).Value = @"C:\YOLO\scratch_ok";
                    ((TextBoxControl)yearBuiltPredictionsOptionsWindow.FindName("TextBoxControl_PythonPath")!).Value = @"C:\Python\python.exe";
                    ((TextBoxControl)yearBuiltPredictionsOptionsWindow.FindName("TextBoxControl_MaxConcurrentRequests")!).Value = "3";

                    // The only flag the dialog still shows. The step checkboxes are gone, and FindName returning
                    // null for one of them is what a re-added control would look like here.
                    ((System.Windows.Controls.CheckBox)yearBuiltPredictionsOptionsWindow.FindName("CheckBox_CleanScratchDirectory")!).IsChecked = false;

                    Assert.Null(yearBuiltPredictionsOptionsWindow.FindName("CheckBox_ExportImages"));
                    Assert.Null(yearBuiltPredictionsOptionsWindow.FindName("CheckBox_RunPrediction"));
                    Assert.Null(yearBuiltPredictionsOptionsWindow.FindName("CheckBox_Score"));
                    Assert.Null(yearBuiltPredictionsOptionsWindow.FindName("CheckBox_Resume"));
                    Assert.Null(yearBuiltPredictionsOptionsWindow.FindName("CheckBox_UpdateDetections"));
                    Assert.Null(yearBuiltPredictionsOptionsWindow.FindName("CheckBox_UpdateYearBuiltData"));
                    Assert.Null(yearBuiltPredictionsOptionsWindow.FindName("CheckBox_UpdatePredictedYearBuilt"));

                    try
                    {
                        ((System.Windows.Controls.Button)yearBuiltPredictionsOptionsWindow.FindName("Button_OK")!).RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    }
                    catch (InvalidOperationException)
                    {
                        // DialogResult refuses a window that was never shown as a dialog - by then every
                        // assignment the handler makes has already been made.
                    }

                    YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_Written = yearBuiltPredictionsOptionsWindow.YearBuiltPredictionPipelineOptions;

                    pathsWritten = yearBuiltPredictionPipelineOptions_Written.ScratchDirectory == @"C:\YOLO\scratch_ok" && yearBuiltPredictionPipelineOptions_Written.PythonPath == @"C:\Python\python.exe";
                    numericWritten = yearBuiltPredictionPipelineOptions_Written.MaxConcurrentRequests == 3;
                    // All six on, and Resume with them, from options that handed in every one of them off. The
                    // cleanup is the one that follows its checkbox.
                    stepsWritten = yearBuiltPredictionPipelineOptions_Written.ExportImages && yearBuiltPredictionPipelineOptions_Written.RunPrediction && yearBuiltPredictionPipelineOptions_Written.Score && yearBuiltPredictionPipelineOptions_Written.Resume && yearBuiltPredictionPipelineOptions_Written.UpdateDetections && yearBuiltPredictionPipelineOptions_Written.UpdateYearBuiltData && yearBuiltPredictionPipelineOptions_Written.UpdatePredictedYearBuilt && !yearBuiltPredictionPipelineOptions_Written.CleanScratchDirectory;
                    countiesCarried = yearBuiltPredictionPipelineOptions_Written.CountyIds is HashSet<int> countyIds && countyIds.Count == 2 && countyIds.Contains(22138) && countyIds.Contains(22139);

                    // The members the window deliberately has no control for survive the click unchanged.
                    untouchedSurvived = yearBuiltPredictionPipelineOptions_Written.ModelPath == @"C:\YOLO\models\model.pt"
                        && yearBuiltPredictionPipelineOptions_Written.WorkingDirectory == @"C:\YOLO\working"
                        && yearBuiltPredictionPipelineOptions_Written.Confidence == 0.25
                        && yearBuiltPredictionPipelineOptions_Written.BatchSize == 2500
                        && yearBuiltPredictionPipelineOptions_Written.ReferenceBatchSize == 5000
                        && yearBuiltPredictionPipelineOptions_Written.Years is Core.Classes.Range<int> years && years.Min == 1900 && years.Max == 2000
                        && yearBuiltPredictionPipelineOptions_Written.Radiuses is not null && yearBuiltPredictionPipelineOptions_Written.Radiuses.Count == 2 && yearBuiltPredictionPipelineOptions_Written.Radiuses[0] == 1.0 && yearBuiltPredictionPipelineOptions_Written.Radiuses[1] == 2.0;
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
            Assert.True(pathsWritten);
            Assert.True(numericWritten);
            Assert.True(stepsWritten);
            Assert.True(countiesCarried);
            Assert.True(untouchedSurvived);
        }

        /// <summary>
        /// Verifies that the Year Built prediction options window opens with <c>scratch</c> in the scratch directory box when the options carry none, and shows a carried value exactly as it is.
        /// <para>A first run carries no options, so the scratch directory is null and the box used to open empty, which OK then refused until a path was typed by hand. Null, an empty string and whitespace all count as none; a window given no options at all is the first-run case itself.</para>
        /// <para>The carried value is padded on purpose: a default substituted by trimming, or by testing the trimmed value and writing it back, would change what an earlier run had chosen.</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_ScratchDirectory_Default()
        {
            Exception? exception = null;
            List<string?> scratchDirectories = [];

            Thread thread = new(() =>
            {
                try
                {
                    List<YearBuiltPredictionPipelineOptions?> yearBuiltPredictionPipelineOptionsList =
                    [
                        null,
                        new() { ScratchDirectory = null },
                        new() { ScratchDirectory = string.Empty },
                        new() { ScratchDirectory = "   " },
                        new() { ScratchDirectory = @" C:\YOLO\scratch " }
                    ];

                    foreach (YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions in yearBuiltPredictionPipelineOptionsList)
                    {
                        YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow = new(yearBuiltPredictionPipelineOptions, []);
                        scratchDirectories.Add(((TextBoxControl)yearBuiltPredictionsOptionsWindow.FindName("TextBoxControl_ScratchDirectory")!).Value);
                    }
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
            Assert.Equal(5, scratchDirectories.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal("scratch", scratchDirectories[i]);
            }

            Assert.Equal(@" C:\YOLO\scratch ", scratchDirectories[4]);
        }

        /// <summary>
        /// Verifies that closing the Year Built prediction options window with OK while the scratch directory box is empty or whitespace writes <c>scratch</c> to the options instead of refusing.
        /// <para>The inputs other than the scratch directory are valid on purpose: the old refusal put a message box on screen, which would block this test rather than fail it, so the options carrying the value at all is what shows it is gone. The click is raised on a window that was never shown, so the handler runs to its final DialogResult assignment, which throws - everything it writes has been written by then.</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_ScratchDirectory_Ok()
        {
            Exception? exception = null;
            List<string?> scratchDirectories = [];

            Thread thread = new(() =>
            {
                try
                {
                    foreach (string text in new string[] { string.Empty, "   " })
                    {
                        List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                        [
                            new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
                        ];

                        YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow = new(new YearBuiltPredictionPipelineOptions() { CountyIds = [22138] }, administrativeAreal2DReferences);

                        ((TextBoxControl)yearBuiltPredictionsOptionsWindow.FindName("TextBoxControl_ScratchDirectory")!).Value = text;
                        ((TextBoxControl)yearBuiltPredictionsOptionsWindow.FindName("TextBoxControl_MaxConcurrentRequests")!).Value = "3";

                        try
                        {
                            ((System.Windows.Controls.Button)yearBuiltPredictionsOptionsWindow.FindName("Button_OK")!).RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        }
                        catch (InvalidOperationException)
                        {
                            // DialogResult refuses a window that was never shown as a dialog.
                        }

                        scratchDirectories.Add(yearBuiltPredictionsOptionsWindow.YearBuiltPredictionPipelineOptions.ScratchDirectory);
                    }
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
            Assert.Equal(["scratch", "scratch"], scratchDirectories);
        }
    }
}
