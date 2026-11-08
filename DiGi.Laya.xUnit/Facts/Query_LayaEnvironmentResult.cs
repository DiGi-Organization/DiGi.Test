using System;
using System.IO;
using System.Linq;

namespace DiGi.Laya.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.LayaEnvironmentResult(Classes.LayaPredictionOptions?, System.Threading.CancellationToken)"/> reports the interpreter and checkpoint of DiGi.Laya_Preflight.conf runnable - laya at the pinned version, the checkpoint loaded - with its option layout, token budgets, the installed packages and the SHA-256 of the checkpoint directory; and that the same interpreter with a model name instead of a checkpoint directory is not runnable, so a preflight never passes a value laya would download.
        /// <para>The interpreter and checkpoint are machine specific, so the fact returns without asserting when the conf, its interpreter or its checkpoint is absent. It also checks the refusals that need no interpreter: no options, and an interpreter that does not exist.</para>
        /// <para>Medium test (8 s on an RTX 5090, 2026-10-15): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Query_LayaEnvironmentResult()
        {
            Classes.LayaEnvironmentResult layaEnvironmentResult_Null = Query.LayaEnvironmentResult(null);
            Assert.False(layaEnvironmentResult_Null.Runnable);
            Assert.NotEmpty(layaEnvironmentResult_Null.Messages!);

            Classes.LayaEnvironmentResult layaEnvironmentResult_NoPython = Query.LayaEnvironmentResult(new Classes.LayaPredictionOptions() { PythonPath = Path.Combine(Path.GetTempPath(), "no-such-python.exe") });
            Assert.False(layaEnvironmentResult_NoPython.Runnable);
            Assert.Contains(layaEnvironmentResult_NoPython.Messages!, x => x.Contains("no-such-python.exe"));

            string directory = Path.Combine(Path.GetTempPath(), string.Concat("Laya_Preflight_", Guid.NewGuid().ToString("N")));

            try
            {
                Classes.LayaPredictionOptions? layaPredictionOptions = PreflightOptions(directory);
                if (layaPredictionOptions == null)
                {
                    return;
                }

                Classes.LayaEnvironmentResult layaEnvironmentResult = Query.LayaEnvironmentResult(layaPredictionOptions);
                Assert.True(layaEnvironmentResult.Runnable, string.Join(Environment.NewLine, layaEnvironmentResult.Messages ?? []));
                Assert.Empty(layaEnvironmentResult.Messages!);
                Assert.False(string.IsNullOrWhiteSpace(layaEnvironmentResult.PythonVersion));
                Assert.NotNull(layaEnvironmentResult.CudaAvailable);
                Assert.Contains(layaEnvironmentResult.Packages!, x => x.Name == "laya" && x.Version != null);
                Assert.Contains(layaEnvironmentResult.Packages!, x => x.Name == "torch" && x.Version != null);
                Assert.Equal(layaPredictionOptions.ModelPath, layaEnvironmentResult.ModelPath);
                Assert.Equal(Python.Query.DirectorySHA256(layaPredictionOptions.ModelPath), layaEnvironmentResult.ModelSHA256);
#pragma warning disable CA1861 // a one-off assertion; hoisting the array buys nothing
                Assert.Contains(layaEnvironmentResult.OptionLayout, new string[] { "sequential", "parallel" });
#pragma warning restore CA1861
                Assert.True(layaEnvironmentResult.MaxLength > 0);
                Assert.True(layaEnvironmentResult.HeadMaxLength > 0);
                Assert.True(File.Exists(Path.Combine(directory, "check.py")));

                //The same interpreter with a model name: the check still runs, but the result is not runnable
                Classes.LayaPredictionOptions layaPredictionOptions_Name = new(layaPredictionOptions) { ModelPath = "multilingual" };
                Classes.LayaEnvironmentResult layaEnvironmentResult_Name = Query.LayaEnvironmentResult(layaPredictionOptions_Name);
                Assert.False(layaEnvironmentResult_Name.Runnable);
                Assert.Contains(layaEnvironmentResult_Name.Messages!, x => x.Contains("Hub id is not accepted"));
                Assert.Null(layaEnvironmentResult_Name.ModelSHA256);
                Assert.Contains(layaEnvironmentResult_Name.Packages!, x => x.Name == "laya");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
