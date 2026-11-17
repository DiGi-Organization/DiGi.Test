using DiGi.Harness.Classes;
using DiGi.Harness.Enums;
using DiGi.Harness.Interfaces;
using System.Net;
using System.Reflection;
using System.Text;

namespace DiGi.Harness.xUnit
{
    public partial class Facts
    {
        private static string Fixture(string fileName)
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), Path.Combine("Harness", fileName));
            Assert.True(File.Exists(path), path);
            return File.ReadAllText(path!);
        }

        /// <summary>
        /// Tests <see cref="Modify.ReadServerSentEventsAsync(Stream?, Func{ServerSentEvent, CancellationToken, Task}?, CancellationToken)"/>: multi-line data, comments, event names, CRLF line ends, and a last event without a closing blank line.
        /// </summary>
        [Fact]
        public async Task ReadServerSentEventsAsync()
        {
            string text = ": comment\r\nevent: first\r\ndata: line 1\r\ndata: line 2\r\n\r\ndata:{\"a\":1}\n\nevent: empty\n\ndata: last";
            using MemoryStream memoryStream = new(Encoding.UTF8.GetBytes(text));

            List<ServerSentEvent> serverSentEvents = [];
            int count = await memoryStream.ReadServerSentEventsAsync((serverSentEvent, cancellationToken) =>
            {
                serverSentEvents.Add(serverSentEvent);
                return Task.CompletedTask;
            });

            Assert.Equal(3, count);
            Assert.Equal("first", serverSentEvents[0].Name);
            Assert.Equal("line 1\nline 2", serverSentEvents[0].Data);
            Assert.Null(serverSentEvents[1].Name);
            Assert.Equal("{\"a\":1}", serverSentEvents[1].Data);
            Assert.Equal("last", serverSentEvents[2].Data);
        }

        /// <summary>
        /// Tests <see cref="Modify.CompleteAsync(HttpClient?, ProviderConfiguration?, System.Text.Json.Nodes.JsonObject?, Func{HarnessEvent, CancellationToken, Task}?, System.Diagnostics.Stopwatch?, CancellationToken)"/> on a recorded Anthropic stream: thinking with its joined signature, text, a tool call whose input arrived in pieces, usage with cache reads, the stop reason, the emitted events, and the request headers and URI.
        /// </summary>
        [Fact]
        public async Task CompleteAsync_Anthropic()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            ProviderConfiguration providerConfiguration = ProviderConfiguration_Anthropic();
            Classes.Conversation conversation = new("anthropic", [new Message(Role.User, [new TextContent("Question")])]);

            List<HarnessEvent> harnessEvents = [];
            CompletionResult completionResult = await httpClient.CompleteAsync(providerConfiguration, conversation.ToAnthropic_JsonObject(providerConfiguration, null, null), (harnessEvent, cancellationToken) =>
            {
                harnessEvents.Add(harnessEvent);
                return Task.CompletedTask;
            }, null);

            Assert.Equal(ErrorCategory.Undefined, completionResult.ErrorCategory);
            Assert.Equal(StopReason.ToolUse, completionResult.StopReason);
            Assert.Equal(1200, completionResult.Usage?.InputTokens);
            Assert.Equal(8000, completionResult.Usage?.CacheReadInputTokens);
            Assert.Equal(95, completionResult.Usage?.OutputTokens);

            IReadOnlyList<IContent>? contents = completionResult.Message?.Contents;
            Assert.Equal(3, contents?.Count);
            ThinkingContent thinkingContent = Assert.IsType<ThinkingContent>(contents![0]);
            Assert.Equal("**Finding the county.** The user asks about buildings in Kraków, so I need its county id first.", thinkingContent.Text);
            Assert.Equal("EqQBCkYIARgCKkBsignature-part-2", thinkingContent.Signature);
            Assert.Equal("Looking up the county.", Assert.IsType<TextContent>(contents[1]).Text);
            ToolUseContent toolUseContent = Assert.IsType<ToolUseContent>(contents[2]);
            Assert.Equal("toolu_01", toolUseContent.Id);
            Assert.Equal("call_endpoint", toolUseContent.Name);
            Assert.Equal("Kraków", toolUseContent.Input()?["query"]?["name"]?.GetValue<string>());

            Assert.Equal(2, harnessEvents.OfType<ThinkingDeltaEvent>().Count());
            Assert.Single(harnessEvents.OfType<ThinkingCompletedEvent>());
            Assert.Equal("Looking up the county.", string.Concat(harnessEvents.OfType<TextDeltaEvent>().Select(x => x.Text)));

            Assert.Equal("https://api.anthropic.com/v1/messages", stubHttpMessageHandler.Requests[0].Uri?.ToString());
            Assert.Equal("sk-test", stubHttpMessageHandler.Headers[0]["x-api-key"]);
            Assert.Equal(Constants.Anthropic.Version, stubHttpMessageHandler.Headers[0]["anthropic-version"]);
            Assert.Equal(Constants.Anthropic.FallbackBeta, stubHttpMessageHandler.Headers[0]["anthropic-beta"]);
        }

        /// <summary>
        /// Tests <see cref="Modify.CompleteAsync(HttpClient?, ProviderConfiguration?, System.Text.Json.Nodes.JsonObject?, Func{HarnessEvent, CancellationToken, Task}?, System.Diagnostics.Stopwatch?, CancellationToken)"/> on a recorded OpenAI-protocol stream: reasoning, two indexed tool calls assembled from fragments (one without an id gets a generated one), usage with cached tokens, and a request without an authorization header for a key-less local server.
        /// </summary>
        [Fact]
        public async Task CompleteAsync_OpenAI()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("OpenAI_ToolCalls.sse"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            ProviderConfiguration providerConfiguration = ProviderConfiguration_Local();
            Classes.Conversation conversation = new("local", [new Message(Role.User, [new TextContent("Question")])]);

            List<HarnessEvent> harnessEvents = [];
            CompletionResult completionResult = await httpClient.CompleteAsync(providerConfiguration, conversation.ToOpenAI_JsonObject(providerConfiguration, null, null), (harnessEvent, cancellationToken) =>
            {
                harnessEvents.Add(harnessEvent);
                return Task.CompletedTask;
            }, null);

            Assert.Equal(StopReason.ToolUse, completionResult.StopReason);
            Assert.Equal(100, completionResult.Usage?.InputTokens);
            Assert.Equal(800, completionResult.Usage?.CacheReadInputTokens);
            Assert.Equal(60, completionResult.Usage?.OutputTokens);

            IReadOnlyList<IContent>? contents = completionResult.Message?.Contents;
            Assert.Equal(3, contents?.Count);
            Assert.Equal("The user wants two lookups.", Assert.IsType<ThinkingContent>(contents![0]).Text);
            ToolUseContent toolUseContent_1 = Assert.IsType<ToolUseContent>(contents[1]);
            Assert.Equal("call_a", toolUseContent_1.Id);
            Assert.Equal("describe_endpoint", toolUseContent_1.Name);
            Assert.Equal("/gis/county/id", toolUseContent_1.Input()?["path"]?.GetValue<string>());
            ToolUseContent toolUseContent_2 = Assert.IsType<ToolUseContent>(contents[2]);
            Assert.StartsWith("call_", toolUseContent_2.Id);
            Assert.Equal("call_endpoint", toolUseContent_2.Name);

            Assert.Single(harnessEvents.OfType<ThinkingCompletedEvent>());
            Assert.Equal("http://localhost:1234/v1/chat/completions", stubHttpMessageHandler.Requests[0].Uri?.ToString());
            Assert.Null(stubHttpMessageHandler.Requests[0].Authorization);
        }

        /// <summary>
        /// Tests the failure categories of <see cref="Modify.CompleteAsync(HttpClient?, ProviderConfiguration?, System.Text.Json.Nodes.JsonObject?, Func{HarnessEvent, CancellationToken, Task}?, System.Diagnostics.Stopwatch?, CancellationToken)"/>: no answer is <see cref="ErrorCategory.Unavailable"/>, a failure status or an error event is <see cref="ErrorCategory.UpstreamFault"/>, the key never appears in the message, and a requested cancellation is rethrown.
        /// </summary>
        [Fact]
        public async Task CompleteAsync_Failure()
        {
            ProviderConfiguration providerConfiguration = ProviderConfiguration_Anthropic();
            System.Text.Json.Nodes.JsonObject? body = new Classes.Conversation("anthropic", [new Message(Role.User, [new TextContent("Question")])]).ToAnthropic_JsonObject(providerConfiguration, null, null);

            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue((httpRequestMessage, cancellationToken) => throw new HttpRequestException("Connection refused"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.Unauthorized, """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}""", "application/json");
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, "event: error\ndata: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}\n\n");
            stubHttpMessageHandler.Enqueue((httpRequestMessage, cancellationToken) => throw new TaskCanceledException("timeout"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            CompletionResult completionResult_Unreachable = await httpClient.CompleteAsync(providerConfiguration, body, null, null);
            Assert.Equal(ErrorCategory.Unavailable, completionResult_Unreachable.ErrorCategory);

            CompletionResult completionResult_Status = await httpClient.CompleteAsync(providerConfiguration, body, null, null);
            Assert.Equal(ErrorCategory.UpstreamFault, completionResult_Status.ErrorCategory);
            Assert.Contains("401", completionResult_Status.ErrorMessage);
            Assert.Contains("invalid x-api-key", completionResult_Status.ErrorMessage);
            Assert.DoesNotContain("sk-test", completionResult_Status.ErrorMessage);

            CompletionResult completionResult_Event = await httpClient.CompleteAsync(providerConfiguration, body, null, null);
            Assert.Equal(ErrorCategory.UpstreamFault, completionResult_Event.ErrorCategory);
            Assert.Contains("overloaded_error", completionResult_Event.ErrorMessage);

            CompletionResult completionResult_Timeout = await httpClient.CompleteAsync(providerConfiguration, body, null, null);
            Assert.Equal(ErrorCategory.Unavailable, completionResult_Timeout.ErrorCategory);
            Assert.Contains("timeout", completionResult_Timeout.ErrorMessage);

            StubHttpMessageHandler stubHttpMessageHandler_Cancel = new();
            stubHttpMessageHandler_Cancel.Enqueue(async (httpRequestMessage, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
            using HttpClient httpClient_Cancel = new(stubHttpMessageHandler_Cancel);
            using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => httpClient_Cancel.CompleteAsync(providerConfiguration, body, null, null, cancellationTokenSource.Token));

            CompletionResult completionResult_Invalid = await httpClient.CompleteAsync(new ProviderConfiguration("x", null, Protocol.Undefined, null, null, null, null, 0, null, false, false, false, true), body, null, null);
            Assert.Equal(ErrorCategory.InvalidConfiguration, completionResult_Invalid.ErrorCategory);
        }
    }
}
