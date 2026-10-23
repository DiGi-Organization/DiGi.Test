using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reproduces DiGi-Organization/DiGi.GIS.WebAPI#71: an action with no transient catch must still answer 503 with Retry-After when the database is unreachable, not the host's 500.
        /// <para>Runs the real MVC pipeline in-process (<c>UseTestServer</c>) with the host's <c>UseExceptionHandler</c>, against converters pointed at a dead loopback port: opening a connection throws a genuine <see cref="Npgsql.NpgsqlException"/> that Npgsql classifies <c>IsTransient = true</c>. <c>ortodatas/nextbuilding2dreferences</c> is the claim endpoint whose 500 failed the tray application's upload task during the 22–23 October 2026 outage; <c>administrativeareal2d/count</c> stands for the other actions without a catch.</para>
        /// <para>Red before the fix (both answered 500); green after.</para>
        /// </summary>
        [MediumFact]
        public async Task TransientDatabaseFailure_NoCatch_Answers503()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                await using WebApplication webApplication = await TransientFailureHostAsync(gISWebAPIConfigurationFileWatcher);
                using HttpClient httpClient = webApplication.GetTestClient();

                await AssertTransient503Async(await httpClient.PostAsync("gis/ortodatas/nextbuilding2dreferences?count=5", new StringContent(string.Empty, Encoding.UTF8, "application/json")));
                await AssertTransient503Async(await httpClient.GetAsync("gis/administrativeareal2d/count"));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Reproduces DiGi-Organization/DiGi.GIS.WebAPI#71 for an action whose generic <c>catch (Exception)</c> turned every failure into its own 500: a transient database failure must answer 503 with Retry-After.
        /// <para>Same in-process pipeline and dead database host as <see cref="TransientDatabaseFailure_NoCatch_Answers503"/>.</para>
        /// <para>Red before the fix (500, "Internal server error during database update"); green after.</para>
        /// </summary>
        [MediumFact]
        public async Task TransientDatabaseFailure_GenericCatch_Answers503()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                await using WebApplication webApplication = await TransientFailureHostAsync(gISWebAPIConfigurationFileWatcher);
                using HttpClient httpClient = webApplication.GetTestClient();

                await AssertTransient503Async(await httpClient.PostAsync("gis/ortodatas/acknowledgebuilding2dreferences", new StringContent("[1,2]", Encoding.UTF8, "application/json")));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Builds the in-process host for the <c>TransientDatabaseFailure_</c> facts: the GIS controllers, the host's exception handler and problem details, and every converter they need pointed at a closed loopback port.
        /// </summary>
        private static async Task<WebApplication> TransientFailureHostAsync(GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher)
        {
            // Port 1 on loopback refuses the connection at once, so each request fails fast with a transient NpgsqlException.
            ConnectionData connectionData_Dead = new("127.0.0.1", "user", "pass", "db", 1);

            WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder();
            webApplicationBuilder.WebHost.UseTestServer();
            webApplicationBuilder.Services.AddSingleton(gISWebAPIConfigurationFileWatcher);
            webApplicationBuilder.Services.AddSingleton(new OrtoDatasPostgreSQLConverter(connectionData_Dead));
            webApplicationBuilder.Services.AddSingleton(new Building2DPostgreSQLConverter(connectionData_Dead));
            webApplicationBuilder.Services.AddSingleton(new AdministrativeAreal2DPostgreSQLConverter(connectionData_Dead));
            webApplicationBuilder.Services.AddProblemDetails();
            webApplicationBuilder.Services.AddControllers().AddApplicationPart(typeof(OrtoDatasController).Assembly);

            WebApplication webApplication = webApplicationBuilder.Build();

            // As in DiGi.WebAPI.WindowsService: an exception that escapes an action becomes the host's 500.
            webApplication.UseExceptionHandler();
            webApplication.MapControllers();

            await webApplication.StartAsync();
            return webApplication;
        }

        /// <summary>
        /// Asserts the transient-failure contract over HTTP: 503 Service Unavailable with the 30-second Retry-After header.
        /// </summary>
        private static async Task AssertTransient503Async(HttpResponseMessage httpResponseMessage)
        {
            string body = await httpResponseMessage.Content.ReadAsStringAsync();
            Assert.True(httpResponseMessage.StatusCode == HttpStatusCode.ServiceUnavailable, $"{httpResponseMessage.RequestMessage?.RequestUri}: {(int)httpResponseMessage.StatusCode} {body}");
            Assert.Equal("30", httpResponseMessage.Headers.RetryAfter?.ToString());
        }
    }
}
