using DiGi.GIS.WebAPI.UI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Creates the Building Shape relay controller (DiGi.GIS.WebAPI.UI#86) on a stub handler, sending the user token cookie only when <paramref name="signedIn"/> is true, and giving the request the raw <paramref name="body"/> when one is provided.
        /// </summary>
        /// <param name="httpMessageHandler">The stub handler answering the upstream requests.</param>
        /// <param name="signedIn">True to send the user token cookie; false for the anonymous visitor.</param>
        /// <param name="body">The raw request body. This value can be null.</param>
        /// <returns>The controller.</returns>
        private static BuildingShapeController CreateBuildingShapeController(HttpMessageHandler httpMessageHandler, bool signedIn, string? body = null)
        {
            BuildingShapeController buildingShapeController = new(new HandlerHttpClientFactory(httpMessageHandler));

            DefaultHttpContext defaultHttpContext = new();
            if (signedIn)
            {
                defaultHttpContext.Request.Headers["Cookie"] = $"{DiGi.GIS.WebAPI.UI.Constants.Default.UserTokenCookieName}=test-token";
            }

            if (body is not null)
            {
                defaultHttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            }

            buildingShapeController.ControllerContext = new ControllerContext { HttpContext = defaultHttpContext };

            return buildingShapeController;
        }

        /// <summary>
        /// Verifies that every Building Shape relay answers 401 for an anonymous visitor before any upstream request is made (DiGi.GIS.WebAPI.UI#86).
        /// </summary>
        [Fact]
        public async Task BuildingShapeController_Relay_Anonymous()
        {
            ScriptedWebApi scriptedWebApi = new((request, body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
            BuildingShapeController buildingShapeController = CreateBuildingShapeController(scriptedWebApi, signedIn: false, body: "{\"Shape\":1}");

            Assert.IsType<UnauthorizedResult>(await buildingShapeController.GetNextAsync(null));
            Assert.IsType<UnauthorizedResult>(await buildingShapeController.GetProgressAsync());
            Assert.IsType<UnauthorizedResult>(await buildingShapeController.GetLabelsAsync());
            Assert.IsType<UnauthorizedResult>(await buildingShapeController.GetSampleItemsAsync());
            Assert.IsType<UnauthorizedResult>(await buildingShapeController.SetLabelAsync());
            Assert.IsType<UnauthorizedResult>(await buildingShapeController.GetImageAsync(1465, "REF"));

            Assert.Empty(scriptedWebApi.Requests);
        }

        /// <summary>
        /// Verifies the next-item relay (DiGi.GIS.WebAPI.UI#86): the upstream query carries the sample key and the optional <c>afterordinal</c> under the names <c>gis/buildingshape/nextsample</c> binds, the session is presented as a bearer token, a 200 body is relayed verbatim, an upstream 404 (every item labelled) becomes the empty object <c>{}</c>, other statuses pass through, and an unreachable service is 503.
        /// </summary>
        [Fact]
        public async Task BuildingShapeController_Relay_Next()
        {
            const string json = "{\"SampleKey\":\"gis12-2026\",\"Ordinal\":3,\"CountyId\":1465,\"Reference\":\"REF\",\"Building2D\":null}";

            ScriptedWebApi scriptedWebApi = new((request, body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            BuildingShapeController buildingShapeController = CreateBuildingShapeController(scriptedWebApi, signedIn: true);

            ContentResult contentResult = Assert.IsType<ContentResult>(await buildingShapeController.GetNextAsync(2));
            Assert.Equal(json, contentResult.Content);

            (HttpMethod method, Uri? requestUri, _) = Assert.Single(scriptedWebApi.Requests);
            Assert.Equal(HttpMethod.Get, method);
            Assert.NotNull(requestUri);
            Assert.Equal("/gis/buildingshape/nextsample", requestUri.AbsolutePath);
            Assert.Contains($"samplekey={DiGi.GIS.WebAPI.UI.Constants.Default.BuildingShapeSampleKey}", requestUri.Query);
            Assert.Contains("afterordinal=2", requestUri.Query);
            Assert.Equal("Bearer test-token", Assert.Single(scriptedWebApi.Authorizations));

            // Without afterordinal the parameter is not sent at all.
            ScriptedWebApi scriptedWebApi_NoOrdinal = new((request, body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
            await CreateBuildingShapeController(scriptedWebApi_NoOrdinal, signedIn: true).GetNextAsync(null);
            Assert.DoesNotContain("afterordinal", scriptedWebApi_NoOrdinal.Requests.Single().RequestUri!.Query);

            // Every item labelled: the upstream 404 is the finished state, not an error.
            ScriptedWebApi scriptedWebApi_Finished = new((request, body) => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) });
            Assert.Equal("{}", Assert.IsType<ContentResult>(await CreateBuildingShapeController(scriptedWebApi_Finished, signedIn: true).GetNextAsync(null)).Content);

            // Other statuses pass through; only the next read maps 404.
            ScriptedWebApi scriptedWebApi_Fault = new((request, body) => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) });
            Assert.Equal(500, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi_Fault, signedIn: true).GetNextAsync(null)).StatusCode);

            ScriptedWebApi scriptedWebApi_Gone = new((request, body) => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) });
            Assert.Equal(404, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi_Gone, signedIn: true).GetProgressAsync()).StatusCode);

            ScriptedWebApi scriptedWebApi_Unreachable = new((request, body) => throw new HttpRequestException("The service is unreachable."));
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi_Unreachable, signedIn: true).GetNextAsync(null)).StatusCode);
        }

        /// <summary>
        /// Verifies the label relay (DiGi.GIS.WebAPI.UI#86): the posted body reaches <c>gis/buildingshape/label</c> byte for byte with the bearer token, the upstream status is mirrored (a 400 refusal stays 400), an empty body is refused before any request, and an unreachable service is 503.
        /// </summary>
        [Fact]
        public async Task BuildingShapeController_Relay_Label()
        {
            const string body = "{\"SampleKey\":\"gis12-2026\",\"CountyId\":1465,\"Reference\":\"REF \\u00f3\",\"Shape\":4,\"Note\":null}";

            ScriptedWebApi scriptedWebApi = new((request, requestBody) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
            Assert.Equal(200, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi, signedIn: true, body: body).SetLabelAsync()).StatusCode);

            (HttpMethod method, Uri? requestUri, string? requestBody) = Assert.Single(scriptedWebApi.Requests);
            Assert.Equal(HttpMethod.Post, method);
            Assert.Equal("/gis/buildingshape/label", requestUri!.AbsolutePath);
            Assert.Equal(body, requestBody);
            Assert.Equal("Bearer test-token", Assert.Single(scriptedWebApi.Authorizations));

            ScriptedWebApi scriptedWebApi_Refused = new((request, requestBody) => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("The item is not part of the sample.") });
            Assert.Equal(400, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi_Refused, signedIn: true, body: body).SetLabelAsync()).StatusCode);

            ScriptedWebApi scriptedWebApi_Empty = new((request, requestBody) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
            Assert.IsType<BadRequestResult>(await CreateBuildingShapeController(scriptedWebApi_Empty, signedIn: true, body: " ").SetLabelAsync());
            Assert.Empty(scriptedWebApi_Empty.Requests);

            ScriptedWebApi scriptedWebApi_Unreachable = new((request, requestBody) => throw new HttpRequestException("The service is unreachable."));
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi_Unreachable, signedIn: true, body: body).SetLabelAsync()).StatusCode);
        }

        /// <summary>
        /// Verifies the orthophoto relay (DiGi.GIS.WebAPI.UI#86): the photo years are read first and the newest one is fetched, with the reference fallback; a footprint without photo years answers 404 without an image request, and a missing reference is 400.
        /// <para>A failure is never read as "no photo" (Coding - WebAPI Contracts section 3): an unreachable service is 503 on either read, a years body that is not a year list and a photo without bytes are 502, an upstream fault status is mirrored, and only an upstream 404 of the photo is 404.</para>
        /// </summary>
        [Fact]
        public async Task BuildingShapeController_Relay_Image()
        {
            byte[] bytes = [0xFF, 0xD8, 0xFF, 0xE0];

            ScriptedWebApi scriptedWebApi = new((request, body) =>
            {
                string path = request.RequestUri?.AbsolutePath ?? string.Empty;
                if (path.EndsWith("/yearsbyreference", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[2009, 2023, 2015]") };
                }

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            });

            FileContentResult fileContentResult = Assert.IsType<FileContentResult>(await CreateBuildingShapeController(scriptedWebApi, signedIn: true).GetImageAsync(1465, "REF"));
            Assert.Equal("image/jpeg", fileContentResult.ContentType);
            Assert.Equal(bytes, fileContentResult.FileContents);

            Assert.Equal(2, scriptedWebApi.Requests.Count);
            Uri imageUri = scriptedWebApi.Requests[1].RequestUri!;
            Assert.EndsWith("/gis/ortodatas/imagebyreference", imageUri.AbsolutePath, StringComparison.Ordinal);
            Assert.Contains("year=2023", imageUri.Query);
            Assert.Contains("fallbackbyreference=", imageUri.Query);

            ScriptedWebApi scriptedWebApi_NoYears = new((request, body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
            Assert.IsType<NotFoundResult>(await CreateBuildingShapeController(scriptedWebApi_NoYears, signedIn: true).GetImageAsync(1465, "REF"));
            Assert.Single(scriptedWebApi_NoYears.Requests);

            Assert.IsType<BadRequestResult>(await CreateBuildingShapeController(scriptedWebApi_NoYears, signedIn: true).GetImageAsync(1465, " "));

            // The years read: unreachable, an answered fault and an unreadable body each keep their own status.
            ScriptedWebApi scriptedWebApi_Unreachable = new((request, body) => throw new HttpRequestException("The service is unreachable."));
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi_Unreachable, signedIn: true).GetImageAsync(1465, "REF")).StatusCode);

            ScriptedWebApi scriptedWebApi_YearsFault = new((request, body) => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) });
            Assert.Equal(500, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi_YearsFault, signedIn: true).GetImageAsync(1465, "REF")).StatusCode);

            ScriptedWebApi scriptedWebApi_YearsGarbled = new((request, body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"not\":\"a list\"}") });
            Assert.Equal(502, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(scriptedWebApi_YearsGarbled, signedIn: true).GetImageAsync(1465, "REF")).StatusCode);

            // The photo read: only its 404 is "no photo".
            ScriptedWebApi ScriptedImage(HttpStatusCode httpStatusCode, byte[] bytes_Image)
            {
                return new ScriptedWebApi((request, body) =>
                {
                    if ((request.RequestUri?.AbsolutePath ?? string.Empty).EndsWith("/yearsbyreference", StringComparison.Ordinal))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[2023]") };
                    }

                    return new HttpResponseMessage(httpStatusCode) { Content = new ByteArrayContent(bytes_Image) };
                });
            }

            Assert.IsType<NotFoundResult>(await CreateBuildingShapeController(ScriptedImage(HttpStatusCode.NotFound, []), signedIn: true).GetImageAsync(1465, "REF"));
            Assert.Equal(500, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(ScriptedImage(HttpStatusCode.InternalServerError, []), signedIn: true).GetImageAsync(1465, "REF")).StatusCode);
            Assert.Equal(502, Assert.IsType<StatusCodeResult>(await CreateBuildingShapeController(ScriptedImage(HttpStatusCode.OK, []), signedIn: true).GetImageAsync(1465, "REF")).StatusCode);
        }
    }
}
