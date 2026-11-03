using DiGi.GIS.Classes;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Index.Strtree;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Production run of DiGi.GIS#17, follow-up for task T17 of papers 01/03: the earliest dated orthophoto observation per county part, from the delivered availability matrix (2.5 km cells, coverage 0.5) written by <see cref="OrtoInventory_Index"/>.
        /// <para>The county parts are read through the deployed API: polygons from <c>itemsbyadministrativearealtype</c>, ids from <c>administrativeareal2dreferencesbyadministrativearealtype</c>, joined by <c>Reference</c>. Every part is reported under its own id, because a county code maps to one row per polygon part (406 parts, 380 codes).</para>
        /// <para>A cell belongs to the part that contains its centre. A part containing no cell centre (a small city) takes the cells its polygon intersects, flagged in the <c>method</c> column. Per part: the earliest observation date in any of its cells with that date's precision (an analog year placeholder is <c>Year</c>), and the dates by which 50 % and 90 % of its cells have at least one observation.</para>
        /// <para>Runs only when the environment variable <c>DIGI_ORTO_INVENTORY_RUN</c> is <c>1</c> (and DIGI_TEST_MAX_DURATION is Long); otherwise it returns at once. Writes <c>user files/reports/OrtoInventory/delivery/earliest-by-county.tsv</c>.</para>
        /// <para>Long test (minutes; the county polygons are about 200 MB of JSON).</para>
        /// </summary>
        [LongFact]
        public async Task OrtoInventory_Counties()
        {
            if (Environment.GetEnvironmentVariable("DIGI_ORTO_INVENTORY_RUN") != "1")
            {
                return;
            }

            string directory = Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "OrtoInventory", "delivery");
            string path_Matrix = Path.Combine(directory, "availability-matrix-2500-0.5.tsv");
            Assert.True(File.Exists(path_Matrix), path_Matrix);

            // Per cell (minimum corner), the earliest observation date and its precision.
            Dictionary<(double, double), (DateTimeOffset, string)> dictionary_Cells = [];
            double cellSize = 0;
            using (StreamReader streamReader = new(path_Matrix))
            {
                string[] headers = streamReader.ReadLine()!.Split('\t');
                int index_MinEasting = Array.IndexOf(headers, "MinEasting");
                int index_MinNorthing = Array.IndexOf(headers, "MinNorthing");
                int index_CellSize = Array.IndexOf(headers, "CellSize");
                int index_Date = Array.IndexOf(headers, "Date");
                int index_DatePrecision = Array.IndexOf(headers, "DatePrecision");

                string? line;
                while ((line = streamReader.ReadLine()) != null)
                {
                    string[] values = line.Split('\t');
                    if (values[index_Date].Length == 0)
                    {
                        continue;
                    }

                    (double, double) key = (double.Parse(values[index_MinEasting], CultureInfo.InvariantCulture), double.Parse(values[index_MinNorthing], CultureInfo.InvariantCulture));
                    cellSize = double.Parse(values[index_CellSize], CultureInfo.InvariantCulture);
                    DateTimeOffset date = DateTimeOffset.Parse(values[index_Date], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

                    if (!dictionary_Cells.TryGetValue(key, out (DateTimeOffset, string) value) || date < value.Item1)
                    {
                        dictionary_Cells[key] = (date, values[index_DatePrecision]);
                    }
                }
            }

            Assert.NotEmpty(dictionary_Cells);

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(15) };

            string json_References = await httpClient.GetStringAsync("https://api.digiproject.uk/gis/administrativeareal2d/administrativeareal2dreferencesbyadministrativearealtype?administrativearealtype=2");
            Dictionary<string, int> dictionary_Ids = [];
            foreach (JsonNode? jsonNode in JsonNode.Parse(json_References)!.AsArray())
            {
                dictionary_Ids[jsonNode!["Reference"]!.GetValue<string>()] = jsonNode["Id"]!.GetValue<int>();
            }

            string json_Items = await httpClient.GetStringAsync("https://api.digiproject.uk/gis/administrativeareal2D/itemsbyadministrativearealtype?administrativearealtype=2");
            List<AdministrativeDivision> administrativeDivisions = Core.Convert.ToDiGi<AdministrativeDivision>(json_Items)!;
            Assert.Equal(dictionary_Ids.Count, administrativeDivisions.Count);

            List<(AdministrativeDivision, Polygon)> items = [];
            STRtree<int> sTRtree = new();
            foreach (AdministrativeDivision administrativeDivision in administrativeDivisions)
            {
                Polygon? polygon = Geometry.Planar.Convert.ToNTS(administrativeDivision.PolygonalFace2D);
                Assert.NotNull(polygon);
                sTRtree.Insert(polygon.EnvelopeInternal, items.Count);
                items.Add((administrativeDivision, polygon));
            }

            sTRtree.Build();

            List<IPreparedGeometry> preparedGeometries = [.. items.Select(x => PreparedGeometryFactory.Prepare(x.Item2))];
            List<List<(double, double)>> cells = [.. items.Select(x => new List<(double, double)>())];

            GeometryFactory geometryFactory = new();
            double minX = items.Min(x => x.Item2.EnvelopeInternal.MinX);
            double maxX = items.Max(x => x.Item2.EnvelopeInternal.MaxX);
            double minY = items.Min(x => x.Item2.EnvelopeInternal.MinY);
            double maxY = items.Max(x => x.Item2.EnvelopeInternal.MaxY);

            for (double x = Math.Floor(minX / cellSize) * cellSize; x < maxX; x += cellSize)
            {
                for (double y = Math.Floor(minY / cellSize) * cellSize; y < maxY; y += cellSize)
                {
                    Point point = geometryFactory.CreatePoint(new Coordinate(x + (cellSize / 2), y + (cellSize / 2)));
                    foreach (int index in sTRtree.Query(point.EnvelopeInternal))
                    {
                        if (preparedGeometries[index].Contains(point))
                        {
                            cells[index].Add((x, y));
                            break;
                        }
                    }
                }
            }

            List<string> lines = ["id\tcode\tname\tmethod\tcells\tcellsObserved\tearliestDate\tearliestDatePrecision\tdate50\tdate90"];
            for (int i = 0; i < items.Count; i++)
            {
                AdministrativeDivision administrativeDivision = items[i].Item1;
                List<(double, double)> cells_Part = cells[i];
                string method = "centre";

                if (cells_Part.Count == 0)
                {
                    method = "intersects";
                    Envelope envelope = items[i].Item2.EnvelopeInternal;
                    for (double x = Math.Floor(envelope.MinX / cellSize) * cellSize; x < envelope.MaxX; x += cellSize)
                    {
                        for (double y = Math.Floor(envelope.MinY / cellSize) * cellSize; y < envelope.MaxY; y += cellSize)
                        {
                            if (preparedGeometries[i].Intersects(geometryFactory.ToGeometry(new Envelope(x, x + cellSize, y, y + cellSize))))
                            {
                                cells_Part.Add((x, y));
                            }
                        }
                    }
                }

                List<(DateTimeOffset, string)> dates = [.. cells_Part.Where(dictionary_Cells.ContainsKey).Select(x => dictionary_Cells[x]).OrderBy(x => x.Item1)];

                string earliestDate = dates.Count == 0 ? string.Empty : Format(dates[0].Item1);
                string earliestDatePrecision = dates.Count == 0 ? string.Empty : dates[0].Item2;

                Assert.True(dictionary_Ids.TryGetValue(administrativeDivision.Reference!, out int id), administrativeDivision.Reference);

                lines.Add(string.Join("\t",
                    id.ToString(CultureInfo.InvariantCulture),
                    administrativeDivision.Code,
                    administrativeDivision.Name,
                    method,
                    cells_Part.Count.ToString(CultureInfo.InvariantCulture),
                    dates.Count.ToString(CultureInfo.InvariantCulture),
                    earliestDate,
                    earliestDatePrecision,
                    Quantile(0.5),
                    Quantile(0.9)));

                // The date by which the given share of all cells (observed or not) has at least one observation.
                string Quantile(double fraction)
                {
                    int count = (int)Math.Ceiling(fraction * cells_Part.Count);
                    return count == 0 || count > dates.Count ? string.Empty : Format(dates[count - 1].Item1);
                }
            }

            File.WriteAllLines(Path.Combine(directory, "earliest-by-county.tsv"), lines);

            Assert.Equal(406, lines.Count - 1);

            static string Format(DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }
    }
}
