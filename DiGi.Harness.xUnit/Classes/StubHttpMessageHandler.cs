using System.Net;
using System.Net.Http;
using System.Text;

namespace DiGi.Harness.xUnit
{
    /// <summary>
    /// An <see cref="HttpMessageHandler"/> answering requests from a queue of scripted responses and recording every request with its body.
    /// </summary>
    public sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> funcs = new();

        /// <summary>
        /// Gets the requests received, in order, as (method, URI, authorization header, body).
        /// </summary>
        public List<(HttpMethod Method, Uri? Uri, string? Authorization, string? Body)> Requests { get; } = [];

        /// <summary>
        /// Gets the headers of the requests received, in order.
        /// </summary>
        public List<Dictionary<string, string>> Headers { get; } = [];

        /// <summary>
        /// Queues a response with a status, body and media type.
        /// </summary>
        /// <param name="httpStatusCode">The status.</param>
        /// <param name="body">The body text.</param>
        /// <param name="mediaType">The media type of the body.</param>
        public void Enqueue(HttpStatusCode httpStatusCode, string body, string mediaType = "text/event-stream")
        {
            funcs.Enqueue((httpRequestMessage, cancellationToken) => Task.FromResult(new HttpResponseMessage(httpStatusCode) { Content = new StringContent(body, Encoding.UTF8, mediaType) }));
        }

        /// <summary>
        /// Queues a custom answer, for failures and delays.
        /// </summary>
        /// <param name="func">The function producing the response or throwing.</param>
        public void Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> func)
        {
            funcs.Enqueue(func);
        }

        /// <summary>
        /// Records the request and answers it with the next queued response; fails the test when the queue is empty.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The token that cancels the request.</param>
        /// <returns>The scripted response.</returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri, request.Headers.Authorization?.ToString(), body));
            Headers.Add(request.Headers.ToDictionary(x => x.Key, x => string.Join(",", x.Value), StringComparer.OrdinalIgnoreCase));

            Assert.True(funcs.Count != 0, $"Unexpected request {request.Method} {request.RequestUri}");
            return await funcs.Dequeue()(request, cancellationToken);
        }
    }
}
