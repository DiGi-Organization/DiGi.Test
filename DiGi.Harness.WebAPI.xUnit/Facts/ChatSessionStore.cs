using DiGi.Harness.Classes;
using DiGi.Harness.Enums;
using DiGi.Harness.WebAPI.Classes;

namespace DiGi.Harness.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the session store: a foreign session reads as missing, an idle session expires, <see cref="Modify.RemoveExpired(Classes.ChatSessionStore?, DateTimeOffset)"/> keeps a session whose turn is running, and adding beyond the per-user limit removes that user's least recently used session only.
        /// </summary>
        [Fact]
        public async Task ChatSessionStore()
        {
            DateTimeOffset now = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
            Classes.ChatSessionStore chatSessionStore = new(TimeSpan.FromMinutes(30), 2);

            ChatSession chatSession_A1 = new("a1", "anna", new Conversation("p", null), now.AddMinutes(-40));
            ChatSession chatSession_A2 = new("a2", "anna", new Conversation("p", null), now.AddMinutes(-10));
            ChatSession chatSession_B1 = new("b1", "bob", new Conversation("p", null), now.AddMinutes(-50));
            Assert.True(chatSessionStore.Add(chatSession_A1));
            Assert.True(chatSessionStore.Add(chatSession_A2));
            Assert.True(chatSessionStore.Add(chatSession_B1));
            Assert.False(chatSessionStore.Add(chatSession_A1));

            Assert.Same(chatSession_A2, chatSessionStore.ChatSession("a2", "anna", now));
            Assert.Null(chatSessionStore.ChatSession("a2", "bob", now));
            Assert.Null(chatSessionStore.ChatSession("a1", "anna", now));
            Assert.Null(chatSessionStore.ChatSession("missing", "anna", now));

            await chatSession_B1.SemaphoreSlim.WaitAsync();
            Assert.Equal(1, chatSessionStore.RemoveExpired(now));
            Assert.False(chatSessionStore.ChatSessions.ContainsKey("a1"));
            Assert.True(chatSessionStore.ChatSessions.ContainsKey("b1"));
            chatSession_B1.SemaphoreSlim.Release();

            ChatSession chatSession_A3 = new("a3", "anna", new Conversation("p", null), now);
            ChatSession chatSession_A4 = new("a4", "anna", new Conversation("p", null), now.AddSeconds(1));
            Assert.True(chatSessionStore.Add(chatSession_A3));
            Assert.True(chatSessionStore.Add(chatSession_A4));
            Assert.False(chatSessionStore.ChatSessions.ContainsKey("a2"));
            Assert.True(chatSessionStore.ChatSessions.ContainsKey("a3"));
            Assert.True(chatSessionStore.ChatSessions.ContainsKey("a4"));
            Assert.True(chatSessionStore.ChatSessions.ContainsKey("b1"));

            Assert.False(chatSessionStore.Remove("a3", "bob"));
            Assert.True(chatSessionStore.Remove("a3", "anna"));
            Assert.False(chatSessionStore.Remove("a3", "anna"));
        }

        /// <summary>
        /// Tests <see cref="Query.VisibleHarnessEvent(HarnessEvent?, ThinkingVisibility)"/> and <see cref="Query.Transcript(Conversation?, ThinkingVisibility)"/>: thinking per visibility, other events unchanged, and no signature or redacted data in a transcript.
        /// </summary>
        [Fact]
        public void VisibleHarnessEvent()
        {
            ThinkingDeltaEvent thinkingDeltaEvent = new("delta", 1);
            ThinkingCompletedEvent thinkingCompletedEvent = new("**Plan.** Then more.", 2);
            TextDeltaEvent textDeltaEvent = new("text", 3);

            Assert.Same(thinkingDeltaEvent, thinkingDeltaEvent.VisibleHarnessEvent(ThinkingVisibility.Full));
            Assert.Null(thinkingDeltaEvent.VisibleHarnessEvent(ThinkingVisibility.Summary));
            Assert.Null(thinkingDeltaEvent.VisibleHarnessEvent(ThinkingVisibility.None));

            Assert.Null(thinkingCompletedEvent.VisibleHarnessEvent(ThinkingVisibility.Full));
            Assert.Null(thinkingCompletedEvent.VisibleHarnessEvent(ThinkingVisibility.None));
            ThinkingSummaryEvent thinkingSummaryEvent = Assert.IsType<ThinkingSummaryEvent>(thinkingCompletedEvent.VisibleHarnessEvent(ThinkingVisibility.Summary));
            Assert.Equal("Plan.", thinkingSummaryEvent.Text);
            Assert.Equal(2, thinkingSummaryEvent.ElapsedMilliseconds);
            Assert.IsType<ThinkingSummaryEvent>(thinkingCompletedEvent.VisibleHarnessEvent(ThinkingVisibility.Undefined));

            foreach (ThinkingVisibility thinkingVisibility in Enum.GetValues<ThinkingVisibility>())
            {
                Assert.Same(textDeltaEvent, textDeltaEvent.VisibleHarnessEvent(thinkingVisibility));
            }

            Conversation conversation = new("p",
            [
                new Message(Role.User, [new TextContent("Q")]),
                new Message(Role.Assistant, [new ThinkingContent("**Plan.** Then more.", "signature", null), new ThinkingContent(null, null, "redacted"), new TextContent("A")]),
            ]);

            string? transcript_Full = Core.Convert.ToSystem_String(conversation.Transcript(ThinkingVisibility.Full));
            Assert.Contains("Then more.", transcript_Full);
            Assert.DoesNotContain("signature", transcript_Full);
            Assert.DoesNotContain("redacted", transcript_Full);

            Conversation? transcript_Summary = conversation.Transcript(ThinkingVisibility.Summary);
            Assert.Equal("Plan.", Assert.IsType<ThinkingContent>(transcript_Summary!.Messages[1].Contents![0]).Text);
            Assert.Equal(2, transcript_Summary.Messages[1].Contents!.Count);

            Conversation? transcript_None = conversation.Transcript(ThinkingVisibility.None);
            Assert.Single(transcript_None!.Messages[1].Contents!);
            Assert.Equal(3, conversation.Messages[1].Contents!.Count);
        }
    }
}
