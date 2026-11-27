using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.GIS.YOLO.UI.Classes;
using DiGi.UI.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Size = System.Windows.Size;

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

        /// <summary>
        /// Verifies that the settings of the Year Built window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#74): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>The step flags and the resume switch are part of the round trip because OK always writes them on: the exporting window is shown with them on, as any run OK completed left them, so the file and OK's forced shape agree. A blank scratch box exports as no directory, and the window filled from that file shows the default in the box and OK substitutes the default on read-back - exactly as when typed.</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_SettingsRoundTrip()
        {
            YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
            {
                ScratchDirectory = "yb_scratch",
                PythonPath = "C:/python/python.exe",
                MaxConcurrentRequests = 4,
                CleanScratchDirectory = true,
                CountyIds = [1001, 1003],
                ExportImages = true,
                RunPrediction = true,
                Score = true,
                UpdateDetections = true,
                UpdateYearBuiltData = true,
                UpdatePredictedYearBuilt = true,
                Resume = true,
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_Read = null;
            string? blankExported = null;
            string? blankAfterOk = null;
            bool refusedWithoutCounty = false;

            Exception? exception = RunSTA(() =>
            {
                YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow = new(yearBuiltPredictionPipelineOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(yearBuiltPredictionPipelineOptions, yearBuiltPredictionsOptionsWindow.YearBuiltPredictionPipelineOptions), "the window holds the caller's options instead of a copy");

                if (yearBuiltPredictionsOptionsWindow.TryReadOptions(false, out YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_Exported, out string? message_Export) && yearBuiltPredictionPipelineOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(yearBuiltPredictionPipelineOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (YearBuiltPredictionsOptionsWindow.TryReadSettings(json, out string? message_Read) is not YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's - and OK writes the
                // run shape the file already carries, so the two serialize alike.
                YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                yearBuiltPredictionsOptionsWindow_Imported.SetControls(yearBuiltPredictionPipelineOptions_Imported);

                if (yearBuiltPredictionsOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    yearBuiltPredictionPipelineOptions_Read = yearBuiltPredictionsOptionsWindow_Imported.YearBuiltPredictionPipelineOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(yearBuiltPredictionPipelineOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // A blank scratch box exports as no directory, the file fills a box showing the default, and
                // OK substitutes the default on read-back - exactly as when typed.
                yearBuiltPredictionsOptionsWindow.SetControls(yearBuiltPredictionPipelineOptions);
                yearBuiltPredictionsOptionsWindow.TextBoxControl_ScratchDirectory.Value = " ";
                if (yearBuiltPredictionsOptionsWindow.TryReadOptions(false, out YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_Blank, out string? message_Blank) && yearBuiltPredictionPipelineOptions_Blank is not null)
                {
                    blankExported = yearBuiltPredictionPipelineOptions_Blank.ScratchDirectory;
                }
                else
                {
                    Expect(false, $"a blank scratch box was refused by the export: {message_Blank}");
                }

                if (YearBuiltPredictionsOptionsWindow.TryReadSettings(Core.Convert.ToSystem_String(new YearBuiltPredictionPipelineOptions() { ScratchDirectory = null, CountyIds = [1001] }), out string? message_Blank_Read) is not YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions_Blank_Imported)
                {
                    Expect(false, $"the blank-scratch file was refused: {message_Blank_Read}");
                    return;
                }

                YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow_Blank = new(null, OpenBuildingImportOptionsWindowCounties(5));
                yearBuiltPredictionsOptionsWindow_Blank.SetControls(yearBuiltPredictionPipelineOptions_Blank_Imported);
                string? boxAfterImport = yearBuiltPredictionsOptionsWindow_Blank.TextBoxControl_ScratchDirectory.Value;
                if (yearBuiltPredictionsOptionsWindow_Blank.TryGetOptions(out string? message_Blank_Ok))
                {
                    blankAfterOk = yearBuiltPredictionsOptionsWindow_Blank.YearBuiltPredictionPipelineOptions.ScratchDirectory;
                    Expect(boxAfterImport == "scratch", $"a file with no scratch directory showed {boxAfterImport ?? "nothing"} in the box");
                }
                else
                {
                    Expect(false, $"the blank-scratch import was refused by OK: {message_Blank_Ok}");
                }

                // No county: written as no county list, refused by OK - the run would stop with no county named.
                yearBuiltPredictionsOptionsWindow.SetControls(yearBuiltPredictionPipelineOptions);
                yearBuiltPredictionsOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => false);
                Expect(yearBuiltPredictionsOptionsWindow.TryReadOptions(false, out YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_NoCounty, out string? message_NoCounty) && yearBuiltPredictionPipelineOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(yearBuiltPredictionPipelineOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!yearBuiltPredictionsOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");
                refusedWithoutCounty = !yearBuiltPredictionsOptionsWindow.TryGetOptions(out _);
            });

            void Expect(bool condition, string failure)
            {
                if (!condition)
                {
                    failures.Add(failure);
                }
            }

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.NotNull(yearBuiltPredictionPipelineOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the scratch directory, the interpreter, the concurrency, the cleanup box,
            // the county subset - and the run shape OK always writes.
            Assert.Equal("yb_scratch", yearBuiltPredictionPipelineOptions_Read!.ScratchDirectory);
            Assert.Equal("C:/python/python.exe", yearBuiltPredictionPipelineOptions_Read!.PythonPath);
            Assert.Equal(4, yearBuiltPredictionPipelineOptions_Read!.MaxConcurrentRequests);
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.CleanScratchDirectory);
            Assert.NotNull(yearBuiltPredictionPipelineOptions_Read!.CountyIds);
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.CountyIds!.SetEquals([1001, 1003]));
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.ExportImages);
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.RunPrediction);
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.Score);
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.UpdateDetections);
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.UpdateYearBuiltData);
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.UpdatePredictedYearBuilt);
            Assert.True(yearBuiltPredictionPipelineOptions_Read!.Resume);

            // The blank box: no directory in the file, the default shown, the default written by OK.
            Assert.Null(blankExported);
            Assert.Equal("scratch", blankAfterOk);

            Assert.True(refusedWithoutCounty);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(4, yearBuiltPredictionPipelineOptions.MaxConcurrentRequests);
            Assert.NotNull(yearBuiltPredictionPipelineOptions.CountyIds);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the Year Built window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#74): an unfinished window still exports - a concurrency of zero, no county - while a concurrency box that does not hold a whole number is refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                YearBuiltPredictionPipelineOptions yearBuiltPredictionPipelineOptions = new()
                {
                    CountyIds = [1001],
                    MaxConcurrentRequests = 4,
                };

                YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow = new(yearBuiltPredictionPipelineOptions, OpenBuildingImportOptionsWindowCounties(5));

                // A concurrency box that does not hold a whole number could not be written back as typed; it is
                // also the rule OK checks first, so no county is needed for this one.
                yearBuiltPredictionsOptionsWindow.TextBoxControl_MaxConcurrentRequests.Value = "half";
                Expect(!yearBuiltPredictionsOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("Max concurrent requests", StringComparison.Ordinal), "a concurrency that is not a number was exported");

                // A concurrency of zero is a whole number: the export takes it and OK's range rule refuses it.
                yearBuiltPredictionsOptionsWindow.SetControls(yearBuiltPredictionPipelineOptions);
                yearBuiltPredictionsOptionsWindow.TextBoxControl_MaxConcurrentRequests.Value = "0";
                Expect(yearBuiltPredictionsOptionsWindow.TryReadOptions(false, out YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_Zero, out string? message_Zero) && yearBuiltPredictionPipelineOptions_Zero is not null, $"a concurrency of zero was refused by the export: {message_Zero}");
                Expect(!yearBuiltPredictionsOptionsWindow.TryGetOptions(out string? message_Zero_Ok) && message_Zero_Ok is not null && message_Zero_Ok.Contains("Max concurrent requests", StringComparison.Ordinal), "a concurrency of zero passed OK");

                // No county: written as no county list, refused by OK - and the refused reads wrote nothing.
                yearBuiltPredictionsOptionsWindow.SetControls(yearBuiltPredictionPipelineOptions);
                yearBuiltPredictionsOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => false);
                Expect(yearBuiltPredictionsOptionsWindow.TryReadOptions(false, out YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_NoCounty, out string? message_NoCounty) && yearBuiltPredictionPipelineOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(yearBuiltPredictionPipelineOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!yearBuiltPredictionsOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                Expect(yearBuiltPredictionsOptionsWindow.YearBuiltPredictionPipelineOptions.MaxConcurrentRequests == 4, "a refused read wrote the concurrency");
                Expect(yearBuiltPredictionsOptionsWindow.YearBuiltPredictionPipelineOptions.CountyIds is HashSet<int> countyIds_Held && countyIds_Held.SetEquals([1001]), "a refused read wrote the county list");
            });

            void Expect(bool condition, string failure)
            {
                if (!condition)
                {
                    failures.Add(failure);
                }
            }

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

            string? json_Self = Core.Convert.ToSystem_String(new YearBuiltPredictionPipelineOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_Self = YearBuiltPredictionsOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_Foreign = YearBuiltPredictionsOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_NotJson = YearBuiltPredictionsOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            YearBuiltPredictionPipelineOptions? yearBuiltPredictionPipelineOptions_Several = YearBuiltPredictionsOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(yearBuiltPredictionPipelineOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(yearBuiltPredictionPipelineOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(yearBuiltPredictionPipelineOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(yearBuiltPredictionPipelineOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the Year Built window without the host (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#74; <c>Coding - WPF Deferred Loading.md</c> section 7): the two settings buttons share the pinned footer with OK and Cancel without running into them at the default and the minimum size, the whole form lies inside the window at the default size, and the scroll above the footer - this window's designed answer to a small screen - engages at the minimum size rather than cropping anything off the bottom.
        /// <para>The shared walk runs over the whole form at the default size and over the pinned footer at the minimum size: content below the fold of a <see cref="ScrollViewer"/> is not clipped, it is scrolled, so flagging it as outside the window would report the design as a defect - the minimum-size numbers below prove the scroll is what carries it.</para>
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                YearBuiltPredictionsOptionsWindow yearBuiltPredictionsOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (yearBuiltPredictionsOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(YearBuiltPredictionsOptionsWindow)}: no content");
                    return;
                }

                yearBuiltPredictionsOptionsWindow.Content = null;

                ScrollViewer? scrollViewer = YearBuiltPredictionsOptionsWindowScrollViewer(root);
                if (scrollViewer is null)
                {
                    failures.Add($"{nameof(YearBuiltPredictionsOptionsWindow)}: the form is not inside a ScrollViewer");
                    return;
                }

                if (yearBuiltPredictionsOptionsWindow.Button_OK.Parent is not Panel footerPanel || footerPanel.Parent is not FrameworkElement footer)
                {
                    failures.Add($"{nameof(YearBuiltPredictionsOptionsWindow)}: the footer with the buttons could not be found");
                    return;
                }

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(yearBuiltPredictionsOptionsWindow.Width - 16, yearBuiltPredictionsOptionsWindow.Height - 39);
                Size size_Min = new(yearBuiltPredictionsOptionsWindow.MinWidth - 16, yearBuiltPredictionsOptionsWindow.MinHeight - 39);

                // The default size: the whole form lies inside the window, so the shared walk covers it - the
                // rows below the ScrollViewer's fold are reachable by scrolling, which the minimum-size check
                // below proves for the case that needs it.
                root.Measure(size_Default);
                root.Arrange(new Rect(size_Default));
                root.UpdateLayout();

                SolarRadiationOptionsWindowLayoutFailures(nameof(YearBuiltPredictionsOptionsWindow), size_Default, root, root, failures);
                OpenBuildingImportOptionsWindowCaptionFailures(nameof(YearBuiltPredictionsOptionsWindow), size_Default, root, root, null, null, failures);
                YearBuiltPredictionsOptionsWindowFooterFailures(nameof(YearBuiltPredictionsOptionsWindow), size_Default, root, footer, yearBuiltPredictionsOptionsWindow, failures);

                // The minimum size: only the pinned footer is checked against the window - the form above it
                // scrolls, which the two numbers below prove rather than assume.
                root.Measure(size_Min);
                root.Arrange(new Rect(size_Min));
                root.UpdateLayout();

                SolarRadiationOptionsWindowLayoutFailures(nameof(YearBuiltPredictionsOptionsWindow), size_Min, root, footer, failures);
                OpenBuildingImportOptionsWindowCaptionFailures(nameof(YearBuiltPredictionsOptionsWindow), size_Min, root, footer, null, null, failures);
                YearBuiltPredictionsOptionsWindowFooterFailures(nameof(YearBuiltPredictionsOptionsWindow), size_Min, root, footer, yearBuiltPredictionsOptionsWindow, failures);

                double extent_Min = scrollViewer.ExtentHeight;
                double viewport_Min = scrollViewer.ViewportHeight;
                if (extent_Min <= viewport_Min)
                {
                    failures.Add($"{nameof(YearBuiltPredictionsOptionsWindow)} at {size_Min}: the form is {extent_Min:F0} px tall in a {viewport_Min:F0} px viewport - the scroll never engages, so nothing proves the bottom of the form is reachable");
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Checks the pinned footer at one size: every part of it sized and inside the window, and the Export button ending before OK starts - the overlap the shared walk cannot see, because two controls may each lie inside the window and still run into each other.
        /// </summary>
        /// <param name="typeName">The window type name, prefixed to every failure.</param>
        /// <param name="size">The client size the content is laid out at.</param>
        /// <param name="root">The detached content of the window.</param>
        /// <param name="footer">The footer grid holding the buttons.</param>
        /// <param name="window">The window the buttons belong to.</param>
        /// <param name="failures">The list the failures are added to.</param>
        private static void YearBuiltPredictionsOptionsWindowFooterFailures(string typeName, Size size, FrameworkElement root, FrameworkElement footer, YearBuiltPredictionsOptionsWindow window, List<string> failures)
        {
            SolarRadiationOptionsWindowLayoutFailures(typeName, size, root, footer, failures);

            Rect rect_Export = window.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(window.Button_ExportSettings.RenderSize));
            Rect rect_OK = window.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(window.Button_OK.RenderSize));

            if (rect_Export.Right > rect_OK.Left + 0.5)
            {
                failures.Add($"{typeName} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
            }
        }

        /// <summary>
        /// Finds the scroll viewer of the form by walking its visual tree, because it is the unnamed container between the window's content and the rows.
        /// </summary>
        /// <param name="dependencyObject">The element to search, starting at the content.</param>
        /// <returns>The first scroll viewer found below it, or null when there is none.</returns>
        private static ScrollViewer? YearBuiltPredictionsOptionsWindowScrollViewer(DependencyObject dependencyObject)
        {
            if (dependencyObject is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }

            int count = VisualTreeHelper.GetChildrenCount(dependencyObject);
            for (int i = 0; i < count; i++)
            {
                ScrollViewer? scrollViewer_Temp = YearBuiltPredictionsOptionsWindowScrollViewer(VisualTreeHelper.GetChild(dependencyObject, i));
                if (scrollViewer_Temp is not null)
                {
                    return scrollViewer_Temp;
                }
            }

            return null;
        }
    }
}
