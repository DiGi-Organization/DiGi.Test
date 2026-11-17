using DiGi.Harness.Classes;
using System.Net;
using System.Text.Json.Nodes;

namespace DiGi.Harness.xUnit
{
    public partial class Facts
    {
        private static JsonObject Swagger_Sample()
        {
            JsonObject? jsonObject = JsonNode.Parse(Fixture("OpenApi_Sample.json")) as JsonObject;
            Assert.NotNull(jsonObject);
            return jsonObject!;
        }

        /// <summary>
        /// Tests <see cref="Create.OpenApiIndex(JsonObject?, string?, IEnumerable{string}?)"/>: one line per GET/POST operation under the prefix, first-sentence summaries, required markers, resolved parameter references, array types and body schema names; DELETE and paths outside the prefix are left out.
        /// </summary>
        [Fact]
        public void OpenApiIndex()
        {
            string? index = Swagger_Sample().OpenApiIndex("/gis/");
            Assert.NotNull(index);

            string[] lines = index!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(5, lines.Length);
            Assert.Contains("GET /gis/county/id — Finds the id of a county by name. (name*: string)", lines);
            Assert.Contains("GET /gis/county/count — Counts the counties.", lines);
            Assert.Contains("GET /gis/building/{reference} — Returns one building. (reference*: string)", lines);
            Assert.Contains("POST /gis/building/itemsbyreferences — Returns the buildings with the given references. (countyids: integer[]) body: Reference[]", lines);
            Assert.DoesNotContain(lines, x => x.StartsWith("DELETE", StringComparison.Ordinal) || x.Contains("/user/me", StringComparison.Ordinal));
        }

