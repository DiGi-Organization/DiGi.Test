using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// How many counties the synthetic workload of <see cref="SampledArea_Parallel_Performance"/> measures - the part count of one production batch (DiGi.GIS.WebAPI Constants.Terrain.MaximumDensityCountyCount).
        /// </summary>
        private const int densities_BenchmarkCounties = 50;

        /// <summary>
        /// How many subdivisions each synthetic county carries. The production read meets 10 000-24 000 subdivisions per 50-part batch (200-500 per part); 300 keeps one sequential pass in the sub-second range while staying the same shape.
        /// </summary>
        private const int densities_BenchmarkSubdivisions = 300;

        /// <summary>
        /// The seed of the synthetic subdivision rectangles, so the workload - and therefore every reported figure - repeats exactly (DiGi-Organization/DiGi.GIS.WebAPI#105).
        /// </summary>
        private const int densities_BenchmarkSeed = 20261121;

        /// <summary>
        /// How many timed runs each configuration of <see cref="SampledArea_Parallel_Performance"/> makes; the range over them is what the fact reports.
        /// </summary>
        private const int densities_BenchmarkRepeats = 3;

        /// <summary>
        /// The generous ceiling on the fastest sequential pass, in milliseconds - a regression bound that clears the in-suite time with room to spare, not a speed-up claim.
        /// </summary>
        private const long densities_BenchmarkBudgetMilliseconds = 15000;

        /// <summary>
        /// The degrees of parallelism <see cref="SampledArea_Parallel_Performance"/> sweeps; 1 is the sequential baseline measured separately.
        /// </summary>
        private static readonly int[] densities_BenchmarkDegreesOfParallelism = [2, 4, 8];

        /// <summary>
        /// The degree of parallelism the production loop of <c>GetDensitiesByCountyIdsAsync</c> runs with - the value of the private <c>TerrainController.maximumDegreeOfParallelism</c>, mirrored here because that constant is not reachable from a fact (DiGi-Organization/DiGi.GIS.WebAPI#105).
        /// </summary>
        private const int densities_BenchmarkProductionDegreeOfParallelism = 4;

        /// <summary>
        /// Measures the sampled-area work of one densities read - what the per-part overlay loop of <c>GetDensitiesByCountyIdsAsync</c> computes - sequentially and side by side at several degrees of parallelism (DiGi-Organization/DiGi.GIS.WebAPI#105).
        /// <para>The workload is synthetic and seeded: 50 county outlines tiled by 300 overlapping subdivision rectangles each, plus a coinciding duplicate and a nested subdivision per county, so every branch the production <c>SampledArea</c> takes is exercised. The computation replicates the production helper with the public geometry API (the helper is private and the workspace has no <c>InternalsVisibleTo</c>); production runs exactly these calls per part.</para>
        /// <para>Agreement between the sequential and every parallel pass is asserted before anything is timed, on the full workload and on a six-part slice of it - the smallest batch the UI sends, which must not lose more than 1.5x to the workers' start-up. The timing then reports ranges over three runs per configuration to the reports folder and asserts two things: the sequential pass stays inside its generous budget, and - on a machine with at least four logical processors - the fastest parallel pass beats the sequential fastest by at least 1.25x; on a smaller machine it only has to not lose more than 1.5x. The chosen production constant is documented in <c>TerrainController</c>, and the figure that counts is the live re-measurement against 162.5 s (DiGi.GIS.WebAPI#103), not this machine's.</para>
        /// <para>Measured duration: tens of seconds - <see cref="MediumFactAttribute"/>.</para>
        /// </summary>
        [MediumFact]
        public void SampledArea_Parallel_Performance()
        {
            List<(PolygonalFace2D Outline, Dictionary<int, PolygonalFace2D> Subdivisions)> workload = Densities_BenchmarkWorkload();
            Assert.Equal(densities_BenchmarkCounties, workload.Count);

            // Agreement first: every parallel pass must produce exactly what the sequential pass produces.
            double?[] baseline = Densities_SampledAreas_Sequential(workload);
            Assert.Equal(workload.Count, baseline.Length);
            foreach (double? area in baseline)
            {
                Assert.NotNull(area);
                Assert.True(double.IsFinite(area.Value), "Every synthetic county must bound a finite area.");
            }

            foreach (int degreeOfParallelism in densities_BenchmarkDegreesOfParallelism)
            {
                double?[] results = Densities_SampledAreas_Parallel(workload, degreeOfParallelism);
                for (int i = 0; i < baseline.Length; i++)
                {
                    Assert.Equal(baseline[i], results[i]);
                }
            }

            // The smallest batch the UI sends is six parts (the 406 parts end in one of six), and at that size the
            // workers' start-up must not cost more than the work they save. DOP 4 mirrors the production constant
            // TerrainController.maximumDegreeOfParallelism, which is private and swept in the timings below.
            List<(PolygonalFace2D Outline, Dictionary<int, PolygonalFace2D> Subdivisions)> workload_Small = [.. workload.Take(6)];
            double?[] baseline_Small = Densities_SampledAreas_Sequential(workload_Small);
            double?[] results_Small = Densities_SampledAreas_Parallel(workload_Small, densities_BenchmarkProductionDegreeOfParallelism);
            for (int i = 0; i < baseline_Small.Length; i++)
            {
                Assert.Equal(baseline_Small[i], results_Small[i]);
            }

            // Warm-up: JIT and any first-run geometry state, outside every timed run.
            _ = Densities_SampledAreas_Sequential(workload);
            foreach (int degreeOfParallelism in densities_BenchmarkDegreesOfParallelism)
            {
                _ = Densities_SampledAreas_Parallel(workload, degreeOfParallelism);
            }

            long[] sequential = Densities_Measure(densities_BenchmarkRepeats, () => _ = Densities_SampledAreas_Sequential(workload));
            Dictionary<int, long[]> parallel = [];
            foreach (int degreeOfParallelism in densities_BenchmarkDegreesOfParallelism)
            {
                parallel[degreeOfParallelism] = Densities_Measure(densities_BenchmarkRepeats, () => _ = Densities_SampledAreas_Parallel(workload, degreeOfParallelism));
            }

            // The same sweep on the six parts the last batch of a production read carries.
            long[] sequential_Small = Densities_Measure(densities_BenchmarkRepeats, () => _ = Densities_SampledAreas_Sequential(workload_Small));
            long[] parallel_Small = Densities_Measure(densities_BenchmarkRepeats, () => _ = Densities_SampledAreas_Parallel(workload_Small, densities_BenchmarkProductionDegreeOfParallelism));

            long sequential_Fastest = sequential.Min();
            long parallel_Fastest = parallel.Values.Select(x => x.Min()).Min();

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "SampledArea_Parallel_Performance - {0} counties x {1} subdivisions, seed {2}, ProcessorCount {3}", workload.Count, densities_BenchmarkSubdivisions, densities_BenchmarkSeed, Environment.ProcessorCount));
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "sequential (DOP 1): {0} range over {1} runs", Densities_FormatRange(sequential), densities_BenchmarkRepeats));
            foreach (int degreeOfParallelism in densities_BenchmarkDegreesOfParallelism)
            {
                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "parallel DOP {0}: {1} range over {2} runs", degreeOfParallelism, Densities_FormatRange(parallel[degreeOfParallelism]), densities_BenchmarkRepeats));
            }

            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "fastest parallel vs fastest sequential: {0:F2}x", sequential_Fastest / (double)parallel_Fastest));
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "6-part batch: sequential {0}, parallel DOP {1}: {2}", Densities_FormatRange(sequential_Small), densities_BenchmarkProductionDegreeOfParallelism, Densities_FormatRange(parallel_Small)));

            string? directory = DiGi.Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            if (!string.IsNullOrWhiteSpace(directory))
            {
                File.WriteAllText(Path.Combine(directory, "SampledArea_Parallel_Performance.txt"), stringBuilder.ToString(), Encoding.UTF8);
            }

            Assert.True(sequential_Fastest < densities_BenchmarkBudgetMilliseconds, string.Format(CultureInfo.InvariantCulture, "The sequential pass took {0} ms, over the {1} ms budget. {2}", sequential_Fastest, densities_BenchmarkBudgetMilliseconds, stringBuilder));

            if (Environment.ProcessorCount >= 4)
            {
                Assert.True(sequential_Fastest >= parallel_Fastest * 1.25, string.Format(CultureInfo.InvariantCulture, "The fastest parallel pass must beat the sequential one by 1.25x. {0}", stringBuilder));
            }
            else
            {
                Assert.True(parallel_Fastest <= sequential_Fastest * 1.5, string.Format(CultureInfo.InvariantCulture, "On fewer than four logical processors parallelism may cost, but not 1.5x. {0}", stringBuilder));
            }

            // Six parts: the workers' start-up must not cost more than the work they save, or the smallest batch a
            // production read sends comes out slower than the sequential loop it replaced.
            Assert.True(parallel_Small.Min() <= sequential_Small.Min() * 1.5, string.Format(CultureInfo.InvariantCulture, "A six-part batch must not lose more than 1.5x to the workers' start-up. {0}", stringBuilder));
        }

        /// <summary>
        /// Builds the seeded synthetic workload of <see cref="SampledArea_Parallel_Performance"/>: one outline and its subdivisions per county.
        /// <para>Each county is a 1000 m square. Subdivision 0 covers most of it, subdivision 1 nests inside 0 (so the container query drops it from the union), a coinciding duplicate of 0 stands for a district and its part of a city, and the rest overlap each other at random - every branch the production overlay takes.</para>
        /// </summary>
        /// <returns>One entry per county: its outline and its subdivisions keyed by identifier.</returns>
        private static List<(PolygonalFace2D Outline, Dictionary<int, PolygonalFace2D> Subdivisions)> Densities_BenchmarkWorkload()
        {
            List<(PolygonalFace2D Outline, Dictionary<int, PolygonalFace2D> Subdivisions)> result = [];

            Random random = new(densities_BenchmarkSeed);
            for (int countyIndex = 0; countyIndex < densities_BenchmarkCounties; countyIndex++)
            {
                double originX = countyIndex * 4000;

                PolygonalFace2D? polygonalFace2D_Outline = Densities_BenchmarkRectangle(originX, 0, 1000, 1000);
                Assert.NotNull(polygonalFace2D_Outline);

                Dictionary<int, PolygonalFace2D> polygonalFace2Ds_ById = [];

                PolygonalFace2D? polygonalFace2D_Large = Densities_BenchmarkRectangle(originX + 50, 50, 900, 900);
                Assert.NotNull(polygonalFace2D_Large);
                polygonalFace2Ds_ById[0] = polygonalFace2D_Large;

                PolygonalFace2D? polygonalFace2D_Nested = Densities_BenchmarkRectangle(originX + 150, 150, 200, 200);
                Assert.NotNull(polygonalFace2D_Nested);
                polygonalFace2Ds_ById[1] = polygonalFace2D_Nested;

                PolygonalFace2D? polygonalFace2D_Coinciding = new(polygonalFace2D_Large);
                polygonalFace2Ds_ById[2] = polygonalFace2D_Coinciding;

                for (int i = 3; i < densities_BenchmarkSubdivisions; i++)
                {
                    double width = 50 + random.Next(200);
                    double height = 50 + random.Next(200);
                    double x = originX + random.Next(750);
                    double y = random.Next(750);

                    PolygonalFace2D? polygonalFace2D = Densities_BenchmarkRectangle(x, y, width, height);
                    Assert.NotNull(polygonalFace2D);
                    polygonalFace2Ds_ById[i] = polygonalFace2D;
                }

                result.Add((polygonalFace2D_Outline, polygonalFace2Ds_ById));
            }

            return result;
        }

        /// <summary>
        /// Creates an axis-aligned rectangular face, the shape every synthetic subdivision and outline is built from.
        /// </summary>
        /// <param name="x">The X coordinate of the lower left corner.</param>
        /// <param name="y">The Y coordinate of the lower left corner.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        /// <returns>The face, or null when the polygon could not be created.</returns>
        private static PolygonalFace2D? Densities_BenchmarkRectangle(double x, double y, double width, double height)
        {
            Polygon2D polygon2D = new([new Point2D(x, y), new Point2D(x + width, y), new Point2D(x + width, y + height), new Point2D(x, y + height)]);
            return Geometry.Planar.Create.PolygonalFace2D(polygon2D);
        }

        /// <summary>
        /// Measures every county of the workload sequentially - the baseline of <see cref="SampledArea_Parallel_Performance"/>.
        /// <para>This is the production <c>SampledArea</c> body against the public geometry API: holes of the outline excluded, only the top-level subdivisions unioned, the union subtracted, never below zero.</para>
        /// </summary>
        /// <param name="workload">The counties to measure.</param>
        /// <returns>One sampled area per county, in workload order.</returns>
        private static double?[] Densities_SampledAreas_Sequential(List<(PolygonalFace2D Outline, Dictionary<int, PolygonalFace2D> Subdivisions)> workload)
        {
            double?[] result = new double?[workload.Count];
            for (int i = 0; i < workload.Count; i++)
            {
                result[i] = Densities_SampledArea_Reference(workload[i].Outline, workload[i].Subdivisions);
            }

            return result;
        }

        /// <summary>
        /// Measures every county of the workload side by side, one county per worker slot - the parallel path of <see cref="SampledArea_Parallel_Performance"/>.
        /// </summary>
        /// <param name="workload">The counties to measure.</param>
        /// <param name="degreeOfParallelism">The largest number of counties measured at the same time.</param>
        /// <returns>One sampled area per county, in workload order.</returns>
        private static double?[] Densities_SampledAreas_Parallel(List<(PolygonalFace2D Outline, Dictionary<int, PolygonalFace2D> Subdivisions)> workload, int degreeOfParallelism)
        {
            double?[] result = new double?[workload.Count];
            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = degreeOfParallelism };
            Parallel.For(0, workload.Count, parallelOptions, (index, parallelLoopState) =>
            {
                result[index] = Densities_SampledArea_Reference(workload[index].Outline, workload[index].Subdivisions);
            });

            return result;
        }

        /// <summary>
        /// The reference sampled area: the outline as far as its top-level subdivisions cover it.
        /// <para>A replica of <c>TerrainController.SampledArea</c> built on the public geometry API, because that helper is private and the workspace has no <c>InternalsVisibleTo</c> - production runs exactly these calls per part.</para>
        /// </summary>
        /// <param name="polygonalFace2D">The outline of the county part.</param>
        /// <param name="polygonalFace2Ds_ById">The outlines of the county's subdivisions keyed by identifier.</param>
        /// <returns>The sampled area, never below zero; not a number when the outline bounds no area; null when the outline could not be overlaid.</returns>
        private static double? Densities_SampledArea_Reference(PolygonalFace2D polygonalFace2D, Dictionary<int, PolygonalFace2D> polygonalFace2Ds_ById)
        {
            double area = polygonalFace2D.GetArea();
            if (double.IsNaN(area) || double.IsInfinity(area) || area <= 0)
            {
                return double.NaN;
            }

            if (polygonalFace2Ds_ById is null || polygonalFace2Ds_ById.Count == 0)
            {
                return area;
            }

            Dictionary<int, List<int>> containerIds_ById = PostgreSQL.Query.ContainerIds(polygonalFace2Ds_ById);
            List<PolygonalFace2D> polygonalFace2Ds_Subdivision = [];
            foreach (KeyValuePair<int, PolygonalFace2D> keyValuePair in polygonalFace2Ds_ById)
            {
                if (!containerIds_ById.TryGetValue(keyValuePair.Key, out List<int>? containerIds) || containerIds.Count == 0)
                {
                    polygonalFace2Ds_Subdivision.Add(keyValuePair.Value);
                }
            }

            List<PolygonalFace2D>? polygonalFace2Ds_Unsampled = Geometry.Planar.Query.Difference(polygonalFace2D, polygonalFace2Ds_Subdivision);
            if (polygonalFace2Ds_Unsampled is null)
            {
                return null;
            }

            foreach (PolygonalFace2D polygonalFace2D_Unsampled in polygonalFace2Ds_Unsampled)
            {
                double area_Unsampled = polygonalFace2D_Unsampled.GetArea();
                if (!double.IsNaN(area_Unsampled) && !double.IsInfinity(area_Unsampled))
                {
                    area -= area_Unsampled;
                }
            }

            return area < 0 ? 0 : area;
        }

        /// <summary>
        /// Runs one action the given number of times, timing each run.
        /// </summary>
        /// <param name="repeats">How many timed runs to make.</param>
        /// <param name="action">The action to time.</param>
        /// <returns>The elapsed milliseconds of each run, in order.</returns>
        private static long[] Densities_Measure(int repeats, Action action)
        {
            long[] result = new long[repeats];
            for (int i = 0; i < repeats; i++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                action();
                stopwatch.Stop();
                result[i] = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// Formats a range of timed runs as "min-max ms" for the fact's report.
        /// </summary>
        /// <param name="values">The elapsed milliseconds of the runs.</param>
        /// <returns>The formatted range.</returns>
        private static string Densities_FormatRange(long[] values)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1} ms", values.Min(), values.Max());
        }
    }
}
