using DiGi.Analytical.Building.Regulation.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Energy.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.PostgreSQL.UI.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.UI.WPF.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Size = System.Windows.Size;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the heat demand run is offered in the tray (DiGi.GIS.PostgreSQL.UI#49): it is a server task - it reads and writes the database directly and needs no WebAPI manager - so it is registered under <see cref="Mode.Server"/> and absent from <see cref="Mode.Client"/>.
        /// <para>The registration, not the type, decides whether the row appears; a task added under the wrong mode builds and tests green while being invisible in the tab an operator opens.</para>
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks_UIPostgreSQLBuildingHeatDemandSolveTask()
        {
            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();

            List<IVisualBackgroundTask>? visualBackgroundTasks_Server = Create.VisualBackgroundTasks(gISPostgreSQLConverterManager, null, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_Server);
            Assert.Single(visualBackgroundTasks_Server, x => x.TypeName == typeof(UIPostgreSQLBuildingHeatDemandSolveTask).Name);

            List<IVisualBackgroundTask>? visualBackgroundTasks_Client = Create.VisualBackgroundTasks(gISPostgreSQLConverterManager, null, null, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_Client);
            Assert.DoesNotContain(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIPostgreSQLBuildingHeatDemandSolveTask).Name);
        }

        /// <summary>
        /// Verifies that the heat demand options window builds without a database or a running application, fills its controls from the options it is given and works on a copy of them (DiGi.GIS.PostgreSQL.UI#49).
        /// <para>Both pieces of a multi-part county are restored by identifier; the construction flavour, the skip, the solar radius, the batch size, the timeout, the building ceiling - unset as an empty box - and the parallelism - its automatic default as 0 - are shown as themselves. The options the window does not show (the engine options, the neighbour radius, the tile size) come through the copy unchanged, and changing what the window holds - as OK does - leaves the caller's options alone, so a cancelled dialog leaves them as they were.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingHeatDemandSolveOptionsWindow_Construction()
        {
            PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions = new()
            {
                CountyIds = [22138, 22139],
                ConstructionFlavour = ConstructionFlavour.Regulation,
                SkipCompleted = false,
                SolarRadius = 50,
                BatchSize = 25,
                CommandTimeout = 900,
                BuildingCountMax = 500,
                MaxDegreeOfParallelism = 6,
                NeighbourRadius = 20,
                TileSize = 125,
                HeatDemandOptions = new DiGi.Analytical.Building.Energy.Classes.HeatDemandOptions() { IncludeSkyRadiation = false },
            };

            List<AdministrativeAreal2DReference>? administrativeAreal2DReferences_Selected = null;
            int? constructionFlavourIndex = null;
            bool? skipCompleted = null;
            string? solarRadius = null;
            string? batchSize = null;
            string? commandTimeout = null;
            string? buildingCountMax_Set = null;
            string? maxDegreeOfParallelism_Set = null;
            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Held = null;

            int? constructionFlavourIndex_Default = null;
            bool? skipCompleted_Default = null;
            string? solarRadius_Default = null;
            string? buildingCountMax_Unset = null;
            string? maxDegreeOfParallelism_Default = null;
            int countyCount_Default = -1;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow = new(postgreSQLBuildingHeatDemandSolveOptions, SolarRadiationOptionsWindowCounties());

                administrativeAreal2DReferences_Selected = postgreSQLBuildingHeatDemandSolveOptionsWindow.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>();
                constructionFlavourIndex = postgreSQLBuildingHeatDemandSolveOptionsWindow.ComboBox_ConstructionFlavour.SelectedIndex;
                skipCompleted = postgreSQLBuildingHeatDemandSolveOptionsWindow.CheckBox_SkipCompleted.IsChecked;
                solarRadius = postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_SolarRadius.Value;
                batchSize = postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_BatchSize.Value;
                commandTimeout = postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_CommandTimeout.Value;
                buildingCountMax_Set = postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_BuildingCountMax.Value;
                maxDegreeOfParallelism_Set = postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_MaxDegreeOfParallelism.Value;

                postgreSQLBuildingHeatDemandSolveOptions_Held = postgreSQLBuildingHeatDemandSolveOptionsWindow.PostgreSQLBuildingHeatDemandSolveOptions;

                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow_Default = new(null, SolarRadiationOptionsWindowCounties());
                constructionFlavourIndex_Default = postgreSQLBuildingHeatDemandSolveOptionsWindow_Default.ComboBox_ConstructionFlavour.SelectedIndex;
                skipCompleted_Default = postgreSQLBuildingHeatDemandSolveOptionsWindow_Default.CheckBox_SkipCompleted.IsChecked;
                solarRadius_Default = postgreSQLBuildingHeatDemandSolveOptionsWindow_Default.TextBoxControl_SolarRadius.Value;
                buildingCountMax_Unset = postgreSQLBuildingHeatDemandSolveOptionsWindow_Default.TextBoxControl_BuildingCountMax.Value;
                maxDegreeOfParallelism_Default = postgreSQLBuildingHeatDemandSolveOptionsWindow_Default.TextBoxControl_MaxDegreeOfParallelism.Value;
                countyCount_Default = postgreSQLBuildingHeatDemandSolveOptionsWindow_Default.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>()?.Count ?? 0;
            });

            Assert.Null(exception);

            // Both pieces of the multi-part county are restored by identifier, and nothing else.
            Assert.NotNull(administrativeAreal2DReferences_Selected);
            Assert.Equal([22138, 22139], administrativeAreal2DReferences_Selected!.ConvertAll(x => x.Id));

            Assert.Equal(1, constructionFlavourIndex);
            Assert.False(skipCompleted);
            Assert.Equal(50.0.ToString(CultureInfo.CurrentCulture), solarRadius);
            Assert.Equal(25.ToString(CultureInfo.CurrentCulture), batchSize);
            Assert.Equal(900.ToString(CultureInfo.CurrentCulture), commandTimeout);
            Assert.Equal(500.ToString(CultureInfo.CurrentCulture), buildingCountMax_Set);
            Assert.Equal(6.ToString(CultureInfo.CurrentCulture), maxDegreeOfParallelism_Set);

            // The defaults: typical as built, skip on, the default solar radius, no ceiling as an empty box, automatic
            // parallelism as 0, and no county chosen - OK refuses that.
            PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions_Default = new();
            Assert.Equal(0, constructionFlavourIndex_Default);
            Assert.True(skipCompleted_Default);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions_Default.SolarRadius.ToString(CultureInfo.CurrentCulture), solarRadius_Default);
            Assert.Equal(string.Empty, buildingCountMax_Unset);
            Assert.Equal(0.ToString(CultureInfo.CurrentCulture), maxDegreeOfParallelism_Default);
            Assert.Equal(0, countyCount_Default);

            // A copy: what the window does not show comes through, and what it holds is not the caller's.
            Assert.NotNull(postgreSQLBuildingHeatDemandSolveOptions_Held);
            Assert.False(ReferenceEquals(postgreSQLBuildingHeatDemandSolveOptions, postgreSQLBuildingHeatDemandSolveOptions_Held));
            Assert.Equal(20, postgreSQLBuildingHeatDemandSolveOptions_Held!.NeighbourRadius);
            Assert.Equal(125, postgreSQLBuildingHeatDemandSolveOptions_Held.TileSize);
            Assert.False(postgreSQLBuildingHeatDemandSolveOptions_Held.HeatDemandOptions?.IncludeSkyRadiation);

            postgreSQLBuildingHeatDemandSolveOptions_Held.CommandTimeout = 30;
            postgreSQLBuildingHeatDemandSolveOptions_Held.BuildingCountMax = null;
            postgreSQLBuildingHeatDemandSolveOptions_Held.ConstructionFlavour = ConstructionFlavour.TypicalAsBuilt;
            postgreSQLBuildingHeatDemandSolveOptions_Held.CountyIds!.Add(55417);
            Assert.Equal(900, postgreSQLBuildingHeatDemandSolveOptions.CommandTimeout);
            Assert.Equal(500, postgreSQLBuildingHeatDemandSolveOptions.BuildingCountMax);
            Assert.Equal(ConstructionFlavour.Regulation, postgreSQLBuildingHeatDemandSolveOptions.ConstructionFlavour);
            Assert.Equal(2, postgreSQLBuildingHeatDemandSolveOptions.CountyIds!.Count);
        }

        /// <summary>
        /// Verifies the layout of the heat demand options window without the host, at its opening and at its minimum size: every named control gets a size, lies inside the window, and no caption is cut short (<c>Coding - WPF Deferred Loading.md</c> §7, DiGi.GIS.PostgreSQL.UI#49). The content is laid out on its own at the window's client size, as in <see cref="SolarRadiationOptionsWindows_Layout"/>.
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingHeatDemandSolveOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow = new(null, SolarRadiationOptionsWindowCounties());
                if (postgreSQLBuildingHeatDemandSolveOptionsWindow.Content is not FrameworkElement frameworkElement)
                {
                    failures.Add($"{nameof(PostgreSQLBuildingHeatDemandSolveOptionsWindow)}: no content");
                    return;
                }

                // Detached so it can be laid out without the window ever getting a handle.
                postgreSQLBuildingHeatDemandSolveOptionsWindow.Content = null;

                foreach ((double width, double height) in new (double, double)[] { (postgreSQLBuildingHeatDemandSolveOptionsWindow.Width, postgreSQLBuildingHeatDemandSolveOptionsWindow.Height), (postgreSQLBuildingHeatDemandSolveOptionsWindow.MinWidth, postgreSQLBuildingHeatDemandSolveOptionsWindow.MinHeight) })
                {
                    // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                    Size size = new(width - 16, height - 39);

                    frameworkElement.Measure(size);
                    frameworkElement.Arrange(new Rect(size));
                    frameworkElement.UpdateLayout();

                    SolarRadiationOptionsWindowLayoutFailures(nameof(PostgreSQLBuildingHeatDemandSolveOptionsWindow), size, frameworkElement, frameworkElement, failures);
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Verifies that the settings of the heat demand window survive the settings file (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#72): what Export reads from the controls serializes to one file, that file reads back as this window's settings, and a window opened with no options filled from it reads the same settings out again through OK's own read.
        /// <para>All eight controlled members are asserted by name, the empty ceiling and the empty scope both round-trip - an empty box is no ceiling, no county list is no scope, and OK refuses the unscoped file as it refuses a typed empty selection - and the three members the window does not show (the engine options, the neighbour radius, the tile size) run both ends: they travel in the file exactly as the exporting copy held them, and importing them leaves the importing window's own copy alone, which is what "carried through as they were" means.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingHeatDemandSolveOptionsWindow_SettingsRoundTrip()
        {
            PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions = new()
            {
                CountyIds = [55417, 4816],
                ConstructionFlavour = ConstructionFlavour.Regulation,
                SkipCompleted = false,
                SolarRadius = 50,
                BatchSize = 25,
                CommandTimeout = 900,
                BuildingCountMax = 500,
                MaxDegreeOfParallelism = 6,
            };

            List<string> failures = [];
            string? json = null;
            string? json_AfterImport = null;
            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Read = null;
            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Carry_File = null;
            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Carry_Held = null;
            bool ceilingCleared = false;
            bool refusedWithoutCounty = false;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow = new(postgreSQLBuildingHeatDemandSolveOptions, SolarRadiationOptionsWindowCounties());

                Expect(!ReferenceEquals(postgreSQLBuildingHeatDemandSolveOptions, postgreSQLBuildingHeatDemandSolveOptionsWindow.PostgreSQLBuildingHeatDemandSolveOptions), "the window holds the caller's options instead of a copy");

                if (postgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Exported, out string? message_Export) && postgreSQLBuildingHeatDemandSolveOptions_Exported is not null)
                {
                    json = Core.Convert.ToSystem_String(postgreSQLBuildingHeatDemandSolveOptions_Exported);
                }
                else
                {
                    Expect(false, $"the export was refused: {message_Export}");
                    return;
                }

                if (PostgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadSettings(json, out string? message_Read) is not PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions_Imported)
                {
                    Expect(false, $"the exported file was refused: {message_Read}");
                    return;
                }

                // Opened without options: what it ends up holding can only be the file's.
                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow_Imported = new(null, SolarRadiationOptionsWindowCounties());
                postgreSQLBuildingHeatDemandSolveOptionsWindow_Imported.SetControls(postgreSQLBuildingHeatDemandSolveOptions_Imported);

                if (postgreSQLBuildingHeatDemandSolveOptionsWindow_Imported.TryGetOptions(out string? message_Accepted))
                {
                    postgreSQLBuildingHeatDemandSolveOptions_Read = postgreSQLBuildingHeatDemandSolveOptionsWindow_Imported.PostgreSQLBuildingHeatDemandSolveOptions;
                    json_AfterImport = Core.Convert.ToSystem_String(postgreSQLBuildingHeatDemandSolveOptions_Read);
                }
                else
                {
                    Expect(false, $"the imported settings were refused by OK: {message_Accepted}");
                }

                // A file with no ceiling empties the box of a window that shows one, and OK reads it back as
                // no ceiling - "every building", not zero.
                PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions_NoCeiling = new() { CountyIds = [4816], BuildingCountMax = null };
                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow_NoCeiling = new(postgreSQLBuildingHeatDemandSolveOptions, SolarRadiationOptionsWindowCounties());
                postgreSQLBuildingHeatDemandSolveOptionsWindow_NoCeiling.SetControls(postgreSQLBuildingHeatDemandSolveOptions_NoCeiling);
                if (postgreSQLBuildingHeatDemandSolveOptionsWindow_NoCeiling.TryGetOptions(out string? message_NoCeiling))
                {
                    ceilingCleared = postgreSQLBuildingHeatDemandSolveOptionsWindow_NoCeiling.PostgreSQLBuildingHeatDemandSolveOptions.BuildingCountMax is null;
                    if (!ceilingCleared)
                    {
                        Expect(false, "the imported empty ceiling did not come back as no ceiling");
                    }
                }
                else
                {
                    Expect(false, $"the no-ceiling import was refused by OK: {message_NoCeiling}");
                }

                // A file with no county list clears the selection, and OK refuses it - the scope of this window
                // is always named, as the window docs say.
                PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions_NoScope = new() { CountyIds = null, BatchSize = 25 };
                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow_NoScope = new(postgreSQLBuildingHeatDemandSolveOptions, SolarRadiationOptionsWindowCounties());
                postgreSQLBuildingHeatDemandSolveOptionsWindow_NoScope.SetControls(postgreSQLBuildingHeatDemandSolveOptions_NoScope);
                refusedWithoutCounty = !postgreSQLBuildingHeatDemandSolveOptionsWindow_NoScope.TryGetOptions(out string? message_NoScope_Ok) && message_NoScope_Ok is not null && message_NoScope_Ok.Contains("county", StringComparison.OrdinalIgnoreCase);
                if (!refusedWithoutCounty)
                {
                    Expect(false, $"an imported empty scope passed OK: {message_NoScope_Ok}");
                }

                // The three members no control shows, both ends: they travel in the file exactly as the
                // exporting copy held them...
                PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions_Carry = new()
                {
                    CountyIds = [4816],
                    NeighbourRadius = 20,
                    TileSize = 125,
                    HeatDemandOptions = new DiGi.Analytical.Building.Energy.Classes.HeatDemandOptions() { IncludeSkyRadiation = false },
                };

                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow_Carry = new(postgreSQLBuildingHeatDemandSolveOptions_Carry, SolarRadiationOptionsWindowCounties());
                if (!postgreSQLBuildingHeatDemandSolveOptionsWindow_Carry.TryReadOptions(false, out PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Carry_Exported, out string? message_Carry_Export) || postgreSQLBuildingHeatDemandSolveOptions_Carry_Exported is null)
                {
                    Expect(false, $"the carry-through export was refused: {message_Carry_Export}");
                    return;
                }

                if (PostgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadSettings(Core.Convert.ToSystem_String(postgreSQLBuildingHeatDemandSolveOptions_Carry_Exported), out string? message_Carry_Read) is not PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions_Carry_Imported)
                {
                    Expect(false, $"the carry-through file was refused: {message_Carry_Read}");
                    return;
                }

                postgreSQLBuildingHeatDemandSolveOptions_Carry_File = postgreSQLBuildingHeatDemandSolveOptions_Carry_Imported;

                // ...and importing them leaves the importing window's own copy alone - only the controls were
                // filled, which is what "carried through as they were" means.
                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow_CarryImported = new(null, SolarRadiationOptionsWindowCounties());
                postgreSQLBuildingHeatDemandSolveOptionsWindow_CarryImported.SetControls(postgreSQLBuildingHeatDemandSolveOptions_Carry_Imported);
                if (postgreSQLBuildingHeatDemandSolveOptionsWindow_CarryImported.TryGetOptions(out string? message_Carry_Ok))
                {
                    postgreSQLBuildingHeatDemandSolveOptions_Carry_Held = postgreSQLBuildingHeatDemandSolveOptionsWindow_CarryImported.PostgreSQLBuildingHeatDemandSolveOptions;
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
            Assert.NotNull(postgreSQLBuildingHeatDemandSolveOptions_Read);

            // Every serialized value came back - the same settings, written twice.
            Assert.Equal(json, json_AfterImport);

            // And the same, named: the counties, the flavour, skip, radius, batch, timeout, ceiling, parallelism.
            Assert.NotNull(postgreSQLBuildingHeatDemandSolveOptions_Read!.CountyIds);
            Assert.True(postgreSQLBuildingHeatDemandSolveOptions_Read!.CountyIds!.SetEquals([55417, 4816]));
            Assert.Equal(ConstructionFlavour.Regulation, postgreSQLBuildingHeatDemandSolveOptions_Read!.ConstructionFlavour);
            Assert.False(postgreSQLBuildingHeatDemandSolveOptions_Read!.SkipCompleted);
            Assert.Equal(50d, postgreSQLBuildingHeatDemandSolveOptions_Read!.SolarRadius);
            Assert.Equal(25, postgreSQLBuildingHeatDemandSolveOptions_Read!.BatchSize);
            Assert.Equal(900, postgreSQLBuildingHeatDemandSolveOptions_Read!.CommandTimeout);
            Assert.Equal(500, postgreSQLBuildingHeatDemandSolveOptions_Read!.BuildingCountMax);
            Assert.Equal(6, postgreSQLBuildingHeatDemandSolveOptions_Read!.MaxDegreeOfParallelism);

            // The three members no control shows came back at the importing window's own defaults: only the
            // controls were filled.
            PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions_Defaults = new();
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions_Defaults.NeighbourRadius, postgreSQLBuildingHeatDemandSolveOptions_Read!.NeighbourRadius);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions_Defaults.TileSize, postgreSQLBuildingHeatDemandSolveOptions_Read!.TileSize);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions_Defaults.HeatDemandOptions, postgreSQLBuildingHeatDemandSolveOptions_Read!.HeatDemandOptions);

            // And they ran both ends: the file holds exactly what the exporting copy held...
            Assert.NotNull(postgreSQLBuildingHeatDemandSolveOptions_Carry_File);
            Assert.Equal(20d, postgreSQLBuildingHeatDemandSolveOptions_Carry_File!.NeighbourRadius);
            Assert.Equal(125, postgreSQLBuildingHeatDemandSolveOptions_Carry_File!.TileSize);
            Assert.False(postgreSQLBuildingHeatDemandSolveOptions_Carry_File!.HeatDemandOptions?.IncludeSkyRadiation);

            // ...and the importing window's own copy of them is untouched.
            Assert.NotNull(postgreSQLBuildingHeatDemandSolveOptions_Carry_Held);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions_Defaults.NeighbourRadius, postgreSQLBuildingHeatDemandSolveOptions_Carry_Held!.NeighbourRadius);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions_Defaults.TileSize, postgreSQLBuildingHeatDemandSolveOptions_Carry_Held!.TileSize);
            Assert.Null(postgreSQLBuildingHeatDemandSolveOptions_Carry_Held!.HeatDemandOptions);

            Assert.True(ceilingCleared);
            Assert.True(refusedWithoutCounty);

            // The window's own options are what the caller gave them: the export read writes nothing.
            Assert.Equal(900, postgreSQLBuildingHeatDemandSolveOptions.CommandTimeout);
        }

        /// <summary>
        /// Verifies the edges of the settings file of the heat demand window (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#72): an unfinished window still exports - no county, a negative radius, a batch size of zero - while a box that does not hold its number is refused with the reason OK gives, and the file read accepts only one settings object of this window.
        /// <para>The file read is called directly: the Import button opens a file dialog, which a fact cannot show (Coding - WPF Deferred Loading.md section 5).</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingHeatDemandSolveOptionsWindow_SettingsRefusals()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions = new() { CountyIds = [55417] };

                PostgreSQLBuildingHeatDemandSolveOptionsWindow postgreSQLBuildingHeatDemandSolveOptionsWindow = new(postgreSQLBuildingHeatDemandSolveOptions, SolarRadiationOptionsWindowCounties());

                // A solar radius that is not a number could not be written back as typed; NaN parses but does
                // not survive JSON; -5 is a number and is OK's to refuse on read-back.
                postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_SolarRadius.Value = "half";
                Expect(!postgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadOptions(false, out _, out string? message_Half) && message_Half is not null && message_Half.Contains("solar radius", StringComparison.OrdinalIgnoreCase), "a solar radius that is not a number was exported");

                postgreSQLBuildingHeatDemandSolveOptionsWindow.SetControls(postgreSQLBuildingHeatDemandSolveOptions);
                postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_SolarRadius.Value = "-5";
                Expect(postgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Negative, out string? message_Negative) && postgreSQLBuildingHeatDemandSolveOptions_Negative is not null, $"a negative radius was refused by the export: {message_Negative}");
                Expect(!postgreSQLBuildingHeatDemandSolveOptionsWindow.TryGetOptions(out string? message_Negative_Ok) && message_Negative_Ok is not null && message_Negative_Ok.Contains("solar radius", StringComparison.OrdinalIgnoreCase), "a negative radius passed OK");

                // A batch size of zero exports and is OK's; a batch size that is not a number is refused on
                // both paths.
                postgreSQLBuildingHeatDemandSolveOptionsWindow.SetControls(postgreSQLBuildingHeatDemandSolveOptions);
                postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_BatchSize.Value = "0";
                Expect(postgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Batch, out string? message_Batch) && postgreSQLBuildingHeatDemandSolveOptions_Batch is not null, $"a batch size of zero was refused by the export: {message_Batch}");
                Expect(!postgreSQLBuildingHeatDemandSolveOptionsWindow.TryGetOptions(out string? message_Batch_Ok) && message_Batch_Ok is not null && message_Batch_Ok.Contains("Batch size", StringComparison.Ordinal), "a batch size of zero passed OK");

                postgreSQLBuildingHeatDemandSolveOptionsWindow.SetControls(postgreSQLBuildingHeatDemandSolveOptions);
                postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_BatchSize.Value = "half";
                Expect(!postgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadOptions(false, out _, out string? message_BatchHalf) && message_BatchHalf is not null && message_BatchHalf.Contains("Batch size", StringComparison.Ordinal), "a batch size that is not a number was exported");

                // A ceiling that is not a whole number is refused on both paths; the box empties again below.
                postgreSQLBuildingHeatDemandSolveOptionsWindow.SetControls(postgreSQLBuildingHeatDemandSolveOptions);
                postgreSQLBuildingHeatDemandSolveOptionsWindow.TextBoxControl_BuildingCountMax.Value = "half";
                Expect(!postgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadOptions(false, out _, out string? message_Ceiling) && message_Ceiling is not null && message_Ceiling.Contains("Buildings per part", StringComparison.Ordinal), "a ceiling that is not a number was exported");

                // No county: written as no county list, refused by OK first of its rules - and the refused
                // reads wrote nothing.
                postgreSQLBuildingHeatDemandSolveOptionsWindow.SetControls(postgreSQLBuildingHeatDemandSolveOptions);
                postgreSQLBuildingHeatDemandSolveOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => false);
                Expect(postgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadOptions(false, out PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_NoCounty, out string? message_NoCounty) && postgreSQLBuildingHeatDemandSolveOptions_NoCounty is not null, $"an export without a county was refused: {message_NoCounty}");
                Expect(postgreSQLBuildingHeatDemandSolveOptions_NoCounty?.CountyIds is null, "an empty county selection was written as a scope");
                Expect(!postgreSQLBuildingHeatDemandSolveOptionsWindow.TryGetOptions(out string? message_NoCounty_Ok) && message_NoCounty_Ok is not null && message_NoCounty_Ok.Contains("county", StringComparison.OrdinalIgnoreCase), "an empty county selection passed OK");

                Expect(postgreSQLBuildingHeatDemandSolveOptionsWindow.PostgreSQLBuildingHeatDemandSolveOptions.BatchSize == 100, "a refused read wrote the batch size");
                Expect(postgreSQLBuildingHeatDemandSolveOptionsWindow.PostgreSQLBuildingHeatDemandSolveOptions.CountyIds is HashSet<int> countyIds_Held && countyIds_Held.SetEquals([55417]), "a refused read wrote the county list");
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

            string? json_Self = Core.Convert.ToSystem_String(new PostgreSQLBuildingHeatDemandSolveOptions());
            string? json_Foreign = Core.Convert.ToSystem_String(new PostgreSQLUpdateOccupancyOptions());
            string? json_Several = json_Self is null ? null : $"[{json_Self},{json_Self}]";

            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Self = PostgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadSettings(json_Self, out string? message_Self);
            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Foreign = PostgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadSettings(json_Foreign, out string? message_Foreign);
            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_NotJson = PostgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadSettings("this is not JSON", out string? message_NotJson);
            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Several = PostgreSQLBuildingHeatDemandSolveOptionsWindow.TryReadSettings(json_Several, out string? message_Several);

            // This window's own file: one settings object, read with no reason to refuse.
            Assert.NotNull(postgreSQLBuildingHeatDemandSolveOptions_Self);
            Assert.Null(message_Self);

            // Everything else answers no settings of this window, so the controls are never touched.
            Assert.Null(postgreSQLBuildingHeatDemandSolveOptions_Foreign);
            Assert.False(string.IsNullOrWhiteSpace(message_Foreign));

            Assert.Null(postgreSQLBuildingHeatDemandSolveOptions_NotJson);
            Assert.False(string.IsNullOrWhiteSpace(message_NotJson));

            Assert.Null(postgreSQLBuildingHeatDemandSolveOptions_Several);
            Assert.False(string.IsNullOrWhiteSpace(message_Several));
        }
    }
}
