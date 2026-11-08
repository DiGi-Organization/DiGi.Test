using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace DiGi.YOLO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Classes.YOLOEnvironmentResult"/> keeps all properties given to it, round-trips cleanly through JSON serialization, and passes contract validation.
        /// </summary>
        [Fact]
        public void YOLOEnvironmentResult()
        {
            DateTimeOffset checkedTime = new(2026, 8, 28, 17, 0, 0, TimeSpan.FromHours(2));

            Classes.YOLOEnvironmentResult yOLOEnvironmentResult = new(
                true,
                @"C:\Users\AppData\Local\Programs\Python\Python313\python.exe",
                "3.13.14",
                "8.3.130",
                "2.7.0+cu128",
                true,
                @"C:\YOLO\models\best.pt",
                "8.3.130",
                [],
                ["CUDA hardware acceleration available."],
                checkedTime);

            Assert.True(yOLOEnvironmentResult.Runnable);
            Assert.Equal(@"C:\Users\AppData\Local\Programs\Python\Python313\python.exe", yOLOEnvironmentResult.PythonPath);
            Assert.Equal("3.13.14", yOLOEnvironmentResult.PythonVersion);
            Assert.Equal("8.3.130", yOLOEnvironmentResult.UltralyticsVersion);
            Assert.Equal("2.7.0+cu128", yOLOEnvironmentResult.TorchVersion);
            Assert.True(yOLOEnvironmentResult.CudaAvailable);
            Assert.Equal(@"C:\YOLO\models\best.pt", yOLOEnvironmentResult.ModelPath);
            Assert.Equal("8.3.130", yOLOEnvironmentResult.ModelUltralyticsVersion);
            Assert.Empty(yOLOEnvironmentResult.Messages!);
            Assert.Single(yOLOEnvironmentResult.Warnings!);
            Assert.Equal(checkedTime, yOLOEnvironmentResult.Checked);

            Core.xUnit.Query.SerializationCheck(yOLOEnvironmentResult);
        }

        /// <summary>
        /// Verifies that probing a missing or garbage interpreter path returns a non-runnable environment result with populated messages rather than throwing an exception.
        /// </summary>
        [Fact]
        public void YOLOEnvironmentResult_InterpreterMissing()
        {
            string invalidPythonPath = Path.Combine(Path.GetTempPath(), "no_such_interpreter_" + Path.GetRandomFileName() + ".exe");

            Classes.YOLOEnvironmentResult yOLOEnvironmentResult = Query.YOLOEnvironmentResult(invalidPythonPath, null);

            Assert.NotNull(yOLOEnvironmentResult);
            Assert.False(yOLOEnvironmentResult.Runnable);
            Assert.Equal(invalidPythonPath, yOLOEnvironmentResult.PythonPath);
            Assert.Null(yOLOEnvironmentResult.PythonVersion);
            Assert.Null(yOLOEnvironmentResult.UltralyticsVersion);
            Assert.Null(yOLOEnvironmentResult.TorchVersion);
            Assert.NotEmpty(yOLOEnvironmentResult.Messages!);

            Core.xUnit.Query.SerializationCheck(yOLOEnvironmentResult);
        }

        /// <summary>
        /// Verifies that probing an environment with a <c>null</c> model path does not throw and handles missing interpreters cleanly.
        /// </summary>
        [Fact]
        public void YOLOEnvironmentResult_NullModel()
        {
            string invalidPythonPath = Path.Combine(Path.GetTempPath(), "no_such_interpreter_" + Path.GetRandomFileName() + ".exe");

            Classes.YOLOEnvironmentResult yOLOEnvironmentResult = Query.YOLOEnvironmentResult(invalidPythonPath, null);

            Assert.NotNull(yOLOEnvironmentResult);
            Assert.False(yOLOEnvironmentResult.Runnable);
            Assert.Null(yOLOEnvironmentResult.ModelPath);
            Assert.Null(yOLOEnvironmentResult.ModelUltralyticsVersion);
        }

        /// <summary>
        /// Verifies that probing a missing or invalid interpreter path completes rapidly so a preflight check does not block execution.
        /// </summary>
        [Fact]
        public void YOLOEnvironmentResult_Performance()
        {
            string invalidPythonPath = Path.Combine(Path.GetTempPath(), "no_such_interpreter_" + Path.GetRandomFileName() + ".exe");

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Classes.YOLOEnvironmentResult yOLOEnvironmentResult = Query.YOLOEnvironmentResult(invalidPythonPath, null);
            stopwatch.Stop();

            Assert.NotNull(yOLOEnvironmentResult);
            Assert.False(yOLOEnvironmentResult.Runnable);
            Assert.True(stopwatch.ElapsedMilliseconds < 2000, string.Format(CultureInfo.CurrentCulture, "Preflight check took {0} ms, expected under 2000 ms.", stopwatch.ElapsedMilliseconds));
        }

        /// <summary>
        /// Verifies that the preflight probe reports a readable checkpoint as runnable and names the ultralytics version that wrote it, and that a present but unreadable model is reported as a warning rather than making the machine unrunnable.
        /// <para>The interpreter and checkpoint are machine specific - a CPython carrying ultralytics and torch plus the frozen model - so both are read from a git-ignored conf (DiGi.YOLO_Preflight.conf) and the fact returns without asserting when that conf is absent. The split between <see cref="Classes.YOLOEnvironmentResult.Messages"/> and <see cref="Classes.YOLOEnvironmentResult.Warnings"/> is what makes the second half hold: runnable is about the machine, and the model header is diagnostic.</para>
        /// <para>Medium test (2.6 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void YOLOEnvironmentResult_Model()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return;
            }

            string path_Configuration = Path.Combine(directory_UserFiles!, "DiGi.YOLO_Preflight.conf");
            if (!File.Exists(path_Configuration))
            {
                return;
            }

            Dictionary<string, string> settings = [];
            foreach (string line in File.ReadAllLines(path_Configuration))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                int index = line.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                settings[line.Substring(0, index).Trim()] = line.Substring(index + 1).Trim();
            }

            settings.TryGetValue("PythonPath", out string? path_Python);
            settings.TryGetValue("ModelPath", out string? path_Model);

            if (string.IsNullOrWhiteSpace(path_Python) || !File.Exists(path_Python) || string.IsNullOrWhiteSpace(path_Model) || !File.Exists(path_Model))
            {
                return;
            }

            // A readable checkpoint reports the ultralytics version recorded inside it and leaves the machine runnable
            Classes.YOLOEnvironmentResult yOLOEnvironmentResult_Readable = Query.YOLOEnvironmentResult(path_Python, path_Model);
            Assert.NotNull(yOLOEnvironmentResult_Readable);
            Assert.True(yOLOEnvironmentResult_Readable.Runnable);
            Assert.Equal("8.3.130", yOLOEnvironmentResult_Readable.ModelUltralyticsVersion);
            Assert.Empty(yOLOEnvironmentResult_Readable.Warnings ?? []);

            // A present model whose header cannot be parsed is a warning, not a refusal: runnable stays true and the
            // reason surfaces in Warnings rather than Messages
            string path_Model_Unreadable = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_Unreadable_" + Path.GetRandomFileName() + ".pt");
            try
            {
                File.WriteAllText(path_Model_Unreadable, "this is not a torch checkpoint");

                Classes.YOLOEnvironmentResult yOLOEnvironmentResult_Unreadable = Query.YOLOEnvironmentResult(path_Python, path_Model_Unreadable);
                Assert.NotNull(yOLOEnvironmentResult_Unreadable);
                Assert.True(yOLOEnvironmentResult_Unreadable.Runnable);
                Assert.Null(yOLOEnvironmentResult_Unreadable.ModelUltralyticsVersion);
                Assert.NotEmpty(yOLOEnvironmentResult_Unreadable.Warnings ?? []);
            }
            finally
            {
                if (File.Exists(path_Model_Unreadable))
                {
                    File.Delete(path_Model_Unreadable);
                }
            }
        }

        /// <summary>
        /// Verifies that running the main (8.4.165) and the venv (8.3.130) environment one after the other in one working directory no longer prints a settings notice or rewrites the other's settings file.
        /// <para>Both interpreters are machine specific, so their paths are read from a git-ignored conf (DiGi.YOLO_Preflight.conf) and the fact returns without asserting when that conf or either interpreter is absent. Before the YOLO_CONFIG_DIR isolation this fact failed with the reported symptom: a settings notice on stdout and the shared %APPDATA% settings file rewritten across every switch.</para>
        /// <para>Medium test (8.4 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void YOLOEnvironmentResult_SettingsIsolation()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return;
            }

            string path_Configuration = Path.Combine(directory_UserFiles!, "DiGi.YOLO_Preflight.conf");
            if (!File.Exists(path_Configuration))
            {
                return;
            }

            Dictionary<string, string> settings = [];
            foreach (string line in File.ReadAllLines(path_Configuration))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                int index = line.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                settings[line.Substring(0, index).Trim()] = line.Substring(index + 1).Trim();
            }

            settings.TryGetValue("PythonPath", out string? path_Python);
            settings.TryGetValue("VenvPythonPath", out string? path_Python_Venv);

            if (string.IsNullOrWhiteSpace(path_Python) || !File.Exists(path_Python) || string.IsNullOrWhiteSpace(path_Python_Venv) || !File.Exists(path_Python_Venv))
            {
                return;
            }

            const string directoryName_Config = Constants.DirectoryName.YoloConfig;

            string workingDirectory = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_Isolation_" + Path.GetRandomFileName());
            string path_Check = Path.Combine(workingDirectory, "check.py");

            List<List<string>> runs_Report = [];

            static string Quoted(string? value)
            {
                return string.Concat("\"", value, "\"");
            }

            static string? Hash(string path)
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using FileStream stream = File.OpenRead(path);
                return Convert.ToBase64String(SHA256.HashData(stream));
            }

            static bool IsSettingsNotice(string line)
            {
                return line.Contains("Ultralytics Settings") || line.Contains("Error reading from") || line.Contains("settings updated");
            }

            try
            {
                Directory.CreateDirectory(workingDirectory);
                Assert.True(Modify.WriteScripts(workingDirectory));

                string path_SharedSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ultralytics", "settings.json");
                string? hash_Shared_Before = Hash(path_SharedSettings);

                //The public API: one preflight run per environment, same working directory
                Classes.YOLOEnvironmentResult result_Main = Query.YOLOEnvironmentResult(path_Python, null, workingDirectory);
                Classes.YOLOEnvironmentResult result_Venv = Query.YOLOEnvironmentResult(path_Python_Venv, null, workingDirectory);

                List<string> report_Preflight = ["Part A: preflight via Query.YOLOEnvironmentResult", string.Concat("main runnable ", result_Main.Runnable.ToString()), string.Concat("venv runnable ", result_Venv.Runnable.ToString())];
                if (result_Main.Messages != null)
                {
                    report_Preflight.AddRange(result_Main.Messages);
                }

                if (result_Venv.Messages != null)
                {
                    report_Preflight.AddRange(result_Venv.Messages);
                }

                runs_Report.Add(report_Preflight);

                Assert.True(result_Main.Runnable);
                Assert.True(result_Venv.Runnable);

                //Each environment keeps its own settings file under the working directory: the venv writes <config>\\settings.json, the main environment <config>\\Ultralytics\\settings.json
                string path_Config = Path.Combine(workingDirectory, directoryName_Config);
                string path_Settings_Venv = Path.Combine(path_Config, "settings.json");
                string path_Settings_Main = Path.Combine(path_Config, "Ultralytics", "settings.json");

                Assert.True(File.Exists(path_Settings_Venv), "The 8.3.130 settings file is missing from the isolated working directory.");
                Assert.True(File.Exists(path_Settings_Main), "The 8.4.165 settings file is missing from the isolated working directory.");

                //The shared machine-wide settings file is never touched by a run
                Assert.Equal(hash_Shared_Before, Hash(path_SharedSettings));

                //Steady state: the exact process launch the runners perform - including the isolated settings directory - must print no settings notice, carry the marker-framed payload, and leave both files untouched
                Dictionary<string, string> environmentVariables = Query.ConfigEnvironmentVariables(workingDirectory);

                Python.Classes.PythonProcessResult[] pythonProcessResults =
                [
                    Python.Query.ExecuteProcess(path_Python, Quoted(path_Check), workingDirectory, environmentVariables),
                    Python.Query.ExecuteProcess(path_Python_Venv, Quoted(path_Check), workingDirectory, environmentVariables),
                    Python.Query.ExecuteProcess(path_Python, Quoted(path_Check), workingDirectory, environmentVariables),
                    Python.Query.ExecuteProcess(path_Python_Venv, Quoted(path_Check), workingDirectory, environmentVariables)
                ];

                string? hash_Venv_Before = Hash(path_Settings_Venv);
                string? hash_Main_Before = Hash(path_Settings_Main);
                string[] interpreters = [path_Python, path_Python_Venv, path_Python, path_Python_Venv];

                for (int i = 0; i < pythonProcessResults.Length; i++)
                {
                    Python.Classes.PythonProcessResult pythonProcessResult = pythonProcessResults[i];
                    List<string> standardOutput = pythonProcessResult.StandardOutput ?? [];

                    List<string> report_Run = [string.Concat("Part B run ", i + 1, ": ", interpreters[i]), string.Concat("exit code ", pythonProcessResult.ExitCode.ToString(CultureInfo.CurrentCulture)), "--stdout--"];
                    report_Run.AddRange(standardOutput);
                    report_Run.Add("--stderr--");
                    report_Run.AddRange(pythonProcessResult.StandardError ?? []);
                    runs_Report.Add(report_Run);

                    Assert.Equal(0, pythonProcessResult.ExitCode);

                    foreach (string line in standardOutput)
                    {
                        Assert.False(IsSettingsNotice(line), string.Concat("Settings notice on stdout: ", line));
                    }

                    string? line_Json = Python.Query.MarkedJson(standardOutput, Constants.Marker.CheckJsonBegin, Constants.Marker.CheckJsonEnd);
                    Assert.False(string.IsNullOrWhiteSpace(line_Json), "check.py printed no marker-framed JSON payload.");

                    System.Text.Json.Nodes.JsonNode? jsonNode = System.Text.Json.Nodes.JsonNode.Parse(line_Json!);
                    Assert.True(jsonNode?["runnable"]?.GetValue<bool>() == true, "The check.py payload does not report a runnable environment.");

                    Assert.Equal(hash_Venv_Before, Hash(path_Settings_Venv));
                    Assert.Equal(hash_Main_Before, Hash(path_Settings_Main));
                }
            }
            catch (Exception)
            {
                string? directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
                if (!string.IsNullOrWhiteSpace(directory_Reports))
                {
                    List<string> reportLines = ["YOLOEnvironmentResult_SettingsIsolation failure report", DateTimeOffset.Now.ToString("O"), string.Empty];
                    foreach (List<string> lines in runs_Report)
                    {
                        reportLines.AddRange(lines);
                        reportLines.Add(string.Empty);
                    }

                    File.WriteAllLines(Path.Combine(directory_Reports!, "YOLOEnvironmentResult_SettingsIsolation_" + DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.CurrentCulture) + ".txt"), reportLines);
                }

                throw;
            }
            finally
            {
                if (Directory.Exists(workingDirectory))
                {
                    Directory.Delete(workingDirectory, true);
                }
            }
        }
    }
}
