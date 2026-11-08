using DiGi.CityGML.Classes;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace DiGi.CityGML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Diagnostic test to inspect duplicate buildings in test fixtures.
        /// </summary>
        [Fact]
        public void InspectDuplicateBuildings()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string[] fileNames =
            [
                "2476_CityGML.zip",
                "2862_CityGML.zip",
                "2862_N-34-77-D-b-1-1.gml",
                "0201_M-33-19-B-d-3-2.gml"
            ];

            List<string> lines = [];
            lines.Add("=== DIAGNOSTIC REPORT: SOURCE FILES AND BUILDINGS ===");

            foreach (string fileName in fileNames)
            {
                string? path = Core.xUnit.Query.FilePath(assembly, fileName);
                lines.Add($"\nSource File: {fileName}");

                if (fileName.EndsWith(".zip", StringComparison.CurrentCulture) && File.Exists(path))
                {
                    using ZipArchive archive = ZipFile.OpenRead(path);
                    lines.Add("  ZIP Archive Entries:");
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        lines.Add($"    - {entry.FullName} ({entry.Length} bytes)");
                    }
                }

                if (File.Exists(path))
                {
                    List<CityModel>? cityModels = Create.CityModels(path);
                    if (cityModels is not null)
                    {
                        lines.Add($"  CityModels Count: {cityModels.Count}");
                        foreach (CityModel cm in cityModels)
                        {
                            if (cm.Buildings is not null)
                            {
                                lines.Add($"    Buildings Count: {cm.Buildings.Count()}");
                                foreach (Building b in cm.Buildings)
                                {
                                    lines.Add($"      - Building UniqueId: {b.UniqueId}");
                                }
                            }
                        }
                    }
                }
            }

            string? dirReports = Core.xUnit.Query.ReportsDirectory(assembly);
            if (dirReports is not null)
            {
                File.WriteAllLines(Path.Combine(dirReports, "Inspect_Duplicates.txt"), lines);
            }
        }
    }
}
