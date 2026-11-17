using DiGi.Analytical.Building;
using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Building.Enums;
using DiGi.Core.Parameter.Classes;
using DiGi.Geometry.Core.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Regulation = DiGi.Analytical.Building.Regulation;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        private static Point2D ClimateZone_Point(double latitude, double longitude)
        {
            Point2D? point2D = DiGi.GIS.Convert.ToEPSG2180(new Point3D(longitude, latitude, 0));
            Assert.NotNull(point2D);
            return point2D;
        }

        private static ClimateZoneMap ClimateZone_Map()
        {
            ClimateZoneMap? climateZoneMap = Query.EmbeddedClimateZoneMap_PL();
            Assert.NotNull(climateZoneMap);
            return climateZoneMap;
        }

        /// <summary>
        /// Tests <see cref="Query.ClimateZone(Point2D, ClimateZoneMap, double)"/> on the control towns of DiGi-Organization/DiGi.GIS.Analytical#7: each gets the zone the source map shows for it.
        /// <para>Szczecin I, Poznań II, Warsaw III, Kraków III, Białystok IV, Cisna IV (the southern strip, about 27 km from any other zone), Sanok III (its marker lies just north of the strip), Suwałki V (the north-east) and Zakopane V (the Tatra). The town-centre coordinates are WGS 84, converted to EPSG:2180. Zakopane lies about 1.6 km inside its zone V area, the closest of the control towns to a boundary. The wiki page Climate Zones lists the same towns.</para>
        /// </summary>
        [Fact]
        public void ClimateZone_ControlTowns()
        {
            ClimateZoneMap climateZoneMap = ClimateZone_Map();

            List<(string Name, double Latitude, double Longitude, Regulation.Enums.ClimateZone ClimateZone)> towns =
            [
                ("Szczecin", 53.4285, 14.5528, Regulation.Enums.ClimateZone.I),
                ("Poznań", 52.4064, 16.9252, Regulation.Enums.ClimateZone.II),
                ("Warszawa", 52.2297, 21.0122, Regulation.Enums.ClimateZone.III),
                ("Kraków", 50.0647, 19.9450, Regulation.Enums.ClimateZone.III),
                ("Białystok", 53.1325, 23.1688, Regulation.Enums.ClimateZone.IV),
                ("Cisna", 49.2167, 22.3333, Regulation.Enums.ClimateZone.IV),
                ("Sanok", 49.5557, 22.2058, Regulation.Enums.ClimateZone.III),
                ("Suwałki", 54.1118, 22.9309, Regulation.Enums.ClimateZone.V),
                ("Zakopane", 49.2992, 19.9496, Regulation.Enums.ClimateZone.V),
            ];

            foreach ((string name, double latitude, double longitude, Regulation.Enums.ClimateZone climateZone) in towns)
            {
                Assert.True(climateZone == ClimateZone_Point(latitude, longitude).ClimateZone(climateZoneMap), name);
            }

            // A building takes the zone of its footprint.
            Point2D point2D_Suwalki = ClimateZone_Point(54.1118, 22.9309);
            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(point2D_Suwalki, new Point2D(point2D_Suwalki.X + 12, point2D_Suwalki.Y), new Point2D(point2D_Suwalki.X + 12, point2D_Suwalki.Y + 9), new Point2D(point2D_Suwalki.X, point2D_Suwalki.Y + 9));
            Building2D building2D = new(Guid.NewGuid(), "Suwałki", polygonalFace2D, 2, null, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]);
            Assert.Equal(Regulation.Enums.ClimateZone.V, building2D.ClimateZone(climateZoneMap));
        }

        /// <summary>
        /// Tests that <see cref="Query.ClimateZone(Point2D, ClimateZoneMap, double)"/> answers <see cref="Regulation.Enums.ClimateZone.Undefined"/> outside Poland and for missing input (DiGi-Organization/DiGi.GIS.Analytical#7).
        /// <para>Berlin, Vilnius and Lviv lie outside every area. A null point, a null building, a building without a footprint and a map without areas also give Undefined.</para>
        /// </summary>
        [Fact]
        public void ClimateZone_Outside()
        {
            ClimateZoneMap climateZoneMap = ClimateZone_Map();

            Assert.Equal(Regulation.Enums.ClimateZone.Undefined, ClimateZone_Point(52.5200, 13.4050).ClimateZone(climateZoneMap));
            Assert.Equal(Regulation.Enums.ClimateZone.Undefined, ClimateZone_Point(54.6872, 25.2797).ClimateZone(climateZoneMap));
            Assert.Equal(Regulation.Enums.ClimateZone.Undefined, ClimateZone_Point(49.8397, 24.0297).ClimateZone(climateZoneMap));

            Assert.Equal(Regulation.Enums.ClimateZone.Undefined, ((Point2D?)null).ClimateZone(climateZoneMap));
            Assert.Equal(Regulation.Enums.ClimateZone.Undefined, ((Building2D?)null).ClimateZone(climateZoneMap));
            Assert.Equal(Regulation.Enums.ClimateZone.Undefined, new Building2D(Guid.NewGuid(), "X", null, 1, null, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]).ClimateZone(climateZoneMap));
            Assert.Equal(Regulation.Enums.ClimateZone.Undefined, ClimateZone_Point(52.2297, 21.0122).ClimateZone(new ClimateZoneMap("0", "empty", 0, null)));
        }

        /// <summary>
        /// Tests that a point on the boundary between two zones gets the colder one (DiGi-Organization/DiGi.GIS.Analytical#7).
        /// <para>The point is a vertex shared by a zone III area and a zone IV area, so both contain it. On the full map it gets IV. On a map holding only the zone III areas, the same vertex gets III, which shows that the colder-zone rule decided the answer, not a gap between the areas.</para>
        /// </summary>
        [Fact]
        public void ClimateZone_Boundary()
        {
            ClimateZoneMap climateZoneMap = ClimateZone_Map();
            List<ClimateZoneArea> climateZoneAreas = climateZoneMap.ClimateZoneAreas ?? [];

            static HashSet<(double, double)> Vertices(IEnumerable<ClimateZoneArea> climateZoneAreas)
            {
                HashSet<(double, double)> result = [];
                foreach (ClimateZoneArea climateZoneArea in climateZoneAreas)
                {
                    if (climateZoneArea.PolygonalFace2D?.ExternalEdge is Polygon2D polygon2D)
                    {
                        foreach (Point2D point2D in polygon2D.GetPoints() ?? [])
                        {
                            result.Add((point2D.X, point2D.Y));
                        }
                    }
                }

                return result;
            }

            List<ClimateZoneArea> climateZoneAreas_III = [.. climateZoneAreas.Where(x => x.ClimateZone == Regulation.Enums.ClimateZone.III)];
            HashSet<(double, double)> vertices_IV = Vertices(climateZoneAreas.Where(x => x.ClimateZone == Regulation.Enums.ClimateZone.IV));
            List<(double, double)> vertices_Shared = [.. Vertices(climateZoneAreas_III).Where(x => vertices_IV.Contains(x)).OrderBy(x => x.Item1).ThenBy(x => x.Item2)];
            Assert.NotEmpty(vertices_Shared);

            // A vertex in the middle of the shared boundary, away from the national border.
            (double x, double y) = vertices_Shared[vertices_Shared.Count / 2];
            Point2D point2D_Boundary = new(x, y);

            Assert.Equal(Regulation.Enums.ClimateZone.IV, point2D_Boundary.ClimateZone(climateZoneMap));
            Assert.Equal(Regulation.Enums.ClimateZone.III, point2D_Boundary.ClimateZone(new ClimateZoneMap(climateZoneMap.Version, climateZoneMap.Source, climateZoneMap.PositionalAccuracy, climateZoneAreas_III)));
        }

        /// <summary>
        /// Tests that the zone areas of the embedded map do not overlap and add up to the national area (DiGi-Organization/DiGi.GIS.Analytical#7).
        /// <para>A 5 km grid is classified with one <see cref="PolygonalFace2DPointRelationSolver"/> per area, and no grid point may lie inside two areas. The areas together must come within 1 % of Poland's 312 696 km2: the map covers the BDOT10k outline buffered by 250 m, which adds about 0.5 %. The grid estimate of the covered area must match the summed areas within 1 %. Coverage at county level is checked by <see cref="ClimateZones_CountyReport"/>.</para>
        /// </summary>
        [Fact]
        public void ClimateZoneMap_Topology()
        {
            ClimateZoneMap climateZoneMap = ClimateZone_Map();
            List<ClimateZoneArea> climateZoneAreas = climateZoneMap.ClimateZoneAreas ?? [];
            Assert.NotEmpty(climateZoneAreas);

            List<PolygonalFace2DPointRelationSolver> polygonalFace2DPointRelationSolvers = [];
            double area = 0;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (ClimateZoneArea climateZoneArea in climateZoneAreas)
            {
                PolygonalFace2D? polygonalFace2D = climateZoneArea.PolygonalFace2D;
                Assert.NotNull(polygonalFace2D);
                area += polygonalFace2D.GetArea();
                BoundingBox2D? boundingBox2D = polygonalFace2D.GetBoundingBox();
                Assert.NotNull(boundingBox2D);
                minX = Math.Min(minX, boundingBox2D.Min.X);
                minY = Math.Min(minY, boundingBox2D.Min.Y);
                maxX = Math.Max(maxX, boundingBox2D.Max.X);
                maxY = Math.Max(maxY, boundingBox2D.Max.Y);
                polygonalFace2DPointRelationSolvers.Add(new PolygonalFace2DPointRelationSolver(polygonalFace2D));
            }

            const double area_Poland = 312696e6;
            Assert.True(Math.Abs(area - area_Poland) < 0.01 * area_Poland, $"zone area {(area / 1e6).ToString("F0", CultureInfo.InvariantCulture)} km2");

            const double step = 5000;
            int count_Inside = 0;
            int count_Overlap = 0;
            for (double x = Math.Floor(minX / step) * step + step / 2; x < maxX; x += step)
            {
                for (double y = Math.Floor(minY / step) * step + step / 2; y < maxY; y += step)
                {
                    Point2D point2D = new(x, y);
                    int count = 0;
                    foreach (PolygonalFace2DPointRelationSolver polygonalFace2DPointRelationSolver in polygonalFace2DPointRelationSolvers)
                    {
                        polygonalFace2DPointRelationSolver.Input = point2D;
                        if (polygonalFace2DPointRelationSolver.Solve() && polygonalFace2DPointRelationSolver.Output == PointRelation.Inside)
                        {
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        count_Inside++;
                    }

                    if (count > 1)
                    {
                        count_Overlap++;
                    }
                }
            }

            Assert.Equal(0, count_Overlap);
            double area_Grid = count_Inside * step * step;
            Assert.True(Math.Abs(area_Grid - area) < 0.01 * area, $"grid area {(area_Grid / 1e6).ToString("F0", CultureInfo.InvariantCulture)} km2 against {(area / 1e6).ToString("F0", CultureInfo.InvariantCulture)} km2");
        }

        /// <summary>
        /// Tests the embedded climate zone map and the serialization of <see cref="ClimateZoneMap"/> and <see cref="ClimateZoneArea"/> (DiGi-Organization/DiGi.GIS.Analytical#7).
        /// <para>The map is embedded, its source tag names PN-82/B-02403 and the reproduction, its accuracy is 10 km, and it holds seven areas: one each for zones I-III, and two each for zones IV and V (the north-east and the southern mountains). <see cref="Convert.ToDiGi_ClimateZoneMap(string)"/> refuses text that is not a FeatureCollection.</para>
        /// </summary>
        [Fact]
        public void EmbeddedClimateZoneMap_PL_Resource()
        {
            using (Stream? stream = typeof(Query).Assembly.GetManifestResourceStream(Query.EmbeddedClimateZoneMap_PL_ResourceName))
            {
                Assert.True(stream is not null, $"{Query.EmbeddedClimateZoneMap_PL_ResourceName} is not embedded");
            }

            ClimateZoneMap climateZoneMap = ClimateZone_Map();
            Assert.Equal("1.0.0", climateZoneMap.Version);
            Assert.Contains("PN-82/B-02403 zone map as reproduced in", climateZoneMap.Source);
            Assert.Contains("digitised 2026-11-17", climateZoneMap.Source);
            Assert.Equal(10000, climateZoneMap.PositionalAccuracy);

            List<ClimateZoneArea> climateZoneAreas = climateZoneMap.ClimateZoneAreas ?? [];
            Assert.Equal(7, climateZoneAreas.Count);
            Assert.Equal(1, climateZoneAreas.Count(x => x.ClimateZone == Regulation.Enums.ClimateZone.I));
            Assert.Equal(1, climateZoneAreas.Count(x => x.ClimateZone == Regulation.Enums.ClimateZone.II));
            Assert.Equal(1, climateZoneAreas.Count(x => x.ClimateZone == Regulation.Enums.ClimateZone.III));
            Assert.Equal(2, climateZoneAreas.Count(x => x.ClimateZone == Regulation.Enums.ClimateZone.IV));
            Assert.Equal(2, climateZoneAreas.Count(x => x.ClimateZone == Regulation.Enums.ClimateZone.V));

            ClimateZoneMap climateZoneMap_Copy = new(climateZoneMap);
            Assert.Equal(climateZoneMap.Version, climateZoneMap_Copy.Version);
            Assert.Equal(climateZoneMap.Source, climateZoneMap_Copy.Source);
            Assert.Equal(climateZoneMap.PositionalAccuracy, climateZoneMap_Copy.PositionalAccuracy);
            Assert.Equal(climateZoneAreas.Count, climateZoneMap_Copy.ClimateZoneAreas!.Count);
            Assert.Equal(Core.Convert.ToSystem_String(climateZoneMap), Core.Convert.ToSystem_String(climateZoneMap_Copy));
            Core.xUnit.Query.SerializationCheck(climateZoneMap);

            ClimateZoneArea climateZoneArea = climateZoneAreas.First(x => x.ClimateZone == Regulation.Enums.ClimateZone.V);
            ClimateZoneArea climateZoneArea_Copy = new(climateZoneArea);
            Assert.Equal(climateZoneArea.ClimateZone, climateZoneArea_Copy.ClimateZone);
            Assert.Equal(climateZoneArea.PolygonalFace2D!.GetArea(), climateZoneArea_Copy.PolygonalFace2D!.GetArea());
            Assert.Equal(Core.Convert.ToSystem_String(climateZoneArea), Core.Convert.ToSystem_String(climateZoneArea_Copy));
            Core.xUnit.Query.SerializationCheck(climateZoneArea);

            Assert.Null(Convert.ToDiGi_ClimateZoneMap(null));
            Assert.Null(Convert.ToDiGi_ClimateZoneMap("not json"));
            Assert.Null(Convert.ToDiGi_ClimateZoneMap("{\"type\":\"Feature\"}"));
            Assert.Empty(Convert.ToDiGi_ClimateZoneMap("{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"properties\":{\"ClimateZone\":\"VI\"},\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,0],[1,1],[0,0]]]}}]}")!.ClimateZoneAreas!);
        }

        /// <summary>
        /// Tests that <see cref="Modify.GenerateOpenings"/> takes the climate zone of the footprint when the input leaves it <see cref="Regulation.Enums.ClimateZone.Undefined"/> (DiGi-Organization/DiGi.GIS.Analytical#7).
        /// <para>A multi-family building in Suwałki (zone V), completed in 2007 and designed in 2005, gets the Regulation windows of 2002. Those are 2.0 W/(m2K) in zones IV-V and 2.6 W/(m2K) in zones I-III. With the zone left Undefined the windows are 2.0, the same as with zone V given explicitly. With zone III given explicitly they are 2.6, so the input still wins over the map.</para>
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_ClimateZoneFallback()
        {
            Point2D point2D_Suwalki = ClimateZone_Point(54.1118, 22.9309);

            double Window(Regulation.Enums.ClimateZone climateZone)
            {
                PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(point2D_Suwalki, new Point2D(point2D_Suwalki.X + 24, point2D_Suwalki.Y), new Point2D(point2D_Suwalki.X + 24, point2D_Suwalki.Y + 12), new Point2D(point2D_Suwalki.X, point2D_Suwalki.Y + 12));
                Building2D building2D = new(Guid.NewGuid(), "Suwałki", polygonalFace2D, 4, null, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.multi_family_building]);
                BuildingModel? buildingModel = Create.BuildingModel(building2D, 0, 3.0);
                Assert.NotNull(buildingModel);

                OpeningGenerationOptions openingGenerationOptions = Create.OpeningGenerationOptions(Regulation.Enums.BuildingRegulation.PL_WT2002);
                openingGenerationOptions.ConstructionFlavour = Regulation.Enums.ConstructionFlavour.Regulation;

                OpeningGenerationResult? openingGenerationResult = buildingModel.GenerateOpenings(building2D, new OpeningGenerationInput(2007, YearBuiltSource.User, null, climateZone, null, openingGenerationOptions));
                Assert.NotNull(openingGenerationResult);
                Assert.Equal(Regulation.Enums.BuildingType.MultiFamily, openingGenerationResult.BuildingType);

                List<IWindow> windows = buildingModel.GetOpenings<IWindow>() ?? [];
                Assert.NotEmpty(windows);
                HashSet<double> thermalTransmittances = [];
                foreach (IWindow window in windows)
                {
                    IWindowConstruction? windowConstruction = buildingModel.GetWindowConstruction(window);
                    if (windowConstruction is null || !windowConstruction.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.ThermalTransmittance), out double thermalTransmittance))
                    {
                        continue;
                    }

                    thermalTransmittances.Add(Math.Round(thermalTransmittance, 6));
                }

                return Assert.Single(thermalTransmittances);
            }

            Assert.Equal(2.0, Window(Regulation.Enums.ClimateZone.Undefined));
            Assert.Equal(2.0, Window(Regulation.Enums.ClimateZone.V));
            Assert.Equal(2.6, Window(Regulation.Enums.ClimateZone.III));
        }

        /// <summary>
        /// Classifies every county part of the production database by climate zone and writes the county table for the wiki (DiGi-Organization/DiGi.GIS.Analytical#7).
        /// <para>The parts are read in one request (<c>gis/administrativeareal2D/itemsbyadministrativearealtype?administrativearealtype=2</c>, about 200 MB) and keyed by id, because a county code has one row per polygon part (406 parts). Each part is classified at an internal point. No part may come out Undefined, which checks that the map covers the country at county level.</para>
        /// <para>The table is written to <c>reports/ClimateZones.md</c>, with one row per county code: the county names filed under it, its number of parts and every zone its parts lie in. The table is copied to the wiki page Climate Zones.</para>
        /// </summary>
        [LongFact]
        public async Task ClimateZones_CountyReport()
        {
            ClimateZoneMap climateZoneMap = ClimateZone_Map();

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(15) };
            string json = await httpClient.GetStringAsync("https://api.digiproject.uk/gis/administrativeareal2D/itemsbyadministrativearealtype?administrativearealtype=2");
            List<AdministrativeDivision>? administrativeDivisions = Core.Convert.ToDiGi<AdministrativeDivision>(json);
            Assert.NotNull(administrativeDivisions);
            Assert.Equal(406, administrativeDivisions.Count);

            SortedDictionary<string, List<(string Name, double Area, Regulation.Enums.ClimateZone ClimateZone)>> dictionary = new(StringComparer.Ordinal);
            List<string> undefineds = [];
            foreach (AdministrativeDivision administrativeDivision in administrativeDivisions)
            {
                PolygonalFace2D? polygonalFace2D = administrativeDivision.PolygonalFace2D;
                Regulation.Enums.ClimateZone climateZone = polygonalFace2D?.GetInternalPoint().ClimateZone(climateZoneMap) ?? Regulation.Enums.ClimateZone.Undefined;
                string code = administrativeDivision.Code ?? string.Empty;
                if (climateZone == Regulation.Enums.ClimateZone.Undefined)
                {
                    undefineds.Add(code);
                }

                if (!dictionary.TryGetValue(code, out List<(string, double, Regulation.Enums.ClimateZone)>? values))
                {
                    values = [];
                    dictionary[code] = values;
                }

                values.Add((administrativeDivision.Name ?? string.Empty, (polygonalFace2D?.GetArea() ?? 0) / 1e6, climateZone));
            }

            // 380 codes: an object whose Code disagrees with its row merges two counties under one code, as nakielski (id 6027) did under
            // 1004 until DiGi-Organization/DiGi.GIS.PostgreSQL#142. Every county name filed under a code is listed, so such a row would show it.
            Assert.Equal(380, dictionary.Count);

            StringBuilder stringBuilder = new();
            stringBuilder.Append("| Code | County | Parts | Zone |\n|---|---|---|---|\n");
            foreach (KeyValuePair<string, List<(string Name, double Area, Regulation.Enums.ClimateZone ClimateZone)>> keyValuePair in dictionary)
            {
                List<(string Name, double Area, Regulation.Enums.ClimateZone ClimateZone)> values = [.. keyValuePair.Value.OrderByDescending(x => x.Area)];
                string names = string.Join(" / ", values.Select(x => x.Name).Distinct());
                string zones = string.Join(", ", values.Select(x => x.ClimateZone).Distinct().OrderBy(x => x).Select(x => x.ToString()));
                stringBuilder.Append(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2} | {3} |\n", keyValuePair.Key, names, values.Count, zones));
            }

            string? path_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(path_Reports);
            File.WriteAllText(Path.Combine(path_Reports, "ClimateZones.md"), stringBuilder.ToString());

            Assert.True(undefineds.Count == 0, $"county parts outside the map: {string.Join(", ", undefineds)}");
        }
    }
}
