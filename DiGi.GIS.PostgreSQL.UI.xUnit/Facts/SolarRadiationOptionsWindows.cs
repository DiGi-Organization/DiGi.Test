using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.Solar.Classes;
using DiGi.GIS.PostgreSQL.UI.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Size = System.Windows.Size;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the solar radiation solve options window builds without a database or a running application, fills its controls from the options it is given and works on a copy of them (DiGi.GIS.PostgreSQL.UI#44).
        /// <para>The window is the only way a solve is scoped, and an unscoped solve is the whole country once per radius. A multi-part county is included because its pieces are told apart only by the identifier the naming callback appends; radius 0 because it is the isolated building, the reference every shortfall is measured against.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationSolveOptionsWindow_Construction()
        {
            PostgreSQLBuildingSolarRadiationSolveOptions postgreSQLBuildingSolarRadiationSolveOptions = new()
            {
                CountyIds = [22138, 22139],
                Radii = [100, 0, 50, 50, -5, double.NaN],
                SkipExisting = false,
                BatchSize = 25,
                TileSize = 125,
                ReceiverCountMax = 300,
                CasterTriangleCountMax = 64_000,
                CommandTimeout = 900
            };

            List<double>? radii = null;
            List<AdministrativeAreal2DReference>? administrativeAreal2DReferences_Selected = null;
            bool? skipExisting = null;
            string? tileSize = null;
            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Held = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingSolarRadiationSolveOptionsWindow postgreSQLBuildingSolarRadiationSolveOptionsWindow = new(postgreSQLBuildingSolarRadiationSolveOptions, SolarRadiationOptionsWindowCounties());

                radii = postgreSQLBuildingSolarRadiationSolveOptionsWindow.ListBoxControl_Radii.GetItems<double>(selected: false);
                administrativeAreal2DReferences_Selected = postgreSQLBuildingSolarRadiationSolveOptionsWindow.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>();
                skipExisting = postgreSQLBuildingSolarRadiationSolveOptionsWindow.CheckBox_SkipExisting.IsChecked;
                tileSize = postgreSQLBuildingSolarRadiationSolveOptionsWindow.TextBoxControl_TileSize.Value;

                postgreSQLBuildingSolarRadiationSolveOptions_Held = postgreSQLBuildingSolarRadiationSolveOptionsWindow.PostgreSQLBuildingSolarRadiationSolveOptions;
                postgreSQLBuildingSolarRadiationSolveOptions_Held.CommandTimeout = 30;
            });

            Assert.Null(exception);

            // Sorted, de-duplicated, and stripped of what the solve would ignore anyway.
            Assert.Equal([0, 50, 100], radii);

            // Both pieces of the multi-part county are restored by identifier, and nothing else.
            Assert.NotNull(administrativeAreal2DReferences_Selected);
            Assert.Equal([22138, 22139], administrativeAreal2DReferences_Selected!.ConvertAll(x => x.Id));

            Assert.False(skipExisting);
            Assert.Equal(125.0.ToString(System.Globalization.CultureInfo.CurrentCulture), tileSize);

            // A cancelled dialog has to leave the caller's options alone, which only holds for a copy.
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_Held);
            Assert.False(ReferenceEquals(postgreSQLBuildingSolarRadiationSolveOptions, postgreSQLBuildingSolarRadiationSolveOptions_Held));
            Assert.Equal(900, postgreSQLBuildingSolarRadiationSolveOptions.CommandTimeout);
        }

        /// <summary>
        /// Verifies that the solar urban-context export options window builds without a database or a running application, keeps the radii in the column order given, proposes <see cref="Query.SolarContextDirectory(string?)"/> when the options carry no folder, keeps a folder they do carry, and works on a copy (DiGi.GIS.PostgreSQL.UI#44).
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationContextExportOptionsWindow_Construction()
        {
            PostgreSQLBuildingSolarRadiationContextExportOptions postgreSQLBuildingSolarRadiationContextExportOptions = new()
            {
                CountyIds = [4816],
                Radii = [100, 0, 50],
                OutputDirectory = null
            };

            string outputDirectory_Given = Path.Combine(Path.GetTempPath(), "solar_context_given");

            List<double>? radii = null;
            string? outputDirectory_Default = null;
            string? outputDirectory_Kept = null;
            string? radiiHint_WithZero = null;
            string? radiiHint_WithoutZero = null;
            PostgreSQLBuildingSolarRadiationContextExportOptions? postgreSQLBuildingSolarRadiationContextExportOptions_Held = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingSolarRadiationContextExportOptionsWindow postgreSQLBuildingSolarRadiationContextExportOptionsWindow = new(postgreSQLBuildingSolarRadiationContextExportOptions, SolarRadiationOptionsWindowCounties());

                radii = postgreSQLBuildingSolarRadiationContextExportOptionsWindow.ListBoxControl_Radii.GetItems<double>(selected: false);
                outputDirectory_Default = postgreSQLBuildingSolarRadiationContextExportOptionsWindow.TextBoxControl_OutputDirectory.Value;
                radiiHint_WithZero = postgreSQLBuildingSolarRadiationContextExportOptionsWindow.Label_RadiiHint.Content as string;

                postgreSQLBuildingSolarRadiationContextExportOptions_Held = postgreSQLBuildingSolarRadiationContextExportOptionsWindow.PostgreSQLBuildingSolarRadiationContextExportOptions;
                postgreSQLBuildingSolarRadiationContextExportOptions_Held.BatchSize = 1;

                PostgreSQLBuildingSolarRadiationContextExportOptionsWindow postgreSQLBuildingSolarRadiationContextExportOptionsWindow_Given = new(new PostgreSQLBuildingSolarRadiationContextExportOptions() { OutputDirectory = outputDirectory_Given, Radii = [50] }, SolarRadiationOptionsWindowCounties());
                outputDirectory_Kept = postgreSQLBuildingSolarRadiationContextExportOptionsWindow_Given.TextBoxControl_OutputDirectory.Value;
                radiiHint_WithoutZero = postgreSQLBuildingSolarRadiationContextExportOptionsWindow_Given.Label_RadiiHint.Content as string;
            });

            Assert.Null(exception);

            // The column order of the files, not sorted.
            Assert.Equal([100, 0, 50], radii);

            Assert.Equal(Query.SolarContextDirectory(), outputDirectory_Default);
            Assert.Equal(outputDirectory_Given, outputDirectory_Kept);

            // The shortfall columns need the isolated building: its absence is said, its presence is not.
            Assert.True(string.IsNullOrEmpty(radiiHint_WithZero));
            Assert.False(string.IsNullOrEmpty(radiiHint_WithoutZero));

            Assert.NotNull(postgreSQLBuildingSolarRadiationContextExportOptions_Held);
            Assert.False(ReferenceEquals(postgreSQLBuildingSolarRadiationContextExportOptions, postgreSQLBuildingSolarRadiationContextExportOptions_Held));
            Assert.Equal(1000, postgreSQLBuildingSolarRadiationContextExportOptions.BatchSize);

            // The window only proposes the folder; the options stay unset until OK.
            Assert.Null(postgreSQLBuildingSolarRadiationContextExportOptions.OutputDirectory);
        }

        /// <summary>
        /// Verifies that the solar summary options window shows an unset <see cref="PostgreSQLBuildingDataSolarRadiationUpdateOptions.Radius"/> as an empty box - the latest result of any radius - and a set one as its value, and works on a copy (DiGi.GIS.PostgreSQL.UI#44).
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow_Construction()
        {
            PostgreSQLBuildingDataSolarRadiationUpdateOptions postgreSQLBuildingDataSolarRadiationUpdateOptions = new()
            {
                CountyIds = [55417],
                Radius = 50,
                SkipCompleted = true
            };

            string? radius_Set = null;
            string? radius_Unset = null;
            bool? skipCompleted = null;
            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Held = null;

            Exception? exception = RunSTA(() =>
            {
                PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow = new(postgreSQLBuildingDataSolarRadiationUpdateOptions, SolarRadiationOptionsWindowCounties());

                radius_Set = postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.TextBoxControl_Radius.Value;
                skipCompleted = postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.CheckBox_SkipCompleted.IsChecked;

                postgreSQLBuildingDataSolarRadiationUpdateOptions_Held = postgreSQLBuildingDataSolarRadiationUpdateOptionsWindow.PostgreSQLBuildingDataSolarRadiationUpdateOptions;
                postgreSQLBuildingDataSolarRadiationUpdateOptions_Held.Radius = null;

                radius_Unset = new PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow(null, SolarRadiationOptionsWindowCounties()).TextBoxControl_Radius.Value;
            });

            Assert.Null(exception);
            Assert.Equal(50.0.ToString(System.Globalization.CultureInfo.CurrentCulture), radius_Set);
            Assert.Equal(string.Empty, radius_Unset);
            Assert.True(skipCompleted);

            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_Held);
            Assert.False(ReferenceEquals(postgreSQLBuildingDataSolarRadiationUpdateOptions, postgreSQLBuildingDataSolarRadiationUpdateOptions_Held));
            Assert.Equal(50, postgreSQLBuildingDataSolarRadiationUpdateOptions.Radius);
        }

        /// <summary>
        /// Verifies the layout of the three solar radiation options windows without the host, at their opening and at their minimum size: every named control gets a size, lies inside the window, and no caption is cut short (<c>Coding - WPF Deferred Loading.md</c> §7, DiGi.GIS.PostgreSQL.UI#44).
        /// <para>A window is laid out by the handle it gets when shown, and a test must not put a dialog on screen, so the window's content is taken out and laid out on its own at the window's client size (the window less its border and caption). That is the layout the dialog shows; what it does not cover is the system font size of a particular machine.</para>
        /// </summary>
        [Fact]
        public void SolarRadiationOptionsWindows_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                List<Window> windows =
                [
                    new PostgreSQLBuildingSolarRadiationSolveOptionsWindow(null, SolarRadiationOptionsWindowCounties()),
                    new PostgreSQLBuildingSolarRadiationContextExportOptionsWindow(null, SolarRadiationOptionsWindowCounties()),
                    new PostgreSQLBuildingDataSolarRadiationUpdateOptionsWindow(null, SolarRadiationOptionsWindowCounties())
                ];

                foreach (Window window in windows)
                {
                    if (window.Content is not FrameworkElement frameworkElement)
                    {
                        failures.Add($"{window.GetType().Name}: no content");
                        continue;
                    }

                    // Detached so it can be laid out without the window ever getting a handle.
                    window.Content = null;

                    foreach ((double width, double height) in new (double, double)[] { (window.Width, window.Height), (window.MinWidth, window.MinHeight) })
                    {
                        // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                        Size size = new(width - 16, height - 39);

                        frameworkElement.Measure(size);
                        frameworkElement.Arrange(new Rect(size));
                        frameworkElement.UpdateLayout();

                        SolarRadiationOptionsWindowLayoutFailures(window.GetType().Name, size, frameworkElement, frameworkElement, failures);
                    }
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        private static List<AdministrativeAreal2DReference> SolarRadiationOptionsWindowCounties()
        {
            return
            [
                new() { Id = 55417, Code = "1465", Name = "m. St. Warszawa", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                // Two pieces of one multi-part county: same code and name, told apart only by the identifier.
                new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 22139, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
            ];
        }

        private static void SolarRadiationOptionsWindowLayoutFailures(string typeName, Size size, FrameworkElement root, DependencyObject dependencyObject, List<string> failures)
        {
            int count = VisualTreeHelper.GetChildrenCount(dependencyObject);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(dependencyObject, i);

                // The items of a list scroll; their text is not the window's layout.
                if (child is ListBox)
                {
                    continue;
                }

                if (child is FrameworkElement frameworkElement && frameworkElement.Visibility == Visibility.Visible)
                {
                    string name = string.IsNullOrEmpty(frameworkElement.Name) ? frameworkElement.GetType().Name : frameworkElement.Name;

                    if (!string.IsNullOrEmpty(frameworkElement.Name) && (frameworkElement.ActualWidth < 1 || frameworkElement.ActualHeight < 1))
                    {
                        failures.Add($"{typeName} at {size}: {name} has no size ({frameworkElement.ActualWidth} x {frameworkElement.ActualHeight})");
                    }

                    if (frameworkElement.ActualWidth >= 1 && frameworkElement.ActualHeight >= 1)
                    {
                        Rect rect = frameworkElement.TransformToAncestor(root).TransformBounds(new Rect(frameworkElement.RenderSize));
                        if (rect.Right > size.Width + 0.5 || rect.Bottom > size.Height + 0.5 || rect.Left < -0.5 || rect.Top < -0.5)
                        {
                            failures.Add($"{typeName} at {size}: {name} lies outside the window ({rect})");
                        }
                    }

                    // A caption cut short: what the text would take on its own against what it was given.
                    if (frameworkElement is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text) && textBlock.ActualWidth >= 1)
                    {
                        TextBlock textBlock_Probe = new() { Text = textBlock.Text, FontFamily = textBlock.FontFamily, FontSize = textBlock.FontSize, FontStyle = textBlock.FontStyle, FontWeight = textBlock.FontWeight };
                        textBlock_Probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        if (textBlock_Probe.DesiredSize.Width > textBlock.ActualWidth + 1)
                        {
                            failures.Add($"{typeName} at {size}: \"{textBlock.Text}\" needs {textBlock_Probe.DesiredSize.Width:F0} px and has {textBlock.ActualWidth:F0}");
                        }
                    }
                }

                SolarRadiationOptionsWindowLayoutFailures(typeName, size, root, child, failures);
            }
        }

        private static Exception? RunSTA(Action action)
        {
            Exception? exception = null;

            Thread thread = new(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception_Temp)
                {
                    exception = exception_Temp;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return exception;
        }
    }
}
