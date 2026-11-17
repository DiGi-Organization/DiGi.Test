using DiGi.Harness.Classes;
using DiGi.Harness.Enums;
using DiGi.Harness.Interfaces;

namespace DiGi.Harness.xUnit
{
    public partial class Facts
    {
        private static Classes.Conversation Conversation_Sample()
        {
            Classes.Conversation conversation = new("anthropic", null);
            conversation.Add(new Message(Role.User, [new TextContent("How many buildings are in Kraków?")]));
            conversation.Add(new Message(Role.Assistant,
            [
                new ThinkingContent("**Finding the county.** I need its id.", "EqQBsignature", null),
                new ThinkingContent(null, null, "redacted-opaque-data"),
                new TextContent("Looking up the county."),
                new ToolUseContent("toolu_01", "call_endpoint", """{"method":"GET","path":"/gis/county/id","query":{"name":"Kraków"}}"""),
            ]));
            conversation.Add(new Message(Role.User, [new ToolResultContent("toolu_01", "HTTP 200 OK\n1261", false)]));
            conversation.Add(new Message(Role.Assistant, [new TextContent("Kraków has 12 345 buildings.")]));
            return conversation;
        }

        /// <summary>
        /// Tests the <see cref="Classes.Conversation"/>, <see cref="Message"/> and content block constructors, the polymorphic JSON round trip of the content list, and the copy constructors.
        /// </summary>
        [Fact]
        public void Conversation()
        {
            Classes.Conversation conversation = Conversation_Sample();
            Assert.Equal("anthropic", conversation.ProviderName);
            Assert.Equal(4, conversation.Messages.Count);

            Core.xUnit.Query.SerializationCheck(conversation);

            string? json = Core.Convert.ToSystem_String(conversation);
            Classes.Conversation? conversation_Json = Core.Convert.ToDiGi<Classes.Conversation>(json)?.FirstOrDefault();
            Assert.NotNull(conversation_Json);
            IReadOnlyList<IContent>? contents = conversation_Json!.Messages[1].Contents;
            Assert.NotNull(contents);
            Assert.IsType<ThinkingContent>(contents![0]);
            Assert.Equal("EqQBsignature", ((ThinkingContent)contents[0]).Signature);
            Assert.Equal("redacted-opaque-data", ((ThinkingContent)contents[1]).RedactedData);
            Assert.IsType<TextContent>(contents[2]);
            Assert.Equal("""{"method":"GET","path":"/gis/county/id","query":{"name":"Kraków"}}""", ((ToolUseContent)contents[3]).InputJson);
            Assert.False(((ToolResultContent)conversation_Json.Messages[2].Contents![0]).IsError);

            Classes.Conversation conversation_Copy = new(conversation);
            Assert.Equal(conversation.ProviderName, conversation_Copy.ProviderName);
            Assert.Equal(conversation.Messages.Count, conversation_Copy.Messages.Count);
            Assert.NotSame(conversation.Messages[1], conversation_Copy.Messages[1]);
            Assert.Equal(Role.Assistant, conversation_Copy.Messages[1].Role);
            Assert.Equal(4, conversation_Copy.Messages[1].Contents!.Count);

            ThinkingContent thinkingContent = new("text", "signature", "data");
            ThinkingContent thinkingContent_Copy = new(thinkingContent);
            Assert.Equal(thinkingContent.Text, thinkingContent_Copy.Text);
            Assert.Equal(thinkingContent.Signature, thinkingContent_Copy.Signature);
            Assert.Equal(thinkingContent.RedactedData, thinkingContent_Copy.RedactedData);

            ToolUseContent toolUseContent = new("id", "name", "{}");
            ToolUseContent toolUseContent_Copy = new(toolUseContent);
            Assert.Equal(toolUseContent.Id, toolUseContent_Copy.Id);
            Assert.Equal(toolUseContent.Name, toolUseContent_Copy.Name);
            Assert.Equal(toolUseContent.InputJson, toolUseContent_Copy.InputJson);

            ToolResultContent toolResultContent = new("id", "text", true);
            ToolResultContent toolResultContent_Copy = new(toolResultContent);
            Assert.Equal(toolResultContent.ToolUseId, toolResultContent_Copy.ToolUseId);
            Assert.Equal(toolResultContent.Text, toolResultContent_Copy.Text);
            Assert.Equal(toolResultContent.IsError, toolResultContent_Copy.IsError);

            Assert.Equal("text", new TextContent(new TextContent("text")).Text);

            ToolDefinition toolDefinition = new("tool", "A tool.", """{"type":"object"}""");
            ToolDefinition toolDefinition_Copy = new(toolDefinition);
            Assert.Equal(toolDefinition.Name, toolDefinition_Copy.Name);
            Assert.Equal(toolDefinition.Description, toolDefinition_Copy.Description);
            Assert.Equal(toolDefinition.InputSchemaJson, toolDefinition_Copy.InputSchemaJson);
            Core.xUnit.Query.SerializationCheck(toolDefinition);

            Assert.Null(new ToolUseContent("id", "name", "{\"method\":").Input());
            Assert.Empty(new ToolUseContent("id", "name", null).Input()!);
            Assert.Equal("object", new ToolDefinition("t", null, "not json").InputSchema()?["type"]?.GetValue<string>());
        }

