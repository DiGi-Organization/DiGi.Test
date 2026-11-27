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
        /// Verifies that the settings of the BuildingModels-from-database window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#75): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>Every member of this window's options has a control, so the round trip is complete. The empty scope is the point of it: no county selected is the every-county run this window has always offered, so it exports as no county list and passes OK on read-back - unlike the windows where the scope is required.</para>
        /// </summary>
        [Fact]
        public void BuildingModelsFromDatabaseOptionsWindow_SettingsRoundTrip()
        {
            string reportDirectory = Path.GetTempPath().TrimEnd('\\', '/');

            BuildingModelsFromDatabaseOptions buildingModelsFromDatabaseOptions = new()
            {
                CountyIds = [1001, 1003],
                Resume = false,
                ReportDirectory = reportDirectory,
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_Read = null;
            BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_NoScope_Read = null;

            Exception? exception = RunSTA(() =>
            {
                BuildingModelsFromDatabaseOptionsWindow buildingModelsFromDatabaseOptionsWindow = new(buildingModelsFromDatabaseOptions, OpenBuildingImportOptionsWindowCounties(5));

                Expect(!ReferenceEquals(buildingModelsFromDatabaseOptions, buildingModelsFromDatabaseOptionsWindow.BuildingModelsFromDatabaseOptions), "the window holds the caller's options instead of a copy");

                if (buildingModelsFromDatabaseOptionsWindow.TryReadOptions(false, out BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_Exported, out string? message_Export) && buildingModelsFromDatabaseOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(buildingModelsFromDatabaseOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (BuildingModelsFromDatabaseOptionsWindow.TryReadSettings(json, out string? message_Read) is not BuildingModelsFromDatabaseOptions buildingModelsFromDatabaseOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's.
                BuildingModelsFromDatabaseOptionsWindow buildingModelsFromDatabaseOptionsWindow_Imported = new(null, OpenBuildingImportOptionsWindowCounties(5));
                buildingModelsFromDatabaseOptionsWindow_Imported.SetControls(buildingModelsFromDatabaseOptions_Imported);

                if (buildingModelsFromDatabaseOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    buildingModelsFromDatabaseOptions_Read = buildingModelsFromDatabaseOptionsWindow_Imported.BuildingModelsFromDatabaseOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(buildingModelsFromDatabaseOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // The legal empty scope: a file with no county list clears the selection of a window that had
                // one, and OK accepts it - every county is what this window's unscoped run means.
                BuildingModelsFromDatabaseOptions buildingModelsFromDatabaseOptions_NoScope = new() { CountyIds = null, Resume = false, ReportDirectory = reportDirectory };
                string? json_NoScope = Core.Convert.ToSystem_String(buildingModelsFromDatabaseOptions_NoScope);
                if (BuildingModelsFromDatabaseOptionsWindow.TryReadSettings(json_NoScope, out string? message_NoScope) is not BuildingModelsFromDatabaseOptions buildingModelsFromDatabaseOptions_NoScope_Imported)
                {
                    Expect(false, $"the no-scope file was refused: {message_NoScope}");
                    return;
                }

                BuildingModelsFromDatabaseOptionsWindow buildingModelsFromDatabaseOptionsWindow_NoScope = new(buildingModelsFromDatabaseOptions, OpenBuildingImportOptionsWindowCounties(5));
                buildingModelsFromDatabaseOptionsWindow_NoScope.SetControls(buildingModelsFromDatabaseOptions_NoScope_Imported);

                if (buildingModelsFromDatabaseOptionsWindow_NoScope.TryGetOptions(out string? message_NoScope_Ok))
                {
                    buildingModelsFromDatabaseOptions_NoScope_Read = buildingModelsFromDatabaseOptionsWindow_NoScope.BuildingModelsFromDatabaseOptions;
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
            Assert.NotNull(buildingModelsFromDatabaseOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the counties, the resume box, the report directory.
            Assert.NotNull(buildingModelsFromDatabaseOptions_Read!.CountyIds);
            Assert.True(buildingModelsFromDatabaseOptions_Read!.CountyIds!.SetEquals([1001, 1003]));
            Assert.False(buildingModelsFromDatabaseOptions_Read!.Resume);
            Assert.Equal(reportDirectory, buildingModelsFromDatabaseOptions_Read!.ReportDirectory);

            // The file with no county list cleared the selection and still passed OK - the legal every-county
            // scope of this window.
            Assert.NotNull(buildingModelsFromDatabaseOptions_NoScope_Read);
            Assert.Null(buildingModelsFromDatabaseOptions_NoScope_Read!.CountyIds);
            Assert.Equal(reportDirectory, buildingModelsFromDatabaseOptions_NoScope_Read!.ReportDirectory);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(reportDirectory, buildingModelsFromDatabaseOptions.ReportDirectory);
            Assert.NotNull(buildingModelsFromDatabaseOptions.CountyIds);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the BuildingModels-from-database window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#75): this window holds no free numbers, so an export never fails on a value - a report directory that does not exist exports as typed and is refused by OK on read-back, a blank box exports as no directory and passes OK (the launch directory), and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void BuildingModelsFromDatabaseOptionsWindow_SettingsRefusals()
        {
            const string reportDirectory = "C:/Temp/digi-buildingmodels-no-such-directory";

            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                BuildingModelsFromDatabaseOptions buildingModelsFromDatabaseOptions = new()
                {
                    CountyIds = [1001],
                    Resume = false,
                    ReportDirectory = reportDirectory,
                };

                BuildingModelsFromDatabaseOptionsWindow buildingModelsFromDatabaseOptionsWindow = new(buildingModelsFromDatabaseOptions, OpenBuildingImportOptionsWindowCounties(5));

                // A report directory that does not exist exports as typed - it is a string that could be
                // written back - and OK's existence rule refuses it on read-back.
                Expect(buildingModelsFromDatabaseOptionsWindow.TryReadOptions(false, out BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_Path, out string? message_Path) && buildingModelsFromDatabaseOptions_Path is not null, $"a nonexistent report directory was refused by the export: {message_Path}");
                Expect(buildingModelsFromDatabaseOptions_Path?.ReportDirectory == reportDirectory, "the report directory was not written as typed");
                Expect(!buildingModelsFromDatabaseOptionsWindow.TryGetOptions(out string? message_Path_Ok) && message_Path_Ok is not null && message_Path_Ok.Contains("does not exist", StringComparison.Ordinal), "a nonexistent report directory passed OK");

                // The refused read wrote nothing: the options still hold what the window was opened with.
                Expect(buildingModelsFromDatabaseOptionsWindow.BuildingModelsFromDatabaseOptions.ReportDirectory == reportDirectory, "a refused read wrote the report directory");
                Expect(buildingModelsFromDatabaseOptionsWindow.BuildingModelsFromDatabaseOptions.CountyIds is HashSet<int> countyIds_Held && countyIds_Held.SetEquals([1001]), "a refused read wrote the county list");
                Expect(!buildingModelsFromDatabaseOptionsWindow.BuildingModelsFromDatabaseOptions.Resume, "a refused read wrote the resume box");

                // A blank box exports as no directory and passes OK - blank means the directory the
                // application was launched from, which the task resolves.
                buildingModelsFromDatabaseOptionsWindow.SetControls(buildingModelsFromDatabaseOptions);
                buildingModelsFromDatabaseOptionsWindow.TextBox_ReportDirectory.Text = string.Empty;
                Expect(buildingModelsFromDatabaseOptionsWindow.TryReadOptions(false, out BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_Blank, out string? message_Blank) && buildingModelsFromDatabaseOptions_Blank?.ReportDirectory is null, $"a blank report box was not written as no directory: {message_Blank}");
                Expect(buildingModelsFromDatabaseOptionsWindow.TryGetOptions(out string? message_Blank_Ok) && message_Blank_Ok is null, $"a blank report box was refused by OK: {message_Blank_Ok}");
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

            string? json_Self = Core.Convert.ToSystem_String(new BuildingModelsFromDatabaseOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_Self = BuildingModelsFromDatabaseOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_Foreign = BuildingModelsFromDatabaseOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_NotJson = BuildingModelsFromDatabaseOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            BuildingModelsFromDatabaseOptions? buildingModelsFromDatabaseOptions_Several = BuildingModelsFromDatabaseOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(buildingModelsFromDatabaseOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(buildingModelsFromDatabaseOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(buildingModelsFromDatabaseOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(buildingModelsFromDatabaseOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }

        /// <summary>
        /// Verifies the layout of the BuildingModels-from-database window without the host, at its opening and at its minimum size: no control clipped or outside the window, no caption cut short, and the two settings buttons sharing the footer row with OK and Cancel without running into them (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#75; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The content is detached and laid out at the client size, the same harness as <see cref="SolarRadiationOptionsWindows_Layout"/>; the footer check is what the shared walk cannot see, because two controls may each lie inside the window and still overlap each other.</para>
        /// </summary>
        [Fact]
        public void BuildingModelsFromDatabaseOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                BuildingModelsFromDatabaseOptionsWindow buildingModelsFromDatabaseOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (buildingModelsFromDatabaseOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(BuildingModelsFromDatabaseOptionsWindow)}: no content");
                    return;
                }

                buildingModelsFromDatabaseOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(buildingModelsFromDatabaseOptionsWindow.Width - 16, buildingModelsFromDatabaseOptionsWindow.Height - 39);
                Size size_Min = new(buildingModelsFromDatabaseOptionsWindow.MinWidth - 16, buildingModelsFromDatabaseOptionsWindow.MinHeight - 39);

                foreach (Size size in new Size[] { size_Default, size_Min })
                {
                    root.Measure(size);
                    root.Arrange(new Rect(size));
                    root.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(BuildingModelsFromDatabaseOptionsWindow), size, root, root, failures);
                    OpenBuildingImportOptionsWindowCaptionFailures(nameof(BuildingModelsFromDatabaseOptionsWindow), size, root, root, null, null, failures);

                    Rect rect_Export = buildingModelsFromDatabaseOptionsWindow.Button_ExportSettings.TransformToAncestor(root).TransformBounds(new Rect(buildingModelsFromDatabaseOptionsWindow.Button_ExportSettings.RenderSize));
                    Rect rect_OK = buildingModelsFromDatabaseOptionsWindow.Button_OK.TransformToAncestor(root).TransformBounds(new Rect(buildingModelsFromDatabaseOptionsWindow.Button_OK.RenderSize));

                    if (rect_Export.Right > rect_OK.Left + 0.5)
                    {
                        failures.Add($"{nameof(BuildingModelsFromDatabaseOptionsWindow)} at {size}: the Export settings button ends at {rect_Export.Right:F0} px and OK starts at {rect_OK.Left:F0} px - the footer row overlaps");
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
