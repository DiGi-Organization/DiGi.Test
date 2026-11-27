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
        /// Verifies that the settings of the terrain point window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#65): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>The import side runs in a window constructed with no options, so every controlled value asserted can only have come from the file. The members with no control - the grid origin, the tile size, the batch and retry settings - are asserted on both ends: they travel in the file exactly as the exporting copy holds them, and an import leaves the importing window's own copy alone, which is what "carried over untouched" means.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointUpdateOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions = new()
            {
                GridSize = 25,
                OverrideExisting = true,
                CountyIds = [1001, 1003],
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Read = null;
            PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Carry_File = null;
            PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Carry_Held = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLTerrainPointUpdateOptionsWindow postgreSQLTerrainPointUpdateOptionsWindow = new(postgreSQLTerrainPointUpdateOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(postgreSQLTerrainPointUpdateOptions, postgreSQLTerrainPointUpdateOptionsWindow.PostgreSQLTerrainPointUpdateOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLTerrainPointUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Exported, out string? message_Export) && postgreSQLTerrainPointUpdateOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLTerrainPointUpdateOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLTerrainPointUpdateOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's, plus the carry-through
                // members both copies hold at their class defaults.
                PostgreSQLTerrainPointUpdateOptionsWindow postgreSQLTerrainPointUpdateOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLTerrainPointUpdateOptionsWindow_Imported.SetControls(postgreSQLTerrainPointUpdateOptions_Imported);

                if (postgreSQLTerrainPointUpdateOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLTerrainPointUpdateOptions_Read = postgreSQLTerrainPointUpdateOptionsWindow_Imported.PostgreSQLTerrainPointUpdateOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLTerrainPointUpdateOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // A copy with the carry-through members changed: they have to travel in the file, and importing
                // them has to leave the window's own copy alone - the window has no control for them.
                PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions_Carry = new()
                {
                    GridSize = 50,
                    OverrideExisting = true,
                    CountyIds = [1001],
                    TileSize = 256,
                    OriginX = 500000,
                    OriginY = 519000,
                };

                PostgreSQLTerrainPointUpdateOptionsWindow postgreSQLTerrainPointUpdateOptionsWindow_Carry = new(postgreSQLTerrainPointUpdateOptions_Carry, OpenBuildingImportOptionsWindowCounties(5));
                if (!postgreSQLTerrainPointUpdateOptionsWindow_Carry.TryReadOptions(false, out PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Carry_Exported, out string? message_Carry) || postgreSQLTerrainPointUpdateOptions_Carry_Exported is null)
                {
                    Expect(false, $"the carry-through export was refused: {message_Carry}");
                    return;
                }

                if (PostgreSQLTerrainPointUpdateOptionsWindow.TryReadSettings(Core.Convert.ToSystem_String(postgreSQLTerrainPointUpdateOptions_Carry_Exported), out string? message_Carry_Read) is not PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions_Carry_Imported)
                {
                    Expect(false, $"the carry-through file was refused: {message_Carry_Read}");
                    return;
                }

                postgreSQLTerrainPointUpdateOptions_Carry_File = postgreSQLTerrainPointUpdateOptions_Carry_Imported;

                PostgreSQLTerrainPointUpdateOptionsWindow postgreSQLTerrainPointUpdateOptionsWindow_CarryImported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLTerrainPointUpdateOptionsWindow_CarryImported.SetControls(postgreSQLTerrainPointUpdateOptions_Carry_Imported);
                if (postgreSQLTerrainPointUpdateOptionsWindow_CarryImported.TryGetOptions(out string? message_Carry_Ok))
                {
                    postgreSQLTerrainPointUpdateOptions_Carry_Held = postgreSQLTerrainPointUpdateOptionsWindow_CarryImported.PostgreSQLTerrainPointUpdateOptions;
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
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the grid size, the override box, the county subset, the carry-through defaults.
            Assert.Equal(25d, postgreSQLTerrainPointUpdateOptions_Read!.GridSize);
            Assert.True(postgreSQLTerrainPointUpdateOptions_Read!.OverrideExisting);
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions_Read!.CountyIds);
            Assert.True(postgreSQLTerrainPointUpdateOptions_Read!.CountyIds!.SetEquals([1001, 1003]));
            Assert.Equal(128, postgreSQLTerrainPointUpdateOptions_Read!.TileSize);
            Assert.Equal(0d, postgreSQLTerrainPointUpdateOptions_Read!.OriginX);

            // The changed carry-through travels in the file exactly as the exporting copy held it...
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions_Carry_File);
            Assert.Equal(256, postgreSQLTerrainPointUpdateOptions_Carry_File!.TileSize);
            Assert.Equal(500000d, postgreSQLTerrainPointUpdateOptions_Carry_File!.OriginX);
            Assert.Equal(519000d, postgreSQLTerrainPointUpdateOptions_Carry_File!.OriginY);

            // ...and an import leaves the window's own copy of them alone: only the controls were filled.
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions_Carry_Held);
            Assert.Equal(128, postgreSQLTerrainPointUpdateOptions_Carry_Held!.TileSize);
            Assert.Equal(0d, postgreSQLTerrainPointUpdateOptions_Carry_Held!.OriginX);
            Assert.Equal(50d, postgreSQLTerrainPointUpdateOptions_Carry_Held!.GridSize);
            Assert.True(postgreSQLTerrainPointUpdateOptions_Carry_Held!.OverrideExisting);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(25d, postgreSQLTerrainPointUpdateOptions.GridSize);
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions.CountyIds);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the terrain point window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#65): an unfinished window still exports - a grid size of zero or a missing county written as it stands - while a grid size box that does not hold a finite number is refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointUpdateOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLTerrainPointUpdateOptionsWindow postgreSQLTerrainPointUpdateOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(5));

                // A grid size that is not a number could not be written back as typed.
                postgreSQLTerrainPointUpdateOptionsWindow.TextBoxControl_GridSize.Value = "half";
                Expect(!postgreSQLTerrainPointUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("Grid size", StringComparison.Ordinal), "a grid size that is not a number was exported");
                postgreSQLTerrainPointUpdateOptionsWindow.SetControls(new PostgreSQLTerrainPointUpdateOptions());

                // NaN parses as a number but is not one that survives JSON.
                postgreSQLTerrainPointUpdateOptionsWindow.TextBoxControl_GridSize.Value = "NaN";
                Expect(!postgreSQLTerrainPointUpdateOptionsWindow.TryReadOptions(false, out _, out string? message_NaN) && message_NaN is not null, "a grid size of NaN was exported");
                postgreSQLTerrainPointUpdateOptionsWindow.SetControls(new PostgreSQLTerrainPointUpdateOptions());

                // A grid size of zero is a number: the export takes it and OK's range rule refuses it on read-back.
                postgreSQLTerrainPointUpdateOptionsWindow.TextBoxControl_GridSize.Value = "0";
                Expect(postgreSQLTerrainPointUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Zero, out string? message_Zero) && postgreSQLTerrainPointUpdateOptions_Zero is not null, $"a grid size of zero was refused by the export: {message_Zero}");
                Expect(!postgreSQLTerrainPointUpdateOptionsWindow.TryGetOptions(out string? message_Zero_Ok) && message_Zero_Ok is not null && message_Zero_Ok.Contains("Grid size", StringComparison.Ordinal), "a grid size of zero passed OK");

                // No county: written as no county list, refused by OK - and the refused reads wrote nothing.
                postgreSQLTerrainPointUpdateOptionsWindow.SetControls(new PostgreSQLTerrainPointUpdateOptions());
                Expect(postgreSQLTerrainPointUpdateOptionsWindow.TryReadOptions(false, out PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_NoCounty, out string? message_NoCounty) && postgreSQLTerrainPointUpdateOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(postgreSQLTerrainPointUpdateOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!postgreSQLTerrainPointUpdateOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                Expect(postgreSQLTerrainPointUpdateOptionsWindow.PostgreSQLTerrainPointUpdateOptions.GridSize == 50, "a refused read wrote the grid size");
                Expect(!postgreSQLTerrainPointUpdateOptionsWindow.PostgreSQLTerrainPointUpdateOptions.OverrideExisting, "a refused read wrote the override box");
                Expect(postgreSQLTerrainPointUpdateOptionsWindow.PostgreSQLTerrainPointUpdateOptions.CountyIds is null, "a refused read wrote the county list");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLTerrainPointUpdateOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Self = PostgreSQLTerrainPointUpdateOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Foreign = PostgreSQLTerrainPointUpdateOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_NotJson = PostgreSQLTerrainPointUpdateOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Several = PostgreSQLTerrainPointUpdateOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLTerrainPointUpdateOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLTerrainPointUpdateOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLTerrainPointUpdateOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the terrain point window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#65; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointUpdateOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLTerrainPointUpdateOptionsWindow postgreSQLTerrainPointUpdateOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (postgreSQLTerrainPointUpdateOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(PostgreSQLTerrainPointUpdateOptionsWindow)}: no content");
                    return;
                }

                postgreSQLTerrainPointUpdateOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(postgreSQLTerrainPointUpdateOptionsWindow.Width - 16, postgreSQLTerrainPointUpdateOptionsWindow.Height - 39);
                Size size_Min = new(postgreSQLTerrainPointUpdateOptionsWindow.MinWidth - 16, postgreSQLTerrainPointUpdateOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLTerrainPointUpdateOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(PostgreSQLTerrainPointUpdateOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = postgreSQLTerrainPointUpdateOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLTerrainPointUpdateOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = postgreSQLTerrainPointUpdateOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLTerrainPointUpdateOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(PostgreSQLTerrainPointUpdateOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
