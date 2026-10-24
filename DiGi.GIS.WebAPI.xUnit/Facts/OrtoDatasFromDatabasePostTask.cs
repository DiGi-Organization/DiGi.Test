using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="OrtoDatasFromDatabasePostTask"/> initializes with expected default property values and implements expected task interfaces.
        /// </summary>
        [Fact]
        public void OrtoDatasFromDatabasePostTask_Defaults()
        {
            GISWebAPIManager gISWebAPIManager = new(null);
            OrtoDatasFromDatabasePostTask task = new(gISWebAPIManager);

            Assert.NotNull(task);
            Assert.Equal(5, task.Count);
            Assert.NotNull(task.OrtoDatasBuilding2DOptions);
            Assert.IsAssignableFrom<OrtoDatasPostTask>(task);
            Assert.IsAssignableFrom<Interfaces.IGISWebAPIObject>(task);
        }

        /// <summary>
        /// Verifies that a 404 from the Building2D fetch is treated as an empty batch: the run moves on to the next claim and completes instead of stopping.
        /// <para>A queue entry can outlive its building_2d row, and the Building2D endpoint then answers 404 for the batch. The task used to rethrow that and die on the first such batch, so "Upload OrtoDatas from database" failed after 0 s. With <see cref="OrtoDatasFromDatabasePostTask.MaxConsecutiveFailureCount"/> at 1, counting the 404 as a failure would also stop the run, so the fact covers that too.</para>
        /// </summary>
        [Fact]
        public async Task OrtoDatasFromDatabasePostTask_Building2DNotFound()
        {
            Building2DReference building2DReference = new() { Id = 1, CountyId = 5, Reference = "PL.PZGIK.BDOT10k.BUBDA.0000.0000000" };
            List<Building2DReference> building2DReferences = [building2DReference];
            string json_Claimed = Core.Convert.ToSystem_String(building2DReferences) ?? throw new InvalidOperationException("Building2DReferences could not be serialized");

            ScriptedHttpMessageHandler scriptedHttpMessageHandler = new((path, count) =>
            {
                if (path.EndsWith("/nextbuilding2dreferences", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(count == 1 ? json_Claimed : "[]", Encoding.UTF8, "application/json") };
                }

                if (path.EndsWith("/itemsbybuilding2dreferences", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"title\":\"Not Found\",\"status\":404}", Encoding.UTF8, "application/problem+json") };
                }

                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

            ServiceCollection serviceCollection = new();
            serviceCollection.AddHttpClient(Constants.Name.Client.GIS, httpClient => httpClient.BaseAddress = new Uri("https://localhost/")).ConfigurePrimaryHttpMessageHandler(() => scriptedHttpMessageHandler);
            serviceCollection.AddHttpClient(Constants.Name.Client.Geoportal).ConfigurePrimaryHttpMessageHandler(() => scriptedHttpMessageHandler);
            using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            GISWebAPIManager gISWebAPIManager = new(serviceProvider.GetRequiredService<IHttpClientFactory>());
            OrtoDatasFromDatabasePostTaskProbe ortoDatasFromDatabasePostTaskProbe = new(gISWebAPIManager) { MaxConsecutiveFailureCount = 1 };

            bool succeeded = await ortoDatasFromDatabasePostTaskProbe.RunAsync();

            Assert.True(succeeded);
            Assert.Equal(2, scriptedHttpMessageHandler.Count("/nextbuilding2dreferences"));
            Assert.Equal(1, scriptedHttpMessageHandler.Count("/itemsbybuilding2dreferences"));
            Assert.Equal(0, scriptedHttpMessageHandler.Count("/acknowledgebuilding2dreferences"));
        }
    }
}
