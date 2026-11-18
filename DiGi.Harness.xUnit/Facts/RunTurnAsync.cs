using DiGi.Harness.Classes;
using DiGi.Harness.Enums;
using DiGi.Harness.Interfaces;
using System.Net;

namespace DiGi.Harness.xUnit
{
    public partial class Facts
    {
        private const string InputSchema_Path = """{"type":"object","properties":{"method":{"type":"string"},"path":{"type":"string"}},"required":["method","path"]}""";

        /// <summary>
        /// Tests <see cref="Modify.RunTurnAsync(Classes.Conversation?, HttpClient?, ProviderConfiguration?, IEnumerable{ITool}?, RunOptions?, Func{HarnessEvent, CancellationToken, Task}?, CancellationToken)"/> on the Anthropic protocol: a tool call, its result returned in the next request, the final answer, the appended history, the summed usage and the event order.
        /// </summary>
        [Fact]
        public async Task RunTurnAsync_Anthropic()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_Answer.sse"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            RecordingTool recordingTool = new(Constants.ToolName.CallEndpoint, InputSchema_Path, (toolUseContent, cancellationToken) => Task.FromResult("HTTP 200 OK\n1261"));

            Classes.Conversation conversation = new("anthropic", null);
            conversation.Add(new Message(Role.User, [new TextContent("How many buildings are in Kraków?")]));

            List<HarnessEvent> harnessEvents = [];
            TurnResult turnResult = await conversation.RunTurnAsync(httpClient, ProviderConfiguration_Anthropic(), [recordingTool], new RunOptions { SystemPrompt = "Prompt" }, (harnessEvent, cancellationToken) =>
            {
                harnessEvents.Add(harnessEvent);
                return Task.CompletedTask;
            });

            Assert.Equal(ErrorCategory.Undefined, turnResult.ErrorCategory);
            Assert.Equal(StopReason.EndTurn, turnResult.StopReason);
            Assert.Equal(2, turnResult.Steps);
            Assert.Equal(1, turnResult.ToolCalls);
            Assert.Equal(1500, turnResult.Usage?.InputTokens);
            Assert.Equal(17200, turnResult.Usage?.CacheReadInputTokens);
            Assert.Equal(107, turnResult.Usage?.OutputTokens);

            Assert.Single(recordingTool.Calls);
            Assert.Equal(4, conversation.Messages.Count);
            Assert.Equal(Role.User, conversation.Messages[2].Role);
            Assert.Equal("toolu_01", Assert.IsType<ToolResultContent>(conversation.Messages[2].Contents![0]).ToolUseId);
            Assert.Equal("Kraków has 12 345 buildings.", Assert.IsType<TextContent>(conversation.Messages[3].Contents![0]).Text);

            Assert.Contains("\"tool_use_id\":\"toolu_01\"", stubHttpMessageHandler.Requests[1].Body);
            Assert.Contains("\"signature\":\"EqQBCkYIARgCKkBsignature-part-2\"", stubHttpMessageHandler.Requests[1].Body);

            Assert.IsType<StepStartedEvent>(harnessEvents[0]);
            Assert.IsType<TurnCompletedEvent>(harnessEvents[^1]);
            int index_Started = harnessEvents.FindIndex(x => x is ToolCallStartedEvent);
            int index_Completed = harnessEvents.FindIndex(x => x is ToolCallCompletedEvent);
            Assert.True(index_Started > 0 && index_Completed > index_Started);
            Assert.Equal(2, harnessEvents.OfType<StepStartedEvent>().Count());
            Assert.Equal(2, harnessEvents.OfType<UsageEvent>().Count());
            Assert.Empty(harnessEvents.OfType<ErrorEvent>());
            Assert.True(harnessEvents.Zip(harnessEvents.Skip(1)).All(x => x.First.ElapsedMilliseconds <= x.Second.ElapsedMilliseconds));
        }

