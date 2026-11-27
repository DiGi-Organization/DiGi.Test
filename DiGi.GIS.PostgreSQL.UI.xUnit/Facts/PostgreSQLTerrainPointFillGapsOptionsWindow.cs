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
        /// Verifies that the settings of the terrain point gap window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#64): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>The import side runs in a window constructed with no options, so every controlled value asserted can only have come from the file. The members with no control - the lattice origin, the tile size, the batch and retry settings - are asserted on both ends: they travel in the file exactly as the exporting copy holds them, and an import leaves the importing window's own copy alone, which is what "carried over untouched" means.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions = new()
            {
                GridSize = 25,
                CountyIds = [1001, 1003],
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Read = null;
            PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Carry_File = null;
            PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Carry_Held = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLTerrainPointFillGapsOptionsWindow postgreSQLTerrainPointFillGapsOptionsWindow = new(postgreSQLTerrainPointFillGapsOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(postgreSQLTerrainPointFillGapsOptions, postgreSQLTerrainPointFillGapsOptionsWindow.PostgreSQLTerrainPointFillGapsOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLTerrainPointFillGapsOptionsWindow.TryReadOptions(false, out PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Exported, out string? message_Export) && postgreSQLTerrainPointFillGapsOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLTerrainPointFillGapsOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLTerrainPointFillGapsOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's, plus the carry-through
                // members both copies hold at their class defaults.
                PostgreSQLTerrainPointFillGapsOptionsWindow postgreSQLTerrainPointFillGapsOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLTerrainPointFillGapsOptionsWindow_Imported.SetControls(postgreSQLTerrainPointFillGapsOptions_Imported);

                if (postgreSQLTerrainPointFillGapsOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLTerrainPointFillGapsOptions_Read = postgreSQLTerrainPointFillGapsOptionsWindow_Imported.PostgreSQLTerrainPointFillGapsOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLTerrainPointFillGapsOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // A copy with the carry-through members changed: they have to travel in the file, and importing
                // them has to leave the window's own copy alone - the window has no control for them.
                PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions_Carry = new()
                {
                    GridSize = 50,
                    CountyIds = [1001],
                    TileSize = 256,
                    OriginX = 500000,
                    OriginY = 519000,
                };

                PostgreSQLTerrainPointFillGapsOptionsWindow postgreSQLTerrainPointFillGapsOptionsWindow_Carry = new(postgreSQLTerrainPointFillGapsOptions_Carry, OpenBuildingImportOptionsWindowCounties(5));
                if (!postgreSQLTerrainPointFillGapsOptionsWindow_Carry.TryReadOptions(false, out PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Carry_Exported, out string? message_Carry) || postgreSQLTerrainPointFillGapsOptions_Carry_Exported is null)
                {
                    Expect(false, $"the carry-through export was refused: {message_Carry}");
                    return;
                }

                if (PostgreSQLTerrainPointFillGapsOptionsWindow.TryReadSettings(Core.Convert.ToSystem_String(postgreSQLTerrainPointFillGapsOptions_Carry_Exported), out string? message_Carry_Read) is not PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions_Carry_Imported)
                {
                    Expect(false, $"the carry-through file was refused: {message_Carry_Read}");
                    return;
                }

                postgreSQLTerrainPointFillGapsOptions_Carry_File = postgreSQLTerrainPointFillGapsOptions_Carry_Imported;

                PostgreSQLTerrainPointFillGapsOptionsWindow postgreSQLTerrainPointFillGapsOptionsWindow_CarryImported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLTerrainPointFillGapsOptionsWindow_CarryImported.SetControls(postgreSQLTerrainPointFillGapsOptions_Carry_Imported);
                if (postgreSQLTerrainPointFillGapsOptionsWindow_CarryImported.TryGetOptions(out string? message_Carry_Ok))
                {
                    postgreSQLTerrainPointFillGapsOptions_Carry_Held = postgreSQLTerrainPointFillGapsOptionsWindow_CarryImported.PostgreSQLTerrainPointFillGapsOptions;
                }
                else
                {
                    Expect(false, $"the carry-through import was refused by OK: {message_Carry_Ok}");
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
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the grid size, the county subset, and the carry-through at their defaults.
            Assert.Equal(25d, postgreSQLTerrainPointFillGapsOptions_Read!.GridSize);
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions_Read!.CountyIds);
            Assert.True(postgreSQLTerrainPointFillGapsOptions_Read!.CountyIds!.SetEquals([1001, 1003]));
            Assert.Equal(1024, postgreSQLTerrainPointFillGapsOptions_Read!.BatchSize);
            Assert.Equal(128, postgreSQLTerrainPointFillGapsOptions_Read!.TileSize);

            // The changed carry-through travels in the file exactly as the exporting copy held it...
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions_Carry_File);
            Assert.Equal(256, postgreSQLTerrainPointFillGapsOptions_Carry_File!.TileSize);
            Assert.Equal(500000d, postgreSQLTerrainPointFillGapsOptions_Carry_File!.OriginX);
            Assert.Equal(519000d, postgreSQLTerrainPointFillGapsOptions_Carry_File!.OriginY);

            // ...and an import leaves the window's own copy of them alone: only the controls were filled.
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions_Carry_Held);
            Assert.Equal(128, postgreSQLTerrainPointFillGapsOptions_Carry_Held!.TileSize);
            Assert.Equal(0d, postgreSQLTerrainPointFillGapsOptions_Carry_Held!.OriginX);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(25d, postgreSQLTerrainPointFillGapsOptions.GridSize);
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions.CountyIds);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the terrain point gap window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#64): an unfinished window still exports - a grid size of zero or a missing county written as it stands - while a grid size box that does not hold a finite number is refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLTerrainPointFillGapsOptionsWindow postgreSQLTerrainPointFillGapsOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(5));

                // A grid size that is not a number could not be written back as typed.
                postgreSQLTerrainPointFillGapsOptionsWindow.TextBoxControl_GridSize.Value = "half";
                Expect(!postgreSQLTerrainPointFillGapsOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("Grid size", StringComparison.Ordinal), "a grid size that is not a number was exported");
                postgreSQLTerrainPointFillGapsOptionsWindow.SetControls(new PostgreSQLTerrainPointFillGapsOptions());

                // NaN parses as a number but is not one that survives JSON.
                postgreSQLTerrainPointFillGapsOptionsWindow.TextBoxControl_GridSize.Value = "NaN";
                Expect(!postgreSQLTerrainPointFillGapsOptionsWindow.TryReadOptions(false, out _, out string? message_NaN) && message_NaN is not null, "a grid size of NaN was exported");
                postgreSQLTerrainPointFillGapsOptionsWindow.SetControls(new PostgreSQLTerrainPointFillGapsOptions());

                // A grid size of zero is a number: the export takes it and OK's range rule refuses it on read-back.
                postgreSQLTerrainPointFillGapsOptionsWindow.TextBoxControl_GridSize.Value = "0";
                Expect(postgreSQLTerrainPointFillGapsOptionsWindow.TryReadOptions(false, out PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Zero, out string? message_Zero) && postgreSQLTerrainPointFillGapsOptions_Zero is not null, $"a grid size of zero was refused by the export: {message_Zero}");
                Expect(!postgreSQLTerrainPointFillGapsOptionsWindow.TryGetOptions(out string? message_Zero_Ok) && message_Zero_Ok is not null && message_Zero_Ok.Contains("Grid size", StringComparison.Ordinal), "a grid size of zero passed OK");

                // No county: written as no county list, refused by OK - and the refused reads wrote nothing.
                postgreSQLTerrainPointFillGapsOptionsWindow.SetControls(new PostgreSQLTerrainPointFillGapsOptions());
                Expect(postgreSQLTerrainPointFillGapsOptionsWindow.TryReadOptions(false, out PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_NoCounty, out string? message_NoCounty) && postgreSQLTerrainPointFillGapsOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(postgreSQLTerrainPointFillGapsOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!postgreSQLTerrainPointFillGapsOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                // A refused read left the window's options as they were: the default grid size, no county.
                Expect(postgreSQLTerrainPointFillGapsOptionsWindow.PostgreSQLTerrainPointFillGapsOptions.GridSize == 100, "a refused read wrote the grid size");
                Expect(postgreSQLTerrainPointFillGapsOptionsWindow.PostgreSQLTerrainPointFillGapsOptions.CountyIds is null, "a refused read wrote the county list");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLTerrainPointFillGapsOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Self = PostgreSQLTerrainPointFillGapsOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Foreign = PostgreSQLTerrainPointFillGapsOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_NotJson = PostgreSQLTerrainPointFillGapsOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Several = PostgreSQLTerrainPointFillGapsOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLTerrainPointFillGapsOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLTerrainPointFillGapsOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLTerrainPointFillGapsOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the terrain point gap window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#64; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other. The density label and progress bar are part of the walk - the label is empty at this point, which is its own state, and the progress bar is collapsed, which the walk skips.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLTerrainPointFillGapsOptionsWindow postgreSQLTerrainPointFillGapsOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (postgreSQLTerrainPointFillGapsOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(PostgreSQLTerrainPointFillGapsOptionsWindow)}: no content");
                    return;
                }

                postgreSQLTerrainPointFillGapsOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(postgreSQLTerrainPointFillGapsOptionsWindow.Width - 16, postgreSQLTerrainPointFillGapsOptionsWindow.Height - 39);
                Size size_Min = new(postgreSQLTerrainPointFillGapsOptionsWindow.MinWidth - 16, postgreSQLTerrainPointFillGapsOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLTerrainPointFillGapsOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(PostgreSQLTerrainPointFillGapsOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = postgreSQLTerrainPointFillGapsOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLTerrainPointFillGapsOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = postgreSQLTerrainPointFillGapsOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLTerrainPointFillGapsOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(PostgreSQLTerrainPointFillGapsOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
