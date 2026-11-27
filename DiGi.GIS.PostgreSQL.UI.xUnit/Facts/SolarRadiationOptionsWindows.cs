using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.Solar.Classes;
using DiGi.GIS.PostgreSQL.UI.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Size = System.Windows.Size;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the solar radiation solve options window builds without a database or a running application, fills its controls from the options it is given - an unset building limit as an empty box - the degree of parallelism as itself and its automatic default as 0 - and works on a copy of them (DiGi.GIS.PostgreSQL.UI#44, #45, #46).
        /// <para>The window is the only way a solve is scoped, and an unscoped solve is the whole country once per radius. A multi-part county is included because its pieces are told apart only by the identifier the naming callback appends; radius 0 because it is the isolated building, the reference every shortfall is measured against.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationSolveOptionsWindow_Construction()
        {
            PostgreSQLBuildingSolarRadiationSolveOptions postgreSQLBuildingSolarRadiationSolveOptions = new()
            {
                CountyIds = [22138, 22139],
                Radii = [100, 0, 50, 50, -5, double.NaN],
                SkipExisting = false,
                BatchSize = 25,
                TileSize = 125,
                ReceiverCountMax = 300,
                CasterTriangleCountMax = 64_000,
                CommandTimeout = 900,
                BuildingCountMax = 500,
                MaxDegreeOfParallelism = 6
            };

            List<double>? radii = null;
            List<AdministrativeAreal2DReference>? administrativeAreal2DReferences_Selected = null;
            bool? skipExisting = null;
            string? tileSize = null;
            string? buildingCountMax_Set = null;
            string? buildingCountMax_Unset = null;
            string? maxDegreeOfParallelism_Set = null;
            string? maxDegreeOfParallelism_Default = null;
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Held = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingSolarRadiationSolveOptionsWindow postgreSQLBuildingSolarRadiationSolveOptionsWindow = new(postgreSQLBuildingSolarRadiationSolveOptions, SolarRadiationOptionsWindowCounties());

                radii = postgreSQLBuildingSolarRadiationSolveOptionsWindow.ListBoxControl_Radii.GetItems<double>(selected: false);
                administrativeAreal2DReferences_Selected = postgreSQLBuildingSolarRadiationSolveOptionsWindow.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>();
                skipExisting = postgreSQLBuildingSolarRadiationSolveOptionsWindow.CheckBox_SkipExisting.IsChecked;
                tileSize = postgreSQLBuildingSolarRadiationSolveOptionsWindow.TextBoxControl_TileSize.Value;
                buildingCountMax_Set = postgreSQLBuildingSolarRadiationSolveOptionsWindow.TextBoxControl_BuildingCountMax.Value;
                maxDegreeOfParallelism_Set = postgreSQLBuildingSolarRadiationSolveOptionsWindow.TextBoxControl_MaxDegreeOfParallelism.Value;

                postgreSQLBuildingSolarRadiationSolveOptions_Held = postgreSQLBuildingSolarRadiationSolveOptionsWindow.PostgreSQLBuildingSolarRadiationSolveOptions;
                postgreSQLBuildingSolarRadiationSolveOptions_Held.CommandTimeout = 30;
                postgreSQLBuildingSolarRadiationSolveOptions_Held.BuildingCountMax = null;

                PostgreSQLBuildingSolarRadiationSolveOptionsWindow postgreSQLBuildingSolarRadiationSolveOptionsWindow_Default = new(null, SolarRadiationOptionsWindowCounties());
                buildingCountMax_Unset = postgreSQLBuildingSolarRadiationSolveOptionsWindow_Default.TextBoxControl_BuildingCountMax.Value;
                maxDegreeOfParallelism_Default = postgreSQLBuildingSolarRadiationSolveOptionsWindow_Default.TextBoxControl_MaxDegreeOfParallelism.Value;
            });

            Assert.Null(exception);

            // Sorted, de-duplicated, and stripped of what the solve would ignore anyway.
            Assert.Equal([0, 50, 100], radii);

            // Both pieces of the multi-part county are restored by identifier, and nothing else.
            Assert.NotNull(administrativeAreal2DReferences_Selected);
            Assert.Equal([22138, 22139], administrativeAreal2DReferences_Selected!.ConvertAll(x => x.Id));

            Assert.False(skipExisting);
            Assert.Equal(125.0.ToString(System.Globalization.CultureInfo.CurrentCulture), tileSize);

            // The building limit: a value as itself, no limit as an empty box rather than 0.
            Assert.Equal(500.ToString(System.Globalization.CultureInfo.CurrentCulture), buildingCountMax_Set);
            Assert.Equal(string.Empty, buildingCountMax_Unset);

            // The degree of parallelism: a value as itself, the automatic default as 0.
            Assert.Equal(6.ToString(System.Globalization.CultureInfo.CurrentCulture), maxDegreeOfParallelism_Set);
            Assert.Equal(0.ToString(System.Globalization.CultureInfo.CurrentCulture), maxDegreeOfParallelism_Default);

            // A cancelled dialog has to leave the caller's options alone, which only holds for a copy.
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_Held);
            Assert.False(ReferenceEquals(postgreSQLBuildingSolarRadiationSolveOptions, postgreSQLBuildingSolarRadiationSolveOptions_Held));
            Assert.Equal(900, postgreSQLBuildingSolarRadiationSolveOptions.CommandTimeout);
            Assert.Equal(500, postgreSQLBuildingSolarRadiationSolveOptions.BuildingCountMax);
        }

        /// <summary>
        /// Verifies that the solar radiation solve options window carries an edited radii list through the accept logic into the options it holds, refuses an empty list with a reason, and leaves the caller's options untouched (DiGi.GIS.PostgreSQL#158).
        /// <para>The dialog is the only place a run's radii are chosen, and it opens with the previous run's list, so a run scoped to one radius has to survive the real Remove and Add buttons and the accept logic. The accept logic is called directly rather than through the OK button: a DialogResult needs ShowDialog and a failed validation shows a modal box, either of which would hang a fact (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationSolveOptionsWindow_AcceptRadii()
        {
            PostgreSQLBuildingSolarRadiationSolveOptions postgreSQLBuildingSolarRadiationSolveOptions = new()
            {
                CountyIds = [55417],
                Radii = [0, 50, 100],
                SkipExisting = true,
                BatchSize = 25,
                TileSize = 125,
                ReceiverCountMax = 300,
                CasterTriangleCountMax = 64_000,
                CommandTimeout = 900,
                BuildingCountMax = 500,
                MaxDegreeOfParallelism = 6
            };

            List<double>? radii_Edited = null;
            bool? accepted = null;
            string? message_Accepted = null;
            bool? accepted_Refused = null;
            string? message_Refused = null;
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Accepted = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingSolarRadiationSolveOptionsWindow postgreSQLBuildingSolarRadiationSolveOptionsWindow = new(postgreSQLBuildingSolarRadiationSolveOptions, SolarRadiationOptionsWindowCounties());

                // The prefill of an earlier run, narrowed to one radius through the buttons the dialog offers:
                // 0 and 50 out, 200 added and taken out again, so both handlers are exercised.
                postgreSQLBuildingSolarRadiationSolveOptionsWindow.ListBoxControl_Radii.SelectItems<double>(x => x == 0 || x == 50);
                postgreSQLBuildingSolarRadiationSolveOptionsWindow.Button_RemoveRadii.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                postgreSQLBuildingSolarRadiationSolveOptionsWindow.TextBoxControl_Radius.Value = "200";
                postgreSQLBuildingSolarRadiationSolveOptionsWindow.Button_AddRadius.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                postgreSQLBuildingSolarRadiationSolveOptionsWindow.ListBoxControl_Radii.SelectItems<double>(x => x == 200);
                postgreSQLBuildingSolarRadiationSolveOptionsWindow.Button_RemoveRadii.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                radii_Edited = postgreSQLBuildingSolarRadiationSolveOptionsWindow.ListBoxControl_Radii.GetItems<double>(selected: false);

                accepted = postgreSQLBuildingSolarRadiationSolveOptionsWindow.TryAccept(out message_Accepted);
                postgreSQLBuildingSolarRadiationSolveOptions_Accepted = postgreSQLBuildingSolarRadiationSolveOptionsWindow.PostgreSQLBuildingSolarRadiationSolveOptions;

                // The last radius out: the dialog has to refuse, with a reason, and leave the accepted options alone.
                postgreSQLBuildingSolarRadiationSolveOptionsWindow.ListBoxControl_Radii.SelectItems<double>(x => x == 100);
                postgreSQLBuildingSolarRadiationSolveOptionsWindow.Button_RemoveRadii.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                accepted_Refused = postgreSQLBuildingSolarRadiationSolveOptionsWindow.TryAccept(out message_Refused);
            });

            Assert.Null(exception);

            // Edited through the real buttons: sorted, de-duplicated, one radius left.
            Assert.Equal([100], radii_Edited);

            // Accepted: the edit carried into the options, the selected county with it, no reason asked for.
            Assert.True(accepted);
            Assert.Null(message_Accepted);
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_Accepted);
            Assert.Equal([100], postgreSQLBuildingSolarRadiationSolveOptions_Accepted!.Radii);
            Assert.Contains(55417, postgreSQLBuildingSolarRadiationSolveOptions_Accepted!.CountyIds!);

            // The window works on a copy: the caller's options still hold what the dialog was opened with.
            Assert.Equal([0, 50, 100], postgreSQLBuildingSolarRadiationSolveOptions.Radii);

            // An empty radii list is refused with a reason and writes nothing over the accepted values.
            Assert.False(accepted_Refused);
            Assert.False(string.IsNullOrWhiteSpace(message_Refused));
            Assert.Equal([100], postgreSQLBuildingSolarRadiationSolveOptions_Accepted.Radii);
        }

        /// <summary>
        /// Verifies that the solve window's settings survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#59): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened without options fills from it so that reading its controls gives the same settings again.
        /// <para>The import side runs in a window constructed with no options at all, so every value asserted below can only have come from the file. The radii are given out of order and hold a duplicate and a negative value, which the window strips - the file therefore holds the ordered list the solve takes.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationSolveOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLBuildingSolarRadiationSolveOptions postgreSQLBuildingSolarRadiationSolveOptions = new()
            {
                CountyIds = [55417, 4816],
                Radii = [100, 0, 50, 50, -5],
                SkipExisting = false,
                BatchSize = 25,
                TileSize = 125,
                ReceiverCountMax = 300,
                CasterTriangleCountMax = 64_000,
                CommandTimeout = 900,
                BuildingCountMax = 500,
                MaxDegreeOfParallelism = 6
            };

            bool? exported = null;
            string? message_Export = null;
            string? json = null;
            string? message_Read = null;
            bool? accepted = null;
            string? message_Accepted = null;
            string? json_AfterImport = null;
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Imported = null;
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Read = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingSolarRadiationSolveOptionsWindow postgreSQLBuildingSolarRadiationSolveOptionsWindow = new(postgreSQLBuildingSolarRadiationSolveOptions, SolarRadiationOptionsWindowCounties());

                PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Exported = postgreSQLBuildingSolarRadiationSolveOptionsWindow.CreateOptions(false, out message_Export);
                exported = postgreSQLBuildingSolarRadiationSolveOptions_Exported is not null;

                json = Core.Convert.ToSystem_String(postgreSQLBuildingSolarRadiationSolveOptions_Exported);
                postgreSQLBuildingSolarRadiationSolveOptions_Imported = PostgreSQLBuildingSolarRadiationSolveOptionsWindow.TryReadSettings(json, out message_Read);
                if (postgreSQLBuildingSolarRadiationSolveOptions_Imported is null)
                {
                    return;
                }

                // Opened without options: what it reads out can only be the file's, not the window's own defaults.
                PostgreSQLBuildingSolarRadiationSolveOptionsWindow postgreSQLBuildingSolarRadiationSolveOptionsWindow_Imported = new(null, SolarRadiationOptionsWindowCounties());
                postgreSQLBuildingSolarRadiationSolveOptionsWindow_Imported.SetControls(postgreSQLBuildingSolarRadiationSolveOptions_Imported);

                postgreSQLBuildingSolarRadiationSolveOptions_Read = postgreSQLBuildingSolarRadiationSolveOptionsWindow_Imported.CreateOptions(true, out message_Accepted);
                accepted = postgreSQLBuildingSolarRadiationSolveOptions_Read is not null;
                json_AfterImport = Core.Convert.ToSystem_String(postgreSQLBuildingSolarRadiationSolveOptions_Read);
            });

            Assert.Null(exception);

            // Export: the controls read with no reason to refuse, into one file.
            Assert.True(exported);
            Assert.Null(message_Export);
            Assert.False(string.IsNullOrWhiteSpace(json));

            // Read back as this window's settings: exactly one object, no reason to refuse.
            Assert.Null(message_Read);
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_Imported);

            // Import: accepted as a complete window, so the same values are read out again.
            Assert.True(accepted);
            Assert.Null(message_Accepted);
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the counties by identifier, the radii ordered and stripped, the rest as typed.
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_Read!.CountyIds);
            Assert.Equal(2, postgreSQLBuildingSolarRadiationSolveOptions_Read!.CountyIds!.Count);
            Assert.Contains(4816, postgreSQLBuildingSolarRadiationSolveOptions_Read!.CountyIds!);
            Assert.Contains(55417, postgreSQLBuildingSolarRadiationSolveOptions_Read!.CountyIds!);
            Assert.Equal([0, 50, 100], postgreSQLBuildingSolarRadiationSolveOptions_Read!.Radii);
            Assert.False(postgreSQLBuildingSolarRadiationSolveOptions_Read!.SkipExisting);
            Assert.Equal(25, postgreSQLBuildingSolarRadiationSolveOptions_Read!.BatchSize);
            Assert.Equal(125d, postgreSQLBuildingSolarRadiationSolveOptions_Read!.TileSize);
            Assert.Equal(300, postgreSQLBuildingSolarRadiationSolveOptions_Read!.ReceiverCountMax);
            Assert.Equal(64_000, postgreSQLBuildingSolarRadiationSolveOptions_Read!.CasterTriangleCountMax);
            Assert.Equal(900, postgreSQLBuildingSolarRadiationSolveOptions_Read!.CommandTimeout);
            Assert.Equal(500, postgreSQLBuildingSolarRadiationSolveOptions_Read!.BuildingCountMax);
            Assert.Equal(6, postgreSQLBuildingSolarRadiationSolveOptions_Read!.MaxDegreeOfParallelism);
        }

        /// <summary>
        /// Verifies the two edges of the settings file of the solve window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#59): a window with no county selected still exports - unfinished work is what the file is for - while a malformed value is refused with the reason OK gives, and the file read accepts only one settings object of this window, refusing another window's settings, a file that is not JSON and a file holding several settings.
        /// <para>The file read is called directly, and its refusals are asserted from the outside: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationSolveOptionsWindow_SettingsRefusals()
        {
            bool? exported_Empty = null;
            string? message_Exported_Empty = null;
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_ExportedEmpty = null;
            bool? refused_Malformed = null;
            string? message_Exported_Malformed = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingSolarRadiationSolveOptionsWindow postgreSQLBuildingSolarRadiationSolveOptionsWindow = new(null, SolarRadiationOptionsWindowCounties());

                // No county selected and nothing else touched: saved as no scope rather than refused.
                PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Exported = postgreSQLBuildingSolarRadiationSolveOptionsWindow.CreateOptions(false, out message_Exported_Empty);
                exported_Empty = postgreSQLBuildingSolarRadiationSolveOptions_Exported is not null;
                postgreSQLBuildingSolarRadiationSolveOptions_ExportedEmpty = postgreSQLBuildingSolarRadiationSolveOptions_Exported;

                // A value that is there but malformed could not be written back as typed, so nothing is written.
                postgreSQLBuildingSolarRadiationSolveOptionsWindow.TextBoxControl_BatchSize.Value = "abc";
                refused_Malformed = postgreSQLBuildingSolarRadiationSolveOptionsWindow.CreateOptions(false, out message_Exported_Malformed) is null;
            });

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLBuildingSolarRadiationSolveOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLBuildingSolarRadiationContextExportOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Self = PostgreSQLBuildingSolarRadiationSolveOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Foreign = PostgreSQLBuildingSolarRadiationSolveOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_NotJson = PostgreSQLBuildingSolarRadiationSolveOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Several = PostgreSQLBuildingSolarRadiationSolveOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            Assert.Null(exception);

            // An unfinished window exports: no county selected is no scope, not a refusal.
            Assert.True(exported_Empty);
            Assert.Null(message_Exported_Empty);
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_ExportedEmpty);
            Assert.Null(postgreSQLBuildingSolarRadiationSolveOptions_ExportedEmpty!.CountyIds);

            // A malformed value is refused with a reason and changes nothing.
            Assert.True(refused_Malformed);
            Assert.False(string.IsNullOrWhiteSpace(message_Exported_Malformed));

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.Null(message_Self);
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLBuildingSolarRadiationSolveOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLBuildingSolarRadiationSolveOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLBuildingSolarRadiationSolveOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies that the solar urban-context export options window builds without a database or a running application, keeps the radii in the column order given, proposes <see cref="Query.SolarContextDirectory(string?)"/> when the options carry no folder, keeps a folder they do carry, and works on a copy (DiGi.GIS.PostgreSQL.UI#44).
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationContextExportOptionsWindow_Construction()
        {
            PostgreSQLBuildingSolarRadiationContextExportOptions postgreSQLBuildingSolarRadiationContextExportOptions = new()
            {
                CountyIds = [4816],
                Radii = [100, 0, 50],
                OutputDirectory = null
            };

            string outputDirectory_Given = Path.Combine(Path.GetTempPath(), "solar_context_given");

            List<double>? radii = null;
            string? outputDirectory_Default = null;
            string? outputDirectory_Kept = null;
            string? radiiHint_WithZero = null;
            string? radiiHint_WithoutZero = null;
            PostgreSQLBuildingSolarRadiationContextExportOptions? postgreSQLBuildingSolarRadiationContextExportOptions_Held = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingSolarRadiationContextExportOptionsWindow postgreSQLBuildingSolarRadiationContextExportOptionsWindow = new(postgreSQLBuildingSolarRadiationContextExportOptions, SolarRadiationOptionsWindowCounties());

                radii = postgreSQLBuildingSolarRadiationContextExportOptionsWindow.ListBoxControl_Radii.GetItems<double>(selected: false);
                outputDirectory_Default = postgreSQLBuildingSolarRadiationContextExportOptionsWindow.TextBoxControl_OutputDirectory.Value;
                radiiHint_WithZero = postgreSQLBuildingSolarRadiationContextExportOptionsWindow.Label_RadiiHint.Content as string;

                postgreSQLBuildingSolarRadiationContextExportOptions_Held = postgreSQLBuildingSolarRadiationContextExportOptionsWindow.PostgreSQLBuildingSolarRadiationContextExportOptions;
                postgreSQLBuildingSolarRadiationContextExportOptions_Held.BatchSize = 1;

                PostgreSQLBuildingSolarRadiationContextExportOptionsWindow postgreSQLBuildingSolarRadiationContextExportOptionsWindow_Given = new(new PostgreSQLBuildingSolarRadiationContextExportOptions() { OutputDirectory = outputDirectory_Given, Radii = [50] }, SolarRadiationOptionsWindowCounties());
                outputDirectory_Kept = postgreSQLBuildingSolarRadiationContextExportOptionsWindow_Given.TextBoxControl_OutputDirectory.Value;
                radiiHint_WithoutZero = postgreSQLBuildingSolarRadiationContextExportOptionsWindow_Given.Label_RadiiHint.Content as string;
            });

            Assert.Null(exception);

            // The column order of the files, not sorted.
            Assert.Equal([100, 0, 50], radii);

            Assert.Equal(Query.SolarContextDirectory(), outputDirectory_Default);
            Assert.Equal(outputDirectory_Given, outputDirectory_Kept);

            // The shortfall columns need the isolated building: its absence is said, its presence is not.
            Assert.True(string.IsNullOrEmpty(radiiHint_WithZero));
            Assert.False(string.IsNullOrEmpty(radiiHint_WithoutZero));

            Assert.NotNull(postgreSQLBuildingSolarRadiationContextExportOptions_Held);
            Assert.False(ReferenceEquals(postgreSQLBuildingSolarRadiationContextExportOptions, postgreSQLBuildingSolarRadiationContextExportOptions_Held));
            Assert.Equal(1000, postgreSQLBuildingSolarRadiationContextExportOptions.BatchSize);

            // The window only proposes the folder; the options stay unset until OK.
            Assert.Null(postgreSQLBuildingSolarRadiationContextExportOptions.OutputDirectory);
        }

        /// <summary>
        /// Verifies that the solar summary options window shows an unset <see cref="PostgreSQLBuildingDataSolarRadiationUpdateOptions.Radius"/> as an empty box - the latest result of any radius - and a set one as its value, and works on a copy (DiGi.GIS.PostgreSQL.UI#44).
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_Construction()
        {
            PostgreSQLBuildingDataSolarRadiationUpdateOptions postgreSQLBuildingDataSolarRadiationUpdateOptions = new()
            {
                CountyIds = [55417],
                Radius = 50,
                SkipCompleted = true
            };

            string? radius_Set = null;
            string? radius_Unset = null;
            bool? skipCompleted = null;
            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Held = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow = new(postgreSQLBuildingDataSolarRadiationUpdateOptions, SolarRadiationOptionsWindowCounties());

                radius_Set = postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TextBoxControl_Radius.Value;
                skipCompleted = postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.CheckBox_SkipCompleted.IsChecked;

                postgreSQLBuildingDataSolarRadiationUpdateOptions_Held = postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.PostgreSQLBuildingDataSolarRadiationUpdateOptions;
                postgreSQLBuildingDataSolarRadiationUpdateOptions_Held.Radius = null;

                radius_Unset = new PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow(null, SolarRadiationOptionsWindowCounties()).TextBoxControl_Radius.Value;
            });

            Assert.Null(exception);
            Assert.Equal(50.0.ToString(System.Globalization.CultureInfo.CurrentCulture), radius_Set);
            Assert.Equal(string.Empty, radius_Unset);
            Assert.True(skipCompleted);

            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_Held);
            Assert.False(ReferenceEquals(postgreSQLBuildingDataSolarRadiationUpdateOptions, postgreSQLBuildingDataSolarRadiationUpdateOptions_Held));
            Assert.Equal(50, postgreSQLBuildingDataSolarRadiationUpdateOptions.Radius);
        }

        /// <summary>
        /// Verifies the layout of the three solar radiation options windows without the host, at their opening and at their minimum size: every named control gets a size, lies inside the window, and no caption is cut short (<c>Coding - WPF Deferred Loading.md</c> §7, DiGi.GIS.PostgreSQL.UI#44).
        /// <para>A window is laid out by the handle it gets when shown, and a test must not put a dialog on screen, so the window's content is taken out and laid out on its own at the window's client size (the window less its border and caption). That is the layout the dialog shows; what it does not cover is the system font size of a particular machine.</para>
        /// </summary>
        /// <summary>
        /// Verifies that the settings of the building data solar summary window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#66): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>All five option members have a control, so the round trip is complete: the county subset, the radius (a value and the empty box), the skip box, the batch size and the timeout. The import side runs in a window constructed with no options, so every value asserted can only have come from the file.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLBuildingDataSolarRadiationUpdateOptions postgreSQLBuildingDataSolarRadiationUpdateOptions = new()
            {
                CountyIds = [55417, 4816],
                Radius = 100,
                SkipCompleted = true,
                BatchSize = 2500,
                CommandTimeout = 900,
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Read = null;
            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Read = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow = new(postgreSQLBuildingDataSolarRadiationUpdateOptions, SolarRadiationOptionsWindowCounties());

                Expect(!ReferenceEquals(postgreSQLBuildingDataSolarRadiationUpdateOptions, postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.PostgreSQLBuildingDataSolarRadiationUpdateOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Exported, out string? message_Export) && postgreSQLBuildingDataSolarRadiationUpdateOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLBuildingDataSolarRadiationUpdateOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLBuildingDataSolarRadiationUpdateOptions postgreSQLBuildingDataSolarRadiationUpdateOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's.
                PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_Imported = new(null, SolarRadiationOptionsWindowCounties());
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_Imported.SetControls(postgreSQLBuildingDataSolarRadiationUpdateOptions_Imported);

                if (postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLBuildingDataSolarRadiationUpdateOptions_Read = postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_Imported.PostgreSQLBuildingDataSolarRadiationUpdateOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLBuildingDataSolarRadiationUpdateOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // The empty radius is a value of its own: a file with no radius has to empty the radius box of a
                // window that shows one, and OK has to read that empty box back as no radius.
                PostgreSQLBuildingDataSolarRadiationUpdateOptions postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius = new()
                {
                    CountyIds = [4816],
                    Radius = null,
                };

                PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_NoRadius = new(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius, SolarRadiationOptionsWindowCounties());
                if (!postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_NoRadius.TryReadOptions(false, out PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Exported, out string? message_NoRadius) || postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Exported is null)
                {
                    Expect(false, $"the no-radius export was refused: {message_NoRadius}");
                    return;
                }

                Expect(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Exported.Radius is null, "an empty radius box was written as a radius");

                if (PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadSettings(Core.Convert.ToSystem_String(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Exported), out string? message_NoRadius_Read) is not PostgreSQLBuildingDataSolarRadiationUpdateOptions postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Imported)
                {
                    Expect(false, $"the no-radius file was refused: {message_NoRadius_Read}");
                    return;
                }

                PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_RadiusShown = new(postgreSQLBuildingDataSolarRadiationUpdateOptions, SolarRadiationOptionsWindowCounties());
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_RadiusShown.SetControls(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Imported);

                if (postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_RadiusShown.TryGetOptions(out string? message_NoRadius_Ok))
                {
                    postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Read = postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_RadiusShown.PostgreSQLBuildingDataSolarRadiationUpdateOptions;
                }
                else
                {
                    Expect(false, $"the no-radius import was refused by OK: {message_NoRadius_Ok}");
                }
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
            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the county subset, the radius, the skip box, the batch size, the timeout.
            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_Read!.CountyIds);
            Assert.True(postgreSQLBuildingDataSolarRadiationUpdateOptions_Read!.CountyIds!.SetEquals([55417, 4816]));
            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_Read!.Radius);
            Assert.Equal(100d, postgreSQLBuildingDataSolarRadiationUpdateOptions_Read!.Radius!.Value);
            Assert.True(postgreSQLBuildingDataSolarRadiationUpdateOptions_Read!.SkipCompleted);
            Assert.Equal(2500, postgreSQLBuildingDataSolarRadiationUpdateOptions_Read!.BatchSize);
            Assert.Equal(900, postgreSQLBuildingDataSolarRadiationUpdateOptions_Read!.CommandTimeout);

            // The file with no radius emptied the box of the window that showed one.
            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Read);
            Assert.Null(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Read!.Radius);
            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Read!.CountyIds);
            Assert.True(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoRadius_Read!.CountyIds!.SetEquals([4816]));

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions.Radius);
            Assert.Equal(100d, postgreSQLBuildingDataSolarRadiationUpdateOptions.Radius!.Value);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the building data solar summary window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#66): an unfinished window still exports - a radius below zero, a batch size of zero, a missing county - while a box that does not hold its number is refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow = new(null, SolarRadiationOptionsWindowCounties());

                // A radius that is not a number could not be written back as typed; NaN parses but does not
                // survive JSON; -5 is a number and is OK's to refuse on read-back.
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TextBoxControl_Radius.Value = "half";
                Expect(!postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("Radius", StringComparison.Ordinal), "a radius that is not a number was exported");

                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TextBoxControl_Radius.Value = "NaN";
                Expect(!postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_NaN) && message_NaN is not null, "a radius of NaN was exported");

                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataSolarRadiationUpdateOptions());
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => true);
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TextBoxControl_Radius.Value = "-5";
                Expect(postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Negative, out string? message_Negative) && postgreSQLBuildingDataSolarRadiationUpdateOptions_Negative is not null, $"a negative radius was refused by the export: {message_Negative}");
                Expect(postgreSQLBuildingDataSolarRadiationUpdateOptions_Negative?.Radius == -5, "the negative radius was not written as typed");
                Expect(!postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryGetOptions(out string? message_Negative_Ok) && message_Negative_Ok is not null && message_Negative_Ok.Contains("Radius", StringComparison.Ordinal), "a negative radius passed OK");

                // A batch size that is not a whole number is refused on both paths; zero exports and is OK's.
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataSolarRadiationUpdateOptions());
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => true);
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TextBoxControl_BatchSize.Value = "half";
                Expect(!postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_Batch) && message_Batch is not null && message_Batch.Contains("Batch size", StringComparison.Ordinal), "a batch size that is not a number was exported");

                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataSolarRadiationUpdateOptions());
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => true);
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TextBoxControl_BatchSize.Value = "0";
                Expect(postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Batch, out string? message_BatchZero) && postgreSQLBuildingDataSolarRadiationUpdateOptions_Batch is not null, $"a batch size of zero was refused by the export: {message_BatchZero}");
                Expect(!postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryGetOptions(out string? message_BatchZero_Ok) && message_BatchZero_Ok is not null && message_BatchZero_Ok.Contains("Batch size", StringComparison.Ordinal), "a batch size of zero passed OK");

                // No county: written as no county list, refused by OK - and the refused reads wrote nothing.
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.SetControls(new PostgreSQLBuildingDataSolarRadiationUpdateOptions());
                postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => false);
                Expect(postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_NoCounty, out string? message_NoCounty) && postgreSQLBuildingDataSolarRadiationUpdateOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(postgreSQLBuildingDataSolarRadiationUpdateOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                Expect(postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.PostgreSQLBuildingDataSolarRadiationUpdateOptions.BatchSize == 1000, "a refused read wrote the batch size");
                Expect(postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.PostgreSQLBuildingDataSolarRadiationUpdateOptions.CountyIds is null, "a refused read wrote the county list");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLBuildingDataSolarRadiationUpdateOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Self = PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Foreign = PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_NotJson = PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Several = PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLBuildingDataSolarRadiationUpdateOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLBuildingDataSolarRadiationUpdateOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLBuildingDataSolarRadiationUpdateOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        [Fact]
        public void SolarRadiationOptionsWindows_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                List<Window> windows =
                [
                    new PostgreSQLBuildingSolarRadiationSolveOptionsWindow(null, SolarRadiationOptionsWindowCounties()),
                    new PostgreSQLBuildingSolarRadiationContextExportOptionsWindow(null, SolarRadiationOptionsWindowCounties()),
                    new PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow(null, SolarRadiationOptionsWindowCounties())
                ];

                foreach (Window window in windows)
                {
                    if (window.Content is not FrameworkElement frameworkElement)
                    {
                        failures.Add($"{window.GetType().Name}: no content");
                        continue;
                    }

                    // Detached so it can be laid out without the window ever getting a handle.
                    window.Content = null;

                    foreach ((double width, double height) in new (double, double)[] { (window.Width, window.Height), (window.MinWidth, window.MinHeight) })
                    {
                        // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                        Size size = new(width - 16, height - 39);

                        frameworkElement.Measure(size);
                        frameworkElement.Arrange(new Rect(size));
                        frameworkElement.UpdateLayout();

                        SolarRadiationOptionsWindowLayoutFailures(window.GetType().Name, size, frameworkElement, frameworkElement, failures);
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        private static List<AdministrativeAreal2DReference> SolarRadiationOptionsWindowCounties()
        {
            return
            [
                new() { Id = 55417, Code = "1465", Name = "m. St. Warszawa", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                // Two pieces of one multi-part county: same code and name, told apart only by the identifier.
                new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 22139, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
            ];
        }

        private static void SolarRadiationOptionsWindowLayoutFailures(string typeName, Size size, FrameworkElement root, DependencyObject dependencyObject, List<string> failures)
        {
            int count = VisualTreeHelper.GetChildrenCount(dependencyObject);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(dependencyObject, i);

                // The items of a list scroll; their text is not the window's layout.
                if (child is ListBox)
                {
                    continue;
                }

                if (child is FrameworkElement frameworkElement && SolarRadiationOptionsWindowIsVisible(frameworkElement, root))
                {
                    string name = string.IsNullOrEmpty(frameworkElement.Name) ? frameworkElement.GetType().Name : frameworkElement.Name;

                    if (!string.IsNullOrEmpty(frameworkElement.Name) && (frameworkElement.ActualWidth < 1 || frameworkElement.ActualHeight < 1))
                    {
                        failures.Add($"{typeName} at {size}: {name} has no size ({frameworkElement.ActualWidth} x {frameworkElement.ActualHeight})");
                    }

                    if (frameworkElement.ActualWidth >= 1 && frameworkElement.ActualHeight >= 1)
                    {
                        Rect rect = frameworkElement.TransformToAncestor(root).TransformBounds(new Rect(frameworkElement.RenderSize));
                        if (rect.Right > size.Width + 0.5 || rect.Bottom > size.Height + 0.5 || rect.Left < -0.5 || rect.Top < -0.5)
                        {
                            failures.Add($"{typeName} at {size}: {name} lies outside the window ({rect})");
                        }
                    }

                    // A caption cut short: what the text would take on its own against what it was given.
                    if (frameworkElement is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text) && textBlock.ActualWidth >= 1)
                    {
                        TextBlock textBlock_Probe = new() { Text = textBlock.Text, FontFamily = textBlock.FontFamily, FontSize = textBlock.FontSize, FontStyle = textBlock.FontStyle, FontWeight = textBlock.FontWeight };
                        textBlock_Probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        if (textBlock_Probe.DesiredSize.Width > textBlock.ActualWidth + 1)
                        {
                            failures.Add($"{typeName} at {size}: \"{textBlock.Text}\" needs {textBlock_Probe.DesiredSize.Width:F0} px and has {textBlock.ActualWidth:F0}");
                        }
                    }
                }

                SolarRadiationOptionsWindowLayoutFailures(typeName, size, root, child, failures);
            }
        }

        /// <summary>
        /// Reports whether an element is actually rendered: visible itself, and visible all the way up to the content being laid out. A control inside a collapsed form keeps its own visible flag while having no size, and that is the form being hidden rather than the control being clipped.
        /// </summary>
        /// <param name="frameworkElement">The element to test.</param>
        /// <param name="root">The content the layout is measured on, where the walk stops.</param>
        /// <returns>true when every element from the content down to the element is visible.</returns>
        private static bool SolarRadiationOptionsWindowIsVisible(FrameworkElement frameworkElement, FrameworkElement root)
        {
            DependencyObject? dependencyObject = frameworkElement;
            while (dependencyObject is FrameworkElement element)
            {
                if (element.Visibility != Visibility.Visible)
                {
                    return false;
                }

                if (ReferenceEquals(element, root))
                {
                    return true;
                }

                dependencyObject = VisualTreeHelper.GetParent(element);
            }

            return true;
        }

        private static Exception? RunSTA(Action action)
        {
            Exception? exception = null;

            Thread thread = new(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception_Temp)
                {
                    exception = exception_Temp;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return exception;
        }
    }
}
