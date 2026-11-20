using DiGi.CityGML.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
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
        /// Verifies that <see cref="UIBuildingModelsCourtyardAuditTask"/> tells an empty county part from a failed read of its references, against a scripted GIS Web API (DiGi.GIS.PostgreSQL.UI#51).
        /// <para>A 404 is a part with nothing to audit and the run succeeds. A 500 fails the run and lists the part under the failed reads; neither is checkpointed, so a resumed run asks again.</para>
        /// </summary>
        [Fact]
        public async Task UIBuildingModelsCourtyardAuditTask_References()
        {
            (bool succeeded, string directory) = await RunUIBuildingModelsCourtyardAuditTaskAsync("References_404", x => x.Contains("/referencesbycountyid", StringComparison.OrdinalIgnoreCase) ? new HttpResponseMessage(HttpStatusCode.NotFound) : null);
            Assert.True(succeeded, "A part with no buildings is a valid outcome.");
            Assert.DoesNotContain("=== FAILED ===", File.ReadAllText(Path.Combine(directory, "BuildingModels_CourtyardAudit_Summary.txt")), StringComparison.Ordinal);

            (succeeded, directory) = await RunUIBuildingModelsCourtyardAuditTaskAsync("References_500", x => x.Contains("/referencesbycountyid", StringComparison.OrdinalIgnoreCase) ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : null);
            Assert.False(succeeded, "A failed references read must fail the run.");
            Assert.Contains("1465;5;References;", File.ReadAllText(Path.Combine(directory, "BuildingModels_CourtyardAudit_Summary.txt")), StringComparison.Ordinal);
            Assert.Empty(CourtyardAuditCheckpoint(directory));
        }

        /// <summary>
        /// Verifies that <see cref="UIBuildingModelsCourtyardAuditTask"/> tells a reference with no stored source from a failed read of its source, against a scripted GIS Web API (DiGi.GIS.PostgreSQL.UI#51).
        /// <para>A 204 is a reference with no courtyard: it is recorded as <c>NoCourtyard</c>, the part is checkpointed and the run succeeds. A 500 records no row - a failed read is not a finding - leaves the part uncheckpointed so a resumed run redoes it, and fails the run.</para>
        /// </summary>
        [Fact]
        public async Task UIBuildingModelsCourtyardAuditTask_Sources()
        {
            (bool succeeded, string directory) = await RunUIBuildingModelsCourtyardAuditTaskAsync("Sources_204", x => CourtyardSourcesReadResponse(x, new HttpResponseMessage(HttpStatusCode.NoContent), HttpStatusCode.NotFound));
            Assert.True(succeeded, "A reference with no stored source is a valid outcome.");
            Assert.Contains(File.ReadAllLines(Path.Combine(directory, "BuildingModels_CourtyardAudit.csv")), x => x.EndsWith(";NoCourtyard", StringComparison.Ordinal));
            Assert.Equal(["5"], CourtyardAuditCheckpoint(directory));

            (succeeded, directory) = await RunUIBuildingModelsCourtyardAuditTaskAsync("Sources_500", x => CourtyardSourcesReadResponse(x, new HttpResponseMessage(HttpStatusCode.InternalServerError), HttpStatusCode.NotFound));
            Assert.False(succeeded, "A failed source read must fail the run.");
            Assert.DoesNotContain(File.ReadAllLines(Path.Combine(directory, "BuildingModels_CourtyardAudit.csv")), x => x.EndsWith(";NoCourtyard", StringComparison.Ordinal));
            Assert.Contains("1465;5;Buildings;1", File.ReadAllText(Path.Combine(directory, "BuildingModels_CourtyardAudit_Summary.txt")), StringComparison.Ordinal);
            Assert.Empty(CourtyardAuditCheckpoint(directory));
        }

        /// <summary>
        /// Verifies that <see cref="UIBuildingModelsCourtyardAuditTask"/> tells a courtyard source with no stored model from a failed read of the stored models, against a scripted GIS Web API (DiGi.GIS.PostgreSQL.UI#51).
        /// <para>A 404 records the reference as <c>Missing</c> and the run succeeds. A 500 records no <c>Missing</c> row, lists the batch under the failed reads, leaves the part uncheckpointed and fails the run.</para>
        /// </summary>
        [Fact]
        public async Task UIBuildingModelsCourtyardAuditTask_BuildingModels()
        {
            string json_Source = Core.Convert.ToSystem_String(CityGML_Building_1465_Courtyard()) ?? throw new InvalidOperationException("The source could not be serialized");

            (bool succeeded, string directory) = await RunUIBuildingModelsCourtyardAuditTaskAsync("BuildingModels_404", x => CourtyardSourcesReadResponse(x, Response(HttpStatusCode.OK, json_Source), HttpStatusCode.NotFound));
            Assert.True(succeeded, "A courtyard source with no stored model is a valid outcome.");
            Assert.Contains(File.ReadAllLines(Path.Combine(directory, "BuildingModels_CourtyardAudit.csv")), x => x.EndsWith(";yes;no;;Missing", StringComparison.Ordinal));
            Assert.Equal(["5"], CourtyardAuditCheckpoint(directory));

            (succeeded, directory) = await RunUIBuildingModelsCourtyardAuditTaskAsync("BuildingModels_500", x => CourtyardSourcesReadResponse(x, Response(HttpStatusCode.OK, json_Source), HttpStatusCode.InternalServerError));
            Assert.False(succeeded, "A failed stored-model read must fail the run.");
            Assert.DoesNotContain(File.ReadAllLines(Path.Combine(directory, "BuildingModels_CourtyardAudit.csv")), x => x.EndsWith(";Missing", StringComparison.Ordinal));
            Assert.Contains("1465;5;BuildingModels;1", File.ReadAllText(Path.Combine(directory, "BuildingModels_CourtyardAudit_Summary.txt")), StringComparison.Ordinal);
            Assert.Empty(CourtyardAuditCheckpoint(directory));
        }

        /// <summary>
        /// Reads the county parts the courtyard audit run in the given directory checkpointed.
        /// </summary>
        /// <param name="directory">The report directory of the run.</param>
        /// <returns>The non-blank lines of the checkpoint file.</returns>
        private static string[] CourtyardAuditCheckpoint(string directory)
        {
            return [.. File.ReadAllLines(Path.Combine(directory, "BuildingModels_CourtyardAudit_Checkpoint.txt")).Where(x => !string.IsNullOrWhiteSpace(x))];
        }

        /// <summary>
        /// Answers the scripted GIS Web API of the courtyard audit facts: one reference for the county part, the given source for it, and the given status for its stored model.
        /// </summary>
        /// <param name="pathAndQuery">The path and query of the request.</param>
        /// <param name="httpResponseMessage_Source">The answer of the source read.</param>
        /// <param name="httpStatusCode_BuildingModels">The status the stored-model read answers.</param>
        /// <returns>The response, or null to leave the request to the county script.</returns>
        private static HttpResponseMessage? CourtyardSourcesReadResponse(string pathAndQuery, HttpResponseMessage httpResponseMessage_Source, HttpStatusCode httpStatusCode_BuildingModels)
        {
            if (pathAndQuery.Contains("/referencesbycountyid", StringComparison.OrdinalIgnoreCase))
            {
                return Response(HttpStatusCode.OK, "[\"" + reference_Courtyard_1465 + "\"]");
            }

            if (pathAndQuery.Contains("/itembyreference", StringComparison.OrdinalIgnoreCase))
            {
                return httpResponseMessage_Source;
            }

            if (pathAndQuery.Contains("/itemsbyreferences", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(httpStatusCode_BuildingModels);
            }

            return null;
        }

        /// <summary>
        /// Runs <see cref="UIBuildingModelsCourtyardAuditTask"/> against a scripted GIS Web API holding one county part, without resuming an earlier run.
        /// </summary>
        /// <param name="name">The name of the run, which names its report directory.</param>
        /// <param name="response">Answers a request by its path and query; null leaves it to the county script.</param>
        /// <returns>A task whose result is the result of the run and its report directory.</returns>
        private static async Task<(bool Succeeded, string Directory)> RunUIBuildingModelsCourtyardAuditTaskAsync(string name, Func<string, HttpResponseMessage?> response)
        {
            string directory = ReadsReportDirectory("UIBuildingModelsCourtyardAuditTask_" + name);

            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler = new(x => response(x) ?? CountyReadResponse(x));
            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveManager(solarSolveHttpMessageHandler);
            using (serviceProvider)
            {
                ProbeUIBuildingModelsCourtyardAuditTask probeUIBuildingModelsCourtyardAuditTask = new(gISWebAPIManager) { ReportDirectory = directory, Resume = false };
                return (await probeUIBuildingModelsCourtyardAuditTask.RunAsync(), directory);
            }
        }

        /// <summary>
        /// Exposes the run of <see cref="UIBuildingModelsCourtyardAuditTask"/> to the facts.
        /// </summary>
        private sealed class ProbeUIBuildingModelsCourtyardAuditTask : UIBuildingModelsCourtyardAuditTask
        {
            public ProbeUIBuildingModelsCourtyardAuditTask(GISWebAPIManager gISWebAPIManager)
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
