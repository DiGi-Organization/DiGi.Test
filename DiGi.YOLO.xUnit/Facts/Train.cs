using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;

namespace DiGi.YOLO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Modify.Train(Classes.YOLOTrainingOptions?, System.Threading.CancellationToken)"/> refuses a run that cannot or must not happen before any interpreter is started: missing options, a start file that is neither .pt nor .yaml, a missing conf.yaml, invalid hyperparameters, and a run directory inside a YOLO\models folder, where the frozen weights live.
        /// </summary>
        [Fact]
        public void Train_Refused()
        {
            Assert.Null(Modify.Train(null));
            Assert.Null(Modify.Train(new Classes.YOLOTrainingOptions()));

            string directory = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_Test_" + Path.GetRandomFileName());

            try
            {
                string directory_Models = Path.Combine(directory, "YOLO", "models");
                Directory.CreateDirectory(directory_Models);

                string path_Model = Path.Combine(directory_Models, "model.pt");
                string path_Onnx = Path.Combine(directory, "model.onnx");
                string path_Configuration = Path.Combine(directory, "conf.yaml");
                File.WriteAllText(path_Model, "dummy");
                File.WriteAllText(path_Onnx, "dummy");
                File.WriteAllText(path_Configuration, "path: .");

                string pythonPath = Path.Combine(directory, "no_such_interpreter.exe");

                Classes.YOLOTrainingResult? Train(string modelPath, string configurationFilePath, string? project, int epochs)
                {
                    return Modify.Train(new Classes.YOLOTrainingOptions()
                    {
                        ConfigurationFilePath = configurationFilePath,
                        Epochs = epochs,
                        ModelPath = modelPath,
                        Project = project,
                        PythonPath = pythonPath,
                        WorkingDirectory = directory
                    });
                }

                List<Classes.YOLOTrainingResult?> yOLOTrainingResults =
                [
                    Train(path_Onnx, path_Configuration, null, 150),
                    Train(path_Model, Path.Combine(directory, "missing.yaml"), null, 150),
                    Train(path_Model, path_Configuration, null, 0),
                    Train(path_Model, path_Configuration, Path.Combine(directory_Models, "runs"), 150),
                    Train(path_Model, path_Configuration, Path.Combine(directory, "other", "YOLO", "models"), 150)
                ];

                foreach (Classes.YOLOTrainingResult? yOLOTrainingResult in yOLOTrainingResults)
                {
                    Assert.NotNull(yOLOTrainingResult);
                    Assert.Equal(-1, yOLOTrainingResult!.ExitCode);
                    Assert.False(yOLOTrainingResult.Succeeded);
                    Assert.Single(yOLOTrainingResult.StandardError ?? []);
                    Assert.Null(yOLOTrainingResult.StandardOutput);
                }

                Assert.Equal(Enums.ModelKind.Undefined, yOLOTrainingResults[0]!.StartModelKind);
                Assert.Equal(Enums.ModelKind.Checkpoint, yOLOTrainingResults[1]!.StartModelKind);

                //Resume mode refuses a start file that is not an existing .pt before any interpreter is started
                Classes.YOLOTrainingResult? Resume(string resumePath)
                {
                    return Modify.Train(new Classes.YOLOTrainingOptions()
                    {
                        PythonPath = pythonPath,
                        ResumePath = resumePath,
                        WorkingDirectory = directory
                    });
                }

                foreach (Classes.YOLOTrainingResult? yOLOTrainingResult_Resume in new Classes.YOLOTrainingResult?[]
                {
                    Resume(Path.Combine(directory, "missing.pt")),
                    Resume(path_Onnx),
                    Resume(path_Configuration)
                })
                {
                    Assert.NotNull(yOLOTrainingResult_Resume);
                    Assert.Equal(-1, yOLOTrainingResult_Resume!.ExitCode);
                    Assert.False(yOLOTrainingResult_Resume.Succeeded);
                    Assert.True(yOLOTrainingResult_Resume.Resumed);
                    Assert.Single(yOLOTrainingResult_Resume.StandardError ?? []);
                    Assert.Null(yOLOTrainingResult_Resume.StandardOutput);
                }

                //The refused runs never reached the working directory
                Assert.False(File.Exists(Path.Combine(directory, "train.py")));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        /// <summary>
        /// Verifies the whole runner path of <see cref="Modify.Train(Classes.YOLOTrainingOptions?, System.Threading.CancellationToken)"/> against a stand-in ultralytics package: every option reaches train.py as the argument ultralytics receives, the start weights are identified, a stale train.py is replaced, the ultralytics settings are isolated, and the success block is parsed and confirmed against the file on disk.
        /// <para>A .yaml start file is used because a checkpoint is preflighted with check.py, which needs the real ultralytics and torch. The stand-in writes the keyword arguments it receives into best.pt, so the fact can read back exactly what the runner asked for.</para>
        /// </summary>
        [Fact]
        public void Train_Mock()
        {
            string? pythonPath = PythonPath_Runnable();
            if (string.IsNullOrWhiteSpace(pythonPath))
            {
                return;
            }

            string directory = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_Test_" + Path.GetRandomFileName(), "working dir");

            try
            {
                Directory.CreateDirectory(directory);

                string path_Definition = Path.Combine(directory, "yolo26x.yaml");
                File.WriteAllText(path_Definition, "nc: 1");

                string path_Configuration = Path.Combine(directory, "conf.yaml");
                File.WriteAllText(path_Configuration, "path: .\nnames:\n  0: Building");

                //A train.py of an older build ignored every argument; the runner must never execute it
                File.WriteAllText(Path.Combine(directory, "train.py"), "raise SystemExit(7)");

                string directory_MockUltralytics = Path.Combine(directory, "ultralytics");
                Directory.CreateDirectory(directory_MockUltralytics);
                File.WriteAllLines(Path.Combine(directory_MockUltralytics, "__init__.py"),
                [
                    "import json, os",
                    "",
                    "class _Trainer:",
                    "    pass",
                    "",
                    "class YOLO:",
                    "    def __init__(self, model):",
                    "        self.model = model",
                    "        self.trainer = None",
                    "",
                    "    def train(self, **kwargs):",
                    "        kwargs['model'] = self.model",
                    "        kwargs['YOLO_CONFIG_DIR'] = os.environ.get('YOLO_CONFIG_DIR')",
                    "        weights = os.path.join(kwargs['project'], kwargs.get('name', 'train'), 'weights')",
                    "        os.makedirs(weights, exist_ok=True)",
                    "        path = os.path.join(weights, 'best.pt')",
                    "        with open(path, 'w') as file:",
                    "            file.write(json.dumps(kwargs, sort_keys=True))",
                    "        self.trainer = _Trainer()",
                    "        self.trainer.best = path",
                    "        self.trainer.amp = kwargs['amp']",
                ]);

                Classes.YOLOTrainingOptions? yOLOTrainingOptions = Create.YOLOTrainingOptions(pythonPath, path_Definition, path_Configuration);
                Assert.NotNull(yOLOTrainingOptions);

                yOLOTrainingOptions!.Amp = false;
                yOLOTrainingOptions.Batch = 4;
                yOLOTrainingOptions.Device = "cpu";
                yOLOTrainingOptions.Epochs = 7;
                yOLOTrainingOptions.ImageSize = 320;
                yOLOTrainingOptions.Name = "train9 fresh";
                yOLOTrainingOptions.Patience = 3;
                yOLOTrainingOptions.Seed = 11;

                CultureInfo cultureInfo = CultureInfo.CurrentCulture;
                Classes.YOLOTrainingResult? yOLOTrainingResult;
                try
                {
                    CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
                    yOLOTrainingResult = Modify.Train(yOLOTrainingOptions);
                }
                finally
                {
                    CultureInfo.CurrentCulture = cultureInfo;
                }

                Assert.NotNull(yOLOTrainingResult);
                Assert.True(yOLOTrainingResult!.Succeeded, string.Join(Environment.NewLine, [.. yOLOTrainingResult.StandardOutput ?? [], .. yOLOTrainingResult.StandardError ?? []]));
                Assert.Equal(0, yOLOTrainingResult.ExitCode);
                Assert.Equal(path_Definition, yOLOTrainingResult.StartModelPath);
                Assert.Equal(Python.Query.FileSHA256(path_Definition), yOLOTrainingResult.StartModelSHA256);
                Assert.Equal(Enums.ModelKind.Definition, yOLOTrainingResult.StartModelKind);
                Assert.False(yOLOTrainingResult.Amp);

                string path_Weights = Path.Combine(directory, "runs", "detect", "train9 fresh", "weights", "best.pt");
                Assert.Equal(path_Weights, yOLOTrainingResult.WeightsPath);
                Assert.Equal(new FileInfo(path_Weights).Length, yOLOTrainingResult.Bytes);
                Assert.Equal(Python.Query.FileSHA256(path_Weights), yOLOTrainingResult.SHA256);

                JsonObject? jsonObject = JsonNode.Parse(File.ReadAllText(path_Weights)) as JsonObject;
                Assert.NotNull(jsonObject);
                Assert.Equal(path_Configuration, jsonObject!["data"]?.GetValue<string>());
                Assert.Equal(path_Definition, jsonObject["model"]?.GetValue<string>());
                Assert.Equal(7, jsonObject["epochs"]?.GetValue<int>());
                Assert.Equal(3, jsonObject["patience"]?.GetValue<int>());
                Assert.Equal(320, jsonObject["imgsz"]?.GetValue<int>());
                Assert.Equal(4, jsonObject["batch"]?.GetValue<int>());
                Assert.Equal(11, jsonObject["seed"]?.GetValue<int>());
                Assert.Equal("cpu", jsonObject["device"]?.GetValue<string>());
                Assert.False(jsonObject["amp"]?.GetValue<bool>());
                Assert.Equal(Path.Combine(directory, "runs", "detect"), jsonObject["project"]?.GetValue<string>());
                Assert.Equal(Path.Combine(directory, ".yolo-config"), jsonObject["YOLO_CONFIG_DIR"]?.GetValue<string>());

                //The dataset's conf.yaml in the working directory survived the script rewrite
                Assert.Contains("0: Building", File.ReadAllText(path_Configuration));
            }
            finally
            {
                string? directory_Root = Path.GetDirectoryName(directory);
                if (directory_Root != null && Directory.Exists(directory_Root))
                {
                    Directory.Delete(directory_Root, true);
                }
            }
        }

        /// <summary>
        /// Verifies that <see cref="Modify.Train(Classes.YOLOTrainingOptions?, System.Threading.CancellationToken)"/> ends a training that stops producing output once <see cref="Classes.YOLOTrainingOptions.InactivityTimeout"/> is exceeded, against a stand-in ultralytics package that prints one line and then sleeps: the run is reported as stalled, does not succeed, writes no weights, and carries the reason in the standard error tail.
        /// <para>Medium test (3 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Train_Mock_Stall()
        {
            string? pythonPath = PythonPath_Runnable();
            if (string.IsNullOrWhiteSpace(pythonPath))
            {
                return;
            }

            string directory = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_Test_" + Path.GetRandomFileName(), "working dir");

            try
            {
                Directory.CreateDirectory(directory);

                string path_Definition = Path.Combine(directory, "yolo26x.yaml");
                File.WriteAllText(path_Definition, "nc: 1");

                string path_Configuration = Path.Combine(directory, "conf.yaml");
                File.WriteAllText(path_Configuration, "path: .\nnames:\n  0: Building");

                //The stand-in prints one flushed line and then goes silent for far longer than the limit, the shape of a deadlocked training
                string directory_MockUltralytics = Path.Combine(directory, "ultralytics");
                Directory.CreateDirectory(directory_MockUltralytics);
                File.WriteAllLines(Path.Combine(directory_MockUltralytics, "__init__.py"),
                [
                    "import time",
                    "",
                    "class _Trainer:",
                    "    pass",
                    "",
                    "class YOLO:",
                    "    def __init__(self, model):",
                    "        self.model = model",
                    "        self.trainer = None",
                    "",
                    "    def train(self, **kwargs):",
                    "        print('mock training started', flush=True)",
                    "        time.sleep(600)",
                ]);

                Classes.YOLOTrainingOptions? yOLOTrainingOptions = Create.YOLOTrainingOptions(pythonPath, path_Definition, path_Configuration);
                Assert.NotNull(yOLOTrainingOptions);

                yOLOTrainingOptions!.InactivityTimeout = TimeSpan.FromSeconds(2);
                yOLOTrainingOptions.Name = "stalled";

                Classes.YOLOTrainingResult? yOLOTrainingResult = Modify.Train(yOLOTrainingOptions);

                Assert.NotNull(yOLOTrainingResult);
                Assert.True(yOLOTrainingResult!.Stalled);
                Assert.False(yOLOTrainingResult.Succeeded);
                Assert.NotEqual(0, yOLOTrainingResult.ExitCode);
                Assert.Null(yOLOTrainingResult.WeightsPath);
                Assert.Contains(yOLOTrainingResult.StandardError ?? [], line => line.StartsWith("Ended after", StringComparison.Ordinal));

                //A stalled run writes no weights
                Assert.False(File.Exists(Path.Combine(directory, "runs", "detect", "stalled", "weights", "best.pt")));
            }
            finally
            {
                string? directory_Root = Path.GetDirectoryName(directory);
                if (directory_Root != null && Directory.Exists(directory_Root))
                {
                    Directory.Delete(directory_Root, true);
                }
            }
        }

        /// <summary>
        /// Verifies that <see cref="Modify.Validate(Classes.YOLOValidationOptions?, System.Threading.CancellationToken)"/> refuses a run that cannot happen before any interpreter is started: missing options, a training split, a .yaml definition, and a missing conf.yaml.
        /// </summary>
        [Fact]
        public void Validate_Refused()
        {
            Assert.Null(Modify.Validate(null));
            Assert.Null(Modify.Validate(new Classes.YOLOValidationOptions()));

            string directory = Path.Combine(Path.GetTempPath(), "DiGi_YOLO_Test_" + Path.GetRandomFileName());

            try
            {
                Directory.CreateDirectory(directory);

                string path_Model = Path.Combine(directory, "best.pt");
                string path_Definition = Path.Combine(directory, "yolo26x.yaml");
                string path_Configuration = Path.Combine(directory, "conf.yaml");
                File.WriteAllText(path_Model, "dummy");
                File.WriteAllText(path_Definition, "dummy");
                File.WriteAllText(path_Configuration, "path: .");

                Classes.YOLOValidationResult? Validate(string modelPath, string configurationFilePath, Enums.Category split)
                {
                    return Modify.Validate(new Classes.YOLOValidationOptions()
                    {
                        ConfigurationFilePath = configurationFilePath,
                        ModelPath = modelPath,
                        PythonPath = Path.Combine(directory, "no_such_interpreter.exe"),
                        Split = split,
                        WorkingDirectory = directory
                    });
                }

                foreach (Classes.YOLOValidationResult? yOLOValidationResult in new Classes.YOLOValidationResult?[]
                {
                    Validate(path_Model, path_Configuration, Enums.Category.Train),
                    Validate(path_Definition, path_Configuration, Enums.Category.Test),
                    Validate(path_Model, Path.Combine(directory, "missing.yaml"), Enums.Category.Test)
                })
                {
                    Assert.NotNull(yOLOValidationResult);
                    Assert.Equal(-1, yOLOValidationResult!.ExitCode);
                    Assert.False(yOLOValidationResult.Succeeded);
                    Assert.Single(yOLOValidationResult.StandardError ?? []);
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        /// <summary>
        /// Runs a real 1-epoch training through <see cref="Modify.Train(Classes.YOLOTrainingOptions?, System.Threading.CancellationToken)"/> from the frozen model.pt and from the base checkpoint yolo26x.pt, then validates the continued weights with <see cref="Modify.Validate(Classes.YOLOValidationOptions?, System.Threading.CancellationToken)"/>, on a small synthetic dataset written by <see cref="Modify.Write(Classes.YOLOModel?)"/>.
        /// <para>The interpreter and both checkpoints are machine specific, so they are read from the git-ignored DiGi.YOLO_Preflight.conf (PythonPath, ModelPath, BaseModelPath) and the fact returns without asserting when any is absent. It needs a GPU to finish in reasonable time and is meant to be run on its own: it returns without asserting when a python process is already computing on the GPU, because the two runs of that shape observed so far both hung. The images - a light rectangle on noise, labelled "Building" - are generated with Pillow, which ultralytics depends on. The frozen model.pt is hashed before and after: no run may change it.</para>
        /// <para>Long test (> 30 s): runs when DIGI_TEST_MAX_DURATION is Long.</para>
        /// </summary>
        [LongFact]
        public void Train_Smoke()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            Dictionary<string, string> settings = PreflightSettings(assembly);
            settings.TryGetValue("PythonPath", out string? path_Python);
            settings.TryGetValue("ModelPath", out string? path_Model);
            settings.TryGetValue("BaseModelPath", out string? path_BaseModel);

            if (string.IsNullOrWhiteSpace(path_Python) || !File.Exists(path_Python) || string.IsNullOrWhiteSpace(path_Model) || !File.Exists(path_Model) || string.IsNullOrWhiteSpace(path_BaseModel) || !File.Exists(path_BaseModel))
            {
                return;
            }

            if (GPUBusyWithPython(assembly))
            {
                return;
            }

            string directory = Path.Combine(Core.xUnit.Query.ReportsDirectory(assembly)!, "YOLO_Train_Smoke");
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            string directory_Source = Path.Combine(directory, "source");
            Directory.CreateDirectory(directory_Source);

            string path_Script = Path.Combine(directory, "images.py");
            File.WriteAllLines(path_Script,
            [
                "import random, sys",
                "from PIL import Image, ImageDraw",
                "random.seed(0)",
                "for i in range(int(sys.argv[2])):",
                "    image = Image.effect_noise((320, 320), 40).convert('RGB')",
                "    w, h = random.randint(60, 160), random.randint(60, 160)",
                "    x, y = random.randint(0, 320 - w), random.randint(0, 320 - h)",
                "    ImageDraw.Draw(image).rectangle([x, y, x + w, y + h], fill=(230, 230, 230))",
                "    name = 'image%03d' % i",
                "    image.save('%s/%s.jpeg' % (sys.argv[1], name), 'JPEG')",
                "    print('%s %r %r %r %r' % (name, (x + w / 2) / 320, (y + h / 2) / 320, w / 320, h / 320))",
            ]);

            Python.Classes.PythonProcessResult pythonProcessResult_Images = Python.Query.ExecuteProcess(path_Python!, string.Format(CultureInfo.CurrentCulture, "\"{0}\" \"{1}\" 32", path_Script, directory_Source), directory);
            int exitCode_Images = pythonProcessResult_Images.ExitCode;
            List<string> standardOutput_Images = pythonProcessResult_Images.StandardOutput ?? [];
            List<string> standardError_Images = pythonProcessResult_Images.StandardError ?? [];
            Assert.True(exitCode_Images == 0, string.Join(Environment.NewLine, standardError_Images));

            string directory_Dataset = Path.Combine(directory, "training");
            Classes.YOLOModel yOLOModel = new(directory_Dataset);
            yOLOModel.Add("Building");

            int index = 0;
            foreach (string line in standardOutput_Images)
            {
                string[] values = line.Split(' ');
                if (values.Length != 5)
                {
                    continue;
                }

                string path_Image = Path.Combine(directory_Source, values[0] + ".jpeg");
                Enums.Category category = index < 20 ? Enums.Category.Train : index < 26 ? Enums.Category.Validate : Enums.Category.Test;
                yOLOModel.Add(path_Image, category);
                yOLOModel.Add(path_Image, "Building", new Classes.BoundingBox(double.Parse(values[1], CultureInfo.InvariantCulture), double.Parse(values[2], CultureInfo.InvariantCulture), double.Parse(values[3], CultureInfo.InvariantCulture), double.Parse(values[4], CultureInfo.InvariantCulture)));
                index++;
            }

            Assert.Equal(32, index);
            Assert.True(Modify.Write(yOLOModel));

            string path_Configuration = Path.Combine(directory_Dataset, "conf.yaml");
            string? sHA256_Model = Python.Query.FileSHA256(path_Model);

            Classes.YOLOTrainingResult Train(string modelPath, string name)
            {
                Classes.YOLOTrainingOptions? yOLOTrainingOptions = Create.YOLOTrainingOptions(path_Python, modelPath, path_Configuration);
                Assert.NotNull(yOLOTrainingOptions);

                yOLOTrainingOptions!.Batch = 8;
                yOLOTrainingOptions.Epochs = 1;
                yOLOTrainingOptions.ImageSize = 320;
                yOLOTrainingOptions.Name = name;

                Classes.YOLOTrainingResult? yOLOTrainingResult = Modify.Train(yOLOTrainingOptions);
                Assert.NotNull(yOLOTrainingResult);
                Assert.True(yOLOTrainingResult!.Succeeded, string.Join(Environment.NewLine, yOLOTrainingResult.StandardError ?? []));

                Assert.Equal(Python.Query.FileSHA256(modelPath), yOLOTrainingResult.StartModelSHA256);
                Assert.Equal(Enums.ModelKind.Checkpoint, yOLOTrainingResult.StartModelKind);
                Assert.Equal(Path.Combine(directory_Dataset, "runs", "detect", name, "weights", "best.pt"), yOLOTrainingResult.WeightsPath);
                Assert.Equal(Python.Query.FileSHA256(yOLOTrainingResult.WeightsPath), yOLOTrainingResult.SHA256);
                Assert.NotNull(yOLOTrainingResult.Amp);

                return yOLOTrainingResult;
            }

            Classes.YOLOTrainingResult yOLOTrainingResult_Continue = Train(path_Model!, "smoke_continue");
            Train(path_BaseModel!, "smoke_fresh");

            Assert.Equal(sHA256_Model, Python.Query.FileSHA256(path_Model));

            Classes.YOLOValidationOptions? yOLOValidationOptions = Create.YOLOValidationOptions(path_Python, yOLOTrainingResult_Continue.WeightsPath, path_Configuration);
            Assert.NotNull(yOLOValidationOptions);
            yOLOValidationOptions!.Batch = 8;
            yOLOValidationOptions.ImageSize = 320;

            Classes.YOLOValidationResult? yOLOValidationResult = Modify.Validate(yOLOValidationOptions);
            Assert.NotNull(yOLOValidationResult);
            Assert.True(yOLOValidationResult!.Succeeded, string.Join(Environment.NewLine, yOLOValidationResult.StandardError ?? []));
            Assert.InRange(yOLOValidationResult.MAP50!.Value, 0, 1);
            Assert.InRange(yOLOValidationResult.MAP50_95!.Value, 0, yOLOValidationResult.MAP50.Value);
            Assert.Equal(yOLOTrainingResult_Continue.SHA256, yOLOValidationResult.ModelSHA256);
        }

        /// <summary>
        /// Determines whether a python process is already computing on the GPU, so a training fact can return without asserting instead of starting beside it.
        /// <para>A smoke training launched while a production training held the GPU left both processes hung (DiGi-Organization/DiGi.YOLO#22). nvidia-smi absent - no NVIDIA driver, or not on PATH - answers false: the git-ignored preflight conf already limits these facts to the GPU machine. When the answer is true, the reason is appended to a GPU_Busy.log line in the reports directory.</para>
        /// </summary>
        /// <param name="assembly">The test assembly whose reports directory receives the skip reason.</param>
        /// <returns>True when nvidia-smi lists a python process among the GPU compute apps.</returns>
        private static bool GPUBusyWithPython(Assembly assembly)
        {
            Python.Classes.PythonProcessResult pythonProcessResult = Python.Query.ExecuteProcess("nvidia-smi", "--query-compute-apps=pid,process_name --format=csv,noheader", Path.GetTempPath());
            int exitCode = pythonProcessResult.ExitCode;
            List<string> standardOutput = pythonProcessResult.StandardOutput ?? [];
            if (exitCode != 0)
            {
                return false;
            }

            foreach (string line in standardOutput)
            {
                if (line.Contains("python", StringComparison.OrdinalIgnoreCase))
                {
                    string? directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
                    if (!string.IsNullOrWhiteSpace(directory_Reports))
                    {
                        File.AppendAllText(Path.Combine(directory_Reports!, "GPU_Busy.log"), string.Format(CultureInfo.InvariantCulture, "{0:u} skipped a GPU training fact: {1}{2}", DateTimeOffset.Now, line, Environment.NewLine));
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads the git-ignored DiGi.YOLO_Preflight.conf of the test solution's user files as key=value pairs, or returns an empty dictionary when it is absent.
        /// </summary>
        /// <param name="assembly">The test assembly whose user files directory holds the conf.</param>
        /// <returns>The settings of the conf.</returns>
        private static Dictionary<string, string> PreflightSettings(Assembly assembly)
        {
            Dictionary<string, string> result = [];

            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return result;
            }

            string path_Configuration = Path.Combine(directory_UserFiles!, "DiGi.YOLO_Preflight.conf");
            if (!File.Exists(path_Configuration))
            {
                return result;
            }

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

                result[line.Substring(0, index).Trim()] = line.Substring(index + 1).Trim();
            }

            return result;
        }
    }
}
