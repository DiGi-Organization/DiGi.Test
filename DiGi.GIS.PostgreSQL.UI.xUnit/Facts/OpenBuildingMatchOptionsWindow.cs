using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Windows;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Size = System.Windows.Size;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the validation of the open building match options (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#57): datasets, the five rule parameters just inside and just outside their ranges, and exactly one scope with an area.
        /// <para>The core task takes the rule parameters as given, so this is the only gate that keeps a share above one or a negative area out of a run; each bound is asserted on both sides of it.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatchOptionsMessage()
        {
            static PostgreSQLOpenBuildingMatchTaskOptions Options(OpenBuildingMatchParameters? openBuildingMatchParameters = null)
            {
                return new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [55417], OpenBuildingMatchParameters = openBuildingMatchParameters };
            }

            Assert.Equal("No options were given.", Query.OpenBuildingMatchOptionsMessage(null));

            // Defaults with a county scope start a run; null parameters mean the defaults.
            Assert.Null(Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters())));
            Assert.Null(Query.OpenBuildingMatchOptionsMessage(Options()));

            // Datasets: null is all five; empty, unknown and GHS-OBAT are refused.
            Assert.Null(Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [1], DatasetKeys = ["eubucco", "OSM"] }));
            Assert.NotNull(Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [1], DatasetKeys = [] }));
            Assert.NotNull(Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [1], DatasetKeys = ["no-such-dataset"] }));
            Assert.Contains("GHS-OBAT", Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [1], DatasetKeys = ["ghs_obat"] }));

            // The share bounds, both sides: 0 and 1 are allowed, a hair beyond is not.
            Assert.Null(Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(ioUThreshold: 0, minOverlapShare: 1))));
            Assert.Null(Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(ioUThreshold: 1, minOverlapShare: 0))));
            Assert.Contains("IoU", Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(ioUThreshold: 1 + 1e-9))));
            Assert.Contains("IoU", Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(ioUThreshold: -1e-9))));
            Assert.Contains("IoU", Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(ioUThreshold: double.NaN))));
            Assert.Contains("overlap share", Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(minOverlapShare: 1 + 1e-9))));
            Assert.Contains("overlap share", Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(minOverlapShare: -1e-9))));

            // The lengths and the area: zero is allowed, below zero and non-finite are not.
            Assert.Null(Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(minArea: 0, maxCentroidDistance: 0, boundaryMargin: 0))));
            Assert.Contains("minimum area", Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(minArea: -1e-9))));
            Assert.Contains("centroid distance", Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(maxCentroidDistance: -1e-9))));
            Assert.Contains("boundary margin", Query.OpenBuildingMatchOptionsMessage(Options(new OpenBuildingMatchParameters(boundaryMargin: double.PositiveInfinity))));

            // Exactly one scope, and a box with an area.
            Assert.Contains("exactly one", Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions()));
            Assert.Contains("exactly one", Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [1], BoundingBox2D = new BoundingBox2D(new Point2D(0, 0), new Point2D(1, 1)) }));
            Assert.Contains("exactly one", Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions() { CountyIds = [] }));
            Assert.Null(Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions() { BoundingBox2D = new BoundingBox2D(new Point2D(629000, 519000), new Point2D(631000, 521000)) }));
            Assert.Contains("no area", Query.OpenBuildingMatchOptionsMessage(new PostgreSQLOpenBuildingMatchTaskOptions() { BoundingBox2D = new BoundingBox2D(new Point2D(629000, 519000), new Point2D(629000, 521000)) }));
        }

        /// <summary>
        /// Verifies that the open building match options window can be built from every construction path and that it shows the options it was given on a copy, without a database or a running application (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#57).
        /// <para>Three paths: chosen datasets with a county scope and changed rule parameters with the links not stored; a bounding-box scope with no county list; and no options at all, which shows every dataset checked, the rule as proposed and the links stored. An edit made to the window's copy leaves the caller's options and their parameter instance untouched.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatchOptionsWindow_Construction()
        {
            OpenBuildingMatchParameters openBuildingMatchParameters = new(0.6, 0.25, 12.5, 3, 150);
            PostgreSQLOpenBuildingMatchTaskOptions options = new()
            {
                DatasetKeys = ["osm", "eubucco"],
                CountyIds = [4816],
                OpenBuildingMatchParameters = openBuildingMatchParameters,
                Persist = false,
            };

            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                OpenBuildingMatchOptionsWindow openBuildingMatchOptionsWindow = new(options, OpenBuildingImportOptionsWindowCounties(5));

                PostgreSQLOpenBuildingMatchTaskOptions held = openBuildingMatchOptionsWindow.PostgreSQLOpenBuildingMatchTaskOptions;
                Expect(!ReferenceEquals(options, held), "the window holds the caller's options instead of a copy");
                Expect(!ReferenceEquals(openBuildingMatchParameters, held.OpenBuildingMatchParameters), "the window holds the caller's parameter instance");

                Expect(openBuildingMatchOptionsWindow.CheckBox_EUBUCCO.IsChecked == true && openBuildingMatchOptionsWindow.CheckBox_OSM.IsChecked == true, "the chosen datasets are not checked");
                Expect(openBuildingMatchOptionsWindow.CheckBox_Overture.IsChecked == false && openBuildingMatchOptionsWindow.CheckBox_DBSM.IsChecked == false && openBuildingMatchOptionsWindow.CheckBox_Microsoft.IsChecked == false, "a dataset that was not chosen is checked");
                Expect(openBuildingMatchOptionsWindow.TextBoxControl_IoUThreshold.Value == "0.6", $"IoU threshold shows {openBuildingMatchOptionsWindow.TextBoxControl_IoUThreshold.Value}");
                Expect(openBuildingMatchOptionsWindow.TextBoxControl_MinOverlapShare.Value == "0.25", "minimum overlap share not shown");
                Expect(openBuildingMatchOptionsWindow.TextBoxControl_MinArea.Value == "12.5", "minimum area not shown");
                Expect(openBuildingMatchOptionsWindow.TextBoxControl_MaxCentroidDistance.Value == "3", "max centroid distance not shown");
                Expect(openBuildingMatchOptionsWindow.TextBoxControl_BoundaryMargin.Value == "150", "boundary margin not shown");
                Expect(openBuildingMatchOptionsWindow.CheckBox_Persist.IsChecked == false, "Persist shown as checked");
                Expect(openBuildingMatchOptionsWindow.RadioButton_CountyParts.IsChecked == true, "the county scope is not checked");

                held.Persist = true;
                held.DatasetKeys = null;

                // The bounding-box path, with no county list to hand.
                OpenBuildingMatchOptionsWindow openBuildingMatchOptionsWindow_Box = new(new PostgreSQLOpenBuildingMatchTaskOptions() { BoundingBox2D = new BoundingBox2D(new Point2D(629000, 519000), new Point2D(631000, 521000)) }, null);
                Expect(openBuildingMatchOptionsWindow_Box.RadioButton_BoundingBox.IsChecked == true, "the box scope is not checked");
                Expect(openBuildingMatchOptionsWindow_Box.TextBoxControl_X1.Value == "629000" && openBuildingMatchOptionsWindow_Box.TextBoxControl_Y2.Value == "521000", "the box corners are not shown");

                // No options: every dataset, the rule as proposed, the links stored.
                OpenBuildingMatchOptionsWindow openBuildingMatchOptionsWindow_Empty = new(null, null);
                Expect(openBuildingMatchOptionsWindow_Empty.CheckBox_EUBUCCO.IsChecked == true && openBuildingMatchOptionsWindow_Empty.CheckBox_Overture.IsChecked == true && openBuildingMatchOptionsWindow_Empty.CheckBox_OSM.IsChecked == true && openBuildingMatchOptionsWindow_Empty.CheckBox_DBSM.IsChecked == true && openBuildingMatchOptionsWindow_Empty.CheckBox_Microsoft.IsChecked == true, "not every dataset is checked by default");
                Expect(openBuildingMatchOptionsWindow_Empty.TextBoxControl_IoUThreshold.Value == "0.5" && openBuildingMatchOptionsWindow_Empty.TextBoxControl_MinOverlapShare.Value == "0.3" && openBuildingMatchOptionsWindow_Empty.TextBoxControl_BoundaryMargin.Value == "100", "the defaults are not shown");
                Expect(openBuildingMatchOptionsWindow_Empty.CheckBox_Persist.IsChecked == true, "storing the links is not the default");
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

            // The caller's options are untouched by the edits made to the window's copy.
            Assert.False(options.Persist);
            Assert.NotNull(options.DatasetKeys);
            Assert.Equal(0.6, openBuildingMatchParameters.IoUThreshold);
        }

        /// <summary>
        /// Verifies what OK reads from the controls (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#57), through the window's <c>TryGetOptions</c> - the part of OK a fact can drive, since a refusal box would hang it.
        /// <para>A refusal (no dataset, a parameter that is not a number, a share above one, no county) leaves the window's options exactly as they were. An accepted read stores every checked dataset as "all", a subset as itself, the five parameters - typed with a comma under <c>pl-PL</c> as well as with a point - the store-links box and the scope; Defaults puts the rule back.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatchOptionsWindow_TryGetOptions()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                CultureInfo cultureInfo_Current = CultureInfo.CurrentCulture;
                try
                {
                    OpenBuildingMatchOptionsWindow openBuildingMatchOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(5));
                    PostgreSQLOpenBuildingMatchTaskOptions held = openBuildingMatchOptionsWindow.PostgreSQLOpenBuildingMatchTaskOptions;

                    // No county selected: refused, nothing written.
                    Expect(!openBuildingMatchOptionsWindow.TryGetOptions(out string? message_NoCounty) && message_NoCounty is not null, "an empty county selection was accepted");
                    Expect(held.CountyIds is null, "a refused read wrote the county list");

                    openBuildingMatchOptionsWindow.ListBoxControl_Counties.SelectItems<AdministrativeAreal2DReference>(x => x.Id == 1001 || x.Id == 1003);

                    // Every dataset checked: stored as all.
                    Expect(openBuildingMatchOptionsWindow.TryGetOptions(out string? message_All) && message_All is null, $"a valid read was refused: {message_All}");
                    Expect(held.DatasetKeys is null, "every dataset checked was not stored as all");
                    Expect(held.CountyIds is HashSet<int> countyIds && countyIds.SetEquals([1001, 1003]), "the selected county parts were not stored");
                    Expect(held.BoundingBox2D is null, "a box was stored with the county scope");

                    // A subset, changed parameters with a comma under pl-PL, links not stored.
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");
                    openBuildingMatchOptionsWindow.CheckBox_Overture.IsChecked = false;
                    openBuildingMatchOptionsWindow.CheckBox_Microsoft.IsChecked = false;
                    openBuildingMatchOptionsWindow.TextBoxControl_IoUThreshold.Value = "0,4";
                    openBuildingMatchOptionsWindow.TextBoxControl_MinOverlapShare.Value = "0.1";
                    openBuildingMatchOptionsWindow.TextBoxControl_MinArea.Value = "25";
                    openBuildingMatchOptionsWindow.TextBoxControl_MaxCentroidDistance.Value = "2,5";
                    openBuildingMatchOptionsWindow.TextBoxControl_BoundaryMargin.Value = "200";
                    openBuildingMatchOptionsWindow.CheckBox_Persist.IsChecked = false;

                    Expect(openBuildingMatchOptionsWindow.TryGetOptions(out string? message_Subset) && message_Subset is null, $"a valid subset was refused: {message_Subset}");
                    Expect(held.DatasetKeys is HashSet<string> datasetKeys && datasetKeys.SetEquals(["eubucco", "osm", "dbsm"]), "the dataset subset was not stored");
                    Expect(held.OpenBuildingMatchParameters is OpenBuildingMatchParameters p && p.IoUThreshold == 0.4 && p.MinOverlapShare == 0.1 && p.MinArea == 25 && p.MaxCentroidDistance == 2.5 && p.BoundaryMargin == 200, "the parameters were not read as typed");
                    Expect(!held.Persist, "the cleared store-links box was stored as checked");

                    // Refusals leave the options as they were.
                    openBuildingMatchOptionsWindow.TextBoxControl_IoUThreshold.Value = "1.5";
                    Expect(!openBuildingMatchOptionsWindow.TryGetOptions(out string? message_Share) && message_Share is not null && message_Share.Contains("IoU", StringComparison.Ordinal), "an IoU threshold above one was accepted");
                    Expect(held.OpenBuildingMatchParameters?.IoUThreshold == 0.4, "a refused read changed the parameters");

                    openBuildingMatchOptionsWindow.TextBoxControl_IoUThreshold.Value = "half";
                    Expect(!openBuildingMatchOptionsWindow.TryGetOptions(out _), "a parameter that is not a number was accepted");

                    foreach (CheckBox checkBox in new CheckBox[] { openBuildingMatchOptionsWindow.CheckBox_EUBUCCO, openBuildingMatchOptionsWindow.CheckBox_Overture, openBuildingMatchOptionsWindow.CheckBox_OSM, openBuildingMatchOptionsWindow.CheckBox_DBSM, openBuildingMatchOptionsWindow.CheckBox_Microsoft })
                    {
                        checkBox.IsChecked = false;
                    }

                    Expect(!openBuildingMatchOptionsWindow.TryGetOptions(out _), "no dataset checked was accepted");
                    Expect(held.DatasetKeys is HashSet<string> datasetKeys_Kept && datasetKeys_Kept.Count == 3, "a refused read changed the datasets");

                    // Defaults puts the rule back; the box scope replaces the county list.
                    openBuildingMatchOptionsWindow.CheckBox_DBSM.IsChecked = true;
                    openBuildingMatchOptionsWindow.Button_Defaults.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                    openBuildingMatchOptionsWindow.RadioButton_BoundingBox.IsChecked = true;
                    openBuildingMatchOptionsWindow.TextBoxControl_X1.Value = "629000";
                    openBuildingMatchOptionsWindow.TextBoxControl_Y1.Value = "519000";
                    openBuildingMatchOptionsWindow.TextBoxControl_X2.Value = "631000";
                    openBuildingMatchOptionsWindow.TextBoxControl_Y2.Value = "521000";

                    Expect(openBuildingMatchOptionsWindow.TryGetOptions(out string? message_Box) && message_Box is null, $"a valid box read was refused: {message_Box}");
                    Expect(held.OpenBuildingMatchParameters is OpenBuildingMatchParameters p_Default && p_Default.IoUThreshold == 0.5 && p_Default.MinOverlapShare == 0.3 && p_Default.MinArea == 0 && p_Default.MaxCentroidDistance == 0 && p_Default.BoundaryMargin == 100, "Defaults did not put the rule back");
                    Expect(held.CountyIds is null && held.BoundingBox2D is BoundingBox2D boundingBox2D && boundingBox2D.Min.X == 629000 && boundingBox2D.Max.Y == 521000, "the box scope was not stored in place of the county list");
                    Expect(held.DatasetKeys is HashSet<string> datasetKeys_DBSM && datasetKeys_DBSM.SetEquals(["dbsm"]), "the single checked dataset was not stored");
                }
                finally
                {
                    CultureInfo.CurrentCulture = cultureInfo_Current;
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
        }

        /// <summary>
        /// Verifies the layout of the open building match options window without the host, at its opening and at its minimum size, for both scopes: no control clipped or outside the window, no caption cut short, the scope form in the right-hand column and the county list showing at least 25 rows at the default size (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#57; <c>Coding - WPF Deferred Loading.md</c> section 7).
        /// <para>The same harness as <see cref="OpenBuildingImportOptionsWindow_Layout"/>: the content is detached and laid out at the client size, the list given 60 county parts so a row count means something, and the scope switched through <c>IsChecked</c>, never through OK.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingMatchOptionsWindow_Layout()
        {
            List<string> failures = [];

            Exception? exception = RunSTA(() =>
            {
                OpenBuildingMatchOptionsWindow openBuildingMatchOptionsWindow = new(null, OpenBuildingImportOptionsWindowCounties(60));

                if (openBuildingMatchOptionsWindow.Content is not FrameworkElement root)
                {
                    failures.Add($"{nameof(OpenBuildingMatchOptionsWindow)}: no content");
                    return;
                }

                openBuildingMatchOptionsWindow.Content = null;

                Size size_Default = new(openBuildingMatchOptionsWindow.Width - 16, openBuildingMatchOptionsWindow.Height - 39);
                Size size_Min = new(openBuildingMatchOptionsWindow.MinWidth - 16, openBuildingMatchOptionsWindow.MinHeight - 39);

                root.Measure(size_Default);
                root.Arrange(new Rect(size_Default));
                root.UpdateLayout();

                ListBox? listBox = OpenBuildingImportOptionsWindowListBox(openBuildingMatchOptionsWindow.ListBoxControl_Counties);
                if (listBox is null)
                {
                    failures.Add($"{nameof(OpenBuildingMatchOptionsWindow)}: the county list holds no list box to measure");
                    return;
                }

                OpenBuildingImportOptionsWindowLayoutFailures(nameof(OpenBuildingMatchOptionsWindow), size_Default, root, true, listBox, openBuildingMatchOptionsWindow.Grid_CountyScope, 25, failures);
                OpenBuildingImportOptionsWindowLayoutFailures(nameof(OpenBuildingMatchOptionsWindow), size_Min, root, true, listBox, openBuildingMatchOptionsWindow.Grid_CountyScope, 0, failures);

                openBuildingMatchOptionsWindow.RadioButton_BoundingBox.IsChecked = true;
                if (openBuildingMatchOptionsWindow.Grid_CountyScope.Visibility != Visibility.Collapsed || openBuildingMatchOptionsWindow.StackPanel_BoundingBox.Visibility != Visibility.Visible)
                {
                    failures.Add($"{nameof(OpenBuildingMatchOptionsWindow)}: the box scope did not replace the county list");
                }

                OpenBuildingImportOptionsWindowLayoutFailures(nameof(OpenBuildingMatchOptionsWindow), size_Default, root, false, listBox, openBuildingMatchOptionsWindow.StackPanel_BoundingBox, 0, failures);
                OpenBuildingImportOptionsWindowLayoutFailures(nameof(OpenBuildingMatchOptionsWindow), size_Min, root, false, listBox, openBuildingMatchOptionsWindow.StackPanel_BoundingBox, 0, failures);

                // The sensitivity note wraps inside the left column rather than running under the scope column.
                TextBlock textBlock_Persist = openBuildingMatchOptionsWindow.TextBlock_Persist;
                Rect rect_Persist = textBlock_Persist.TransformToAncestor(root).TransformBounds(new Rect(textBlock_Persist.RenderSize));
                Rect rect_Scope = openBuildingMatchOptionsWindow.StackPanel_BoundingBox.TransformToAncestor(root).TransformBounds(new Rect(openBuildingMatchOptionsWindow.StackPanel_BoundingBox.RenderSize));
                if (rect_Persist.Right > rect_Scope.Left || rect_Persist.Height < 1)
                {
                    failures.Add($"{nameof(OpenBuildingMatchOptionsWindow)}: the sensitivity note lies at {rect_Persist}, into the scope column at {rect_Scope}");
                }
            });

            Assert.Null(exception);
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
