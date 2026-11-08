using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.PostgreSQL.UI.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.UI.WPF.Interfaces;
using DiGi.User.PostgreSQL.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Create.VisualBackgroundTasks(GISPostgreSQLConverterManager?, UserPostgreSQLConverterManager?, GISWebAPIManager?, Mode, string?)"/> handles null managers without throwing exceptions, ensuring that a missing WebAPI configuration does not crash the server task list.
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks()
        {
            // Null converter manager and null WebAPI manager should return empty list across all modes
            List<IVisualBackgroundTask>? visualBackgroundTasks_NullManagers_Server = Create.VisualBackgroundTasks(null, null, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_NullManagers_Server);
            Assert.Empty(visualBackgroundTasks_NullManagers_Server);

            List<IVisualBackgroundTask>? visualBackgroundTasks_NullManagers_Client = Create.VisualBackgroundTasks(null, null, null, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_NullManagers_Client);
            Assert.Empty(visualBackgroundTasks_NullManagers_Client);

            List<IVisualBackgroundTask>? visualBackgroundTasks_NullManagers_Both = Create.VisualBackgroundTasks(null, null, null, Mode.ServerAndClient);
            Assert.NotNull(visualBackgroundTasks_NullManagers_Both);
            Assert.Empty(visualBackgroundTasks_NullManagers_Both);

            // With converter manager but null WebAPI manager (the issue #1 scenario)
            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();

            List<IVisualBackgroundTask>? visualBackgroundTasks_Server = Create.VisualBackgroundTasks(gISPostgreSQLConverterManager, null, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_Server);
            Assert.NotEmpty(visualBackgroundTasks_Server);

            // OrtoDatasTask requires gISWebAPIManager and must be excluded when it is null
            Assert.DoesNotContain(visualBackgroundTasks_Server, x => x.TypeName == typeof(OrtoDatasTask).Name);

            // Client mode with null WebAPI manager should return empty list
            List<IVisualBackgroundTask>? visualBackgroundTasks_Client = Create.VisualBackgroundTasks(gISPostgreSQLConverterManager, null, null, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_Client);
            Assert.Empty(visualBackgroundTasks_Client);
        }

        /// <summary>
        /// Tests that the Year Built prediction task, and the YOLO training task hosted by the same runner, are offered where they can actually run, and nowhere else.
        /// <para>The task reads the county rows the dialog is scoped from over the Web API, so a null manager has to leave it out rather than produce a row that throws when it is clicked - the failure DiGi-Organization/DiGi.GIS.PostgreSQL.UI#1 was about. It belongs to the client side for the same reason: it holds no PostgreSQL converter and reaches the estate only through the API.</para>
        /// <para>It is registered rather than constructed here, because constructing it proves nothing - the registration is what decides whether the row appears at all, and a task added under the wrong mode builds and tests green while being invisible in the tab an operator opens.</para>
        /// <para>The runner it hands the run to is an optional part of the deployment, so the row is gated on that being present too. A machine that will never score a building must not be offered a task whose only possible outcome is that it found no executable - which would be discovered after the counties had been chosen and the imagery scoped.</para>
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks_UIYearBuiltPredictionsTask()
        {
            // Any non-empty key builds a manager. Nothing here reaches the network - the task is registered, never started.
            GISWebAPIManager? gISWebAPIManager = WebAPI.Create.GISWebAPIManager("00000000-0000-0000-0000-000000000000");
            Assert.NotNull(gISWebAPIManager);

            // A file standing in for the runner is enough: what decides the row is whether something is there, and
            // whether it starts is a different question, asked later and by a different piece of code.
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string path_ConsoleApp = Path.Combine(directory, Constants.FileName.YearBuiltPredictionConsoleApp);

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path_ConsoleApp, string.Empty);

                List<IVisualBackgroundTask>? visualBackgroundTasks_Client = Create.VisualBackgroundTasks(null, null, gISWebAPIManager, Mode.Client, path_ConsoleApp);
                Assert.NotNull(visualBackgroundTasks_Client);
                Assert.Contains(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIYearBuiltPredictionsTask).Name);
                Assert.Contains(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIYOLOTrainingTask).Name);

                // The server tab holds the tasks driven by a PostgreSQL converter; this one is not among them.
                List<IVisualBackgroundTask>? visualBackgroundTasks_Server = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, gISWebAPIManager, Mode.Server, path_ConsoleApp);
                Assert.NotNull(visualBackgroundTasks_Server);
                Assert.DoesNotContain(visualBackgroundTasks_Server, x => x.TypeName == typeof(UIYearBuiltPredictionsTask).Name);
                Assert.DoesNotContain(visualBackgroundTasks_Server, x => x.TypeName == typeof(UIYOLOTrainingTask).Name);

                // Without a manager there is nothing to read the counties with, so the row must not be offered at all.
                List<IVisualBackgroundTask>? visualBackgroundTasks_NoManager = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.ServerAndClient, path_ConsoleApp);
                Assert.NotNull(visualBackgroundTasks_NoManager);
                Assert.DoesNotContain(visualBackgroundTasks_NoManager, x => x.TypeName == typeof(UIYearBuiltPredictionsTask).Name);
                Assert.DoesNotContain(visualBackgroundTasks_NoManager, x => x.TypeName == typeof(UIYOLOTrainingTask).Name);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }

            // No runner anywhere: the named path does not exist, and neither does any candidate the resolver probes
            // below it from a test run - so the row is withheld rather than offered as one that cannot work.
            string path_Absent = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), Constants.FileName.YearBuiltPredictionConsoleApp);

            List<IVisualBackgroundTask>? visualBackgroundTasks_NoRunner = Create.VisualBackgroundTasks(null, null, gISWebAPIManager, Mode.Client, path_Absent);
            Assert.NotNull(visualBackgroundTasks_NoRunner);
            Assert.DoesNotContain(visualBackgroundTasks_NoRunner, x => x.TypeName == typeof(UIYearBuiltPredictionsTask).Name);
            Assert.DoesNotContain(visualBackgroundTasks_NoRunner, x => x.TypeName == typeof(UIYOLOTrainingTask).Name);

            // The rest of the client list is unaffected by the runner being absent
            Assert.NotEmpty(visualBackgroundTasks_NoRunner);
        }

        /// <summary>
        /// Tests that the create user task is offered where a user database is actually named, and nowhere else.
        /// <para>Users live in their own database, reached through User_PostgreSQL_Main.conf rather than through the configuration the rest of this application runs on. Where that file is absent <c>Create.UserPostgreSQLConverterManager</c> answers a manager holding no converter, and a row whose only possible outcome is that it could not connect must not be offered - the rule the Year Built task is gated by as well.</para>
        /// <para>The registration is what decides whether the row appears, so the registration is what is asserted: constructing the task proves nothing about the tab an operator opens.</para>
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks_UIPostgreSQLUserCreateTask()
        {
            // No manager at all - nothing names a user database.
            List<IVisualBackgroundTask>? visualBackgroundTasks_NoManager = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_NoManager);
            Assert.DoesNotContain(visualBackgroundTasks_NoManager, x => x.TypeName == typeof(UIPostgreSQLUserCreateTask).Name);

            // A manager holding no converter is the shape a missing conf actually produces, and so the one this gate has to catch.
            List<IVisualBackgroundTask>? visualBackgroundTasks_NoConverter = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), new UserPostgreSQLConverterManager(), null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_NoConverter);
            Assert.DoesNotContain(visualBackgroundTasks_NoConverter, x => x.TypeName == typeof(UIPostgreSQLUserCreateTask).Name);

            // Nothing here reaches a database - the task is registered, never started - so the connection data only has to exist.
            UserPostgreSQLConverterManager userPostgreSQLConverterManager = new();
            Assert.True(userPostgreSQLConverterManager.Add(new UserPostgreSQLConverter(new ConnectionData("localhost", "test-mock-user", "test-mock-password", "user", 5432))));

            List<IVisualBackgroundTask>? visualBackgroundTasks_Server = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), userPostgreSQLConverterManager, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_Server);
            Assert.Contains(visualBackgroundTasks_Server, x => x.TypeName == typeof(UIPostgreSQLUserCreateTask).Name);

            // The server tab holds the tasks driven by a PostgreSQL converter; the client tab does not.
            List<IVisualBackgroundTask>? visualBackgroundTasks_Client = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), userPostgreSQLConverterManager, null, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_Client);
            Assert.DoesNotContain(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIPostgreSQLUserCreateTask).Name);
        }

        /// <summary>
        /// Tests that the external components area task is offered on the server tab, under the name the operator sees, and nowhere else.
        /// <para>The task reads the stored building models and writes the building data table directly from the storage database, so it belongs on the server side beside the Update building data row it completes, and a missing converter manager has to leave it out rather than produce a row that throws when it is clicked.</para>
        /// <para>The registration is what decides whether the row appears, so the registration is what is asserted: constructing the task proves nothing about the tab an operator opens.</para>
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks_UIPostgreSQLBuildingDataExternalComponentsUpdateTask()
        {
            // Nothing here reaches a database - the task is registered, never started.
            List<IVisualBackgroundTask>? visualBackgroundTasks_Server = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_Server);
            Assert.Contains(visualBackgroundTasks_Server, x => x.TypeName == typeof(UIPostgreSQLBuildingDataExternalComponentsUpdateTask).Name && x.Name == "Update external components area");

            // The client tab holds the tasks driven by the Web API; this one is not among them.
            List<IVisualBackgroundTask>? visualBackgroundTasks_Client = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_Client);
            Assert.DoesNotContain(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIPostgreSQLBuildingDataExternalComponentsUpdateTask).Name);

            // Without a converter manager there is nothing to read the stored models with, so the row must not be offered at all.
            List<IVisualBackgroundTask>? visualBackgroundTasks_NoManager = Create.VisualBackgroundTasks(null, null, null, Mode.ServerAndClient);
            Assert.NotNull(visualBackgroundTasks_NoManager);
            Assert.Empty(visualBackgroundTasks_NoManager);
        }

        private class TestRefusingBackgroundTask : Core.Classes.BackgroundTask
        {
            protected override Task<bool> ExecuteAsync()
            {
                // A deliberate refusal: reports failure without throwing, the way a task declining to act
                // does - and the situation the fallback wrap exists for.
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Tests that a task failing without an exception still surfaces a reason on its row: the fallback <see cref="Core.Classes.BackgroundTaskFailureException"/> reaches the hover text and the clipboard text of the visual wrapper, rather than leaving a Failed status whose reason is buried in the log file.
        /// <para>The wrapper is constructed only after the task has finished, because it refreshes through the WPF dispatcher on the task's events and a test host runs no WPF application.</para>
        /// </summary>
        [Fact]
        public async Task VisualBackgroundTask_FailureWithoutException_SurfacesReason()
        {
            TestRefusingBackgroundTask backgroundTask = new();

            backgroundTask.Start();

            int timeoutMs = 1000;
            while (!backgroundTask.IsCompleted && timeoutMs > 0)
            {
                await Task.Delay(10);
                timeoutMs -= 10;
            }

            Assert.True(backgroundTask.IsCompleted);
            Assert.NotNull(backgroundTask.Exception);

            DiGi.UI.WPF.Classes.VisualBackgroundTask visualBackgroundTask = new(backgroundTask, "Test task", "A task that refuses");

            // The hover text names the failure type and carries the message rather than staying null, which is what turns the tooltip off in the task list
            Assert.NotNull(visualBackgroundTask.ExceptionText);
            Assert.StartsWith("BackgroundTaskFailureException", visualBackgroundTask.ExceptionText);
            Assert.Contains("The task reported failure without an exception", visualBackgroundTask.ExceptionText);

            // The clipboard text carries the same failure for Ctrl+C rather than falling back to the bare status
            Assert.NotNull(visualBackgroundTask.StatusText);
            Assert.Contains("The task reported failure without an exception", visualBackgroundTask.StatusText);
        }
    }
}
