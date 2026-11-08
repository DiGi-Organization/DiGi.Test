using DiGi.GIS.Classes;
using DiGi.GIS.WebAPI.Classes;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="SerializableObjectsPostOptions"/> properly stores, copies, and serializes the <see cref="SerializableObjectsPostOptions.Key"/> property.
        /// </summary>
        [Fact]
        public void SerializableObjectsPostOptions_Key()
        {
            SerializableObjectsPostOptions serializableObjectsPostOptions_Default = new();
            Assert.Null(serializableObjectsPostOptions_Default.Key);

            serializableObjectsPostOptions_Default.Key = "test-auth-token";
            Assert.Equal("test-auth-token", serializableObjectsPostOptions_Default.Key);

            SerializableObjectsPostOptions serializableObjectsPostOptions_Copy = new(serializableObjectsPostOptions_Default);
            Assert.Equal("test-auth-token", serializableObjectsPostOptions_Copy.Key);
            Assert.Equal(serializableObjectsPostOptions_Default.BatchMemorySize, serializableObjectsPostOptions_Copy.BatchMemorySize);

            Core.xUnit.Query.SerializationCheck(serializableObjectsPostOptions_Default);
        }

        /// <summary>
        /// Verifies that <see cref="GISWebAPIManager"/> configures authorization headers on created <see cref="HttpClient"/> instances.
        /// </summary>
        [Fact]
        public void GISWebAPIManager_Key_Header_Configuration()
        {
            ServiceCollection serviceCollection = new();
            serviceCollection.AddHttpClient("TestClient");
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            IHttpClientFactory httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            GISWebAPIManager gISWebAPIManager_NoKey = new(httpClientFactory);
            Assert.Null(gISWebAPIManager_NoKey.Key);
            HttpClient? httpClient_NoKey = gISWebAPIManager_NoKey.CreateHttpClient("TestClient");
            Assert.NotNull(httpClient_NoKey);
            Assert.False(httpClient_NoKey.DefaultRequestHeaders.Contains("key"));

            GISWebAPIManager gISWebAPIManager_WithKey = new(httpClientFactory, "secret-bearer-key");
            Assert.Equal("secret-bearer-key", gISWebAPIManager_WithKey.Key);
            HttpClient? httpClient_WithKey = gISWebAPIManager_WithKey.CreateHttpClient("TestClient");
            Assert.NotNull(httpClient_WithKey);
            Assert.True(httpClient_WithKey.DefaultRequestHeaders.Contains("key"));
            Assert.Equal("secret-bearer-key", System.Linq.Enumerable.FirstOrDefault(httpClient_WithKey.DefaultRequestHeaders.GetValues("key")));

            gISWebAPIManager_WithKey.Key = "updated-key";
            HttpClient? httpClient_Updated = gISWebAPIManager_WithKey.CreateHttpClient("TestClient");
            Assert.NotNull(httpClient_Updated);
            Assert.True(httpClient_Updated.DefaultRequestHeaders.Contains("key"));
            Assert.Equal("updated-key", System.Linq.Enumerable.FirstOrDefault(httpClient_Updated.DefaultRequestHeaders.GetValues("key")));
        }

        /// <summary>
        /// Verifies that background post tasks expose the <see cref="SerializableObjectsPostTask{T}.Key"/> property and propagate it to options.
        /// </summary>
        [Fact]
        public void SerializableObjectsPostTask_Key_Propagation()
        {
            ServiceCollection serviceCollection = new();
            serviceCollection.AddHttpClient(Constants.Name.Client.GIS);
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
            IHttpClientFactory httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            GISWebAPIManager gISWebAPIManager = new(httpClientFactory);

            Building2DsPostTask building2DsPostTask = new(gISWebAPIManager);
            Assert.Null(building2DsPostTask.Key);

            building2DsPostTask.Key = "task-token-123";
            Assert.Equal("task-token-123", building2DsPostTask.Key);
            Assert.Equal("task-token-123", building2DsPostTask.SerializableObjectsPostOptions.Key);

            building2DsPostTask.SerializableObjectsPostOptions.Key = "direct-token-456";
            Assert.Equal("direct-token-456", building2DsPostTask.Key);
        }

        /// <summary>
        /// Verifies that <see cref="Modify.UpdateItemsAsync(HttpClient, string?, string?, DiGi.WebAPI.Classes.PostOptions?, string?)"/> attaches the authorization key to request headers.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UpdateItemsAsync_AttachesKeyHeader()
        {
            string? capturedHeaderKey = null;
            HttpMessageHandlerStub httpMessageHandlerStub = new((HttpRequestMessage httpRequestMessage) =>
            {
                if (httpRequestMessage.Headers.TryGetValues("key", out System.Collections.Generic.IEnumerable<string>? values))
                {
                    capturedHeaderKey = System.Linq.Enumerable.FirstOrDefault(values);
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });

            using HttpClient httpClient = new(httpMessageHandlerStub);
            bool result = await httpClient.UpdateItemsAsync("https://example.local/test", "[]", key: "custom-api-key");
            Assert.True(result);
            Assert.Equal("custom-api-key", capturedHeaderKey);
        }

        private sealed class HttpMessageHandlerStub : HttpMessageHandler
        {
            private readonly System.Func<HttpRequestMessage, HttpResponseMessage> handler;

            public HttpMessageHandlerStub(System.Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                this.handler = handler;
            }

            protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                return System.Threading.Tasks.Task.FromResult(handler(request));
            }
        }
    }
}
