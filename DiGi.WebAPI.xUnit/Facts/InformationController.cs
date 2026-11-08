using DiGi.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="ServiceHealthInformation"/>.
        /// </summary>
        [Fact]
        public void ServiceHealthInformation_Serialization()
        {
            ServiceHealthInformation serviceHealthInformation = new("Healthy", DateTime.UtcNow, DateTimeOffset.Now, TimeSpan.FromHours(5), 1234);

            Assert.Equal("Healthy", serviceHealthInformation.Status);
            Assert.Equal(1234, serviceHealthInformation.ProcessId);
            Assert.Equal(TimeSpan.FromHours(5), serviceHealthInformation.Uptime);

            ServiceHealthInformation serviceHealthInformation_Copy = new(serviceHealthInformation);
            Assert.Equal(serviceHealthInformation.Status, serviceHealthInformation_Copy.Status);
            Assert.Equal(serviceHealthInformation.ProcessId, serviceHealthInformation_Copy.ProcessId);
            Assert.Equal(serviceHealthInformation.Uptime, serviceHealthInformation_Copy.Uptime);

            Core.xUnit.Query.SerializationCheck(serviceHealthInformation);
        }

        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="VersionInformation"/>.
        /// </summary>
        [Fact]
        public void VersionInformation_Serialization()
        {
            List<ExtensionVersionInformation> extensions = [
                new("DiGi.GIS.WebAPI", "0.8.7.20260821120000"),
                new("DiGi.GLTF.WebAPI", "0.8.6.20260701120000")
            ];

            VersionInformation versionInformation = new("1.0.0.0", "1.0.0+abc1234", "0.8.8.0", "0.8.8+def5678", "10.0.0", ".NET 10.0.0-rt", DateTime.UtcNow, extensions);

            Assert.Equal("1.0.0.0", versionInformation.ServiceVersion);
            Assert.Equal("1.0.0+abc1234", versionInformation.ServiceInformationalVersion);
            Assert.Equal("0.8.8.0", versionInformation.WebAPIVersion);
            Assert.Equal("0.8.8+def5678", versionInformation.WebAPIInformationalVersion);
            Assert.Equal("10.0.0", versionInformation.RuntimeVersion);
            Assert.Equal(".NET 10.0.0-rt", versionInformation.FrameworkDescription);
            Assert.NotNull(versionInformation.StartTimeUtc);
            Assert.NotNull(versionInformation.Extensions);
            Assert.Equal(2, versionInformation.Extensions.Count());

            VersionInformation versionInformation_Copy = new(versionInformation);
            Assert.Equal(versionInformation.ServiceVersion, versionInformation_Copy.ServiceVersion);
            Assert.Equal(versionInformation.WebAPIVersion, versionInformation_Copy.WebAPIVersion);
            Assert.NotNull(versionInformation_Copy.Extensions);
            Assert.Equal(2, versionInformation_Copy.Extensions.Count());
            Assert.Equal("DiGi.GIS.WebAPI", versionInformation_Copy.Extensions.First().Name);

            Core.xUnit.Query.SerializationCheck(versionInformation);
        }

        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="ExtensionVersionInformation"/>.
        /// </summary>
        [Fact]
        public void ExtensionVersionInformation_Serialization()
        {
            ExtensionVersionInformation extensionVersionInformation = new("DiGi.GIS.WebAPI", "0.8.7.20260821120000");

            Assert.Equal("DiGi.GIS.WebAPI", extensionVersionInformation.Name);
            Assert.Equal("0.8.7.20260821120000", extensionVersionInformation.InformationalVersion);

            ExtensionVersionInformation extensionVersionInformation_Copy = new(extensionVersionInformation);
            Assert.Equal(extensionVersionInformation.Name, extensionVersionInformation_Copy.Name);
            Assert.Equal(extensionVersionInformation.InformationalVersion, extensionVersionInformation_Copy.InformationalVersion);

            Core.xUnit.Query.SerializationCheck(extensionVersionInformation);
        }

        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="EndpointParameterInformation"/>.
        /// </summary>
        [Fact]
        public void EndpointParameterInformation_Serialization()
        {
            EndpointParameterInformation endpointParameterInformation = new("code", "Query", "String", true, false);

            Assert.Equal("code", endpointParameterInformation.Name);
            Assert.Equal("Query", endpointParameterInformation.Source);
            Assert.Equal("String", endpointParameterInformation.TypeName);
            Assert.True(endpointParameterInformation.IsNullable);
            Assert.False(endpointParameterInformation.HasDefaultValue);

            EndpointParameterInformation endpointParameterInformation_Copy = new(endpointParameterInformation);
            Assert.Equal(endpointParameterInformation.Name, endpointParameterInformation_Copy.Name);
            Assert.Equal(endpointParameterInformation.Source, endpointParameterInformation_Copy.Source);
            Assert.Equal(endpointParameterInformation.TypeName, endpointParameterInformation_Copy.TypeName);
            Assert.Equal(endpointParameterInformation.IsNullable, endpointParameterInformation_Copy.IsNullable);
            Assert.Equal(endpointParameterInformation.HasDefaultValue, endpointParameterInformation_Copy.HasDefaultValue);

            Core.xUnit.Query.SerializationCheck(endpointParameterInformation);
        }

        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="EndpointInformation"/>.
        /// </summary>
        [Fact]
        public void EndpointInformation_Serialization()
        {
            List<EndpointParameterInformation> parameters = [
                new("code", "Query", "String", true, false),
                new("id", "Query", "Int32", false, true)
            ];

            EndpointInformation endpointInformation = new("AdministrativeAreal2D", "GetByCodeAsync", "gis/administrativeareal2d/bycode", ["GET"], false, "IActionResult", parameters);

            Assert.Equal("AdministrativeAreal2D", endpointInformation.ControllerName);
            Assert.Equal("GetByCodeAsync", endpointInformation.ActionName);
            Assert.Equal("gis/administrativeareal2d/bycode", endpointInformation.RouteTemplate);
            Assert.False(endpointInformation.IsApiIgnored);
            Assert.Equal("IActionResult", endpointInformation.ReturnTypeName);
            Assert.NotNull(endpointInformation.HttpMethods);
            Assert.Contains("GET", endpointInformation.HttpMethods);
            Assert.NotNull(endpointInformation.Parameters);
            Assert.Equal(2, endpointInformation.Parameters.Count());

            EndpointInformation endpointInformation_Copy = new(endpointInformation);
            Assert.Equal(endpointInformation.ControllerName, endpointInformation_Copy.ControllerName);
            Assert.Equal(endpointInformation.ActionName, endpointInformation_Copy.ActionName);
            Assert.Equal(endpointInformation.RouteTemplate, endpointInformation_Copy.RouteTemplate);
            Assert.Equal(endpointInformation.IsApiIgnored, endpointInformation_Copy.IsApiIgnored);
            Assert.Equal(endpointInformation.ReturnTypeName, endpointInformation_Copy.ReturnTypeName);
            Assert.NotNull(endpointInformation_Copy.Parameters);
            Assert.Equal(2, endpointInformation_Copy.Parameters.Count());

            Core.xUnit.Query.SerializationCheck(endpointInformation);
        }

        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="AssemblyInformation"/>.
        /// </summary>
        [Fact]
        public void AssemblyInformation_Serialization()
        {
            AssemblyInformation assemblyInformation = new("DiGi.WebAPI", "DiGi.WebAPI, Version=0.8.8.0, Culture=neutral, PublicKeyToken=null", "0.8.8.0", "0.8.8+commitsha", false);

            Assert.Equal("DiGi.WebAPI", assemblyInformation.Name);
            Assert.Equal("DiGi.WebAPI, Version=0.8.8.0, Culture=neutral, PublicKeyToken=null", assemblyInformation.FullName);
            Assert.Equal("0.8.8.0", assemblyInformation.Version);
            Assert.Equal("0.8.8+commitsha", assemblyInformation.InformationalVersion);
            Assert.False(assemblyInformation.IsDynamic);

            AssemblyInformation assemblyInformation_Copy = new(assemblyInformation);
            Assert.Equal(assemblyInformation.Name, assemblyInformation_Copy.Name);
            Assert.Equal(assemblyInformation.FullName, assemblyInformation_Copy.FullName);
            Assert.Equal(assemblyInformation.Version, assemblyInformation_Copy.Version);
            Assert.Equal(assemblyInformation.InformationalVersion, assemblyInformation_Copy.InformationalVersion);
            Assert.Equal(assemblyInformation.IsDynamic, assemblyInformation_Copy.IsDynamic);

            Core.xUnit.Query.SerializationCheck(assemblyInformation);
        }

        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="SystemInformation"/>.
        /// </summary>
        [Fact]
        public void SystemInformation_Serialization()
        {
            SystemInformation systemInformation = new("Production", "Microsoft Windows 11", "X64", 16, 1024 * 1024 * 150, 1024 * 1024 * 120, 1024 * 1024 * 50, 5, 2, 0, 32767, 1000);

            Assert.Equal("Production", systemInformation.EnvironmentName);
            Assert.Equal("Microsoft Windows 11", systemInformation.OSVersion);
            Assert.Equal("X64", systemInformation.ProcessArchitecture);
            Assert.Equal(16, systemInformation.ProcessorCount);
            Assert.Equal(1024 * 1024 * 150, systemInformation.MemoryWorkingSetBytes);
            Assert.Equal(1024 * 1024 * 120, systemInformation.MemoryPrivateBytes);
            Assert.Equal(1024 * 1024 * 50, systemInformation.GCTotalMemoryBytes);
            Assert.Equal(5, systemInformation.GCCollectionsGen0);
            Assert.Equal(2, systemInformation.GCCollectionsGen1);
            Assert.Equal(0, systemInformation.GCCollectionsGen2);
            Assert.Equal(32767, systemInformation.ThreadPoolAvailableWorkerThreads);
            Assert.Equal(1000, systemInformation.ThreadPoolAvailableCompletionPortThreads);

            SystemInformation systemInformation_Copy = new(systemInformation);
            Assert.Equal(systemInformation.EnvironmentName, systemInformation_Copy.EnvironmentName);
            Assert.Equal(systemInformation.OSVersion, systemInformation_Copy.OSVersion);
            Assert.Equal(systemInformation.ProcessArchitecture, systemInformation_Copy.ProcessArchitecture);
            Assert.Equal(systemInformation.ProcessorCount, systemInformation_Copy.ProcessorCount);

            Core.xUnit.Query.SerializationCheck(systemInformation);
        }

        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="ControllerInformation"/>.
        /// </summary>
        [Fact]
        public void ControllerInformation_Serialization()
        {
            ControllerInformation controllerInformation = new("InformationController", "DiGi.WebAPI", "0.8.8.0", "0.8.8+commitsha", 6, "[controller]");

            Assert.Equal("InformationController", controllerInformation.Name);
            Assert.Equal("DiGi.WebAPI", controllerInformation.AssemblyName);
            Assert.Equal("0.8.8.0", controllerInformation.Version);
            Assert.Equal("0.8.8+commitsha", controllerInformation.InformationalVersion);
            Assert.Equal(6, controllerInformation.ActionCount);
            Assert.Equal("[controller]", controllerInformation.RoutePrefix);

            ControllerInformation controllerInformation_Copy = new(controllerInformation);
            Assert.Equal(controllerInformation.Name, controllerInformation_Copy.Name);
            Assert.Equal(controllerInformation.AssemblyName, controllerInformation_Copy.AssemblyName);
            Assert.Equal(controllerInformation.Version, controllerInformation_Copy.Version);
            Assert.Equal(controllerInformation.InformationalVersion, controllerInformation_Copy.InformationalVersion);
            Assert.Equal(controllerInformation.ActionCount, controllerInformation_Copy.ActionCount);
            Assert.Equal(controllerInformation.RoutePrefix, controllerInformation_Copy.RoutePrefix);

            Core.xUnit.Query.SerializationCheck(controllerInformation);
        }

        /// <summary>
        /// Tests factory extension methods for creating diagnostic models.
        /// </summary>
        [Fact]
        public void InformationController_Factories()
        {
            ServiceHealthInformation serviceHealthInformation = Create.ServiceHealthInformation("Healthy");
            Assert.NotNull(serviceHealthInformation);
            Assert.Equal("Healthy", serviceHealthInformation.Status);
            Assert.True(serviceHealthInformation.ProcessId > 0);

            VersionInformation versionInformation = Create.VersionInformation();
            Assert.NotNull(versionInformation);
            Assert.False(string.IsNullOrWhiteSpace(versionInformation.RuntimeVersion));
            Assert.False(string.IsNullOrWhiteSpace(versionInformation.FrameworkDescription));

            SystemInformation systemInformation = Create.SystemInformation();
            Assert.NotNull(systemInformation);
            Assert.True(systemInformation.ProcessorCount > 0);
            Assert.True(systemInformation.MemoryWorkingSetBytes > 0);

            AssemblyInformation? assemblyInformation = Create.AssemblyInformation(typeof(Create).Assembly);
            Assert.NotNull(assemblyInformation);
            Assert.Equal("DiGi.WebAPI", assemblyInformation.Name);
            Assert.False(string.IsNullOrWhiteSpace(assemblyInformation.Version));

            ControllerInformation? controllerInformation = Create.ControllerInformation(typeof(InformationController).GetTypeInfo());
            Assert.NotNull(controllerInformation);
            Assert.Equal(nameof(InformationController), controllerInformation.Name);
            Assert.True(controllerInformation.ActionCount >= 5);
        }

        /// <summary>
        /// Tests that <see cref="InformationController"/> diagnostic actions execute and return populated JSON responses.
        /// </summary>
        [Fact]
        public async Task InformationController_ActionExecution()
        {
            ApplicationPartManager applicationPartManager = new();
            applicationPartManager.ApplicationParts.Add(new AssemblyPart(typeof(InformationController).Assembly));
            applicationPartManager.FeatureProviders.Add(new ControllerFeatureProvider());

            // Authorization is denied by default, so the tier is explicitly waived here: this fact
            // covers action execution, while InformationController_TieredAccess covers the gate.
            DiagnosticsConfiguration diagnosticsConfiguration = new(open: true);
            InformationController controller = new(applicationPartManager, diagnosticsConfiguration: diagnosticsConfiguration);

            IActionResult result_Health = await controller.GetServiceHealthInformationAsync();
            Assert.IsType<ContentResult>(result_Health);
            ContentResult contentResult_Health = (ContentResult)result_Health;
            Assert.Equal("application/json", contentResult_Health.ContentType);
            Assert.Contains("Healthy", contentResult_Health.Content);

            IActionResult result_Version = await controller.GetVersionInformationAsync();
            Assert.IsType<ContentResult>(result_Version);
            ContentResult contentResult_Version = (ContentResult)result_Version;
            Assert.Equal("application/json", contentResult_Version.ContentType);
            Assert.False(string.IsNullOrWhiteSpace(contentResult_Version.Content));

            IActionResult result_Assemblies = await controller.GetAssemblyInformationsAsync();
            Assert.IsType<ContentResult>(result_Assemblies);
            ContentResult contentResult_Assemblies = (ContentResult)result_Assemblies;
            Assert.Equal("application/json", contentResult_Assemblies.ContentType);
            Assert.Contains("DiGi.WebAPI", contentResult_Assemblies.Content);

            IActionResult result_System = await controller.GetSystemInformationAsync();
            Assert.IsType<ContentResult>(result_System);
            ContentResult contentResult_System = (ContentResult)result_System;
            Assert.Equal("application/json", contentResult_System.ContentType);
            Assert.False(string.IsNullOrWhiteSpace(contentResult_System.Content));

            IActionResult result_Controllers = await controller.GetControllerInformationsAsync();
            Assert.IsType<ContentResult>(result_Controllers);
            ContentResult contentResult_Controllers = (ContentResult)result_Controllers;
            Assert.Equal("application/json", contentResult_Controllers.ContentType);
            Assert.Contains("InformationController", contentResult_Controllers.Content);
        }

        /// <summary>
        /// Tests serialization, copy constructor, and property values of <see cref="DiagnosticsConfiguration"/>.
        /// </summary>
        [Fact]
        public void DiagnosticsConfiguration_Serialization()
        {
            DiagnosticsConfiguration diagnosticsConfiguration = new("test-mock-key-123", true, false);

            Assert.Equal("test-mock-key-123", diagnosticsConfiguration.Key);
            Assert.True(diagnosticsConfiguration.Enabled);
            Assert.False(diagnosticsConfiguration.Open);

            DiagnosticsConfiguration diagnosticsConfiguration_Copy = new(diagnosticsConfiguration);
            Assert.Equal(diagnosticsConfiguration.Key, diagnosticsConfiguration_Copy.Key);
            Assert.Equal(diagnosticsConfiguration.Enabled, diagnosticsConfiguration_Copy.Enabled);
            Assert.Equal(diagnosticsConfiguration.Open, diagnosticsConfiguration_Copy.Open);

            DiagnosticsConfiguration diagnosticsConfiguration_Open = new(null, false, true);
            Assert.True(diagnosticsConfiguration_Open.Open);
            Assert.True(new DiagnosticsConfiguration(diagnosticsConfiguration_Open).Open);
            Core.xUnit.Query.SerializationCheck(diagnosticsConfiguration_Open);

            Core.xUnit.Query.SerializationCheck(diagnosticsConfiguration);
        }

        /// <summary>
        /// Tests that authorization denies by default and grants only on an exact key match.
        /// <para>Every branch that is not an exact match must deny: a null configuration, disabled
        /// enforcement, a blank configured key and a blank supplied key. Only the explicit Open
        /// opt-out grants access without a key.</para>
        /// </summary>
        [Fact]
        public void DiagnosticsConfiguration_Authorization()
        {
            DiagnosticsConfiguration? diagnosticsConfiguration_Null = null;
            Assert.False(diagnosticsConfiguration_Null.IsAuthorized(null));
            Assert.False(diagnosticsConfiguration_Null.IsAuthorized("any-key"));

            // An unconfigured host denies everything - this is the regression that shipped open.
            DiagnosticsConfiguration diagnosticsConfiguration_Default = new();
            Assert.False(diagnosticsConfiguration_Default.IsAuthorized(null));
            Assert.False(diagnosticsConfiguration_Default.IsAuthorized("any-key"));

            // Enabled=false means "no key check configured", not "let everyone in".
            DiagnosticsConfiguration diagnosticsConfiguration_Disabled = new("test-mock-key", false);
            Assert.False(diagnosticsConfiguration_Disabled.IsAuthorized(null));
            Assert.False(diagnosticsConfiguration_Disabled.IsAuthorized("wrong-key"));
            Assert.False(diagnosticsConfiguration_Disabled.IsAuthorized("test-mock-key"));

            // A misconfiguration must never open the endpoint.
            DiagnosticsConfiguration diagnosticsConfiguration_BlankKey = new(null, true);
            Assert.False(diagnosticsConfiguration_BlankKey.IsAuthorized(null));
            Assert.False(diagnosticsConfiguration_BlankKey.IsAuthorized(""));
            Assert.False(diagnosticsConfiguration_BlankKey.IsAuthorized("any-key"));

            DiagnosticsConfiguration diagnosticsConfiguration_WhitespaceKey = new("   ", true);
            Assert.False(diagnosticsConfiguration_WhitespaceKey.IsAuthorized("   "));

            DiagnosticsConfiguration diagnosticsConfiguration_Enabled = new("test-mock-key", true);
            Assert.False(diagnosticsConfiguration_Enabled.IsAuthorized(null));
            Assert.False(diagnosticsConfiguration_Enabled.IsAuthorized(""));
            Assert.False(diagnosticsConfiguration_Enabled.IsAuthorized("wrong-key"));
            Assert.False(diagnosticsConfiguration_Enabled.IsAuthorized("test-mock-ke"));
            Assert.False(diagnosticsConfiguration_Enabled.IsAuthorized("test-mock-keyy"));
            Assert.False(diagnosticsConfiguration_Enabled.IsAuthorized("TEST-MOCK-KEY"));
            Assert.True(diagnosticsConfiguration_Enabled.IsAuthorized("test-mock-key"));

            // Open is the sole escape hatch, and it works without any key at all.
            DiagnosticsConfiguration diagnosticsConfiguration_Open = new(null, false, true);
            Assert.True(diagnosticsConfiguration_Open.IsAuthorized(null));
            Assert.True(diagnosticsConfiguration_Open.IsAuthorized("anything"));
        }

        /// <summary>
        /// Tests that <see cref="InformationController"/> enforces tiered access when authorization is configured.
        /// </summary>
        [Fact]
        public async Task InformationController_TieredAccess()
        {
            ApplicationPartManager applicationPartManager = new();
            applicationPartManager.ApplicationParts.Add(new AssemblyPart(typeof(InformationController).Assembly));
            applicationPartManager.FeatureProviders.Add(new ControllerFeatureProvider());

            DiagnosticsConfiguration diagnosticsConfiguration = new("test-mock-token", true);
            InformationController controller = new(applicationPartManager, diagnosticsConfiguration: diagnosticsConfiguration);

            // Public endpoints succeed without key
            IActionResult result_Health = await controller.GetServiceHealthInformationAsync();
            Assert.IsType<ContentResult>(result_Health);

            IActionResult result_Version = await controller.GetVersionInformationAsync();
            Assert.IsType<ContentResult>(result_Version);

            IActionResult result_Endpoints_Public = await controller.GetEndpointInformationsAsync(null, includeIgnored: false, key: null);
            // Result is NoContent or Content depending on whether ActionDescriptorProvider was passed, but definitely NOT Unauthorized
            Assert.IsNotType<UnauthorizedResult>(result_Endpoints_Public);

            // Protected endpoints without key return 401 Unauthorized
            IActionResult result_System_NoKey = await controller.GetSystemInformationAsync(key: null);
            Assert.IsType<UnauthorizedResult>(result_System_NoKey);

            IActionResult result_Assemblies_NoKey = await controller.GetAssemblyInformationsAsync(key: null);
            Assert.IsType<UnauthorizedResult>(result_Assemblies_NoKey);

            IActionResult result_Endpoints_Ignored_NoKey = await controller.GetEndpointInformationsAsync(null, includeIgnored: true, key: null);
            Assert.IsType<UnauthorizedResult>(result_Endpoints_Ignored_NoKey);

            // Protected endpoints with invalid key return 401 Unauthorized
            IActionResult result_System_InvalidKey = await controller.GetSystemInformationAsync(key: "invalid-key");
            Assert.IsType<UnauthorizedResult>(result_System_InvalidKey);

            // Protected endpoints with valid key return 200 OK
            IActionResult result_System_ValidKey = await controller.GetSystemInformationAsync(key: "test-mock-token");
            Assert.IsType<ContentResult>(result_System_ValidKey);

            IActionResult result_Assemblies_ValidKey = await controller.GetAssemblyInformationsAsync(key: "test-mock-token");
            Assert.IsType<ContentResult>(result_Assemblies_ValidKey);

            // /controllers names every deployed controller with its route prefix, so it sits in the
            // protected tier rather than the public one.
            IActionResult result_Controllers_NoKey = await controller.GetControllerInformationsAsync(key: null);
            Assert.IsType<UnauthorizedResult>(result_Controllers_NoKey);

            IActionResult result_Controllers_ValidKey = await controller.GetControllerInformationsAsync(key: "test-mock-token");
            Assert.IsType<ContentResult>(result_Controllers_ValidKey);

            // An unconfigured controller must deny the whole protected tier. This is the exact
            // regression that reached production: every one of these answered 200.
            InformationController controller_Unconfigured = new(applicationPartManager, diagnosticsConfiguration: new DiagnosticsConfiguration());

            Assert.IsType<UnauthorizedResult>(await controller_Unconfigured.GetSystemInformationAsync());
            Assert.IsType<UnauthorizedResult>(await controller_Unconfigured.GetAssemblyInformationsAsync());
            Assert.IsType<UnauthorizedResult>(await controller_Unconfigured.GetControllerInformationsAsync());
            Assert.IsType<UnauthorizedResult>(await controller_Unconfigured.GetEndpointInformationsAsync(null, includeIgnored: true));

            // Public tier stays reachable on the same unconfigured host.
            Assert.IsType<ContentResult>(await controller_Unconfigured.GetServiceHealthInformationAsync());
            Assert.IsType<ContentResult>(await controller_Unconfigured.GetVersionInformationAsync());
        }

        /// <summary>
        /// Tests that the informational version withholds the source control commit hash unless the caller is authorized.
        /// <para>The commit hash identifies the exact revision of a publicly readable repository, so it belongs to the protected tier while the build stamp stays public.</para>
        /// </summary>
        [Fact]
        public void VersionInformation_CommitHash()
        {
            VersionInformation versionInformation_Public = Create.VersionInformation(false);
            Assert.NotNull(versionInformation_Public);
            Assert.DoesNotContain("+", versionInformation_Public.WebAPIInformationalVersion ?? string.Empty);
            Assert.DoesNotContain("+", versionInformation_Public.ServiceInformationalVersion ?? string.Empty);
            Assert.False(string.IsNullOrWhiteSpace(versionInformation_Public.WebAPIVersion));

            VersionInformation versionInformation_Authorized = Create.VersionInformation(true);
            Assert.NotNull(versionInformation_Authorized);
            Assert.Equal(versionInformation_Public.WebAPIVersion, versionInformation_Authorized.WebAPIVersion);

            string informationalVersion = versionInformation_Authorized.WebAPIInformationalVersion ?? string.Empty;
            if (informationalVersion.Contains('+'))
            {
                Assert.StartsWith(versionInformation_Public.WebAPIInformationalVersion ?? string.Empty, informationalVersion);
            }
        }

        /// <summary>
        /// Tests that loaded extensions are listed on the public version tier, with the same commit-hash rule as the host and framework: the build stamp stays public, the commit hash is withheld until the caller is authorized.
        /// </summary>
        [Fact]
        public void VersionInformation_Extensions()
        {
            // Inject a known assembly through the explicit parameter, bypassing the location filter, so the assertion is deterministic in any test host.
            Assembly extensionAssembly = typeof(Create).Assembly;

            VersionInformation versionInformation_Public = Create.VersionInformation(false, extensionAssemblies: [extensionAssembly]);
            Assert.NotNull(versionInformation_Public.Extensions);
            Assert.Single(versionInformation_Public.Extensions);
            ExtensionVersionInformation publicExtension = versionInformation_Public.Extensions.First();
            Assert.Equal(extensionAssembly.GetName().Name, publicExtension.Name);
            Assert.DoesNotContain("+", publicExtension.InformationalVersion ?? string.Empty);

            VersionInformation versionInformation_Authorized = Create.VersionInformation(true, extensionAssemblies: [extensionAssembly]);
            Assert.NotNull(versionInformation_Authorized.Extensions);
            Assert.Single(versionInformation_Authorized.Extensions);
            ExtensionVersionInformation authorizedExtension = versionInformation_Authorized.Extensions.First();
            Assert.Equal(extensionAssembly.GetName().Name, authorizedExtension.Name);

            // If the build injected a commit hash, the authorized tier carries it and the public tier is its prefix.
            string authorizedInformationalVersion = authorizedExtension.InformationalVersion ?? string.Empty;
            if (authorizedInformationalVersion.Contains('+'))
            {
                Assert.StartsWith(publicExtension.InformationalVersion ?? string.Empty, authorizedInformationalVersion);
            }

            // The framework assembly is loaded into the default ALC but lives in the normal bin, not an 'extensions/' directory, so the default location filter must exclude it.
            VersionInformation versionInformation_Default = Create.VersionInformation(false);
            Assert.NotNull(versionInformation_Default.Extensions);
            foreach (ExtensionVersionInformation extension in versionInformation_Default.Extensions)
            {
                Assert.NotEqual("DiGi.WebAPI", extension.Name);
            }
        }

        /// <summary>
        /// Tests that the <see cref="DiagnosticsConfiguration"/> factory fails closed when no configuration is present and honours an explicit Open opt-out.
        /// <para>Reads only synthetic files written to the test reports directory - never the real on-disk configuration.</para>
        /// </summary>
        [Fact]
        public void DiagnosticsConfiguration_Create()
        {
            string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory));

            string path_Missing = System.IO.Path.Combine(directory, "WebAPI_Diagnostics_Missing.conf");
            if (System.IO.File.Exists(path_Missing))
            {
                System.IO.File.Delete(path_Missing);
            }

            // An unresolvable path must not fall back to an open configuration. The factory probes the
            // base directory next, so assert on the authorization outcome rather than on Enabled alone.
            DiagnosticsConfiguration diagnosticsConfiguration_Missing = Create.DiagnosticsConfiguration(path_Missing);
            Assert.NotNull(diagnosticsConfiguration_Missing);
            Assert.False(diagnosticsConfiguration_Missing.IsAuthorized(null));
            Assert.False(diagnosticsConfiguration_Missing.IsAuthorized("guessed-key"));

            string path_Enabled = System.IO.Path.Combine(directory, "WebAPI_Diagnostics_Enabled.conf");
            System.IO.File.WriteAllLines(path_Enabled, ["Enabled=true", "Key=\"test-mock-file-key\"", "Open=false"]);

            DiagnosticsConfiguration diagnosticsConfiguration_Enabled = Create.DiagnosticsConfiguration(path_Enabled);
            Assert.True(diagnosticsConfiguration_Enabled.Enabled);
            Assert.False(diagnosticsConfiguration_Enabled.Open);
            Assert.Equal("test-mock-file-key", diagnosticsConfiguration_Enabled.Key);
            Assert.False(diagnosticsConfiguration_Enabled.IsAuthorized("wrong-key"));
            Assert.True(diagnosticsConfiguration_Enabled.IsAuthorized("test-mock-file-key"));

            string path_Disabled = System.IO.Path.Combine(directory, "WebAPI_Diagnostics_Disabled.conf");
            System.IO.File.WriteAllLines(path_Disabled, ["Enabled=false", "Key=\"test-mock-file-key\"", "Open=false"]);

            // The live production configuration looked exactly like this and left every protected
            // endpoint reachable. It must deny.
            DiagnosticsConfiguration diagnosticsConfiguration_Disabled = Create.DiagnosticsConfiguration(path_Disabled);
            Assert.False(diagnosticsConfiguration_Disabled.Enabled);
            Assert.False(diagnosticsConfiguration_Disabled.IsAuthorized(null));
            Assert.False(diagnosticsConfiguration_Disabled.IsAuthorized("test-mock-file-key"));

            string path_Open = System.IO.Path.Combine(directory, "WebAPI_Diagnostics_Open.conf");
            System.IO.File.WriteAllLines(path_Open, ["Enabled=false", "Key=\"\"", "Open=true"]);

            DiagnosticsConfiguration diagnosticsConfiguration_Open = Create.DiagnosticsConfiguration(path_Open);
            Assert.True(diagnosticsConfiguration_Open.Open);
            Assert.True(diagnosticsConfiguration_Open.IsAuthorized(null));
        }
    }
}
