using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="UIBuildingModelsVerificationTask"/> tells an empty county part from a failed read of its references, against a scripted GIS Web API (DiGi.GIS.PostgreSQL.UI#51).
        /// <para>A 404 is a part with no buildings (DiGi.GIS.WebAPI#97): the run succeeds and the summary carries the part's zero row. A 500 is a failed read: the run fails, the part has no zero row and is listed under the failed reads instead.</para>
        /// </summary>
        [Fact]
        public async Task UIBuildingModelsVerificationTask_References()
        {
            (bool succeeded, string directory) = await RunUIBuildingModelsVerificationTaskAsync("References_404", x => x.Contains("/referencesbycountyid", StringComparison.OrdinalIgnoreCase) ? new HttpResponseMessage(HttpStatusCode.NotFound) : null);
            Assert.True(succeeded, "A part with no buildings is a valid outcome.");
            string summary = File.ReadAllText(Path.Combine(directory, "BuildingModels_Verification_Summary.txt"));
            Assert.Contains("1465;5;0;0;0;0;0;0;0;0", summary, StringComparison.Ordinal);
            Assert.DoesNotContain("=== FAILED ===", summary, StringComparison.Ordinal);

            (succeeded, directory) = await RunUIBuildingModelsVerificationTaskAsync("References_500", x => x.Contains("/referencesbycountyid", StringComparison.OrdinalIgnoreCase) ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : null);
            Assert.False(succeeded, "A failed references read must fail the run.");
            summary = File.ReadAllText(Path.Combine(directory, "BuildingModels_Verification_Summary.txt"));
            Assert.DoesNotContain("1465;5;0;0;0;0;0;0;0;0", summary, StringComparison.Ordinal);
            Assert.Contains("1465;5;References;", summary, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies that <see cref="UIBuildingModelsVerificationTask"/> tells a batch with no stored model from a failed read of the batch, against a scripted GIS Web API (DiGi.GIS.PostgreSQL.UI#51).
        /// <para>A 404 records every reference of the batch as missing and the run succeeds. A 500 records none of them - a failed read is not a finding - and the run fails with the batch listed under the failed reads.</para>
        /// </summary>
        [Fact]
        public async Task UIBuildingModelsVerificationTask_BuildingModels()
        {
            (bool succeeded, string directory) = await RunUIBuildingModelsVerificationTaskAsync("BuildingModels_404", x => BuildingModelsReadResponse(x, HttpStatusCode.NotFound));
            Assert.True(succeeded, "A batch with no stored model is a valid outcome.");
            Assert.Equal(2, File.ReadAllLines(Path.Combine(directory, "BuildingModels_Verification.csv")).Count(x => x.EndsWith(";Missing;;;;;;;;;", StringComparison.Ordinal)));

            (succeeded, directory) = await RunUIBuildingModelsVerificationTaskAsync("BuildingModels_500", x => BuildingModelsReadResponse(x, HttpStatusCode.InternalServerError));
            Assert.False(succeeded, "A failed batch read must fail the run.");
            Assert.DoesNotContain(File.ReadAllLines(Path.Combine(directory, "BuildingModels_Verification.csv")), x => x.Contains(";Missing;", StringComparison.Ordinal));
            Assert.Contains("1465;5;BuildingModels;2", File.ReadAllText(Path.Combine(directory, "BuildingModels_Verification_Summary.txt")), StringComparison.Ordinal);
        }

        /// <summary>
        /// Answers the scripted GIS Web API of the building model read facts: two references for the county part, and the given status for the stored models.
        /// </summary>
        /// <param name="pathAndQuery">The path and query of the request.</param>
        /// <param name="httpStatusCode">The status the stored-model read answers.</param>
        /// <returns>The response, or null to leave the request to the default script.</returns>
        private static HttpResponseMessage? BuildingModelsReadResponse(string pathAndQuery, HttpStatusCode httpStatusCode)
        {
            if (pathAndQuery.Contains("/referencesbycountyid", StringComparison.OrdinalIgnoreCase))
            {
                return Response(HttpStatusCode.OK, "[\"PL.REF.A\",\"PL.REF.B\"]");
            }

            if (pathAndQuery.Contains("/itemsbyreferences", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(httpStatusCode);
            }

            return null;
        }

        /// <summary>
        /// Answers the county list every scripted run of the read facts starts from: one county part, code 1465 and id 5.
        /// </summary>
        /// <param name="pathAndQuery">The path and query of the request.</param>
        /// <returns>The county list for the county read, otherwise a 500.</returns>
        private static HttpResponseMessage CountyReadResponse(string pathAndQuery)
        {
            if (pathAndQuery.Contains("/administrativeareal2Dreferencesbyadministrativearealtype", StringComparison.OrdinalIgnoreCase))
            {
                string json = Core.Convert.ToSystem_String(new List<AdministrativeAreal2DReference>() { new() { Id = 5, Code = "1465", AdministrativeArealType = AdministrativeArealType.County } }) ?? throw new InvalidOperationException("The counties could not be serialized");
                return Response(HttpStatusCode.OK, json);
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Creates an empty report directory of a scripted run under the test reports directory.
        /// </summary>
        /// <param name="name">The name of the run.</param>
        /// <returns>The full path of the directory.</returns>
        private static string ReadsReportDirectory(string name)
        {
            string? reportsDirectory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(reportsDirectory));

            string directory = Path.Combine(reportsDirectory!, name);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>
        /// Runs <see cref="UIBuildingModelsVerificationTask"/> against a scripted GIS Web API holding one county part.
        /// </summary>
        /// <param name="name">The name of the run, which names its report directory.</param>
        /// <param name="response">Answers a request by its path and query; null leaves it to the county script.</param>
        /// <returns>A task whose result is the result of the run and its report directory.</returns>
        private static async Task<(bool Succeeded, string Directory)> RunUIBuildingModelsVerificationTaskAsync(string name, Func<string, HttpResponseMessage?> response)
        {
            string directory = ReadsReportDirectory("UIBuildingModelsVerificationTask_" + name);

            using SolarSolveHttpMessageHandler solarSolveHttpMessageHandler = new(x => response(x) ?? CountyReadResponse(x));
            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveManager(solarSolveHttpMessageHandler);
            using (serviceProvider)
            {
                ProbeUIBuildingModelsVerificationTask probeUIBuildingModelsVerificationTask = new(gISWebAPIManager) { ReportDirectory = directory };
                return (await probeUIBuildingModelsVerificationTask.RunAsync(), directory);
            }
        }

        /// <summary>
        /// Exposes the run of <see cref="UIBuildingModelsVerificationTask"/> to the facts.
        /// </summary>
        private sealed class ProbeUIBuildingModelsVerificationTask : UIBuildingModelsVerificationTask
        {
            public ProbeUIBuildingModelsVerificationTask(GISWebAPIManager gISWebAPIManager)
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
