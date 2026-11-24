using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.GIS.IO.Classes;
using DiGi.GIS.IO.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.PostgreSQL.Classes;
using GeoAPI.CoordinateSystems.Transformations;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The machine-specific configuration of <see cref="OpenBuildingImport_StageSplit"/>, kept in the git-ignored <c>user files/</c>.
        /// </summary>
        private const string fileName_OpenBuildingStageSplit = "DiGi.GIS.PostgreSQL_OpenBuildingStageSplit.conf";

        /// <summary>
        /// The number of timed runs per pass; the report gives their range.
        /// </summary>
        private const int count_StageSplit_Runs = 3;

        /// <summary>
        /// Measures where an open building import spends its time - reading the source, converting the footprints (<see cref="OpenBuildingPostgreSQLConverter.CreateRow"/>: reprojection, county filing, serialization) and writing them - per dataset file format, as the first step of DiGi.GIS.PostgreSQL#170.
        /// <para>Four passes over the same capped prefix of every configured input: (a) the reader alone, (b) the reader plus <see cref="OpenBuildingPostgreSQLConverter.CreateRow"/> against the real county parts, (c) the whole <see cref="OpenBuildingPostgreSQLConverter.ImportAsync"/> into the throwaway Storage test database at degree one, and (d) the same at the task's default degree, half the processors - the benchmark of the parallel conversion. Write cost is (c) minus (b), convert cost (b) minus (a). The optional <c>Releases</c> setting (<c>datasetKey|directory|pattern</c> separated by <c>;</c>) adds, per whole release folder, how many of its files <see cref="Query.SourcePathsOutOfScopeAsync"/> leaves unread for the scope; the optional <c>Passes</c> setting (default <c>abcd</c>) names the passes to run, so a comparison of import builds can leave out (a) and (b), which time code the import does not own. Each pass runs once as a warm-up and then <see cref="count_StageSplit_Runs"/> times; the report gives the range and <see cref="Environment.ProcessorCount"/>.</para>
        /// <para>The inputs are machine-specific and read from <c>user files/DiGi.GIS.PostgreSQL_OpenBuildingStageSplit.conf</c>: <c>CountiesPath</c> (a JSON array of <c>{"Id": …, "Object": …}</c>, the stored columns of the county parts, exported read-only from a development Main database), <c>CountyIds</c> (the scope), <c>Inputs</c> (<c>datasetKey|path</c> separated by <c>;</c>) and <c>MaxFootprints</c>. Without the file, or with an input missing, the fact returns without measuring anything. Pass (c) runs only when the Storage test configuration is usable; it writes into the scope's partitions of the throwaway database and drops them, with the register rows, in <c>finally</c>. The figures describe this machine and the throwaway database, not production.</para>
        /// <para>Long test (minutes per input). Run in isolation in Release: <c>dotnet test -c Release --filter "FullyQualifiedName~OpenBuildingImport_StageSplit"</c>.</para>
        /// </summary>
        [LongFact]
        public async Task OpenBuildingImport_StageSplit()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            Dictionary<string, string>? settings = OpenBuildingStageSplit_Settings(assembly);
            if (settings is null)
            {
                return;
            }

            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            Assert.NotNull(directory_UserFiles);

            string path_Counties = Path.Combine(directory_UserFiles, settings["CountiesPath"]);
            if (!File.Exists(path_Counties))
            {
                return;
            }

            // The export holds the stored columns the converter reads - the row id and the object - as
            // {"Id": …, "Object": …}: a county part's Object member does not survive the serializer round trip.
            JsonArray? jsonArray = JsonNode.Parse(File.ReadAllText(path_Counties)) as JsonArray;
            Assert.NotNull(jsonArray);

            List<AdministrativeAreal2D> counties = [];
            foreach (JsonNode? jsonNode in jsonArray)
            {
                if (jsonNode is JsonObject jsonObject && jsonObject["Object"] is JsonObject jsonObject_Object)
                {
                    counties.Add(new AdministrativeAreal2D()
                    {
                        Id = jsonObject["Id"]!.GetValue<int>(),
                        AdministrativeArealType = AdministrativeArealType.County,
                        Object = (JsonObject)jsonObject_Object.DeepClone(),
                    });
                }
            }

            Assert.NotEmpty(counties);
            Assert.NotEmpty(counties.Polygonal2DsByCountyId());

            HashSet<int> countyIds_Scope = [.. settings["CountyIds"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => int.Parse(x, CultureInfo.InvariantCulture))];
            int maxFootprints = int.Parse(settings["MaxFootprints"], CultureInfo.InvariantCulture);
            string passes = settings.TryGetValue("Passes", out string? passes_Setting) && !string.IsNullOrWhiteSpace(passes_Setting) ? passes_Setting.ToLowerInvariant() : "abcd";

            List<(string DatasetKey, string Path)> inputs = [];
            foreach (string input in settings["Inputs"].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string[] parts = input.Split('|', StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !File.Exists(parts[1]))
                {
                    return;
                }

                inputs.Add((parts[0], parts[1]));
            }

            // Pass (c) only against a usable throwaway Storage database - never a host's (TestDatabase).
            ConnectionData? connectionData_Storage = TestConnectionData(fileName_PostgreSQL_Storage_Test);
            bool isWritable = connectionData_Storage is not null
                && !IsSameDatabase(connectionData_Storage, TestConnectionData(Constants.FileName.PostgreSQL_Main))
                && !IsSameDatabase(connectionData_Storage, TestConnectionData(Constants.FileName.PostgreSQL_Storage))
                && DiGi.PostgreSQL.Query.IsAvailable(connectionData_Storage);

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"OpenBuildingImport_StageSplit (DiGi.GIS.PostgreSQL#170) - {DateTime.Now:yyyy-MM-dd HH:mm}");
            stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"ProcessorCount {Environment.ProcessorCount}, {counties.Count} county parts, scope {countyIds_Scope.Count} parts, {maxFootprints} footprints per input and pass, {count_StageSplit_Runs} runs after one warm-up");
            stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"Passes {passes}; (c) and (d) write into the throwaway Storage test database: {isWritable}");
            stringBuilder.AppendLine();

            foreach ((string datasetKey, string path) in inputs)
            {
                bool isGhsObat = string.Equals(datasetKey, "ghs_obat", StringComparison.OrdinalIgnoreCase);

                Dictionary<int, IPolygonal2D>? polygonal2Ds_ByCountyId = isGhsObat ? null : counties.Polygonal2DsByCountyId();
                Dictionary<int, CountyFilingGeometry>? countyFilingGeometries_ByCountyId = polygonal2Ds_ByCountyId?.CountyFilingGeometriesByCountyId();
                Dictionary<int, PolygonalFace2D>? polygonalFace2Ds_ByCountyId = isGhsObat ? counties.PolygonalFace2DsById() : null;
                Dictionary<int, BoundingBox2D> boundingBox2Ds_ByCountyId = [];

                if (polygonal2Ds_ByCountyId is not null)
                {
                    foreach (KeyValuePair<int, IPolygonal2D> pair in polygonal2Ds_ByCountyId)
                    {
                        if (pair.Value.GetBoundingBox() is BoundingBox2D boundingBox2D)
                        {
                            boundingBox2Ds_ByCountyId[pair.Key] = boundingBox2D;
                        }
                    }
                }

                if (polygonalFace2Ds_ByCountyId is not null)
                {
                    foreach (KeyValuePair<int, PolygonalFace2D> pair in polygonalFace2Ds_ByCountyId)
                    {
                        if (pair.Value.GetBoundingBox() is BoundingBox2D boundingBox2D)
                        {
                            boundingBox2Ds_ByCountyId[pair.Key] = boundingBox2D;
                        }
                    }
                }

                // (a) read only.
                async Task<long> ReadAsync()
                {
                    long count = 0;
                    await foreach (Footprint footprint in OpenBuildingStageSplit_Footprints(datasetKey, path, maxFootprints))
                    {
                        count++;
                    }

                    return count;
                }

                // (b) read and convert, the way ImportAsync does it: one transformation per source reference system.
                ICoordinateTransformation? transformation_4326 = GIS.Create.EPSG2180TransformationFromEPSG4326();
                ICoordinateTransformation? transformation_3035 = GIS.Create.EPSG2180TransformationFromEPSG3035();

                long kept = 0;
                long filtered = 0;
                long rejected = 0;

                async Task<long> ConvertAsync()
                {
                    long count = 0;
                    kept = 0;
                    filtered = 0;
                    rejected = 0;

                    await foreach (Footprint footprint in OpenBuildingStageSplit_Footprints(datasetKey, path, maxFootprints))
                    {
                        count++;

                        ICoordinateTransformation? coordinateTransformation = footprint.SourceSrid switch
                        {
                            4326 => transformation_4326,
                            3035 => transformation_3035,
                            _ => null,
                        };

                        OpenBuildingRow? openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, polygonalFace2Ds_ByCountyId, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out UpdateRejectionReason? updateRejectionReason);

                        if (openBuildingRow is not null)
                        {
                            kept++;
                        }
                        else if (updateRejectionReason is null)
                        {
                            filtered++;
                        }
                        else
                        {
                            rejected++;
                        }
                    }

                    return count;
                }

                // Passes (a) and (b) time code no import change touches (the reader, CreateRow); a run that
                // compares builds of the import can leave them out with the Passes setting.
                List<double>? seconds_Read = passes.Contains('a') ? await OpenBuildingStageSplit_TimeAsync(ReadAsync) : null;
                List<double>? seconds_Convert = passes.Contains('b') ? await OpenBuildingStageSplit_TimeAsync(ConvertAsync) : null;
                long readCount = await ReadAsync();
                long? kept_Convert = seconds_Convert is null ? null : kept;

                if (seconds_Convert is not null)
                {
                    Assert.Equal(readCount, kept + filtered + rejected);
                }

                List<double>? seconds_Import = null;
                List<double>? seconds_ImportParallel = null;
                if (isWritable && passes.Contains('c'))
                {
                    seconds_Import = await OpenBuildingStageSplit_ImportAsync(connectionData_Storage!, datasetKey, path, maxFootprints, countyIds_Scope, counties, kept_Convert, 1);
                }

                if (isWritable && passes.Contains('d'))
                {
                    seconds_ImportParallel = await OpenBuildingStageSplit_ImportAsync(connectionData_Storage!, datasetKey, path, maxFootprints, countyIds_Scope, counties, kept_Convert, 0);
                }

                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"{datasetKey}: {path}");
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  footprints {readCount}{(seconds_Convert is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $": kept {kept}, out of scope {filtered}, rejected {rejected}"))}");
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  (a) read            {(seconds_Read is null ? "not measured" : OpenBuildingStageSplit_Range(seconds_Read, readCount))}");
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  (b) read + convert  {(seconds_Convert is null ? "not measured" : OpenBuildingStageSplit_Range(seconds_Convert, readCount))}");
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  (c) whole import    {(seconds_Import is null ? "not measured" : OpenBuildingStageSplit_Range(seconds_Import, readCount))}");
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  (d) whole import, degree {Math.Max(1, Core.Query.ProcessorCount(0.5))} (MaxDegreeOfParallelism 0)  {(seconds_ImportParallel is null ? "not measured" : OpenBuildingStageSplit_Range(seconds_ImportParallel, readCount))}");
                if (seconds_Import is not null && seconds_ImportParallel is not null)
                {
                    stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  speed-up (d) over (c), medians: {seconds_Import.OrderBy(x => x).ElementAt(seconds_Import.Count / 2) / Math.Max(seconds_ImportParallel.OrderBy(x => x).ElementAt(seconds_ImportParallel.Count / 2), 1e-9):0.00}x");
                }

                if (seconds_Read is not null && seconds_Convert is not null)
                {
                    stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  shares (median): read {OpenBuildingStageSplit_Share(seconds_Read, null, seconds_Import ?? seconds_Convert)}, convert {OpenBuildingStageSplit_Share(seconds_Convert, seconds_Read, seconds_Import ?? seconds_Convert)}, write {(seconds_Import is null ? "n/a" : OpenBuildingStageSplit_Share(seconds_Import, seconds_Convert, seconds_Import))}");
                }

                stringBuilder.AppendLine();
            }

            // File skipping (option 1): how many files of each whole release folder can reach the scope at all.
            if (settings.TryGetValue("Releases", out string? releases) && !string.IsNullOrWhiteSpace(releases))
            {
                List<BoundingBox2D?> boundingBox2Ds_Scope = [];
                Dictionary<int, IPolygonal2D> polygonal2Ds_All = counties.Polygonal2DsByCountyId();
                foreach (int countyId in countyIds_Scope)
                {
                    boundingBox2Ds_Scope.Add(polygonal2Ds_All.TryGetValue(countyId, out IPolygonal2D? polygonal2D) ? polygonal2D.GetBoundingBox() : null);
                }

                foreach (string release in releases.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    string[] parts = release.Split('|', StringSplitOptions.TrimEntries);
                    if (parts.Length != 3 || !Directory.Exists(parts[1]) || !Enum.TryParse(parts[0], true, out BuildingDataset buildingDataset))
                    {
                        continue;
                    }

                    List<string> paths = [.. Directory.EnumerateFiles(parts[1], parts[2], SearchOption.AllDirectories)];
                    paths.Sort(StringComparer.Ordinal);

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    List<string> paths_Skipped = await Query.SourcePathsOutOfScopeAsync(paths, buildingDataset, boundingBox2Ds_Scope, 1000);

                    stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"File skipping, {buildingDataset} release {parts[1]}: {paths.Count - paths_Skipped.Count} of {paths.Count} files can reach the scope and are read, {paths_Skipped.Count} left unread (decided in {stopwatch.Elapsed.TotalSeconds:0.00} s)");
                }

                stringBuilder.AppendLine();
            }

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
            Assert.NotNull(directory_Reports);
            File.WriteAllText(Path.Combine(directory_Reports, "OpenBuildingImport_StageSplit.txt"), stringBuilder.ToString());
        }

        /// <summary>
        /// Runs a pass once as a warm-up and then <see cref="count_StageSplit_Runs"/> times, returning the elapsed seconds of the timed runs.
        /// </summary>
        /// <param name="func">The pass; its result is the number of footprints it read.</param>
        /// <returns>The elapsed seconds of every timed run.</returns>
        private static async Task<List<double>> OpenBuildingStageSplit_TimeAsync(Func<Task<long>> func)
        {
            await func();

            List<double> result = [];
            for (int i = 0; i < count_StageSplit_Runs; i++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                await func();
                result.Add(stopwatch.Elapsed.TotalSeconds);
            }

            return result;
        }

        /// <summary>
        /// Times the whole import of the capped input into the throwaway Storage database: one warm-up and <see cref="count_StageSplit_Runs"/> timed runs of the same release, each replacing the previous snapshot, then drops the scope's partitions and the register row.
        /// </summary>
        /// <param name="connectionData_Storage">The throwaway Storage test database.</param>
        /// <param name="datasetKey">The dataset key of the input.</param>
        /// <param name="path">The source file.</param>
        /// <param name="maxFootprints">The footprints read per run.</param>
        /// <param name="countyIds_Scope">The scope.</param>
        /// <param name="counties">The county parts.</param>
        /// <param name="kept">The rows pass (b) kept, which every run must store exactly; null when pass (b) did not run.</param>
        /// <param name="maxDegreeOfParallelism">The degree the import converts at; zero is the task's default, half the processors.</param>
        /// <returns>The elapsed seconds of every timed run.</returns>
        private static async Task<List<double>> OpenBuildingStageSplit_ImportAsync(
            ConnectionData connectionData_Storage,
            string datasetKey,
            string path,
            int maxFootprints,
            HashSet<int> countyIds_Scope,
            List<AdministrativeAreal2D> counties,
            long? kept,
            int maxDegreeOfParallelism)
        {
            string version = $"TST-{Guid.NewGuid():N}";
            OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Storage);

            async Task<long> ImportAsync()
            {
                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(
                    OpenBuildingStageSplit_Footprints(datasetKey, path, maxFootprints),
                    new BuildingDatasetRelease(datasetKey, version, DateTime.Today),
                    countyIds_Scope,
                    counties,
                    maxDegreeOfParallelism: maxDegreeOfParallelism,
                    cancellationToken: default);

                Assert.NotNull(openBuildingImportResult);
                if (kept is long kept_Temp)
                {
                    Assert.Equal(kept_Temp, openBuildingImportResult.RowCount);
                }

                return openBuildingImportResult.RowCount;
            }

            try
            {
                return await OpenBuildingStageSplit_TimeAsync(ImportAsync);
            }
            finally
            {
                await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection_Storage);
                await npgsqlConnection_Storage.OpenAsync();

                string? tableName = Query.OpenBuildingTableName(datasetKey);
                List<string> commandTexts = [];

                if (tableName is not null && !string.Equals(tableName, Constants.TableName.GHS_OBAT_Building, StringComparison.Ordinal))
                {
                    foreach (int countyId in countyIds_Scope)
                    {
                        commandTexts.Add($"DROP TABLE IF EXISTS {tableName}_{countyId};");
                    }
                }
                else if (tableName is not null)
                {
                    commandTexts.Add($"DELETE FROM {tableName} WHERE dataset_id IN (SELECT id FROM {Constants.TableName.BuildingDataset} WHERE version = '{version}');");
                }

                commandTexts.Add($"DELETE FROM {Constants.TableName.BuildingDataset} WHERE version = '{version}';");

                await ExecuteAsync(npgsqlConnection_Storage, string.Join(" ", commandTexts));
            }
        }

        /// <summary>
        /// Opens the reader the import task would open for one source file and yields at most <paramref name="maxFootprints"/> footprints.
        /// </summary>
        /// <param name="datasetKey">The dataset key: microsoft, eubucco, dbsm, overture or ghs_obat.</param>
        /// <param name="path">The source file.</param>
        /// <param name="maxFootprints">The most footprints yielded.</param>
        /// <returns>The capped stream.</returns>
        private static async IAsyncEnumerable<Footprint> OpenBuildingStageSplit_Footprints(string datasetKey, string path, int maxFootprints)
        {
            IAsyncEnumerable<Footprint>? footprints = datasetKey.ToLowerInvariant() switch
            {
                "microsoft" => DiGi.GIS.IO.Query.Footprints_GeoJsonSequenceAsync(path, BuildingDataset.Microsoft, DiGi.GIS.IO.Query.Quadkey(path) ?? "0"),
                "eubucco" => DiGi.GIS.IO.Query.Footprints_GeoPackageAsync(path, BuildingDataset.Eubucco),
                "dbsm" => DiGi.GIS.IO.Query.Footprints_GeoPackageAsync(path, BuildingDataset.Dbsm),
                "overture" => DiGi.GIS.IO.Query.Footprints_GeoParquetAsync(path, BuildingDataset.Overture),
                "ghs_obat" => DiGi.GIS.IO.Query.Footprints_GeoPackageAsync(path, BuildingDataset.GhsObat),
                _ => null,
            };

            Assert.NotNull(footprints);

            int count = 0;
            await foreach (Footprint footprint in footprints)
            {
                if (count >= maxFootprints)
                {
                    yield break;
                }

                count++;
                yield return footprint;
            }
        }

        /// <summary>
        /// Formats the range of a pass's timed runs with the footprint rate of its median run.
        /// </summary>
        /// <param name="seconds">The elapsed seconds of the timed runs.</param>
        /// <param name="count">The footprints read per run.</param>
        /// <returns>The formatted range.</returns>
        private static string OpenBuildingStageSplit_Range(List<double> seconds, long count)
        {
            List<double> sorted = [.. seconds.OrderBy(x => x)];
            double median = sorted[sorted.Count / 2];

            return string.Format(CultureInfo.InvariantCulture, "{0:0.00}-{1:0.00} s, median {2:0.00} s = {3:0} footprints/s", sorted[0], sorted[^1], median, count / Math.Max(median, 1e-9));
        }

        /// <summary>
        /// Formats the share of one stage in the median of the whole pass: the stage's median minus the median of the pass it extends.
        /// </summary>
        /// <param name="seconds">The elapsed seconds of the pass that includes the stage.</param>
        /// <param name="seconds_Previous">The elapsed seconds of the pass without the stage, or null for the first stage.</param>
        /// <param name="seconds_Total">The elapsed seconds of the most complete pass.</param>
        /// <returns>The formatted share.</returns>
        private static string OpenBuildingStageSplit_Share(List<double> seconds, List<double>? seconds_Previous, List<double> seconds_Total)
        {
            static double Median(List<double> values)
            {
                List<double> sorted = [.. values.OrderBy(x => x)];
                return sorted[sorted.Count / 2];
            }

            double stage = Median(seconds) - (seconds_Previous is null ? 0 : Median(seconds_Previous));
            return string.Format(CultureInfo.InvariantCulture, "{0:0}%", 100 * stage / Math.Max(Median(seconds_Total), 1e-9));
        }

        /// <summary>
        /// Reads <c>user files/DiGi.GIS.PostgreSQL_OpenBuildingStageSplit.conf</c> (<c>CountiesPath</c>, <c>CountyIds</c>, <c>Inputs</c>, <c>MaxFootprints</c>), or returns null when the file or a key is missing, so the fact returns without measuring anything.
        /// </summary>
        /// <param name="assembly">The test assembly.</param>
        /// <returns>The settings, or null.</returns>
        private static Dictionary<string, string>? OpenBuildingStageSplit_Settings(Assembly assembly)
        {
            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            if (string.IsNullOrWhiteSpace(directory_UserFiles))
            {
                return null;
            }

            string path_Configuration = Path.Combine(directory_UserFiles, fileName_OpenBuildingStageSplit);
            if (!File.Exists(path_Configuration))
            {
                return null;
            }

            Dictionary<string, string> result = [];
            foreach (string line in File.ReadAllLines(path_Configuration))
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                {
                    continue;
                }

                int index = line.IndexOf('=');
                if (index <= 0)
                {
                    continue;
                }

                result[line[..index].Trim()] = line[(index + 1)..].Trim();
            }

            foreach (string key in new string[] { "CountiesPath", "CountyIds", "Inputs", "MaxFootprints" })
            {
                if (!result.ContainsKey(key))
                {
                    return null;
                }
            }

            return result;
        }
    }
}
