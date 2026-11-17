using DiGi.Harness.Classes;
using DiGi.Harness.Enums;
using DiGi.Harness.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace DiGi.Harness.WebAPI.xUnit
{
    public partial class Facts
    {
        private const string Stream_ThinkingAnswer = """
            event: message_start
            data: {"type":"message_start","message":{"usage":{"input_tokens":10,"output_tokens":1}}}

            event: content_block_start
            data: {"type":"content_block_start","index":0,"content_block":{"type":"thinking","thinking":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"thinking_delta","thinking":"**Counting the counties.** The index has a count endpoint."}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":0,"delta":{"type":"signature_delta","signature":"sig"}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":0}

            event: content_block_start
            data: {"type":"content_block_start","index":1,"content_block":{"type":"text","text":""}}

            event: content_block_delta
            data: {"type":"content_block_delta","index":1,"delta":{"type":"text_delta","text":"There are 380 counties."}}

            event: content_block_stop
            data: {"type":"content_block_stop","index":1}

            event: message_delta
            data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":20}}

            """;

        private static HarnessConfiguration HarnessConfiguration_Test(bool enabled = true, IEnumerable<string>? allowedUserIds = null)
        {
            ProviderConfiguration providerConfiguration = new("anthropic", "Claude", Protocol.Anthropic, "https://api.anthropic.test", "claude-opus-5-5", "sk-secret", null, 1000, null, true, false, false, true);
            return new HarnessConfiguration(enabled, "anthropic", [providerConfiguration], 4, 1000, 30, allowedUserIds);
        }

        private static HarnessChatController Controller(HarnessConfiguration harnessConfiguration, ChatSessionStore chatSessionStore, StubHarnessHost stubHarnessHost, StubHttpClientFactory stubHttpClientFactory, string? body = null)
        {
            DefaultHttpContext defaultHttpContext = new();
            defaultHttpContext.Response.Body = new MemoryStream();
            if (body is not null)
            {
                defaultHttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            }

            return new HarnessChatController(harnessConfiguration, chatSessionStore, stubHarnessHost, stubHttpClientFactory, TimeProvider.System)
            {
                ControllerContext = new ControllerContext { HttpContext = defaultHttpContext },
            };
        }

        private static int StatusCode(IActionResult actionResult)
        {
            return actionResult switch
            {
                IStatusCodeActionResult statusCodeActionResult => statusCodeActionResult.StatusCode ?? 200,
                _ => 200,
            };
        }

        private static List<JsonObject> Events(HarnessChatController harnessChatController)
        {
            MemoryStream memoryStream = (MemoryStream)harnessChatController.HttpContext.Response.Body;
            string text = Encoding.UTF8.GetString(memoryStream.ToArray());

            List<JsonObject> result = [];
            foreach (string block in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                Assert.StartsWith("data: ", block);
                Assert.DoesNotContain('\n', block);
                result.Add((JsonObject)JsonNode.Parse(block["data: ".Length..])!);
            }

            return result;
        }

        private static string TypeName(JsonObject jsonObject)
        {
            string fullName = jsonObject["_type"]!.GetValue<string>().Split(',')[0];
            return fullName[(fullName.LastIndexOf('.') + 1)..];
        }

        /// <summary>
        /// Asserts that every controller in <c>DiGi.Harness.WebAPI</c> has exactly one public constructor and can be activated by MVC; a second constructor answers HTTP 500 on every endpoint.
        /// </summary>
        [Fact]
        public void ControllerActivation_SingleApplicableConstructor()
        {
            Assembly assembly = typeof(HarnessChatController).Assembly;

            List<Type> types = [.. assembly.GetTypes().Where(x => x.IsPublic && !x.IsAbstract && typeof(DiGi.WebAPI.Classes.WebAPIController).IsAssignableFrom(x))];
            Assert.NotEmpty(types);

            foreach (Type type in types)
            {
                Assert.True(type.GetConstructors().Length == 1, $"{type.FullName} declares more than one public constructor.");
                Assert.NotNull(ActivatorUtilities.CreateFactory(type, Type.EmptyTypes));
            }
        }

        /// <summary>
        /// Tests the statuses of <see cref="HarnessChatController"/> before a stream starts: 401 without a user or for a user outside the allowlist, <c>Enabled: false</c> from the configuration for them, 503 when disabled, 400 for an unknown provider or empty text, 404 for a foreign session, 409 while a turn runs, 204 on delete; and that the configuration never carries a key or a URI.
        /// </summary>
        [Fact]
        public async Task HarnessChatController_Statuses()
        {
            ChatSessionStore chatSessionStore = new(TimeSpan.FromHours(1), 5);
            StubHttpClientFactory stubHttpClientFactory = new();
            StubHarnessHost stubHarnessHost_None = new(null);
            StubHarnessHost stubHarnessHost_Anna = new(new HarnessUser("anna@example.test", "Anna", "token-anna"));
            StubHarnessHost stubHarnessHost_Bob = new(new HarnessUser("bob@example.test", "Bob", "token-bob"));

            Assert.Equal(401, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_None, stubHttpClientFactory).ConfigurationAsync()));

            IActionResult actionResult_Configuration = await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).ConfigurationAsync();
            string configuration = Assert.IsType<ContentResult>(actionResult_Configuration).Content!;
            Assert.True(JsonNode.Parse(configuration)!["Enabled"]!.GetValue<bool>());
            Assert.Equal("anthropic", JsonNode.Parse(configuration)!["Providers"]![0]!["Name"]!.GetValue<string>());
            Assert.DoesNotContain("sk-secret", configuration);
            Assert.DoesNotContain("api.anthropic.test", configuration);

            string configuration_Disabled = Assert.IsType<ContentResult>(await Controller(HarnessConfiguration_Test(enabled: false), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).ConfigurationAsync()).Content!;
            Assert.False(JsonNode.Parse(configuration_Disabled)!["Enabled"]!.GetValue<bool>());

            string configuration_NotAllowed = Assert.IsType<ContentResult>(await Controller(HarnessConfiguration_Test(allowedUserIds: ["anna@example.test"]), chatSessionStore, stubHarnessHost_Bob, stubHttpClientFactory).ConfigurationAsync()).Content!;
            Assert.False(JsonNode.Parse(configuration_NotAllowed)!["Enabled"]!.GetValue<bool>());
            Assert.Equal(401, StatusCode(await Controller(HarnessConfiguration_Test(allowedUserIds: ["anna@example.test"]), chatSessionStore, stubHarnessHost_Bob, stubHttpClientFactory).CreateSessionAsync(null)));

            Assert.Equal(503, StatusCode(await Controller(HarnessConfiguration_Test(enabled: false), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).CreateSessionAsync(null)));
            Assert.Equal(401, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_None, stubHttpClientFactory).CreateSessionAsync(null)));
            Assert.Equal(400, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).CreateSessionAsync(new JsonObject { ["Provider"] = "missing" })));

            IActionResult actionResult_Session = await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).CreateSessionAsync(null);
            string sessionId = JsonNode.Parse(Assert.IsType<ContentResult>(actionResult_Session).Content!)!["Id"]!.GetValue<string>();

            Assert.Equal(404, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Bob, stubHttpClientFactory).SessionAsync(sessionId)));
            Assert.Equal(200, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).SessionAsync(sessionId)));
            Assert.Equal(404, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Bob, stubHttpClientFactory).MessageAsync(sessionId, new JsonObject { ["Text"] = "Hi" })));
            Assert.Equal(400, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).MessageAsync(sessionId, new JsonObject { ["Text"] = "  " })));
            Assert.Equal(400, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).MessageAsync(sessionId, new JsonObject { ["Text"] = new string('a', Constants.Default.MaxMessageCharacters + 1) })));

            ChatSession chatSession = chatSessionStore.ChatSessions[sessionId];
            await chatSession.SemaphoreSlim.WaitAsync();
            Assert.Equal(409, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).MessageAsync(sessionId, new JsonObject { ["Text"] = "Hi" })));
            chatSession.SemaphoreSlim.Release();

            Assert.Equal(404, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Bob, stubHttpClientFactory).DeleteSessionAsync(sessionId)));
            Assert.Equal(204, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).DeleteSessionAsync(sessionId)));
            Assert.Equal(404, StatusCode(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost_Anna, stubHttpClientFactory).SessionAsync(sessionId)));
            Assert.Equal(0, stubHttpClientFactory.Count);
        }

        /// <summary>
        /// Tests the event stream of <see cref="HarnessChatController.MessageAsync(string?, JsonObject?, CancellationToken)"/> for each thinking visibility: one compact JSON event per <c>data:</c> line ending with the completed turn; <c>None</c> sends no thinking event at all, <c>Summary</c> one capped summary line and no deltas, <c>Full</c> the deltas and no summary; and the transcript drops signatures.
        /// </summary>
        [Fact]
        public async Task HarnessChatController_MessageStream()
        {
            StubHarnessHost stubHarnessHost = new(new HarnessUser("anna@example.test", "Anna", "token-anna"));

            foreach (string visibility in new[] { "None", "Summary", "Full" })
            {
                ChatSessionStore chatSessionStore = new(TimeSpan.FromHours(1), 5);
                StubHttpClientFactory stubHttpClientFactory = new();
                stubHttpClientFactory.Enqueue(Stream_ThinkingAnswer);

                string sessionId = JsonNode.Parse(Assert.IsType<ContentResult>(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost, stubHttpClientFactory).CreateSessionAsync(null)).Content!)!["Id"]!.GetValue<string>();

                HarnessChatController harnessChatController = Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost, stubHttpClientFactory);
                IActionResult actionResult = await harnessChatController.MessageAsync(sessionId, new JsonObject { ["Text"] = "How many counties?", ["ThinkingVisibility"] = visibility });
                Assert.IsType<EmptyResult>(actionResult);
                Assert.Equal("text/event-stream; charset=utf-8", harnessChatController.HttpContext.Response.ContentType);

                List<JsonObject> events = Events(harnessChatController);
                List<string> typeNames = events.ConvertAll(TypeName);

                Assert.Equal("StepStartedEvent", typeNames[0]);
                Assert.Equal("TurnCompletedEvent", typeNames[^1]);
                Assert.Equal("There are 380 counties.", string.Concat(events.Where(x => TypeName(x) == "TextDeltaEvent").Select(x => x["Text"]!.GetValue<string>())));
                Assert.DoesNotContain("ThinkingCompletedEvent", typeNames);
                Assert.DoesNotContain(events, x => x.ToJsonString().Contains("sk-secret", StringComparison.Ordinal));

                switch (visibility)
                {
                    case "None":
                        Assert.DoesNotContain(typeNames, x => x.StartsWith("Thinking", StringComparison.Ordinal));
                        Assert.DoesNotContain(events, x => x.ToJsonString().Contains("Counting the counties", StringComparison.Ordinal));
                        break;

                    case "Summary":
                        Assert.DoesNotContain("ThinkingDeltaEvent", typeNames);
                        Assert.Equal("Counting the counties.", events.Single(x => TypeName(x) == "ThinkingSummaryEvent")["Text"]!.GetValue<string>());
                        break;

                    case "Full":
                        Assert.Contains("ThinkingDeltaEvent", typeNames);
                        Assert.DoesNotContain("ThinkingSummaryEvent", typeNames);
                        break;
                }

                string transcript = Assert.IsType<ContentResult>(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost, stubHttpClientFactory).SessionAsync(sessionId, ThinkingVisibility.Full)).Content!;
                Assert.Contains("There are 380 counties.", transcript);
                Assert.DoesNotContain("\"sig\"", transcript);

                string transcript_None = Assert.IsType<ContentResult>(await Controller(HarnessConfiguration_Test(), chatSessionStore, stubHarnessHost, stubHttpClientFactory).SessionAsync(sessionId, ThinkingVisibility.None)).Content!;
                Assert.DoesNotContain("Counting the counties", transcript_None);

                Assert.Equal(2, chatSessionStore.ChatSessions[sessionId].Conversation.Messages.Count);
                Assert.Equal(1, stubHttpClientFactory.Count);
                Assert.Equal(1, chatSessionStore.ChatSessions[sessionId].SemaphoreSlim.CurrentCount);
            }
        }
    }
}
