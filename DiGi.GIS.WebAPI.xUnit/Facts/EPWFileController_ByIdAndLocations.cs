using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the query parameter names of <c>gis/epwfile/itembyid</c> and <c>gis/epwfile/locations</c> (<c>Coding - WebAPI Contracts.md</c> §1), and that the id binds as nullable, so an omitted id reaches the action as null and is refused instead of meaning 0 (§2). Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public void EPWFileController_ByIdAndLocations_QueryParameterNames()
        {
            string[] names_ById = [.. EPWFileQueryParameterNames(nameof(EPWFileController.GetEPWFileByIdAsync))];
            string[] names_ById_Expected = ["id", "commandtimeout"];
            Assert.Equal(names_ById_Expected, names_ById);

            ParameterInfo parameterInfo_Id = typeof(EPWFileController).GetMethod(nameof(EPWFileController.GetEPWFileByIdAsync))!.GetParameters()[0];
            Assert.NotNull(Nullable.GetUnderlyingType(parameterInfo_Id.ParameterType));

            string[] names_Locations = [.. EPWFileQueryParameterNames(nameof(EPWFileController.GetEPWFileLocationsAsync))];
            string[] names_Locations_Expected = ["commandtimeout"];
            Assert.Equal(names_Locations_Expected, names_Locations);
        }

        /// <summary>
        /// Verifies that <c>gis/epwfile/itembyid</c> answers 400 for a missing, zero or negative id and a negative timeout, and 500 - not the 404 of a missing row - when the converter has no connection; and that <c>gis/epwfile/locations</c> answers 400 for a negative timeout and 500, never an empty list, without a connection. Reference: DiGi.GIS.WebAPI#93.
        /// </summary>
        [Fact]
        public async Task EPWFileController_ByIdAndLocations_InvalidParameter()
        {
            using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(SolarRadiationConfigurationFilePath(true));
            EPWFileController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.EPWFilePostgreSQLConverter(null));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            Assert.IsType<BadRequestResult>(await controller.GetEPWFileByIdAsync(null));
            Assert.IsType<BadRequestResult>(await controller.GetEPWFileByIdAsync(0));
            Assert.IsType<BadRequestResult>(await controller.GetEPWFileByIdAsync(-3));
            Assert.IsType<BadRequestResult>(await controller.GetEPWFileByIdAsync(7, -1));
            Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<ObjectResult>(await controller.GetEPWFileByIdAsync(7)).StatusCode);

            Assert.IsType<BadRequestResult>(await controller.GetEPWFileLocationsAsync(-1));
            Assert.Equal(StatusCodes.Status500InternalServerError, Assert.IsType<ObjectResult>(await controller.GetEPWFileLocationsAsync()).StatusCode);
        }

        /// <summary>
        /// Verifies on the throwaway main test database that <c>gis/epwfile/itembyid</c> answers the same body and <c>DiGi-EPWFile-Id</c> header as the by-location read of the same row, that an unknown id answers 404, and that <c>gis/epwfile/locations</c> lists the row with its id and coordinates, in id order.
        /// <para>The Warsaw fixture is stored as a scratch row (<c>XUNIT-EPW-93</c>) far outside Poland, so the nearest row to that point is the scratch row, and removed before and after. Medium test (a live database server). Reference: DiGi.GIS.WebAPI#93.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task EPWFileController_ByIdAndLocations_RoundTrip()
        {
            const double x = -9999998;
            const double y = -9999997;

            (ConnectionData connectionData_Main, _) = SolarRadiationTestConnectionDatas();

            EPW.Classes.EPWFile? ePWFile_Fixture = EPW.Modify.Read(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw"));
            Assert.NotNull(ePWFile_Fixture);

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();
            Assert.True(await PostgreSQL.Create.TableAsync_EPWFile(npgsqlConnection));

            string commandText_Cleanup = $"DELETE FROM {PostgreSQL.Constants.TableName.EPWFile} WHERE name = 'XUNIT-EPW-93';";
            await ExecuteSolarRadiationAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                int id;
                await using (NpgsqlCommand npgsqlCommand = new($"INSERT INTO {PostgreSQL.Constants.TableName.EPWFile} (name, x, y, object) VALUES ('XUNIT-EPW-93', @x, @y, @object) RETURNING id;", npgsqlConnection))
                {
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("x", NpgsqlDbType.Double) { Value = x });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("y", NpgsqlDbType.Double) { Value = y });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = ePWFile_Fixture.ToJsonObject()!.ToJsonString() });
                    id = (int)(await npgsqlCommand.ExecuteScalarAsync())!;
                }

                PostgreSQL.Classes.EPWFilePostgreSQLConverter ePWFilePostgreSQLConverter = new(connectionData_Main);
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(SolarRadiationConfigurationFilePath(true));

                EPWFileController controller_ByLocation = new(gISWebAPIConfigurationFileWatcher, ePWFilePostgreSQLConverter);
                controller_ByLocation.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
                ContentResult contentResult_ByLocation = Assert.IsType<ContentResult>(await controller_ByLocation.GetEPWFileAsync(x, y));

                EPWFileController controller_ById = new(gISWebAPIConfigurationFileWatcher, ePWFilePostgreSQLConverter);
                controller_ById.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
                ContentResult contentResult_ById = Assert.IsType<ContentResult>(await controller_ById.GetEPWFileByIdAsync(id));

                Assert.Equal(contentResult_ByLocation.Content, contentResult_ById.Content);
                Assert.Equal(id.ToString(CultureInfo.InvariantCulture), controller_ById.HttpContext.Response.Headers[Constants.Header.EPWFileId].ToString());
                Assert.Equal(controller_ByLocation.HttpContext.Response.Headers[Constants.Header.EPWFileId].ToString(), controller_ById.HttpContext.Response.Headers[Constants.Header.EPWFileId].ToString());

                EPWFileController controller_Unknown = new(gISWebAPIConfigurationFileWatcher, ePWFilePostgreSQLConverter);
                controller_Unknown.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
                Assert.IsType<NotFoundResult>(await controller_Unknown.GetEPWFileByIdAsync(int.MaxValue));

                EPWFileController controller_Locations = new(gISWebAPIConfigurationFileWatcher, ePWFilePostgreSQLConverter);
                controller_Locations.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
                ContentResult contentResult_Locations = Assert.IsType<ContentResult>(await controller_Locations.GetEPWFileLocationsAsync());

                List<PostgreSQL.Classes.EPWFileLocationResult>? ePWFileLocationResults = Core.Convert.ToDiGi<PostgreSQL.Classes.EPWFileLocationResult>(contentResult_Locations.Content);
                Assert.NotNull(ePWFileLocationResults);

                PostgreSQL.Classes.EPWFileLocationResult ePWFileLocationResult = Assert.Single(ePWFileLocationResults, ePWFileLocationResult => ePWFileLocationResult.Id == id);
                Assert.Equal(x, ePWFileLocationResult.X);
                Assert.Equal(y, ePWFileLocationResult.Y);

                int[] ids = [.. ePWFileLocationResults.Select(ePWFileLocationResult => ePWFileLocationResult.Id)];
                int[] ids_Ordered = [.. ids.Order()];
                Assert.Equal(ids_Ordered, ids);
            }
            finally
            {
                await ExecuteSolarRadiationAsync(npgsqlConnection, commandText_Cleanup);
            }
        }

        /// <summary>
        /// Lists the names of the query parameters of an <see cref="EPWFileController"/> action, in declaration order.
        /// </summary>
        /// <param name="methodName">The name of the action.</param>
        /// <returns>The names.</returns>
        private static List<string> EPWFileQueryParameterNames(string methodName)
        {
            MethodInfo? methodInfo = typeof(EPWFileController).GetMethod(methodName);
            Assert.NotNull(methodInfo);

            List<string> result = [];
            foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
            {
                if (parameterInfo.GetCustomAttribute<FromQueryAttribute>() is FromQueryAttribute fromQueryAttribute)
                {
                    Assert.NotNull(fromQueryAttribute.Name);
                    result.Add(fromQueryAttribute.Name);
                }
            }

            return result;
        }
    }
}
