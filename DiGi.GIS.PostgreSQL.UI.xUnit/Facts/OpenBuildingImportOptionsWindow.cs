using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Size = System.Windows.Size;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the open building import options window can be built and that it works on a copy of the options it is given, without a database or a running application (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#38).
        /// <para>The window is the only way a tray run is scoped, so one whose markup failed to parse, or whose constructor threw on the county list or on a bounding box, would not be found until an operator opened it to import a dataset. The three construction paths are covered: a county scope with a county list, a bounding-box scope with no county list to hand, and no options at all - the last reaching the window through a task constructed by hand.</para>
        /// <para><b>This fact does not check what the window looks like.</b> A window is laid out by the handle it gets when it is shown, and showing one during a test run would put a dialog on screen - the layout is measured without its host by <see cref="OpenBuildingImportOptionsWindow_Layout"/> instead (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#56).</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImportOptionsWindow_Construction()
        {
            PostgreSQLOpenBuildingImportTaskOptions options = new()
            {
                DatasetKey = "microsoft",
                Version = "TST-1",
                DownloadDate = new DateTime(2026, 11, 6),
                Quadkey = "03210",
                SourcePaths = ["C:/source/chunk.geojsonl"],
                CountyIds = [4816]
            };

            Exception? exception = null;
            bool copied = false;
            bool versionCarried = false;
            bool quadkeyCarried = false;
            bool countyIdsCarried = false;
            bool sourcePathsCarried = false;
            bool untouched = false;

            Thread thread = new(() =>
            {
                try
                {
                    List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
                    [
                        new() { Id = 55417, Code = "1465", Name = "m. St. Warszawa", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                        // Two pieces of one multi-part county: same code and name, told apart by the identifier.
                        new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                        new() { Id = 22139, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
                    ];

                    OpenBuildingImportOptionsWindow openBuildingImportOptionsWindow = new(options, administrativeAreal2DReferences);

                    PostgreSQLOpenBuildingImportTaskOptions held = openBuildingImportOptionsWindow.PostgreSQLOpenBuildingImportTaskOptions;

                    // Until OK is pressed the window holds what it was given.
                    copied = !ReferenceEquals(options, held);
                    versionCarried = held.Version == "TST-1";
                    quadkeyCarried = held.Quadkey == "03210";
                    countyIdsCarried = held.CountyIds is HashSet<int> countyIds && countyIds.Count == 1 && countyIds.Contains(4816);
                    sourcePathsCarried = held.SourcePaths is HashSet<string> sourcePaths && sourcePaths.Count == 1;

                    // A cancelled dialog has to leave the caller's options alone, which only holds if the window
                    // took a copy rather than a reference.
                    held.Version = "changed";

                    // The bounding-box construction branch, with no county list to hand at all.
                    OpenBuildingImportOptionsWindow openBuildingImportOptionsWindow_Box = new(
                        new PostgreSQLOpenBuildingImportTaskOptions
                        {
                            DatasetKey = "osm",
                            BoundingBox2D = new BoundingBox2D(new Point2D(629000, 519000), new Point2D(631000, 521000))
                        },
                        null);
                    _ = openBuildingImportOptionsWindow_Box.PostgreSQLOpenBuildingImportTaskOptions;

                    // No options and no counties: the window opens reporting that no dataset was chosen rather
                    // than throwing, because a task built by hand can reach it.
                    OpenBuildingImportOptionsWindow openBuildingImportOptionsWindow_Empty = new(null, null);
                    _ = openBuildingImportOptionsWindow_Empty.PostgreSQLOpenBuildingImportTaskOptions;

                    untouched = options.Version == "TST-1";
                }
                catch (Exception exception_Temp)
                {
                    exception = exception_Temp;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(exception);
            Assert.True(copied);
            Assert.True(versionCarried);
            Assert.True(quadkeyCarried);
            Assert.True(countyIdsCarried);
            Assert.True(sourcePathsCarried);

            // The caller's own instance is untouched by the edit made to the window's copy above.
            Assert.True(untouched);
        }

        /// <summary>
        /// Verifies the layout of the open building import options window without the host, at its opening and at its minimum size: the county list fills a right-hand column and shows at least 25 rows at the default size, the scope form of the checked radio lies in that column, and no control is clipped (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#56, <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>A window is laid out by the handle it gets when it is shown, and a fact must not put a dialog on screen, so the window's content is taken out and laid out on its own at the window's client size - the window less its frame and caption - as in <see cref="SolarRadiationOptionsWindows_Layout"/>.</para>
        /// <para>The list is given 60 county parts: a handful of entries could never show 25 rows, so a row count taken from an empty or short list would pass without measuring anything. The row height is read from a generated row rather than assumed. The scope radio is switched by setting <c>IsChecked</c>, never by pressing OK, whose refusal boxes would hang the fact (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#56 acceptance: the same input hands back the same values, covered by <see cref="OpenBuildingImportOptionsWindow_Construction"/>).</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImportOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                OpenBuildingImportOptionsWindow openBuildingImportOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                List<AdministrativeAreal2DReference>? administrativeAreal2DReferences = openBuildingImportOptionsWindow.ListBoxControl_Counties.GetItems<AdministrativeAreal2DReference>(false);
                if (administrativeAreal2DReferences is null || administrativeAreal2DReferences.Count != 60)
                {
                    // The row count below is only meaningful against a list that holds the entries it shows.
                    failures.Add($"{nameof(OpenBuildingImportOptionsWindow)}: the county list holds {administrativeAreal2DReferences?.Count ?? 0} entries, 60 were given");
                    return;
                }

                if (openBuildingImportOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(OpenBuildingImportOptionsWindow)}: no content");
                    return;
                }

                // Detached so it can be laid out without the window ever getting a handle.
                openBuildingImportOptionsWindow.Content = null;

                // A single-border window takes about 16 px of width and 39 px of height for its frame and caption.
                Size size_Default = new(openBuildingImportOptionsWindow.Width - 16, openBuildingImportOptionsWindow.Height - 39);
                Size size_Min = new(openBuildingImportOptionsWindow.MinWidth - 16, openBuildingImportOptionsWindow.MinHeight - 39);

                // The controls of a list carry their list box behind a template, and a template only exists
                // once the content has been laid out - so lay it out once before walking into it.
                root.Measure(size_Default);
                root.Arrange(new Rect(size_Default));
                root.UpdateLayout();

                ListBox? listBox = OpenBuildingImportOptionsWindowListBox(openBuildingImportOptionsWindow.ListBoxControl_Counties);
                if (listBox is null)
                {
                    failures.Add($"{nameof(OpenBuildingImportOptionsWindow)}: the county list holds no list box to measure");
                    return;
                }

                // County scope: at the opening size the list has to show the rows a run's pick needs; at the
                // minimum size it only has to have a size and lie inside the window.
                OpenBuildingImportOptionsWindowLayoutFailures(nameof(OpenBuildingImportOptionsWindow), size_Default, root, true, listBox, openBuildingImportOptionsWindow.Grid_CountyScope, 25, failures);
                OpenBuildingImportOptionsWindowLayoutFailures(nameof(OpenBuildingImportOptionsWindow), size_Min, root, true, listBox, openBuildingImportOptionsWindow.Grid_CountyScope, 0, failures);

                // The bounding box shares the right-hand column: the list is hidden rather than left beside
                // the four boxes, and the boxes are laid out in the column the list filled.
                openBuildingImportOptionsWindow.RadioButton_BoundingBox.IsChecked = true;

                if (openBuildingImportOptionsWindow.Grid_CountyScope.Visibility != Visibility.Collapsed)
                {
                    failures.Add($"{nameof(OpenBuildingImportOptionsWindow)}: the county list is still shown once the bounding box scope is checked");
                }

                if (openBuildingImportOptionsWindow.StackPanel_BoundingBox.Visibility != Visibility.Visible)
                {
                    failures.Add($"{nameof(OpenBuildingImportOptionsWindow)}: the bounding box is not shown once its scope is checked");
                }

                OpenBuildingImportOptionsWindowLayoutFailures(nameof(OpenBuildingImportOptionsWindow), size_Default, root, false, listBox, openBuildingImportOptionsWindow.StackPanel_BoundingBox, 0, failures);
                OpenBuildingImportOptionsWindowLayoutFailures(nameof(OpenBuildingImportOptionsWindow), size_Min, root, false, listBox, openBuildingImportOptionsWindow.StackPanel_BoundingBox, 0, failures);

                // Back to the county parts: the two radios swap one form for the other, so the window never
                // ends up offering neither.
                openBuildingImportOptionsWindow.RadioButton_CountyParts.IsChecked = true;

                if (openBuildingImportOptionsWindow.Grid_CountyScope.Visibility != Visibility.Visible
                    || openBuildingImportOptionsWindow.StackPanel_BoundingBox.Visibility != Visibility.Collapsed)
                {
                    failures.Add($"{nameof(OpenBuildingImportOptionsWindow)}: the county list was not restored when its scope was checked again");
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Lays the window's content out at one size and records everything that is wrong with it: the shared walk over every visible control (sizes, being inside the window, captions not cut short), where the scope form sits, and - for the county scope - how many rows the list shows.
        /// </summary>
        /// <param name="typeName">The window type name, prefixed to every failure.</param>
        /// <param name="size">The client size to lay the content out at.</param>
        /// <param name="root">The detached content of the window.</param>
        /// <param name="county">Whether the county scope form is the one shown, so the list is measured as well.</param>
        /// <param name="listBox">The list box inside the county list control.</param>
        /// <param name="scopePanel">The panel holding the scope form that is shown - the county list or the bounding box.</param>
        /// <param name="minimumRows">The rows the list has to show at this size, or 0 to only check that it has a size and lies inside the window.</param>
        /// <param name="failures">The list the failures are added to.</param>
        private static void OpenBuildingImportOptionsWindowLayoutFailures(string typeName, Size size, FrameworkElement root, bool county, ListBox listBox, FrameworkElement scopePanel, double minimumRows, List<string> failures)
        {
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();

            SolarRadiationOptionsWindowLayoutFailures(typeName, size, root, root, failures);

            // The window-wide walk cannot see a caption that runs past the control it is written on: a label
            // lays its text out at the text's own width, so the text keeps a size and only covers what is
            // next to it (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#56 acceptance: no control is clipped).
            OpenBuildingImportOptionsWindowCaptionFailures(typeName, size, root, root, null, null, failures);

            // The scope form belongs to the right-hand column: it starts past the middle of the window and
            // stays inside it at every size.
            Rect rect_Scope = scopePanel.TransformToAncestor(root).TransformBounds(new Rect(scopePanel.RenderSize));
            if (rect_Scope.Left <= size.Width / 2 || rect_Scope.Right > size.Width + 0.5 || rect_Scope.Bottom > size.Height + 0.5 || rect_Scope.Left < -0.5 || rect_Scope.Top < -0.5)
            {
                failures.Add($"{typeName} at {size}: the scope form lies at {rect_Scope} instead of in the right-hand column");
            }

            if (!county)
            {
                return;
            }

            // The shared walk skips the inside of a list, because its items scroll; the list itself is
            // therefore measured here, as the list box rather than as the control wrapping it.
            if (listBox.ActualWidth < 1 || listBox.ActualHeight < 1)
            {
                failures.Add($"{typeName} at {size}: the county list has no size ({listBox.ActualWidth} x {listBox.ActualHeight})");
                return;
            }

            Rect rect_ListBox = listBox.TransformToAncestor(root).TransformBounds(new Rect(listBox.RenderSize));
            if (rect_ListBox.Right > size.Width + 0.5 || rect_ListBox.Bottom > size.Height + 0.5 || rect_ListBox.Left < -0.5 || rect_ListBox.Top < -0.5)
            {
                failures.Add($"{typeName} at {size}: the county list lies outside the window ({rect_ListBox})");
            }

            if (minimumRows < 1)
            {
                return;
            }

            // A virtualizing panel generates its rows in a dispatcher pass; pump before reading them, so the
            // count never depends on whether a container happened to exist already.
            for (int i = 0; i < 3; i++)
            {
                DispatcherFrame dispatcherFrame = new();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => dispatcherFrame.Continue = false));
                Dispatcher.PushFrame(dispatcherFrame);
            }

            if (listBox.ItemContainerGenerator.ContainerFromIndex(0) is not FrameworkElement row || row.ActualHeight < 1)
            {
                // Said, not guessed: a row height assumed from a constant could make the check below pass on
                // a list that was never laid out.
                failures.Add($"{typeName} at {size}: the first county row was not generated, so the rows it would show cannot be counted");
                return;
            }

            double rows = Math.Floor(listBox.ActualHeight / row.ActualHeight);
            if (rows < minimumRows)
            {
                failures.Add($"{typeName} at {size}: the county list shows {rows:F0} rows ({listBox.ActualHeight:F0} px of list over {row.ActualHeight:F0} px per row) and {minimumRows:F0} are needed");
            }
        }

        /// <summary>
        /// Walks the content recording every caption that does not fit the control it is written on. A label lays its text out at the text's own width rather than at the width the label was given, so a caption that is too long keeps its size, crosses into whatever sits beside it and is cut off only by the window edge - which the window-wide walk reports too late, if at all.
        /// </summary>
        /// <param name="typeName">The window type name, prefixed to every failure.</param>
        /// <param name="size">The size the content was laid out at.</param>
        /// <param name="root">The detached content of the window.</param>
        /// <param name="dependencyObject">The element whose children are being walked.</param>
        /// <param name="containerRect">The bounds of the label or check box the captions below are written on, or null when there is none.</param>
        /// <param name="containerName">The name of that control, used in the failure text.</param>
        /// <param name="failures">The list the failures are added to.</param>
        private static void OpenBuildingImportOptionsWindowCaptionFailures(string typeName, Size size, FrameworkElement root, DependencyObject dependencyObject, Rect? containerRect, string? containerName, List<string> failures)
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

                Rect? childContainerRect = containerRect;
                string? childContainerName = containerName;

                if (child is FrameworkElement frameworkElement && SolarRadiationOptionsWindowIsVisible(frameworkElement, root) && frameworkElement.ActualWidth >= 1)
                {
                    if (frameworkElement is Label or CheckBox)
                    {
                        // The control's own width is the width it was given, so its text has to fit inside it.
                        childContainerRect = frameworkElement.TransformToAncestor(root).TransformBounds(new Rect(frameworkElement.RenderSize));
                        childContainerName = string.IsNullOrEmpty(frameworkElement.Name) ? frameworkElement.GetType().Name : frameworkElement.Name;
                    }
                    else if (frameworkElement is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text) && childContainerRect is Rect rect_Container)
                    {
                        Rect rect_Text = textBlock.TransformToAncestor(root).TransformBounds(new Rect(textBlock.RenderSize));
                        if (rect_Text.Right > rect_Container.Right + 0.5 || rect_Text.Bottom > rect_Container.Bottom + 0.5)
                        {
                            failures.Add($"{typeName} at {size}: \"{textBlock.Text}\" needs {rect_Text.Width:F0} px and the {childContainerName} gives {rect_Container.Width:F0} px");
                        }
                    }
                }

                OpenBuildingImportOptionsWindowCaptionFailures(typeName, size, root, child, childContainerRect, childContainerName, failures);
            }
        }

        /// <summary>
        /// Finds the list box inside a list control by walking its visual tree, because the control keeps it out of reach behind its own template.
        /// </summary>
        /// <param name="dependencyObject">The element to search, starting at the control.</param>
        /// <returns>The first list box found below it, or null when there is none.</returns>
        private static ListBox? OpenBuildingImportOptionsWindowListBox(DependencyObject dependencyObject)
        {
            if (dependencyObject is ListBox listBox)
            {
                return listBox;
            }

            int count = VisualTreeHelper.GetChildrenCount(dependencyObject);
            for (int i = 0; i < count; i++)
            {
                ListBox? listBox_Temp = OpenBuildingImportOptionsWindowListBox(VisualTreeHelper.GetChild(dependencyObject, i));
                if (listBox_Temp is not null)
                {
                    return listBox_Temp;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds the county parts the layout fact chooses from: as many entries as a run could ever be given, each one line long, with identifiers that stay unique however the window sorts them.
        /// </summary>
        /// <param name="count">The number of county parts to build.</param>
        /// <returns>The county parts, one per piece as the dialog lists them.</returns>
        private static List<AdministrativeAreal2DReference> OpenBuildingImportOptionsWindowCounties(int count)
        {
            List<AdministrativeAreal2DReference> administrativeAreal2DReferences = [];
            for (int i = 0; i < count; i++)
            {
                administrativeAreal2DReferences.Add(new AdministrativeAreal2DReference() { Id = 1000 + i, Code = i.ToString("D4", CultureInfo.InvariantCulture), Name = $"county {i}", AdministrativeArealType = AdministrativeArealType.County });
            }

            return administrativeAreal2DReferences;
        }
    }
}
