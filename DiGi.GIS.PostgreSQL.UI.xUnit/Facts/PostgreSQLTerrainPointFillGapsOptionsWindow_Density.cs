using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.UI.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the terrain point fill gaps options window reads the county densities in the background and shows a progress bar in the bottom-left corner only while it does (DiGi.GIS.PostgreSQL.UI#41).
        /// <para>Four windows, the four reads of <c>Coding - WPF Deferred Loading.md</c> §5: none, which never shows the bar or annotates a county; a pending one, which shows the bar until the densities are set and then annotates in place without losing the selection; one completing at once, and one throwing at once - the last says the densities could not be read and annotates nothing. The pending read is never completed: its continuation would be posted to a dispatcher this thread does not pump, so the densities are set directly instead, which is the call the read ends in.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptionsWindow_Density()
        {
            Exception? exception = null;
            Visibility visibility_NoRead = Visibility.Visible;
            Visibility visibility_Pending = Visibility.Collapsed;
            Visibility visibility_PendingSet = Visibility.Visible;
            Visibility visibility_Completed = Visibility.Visible;
            Visibility visibility_Failed = Visibility.Visible;
            List<string?> texts_NoRead = [];
            List<string?> texts_Pending = [];
            List<string?> texts_PendingSet = [];
            List<string?> texts_Completed = [];
            List<string?> texts_Failed = [];
            int selectedCount_PendingSet = -1;
            string? status_NoRead = null;
            string? status_Pending = null;
            string? status_Failed = null;

            List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
            [
                new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
            ];

            Dictionary<int, TerrainPointDensityResult> terrainPointDensityResults = new()
            {
                [22138] = new TerrainPointDensityResult(22138, 1234, 1000.0, 0.25, 2.0, null, null)
            };

            Thread thread = new(() =>
            {
                try
                {
                    PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions = new() { CountyIds = [4816, 22138] };

                    PostgreSQLTerrainPointFillGapsOptionsWindow window_NoRead = new(postgreSQLTerrainPointFillGapsOptions, administrativeAreal2DReferences);
                    visibility_NoRead = ProgressBar(window_NoRead).Visibility;
                    texts_NoRead = Texts(window_NoRead);
                    status_NoRead = Status(window_NoRead);

                    TaskCompletionSource<Dictionary<int, TerrainPointDensityResult>?> taskCompletionSource = new();
                    PostgreSQLTerrainPointFillGapsOptionsWindow window_Pending = new(postgreSQLTerrainPointFillGapsOptions, administrativeAreal2DReferences, _ => taskCompletionSource.Task);
                    visibility_Pending = ProgressBar(window_Pending).Visibility;
                    texts_Pending = Texts(window_Pending);
                    status_Pending = Status(window_Pending);
                    window_Pending.SetTerrainPointDensityResults(terrainPointDensityResults);
                    visibility_PendingSet = ProgressBar(window_Pending).Visibility;
                    texts_PendingSet = Texts(window_Pending);
                    selectedCount_PendingSet = ((ListBoxControl)window_Pending.FindName("ListBoxControl_Counties")!).GetItems<AdministrativeAreal2DReference>()?.Count ?? 0;

                    PostgreSQLTerrainPointFillGapsOptionsWindow window_Completed = new(postgreSQLTerrainPointFillGapsOptions, administrativeAreal2DReferences, _ => Task.FromResult<Dictionary<int, TerrainPointDensityResult>?>(terrainPointDensityResults));
                    visibility_Completed = ProgressBar(window_Completed).Visibility;
                    texts_Completed = Texts(window_Completed);

                    PostgreSQLTerrainPointFillGapsOptionsWindow window_Failed = new(postgreSQLTerrainPointFillGapsOptions, administrativeAreal2DReferences, _ => Task.FromException<Dictionary<int, TerrainPointDensityResult>?>(new InvalidOperationException("unreachable")));
                    visibility_Failed = ProgressBar(window_Failed).Visibility;
                    texts_Failed = Texts(window_Failed);
                    status_Failed = Status(window_Failed);
                }
                catch (Exception exception_Thread)
                {
                    exception = exception_Thread;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(exception);

            List<string?> texts_Bare = ["0201 boleslawiecki (id 4816)", "2412 rybnicki (id 22138)"];
            List<string?> texts_Annotated = ["0201 boleslawiecki (id 4816)", "2412 rybnicki (id 22138) - 1,234 pts (0.25 pts/m², ~2 m)"];

            Assert.Equal(Visibility.Collapsed, visibility_NoRead);
            Assert.Equal(texts_Bare, texts_NoRead);
            Assert.True(string.IsNullOrEmpty(status_NoRead));

            Assert.Equal(Visibility.Visible, visibility_Pending);
            Assert.Equal(texts_Bare, texts_Pending);
            Assert.Equal("Reading terrain point densities...", status_Pending);
            Assert.Equal(Visibility.Collapsed, visibility_PendingSet);
            Assert.Equal(texts_Annotated, texts_PendingSet);
            Assert.Equal(2, selectedCount_PendingSet);

            Assert.Equal(Visibility.Collapsed, visibility_Completed);
            Assert.Equal(texts_Annotated, texts_Completed);

            Assert.Equal(Visibility.Collapsed, visibility_Failed);
            Assert.Equal(texts_Bare, texts_Failed);
            Assert.Equal("Terrain point densities could not be read - no county is annotated", status_Failed);

            static ProgressBar ProgressBar(Window window)
            {
                return (ProgressBar)window.FindName("ProgressBar_Density")!;
            }

            static string? Status(Window window)
            {
                return ((Label)window.FindName("Label_Density")!).Content as string;
            }

            static List<string?> Texts(Window window)
            {
                List<string?> result = [];
                foreach (object @object in ((ListBox)((ListBoxControl)window.FindName("ListBoxControl_Counties")!).FindName("ListBox_Main")!).Items)
                {
                    result.Add((@object as ListBoxItem)?.Content as string);
                }

                return result;
            }
        }
    }
}
