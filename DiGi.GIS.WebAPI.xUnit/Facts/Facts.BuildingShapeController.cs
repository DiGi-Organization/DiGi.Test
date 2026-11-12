using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The access key written into the synthetic configuration of the building shape controller facts.
        /// </summary>
        private const string buildingShapeKey = "xunit-building-shape-key";

        /// <summary>
        /// Writes a synthetic GIS Web API configuration for the building shape controller facts: the <see cref="GISWebAPIConfigurationFileWatcher.AllowUpdateBuildingShapeLabel"/> gate, and either the open escape hatch or an enforced access key (<see cref="buildingShapeKey"/>).
        /// <para>The watcher reads plain Name=Value lines and its constructor throws when the file is missing, so the keys have to exist on disk before the controller is built.</para>
        /// </summary>
        /// <param name="allowUpdateBuildingShapeLabel">The value written for the labelling gate.</param>
        /// <param name="open">Whether the configuration sets <c>Open=true</c>; when false, key enforcement is enabled with <see cref="buildingShapeKey"/>.</param>
        /// <returns>The path of the temporary configuration file; the caller deletes it.</returns>
        private static string BuildingShapeConfigurationFilePath(bool allowUpdateBuildingShapeLabel = true, bool open = true)
        {
            string result = System.IO.Path.GetTempFileName();

            System.IO.File.WriteAllLines(result,
            [
                $"{nameof(GISWebAPIConfigurationFileWatcher.Open)}={(open ? "true" : "false")}",
                $"{nameof(GISWebAPIConfigurationFileWatcher.Enabled)}=true",
                $"{nameof(GISWebAPIConfigurationFileWatcher.Key)}={buildingShapeKey}",
                $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateBuildingShapeLabel)}={(allowUpdateBuildingShapeLabel ? "true" : "false")}",
            ]);

            return result;
        }

        /// <summary>
        /// Builds a <see cref="BuildingShapeController"/> over converters on the given connection data (null: every store read answers null without opening a connection), and wires its HttpContext either anonymously or with a user token minted the way the user extension mints one.
        /// <para>The security key manager and the revocation store are created in both modes, so an anonymous 401 is proved to come from the missing or invalid Authorization header rather than from an absent user extension.</para>
        /// </summary>
        /// <param name="gISWebAPIConfigurationFileWatcher">The configuration watcher holding the gate and the key.</param>
        /// <param name="supplyToken">Whether to set a Bearer Authorization header.</param>
        /// <param name="rawToken">The raw token to send instead of a freshly minted valid one; used only with <paramref name="supplyToken"/> set.</param>
        /// <param name="email">The e-mail the minted token carries.</param>
        /// <param name="connectionData_Main">The main database the converters use, or null for no database.</param>
        /// <returns>The controller with its ControllerContext set.</returns>
        private static BuildingShapeController CreateBuildingShapeController(GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher, bool supplyToken, string? rawToken = null, string email = "annotator@example.com", ConnectionData? connectionData_Main = null)
        {
            SecurityKeyManager securityKeyManager = new();
            _ = securityKeyManager.Generate();
            TokenRevocationStore tokenRevocationStore = new();

            DefaultHttpContext httpContext = new();
            if (supplyToken)
            {
                string token = rawToken ?? CreateToken(securityKeyManager, email);
                httpContext.Request.Headers.Authorization = "Bearer " + token;
            }

            BuildingShapeController controller = new(
                gISWebAPIConfigurationFileWatcher,
                new PostgreSQL.Classes.Building2DPostgreSQLConverter(connectionData_Main),
                new PostgreSQL.Classes.BuildingShapePostgreSQLConverter(connectionData_Main),
                securityKeyManager,
                tokenRevocationStore);

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            return controller;
        }
    }
}
