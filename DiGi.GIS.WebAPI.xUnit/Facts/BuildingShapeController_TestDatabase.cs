using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Walks one annotator through a scratch sample on the throwaway main test database through <c>gis/buildingshape</c> (DiGi.GIS.WebAPI#80), checking the wire bodies the labelling page relies on.
        /// <para><c>nextsample</c> answers <c>SampleKey</c>, <c>Ordinal</c>, <c>CountyId</c>, <c>Reference</c> and <c>Building2D</c> - null here, because the scratch references hold no footprint - and never <c>SolverShape</c>; <c>afterordinal</c> moves past a skipped item. A label is stamped with the token's e-mail, <c>progress</c> answers <c>{ Labelled, Total }</c>, <c>labels</c> and <c>sampleitems</c> answer DiGi JSON that reads back as <see cref="BuildingShapeLabel"/> and <see cref="BuildingShapeSampleItem"/>, a label outside the sample answers 400, and loading the existing sample key answers 409 before any footprint is read. The fact writes scratch rows under a guid-based <c>xunit-building-shape-api-</c> key and removes them before and after. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingShapeController_TestDatabase()
        {
            (ConnectionData connectionData_Main, _) = SolarRadiationTestConnectionDatas();

            string sampleKey = $"xunit-building-shape-api-{Guid.NewGuid():N}";
            const string email = "xunit-building-shape-api@example.invalid";
            const int countyId = 990703;

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await PostgreSQL.Create.TableAsync_BuildingShapeSample(npgsqlConnection));
            Assert.True(await PostgreSQL.Create.TableAsync_BuildingShapeLabel(npgsqlConnection));

            string commandText_Cleanup = $"DELETE FROM {PostgreSQL.Constants.TableName.BuildingShapeLabel} WHERE sample_key = '{sampleKey}'; DELETE FROM {PostgreSQL.Constants.TableName.BuildingShapeSample} WHERE sample_key = '{sampleKey}';";
            await ExecuteSolarRadiationAsync(npgsqlConnection, commandText_Cleanup);

            string path = BuildingShapeConfigurationFilePath();
            try
            {
                PostgreSQL.Classes.BuildingShapePostgreSQLConverter buildingShapePostgreSQLConverter = new(connectionData_Main);
                Assert.True(await buildingShapePostgreSQLConverter.InsertSampleAsync(
                [
                    new BuildingShapeSampleItem(sampleKey, 0, countyId, "XUNIT-API-SHAPE-0", BuildingShape.T),
                    new BuildingShapeSampleItem(sampleKey, 1, countyId, "XUNIT-API-SHAPE-1", BuildingShape.Square),
                ]));

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingShapeController controller = CreateBuildingShapeController(gISWebAPIConfigurationFileWatcher, supplyToken: true, email: email, connectionData_Main: connectionData_Main);

                // The next item carries the identity and the footprint slot, never the solver shape.
                JsonObject jsonObject_Next = Assert.IsType<JsonObject>(JsonNode.Parse(Assert.IsType<ContentResult>(await controller.NextSampleAsync(sampleKey)).Content!));
                Assert.Equal(sampleKey, (string?)jsonObject_Next["SampleKey"]);
                Assert.Equal(0, (int?)jsonObject_Next["Ordinal"]);
                Assert.Equal(countyId, (int?)jsonObject_Next["CountyId"]);
                Assert.Equal("XUNIT-API-SHAPE-0", (string?)jsonObject_Next["Reference"]);
                Assert.True(jsonObject_Next.ContainsKey("Building2D"));
                Assert.Null(jsonObject_Next["Building2D"]);
                Assert.False(jsonObject_Next.ContainsKey("SolverShape"));

                // Skip: after ordinal 0 the next item is ordinal 1.
                JsonObject jsonObject_Skipped = Assert.IsType<JsonObject>(JsonNode.Parse(Assert.IsType<ContentResult>(await controller.NextSampleAsync(sampleKey, afterOrdinal: 0)).Content!));
                Assert.Equal(1, (int?)jsonObject_Skipped["Ordinal"]);

                // Label the first item; progress moves and the next item is ordinal 1.
                Assert.IsType<OkResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { SampleKey = sampleKey, CountyId = countyId, Reference = "XUNIT-API-SHAPE-0", Shape = (short)BuildingShape.L, Note = "notch" }));

                JsonObject jsonObject_Progress = Assert.IsType<JsonObject>(JsonNode.Parse(Assert.IsType<ContentResult>(await controller.ProgressAsync(sampleKey)).Content!));
                Assert.Equal(1, (int?)jsonObject_Progress["Labelled"]);
                Assert.Equal(2, (int?)jsonObject_Progress["Total"]);

                JsonObject jsonObject_Next_2 = Assert.IsType<JsonObject>(JsonNode.Parse(Assert.IsType<ContentResult>(await controller.NextSampleAsync(sampleKey)).Content!));
                Assert.Equal(1, (int?)jsonObject_Next_2["Ordinal"]);

                // A label outside the sample is refused.
                Assert.IsType<BadRequestObjectResult>(await controller.SetLabelAsync(new Classes.Parameter.BuildingShapeLabelParameter() { SampleKey = sampleKey, CountyId = countyId, Reference = "XUNIT-API-SHAPE-OUTSIDE", Shape = 4 }));

                // The exports read back as DiGi objects; the interpreter is the token's e-mail.
                List<BuildingShapeLabel>? buildingShapeLabels = Core.Convert.ToDiGi<BuildingShapeLabel>(Assert.IsType<ContentResult>(await controller.LabelsAsync(sampleKey)).Content);
                BuildingShapeLabel buildingShapeLabel = Assert.Single(buildingShapeLabels!);
                Assert.Equal(email, buildingShapeLabel.Interpreter);
                Assert.Equal(BuildingShape.L, buildingShapeLabel.BuildingShape);
                Assert.Equal("notch", buildingShapeLabel.Note);

                List<BuildingShapeSampleItem>? buildingShapeSampleItems = Core.Convert.ToDiGi<BuildingShapeSampleItem>(Assert.IsType<ContentResult>(await controller.SampleItemsAsync(sampleKey)).Content);
                Assert.NotNull(buildingShapeSampleItems);
                Assert.Equal([BuildingShape.T, BuildingShape.Square], buildingShapeSampleItems.Select(x => x.SolverShape));

                // Loading an existing key is refused before any footprint is read.
                Assert.IsType<ConflictObjectResult>(await controller.CreateSampleAsync(sampleKey, [countyId], 10, 12, key: buildingShapeKey));
            }
            finally
            {
                System.IO.File.Delete(path);
                await ExecuteSolarRadiationAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
