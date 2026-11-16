using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Reflection;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="BuildingDataController"/> write endpoints return BadRequest when provided invalid parameters, and that the commandTimeout guard refuses a negative value while accepting 0 (DiGi-Organization/DiGi.GIS.WebAPI#48).
        /// </summary>
        [Fact]
        public async Task BuildingDataController_Validation_AnswersBadRequest()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null), new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));

                Assert.IsType<BadRequestResult>(await controller.UpdateItemsByCountyIdsAsync(new JsonObject(), null));
                Assert.IsType<BadRequestResult>(await controller.UpdateItemsByCountyIdsAsync(new JsonObject(), []));

                // commandTimeout: -1 is refused by the guard ahead of the countyIds and body checks, while
                // 0 disables the timeout and runs on to the empty body's NoContent (DiGi.GIS.WebAPI#48).
                Assert.IsType<BadRequestResult>(await controller.UpdateItemsByCountyIdsAsync(new JsonObject(), [1], -1));
                Assert.IsType<NoContentResult>(await controller.UpdateItemsByCountyIdsAsync(null, [1], 0));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies that <see cref="BuildingDataController"/> write endpoints return Unauthorized when write permissions are disabled in the configuration.
        /// </summary>
        [Fact]
        public async Task BuildingDataController_DisabledUpdates_AnswersUnauthorized()
        {
            string path = System.IO.Path.GetTempFileName();

            try
            {
                System.IO.File.WriteAllLines(path,
                [
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateBuildingData)}=false",
                ]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null), new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));

                Assert.IsType<UnauthorizedResult>(await controller.UpdateItemsByCountyIdsAsync(new JsonObject(), [1]));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies that <see cref="BuildingDataController"/> write endpoints return BadRequest when the table does not contain a Reference column.
        /// </summary>
        [Fact]
        public async Task BuildingDataController_MissingReferenceColumn_AnswersBadRequest()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null), new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));

                Core.IO.Table.Classes.Table table_NoReference = new();
                table_NoReference.AddColumn("Other", typeof(string));
                Core.IO.Table.Classes.Row? row = table_NoReference.AddRow();
                if (row is not null)
                {
                    row[0] = "val";
                }
                string? json_NoReference = Core.IO.Table.Convert.ToSystem_String<Core.IO.Table.Classes.Table, Core.IO.Table.Classes.Column, Core.IO.Table.Classes.Row>(table_NoReference);
                Assert.NotNull(json_NoReference);
                JsonObject? jsonObject_NoReference = JsonNode.Parse(json_NoReference) as JsonObject;

                Assert.IsType<BadRequestResult>(await controller.UpdateItemsByCountyIdsAsync(jsonObject_NoReference, [1]));

                Core.IO.Table.Classes.Table table_LowercaseReference = new();
                table_LowercaseReference.AddColumn("reference", typeof(string));
                Core.IO.Table.Classes.Row? row_Lower = table_LowercaseReference.AddRow();
                if (row_Lower is not null)
                {
                    row_Lower[0] = "ref1";
                }
                string? json_Lower = Core.IO.Table.Convert.ToSystem_String<Core.IO.Table.Classes.Table, Core.IO.Table.Classes.Column, Core.IO.Table.Classes.Row>(table_LowercaseReference);
                Assert.NotNull(json_Lower);
                JsonObject? jsonObject_Lower = JsonNode.Parse(json_Lower) as JsonObject;

                IActionResult actionResult = await controller.UpdateItemsByCountyIdsAsync(jsonObject_Lower, [1]);
                ObjectResult objectResult = Assert.IsType<ObjectResult>(actionResult);
                Assert.Equal(500, objectResult.StatusCode);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// A transient database failure is a "retry can succeed" class, so it must answer 503 with a Retry-After header rather than the uniform 500.
        /// <para>Drives a real transient failure through the real stack with no server: a dead loopback host makes the converter's OpenAsync throw a genuine NpgsqlException wrapping a SocketException, which Npgsql classifies IsTransient = true (verified against the deployed Npgsql 10.0.2). The 2026-09-12 production read-timeout failures and the pool-exhaustion shape are the same IsTransient = true class, so this fact covers them.</para>
        /// <para>Red before the 503 mapping existed (the path answered 500); green after.</para>
        /// <para>Medium test (2.1 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingDataController_TransientDatabaseFailure_Answers503()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(
                    gISWebAPIConfigurationFileWatcher,
                    new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(new DiGi.PostgreSQL.Classes.ConnectionData("127.0.0.1", "user", "pass", "db", 1)),
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                ObjectResult objectResult = Assert.IsType<ObjectResult>(await controller.GetCategoriesAsync());

                Assert.Equal(503, objectResult.StatusCode);
                Assert.Equal("30", (string?)controller.HttpContext.Response.Headers["Retry-After"]);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// The 503 branch is guarded by <c>NpgsqlException.IsTransient</c>, so non-transient database failures keep the uniform 500 and only the "retry can succeed" class is reclassified.
        /// <para>Pins the classification contract the guard relies on (verified against Npgsql 10.0.2): a statement timeout (57014) and a plain NpgsqlException are not transient; pool exhaustion (53300) and socket read timeouts are.</para>
        /// </summary>
        [Fact]
        public void BuildingDataController_TransientPredicate_RoutesOnlyTransientTo503()
        {
            Assert.True(new NpgsqlException("read", new System.TimeoutException()).IsTransient);
            Assert.True(new NpgsqlException("read", new System.IO.IOException()).IsTransient);
            Assert.True(new PostgresException("too many clients", "FATAL", "FATAL", "53300").IsTransient);

            Assert.False(new NpgsqlException("boom").IsTransient);
            Assert.False(new PostgresException("cancelling statement", "ERROR", "ERROR", "57014").IsTransient);
        }

        /// <summary>
        /// Pins the <c>histogramsummary</c> body contract of DiGi-Organization/DiGi.GIS.WebAPI#35: <see cref="HistogramRequestParameter.HistogramBucketing"/> binds from its integer value under the web defaults the input formatter deserializes with (<c>1</c> is equal count), and a body without the property keeps equal width, so clients written against the earlier contract are unaffected.
        /// </summary>
        [Fact]
        public void HistogramRequestParameter_HistogramBucketing_BindsFromInteger()
        {
            System.Text.Json.JsonSerializerOptions jsonSerializerOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

            HistogramRequestParameter? equalCount = System.Text.Json.JsonSerializer.Deserialize<HistogramRequestParameter>("{\"ColumnUniqueId\":\"floor_area\",\"CountyId\":53477,\"BucketCount\":1000,\"HistogramBucketing\":1}", jsonSerializerOptions);
            Assert.NotNull(equalCount);
            Assert.Equal(DiGi.PostgreSQL.Table.Enums.HistogramBucketing.EqualCount, equalCount.HistogramBucketing);
            Assert.Equal(1000, equalCount.BucketCount);
            Assert.Equal(53477, equalCount.CountyId);

            HistogramRequestParameter? omitted = System.Text.Json.JsonSerializer.Deserialize<HistogramRequestParameter>("{\"ColumnUniqueId\":\"floor_area\",\"CountyId\":53477,\"BucketCount\":1000}", jsonSerializerOptions);
            Assert.NotNull(omitted);
            Assert.Equal(DiGi.PostgreSQL.Table.Enums.HistogramBucketing.EqualWidth, omitted.HistogramBucketing);
        }
        /// <summary>
        /// Pins the <c>countbyfiltergroup</c> contract of DiGi-Organization/DiGi.GIS.WebAPI#52: the action is a thin filtered <c>COUNT(*)</c> over the converter, so a negative <c>commandtimeout</c> is refused with 400 before any database work, and with no connection to build (null connection data) the converter returns null and the action answers 404 rather than a 5xx - for both an absent filter group and a present one.
        /// </summary>
        [Fact]
        public async Task BuildingDataController_CountByFilterGroup_GuardsAndNoConnection()
        {
            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null), new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));

                BuildingDataCountByFilterGroupParameter parameter = new() { CountyId = 10365 };

                // commandtimeout: -1 is refused by the guard ahead of the database read.
                Assert.IsType<BadRequestResult>(await controller.GetCountByFilterGroupAsync(parameter, -1));

                // No connection to build: the converter returns null, so the action answers NotFound - with an absent filter group...
                Assert.IsType<NotFoundResult>(await controller.GetCountByFilterGroupAsync(parameter, 600));

                // ...and with a present (empty) filter group alike.
                BuildingDataCountByFilterGroupParameter parameterWithGroup = new() { CountyId = 10365, FilterGroup = new DiGi.PostgreSQL.Table.Classes.FilterGroup() };
                Assert.IsType<NotFoundResult>(await controller.GetCountByFilterGroupAsync(parameterWithGroup, 600));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Pins the <c>countbyfiltergroup</c> route and success response type of DiGi-Organization/DiGi.GIS.WebAPI#52: it is a <c>POST</c> to <c>countbyfiltergroup</c>, and the 200 response is documented as a bare <c>long</c> integer.
        /// </summary>
        [Fact]
        public void BuildingDataController_CountByFilterGroup_RouteAndResponseType()
        {
            System.Reflection.MethodInfo method = typeof(BuildingDataController).GetMethod(nameof(BuildingDataController.GetCountByFilterGroupAsync))!;
            Assert.NotNull(method);

            HttpPostAttribute? httpPost = method.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(httpPost);
            Assert.Equal("countbyfiltergroup", httpPost.Template);

            ProducesResponseTypeAttribute? produces = null;
            foreach (ProducesResponseTypeAttribute attribute in method.GetCustomAttributes<ProducesResponseTypeAttribute>())
            {
                if (attribute.StatusCode == 200)
                {
                    produces = attribute;
                    break;
                }
            }

            Assert.NotNull(produces);
            Assert.Equal(typeof(long), produces.Type);
        }

    }
}
