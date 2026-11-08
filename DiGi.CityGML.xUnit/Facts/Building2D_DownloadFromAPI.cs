using DiGi.CityGML.Classes;
using DiGi.GIS.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;

namespace DiGi.CityGML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Downloads matching Building2D objects from api.digiproject.uk for each Building in Buildings.json using buildingId, saves the result to Building2D.json, and reports missing pairs.
        /// <para>Medium test (8.5 s, live API): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async System.Threading.Tasks.Task DownloadBuilding2DsFromAPIAsync()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? directory_Files = Core.xUnit.Query.FilesDirectory(assembly);

            Assert.False(string.IsNullOrWhiteSpace(directory_Files));

            string path_BuildingsJson = Path.Combine(directory_Files!, "Buildings.json");

            Assert.True(File.Exists(path_BuildingsJson));

            string json_Buildings = File.ReadAllText(path_BuildingsJson);
            List<Building>? buildings = Core.Convert.ToDiGi<Building>(json_Buildings);

            Assert.NotNull(buildings);
            Assert.NotEmpty(buildings);

            List<Building2D> building2Ds = [];
            List<string> missingPairs = [];
            HashSet<string> processedReferences = [];

            using HttpClient httpClient = new();

            foreach (Building building in buildings)
            {
                string? buildingId = building.GetValue<string>(Enums.BuildingParameter.buildingId);

                if (string.IsNullOrWhiteSpace(buildingId))
                {
                    string? uniqueId = building.UniqueId;
                    if (!string.IsNullOrWhiteSpace(uniqueId) && uniqueId.StartsWith("ID-", StringComparison.CurrentCulture))
                    {
                        string[] parts = uniqueId.Split('-');
                        if (parts.Length >= 3)
                        {
                            buildingId = string.Join("-", parts.Skip(2));
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(buildingId))
                {
                    missingPairs.Add(string.Format(CultureInfo.CurrentCulture, "Building UniqueId={0} has no buildingId parameter.", building.UniqueId));
                    continue;
                }

                if (!processedReferences.Add(buildingId))
                {
                    continue;
                }

                string url = string.Format(CultureInfo.CurrentCulture, "https://api.digiproject.uk/gis/building2d/itembyreference?reference={0}", buildingId);
                HttpResponseMessage response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
                {
                    string json_Building2D = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json_Building2D))
                    {
                        string json_Array = json_Building2D.Trim().StartsWith("[", StringComparison.CurrentCulture) ? json_Building2D : string.Format(CultureInfo.CurrentCulture, "[{0}]", json_Building2D);
                        List<Building2D>? items = Core.Convert.ToDiGi<Building2D>(json_Array);
                        Building2D? building2D = items?.FirstOrDefault();

                        if (building2D != null)
                        {
                            building2Ds.Add(building2D);
                        }
                        else
                        {
                            missingPairs.Add(string.Format(CultureInfo.CurrentCulture, "Reference={0} (Building UniqueId={1}) returned empty payload.", buildingId, building.UniqueId));
                        }
                    }
                    else
                    {
                        missingPairs.Add(string.Format(CultureInfo.CurrentCulture, "Reference={0} (Building UniqueId={1}) returned empty response body.", buildingId, building.UniqueId));
                    }
                }
                else
                {
                    missingPairs.Add(string.Format(CultureInfo.CurrentCulture, "Reference={0} (Building UniqueId={1}) status code={2}.", buildingId, building.UniqueId, (int)response.StatusCode));
                }
            }

            Assert.NotEmpty(building2Ds);

            string? json_Building2Ds = Core.Convert.ToSystem_String(building2Ds);

            Assert.False(string.IsNullOrWhiteSpace(json_Building2Ds));

            string path_Output = Path.Combine(directory_Files!, "Building2Ds.json");

            File.WriteAllText(path_Output, json_Building2Ds);

            Assert.True(File.Exists(path_Output));

            string path_Report = Path.Combine(directory_Files!, "Building2D_Report.txt");
            File.WriteAllLines(path_Report, missingPairs);
        }
    }
}
