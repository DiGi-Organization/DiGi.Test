using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the query parameter names of <c>gis/buildingsolarradiation/summariesbycountyids</c>: <c>countyids</c>, <c>solverversion</c> and <c>commandtimeout</c>. A renamed <c>[FromQuery]</c> is silently ignored and leaves the parameter at its default, so the production measure of #137 would summarise every county without saying so (<c>Coding - WebAPI Contracts.md</c> §1).
        /// <para>Reference: DiGi.GIS.WebAPI#82.</para>
        /// </summary>
        [Fact]
        public void BuildingSolarRadiationController_Summaries_QueryParameterNames()
        {
            MethodInfo? methodInfo = typeof(BuildingSolarRadiationController).GetMethod(nameof(BuildingSolarRadiationController.GetSummariesByCountyIdsAsync));
            Assert.NotNull(methodInfo);

            List<string> names = [];
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
            {
                if (parameterInfo.GetCustomAttribute<FromQueryAttribute>() is FromQueryAttribute fromQueryAttribute)
                {
                    Assert.NotNull(fromQueryAttribute.Name);
                    names.Add(fromQueryAttribute.Name);
                }
            }

            Assert.Equal(["countyids", "solverversion", "commandtimeout"], names);
        }

        /// <summary>
        /// Verifies the failure answers of the summary read without a database: a negative <c>commandtimeout</c> answers 400, and a converter that cannot reach its database answers 500 - never an empty list, which would read as "nothing stored" (<c>Coding - WebAPI Contracts.md</c>).
        /// <para>Reference: DiGi.GIS.WebAPI#82.</para>
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationController_Summaries_Failures()
        {
            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, null, null, null);

            Assert.IsType<BadRequestResult>(await controller.GetSummariesByCountyIdsAsync([1], null, -1));

            ObjectResult objectResult = Assert.IsType<ObjectResult>(await controller.GetSummariesByCountyIdsAsync([1], null, 30));
            Assert.Equal(500, objectResult.StatusCode);
        }

        /// <summary>
        /// Verifies the summary read end to end on the throwaway Storage test database: one stored result under the scratch part 990311 at radius 50 answers 200 with one entry for that part, radius and solver version, one row and a partition size above zero; a part holding nothing answers 200 with <c>[]</c>.
        /// <para>Medium test (two live database servers): it drops the scratch partitions before and after. The residential counts depend on <c>building_data</c>, which this fact does not seed, so they are not asserted here (DiGi.GIS.PostgreSQL#138 covers them). Reference: DiGi.GIS.WebAPI#82.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationController_Summaries_RoundTrip()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            using GISWebAPIConfigurationFileWatcher watcher = new(SolarRadiationConfigurationFilePath(true));
            BuildingSolarRadiationController controller = SolarRadiationController(watcher, connectionData_Main, connectionData_Storage, null);

            await CleanupSolarRadiationScratchAsync(connectionData_Main, connectionData_Storage);
            try
            {
                BuildingSolarRadiationPostgreSQLConverter buildingSolarRadiationPostgreSQLConverter = new(connectionData_Storage);
                long? id = await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(countyId_SolarRadiation, ScratchSolarRadiationResult(countyId_SolarRadiation, "XUNIT-SR-API", null));
                Assert.NotNull(id);

                ContentResult contentResult = Assert.IsType<ContentResult>(await controller.GetSummariesByCountyIdsAsync([countyId_SolarRadiation], 1, 30));
                Assert.Equal("application/json", contentResult.ContentType);

                List<BuildingSolarRadiationCountyResult>? buildingSolarRadiationCountyResults = Core.Convert.ToDiGi<BuildingSolarRadiationCountyResult>(contentResult.Content);
                Assert.NotNull(buildingSolarRadiationCountyResults);
                BuildingSolarRadiationCountyResult buildingSolarRadiationCountyResult = Assert.Single(buildingSolarRadiationCountyResults);
                Assert.Equal(countyId_SolarRadiation, buildingSolarRadiationCountyResult.CountyId);
                Assert.Equal(50, buildingSolarRadiationCountyResult.Radius);
                Assert.Equal(1, buildingSolarRadiationCountyResult.SolverVersion);
                Assert.Equal(1, buildingSolarRadiationCountyResult.RowCount);
                Assert.True(buildingSolarRadiationCountyResult.PartitionSize > 0, $"partition size {buildingSolarRadiationCountyResult.PartitionSize}");
                Assert.NotNull(buildingSolarRadiationCountyResult.CreatedAtMin);

                ContentResult contentResult_Empty = Assert.IsType<ContentResult>(await controller.GetSummariesByCountyIdsAsync([countyId_SolarRadiation + 1], 1, 30));
                Assert.Equal("[]", contentResult_Empty.Content);
            }
            finally
            {
                await CleanupSolarRadiationScratchAsync(connectionData_Main, connectionData_Storage);
            }
        }
    }
}
