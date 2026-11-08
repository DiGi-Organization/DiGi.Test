using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Windows;
using DiGi.GIS.YOLO.UI.Classes;
using DiGi.UI.WPF.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that closing the Year Built prediction options window before its coverage arrives cancels the read, hides the progress bar and leaves the county list untouched (<c>Coding - WPF Deferred Loading.md</c> §6).
        /// </summary>
        [Fact]
        public void YearBuiltPredictionsOptionsWindow_CloseBeforeData()
        {
            CloseBeforeDataResult closeBeforeDataResult = CloseBeforeData<Dictionary<int, double>>(
                xAsync => new YearBuiltPredictionsOptionsWindow(new YearBuiltPredictionPipelineOptions(), CloseBeforeDataCounties(), xAsync),
                "ProgressBar_Coverage",
                "Label_Coverage");

            AssertCloseBeforeData(closeBeforeDataResult, "Reading prediction coverage...");
        }

        /// <summary>
        /// Verifies that closing the terrain point update options window before its densities arrive cancels the read, hides the progress bar and leaves the county list untouched (<c>Coding - WPF Deferred Loading.md</c> §6, DiGi.GIS.PostgreSQL.UI#41).
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointUpdateOptionsWindow_CloseBeforeData()
        {
            CloseBeforeDataResult closeBeforeDataResult = CloseBeforeData<Dictionary<int, TerrainPointDensityResult>>(
                xAsync => new PostgreSQLTerrainPointUpdateOptionsWindow(new PostgreSQLTerrainPointUpdateOptions(), CloseBeforeDataCounties(), xAsync),
                "ProgressBar_Density",
                "Label_Density");

            AssertCloseBeforeData(closeBeforeDataResult, "Reading terrain point densities...");
        }

        /// <summary>
        /// Verifies that closing the terrain point fill gaps options window before its densities arrive cancels the read, hides the progress bar and leaves the county list untouched (<c>Coding - WPF Deferred Loading.md</c> §6, DiGi.GIS.PostgreSQL.UI#41).
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptionsWindow_CloseBeforeData()
        {
            CloseBeforeDataResult closeBeforeDataResult = CloseBeforeData<Dictionary<int, TerrainPointDensityResult>>(
                xAsync => new PostgreSQLTerrainPointFillGapsOptionsWindow(new PostgreSQLTerrainPointFillGapsOptions(), CloseBeforeDataCounties(), xAsync),
                "ProgressBar_Density",
                "Label_Density");

            AssertCloseBeforeData(closeBeforeDataResult, "Reading terrain point densities...");
        }

        /// <summary>
        /// Verifies that closing the acquisition backfill options window before its coverage arrives cancels the read, hides the progress bar and leaves the county list untouched (<c>Coding - WPF Deferred Loading.md</c> §6, DiGi.GIS.PostgreSQL#134).
        /// </summary>
        [Fact]
        public void PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow_CloseBeforeData()
        {
            CloseBeforeDataResult closeBeforeDataResult = CloseBeforeData<Dictionary<int, OrtoDataAcquisitionCoverageResult>>(
                xAsync => new PostgreSQLOrtoDatasAcquisitionUpdateOptionsWindow(new PostgreSQLOrtoDatasAcquisitionUpdateOptions(), CloseBeforeDataCounties(), xAsync),
                "ProgressBar_Coverage",
                "Label_Coverage");

            AssertCloseBeforeData(closeBeforeDataResult, "Reading acquisition-date coverage...");
        }

        private sealed record CloseBeforeDataResult(Exception? Exception, Visibility Visibility_Before, bool Cancelled, Visibility Visibility_After, List<string?> Texts_Before, List<string?> Texts_After, string? Status_After);

        private static List<AdministrativeAreal2DReference> CloseBeforeDataCounties()
        {
            return
            [
                new() { Id = 4816, Code = "0201", Name = "boleslawiecki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 22138, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County }
            ];
        }

        /// <summary>
        /// Opens a window with a read that only ends when its token is cancelled, closes the window, then pumps the dispatcher so the read's continuation runs.
        /// <para>The thread is given a <see cref="DispatcherSynchronizationContext"/> first, as the tray application's user interface thread has: without it the window's await would resume on the thread pool rather than being posted to this dispatcher, and the fact would exercise a path the application never takes.</para>
        /// </summary>
        private static CloseBeforeDataResult CloseBeforeData<T>(Func<Func<CancellationToken, Task<T?>>, Window> createWindow, string progressBarName, string labelName) where T : class
        {
            CloseBeforeDataResult? result = null;

            Thread thread = new(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                    CancellationToken cancellationToken = default;
                    TaskCompletionSource<T?> taskCompletionSource = new();

                    Window window = createWindow(token =>
                    {
                        cancellationToken = token;
                        token.Register(() => taskCompletionSource.TrySetCanceled(token));
                        return taskCompletionSource.Task;
                    });

                    Visibility visibility_Before = ((ProgressBar)window.FindName(progressBarName)!).Visibility;
                    List<string?> texts_Before = Texts(window);

                    window.Close();
                    bool cancelled = cancellationToken.IsCancellationRequested;

                    DispatcherFrame dispatcherFrame = new();
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => dispatcherFrame.Continue = false));
                    Dispatcher.PushFrame(dispatcherFrame);

                    result = new CloseBeforeDataResult(null, visibility_Before, cancelled, ((ProgressBar)window.FindName(progressBarName)!).Visibility, texts_Before, Texts(window), ((Label)window.FindName(labelName)!).Content as string);
                }
                catch (Exception exception)
                {
                    result = new CloseBeforeDataResult(exception, Visibility.Collapsed, false, Visibility.Visible, [], [], null);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return result!;

            static List<string?> Texts(Window window)
            {
                List<string?> texts = [];
                foreach (object @object in ((ListBox)((ListBoxControl)window.FindName("ListBoxControl_Counties")!).FindName("ListBox_Main")!).Items)
                {
                    texts.Add((@object as ListBoxItem)?.Content as string);
                }

                return texts;
            }
        }

        private static void AssertCloseBeforeData(CloseBeforeDataResult closeBeforeDataResult, string status_Reading)
        {
            Assert.Null(closeBeforeDataResult.Exception);

            // Reading when the window closed.
            Assert.Equal(Visibility.Visible, closeBeforeDataResult.Visibility_Before);

            // Closing cancelled the token the read was handed.
            Assert.True(closeBeforeDataResult.Cancelled);

            // The cancelled read returned before the set: the bar is hidden by the finally, the labels and the status are
            // as the window closed with them.
            Assert.Equal(Visibility.Collapsed, closeBeforeDataResult.Visibility_After);
            Assert.Equal(closeBeforeDataResult.Texts_Before, closeBeforeDataResult.Texts_After);
            Assert.Equal(["0201 boleslawiecki (id 4816)", "2412 rybnicki (id 22138)"], closeBeforeDataResult.Texts_After);
            Assert.Equal(status_Reading, closeBeforeDataResult.Status_After);
        }
    }
}
