using DiGi.Core.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="UIPostgreSQLStatisticalDataCollectionCreateTableTask"/> ends a run whose folder dialog was cancelled as Canceled with no exception, so the tray logs no error, and still fails a run given a directory that does not exist (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#77).
        /// <para>Before the fix the task was a plain background task and both cases returned false through one check: the row read Failed and the tray logged "[ERR] ... failed" for a cancel. The dialog is skipped through <see cref="UIPostgreSQLStatisticalDataCollectionCreateTableTask.SkipDirectoryDialog"/>, with an empty directory standing for the cancel; neither case reaches the database, so the converter has no connection.</para>
        /// </summary>
        [Fact]
        public async Task UIPostgreSQLStatisticalDataCollectionCreateTableTask_CancelAndRefusal()
        {
            UIPostgreSQLStatisticalDataCollectionCreateTableTask uIPostgreSQLStatisticalDataCollectionCreateTableTask_Cancel = new(new StatisticalDataCollectionPostgreSQLConverter(null)) { SkipDirectoryDialog = true, SourceDirectory = null };
            await RunToCompletionAsync(uIPostgreSQLStatisticalDataCollectionCreateTableTask_Cancel);

            Assert.Null(uIPostgreSQLStatisticalDataCollectionCreateTableTask_Cancel.Exception);
            Assert.True(uIPostgreSQLStatisticalDataCollectionCreateTableTask_Cancel.IsCanceled);
            Assert.Equal(CancelableBackgroundTaskStatus.Canceled, uIPostgreSQLStatisticalDataCollectionCreateTableTask_Cancel.CancelableBackgroundTaskStatus);

            string directory_Absent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            UIPostgreSQLStatisticalDataCollectionCreateTableTask uIPostgreSQLStatisticalDataCollectionCreateTableTask_Absent = new(new StatisticalDataCollectionPostgreSQLConverter(null)) { SkipDirectoryDialog = true, SourceDirectory = directory_Absent };
            await RunToCompletionAsync(uIPostgreSQLStatisticalDataCollectionCreateTableTask_Absent);

            Assert.False(uIPostgreSQLStatisticalDataCollectionCreateTableTask_Absent.IsCanceled);
            Assert.IsType<Core.Classes.BackgroundTaskFailureException>(uIPostgreSQLStatisticalDataCollectionCreateTableTask_Absent.Exception);
            Assert.Equal(CancelableBackgroundTaskStatus.Failed, uIPostgreSQLStatisticalDataCollectionCreateTableTask_Absent.CancelableBackgroundTaskStatus);
        }

        /// <summary>
        /// Starts a background task and waits up to 2 s for it to complete.
        /// </summary>
        /// <param name="backgroundTask">The task to run.</param>
        /// <returns>A task that completes when the background task has completed or the wait has run out.</returns>
        private static async Task RunToCompletionAsync(Core.Classes.BackgroundTask backgroundTask)
        {
            backgroundTask.Start();

            int timeoutMs = 2000;
            while (!backgroundTask.IsCompleted && timeoutMs > 0)
            {
                await Task.Delay(10);
                timeoutMs -= 10;
            }

            Assert.True(backgroundTask.IsCompleted);
        }
    }
}
