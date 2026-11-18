using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.PostgreSQL.UI.Classes;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Query.TokenExpiresAt"/> and <see cref="Query.TokenString"/>, the two readers a client-side solve renews its session by (DiGi.GIS.PostgreSQL.UI#48): the expiry comes from the <c>exp</c> claim of a base64url payload without padding, including one holding <c>-</c> and <c>_</c>; anything that is not a three-part token with a numeric <c>exp</c> is null. The token comes from a <c>token</c> property in any case; a body that is not an object, a non-string token or a blank one is null.
        /// </summary>
        [Fact]
        public void UserSession_TokenReaders()
        {
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(1_900_000_000);
            Assert.Equal(dateTimeOffset, Query.TokenExpiresAt(SessionToken(dateTimeOffset)));

            // Six '~' and six '?' hold a base64-aligned "~~~" (fn5+) and "???" (Pz8/) wherever they start, so the payload carries '-' and '_' once made url-safe.
            string tokenString_UrlSafe = SessionToken(dateTimeOffset, "~~~~~~??????");
            Assert.Contains(tokenString_UrlSafe.Split('.')[1], x => x == '-' || x == '_');
            Assert.Equal(dateTimeOffset, Query.TokenExpiresAt(tokenString_UrlSafe));

            Assert.Null(Query.TokenExpiresAt(null));
            Assert.Null(Query.TokenExpiresAt("a.b"));
            Assert.Null(Query.TokenExpiresAt("a.!!!.c"));
            Assert.Null(Query.TokenExpiresAt("a." + Base64Url("{\"exp\":\"soon\"}") + ".c"));
            Assert.Null(Query.TokenExpiresAt("a." + Base64Url("[1]") + ".c"));

            Assert.Equal("T", Query.TokenString("{\"token\":\"T\"}"));
            Assert.Equal("T", Query.TokenString("{\"Token\":\"T\"}"));
            Assert.Null(Query.TokenString("[\"T\"]"));
            Assert.Null(Query.TokenString("{\"token\":1}"));
            Assert.Null(Query.TokenString("{\"token\":\" \"}"));
            Assert.Null(Query.TokenString("not json"));
            Assert.Null(Query.TokenString(null));
        }

        /// <summary>
        /// Verifies the sign-in and renewal requests against a scripted host, recording them because the login answers 401 for every kind of refusal (<c>Coding - WebAPI Contracts.md</c> §4): <see cref="Query.UserTokenStringAsync"/> posts <c>{"Email":…,"Password":…}</c> to <c>/user/login</c> with a length, not chunked, and no bearer; <see cref="Query.RefreshedUserTokenStringAsync"/> posts to <c>/user/session/refresh</c> with the token as a bearer on the request. A 200 gives the token of its body, a 401 gives null, and blank credentials send nothing.
        /// </summary>
        [Fact]
        public async Task UserSession_SignInAndRefresh()
        {
            using SessionHttpMessageHandler sessionHttpMessageHandler = new((path, count) => count == 1 ? Response(HttpStatusCode.OK, path.EndsWith("/refresh", StringComparison.Ordinal) ? "{\"token\":\"T2\"}" : "{\"token\":\"T1\"}") : new HttpResponseMessage(HttpStatusCode.Unauthorized));
            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SessionManager(sessionHttpMessageHandler);
            using (serviceProvider)
            {
                Assert.Null(await gISWebAPIManager.UserTokenStringAsync(" ", "p"));
                Assert.Null(await gISWebAPIManager.UserTokenStringAsync("xunit@example.com", string.Empty));
                Assert.Empty(sessionHttpMessageHandler.Requests);

                Assert.Equal("T1", await gISWebAPIManager.UserTokenStringAsync("xunit@example.com", "p \"w"));
                Assert.Null(await gISWebAPIManager.UserTokenStringAsync("xunit@example.com", "wrong"));

                Assert.Equal("T2", await gISWebAPIManager.RefreshedUserTokenStringAsync("T1"));
                Assert.Null(await gISWebAPIManager.RefreshedUserTokenStringAsync("T1"));
            }

            List<SessionRequest> sessionRequests = sessionHttpMessageHandler.Requests;
            Assert.Equal(4, sessionRequests.Count);

            SessionRequest sessionRequest_Login = sessionRequests[0];
            Assert.Equal("POST", sessionRequest_Login.Method);
            Assert.Equal("/user/login", sessionRequest_Login.PathAndQuery);
            Assert.Null(sessionRequest_Login.Authorization);
            Assert.NotNull(sessionRequest_Login.ContentLength);
            JsonObject? jsonObject = JsonNode.Parse(sessionRequest_Login.Body!) as JsonObject;
            Assert.NotNull(jsonObject);
            Assert.Equal(new string[] { "Email", "Password" }, jsonObject.Select(x => x.Key));
            Assert.Equal("xunit@example.com", (string?)jsonObject["Email"]);
            Assert.Equal("p \"w", (string?)jsonObject["Password"]);

            SessionRequest sessionRequest_Refresh = sessionRequests[2];
            Assert.Equal("POST", sessionRequest_Refresh.Method);
            Assert.Equal("/user/session/refresh", sessionRequest_Refresh.PathAndQuery);
            Assert.Equal("Bearer T1", sessionRequest_Refresh.Authorization);
        }

        /// <summary>
        /// Verifies that a write of <see cref="WebAPIBuildingSolarRadiationSolveTask"/> no longer renews the session itself (DiGi.GIS.PostgreSQL.UI#52): a token still valid - even one within <see cref="Constants.UserSession.RefreshMargin"/> of its expiry - is sent as it is, with no refresh request; an expired token fails the write (null) without anything being sent.
        /// <para>The renewal is the loop's alone (<see cref="WebAPIBuildingSolarRadiationSolveTask_RenewsSession"/>), so no two renewals race.</para>
        /// </summary>
        [Fact]
        public async Task WebAPIBuildingSolarRadiationSolveTask_WriteToken()
        {
            string tokenString_Due = SessionToken(DateTimeOffset.UtcNow.AddMinutes(1));
            string tokenString_Expired = SessionToken(DateTimeOffset.UtcNow.AddMinutes(-1));

            using SessionHttpMessageHandler sessionHttpMessageHandler = new((path, count) => path.EndsWith("/refresh", StringComparison.Ordinal) ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Response(HttpStatusCode.OK, "1"));

            List<BuildingSolarRadiationResult> buildingSolarRadiationResults = [new BuildingSolarRadiationResult("XUNIT-R", 5, 1, 50, 1, WeatherSourceType.EPW, "7", null, null, null, null, null)];

            (GISWebAPIManager gISWebAPIManager, ServiceProvider serviceProvider) = SessionManager(sessionHttpMessageHandler);
            using (serviceProvider)
            {
                ProbeSessionWebAPIBuildingSolarRadiationSolveTask probeSessionWebAPIBuildingSolarRadiationSolveTask = new(gISWebAPIManager) { TokenString = tokenString_Due };

                Assert.Equal(1, await probeSessionWebAPIBuildingSolarRadiationSolveTask.WriteAsync(buildingSolarRadiationResults));
                Assert.Equal(tokenString_Due, probeSessionWebAPIBuildingSolarRadiationSolveTask.TokenString);

                probeSessionWebAPIBuildingSolarRadiationSolveTask.TokenString = tokenString_Expired;
                Assert.Null(await probeSessionWebAPIBuildingSolarRadiationSolveTask.WriteAsync(buildingSolarRadiationResults));
            }

            SessionRequest sessionRequest = Assert.Single(sessionHttpMessageHandler.Requests);
            Assert.Contains("/setsolarradiations?", sessionRequest.PathAndQuery, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Bearer " + tokenString_Due, sessionRequest.Authorization);
        }

        /// <summary>
        /// Verifies the renewal loop <see cref="WebAPIBuildingSolarRadiationSolveTask"/> runs beside its solve (DiGi.GIS.PostgreSQL.UI#52): a token within <see cref="Constants.UserSession.RefreshMargin"/> of its expiry is exchanged for a new one while the run is still reading - before any write - with the old token as the bearer; a token far from its expiry is not renewed; an expired token whose renewal is refused is tried once and the loop stops, since no retry can succeed.
        /// <para>The probe holds the run in its first read until the loop has had its chance, then ends it; a batch that solves for longer than a token lives is the case this covers, as the write no longer renews.</para>
        /// </summary>
        [Fact]
        public async Task WebAPIBuildingSolarRadiationSolveTask_RenewsSession()
        {
            string tokenString_Due = SessionToken(DateTimeOffset.UtcNow.AddMinutes(1));
            string tokenString_New = SessionToken(DateTimeOffset.UtcNow.AddHours(1), "new");
            string tokenString_Expired = SessionToken(DateTimeOffset.UtcNow.AddMinutes(-1));

            // Due: renewed while the run reads.
            using SessionHttpMessageHandler sessionHttpMessageHandler_Due = new((path, count) => path.EndsWith("/refresh", StringComparison.Ordinal) && count == 1 ? Response(HttpStatusCode.OK, "{\"token\":\"" + tokenString_New + "\"}") : new HttpResponseMessage(HttpStatusCode.Unauthorized));
            (GISWebAPIManager gISWebAPIManager_Due, ServiceProvider serviceProvider_Due) = SessionManager(sessionHttpMessageHandler_Due);
            using (serviceProvider_Due)
            {
                ProbeSessionWebAPIBuildingSolarRadiationSolveTask probeSessionWebAPIBuildingSolarRadiationSolveTask = new(gISWebAPIManager_Due) { TokenString = tokenString_Due };
                probeSessionWebAPIBuildingSolarRadiationSolveTask.Holding = () => Until(() => probeSessionWebAPIBuildingSolarRadiationSolveTask.TokenString != tokenString_Due);

                Assert.False(await probeSessionWebAPIBuildingSolarRadiationSolveTask.RunAsync());
                Assert.Equal(tokenString_New, probeSessionWebAPIBuildingSolarRadiationSolveTask.TokenString);
            }

            SessionRequest sessionRequest_Due = Assert.Single(sessionHttpMessageHandler_Due.Requests);
            Assert.Equal("/user/session/refresh", sessionRequest_Due.PathAndQuery);
            Assert.Equal("Bearer " + tokenString_Due, sessionRequest_Due.Authorization);

            // Fresh: not renewed.
            using SessionHttpMessageHandler sessionHttpMessageHandler_Fresh = new((path, count) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
            (GISWebAPIManager gISWebAPIManager_Fresh, ServiceProvider serviceProvider_Fresh) = SessionManager(sessionHttpMessageHandler_Fresh);
            using (serviceProvider_Fresh)
            {
                ProbeSessionWebAPIBuildingSolarRadiationSolveTask probeSessionWebAPIBuildingSolarRadiationSolveTask = new(gISWebAPIManager_Fresh) { TokenString = tokenString_New };
                probeSessionWebAPIBuildingSolarRadiationSolveTask.Holding = () => Task.Delay(TimeSpan.FromMilliseconds(100));

                Assert.False(await probeSessionWebAPIBuildingSolarRadiationSolveTask.RunAsync());
                Assert.Equal(tokenString_New, probeSessionWebAPIBuildingSolarRadiationSolveTask.TokenString);
            }

            Assert.Empty(sessionHttpMessageHandler_Fresh.Requests);

            // Expired and refused: one try, then the loop stops.
            using SessionHttpMessageHandler sessionHttpMessageHandler_Expired = new((path, count) => new HttpResponseMessage(HttpStatusCode.Unauthorized));
            (GISWebAPIManager gISWebAPIManager_Expired, ServiceProvider serviceProvider_Expired) = SessionManager(sessionHttpMessageHandler_Expired);
            using (serviceProvider_Expired)
            {
                ProbeSessionWebAPIBuildingSolarRadiationSolveTask probeSessionWebAPIBuildingSolarRadiationSolveTask = new(gISWebAPIManager_Expired) { TokenString = tokenString_Expired };
                probeSessionWebAPIBuildingSolarRadiationSolveTask.Holding = async () =>
                {
                    await Until(() => sessionHttpMessageHandler_Expired.Requests.Count != 0);
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                };

                Assert.False(await probeSessionWebAPIBuildingSolarRadiationSolveTask.RunAsync());
                Assert.Equal(tokenString_Expired, probeSessionWebAPIBuildingSolarRadiationSolveTask.TokenString);
            }

            Assert.Equal("/user/session/refresh", Assert.Single(sessionHttpMessageHandler_Expired.Requests).PathAndQuery);

            // Polls until the condition holds, for at most a second - the loop's first check runs as the run starts.
            static async Task Until(Func<bool> condition)
            {
                for (int i = 0; i < 100 && !condition(); i++)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }
        }

        private static string Base64Url(string text)
        {
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static string SessionToken(DateTimeOffset expiresAt, string subject = "xunit")
        {
            JsonObject jsonObject = new()
            {
                ["sub"] = subject,
                ["exp"] = expiresAt.ToUnixTimeSeconds()
            };

            return Base64Url("{\"alg\":\"none\"}") + "." + Base64Url(jsonObject.ToJsonString()) + ".signature";
        }

        private static (GISWebAPIManager GISWebAPIManager, ServiceProvider ServiceProvider) SessionManager(SessionHttpMessageHandler sessionHttpMessageHandler)
        {
            ServiceCollection serviceCollection = new();
            serviceCollection.AddHttpClient(WebAPI.Constants.Name.Client.GIS, httpClient => httpClient.BaseAddress = new Uri("https://localhost/")).ConfigurePrimaryHttpMessageHandler(() => sessionHttpMessageHandler);
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            return (new GISWebAPIManager(serviceProvider.GetRequiredService<IHttpClientFactory>()), serviceProvider);
        }

        /// <summary>
        /// One request a <see cref="SessionHttpMessageHandler"/> answered.
        /// </summary>
        /// <param name="Method">The HTTP method.</param>
        /// <param name="PathAndQuery">The path and query.</param>
        /// <param name="Authorization">The <c>Authorization</c> header, or null.</param>
        /// <param name="ContentLength">The declared length of the body, or null when it was sent chunked or without one.</param>
        /// <param name="Body">The body, or null.</param>
        private sealed record SessionRequest(string Method, string PathAndQuery, string? Authorization, long? ContentLength, string? Body);

        /// <summary>
        /// Exposes the protected write and run of <see cref="WebAPIBuildingSolarRadiationSolveTask"/> to the facts. A run is held in its first read (the EPW stations) for as long as <see cref="Holding"/> takes, then ends there - no station - so the renewal loop that runs beside it can be observed without a solve.
        /// </summary>
        private sealed class ProbeSessionWebAPIBuildingSolarRadiationSolveTask : WebAPIBuildingSolarRadiationSolveTask
        {
            public ProbeSessionWebAPIBuildingSolarRadiationSolveTask(GISWebAPIManager gISWebAPIManager)
                : base(gISWebAPIManager)
            {
                PostgreSQLBuildingSolarRadiationSolveOptions = new Solar.Classes.PostgreSQLBuildingSolarRadiationSolveOptions() { Radii = [50] };
            }

            public Func<Task>? Holding { get; set; }

            public Task<bool> RunAsync()
            {
                return ExecuteAsync(new Progress<long>(), CancellationToken.None);
            }

            public Task<int?> WriteAsync(List<BuildingSolarRadiationResult> buildingSolarRadiationResults)
            {
                return WriteAsync(5, buildingSolarRadiationResults, 30, CancellationToken.None);
            }

            protected override async Task<List<(int Id, double X, double Y)>?> ReadEPWFileLocationsAsync(int commandTimeout, CancellationToken cancellationToken)
            {
                if (Holding is not null)
                {
                    await Holding();
                }

                return null;
            }
        }

        /// <summary>
        /// Answers every request from a script of its path and the number of times that path was asked for, counting from one, and records method, path, bearer, declared length and body.
        /// </summary>
        private sealed class SessionHttpMessageHandler : HttpMessageHandler
        {
            private readonly ConcurrentDictionary<string, int> counts = new();
            private readonly ConcurrentQueue<SessionRequest> requests = new();
            private readonly Func<string, int, HttpResponseMessage> response;

            public SessionHttpMessageHandler(Func<string, int, HttpResponseMessage> response)
            {
                this.response = response;
            }

            public List<SessionRequest> Requests => [.. requests];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string path = request.RequestUri?.AbsolutePath ?? string.Empty;
                string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                requests.Enqueue(new SessionRequest(request.Method.Method, request.RequestUri?.PathAndQuery ?? string.Empty, request.Headers.Authorization?.ToString(), request.Content?.Headers.ContentLength, body));
                return response(path, counts.AddOrUpdate(path, 1, (_, x) => x + 1));
            }
        }
    }
}
