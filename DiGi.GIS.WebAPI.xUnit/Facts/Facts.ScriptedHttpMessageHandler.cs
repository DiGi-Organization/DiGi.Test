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

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? string.Empty;
                int count = counts_ByPath.AddOrUpdate(path, 1, (key, value) => value + 1);

                return Task.FromResult(response(path, count));
            }
        }
    }
}
