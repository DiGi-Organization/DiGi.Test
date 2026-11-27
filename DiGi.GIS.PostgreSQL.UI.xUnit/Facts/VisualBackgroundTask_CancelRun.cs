using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// A task that ends its run the way a tray task does when its options or file dialog is cancelled.
        /// </summary>
        private sealed class TestDialogCancelledBackgroundTask : Core.Classes.CancelableBackgroundTask
        {
            protected override Task<bool> ExecuteAsync(CancellationToken token)
            {
                CancelRun();
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Verifies that a run ended with <c>CancelRun</c> - a cancelled dialog - shows as Canceled on its row and carries no exception, so the tray's <c>Visual</c> wrapper, which logs every task exception as "{Name} failed" at ERR level, logs nothing (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#61).
        /// <para>Before the fix the cancel branches returned false alone, which <see cref="Core.Classes.BackgroundTask"/> wraps in a <see cref="Core.Classes.BackgroundTaskFailureException"/>: the row read Failed and the log got an error. The wrapper is constructed after the task finished, because it refreshes through the WPF dispatcher and a test host runs no WPF application.</para>
        /// </summary>
        [Fact]
        public async Task VisualBackgroundTask_CancelRun_ShowsCanceled()
        {
            TestDialogCancelledBackgroundTask backgroundTask = new();

            backgroundTask.Start();

            int timeoutMs = 1000;
            while (!backgroundTask.IsCompleted && timeoutMs > 0)
            {
                await Task.Delay(10);
                timeoutMs -= 10;
            }

            Assert.True(backgroundTask.IsCompleted);
            Assert.Null(backgroundTask.Exception);

            DiGi.UI.WPF.Classes.VisualBackgroundTask visualBackgroundTask = new(backgroundTask, "Test task", "A task whose dialog was cancelled");

            Assert.Equal("Canceled", visualBackgroundTask.Status);
            Assert.Null(visualBackgroundTask.ExceptionText);
            Assert.Equal("Canceled", visualBackgroundTask.StatusText);
        }
    }
}
