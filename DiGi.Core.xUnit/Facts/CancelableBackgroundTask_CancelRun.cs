using DiGi.Core.Classes;
using DiGi.Core.Enums;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// A task that declines its run from inside it, the way a tray task does when its options dialog is cancelled, unless told to run.
        /// </summary>
        private sealed class TestDecliningBackgroundTask : CancelableBackgroundTask
        {
            public bool Decline { get; set; } = true;

            protected override Task<bool> ExecuteAsync(CancellationToken token)
            {
                if (Decline)
                {
                    CancelRun();
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
        }

        /// <summary>
        /// Verifies that a run ended with <c>CancelRun</c> - a cancelled options dialog - completes as <see cref="CancelableBackgroundTaskStatus.Canceled"/> with no exception, instead of as a failure wrapped in a <see cref="BackgroundTaskFailureException"/> (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#61).
        /// <para>The tray logs every task exception at ERR level, so a declined run must leave <see cref="BackgroundTask.Exception"/> null. The next <see cref="CancelableBackgroundTask.Start"/> runs normally and completes.</para>
        /// </summary>
        [Fact]
        public async Task CancelableBackgroundTask_CancelRun()
        {
            TestDecliningBackgroundTask task = new();

            task.Start();
            for (int i = 0; i < 200 && !task.IsCompleted; i++)
            {
                await Task.Delay(10);
            }

            Assert.True(task.IsCompleted);
            Assert.Null(task.Exception);
            Assert.True(task.IsCanceled);
            Assert.False(task.IsSucceeded);
            Assert.Equal(CancelableBackgroundTaskStatus.Canceled, task.CancelableBackgroundTaskStatus);

            task.Decline = false;
            task.Start();
            for (int i = 0; i < 200 && (task.IsRunning || !task.IsCompleted); i++)
            {
                await Task.Delay(10);
            }

            Assert.Null(task.Exception);
            Assert.False(task.IsCanceled);
            Assert.True(task.IsSucceeded);
            Assert.Equal(CancelableBackgroundTaskStatus.Completed, task.CancelableBackgroundTaskStatus);
        }
    }
}