        /// <summary>
        /// Tests <see cref="Query.PathTemplate(JsonObject?, string?, string?)"/> and <see cref="Query.Operation(JsonObject?, string?, string?)"/>: case-insensitive literal segments, template segments, the method check, and the rejection of absolute, protocol-relative, dot-dot, percent-encoded, query and fragment paths.
        /// </summary>
        [Fact]
        public void PathTemplate()
        {
            JsonObject swagger = Swagger_Sample();

            Assert.Equal("/gis/county/id", swagger.PathTemplate("get", "/GIS/County/Id"));
            Assert.Equal("/gis/building/{reference}", swagger.PathTemplate("GET", "/gis/building/abc-123"));
            Assert.Null(swagger.PathTemplate("POST", "/gis/county/id"));
            Assert.Null(swagger.PathTemplate("GET", "/gis/building/"));
            Assert.Null(swagger.PathTemplate("GET", "https://evil.example/gis/county/id"));
            Assert.Null(swagger.PathTemplate("GET", "//evil.example/gis/county/id"));
            Assert.Null(swagger.PathTemplate("GET", "/gis/building/../../user/me"));
            Assert.Null(swagger.PathTemplate("GET", "/gis/building/%2e%2e"));
            Assert.Null(swagger.PathTemplate("GET", "/gis/county/id?name=x"));
            Assert.Null(swagger.PathTemplate("GET", "/gis/county/id#x"));
            Assert.Null(swagger.PathTemplate("GET", "gis/county/id"));

            JsonObject? operation = swagger.Operation("GET", "/gis/building/xyz");
            Assert.NotNull(operation);
            Assert.Equal("/gis/building/{reference}", operation!["path"]?.GetValue<string>());
            Assert.Equal("reference", operation["parameters"]?[0]?["name"]?.GetValue<string>());
            Assert.Equal("string", operation["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["properties"]?["Reference"]?["properties"]?["Value"]?["type"]?.GetValue<string>());
            Assert.Null(operation["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["properties"]?["Storeys"]?["example"]);
        }

        /// <summary>
        /// Tests <see cref="CallEndpointTool"/>: a GET with encoded query values and the bearer token, an allowlisted POST with its body, and the refusals (path outside the prefix, unknown path, POST not on the allowlist, DELETE, injection attempts) that send nothing.
        /// </summary>
        [Fact]
        public async Task CallEndpointTool()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, "1261", "application/json");
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, "[]", "application/json");
            stubHttpMessageHandler.Enqueue(HttpStatusCode.NotFound, "", "application/json");
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, "binary", "image/jpeg");
            using HttpClient httpClient = new(stubHttpMessageHandler);

            Harness.Classes.CallEndpointTool callEndpointTool = new(new Uri("https://api.example.test"), "/gis/", Swagger_Sample(), ["/gis/building/itemsbyreferences"], "user-token", httpClient);
            Assert.Equal(Constants.ToolName.CallEndpoint, callEndpointTool.ToolDefinition.Name);

            ToolResultContent toolResultContent_Get = await callEndpointTool.InvokeAsync(new ToolUseContent("1", "call_endpoint", """{"method":"get","path":"/gis/county/id","query":{"name":"Kraków & Co"}}"""));
            Assert.False(toolResultContent_Get.IsError);
            Assert.Equal("HTTP 200 OK\n1261", toolResultContent_Get.Text);
            Assert.Equal("https://api.example.test/gis/county/id?name=Krak%C3%B3w%20%26%20Co", stubHttpMessageHandler.Requests[0].Uri?.AbsoluteUri);
            Assert.Equal("Bearer user-token", stubHttpMessageHandler.Requests[0].Authorization);

            ToolResultContent toolResultContent_Post = await callEndpointTool.InvokeAsync(new ToolUseContent("2", "call_endpoint", """{"method":"POST","path":"/gis/building/itemsbyreferences","query":{"countyids":[1,2]},"body":[{"Value":"a"}]}"""));
            Assert.False(toolResultContent_Post.IsError);
            Assert.Equal(HttpMethod.Post, stubHttpMessageHandler.Requests[1].Method);
            Assert.Equal("?countyids=1&countyids=2", stubHttpMessageHandler.Requests[1].Uri?.Query);
            Assert.Equal("""[{"Value":"a"}]""", stubHttpMessageHandler.Requests[1].Body);

            ToolResultContent toolResultContent_NotFound = await callEndpointTool.InvokeAsync(new ToolUseContent("3", "call_endpoint", """{"method":"GET","path":"/gis/building/b1"}"""));
            Assert.True(toolResultContent_NotFound.IsError);
            Assert.StartsWith("HTTP 404", toolResultContent_NotFound.Text);

            ToolResultContent toolResultContent_Binary = await callEndpointTool.InvokeAsync(new ToolUseContent("4", "call_endpoint", """{"method":"GET","path":"/gis/county/count"}"""));
            Assert.Contains("image/jpeg content of 6 bytes", toolResultContent_Binary.Text);

            string[] inputs_Refused =
            [
                """{"method":"GET","path":"/user/me"}""",
                """{"method":"GET","path":"/gis/unknown"}""",
                """{"method":"POST","path":"/gis/building/delete"}""",
                """{"method":"DELETE","path":"/gis/building/delete"}""",
                """{"method":"GET","path":"https://evil.example/gis/county/id"}""",
                """{"method":"GET","path":"//evil.example/gis/county/id"}""",
                """{"method":"GET","path":"/gis/building/../../user/me"}""",
                """{"method":"GET","path":"/gis/county/id?x=1"}""",
            ];

            foreach (string input in inputs_Refused)
            {
                ToolResultContent toolResultContent = await callEndpointTool.InvokeAsync(new ToolUseContent("r", "call_endpoint", input));
                Assert.True(toolResultContent.IsError, input);
            }

            Assert.Equal(4, stubHttpMessageHandler.Requests.Count);

            DescribeEndpointTool describeEndpointTool = new(Swagger_Sample(), "/gis/");
            ToolResultContent toolResultContent_Describe = await describeEndpointTool.InvokeAsync(new ToolUseContent("5", "describe_endpoint", """{"method":"GET","path":"/gis/county/id"}"""));
            Assert.False(toolResultContent_Describe.IsError);
            Assert.Contains("\"name\":\"name\"", toolResultContent_Describe.Text);

            ToolResultContent toolResultContent_DescribeOutside = await describeEndpointTool.InvokeAsync(new ToolUseContent("6", "describe_endpoint", """{"method":"GET","path":"/user/me"}"""));
            Assert.True(toolResultContent_DescribeOutside.IsError);
        }
    }
}
