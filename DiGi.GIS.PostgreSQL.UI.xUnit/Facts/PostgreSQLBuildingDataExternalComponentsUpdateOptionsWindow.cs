using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using Size = System.Windows.Size;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the external components area options window can be built, that it works on a copy of the options it is given, and that a county set it was not given stays a county set of nothing, all without a database or a running application.
        /// <para>The window is the only way a run can be scoped, and a run left unscoped reads every stored building model in the country. A window whose markup failed to parse, or whose constructor threw on the county list, would not be found until someone opened it to scope a run and could not.</para>
        /// <para>What the constructor does is what this exercises: it parses the markup, wires the item naming of the county list, fills and sorts the counties, and decides the selection - the given county set when the options carry one, the default of every county when they do not. A multi-part county is included because two of its pieces share a code and a name and are told apart only by the identifier the naming callback appends.</para>
        /// <para><b>It does not check what the window looks like.</b> A window is laid out by the handle it gets when it is shown, so measuring one that is never shown reports nothing, and showing one during a test run would put a dialog on screen. The controls are private to the window, so the selection they end up holding cannot be read from here either - that the default selection is the whole country, and that the dialog behaves correctly on screen, is something a person has to look at once.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Construction()
        {
            Exception? exception = null;
            bool countiesCarried = false;
            bool commandTimeoutCarried = false;
            bool copied = false;
            bool countySetKeptAbsent = false;

            PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions = new()
            {
                CountyIds = [4816],
                CommandTimeout = 600
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

                    PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow = new(postgreSQLBuildingDataExternalComponentsUpdateOptions, administrativeAreal2DReferences);

                    PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions_Held = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.PostgreSQLBuildingDataExternalComponentsUpdateOptions;

                    // Until OK is pressed the window holds what it was given.
                    countiesCarried = postgreSQLBuildingDataExternalComponentsUpdateOptions_Held.CountyIds is HashSet<int> countyIds && countyIds.Count == 1 && countyIds.Contains(4816);
                    commandTimeoutCarried = postgreSQLBuildingDataExternalComponentsUpdateOptions_Held.CommandTimeout == 600;

                    // A cancelled dialog has to leave the caller's options alone, which only holds if the window
                    // took a copy rather than a reference.
                    copied = !ReferenceEquals(postgreSQLBuildingDataExternalComponentsUpdateOptions, postgreSQLBuildingDataExternalComponentsUpdateOptions_Held);
                    postgreSQLBuildingDataExternalComponentsUpdateOptions_Held.CommandTimeout = 30;

                    // No county set in the options is the "every county" scope: the window selects all the counties
                    // it shows, but the options it holds stay as they were opened with until OK is pressed - so the
                    // held county set must still be absent here, and that is what keeps a cancelled dialog from
                    // turning a scoped run into a national one.
                    PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions_Default = new();

                    PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Default = new(postgreSQLBuildingDataExternalComponentsUpdateOptions_Default, administrativeAreal2DReferences);

                    countySetKeptAbsent = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Default.PostgreSQLBuildingDataExternalComponentsUpdateOptions.CountyIds is null;
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
            Assert.True(commandTimeoutCarried);
            Assert.True(copied);
            Assert.True(countySetKeptAbsent);

            // The caller's own instance is untouched by the edit made to the window's copy above.
            Assert.Equal(600, postgreSQLBuildingDataExternalComponentsUpdateOptions.CommandTimeout);
        }

        /// <summary>
        /// Verifies that the party walls box of the external components area options window starts from the options it is given and that OK writes the box back, so the dialog can switch the party wall measurement off for a run that only needs the other columns (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#54).
        /// <para>Party walls are most of the cost of a national run: with them every batch also reads the outlines and the stored models of its neighbours. A box that did not reach the options would leave the run measuring them whatever the person chose, and a box that started unchecked for a default run would silently drop them.</para>
        /// <para>The box is found by name and OK is pressed by raising its click, with no dialog shown. The handler copies the controls into the options before it sets the dialog result, and setting that result on a window that was never shown throws, so the exception is expected and the options are read after it; a window that refused the input would show a message box and hang here instead, which is what keeps this from passing by accident.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_PartyWalls()
        {
            Exception? exception = null;
            bool checkedFromDefault = false;
            bool uncheckedFromOptions = false;
            bool switchedOff = false;
            bool switchedOn = false;

            Thread thread = new(() =>
            {
                try
                {
                    List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                    [
                        new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County }
                    ];

                    // A default run measures party walls, so the box starts checked.
                    PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Default = new(new PostgreSQLBuildingDataExternalComponentsUpdateOptions(), administrativeAreal2DReferences);
                    checkedFromDefault = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Default.FindName("CheckBox_PartyWalls") is System.Windows.Controls.CheckBox checkBox_Default && checkBox_Default.IsChecked == true;

                    // A run configured without them opens unchecked.
                    PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow = new(new PostgreSQLBuildingDataExternalComponentsUpdateOptions() { PartyWalls = false }, administrativeAreal2DReferences);
                    System.Windows.Controls.CheckBox? checkBox = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.FindName("CheckBox_PartyWalls") as System.Windows.Controls.CheckBox;
                    uncheckedFromOptions = checkBox is not null && checkBox.IsChecked == false;

                    // OK writes the box back, checked or not.
                    if (checkBox is not null)
                    {
                        switchedOn = Press(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow, checkBox, true);
                        switchedOff = Press(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow, checkBox, false);
                    }
                }
                catch (Exception exception_Temp)
                {
                    exception = exception_Temp;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "The window waited on a message box - its input was refused.");

            Assert.Null(exception);
            Assert.True(checkedFromDefault);
            Assert.True(uncheckedFromOptions);
            Assert.True(switchedOn);
            Assert.True(switchedOff);
        }

        private static bool Press(PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow window, System.Windows.Controls.CheckBox checkBox, bool partyWalls)
        {
            checkBox.IsChecked = partyWalls;

            if (window.FindName("Button_OK") is not System.Windows.Controls.Button button)
            {
                return false;
            }

            try
            {
                button.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            }
            catch (InvalidOperationException)
            {
                // DialogResult cannot be set on a window that was never shown; the options are written before that line.
            }

            return window.PostgreSQLBuildingDataExternalComponentsUpdateOptions.PartyWalls == partyWalls;
        }

        /// <summary>
        /// Verifies that the parallel batches box of the external components area options window starts from the options it is given - 0, the automatic default, as 0 and a configured degree as itself - and that OK writes the value back, so a run started from the dialog decides how many batches of a county are in flight (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#54).
        /// <para>The degree decides the speed and the memory of a run: 0 takes half of the logical processors and 1 is the sequential run of the deployed build, while the memory of the run grows with the degree times the batch size. A box that did not reach the options would leave every run at whatever the options were opened with, and the dialog would appear to offer a setting it does not have.</para>
        /// <para>The box is found by name and OK is pressed by raising its click, with no dialog shown. The handler copies the controls into the options before it sets the dialog result, and setting that result on a window that was never shown throws, so the exception is expected and the options are read after it; a box holding something the handler refuses would open a message box and hang here instead, which is what keeps this from passing by accident. The counties are left selected, so the handler never takes the refusal path.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_MaxDegreeOfParallelism()
        {
            Exception? exception = null;
            bool shownFromDefault = false;
            bool shownFromOptions = false;
            bool wroteZero = false;
            bool wroteOne = false;
            bool wroteEight = false;

            Thread thread = new(() =>
            {
                try
                {
                    List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                    [
                        new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County }
                    ];

                    // The default is 0 - half of the processors - and the box shows it as itself.
                    PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Default = new(new PostgreSQLBuildingDataExternalComponentsUpdateOptions(), administrativeAreal2DReferences);
                    shownFromDefault = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Default.FindName("TextBoxControl_MaxDegreeOfParallelism") is DiGi.UI.WPF.Controls.TextBoxControl textBox_Default && textBox_Default.Value == 0.ToString(System.Globalization.CultureInfo.CurrentCulture);

                    // A run configured for four batches in flight opens with four.
                    PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow = new(new PostgreSQLBuildingDataExternalComponentsUpdateOptions() { MaxDegreeOfParallelism = 4 }, administrativeAreal2DReferences);
                    DiGi.UI.WPF.Controls.TextBoxControl? textBox = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.FindName("TextBoxControl_MaxDegreeOfParallelism") as DiGi.UI.WPF.Controls.TextBoxControl;
                    shownFromOptions = textBox is not null && textBox.Value == 4.ToString(System.Globalization.CultureInfo.CurrentCulture);

                    // OK writes the value back, the automatic 0, the sequential 1 and a degree above 1 alike.
                    if (textBox is not null)
                    {
                        wroteZero = Press(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow, textBox, 0);
                        wroteOne = Press(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow, textBox, 1);
                        wroteEight = Press(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow, textBox, 8);
                    }
                }
                catch (Exception exception_Temp)
                {
                    exception = exception_Temp;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "The window waited on a message box - its input was refused.");

            Assert.Null(exception);
            Assert.True(shownFromDefault);
            Assert.True(shownFromOptions);
            Assert.True(wroteZero);
            Assert.True(wroteOne);
            Assert.True(wroteEight);
        }

        private static bool Press(PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow window, DiGi.UI.WPF.Controls.TextBoxControl textBoxControl, int maxDegreeOfParallelism)
        {
            textBoxControl.Value = maxDegreeOfParallelism.ToString(System.Globalization.CultureInfo.CurrentCulture);

            if (window.FindName("Button_OK") is not System.Windows.Controls.Button button)
            {
                return false;
            }

            try
            {
                button.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            }
            catch (InvalidOperationException)
            {
                // DialogResult cannot be set on a window that was never shown; the options are written before that line.
            }

            return window.PostgreSQLBuildingDataExternalComponentsUpdateOptions.MaxDegreeOfParallelism == maxDegreeOfParallelism;
        }

        /// <summary>
        /// Verifies that the settings of the external components window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#68): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>Two rules of this window are asserted: an export always carries a county selection (the empty one is refused, not written), and a file with no county list - which this window can no longer produce, but an older or hand-written file can still hold - opens with **every** county selected, the way the window always shows the unscoped run. The member with no control, the party wall distance, travels in the file as the copy held it.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions = new()
            {
                CountyIds = [1001, 1003],
                CommandTimeout = 300,
                BatchSize = 256,
                SkipCompleted = true,
                PartyWalls = false,
                MaxDegreeOfParallelism = 4,
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Read = null;
            int selectedFromNoScopeFile = -1;
            int listedCount = -1;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow = new(postgreSQLBuildingDataExternalComponentsUpdateOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(postgreSQLBuildingDataExternalComponentsUpdateOptions, postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.PostgreSQLBuildingDataExternalComponentsUpdateOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Exported, out string? message_Export) && postgreSQLBuildingDataExternalComponentsUpdateOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLBuildingDataExternalComponentsUpdateOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: the select-all default of the constructor has to give way to exactly
                // what the file names.
                PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Imported.SetControls(postgreSQLBuildingDataExternalComponentsUpdateOptions_Imported);

                if (postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLBuildingDataExternalComponentsUpdateOptions_Read = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Imported.PostgreSQLBuildingDataExternalComponentsUpdateOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLBuildingDataExternalComponentsUpdateOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // A file with no county list - built directly, because this window can no longer export one -
                // has to open with every county selected: the unscoped run the window always shows.
                PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions_NoScope = new() { CountyIds = null, CommandTimeout = 600 };
                string? json_NoScope = Core.Convert.ToSystem_String(postgreSQLBuildingDataExternalComponentsUpdateOptions_NoScope);
                if (PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadSettings(json_NoScope, out string? message_NoScope) is not PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions_NoScope_Imported)
                {
                    Expect(false, $"the no-scope file was refused: {message_NoScope}");
                    return;
                }

                PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_NoScope = new(postgreSQLBuildingDataExternalComponentsUpdateOptions, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_NoScope.SetControls(postgreSQLBuildingDataExternalComponentsUpdateOptions_NoScope_Imported);

                List<AdministrativeAreal2DReference>? administrativeAreal2DReferences = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_NoScope.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>();
                listedCount = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_NoScope.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>(false)?.Count ?? 0;
                selectedFromNoScopeFile = administrativeAreal2DReferences?.Count ?? 0;
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
            Assert.NotNull(postgreSQLBuildingDataExternalComponentsUpdateOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the counties, the three numbers, both boxes - and the party wall distance,
            // which no control shows.
            Assert.NotNull(postgreSQLBuildingDataExternalComponentsUpdateOptions_Read!.CountyIds);
            Assert.True(postgreSQLBuildingDataExternalComponentsUpdateOptions_Read!.CountyIds!.SetEquals([1001, 1003]));
            Assert.Equal(300, postgreSQLBuildingDataExternalComponentsUpdateOptions_Read!.CommandTimeout);
            Assert.Equal(256, postgreSQLBuildingDataExternalComponentsUpdateOptions_Read!.BatchSize);
            Assert.True(postgreSQLBuildingDataExternalComponentsUpdateOptions_Read!.SkipCompleted);
            Assert.False(postgreSQLBuildingDataExternalComponentsUpdateOptions_Read!.PartyWalls);
            Assert.Equal(4, postgreSQLBuildingDataExternalComponentsUpdateOptions_Read!.MaxDegreeOfParallelism);

            // The file with no county list selected every county of the list it was given.
            Assert.Equal(listedCount, selectedFromNoScopeFile);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(300, postgreSQLBuildingDataExternalComponentsUpdateOptions.CommandTimeout);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the external components window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#68): an empty county selection is refused by Export with the reason OK gives - it would come back as the national scope - while a range value still exports and a box that does not hold a whole number is refused on both paths, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(5));

                // An empty county selection cannot round-trip: this window shows "no county set" as every
                // county selected, so Export refuses it with the message OK gives and writes nothing.
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => false);
                Expect(!postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_Empty) && message_Empty is not null && message_Empty.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection was exported");
                Expect(!postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryGetOptions(out string? message_Empty_Ok) && message_Empty_Ok is not null && message_Empty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                // Options with no county list open with every county selected - the window's own default.
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataExternalComponentsUpdateOptions());
                int selected = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>()?.Count ?? 0;
                int listed = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>(false)?.Count ?? 0;
                Expect(listed > 0 && selected == listed, $"no county list selected {selected} of {listed} counties");

                // A box that does not hold a whole number is refused on both paths.
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TextBoxControl_BatchSize.Value = "half";
                Expect(!postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_Batch) && message_Batch is not null && message_Batch.Contains("Batch size", StringComparison.Ordinal), "a batch size that is not a number was exported");
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataExternalComponentsUpdateOptions());

                // A batch size of zero is a whole number: it exports, and OK's range rule refuses it on read-back.
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TextBoxControl_BatchSize.Value = "0";
                Expect(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Batch, out string? message_BatchZero) && postgreSQLBuildingDataExternalComponentsUpdateOptions_Batch is not null, $"a batch size of zero was refused by the export: {message_BatchZero}");
                Expect(!postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryGetOptions(out string? message_BatchZero_Ok) && message_BatchZero_Ok is not null && message_BatchZero_Ok.Contains("Batch size", StringComparison.Ordinal), "a batch size of zero passed OK");

                // A negative timeout likewise.
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataExternalComponentsUpdateOptions());
                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TextBoxControl_CommandTimeout.Value = "-1";
                Expect(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Timeout, out string? message_Timeout) && postgreSQLBuildingDataExternalComponentsUpdateOptions_Timeout is not null, $"a negative timeout was refused by the export: {message_Timeout}");
                Expect(!postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryGetOptions(out string? message_Timeout_Ok) && message_Timeout_Ok is not null && message_Timeout_Ok.Contains("timeout", StringComparison.OrdinalIgnoreCase), "a negative timeout passed OK");

                // The refused reads wrote nothing: the options still hold the defaults they opened with.
                Expect(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.PostgreSQLBuildingDataExternalComponentsUpdateOptions.CommandTimeout == 600, "a refused read wrote the timeout");
                Expect(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.PostgreSQLBuildingDataExternalComponentsUpdateOptions.CountyIds is null, "a refused read wrote the county list");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLBuildingDataExternalComponentsUpdateOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Self = PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Foreign = PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_NotJson = PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Several = PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLBuildingDataExternalComponentsUpdateOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLBuildingDataExternalComponentsUpdateOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLBuildingDataExternalComponentsUpdateOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLBuildingDataExternalComponentsUpdateOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the external components window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#68; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow)}: no content");
                    return;
                }

                postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.Width - 16, postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.Height - 39);
                Size size_Min = new(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.MinWidth - 16, postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLBuildingDataExternalComponentsUpdateOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(PostgreSQLBuildingDataExternalComponentsUpdateOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
