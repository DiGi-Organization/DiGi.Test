using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the every-column answer of DiGi-Organization/DiGi.GIS.WebAPI#102: <c>GET gis/buildingdata/columnvalues</c>
        /// carries one key per limited-choice column, each key the unique id <c>DiGi.PostgreSQL.Table.Query.UniqueId</c> gives
        /// the real <c>DiGi.GIS.IO.Constants.Column</c> member, and each value set is exactly what the writer stores, in enum
        /// declaration order. The controller's converters are built with no connection data, so the fact also proves the answer
        /// reads no database: a read would fail rather than answer.
        /// </summary>
        [Fact]
        public void BuildingDataController_ColumnValues_AnswersEveryLimitedChoiceColumn()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = CreateBuildingDataController(gISWebAPIConfigurationFileWatcher);

                ContentResult contentResult = Assert.IsType<ContentResult>(controller.GetColumnValues());
                Assert.Equal("application/json", contentResult.ContentType);
                Assert.NotNull(contentResult.Content);

                JsonObject? jsonObject = JsonNode.Parse(contentResult.Content)?.AsObject();
                Assert.NotNull(jsonObject);

                string[] columnUniqueIds =
                [
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.BuildingGeneralFunction)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.BuildingSpecificFunctions)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.BuildingPhase)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.CardinalDirection)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.SettlementType)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.CalculatedBuildingShape)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.PredictedBuildingShape)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.UserBuildingShape)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.IsResidential)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.IsOccupied)!,
                    DiGi.PostgreSQL.Table.Query.UniqueId(DiGi.GIS.IO.Constants.Column.SolarWeatherSourceType)!,
                ];

                // Every key is a real column's unique id, so a caller can hand it straight back to columns or uniquevalues.
                Assert.Equal(
                    columnUniqueIds.OrderBy(x => x, StringComparer.Ordinal),
                    jsonObject.Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal));

                AssertValues(jsonObject["building_general_function"]!.AsArray(), Descriptions<DiGi.GIS.Enums.BuildingGeneralFunction>());
                AssertValues(jsonObject["building_specific_functions"]!.AsArray(), Descriptions<DiGi.GIS.Enums.BuildingSpecificFunction>());
                AssertValues(jsonObject["building_phase"]!.AsArray(), Descriptions<DiGi.GIS.Enums.BuildingPhase>());
                AssertValues(jsonObject["settlement_type"]!.AsArray(), Descriptions<DiGi.GIS.Enums.SettlementType>());
                AssertValues(jsonObject["calculated_building_shape"]!.AsArray(), Descriptions<DiGi.GIS.Enums.BuildingShape>());
                AssertValues(jsonObject["predicted_building_shape"]!.AsArray(), Descriptions<DiGi.GIS.Enums.BuildingShape>());
                AssertValues(jsonObject["user_building_shape"]!.AsArray(), Descriptions<DiGi.GIS.Enums.BuildingShape>());

                // The one column stored by the enum-to-string conversion: member names, never the descriptions, whose
                // "North East" / "South East" / ... differ from the stored NorthEast / SouthEast / ... .
                AssertValues(jsonObject["cardinal_direction"]!.AsArray(), Names<DiGi.GIS.Enums.CardinalDirection>());

                AssertValues(jsonObject["is_residential"]!.AsArray(), [false, true]);
                AssertValues(jsonObject["is_occupied"]!.AsArray(), [false, true]);
                AssertValues(jsonObject["solar_weather_source_type"]!.AsArray(), [-1, 0, 1]);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Pins the single-column answer of DiGi-Organization/DiGi.GIS.WebAPI#102: <c>columnuniqueid</c> narrows the answer
        /// to that column's array alone, shaped like <c>uniquevalues</c>, carrying the exact text and order the writer
        /// stores - <c>building_phase</c> as its four descriptions, <c>cardinal_direction</c> as its member names.
        /// </summary>
        [Fact]
        public void BuildingDataController_ColumnValues_SingleColumn_AnswersThatColumnsArray()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = CreateBuildingDataController(gISWebAPIConfigurationFileWatcher);

                ContentResult buildingPhase = Assert.IsType<ContentResult>(controller.GetColumnValues("building_phase"));
                Assert.Equal("application/json", buildingPhase.ContentType);
                Assert.Equal("""["in use","unoccupied","under construction","destroyed"]""", buildingPhase.Content);

                ContentResult cardinalDirection = Assert.IsType<ContentResult>(controller.GetColumnValues("cardinal_direction"));
                Assert.Equal("application/json", cardinalDirection.ContentType);
                Assert.Equal("""["Undefined","North","NorthEast","East","SouthEast","South","SouthWest","West","NorthWest"]""", cardinalDirection.Content);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Pins the 400 contract of DiGi-Organization/DiGi.GIS.WebAPI#102: a <c>columnuniqueid</c> that names no
        /// limited-choice column - a stored column with no bounded domain among them, as <c>floor_area</c> is - and a blank
        /// id are client errors answered with a ProblemDetails body, never a silent empty array and never a 500.
        /// </summary>
        [Fact]
        public void BuildingDataController_ColumnValues_UnsupportedColumn_Answers400()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = CreateBuildingDataController(gISWebAPIConfigurationFileWatcher);

                ObjectResult objectResult = Assert.IsType<ObjectResult>(controller.GetColumnValues("floor_area"));
                Assert.Equal(400, objectResult.StatusCode);

                ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
                Assert.False(string.IsNullOrWhiteSpace(problemDetails.Detail));
                Assert.Contains("floor_area", problemDetails.Detail);

                ObjectResult blankResult = Assert.IsType<ObjectResult>(controller.GetColumnValues("   "));
                Assert.Equal(400, blankResult.StatusCode);
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Builds a <see cref="BuildingDataController"/> whose converters carry no connection data: the columnvalues answer
        /// reads no database, so any read would fail loudly instead of answering.
        /// </summary>
        /// <param name="gISWebAPIConfigurationFileWatcher">The watcher the controller takes.</param>
        /// <returns>The controller under test.</returns>
        private static BuildingDataController CreateBuildingDataController(GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher)
        {
            return new BuildingDataController(
                gISWebAPIConfigurationFileWatcher,
                new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null),
                new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null),
                new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));
        }

        /// <summary>
        /// Asserts that a value array of the columnvalues answer holds exactly the expected values, in order, each
        /// serialized the way the answer serializes it.
        /// </summary>
        /// <param name="jsonArray">The array read from the answer.</param>
        /// <param name="expectedValues">The expected values, in order.</param>
        private static void AssertValues(JsonArray? jsonArray, IEnumerable<object?> expectedValues)
        {
            Assert.NotNull(jsonArray);

            JsonArray expectedJsonArray = [];
            foreach (object? expectedValue in expectedValues)
            {
                expectedJsonArray.Add(Core.Create.JsonNode(expectedValue));
            }

            Assert.Equal(expectedJsonArray.ToJsonString(), jsonArray.ToJsonString());
        }

        /// <summary>
        /// Gets the description of every member of an enumeration, in declaration order - the text the writer stores.
        /// </summary>
        /// <typeparam name="T">The enumeration whose members to describe.</typeparam>
        /// <returns>The descriptions, boxed as objects for <see cref="AssertValues"/>.</returns>
        private static IEnumerable<object?> Descriptions<T>() where T : struct, Enum
        {
            return Enum.GetValues<T>().Select(x => (object?)Core.Query.Description(x));
        }

        /// <summary>
        /// Gets the member name of every member of an enumeration, in declaration order - what the enum-to-string
        /// conversion stores.
        /// </summary>
        /// <typeparam name="T">The enumeration whose names to read.</typeparam>
        /// <returns>The names, boxed as objects for <see cref="AssertValues"/>.</returns>
        private static IEnumerable<object?> Names<T>() where T : struct, Enum
        {
            return Enum.GetValues<T>().Select(x => (object?)Enum.GetName(x));
        }
    }
}