        /// <summary>
        /// Tests the JSON round trip and copy constructors of every <see cref="HarnessEvent"/>, <see cref="TurnResult"/>, <see cref="Usage"/> and <see cref="RunOptions"/>.
        /// </summary>
        [Fact]
        public void HarnessEvent()
        {
            Usage usage = new(10, 20, 30, 40);
            TurnResult turnResult = new(StopReason.EndTurn, ErrorCategory.Undefined, null, 2, 3, usage, 1234.5);

            List<HarnessEvent> harnessEvents =
            [
                new StepStartedEvent(1, 1.5),
                new TextDeltaEvent("text", 2),
                new ThinkingDeltaEvent("thinking", 3),
                new ThinkingCompletedEvent("thinking done", 4),
                new ThinkingSummaryEvent("summary", 5),
                new ToolCallStartedEvent("toolu_01", "call_endpoint", "{}", 6),
                new ToolCallCompletedEvent("toolu_01", "call_endpoint", true, 7.25, 100, true, 8),
                new UsageEvent(usage, 9),
                new ErrorEvent(ErrorCategory.Unavailable, "no answer", 10),
                new TurnCompletedEvent(turnResult, 11),
            ];

            foreach (HarnessEvent harnessEvent in harnessEvents)
            {
                Core.xUnit.Query.SerializationCheck(harnessEvent);
            }

            Core.xUnit.Query.SerializationCheck(turnResult);

            TurnResult turnResult_Copy = new(turnResult);
            Assert.Equal(turnResult.StopReason, turnResult_Copy.StopReason);
            Assert.Equal(turnResult.ErrorCategory, turnResult_Copy.ErrorCategory);
            Assert.Equal(turnResult.ErrorMessage, turnResult_Copy.ErrorMessage);
            Assert.Equal(turnResult.Steps, turnResult_Copy.Steps);
            Assert.Equal(turnResult.ToolCalls, turnResult_Copy.ToolCalls);
            Assert.Equal(turnResult.TotalMilliseconds, turnResult_Copy.TotalMilliseconds);
            Assert.Equal(30, turnResult_Copy.Usage?.CacheReadInputTokens);
            Assert.NotSame(turnResult.Usage, turnResult_Copy.Usage);

            Usage usage_Copy = new(usage);
            Assert.Equal(usage.InputTokens, usage_Copy.InputTokens);
            Assert.Equal(usage.OutputTokens, usage_Copy.OutputTokens);
            Assert.Equal(usage.CacheReadInputTokens, usage_Copy.CacheReadInputTokens);
            Assert.Equal(usage.CacheCreationInputTokens, usage_Copy.CacheCreationInputTokens);

            ToolCallCompletedEvent toolCallCompletedEvent = new((ToolCallCompletedEvent)harnessEvents[6]);
            Assert.Equal("toolu_01", toolCallCompletedEvent.ToolUseId);
            Assert.Equal("call_endpoint", toolCallCompletedEvent.ToolName);
            Assert.True(toolCallCompletedEvent.IsError);
            Assert.Equal(7.25, toolCallCompletedEvent.DurationMilliseconds);
            Assert.Equal(100, toolCallCompletedEvent.Characters);
            Assert.True(toolCallCompletedEvent.Truncated);
            Assert.Equal(8, toolCallCompletedEvent.ElapsedMilliseconds);

            ToolCallStartedEvent toolCallStartedEvent = new((ToolCallStartedEvent)harnessEvents[5]);
            Assert.Equal("toolu_01", toolCallStartedEvent.ToolUseId);
            Assert.Equal("call_endpoint", toolCallStartedEvent.ToolName);
            Assert.Equal("{}", toolCallStartedEvent.InputJson);

            Assert.Equal(1, new StepStartedEvent((StepStartedEvent)harnessEvents[0]).Step);
            Assert.Equal("text", new TextDeltaEvent((TextDeltaEvent)harnessEvents[1]).Text);
            Assert.Equal("thinking", new ThinkingDeltaEvent((ThinkingDeltaEvent)harnessEvents[2]).Text);
            Assert.Equal("thinking done", new ThinkingCompletedEvent((ThinkingCompletedEvent)harnessEvents[3]).Text);
            Assert.Equal("summary", new ThinkingSummaryEvent((ThinkingSummaryEvent)harnessEvents[4]).Text);
            Assert.Equal(20, new UsageEvent((UsageEvent)harnessEvents[7]).Usage?.OutputTokens);
            ErrorEvent errorEvent = new((ErrorEvent)harnessEvents[8]);
            Assert.Equal(ErrorCategory.Unavailable, errorEvent.ErrorCategory);
            Assert.Equal("no answer", errorEvent.Message);
            Assert.Equal(2, new TurnCompletedEvent((TurnCompletedEvent)harnessEvents[9]).TurnResult?.Steps);

            RunOptions runOptions = new() { SystemPrompt = "prompt", MaxToolIterations = 3, MaxToolResultCharacters = 99 };
            Core.xUnit.Query.SerializationCheck(runOptions);
            RunOptions runOptions_Copy = new(runOptions);
            Assert.Equal("prompt", runOptions_Copy.SystemPrompt);
            Assert.Equal(3, runOptions_Copy.MaxToolIterations);
            Assert.Equal(99, runOptions_Copy.MaxToolResultCharacters);
        }

        /// <summary>
        /// Tests <see cref="Query.SummaryLine(string?, int)"/>: first sentence, first line, markdown markers dropped, and the cut with an ellipsis.
        /// </summary>
        [Fact]
        public void SummaryLine()
        {
            Assert.Equal("Finding the county.", "**Finding the county.** The user asks about Kraków.".SummaryLine(200));
            Assert.Equal("First line", "First line\nSecond line".SummaryLine(200));
            Assert.Equal("Checking counties", "## Checking counties".SummaryLine(200));
            Assert.Equal("abcdefghi…", "abcdefghijklmnop".SummaryLine(10));
            Assert.Null("   ".SummaryLine(200));
            Assert.Null(((string?)null).SummaryLine(200));
        }
    }
}
