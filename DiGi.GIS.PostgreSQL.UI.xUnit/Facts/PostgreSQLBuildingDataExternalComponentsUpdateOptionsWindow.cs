using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using System;
using System.Collections.Generic;
using System.Threading;

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
    }
}
