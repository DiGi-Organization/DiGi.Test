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
    }
}
