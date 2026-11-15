using DiGi.Analytical.Building.Classes;
using DiGi.EPW.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.WebAPI.UI.Classes;
using DiGi.GIS.WebAPI.UI.Controllers;
using DiGi.GIS.WebAPI.UI.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a building with a result stored for its model, radius, solver version and this EPW file is answered from storage on both synchronous routes: nothing is solved - the solve gate is closed, so a solve would answer 503 - and no neighbour is fetched.
        /// <para>The stored read sends the contract's names - <c>id</c> (the model id derived from its Guid), <c>countyid</c>, <c>radius</c>, <c>solverversion</c> - and no <c>shadingfactors</c>; the view reports <c>Source = Stored</c> with the stored time, and nothing is saved again. Reference: DiGi.GIS.WebAPI.UI#74.</para>
        /// </summary>
        [Fact]
        public async Task SolarController_Stored_Hit()
        {
            BuildingModel buildingModel = SolarStoreFixture_BuildingModel();
            ScriptedWebApi scriptedWebApi = SolarStoreFixture_ScriptedWebApi(buildingModel, SolarFixture_EPWFile(), SolarStoreFixture_Result(buildingModel, SolarStoreFixture_EPWFileId.ToString(CultureInfo.InvariantCulture)));

            SolarRadiationViewModel solarRadiationViewModel = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi, true).GetViewByBuildingModelIdAsync(7, 1465, null)).Value);
            Assert.Equal("Stored", solarRadiationViewModel.Source);
            Assert.Equal(SolarStoreFixture_StoredAt, solarRadiationViewModel.StoredAt);
            Assert.False(solarRadiationViewModel.Saved);
            Assert.True(solarRadiationViewModel.SignedIn);
            Assert.Equal(5, solarRadiationViewModel.Surfaces.Count);

            ContentResult contentResult = Assert.IsType<ContentResult>(await SolarStoreFixture_Controller(scriptedWebApi, false).GetRadiationByBuildingModelIdAsync(7, 1465, 50));
            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults = Core.Convert.ToDiGi<SurfaceSolarRadiationResult>(contentResult.Content);
            Assert.NotNull(surfaceSolarRadiationResults);
            Assert.Equal(5, surfaceSolarRadiationResults.Count);
            Assert.All(surfaceSolarRadiationResults, x => Assert.Equal(500, x.Irradiation));

            Assert.Empty(SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingmodel/itemsbycircle"));
            Assert.Empty(SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingsolarradiation/setsolarradiation"));

            List<(HttpMethod Method, Uri? RequestUri, string? Body)> requests_Stored = SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingsolarradiation/solarradiationbybuildingmodelid");
            Assert.Equal(2, requests_Stored.Count);
            Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(requests_Stored[0].RequestUri!.Query);
            Assert.Equal(buildingModel.SolarBuildingModelId()!.Value.ToString(CultureInfo.InvariantCulture), query["id"].ToString());
            Assert.Equal("1465", query["countyid"].ToString());
            Assert.Equal(50.0, double.Parse(query["radius"]!, CultureInfo.InvariantCulture));
            Assert.Equal(GIS.Analytical.Solar.Constants.Default.SolarSolverVersion.ToString(CultureInfo.InvariantCulture), query["solverversion"].ToString());
            Assert.False(query.ContainsKey("shadingfactors"));
        }

        /// <summary>
        /// Verifies that a result stored for another weather source is rebuilt from its stored shading factors for this EPW file, without neighbours or a solve, and stored again for a signed-in visitor: the factors are re-read with <c>shadingfactors=true</c>, one save is posted with the visitor's bearer token, and the view reports <c>Source = Rebuilt</c>, saved. An anonymous visitor gets the rebuild and no save.
        /// <para>Reference: DiGi.GIS.WebAPI.UI#73, #74.</para>
        /// </summary>
        [Fact]
        public async Task SolarController_Stored_OtherWeatherSource()
        {
            BuildingModel buildingModel = SolarStoreFixture_BuildingModel();
            ScriptedWebApi scriptedWebApi = SolarStoreFixture_ScriptedWebApi(buildingModel, SolarFixture_EPWFile(), SolarStoreFixture_Result(buildingModel, "16"));

            SolarRadiationViewModel solarRadiationViewModel = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi, true).GetViewByBuildingModelIdAsync(7, 1465, null)).Value);
            Assert.Equal("Rebuilt", solarRadiationViewModel.Source);
            Assert.True(solarRadiationViewModel.Saved);
            Assert.Equal(5, solarRadiationViewModel.Surfaces.Count);

            Assert.Empty(SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingmodel/itemsbycircle"));
            Assert.Contains(SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingsolarradiation/solarradiationbybuildingmodelid"), x => QueryHelpers.ParseQuery(x.RequestUri!.Query).TryGetValue("shadingfactors", out StringValues value) && bool.TryParse(value.ToString(), out bool includeShadingFactors) && includeShadingFactors);

            (HttpMethod Method, Uri? RequestUri, string? Body) request_Store = Assert.Single(SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingsolarradiation/setsolarradiation"));
            Assert.Equal(HttpMethod.Post, request_Store.Method);
            Assert.Equal($"Bearer {SolarStoreFixture_Token}", scriptedWebApi.Authorizations[scriptedWebApi.Requests.IndexOf(request_Store)]);
            GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult_Stored = Core.Convert.ToDiGi<GIS.Classes.BuildingSolarRadiationResult>(request_Store.Body)?[0];
            Assert.NotNull(buildingSolarRadiationResult_Stored);
            Assert.Equal(SolarStoreFixture_EPWFileId.ToString(CultureInfo.InvariantCulture), buildingSolarRadiationResult_Stored.WeatherSourceId);
            Assert.Null(buildingSolarRadiationResult_Stored.UserEmail);

            ScriptedWebApi scriptedWebApi_Anonymous = SolarStoreFixture_ScriptedWebApi(buildingModel, SolarFixture_EPWFile(), SolarStoreFixture_Result(buildingModel, "16"));
            SolarRadiationViewModel solarRadiationViewModel_Anonymous = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi_Anonymous, false).GetViewByBuildingModelIdAsync(7, 1465, null)).Value);
            Assert.Equal("Rebuilt", solarRadiationViewModel_Anonymous.Source);
            Assert.False(solarRadiationViewModel_Anonymous.Saved);
            Assert.False(solarRadiationViewModel_Anonymous.SignedIn);
            Assert.Empty(SolarStoreFixture_Requests(scriptedWebApi_Anonymous, "/gis/buildingsolarradiation/setsolarradiation"));
        }

        /// <summary>
        /// Verifies that a stored building above the synchronous receiver ceiling (more than <see cref="Constants.Default.SolarReceiverCountMax"/> walls and roofs) is answered from storage on the synchronous route instead of 413: the ceilings guard a solve, and a stored result needs none.
        /// <para>Reference: DiGi.GIS.WebAPI.UI#74.</para>
        /// </summary>
        [Fact]
        public async Task SolarController_Stored_HitOverReceiverCeiling()
        {
            BuildingModel buildingModel = SolarStoreFixture_BuildingModel(30);
            GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult = SolarStoreFixture_Result(buildingModel, SolarStoreFixture_EPWFileId.ToString(CultureInfo.InvariantCulture));
            Assert.True(buildingSolarRadiationResult.SurfaceSolarRadiations!.Count > Constants.Default.SolarReceiverCountMax);

            ScriptedWebApi scriptedWebApi = SolarStoreFixture_ScriptedWebApi(buildingModel, SolarFixture_EPWFile(), buildingSolarRadiationResult);

            SolarRadiationViewModel solarRadiationViewModel = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi, false).GetViewByBuildingModelIdAsync(7, 1465, null)).Value);
            Assert.Equal("Stored", solarRadiationViewModel.Source);
            Assert.Equal(buildingSolarRadiationResult.SurfaceSolarRadiations.Count, solarRadiationViewModel.Surfaces.Count);

            // Without a stored result the same request is refused as before.
            ScriptedWebApi scriptedWebApi_Miss = SolarStoreFixture_ScriptedWebApi(buildingModel, SolarFixture_EPWFile(), null);
            ObjectResult objectResult = Assert.IsType<ObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi_Miss, false).GetViewByBuildingModelIdAsync(7, 1465, null));
            Assert.Equal(413, objectResult.StatusCode);
        }

        /// <summary>
        /// Verifies that an anonymous request for a building with nothing stored is solved as before and posts no save: the view reports <c>Source = Solved</c>, not saved, not signed in.
        /// <para>Medium test (a full-year solve of the box). Reference: DiGi.GIS.WebAPI.UI#74.</para>
        /// </summary>
        [MediumFact]
        public async Task SolarController_Anonymous_NoSave()
        {
            BuildingModel buildingModel = SolarStoreFixture_BuildingModel();
            ScriptedWebApi scriptedWebApi = SolarStoreFixture_ScriptedWebApi(buildingModel, SolarFixture_EPWFile(), null);

            SolarRadiationViewModel solarRadiationViewModel = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi, false, new SemaphoreSlim(1, 1)).GetViewByBuildingModelIdAsync(7, 1465, null)).Value);
            Assert.Equal("Solved", solarRadiationViewModel.Source);
            Assert.False(solarRadiationViewModel.Saved);
            Assert.False(solarRadiationViewModel.SignedIn);
            Assert.Null(solarRadiationViewModel.StoredAt);

            Assert.Single(SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingmodel/itemsbycircle"));
            Assert.Empty(SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingsolarradiation/setsolarradiation"));
        }

        /// <summary>
        /// Verifies that a signed-in request for a building with nothing stored is solved and stored once, with the visitor's bearer token and a body in the DiGi shape the endpoint binds: <c>_type</c>, the key the reads use, <c>WeatherSourceType</c> as an integer, the EPW id as the weather source, 8 760 shading factors per surface, and no user email or time - the server stamps both.
        /// <para>A save the GIS Web API refuses (500) still answers the visitor 200, with <c>Saved = false</c>. Medium test (two full-year solves). Reference: DiGi.GIS.WebAPI.UI#74.</para>
        /// </summary>
        [MediumFact]
        public async Task SolarController_LoggedIn_SavesOnce()
        {
            BuildingModel buildingModel = SolarStoreFixture_BuildingModel();
            EPWFile ePWFile = SolarFixture_EPWFile();

            ScriptedWebApi scriptedWebApi = SolarStoreFixture_ScriptedWebApi(buildingModel, ePWFile, null);
            SolarRadiationViewModel solarRadiationViewModel = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi, true, new SemaphoreSlim(1, 1)).GetViewByBuildingModelIdAsync(7, 1465, null)).Value);
            Assert.Equal("Solved", solarRadiationViewModel.Source);
            Assert.True(solarRadiationViewModel.Saved);
            Assert.True(solarRadiationViewModel.SignedIn);

            (HttpMethod Method, Uri? RequestUri, string? Body) request_Store = Assert.Single(SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingsolarradiation/setsolarradiation"));
            Assert.Equal($"Bearer {SolarStoreFixture_Token}", scriptedWebApi.Authorizations[scriptedWebApi.Requests.IndexOf(request_Store)]);

            JsonObject? jsonObject = JsonNode.Parse(request_Store.Body!) as JsonObject;
            Assert.NotNull(jsonObject);
            Assert.Contains("BuildingSolarRadiationResult", jsonObject["_type"]?.GetValue<string>());
            Assert.Equal(JsonValueKind.Number, jsonObject[nameof(GIS.Classes.BuildingSolarRadiationResult.WeatherSourceType)]?.GetValueKind());

            GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult = Core.Convert.ToDiGi<GIS.Classes.BuildingSolarRadiationResult>(request_Store.Body)?[0];
            Assert.NotNull(buildingSolarRadiationResult);
            Assert.Equal("B7", buildingSolarRadiationResult.Reference);
            Assert.Equal(1465, buildingSolarRadiationResult.CountyId);
            Assert.Equal(buildingModel.SolarBuildingModelId(), buildingSolarRadiationResult.BuildingModelId);
            Assert.Equal(Constants.Default.SolarSurroundingRadius, buildingSolarRadiationResult.Radius);
            Assert.Equal(GIS.Analytical.Solar.Constants.Default.SolarSolverVersion, buildingSolarRadiationResult.SolverVersion);
            Assert.Equal(GIS.Enums.WeatherSourceType.EPW, buildingSolarRadiationResult.WeatherSourceType);
            Assert.Equal(SolarStoreFixture_EPWFileId.ToString(CultureInfo.InvariantCulture), buildingSolarRadiationResult.WeatherSourceId);
            Assert.Null(buildingSolarRadiationResult.UserEmail);
            Assert.Null(buildingSolarRadiationResult.CreatedAt);
            Assert.Equal(5, buildingSolarRadiationResult.SurfaceSolarRadiations!.Count);
            Assert.All(buildingSolarRadiationResult.SurfaceSolarRadiations, x => Assert.Equal(GIS.Analytical.Solar.Constants.Default.SolarReferenceHourCount, x.ShadingFactors?.Length));

            // A refused save never fails the visitor's response.
            ScriptedWebApi scriptedWebApi_Refused = SolarStoreFixture_ScriptedWebApi(buildingModel, ePWFile, null, HttpStatusCode.InternalServerError);
            SolarRadiationViewModel solarRadiationViewModel_Refused = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi_Refused, true, new SemaphoreSlim(1, 1)).GetViewByBuildingModelIdAsync(7, 1465, null)).Value);
            Assert.False(solarRadiationViewModel_Refused.Saved);
            Assert.True(solarRadiationViewModel_Refused.SignedIn);
            Assert.Single(SolarStoreFixture_Requests(scriptedWebApi_Refused, "/gis/buildingsolarradiation/setsolarradiation"));
        }

        /// <summary>
        /// Verifies the job route with a stored result: the POST answers 200 with the view instead of 202, and no job is queued.
        /// <para>Reference: DiGi.GIS.WebAPI.UI#74.</para>
        /// </summary>
        [Fact]
        public async Task SolarController_Job_Stored_Hit()
        {
            BuildingModel buildingModel = SolarStoreFixture_BuildingModel(30);
            ScriptedWebApi scriptedWebApi = SolarStoreFixture_ScriptedWebApi(buildingModel, SolarFixture_EPWFile(), SolarStoreFixture_Result(buildingModel, SolarStoreFixture_EPWFileId.ToString(CultureInfo.InvariantCulture)));
            SolarJobQueue solarJobQueue = new(TimeProvider.System);

            SolarRadiationViewModel solarRadiationViewModel = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(await SolarStoreFixture_Controller(scriptedWebApi, false, null, solarJobQueue).PostJobAsync(7, 1465, null)).Value);
            Assert.Equal("Stored", solarRadiationViewModel.Source);
            Assert.Equal(0, solarJobQueue.QueuedCount());
            Assert.Empty(solarJobQueue.SolarJobs);
        }

        /// <summary>
        /// Verifies the job route without a stored result: a signed-in job stores its result once after the run, with the visitor's token, and its view reports it saved; an anonymous job posts nothing and reports not signed in. The token is released with the storage step once it has run.
        /// <para>Medium test (two full-year solves). Reference: DiGi.GIS.WebAPI.UI#74.</para>
        /// </summary>
        [MediumFact]
        public async Task SolarController_Job_SavesOnce()
        {
            BuildingModel buildingModel = SolarStoreFixture_BuildingModel();
            EPWFile ePWFile = SolarFixture_EPWFile();

            foreach (bool signedIn in new bool[] { true, false })
            {
                ScriptedWebApi scriptedWebApi = SolarStoreFixture_ScriptedWebApi(buildingModel, ePWFile, null);
                SolarJobQueue solarJobQueue = new(TimeProvider.System);
                SemaphoreSlim semaphoreSlim = new(1, 1);
                SolarController solarController = SolarStoreFixture_Controller(scriptedWebApi, signedIn, semaphoreSlim, solarJobQueue);

                Assert.IsType<AcceptedResult>(await solarController.PostJobAsync(7, 1465, null));
                SolarJob solarJob = Assert.Single(solarJobQueue.SolarJobs.Values);
                Assert.Equal(signedIn, solarJob.SignedIn);

                await solarJobQueue.SolveAsync(solarJob, semaphoreSlim);
                Assert.Equal(Enums.SolarJobStatus.Completed, solarJob.Status);
                Assert.Null(solarJob.Storage);
                Assert.Equal(signedIn, solarJob.Saved);

                List<(HttpMethod Method, Uri? RequestUri, string? Body)> requests_Store = SolarStoreFixture_Requests(scriptedWebApi, "/gis/buildingsolarradiation/setsolarradiation");
                Assert.Equal(signedIn ? 1 : 0, requests_Store.Count);
                if (signedIn)
                {
                    Assert.Equal($"Bearer {SolarStoreFixture_Token}", scriptedWebApi.Authorizations[scriptedWebApi.Requests.IndexOf(requests_Store[0])]);
                }

                SolarRadiationViewModel solarRadiationViewModel = Assert.IsType<SolarRadiationViewModel>(Assert.IsType<OkObjectResult>(solarController.GetJobView(solarJob.Id)).Value);
                Assert.Equal("Solved", solarRadiationViewModel.Source);
                Assert.Equal(signedIn, solarRadiationViewModel.Saved);
                Assert.Equal(signedIn, solarRadiationViewModel.SignedIn);
            }
        }
    }
}
