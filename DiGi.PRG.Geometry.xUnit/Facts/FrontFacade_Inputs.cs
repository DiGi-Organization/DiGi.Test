using DiGi.BDOT10k.Geometry.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.PRG.Geometry.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The largest distance from a footprint at which an address point outside every footprint is still matched to it, as the import does (<c>maxDistance</c> of <c>UpdateAddressPointsAsync</c>).
        /// </summary>
        private const double frontFacade_MatchDistance = 10.0;

        /// <summary>
        /// Reads <c>user files/DiGi.PRG.Geometry_FrontFacade.conf</c> (<c>BDOT10kArchive</c>, <c>PRGDirectory</c>, <c>Counties</c>) and returns its settings, or null when the file or any input it names is missing, so the facts using it return without measuring anything.
        /// </summary>
        /// <param name="assembly">The test assembly.</param>
        /// <returns>The settings, or null.</returns>
        private static Dictionary<string, string>? FrontFacade_Settings(Assembly assembly)
        {
            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return null;
            }

            string path_Configuration = Path.Combine(directory_UserFiles!, "DiGi.PRG.Geometry_FrontFacade.conf");
            if (!File.Exists(path_Configuration))
            {
                return null;
            }

            Dictionary<string, string> result = [];
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

                result[line.Substring(0, index).Trim()] = line.Substring(index + 1).Trim();
            }

            if (!result.TryGetValue("BDOT10kArchive", out string? path_Archive) || !File.Exists(path_Archive) || !result.TryGetValue("PRGDirectory", out string? directory_PRG) || !Directory.Exists(directory_PRG) || !result.TryGetValue("Counties", out string? counties) || string.IsNullOrWhiteSpace(counties))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Returns the county codes of the <c>Counties</c> setting, in the order given, without duplicates.
        /// </summary>
        /// <param name="settings">The settings of <see cref="FrontFacade_Settings"/>.</param>
        /// <returns>The county codes.</returns>
        private static List<string> FrontFacade_Codes(Dictionary<string, string> settings)
        {
            return [.. settings["Counties"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];
        }

        /// <summary>
        /// Loads every county of the settings and calls <paramref name="action"/> with its code, its BDOT10k buildings and roads, and the PRG points whose locality commune lies in it.
        /// <para>The BDOT10k county package is read in place from the national archive: the voivodeship zip is staged in the temp folder (deleted in <c>finally</c>), the county zip is read into memory and only <c>OT_BUBD_A</c>, <c>OT_SKDR_L</c>, <c>OT_SKJZ_L</c> and <c>OT_SKRP_L</c> are loaded. Each voivodeship PRG file is loaded once for all its counties.</para>
        /// </summary>
        /// <param name="settings">The settings of <see cref="FrontFacade_Settings"/>.</param>
        /// <param name="action">The action called once per county, in the order of the setting grouped by voivodeship.</param>
        private static void FrontFacade_ForEachCounty(Dictionary<string, string> settings, Action<string, SlownikObiektowGeometrycznych, List<PunktAdresowy>> action)
        {
            string path_Archive = settings["BDOT10kArchive"];
            string directory_PRG = settings["PRGDirectory"];

            foreach (IGrouping<string, string> grouping in FrontFacade_Codes(settings).GroupBy(x => x.Substring(0, 2)))
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

                        action(code, slownikObiektowGeometrycznych, punktyAdresowe.FindAll(x => x.TERYTGminy!.StartsWith(code, StringComparison.Ordinal)));
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
        }

        /// <summary>
        /// Returns every BDOT10k building with a usable footprint ring (<see cref="FrontFacade_Ring"/>), in load order.
        /// </summary>
        /// <param name="slownikObiektowGeometrycznych">The loaded BDOT10k objects.</param>
        /// <returns>The buildings and their counter-clockwise open rings.</returns>
        private static List<(BUBD_A BUBD_A, List<Point2D> Ring)> FrontFacade_Footprints(SlownikObiektowGeometrycznych slownikObiektowGeometrycznych)
        {
            List<(BUBD_A BUBD_A, List<Point2D> Ring)> result = [];
            foreach (BUBD_A bUBD_A in slownikObiektowGeometrycznych.GetObiektyGeometryczne<BUBD_A>() ?? [])
            {
                List<Point2D>? ring = FrontFacade_Ring(bUBD_A.Geometry?.ExternalEdge?.GetPoints());
                if (ring is not null)
                {
                    result.Add((bUBD_A, ring));
                }
            }

            return result;
        }

        /// <summary>
        /// Indexes footprints by their edges, so one grid serves the inside test and the 10 m match of <see cref="FrontFacade_Match"/>.
        /// </summary>
        /// <param name="rings">The footprint rings.</param>
        /// <returns>The grid index of all edges and, per edge, the index of its ring.</returns>
        private static (Dictionary<(long, long), List<int>> Index, List<int> EdgeRings) FrontFacade_FootprintIndex(List<List<Point2D>> rings)
        {
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

            return (FrontFacade_Index(edges), edgeRings);
        }

        /// <summary>
        /// Matches an address point to a footprint like the import does: the first footprint containing it, else the nearest within <see cref="frontFacade_MatchDistance"/>. Returns the ring index, or -1 when no footprint qualifies.
        /// </summary>
        /// <param name="point2D">The address point.</param>
        /// <param name="rings">The footprint rings.</param>
        /// <param name="footprintIndex">The index of <see cref="FrontFacade_FootprintIndex"/>.</param>
        /// <returns>The ring index, or -1.</returns>
        private static int FrontFacade_Match(Point2D point2D, List<List<Point2D>> rings, (Dictionary<(long, long), List<int>> Index, List<int> EdgeRings) footprintIndex)
        {
            HashSet<int> visited = [];
            int result = -1;
            double distance_Nearest = double.MaxValue;
            for (long cellX = FrontFacade_Cell(point2D.X - frontFacade_MatchDistance); cellX <= FrontFacade_Cell(point2D.X + frontFacade_MatchDistance); cellX++)
            {
                for (long cellY = FrontFacade_Cell(point2D.Y - frontFacade_MatchDistance); cellY <= FrontFacade_Cell(point2D.Y + frontFacade_MatchDistance); cellY++)
                {
                    if (!footprintIndex.Index.TryGetValue((cellX, cellY), out List<int>? indexes))
                    {
                        continue;
                    }

                    foreach (int i in indexes)
                    {
                        int ring_Index = footprintIndex.EdgeRings[i];
                        if (!visited.Add(ring_Index))
                        {
                            continue;
                        }

                        List<Point2D> ring = rings[ring_Index];
                        if (FrontFacade_Inside(ring, point2D))
                        {
                            return ring_Index;
                        }

                        for (int j = 0; j < ring.Count; j++)
                        {
                            double distance = FrontFacade_Closest(point2D.X, point2D.Y, ring[j], ring[(j + 1) % ring.Count]).Distance;
                            if (distance < distance_Nearest)
                            {
                                distance_Nearest = distance;
                                result = ring_Index;
                            }
                        }
                    }
                }
            }

            return distance_Nearest <= frontFacade_MatchDistance ? result : -1;
        }

        /// <summary>
        /// Returns whether a point lies inside a ring (even-odd rule).
        /// </summary>
        /// <param name="ring">The ring.</param>
        /// <param name="point2D">The point.</param>
        /// <returns>True when inside.</returns>
        private static bool FrontFacade_Inside(List<Point2D> ring, Point2D point2D)
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
    }
}
