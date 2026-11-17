using DiGi.Harness.Classes;
using DiGi.Harness.Enums;
using System.Globalization;
using System.Reflection;

namespace DiGi.Harness.xUnit
{
    public partial class Facts
    {
        private static ProviderConfiguration ProviderConfiguration_Anthropic()
        {
            return new ProviderConfiguration(
                "anthropic",
                "Claude Opus 5.5",
                Protocol.Anthropic,
                "https://api.anthropic.com",
                "claude-opus-5-5",
                "sk-test",
                null,
                32000,
                "medium",
                true,
                false,
                true,
                true);
        }

        private static ProviderConfiguration ProviderConfiguration_Local()
        {
            return new ProviderConfiguration(
                "local",
                "LM Studio",
                Protocol.OpenAI,
                "http://localhost:1234/v1",
                "qwen/qwen3-32b",
                null,
                null,
                0,
                null,
                false,
                true,
                false,
                true);
        }

        /// <summary>
        /// Tests the <see cref="Classes.HarnessConfiguration"/> and <see cref="ProviderConfiguration"/> constructors, JSON round trip and copy constructors.
        /// </summary>
        [Fact]
        public void HarnessConfiguration()
        {
            ProviderConfiguration providerConfiguration = ProviderConfiguration_Anthropic();
            Classes.HarnessConfiguration harnessConfiguration = new(true, "anthropic", [providerConfiguration, ProviderConfiguration_Local()], 8, 1000, 60, ["user-1"]);

            Assert.True(harnessConfiguration.Enabled);
            Assert.Equal("anthropic", harnessConfiguration.DefaultProviderName);
            Assert.Equal(2, harnessConfiguration.Providers?.Count);
            Assert.Equal(8, harnessConfiguration.MaxToolIterations);
            Assert.Equal(1000, harnessConfiguration.MaxToolResultCharacters);
            Assert.Equal(60, harnessConfiguration.RequestTimeoutSeconds);
            Assert.Equal(["user-1"], harnessConfiguration.AllowedUserIds!);

            Core.xUnit.Query.SerializationCheck(providerConfiguration);
            Core.xUnit.Query.SerializationCheck(harnessConfiguration);

            string? json = Core.Convert.ToSystem_String(harnessConfiguration);
            Classes.HarnessConfiguration? harnessConfiguration_Json = Core.Convert.ToDiGi<Classes.HarnessConfiguration>(json)?.FirstOrDefault();
            Assert.NotNull(harnessConfiguration_Json);
            Assert.Equal(Protocol.OpenAI, harnessConfiguration_Json!.Providers![1].Protocol);

            ProviderConfiguration providerConfiguration_Copy = new(providerConfiguration);
            Assert.Equal(providerConfiguration.Name, providerConfiguration_Copy.Name);
            Assert.Equal(providerConfiguration.DisplayName, providerConfiguration_Copy.DisplayName);
            Assert.Equal(providerConfiguration.Protocol, providerConfiguration_Copy.Protocol);
            Assert.Equal(providerConfiguration.BaseUri, providerConfiguration_Copy.BaseUri);
            Assert.Equal(providerConfiguration.Model, providerConfiguration_Copy.Model);
            Assert.Equal(providerConfiguration.Key, providerConfiguration_Copy.Key);
            Assert.Equal(providerConfiguration.KeyEnvironmentVariable, providerConfiguration_Copy.KeyEnvironmentVariable);
            Assert.Equal(providerConfiguration.MaxTokens, providerConfiguration_Copy.MaxTokens);
            Assert.Equal(providerConfiguration.Effort, providerConfiguration_Copy.Effort);
            Assert.Equal(providerConfiguration.Thinking, providerConfiguration_Copy.Thinking);
            Assert.Equal(providerConfiguration.ReplayReasoning, providerConfiguration_Copy.ReplayReasoning);
            Assert.Equal(providerConfiguration.AnthropicFallbacks, providerConfiguration_Copy.AnthropicFallbacks);
            Assert.Equal(providerConfiguration.Enabled, providerConfiguration_Copy.Enabled);

            Classes.HarnessConfiguration harnessConfiguration_Copy = new(harnessConfiguration);
            Assert.Equal(harnessConfiguration.Enabled, harnessConfiguration_Copy.Enabled);
            Assert.Equal(harnessConfiguration.DefaultProviderName, harnessConfiguration_Copy.DefaultProviderName);
            Assert.Equal(harnessConfiguration.MaxToolIterations, harnessConfiguration_Copy.MaxToolIterations);
            Assert.Equal(harnessConfiguration.MaxToolResultCharacters, harnessConfiguration_Copy.MaxToolResultCharacters);
            Assert.Equal(harnessConfiguration.RequestTimeoutSeconds, harnessConfiguration_Copy.RequestTimeoutSeconds);
            Assert.Equal(harnessConfiguration.AllowedUserIds!, harnessConfiguration_Copy.AllowedUserIds!);
            Assert.Equal("qwen/qwen3-32b", harnessConfiguration_Copy.Providers![1].Model);
            Assert.NotSame(harnessConfiguration.Providers![0], harnessConfiguration_Copy.Providers[0]);
        }

