using DiGi.Harness.Classes;
using DiGi.Harness.Interfaces;

namespace DiGi.Harness.xUnit
{
    /// <summary>
    /// A tool answering every call with a fixed text, recording the calls it received.
    /// </summary>
    public sealed class RecordingTool : ITool
    {
        private readonly Func<ToolUseContent, CancellationToken, Task<string>> func;

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordingTool"/> class.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="inputSchemaJson">The input schema as JSON text.</param>
        /// <param name="func">The function producing the result text.</param>
        public RecordingTool(string name, string inputSchemaJson, Func<ToolUseContent, CancellationToken, Task<string>> func)
        {
            ToolDefinition = new ToolDefinition(name, "A test tool.", inputSchemaJson);
            this.func = func;
        }

        /// <summary>
        /// Gets the definition sent to the model.
        /// </summary>
        public ToolDefinition ToolDefinition { get; }

        /// <summary>
        /// Gets the calls received, in order.
        /// </summary>
        public List<ToolUseContent> Calls { get; } = [];

        /// <summary>
        /// Records the call and returns the scripted text.
        /// </summary>
        /// <param name="toolUseContent">The call.</param>
        /// <param name="cancellationToken">The token that cancels the call.</param>
        /// <returns>The result.</returns>
        public async Task<ToolResultContent> InvokeAsync(ToolUseContent toolUseContent, CancellationToken cancellationToken = default)
        {
            lock (Calls)
            {
                Calls.Add(toolUseContent);
            }

            string text = await func(toolUseContent, cancellationToken);
            return new ToolResultContent(toolUseContent.Id, text, false);
        }
    }
}
