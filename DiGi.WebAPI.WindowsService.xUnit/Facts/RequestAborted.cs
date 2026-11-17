using DiGi.WebAPI.WindowsService.Modify;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.WebAPI.WindowsService.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// A client that closes its TLS socket mid-request cancels the action's token within 1 s, through the pipeline the service runs (DiGi-Organization/DiGi.GIS.WebAPI#77).
        /// <para>The client sends no TLS close_notify, as the aborting client measured on api.digiproject.uk did: it shuts the socket down and closes it.</para>
        /// </summary>
        [MediumFact]
        public async Task RequestAborted_Https()
        {
            double? milliseconds = await RequestAbortedMillisecondsAsync(useHttps: true);

            Assert.NotNull(milliseconds);
            Assert.True(milliseconds < 1000, $"The token was cancelled {milliseconds} ms after the client closed its socket");
        }

        /// <summary>
        /// A client that closes its plain HTTP socket mid-request cancels the action's token within 1 s, through the pipeline the service runs (DiGi-Organization/DiGi.GIS.WebAPI#77).
        /// </summary>
        [MediumFact]
        public async Task RequestAborted_Http()
        {
            double? milliseconds = await RequestAbortedMillisecondsAsync(useHttps: false);

            Assert.NotNull(milliseconds);
            Assert.True(milliseconds < 1000, $"The token was cancelled {milliseconds} ms after the client closed its socket");
        }

        // Starts Kestrel on a loopback port with the service's own pipeline (Modify.ConfigurePipeline), sends one GET to
        // RequestAbortedControllerFixture over a raw socket, closes the socket once the action is waiting, and answers
        // how many milliseconds after the close the action saw its token cancelled - null when it never did.
        private static async Task<double?> RequestAbortedMillisecondsAsync(bool useHttps)
        {
            using X509Certificate2 x509Certificate2 = SelfSignedCertificate();

            WebApplicationBuilder webApplicationBuilder = WebApplication.CreateBuilder(new WebApplicationOptions() { EnvironmentName = Environments.Production });
            webApplicationBuilder.WebHost.ConfigureKestrel(kestrelServerOptions =>
            {
                kestrelServerOptions.Listen(IPAddress.Loopback, 0, listenOptions =>
                {
                    if (useHttps)
                    {
                        listenOptions.UseHttps(x509Certificate2);
                    }
                });
            });

            // The services the pipeline's middleware needs; the service registers the same (Program.ConfigureServicesAsync).
            IServiceCollection serviceCollection = webApplicationBuilder.Services;
            serviceCollection.AddSingleton<RequestAbortedStateFixture>();
            serviceCollection.AddProblemDetails();
            serviceCollection.AddCors(corsOptions => corsOptions.AddPolicy(Constants.Name.Policy, corsPolicyBuilder => corsPolicyBuilder.AllowAnyOrigin()));
            serviceCollection.AddRequestDecompression();
            serviceCollection.AddControllers().ConfigureApplicationPartManager(applicationPartManager => applicationPartManager.FeatureProviders.Add(new ControllerFeatureProviderFixture([typeof(RequestAbortedControllerFixture)])));
            serviceCollection.AddEndpointsApiExplorer();
            serviceCollection.AddSwaggerGen();

            await using WebApplication webApplication = webApplicationBuilder.Build();
            webApplication.ConfigurePipeline();
            await webApplication.StartAsync();

            string address = webApplication.Services.GetRequiredService<IServer>().Features.GetRequiredFeature<IServerAddressesFeature>().Addresses.First();
            int port = new Uri(address).Port;

            RequestAbortedStateFixture requestAbortedStateFixture = webApplication.Services.GetRequiredService<RequestAbortedStateFixture>();

            long timestamp_Closed;
            using (TcpClient tcpClient = new())
            {
                await tcpClient.ConnectAsync(IPAddress.Loopback, port);

                Stream stream = tcpClient.GetStream();
                if (useHttps)
                {
                    // Trusts exactly the certificate this fact generated, nothing else.
                    SslStream sslStream = new(stream, leaveInnerStreamOpen: true, (sender, certificate, chain, sslPolicyErrors) => certificate is not null && string.Equals(certificate.GetCertHashString(), x509Certificate2.Thumbprint, StringComparison.OrdinalIgnoreCase));
                    await sslStream.AuthenticateAsClientAsync("localhost");
                    stream = sslStream;
                }

                byte[] request = Encoding.ASCII.GetBytes("GET /requestaborted/wait HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(request);
                await stream.FlushAsync();

                await requestAbortedStateFixture.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Task.Delay(200);

                timestamp_Closed = Stopwatch.GetTimestamp();
                tcpClient.Client.Shutdown(SocketShutdown.Both);
                tcpClient.Close();
            }

            long? timestamp_Cancelled = await requestAbortedStateFixture.Ended.Task.WaitAsync(TimeSpan.FromSeconds(40));

            await webApplication.StopAsync();

            if (timestamp_Cancelled is null)
            {
                return null;
            }

            return Stopwatch.GetElapsedTime(timestamp_Closed, timestamp_Cancelled.Value).TotalMilliseconds;
        }

        // A loopback certificate Kestrel can serve. Round-tripped through PFX because SChannel cannot use the ephemeral key
        // CreateSelfSigned returns.
        private static X509Certificate2 SelfSignedCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest certificateRequest = new("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            SubjectAlternativeNameBuilder subjectAlternativeNameBuilder = new();
            subjectAlternativeNameBuilder.AddDnsName("localhost");
            subjectAlternativeNameBuilder.AddIpAddress(IPAddress.Loopback);
            certificateRequest.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());

            using X509Certificate2 x509Certificate2 = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            return X509CertificateLoader.LoadPkcs12(x509Certificate2.Export(X509ContentType.Pfx), null);
        }
    }
}
