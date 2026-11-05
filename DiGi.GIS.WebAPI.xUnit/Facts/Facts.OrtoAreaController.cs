using DiGi.GIS.WebAPI.Classes;
using DiGi.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Writes a synthetic GIS Web API configuration whose <see cref="GISWebAPIConfigurationFileWatcher.AllowUpdateOrtoAreaLabel"/> gate is set to the requested value, so the label write can be driven with the flag on (validation chains reach their own guards) and off (the gate itself answers).
        /// <para>The watcher reads plain Name=Value lines and its constructor throws when the file is missing, so the keys have to exist on disk before the controller is built.</para>
        /// </summary>
        /// <param name="allowUpdateOrtoAreaLabel">The value written for the label gate.</param>
        /// <returns>The path of the temporary configuration file; the caller deletes it.</returns>
        private static string OrtoAreaConfigurationFilePath(bool allowUpdateOrtoAreaLabel = true)
        {
            string result = System.IO.Path.GetTempFileName();

            System.IO.File.WriteAllLines(result,
            [
                $"{nameof(GISWebAPIConfigurationFileWatcher.Open)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateOrtoAreaLabel)}={(allowUpdateOrtoAreaLabel ? "true" : "false")}",
            ]);

            return result;
        }

        /// <summary>
        /// Builds an <see cref="OrtoAreaController"/> whose seven converters all sit on null ConnectionData, so every store read answers null without opening a connection, and wires its HttpContext either anonymously or with a user token minted the way the user extension mints one.
        /// <para>The security key manager and the revocation store are created in both modes, so an anonymous 401 is proved to come from the missing or invalid Authorization header rather than from an absent user extension.</para>
        /// </summary>
        /// <param name="gISWebAPIConfigurationFileWatcher">The configuration watcher holding the label gate.</param>
        /// <param name="supplyToken">Whether to set a Bearer Authorization header.</param>
        /// <param name="rawToken">The raw token to send instead of a freshly minted valid one; used only with <paramref name="supplyToken"/> set, to pin that a malformed token denies too.</param>
        /// <returns>The controller with its ControllerContext set.</returns>
        private static OrtoAreaController CreateOrtoAreaController(GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher, bool supplyToken, string? rawToken = null)
        {
            SecurityKeyManager securityKeyManager = new();
            _ = securityKeyManager.Generate();
            TokenRevocationStore tokenRevocationStore = new();

            DefaultHttpContext httpContext = new();
            if (supplyToken)
            {
                string token = rawToken ?? CreateToken(securityKeyManager, "reviewer@example.com");
                httpContext.Request.Headers.Authorization = "Bearer " + token;
            }

            OrtoAreaController controller = new(
                gISWebAPIConfigurationFileWatcher,
                new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                new PostgreSQL.Classes.OrtoAreaImagePostgreSQLConverter(null),
                new PostgreSQL.Classes.OrtoAreaLabelPostgreSQLConverter(null),
                new PostgreSQL.Classes.OrtoAreaObjectPostgreSQLConverter(null),
                new PostgreSQL.Classes.OrtoAreaPostgreSQLConverter(null),
                new PostgreSQL.Classes.OrtoSheetPostgreSQLConverter(null),
                securityKeyManager,
                tokenRevocationStore);

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            return controller;
        }

        /// <summary>
        /// Serializes a drawn polygon as the bare point-pair array a relayed body may carry, which is the first shape <c>gis/ortoarea</c>'s polygon parser accepts.
        /// </summary>
        /// <param name="vertices">The vertices, each as an easting/northing pair.</param>
        /// <returns>The JSON array of point pairs.</returns>
        private static JsonArray OrtoAreaPolygonJson(params double[][] vertices)
        {
            JsonArray jsonArray = [];
            foreach (double[] vertex in vertices)
            {
                JsonArray jsonArray_Point = [];
                foreach (double coordinate in vertex)
                {
                    jsonArray_Point.Add(coordinate);
                }

                jsonArray.Add(jsonArray_Point);
            }

            return jsonArray;
        }
    }
}
