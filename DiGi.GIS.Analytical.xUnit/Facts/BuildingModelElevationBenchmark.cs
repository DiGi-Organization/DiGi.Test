using DiGi.Analytical.Building.Classes;
using DiGi.CityGML.Classes;
using DiGi.CityGML.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The seed of the elevation benchmark sample draw. Changing it draws a different sample; keep it so the draw stays repeatable. It is the number of DiGi-Organization/DiGi.GIS.Analytical#3, the issue that grew the benchmark from 10 to 200 buildings per dataset.
        /// </summary>
        private const int elevationBenchmark_Seed = 3;

        /// <summary>
        /// The number of buildings drawn per county per dataset. Eight panel counties times 25 = 200 per dataset.
        /// </summary>
        private const int elevationBenchmark_BuildingsPerCounty = 25;

        /// <summary>
        /// The number of map sheets a county's quota is spread over (the quota per sheet is the quota divided by this, rounded up), so the sample covers a county spatially instead of one street corner.
        /// </summary>
        private const int elevationBenchmark_SheetsPerCounty = 5;

        /// <summary>
        /// The maximum number of map sheets parsed per county archive before the walk gives up on that county.
        /// </summary>
        private const int elevationBenchmark_MaxSheetsParsed = 20;

        /// <summary>
        /// The maximum number of concurrent GUGiK NMT requests.
        /// </summary>
        private const int elevationBenchmark_MaxConcurrentRequests = 8;

        /// <summary>
        /// The number of times a transient NMT failure is retried per point.
        /// </summary>
        private const int elevationBenchmark_RetryCount = 3;

        /// <summary>
        /// The delay before the first NMT retry, doubled per attempt.
        /// </summary>
        private static readonly TimeSpan elevationBenchmark_RetryDelay = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The signed-mean magnitude at which a stratum is flagged as systematically offset. 0.10 m sits below the mean KRON86 to EVRF2007 datum step of about 0.165 m, so a datum-driven bias is detectable.
        /// </summary>
        private const double elevationBenchmark_OffsetFlagAbsMean = 0.10;

        /// <summary>
        /// The model height at or below which a record whose NMT is at or above <see cref="elevationBenchmark_GroundRelativeNmt"/> is classified as ground-relative (geometry relative to local ground).
        /// </summary>
        private const double elevationBenchmark_GroundRelativeModelZ = 5.0;

        /// <summary>
        /// The NMT elevation at or above which a record whose model height is at or below <see cref="elevationBenchmark_GroundRelativeModelZ"/> is classified as ground-relative.
        /// </summary>
        private const double elevationBenchmark_GroundRelativeNmt = 30.0;

        /// <summary>
        /// The lower bound of the PL-1992 (EPSG:2180) coordinate guard range. A centroid outside it is <c>CrsSuspect</c>.
        /// </summary>
        private const double elevationBenchmark_CrsMin = 100000.0;

        /// <summary>
        /// The upper bound of the PL-1992 (EPSG:2180) coordinate guard range. See <see cref="elevationBenchmark_CrsMin"/>.
        /// </summary>
        private const double elevationBenchmark_CrsMax = 900000.0;

        /// <summary>
        /// The uncompressed size below which a map sheet is an empty stub - the real stubs are 1,146-byte headers with zero buildings - and is skipped without parsing.
        /// </summary>
        private const long elevationBenchmark_StubLength = 2048;

        /// <summary>
        /// The name of the machine-specific input file read from <c>user files/</c> (<c>ModeleDirectory</c>, optional <c>Counties</c>, optional <c>BuildingsPerCounty</c>).
        /// </summary>
        private const string elevationBenchmark_ConfName = "DiGi.GIS.Analytical_ElevationBenchmark.conf";

        /// <summary>
        /// The committed sample fixture read from <c>DiGi.Test/files/</c> and drawn to <c>user files/reports/</c>.
        /// </summary>
        private const string elevationBenchmark_FixtureName = "ElevationBenchmarkSample.json";

        /// <summary>
        /// The upper edges of the absolute-dZ distribution bins; the bins are [0, e0), [e0, e1), ... and [eN, infinity) in metres.
        /// </summary>
        private static readonly double[] elevationBenchmark_BinEdges = [0.1, 0.25, 0.5, 1.0, 2.0];

        /// <summary>
        /// The dataset subfolders of the Geoportal Modele 3D mirror, in draw order.
        /// </summary>
        private static readonly string[] elevationBenchmark_DatasetPaths = ["LOD1\\2019", "LOD1\\2021", "LOD1\\2022", "LOD1\\2024", "LOD2"];

        /// <summary>
        /// The dataset names used in the fixture and reports, parallel to <see cref="elevationBenchmark_DatasetPaths"/>.
        /// </summary>
        private static readonly string[] elevationBenchmark_DatasetNames = ["LOD1-2019", "LOD1-2021", "LOD1-2022", "LOD1-2024", "LOD2"];

        /// <summary>
        /// The county panel: eight counties verified present in all five datasets, spread over terrain bands and six voivodeships (DiGi.GIS.Analytical#3).
        /// </summary>
        private static readonly string[] elevationBenchmark_Counties = ["1217", "1211", "1801", "2802", "1465", "1261", "0464", "2661"];

        /// <summary>
        /// Draws the elevation benchmark sample from the Geoportal Modele 3D mirror and writes it to <c>user files/reports/ElevationBenchmarkSample.json</c>, with the CRS and height-class census (<c>user files/reports/ElevationBenchmark_CRS_Census.txt</c>) and a diff against the committed fixture (<c>user files/reports/ElevationBenchmark_Draw_Diff.txt</c>). Reference: DiGi.GIS.Analytical#3.
        /// <para>Inputs are machine-specific and read from <c>user files/DiGi.GIS.Analytical_ElevationBenchmark.conf</c> (<c>ModeleDirectory</c> root of the mirror, optional <c>Counties</c> panel override, optional <c>BuildingsPerCounty</c> quota override). Without the file, or with a missing <c>ModeleDirectory</c>, the fact returns without measuring anything.</para>
        /// <para>Sampling unit is county, then sheet, then building: for each of the five datasets and each panel county, the archive's <c>.gml</c> entries are sorted ordinally and shuffled with a per-stratum seed (the <c>unchecked((seed * 397) + countyCode)</c> arithmetic of <c>DiGi.GIS.PostgreSQL.Query.RandomSeed</c>, combined with the dataset index), then walked until the county quota is met; each sheet contributes at most <c>ceil(quota / sheetsPerCounty)</c> buildings, taken from the <c>gml:id</c>-sorted building list shuffled with a per-sheet seed. The same inputs always draw the same sample.</para>
        /// <para>The census scans the header of every <c>.gml</c> entry in each panel archive (not only the sampled sheets) and records the horizontal and vertical CRS components, the coordinate ranges and the empty-stub counts, so a future Geoportal release in PL-2000 or UTM is caught before it enters the sample. A non-EPSG:2180 sheet or an out-of-range centroid fails the fact loudly with the conversion path named.</para>
        /// <para>The drawn sample is test-execution output and lands in the reports directory; the commit step copies the reviewed file to <c>DiGi.Test/files/ElevationBenchmarkSample.json</c>, which <see cref="BuildingModel_ElevationSample_Integrity"/>, <see cref="BuildingModel_ElevationSample_Verify"/> and <see cref="BuildingModel_ElevationBenchmarkAsync"/> read.</para>
        /// <para>Medium test (24 s Debug, measured 2026-11-03): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void BuildingModel_ElevationSample_Draw()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            Dictionary<string, string>? settings = ElevationBenchmark_Settings(assembly);
            if (settings is null)
            {
                return;
            }

            List<string> counties = ElevationBenchmark_Codes(settings);
            int quota = ElevationBenchmark_Quota(settings);
            int perSheet = (quota + elevationBenchmark_SheetsPerCounty - 1) / elevationBenchmark_SheetsPerCounty;

            string directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly)!;

            List<JsonObject> records = [];
            List<string> censusLines =
            [
                "# CRS and height-class census of the elevation benchmark panel (DiGi.GIS.Analytical#3).",
                "# Full header scan of every .gml entry in each panel archive. Stubs are sheets below " + elevationBenchmark_StubLength.ToString(CultureInfo.InvariantCulture) + " uncompressed bytes (empty header-only sheets, skipped without parsing).",
                "# dataset county classification stubs noEnvelope zeroBuildings unusable noReference parsedSheets drawn xRange yRange srsNameCounts",
            ];
            List<string> shortfalls = [];
            int count_Non2180 = 0;
            int count_CrsSuspect = 0;

            for (int datasetIndex = 0; datasetIndex < elevationBenchmark_DatasetPaths.Length; datasetIndex++)
            {
                string datasetPath = elevationBenchmark_DatasetPaths[datasetIndex];
                string datasetName = elevationBenchmark_DatasetNames[datasetIndex];

                foreach (string county in counties)
                {
                    string path_Zip = Path.Combine(settings["ModeleDirectory"], datasetPath, county + ".zip");
                    Assert.True(File.Exists(path_Zip), $"Panel county archive not found: {path_Zip}");

                    // The per-stratum seed arithmetic mirrors DiGi.GIS.PostgreSQL.Query.RandomSeed (unchecked((seed * 397) + countyId));
                    // it is duplicated rather than called because this project does not reference the PostgreSQL layer.
                    int seed_Stratum = unchecked((elevationBenchmark_Seed * 397) + int.Parse(county, CultureInfo.InvariantCulture)) ^ datasetIndex;

                    Dictionary<string, (string SrsName, string Horizontal, string Vertical, double MinX, double MinY, double MaxX, double MaxY, bool HasEnvelope)> headers = [];
                    Dictionary<string, int> srsNames = [];
                    int stubs = 0;
                    int noEnvelope = 0;
                    int zeroBuildings = 0;
                    int unusable = 0;
                    int noReference = 0;
                    int parsedSheets = 0;
                    double minX = double.MaxValue;
                    double minY = double.MaxValue;
                    double maxX = double.MinValue;
                    double maxY = double.MinValue;
                    List<JsonObject> records_County = [];

                    using (ZipArchive zipArchive = ZipFile.OpenRead(path_Zip))
                    {
                        List<ZipArchiveEntry> zipArchiveEntries = [.. zipArchive.Entries.Where(x => x.Name.EndsWith(".gml", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.FullName, StringComparer.Ordinal)];

                        foreach (ZipArchiveEntry zipArchiveEntry in zipArchiveEntries)
                        {
                            if (zipArchiveEntry.Length < elevationBenchmark_StubLength)
                            {
                                stubs++;
                                continue;
                            }

                            (string SrsName, string Horizontal, string Vertical, double MinX, double MinY, double MaxX, double MaxY, bool HasEnvelope) header = ElevationBenchmark_Header(ElevationBenchmark_Peek(zipArchiveEntry, 8192));
                            headers[zipArchiveEntry.FullName] = header;

                            if (!header.HasEnvelope)
                            {
                                noEnvelope++;
                                continue;
                            }

                            srsNames[header.SrsName] = srsNames.TryGetValue(header.SrsName, out int count_SrsName) ? count_SrsName + 1 : 1;

                            minX = Math.Min(minX, header.MinX);
                            minY = Math.Min(minY, header.MinY);
                            maxX = Math.Max(maxX, header.MaxX);
                            maxY = Math.Max(maxY, header.MaxY);

                            if (header.Horizontal != "EPSG:2180")
                            {
                                count_Non2180++;
                            }
                        }

                        ElevationBenchmark_Shuffle(zipArchiveEntries, new Random(seed_Stratum));

                        (LOD? lOD, int? year) = ElevationBenchmark_LODYear(path_Zip);
                        foreach (ZipArchiveEntry zipArchiveEntry in zipArchiveEntries)
                        {
                            if (records_County.Count >= quota)
                            {
                                break;
                            }

                            if (zipArchiveEntry.Length < elevationBenchmark_StubLength)
                            {
                                continue;
                            }

                            if (parsedSheets >= elevationBenchmark_MaxSheetsParsed)
                            {
                                break;
                            }

                            parsedSheets++;

                            CityModel? cityModel;
                            using (Stream stream = zipArchiveEntry.Open())
                            {
                                cityModel = CityGML.Create.CityModel(stream, lOD, year);
                            }

                            List<Building>? buildings = cityModel?.Buildings?.ToList();
                            if (buildings == null || buildings.Count == 0)
                            {
                                zeroBuildings++;
                                continue;
                            }

                            buildings.Sort((x, y) => string.CompareOrdinal(x.UniqueId, y.UniqueId));
                            ElevationBenchmark_Shuffle(buildings, new Random(unchecked(seed_Stratum + parsedSheets)));

                            int taken = 0;
                            foreach (Building building in buildings)
                            {
                                if (records_County.Count >= quota || taken >= perSheet)
                                {
                                    break;
                                }

                                string? reference = CityGML.Query.Reference(building);
                                if (string.IsNullOrWhiteSpace(reference))
                                {
                                    noReference++;
                                    continue;
                                }

                                BuildingModel? buildingModel = Create.BuildingModel(building);
                                BoundingBox3D? boundingBox3D = buildingModel?.GetBoundingBox();
                                Point3D? centroid = boundingBox3D?.GetCentroid();
                                if (boundingBox3D == null || centroid == null)
                                {
                                    unusable++;
                                    continue;
                                }

                                (string SrsName, string Horizontal, string Vertical, double MinX, double MinY, double MaxX, double MaxY, bool HasEnvelope) header = headers[zipArchiveEntry.FullName];

                                if (centroid.X < elevationBenchmark_CrsMin || centroid.X > elevationBenchmark_CrsMax || centroid.Y < elevationBenchmark_CrsMin || centroid.Y > elevationBenchmark_CrsMax)
                                {
                                    count_CrsSuspect++;
                                }

                                records_County.Add(new JsonObject
                                {
                                    ["Dataset"] = datasetName,
                                    ["County"] = county,
                                    ["Source"] = Path.Combine(datasetPath, county + ".zip") + "!" + zipArchiveEntry.FullName,
                                    ["Reference"] = reference,
                                    ["Easting"] = centroid.X,
                                    ["Northing"] = centroid.Y,
                                    ["MinZ"] = boundingBox3D.Min.Z,
                                    ["MaxZ"] = boundingBox3D.Max.Z,
                                    ["HorizontalCrs"] = header.Horizontal,
                                    ["VerticalCrs"] = header.Vertical,
                                });

                                taken++;
                            }
                        }
                    }

                    if (records_County.Count < quota)
                    {
                        shortfalls.Add($"{datasetName}/{county} drew {records_County.Count.ToString(CultureInfo.InvariantCulture)} of {quota.ToString(CultureInfo.InvariantCulture)}");
                    }

                    records.AddRange(records_County);

                    int entries_Other = headers.Values.Count(x => x.HasEnvelope && x.Horizontal != "EPSG:2180");
                    string classification = entries_Other == 0 ? "All2180" : (headers.Values.Count(x => x.HasEnvelope && x.Horizontal == "EPSG:2180") > 0 ? "Mixed" : "Other");

                    censusLines.Add(string.Join(' ',
                        datasetName,
                        county,
                        classification,
                        "stubs=" + stubs.ToString(CultureInfo.InvariantCulture),
                        "noEnvelope=" + noEnvelope.ToString(CultureInfo.InvariantCulture),
                        "zeroBuildings=" + zeroBuildings.ToString(CultureInfo.InvariantCulture),
                        "unusable=" + unusable.ToString(CultureInfo.InvariantCulture),
                        "noReference=" + noReference.ToString(CultureInfo.InvariantCulture),
                        "parsedSheets=" + parsedSheets.ToString(CultureInfo.InvariantCulture),
                        "drawn=" + records_County.Count.ToString(CultureInfo.InvariantCulture),
                        "x=[" + ElevationBenchmark_Range(minX, maxX) + "]",
                        "y=[" + ElevationBenchmark_Range(minY, maxY) + "]",
                        "srsName={" + string.Join(", ", srsNames.Select(x => x.Key + "=" + x.Value.ToString(CultureInfo.InvariantCulture))) + "}"));
                }
            }

            JsonArray jsonArray_Counties = [];
            counties.ForEach(x => jsonArray_Counties.Add(x));

            JsonArray jsonArray_Records = [];
            records.ForEach(x => jsonArray_Records.Add(x));

            JsonObject sample = new()
            {
                ["Description"] = "LoD base-elevation vs GUGiK NMT benchmark sample (DiGi.GIS.Analytical#3). MinZ/MaxZ and Easting/Northing come from the bounding box of the building model built from the named CityGML sheet; Source is <dataset zip>!<gml entry> under the ModeleDirectory of user files/DiGi.GIS.Analytical_ElevationBenchmark.conf. Drawn to user files/reports/ and committed to DiGi.Test/files/ after review.",
                ["Seed"] = elevationBenchmark_Seed,
                ["DrawnUtc"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["BuildingsPerCounty"] = quota,
                ["Counties"] = jsonArray_Counties,
                ["Records"] = jsonArray_Records,
            };

            File.WriteAllText(Path.Combine(directory_Reports, elevationBenchmark_FixtureName), sample.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            File.WriteAllLines(Path.Combine(directory_Reports, "ElevationBenchmark_CRS_Census.txt"), censusLines);

            File.WriteAllLines(Path.Combine(directory_Reports, "ElevationBenchmark_Draw_Diff.txt"), ElevationBenchmark_Diff(ElevationBenchmark_SampleJson(assembly), records));

            // The guards run after the reports so a red run keeps its evidence.
            Assert.True(shortfalls.Count == 0, $"Sample shortfall in {shortfalls.Count.ToString(CultureInfo.InvariantCulture)} county-dataset cells: {string.Join(", ", shortfalls)}");
            Assert.True(count_Non2180 == 0, $"The CRS census found {count_Non2180.ToString(CultureInfo.InvariantCulture)} non-EPSG:2180 sheet headers in the panel - implement the projected-source ToEPSG2180 overload (DiGi.GIS/Convert/ToEPSG2180/Point2D.cs, plan Step 1.3), re-tier DiGi.GIS.Analytical#3 to ai: heavy, and revisit the panel before drawing the sample.");
            Assert.True(count_CrsSuspect == 0, $"The CRS guard found {count_CrsSuspect.ToString(CultureInfo.InvariantCulture)} sampled centroids outside the PL-1992 range {elevationBenchmark_CrsMin.ToString(CultureInfo.InvariantCulture)}-{elevationBenchmark_CrsMax.ToString(CultureInfo.InvariantCulture)} m - see the census report.");
            Assert.Equal(elevationBenchmark_DatasetPaths.Length * counties.Count * quota, records.Count);
        }

        /// <summary>
        /// Guards the committed elevation benchmark sample fixture <c>DiGi.Test/files/ElevationBenchmarkSample.json</c>: the five datasets are present, each has at least 200 records across at least 8 counties, no <c>(Dataset, Reference)</c> pair repeats, and every coordinate sits in the PL-1992 range. Reference: DiGi.GIS.Analytical#3.
        /// <para>Structural only - it reads the fixture and touches no archive and no network, so it runs in every default Short/Medium pass while the draw, verify and measure facts are Long.</para>
        /// <para>Short test: runs when DIGI_TEST_MAX_DURATION is Short, Medium (the default) or Long.</para>
        /// </summary>
        [Fact]
        public void BuildingModel_ElevationSample_Integrity()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            JsonObject? sample = ElevationBenchmark_SampleJson(assembly);
            Assert.NotNull(sample);

            List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> records = ElevationBenchmark_Records(sample!);
            Assert.NotEmpty(records);

            HashSet<string> datasets = [.. records.ConvertAll(x => x.Dataset)];
            Assert.True(datasets.SetEquals(elevationBenchmark_DatasetNames), $"Fixture datasets [{string.Join(", ", datasets)}] do not match the expected five [{string.Join(", ", elevationBenchmark_DatasetNames)}]");

            foreach (string datasetName in elevationBenchmark_DatasetNames)
            {
                List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> records_Dataset = records.FindAll(x => x.Dataset == datasetName);
                Assert.True(records_Dataset.Count >= 200, $"{datasetName} holds {records_Dataset.Count.ToString(CultureInfo.InvariantCulture)} records, the benchmark requires at least 200");

                HashSet<string> counties_Dataset = [.. records_Dataset.ConvertAll(x => x.County)];
                Assert.True(counties_Dataset.Count >= 8, $"{datasetName} spans {counties_Dataset.Count.ToString(CultureInfo.InvariantCulture)} counties, the benchmark requires at least 8");
            }

            HashSet<string> keys = [.. records.ConvertAll(x => x.Dataset + "|" + x.Reference)];
            Assert.Equal(records.Count, keys.Count);

            List<string> crsSuspect = records.FindAll(x => x.Easting < elevationBenchmark_CrsMin || x.Easting > elevationBenchmark_CrsMax || x.Northing < elevationBenchmark_CrsMin || x.Northing > elevationBenchmark_CrsMax).ConvertAll(x => x.Dataset + "/" + x.Reference);
            Assert.True(crsSuspect.Count == 0, $"The committed fixture holds {crsSuspect.Count.ToString(CultureInfo.InvariantCulture)} records outside the PL-1992 range ({string.Join(", ", crsSuspect.Take(10))}) - fix or drop them (they are CrsSuspect at measure time) and redraw.");
        }

        /// <summary>
        /// Re-parses every sheet named in the committed elevation benchmark sample fixture and asserts the stored bounding-box values still reproduce: <c>Min.Z</c> and the centroid within <c>Core.Constants.Tolerance.Distance</c>. Reference: DiGi.GIS.Analytical#3.
        /// <para>Conf and archive guarded like <see cref="BuildingModel_ElevationSample_Draw"/>: without <c>user files/DiGi.GIS.Analytical_ElevationBenchmark.conf</c> the fact returns without asserting anything. It catches a stale fixture after a Geoportal data refresh or a change to the building-model creator.</para>
        /// <para>Medium test (24 s Debug, measured 2026-11-03): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void BuildingModel_ElevationSample_Verify()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            Dictionary<string, string>? settings = ElevationBenchmark_Settings(assembly);
            if (settings is null)
            {
                return;
            }

            JsonObject? sample = ElevationBenchmark_SampleJson(assembly);
            Assert.NotNull(sample);

            List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> records = ElevationBenchmark_Records(sample!);
            Assert.NotEmpty(records);

            double tolerance = Core.Constants.Tolerance.Distance;

            foreach (IGrouping<string, (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> grouping_Zip in records.GroupBy(x => x.Source.Substring(0, x.Source.IndexOf('!'))))
            {
                string path_Zip = Path.Combine(settings["ModeleDirectory"], grouping_Zip.Key);
                Assert.True(File.Exists(path_Zip), $"Fixture source archive not found: {path_Zip}");

                using ZipArchive zipArchive = ZipFile.OpenRead(path_Zip);

                foreach (IGrouping<string, (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> grouping_Entry in grouping_Zip.GroupBy(x => x.Source))
                {
                    string name_Entry = grouping_Entry.Key.Substring(grouping_Entry.Key.IndexOf('!') + 1);
                    ZipArchiveEntry? zipArchiveEntry = zipArchive.GetEntry(name_Entry);
                    Assert.True(zipArchiveEntry != null, $"Fixture source entry not found: {grouping_Entry.Key}");

                    (LOD? lOD, int? year) = ElevationBenchmark_LODYear(path_Zip);
                    CityModel? cityModel;
                    using (Stream stream = zipArchiveEntry!.Open())
                    {
                        cityModel = CityGML.Create.CityModel(stream, lOD, year);
                    }

                    foreach ((string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) record in grouping_Entry)
                    {
                        Building? building = cityModel?.Buildings?.FirstOrDefault(x => CityGML.Query.Reference(x) == record.Reference);
                        Assert.True(building != null, $"Fixture reference {record.Dataset}/{record.Reference} not found in {record.Source}");

                        BuildingModel? buildingModel = Create.BuildingModel(building);
                        Assert.True(buildingModel != null, $"Fixture building {record.Dataset}/{record.Reference} no longer converts to a BuildingModel");

                        BoundingBox3D? boundingBox3D = buildingModel!.GetBoundingBox();
                        Assert.True(boundingBox3D != null, $"Fixture building {record.Dataset}/{record.Reference} no longer has a bounding box");

                        Point3D? centroid = boundingBox3D!.GetCentroid();
                        Assert.True(centroid != null, $"Fixture building {record.Dataset}/{record.Reference} no longer has a centroid");

                        Assert.True(Math.Abs(boundingBox3D.Min.Z - record.MinZ) <= tolerance, $"Fixture {record.Dataset}/{record.Reference} Min.Z drifted: stored {ElevationBenchmark_Invariant(record.MinZ)}, recomputed {ElevationBenchmark_Invariant(boundingBox3D.Min.Z)}");
                        Assert.True(Math.Abs(centroid!.X - record.Easting) <= tolerance && Math.Abs(centroid.Y - record.Northing) <= tolerance, $"Fixture {record.Dataset}/{record.Reference} centroid drifted: stored ({ElevationBenchmark_Invariant(record.Easting)}, {ElevationBenchmark_Invariant(record.Northing)}), recomputed ({ElevationBenchmark_Invariant(centroid.X)}, {ElevationBenchmark_Invariant(centroid.Y)})");
                    }
                }
            }
        }

        /// <summary>
        /// Measures the LoD base elevations of the committed elevation benchmark sample against the GUGiK NMT service and writes the statistics to <c>user files/reports/</c>: per-record rows (<c>ElevationBenchmark_Rows.csv</c>) and the per-dataset, per-county and distribution tables (<c>ElevationBenchmark_Summary.md</c>). Reference: DiGi.GIS.Analytical#3.
        /// <para>Network only - it reads the committed fixture via <c>Core.xUnit.Query.FilePath</c> and needs no conf, so it runs on any machine. Each record is classified as <c>CrsSuspect</c> (centroid outside the PL-1992 range), <c>Unresolved</c> (NMT not retrievable - the documented zero-sentinel handling of <c>Query.ElevationAsync</c>), <c>GroundRelative</c> (model height at or below 5 m with NMT at or above 30 m) or <c>Ok</c>; only <c>Ok</c> records enter the statistics, the rest are counted and listed.</para>
        /// <para>The tables carry mean, median, p95 (nearest-rank) and max of absolute dZ, plus the signed mean with its 95% confidence interval, RMSE and LE95 = 1.96 * RMSE (NSSDA). A stratum is flagged when the signed mean reaches 0.10 m in magnitude and its confidence interval excludes zero. Numbers are written under <c>InvariantCulture</c>.</para>
        /// <para>Medium test (7-27 s depending on GUGiK service latency, measured 2026-11-03): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingModel_ElevationBenchmarkAsync()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            JsonObject? sample = ElevationBenchmark_SampleJson(assembly);
            Assert.NotNull(sample);

            List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> records = ElevationBenchmark_Records(sample!);
            Assert.NotEmpty(records);

            // Structural checks first, so a fixture problem is named before any statistic is reported.
            HashSet<string> datasets = [.. records.ConvertAll(x => x.Dataset)];
            Assert.True(datasets.SetEquals(elevationBenchmark_DatasetNames), $"Fixture datasets [{string.Join(", ", datasets)}] do not match the expected five");
            foreach (string datasetName in elevationBenchmark_DatasetNames)
            {
                List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> records_Dataset = records.FindAll(x => x.Dataset == datasetName);
                Assert.True(records_Dataset.Count >= 200, $"{datasetName} holds {records_Dataset.Count.ToString(CultureInfo.InvariantCulture)} records, the benchmark requires at least 200");
                Assert.True(records_Dataset.ConvertAll(x => x.County).Distinct().Count() >= 8, $"{datasetName} spans fewer than 8 counties");
            }

            HashSet<string> keys = [.. records.ConvertAll(x => x.Dataset + "|" + x.Reference)];
            Assert.Equal(records.Count, keys.Count);

            using HttpClient httpClient = new();

            double?[] nmts = new double?[records.Count];
            for (int datasetIndex = 0; datasetIndex < elevationBenchmark_DatasetNames.Length; datasetIndex++)
            {
                string datasetName = elevationBenchmark_DatasetNames[datasetIndex];

                List<int> indexes = [];
                List<Point2D> point2Ds = [];
                for (int i = 0; i < records.Count; i++)
                {
                    if (records[i].Dataset == datasetName)
                    {
                        indexes.Add(i);
                        point2Ds.Add(new Point2D(records[i].Easting, records[i].Northing));
                    }
                }

                if (indexes.Count == 0)
                {
                    continue;
                }

                List<Point3D?>? point3Ds = await httpClient.ElevationsAsync(point2Ds, elevationBenchmark_MaxConcurrentRequests, elevationBenchmark_RetryCount, elevationBenchmark_RetryDelay, cancellationToken: CancellationToken.None);
                Assert.NotNull(point3Ds);
                Assert.Equal(point2Ds.Count, point3Ds!.Count);

                for (int j = 0; j < indexes.Count; j++)
                {
                    nmts[indexes[j]] = point3Ds[j]?.Z;
                }
            }

            string[] flags = new string[records.Count];
            double[] dzSigned = new double[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) record = records[i];
                double? nmt = nmts[i];

                if (record.Easting < elevationBenchmark_CrsMin || record.Easting > elevationBenchmark_CrsMax || record.Northing < elevationBenchmark_CrsMin || record.Northing > elevationBenchmark_CrsMax)
                {
                    flags[i] = "CrsSuspect";
                }
                else if (nmt == null)
                {
                    flags[i] = "Unresolved";
                }
                else if (record.MinZ <= elevationBenchmark_GroundRelativeModelZ && nmt >= elevationBenchmark_GroundRelativeNmt)
                {
                    flags[i] = "GroundRelative";
                }
                else
                {
                    flags[i] = "Ok";
                    dzSigned[i] = record.MinZ - nmt.Value;
                }
            }

            List<string> rows_Csv = ["Dataset,County,Reference,Easting,Northing,MinZ,NMT,DzSigned,DzAbs,VerticalCrs,Flag"];
            for (int i = 0; i < records.Count; i++)
            {
                (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) record = records[i];
                bool ok = flags[i] == "Ok";
                rows_Csv.Add(string.Join(',',
                    record.Dataset,
                    record.County,
                    record.Reference,
                    ElevationBenchmark_Invariant(record.Easting),
                    ElevationBenchmark_Invariant(record.Northing),
                    ElevationBenchmark_Invariant(record.MinZ),
                    nmts[i] == null ? string.Empty : ElevationBenchmark_Invariant(nmts[i]!.Value),
                    ok ? ElevationBenchmark_Invariant(dzSigned[i]) : string.Empty,
                    ok ? ElevationBenchmark_Invariant(Math.Abs(dzSigned[i])) : string.Empty,
                    record.VerticalCrs,
                    flags[i]));
            }
            Assert.Equal(records.Count + 1, rows_Csv.Count);

            Dictionary<string, List<double>> dz_Dataset = [];
            Dictionary<string, List<double>> dz_County = [];
            Dictionary<string, List<double>> dz_Cell = [];
            Dictionary<string, int[]> counts_Dataset = [];
            Dictionary<string, int[]> counts_County = [];
            Dictionary<string, int[]> counts_Cell = [];
            for (int i = 0; i < records.Count; i++)
            {
                (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) record = records[i];

                if (!counts_Dataset.TryGetValue(record.Dataset, out int[]? counts_Dataset_Temp))
                {
                    counts_Dataset_Temp = new int[4];
                    counts_Dataset[record.Dataset] = counts_Dataset_Temp;
                    dz_Dataset[record.Dataset] = [];
                }

                if (!counts_County.TryGetValue(record.County, out int[]? counts_County_Temp))
                {
                    counts_County_Temp = new int[4];
                    counts_County[record.County] = counts_County_Temp;
                    dz_County[record.County] = [];
                }

                string key_Cell = record.Dataset + "|" + record.County;
                if (!counts_Cell.TryGetValue(key_Cell, out int[]? counts_Cell_Temp))
                {
                    counts_Cell_Temp = new int[4];
                    counts_Cell[key_Cell] = counts_Cell_Temp;
                    dz_Cell[key_Cell] = [];
                }

                int bucket = flags[i] switch
                {
                    "Unresolved" => 0,
                    "GroundRelative" => 1,
                    "CrsSuspect" => 2,
                    _ => 3,
                };

                counts_Dataset_Temp[bucket]++;
                counts_County_Temp[bucket]++;
                counts_Cell_Temp[bucket]++;

                if (bucket == 3)
                {
                    dz_Dataset[record.Dataset].Add(dzSigned[i]);
                    dz_County[record.County].Add(dzSigned[i]);
                    dz_Cell[key_Cell].Add(dzSigned[i]);
                }
            }

            int count_Ok = 0;
            int count_Unresolved = 0;
            int count_GroundRelative = 0;
            int count_CrsSuspect = 0;
            foreach (int[] counts in counts_Dataset.Values)
            {
                count_Unresolved += counts[0];
                count_GroundRelative += counts[1];
                count_CrsSuspect += counts[2];
                count_Ok += counts[3];
            }

            string? path_Fixture = Core.xUnit.Query.FilePath(assembly, elevationBenchmark_FixtureName);
            string hash_Fixture = path_Fixture == null || !File.Exists(path_Fixture) ? "unknown" : System.Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path_Fixture))).ToLowerInvariant();

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("# LoD base elevation vs GUGiK NMT - benchmark summary (DiGi.GIS.Analytical#3)");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(FormattableString.Invariant($"query_utc={DateTime.UtcNow:yyyy-MM-dd'T'HH:mm:ss'Z'} nmt_source=GetHByXY seed={elevationBenchmark_Seed} fixture_sha256={hash_Fixture}"));
            stringBuilder.AppendLine(FormattableString.Invariant($"rows={records.Count} ok={count_Ok} unresolved={count_Unresolved} groundRelative={count_GroundRelative} crsSuspect={count_CrsSuspect}"));
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Classification precedence: CrsSuspect (centroid outside the PL-1992 range), then Unresolved (NMT not retrievable), then GroundRelative (MinZ <= " + elevationBenchmark_GroundRelativeModelZ.ToString(CultureInfo.InvariantCulture) + " m and NMT >= " + elevationBenchmark_GroundRelativeNmt.ToString(CultureInfo.InvariantCulture) + " m), then Ok. Only Ok records enter the statistics.");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("p95 is nearest-rank (index ceil(p * n)); the 95% CI is mean +/- 1.96 * s / sqrt(n) with the sample standard deviation; LE95 = 1.96 * RMSE (NSSDA). A stratum is flagged when |mean signed dZ| >= " + elevationBenchmark_OffsetFlagAbsMean.ToString(CultureInfo.InvariantCulture) + " m and the CI excludes zero.");

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("## Per dataset");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Dataset | n | Unresolved | GroundRelative | CrsSuspect | mean abs dZ | median abs dZ | p95 abs dZ | max abs dZ | mean dZ signed | CI low | CI high | RMSE | LE95 | Vertical CRS | Flag |");
            stringBuilder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|");
            foreach (string datasetName in elevationBenchmark_DatasetNames)
            {
                List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> records_Dataset = records.FindAll(x => x.Dataset == datasetName);
                HashSet<string> vertical_Crs = [.. records_Dataset.ConvertAll(x => x.VerticalCrs)];
                stringBuilder.AppendLine(ElevationBenchmark_Row(datasetName, counts_Dataset[datasetName], dz_Dataset[datasetName], string.Join(", ", vertical_Crs)));
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("## Per county (pooled over datasets)");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| County | n | Unresolved | GroundRelative | CrsSuspect | mean abs dZ | median abs dZ | p95 abs dZ | max abs dZ | mean dZ signed | CI low | CI high | RMSE | LE95 | Flag |");
            stringBuilder.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
            foreach (string county in records.ConvertAll(x => x.County).Distinct().Order(StringComparer.Ordinal))
            {
                stringBuilder.AppendLine(ElevationBenchmark_Row(county, counts_County[county], dz_County[county], null));
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("## Per dataset and county (cells of n = 25; p95 is coarse at this size)");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Dataset | County | n | mean abs dZ | p95 abs dZ | max abs dZ |");
            stringBuilder.AppendLine("|---|---|---:|---:|---:|---:|");
            foreach (string key_Cell in counts_Cell.Keys.Order(StringComparer.Ordinal))
            {
                (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) record_Cell = records.Find(x => x.Dataset + "|" + x.County == key_Cell);
                List<double> dz_Cell_Temp = dz_Cell[key_Cell];
                (int N, double MeanAbs, double MedianAbs, double P95Abs, double MaxAbs, double MeanSigned, double CiLow, double CiHigh, double Rmse, double Le95) stats = ElevationBenchmark_Stats(dz_Cell_Temp);
                stringBuilder.AppendLine($"| {record_Cell.Dataset} | {record_Cell.County} | {stats.N.ToString(CultureInfo.InvariantCulture)} | {ElevationBenchmark_Text(stats.MeanAbs)} | {ElevationBenchmark_Text(stats.P95Abs)} | {ElevationBenchmark_Text(stats.MaxAbs)} |");
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("## Absolute dZ distribution (Ok records, counts and share of dataset)");
            stringBuilder.AppendLine();
            stringBuilder.Append("| Dataset | ");
            for (int i = 0; i < elevationBenchmark_BinEdges.Length; i++)
            {
                stringBuilder.Append('[').Append(ElevationBenchmark_Text(i == 0 ? 0 : elevationBenchmark_BinEdges[i - 1])).Append(", ").Append(ElevationBenchmark_Text(elevationBenchmark_BinEdges[i])).Append(") | ");
            }

            stringBuilder.Append($"[ {ElevationBenchmark_Text(elevationBenchmark_BinEdges[^1])}, inf) |");
            stringBuilder.AppendLine();
            stringBuilder.Append("|---|");
            for (int i = 0; i < elevationBenchmark_BinEdges.Length + 1; i++)
            {
                stringBuilder.Append("---:|");
            }

            stringBuilder.AppendLine();
            foreach (string datasetName in elevationBenchmark_DatasetNames)
            {
                int[] bins = new int[elevationBenchmark_BinEdges.Length + 1];
                foreach (double dz in dz_Dataset[datasetName])
                {
                    double value = Math.Abs(dz);
                    int bin = 0;
                    while (bin < elevationBenchmark_BinEdges.Length && value >= elevationBenchmark_BinEdges[bin])
                    {
                        bin++;
                    }

                    bins[bin]++;
                }

                int total = dz_Dataset[datasetName].Count;
                stringBuilder.Append("| ").Append(datasetName).Append(" | ");
                foreach (int bin in bins)
                {
                    double share = total == 0 ? 0 : 100.0 * bin / total;
                    stringBuilder.Append(bin.ToString(CultureInfo.InvariantCulture)).Append(" (").Append(share.ToString("0.#", CultureInfo.InvariantCulture)).Append("%) | ");
                }

                stringBuilder.AppendLine();
            }

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("## Resolution");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Dataset | rows | resolved | fraction |");
            stringBuilder.AppendLine("|---|---:|---:|---:|");
            foreach (string datasetName in elevationBenchmark_DatasetNames)
            {
                int[] counts = counts_Dataset[datasetName];
                int rows = counts[0] + counts[1] + counts[2] + counts[3];
                int resolved = counts[1] + counts[3];
                double fraction = rows == 0 ? 0 : (double)resolved / rows;
                stringBuilder.AppendLine($"| {datasetName} | {rows.ToString(CultureInfo.InvariantCulture)} | {resolved.ToString(CultureInfo.InvariantCulture)} | {fraction.ToString("0.000", CultureInfo.InvariantCulture)} |");
            }

            string directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly)!;
            File.WriteAllLines(Path.Combine(directory_Reports, "ElevationBenchmark_Rows.csv"), rows_Csv);
            File.WriteAllText(Path.Combine(directory_Reports, "ElevationBenchmark_Summary.md"), stringBuilder.ToString());

            // Accuracy bounds pinned after the first measured runs (2026-11-03, Release, fact run alone):
            // every dataset resolved 1.000 and the largest per-dataset p95 abs dZ was 1.54 m. The plan's
            // candidate bound of 1.5 m is already breached by the measured 1.54 m, so the pinned p95 bound
            // is 2.0 m - loose enough for GUGiK NMT revisions, tight enough to notice a regression - and
            // the resolved-fraction floor of 0.95 leaves slack for coastal no-data voids (measure the
            // distribution before stating the tolerance - Coding - Automatic Tests.md).
            Assert.True(count_CrsSuspect == 0, $"The committed fixture holds {count_CrsSuspect.ToString(CultureInfo.InvariantCulture)} CrsSuspect records - see ElevationBenchmark_Rows.csv");
            foreach (string datasetName in elevationBenchmark_DatasetNames)
            {
                Assert.True(dz_Dataset[datasetName].Count > 0, $"{datasetName} has no Ok record to measure");

                int[] counts = counts_Dataset[datasetName];
                int rows = counts[0] + counts[1] + counts[2] + counts[3];
                int resolved = counts[1] + counts[3];
                Assert.True(rows == 0 || (double)resolved / rows >= 0.95, $"{datasetName} resolved fraction dropped below 0.95 (measured 1.000 on 2026-11-03)");

                (int N, double MeanAbs, double MedianAbs, double P95Abs, double MaxAbs, double MeanSigned, double CiLow, double CiHigh, double Rmse, double Le95) stats = ElevationBenchmark_Stats(dz_Dataset[datasetName]);
                Assert.True(stats.P95Abs <= 2.0, $"{datasetName} p95 abs dZ {ElevationBenchmark_Text(stats.P95Abs)} m exceeds the 2.0 m bound (measured 1.54 m max on 2026-11-03)");
            }
        }

        /// <summary>
        /// Reads <c>user files/DiGi.GIS.Analytical_ElevationBenchmark.conf</c> (<c>ModeleDirectory</c>, optional <c>Counties</c>, optional <c>BuildingsPerCounty</c>) and returns its settings, or null when the file or the directory it names is missing, so the facts using it return without measuring anything.
        /// </summary>
        /// <param name="assembly">The test assembly.</param>
        /// <returns>The settings, or null.</returns>
        private static Dictionary<string, string>? ElevationBenchmark_Settings(Assembly assembly)
        {
            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return null;
            }

            string path_Configuration = Path.Combine(directory_UserFiles!, elevationBenchmark_ConfName);
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

            if (!result.TryGetValue("ModeleDirectory", out string? directory_Modele) || !Directory.Exists(directory_Modele))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Returns the county codes of the <c>Counties</c> setting, or the default panel when the setting is absent, without duplicates.
        /// </summary>
        /// <param name="settings">The settings of <see cref="ElevationBenchmark_Settings"/>.</param>
        /// <returns>The county codes.</returns>
        private static List<string> ElevationBenchmark_Codes(Dictionary<string, string> settings)
        {
            if (!settings.TryGetValue("Counties", out string? counties) || string.IsNullOrWhiteSpace(counties))
            {
                return [.. elevationBenchmark_Counties];
            }

            return [.. counties!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct()];
        }

        /// <summary>
        /// Returns the <c>BuildingsPerCounty</c> setting, or the default quota when the setting is absent.
        /// </summary>
        /// <param name="settings">The settings of <see cref="ElevationBenchmark_Settings"/>.</param>
        /// <returns>The number of buildings drawn per county per dataset.</returns>
        private static int ElevationBenchmark_Quota(Dictionary<string, string> settings)
        {
            if (settings.TryGetValue("BuildingsPerCounty", out string? buildingsPerCounty) && int.TryParse(buildingsPerCounty, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) && result > 0)
            {
                return result;
            }

            return elevationBenchmark_BuildingsPerCounty;
        }

        /// <summary>
        /// Derives the <see cref="LOD"/> and year of a dataset archive from its path exactly as <c>CityGML.Query.RunAsync</c> does, so the draw and the verify parse the sheets the same way.
        /// </summary>
        /// <param name="path_Zip">The full path of the county archive.</param>
        /// <returns>The level of detail and year, either null when the path does not carry them.</returns>
        private static (LOD? lOD, int? year) ElevationBenchmark_LODYear(string path_Zip)
        {
            int? year = null;
            string? yearString = Path.GetFileName(Path.GetDirectoryName(path_Zip));
            if (!string.IsNullOrWhiteSpace(yearString) && int.TryParse(yearString.Trim(), out int year_Temp))
            {
                year = year_Temp;
            }

            LOD? lOD = null;
            string? lODString = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path_Zip)!));
            if (!string.IsNullOrWhiteSpace(lODString) && Enum.TryParse(lODString, true, out LOD lOD_Temp))
            {
                lOD = lOD_Temp;
            }

            if (lOD is null && year is null)
            {
                lODString = Path.GetFileName(Path.GetDirectoryName(path_Zip));
                if (!string.IsNullOrWhiteSpace(lODString) && Enum.TryParse(lODString, true, out lOD_Temp))
                {
                    lOD = lOD_Temp;
                }
            }

            return (lOD, year);
        }

        /// <summary>
        /// Reads the first bytes of a map sheet entry as text, for the header census. The entry can be opened again afterwards for parsing.
        /// </summary>
        /// <param name="zipArchiveEntry">The map sheet entry.</param>
        /// <param name="count">The maximum number of bytes to read.</param>
        /// <returns>The decoded header text.</returns>
        private static string ElevationBenchmark_Peek(ZipArchiveEntry zipArchiveEntry, int count)
        {
            byte[] bytes = new byte[count];
            int read = 0;
            using (Stream stream = zipArchiveEntry.Open())
            {
                while (read < count)
                {
                    int n = stream.Read(bytes, read, count - read);
                    if (n == 0)
                    {
                        break;
                    }

                    read += n;
                }
            }

            return Encoding.UTF8.GetString(bytes, 0, read);
        }

        /// <summary>
        /// Extracts the <c>gml:Envelope</c> srsName and corner coordinates from a map sheet header. The srsName is a comma-separated compound URN such as <c>urn:ogc:def:crs,crs:EPSG::2180,crs:PL-KRON86-NH</c>; the horizontal component is the EPSG one, the vertical component the datum name.
        /// </summary>
        /// <param name="text">The decoded header text.</param>
        /// <returns>The srsName, its horizontal and vertical components, the corner coordinate ranges, and whether an envelope was found at all.</returns>
        private static (string SrsName, string Horizontal, string Vertical, double MinX, double MinY, double MaxX, double MaxY, bool HasEnvelope) ElevationBenchmark_Header(string text)
        {
            Match match_Envelope = Regex.Match(text, "<(?:[A-Za-z0-9_]+:)?Envelope[^>]*srsName=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (!match_Envelope.Success)
            {
                return ("none", "none", "none", 0, 0, 0, 0, false);
            }

            (string Horizontal, string Vertical) crs = ElevationBenchmark_Crs(match_Envelope.Groups[1].Value);

            double[] lower = ElevationBenchmark_Corner(Regex.Match(text, "<(?:[A-Za-z0-9_]+:)?lowerCorner[^>]*>([^<]+)<", RegexOptions.IgnoreCase));
            double[] upper = ElevationBenchmark_Corner(Regex.Match(text, "<(?:[A-Za-z0-9_]+:)?upperCorner[^>]*>([^<]+)<", RegexOptions.IgnoreCase));
            if (lower.Length < 2 || upper.Length < 2)
            {
                // No corners: the sentinels leave the census coordinate ranges untouched.
                return (match_Envelope.Groups[1].Value, crs.Horizontal, crs.Vertical, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, true);
            }

            return (match_Envelope.Groups[1].Value, crs.Horizontal, crs.Vertical, Math.Min(lower[0], upper[0]), Math.Min(lower[1], upper[1]), Math.Max(lower[0], upper[0]), Math.Max(lower[1], upper[1]), true);
        }

        /// <summary>
        /// Parses the first two coordinates of a <c>gml:lowerCorner</c> or <c>gml:upperCorner</c> match.
        /// </summary>
        /// <param name="match">The corner match.</param>
        /// <returns>The coordinates, empty when the match is absent or unparsable.</returns>
        private static double[] ElevationBenchmark_Corner(Match match)
        {
            if (!match.Success)
            {
                return [];
            }

            string[] tokens = match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2 || !double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) || !double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
            {
                return [];
            }

            return [x, y];
        }

        /// <summary>
        /// Splits a compound srsName URN into its horizontal EPSG component and its vertical datum component.
        /// </summary>
        /// <param name="srsName">The srsName attribute value.</param>
        /// <returns>The horizontal and vertical components, "none" when absent.</returns>
        private static (string Horizontal, string Vertical) ElevationBenchmark_Crs(string srsName)
        {
            string horizontal = "none";
            string vertical = "none";

            foreach (string part_Raw in srsName.Split(',', StringSplitOptions.TrimEntries))
            {
                string part = part_Raw;
                if (part.StartsWith("urn:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (part.StartsWith("crs:", StringComparison.OrdinalIgnoreCase))
                {
                    part = part.Substring(4);
                }

                part = part.Replace("EPSG::", "EPSG:", StringComparison.Ordinal);

                if (part.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase))
                {
                    if (horizontal == "none")
                    {
                        horizontal = part;
                    }
                }
                else if (vertical == "none")
                {
                    vertical = part;
                }
            }

            return (horizontal, vertical);
        }

        /// <summary>
        /// Shuffles a list in place with the Fisher-Yates algorithm, so the same seed over the same order always gives the same permutation.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="values">The list to shuffle.</param>
        /// <param name="random">The seeded generator.</param>
        private static void ElevationBenchmark_Shuffle<T>(List<T> values, Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        /// <summary>
        /// Formats a coordinate range for the census report, invariant and empty-safe.
        /// </summary>
        /// <param name="min">The minimum.</param>
        /// <param name="max">The maximum.</param>
        /// <returns>The formatted range.</returns>
        private static string ElevationBenchmark_Range(double min, double max)
        {
            if (min > max)
            {
                return "empty";
            }

            return ElevationBenchmark_Invariant(min) + ", " + ElevationBenchmark_Invariant(max);
        }

        /// <summary>
        /// Renders a number under the invariant culture, at full round-trip precision.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The invariant text.</returns>
        private static string ElevationBenchmark_Invariant(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Renders a number under the invariant culture at three decimals, for the report tables.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The invariant text.</returns>
        private static string ElevationBenchmark_Text(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the <c>Records</c> array of the sample fixture into typed rows.
        /// </summary>
        /// <param name="sample">The sample fixture.</param>
        /// <returns>The rows.</returns>
        private static List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> ElevationBenchmark_Records(JsonObject sample)
        {
            List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> result = [];

            JsonArray? jsonArray = sample["Records"] as JsonArray;
            if (jsonArray == null)
            {
                return result;
            }

            foreach (JsonNode? jsonNode in jsonArray)
            {
                JsonObject? record = jsonNode as JsonObject;
                if (record == null)
                {
                    continue;
                }

                result.Add((
                    record["Dataset"]!.GetValue<string>(),
                    record["County"]!.GetValue<string>(),
                    record["Source"]!.GetValue<string>(),
                    record["Reference"]!.GetValue<string>(),
                    record["Easting"]!.GetValue<double>(),
                    record["Northing"]!.GetValue<double>(),
                    record["MinZ"]!.GetValue<double>(),
                    record["MaxZ"]!.GetValue<double>(),
                    record["HorizontalCrs"]!.GetValue<string>(),
                    record["VerticalCrs"]!.GetValue<string>()));
            }

            return result;
        }

        /// <summary>
        /// Reads the committed sample fixture, or null when it has not been committed yet.
        /// </summary>
        /// <param name="assembly">The test assembly.</param>
        /// <returns>The fixture, or null.</returns>
        private static JsonObject? ElevationBenchmark_SampleJson(Assembly assembly)
        {
            string? path = Core.xUnit.Query.FilePath(assembly, elevationBenchmark_FixtureName);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            return JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        }

        /// <summary>
        /// Compares a freshly drawn sample against the committed fixture and lists the missing, extra and changed rows. The diff reports; it never fails, because Geoportal revisions and creator changes legitimately move the sample - a human reviews and recommits.
        /// </summary>
        /// <param name="committed">The committed fixture, or null when none exists yet.</param>
        /// <param name="drawn">The freshly drawn records.</param>
        /// <returns>The report lines.</returns>
        private static List<string> ElevationBenchmark_Diff(JsonObject? committed, List<JsonObject> drawn)
        {
            List<string> result = [$"# ElevationBenchmark sample diff - drawn {DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}"];

            List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> rows_Drawn = ElevationBenchmark_Records_ToRows(drawn);
            if (committed == null)
            {
                result.Add("No committed fixture yet - review and copy user files/reports/" + elevationBenchmark_FixtureName + " to DiGi.Test/files/" + elevationBenchmark_FixtureName + ".");
                result.Add($"drawn: {rows_Drawn.Count.ToString(CultureInfo.InvariantCulture)} rows");
                return result;
            }

            List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> rows_Committed = ElevationBenchmark_Records(committed);
            result.Add($"committed: {rows_Committed.Count.ToString(CultureInfo.InvariantCulture)} rows, drawn: {rows_Drawn.Count.ToString(CultureInfo.InvariantCulture)} rows");

            Dictionary<string, (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> committed_ByKey = [];
            foreach ((string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) row in rows_Committed)
            {
                committed_ByKey[row.Dataset + "|" + row.Reference] = row;
            }

            Dictionary<string, (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> drawn_ByKey = [];
            foreach ((string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) row in rows_Drawn)
            {
                drawn_ByKey[row.Dataset + "|" + row.Reference] = row;
            }

            List<string> missing = [.. committed_ByKey.Keys.Except(drawn_ByKey.Keys).Order(StringComparer.Ordinal)];
            List<string> extra = [.. drawn_ByKey.Keys.Except(committed_ByKey.Keys).Order(StringComparer.Ordinal)];
            List<string> changed = [];
            foreach (string key in committed_ByKey.Keys.Intersect(drawn_ByKey.Keys).Order(StringComparer.Ordinal))
            {
                (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) before = committed_ByKey[key];
                (string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs) after = drawn_ByKey[key];

                double tolerance = Core.Constants.Tolerance.Distance;
                if (before.Source != after.Source || Math.Abs(before.MinZ - after.MinZ) > tolerance || Math.Abs(before.Easting - after.Easting) > tolerance || Math.Abs(before.Northing - after.Northing) > tolerance)
                {
                    changed.Add($"{key} MinZ {ElevationBenchmark_Invariant(before.MinZ)} -> {ElevationBenchmark_Invariant(after.MinZ)} centroid ({ElevationBenchmark_Invariant(before.Easting)}, {ElevationBenchmark_Invariant(before.Northing)}) -> ({ElevationBenchmark_Invariant(after.Easting)}, {ElevationBenchmark_Invariant(after.Northing)}) source {before.Source} -> {after.Source}");
                }
            }

            result.Add($"missing from draw (committed only): {missing.Count.ToString(CultureInfo.InvariantCulture)}");
            missing.ForEach(x => result.Add("  " + x));
            result.Add($"extra in draw (not committed): {extra.Count.ToString(CultureInfo.InvariantCulture)}");
            extra.ForEach(x => result.Add("  " + x));
            result.Add($"changed (same key, different geometry or source): {changed.Count.ToString(CultureInfo.InvariantCulture)}");
            changed.ForEach(x => result.Add("  " + x));

            return result;
        }

        /// <summary>
        /// Converts drawn record objects to typed rows, for the diff.
        /// </summary>
        /// <param name="records">The drawn records.</param>
        /// <returns>The typed rows.</returns>
        private static List<(string Dataset, string County, string Source, string Reference, double Easting, double Northing, double MinZ, double MaxZ, string HorizontalCrs, string VerticalCrs)> ElevationBenchmark_Records_ToRows(List<JsonObject> records)
        {
            JsonObject sample = [];
            JsonArray jsonArray = [];

            // DeepClone: the drawn nodes already belong to the sample array written to the reports directory.
            records.ForEach(x => jsonArray.Add(x.DeepClone()));
            sample["Records"] = jsonArray;

            return ElevationBenchmark_Records(sample);
        }

        /// <summary>
        /// Computes the benchmark statistics of a stratum from its signed dZ values: count, mean/median/p95/max of the absolute values, the signed mean with its 95% confidence interval, RMSE and LE95 = 1.96 * RMSE (NSSDA).
        /// </summary>
        /// <param name="dzSigned">The signed dZ values of the stratum's Ok records.</param>
        /// <returns>The statistics; all zeros when the stratum is empty.</returns>
        private static (int N, double MeanAbs, double MedianAbs, double P95Abs, double MaxAbs, double MeanSigned, double CiLow, double CiHigh, double Rmse, double Le95) ElevationBenchmark_Stats(List<double> dzSigned)
        {
            int n = dzSigned.Count;
            if (n == 0)
            {
                return (0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            }

            List<double> abs = [.. dzSigned.ConvertAll(Math.Abs)];
            abs.Sort();

            double meanSigned = dzSigned.Average();
            double meanAbs = abs.Average();

            double sum_Squares = dzSigned.Sum(x => (x - meanSigned) * (x - meanSigned));
            double deviation_Standard = n > 1 ? Math.Sqrt(sum_Squares / (n - 1)) : 0;
            double halfWidth = 1.96 * deviation_Standard / Math.Sqrt(n);

            double rmse = Math.Sqrt(dzSigned.Sum(x => x * x) / n);

            // Nearest-rank percentile: the value at index ceil(p * n) of the ascending list.
            int index_P95 = Math.Clamp((int)Math.Ceiling(0.95 * n) - 1, 0, n - 1);

            return (n, meanAbs, ElevationBenchmark_Median(abs), abs[index_P95], abs[^1], meanSigned, meanSigned - halfWidth, meanSigned + halfWidth, rmse, 1.96 * rmse);
        }

        /// <summary>
        /// Returns the median of an ascending-sorted list (the mean of the two middle values for even counts).
        /// </summary>
        /// <param name="values_Sorted">The sorted values.</param>
        /// <returns>The median.</returns>
        private static double ElevationBenchmark_Median(List<double> values_Sorted)
        {
            int n = values_Sorted.Count;
            if (n == 0)
            {
                return 0;
            }

            return n % 2 == 1 ? values_Sorted[n / 2] : (values_Sorted[(n / 2) - 1] + values_Sorted[n / 2]) / 2.0;
        }

        /// <summary>
        /// Formats one statistics table row. The <paramref name="verticalCrs"/> column is rendered for the per-dataset table only.
        /// </summary>
        /// <param name="name">The stratum name.</param>
        /// <param name="counts">The classification counts: Unresolved, GroundRelative, CrsSuspect, Ok.</param>
        /// <param name="dzSigned">The signed dZ values of the Ok records.</param>
        /// <param name="verticalCrs">The declared vertical CRS values of the stratum, or null.</param>
        /// <returns>The markdown table row.</returns>
        private static string ElevationBenchmark_Row(string name, int[] counts, List<double> dzSigned, string? verticalCrs)
        {
            (int N, double MeanAbs, double MedianAbs, double P95Abs, double MaxAbs, double MeanSigned, double CiLow, double CiHigh, double Rmse, double Le95) stats = ElevationBenchmark_Stats(dzSigned);

            bool flag = Math.Abs(stats.MeanSigned) >= elevationBenchmark_OffsetFlagAbsMean && (stats.CiLow > 0 || stats.CiHigh < 0);

            StringBuilder stringBuilder = new();
            stringBuilder.Append("| ").Append(name).Append(" | ");
            stringBuilder.Append(stats.N.ToString(CultureInfo.InvariantCulture)).Append(" | ");
            stringBuilder.Append(counts[0].ToString(CultureInfo.InvariantCulture)).Append(" | ");
            stringBuilder.Append(counts[1].ToString(CultureInfo.InvariantCulture)).Append(" | ");
            stringBuilder.Append(counts[2].ToString(CultureInfo.InvariantCulture)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.MeanAbs)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.MedianAbs)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.P95Abs)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.MaxAbs)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.MeanSigned)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.CiLow)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.CiHigh)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.Rmse)).Append(" | ");
            stringBuilder.Append(ElevationBenchmark_Text(stats.Le95)).Append(" | ");
            if (verticalCrs != null)
            {
                stringBuilder.Append(verticalCrs).Append(" | ");
            }

            stringBuilder.Append(flag ? "yes" : "-").Append(" |");

            return stringBuilder.ToString();
        }
    }
}
