using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Production run of DiGi.GIS#17, steps 1, 2 and 4: pulls every yearly typename of the live GUGiK orthophoto index, deduplicates it nationally and builds availability matrices for several cell sizes and coverage fractions.
        /// <para>Runs only when the environment variable <c>DIGI_ORTO_INVENTORY_RUN</c> is <c>1</c> (and DIGI_TEST_MAX_DURATION is Long); otherwise it returns at once, so a Long suite never starts a multi-hour national pull by accident.</para>
        /// <para>Writes to <c>user files/reports/OrtoInventory/</c>: per year <c>index/sheets-{year}.json</c> (resume cache: a year whose file exists is not pulled again) and <c>.tsv</c>; <c>index-counts.tsv</c> (rows against <c>RESULTTYPE=hits</c>, footprints outside the PL-1992 extent); <c>duplicate-groups.tsv</c> (group sizes per year); <c>sheets-canonical.tsv</c>; and <c>availability-matrix-{cellSize}-{coverageFraction}.tsv</c> with <c>matrix-settings.tsv</c> counting cells and epochs per setting.</para>
        /// <para>Verification (a) and (b) of the issue are asserted: every year's row count equals its hits count, and every footprint lies inside Poland's EPSG:2180 extent.</para>
        /// <para>Long test (hours; about 2.4 GB of GML fetched sequentially).</para>
        /// </summary>
        [LongFact]
        public async Task OrtoInventory_Index()
        {
            if (Environment.GetEnvironmentVariable("DIGI_ORTO_INVENTORY_RUN") != "1")
            {
                return;
            }

            string directory = Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "OrtoInventory");
            string directory_Index = Path.Combine(directory, "index");
            Directory.CreateDirectory(directory_Index);

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

            string capabilities = await httpClient.GetStringAsync(Constants.WebService.OrtoSheetIndex + "?SERVICE=WFS&VERSION=1.1.0&REQUEST=GetCapabilities");
            List<int> years = [.. Regex.Matches(capabilities, @"SkorowidzOrtofomapy(\d{4})").Select(x => int.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().OrderBy(x => x)];
            Assert.NotEmpty(years);

            List<string> lines_Counts = ["year\thits\trows\tfootprintOutside"];
            List<Classes.OrtoSheet> ortoSheets = [];

            foreach (int year in years)
            {
                string typeName = Query.OrtoSheetTypeName(year);
                string path_Json = Path.Combine(directory_Index, $"sheets-{year.ToString(CultureInfo.InvariantCulture)}.json");

                int? count = await Query.OrtoSheetCountAsync(httpClient, typeName, null, 5, TimeSpan.FromSeconds(10));
                Assert.NotNull(count);

                List<Classes.OrtoSheet>? ortoSheets_Year = null;
                if (File.Exists(path_Json))
                {
                    ortoSheets_Year = Core.Convert.ToDiGi<Classes.OrtoSheet>(File.ReadAllText(path_Json));
                }

                if (ortoSheets_Year == null || ortoSheets_Year.Count != count)
                {
                    ortoSheets_Year = await Query.OrtoSheetsAsync(httpClient, typeName, null, Constants.WebService.OrtoSheetPageSize, 5, TimeSpan.FromSeconds(10), DateTimeOffset.UtcNow);
                    Assert.NotNull(ortoSheets_Year);

                    File.WriteAllText(path_Json, Core.Convert.ToSystem_String(ortoSheets_Year));
                    File.WriteAllLines(Path.Combine(directory_Index, $"sheets-{year.ToString(CultureInfo.InvariantCulture)}.tsv"), Convert.ToSystem_Strings(ortoSheets_Year)!);
                }

                int footprintOutside = 0;
                foreach (Classes.OrtoSheet ortoSheet in ortoSheets_Year)
                {
                    BoundingBox2D? boundingBox2D = ortoSheet.Footprint?.GetBoundingBox();
                    if (boundingBox2D == null || boundingBox2D.Min.X < 140000 || boundingBox2D.Max.X > 880000 || boundingBox2D.Min.Y < 120000 || boundingBox2D.Max.Y > 790000)
                    {
                        footprintOutside++;
                    }
                }

                lines_Counts.Add(string.Join("\t", year.ToString(CultureInfo.InvariantCulture), count.Value.ToString(CultureInfo.InvariantCulture), ortoSheets_Year.Count.ToString(CultureInfo.InvariantCulture), footprintOutside.ToString(CultureInfo.InvariantCulture)));
                File.WriteAllLines(Path.Combine(directory, "index-counts.tsv"), lines_Counts);

                ortoSheets.AddRange(ortoSheets_Year);
            }

            List<List<Classes.OrtoSheet>> groups = Query.OrtoSheetDuplicateGroups(ortoSheets)!;
            Assert.Equal(ortoSheets.Count, groups.Sum(x => x.Count));

            List<string> lines_Groups = ["year\tgroupSize\tgroupCount"];
            foreach (IGrouping<(int, int), List<Classes.OrtoSheet>> grouping in groups.GroupBy(x => (x[0].Year, x.Count)).OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2))
            {
                lines_Groups.Add(string.Join("\t", grouping.Key.Item1.ToString(CultureInfo.InvariantCulture), grouping.Key.Item2.ToString(CultureInfo.InvariantCulture), grouping.Count().ToString(CultureInfo.InvariantCulture)));
            }

            File.WriteAllLines(Path.Combine(directory, "duplicate-groups.tsv"), lines_Groups);
            File.WriteAllLines(Path.Combine(directory, "sheets-canonical.tsv"), Convert.ToSystem_Strings(groups.Select(x => x[0]))!);

            List<string> lines_Settings = ["cellSize\tcoverageFraction\tcells\tepochs"];
            foreach (double cellSize in new double[] { 2500, 5000 })
            {
                foreach (double coverageFraction in new double[] { 0.5, 0.9 })
                {
                    Classes.OrtoAvailabilityMatrix? ortoAvailabilityMatrix = Create.OrtoAvailabilityMatrix(ortoSheets, cellSize, coverageFraction);
                    Assert.NotNull(ortoAvailabilityMatrix);

                    string suffix = cellSize.ToString("R", CultureInfo.InvariantCulture) + "-" + coverageFraction.ToString("R", CultureInfo.InvariantCulture);
                    File.WriteAllLines(Path.Combine(directory, $"availability-matrix-{suffix}.tsv"), Convert.ToSystem_Strings(ortoAvailabilityMatrix)!);

                    List<Classes.OrtoAvailability> ortoAvailabilities = ortoAvailabilityMatrix.OrtoAvailabilities ?? [];
                    int epochCount = ortoAvailabilities.Sum(x => x.OrtoEpochs?.Count ?? 0);
                    lines_Settings.Add(string.Join("\t", cellSize.ToString("R", CultureInfo.InvariantCulture), coverageFraction.ToString("R", CultureInfo.InvariantCulture), ortoAvailabilities.Count.ToString(CultureInfo.InvariantCulture), epochCount.ToString(CultureInfo.InvariantCulture)));
                    File.WriteAllLines(Path.Combine(directory, "matrix-settings.tsv"), lines_Settings);
                }
            }

            foreach (string line in lines_Counts.Skip(1))
            {
                string[] values = line.Split('\t');
                Assert.Equal(values[1], values[2]);
                Assert.Equal("0", values[3]);
            }
        }
    }
}