        /// <summary>
        /// Tests <see cref="Modify.RunTurnAsync(Classes.Conversation?, HttpClient?, ProviderConfiguration?, IEnumerable{ITool}?, RunOptions?, Func{HarnessEvent, CancellationToken, Task}?, CancellationToken)"/> on the OpenAI protocol with two parallel tool calls: an unknown tool and a known one, both answered in one message, the known result truncated to the limit with a note.
        /// </summary>
        [Fact]
        public async Task RunTurnAsync_OpenAI()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("OpenAI_ToolCalls.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("OpenAI_Answer.sse"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            RecordingTool recordingTool = new(Constants.ToolName.CallEndpoint, InputSchema_Path, (toolUseContent, cancellationToken) => Task.FromResult(new string('x', 500)));

            Classes.Conversation conversation = new("local", [new Message(Role.User, [new TextContent("How many counties?")])]);

            List<HarnessEvent> harnessEvents = [];
            TurnResult turnResult = await conversation.RunTurnAsync(httpClient, ProviderConfiguration_Local(), [recordingTool], new RunOptions { MaxToolResultCharacters = 100 }, (harnessEvent, cancellationToken) =>
            {
                harnessEvents.Add(harnessEvent);
                return Task.CompletedTask;
            });

            Assert.Equal(ErrorCategory.Undefined, turnResult.ErrorCategory);
            Assert.Equal(2, turnResult.ToolCalls);

            IReadOnlyList<IContent> contents = conversation.Messages[2].Contents!;
            Assert.Equal(2, contents.Count);
            ToolResultContent toolResultContent_Unknown = Assert.IsType<ToolResultContent>(contents[0]);
            Assert.True(toolResultContent_Unknown.IsError);
            Assert.Contains("Unknown tool 'describe_endpoint'", toolResultContent_Unknown.Text);

            ToolResultContent toolResultContent_Known = Assert.IsType<ToolResultContent>(contents[1]);
            Assert.False(toolResultContent_Known.IsError);
            Assert.StartsWith(new string('x', 100) + "\n[Truncated: 100 of 500", toolResultContent_Known.Text);

            ToolCallCompletedEvent toolCallCompletedEvent = harnessEvents.OfType<ToolCallCompletedEvent>().Single(x => x.ToolName == Constants.ToolName.CallEndpoint);
            Assert.True(toolCallCompletedEvent.Truncated);
            Assert.Equal(500, toolCallCompletedEvent.Characters);

            Assert.Contains("\"role\":\"tool\"", stubHttpMessageHandler.Requests[1].Body);
            Assert.Contains("\"reasoning_content\":\"The user wants two lookups.\"", stubHttpMessageHandler.Requests[1].Body);
            Assert.Equal("There are 7 counties.", Assert.IsType<TextContent>(conversation.Messages[3].Contents![0]).Text);
        }

