using DiGi.Analytical.Building.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// A building model converter pointed at a scratch table of the building model shape, so the paging fact never touches the rows a test database already holds.
        /// </summary>
        private sealed class ScratchBuildingModelPostgreSQLConverter : PostgreSQL.Classes.BuildingModelPostgreSQLConverter
        {
            private readonly string tableName;

            /// <summary>
            /// Initializes a new instance of the <see cref="ScratchBuildingModelPostgreSQLConverter"/> class.
            /// </summary>
            /// <param name="connectionData">The connection data of the database the scratch table lives in.</param>
            public ScratchBuildingModelPostgreSQLConverter(ConnectionData? connectionData)
                : base(connectionData, BuildingModelDetailLevel.Component)
            {
                tableName = $"scratch_building_model_{Guid.NewGuid():N}";
            }

            /// <summary>
            /// Gets the name of the scratch table.
            /// </summary>
            public override string TableName => tableName;
        }

        /// <summary>
        /// Pins the query parameter names of <c>gis/buildingmodel/itemsbycountyid</c>, the contract a client sends without any compile-time link (<c>Coding - WebAPI Contracts.md</c> §1). Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public void BuildingModelController_ItemsByCountyId_QueryParameterNames()
        {
            MethodInfo? methodInfo = typeof(BuildingModelController).GetMethod(nameof(BuildingModelController.GetItemsByCountyIdAsync));
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

            string[] names_Expected = ["countyid", "idafter", "limit", "commandtimeout"];
            Assert.Equal(names_Expected, names);
        }

        /// <summary>
        /// Verifies that <c>gis/buildingmodel/itemsbycountyid</c> answers 400 for a county id below 1, a negative <c>idafter</c>, a limit of 0 or above the cap, and a negative timeout, and 500 - never an empty page - when the converter has no connection. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task BuildingModelController_ItemsByCountyId_InvalidParameter()
        {
            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingModelController buildingModelController = ItemsByCountyIdController(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingModelPostgreSQLConverter(null, BuildingModelDetailLevel.Component));

                Assert.IsType<BadRequestResult>(await buildingModelController.GetItemsByCountyIdAsync(0));
                Assert.IsType<BadRequestResult>(await buildingModelController.GetItemsByCountyIdAsync(1, idAfter: -1));
                Assert.IsType<BadRequestResult>(await buildingModelController.GetItemsByCountyIdAsync(1, limit: 0));
                Assert.IsType<BadRequestResult>(await buildingModelController.GetItemsByCountyIdAsync(1, limit: Constants.BuildingModel.MaximumItemsByCountyIdLimit + 1));
                Assert.IsType<BadRequestResult>(await buildingModelController.GetItemsByCountyIdAsync(1, commandTimeout: -1));

                ObjectResult objectResult = Assert.IsType<ObjectResult>(await buildingModelController.GetItemsByCountyIdAsync(1, limit: Constants.BuildingModel.MaximumItemsByCountyIdLimit));
                Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies the keyset paging of <c>gis/buildingmodel/itemsbycountyid</c> on the throwaway storage test database.
        /// <para>Five building models are seeded into a scratch table under one county part. Pages of two read them in row id order, each model once; a full page answers <c>DiGi-Next-Cursor</c> with its last row id, the last page answers none, and a page past the last row is an empty list. An unknown county part is an empty list and a table that does not exist answers 404.</para>
        /// <para>Medium test (a live database server): the scratch table is created by the application's own upsert and dropped before and after. Reference: DiGi.GIS.WebAPI#93.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingModelController_ItemsByCountyId_Paging()
        {
            const int countyId = 990393;

            (_, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            ScratchBuildingModelPostgreSQLConverter scratchBuildingModelPostgreSQLConverter = new(connectionData_Storage);
            ScratchBuildingModelPostgreSQLConverter scratchBuildingModelPostgreSQLConverter_Missing = new(connectionData_Storage);

            string path = ConfigurationFilePath();
            try
            {
                List<PostgreSQL.Classes.BuildingModel> buildingModels_PostgreSQL = [];
                for (int i = 0; i < 5; i++)
                {
                    buildingModels_PostgreSQL.Add(new PostgreSQL.Classes.BuildingModel
                    {
                        CountyId = countyId,
                        UniqueId = $"xunit-93-{i}",
                        Reference = $"XUNIT-93-{i}",
                        Object = BuildingModel().ToJsonObject(),
                    });
                }

                PostgreSQL.Classes.PostgreSQLUpdateResult? postgreSQLUpdateResult = await scratchBuildingModelPostgreSQLConverter.UpdateAsync(buildingModels_PostgreSQL);
                Assert.NotNull(postgreSQLUpdateResult);
                Assert.Equal(5, postgreSQLUpdateResult.Ids.Count);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);

                List<string> cursors = [];
                int count = 0;
                long idAfter = 0;
                while (true)
                {
                    BuildingModelController buildingModelController = ItemsByCountyIdController(gISWebAPIConfigurationFileWatcher, scratchBuildingModelPostgreSQLConverter);
                    ContentResult contentResult = Assert.IsType<ContentResult>(await buildingModelController.GetItemsByCountyIdAsync(countyId, idAfter, 2));

                    List<DiGi.Analytical.Building.Classes.BuildingModel>? buildingModels = Core.Convert.ToDiGi<DiGi.Analytical.Building.Classes.BuildingModel>(contentResult.Content);
                    Assert.NotNull(buildingModels);
                    count += buildingModels.Count;

                    string cursor = buildingModelController.HttpContext.Response.Headers[Constants.Header.NextCursor].ToString();
                    if (string.IsNullOrEmpty(cursor))
                    {
                        Assert.Single(buildingModels);
                        break;
                    }

                    Assert.Equal(2, buildingModels.Count);
                    cursors.Add(cursor);
                    idAfter = long.Parse(cursor, CultureInfo.InvariantCulture);
                }

                Assert.Equal(5, count);
                string[] cursors_Expected = [buildingModels_PostgreSQL[1].Id.ToString(CultureInfo.InvariantCulture), buildingModels_PostgreSQL[3].Id.ToString(CultureInfo.InvariantCulture)];
                Assert.Equal(cursors_Expected, cursors);

                ContentResult contentResult_Past = Assert.IsType<ContentResult>(await ItemsByCountyIdController(gISWebAPIConfigurationFileWatcher, scratchBuildingModelPostgreSQLConverter).GetItemsByCountyIdAsync(countyId, buildingModels_PostgreSQL[4].Id));
                Assert.Equal("[]", contentResult_Past.Content);

                ContentResult contentResult_Unknown = Assert.IsType<ContentResult>(await ItemsByCountyIdController(gISWebAPIConfigurationFileWatcher, scratchBuildingModelPostgreSQLConverter).GetItemsByCountyIdAsync(990394));
                Assert.Equal("[]", contentResult_Unknown.Content);

                Assert.IsType<NotFoundResult>(await ItemsByCountyIdController(gISWebAPIConfigurationFileWatcher, scratchBuildingModelPostgreSQLConverter_Missing).GetItemsByCountyIdAsync(countyId));
            }
            finally
            {
                System.IO.File.Delete(path);

                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();
                await ExecuteSolarRadiationAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {scratchBuildingModelPostgreSQLConverter.TableName};");
            }
        }

        /// <summary>
        /// Builds a building model controller over the given building model converter, with no main database and a fresh request.
        /// </summary>
        /// <param name="gISWebAPIConfigurationFileWatcher">The configuration watcher.</param>
        /// <param name="buildingModelPostgreSQLConverter">The building model converter.</param>
        /// <returns>The controller.</returns>
        private static BuildingModelController ItemsByCountyIdController(GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher, PostgreSQL.Classes.BuildingModelPostgreSQLConverter buildingModelPostgreSQLConverter)
        {
            BuildingModelController result = new(gISWebAPIConfigurationFileWatcher, buildingModelPostgreSQLConverter, new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
            result.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return result;
        }
    }
}
