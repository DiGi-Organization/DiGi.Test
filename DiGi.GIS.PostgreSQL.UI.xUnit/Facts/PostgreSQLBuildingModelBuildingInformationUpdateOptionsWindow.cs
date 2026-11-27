using DiGi.Analytical.Building.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.PostgreSQL.UI.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Size = System.Windows.Size;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the settings of the BuildingInformation window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#73): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>Every member of this window's options has a control, so the round trip is complete. Two county rules are asserted beside it: a window opened with no options shows the national scope - every county selected - while a file with no county list clears the selection of the window it fills, and OK then refuses it exactly as a typed empty selection.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLBuildingModelBuildingInformationUpdateOptions postgreSQLBuildingModelBuildingInformationUpdateOptions = new()
            {
                CountyIds = [1001, 1003],
                BuildingModelDetailLevel = BuildingModelDetailLevel.Component,
                BatchSize = 250,
                CommandTimeout = 900,
                ReportDirectory = Path.GetTempPath().TrimEnd('\\', '/'),
                Resume = false,
                DryRun = false,
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_Read = null;
            int openingSelection = -1;
            int listedAfterOpening = -1;
            bool refusedWithoutCounty = false;
            bool clearedWithoutCounty = false;

            Exception? exception = RunSTA(() =>
            {
                // The opening default of an unscoped window: every county selected. This is SetCounties' rule,
                // which the constructor keeps by running SetControls before the list exists.
                PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_Opening = new(null, OpenBuildingImportOptionsWindowCounties(5));
                listedAfterOpening = postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_Opening.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>(false)?.Count ?? 0;
                openingSelection = postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_Opening.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>()?.Count ?? 0;

                PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow = new(postgreSQLBuildingModelBuildingInformationUpdateOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(postgreSQLBuildingModelBuildingInformationUpdateOptions, postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.PostgreSQLBuildingModelBuildingInformationUpdateOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_Exported, out string? message_Export) && postgreSQLBuildingModelBuildingInformationUpdateOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLBuildingModelBuildingInformationUpdateOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLBuildingModelBuildingInformationUpdateOptions postgreSQLBuildingModelBuildingInformationUpdateOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's.
                PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_Imported.SetControls(postgreSQLBuildingModelBuildingInformationUpdateOptions_Imported);

                if (postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLBuildingModelBuildingInformationUpdateOptions_Read = postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_Imported.PostgreSQLBuildingModelBuildingInformationUpdateOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLBuildingModelBuildingInformationUpdateOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // A file with no county list clears the selection of the window it fills - even though that
                // window opened showing every county - and OK refuses the result.
                PostgreSQLBuildingModelBuildingInformationUpdateOptions postgreSQLBuildingModelBuildingInformationUpdateOptions_NoScope = new() { CountyIds = null, BatchSize = 250 };
                string? json_NoScope = Core.Convert.ToSystem_String(postgreSQLBuildingModelBuildingInformationUpdateOptions_NoScope);
                if (PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadSettings(json_NoScope, out string? message_NoScope) is not PostgreSQLBuildingModelBuildingInformationUpdateOptions postgreSQLBuildingModelBuildingInformationUpdateOptions_NoScope_Imported)
                {
                    Expect(false, $"the no-scope file was refused: {message_NoScope}");
                    return;
                }

                PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_NoScope = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_NoScope.SetControls(postgreSQLBuildingModelBuildingInformationUpdateOptions_NoScope_Imported);
                clearedWithoutCounty = (postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_NoScope.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>() ?? []).Count == 0;
                if (!clearedWithoutCounty)
                {
                    Expect(false, "the import of a file with no county list did not clear the selection");
                }

                refusedWithoutCounty = !postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_NoScope.TryGetOptions(out string? message_NoScope_Ok) && message_NoScope_Ok is not null && message_NoScope_Ok.Contains("county", StringComparison.OrdinalIgnoreCase);
                if (!refusedWithoutCounty)
                {
                    Expect(false, $"an imported empty scope passed OK: {message_NoScope_Ok}");
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

            // The opening default held: every county of the list selected.
            Assert.Equal(5, listedAfterOpening);
            Assert.Equal(5, openingSelection);

            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.NotNull(postgreSQLBuildingModelBuildingInformationUpdateOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the counties, the detail level, the batch, the timeout, the report
            // directory, both switches.
            Assert.NotNull(postgreSQLBuildingModelBuildingInformationUpdateOptions_Read!.CountyIds);
            Assert.True(postgreSQLBuildingModelBuildingInformationUpdateOptions_Read!.CountyIds!.SetEquals([1001, 1003]));
            Assert.Equal(BuildingModelDetailLevel.Component, postgreSQLBuildingModelBuildingInformationUpdateOptions_Read!.BuildingModelDetailLevel);
            Assert.Equal(250, postgreSQLBuildingModelBuildingInformationUpdateOptions_Read!.BatchSize);
            Assert.Equal(900, postgreSQLBuildingModelBuildingInformationUpdateOptions_Read!.CommandTimeout);
            Assert.Equal(postgreSQLBuildingModelBuildingInformationUpdateOptions.ReportDirectory, postgreSQLBuildingModelBuildingInformationUpdateOptions_Read!.ReportDirectory);
            Assert.False(postgreSQLBuildingModelBuildingInformationUpdateOptions_Read!.Resume);
            Assert.False(postgreSQLBuildingModelBuildingInformationUpdateOptions_Read!.DryRun);

            Assert.True(clearedWithoutCounty);
            Assert.True(refusedWithoutCounty);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(900, postgreSQLBuildingModelBuildingInformationUpdateOptions.CommandTimeout);
            Assert.False(postgreSQLBuildingModelBuildingInformationUpdateOptions.DryRun);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the BuildingInformation window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#73): an unfinished window still exports - a report directory that does not exist written as it stands, no county as no county list - while a box that does not hold a whole number is refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_SettingsRefusals()
        {
            const string reportDirectory = "C:/Temp/digi-buildinginformation-no-such-directory";

            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingModelBuildingInformationUpdateOptions postgreSQLBuildingModelBuildingInformationUpdateOptions = new()
                {
                    CountyIds = [1001],
                    ReportDirectory = reportDirectory,
                };

                PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow = new(postgreSQLBuildingModelBuildingInformationUpdateOptions, OpenBuildingImportOptionsWindowCounties(5));

                // A batch size that is not a whole number could not be written back as typed.
                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TextBoxControl_BatchSize.Value = "half";
                Expect(!postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("Batch size", StringComparison.Ordinal), "a batch size that is not a number was exported");

                // A batch size of zero is a whole number: it exports and OK's range rule refuses it.
                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.SetControls(postgreSQLBuildingModelBuildingInformationUpdateOptions);
                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TextBoxControl_BatchSize.Value = "0";
                Expect(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_Batch, out string? message_Batch) && postgreSQLBuildingModelBuildingInformationUpdateOptions_Batch is not null, $"a batch size of zero was refused by the export: {message_Batch}");
                Expect(!postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryGetOptions(out string? message_Batch_Ok) && message_Batch_Ok is not null && message_Batch_Ok.Contains("Batch size", StringComparison.Ordinal), "a batch size of zero passed OK");

                // A report directory that does not exist exports as typed - it is a string that could be
                // written back - and OK's existence rule refuses it on read-back.
                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.SetControls(postgreSQLBuildingModelBuildingInformationUpdateOptions);
                Expect(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_Path, out string? message_Path) && postgreSQLBuildingModelBuildingInformationUpdateOptions_Path is not null, $"a nonexistent report directory was refused by the export: {message_Path}");
                Expect(postgreSQLBuildingModelBuildingInformationUpdateOptions_Path?.ReportDirectory == reportDirectory, "the report directory was not written as typed");
                Expect(!postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryGetOptions(out string? message_Path_Ok) && message_Path_Ok is not null && message_Path_Ok.Contains("does not exist", StringComparison.Ordinal), "a nonexistent report directory passed OK");

                // No county: written as no county list, refused by OK - and the refused reads wrote nothing.
                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.SetControls(postgreSQLBuildingModelBuildingInformationUpdateOptions);
                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => false);
                Expect(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_NoCounty, out string? message_NoCounty) && postgreSQLBuildingModelBuildingInformationUpdateOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(postgreSQLBuildingModelBuildingInformationUpdateOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                // The refused reads wrote nothing: the options still hold what the window was opened with.
                Expect(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.PostgreSQLBuildingModelBuildingInformationUpdateOptions.BatchSize == 500, "a refused read wrote the batch size");
                Expect(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.PostgreSQLBuildingModelBuildingInformationUpdateOptions.CountyIds is HashSet<int> countyIds_Held && countyIds_Held.SetEquals([1001]), "a refused read wrote the county list");
                Expect(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.PostgreSQLBuildingModelBuildingInformationUpdateOptions.ReportDirectory == reportDirectory, "a refused read wrote the report directory");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLBuildingModelBuildingInformationUpdateOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_Self = PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_Foreign = PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_NotJson = PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLBuildingModelBuildingInformationUpdateOptions? postgreSQLBuildingModelBuildingInformationUpdateOptions_Several = PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLBuildingModelBuildingInformationUpdateOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLBuildingModelBuildingInformationUpdateOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLBuildingModelBuildingInformationUpdateOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLBuildingModelBuildingInformationUpdateOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the BuildingInformation window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#73; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow)}: no content");
                    return;
                }

                postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.Width - 16, postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.Height - 39);
                Size size_Min = new(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.MinWidth - 16, postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLBuildingModelBuildingInformationUpdateOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(PostgreSQLBuildingModelBuildingInformationUpdateOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
