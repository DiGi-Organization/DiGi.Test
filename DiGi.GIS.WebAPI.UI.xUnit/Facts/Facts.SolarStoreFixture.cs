using DiGi.Analytical.Building.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.WebAPI.UI.Classes;
using DiGi.GIS.WebAPI.UI.Controllers;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The <c>epw_file</c> id the scripted GIS Web API names in the <c>DiGi-EPWFile-Id</c> header.
        /// </summary>
        private const int SolarStoreFixture_EPWFileId = 17;

        /// <summary>
        /// The bearer token a signed-in request carries in its cookie.
        /// </summary>
        private const string SolarStoreFixture_Token = "xunit-token";

        /// <summary>
        /// The stamp of the scratch stored results.
        /// </summary>
        private static readonly DateTimeOffset SolarStoreFixture_StoredAt = new(2026, 10, 1, 9, 30, 0, TimeSpan.Zero);

        /// <summary>
        /// Builds a 10 m box with <paramref name="southStripCount"/> south wall strips, stamped and carrying the plain <c>building_2d</c> reference <c>B7</c>, as the solar routes receive a model.
        /// </summary>
        /// <param name="southStripCount">The number of strips the south wall is split into; more than 26 puts the box above the synchronous receiver ceiling.</param>
        /// <returns>The building model.</returns>
        private static BuildingModel SolarStoreFixture_BuildingModel(int southStripCount = 1)
        {
            BuildingModel buildingModel = SolarFixture_BuildingModel(SolarFixture_BoxComponents(SolarFixture_Origin.X, SolarFixture_Origin.Y, 10, false, southStripCount), new Point3D(SolarFixture_Origin.X + 5, SolarFixture_Origin.Y + 5, 5));
            Assert.True(buildingModel.SetValue(Analytical.Enums.BuildingModelParameter.Reference, "B7", new Core.Parameter.Classes.SetValueSettings(true, false)));
            return buildingModel;
        }

        /// <summary>
        /// Builds a stored result for every receiving surface of a building without solving: made-up summary values and shading factors of 0 (unshaded), enough for a reuse or a rebuild to be answered.
        /// </summary>
        /// <param name="buildingModel">The building model.</param>
        /// <param name="weatherSourceId">The weather source id of the stored summary.</param>
        /// <returns>The stored result, keyed as the controller keys it at radius 50 m.</returns>
        private static GIS.Classes.BuildingSolarRadiationResult SolarStoreFixture_Result(BuildingModel buildingModel, string weatherSourceId)
        {
            BuildingModel buildingModel_Stamped = new(buildingModel);
            Assert.True(GIS.Analytical.Modify.UpdateBuildingInformation(buildingModel_Stamped));
            Dictionary<string, Vector3D>? normals = buildingModel_Stamped.SolarReceiverNormals();
            Assert.NotNull(normals);

            List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations = [];
            foreach (KeyValuePair<string, Vector3D> keyValuePair in normals)
            {
                surfaceSolarRadiations.Add(new GIS.Classes.SurfaceSolarRadiation(keyValuePair.Key, 20, keyValuePair.Value, 1, 1, 500, 300, 150, 50, 500, 10000, new byte[GIS.Analytical.Solar.Constants.Default.SolarReferenceHourCount]));
            }

            long? buildingModelId = buildingModel.SolarBuildingModelId();
            Assert.NotNull(buildingModelId);

            return new GIS.Classes.BuildingSolarRadiationResult("B7", 1465, buildingModelId.Value, 50, GIS.Analytical.Solar.Constants.Default.SolarSolverVersion, GIS.Enums.WeatherSourceType.EPW, weatherSourceId, null, null, "owner@example.com", SolarStoreFixture_StoredAt, surfaceSolarRadiations);
        }

        /// <summary>
        /// A GIS Web API serving one building model, its neighbours, a weather file with its <c>DiGi-EPWFile-Id</c>, and a stored result: without factors on the default read, with them for <c>shadingfactors=true</c>, 204 when none is given. The save answers <paramref name="httpStatusCode_Store"/> with a row id.
        /// </summary>
        /// <param name="buildingModel">The building model.</param>
        /// <param name="ePWFile">The weather file.</param>
        /// <param name="buildingSolarRadiationResult">The stored result, or null for none (204).</param>
        /// <param name="httpStatusCode_Store">The status the save answers.</param>
        /// <returns>The scripted API.</returns>
        private static ScriptedWebApi SolarStoreFixture_ScriptedWebApi(BuildingModel buildingModel, EPWFile ePWFile, GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult, HttpStatusCode httpStatusCode_Store = HttpStatusCode.OK)
        {
            string? json_Model = Core.Convert.ToSystem_String(new List<BuildingModel>() { buildingModel });
            string? json_EPW = Core.Convert.ToSystem_String((Core.Interfaces.ISerializableObject)ePWFile);
            string? json_Reference = Core.Convert.ToSystem_String(new PostgreSQL.Classes.Building2DReference() { Id = 7, CountyId = 1465, Reference = "B7" });
            string? json_Stored = buildingSolarRadiationResult is null ? null : Core.Convert.ToSystem_String(PostgreSQL.Create.BuildingSolarRadiationResult(buildingSolarRadiationResult, buildingSolarRadiationResult.CountyId, null));
            string? json_Stored_Factors = buildingSolarRadiationResult is null ? null : Core.Convert.ToSystem_String(buildingSolarRadiationResult);

            return new ScriptedWebApi((httpRequestMessage, body) =>
            {
                string path = httpRequestMessage.RequestUri!.AbsolutePath;

                string? json = null;
                if (path.EndsWith("/gis/building2D/building2Dreferencebyid", StringComparison.OrdinalIgnoreCase))
                {
                    json = json_Reference;
                }
                else if (path.EndsWith("/gis/buildingmodel/itemsbyreferences", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/gis/buildingmodel/itemsbycircle", StringComparison.OrdinalIgnoreCase))
                {
                    json = json_Model;
                }
                else if (path.EndsWith("/gis/epwfile/item", StringComparison.OrdinalIgnoreCase))
                {
                    HttpResponseMessage httpResponseMessage_EPW = new(HttpStatusCode.OK) { Content = new StringContent(json_EPW!) };
                    httpResponseMessage_EPW.Headers.Add(Constants.Default.EPWFileIdHeaderName, SolarStoreFixture_EPWFileId.ToString(CultureInfo.InvariantCulture));
                    return httpResponseMessage_EPW;
                }
                else if (path.EndsWith("/gis/buildingsolarradiation/solarradiationbybuildingmodelid", StringComparison.OrdinalIgnoreCase))
                {
                    Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(httpRequestMessage.RequestUri.Query);
                    json = query.TryGetValue("shadingfactors", out StringValues shadingFactors) && bool.TryParse(shadingFactors.ToString(), out bool includeShadingFactors) && includeShadingFactors ? json_Stored_Factors : json_Stored;
                    if (json is null)
                    {
                        return new HttpResponseMessage(HttpStatusCode.NoContent) { Content = new StringContent(string.Empty) };
                    }
                }
                else if (path.EndsWith("/gis/buildingsolarradiation/setsolarradiation", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(httpStatusCode_Store) { Content = new StringContent(httpStatusCode_Store == HttpStatusCode.OK ? "123" : string.Empty, Encoding.UTF8, "application/json") };
                }

                if (json is null)
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
                }

                return Answer(json);
            });
        }

        /// <summary>
        /// The controller under test on a scripted GIS Web API, signed in when <paramref name="signedIn"/> is true. Its solve gate holds no slot and is waited on for 1 ms, so a request that reaches the solve answers 503 unless the fact passes a gate of its own.
        /// </summary>
        /// <param name="scriptedWebApi">The scripted GIS Web API.</param>
        /// <param name="signedIn">True to send the user token cookie.</param>
        /// <param name="semaphoreSlim">The solve gate, or null for a closed one.</param>
        /// <param name="solarJobQueue">The job queue, or null for a new one.</param>
        /// <returns>The controller.</returns>
        private static SolarController SolarStoreFixture_Controller(ScriptedWebApi scriptedWebApi, bool signedIn, SemaphoreSlim? semaphoreSlim = null, SolarJobQueue? solarJobQueue = null)
        {
            SolarController solarController = SolarController_Controller(scriptedWebApi, semaphoreSlim ?? new SemaphoreSlim(0, 1), solarJobQueue);
            solarController.SolveGateWait = TimeSpan.FromMilliseconds(1);
            if (signedIn)
            {
                solarController.ControllerContext.HttpContext.Request.Headers["Cookie"] = $"{Constants.Default.UserTokenCookieName}={SolarStoreFixture_Token}";
            }

            return solarController;
        }

        /// <summary>
        /// The requests a scripted GIS Web API received on one path.
        /// </summary>
        /// <param name="scriptedWebApi">The scripted GIS Web API.</param>
        /// <param name="path">The path the requests end with.</param>
        /// <returns>The requests, in order.</returns>
        private static List<(HttpMethod Method, Uri? RequestUri, string? Body)> SolarStoreFixture_Requests(ScriptedWebApi scriptedWebApi, string path)
        {
            return [.. scriptedWebApi.Requests.Where(x => x.RequestUri!.AbsolutePath.EndsWith(path, StringComparison.OrdinalIgnoreCase))];
        }
    }
}
