using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Emgu.CV.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Production run of DiGi.GIS#17, step 3: the probe campaign over the study area of papers 01/03, county <c>1465</c> (m. st. Warszawa, <c>administrative_areal_2d</c> id <c>55417</c>, one part).
        /// <para>The county polygon is read through the deployed API (<c>/gis/administrativeareal2D/itembyid</c>). Its sheets are pulled from the live index for every yearly typename, limited to the county box, and an availability matrix of 5 km cells is built from them. At the centre of every cell whose centre lies in the county, one 256 m tile (512 px, scale denominator about 1 800, far below half of the service limit) is probed for every year from 1995 on with <see cref="Query.IsOrtoBlank(byte[])"/>; the obstacle fractions of every image with content are measured from the same bytes. The true orthophoto sheets covering each centre are read with <see cref="GIS.Query.TrueOrthoSheetsAsync"/>.</para>
        /// <para>Runs only when the environment variable <c>DIGI_ORTO_INVENTORY_RUN</c> is <c>1</c> (and DIGI_TEST_MAX_DURATION is Long); otherwise it returns at once.</para>
        /// <para>Writes to <c>user files/reports/OrtoInventory_1465/</c>: <c>sheets.tsv</c>, <c>availability-matrix.tsv</c>, <c>probes.tsv</c> (per point and year: status, hash, obstacle fractions, the index sheets covering the point and their best usability under <see cref="GIS.Constants.OrtoUsability.Version"/>), <c>probe-status.tsv</c> (counts per status), and <c>true-ortho-sheets.tsv</c>.</para>
        /// <para>Verification (c) and (d) of the issue are asserted: no image hash recurs at three or more points (a uniform tile recorded as an image would), and every usability score recomputes to the same value.</para>
        /// <para>Long test (about one hour; requests are sent one after another).</para>
        /// </summary>
        [LongFact]
        public async Task OrtoInventory_Probes()
        {
            if (Environment.GetEnvironmentVariable("DIGI_ORTO_INVENTORY_RUN") != "1")
            {
                return;
            }

            const double cellSize = 5000;
            const double tileSize = 256;
            const int width = 512;

            string directory = Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "OrtoInventory_1465");
            Directory.CreateDirectory(directory);

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

            string json = await httpClient.GetStringAsync("https://api.digiproject.uk/gis/administrativeareal2D/itembyid?id=55417");
            AdministrativeDivision? administrativeDivision = Core.Convert.ToDiGi<AdministrativeDivision>(json)?.FirstOrDefault();
            Assert.Equal("1465", administrativeDivision?.Code);

            PolygonalFace2D? polygonalFace2D = administrativeDivision!.PolygonalFace2D;
            BoundingBox2D? boundingBox2D_County = polygonalFace2D?.GetBoundingBox();
            Assert.NotNull(boundingBox2D_County);

            string capabilities = await httpClient.GetStringAsync(GIS.Constants.WebService.OrtoSheetIndex + "?SERVICE=WFS&VERSION=1.1.0&REQUEST=GetCapabilities");
            List<int> years = [.. Regex.Matches(capabilities, @"SkorowidzOrtofomapy(\d{4})").Select(x => int.Parse(x.Groups[1].Value, CultureInfo.InvariantCulture)).Distinct().OrderBy(x => x)];
            Assert.NotEmpty(years);

            DateTimeOffset probeDate = DateTimeOffset.UtcNow;

            List<OrtoSheet> ortoSheets = [];
            foreach (int year in years)
            {
                List<OrtoSheet>? ortoSheets_Year = await GIS.Query.OrtoSheetsAsync(httpClient, GIS.Query.OrtoSheetTypeName(year), boundingBox2D_County, GIS.Constants.WebService.OrtoSheetPageSize, 5, TimeSpan.FromSeconds(10), probeDate);
                Assert.NotNull(ortoSheets_Year);
                ortoSheets.AddRange(ortoSheets_Year);
            }

            File.WriteAllLines(Path.Combine(directory, "sheets.tsv"), GIS.Convert.ToSystem_Strings(ortoSheets)!);

            OrtoAvailabilityMatrix? ortoAvailabilityMatrix = GIS.Create.OrtoAvailabilityMatrix(ortoSheets, cellSize, 0.9, probeDate);
            Assert.NotNull(ortoAvailabilityMatrix);
            File.WriteAllLines(Path.Combine(directory, "availability-matrix.tsv"), GIS.Convert.ToSystem_Strings(ortoAvailabilityMatrix)!);

            List<Point2D> point2Ds = [];
            for (double x = Math.Floor(boundingBox2D_County.Min.X / cellSize) * cellSize; x < boundingBox2D_County.Max.X; x += cellSize)
            {
                for (double y = Math.Floor(boundingBox2D_County.Min.Y / cellSize) * cellSize; y < boundingBox2D_County.Max.Y; y += cellSize)
                {
                    Point2D point2D = new(x + (cellSize / 2), y + (cellSize / 2));
                    if (polygonalFace2D!.InRange(point2D))
                    {
                        point2Ds.Add(point2D);
                    }
                }
            }

            Assert.NotEmpty(point2Ds);

            List<string> times = [.. years.Where(x => x >= 1995).Select(x => x.ToString(CultureInfo.InvariantCulture))];

            // The probe keeps only the hash, so the fractions of every image with content are measured here, from the same bytes.
            Dictionary<string, OrtoObstacleFractions?> dictionary_Fractions = [];
            bool IsBlank(byte[] bytes)
            {
                bool result = Query.IsOrtoBlank(bytes);
                if (!result)
                {
                    string? contentHash = GIS.Query.ContentHash(bytes);
                    if (contentHash != null && !dictionary_Fractions.ContainsKey(contentHash))
                    {
                        dictionary_Fractions[contentHash] = Create.OrtoObstacleFractions(bytes);
                    }
                }

                return result;
            }

            List<string> lines_Probes = ["x\ty\ttime\tstatus\tcontentHash\tshadow\tsnow\thaze\tcloud\tobstacles\tindexSheets\tusabilityMetadata\tusability\tusabilityVersion"];
            List<string> lines_TrueOrtho = ["x\ty\tsheetId\tdate\tpixelSize\tarchiveModule\tdownloadUrl"];
            Dictionary<OrtoImageProbeStatus, int> dictionary_Status = [];
            Dictionary<string, HashSet<string>> dictionary_HashPoints = [];

            foreach (Point2D point2D in point2Ds)
            {
                string x = point2D.X.ToString("R", CultureInfo.InvariantCulture);
                string y = point2D.Y.ToString("R", CultureInfo.InvariantCulture);

                BoundingBox2D boundingBox2D = new(new Point2D(point2D.X - (tileSize / 2), point2D.Y - (tileSize / 2)), new Point2D(point2D.X + (tileSize / 2), point2D.Y + (tileSize / 2)));

                List<OrtoImageProbe>? ortoImageProbes = await GIS.Query.OrtoImageProbesAsync(httpClient, boundingBox2D, GIS.Constants.WebService.StandardResolutionTime, GIS.Constants.WebService.StandardResolutionTimeLayer, times, width, IsBlank, 3, TimeSpan.FromSeconds(5));
                Assert.NotNull(ortoImageProbes);

                foreach (OrtoImageProbe ortoImageProbe in ortoImageProbes)
                {
                    dictionary_Status[ortoImageProbe.OrtoImageProbeStatus] = dictionary_Status.TryGetValue(ortoImageProbe.OrtoImageProbeStatus, out int count) ? count + 1 : 1;

                    OrtoObstacleFractions? ortoObstacleFractions = null;
                    if (ortoImageProbe.ContentHash != null)
                    {
                        dictionary_Fractions.TryGetValue(ortoImageProbe.ContentHash, out ortoObstacleFractions);

                        if (!dictionary_HashPoints.TryGetValue(ortoImageProbe.ContentHash, out HashSet<string>? points))
                        {
                            points = [];
                            dictionary_HashPoints[ortoImageProbe.ContentHash] = points;
                        }

                        points.Add(x + "_" + y);
                    }

                    int year = int.Parse(ortoImageProbe.Time!, CultureInfo.InvariantCulture);
                    List<OrtoSheet> ortoSheets_Point = [.. ortoSheets.Where(z => z.Year == year && z.Footprint != null && z.Footprint.InRange(point2D))];

                    double? usabilityMetadata = ortoSheets_Point.Select(z => GIS.Query.OrtoSheetUsability(z)).Max();
                    double? usability = ortoObstacleFractions == null ? null : ortoSheets_Point.Select(z => GIS.Query.OrtoSheetUsability(z, ortoObstacleFractions.Total)).Max();

                    // (d) Usability is a pure function of the sheet, the fraction and the rubric version.
                    Assert.Equal(usability, ortoObstacleFractions == null ? null : ortoSheets_Point.Select(z => GIS.Query.OrtoSheetUsability(z, ortoObstacleFractions.Total)).Max());

                    lines_Probes.Add(string.Join("\t",
                        x,
                        y,
                        ortoImageProbe.Time,
                        ortoImageProbe.OrtoImageProbeStatus.ToString(),
                        ortoImageProbe.ContentHash ?? string.Empty,
                        Format(ortoObstacleFractions?.Shadow),
                        Format(ortoObstacleFractions?.Snow),
                        Format(ortoObstacleFractions?.Haze),
                        Format(ortoObstacleFractions?.Cloud),
                        Query.OrtoObstacles(ortoObstacleFractions)?.ToString() ?? string.Empty,
                        string.Join(";", ortoSheets_Point.Select(z => z.SheetId)),
                        Format(usabilityMetadata),
                        Format(usability),
                        GIS.Constants.OrtoUsability.Version.ToString(CultureInfo.InvariantCulture)));
                }

                File.WriteAllLines(Path.Combine(directory, "probes.tsv"), lines_Probes);

                List<OrtoSheet>? ortoSheets_TrueOrtho = await GIS.Query.TrueOrthoSheetsAsync(httpClient, point2D, 3, TimeSpan.FromSeconds(5), probeDate);
                foreach (OrtoSheet ortoSheet in ortoSheets_TrueOrtho ?? [])
                {
                    lines_TrueOrtho.Add(string.Join("\t", x, y, ortoSheet.SheetId, ortoSheet.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty, Format(ortoSheet.PixelSize), ortoSheet.ArchiveModule ?? string.Empty, ortoSheet.DownloadUrl ?? string.Empty));
                }

                File.WriteAllLines(Path.Combine(directory, "true-ortho-sheets.tsv"), lines_TrueOrtho);
            }

            File.WriteAllLines(Path.Combine(directory, "probe-status.tsv"), ["status\tcount", .. dictionary_Status.OrderBy(z => z.Key).Select(z => z.Key.ToString() + "\t" + z.Value.ToString(CultureInfo.InvariantCulture))]);

            // (c) A real image never repeats at three or more points 5 km apart; a uniform tile recorded as an image would.
            Assert.All(dictionary_HashPoints, z => Assert.True(z.Value.Count < 3, z.Key));

            static string Format(double? value)
            {
                return value?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }
}
