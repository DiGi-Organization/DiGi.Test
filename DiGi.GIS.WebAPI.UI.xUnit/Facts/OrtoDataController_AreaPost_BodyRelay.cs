using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the two area POST relays (issue #84): the posted document reaches the sibling service byte-identically - nothing re-serialized, <c>state</c> still the integer code the page sent - with the session presented as a bearer token and the method, path and upstream status mirrored, while an empty body is refused with 400 before a round trip is spent on it.
        /// <para>The label leg posts irregular whitespace instead of the compact document: only a relay that forwards the bytes it arrived as can keep the leading space, the space after each colon and the trailing space, so any future re-serialization fails the comparison.</para>
        /// </summary>
        [Fact]
        public async Task OrtoDataController_AreaPost_BodyRelay()
        {
            // The object upsert: byte-identical body, bearer token, upstream 400 mirrored as 400.
            ScriptedWebApi scriptedWebApi_Object = new((httpRequestMessage, body) => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(string.Empty) });
            IActionResult result_Object = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Object, scriptedWebApi_Object, true, OrtoAreaPostBody);
            Assert.Equal(400, Assert.IsType<StatusCodeResult>(result_Object).StatusCode);

            (HttpMethod Method, Uri? RequestUri, string? Body) request_Object = Assert.Single(scriptedWebApi_Object.Requests);
            Assert.Equal(HttpMethod.Post, request_Object.Method);
            Assert.Equal("/gis/ortoarea/objects", request_Object.RequestUri!.AbsolutePath);
            Assert.Equal(OrtoAreaPostBody, request_Object.Body);
            Assert.Contains("\"state\":0", request_Object.Body!);
            Assert.Equal("Bearer test-token", Assert.Single(scriptedWebApi_Object.Authorizations));

            // The label upsert: the same byte-identity under irregular whitespace, and an upstream 404 mirrored as 404.
            const string labelBody = " {\"x_1\": 1,\"state\": 0} ";
            ScriptedWebApi scriptedWebApi_Label = new((httpRequestMessage, body) => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) });
            IActionResult result_Label = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Label, scriptedWebApi_Label, true, labelBody);
            Assert.Equal(404, Assert.IsType<StatusCodeResult>(result_Label).StatusCode);

            (HttpMethod Method, Uri? RequestUri, string? Body) request_Label = Assert.Single(scriptedWebApi_Label.Requests);
            Assert.Equal("/gis/ortoarea/label", request_Label.RequestUri!.AbsolutePath);
            Assert.Equal(labelBody, request_Label.Body);

            // An empty body: 400, before any request is made.
            ScriptedWebApi scriptedWebApi_Empty = new((httpRequestMessage, body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });
            IActionResult result_Empty = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Object, scriptedWebApi_Empty, true, string.Empty);
            Assert.IsType<BadRequestResult>(result_Empty);
            Assert.Empty(scriptedWebApi_Empty.Requests);
        }
    }
}
