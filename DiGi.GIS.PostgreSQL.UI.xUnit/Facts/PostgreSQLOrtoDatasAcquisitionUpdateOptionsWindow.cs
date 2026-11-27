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
        /// Verifies that the settings of the acquisition backfill window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#70): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>The import side runs in a window constructed with no options, so every value asserted can only have come from the file. The scope of this window is always named by OK, so a file with no county list clears the selection and is refused when read back - exactly as a typed empty selection is.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLOrtoDatasAcquisitionUpdateOptions postgreSQLOrtoDatasAcquisitionUpdateOptions = new()
            {
                LastYear = 2015,
                PageSize = 250,
                CommandTimeout = 0,
                CountyIds = [1001, 1003],
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLOrtoDatasAcquisitionUpdateOptions? postgreSQLOrtoDatasAcquisitionUpdateOptions_Read = null;
            bool refusedAfterNoScopeImport = false;
            bool noScopeHeldUntouched = false;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow = new(postgreSQLOrtoDatasAcquisitionUpdateOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(postgreSQLOrtoDatasAcquisitionUpdateOptions, postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.PostgreSQLOrtoDatasAcquisitionUpdateOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLOrtoDatasAcquisitionUpdateOptions? postgreSQLOrtoDatasAcquisitionUpdateOptions_Exported, out string? message_Export) && postgreSQLOrtoDatasAcquisitionUpdateOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLOrtoDatasAcquisitionUpdateOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLOrtoDatasAcquisitionUpdateOptions postgreSQLOrtoDatasAcquisitionUpdateOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's.
                PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_Imported.SetControls(postgreSQLOrtoDatasAcquisitionUpdateOptions_Imported);

                if (postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLOrtoDatasAcquisitionUpdateOptions_Read = postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_Imported.PostgreSQLOrtoDatasAcquisitionUpdateOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLOrtoDatasAcquisitionUpdateOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // A file with no county list clears the selection, and OK refuses the result - this window's
                // scope is always named, whether the empty selection was typed or imported.
                PostgreSQLOrtoDatasAcquisitionUpdateOptions postgreSQLOrtoDatasAcquisitionUpdateOptions_NoScope = new() { CountyIds = null, LastYear = 2015, PageSize = 250 };
                string? json_NoScope = Core.Convert.ToSystem_String(postgreSQLOrtoDatasAcquisitionUpdateOptions_NoScope);
                if (PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadSettings(json_NoScope, out string? message_NoScope) is not PostgreSQLOrtoDatasAcquisitionUpdateOptions postgreSQLOrtoDatasAcquisitionUpdateOptions_NoScope_Imported)
                {
                    Expect(false, $"the no-scope file was refused: {message_NoScope}");
                    return;
                }

                PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_NoScope = new(postgreSQLOrtoDatasAcquisitionUpdateOptions, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_NoScope.SetControls(postgreSQLOrtoDatasAcquisitionUpdateOptions_NoScope_Imported);

                refusedAfterNoScopeImport = !postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_NoScope.TryGetOptions(out string? message_NoScope_Ok) && message_NoScope_Ok is not null && message_NoScope_Ok.Contains("county part", StringComparison.OrdinalIgnoreCase);
                if (!refusedAfterNoScopeImport)
                {
                    Expect(false, $"an imported empty scope passed OK: {message_NoScope_Ok}");
                }

                // The refused read left that window's options exactly as the caller gave them.
                noScopeHeldUntouched = postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_NoScope.PostgreSQLOrtoDatasAcquisitionUpdateOptions.CountyIds is HashSet<int> countyIds_Held && countyIds_Held.SetEquals([1001, 1003]);
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
            Assert.NotNull(postgreSQLOrtoDatasAcquisitionUpdateOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the last year, the page size, the timeout, the county subset.
            Assert.Equal(2015, postgreSQLOrtoDatasAcquisitionUpdateOptions_Read!.LastYear);
            Assert.Equal(250, postgreSQLOrtoDatasAcquisitionUpdateOptions_Read!.PageSize);
            Assert.Equal(0, postgreSQLOrtoDatasAcquisitionUpdateOptions_Read!.CommandTimeout);
            Assert.NotNull(postgreSQLOrtoDatasAcquisitionUpdateOptions_Read!.CountyIds);
            Assert.True(postgreSQLOrtoDatasAcquisitionUpdateOptions_Read!.CountyIds!.SetEquals([1001, 1003]));

            // The imported empty scope was refused by OK, so that window's options were never written.
            Assert.True(refusedAfterNoScopeImport);
            Assert.True(noScopeHeldUntouched);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(2015, postgreSQLOrtoDatasAcquisitionUpdateOptions.LastYear);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the acquisition backfill window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#70): an unfinished window still exports - a last year below 1900 or a missing county written as it stands - while a box that does not hold its number is refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(5));

                // A page size that is not a whole number could not be written back as typed.
                postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TextBoxControl_PageSize.Value = "half";
                Expect(!postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("Page size", StringComparison.Ordinal), "a page size that is not a number was exported");

                // A last year below 1900 is a whole number: the export takes it and OK's range rule refuses it.
                postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.SetControls(new PostgreSQLOrtoDatasAcquisitionUpdateOptions());
                postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TextBoxControl_LastYear.Value = "1800";
                Expect(postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLOrtoDatasAcquisitionUpdateOptions? postgreSQLOrtoDatasAcquisitionUpdateOptions_Old, out string? message_Old) && postgreSQLOrtoDatasAcquisitionUpdateOptions_Old is not null, $"a last year below 1900 was refused by the export: {message_Old}");
                Expect(postgreSQLOrtoDatasAcquisitionUpdateOptions_Old?.LastYear == 1800, "the out-of-range year was not written as typed");
                Expect(!postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryGetOptions(out string? message_Old_Ok) && message_Old_Ok is not null && message_Old_Ok.Contains("1900", StringComparison.Ordinal), "a last year below 1900 passed OK");

                // No county part: written as no county list, refused by OK - this window's scope is always
                // named - and the refused reads wrote nothing.
                postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.SetControls(new PostgreSQLOrtoDatasAcquisitionUpdateOptions());
                Expect(postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLOrtoDatasAcquisitionUpdateOptions? postgreSQLOrtoDatasAcquisitionUpdateOptions_NoCounty, out string? message_NoCounty) && postgreSQLOrtoDatasAcquisitionUpdateOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(postgreSQLOrtoDatasAcquisitionUpdateOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county part", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                Expect(postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.PostgreSQLOrtoDatasAcquisitionUpdateOptions.PageSize == 100, "a refused read wrote the page size");
                Expect(postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.PostgreSQLOrtoDatasAcquisitionUpdateOptions.CountyIds is null, "a refused read wrote the county list");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLOrtoDatasAcquisitionUpdateOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLOrtoDatasAcquisitionUpdateOptions? postgreSQLOrtoDatasAcquisitionUpdateOptions_Self = PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLOrtoDatasAcquisitionUpdateOptions? postgreSQLOrtoDatasAcquisitionUpdateOptions_Foreign = PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLOrtoDatasAcquisitionUpdateOptions? postgreSQLOrtoDatasAcquisitionUpdateOptions_NotJson = PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLOrtoDatasAcquisitionUpdateOptions? postgreSQLOrtoDatasAcquisitionUpdateOptions_Several = PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLOrtoDatasAcquisitionUpdateOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLOrtoDatasAcquisitionUpdateOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLOrtoDatasAcquisitionUpdateOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLOrtoDatasAcquisitionUpdateOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the acquisition backfill window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#70; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow)}: no content");
                    return;
                }

                postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.Width - 16, postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.Height - 39);
                Size size_Min = new(postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.MinWidth - 16, postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLOrtoDatasAcquisitionUpdateOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
