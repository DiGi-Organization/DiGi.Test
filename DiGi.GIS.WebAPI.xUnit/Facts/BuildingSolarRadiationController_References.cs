using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the query parameter names of <c>gis/buildingsolarradiation/referencesbycountyid</c> (<c>Coding - WebAPI Contracts.md</c> §1), and that the radius and the solver version bind as nullable, so an omitted one is refused instead of meaning 0 (§2). Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public void BuildingSolarRadiationController_References_QueryParameterNames()
        {
            MethodInfo? methodInfo = typeof(BuildingSolarRadiationController).GetMethod(nameof(BuildingSolarRadiationController.GetReferencesByCountyIdAsync));
            Assert.NotNull(methodInfo);

            List<string> names = [];
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
            {
                if (parameterInfo.GetCustomAttribute<FromQueryAttribute>() is not FromQueryAttribute fromQueryAttribute)
                {
                    continue;
                }

                Assert.NotNull(fromQueryAttribute.Name);
                names.Add(fromQueryAttribute.Name);
                if (fromQueryAttribute.Name is "radius" or "solverversion")
                {
                    Assert.True(Nullable.GetUnderlyingType(parameterInfo.ParameterType) is not null, $"Query parameter '{fromQueryAttribute.Name}' is not nullable.");
                }
            }

            string[] names_Expected = ["countyid", "radius", "solverversion", "commandtimeout"];
            Assert.Equal(names_Expected, names);
        }

        /// <summary>
        /// Verifies that <c>gis/buildingsolarradiation/referencesbycountyid</c> answers 400 for a county id below 1, a missing, negative or non-finite radius, a missing or non-positive solver version and a negative timeout, accepts radius 0 (the isolated building), and answers 500 - never an empty list - when the converter has no connection. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_References_InvalidParameter()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, null);

            Assert.IsType<BadRequestResult>(await controller.GetReferencesByCountyIdAsync(0, 50, 1));
            Assert.IsType<BadRequestResult>(await controller.GetReferencesByCountyIdAsync(countyId_SolarRadiation, null, 1));
            Assert.IsType<BadRequestResult>(await controller.GetReferencesByCountyIdAsync(countyId_SolarRadiation, -0.1, 1));
            Assert.IsType<BadRequestResult>(await controller.GetReferencesByCountyIdAsync(countyId_SolarRadiation, double.NaN, 1));
            Assert.IsType<BadRequestResult>(await controller.GetReferencesByCountyIdAsync(countyId_SolarRadiation, 50, null));
            Assert.IsType<BadRequestResult>(await controller.GetReferencesByCountyIdAsync(countyId_SolarRadiation, 50, 0));
            Assert.IsType<BadRequestResult>(await controller.GetReferencesByCountyIdAsync(countyId_SolarRadiation, 50, 1, -1));

            Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<ObjectResult>(await controller.GetReferencesByCountyIdAsync(countyId_SolarRadiation, 0, 1)).StatusCode);
        }

        /// <summary>
        /// Verifies on the throwaway storage test database that <c>gis/buildingsolarradiation/referencesbycountyid</c> lists exactly the references stored for the asked radius and solver version, in ordinal order: two at 50 m, the isolated building alone at 0 m, another solver version apart, and an empty list for a part holding nothing.
        /// <para>Medium test (a live database server): it writes the scratch partition of 990311 and drops it before and after. Reference: DiGi.GIS.WebAPI#93.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationController_References_ByVariant()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));

            await CleanupSolarRadiationScratchAsync(connectionData_Main, connectionData_Storage);
            try
            {
                List<GIS.Classes.BuildingSolarRadiationResult?> buildingSolarRadiationResults =
                [
                    ScratchVariantSolarRadiationResult("XUNIT-SR-R2", 990311202L, 50, 1),
                    ScratchVariantSolarRadiationResult("XUNIT-SR-R1", 990311201L, 50, 1),
                    ScratchVariantSolarRadiationResult("XUNIT-SR-R3", 990311203L, 0, 1),
                    ScratchVariantSolarRadiationResult("XUNIT-SR-R4", 990311204L, 50, 2),
                ];

                PostgreSQL.Classes.BuildingSolarRadiationPostgreSQLConverter buildingSolarRadiationPostgreSQLConverter = new(connectionData_Storage);
                Assert.Equal(4, await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(countyId_SolarRadiation, buildingSolarRadiationResults));

                string[] references_50 = ["XUNIT-SR-R1", "XUNIT-SR-R2"];
                string[] references_Isolated = ["XUNIT-SR-R3"];
                string[] references_Version2 = ["XUNIT-SR-R4"];
                Assert.Equal(references_50, await SolarRadiationReferencesAsync(watcher, connectionData_Main, connectionData_Storage, countyId_SolarRadiation, 50, 1));
                Assert.Equal(references_Isolated, await SolarRadiationReferencesAsync(watcher, connectionData_Main, connectionData_Storage, countyId_SolarRadiation, 0, 1));
                Assert.Equal(references_Version2, await SolarRadiationReferencesAsync(watcher, connectionData_Main, connectionData_Storage, countyId_SolarRadiation, 50, 2));
                Assert.Empty(await SolarRadiationReferencesAsync(watcher, connectionData_Main, connectionData_Storage, countyId_SolarRadiation, 50.1, 1));
                Assert.Empty(await SolarRadiationReferencesAsync(watcher, connectionData_Main, connectionData_Storage, countyId_SolarRadiation_Batch, 50, 1));
            }
            finally
            {
                await CleanupSolarRadiationScratchAsync(connectionData_Main, connectionData_Storage);
            }
        }

        /// <summary>
        /// Reads the stored references of one variant of a county part through a fresh controller and returns them as a list.
        /// </summary>
        /// <param name="watcher">The configuration watcher.</param>
        /// <param name="connectionData_Main">The main test database.</param>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <param name="countyId">The county part.</param>
        /// <param name="radius">The neighbour radius, in m.</param>
        /// <param name="solverVersion">The solver version.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the references in the order the endpoint answered them.</returns>
        private static async Task<string[]> SolarRadiationReferencesAsync(GISWebAPIConfigurationFileWatcher watcher, ConnectionData connectionData_Main, ConnectionData connectionData_Storage, int countyId, double radius, int solverVersion)
        {
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, connectionData_Main, connectionData_Storage, null);
            ContentResult contentResult = Assert.IsType<ContentResult>(await controller.GetReferencesByCountyIdAsync(countyId, radius, solverVersion));
            Assert.Equal("application/json", contentResult.ContentType);

            JsonArray jsonArray = Assert.IsType<JsonArray>(JsonNode.Parse(contentResult.Content!));

            List<string> result = [];
            foreach (JsonNode? jsonNode in jsonArray)
            {
                result.Add(jsonNode!.GetValue<string>());
            }

            return [.. result];
        }

        /// <summary>
        /// Builds a scratch result filed under <see cref="countyId_SolarRadiation"/> for one radius and solver version.
        /// </summary>
        /// <param name="reference">The building reference.</param>
        /// <param name="buildingModelId">The id of the building model.</param>
        /// <param name="radius">The neighbour radius, in m.</param>
        /// <param name="solverVersion">The solver version.</param>
        /// <returns>The result.</returns>
        private static GIS.Classes.BuildingSolarRadiationResult ScratchVariantSolarRadiationResult(string reference, long buildingModelId, double radius, int solverVersion)
        {
            return new GIS.Classes.BuildingSolarRadiationResult(reference, countyId_SolarRadiation, buildingModelId, radius, solverVersion, GIS.Enums.WeatherSourceType.EPW, "17", null, null, null, new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero), [ScratchSurfaceSolarRadiation(Constants.SolarRadiation.HourCount)]);
        }
    }
}
