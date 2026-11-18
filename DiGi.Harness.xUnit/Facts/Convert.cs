using DiGi.Harness.Classes;
using DiGi.Harness.Enums;
using System.Text.Json.Nodes;

namespace DiGi.Harness.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Convert.ToAnthropic_JsonObject(Classes.Conversation?, ProviderConfiguration?, IEnumerable{ToolDefinition}?, string?, ToolChoice)"/>: the cached system block, adaptive summarized thinking, effort, fallbacks, automatic tool choice and <c>tool_choice: none</c> with the tools kept, and thinking blocks replayed unchanged with their signatures.
        /// </summary>
        [Fact]
        public void ToAnthropic_JsonObject()
        {
            Classes.Conversation conversation = Conversation_Sample();
            ToolDefinition toolDefinition = new("call_endpoint", "Calls an endpoint.", """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"]}""");

            JsonObject? jsonObject = conversation.ToAnthropic_JsonObject(ProviderConfiguration_Anthropic(), [toolDefinition], "You are the DiGi GIS assistant.");
            Assert.NotNull(jsonObject);

            Assert.Equal("claude-opus-5-5", jsonObject!["model"]?.GetValue<string>());
            Assert.Equal(32000, jsonObject["max_tokens"]?.GetValue<int>());
            Assert.True(jsonObject["stream"]?.GetValue<bool>());
            Assert.Equal("ephemeral", jsonObject["system"]?[0]?["cache_control"]?["type"]?.GetValue<string>());
            Assert.Equal("You are the DiGi GIS assistant.", jsonObject["system"]?[0]?["text"]?.GetValue<string>());
            Assert.Equal("adaptive", jsonObject["thinking"]?["type"]?.GetValue<string>());
            Assert.Equal("summarized", jsonObject["thinking"]?["display"]?.GetValue<string>());
            Assert.Equal("medium", jsonObject["output_config"]?["effort"]?.GetValue<string>());
            Assert.Equal("default", jsonObject["fallbacks"]?.GetValue<string>());
            Assert.Equal("auto", jsonObject["tool_choice"]?["type"]?.GetValue<string>());
            Assert.Equal("path", jsonObject["tools"]?[0]?["input_schema"]?["required"]?[0]?.GetValue<string>());

            JsonArray? jsonArray_Messages = jsonObject["messages"] as JsonArray;
            Assert.Equal(4, jsonArray_Messages?.Count);

            JsonNode? jsonNode_Assistant = jsonArray_Messages![1];
            Assert.Equal("assistant", jsonNode_Assistant?["role"]?.GetValue<string>());
            Assert.Equal("""{"type":"thinking","thinking":"**Finding the county.** I need its id.","signature":"EqQBsignature"}""", jsonNode_Assistant?["content"]?[0]?.ToJsonString());
            Assert.Equal("""{"type":"redacted_thinking","data":"redacted-opaque-data"}""", jsonNode_Assistant?["content"]?[1]?.ToJsonString());
            Assert.Equal("tool_use", jsonNode_Assistant?["content"]?[3]?["type"]?.GetValue<string>());
            Assert.Equal("Kraków", jsonNode_Assistant?["content"]?[3]?["input"]?["query"]?["name"]?.GetValue<string>());

            JsonNode? jsonNode_ToolResult = jsonArray_Messages[2]?["content"]?[0];
            Assert.Equal("tool_result", jsonNode_ToolResult?["type"]?.GetValue<string>());
            Assert.Equal("toolu_01", jsonNode_ToolResult?["tool_use_id"]?.GetValue<string>());
            Assert.Null(jsonNode_ToolResult?["is_error"]);

            JsonObject? jsonObject_Plain = conversation.ToAnthropic_JsonObject(ProviderConfiguration_Local(), null, null);
            Assert.Null(jsonObject_Plain?["thinking"]);
            Assert.Null(jsonObject_Plain?["fallbacks"]);
            Assert.Null(jsonObject_Plain?["tools"]);
            Assert.Null(jsonObject_Plain?["system"]);
            Assert.Equal(Constants.Default.MaxTokens, jsonObject_Plain?["max_tokens"]?.GetValue<int>());

            JsonObject? jsonObject_None = conversation.ToAnthropic_JsonObject(ProviderConfiguration_Anthropic(), [toolDefinition], null, ToolChoice.None);
            Assert.Equal("none", jsonObject_None?["tool_choice"]?["type"]?.GetValue<string>());
            Assert.Equal("call_endpoint", jsonObject_None?["tools"]?[0]?["name"]?.GetValue<string>());
            Assert.Equal("auto", conversation.ToAnthropic_JsonObject(ProviderConfiguration_Anthropic(), [toolDefinition], null, ToolChoice.Auto)?["tool_choice"]?["type"]?.GetValue<string>());
        }

        /// <summary>
        /// Tests <see cref="Convert.ToOpenAI_JsonObject(Classes.Conversation?, ProviderConfiguration?, IEnumerable{ToolDefinition}?, string?, ToolChoice)"/>: the system message, tool calls with their arguments string, <c>role: tool</c> results, <c>reasoning_content</c> replayed only when the provider asks for it, and <c>tool_choice: "none"</c> with the tools kept.
        /// </summary>
        [Fact]
        public void ToOpenAI_JsonObject()
        {
            Classes.Conversation conversation = Conversation_Sample();
            ToolDefinition toolDefinition = new("call_endpoint", "Calls an endpoint.", """{"type":"object","properties":{"path":{"type":"string"}}}""");

            JsonObject? jsonObject = conversation.ToOpenAI_JsonObject(ProviderConfiguration_Local(), [toolDefinition], "System prompt.");
            Assert.NotNull(jsonObject);
            Assert.True(jsonObject!["stream_options"]?["include_usage"]?.GetValue<bool>());
            Assert.Equal("auto", jsonObject["tool_choice"]?.GetValue<string>());
            Assert.Equal("function", jsonObject["tools"]?[0]?["type"]?.GetValue<string>());
            Assert.Equal("call_endpoint", jsonObject["tools"]?[0]?["function"]?["name"]?.GetValue<string>());
            Assert.Null(jsonObject["reasoning_effort"]);

            JsonArray? jsonArray_Messages = jsonObject["messages"] as JsonArray;
            Assert.Equal(5, jsonArray_Messages?.Count);
            Assert.Equal("system", jsonArray_Messages![0]?["role"]?.GetValue<string>());
            Assert.Equal("user", jsonArray_Messages[1]?["role"]?.GetValue<string>());

            JsonNode? jsonNode_Assistant = jsonArray_Messages[2];
            Assert.Equal("Looking up the county.", jsonNode_Assistant?["content"]?.GetValue<string>());
            Assert.Equal("**Finding the county.** I need its id.", jsonNode_Assistant?["reasoning_content"]?.GetValue<string>());
            Assert.Equal("toolu_01", jsonNode_Assistant?["tool_calls"]?[0]?["id"]?.GetValue<string>());
            string? arguments = jsonNode_Assistant?["tool_calls"]?[0]?["function"]?["arguments"]?.GetValue<string>();
            Assert.Equal("Kraków", JsonNode.Parse(arguments!)?["query"]?["name"]?.GetValue<string>());

            Assert.Equal("tool", jsonArray_Messages[3]?["role"]?.GetValue<string>());
            Assert.Equal("toolu_01", jsonArray_Messages[3]?["tool_call_id"]?.GetValue<string>());
            Assert.Equal("assistant", jsonArray_Messages[4]?["role"]?.GetValue<string>());

            ProviderConfiguration providerConfiguration_NoReplay = new("x", null, Protocol.OpenAI, "http://host/v1", "m", null, null, 0, "high", false, false, false, true);
            JsonObject? jsonObject_NoReplay = conversation.ToOpenAI_JsonObject(providerConfiguration_NoReplay, null, null);
            Assert.Null(jsonObject_NoReplay?["messages"]?[1]?["reasoning_content"]);
            Assert.Equal("high", jsonObject_NoReplay?["reasoning_effort"]?.GetValue<string>());
            Assert.Equal(4, (jsonObject_NoReplay?["messages"] as JsonArray)?.Count);

            JsonObject? jsonObject_None = conversation.ToOpenAI_JsonObject(ProviderConfiguration_Local(), [toolDefinition], null, ToolChoice.None);
            Assert.Equal("none", jsonObject_None?["tool_choice"]?.GetValue<string>());
            Assert.Equal("call_endpoint", jsonObject_None?["tools"]?[0]?["function"]?["name"]?.GetValue<string>());
            Assert.Equal("auto", conversation.ToOpenAI_JsonObject(ProviderConfiguration_Local(), [toolDefinition], null, ToolChoice.Auto)?["tool_choice"]?.GetValue<string>());
            Assert.Null(conversation.ToOpenAI_JsonObject(ProviderConfiguration_Local(), null, null, ToolChoice.None)?["tool_choice"]);
        }

        /// <summary>
        /// Tests <see cref="Convert.ToHarness_StopReason(string?)"/> for both protocols' values.
        /// </summary>
        [Fact]
        public void ToHarness_StopReason()
        {
            Assert.Equal(StopReason.EndTurn, "end_turn".ToHarness_StopReason());
            Assert.Equal(StopReason.EndTurn, "stop".ToHarness_StopReason());
            Assert.Equal(StopReason.ToolUse, "tool_use".ToHarness_StopReason());
            Assert.Equal(StopReason.ToolUse, "TOOL_CALLS".ToHarness_StopReason());
            Assert.Equal(StopReason.MaxTokens, "length".ToHarness_StopReason());
            Assert.Equal(StopReason.Refusal, "refusal".ToHarness_StopReason());
            Assert.Equal(StopReason.Refusal, "content_filter".ToHarness_StopReason());
            Assert.Equal(StopReason.Other, "pause_turn".ToHarness_StopReason());
            Assert.Equal(StopReason.Undefined, ((string?)null).ToHarness_StopReason());
        }
    }
}
