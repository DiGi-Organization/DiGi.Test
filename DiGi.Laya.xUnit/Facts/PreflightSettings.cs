using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DiGi.Laya.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reads the git-ignored DiGi.Laya_Preflight.conf of the test solution's user files as key=value pairs, or returns an empty dictionary when it is absent.
        /// <para>The conf names the interpreter (PythonPath) and the local checkpoint directory (ModelPath) the facts that run the real model use; both are machine specific.</para>
        /// </summary>
        /// <returns>The settings of the conf.</returns>
        private static Dictionary<string, string> PreflightSettings()
        {
            Dictionary<string, string> result = [];

            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(Assembly.GetExecutingAssembly());
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return result;
            }

            string path_Configuration = Path.Combine(directory_UserFiles!, "DiGi.Laya_Preflight.conf");
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

        /// <summary>
        /// Builds prediction options from DiGi.Laya_Preflight.conf for the facts that run the real model, or returns <c>null</c> when the conf, its interpreter or its checkpoint is absent on this machine.
        /// </summary>
        /// <param name="workingDirectory">The working directory of the run.</param>
        /// <returns>The options, or <c>null</c> when the real model cannot be run here.</returns>
        private static Classes.LayaPredictionOptions? PreflightOptions(string? workingDirectory)
        {
            Dictionary<string, string> settings = PreflightSettings();
            if (!settings.TryGetValue("PythonPath", out string? pythonPath) || !File.Exists(pythonPath))
            {
                return null;
            }

            if (!settings.TryGetValue("ModelPath", out string? modelPath) || Query.LayaModelPath(modelPath) == null)
            {
                return null;
            }

            return Create.LayaPredictionOptions(pythonPath, modelPath, workingDirectory);
        }
    }
}
