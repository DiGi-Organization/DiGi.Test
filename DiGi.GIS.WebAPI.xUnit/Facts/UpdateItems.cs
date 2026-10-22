using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Core.IO.Table.Classes;
using DiGi.Core.Parameter.Classes;
using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Asserts that a write endpoint answers 500 rather than Ok when the database stored nothing.
        /// <para>The converters are built on null connection data, so <c>DiGi.PostgreSQL.Create.NpgsqlConnection</c> hands back null and every <c>UpdateAsync</c> returns null without touching a server. That is exactly the incident this guards: the storage database was unreachable, a county regeneration posted 33 687 models into nothing, every batch came back with no identifiers, and the task reported success because the controller answered Ok on an empty result.</para>
        /// <para>Every one of these tables is written with <c>INSERT … ON CONFLICT … DO UPDATE … RETURNING id</c>, which yields a row for both branches - so past the earlier <see cref="NoContentResult"/> guards, an empty result cannot mean "correctly matched nothing".</para>
        /// <para>The county-part lookup is guarded separately (<see cref="AssertLookupNotRun"/>): when <c>building_2d</c> could not be read at all, the answer is a distinct 500 naming the lookup, not the conflated "no reference resolved" text and not <c>BuildingModelController</c>'s pre-#29 204.</para>
        /// </summary>
        [Fact]
        public async Task UpdateItems_NothingStoredAnswersInternalServerError()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher GISWebAPIConfigurationFileWatcher = new(path);

                Building2DController building2DController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                AssertInternalServerError(await building2DController.UpdateItemsAsync(JsonArray(Building2D()), null));
                AssertInternalServerError(await building2DController.UpdateItemsByCountyIdsAsync(JsonArray(Building2D()), [1]));
                AssertInternalServerError(await building2DController.UpdateItemAsync(Building2D().ToJsonObject(), null));

                EPWFileController ePWFileController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.EPWFilePostgreSQLConverter(null));
                AssertInternalServerError(await ePWFileController.UpdateItemsAsync(JsonArray(new EPW.Classes.EPWFile((EPW.Classes.Location?)null))));

                // First of the new legs: this is the endpoint behind the 33 687-model incident, and it
                // answered 204 here until DiGi.GIS.WebAPI#29.
                BuildingModelController buildingModelController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingModelPostgreSQLConverter(null, BuildingModelDetailLevel.Component), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                AssertInternalServerError(await buildingModelController.UpdateItemsByCountyIdsAsync(JsonArray(BuildingModel()), [1]));
                AssertLookupNotRun(await buildingModelController.UpdateItemsByCountyIdsAsync(JsonArray(BuildingModel()), [1]));

                OccupancyDataController occupancyDataController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.Building2DOccupancyDataPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DOccupancyDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                AssertInternalServerError(await occupancyDataController.Building2DUpdateItemsByCountyIdsAsync(JsonArray(OccupancyData()), [1]));
                AssertLookupNotRun(await occupancyDataController.Building2DUpdateItemsByCountyIdsAsync(JsonArray(OccupancyData()), [1]));
                AssertInternalServerError(await occupancyDataController.AdministrativeAreal2DUpdateItemsAsync(JsonArray(OccupancyData())));

                OrtoDatasController ortoDatasController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.OrtoDatasPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                AssertInternalServerError(await ortoDatasController.UpdateItemsByCountyIdsAsync(JsonArray(new GIS.Classes.OrtoDatas("reference", null)), [1]));
                // Two candidate parts, so the building_2d resolver runs and its outcome is answerable; with a
                // single part the write decides by geometry and the resolver is never reached.
                AssertLookupNotRun(await ortoDatasController.UpdateItemsByCountyIdsAsync(JsonArray(new GIS.Classes.OrtoDatas("reference", null)), [1, 2]));

                BuildingController buildingController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                AssertInternalServerError(await buildingController.UpdateItemsByCountyIdsAsync(JsonArray(Building()), [1]));
                // Several candidate parts, so the resolver runs before the geometry fallback; a single part
                // never reaches it, and an empty map must not send the batch to the fallback instead.
                AssertLookupNotRun(await buildingController.UpdateItemsByCountyIdsAsync(JsonArray(Building()), [1, 2]));

                YearBuiltDataController yearBuiltDataController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.YearBuiltDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null), new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null));
                AssertInternalServerError(await yearBuiltDataController.UpdateItemsByCountyIdsAsync(JsonArray(new GIS.Classes.YearBuiltData("reference")), [1]));
                AssertLookupNotRun(await yearBuiltDataController.UpdateItemsByCountyIdsAsync(JsonArray(new GIS.Classes.YearBuiltData("reference")), [1]));

                BuildingDataController buildingDataController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                Table table = new();
                Column? column_Reference = table.AddColumn("Reference", typeof(string));
                Column? column_CountyId = table.AddColumn("County Id", typeof(int));
                Row? row = table.AddRow();
                if (row is not null && column_Reference is not null && column_CountyId is not null)
                {
                    row[column_Reference.Index] = "reference";
                    row[column_CountyId.Index] = 1;
                }
                string? json = Core.IO.Table.Convert.ToSystem_String<Table, Column, Row>(table);
                Assert.NotNull(json);
                JsonObject? jsonObject = JsonNode.Parse(json) as JsonObject;
                AssertInternalServerError(await buildingDataController.UpdateItemsByCountyIdsAsync(jsonObject, [1]));
                AssertLookupNotRun(await buildingDataController.UpdateItemsByCountyIdsAsync(jsonObject, [1]));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Asserts that a write endpoint given nothing to write still answers 204, not 500.
        /// <para>This is the branch that makes a later empty result unambiguous, and it is the one that must not regress: an upload that correctly matches nothing has to stay a success. Every route above reaches its database call only after this guard, which is why zero identifiers past it can be read as a failure.</para>
        /// </summary>
        [Fact]
        public async Task UpdateItems_NothingToWriteAnswersNoContent()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher GISWebAPIConfigurationFileWatcher = new(path);

                JsonArray jsonArray = [];

                Building2DController building2DController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                Assert.IsType<NoContentResult>(await building2DController.UpdateItemsAsync(jsonArray, null));
                Assert.IsType<NoContentResult>(await building2DController.UpdateItemsByCountyIdsAsync(jsonArray, [1]));

                EPWFileController ePWFileController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.EPWFilePostgreSQLConverter(null));
                Assert.IsType<NoContentResult>(await ePWFileController.UpdateItemsAsync(jsonArray));

                OccupancyDataController occupancyDataController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.Building2DOccupancyDataPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DOccupancyDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                Assert.IsType<NoContentResult>(await occupancyDataController.Building2DUpdateItemsByCountyIdsAsync(jsonArray, [1]));
                Assert.IsType<NoContentResult>(await occupancyDataController.AdministrativeAreal2DUpdateItemsAsync(jsonArray));

                OrtoDatasController ortoDatasController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.OrtoDatasPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                Assert.IsType<NoContentResult>(await ortoDatasController.UpdateItemsByCountyIdsAsync(jsonArray, [1]));

                YearBuiltDataController yearBuiltDataController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.YearBuiltDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null), new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null));
                Assert.IsType<NoContentResult>(await yearBuiltDataController.UpdateItemsByCountyIdsAsync(jsonArray, [1]));

                BuildingModelController buildingModelController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingModelPostgreSQLConverter(null, BuildingModelDetailLevel.Component), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                Assert.IsType<NoContentResult>(await buildingModelController.UpdateItemsByCountyIdsAsync(jsonArray, [1]));

                BuildingDataController buildingDataController = new(GISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
                Assert.IsType<NoContentResult>(await buildingDataController.UpdateItemsByCountyIdsAsync(null, [1]));
                Assert.IsType<NoContentResult>(await buildingDataController.UpdateItemsByCountyIdsAsync(new JsonObject(), [1]));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        private static void AssertInternalServerError(IActionResult actionResult)
        {
            ObjectResult objectResult = Assert.IsType<ObjectResult>(actionResult);

            Assert.Equal(500, objectResult.StatusCode);
        }

        private static void AssertLookupNotRun(IActionResult actionResult)
        {
            ObjectResult objectResult = Assert.IsType<ObjectResult>(actionResult);

            Assert.Equal(500, objectResult.StatusCode);

            // Only the lookup-not-run body tells this 500 from the "resolved nothing" and "database failed"
            // 500s, so the body is part of the assertion.
            string? message = objectResult.Value as string;
            Assert.True(message is not null && message.Contains("could not be resolved"), $"Expected the county-part lookup body, actual: {message}");
        }

        private static GIS.Classes.Building2D Building2D()
        {
            return new(Guid.NewGuid(), "reference", null, 1, null, null, []);
        }

        private static CityGML.Classes.Building Building()
        {
            return new(Guid.NewGuid().ToString(), -1, null);
        }

        private static BuildingModel BuildingModel()
        {
            BuildingModel buildingModel = new();
            buildingModel.SetValue(BuildingModelParameter.Reference, "reference", new SetValueSettings(true, false));
            return buildingModel;
        }

        private static string ConfigurationFilePath()
        {
            // The watcher reads plain 'Name=Value' lines and its constructor throws when the file is
            // missing, so the flags have to exist on disk before the controllers are built.
            string result = System.IO.Path.GetTempFileName();

            System.IO.File.WriteAllLines(result,
            [
                $"{nameof(GISWebAPIConfigurationFileWatcher.Open)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateAddressPoint)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateAdministrativeAreal2D)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateBuilding)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateBuilding2D)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateBuildingData)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateBuildingModel)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateEPWFile)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateOccupancyData)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateOrtoDatas)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateYearBuiltData)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowDeleteYearBuiltData)}=true",
            ]);

            return result;
        }

        private static JsonArray JsonArray(Core.Interfaces.ISerializableObject serializableObject)
        {
            return [serializableObject.ToJsonObject()];
        }

        private static GIS.Classes.OccupancyData OccupancyData()
        {
            return new("reference", 100, 4);
        }
    }
}