        /// <summary>
        /// Tests the stops of <see cref="Modify.RunTurnAsync(Classes.Conversation?, HttpClient?, ProviderConfiguration?, IEnumerable{ITool}?, RunOptions?, Func{HarnessEvent, CancellationToken, Task}?, CancellationToken)"/>: the iteration limit, whose tool-less final step fails at the provider, answers the last tool calls and ends with <see cref="ErrorCategory.MaxToolIterations"/>; invalid tool input is answered with an error without running the tool; a refusal ends with <see cref="ErrorCategory.Refusal"/>; an unreachable provider ends with <see cref="ErrorCategory.Unavailable"/>; and every stop ends with an <see cref="ErrorEvent"/> before <see cref="TurnCompletedEvent"/>.
        /// </summary>
        [Fact]
        public async Task RunTurnAsync_Stops()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            stubHttpMessageHandler.Enqueue((httpRequestMessage, cancellationToken) => throw new HttpRequestException("refused"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            RecordingTool recordingTool = new(Constants.ToolName.CallEndpoint, """{"type":"object","properties":{"method":{"type":"string"},"path":{"type":"string"},"county":{"type":"string"}},"required":["method","path","county"]}""", (toolUseContent, cancellationToken) => Task.FromResult("never"));

            Classes.Conversation conversation = new("anthropic", [new Message(Role.User, [new TextContent("Loop")])]);
            List<HarnessEvent> harnessEvents = [];
            TurnResult turnResult = await conversation.RunTurnAsync(httpClient, ProviderConfiguration_Anthropic(), [recordingTool], new RunOptions { MaxToolIterations = 2 }, (harnessEvent, cancellationToken) =>
            {
                harnessEvents.Add(harnessEvent);
                return Task.CompletedTask;
            });

            // The tool-less final step after the limit fails here (provider unreachable), so the limit stays the reported error.
            Assert.Equal(ErrorCategory.MaxToolIterations, turnResult.ErrorCategory);
            Assert.Equal(3, turnResult.Steps);
            Assert.Equal(3, stubHttpMessageHandler.Requests.Count);
            Assert.Empty(recordingTool.Calls);
            Assert.Contains("lacks required properties: county", Assert.IsType<ToolResultContent>(conversation.Messages[^1].Contents![0]).Text);
            Assert.Equal(Role.User, conversation.Messages[^1].Role);
            Assert.IsType<ErrorEvent>(harnessEvents[^2]);
            Assert.IsType<TurnCompletedEvent>(harnessEvents[^1]);

            StubHttpMessageHandler stubHttpMessageHandler_Refusal = new();
            stubHttpMessageHandler_Refusal.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_Refusal.sse"));
            using HttpClient httpClient_Refusal = new(stubHttpMessageHandler_Refusal);
            Classes.Conversation conversation_Refusal = new("anthropic", [new Message(Role.User, [new TextContent("Refused")])]);
            TurnResult turnResult_Refusal = await conversation_Refusal.RunTurnAsync(httpClient_Refusal, ProviderConfiguration_Anthropic(), null, null, null);
            Assert.Equal(ErrorCategory.Refusal, turnResult_Refusal.ErrorCategory);
            Assert.Contains("Declined by policy.", turnResult_Refusal.ErrorMessage);
            Assert.Single(conversation_Refusal.Messages);

            StubHttpMessageHandler stubHttpMessageHandler_Unreachable = new();
            stubHttpMessageHandler_Unreachable.Enqueue((httpRequestMessage, cancellationToken) => throw new HttpRequestException("refused"));
            using HttpClient httpClient_Unreachable = new(stubHttpMessageHandler_Unreachable);
            Classes.Conversation conversation_Unreachable = new("local", [new Message(Role.User, [new TextContent("Hello")])]);
            TurnResult turnResult_Unreachable = await conversation_Unreachable.RunTurnAsync(httpClient_Unreachable, ProviderConfiguration_Local(), null, null, null);
            Assert.Equal(ErrorCategory.Unavailable, turnResult_Unreachable.ErrorCategory);
            Assert.Single(conversation_Unreachable.Messages);

            TurnResult turnResult_Invalid = await conversation_Unreachable.RunTurnAsync(httpClient_Unreachable, null, null, null, null);
            Assert.Equal(ErrorCategory.InvalidConfiguration, turnResult_Invalid.ErrorCategory);
        }

        /// <summary>
        /// Tests the final step of <see cref="Modify.RunTurnAsync(Classes.Conversation?, HttpClient?, ProviderConfiguration?, IEnumerable{ITool}?, RunOptions?, Func{HarnessEvent, CancellationToken, Task}?, CancellationToken)"/> on the Anthropic protocol (DiGi.Harness#6): a model still calling tools at the iteration limit gets one more request with the tools offered but <c>tool_choice: none</c>, and its text answer ends the turn without an error.
        /// </summary>
        [Fact]
        public async Task RunTurnAsync_MaxToolIterations_Anthropic()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_Answer.sse"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            RecordingTool recordingTool = new(Constants.ToolName.CallEndpoint, InputSchema_Path, (toolUseContent, cancellationToken) => Task.FromResult("HTTP 200 OK\n1261"));

            Classes.Conversation conversation = new("anthropic", [new Message(Role.User, [new TextContent("How many buildings are in Kraków?")])]);
            List<HarnessEvent> harnessEvents = [];
            TurnResult turnResult = await conversation.RunTurnAsync(httpClient, ProviderConfiguration_Anthropic(), [recordingTool], new RunOptions { SystemPrompt = "Prompt", MaxToolIterations = 2 }, (harnessEvent, cancellationToken) =>
            {
                harnessEvents.Add(harnessEvent);
                return Task.CompletedTask;
            });

            Assert.Equal(ErrorCategory.Undefined, turnResult.ErrorCategory);
            Assert.Null(turnResult.ErrorMessage);
            Assert.Equal(StopReason.EndTurn, turnResult.StopReason);
            Assert.Equal(3, turnResult.Steps);
            Assert.Equal(2, turnResult.ToolCalls);
            Assert.Equal(2, recordingTool.Calls.Count);

            Assert.Equal(6, conversation.Messages.Count);
            Assert.Equal(Role.Assistant, conversation.Messages[^1].Role);
            Assert.Equal("Kraków has 12 345 buildings.", Assert.IsType<TextContent>(conversation.Messages[^1].Contents![0]).Text);

            Assert.Equal(3, stubHttpMessageHandler.Requests.Count);
            Assert.Contains("\"tool_choice\":{\"type\":\"auto\"}", stubHttpMessageHandler.Requests[1].Body);
            Assert.Contains("\"tools\":[", stubHttpMessageHandler.Requests[2].Body);
            Assert.Contains("\"tool_choice\":{\"type\":\"none\"}", stubHttpMessageHandler.Requests[2].Body);
            Assert.Contains(System.Text.Json.JsonSerializer.Serialize(Constants.Default.FinalStepPrompt)[1..^1], stubHttpMessageHandler.Requests[2].Body);
            Assert.DoesNotContain(System.Text.Json.JsonSerializer.Serialize(Constants.Default.FinalStepPrompt)[1..^1], stubHttpMessageHandler.Requests[1].Body);

            Assert.Equal(3, harnessEvents.OfType<StepStartedEvent>().Count());
            Assert.Empty(harnessEvents.OfType<ErrorEvent>());
            Assert.IsType<TurnCompletedEvent>(harnessEvents[^1]);
        }

