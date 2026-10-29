using DiGi.BDOT10k.Geometry.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.PRG.Geometry.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Measures, per sample county, how well the BDOT10k road network can locate a building's front facade from its PRG address point, and writes the table to <c>reports/FrontFacade_Counties.md</c>. Reference: DiGi.Test#5, tracking issue DiGi.GIS.PostgreSQL#117.
        /// <para>Inputs are machine-specific and read from <c>user files/DiGi.PRG.Geometry_FrontFacade.conf</c>: <c>BDOT10kArchive</c> (the national <c>Polska_GML.zip</c>, read in place: voivodeship zip, then county zip, then the <c>OT_BUBD_A</c>, <c>OT_SKDR_L</c>, <c>OT_SKJZ_L</c> and <c>OT_SKRP_L</c> files), <c>PRGDirectory</c> (one <c>NN_*.gml</c> per voivodeship) and <c>Counties</c> (comma-separated county codes). Without the file, or with a missing input, the fact returns without measuring anything.</para>
        /// <para>Per county: the PRG points whose locality commune lies in it are matched to a BDOT10k footprint like the import does (inside, else the nearest within 10 m). For each matched point the four rules of <see cref="FrontFacade_Rules"/> pick an edge: same-ULIC road (SKJZ/SKRP with the point's ULIC within 50 m of the footprint), nearest road (any class within 200 m), address point (nearest edge) and longest edge. The table reports the ULIC join rate, road distances and how often the rules agree, counting two edges facing the same side (<see cref="FrontFacade_SameSide"/>) as agreeing and giving the exact-edge rate beside it. Counts are per address point, so a building with several points counts once per point. Long test (voivodeship GML files of up to 1.6 GB).</para>
        /// </summary>
        [LongFact]
        public void FrontFacade_Counties()
        {
            const double distance_Match = 10.0;

            Assembly assembly = Assembly.GetExecutingAssembly();

            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return;
            }

            string path_Configuration = Path.Combine(directory_UserFiles!, "DiGi.PRG.Geometry_FrontFacade.conf");
            if (!File.Exists(path_Configuration))
            {
                return;
            }

            Dictionary<string, string> settings = [];
            foreach (string line in File.ReadAllLines(path_Configuration))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
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

            settings.TryGetValue("BDOT10kArchive", out string? path_Archive);
            settings.TryGetValue("PRGDirectory", out string? directory_PRG);
            settings.TryGetValue("Counties", out string? counties);

            if (string.IsNullOrWhiteSpace(path_Archive) || !File.Exists(path_Archive) || string.IsNullOrWhiteSpace(directory_PRG) || !Directory.Exists(directory_PRG) || string.IsNullOrWhiteSpace(counties))
            {
                return;
            }

            List<string> codes = [.. counties.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];
            Assert.NotEmpty(codes);

            List<string> rows = [];
            foreach (IGrouping<string, string> grouping in codes.GroupBy(x => x.Substring(0, 2)))
            {
                string? path_PRG = Directory.GetFiles(directory_PRG, $"{grouping.Key}_*.gml").FirstOrDefault();
                Assert.NotNull(path_PRG);

                HashSet<string> codes_Voivodeship = [.. grouping];
                List<PunktAdresowy>? punktyAdresowe = Create.PunktyAdresowe(path_PRG)?.FindAll(x => x.TERYTGminy is string tERYTGminy && tERYTGminy.Length >= 4 && codes_Voivodeship.Contains(tERYTGminy.Substring(0, 4)));
                Assert.NotNull(punktyAdresowe);

                string path_Voivodeship = Path.Combine(Path.GetTempPath(), $"DiGi_FrontFacade_{grouping.Key}_{Guid.NewGuid():N}.zip");
                try
                {
                    using (ZipArchive zipArchive_Country = ZipFile.OpenRead(path_Archive))
                    {
                        ZipArchiveEntry? zipArchiveEntry_Voivodeship = zipArchive_Country.GetEntry($"BDOT10k/{grouping.Key}_GML.zip");
                        Assert.NotNull(zipArchiveEntry_Voivodeship);
                        zipArchiveEntry_Voivodeship.ExtractToFile(path_Voivodeship);
                    }

                    using ZipArchive zipArchive_Voivodeship = ZipFile.OpenRead(path_Voivodeship);
                    foreach (string code in grouping)
                    {
                        ZipArchiveEntry? zipArchiveEntry_County = zipArchive_Voivodeship.GetEntry($"{grouping.Key}/{code}_GML.zip");
                        Assert.NotNull(zipArchiveEntry_County);

                        using MemoryStream memoryStream_County = new();
                        using (Stream stream = zipArchiveEntry_County.Open())
                        {
                            stream.CopyTo(memoryStream_County);
                        }

                        memoryStream_County.Position = 0;
                        SlownikObiektowGeometrycznych slownikObiektowGeometrycznych = new();
                        using (ZipArchive zipArchive_County = new(memoryStream_County, ZipArchiveMode.Read))
                        {
                            foreach (ZipArchiveEntry zipArchiveEntry in zipArchive_County.Entries)
                            {
                                if (!(zipArchiveEntry.Name.EndsWith("__OT_BUBD_A.xml", StringComparison.Ordinal) || zipArchiveEntry.Name.EndsWith("__OT_SKDR_L.xml", StringComparison.Ordinal) || zipArchiveEntry.Name.EndsWith("__OT_SKJZ_L.xml", StringComparison.Ordinal) || zipArchiveEntry.Name.EndsWith("__OT_SKRP_L.xml", StringComparison.Ordinal)))
                                {
                                    continue;
                                }

                                using MemoryStream memoryStream_Entry = new();
                                using (Stream stream = zipArchiveEntry.Open())
                                {
                                    stream.CopyTo(memoryStream_Entry);
                                }

                                memoryStream_Entry.Position = 0;
                                Assert.True(BDOT10k.Geometry.Modify.Load(slownikObiektowGeometrycznych, memoryStream_Entry));
                            }
                        }

                        rows.Add(Measure(code, slownikObiektowGeometrycznych, punktyAdresowe.FindAll(x => x.TERYTGminy!.StartsWith(code, StringComparison.Ordinal))));
                    }
                }
                finally
                {
                    if (File.Exists(path_Voivodeship))
                    {
                        File.Delete(path_Voivodeship);
                    }
                }
            }

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("# Front facade from address point and road: per county (DiGi.Test#5)");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"BDOT10k: `{Path.GetFileName(Path.GetDirectoryName(path_Archive))}/{Path.GetFileName(path_Archive)}`; PRG: `{Path.GetFileName(directory_PRG)}`; run {DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture)}.");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Rule comparisons count two edges as agreeing when they face the same side of the building (outward normals within 20 degrees); the exact-edge rate follows in brackets. Shares of points are per address point.");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| County | Points | No ULIC | Matched | Same-ULIC road <= 50 m | Nearest road <= 200 m | Road distance median / P90 (m) | Same-ULIC distance median / P90 (m) | Same-ULIC vs nearest road differ | Same-ULIC = address point | Nearest road = address point | Longest = address point | Buildings | Road segments |");
            stringBuilder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            rows.ForEach(x => stringBuilder.AppendLine(x));

            string directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly)!;
            File.WriteAllText(Path.Combine(directory_Reports, "FrontFacade_Counties.md"), stringBuilder.ToString());

            Assert.Equal(codes.Count, rows.Count);

            string Measure(string code, SlownikObiektowGeometrycznych slownikObiektowGeometrycznych, List<PunktAdresowy> punktyAdresowe_County)
            {
                List<(Point2D Start, Point2D End, string? ULIC)> roadSegments = FrontFacade_RoadSegments(slownikObiektowGeometrycznych);
                Assert.NotEmpty(roadSegments);
                Dictionary<(long, long), List<int>> roadIndex = FrontFacade_Index(roadSegments);

                List<List<Point2D>> rings = [];
                foreach (BUBD_A bUBD_A in slownikObiektowGeometrycznych.GetObiektyGeometryczne<BUBD_A>() ?? [])
                {
                    List<Point2D>? ring = FrontFacade_Ring(bUBD_A.Geometry?.ExternalEdge?.GetPoints());
                    if (ring is not null)
                    {
                        rings.Add(ring);
                    }
                }

                Assert.NotEmpty(rings);

                // Footprints indexed by their edges, so the same grid serves the inside test and the 10 m match
                List<(Point2D Start, Point2D End, string? ULIC)> edges = [];
                List<int> edgeRings = [];
                for (int i = 0; i < rings.Count; i++)
                {
                    for (int j = 0; j < rings[i].Count; j++)
                    {
                        edges.Add((rings[i][j], rings[i][(j + 1) % rings[i].Count], null));
                        edgeRings.Add(i);
                    }
                }

                Dictionary<(long, long), List<int>> edgeIndex = FrontFacade_Index(edges);

                int count_NoULIC = 0;
                int count_Matched = 0;
                int count_SameULIC = 0;
                int count_NearestRoad = 0;
                int count_Compared_RoadRules = 0;
                int count_Differ_RoadRules = 0;
                int count_Differ_RoadRules_Exact = 0;
                int count_SameULIC_AddressPoint = 0;
                int count_SameULIC_AddressPoint_Exact = 0;
                int count_NearestRoad_AddressPoint = 0;
                int count_NearestRoad_AddressPoint_Exact = 0;
                int count_Longest_AddressPoint = 0;
                int count_Longest_AddressPoint_Exact = 0;
                List<double> distances_Road = [];
                List<double> distances_SameULIC = [];

                foreach (PunktAdresowy punktAdresowy in punktyAdresowe_County)
                {
                    Point2D? point2D = punktAdresowy.Geometry;
                    if (point2D is null)
                    {
                        continue;
                    }

                    string? uLIC = string.IsNullOrWhiteSpace(punktAdresowy.identyfikatorULIC) ? null : punktAdresowy.identyfikatorULIC.Trim();
                    if (uLIC is null)
                    {
                        count_NoULIC++;
                    }

                    List<Point2D>? ring = MatchedRing(point2D);
                    if (ring is null)
                    {
                        continue;
                    }

                    count_Matched++;

                    int edge_AddressPoint = FrontFacade_NearestEdge(ring, point2D);
                    int edge_Longest = FrontFacade_LongestEdge(ring);
                    count_Longest_AddressPoint += FrontFacade_SameSide(ring, edge_Longest, edge_AddressPoint) ? 1 : 0;
                    count_Longest_AddressPoint_Exact += edge_Longest == edge_AddressPoint ? 1 : 0;

                    (Point2D? point2D_Road, double distance_Road) = FrontFacade_NearestRoad(ring, roadSegments, roadIndex, null, frontFacade_SearchDistance);
                    int edge_NearestRoad = -1;
                    if (point2D_Road is not null)
                    {
                        count_NearestRoad++;
                        distances_Road.Add(distance_Road);
                        edge_NearestRoad = FrontFacade_FacingEdge(ring, point2D_Road);
                        count_NearestRoad_AddressPoint += FrontFacade_SameSide(ring, edge_NearestRoad, edge_AddressPoint) ? 1 : 0;
                        count_NearestRoad_AddressPoint_Exact += edge_NearestRoad == edge_AddressPoint ? 1 : 0;
                    }

                    if (uLIC is null)
                    {
                        continue;
                    }

                    (Point2D? point2D_SameULIC, double distance_SameULIC) = FrontFacade_NearestRoad(ring, roadSegments, roadIndex, uLIC, frontFacade_SearchDistance);
                    if (point2D_SameULIC is null)
                    {
                        continue;
                    }

                    distances_SameULIC.Add(distance_SameULIC);
                    if (distance_SameULIC > frontFacade_SameULICDistance)
                    {
                        continue;
                    }

                    count_SameULIC++;
                    int edge_SameULIC = FrontFacade_FacingEdge(ring, point2D_SameULIC);
                    count_SameULIC_AddressPoint += FrontFacade_SameSide(ring, edge_SameULIC, edge_AddressPoint) ? 1 : 0;
                    count_SameULIC_AddressPoint_Exact += edge_SameULIC == edge_AddressPoint ? 1 : 0;

                    if (edge_NearestRoad != -1)
                    {
                        count_Compared_RoadRules++;
                        count_Differ_RoadRules += FrontFacade_SameSide(ring, edge_NearestRoad, edge_SameULIC) ? 0 : 1;
                        count_Differ_RoadRules_Exact += edge_NearestRoad != edge_SameULIC ? 1 : 0;
                    }
                }

                Assert.True(count_Matched > 0);

                return string.Join(" | ", "| " + code, Count(punktyAdresowe_County.Count), Share(count_NoULIC, punktyAdresowe_County.Count), Share(count_Matched, punktyAdresowe_County.Count), Share(count_SameULIC, count_Matched), Share(count_NearestRoad, count_Matched), Percentiles(distances_Road), Percentiles(distances_SameULIC), Pair(count_Differ_RoadRules, count_Differ_RoadRules_Exact, count_Compared_RoadRules), Pair(count_SameULIC_AddressPoint, count_SameULIC_AddressPoint_Exact, count_SameULIC), Pair(count_NearestRoad_AddressPoint, count_NearestRoad_AddressPoint_Exact, count_NearestRoad), Pair(count_Longest_AddressPoint, count_Longest_AddressPoint_Exact, count_Matched), Count(rings.Count), Count(roadSegments.Count) + " |");

                List<Point2D>? MatchedRing(Point2D point2D)
                {
                    HashSet<int> visited = [];
                    int ring_Nearest = -1;
                    double distance_Nearest = double.MaxValue;
                    for (long cellX = FrontFacade_Cell(point2D.X - distance_Match); cellX <= FrontFacade_Cell(point2D.X + distance_Match); cellX++)
                    {
                        for (long cellY = FrontFacade_Cell(point2D.Y - distance_Match); cellY <= FrontFacade_Cell(point2D.Y + distance_Match); cellY++)
                        {
                            if (!edgeIndex.TryGetValue((cellX, cellY), out List<int>? indexes))
                            {
                                continue;
                            }

                            foreach (int i in indexes)
                            {
                                int ring_Index = edgeRings[i];
                                if (!visited.Add(ring_Index))
                                {
                                    continue;
                                }

                                List<Point2D> ring = rings[ring_Index];
                                if (Inside(ring, point2D))
                                {
                                    return ring;
                                }

                                for (int j = 0; j < ring.Count; j++)
                                {
                                    double distance = FrontFacade_Closest(point2D.X, point2D.Y, ring[j], ring[(j + 1) % ring.Count]).Distance;
                                    if (distance < distance_Nearest)
                                    {
                                        distance_Nearest = distance;
                                        ring_Nearest = ring_Index;
                                    }
                                }
                            }
                        }
                    }

                    return distance_Nearest <= distance_Match ? rings[ring_Nearest] : null;
                }
            }

            static bool Inside(List<Point2D> ring, Point2D point2D)
            {
                bool result = false;
                for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                {
                    if ((ring[i].Y > point2D.Y) != (ring[j].Y > point2D.Y) && point2D.X < ((ring[j].X - ring[i].X) * (point2D.Y - ring[i].Y) / (ring[j].Y - ring[i].Y)) + ring[i].X)
                    {
                        result = !result;
                    }
                }

                return result;
            }

            static string Count(int value)
            {
                return value.ToString("N0", CultureInfo.InvariantCulture).Replace(",", " ");
            }

            static string Share(int value, int total)
            {
                return total == 0 ? "-" : $"{Count(value)} ({(100.0 * value / total).ToString("0.0", CultureInfo.InvariantCulture)} %)";
            }

            static string Pair(int value_SameSide, int value_Exact, int total)
            {
                return total == 0 ? "-" : $"{(100.0 * value_SameSide / total).ToString("0.0", CultureInfo.InvariantCulture)} % ({(100.0 * value_Exact / total).ToString("0.0", CultureInfo.InvariantCulture)} %)";
            }

            static string Percentiles(List<double> values)
            {
                if (values.Count == 0)
                {
                    return "-";
                }

                values.Sort();
                return $"{values[values.Count / 2].ToString("0.0", CultureInfo.InvariantCulture)} / {values[Math.Min(values.Count - 1, (int)(0.9 * values.Count))].ToString("0.0", CultureInfo.InvariantCulture)}";
            }
        }
    }
}
