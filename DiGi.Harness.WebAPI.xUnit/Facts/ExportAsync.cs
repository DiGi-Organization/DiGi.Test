using DiGi.Harness.Classes;
using DiGi.Harness.Enums;
using DiGi.Harness.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace DiGi.Harness.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="HarnessChatController.ExportAsync(string?, string?, CancellationToken)"/>: 401 without a user, 503 when disabled, 400 for an unknown mode, 404 for a foreign session, 409 while a turn runs; <c>Messages</c> by default (text only), <c>Full</c> with the complete thinking text and tool calls but no signature or redacted data; and the attachment file name.
        /// </summary>
        [Fact]
        public async Task ExportAsync()
        {
            ChatSessionStore chatSessionStore = new(TimeSpan.FromHours(1), 5);
            StubHttpClientFactory stubHttpClientFactory = new();
            StubHarnessHost stubHarnessHost_None = new(null);
            StubHarnessHost stubHarnessHost_Anna = new(new HarnessUser("anna@example.test", "Anna", "token-anna"));
            StubHarnessHost stubHarnessHost_Bob = new(new HarnessUser("bob@example.test", "Bob", "token-bob"));

            IActionResult actionResult_Session = await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).CreateSessionAsync(null);
            string sessionId = JsonNode.Parse(Assert.IsType<ContentResult>(actionResult_Session).Content!)!["Id"]!.GetValue<string>();

            string text_Thinking = string.Concat("**Counting the counties.** ", new string('x', 400));

            Classes.ChatSession chatSession = chatSessionStore.ChatSessions[sessionId];
            chatSession.Conversation.Add(new Message(Role.User, [new TextContent("How many counties?")]));
            chatSession.Conversation.Add(new Message(Role.Assistant,
            [
                new ThinkingContent(text_Thinking, "sig-secret", null),
                new ThinkingContent(null, null, "redacted-secret"),
                new ToolUseContent("toolu_01", "call_endpoint", """{"method":"GET"}"""),
            ]));
            chatSession.Conversation.Add(new Message(Role.User, [new ToolResultContent("toolu_01", "380", false)]));
            chatSession.Conversation.Add(new Message(Role.Assistant, [new TextContent("There are 380 counties.")]));

            Assert.Equal(401, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_None, stubHttpClientFactory).ExportAsync(sessionId)));
            Assert.Equal(503, StatusCode(await Controller(HarnessConfiguration_Test(enabled: false), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).ExportAsync(sessionId)));
            Assert.Equal(400, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).ExportAsync(sessionId, "Undefined")));
            Assert.Equal(400, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).ExportAsync(sessionId, "2")));
            Assert.Equal(404, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Bob, stubHttpClientFactory).ExportAsync(sessionId)));

            await chatSession.SemaphoreSlim.WaitAsync();
            Assert.Equal(409, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).ExportAsync(sessionId)));
            chatSession.SemaphoreSlim.Release();

            HarnessChatController harnessChatController_Messages = Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory);
            string json_Messages = Assert.IsType<ContentResult>(await harnessChatController_Messages.ExportAsync(sessionId)).Content!;
            ConversationExport? conversationExport_Messages = Core.Convert.ToDiGi<ConversationExport>(json_Messages)?.FirstOrDefault();
            Assert.NotNull(conversationExport_Messages);
            Assert.Equal(ExportMode.Messages, conversationExport_Messages!.Mode);
            Assert.Equal("anthropic", conversationExport_Messages.ProviderName);
            Assert.False(string.IsNullOrWhiteSpace(conversationExport_Messages.Version));
            Assert.Equal(2, conversationExport_Messages.Conversation?.Messages.Count);
            Assert.DoesNotContain("Counting the counties", json_Messages);
            Assert.DoesNotContain("call_endpoint", json_Messages);
            Assert.Matches("""^attachment; filename=chat-\d{8}-\d{6}-messages\.json$""", harnessChatController_Messages.Response.Headers.ContentDisposition.ToString());

            HarnessChatController harnessChatController_Full = Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory);
            string json_Full = Assert.IsType<ContentResult>(await harnessChatController_Full.ExportAsync(sessionId, "full")).Content!;
            ConversationExport? conversationExport_Full = Core.Convert.ToDiGi<ConversationExport>(json_Full)?.FirstOrDefault();
            Assert.NotNull(conversationExport_Full);
            Assert.Equal(ExportMode.Full, conversationExport_Full!.Mode);
            Assert.Equal(4, conversationExport_Full.Conversation?.Messages.Count);
            Assert.Equal(text_Thinking, ((ThinkingContent)conversationExport_Full.Conversation!.Messages[1].Contents![0]).Text);
            Assert.Contains("call_endpoint", json_Full);
            Assert.DoesNotContain("sig-secret", json_Full);
            Assert.DoesNotContain("redacted-secret", json_Full);
            Assert.EndsWith("-full.json", harnessChatController_Full.Response.Headers.ContentDisposition.ToString());

            Assert.Equal(0, stubHttpClientFactory.Count);
        }
    }
}