        /// <summary>
        /// Tests the final step of <see cref="Modify.RunTurnAsync(Classes.Conversation?, HttpClient?, ProviderConfiguration?, IEnumerable{ITool}?, RunOptions?, Func{HarnessEvent, CancellationToken, Task}?, CancellationToken)"/> on the OpenAI protocol (DiGi.Harness#6): the request after the iteration limit sends <c>tool_choice: "none"</c> and the text answer ends the turn without an error.
        /// </summary>
        [Fact]
        public async Task RunTurnAsync_MaxToolIterations_OpenAI()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("OpenAI_ToolCalls.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("OpenAI_ToolCalls.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("OpenAI_Answer.sse"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            RecordingTool recordingTool = new(Constants.ToolName.CallEndpoint, InputSchema_Path, (toolUseContent, cancellationToken) => Task.FromResult("7"));

            Classes.Conversation conversation = new("local", [new Message(Role.User, [new TextContent("How many counties?")])]);
            TurnResult turnResult = await conversation.RunTurnAsync(httpClient, ProviderConfiguration_Local(), [recordingTool], new RunOptions { SystemPrompt = "Prompt", MaxToolIterations = 2 }, null);

            Assert.Equal(ErrorCategory.Undefined, turnResult.ErrorCategory);
            Assert.Equal(3, turnResult.Steps);
            Assert.Equal(4, turnResult.ToolCalls);

            Assert.Equal(Role.Assistant, conversation.Messages[^1].Role);
            Assert.Equal("There are 7 counties.", Assert.IsType<TextContent>(conversation.Messages[^1].Contents![0]).Text);

            Assert.Equal(3, stubHttpMessageHandler.Requests.Count);
            Assert.Contains("\"tool_choice\":\"auto\"", stubHttpMessageHandler.Requests[1].Body);
            Assert.Contains("\"tools\":[", stubHttpMessageHandler.Requests[2].Body);
            Assert.Contains("\"tool_choice\":\"none\"", stubHttpMessageHandler.Requests[2].Body);
            Assert.Contains(System.Text.Json.JsonSerializer.Serialize(Constants.Default.FinalStepPrompt)[1..^1], stubHttpMessageHandler.Requests[2].Body);
        }

        /// <summary>
        /// Tests the final step of <see cref="Modify.RunTurnAsync(Classes.Conversation?, HttpClient?, ProviderConfiguration?, IEnumerable{ITool}?, RunOptions?, Func{HarnessEvent, CancellationToken, Task}?, CancellationToken)"/> when the model ignores <c>tool_choice: none</c> and calls a tool anyway (DiGi.Harness#6): the call is answered with an error result without running it, and the turn ends with <see cref="ErrorCategory.MaxToolIterations"/>.
        /// </summary>
        [Fact]
        public async Task RunTurnAsync_MaxToolIterations_ToolCallIgnored()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            RecordingTool recordingTool = new(Constants.ToolName.CallEndpoint, InputSchema_Path, (toolUseContent, cancellationToken) => Task.FromResult("HTTP 200 OK\n1261"));

            Classes.Conversation conversation = new("anthropic", [new Message(Role.User, [new TextContent("Loop")])]);
            List<HarnessEvent> harnessEvents = [];
            TurnResult turnResult = await conversation.RunTurnAsync(httpClient, ProviderConfiguration_Anthropic(), [recordingTool], new RunOptions { MaxToolIterations = 2 }, (harnessEvent, cancellationToken) =>
            {
                harnessEvents.Add(harnessEvent);
                return Task.CompletedTask;
            });

            Assert.Equal(ErrorCategory.MaxToolIterations, turnResult.ErrorCategory);
            Assert.Equal(3, turnResult.Steps);
            Assert.Equal(2, recordingTool.Calls.Count);
            Assert.Equal(3, stubHttpMessageHandler.Requests.Count);

            Assert.Equal(Role.User, conversation.Messages[^1].Role);
            ToolResultContent toolResultContent = Assert.IsType<ToolResultContent>(conversation.Messages[^1].Contents![0]);
            Assert.True(toolResultContent.IsError);
            Assert.StartsWith("Not run: ", toolResultContent.Text);
            Assert.IsType<ErrorEvent>(harnessEvents[^2]);
            Assert.IsType<TurnCompletedEvent>(harnessEvents[^1]);
        }

