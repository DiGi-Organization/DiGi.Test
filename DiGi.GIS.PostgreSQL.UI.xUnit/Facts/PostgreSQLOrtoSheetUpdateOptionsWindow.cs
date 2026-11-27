using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Windows;
using System;
using System.Collections.Generic;
using System.Windows;
using Size = System.Windows.Size;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the settings of the orthophoto sheet index window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#62): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>The import side runs in a window constructed with no options at all, so every value asserted below can only have come from the file. Both sides go through the real reads: <c>TryReadOptions</c> for the export and <c>TryGetOptions</c> - OK's read with its validation - after the import.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLOrtoSheetUpdateOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLOrtoSheetUpdateOptions postgreSQLOrtoSheetUpdateOptions = new()
            {
                FirstYear = 1957,
                LastYear = 2026,
                PageSize = 5000,
                RetryCount = 3,
                RetryDelaySeconds = 1.5,
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLOrtoSheetUpdateOptions? postgreSQLOrtoSheetUpdateOptions_Read = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLOrtoSheetUpdateOptionsWindow postgreSQLOrtoSheetUpdateOptionsWindow = new(postgreSQLOrtoSheetUpdateOptions);

                Expect(!ReferenceEquals(postgreSQLOrtoSheetUpdateOptions, postgreSQLOrtoSheetUpdateOptionsWindow.PostgreSQLOrtoSheetUpdateOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLOrtoSheetUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLOrtoSheetUpdateOptions? postgreSQLOrtoSheetUpdateOptions_Exported, out string? message_Export) && postgreSQLOrtoSheetUpdateOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLOrtoSheetUpdateOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLOrtoSheetUpdateOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLOrtoSheetUpdateOptions postgreSQLOrtoSheetUpdateOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's.
                PostgreSQLOrtoSheetUpdateOptionsWindow postgreSQLOrtoSheetUpdateOptionsWindow_Imported = new(null);
                postgreSQLOrtoSheetUpdateOptionsWindow_Imported.SetControls(postgreSQLOrtoSheetUpdateOptions_Imported);

                if (postgreSQLOrtoSheetUpdateOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLOrtoSheetUpdateOptions_Read = postgreSQLOrtoSheetUpdateOptionsWindow_Imported.PostgreSQLOrtoSheetUpdateOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLOrtoSheetUpdateOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
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
            Assert.NotNull(postgreSQLOrtoSheetUpdateOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the years, the page size, the retry count and the retry delay.
            Assert.Equal(1957, postgreSQLOrtoSheetUpdateOptions_Read!.FirstYear);
            Assert.Equal(2026, postgreSQLOrtoSheetUpdateOptions_Read!.LastYear);
            Assert.Equal(5000, postgreSQLOrtoSheetUpdateOptions_Read!.PageSize);
            Assert.Equal(3, postgreSQLOrtoSheetUpdateOptions_Read!.RetryCount);
            Assert.Equal(1.5, postgreSQLOrtoSheetUpdateOptions_Read!.RetryDelaySeconds);

            // The window's own options are what the caller gave it: the export read writes nothing.
            Assert.Equal(1957, postgreSQLOrtoSheetUpdateOptions.FirstYear);
            Assert.Equal(3, postgreSQLOrtoSheetUpdateOptions.RetryCount);
            Assert.Equal(1.5, postgreSQLOrtoSheetUpdateOptions.RetryDelaySeconds);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the orthophoto sheet index window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#62): an unfinished window still exports - a year below 1900 written as typed and refused by OK on read-back - while a box that does not hold its number and a retry delay that is not a finite number are refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLOrtoSheetUpdateOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLOrtoSheetUpdateOptionsWindow postgreSQLOrtoSheetUpdateOptionsWindow = new(null);

                // A year below 1900 is a whole number and could be written back as typed: the export takes it,
                // and OK's range rule is what refuses it when the file is read back.
                postgreSQLOrtoSheetUpdateOptionsWindow.TextBoxControl_FirstYear.Value = "1800";
                Expect(postgreSQLOrtoSheetUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLOrtoSheetUpdateOptions? postgreSQLOrtoSheetUpdateOptions_OldYear, out string? message_OldYear) && postgreSQLOrtoSheetUpdateOptions_OldYear is not null, $"an out-of-range year was refused by the export: {message_OldYear}");
                Expect(postgreSQLOrtoSheetUpdateOptions_OldYear?.FirstYear == 1800, "the out-of-range year was not written as typed");
                Expect(!postgreSQLOrtoSheetUpdateOptionsWindow.TryGetOptions(out string? message_OldYear_Ok) && message_OldYear_Ok is not null && message_OldYear_Ok.Contains("1900", StringComparison.Ordinal), "a year below 1900 passed OK");

                // SetControls clears what the options leave out, so the window is back to its defaults.
                postgreSQLOrtoSheetUpdateOptionsWindow.SetControls(new PostgreSQLOrtoSheetUpdateOptions());

                // A page size that is not a whole number could not be written back as typed.
                postgreSQLOrtoSheetUpdateOptionsWindow.TextBoxControl_PageSize.Value = "half";
                Expect(!postgreSQLOrtoSheetUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_PageSize) && message_PageSize is not null && message_PageSize.Contains("Page size", StringComparison.Ordinal), "a page size that is not a number was exported");
                postgreSQLOrtoSheetUpdateOptionsWindow.SetControls(new PostgreSQLOrtoSheetUpdateOptions());

                // A retry delay of NaN parses as a number but is not one that survives JSON.
                postgreSQLOrtoSheetUpdateOptionsWindow.TextBoxControl_RetryDelaySeconds.Value = "NaN";
                Expect(!postgreSQLOrtoSheetUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_NaN) && message_NaN is not null, "a retry delay of NaN was exported");
                postgreSQLOrtoSheetUpdateOptionsWindow.SetControls(new PostgreSQLOrtoSheetUpdateOptions());

                // The defaults: an unfinished window with its five boxes as they open still exports.
                Expect(postgreSQLOrtoSheetUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLOrtoSheetUpdateOptions? postgreSQLOrtoSheetUpdateOptions_Defaults, out string? message_Defaults) && postgreSQLOrtoSheetUpdateOptions_Defaults is not null, $"the defaults were refused by the export: {message_Defaults}");

                // And OK still applies its full rules to the same window.
                Expect(postgreSQLOrtoSheetUpdateOptionsWindow.TryGetOptions(out string? message_Defaults_Ok) && message_Defaults_Ok is null, $"the defaults were refused by OK: {message_Defaults_Ok}");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLOrtoSheetUpdateOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLOrtoSheetUpdateOptions? postgreSQLOrtoSheetUpdateOptions_Self = PostgreSQLOrtoSheetUpdateOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLOrtoSheetUpdateOptions? postgreSQLOrtoSheetUpdateOptions_Foreign = PostgreSQLOrtoSheetUpdateOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLOrtoSheetUpdateOptions? postgreSQLOrtoSheetUpdateOptions_NotJson = PostgreSQLOrtoSheetUpdateOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLOrtoSheetUpdateOptions? postgreSQLOrtoSheetUpdateOptions_Several = PostgreSQLOrtoSheetUpdateOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLOrtoSheetUpdateOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLOrtoSheetUpdateOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLOrtoSheetUpdateOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLOrtoSheetUpdateOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the orthophoto sheet index window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#62; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLOrtoSheetUpdateOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLOrtoSheetUpdateOptionsWindow postgreSQLOrtoSheetUpdateOptionsWindow = new(null);

                if (postgreSQLOrtoSheetUpdateOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(PostgreSQLOrtoSheetUpdateOptionsWindow)}: no content");
                    return;
                }

                postgreSQLOrtoSheetUpdateOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(postgreSQLOrtoSheetUpdateOptionsWindow.Width - 16, postgreSQLOrtoSheetUpdateOptionsWindow.Height - 39);
                Size size_Min = new(postgreSQLOrtoSheetUpdateOptionsWindow.MinWidth - 16, postgreSQLOrtoSheetUpdateOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLOrtoSheetUpdateOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(PostgreSQLOrtoSheetUpdateOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = postgreSQLOrtoSheetUpdateOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLOrtoSheetUpdateOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = postgreSQLOrtoSheetUpdateOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLOrtoSheetUpdateOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(PostgreSQLOrtoSheetUpdateOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
