using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.PostgreSQL.UI.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.UI.WPF.Interfaces;
using DiGi.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        /// Verifies the order in which <see cref="UIWebAPIBuildingSolarRadiationSolveTask"/> refuses, against a scripted GIS Web API and with no WPF application (a test host runs none): an unreadable endpoint catalogue ends the run before anything else is asked for; a complete catalogue lets it reach the user interface check, which ends it before the counties are read - no dialog, no sign-in, no other request.
        /// </summary>
        [Fact]
        public async Task UIWebAPIBuildingSolarRadiationSolveTask_Refusal()
        {
            Assert.Null(System.Windows.Application.Current);

            List<EndpointInformation> endpointInformations =
            [
                .. new string?[]
                {
                    DiGi.WebAPI.Query.Path<Building2DController>(nameof(Building2DController.GetCentroidsByCountyIdAsync)),
                    DiGi.WebAPI.Query.Path<BuildingSolarRadiationController>(nameof(BuildingSolarRadiationController.GetReferencesByCountyIdAsync)),
                    DiGi.WebAPI.Query.Path<BuildingSolarRadiationController>(nameof(BuildingSolarRadiationController.SetSolarRadiationsAsync)),
                    DiGi.WebAPI.Query.Path<EPWFileController>(nameof(EPWFileController.GetEPWFileByIdAsync)),
                    DiGi.WebAPI.Query.Path<EPWFileController>(nameof(EPWFileController.GetEPWFileLocationsAsync)),
                }.Select(x => new EndpointInformation("X", "X", x, ["GET"], false, null, null))
            ];
            string json = Core.Convert.ToSystem_String(endpointInformations) ?? throw new InvalidOperationException("The catalogue could not be serialized");

            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler_Unreadable = new(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
            Assert.False(await Run(solarSolveHttpMessageHandler_Unreadable));
            Assert.Equal(new string[] { "/information/endpoints" }, solarSolveHttpMessageHandler_Unreadable.PathAndQueries);

            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler_Complete = new(_ => Response(HttpStatusCode.OK, json));
            Assert.False(await Run(solarSolveHttpMessageHandler_Complete));
            Assert.Equal(new string[] { "/information/endpoints" }, solarSolveHttpMessageHandler_Complete.PathAndQueries);

            static async Task<bool> Run(SolarSolveHttpMessageHandler solarSolveHttpMessageHandler)
            {
                (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveManager(solarSolveHttpMessageHandler);
                using (serviceProvider)
                {
                    ProbeUIWebAPIBuildingSolarRadiationSolveTask probeUIWebAPIBuildingSolarRadiationSolveTask = new(gISWebAPIManager);
                    return await probeUIWebAPIBuildingSolarRadiationSolveTask.RunAsync();
                }
            }
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
