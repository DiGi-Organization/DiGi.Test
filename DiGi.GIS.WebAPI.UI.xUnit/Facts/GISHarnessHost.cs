using DiGi.Harness.Classes;
using DiGi.Harness.Interfaces;
using DiGi.Harness.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        private const string Swagger_GISHarness = """
            {
              "openapi": "3.0.4",
              "paths": {
                "/gis/administrativeareal2d/referencesbyname": {
                  "get": { "summary": "Finds administrative areas by name.", "parameters": [ { "name": "name", "in": "query", "required": true, "schema": { "type": "string" } } ] }
                },
                "/gis/buildingdata/updateitemsbycountyids": {
                  "post": { "summary": "Updates building data." }
                },
                "/user/me": {
                  "get": { "summary": "The user." }
                }
              }
            }
            """;

        private static DefaultHttpContext HttpContext_Cookie(string? token)
        {
            DefaultHttpContext defaultHttpContext = new();
            if (token is not null)
            {
                defaultHttpContext.Request.Headers.Cookie = $"{Constants.Default.UserTokenCookieName}={token}";
            }

            return defaultHttpContext;
        }

        /// <summary>
        /// Tests <see cref="Classes.GISHarnessHost.UserAsync(HttpContext, System.Threading.CancellationToken)"/>: no cookie asks nothing and answers null; a token the user service refuses answers null; an accepted token answers the session email as the user, with the token as the tools' credential, asked of <see cref="Constants.Default.UserSessionUri"/> with that bearer token.
        /// </summary>
        [Fact]
        public async Task GISHarnessHost_UserAsync()
        {
            ScriptedWebApi scriptedWebApi = new((httpRequestMessage, body) => httpRequestMessage.Headers.Authorization?.Parameter == "good-token"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"email":"anna@example.test","jti":"x"}""", Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.Unauthorized));

            using Classes.GISHarnessHost gISHarnessHost = new(new ScriptedHttpClientFactory(scriptedWebApi), TimeProvider.System);

            Assert.Null(await gISHarnessHost.UserAsync(HttpContext_Cookie(null)));
            Assert.Empty(scriptedWebApi.Requests);

            Assert.Null(await gISHarnessHost.UserAsync(HttpContext_Cookie("bad-token")));

            HarnessUser? harnessUser = await gISHarnessHost.UserAsync(HttpContext_Cookie("good-token"));
            Assert.NotNull(harnessUser);
            Assert.Equal("anna@example.test", harnessUser!.Id);
            Assert.Equal("good-token", harnessUser.Credential);
            Assert.Equal(Constants.Default.UserSessionUri, scriptedWebApi.Requests[^1].RequestUri?.ToString());
            Assert.Equal("Bearer good-token", scriptedWebApi.Authorizations[^1]);
        }

        /// <summary>
        /// Tests <see cref="Classes.GISHarnessHost.ToolsAsync(HttpContext, HarnessUser, System.Threading.CancellationToken)"/> and <see cref="Classes.GISHarnessHost.SystemPromptAsync(System.Threading.CancellationToken)"/>: the swagger is fetched once from <see cref="Constants.Default.GISSwaggerUri"/>; the index lists the GIS GET endpoints but neither a POST missing from the allowlist nor a non-GIS path; <c>call_endpoint</c> calls the GIS Web API with the user's token and refuses the excluded endpoints without sending anything.
        /// </summary>
        [Fact]
        public async Task GISHarnessHost_ToolsAsync()
        {
            ScriptedWebApi scriptedWebApi = new((httpRequestMessage, body) => httpRequestMessage.RequestUri?.AbsolutePath == "/swagger/gis/swagger.json"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Swagger_GISHarness, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") });

            using Classes.GISHarnessHost gISHarnessHost = new(new ScriptedHttpClientFactory(scriptedWebApi), TimeProvider.System);

            string? systemPrompt = await gISHarnessHost.SystemPromptAsync();
            Assert.NotNull(systemPrompt);
            Assert.Contains("GET /gis/administrativeareal2d/referencesbyname — Finds administrative areas by name. (name*: string)", systemPrompt);
            Assert.DoesNotContain("updateitemsbycountyids", systemPrompt);
            Assert.DoesNotContain("/user/me", systemPrompt);

            IReadOnlyList<ITool> tools = await gISHarnessHost.ToolsAsync(new DefaultHttpContext(), new HarnessUser("anna@example.test", null, "user-token"));
            Assert.Equal(2, tools.Count);
            Assert.Equal(1, scriptedWebApi.Requests.Count(x => x.RequestUri?.AbsolutePath == "/swagger/gis/swagger.json"));

            ITool tool_Call = tools.Single(x => x.ToolDefinition.Name == Harness.Constants.ToolName.CallEndpoint);

            ToolResultContent toolResultContent = await tool_Call.InvokeAsync(new ToolUseContent("1", "call_endpoint", """{"method":"GET","path":"/gis/administrativeareal2d/referencesbyname","query":{"name":"Kraków"}}"""));
            Assert.False(toolResultContent.IsError);
            Assert.Equal($"{Constants.Default.GISWebAPIUri}/gis/administrativeareal2d/referencesbyname?name=Krak%C3%B3w", scriptedWebApi.Requests[^1].RequestUri?.AbsoluteUri);
            Assert.Equal("Bearer user-token", scriptedWebApi.Authorizations[^1]);

            int count = scriptedWebApi.Requests.Count;
            Assert.True((await tool_Call.InvokeAsync(new ToolUseContent("2", "call_endpoint", """{"method":"POST","path":"/gis/buildingdata/updateitemsbycountyids","body":[]}"""))).IsError);
            Assert.True((await tool_Call.InvokeAsync(new ToolUseContent("3", "call_endpoint", """{"method":"GET","path":"/user/me"}"""))).IsError);
            Assert.Equal(count, scriptedWebApi.Requests.Count);
        }
    }
}
