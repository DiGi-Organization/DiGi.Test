using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.PostgreSQL.UI.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.UI.WPF.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that the client-side solar solve (DiGi.GIS.PostgreSQL.UI#48) is offered on the Client tab whenever a Web API manager exists - with or without a converter manager, since a Client machine has none - under the name the operator sees, and that the server row stays where it was.
        /// <para>The registration is what decides whether the row appears, so the registration is what is asserted. Nothing here reaches the network - the tasks are registered, never started.</para>
        /// </summary>
        [Fact]
        public void VisualBackgroundTasks_UIWebAPIBuildingSolarRadiationSolveTask()
        {
            GISWebAPIManager? gISWebAPIManager = WebAPI.Create.GISWebAPIManager("00000000-0000-0000-0000-000000000000");
            Assert.NotNull(gISWebAPIManager);

            List<IVisualBackgroundTask>? visualBackgroundTasks_Client = Create.VisualBackgroundTasks(null, null, gISWebAPIManager, Mode.Client);
            Assert.NotNull(visualBackgroundTasks_Client);
            Assert.Contains(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIWebAPIBuildingSolarRadiationSolveTask).Name && x.Name == "Solve building solar radiation (client)");
            Assert.DoesNotContain(visualBackgroundTasks_Client, x => x.TypeName == typeof(UIPostgreSQLBuildingSolarRadiationSolveTask).Name);

            List<IVisualBackgroundTask>? visualBackgroundTasks_Both = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, gISWebAPIManager, Mode.ServerAndClient);
            Assert.NotNull(visualBackgroundTasks_Both);
            Assert.Contains(visualBackgroundTasks_Both, x => x.TypeName == typeof(UIWebAPIBuildingSolarRadiationSolveTask).Name);
            Assert.Contains(visualBackgroundTasks_Both, x => x.TypeName == typeof(UIPostgreSQLBuildingSolarRadiationSolveTask).Name && x.Name == "Solve building solar radiation");

            List<IVisualBackgroundTask>? visualBackgroundTasks_Server = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, gISWebAPIManager, Mode.Server);
            Assert.NotNull(visualBackgroundTasks_Server);
            Assert.DoesNotContain(visualBackgroundTasks_Server, x => x.TypeName == typeof(UIWebAPIBuildingSolarRadiationSolveTask).Name);

            List<IVisualBackgroundTask>? visualBackgroundTasks_NoManager = Create.VisualBackgroundTasks(new GISPostgreSQLConverterManager(), null, null, Mode.ServerAndClient);
            Assert.NotNull(visualBackgroundTasks_NoManager);
            Assert.DoesNotContain(visualBackgroundTasks_NoManager, x => x.TypeName == typeof(UIWebAPIBuildingSolarRadiationSolveTask).Name);
        }

        /// <summary>
        /// Verifies that <see cref="UIWebAPIBuildingSolarRadiationSolveTask"/> refuses before any request when no WPF application runs (a test host runs none), against a scripted GIS Web API: the user interface check comes first, so no county is read, no dialog opens and no sign-in is sent (DiGi.GIS.PostgreSQL.UI#50).
        /// </summary>
        [Fact]
        public async Task UIWebAPIBuildingSolarRadiationSolveTask_Refusal()
        {
            Assert.Null(System.Windows.Application.Current);

            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler = new(_ => Response(HttpStatusCode.OK, "[]"));
            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveManager(solarSolveHttpMessageHandler);
            using (serviceProvider)
            {
                ProbeUIWebAPIBuildingSolarRadiationSolveTask probeUIWebAPIBuildingSolarRadiationSolveTask = new(gISWebAPIManager);
                Assert.False(await probeUIWebAPIBuildingSolarRadiationSolveTask.RunAsync());
            }

            Assert.Empty(solarSolveHttpMessageHandler.PathAndQueries);
        }

        /// <summary>
        /// Exposes the run of <see cref="UIWebAPIBuildingSolarRadiationSolveTask"/> to the facts.
        /// </summary>
        private sealed class ProbeUIWebAPIBuildingSolarRadiationSolveTask : UIWebAPIBuildingSolarRadiationSolveTask
        {
            public ProbeUIWebAPIBuildingSolarRadiationSolveTask(GISWebAPIManager gISWebAPIManager)
                : base(gISWebAPIManager)
            {
            }

            public Task<bool> RunAsync()
            {
                return ExecuteAsync(new Progress<long>(), CancellationToken.None);
            }
        }
    }
}
