using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.GIS.IO.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using GeoAPI.CoordinateSystems.Transformations;
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
        /// Verifies over a real stream that the open building import files every footprint exactly as before once the county rings are indexed per run: <see cref="OpenBuildingPostgreSQLConverter.CreateRow"/> with <see cref="Query.CountyFilingGeometriesByCountyId"/> against the same call without them, footprint by footprint - the same county part, or the same filtered or rejected outcome with the same reason.
        /// <para>The inputs are those of <see cref="OpenBuildingImport_StageSplit"/> (<c>user files/DiGi.GIS.PostgreSQL_OpenBuildingStageSplit.conf</c>: the exported county parts, the scope and <c>MaxFootprints</c> footprints of each input); without the file, or with an input missing, the fact returns without asserting. Both sides convert in parallel with one transformation pair per worker - the legacy side because it is slow, the indexed side because that is how the import's workers read the indexes. The report (<c>OpenBuildingImport_FilingParity.txt</c>) gives the outcome counts and the time of each side.</para>
        /// <para>Long test (minutes per input). Run in isolation in Release: <c>dotnet test -c Release --filter "FullyQualifiedName~OpenBuildingImport_FilingParity"</c>.</para>
        /// </summary>
        [LongFact]
        public async Task OpenBuildingImport_FilingParity()
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

            HashSet<int> countyIds_Scope = [.. settings["CountyIds"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => int.Parse(x, CultureInfo.InvariantCulture))];
            int maxFootprints = int.Parse(settings["MaxFootprints"], CultureInfo.InvariantCulture);

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

            Dictionary<int, IPolygonal2D> polygonal2Ds_ByCountyId = counties.Polygonal2DsByCountyId();

            Stopwatch stopwatch_Index = Stopwatch.StartNew();
            Dictionary<int, CountyFilingGeometry> countyFilingGeometries_ByCountyId = polygonal2Ds_ByCountyId.CountyFilingGeometriesByCountyId();
            double seconds_Index = stopwatch_Index.Elapsed.TotalSeconds;

            Dictionary<int, BoundingBox2D> boundingBox2Ds_ByCountyId = [];
            foreach (KeyValuePair<int, IPolygonal2D> pair in polygonal2Ds_ByCountyId)
            {
                if (pair.Value.GetBoundingBox() is BoundingBox2D boundingBox2D)
                {
                    boundingBox2Ds_ByCountyId[pair.Key] = boundingBox2D;
                }
            }

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"OpenBuildingImport_FilingParity - {DateTime.Now:yyyy-MM-dd HH:mm}");
            stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"ProcessorCount {Environment.ProcessorCount}, {counties.Count} county parts ({polygonal2Ds_ByCountyId.Count} rings, {countyFilingGeometries_ByCountyId.Count} indexed in {seconds_Index:0.00} s), scope {countyIds_Scope.Count} parts, {maxFootprints} footprints per input");
            stringBuilder.AppendLine();

            foreach ((string datasetKey, string path) in inputs)
            {
                if (string.Equals(datasetKey, "ghs_obat", StringComparison.OrdinalIgnoreCase))
                {
                    // Centroids file against the faces, a path the indexes do not touch.
                    continue;
                }

                List<Footprint> footprints = [];
                await foreach (Footprint footprint in OpenBuildingStageSplit_Footprints(datasetKey, path, maxFootprints))
                {
                    footprints.Add(footprint);
                }

                Assert.NotEmpty(footprints);

                (double seconds_Legacy, (int? CountyId, UpdateRejectionReason? UpdateRejectionReason)[] outcomes_Legacy) = FilingParity_Outcomes(footprints, polygonal2Ds_ByCountyId, null, boundingBox2Ds_ByCountyId, countyIds_Scope);
                (double seconds_Indexed, (int? CountyId, UpdateRejectionReason? UpdateRejectionReason)[] outcomes_Indexed) = FilingParity_Outcomes(footprints, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, countyIds_Scope);

                List<int> mismatches = [];
                for (int i = 0; i < footprints.Count; i++)
                {
                    if (outcomes_Legacy[i] != outcomes_Indexed[i])
                    {
                        mismatches.Add(i);
                    }
                }

                int filed = outcomes_Legacy.Count(x => x.CountyId is not null);
                int filtered = outcomes_Legacy.Count(x => x.CountyId is null && x.UpdateRejectionReason is null);
                int rejected = outcomes_Legacy.Count(x => x.CountyId is null && x.UpdateRejectionReason is not null);

                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"{datasetKey}: {path}");
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  footprints {footprints.Count}: filed {filed} in {outcomes_Legacy.Where(x => x.CountyId is not null).Select(x => x.CountyId).Distinct().Count()} parts, out of scope {filtered}, rejected {rejected}");
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  legacy (rings)    {seconds_Legacy:0.00} s, indexed {seconds_Indexed:0.00} s, degree {Environment.ProcessorCount}");
                stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"  mismatches {mismatches.Count}");
                foreach (int index in mismatches.Take(20))
                {
                    stringBuilder.AppendLine(CultureInfo.InvariantCulture, $"    #{index} {footprints[index].SourceId}: legacy {outcomes_Legacy[index]}, indexed {outcomes_Indexed[index]}");
                }

                stringBuilder.AppendLine();

                string? directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
                Assert.NotNull(directory_Reports);
                File.WriteAllText(Path.Combine(directory_Reports, "OpenBuildingImport_FilingParity.txt"), stringBuilder.ToString());

                Assert.Empty(mismatches);
                Assert.True(filed > 0);
            }
        }

        /// <summary>
        /// Files every footprint through <see cref="OpenBuildingPostgreSQLConverter.CreateRow"/> in parallel, with one transformation pair per worker, and returns the outcome of each with the elapsed time.
        /// </summary>
        /// <param name="footprints">The footprints, in their source reference system.</param>
        /// <param name="polygonal2Ds_ByCountyId">The county rings.</param>
        /// <param name="countyFilingGeometries_ByCountyId">The county indexes, or null for the legacy filing against the rings alone.</param>
        /// <param name="boundingBox2Ds_ByCountyId">The county envelopes.</param>
        /// <param name="countyIds_Scope">The scope.</param>
        /// <returns>The elapsed seconds and, index-aligned with the footprints, the county part filed under (null when filtered or rejected) and the rejection reason.</returns>
        private static (double Seconds, (int? CountyId, UpdateRejectionReason? UpdateRejectionReason)[] Outcomes) FilingParity_Outcomes(List<Footprint> footprints, Dictionary<int, IPolygonal2D> polygonal2Ds_ByCountyId, Dictionary<int, CountyFilingGeometry>? countyFilingGeometries_ByCountyId, Dictionary<int, BoundingBox2D> boundingBox2Ds_ByCountyId, HashSet<int> countyIds_Scope)
        {
            (int? CountyId, UpdateRejectionReason? UpdateRejectionReason)[] outcomes = new (int?, UpdateRejectionReason?)[footprints.Count];

            Stopwatch stopwatch = Stopwatch.StartNew();

            // The thread safety of a ProjNet transformation is not established, so each worker builds its own pair.
            Parallel.For(
                0,
                footprints.Count,
                () => (GIS.Create.EPSG2180TransformationFromEPSG4326(), GIS.Create.EPSG2180TransformationFromEPSG3035()),
                (i, _, transformations) =>
                {
                    Footprint footprint = footprints[i];
                    ICoordinateTransformation? coordinateTransformation = footprint.SourceSrid switch
                    {
                        4326 => transformations.Item1,
                        3035 => transformations.Item2,
                        _ => null,
                    };

                    OpenBuildingRow? openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out UpdateRejectionReason? updateRejectionReason);
                    outcomes[i] = (openBuildingRow?.CountyId, updateRejectionReason);

                    return transformations;
                },
                _ => { });

            return (stopwatch.Elapsed.TotalSeconds, outcomes);
        }
    }
}
