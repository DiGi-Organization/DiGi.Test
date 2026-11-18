using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the query parameter names of <c>gis/building2d/centroidsbycountyid</c>, the contract a client sends without any compile-time link (<c>Coding - WebAPI Contracts.md</c> §1). Reference: DiGi.GIS.PostgreSQL.UI#48.
        /// </summary>
        [Fact]
        public void Building2DController_CentroidsByCountyId_QueryParameterNames()
        {
            MethodInfo? methodInfo = typeof(Building2DController).GetMethod(nameof(Building2DController.GetCentroidsByCountyIdAsync));
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

            string[] names_Expected = ["countyid", "commandtimeout"];
            Assert.Equal(names_Expected, names);
        }

        /// <summary>
        /// Verifies that <c>gis/building2d/centroidsbycountyid</c> answers 400 for a county id below 1 and a negative timeout, and 500 - never four empty arrays - when the converter has no connection. Reference: DiGi.GIS.PostgreSQL.UI#48.
        /// </summary>
        [Fact]
        public async Task Building2DController_CentroidsByCountyId_InvalidParameter()
        {
            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                Building2DController building2DController = new(gISWebAPIConfigurationFileWatcher, new Building2DPostgreSQLConverter(null), new AdministrativeAreal2DPostgreSQLConverter(null))
                {
                    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
                };

                Assert.IsType<BadRequestResult>(await building2DController.GetCentroidsByCountyIdAsync(0));
                Assert.IsType<BadRequestResult>(await building2DController.GetCentroidsByCountyIdAsync(1, commandTimeout: -1));

                ObjectResult objectResult = Assert.IsType<ObjectResult>(await building2DController.GetCentroidsByCountyIdAsync(1));
                Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies <see cref="Convert.ToDiGi_Building2DCentroids"/> as the inverse of <see cref="Convert.ToSystem_JsonObject(IEnumerable{Building2DCentroid}?)"/> through the JSON text, under the invariant culture and under <c>pl-PL</c> (a decimal comma): references, county ids and the 0.01 m coordinates come back in order, four empty arrays are an empty list, and a missing array, arrays of different lengths, a coordinate that is not a number or a county id that is not an integer give null, never a shorter list. Reference: DiGi.GIS.PostgreSQL.UI#48.
        /// </summary>
        [Fact]
        public void Convert_ToDiGi_Building2DCentroids()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                foreach (string name in new string[] { string.Empty, "pl-PL" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

                    List<Building2DCentroid> building2DCentroids =
                    [
                        new() { Reference = "XUNIT-C-2", CountyId = 7, X = 638000.123, Y = 486000.5 },
                        new() { Reference = "XUNIT-C-1", CountyId = 7, X = -0.004, Y = 12 },
                    ];

                    string json = Convert.ToSystem_JsonObject(building2DCentroids)!.ToJsonString();
                    List<Building2DCentroid>? building2DCentroids_Read = (JsonNode.Parse(json) as JsonObject).ToDiGi_Building2DCentroids();
                    Assert.NotNull(building2DCentroids_Read);
                    Assert.Equal(new string?[] { "XUNIT-C-2", "XUNIT-C-1" }, building2DCentroids_Read.Select(x => x.Reference));
                    Assert.All(building2DCentroids_Read, x => Assert.Equal(7, x.CountyId));
                    Assert.Equal(638000.12, building2DCentroids_Read[0].X);
                    Assert.Equal(486000.5, building2DCentroids_Read[0].Y);
                    Assert.Equal(0, building2DCentroids_Read[1].X);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }

            List<Building2DCentroid>? building2DCentroids_Empty = (JsonNode.Parse("{\"References\":[],\"CountyIds\":[],\"X\":[],\"Y\":[]}") as JsonObject).ToDiGi_Building2DCentroids();
            Assert.NotNull(building2DCentroids_Empty);
            Assert.Empty(building2DCentroids_Empty);

            Assert.Null(Convert.ToDiGi_Building2DCentroids(null));
            Assert.Null((JsonNode.Parse("{\"References\":[\"A\"],\"CountyIds\":[1],\"X\":[1]}") as JsonObject).ToDiGi_Building2DCentroids());
            Assert.Null((JsonNode.Parse("{\"References\":[\"A\",\"B\"],\"CountyIds\":[1],\"X\":[1],\"Y\":[1]}") as JsonObject).ToDiGi_Building2DCentroids());
            Assert.Null((JsonNode.Parse("{\"References\":[\"A\"],\"CountyIds\":[1],\"X\":[\"1\"],\"Y\":[1]}") as JsonObject).ToDiGi_Building2DCentroids());
            Assert.Null((JsonNode.Parse("{\"References\":[\"A\"],\"CountyIds\":[1.5],\"X\":[1],\"Y\":[1]}") as JsonObject).ToDiGi_Building2DCentroids());
        }

        /// <summary>
        /// Verifies <see cref="Query.Building2DCentroidsAsync"/> against a scripted server: the query names the part and the timeout; a columnar answer gives its centroids; a 500, a malformed answer or a county id below 1 gives null, never an empty list. Reference: DiGi.GIS.PostgreSQL.UI#48.
        /// </summary>
        [Fact]
        public async Task Building2DCentroidsAsync_Read()
        {
            ScriptedHttpMessageHandler scriptedHttpMessageHandler = new((path, count) => count switch
            {
                1 => SolarSolveClientResponse(HttpStatusCode.OK, "{\"References\":[\"XUNIT-C-1\"],\"CountyIds\":[11],\"X\":[1.25],\"Y\":[2.5]}"),
                2 => new System.Net.Http.HttpResponseMessage(HttpStatusCode.InternalServerError),
                _ => SolarSolveClientResponse(HttpStatusCode.OK, "{\"References\":[\"XUNIT-C-1\"],\"CountyIds\":[11],\"X\":[],\"Y\":[]}"),
            });

            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SolarSolveClientManager(scriptedHttpMessageHandler);
            using (serviceProvider)
            {
                List<Building2DCentroid>? building2DCentroids = await gISWebAPIManager.Building2DCentroidsAsync(11, commandTimeout: 90);
                Assert.NotNull(building2DCentroids);
                Building2DCentroid building2DCentroid = Assert.Single(building2DCentroids);
                Assert.Equal("XUNIT-C-1", building2DCentroid.Reference);
                Assert.Equal(11, building2DCentroid.CountyId);
                Assert.Equal(1.25, building2DCentroid.X);
                Assert.Equal(2.5, building2DCentroid.Y);

                Assert.Null(await gISWebAPIManager.Building2DCentroidsAsync(11));
                Assert.Null(await gISWebAPIManager.Building2DCentroidsAsync(11));
                Assert.Null(await gISWebAPIManager.Building2DCentroidsAsync(0));
            }

            Assert.Equal(3, scriptedHttpMessageHandler.Requests.Count);
            string pathAndQuery = scriptedHttpMessageHandler.Requests.First().PathAndQuery;
            Assert.Contains("/centroidsbycountyid?", pathAndQuery, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("countyid=11", pathAndQuery, StringComparison.Ordinal);
            Assert.Contains("commandtimeout=90", pathAndQuery, StringComparison.Ordinal);
        }
    }
}
