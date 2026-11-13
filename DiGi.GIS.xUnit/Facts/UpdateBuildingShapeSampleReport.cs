using System;
using System.IO;
using System.Reflection;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Replaces the named section of the building shape report in <c>user files/reports/BuildingShapeSolver_Accuracy.md</c>, creating the report with a pending placeholder when it or the section does not exist yet; every other section is left untouched, so re-running one fact never drops the figures of the other.
        /// </summary>
        /// <param name="sectionName">The section identity written into the begin/end markers, for example <c>BuildingShapeSolver_Accuracy</c>.</param>
        /// <param name="content">The section body, without the markers.</param>
        private static void UpdateBuildingShapeSampleReport(string sectionName, string content)
        {
            Assert.False(string.IsNullOrWhiteSpace(sectionName));
            Assert.NotNull(content);

            string? reportsDirectory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(reportsDirectory));

            string path = Path.Combine(reportsDirectory!, "BuildingShapeSolver_Accuracy.md");

            string begin = $"<!-- BEGIN {sectionName} -->";
            string end = $"<!-- END {sectionName} -->";

            string text = File.Exists(path)
                ? File.ReadAllText(path)
                : "# BuildingShapeSolver accuracy and sensitivity (gis12-2026)" + Environment.NewLine + Environment.NewLine
                    + "<!-- Written by the BuildingShapeSolver_Accuracy and BuildingShapeSolver_Sensitivity facts of DiGi.GIS.xUnit; do not edit by hand. -->" + Environment.NewLine;

            int beginIndex = text.IndexOf(begin, StringComparison.Ordinal);
            int endIndex = text.IndexOf(end, StringComparison.Ordinal);

            if (beginIndex >= 0 && endIndex > beginIndex)
            {
                text = string.Concat(
                    text.Substring(0, beginIndex),
                    begin, Environment.NewLine,
                    content.TrimEnd(), Environment.NewLine,
                    end,
                    text.Substring(endIndex + end.Length));
            }
            else
            {
                text += Environment.NewLine + begin + Environment.NewLine + content.TrimEnd() + Environment.NewLine + end + Environment.NewLine;
            }

            File.WriteAllText(path, text);
        }
    }
}
