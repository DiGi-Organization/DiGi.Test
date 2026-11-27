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
        /// Verifies that the settings of the Building2D refresh window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#69): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>The empty scope is the point of this one: unlike the external components window, no county selected here is legal - the refresh's own every-county scope - so it exports as no county list, imports as an empty selection, and passes OK. The members with no control, the keyset anchor and the tolerance, travel in the file as the exporting copy held them.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuilding2DRefreshOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLBuilding2DRefreshOptions postgreSQLBuilding2DRefreshOptions = new()
            {
                BatchSize = 750,
                OverrideExistingSubdivisionIds = true,
                NestedSubdivisionsOnly = true,
                CountyIds = [1001, 1003],
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_Read = null;
            PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_NoScope_Read = null;
            bool selectionCleared = false;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuilding2DRefreshOptionsWindow postgreSQLBuilding2DRefreshOptionsWindow = new(postgreSQLBuilding2DRefreshOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(postgreSQLBuilding2DRefreshOptions, postgreSQLBuilding2DRefreshOptionsWindow.PostgreSQLBuilding2DRefreshOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLBuilding2DRefreshOptionsWindow.TryReadOptions(false, out PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_Exported, out string? message_Export) && postgreSQLBuilding2DRefreshOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLBuilding2DRefreshOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLBuilding2DRefreshOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLBuilding2DRefreshOptions postgreSQLBuilding2DRefreshOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's, plus the carry-through
                // members both copies hold at their class defaults.
                PostgreSQLBuilding2DRefreshOptionsWindow postgreSQLBuilding2DRefreshOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLBuilding2DRefreshOptionsWindow_Imported.SetControls(postgreSQLBuilding2DRefreshOptions_Imported);

                if (postgreSQLBuilding2DRefreshOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLBuilding2DRefreshOptions_Read = postgreSQLBuilding2DRefreshOptionsWindow_Imported.PostgreSQLBuilding2DRefreshOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLBuilding2DRefreshOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // The empty scope, both ways: a window with nothing selected exports it as no county list, and
                // importing that file clears the selection of a window that had one - and OK accepts it, because
                // the empty scope is this window's own.
                PostgreSQLBuilding2DRefreshOptions postgreSQLBuilding2DRefreshOptions_NoScope = new() { BatchSize = 750 };

                PostgreSQLBuilding2DRefreshOptionsWindow postgreSQLBuilding2DRefreshOptionsWindow_NoScope = new(postgreSQLBuilding2DRefreshOptions_NoScope, OpenBuildingImportOptionsWindowCounties(5));
                if (!postgreSQLBuilding2DRefreshOptionsWindow_NoScope.TryReadOptions(false, out PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_NoScope_Exported, out string? message_NoScope) || postgreSQLBuilding2DRefreshOptions_NoScope_Exported is null)
                {
                    Expect(false, $"the no-scope export was refused: {message_NoScope}");
                    return;
                }

                Expect(postgreSQLBuilding2DRefreshOptions_NoScope_Exported.CountyIds is null, "an empty county selection was written as a scope");

                if (PostgreSQLBuilding2DRefreshOptionsWindow.TryReadSettings(Core.Convert.ToSystem_String(postgreSQLBuilding2DRefreshOptions_NoScope_Exported), out string? message_NoScope_Read) is not PostgreSQLBuilding2DRefreshOptions postgreSQLBuilding2DRefreshOptions_NoScope_Imported)
                {
                    Expect(false, $"the no-scope file was refused: {message_NoScope_Read}");
                    return;
                }

                PostgreSQLBuilding2DRefreshOptionsWindow postgreSQLBuilding2DRefreshOptionsWindow_Selected = new(postgreSQLBuilding2DRefreshOptions, OpenBuildingImportOptionsWindowCounties(5));
                postgreSQLBuilding2DRefreshOptionsWindow_Selected.SetControls(postgreSQLBuilding2DRefreshOptions_NoScope_Imported);

                if (postgreSQLBuilding2DRefreshOptionsWindow_Selected.TryGetOptions(out string? message_NoScope_Ok))
                {
                    postgreSQLBuilding2DRefreshOptions_NoScope_Read = postgreSQLBuilding2DRefreshOptionsWindow_Selected.PostgreSQLBuilding2DRefreshOptions;
                    selectionCleared = (postgreSQLBuilding2DRefreshOptionsWindow_Selected.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>() ?? []).Count == 0;
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
            Assert.NotNull(postgreSQLBuilding2DRefreshOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the batch size, both boxes, the county subset - and the two carry-through
            // members at their defaults.
            Assert.Equal(750, postgreSQLBuilding2DRefreshOptions_Read!.BatchSize);
            Assert.True(postgreSQLBuilding2DRefreshOptions_Read!.OverrideExistingSubdivisionIds);
            Assert.True(postgreSQLBuilding2DRefreshOptions_Read!.NestedSubdivisionsOnly);
            Assert.NotNull(postgreSQLBuilding2DRefreshOptions_Read!.CountyIds);
            Assert.True(postgreSQLBuilding2DRefreshOptions_Read!.CountyIds!.SetEquals([1001, 1003]));
            Assert.Equal(0, postgreSQLBuilding2DRefreshOptions_Read!.StartId);
            Assert.Equal(Core.Constants.Tolerance.MacroDistance, postgreSQLBuilding2DRefreshOptions_Read!.Tolerance);

            // The file with no county list cleared the selection and still passed OK - the legal empty scope.
            Assert.NotNull(postgreSQLBuilding2DRefreshOptions_NoScope_Read);
            Assert.Null(postgreSQLBuilding2DRefreshOptions_NoScope_Read!.CountyIds);
            Assert.Equal(750, postgreSQLBuilding2DRefreshOptions_NoScope_Read!.BatchSize);
            Assert.True(selectionCleared);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(750, postgreSQLBuilding2DRefreshOptions.BatchSize);
            Assert.NotNull(postgreSQLBuilding2DRefreshOptions.CountyIds);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the Building2D refresh window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#69): a batch size box that does not hold a whole number is refused with the reason OK gives, while a batch size of zero exports to be refused by OK on read-back, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuilding2DRefreshOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuilding2DRefreshOptionsWindow postgreSQLBuilding2DRefreshOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(5));

                // A batch size that is not a whole number could not be written back as typed.
                postgreSQLBuilding2DRefreshOptionsWindow.TextBoxControl_BatchSize.Value = "half";
                Expect(!postgreSQLBuilding2DRefreshOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("Batch size", StringComparison.Ordinal), "a batch size that is not a number was exported");

                // A batch size of zero is a whole number: the export takes it and OK's range rule refuses it.
                postgreSQLBuilding2DRefreshOptionsWindow.SetControls(new PostgreSQLBuilding2DRefreshOptions());
                postgreSQLBuilding2DRefreshOptionsWindow.TextBoxControl_BatchSize.Value = "0";
                Expect(postgreSQLBuilding2DRefreshOptionsWindow.TryReadOptions(false, out PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_Zero, out string? message_Zero) && postgreSQLBuilding2DRefreshOptions_Zero is not null, $"a batch size of zero was refused by the export: {message_Zero}");
                Expect(!postgreSQLBuilding2DRefreshOptionsWindow.TryGetOptions(out string? message_Zero_Ok) && message_Zero_Ok is not null && message_Zero_Ok.Contains("Batch size", StringComparison.Ordinal), "a batch size of zero passed OK");

                // The refused reads wrote nothing: the options still hold the defaults they opened with.
                Expect(postgreSQLBuilding2DRefreshOptionsWindow.PostgreSQLBuilding2DRefreshOptions.BatchSize == 500, "a refused read wrote the batch size");
                Expect(postgreSQLBuilding2DRefreshOptionsWindow.PostgreSQLBuilding2DRefreshOptions.CountyIds is null, "a refused read wrote the county list");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLBuilding2DRefreshOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_Self = PostgreSQLBuilding2DRefreshOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_Foreign = PostgreSQLBuilding2DRefreshOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_NotJson = PostgreSQLBuilding2DRefreshOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLBuilding2DRefreshOptions? postgreSQLBuilding2DRefreshOptions_Several = PostgreSQLBuilding2DRefreshOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLBuilding2DRefreshOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLBuilding2DRefreshOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLBuilding2DRefreshOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLBuilding2DRefreshOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the Building2D refresh window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#69; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuilding2DRefreshOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuilding2DRefreshOptionsWindow postgreSQLBuilding2DRefreshOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (postgreSQLBuilding2DRefreshOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(PostgreSQLBuilding2DRefreshOptionsWindow)}: no content");
                    return;
                }

                postgreSQLBuilding2DRefreshOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(postgreSQLBuilding2DRefreshOptionsWindow.Width - 16, postgreSQLBuilding2DRefreshOptionsWindow.Height - 39);
                Size size_Min = new(postgreSQLBuilding2DRefreshOptionsWindow.MinWidth - 16, postgreSQLBuilding2DRefreshOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLBuilding2DRefreshOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(PostgreSQLBuilding2DRefreshOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = postgreSQLBuilding2DRefreshOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLBuilding2DRefreshOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = postgreSQLBuilding2DRefreshOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(postgreSQLBuilding2DRefreshOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(PostgreSQLBuilding2DRefreshOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
