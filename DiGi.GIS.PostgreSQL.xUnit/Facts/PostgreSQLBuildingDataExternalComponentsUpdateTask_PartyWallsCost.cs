using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Measures what the party walls option adds to the compute cost of an external components area batch, on stored building models read through api.digiproject.uk, and writes the times to a report (DiGi-Organization/DiGi.GIS.PostgreSQL#153).
        /// <para>The models are the production ones, saved by hand from <c>GET gis/buildingmodel/itemsbycircle</c> into JSON files; DIGI_PARTY_WALLS_COST_FILES lists the files separated by semicolons and the fact is skipped without it. A circle gives a compact cluster, which is what a batch ordered along a Z-order curve looks like, so the models of a file are both the examined and the touching set.</para>
        /// <para><b>It measures compute only.</b> The classification (<c>Update_ExternalComponentsArea</c> without party walls) is timed against the shared wall measurement (<c>Query.PartyWalls</c>) over the same models. The database reads of the task - the models of a batch, and with party walls also the outlines and the models of the neighbours - are not in either figure, so the ratio is a lower bound for what the box saves. Nothing is written anywhere.</para>
        /// <para>One warm-up call each, then three timed calls; the report lists the range of the three and the ratio is taken on the fastest of each, which is the stable figure.</para>
        /// <para>Then the parallel batches of the task (DiGi-Organization/DiGi.GIS.PostgreSQL#154): eight batches, each its own copy of the file's models, are measured for party walls and classified with 1, 2, 4 and 8 in flight. The report gives the range of three runs, the speed-up of the fastest over the fastest with one in flight, and the peak working set of the process after the degree ran (a process-wide high-water mark, so it never goes down from one degree to the next). Compute only, as above: the overlap of database time the task also gains is not in it. Run it in Release, alone.</para>
        /// </summary>
        [MediumSkippableFact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateTask_PartyWallsCost()
        {
            string? files = Environment.GetEnvironmentVariable("DIGI_PARTY_WALLS_COST_FILES");
            Skip.If(string.IsNullOrWhiteSpace(files), "Set DIGI_PARTY_WALLS_COST_FILES to the JSON files of stored building models saved from gis/buildingmodel/itemsbycircle, separated by semicolons.");

            List<string> lines = ["Production building models read through api.digiproject.uk, timed in memory; no database read, nothing written."];

            foreach (string path in files!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                Assert.True(File.Exists(path), path);

                List<DiGi.Analytical.Building.Classes.BuildingModel>? buildingModels = Core.Convert.ToDiGi<DiGi.Analytical.Building.Classes.BuildingModel>(File.ReadAllText(path));
                Assert.NotNull(buildingModels);
                Assert.NotEmpty(buildingModels);

                List<BuildingModel> buildingModels_PostgreSQL = [];
                foreach (DiGi.Analytical.Building.Classes.BuildingModel buildingModel in buildingModels)
                {
                    BuildingModel? buildingModel_PostgreSQL = buildingModel.ToPostgreSQL(1);
                    if (buildingModel_PostgreSQL is not null && !string.IsNullOrWhiteSpace(buildingModel_PostgreSQL.Reference))
                    {
                        buildingModels_PostgreSQL.Add(buildingModel_PostgreSQL);
                    }
                }

                Assert.NotEmpty(buildingModels_PostgreSQL);

                double[] milliseconds_Classification = new double[3];
                double[] milliseconds_PartyWalls = new double[3];
                int partyWallBuildingCount = 0;
                int degenerateCount = 0;

                // Warm-up: JIT, and the envelope construction paths.
                Modify.Update_ExternalComponentsArea(new Table(), buildingModels_PostgreSQL);
                DiGi.Analytical.Building.Query.PartyWalls(buildingModels, buildingModels);

                for (int i = 0; i < 3; i++)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    ExternalComponentsAreaResult? externalComponentsAreaResult = Modify.Update_ExternalComponentsArea(new Table(), buildingModels_PostgreSQL);
                    stopwatch.Stop();
                    milliseconds_Classification[i] = stopwatch.Elapsed.TotalMilliseconds;
                    degenerateCount = externalComponentsAreaResult?.DegenerateReferences?.Count ?? 0;

                    stopwatch.Restart();
                    Dictionary<Guid, List<DiGi.Analytical.Building.Classes.PartyWallResult>>? partyWallResults = DiGi.Analytical.Building.Query.PartyWalls(buildingModels, buildingModels);
                    stopwatch.Stop();
                    milliseconds_PartyWalls[i] = stopwatch.Elapsed.TotalMilliseconds;
                    partyWallBuildingCount = partyWallResults?.Count ?? 0;
                }

                double classification = Min(milliseconds_Classification);
                double partyWalls = Min(milliseconds_PartyWalls);

                lines.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1} models ({2} degenerate, {3} with party walls); classification {4:F0}-{5:F0} ms ({6:F1} ms/model); party walls {7:F0}-{8:F0} ms ({9:F1} ms/model); with party walls the compute is {10:F2}x the classification alone", Path.GetFileName(path), buildingModels_PostgreSQL.Count, degenerateCount, partyWallBuildingCount, classification, Max(milliseconds_Classification), classification / buildingModels_PostgreSQL.Count, partyWalls, Max(milliseconds_PartyWalls), partyWalls / buildingModels_PostgreSQL.Count, (classification + partyWalls) / classification));

                // The batches of a parallel run: each holds its own models, as each batch of the task reads its own.
                const int batchCount = 8;
                List<List<BuildingModel>> buildingModels_Batches = [];
                List<List<DiGi.Analytical.Building.Classes.BuildingModel>> buildingModels_Analytical_Batches = [];
                for (int i = 0; i < batchCount; i++)
                {
                    List<BuildingModel> buildingModels_Batch = [];
                    List<DiGi.Analytical.Building.Classes.BuildingModel> buildingModels_Analytical_Batch = [];
                    foreach (BuildingModel buildingModel_PostgreSQL in buildingModels_PostgreSQL)
                    {
                        DiGi.Analytical.Building.Classes.BuildingModel? buildingModel_Analytical = buildingModel_PostgreSQL.ToDiGi();
                        BuildingModel? buildingModel_Copy = buildingModel_Analytical?.ToPostgreSQL(1);
                        Assert.NotNull(buildingModel_Analytical);
                        Assert.NotNull(buildingModel_Copy);

                        buildingModels_Analytical_Batch.Add(buildingModel_Analytical);
                        buildingModels_Batch.Add(buildingModel_Copy);
                    }

                    buildingModels_Batches.Add(buildingModels_Batch);
                    buildingModels_Analytical_Batches.Add(buildingModels_Analytical_Batch);
                }

                double milliseconds_One = double.NaN;
                foreach (int degreeOfParallelism in new int[] { 1, 2, 4, 8 })
                {
                    double[] milliseconds_Degree = new double[3];
                    for (int i = 0; i < 3; i++)
                    {
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        System.Threading.Tasks.Parallel.For(0, batchCount, new System.Threading.Tasks.ParallelOptions() { MaxDegreeOfParallelism = degreeOfParallelism }, j =>
                        {
                            List<DiGi.Analytical.Building.Classes.PartyWallResult> partyWallResults_Batch = [];
                            Dictionary<Guid, List<DiGi.Analytical.Building.Classes.PartyWallResult>>? partyWallResults_ByGuid = DiGi.Analytical.Building.Query.PartyWalls(buildingModels_Analytical_Batches[j], buildingModels_Analytical_Batches[j]);
                            if (partyWallResults_ByGuid is not null)
                            {
                                foreach (List<DiGi.Analytical.Building.Classes.PartyWallResult> partyWallResults_Model in partyWallResults_ByGuid.Values)
                                {
                                    partyWallResults_Batch.AddRange(partyWallResults_Model);
                                }
                            }

                            Modify.Update_ExternalComponentsArea(new Table(), buildingModels_Batches[j], partyWallResults_Batch);
                        });
                        stopwatch.Stop();
                        milliseconds_Degree[i] = stopwatch.Elapsed.TotalMilliseconds;
                    }

                    double fastest = Min(milliseconds_Degree);
                    if (degreeOfParallelism == 1)
                    {
                        milliseconds_One = fastest;
                    }

                    using Process process = Process.GetCurrentProcess();
                    process.Refresh();

                    lines.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1} batches of {2} models, {3} in flight - {4:F0}-{5:F0} ms, speed-up {6:F2}x, peak working set {7:F0} MB", Path.GetFileName(path), batchCount, buildingModels_PostgreSQL.Count, degreeOfParallelism, fastest, Max(milliseconds_Degree), milliseconds_One / fastest, process.PeakWorkingSet64 / (1024.0 * 1024.0)));
                }
            }

            string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory));
            Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, "PartyWallsCost.txt"), lines);

            foreach (string line in lines)
            {
                Console.WriteLine(line);
            }
        }

        private static double Min(double[] values)
        {
            double result = double.MaxValue;
            foreach (double value in values)
            {
                result = Math.Min(result, value);
            }

            return result;
        }

        private static double Max(double[] values)
        {
            double result = double.MinValue;
            foreach (double value in values)
            {
                result = Math.Max(result, value);
            }

            return result;
        }
    }
}
