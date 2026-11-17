using DiGi.Harness.Classes;
using DiGi.Harness.Enums;

namespace DiGi.Harness.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Query.Export(Classes.Conversation?, ExportMode)"/> in <see cref="ExportMode.Messages"/> (and <see cref="ExportMode.Undefined"/>) keeps the text only, drops the tool-result-only user message and leaves the source untouched.
        /// </summary>
        [Fact]
        public void Export_Messages()
        {
            Classes.Conversation conversation = Conversation_Sample();

            foreach (ExportMode exportMode in new[] { ExportMode.Messages, ExportMode.Undefined })
            {
                Classes.Conversation? conversation_Export = conversation.Export(exportMode);
                Assert.NotNull(conversation_Export);
                Assert.Equal("anthropic", conversation_Export!.ProviderName);
                Assert.Equal(3, conversation_Export.Messages.Count);

                Assert.Equal([Role.User, Role.Assistant, Role.Assistant], conversation_Export.Messages.Select(x => x.Role));
                Assert.All(conversation_Export.Messages, x => Assert.All(x.Contents!, y => Assert.IsType<TextContent>(y)));
                Assert.Equal("Looking up the county.", ((TextContent)conversation_Export.Messages[1].Contents!.Single()).Text);
                Assert.Equal("Kraków has 12 345 buildings.", ((TextContent)conversation_Export.Messages[2].Contents!.Single()).Text);
            }

            Assert.Equal(4, conversation.Messages.Count);
            Assert.Equal(4, conversation.Messages[1].Contents!.Count);
            Assert.Equal("EqQBsignature", ((ThinkingContent)conversation.Messages[1].Contents![0]).Signature);

            Assert.Null(((Classes.Conversation?)null).Export(ExportMode.Full));
        }

        /// <summary>
        /// Tests that <see cref="Query.Export(Classes.Conversation?, ExportMode)"/> in <see cref="ExportMode.Full"/> keeps the complete thinking text (longer than a summary line), the tool calls and the tool results, without signatures or redacted thinking data.
        /// </summary>
        [Fact]
        public void Export_Full()
        {
            string text_Thinking = string.Concat("**Finding the county.** ", new string('x', 400), "\nSecond line.");

            Classes.Conversation conversation = new("anthropic", null);
            conversation.Add(new Message(Role.User, [new TextContent("How many buildings are in Kraków?")]));
            conversation.Add(new Message(Role.Assistant,
            [
                new ThinkingContent(text_Thinking, "EqQBsignature", null),
                new ThinkingContent(null, null, "redacted-opaque-data"),
                new ToolUseContent("toolu_01", "call_endpoint", """{"method":"GET"}"""),
            ]));
            conversation.Add(new Message(Role.User, [new ToolResultContent("toolu_01", "HTTP 200 OK\n1261", false)]));
            conversation.Add(new Message(Role.Assistant, [new TextContent("Kraków has 12 345 buildings.")]));

            Classes.Conversation? conversation_Export = conversation.Export(ExportMode.Full);
            Assert.NotNull(conversation_Export);
            Assert.Equal(4, conversation_Export!.Messages.Count);

            List<Interfaces.IContent> contents = [.. conversation_Export.Messages[1].Contents!];
            Assert.Equal(2, contents.Count);
            ThinkingContent thinkingContent = Assert.IsType<ThinkingContent>(contents[0]);
            Assert.Equal(text_Thinking, thinkingContent.Text);
            Assert.Null(thinkingContent.Signature);
            Assert.Null(thinkingContent.RedactedData);
            Assert.Equal("""{"method":"GET"}""", Assert.IsType<ToolUseContent>(contents[1]).InputJson);

            ToolResultContent toolResultContent = Assert.IsType<ToolResultContent>(conversation_Export.Messages[2].Contents!.Single());
            Assert.Equal("HTTP 200 OK\n1261", toolResultContent.Text);

            string? json = Core.Convert.ToSystem_String(conversation_Export);
            Assert.DoesNotContain("EqQBsignature", json);
            Assert.DoesNotContain("redacted-opaque-data", json);
        }

        /// <summary>
        /// Tests <see cref="Create.ConversationExport(Classes.Conversation?, ExportMode, DateTimeOffset, string?)"/>, the JSON round trip of <see cref="Classes.ConversationExport"/> and its copy constructor.
        /// </summary>
        [Fact]
        public void ConversationExport()
        {
            DateTimeOffset exportedAt = new(2026, 11, 17, 10, 30, 0, TimeSpan.Zero);

            Assert.Null(Create.ConversationExport(null, ExportMode.Full, exportedAt, "1.0.0"));

            Classes.ConversationExport? conversationExport_Undefined = Create.ConversationExport(Conversation_Sample(), ExportMode.Undefined, exportedAt, "1.0.0");
            Assert.Equal(ExportMode.Messages, conversationExport_Undefined?.Mode);

            Classes.ConversationExport? conversationExport = Create.ConversationExport(Conversation_Sample(), ExportMode.Full, exportedAt, "1.0.0");
            Assert.NotNull(conversationExport);
            Assert.Equal(ExportMode.Full, conversationExport!.Mode);
            Assert.Equal(exportedAt, conversationExport.ExportedAt);
            Assert.Equal("anthropic", conversationExport.ProviderName);
            Assert.Equal("1.0.0", conversationExport.Version);
            Assert.Equal(4, conversationExport.Conversation?.Messages.Count);

            Core.xUnit.Query.SerializationCheck(conversationExport);

            Classes.ConversationExport? conversationExport_Json = Core.Convert.ToDiGi<Classes.ConversationExport>(Core.Convert.ToSystem_String(conversationExport))?.FirstOrDefault();
            Assert.NotNull(conversationExport_Json);
            Assert.Equal(ExportMode.Full, conversationExport_Json!.Mode);
            Assert.Equal(exportedAt, conversationExport_Json.ExportedAt);
            Assert.Equal(4, conversationExport_Json.Conversation?.Messages.Count);

            Classes.ConversationExport conversationExport_Copy = new(conversationExport);
            Assert.Equal(conversationExport.Mode, conversationExport_Copy.Mode);
            Assert.Equal(conversationExport.ExportedAt, conversationExport_Copy.ExportedAt);
            Assert.Equal(conversationExport.ProviderName, conversationExport_Copy.ProviderName);
            Assert.Equal(conversationExport.Version, conversationExport_Copy.Version);
            Assert.NotNull(conversationExport_Copy.Conversation);
            Assert.NotSame(conversationExport.Conversation, conversationExport_Copy.Conversation);
            Assert.Equal(conversationExport.Conversation!.Messages.Count, conversationExport_Copy.Conversation!.Messages.Count);
        }
    }
}
