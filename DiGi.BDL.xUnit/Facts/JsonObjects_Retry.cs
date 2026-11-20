using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.BDL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// <c>Create.JsonObjects</c> against a local HTTP server that answers like BDL (DiGi-Organization/DiGi.GIS.PostgreSQL#149): a 429 with a short <c>Retry-After</c> written the BDL way ("1 sek") is waited for and the page requested again, and the following page is still followed; a 429 whose wait is a 7-day quota ("2752 sek") fails at once without waiting; the GUS security block page (HTML, HTTP 500) fails at once; the X-ClientId header is sent.
        /// </summary>
        [MediumFact]
        public async Task JsonObjects_Retry()
        {
            int port;
            using (TcpListener tcpListener = new(IPAddress.Loopback, 0))
            {
                tcpListener.Start();
                port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                tcpListener.Stop();
            }

            string prefix = $"http://localhost:{port}/";
            using HttpListener httpListener = new();
            httpListener.Prefixes.Add(prefix);
            httpListener.Start();

            List<string> requests = [];
            Queue<(int StatusCode, string? RetryAfter, string Body)> responses = new();
            Task server = Task.Run(async () =>
            {
                while (httpListener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await httpListener.GetContextAsync();
                    }
                    catch (HttpListenerException)
                    {
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }

                    lock (requests)
                    {
                        requests.Add(context.Request.Url!.PathAndQuery + " | " + context.Request.Headers["X-ClientId"]);
                    }

                    (int statusCode, string? retryAfter, string body) = responses.Count > 0 ? responses.Dequeue() : (404, null, "{}");
                    context.Response.StatusCode = statusCode;
                    if (retryAfter is not null)
                    {
                        context.Response.Headers["Retry-After"] = retryAfter;
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(body);
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
            });

            try
            {
                // A short wait is waited for; the next page is followed after the retry.
                responses.Enqueue((429, "1 sek", "{\"errorResult\":\"limit\"}"));
                responses.Enqueue((200, null, "{\"results\":[{\"id\":\"A\"}],\"links\":{\"next\":\"" + prefix + "data?page=1\"}}"));
                responses.Enqueue((200, null, "{\"results\":[{\"id\":\"B\"}]}"));

                Stopwatch stopwatch = Stopwatch.StartNew();
                List<JsonObject>? jsonObjects = await Create.JsonObjects(prefix + "data?format=json", clientId: "xunit-client");
                stopwatch.Stop();

                Assert.NotNull(jsonObjects);
                Assert.Equal(2, jsonObjects.Count);
                Assert.True(stopwatch.Elapsed >= TimeSpan.FromSeconds(1), $"the retry did not wait: {stopwatch.Elapsed}");
                Assert.Equal(3, requests.Count);
                Assert.All(requests, x => Assert.EndsWith("| xunit-client", x));

                // A quota wait far over the limit fails at once.
                requests.Clear();
                responses.Enqueue((429, "2752 sek", "{\"errorResult\":\"Przekroczono limit\"}"));
                stopwatch.Restart();
                Assert.Null(await Create.JsonObjects(prefix + "data?format=json"));
                Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"the quota wait was waited for: {stopwatch.Elapsed}");
                Assert.Single(requests);

                // The security block page fails at once.
                requests.Clear();
                responses.Enqueue((500, null, "<!DOCTYPE html><html><head><title>Strona została zablokowana ze względów bezpieczeństwa</title></head></html>"));
                Assert.Null(await Create.JsonObjects(prefix + "data?format=json"));
                Assert.Single(requests);

                // Retries run out: three 429s after the first request leave the page failed.
                requests.Clear();
                for (int i = 0; i < 4; i++)
                {
                    responses.Enqueue((429, "0 sek", "{}"));
                }

                Assert.Null(await Create.JsonObjects(prefix + "data?format=json", retryCount: 3));
                Assert.Equal(4, requests.Count);
            }
            finally
            {
                httpListener.Stop();
                await server;
            }
        }
    }
}
