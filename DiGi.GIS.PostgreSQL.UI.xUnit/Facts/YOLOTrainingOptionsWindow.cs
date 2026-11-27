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
        /// Verifies that the YOLO training options window can be built, that it holds a copy of the options it is given rather than the options themselves, and that it opens with the previous run's values rather than a scenario's defaults.
        /// <para>The dialog is handed the previous run's options every time the task is started, so a window that applied the scenario's defaults on opening would throw away the start weights and epochs chosen last time - and one that changed the instance it was given would do it even when the dialog is cancelled.</para>
        /// <para>Opened with no options, it must hold the scenario's defaults instead, including every step ticked: the runner reads an empty step list as every step. The two settings buttons of DiGi-Organization/DiGi.GIS.PostgreSQL.UI#76 are checked for presence and their 110 px here - the window's form scrolls, so the pinned footer is where a layout walk would add nothing the width check does not already say. It does not otherwise check what the window looks like - a window is laid out when it is shown, and showing one would put a dialog on screen.</para>
        /// </summary>
        [Fact]
        public void YOLOTrainingOptionsWindow_Construction()
        {
            Exception? exception = null;
            bool copied = false;
            bool previousKept = false;
            bool scenarioKept = false;
            bool defaultsApplied = false;
            bool buttonsPresent = false;

            YOLOTrainingRunOptions yOLOTrainingRunOptions = new()
            {
                StartWeightsPath = @"C:\YOLO\previous.pt",
                Epochs = 12,
                RunName = "train9",
                ProjectDirectory = @"C:\YOLO\runs",
                Steps = [YOLOTrainingStep.Train],
                DatasetOptions = new YOLOTrainingDatasetOptions()
                {
                    CountyIds = [22138],
                    OutputDirectory = @"C:\YOLO\dataset",
                    Confidence = 0.2
                }
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

                    YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow = new(YOLOTrainingScenario.Fresh, yOLOTrainingRunOptions, administrativeAreal2DReferences, null);

                    YOLOTrainingRunOptions yOLOTrainingRunOptions_Held = yOLOTrainingOptionsWindow.YOLOTrainingRunOptions;

                    copied = !ReferenceEquals(yOLOTrainingRunOptions, yOLOTrainingRunOptions_Held);
                    previousKept = yOLOTrainingRunOptions_Held.StartWeightsPath == @"C:\YOLO\previous.pt"
                        && yOLOTrainingRunOptions_Held.Epochs == 12
                        && yOLOTrainingRunOptions_Held.RunName == "train9"
                        && yOLOTrainingRunOptions_Held.DatasetOptions?.Confidence == 0.2
                        && yOLOTrainingRunOptions_Held.DatasetOptions.CountyIds is HashSet<int> countyIds && countyIds.Count == 1 && countyIds.Contains(22138);
                    scenarioKept = yOLOTrainingOptionsWindow.YOLOTrainingScenario == YOLOTrainingScenario.Fresh
                        && ((System.Windows.Controls.ComboBox)yOLOTrainingOptionsWindow.FindName("ComboBox_Scenario")!).SelectedIndex == 1;

                    yOLOTrainingRunOptions_Held.Epochs = 99;

                    YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow_Default = new(YOLOTrainingScenario.Retrain, null, administrativeAreal2DReferences, null);
                    YOLOTrainingRunOptions yOLOTrainingRunOptions_Default = yOLOTrainingOptionsWindow_Default.YOLOTrainingRunOptions;
                    defaultsApplied = yOLOTrainingRunOptions_Default.Epochs == 150
                        && yOLOTrainingRunOptions_Default.DatasetOptions?.Resume == true
                        && yOLOTrainingRunOptions_Default.Steps?.Count == 5
                        && ((System.Windows.Controls.CheckBox)yOLOTrainingOptionsWindow_Default.FindName("CheckBox_Dataset")!).IsChecked == true
                        && ((System.Windows.Controls.CheckBox)yOLOTrainingOptionsWindow_Default.FindName("CheckBox_Evaluate")!).IsChecked == true;

                    buttonsPresent = yOLOTrainingOptionsWindow.FindName("Button_ImportSettings") is System.Windows.Controls.Button button_Import
                        && button_Import.Width == 110
                        && yOLOTrainingOptionsWindow.FindName("Button_ExportSettings") is System.Windows.Controls.Button button_Export
                        && button_Export.Width == 110;
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
            Assert.True(copied);
            Assert.True(previousKept);
            Assert.True(scenarioKept);
            Assert.True(defaultsApplied);
            Assert.True(buttonsPresent);

            Assert.Equal(12, yOLOTrainingRunOptions.Epochs);
        }

        /// <summary>
        /// Verifies that closing the YOLO training options window with OK writes every control into the options it holds, applies the chosen scenario, and leaves the members it has no control for unchanged.
        /// <para>The scenario is switched from Re-train to Start from yolo26x.pt before OK, and the start weights are then typed over: the switch has to decide the dataset folder's resume flag - off, so an existing dataset is refused - while the typed start weights, not the scenario's default, are what runs. <c>CountOnly</c> is handed in on and must come back off, and the unticked steps must be left out of a list the runner would otherwise read as every step.</para>
        /// <para>The click is raised on a window that was never shown, so the handler runs to its final DialogResult assignment, which throws on a window that was never shown as a dialog - by then every assignment has been made. The inputs are valid on purpose: a refusal would put a message box on screen and block the run rather than fail it, so the refusals are left to the person who opens the dialog once.</para>
        /// </summary>
        [Fact]
        public void YOLOTrainingOptionsWindow_Ok()
        {
            Exception? exception = null;
            YOLOTrainingRunOptions? yOLOTrainingRunOptions_Written = null;
            YOLOTrainingScenario yOLOTrainingScenario_Written = YOLOTrainingScenario.Retrain;

            YOLOTrainingRunOptions yOLOTrainingRunOptions = new()
            {
                Steps = [],
                DatasetOptions = new YOLOTrainingDatasetOptions()
                {
                    CountyIds = [22138, 22139],
                    CountOnly = true,
                    Resume = true,
                    Confidence = 0.2,
                    HoldoutDenominator = 7
                }
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

                    YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow = new(YOLOTrainingScenario.Retrain, yOLOTrainingRunOptions, administrativeAreal2DReferences, null);

                    ((System.Windows.Controls.ComboBox)yOLOTrainingOptionsWindow.FindName("ComboBox_Scenario")!).SelectedIndex = 1;

                    void Set(string name, string value)
                    {
                        ((TextBoxControl)yOLOTrainingOptionsWindow.FindName(name)!).Value = value;
                    }

                    Set("TextBoxControl_DatasetDirectory", @"C:\YOLO\dataset_new");
                    Set("TextBoxControl_StartWeightsPath", @"C:\YOLO\typed.pt");
                    Set("TextBoxControl_Epochs", "250");
                    Set("TextBoxControl_Patience", "30");
                    Set("TextBoxControl_ImageSize", "800");
                    Set("TextBoxControl_Batch", "8");
                    Set("TextBoxControl_Seed", "3");
                    Set("TextBoxControl_Device", "0");
                    Set("TextBoxControl_RunName", "train10");
                    Set("TextBoxControl_ProjectDirectory", @"C:\YOLO\runs");
                    Set("TextBoxControl_PythonPath", @"C:\Python\python.exe");
                    Set("TextBoxControl_WorkingDirectory", " ");
                    Set("TextBoxControl_WeightsPaths", @"C:\YOLO\a.pt; ; C:\YOLO\b.pt");

                    ((System.Windows.Controls.CheckBox)yOLOTrainingOptionsWindow.FindName("CheckBox_Dataset")!).IsChecked = true;
                    ((System.Windows.Controls.CheckBox)yOLOTrainingOptionsWindow.FindName("CheckBox_LabelCheck")!).IsChecked = false;
                    ((System.Windows.Controls.CheckBox)yOLOTrainingOptionsWindow.FindName("CheckBox_Train")!).IsChecked = true;
                    ((System.Windows.Controls.CheckBox)yOLOTrainingOptionsWindow.FindName("CheckBox_Validate")!).IsChecked = false;
                    ((System.Windows.Controls.CheckBox)yOLOTrainingOptionsWindow.FindName("CheckBox_Evaluate")!).IsChecked = true;

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
                    yOLOTrainingScenario_Written = yOLOTrainingOptionsWindow.YOLOTrainingScenario;
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
            Assert.NotNull(yOLOTrainingRunOptions_Written);

            Assert.Equal(YOLOTrainingScenario.Fresh, yOLOTrainingScenario_Written);
            Assert.Equal(@"C:\YOLO\typed.pt", yOLOTrainingRunOptions_Written!.StartWeightsPath);
            Assert.Equal(250, yOLOTrainingRunOptions_Written.Epochs);
            Assert.Equal(30, yOLOTrainingRunOptions_Written.Patience);
            Assert.Equal(800, yOLOTrainingRunOptions_Written.ImageSize);
            Assert.Equal(8, yOLOTrainingRunOptions_Written.Batch);
            Assert.Equal(3, yOLOTrainingRunOptions_Written.Seed);
            Assert.Equal("0", yOLOTrainingRunOptions_Written.Device);
            Assert.Equal("train10", yOLOTrainingRunOptions_Written.RunName);
            Assert.Equal(@"C:\YOLO\runs", yOLOTrainingRunOptions_Written.ProjectDirectory);
            Assert.Equal(@"C:\Python\python.exe", yOLOTrainingRunOptions_Written.PythonPath);
            Assert.Null(yOLOTrainingRunOptions_Written.WorkingDirectory);
            Assert.Equal<IEnumerable<YOLOTrainingStep>>([YOLOTrainingStep.Dataset, YOLOTrainingStep.Train, YOLOTrainingStep.Evaluate], yOLOTrainingRunOptions_Written.Steps!);

            YOLOTrainingDatasetOptions? yOLOTrainingDatasetOptions_Written = yOLOTrainingRunOptions_Written.DatasetOptions;
            Assert.NotNull(yOLOTrainingDatasetOptions_Written);
            Assert.Equal(@"C:\YOLO\dataset_new", yOLOTrainingDatasetOptions_Written!.OutputDirectory);
            Assert.Equal<IEnumerable<int>>([22138, 22139], yOLOTrainingDatasetOptions_Written.CountyIds!);
            Assert.Equal(@"C:\Python\python.exe", yOLOTrainingDatasetOptions_Written.PythonPath);
            Assert.Null(yOLOTrainingDatasetOptions_Written.WorkingDirectory);
            Assert.Equal<IEnumerable<string>>([@"C:\YOLO\a.pt", @"C:\YOLO\b.pt"], yOLOTrainingDatasetOptions_Written.WeightsPaths!);
            Assert.False(yOLOTrainingDatasetOptions_Written.Resume);
            Assert.False(yOLOTrainingDatasetOptions_Written.CountOnly);

            // No control for these: they survive the click unchanged.
            Assert.Equal(0.2, yOLOTrainingDatasetOptions_Written.Confidence);
            Assert.Equal(7, yOLOTrainingDatasetOptions_Written.HoldoutDenominator);

            // The caller's own instance is untouched.
            Assert.True(yOLOTrainingRunOptions.DatasetOptions!.CountOnly);
            Assert.Empty(yOLOTrainingRunOptions.Steps!);
        }

        /// <summary>
        /// Verifies that the settings of the YOLO training window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#76): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>Both windows are opened with the same scenario and no options, so the members neither the file nor the controls carry are derived identically on both sides - which is what makes the two serializations equal. The scenario itself is asserted unchanged by an import in a window opened with the other scenario: it is not part of the file.</para>
        /// </summary>
        [Fact]
        public void YOLOTrainingOptionsWindow_SettingsRoundTrip()
        {
            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            YOLOTrainingRunOptions? yOLOTrainingRunOptions_Read = null;
            YOLOTrainingScenario scenarioAfterImport = YOLOTrainingScenario.Retrain;
            int scenarioIndexAfterImport = -1;

            Exception? exception = RunSTA(() =>
            {
                YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow = new(YOLOTrainingScenario.Retrain, null, OpenBuildingImportOptionsWindowCounties(5), null);

                yOLOTrainingOptionsWindow.TextBoxControl_Epochs.Value = "77";
                yOLOTrainingOptionsWindow.TextBoxControl_Batch.Value = "4";
                yOLOTrainingOptionsWindow.TextBoxControl_DatasetDirectory.Value = "C:/Temp/yolo-dataset-settings-fact";
                yOLOTrainingOptionsWindow.TextBoxControl_StartWeightsPath.Value = "C:/Temp/yolo-weights-fact.pt";
                yOLOTrainingOptionsWindow.TextBoxControl_RunName.Value = "yolo_settings_fact";
                yOLOTrainingOptionsWindow.TextBoxControl_ProjectDirectory.Value = "C:/Temp/yolo-runs-fact";
                yOLOTrainingOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => x.Id == 1001 || x.Id == 1003);

                if (yOLOTrainingOptionsWindow.TryReadOptions(false, out YOLOTrainingRunOptions? yOLOTrainingRunOptions_Exported, out string? message_Export) && yOLOTrainingRunOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(yOLOTrainingRunOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (YOLOTrainingOptionsWindow.TryReadSettings(json, out string? message_Read) is not YOLOTrainingRunOptions yOLOTrainingRunOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened with the same scenario and no options: what it ends up holding after OK's read can
                // only be the file's controls over the same defaults the exporting window started from.
                YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow_Imported = new(YOLOTrainingScenario.Retrain, null, OpenBuildingImportOptionsWindowCounties(5), null);
                yOLOTrainingOptionsWindow_Imported.SetControls(yOLOTrainingRunOptions_Imported);

                if (yOLOTrainingOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    yOLOTrainingRunOptions_Read = yOLOTrainingOptionsWindow_Imported.YOLOTrainingRunOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(yOLOTrainingRunOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // The scenario is not in the file: a window opened with the other scenario keeps it, whatever
                // the file carries.
                YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow_Fresh = new(YOLOTrainingScenario.Fresh, null, OpenBuildingImportOptionsWindowCounties(5), null);
                yOLOTrainingOptionsWindow_Fresh.SetControls(yOLOTrainingRunOptions_Imported);
                scenarioAfterImport = yOLOTrainingOptionsWindow_Fresh.YOLOTrainingScenario;
                scenarioIndexAfterImport = yOLOTrainingOptionsWindow_Fresh.ComboBox_Scenario.SelectedIndex;
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
            Assert.NotNull(yOLOTrainingRunOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the hyperparameters, the folders, the counties, the step list and the
            // resume switch.
            Assert.Equal(77, yOLOTrainingRunOptions_Read!.Epochs);
            Assert.Equal(4, yOLOTrainingRunOptions_Read!.Batch);
            Assert.Equal("C:/Temp/yolo-weights-fact.pt", yOLOTrainingRunOptions_Read!.StartWeightsPath);
            Assert.Equal("yolo_settings_fact", yOLOTrainingRunOptions_Read!.RunName);
            Assert.Equal("C:/Temp/yolo-runs-fact", yOLOTrainingRunOptions_Read!.ProjectDirectory);
            Assert.Equal("C:/Temp/yolo-dataset-settings-fact", yOLOTrainingRunOptions_Read!.DatasetOptions?.OutputDirectory);
            Assert.NotNull(yOLOTrainingRunOptions_Read!.DatasetOptions?.CountyIds);
            Assert.True(yOLOTrainingRunOptions_Read!.DatasetOptions!.CountyIds!.SetEquals([1001, 1003]));
            Assert.NotNull(yOLOTrainingRunOptions_Read!.Steps);
            Assert.Equal(5, yOLOTrainingRunOptions_Read!.Steps!.Count);
            Assert.False(yOLOTrainingRunOptions_Read!.ResumeTraining);

            // The scenario survived the import untouched - it is not part of the file.
            Assert.Equal(YOLOTrainingScenario.Fresh, scenarioAfterImport);
            Assert.Equal(1, scenarioIndexAfterImport);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the YOLO training window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#76): an unfinished window still exports - a zero epochs, no county, no dataset directory - while a box that does not hold its number, exclusion text that does not parse and an empty step list (which the runner would read as every step) are refused with the reasons OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void YOLOTrainingOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                YOLOTrainingRunOptions yOLOTrainingRunOptions = new() { Epochs = 42 };

                YOLOTrainingOptionsWindow yOLOTrainingOptionsWindow = new(YOLOTrainingScenario.Retrain, yOLOTrainingRunOptions, OpenBuildingImportOptionsWindowCounties(5), null);
                yOLOTrainingOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => true);

                // A hyperparameter that is not a whole number could not be written back as typed.
                yOLOTrainingOptionsWindow.TextBoxControl_Epochs.Value = "half";
                Expect(!yOLOTrainingOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("Epochs", StringComparison.Ordinal), "an epochs that is not a number was exported");

                // Epochs of zero is a whole number: it exports and OK's range rule refuses it - with a county
                // selected and the steps ticked, so the epochs rule is what answers.
                yOLOTrainingOptionsWindow.SetControls(new YOLOTrainingRunOptions());
                yOLOTrainingOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => true);
                yOLOTrainingOptionsWindow.TextBoxControl_Epochs.Value = "0";
                Expect(yOLOTrainingOptionsWindow.TryReadOptions(false, out YOLOTrainingRunOptions? yOLOTrainingRunOptions_Zero, out string? message_Zero) && yOLOTrainingRunOptions_Zero is not null, $"epochs of zero was refused by the export: {message_Zero}");
                Expect(!yOLOTrainingOptionsWindow.TryGetOptions(out string? message_Zero_Ok) && message_Zero_Ok is not null && message_Zero_Ok.Contains("Epochs", StringComparison.Ordinal), "epochs of zero passed OK");

                // No step ticked: refused on both paths - the runner reads an empty list as every step, so the
                // selection could not round-trip.
                yOLOTrainingOptionsWindow.SetControls(new YOLOTrainingRunOptions());
                foreach (System.Windows.Controls.CheckBox checkBox in new System.Windows.Controls.CheckBox[]
                {
                    yOLOTrainingOptionsWindow.CheckBox_Dataset,
                    yOLOTrainingOptionsWindow.CheckBox_LabelCheck,
                    yOLOTrainingOptionsWindow.CheckBox_Train,
                    yOLOTrainingOptionsWindow.CheckBox_Validate,
                    yOLOTrainingOptionsWindow.CheckBox_Evaluate,
                })
                {
                    checkBox.IsChecked = false;
                }

                Expect(!yOLOTrainingOptionsWindow.TryReadOptions(false, out _, out string? message_Steps) && message_Steps is not null && message_Steps.Contains("step", StringComparison.OrdinalIgnoreCase), "an empty step selection was exported");

                // Exclusion text that does not parse is refused with its reason, on both paths.
                yOLOTrainingOptionsWindow.SetControls(new YOLOTrainingRunOptions());
                yOLOTrainingOptionsWindow.TextBox_ExclusionBoundingBoxes.Text = "not a box";
                Expect(!yOLOTrainingOptionsWindow.TryReadOptions(false, out _, out string? message_Boxes) && message_Boxes is not null, "malformed exclusion boxes were exported");

                yOLOTrainingOptionsWindow.SetControls(new YOLOTrainingRunOptions());
                yOLOTrainingOptionsWindow.TextBoxControl_ExclusionBuffer.Value = "half";
                Expect(!yOLOTrainingOptionsWindow.TryReadOptions(false, out _, out string? message_Buffer) && message_Buffer is not null, "an exclusion buffer that is not a number was exported");

                // No county and no dataset directory: both written as nothing and refused by OK - and the
                // refused reads wrote nothing, the options still holding the epochs they were opened with.
                yOLOTrainingOptionsWindow.SetControls(new YOLOTrainingRunOptions());
                Expect(yOLOTrainingOptionsWindow.TryReadOptions(false, out YOLOTrainingRunOptions? yOLOTrainingRunOptions_Unfinished, out string? message_Unfinished) && yOLOTrainingRunOptions_Unfinished is not null, $"an unfinished window was refused by the export: {message_Unfinished}");
                Expect(yOLOTrainingRunOptions_Unfinished?.DatasetOptions?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!yOLOTrainingOptionsWindow.TryGetOptions(out string? message_Unfinished_Ok) && message_Unfinished_Ok is not null, "an unfinished window passed OK");

                Expect(yOLOTrainingOptionsWindow.YOLOTrainingRunOptions.Epochs == 42, "a refused read wrote the options");
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

            string? json_Self = Core.Convert.ToSystem_String(new YOLOTrainingRunOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            YOLOTrainingRunOptions? yOLOTrainingRunOptions_Self = YOLOTrainingOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            YOLOTrainingRunOptions? yOLOTrainingRunOptions_Foreign = YOLOTrainingOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            YOLOTrainingRunOptions? yOLOTrainingRunOptions_NotJson = YOLOTrainingOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            YOLOTrainingRunOptions? yOLOTrainingRunOptions_Several = YOLOTrainingOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(yOLOTrainingRunOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(yOLOTrainingRunOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(yOLOTrainingRunOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(yOLOTrainingRunOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }
    }
}