        /// <summary>
        /// Tests <see cref="Create.HarnessConfiguration(string?)"/> on a hand-written file without <c>_type</c> discriminators and with the protocol written by name, and the fail-closed answer for a missing or broken file.
        /// </summary>
        [Fact]
        public void Create_HarnessConfiguration()
        {
            string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(directory);

            string path = Path.Combine(directory!, "Harness_Create.json");
            File.WriteAllText(path, """
                {
                  "Enabled": true,
                  "DefaultProviderName": "local",
                  "MaxToolIterations": 5,
                  "Providers": [
                    { "Name": "local", "DisplayName": "LM Studio", "Protocol": "OpenAI", "BaseUri": "http://localhost:1234/v1", "Model": "qwen/qwen3-32b", "Enabled": true },
                    { "Name": "anthropic", "Protocol": "Anthropic", "BaseUri": "https://api.anthropic.com", "Model": "claude-opus-5-5", "KeyEnvironmentVariable": "DIGI_HARNESS_TEST_KEY", "Thinking": true, "Enabled": true }
                  ]
                }
                """);

            Classes.HarnessConfiguration harnessConfiguration = Create.HarnessConfiguration(path);
            Assert.True(harnessConfiguration.Enabled);
            Assert.Equal(5, harnessConfiguration.MaxToolIterations);
            Assert.Equal(2, harnessConfiguration.Providers?.Count);
            Assert.Equal(Protocol.OpenAI, harnessConfiguration.Providers![0].Protocol);
            Assert.Equal(Protocol.Anthropic, harnessConfiguration.Providers[1].Protocol);
            Assert.True(harnessConfiguration.Providers[1].Thinking);

            Assert.Equal("local", harnessConfiguration.ProviderConfiguration()?.Name);
            Assert.Equal("anthropic", harnessConfiguration.ProviderConfiguration("ANTHROPIC")?.Name);
            Assert.Null(harnessConfiguration.ProviderConfiguration("missing"));

            string path_Broken = Path.Combine(directory!, "Harness_Broken.json");
            File.WriteAllText(path_Broken, "{ not json");
            Assert.False(Create.HarnessConfiguration(path_Broken).Enabled);

            Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, Constants.FileName.Harness)));
            Assert.False(Create.HarnessConfiguration(Path.Combine(directory!, "Harness_Missing.json")).Enabled);
        }

        /// <summary>
        /// Tests <see cref="Query.IsAllowed(Classes.HarnessConfiguration?, string?)"/>, <see cref="Query.IsUsable(ProviderConfiguration?)"/> and <see cref="Query.ApiKey(ProviderConfiguration?)"/>, including the deny-by-default answers and a second pass under <c>pl-PL</c>.
        /// </summary>
        [Fact]
        public void IsAllowed()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                foreach (string cultureName in new[] { "en-GB", "pl-PL" })
                {
                    CultureInfo.CurrentCulture = new CultureInfo(cultureName);

                    Classes.HarnessConfiguration harnessConfiguration_Open = new(true, null, null, 0, 0, 0, null);
                    Assert.True(harnessConfiguration_Open.IsAllowed("anyone"));
                    Assert.False(harnessConfiguration_Open.IsAllowed(null));
                    Assert.False(harnessConfiguration_Open.IsAllowed(" "));

                    Classes.HarnessConfiguration harnessConfiguration_List = new(true, null, null, 0, 0, 0, ["Iwona"]);
                    Assert.True(harnessConfiguration_List.IsAllowed("IWONA"));
                    Assert.False(harnessConfiguration_List.IsAllowed("Ivan"));

                    Classes.HarnessConfiguration harnessConfiguration_Disabled = new(false, null, null, 0, 0, 0, null);
                    Assert.False(harnessConfiguration_Disabled.IsAllowed("anyone"));
                    Assert.False(((Classes.HarnessConfiguration?)null).IsAllowed("anyone"));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }

            Assert.True(ProviderConfiguration_Anthropic().IsUsable());
            Assert.True(ProviderConfiguration_Local().IsUsable());
            Assert.False(new ProviderConfiguration("x", null, Protocol.OpenAI, "ftp://host", "m", null, null, 0, null, false, false, false, true).IsUsable());
            Assert.False(new ProviderConfiguration("x", null, Protocol.Undefined, "http://host", "m", null, null, 0, null, false, false, false, true).IsUsable());
            Assert.False(new ProviderConfiguration("x", null, Protocol.OpenAI, "http://host", "m", null, null, 0, null, false, false, false, false).IsUsable());

            Assert.Equal("sk-test", ProviderConfiguration_Anthropic().ApiKey());
            Assert.Null(ProviderConfiguration_Local().ApiKey());

            Environment.SetEnvironmentVariable("DIGI_HARNESS_TEST_KEY_2", "env-key");
            try
            {
                Assert.Equal("env-key", new ProviderConfiguration("x", null, Protocol.OpenAI, "http://host", "m", null, "DIGI_HARNESS_TEST_KEY_2", 0, null, false, false, false, true).ApiKey());
            }
            finally
            {
                Environment.SetEnvironmentVariable("DIGI_HARNESS_TEST_KEY_2", null);
            }
        }
    }
}
