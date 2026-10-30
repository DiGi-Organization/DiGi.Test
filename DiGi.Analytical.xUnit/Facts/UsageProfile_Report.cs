using DiGi.Analytical.Building.HVAC.Enums;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Writes the values and sources of every usage profile of Building.HVAC.Create.UsageProfile to reports/UsageProfiles.md, the table the Usage-Profiles wiki page is pasted from, so the page is generated from the code rather than typed (DiGi-Organization/DiGi.Analytical#9).
        /// </summary>
        [Fact]
        public void UsageProfile_Report()
        {
            (string Name, string Header)[] columns =
            [
                (nameof(Building.HVAC.Classes.UsageProfile.HeatingSetPoint), "θ [°C]"),
                (nameof(Building.HVAC.Classes.UsageProfile.InternalHeatGain), "q_int [W/m²]"),
                (nameof(Building.HVAC.Classes.UsageProfile.Beta), "β [-]"),
                (nameof(Building.HVAC.Classes.UsageProfile.OutsideAirPerArea), "V_ve [l/(s·m²)]"),
                (nameof(Building.HVAC.Classes.UsageProfile.DomesticHotWater), "V_Wi [dm³/(m²·d)]"),
                (nameof(Building.HVAC.Classes.UsageProfile.DomesticHotWaterCorrection), "k_R [-]"),
                (nameof(Building.HVAC.Classes.UsageProfile.FloorAreaPerPerson), "a_std [m²/person]"),
            ];

            static string Text(double value)
            {
                return double.IsNaN(value) ? "-" : value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            static string Tag(string? source)
            {
                if (source is null)
                {
                    return string.Empty;
                }

                int index = source.IndexOf(':');
                return index < 0 ? source : source.Substring(0, index);
            }

            List<Building.HVAC.Classes.UsageProfile> usageProfiles = [];
            foreach (UsageType usageType in System.Enum.GetValues(typeof(UsageType)).Cast<UsageType>().Where(x => x != UsageType.Undefined))
            {
                Building.HVAC.Classes.UsageProfile? usageProfile = Building.HVAC.Create.UsageProfile(usageType);
                Assert.NotNull(usageProfile);
                usageProfiles.Add(usageProfile);
            }

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("| Usage type | " + string.Join(" | ", columns.Select(x => x.Header)) + " |");
            stringBuilder.AppendLine("|---|" + string.Concat(columns.Select(x => "---|")));
            foreach (Building.HVAC.Classes.UsageProfile usageProfile in usageProfiles)
            {
                Dictionary<string, string> sources = usageProfile.Sources ?? [];
                List<string> cells = [];
                foreach ((string name, string _) in columns)
                {
                    double value = (double)typeof(Building.HVAC.Classes.UsageProfile).GetProperty(name)!.GetValue(usageProfile)!;
                    sources.TryGetValue(name, out string? source);
                    cells.Add(double.IsNaN(value) ? "-" : Text(value) + " `" + Tag(source) + "`");
                }

                stringBuilder.AppendLine("| " + usageProfile.UsageType + " | " + string.Join(" | ", cells) + " |");
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Sources of every value:");
            stringBuilder.AppendLine();
            foreach (Building.HVAC.Classes.UsageProfile usageProfile in usageProfiles)
            {
                stringBuilder.AppendLine("- **" + usageProfile.UsageType + "**");
                foreach (KeyValuePair<string, string> keyValuePair in (usageProfile.Sources ?? []).OrderBy(x => x.Key))
                {
                    stringBuilder.AppendLine("  - " + keyValuePair.Key + ": " + keyValuePair.Value);
                }
            }

            string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(directory);

            string path = Path.Combine(directory, "UsageProfiles.md");
            File.WriteAllText(path, stringBuilder.ToString());
            Assert.True(File.Exists(path));
        }
    }
}
