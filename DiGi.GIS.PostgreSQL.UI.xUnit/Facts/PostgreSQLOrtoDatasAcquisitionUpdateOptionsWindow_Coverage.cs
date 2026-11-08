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
        /// Verifies that the acquisition backfill options window reads the coverage of the county parts in the background and shows a progress bar in the bottom-left corner only while it does (DiGi.GIS.PostgreSQL#134).
        /// <para>Four windows, the four reads of <c>Coding - WPF Deferred Loading.md</c> §5: none, which never shows the bar or annotates a part; a pending one, which shows the bar until the coverage is set and then annotates in place without losing the selection; one completing at once, and one throwing at once - the last says the coverage could not be read and annotates nothing. A part absent from a read result is annotated as holding no OrtoDatas, which is a measured answer, unlike a failed read. The pending read is never completed: its continuation would be posted to a dispatcher this thread does not pump, so the coverage is set directly instead, which is the call the read ends in.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_Coverage()
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
            string? status_Completed = null;
            string? status_Failed = null;

            List<AdministrativeAreal2DReference> administrativeAreal2DReferences =
            [
                new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 73485, Code = "2212", Name = "slupski", AdministrativeArealType = AdministrativeArealType.County }
            ];

            Dictionary<int, OrtoDataAcquisitionCoverageResult> ortoDataAcquisitionCoverageResults = new()
            {
                [73485] = new OrtoDataAcquisitionCoverageResult(73485, 44_804, 40_000, 39_500, 300_000, 20_000, 5_000, 1_200, null, null, null)
            };

            Thread thread = new(() =>
            {
                try
                {
                    PostgreSQLOrtoDatasAcquisitionUpdateOptions postgreSQLOrtoDatasAcquisitionUpdateOptions = new() { CountyIds = [4816, 73485] };

                    PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow window_NoRead = new(postgreSQLOrtoDatasAcquisitionUpdateOptions, administrativeAreal2DReferences);
                    visibility_NoRead = ProgressBar(window_NoRead).Visibility;
                    texts_NoRead = Texts(window_NoRead);
                    status_NoRead = Status(window_NoRead);

                    TaskCompletionSource<Dictionary<int, OrtoDataAcquisitionCoverageResult>?> taskCompletionSource = new();
                    PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow window_Pending = new(postgreSQLOrtoDatasAcquisitionUpdateOptions, administrativeAreal2DReferences, _ => taskCompletionSource.Task);
                    visibility_Pending = ProgressBar(window_Pending).Visibility;
                    texts_Pending = Texts(window_Pending);
                    status_Pending = Status(window_Pending);
                    window_Pending.SetOrtoDataAcquisitionCoverageResults(ortoDataAcquisitionCoverageResults);
                    visibility_PendingSet = ProgressBar(window_Pending).Visibility;
                    texts_PendingSet = Texts(window_Pending);
                    selectedCount_PendingSet = ((ListBoxControl)window_Pending.FindName("ListBoxControl_Counties")!).GetItems<AdministrativeAreal2DReference>()?.Count ?? 0;

                    PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow window_Completed = new(postgreSQLOrtoDatasAcquisitionUpdateOptions, administrativeAreal2DReferences, _ => Task.FromResult<Dictionary<int, OrtoDataAcquisitionCoverageResult>?>(ortoDataAcquisitionCoverageResults));
                    visibility_Completed = ProgressBar(window_Completed).Visibility;
                    texts_Completed = Texts(window_Completed);
                    status_Completed = Status(window_Completed);

                    PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow window_Failed = new(postgreSQLOrtoDatasAcquisitionUpdateOptions, administrativeAreal2DReferences, _ => Task.FromException<Dictionary<int, OrtoDataAcquisitionCoverageResult>?>(new InvalidOperationException("unreachable")));
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

            List<string?> texts_Bare = ["0201 boleslawiecki (id 4816)", "2212 slupski (id 73485)"];
            List<string?> texts_Annotated = ["0201 boleslawiecki (id 4816) - no OrtoDatas", "2212 slupski (id 73485) - 44,804 series, 4,804 not backfilled, 325,000 of 326,200 images dated"];

            Assert.Equal(Visibility.Collapsed, visibility_NoRead);
            Assert.Equal(texts_Bare, texts_NoRead);
            Assert.True(string.IsNullOrEmpty(status_NoRead));

            Assert.Equal(Visibility.Visible, visibility_Pending);
            Assert.Equal(texts_Bare, texts_Pending);
            Assert.Equal("Reading acquisition-date coverage...", status_Pending);
            Assert.Equal(Visibility.Collapsed, visibility_PendingSet);
            Assert.Equal(texts_Annotated, texts_PendingSet);
            Assert.Equal(2, selectedCount_PendingSet);

            Assert.Equal(Visibility.Collapsed, visibility_Completed);
            Assert.Equal(texts_Annotated, texts_Completed);
            Assert.Equal("OrtoDatas series per part, how many are not backfilled yet, and how many of their images are dated", status_Completed);

            Assert.Equal(Visibility.Collapsed, visibility_Failed);
            Assert.Equal(texts_Bare, texts_Failed);
            Assert.Equal("Acquisition-date coverage could not be read - no county part is annotated", status_Failed);

            static ProgressBar ProgressBar(Window window)
            {
                return (ProgressBar)window.FindName("ProgressBar_Coverage")!;
            }

            static string? Status(Window window)
            {
                return ((Label)window.FindName("Label_Coverage")!).Content as string;
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
