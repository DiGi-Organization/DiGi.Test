using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// A stub message handler that answers requests from a script rather than from the network, so a background task's handling of each server answer can be tested without a deployed Web API.
        /// <para>Counts are kept per request path, which is what lets a fact assert how often each endpoint was called.</para>
        /// </summary>
        private sealed class ScriptedHttpMessageHandler : HttpMessageHandler
        {
            private readonly ConcurrentDictionary<string, int> counts_ByPath = new(StringComparer.OrdinalIgnoreCase);
            private readonly Func<string, int, HttpResponseMessage> response;

            /// <summary>
            /// Initializes a new instance of the <see cref="ScriptedHttpMessageHandler"/> class.
            /// </summary>
            /// <param name="response">Builds the answer from the request path and the number of times that path has been asked for, counting from one.</param>
            public ScriptedHttpMessageHandler(Func<string, int, HttpResponseMessage> response)
            {
                this.response = response;
            }

            /// <summary>
            /// Gets the number of times a path ending with the specified suffix has been requested.
            /// </summary>
            /// <param name="suffix">The path suffix, compared ignoring case.</param>
            /// <returns>The number of matching requests.</returns>
            public int Count(string suffix)
            {
                int result = 0;
                foreach (System.Collections.Generic.KeyValuePair<string, int> keyValuePair in counts_ByPath)
                {
                    if (keyValuePair.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        result += keyValuePair.Value;
                    }
                }

                return result;
            }

            /// <summary>
            /// Gets every request answered, in order: its path and query, and its body when it had one.
            /// </summary>
            public ConcurrentQueue<(string PathAndQuery, string? Body)> Requests { get; } = new();

            /// <inheritdoc />
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? string.Empty;
                int count = counts_ByPath.AddOrUpdate(path, 1, (key, value) => value + 1);

                // The DiGi clients gzip their bodies, so a compressed one is unpacked before it is recorded.
                string? body = null;
                if (request.Content is not null)
                {
                    byte[] bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                    if (request.Content.Headers.ContentEncoding.Contains("gzip"))
                    {
                        using System.IO.MemoryStream memoryStream = new(bytes);
                        using System.IO.Compression.GZipStream gZipStream = new(memoryStream, System.IO.Compression.CompressionMode.Decompress);
                        using System.IO.StreamReader streamReader = new(gZipStream);
                        body = await streamReader.ReadToEndAsync(cancellationToken);
                    }
                    else
                    {
                        body = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                }

                Requests.Enqueue((request.RequestUri?.PathAndQuery ?? string.Empty, body));

                return response(path, count);
            }
        }
    }
}
