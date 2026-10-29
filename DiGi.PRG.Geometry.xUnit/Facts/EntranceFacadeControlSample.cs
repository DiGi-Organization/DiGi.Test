using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The rules scored against the labelled entrances, in report order; <c>Cascade</c> is same-ULIC road, else nearest road, else longest edge.
        /// </summary>
        private static readonly string[] entranceFacadeControlSample_Rules = ["SameULIC", "NearestRoad", "AddressPoint", "Longest", "Cascade"];

        /// <summary>
        /// Tests that the committed entrance-facade control sample <c>EntranceFacadeControlSample.json</c> is complete and consistent. Reference: DiGi.Test#7.
        /// <para>The sample is drawn by <see cref="EntranceFacadeControlSample_Draw"/> (seed 117) and labelled by a person from imagery. Checked: 120 buildings, 30 per stratum, unique <c>BuildingId</c> and <c>Index</c> 0 to 119 in order; every ring counter-clockwise with at least 4 points; every building <c>Labelled</c> (an <c>EntranceEdge</c> that is an edge of its ring) or <c>Unknown</c> (no edge), none <c>Unlabelled</c>; the labeller and label date set; every building has at least one address point.</para>
        /// </summary>
        [Fact]
        public void EntranceFacadeControlSample_Integrity()
        {
            JsonObject sample = EntranceFacadeControlSample_Load();

            Assert.Equal(entranceFacadeControlSample_Seed, (int)sample["Seed"]!);
            Assert.False(string.IsNullOrWhiteSpace((string?)sample["Labeller"]));
            Assert.False(string.IsNullOrWhiteSpace((string?)sample["LabelDate"]));

            JsonArray buildings = sample["Buildings"]!.AsArray();
            Assert.Equal(4 * entranceFacadeControlSample_PerStratum, buildings.Count);

            HashSet<string> buildingIds = [];
            Dictionary<string, int> strata = [];
            for (int i = 0; i < buildings.Count; i++)
            {
                JsonObject building = buildings[i]!.AsObject();
                Assert.Equal(i, (int)building["Index"]!);
                Assert.True(buildingIds.Add((string)building["BuildingId"]!), $"Duplicate building {building["BuildingId"]}");

                string stratum = (string)building["Stratum"]!;
                strata[stratum] = strata.GetValueOrDefault(stratum) + 1;

                List<Point2D> ring = EntranceFacadeControlSample_Ring(building);
                Assert.True(ring.Count >= 4, $"Building {i}: fewer than 4 corners");
                Assert.True(FrontFacade_SignedArea(ring) > 0.0, $"Building {i}: ring is not counter-clockwise");
                Assert.NotEmpty(building["AddressPoints"]!.AsArray());

                string status = (string)building["Status"]!;
                int? entranceEdge = (int?)building["EntranceEdge"];
                switch (status)
                {
                    case "Labelled":
                        Assert.NotNull(entranceEdge);
                        Assert.InRange(entranceEdge.Value, 0, ring.Count - 1);
                        break;

                    case "Unknown":
                        Assert.Null(entranceEdge);
                        break;

                    default:
                        Assert.Fail($"Building {i}: status {status}");
                        break;
                }
            }

            Assert.Equal(4, strata.Count);
            Assert.All(strata.Values, x => Assert.Equal(entranceFacadeControlSample_PerStratum, x));
        }

        /// <summary>
        /// Scores the four front-facade rules of <see cref="FrontFacade_Rules"/> and their cascade against the labelled entrances of <c>EntranceFacadeControlSample.json</c>, per stratum, and writes the table to <c>reports/EntranceFacadeControlSample_Scores.md</c>. Reference: DiGi.Test#7, tracking issue DiGi.GIS.PostgreSQL#117.
        /// <para>The rules run on the record alone (stored ring, address points and road segments): same-ULIC road within 50 m (the first address point with a ULIC), nearest road within 200 m, the edge nearest the first address point, the longest edge, and the cascade same-ULIC, else nearest road, else longest. First the guard: each rule must pick on the stored segments the edge it picked on the full road network at draw time (<c>RulesAtDraw</c>), or the stored roads would not stand for the network. Then each <c>Labelled</c> building scores a rule correct when the rule's edge faces the same side as the labelled entrance (<see cref="FrontFacade_SameSide"/>), with the exact-edge rate beside it; a rule that finds no road does not apply and is counted apart. No accuracy threshold is asserted: the table is the result.</para>
        /// </summary>
        [Fact]
        public void EntranceFacadeControlSample_Scores()
        {
            JsonObject sample = EntranceFacadeControlSample_Load();
            JsonArray buildings = sample["Buildings"]!.AsArray();

            // Per stratum and rule: applicable, same side, exact
            Dictionary<string, Dictionary<string, (int Applicable, int SameSide, int Exact)>> scores = [];
            Dictionary<string, (int Labelled, int Unknown)> counts = [];
            int scored = 0;

            foreach (JsonNode? jsonNode in buildings)
            {
                JsonObject building = jsonNode!.AsObject();
                int index = (int)building["Index"]!;
                List<Point2D> ring = EntranceFacadeControlSample_Ring(building);

                List<(Point2D Start, Point2D End, string? ULIC)> roadSegments = [];
                foreach (JsonNode? road in building["Roads"]!.AsArray())
                {
                    roadSegments.Add((EntranceFacadeControlSample_Point(road!["Start"]!), EntranceFacadeControlSample_Point(road["End"]!), (string?)road["ULIC"]));
                }

                Dictionary<(long, long), List<int>> roadIndex = FrontFacade_Index(roadSegments);

                JsonArray addressPoints = building["AddressPoints"]!.AsArray();
                Point2D point2D_First = new((double)addressPoints[0]!["X"]!, (double)addressPoints[0]!["Y"]!);
                string? uLIC_First = addressPoints.Select(x => (string?)x!["ULIC"]).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                (Point2D? point2D_SameULIC, _) = uLIC_First is null ? (null, double.MaxValue) : FrontFacade_NearestRoad(ring, roadSegments, roadIndex, uLIC_First, frontFacade_SameULICDistance);
                (Point2D? point2D_NearestRoad, _) = FrontFacade_NearestRoad(ring, roadSegments, roadIndex, null, frontFacade_SearchDistance);

                Dictionary<string, int> edges = new()
                {
                    ["SameULIC"] = point2D_SameULIC is null ? -1 : FrontFacade_FacingEdge(ring, point2D_SameULIC),
                    ["NearestRoad"] = point2D_NearestRoad is null ? -1 : FrontFacade_FacingEdge(ring, point2D_NearestRoad),
                    ["AddressPoint"] = FrontFacade_NearestEdge(ring, point2D_First),
                    ["Longest"] = FrontFacade_LongestEdge(ring),
                };

                // Guard: the stored segments reproduce what the rules picked on the full network
                JsonObject rulesAtDraw = building["RulesAtDraw"]!.AsObject();
                foreach (KeyValuePair<string, int> keyValuePair in edges)
                {
                    Assert.True(keyValuePair.Value == (int)rulesAtDraw[keyValuePair.Key]!, $"Building {index}: {keyValuePair.Key} picks edge {keyValuePair.Value}, at draw {rulesAtDraw[keyValuePair.Key]}");
                }

                edges["Cascade"] = edges["SameULIC"] != -1 ? edges["SameULIC"] : edges["NearestRoad"] != -1 ? edges["NearestRoad"] : edges["Longest"];

                string stratum = (string)building["Stratum"]!;
                (int labelled, int unknown) = counts.GetValueOrDefault(stratum);
                if ((string)building["Status"]! != "Labelled")
                {
                    counts[stratum] = (labelled, unknown + 1);
                    continue;
                }

                counts[stratum] = (labelled + 1, unknown);
                int entranceEdge = (int)building["EntranceEdge"]!;
                scored++;

                foreach (string key in (string[])[stratum, "All"])
                {
                    if (!scores.TryGetValue(key, out Dictionary<string, (int Applicable, int SameSide, int Exact)>? dictionary))
                    {
                        dictionary = [];
                        scores[key] = dictionary;
                    }

                    foreach (string rule in entranceFacadeControlSample_Rules)
                    {
                        int edge = edges[rule];
                        (int applicable, int sameSide, int exact) = dictionary.GetValueOrDefault(rule);
                        if (edge != -1)
                        {
                            applicable++;
                            sameSide += FrontFacade_SameSide(ring, edge, entranceEdge) ? 1 : 0;
                            exact += edge == entranceEdge ? 1 : 0;
                        }

                        dictionary[rule] = (applicable, sameSide, exact);
                    }
                }
            }

            Assert.Equal(counts.Values.Sum(x => x.Labelled), scored);

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("# Entrance-facade rule accuracy (DiGi.Test#7)");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine($"Sample seed {sample["Seed"]}, BDOT10k `{sample["BDOT10k"]}`, PRG `{sample["PRG"]}`, labelled by {sample["Labeller"]} on {sample["LabelDate"]}. A rule is correct when its edge faces the same side as the labelled entrance (outward normals within 20 degrees); the exact-edge rate is in brackets; n is the number of labelled buildings the rule applies to (a road rule finding no road does not apply).");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Stratum | Labelled | Unknown | " + string.Join(" | ", entranceFacadeControlSample_Rules) + " |");
            stringBuilder.AppendLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", entranceFacadeControlSample_Rules.Length)));
            foreach (string stratum in counts.Keys.Order(StringComparer.Ordinal).Append("All"))
            {
                (int labelled, int unknown) = stratum == "All" ? (counts.Values.Sum(x => x.Labelled), counts.Values.Sum(x => x.Unknown)) : counts[stratum];
                Dictionary<string, (int Applicable, int SameSide, int Exact)> dictionary = scores.GetValueOrDefault(stratum) ?? [];
                List<string> cells = [];
                foreach (string rule in entranceFacadeControlSample_Rules)
                {
                    (int applicable, int sameSide, int exact) = dictionary.GetValueOrDefault(rule);
                    cells.Add(applicable == 0 ? "-" : $"{Percent(sameSide, applicable)} ({Percent(exact, applicable)}), n {applicable}");
                }

                stringBuilder.AppendLine($"| {stratum} | {labelled} | {unknown} | {string.Join(" | ", cells)} |");
            }

            File.WriteAllText(Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "EntranceFacadeControlSample_Scores.md"), stringBuilder.ToString());

            static string Percent(int value, int total)
            {
                return (100.0 * value / total).ToString("0.0", CultureInfo.InvariantCulture) + " %";
            }
        }

        /// <summary>
        /// Reads the committed sample <c>DiGi.Test/files/EntranceFacadeControlSample.json</c>.
        /// </summary>
        /// <returns>The sample.</returns>
        private static JsonObject EntranceFacadeControlSample_Load()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "EntranceFacadeControlSample.json");
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path), $"Missing fixture {path}");

            JsonObject? result = JsonNode.Parse(File.ReadAllText(path!))?.AsObject();
            Assert.NotNull(result);
            return result;
        }

        /// <summary>
        /// Returns the footprint ring of a sample building, as stored (counter-clockwise, open).
        /// </summary>
        /// <param name="building">The building record.</param>
        /// <returns>The ring.</returns>
        private static List<Point2D> EntranceFacadeControlSample_Ring(JsonObject building)
        {
            return [.. building["Ring"]!.AsArray().Select(x => EntranceFacadeControlSample_Point(x!))];
        }

        /// <summary>
        /// Returns a point stored as <c>[X, Y]</c>.
        /// </summary>
        /// <param name="jsonNode">The coordinate pair.</param>
        /// <returns>The point.</returns>
        private static Point2D EntranceFacadeControlSample_Point(JsonNode jsonNode)
        {
            return new Point2D((double)jsonNode[0]!, (double)jsonNode[1]!);
        }
    }
}
