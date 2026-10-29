using DiGi.BDOT10k.Enums;
using DiGi.BDOT10k.Geometry.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.PRG.Geometry.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The seed of the control sample draw. Changing it draws a different sample; keep it so the draw stays repeatable. Reference: DiGi.Test#6.
        /// </summary>
        private const int entranceFacadeControlSample_Seed = 117;

        /// <summary>
        /// The number of buildings drawn per stratum.
        /// </summary>
        private const int entranceFacadeControlSample_PerStratum = 30;

        /// <summary>
        /// Draws the entrance-facade control sample from the road-assessment counties and writes it, unlabelled, to <c>reports/EntranceFacadeControlSample.json</c> with a review page <c>reports/EntranceFacadeControlSample_Review.html</c> for manual labelling. Reference: DiGi.Test#6, tracking issue DiGi.GIS.PostgreSQL#117.
        /// <para>Inputs are those of <see cref="FrontFacade_Counties"/> (<c>user files/DiGi.PRG.Geometry_FrontFacade.conf</c>); without them the fact returns. Candidates are residential BDOT10k buildings (<c>funkcjaOgolnaBudynku</c> mieszkalne, <c>przewazajacaFunkcjaBudynku</c> jednorodzinny or wielorodzinny) with a footprint of 4 or more corners and at least one PRG address point matched to them as the import matches (inside, else nearest within 10 m). Strata: city multi-family and city single-family (3061, 0262), suburban (1405), rural (1436, 1001). Each stratum's candidates are ordered by county code and BDOT10k <c>lokalnyId</c>, shuffled with a fixed seed and the first 30 kept, so the same inputs always draw the same sample.</para>
        /// <para>Each record is self-contained, so the integrity and scoring facts need no database and no archive: the footprint ring (EPSG:2180, counter-clockwise, edge <c>i</c> from point <c>i</c> to <c>i + 1</c>), the matched address points, and the road segments the four rules of <see cref="FrontFacade_Rules"/> read (every segment within 50 m of the footprint, plus the nearest one within 200 m). The label fields stay empty for the labeller: <c>EntranceEdge</c> and <c>Status</c> (<c>Unlabelled</c>, <c>Labelled</c> or <c>Unknown</c>). The review page shows no road and no rule result, so the labels do not echo a rule. Long test.</para>
        /// </summary>
        [LongFact]
        public void EntranceFacadeControlSample_Draw()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            Dictionary<string, string>? settings = FrontFacade_Settings(assembly);
            if (settings is null)
            {
                return;
            }

            Dictionary<string, string> areas = new() { ["3061"] = "City", ["0262"] = "City", ["1405"] = "Suburban", ["1436"] = "Rural", ["1001"] = "Rural" };
            Assert.All(FrontFacade_Codes(settings), x => Assert.True(areas.ContainsKey(x), $"County {x} has no stratum"));

            Dictionary<string, List<JsonObject>> candidates = new() { ["CityMultiFamily"] = [], ["CitySingleFamily"] = [], ["Suburban"] = [], ["Rural"] = [] };

            FrontFacade_ForEachCounty(settings, (code, slownikObiektowGeometrycznych, punktyAdresowe_County) =>
            {
                List<(Point2D Start, Point2D End, string? ULIC)> roadSegments = FrontFacade_RoadSegments(slownikObiektowGeometrycznych);
                Dictionary<(long, long), List<int>> roadIndex = FrontFacade_Index(roadSegments);

                List<(BUBD_A BUBD_A, List<Point2D> Ring)> footprints = FrontFacade_Footprints(slownikObiektowGeometrycznych);
                List<List<Point2D>> rings = footprints.ConvertAll(x => x.Ring);
                (Dictionary<(long, long), List<int>> Index, List<int> EdgeRings) footprintIndex = FrontFacade_FootprintIndex(rings);

                Dictionary<int, List<PunktAdresowy>> punktyAdresowe_Matched = [];
                foreach (PunktAdresowy punktAdresowy in punktyAdresowe_County)
                {
                    if (punktAdresowy.Geometry is not Point2D point2D)
                    {
                        continue;
                    }

                    int ring_Index = FrontFacade_Match(point2D, rings, footprintIndex);
                    if (ring_Index == -1)
                    {
                        continue;
                    }

                    if (!punktyAdresowe_Matched.TryGetValue(ring_Index, out List<PunktAdresowy>? punktyAdresowe))
                    {
                        punktyAdresowe = [];
                        punktyAdresowe_Matched[ring_Index] = punktyAdresowe;
                    }

                    punktyAdresowe.Add(punktAdresowy);
                }

                foreach (KeyValuePair<int, List<PunktAdresowy>> keyValuePair in punktyAdresowe_Matched)
                {
                    (BUBD_A bUBD_A, List<Point2D> ring) = footprints[keyValuePair.Key];
                    if (ring.Count < 4 || bUBD_A.OT_PowierzchniowyObiektGeometryczny is not BDOT10k.Classes.OT_BUBD_A oT_BUBD_A || oT_BUBD_A.funkcjaOgolnaBudynku != OT_FunOgolnaBudynku.budynki_mieszkalne)
                    {
                        continue;
                    }

                    string? stratum = areas[code] switch
                    {
                        "City" when oT_BUBD_A.przewazajacaFunkcjaBudynku == OT_FunSzczegolowaBudynku.budynek_wielorodzinny => "CityMultiFamily",
                        "City" when oT_BUBD_A.przewazajacaFunkcjaBudynku == OT_FunSzczegolowaBudynku.budynek_jednorodzinny => "CitySingleFamily",
                        "Suburban" or "Rural" when oT_BUBD_A.przewazajacaFunkcjaBudynku is OT_FunSzczegolowaBudynku.budynek_jednorodzinny or OT_FunSzczegolowaBudynku.budynek_wielorodzinny => areas[code],
                        _ => null,
                    };

                    if (stratum is null || string.IsNullOrWhiteSpace(oT_BUBD_A.lokalnyId))
                    {
                        continue;
                    }

                    candidates[stratum].Add(Record(code, stratum, oT_BUBD_A, ring, keyValuePair.Value, roadSegments, roadIndex));
                }
            });

            JsonArray buildings = [];
            JsonObject strata = [];
            foreach (KeyValuePair<string, List<JsonObject>> keyValuePair in candidates)
            {
                List<JsonObject> jsonObjects = [.. keyValuePair.Value.OrderBy(x => (string)x["CountyCode"]!, StringComparer.Ordinal).ThenBy(x => (string)x["BuildingId"]!, StringComparer.Ordinal)];

                // Fisher-Yates with a fixed seed: the same candidates always give the same sample
                Random random = new(entranceFacadeControlSample_Seed);
                for (int i = jsonObjects.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (jsonObjects[i], jsonObjects[j]) = (jsonObjects[j], jsonObjects[i]);
                }

                List<JsonObject> jsonObjects_Sample = jsonObjects.GetRange(0, Math.Min(entranceFacadeControlSample_PerStratum, jsonObjects.Count));
                strata[keyValuePair.Key] = new JsonObject { ["Candidates"] = jsonObjects.Count, ["Drawn"] = jsonObjects_Sample.Count };
                jsonObjects_Sample.ForEach(x => buildings.Add(x));

                Assert.Equal(entranceFacadeControlSample_PerStratum, jsonObjects_Sample.Count);
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                buildings[i]!["Index"] = i;
            }

            JsonObject sample = new()
            {
                ["Description"] = "Entrance-facade control sample (DiGi.Test#6). Edge i of Ring runs from point i to point i + 1; Ring is counter-clockwise in EPSG:2180 (X east, Y north). EntranceEdge is the edge holding the main entrance, set by a person from imagery; Status is Unlabelled, Labelled or Unknown.",
                ["Seed"] = entranceFacadeControlSample_Seed,
                ["PerStratum"] = entranceFacadeControlSample_PerStratum,
                ["BDOT10k"] = $"{Path.GetFileName(Path.GetDirectoryName(settings["BDOT10kArchive"]))}/{Path.GetFileName(settings["BDOT10kArchive"])}",
                ["PRG"] = Path.GetFileName(settings["PRGDirectory"]),
                ["Strata"] = strata,
                ["Labeller"] = null,
                ["LabelDate"] = null,
                ["Buildings"] = buildings,
            };

            string json = sample.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            string directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly)!;
            File.WriteAllText(Path.Combine(directory_Reports, "EntranceFacadeControlSample.json"), json);
            File.WriteAllText(Path.Combine(directory_Reports, "EntranceFacadeControlSample_Review.html"), entranceFacadeControlSample_ReviewPage.Replace("__SAMPLE__", sample.ToJsonString().Replace("</", "<\\/", StringComparison.Ordinal), StringComparison.Ordinal));

            Assert.Equal(4 * entranceFacadeControlSample_PerStratum, buildings.Count);

            static JsonObject Record(string code, string stratum, BDOT10k.Classes.OT_BUBD_A oT_BUBD_A, List<Point2D> ring, List<PunktAdresowy> punktyAdresowe, List<(Point2D Start, Point2D End, string? ULIC)> roadSegments, Dictionary<(long, long), List<int>> roadIndex)
            {
                double centroidX = ring.Average(x => x.X);
                double centroidY = ring.Average(x => x.Y);
                (double latitude, double longitude) = WGS84(centroidX, centroidY);

                JsonArray jsonArray_Ring = [];
                ring.ForEach(x => jsonArray_Ring.Add(new JsonArray(Round(x.X), Round(x.Y))));

                JsonArray jsonArray_AddressPoints = [];
                foreach (PunktAdresowy punktAdresowy in punktyAdresowe.OrderBy(x => x.lokalnyId, StringComparer.Ordinal))
                {
                    Point2D point2D = punktAdresowy.Geometry!;
                    (double latitude_Point, double longitude_Point) = WGS84(point2D.X, point2D.Y);
                    jsonArray_AddressPoints.Add(new JsonObject
                    {
                        ["ReferenceId"] = punktAdresowy.lokalnyId,
                        ["X"] = Round(point2D.X),
                        ["Y"] = Round(point2D.Y),
                        ["Latitude"] = Math.Round(latitude_Point, 7),
                        ["Longitude"] = Math.Round(longitude_Point, 7),
                        ["ULIC"] = string.IsNullOrWhiteSpace(punktAdresowy.identyfikatorULIC) ? null : punktAdresowy.identyfikatorULIC.Trim(),
                        ["Street"] = punktAdresowy.ulicaNazwaPelna,
                        ["HouseNumber"] = punktAdresowy.numerPorzadkowy,
                        ["Locality"] = punktAdresowy.miejscowoscNazwa,
                    });
                }

                // The segments the rules read: every one within the same-ULIC radius, plus the nearest within the search radius
                HashSet<int> segments = [.. RoadsNear(ring, roadSegments, roadIndex, frontFacade_SameULICDistance)];
                int segment_Nearest = RoadsNear(ring, roadSegments, roadIndex, frontFacade_SearchDistance).FirstOrDefault(-1);
                if (segment_Nearest != -1)
                {
                    segments.Add(segment_Nearest);
                }

                JsonArray jsonArray_Roads = [];
                foreach (int i in segments.Order())
                {
                    (Point2D start, Point2D end, string? uLIC) = roadSegments[i];
                    jsonArray_Roads.Add(new JsonObject { ["Start"] = new JsonArray(Round(start.X), Round(start.Y)), ["End"] = new JsonArray(Round(end.X), Round(end.Y)), ["ULIC"] = uLIC });
                }

                return new JsonObject
                {
                    ["Index"] = -1,
                    ["Stratum"] = stratum,
                    ["CountyCode"] = code,
                    ["BuildingId"] = oT_BUBD_A.lokalnyId,
                    ["Function"] = oT_BUBD_A.przewazajacaFunkcjaBudynku.ToString(),
                    ["Storeys"] = oT_BUBD_A.liczbaKondygnacji,
                    ["Latitude"] = Math.Round(latitude, 7),
                    ["Longitude"] = Math.Round(longitude, 7),
                    ["Ring"] = jsonArray_Ring,
                    ["AddressPoints"] = jsonArray_AddressPoints,
                    ["Roads"] = jsonArray_Roads,
                    ["EntranceEdge"] = null,
                    ["Status"] = "Unlabelled",
                };
            }

            // Segment indexes within the distance of the ring, nearest first
            static List<int> RoadsNear(List<Point2D> ring, List<(Point2D Start, Point2D End, string? ULIC)> roadSegments, Dictionary<(long, long), List<int>> roadIndex, double distance_Max)
            {
                double minX = ring.Min(x => x.X) - distance_Max;
                double minY = ring.Min(x => x.Y) - distance_Max;
                double maxX = ring.Max(x => x.X) + distance_Max;
                double maxY = ring.Max(x => x.Y) + distance_Max;

                Dictionary<int, double> distances = [];
                for (long cellX = FrontFacade_Cell(minX); cellX <= FrontFacade_Cell(maxX); cellX++)
                {
                    for (long cellY = FrontFacade_Cell(minY); cellY <= FrontFacade_Cell(maxY); cellY++)
                    {
                        if (!roadIndex.TryGetValue((cellX, cellY), out List<int>? indexes))
                        {
                            continue;
                        }

                        foreach (int i in indexes)
                        {
                            if (distances.ContainsKey(i))
                            {
                                continue;
                            }

                            (Point2D start, Point2D end, _) = roadSegments[i];
                            double distance = double.MaxValue;
                            foreach (Point2D point2D in ring)
                            {
                                distance = Math.Min(distance, FrontFacade_Closest(point2D.X, point2D.Y, start, end).Distance);
                            }

                            for (int j = 0; j < ring.Count; j++)
                            {
                                distance = Math.Min(distance, FrontFacade_Closest(start.X, start.Y, ring[j], ring[(j + 1) % ring.Count]).Distance);
                                distance = Math.Min(distance, FrontFacade_Closest(end.X, end.Y, ring[j], ring[(j + 1) % ring.Count]).Distance);
                            }

                            distances[i] = distance;
                        }
                    }
                }

                return [.. distances.Where(x => x.Value <= distance_Max).OrderBy(x => x.Value).ThenBy(x => x.Key).Select(x => x.Key)];
            }

            static double Round(double value)
            {
                return Math.Round(value, 2);
            }

            // Inverse transverse Mercator of PL-1992 (EPSG:2180: GRS80, central meridian 19 E, scale 0.9993, false easting 500 000, false northing -5 300 000), Snyder's series
            static (double Latitude, double Longitude) WGS84(double easting, double northing)
            {
                const double a = 6378137.0;
                const double f = 1.0 / 298.257222101;
                const double k0 = 0.9993;
                double e2 = f * (2.0 - f);
                double x = easting - 500000.0;
                double y = northing + 5300000.0;
                double mu = y / k0 / (a * (1.0 - (e2 / 4.0) - (3.0 * e2 * e2 / 64.0) - (5.0 * e2 * e2 * e2 / 256.0)));
                double e1 = (1.0 - Math.Sqrt(1.0 - e2)) / (1.0 + Math.Sqrt(1.0 - e2));
                double phi = mu + (((3.0 * e1 / 2.0) - (27.0 * Math.Pow(e1, 3) / 32.0)) * Math.Sin(2.0 * mu)) + (((21.0 * e1 * e1 / 16.0) - (55.0 * Math.Pow(e1, 4) / 32.0)) * Math.Sin(4.0 * mu)) + (151.0 * Math.Pow(e1, 3) / 96.0 * Math.Sin(6.0 * mu)) + (1097.0 * Math.Pow(e1, 4) / 512.0 * Math.Sin(8.0 * mu));
                double ep2 = e2 / (1.0 - e2);
                double c = ep2 * Math.Cos(phi) * Math.Cos(phi);
                double t = Math.Tan(phi) * Math.Tan(phi);
                double sin = Math.Sin(phi);
                double n = a / Math.Sqrt(1.0 - (e2 * sin * sin));
                double r = a * (1.0 - e2) / Math.Pow(1.0 - (e2 * sin * sin), 1.5);
                double d = x / (n * k0);
                double latitude = phi - (n * Math.Tan(phi) / r * ((d * d / 2.0) - ((5.0 + (3.0 * t) + (10.0 * c) - (4.0 * c * c) - (9.0 * ep2)) * Math.Pow(d, 4) / 24.0) + ((61.0 + (90.0 * t) + (298.0 * c) + (45.0 * t * t) - (252.0 * ep2) - (3.0 * c * c)) * Math.Pow(d, 6) / 720.0)));
                double longitude = (19.0 * Math.PI / 180.0) + ((d - ((1.0 + (2.0 * t) + c) * Math.Pow(d, 3) / 6.0) + ((5.0 - (2.0 * c) + (28.0 * t) - (3.0 * c * c) + (8.0 * ep2) + (24.0 * t * t)) * Math.Pow(d, 5) / 120.0)) / Math.Cos(phi));
                return (latitude * 180.0 / Math.PI, longitude * 180.0 / Math.PI);
            }
        }
    }
}