        /// <summary>
        /// Tests that cancelling <see cref="Modify.RunTurnAsync(Classes.Conversation?, HttpClient?, ProviderConfiguration?, IEnumerable{ITool}?, RunOptions?, Func{HarnessEvent, CancellationToken, Task}?, CancellationToken)"/> while a tool runs rethrows the cancellation and still answers the pending tool call, so the conversation never ends with an unanswered call.
        /// </summary>
        [Fact]
        public async Task RunTurnAsync_Cancel()
        {
            StubHttpMessageHandler stubHttpMessageHandler = new();
            stubHttpMessageHandler.Enqueue(HttpStatusCode.OK, Fixture("Anthropic_ToolUse.sse"));
            using HttpClient httpClient = new(stubHttpMessageHandler);

            using CancellationTokenSource cancellationTokenSource = new();
            RecordingTool recordingTool = new(Constants.ToolName.CallEndpoint, InputSchema_Path, async (toolUseContent, cancellationToken) =>
            {
                await cancellationTokenSource.CancelAsync();
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return "never";
            });

            Classes.Conversation conversation = new("anthropic", [new Message(Role.User, [new TextContent("Question")])]);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => conversation.RunTurnAsync(httpClient, ProviderConfiguration_Anthropic(), [recordingTool], null, null, cancellationTokenSource.Token));

            Assert.Equal(3, conversation.Messages.Count);
            ToolResultContent toolResultContent = Assert.IsType<ToolResultContent>(conversation.Messages[2].Contents![0]);
            Assert.True(toolResultContent.IsError);
            Assert.Equal("toolu_01", toolResultContent.ToolUseId);
        }
    }
}
