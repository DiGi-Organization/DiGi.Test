using DiGi.Harness.Interfaces;
using DiGi.Harness.WebAPI.Classes;
using DiGi.Harness.WebAPI.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http;
using System.Text;

namespace DiGi.Harness.WebAPI.xUnit
{
    /// <summary>
    /// An <see cref="IHarnessHost"/> returning a fixed user (or none), no tools and a fixed prompt.
    /// </summary>
    public sealed class StubHarnessHost : IHarnessHost
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StubHarnessHost"/> class.
        /// </summary>
        /// <param name="harnessUser">The user every request resolves to, or null for a signed-out request.</param>
        public StubHarnessHost(HarnessUser? harnessUser)
        {
            HarnessUser = harnessUser;
        }

        /// <summary>
        /// Gets or sets the user every request resolves to, or null for a signed-out request.
        /// </summary>
        public HarnessUser? HarnessUser { get; set; }

        /// <summary>
        /// Returns <see cref="HarnessUser"/>.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <param name="cancellationToken">The token that cancels the lookup.</param>
        /// <returns>The user.</returns>
        public Task<HarnessUser?> UserAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HarnessUser);
        }

        /// <summary>
        /// Returns no tools.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <param name="harnessUser">The user.</param>
        /// <param name="cancellationToken">The token that cancels the setup.</param>
        /// <returns>An empty list.</returns>
        public Task<IReadOnlyList<ITool>> ToolsAsync(HttpContext httpContext, HarnessUser harnessUser, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ITool>>([]);
        }

        /// <summary>
        /// Returns a fixed prompt.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the build.</param>
        /// <returns>The prompt.</returns>
        public Task<string?> SystemPromptAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>("Test prompt.");
        }
    }

    /// <summary>
    /// An <see cref="IHttpClientFactory"/> whose clients answer from a queue of scripted SSE bodies.
    /// </summary>
    public sealed class StubHttpClientFactory : IHttpClientFactory, IDisposable
    {
        private readonly StubHttpMessageHandler stubHttpMessageHandler = new();

        /// <summary>
        /// Disposes the scripted handler.
        /// </summary>
        public void Dispose()
        {
            stubHttpMessageHandler.Dispose();
        }

        /// <summary>
        /// Queues a 200 response with an event-stream body.
        /// </summary>
        /// <param name="body">The body.</param>
        public void Enqueue(string body)
        {
            stubHttpMessageHandler.Bodies.Enqueue(body);
        }

        /// <summary>
        /// Gets the number of requests sent.
        /// </summary>
        public int Count => stubHttpMessageHandler.Count;

        /// <summary>
        /// Creates a client on the scripted handler.
        /// </summary>
        /// <param name="name">The client name (ignored).</param>
        /// <returns>The client.</returns>
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(stubHttpMessageHandler, false);
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            public Queue<string> Bodies { get; } = new();

            public int Count { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Count++;
                Assert.True(Bodies.Count != 0, "Unexpected model request.");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Bodies.Dequeue(), Encoding.UTF8, "text/event-stream") });
            }
        }
    }
}
