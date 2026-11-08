using DiGi.GIS.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.WebAPI.Classes;
using DiGi.WebAPI.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Modify.ExportPredictionImagesAsync(DiGi.GIS.WebAPI.Classes.GISWebAPIManager?, int, string?, int, bool, System.Collections.Generic.IEnumerable{string}?, System.Threading.CancellationToken)"/> properly validates input parameters and returns false when handed invalid arguments.
        /// </summary>
        [Fact]
        public async Task ExportPredictionImages_Validation()
        {
            GISWebAPIManager gISWebAPIManager = new(null);

            bool result_NullManager = await Modify.ExportPredictionImagesAsync(null, 1, "test");
            Assert.False(result_NullManager);

            bool result_InvalidCounty = await gISWebAPIManager.ExportPredictionImagesAsync(0, "test");
            Assert.False(result_InvalidCounty);

            bool result_InvalidDir = await gISWebAPIManager.ExportPredictionImagesAsync(1, "   ");
            Assert.False(result_InvalidDir);
        }

        /// <summary>
        /// Verifies that database-sourced prediction image export (<see cref="Modify.ExportPredictionImagesAsync(DiGi.GIS.WebAPI.Classes.GISWebAPIManager?, int, string?, int, bool, System.Collections.Generic.IEnumerable{string}?, System.Threading.CancellationToken)"/>) and file-based prediction image export (<see cref="GIS.UI.Modify.WriteImages(DiGi.GIS.Classes.OrtoDatas?, string?, bool, List{DiGi.Geometry.Planar.Classes.Point2D}?, List{DiGi.Geometry.Planar.Classes.Point2D}?)"/>) produce byte-identical JPEG outputs when processing real orthophoto payloads loaded from test fixtures.
        /// </summary>
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task ExportPredictionImages_ByteParity()
        {
            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoDatas_BoundingBox2D_OrtoDatas.json");
            Assert.False(string.IsNullOrWhiteSpace(path_Fixture));
            Assert.True(File.Exists(path_Fixture));

            GIS.Classes.OrtoDatas? ortoDatas = Core.Convert.ToDiGi<GIS.Classes.OrtoDatas>((Core.Classes.Path)path_Fixture!)?.FirstOrDefault();
            Assert.NotNull(ortoDatas);
            Assert.NotEmpty(ortoDatas);

            string? path_ReportsDir = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(path_ReportsDir));

            string directory_YoloExport = Path.Combine(path_ReportsDir!, "ByteParity_YOLO");
            string directory_UIExport = Path.Combine(path_ReportsDir!, "ByteParity_UI");

            if (Directory.Exists(directory_YoloExport))
            {
                Directory.Delete(directory_YoloExport, true);
            }
            if (Directory.Exists(directory_UIExport))
            {
                Directory.Delete(directory_UIExport, true);
            }

            Directory.CreateDirectory(directory_YoloExport);
            Directory.CreateDirectory(directory_UIExport);

            // 1. Export prediction images via DiGi.GIS.UI prediction export pipeline
            bool result_UI = GIS.UI.Modify.WriteImages(ortoDatas, directory_UIExport);
            Assert.True(result_UI);

            // 2. Export prediction images via DiGi.GIS.YOLO database export pipeline using stubbed WebAPI response
            string json_References = Core.Convert.ToSystem_String(
                new List<OrtoDatasReference> { new() { Reference = ortoDatas.Reference, CountyId = 1 } }) ?? string.Empty;

            string json_Item = Core.Convert.ToSystem_String((Core.Interfaces.ISerializableObject)ortoDatas) ?? string.Empty;
            StubHttpClientFactory stubHttpClientFactory = new((request) =>
            {
                string requestUrl = request.RequestUri?.ToString() ?? string.Empty;
                string responseJson = requestUrl.Contains("itembyreference", StringComparison.OrdinalIgnoreCase)
                    ? json_Item
                    : json_References;

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
                };
            });

            GISWebAPIManager gISWebAPIManager = new(stubHttpClientFactory);
            bool result_YOLO = await gISWebAPIManager.ExportPredictionImagesAsync(1, directory_YoloExport, resume: false);
            Assert.True(result_YOLO);

            // 3. Compare exported JPEG files byte-for-byte across both pipelines
            string[] files_Yolo = Directory.GetFiles(directory_YoloExport, "*.jpeg");
            string[] files_UI = Directory.GetFiles(directory_UIExport, "*.jpeg");

            Assert.NotEmpty(files_Yolo);
            Assert.Equal(files_Yolo.Length, files_UI.Length);

            foreach (string file_Yolo in files_Yolo)
            {
                string fileName = Path.GetFileName(file_Yolo);
                string file_UI = Path.Combine(directory_UIExport, fileName);

                Assert.True(File.Exists(file_UI), $"Corresponding UI export file missing: {fileName}");

                byte[] bytes_Yolo = File.ReadAllBytes(file_Yolo);
                byte[] bytes_UI = File.ReadAllBytes(file_UI);

                Assert.Equal(bytes_Yolo.Length, bytes_UI.Length);
                Assert.Equal(bytes_Yolo, bytes_UI);
            }
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

            public StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                this.handler = handler;
            }

            public HttpClient CreateClient(string name)
            {
                return new HttpClient(new StubHttpMessageHandler(handler))
                {
                    BaseAddress = new Uri("http://localhost/")
                };
            }
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                this.handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(handler(request));
            }
        }
    }
}
