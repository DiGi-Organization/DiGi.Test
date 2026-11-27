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
        /// Verifies that the settings of the occupancy window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#63): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>The scope round trip runs both ways - a subset of counties, and no county list at all: the file without a list clears the selection of a window that had one, because an empty selection is this window's every-county state and an empty set would mean "none". The import side runs in windows constructed without options, so every value asserted can only have come from the file.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLUpdateOccupancyOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLUpdateOccupancyOptions postgreSQLUpdateOccupancyOptions = new()
            {
                IncludeAdministrativeAreal2Ds = true,
                IncludeBuilding2Ds = false,
                Clear = true,
                CountyIds = [1001, 1003],
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_Read = null;
            PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_NoScope_Read = null;
            bool selectionCleared = false;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLUpdateOccupancyOptionsWindow postgreSQLUpdateOccupancyOptionsWindow = new(postgreSQLUpdateOccupancyOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(postgreSQLUpdateOccupancyOptions, postgreSQLUpdateOccupancyOptionsWindow.PostgreSQLUpdateOccupancyOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLUpdateOccupancyOptionsWindow.TryReadOptions(false, out PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_Exported, out string? message_Export) && postgreSQLUpdateOccupancyOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLUpdateOccupancyOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLUpdateOccupancyOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLUpdateOccupancyOptions postgreSQLUpdateOccupancyOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's.
                PostgreSQLUpdateOccupancyOptionsWindow postgreSQLUpdateOccupancyOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLUpdateOccupancyOptionsWindow_Imported.SetControls(postgreSQLUpdateOccupancyOptions_Imported);

                if (postgreSQLUpdateOccupancyOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLUpdateOccupancyOptions_Read = postgreSQLUpdateOccupancyOptionsWindow_Imported.PostgreSQLUpdateOccupancyOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLUpdateOccupancyOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // The no-scope side: a window with neither side written and no county list still exports, and
                // importing that file into a window that holds a selection clears it.
                PostgreSQLUpdateOccupancyOptions postgreSQLUpdateOccupancyOptions_NoScope = new()
                {
                    IncludeAdministrativeAreal2Ds = false,
                    IncludeBuilding2Ds = true,
                };

                PostgreSQLUpdateOccupancyOptionsWindow postgreSQLUpdateOccupancyOptionsWindow_NoScope = new(postgreSQLUpdateOccupancyOptions_NoScope, OpenBuildingImportOptionsWindowCounties(5));
                if (!postgreSQLUpdateOccupancyOptionsWindow_NoScope.TryReadOptions(false, out PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_NoScope_Exported, out string? message_NoScope) || postgreSQLUpdateOccupancyOptions_NoScope_Exported is null)
                {
                    Expect(false, $"the no-scope export was refused: {message_NoScope}");
                    return;
                }

                Expect(postgreSQLUpdateOccupancyOptions_NoScope_Exported.CountyIds is null, "an empty county selection was written as a scope");

                if (PostgreSQLUpdateOccupancyOptionsWindow.TryReadSettings(Core.Convert.ToSystem_String(postgreSQLUpdateOccupancyOptions_NoScope_Exported), out string? message_NoScope_Read) is not PostgreSQLUpdateOccupancyOptions postgreSQLUpdateOccupancyOptions_NoScope_Imported)
                {
                    Expect(false, $"the no-scope file was refused: {message_NoScope_Read}");
                    return;
                }

                // The window holds the scoped selection right now; the import has to clear it.
                PostgreSQLUpdateOccupancyOptionsWindow postgreSQLUpdateOccupancyOptionsWindow_Selected = new(postgreSQLUpdateOccupancyOptions, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLUpdateOccupancyOptionsWindow_Selected.SetControls(postgreSQLUpdateOccupancyOptions_NoScope_Imported);

                if (postgreSQLUpdateOccupancyOptionsWindow_Selected.TryGetOptions(out string? message_NoScope_Ok))
                {
                    postgreSQLUpdateOccupancyOptions_NoScope_Read = postgreSQLUpdateOccupancyOptionsWindow_Selected.PostgreSQLUpdateOccupancyOptions;
                    selectionCleared = (postgreSQLUpdateOccupancyOptionsWindow_Selected.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>() ?? []).Count == 0;
                }
                else
                {
                    Expect(false, $"the no-scope import was refused by OK: {message_NoScope_Ok}");
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
            Assert.NotNull(postgreSQLUpdateOccupancyOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: both sides, clear-first, the county subset.
            Assert.True(postgreSQLUpdateOccupancyOptions_Read!.IncludeAdministrativeAreal2Ds);
            Assert.False(postgreSQLUpdateOccupancyOptions_Read!.IncludeBuilding2Ds);
            Assert.True(postgreSQLUpdateOccupancyOptions_Read!.Clear);
            Assert.NotNull(postgreSQLUpdateOccupancyOptions_Read!.CountyIds);
            Assert.True(postgreSQLUpdateOccupancyOptions_Read!.CountyIds!.SetEquals([1001, 1003]));

            // The file with no county list cleared the selection instead of leaving the old scope behind.
            Assert.NotNull(postgreSQLUpdateOccupancyOptions_NoScope_Read);
            Assert.Null(postgreSQLUpdateOccupancyOptions_NoScope_Read!.CountyIds);
            Assert.False(postgreSQLUpdateOccupancyOptions_NoScope_Read!.IncludeAdministrativeAreal2Ds);
            Assert.True(postgreSQLUpdateOccupancyOptions_NoScope_Read!.IncludeBuilding2Ds);
            Assert.True(selectionCleared);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.True(postgreSQLUpdateOccupancyOptions.Clear);
            Assert.NotNull(postgreSQLUpdateOccupancyOptions.CountyIds);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the occupancy window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#63): a window with neither side checked still exports - the gate is OK's, and this window holds no free numbers - while OK refuses the same window on read-back, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLUpdateOccupancyOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLUpdateOccupancyOptionsWindow postgreSQLUpdateOccupancyOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(5));

                // Neither side checked: an unfinished window is saved as it stands - nothing here could not be
                // written back as typed - and OK is what refuses it when the file is read back.
                postgreSQLUpdateOccupancyOptionsWindow.CheckBox_IncludeAdministrativeAreal2Ds.IsChecked = false;
                postgreSQLUpdateOccupancyOptionsWindow.CheckBox_IncludeBuilding2Ds.IsChecked = false;

                Expect(postgreSQLUpdateOccupancyOptionsWindow.TryReadOptions(false, out PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_Neither, out string? message_Neither) && postgreSQLUpdateOccupancyOptions_Neither is not null, $"an export with neither side checked was refused: {message_Neither}");
                Expect(postgreSQLUpdateOccupancyOptions_Neither is not null && !postgreSQLUpdateOccupancyOptions_Neither.IncludeAdministrativeAreal2Ds && !postgreSQLUpdateOccupancyOptions_Neither.IncludeBuilding2Ds, "the unchecked sides were not written as unchecked");
                Expect(!postgreSQLUpdateOccupancyOptionsWindow.TryGetOptions(out string? message_Neither_Ok) && message_Neither_Ok is not null && message_Neither_Ok.Contains("side", StringComparison.OrdinalIgnoreCase), "a window with neither side checked passed OK");

                // The refused read wrote nothing: the options still hold both sides as the window opened them,
                // not the two boxes now unchecked.
                Expect(postgreSQLUpdateOccupancyOptionsWindow.PostgreSQLUpdateOccupancyOptions.IncludeAdministrativeAreal2Ds && postgreSQLUpdateOccupancyOptionsWindow.PostgreSQLUpdateOccupancyOptions.IncludeBuilding2Ds, "a refused read wrote the sides");

                // One side checked and no county: OK accepts - no scope is this window's every-county state.
                postgreSQLUpdateOccupancyOptionsWindow.CheckBox_IncludeBuilding2Ds.IsChecked = true;
                Expect(postgreSQLUpdateOccupancyOptionsWindow.TryGetOptions(out string? message_OneSide) && message_OneSide is null, $"one side checked with no county was refused by OK: {message_OneSide}");
                Expect(postgreSQLUpdateOccupancyOptionsWindow.PostgreSQLUpdateOccupancyOptions.CountyIds is null, "an empty county selection was stored as a scope");
                Expect(!postgreSQLUpdateOccupancyOptionsWindow.PostgreSQLUpdateOccupancyOptions.IncludeAdministrativeAreal2Ds && postgreSQLUpdateOccupancyOptionsWindow.PostgreSQLUpdateOccupancyOptions.IncludeBuilding2Ds, "the accepted read did not store the sides");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLOrtoSheetUpdateOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_Self = PostgreSQLUpdateOccupancyOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_Foreign = PostgreSQLUpdateOccupancyOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_NotJson = PostgreSQLUpdateOccupancyOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLUpdateOccupancyOptions? postgreSQLUpdateOccupancyOptions_Several = PostgreSQLUpdateOccupancyOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLUpdateOccupancyOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLUpdateOccupancyOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLUpdateOccupancyOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLUpdateOccupancyOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the occupancy window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#63; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLUpdateOccupancyOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLUpdateOccupancyOptionsWindow postgreSQLUpdateOccupancyOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (postgreSQLUpdateOccupancyOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(PostgreSQLUpdateOccupancyOptionsWindow)}: no content");
                    return;
                }

                postgreSQLUpdateOccupancyOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(postgreSQLUpdateOccupancyOptionsWindow.Width - 16, postgreSQLUpdateOccupancyOptionsWindow.Height - 39);
                Size size_Min = new(postgreSQLUpdateOccupancyOptionsWindow.MinWidth - 16, postgreSQLUpdateOccupancyOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLUpdateOccupancyOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(PostgreSQLUpdateOccupancyOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = postgreSQLUpdateOccupancyOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLUpdateOccupancyOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = postgreSQLUpdateOccupancyOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLUpdateOccupancyOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(PostgreSQLUpdateOccupancyOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
