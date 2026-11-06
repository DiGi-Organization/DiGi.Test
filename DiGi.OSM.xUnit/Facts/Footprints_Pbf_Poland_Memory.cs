using DiGi.OSM.Classes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace DiGi.OSM.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Runs the reader over a whole dated Geofabrik Poland extract and asserts the memory bound of the bounded node cache: the process peak working set must stay within the documented budget of 3 300 000 000 bytes, and the highest managed reading sampled during the enumeration, the referenced-node count and the per-node figure are written to <c>user files/reports/Footprints_Pbf_Poland_Memory.txt</c> together with the run's counts and elapsed time.
        /// <para>Measured 2026-11-06 on <c>poland-261104.osm.pbf</c> (2 115 837 171 bytes, MD5 verified): peak working set 2 989 649 920, 2 990 034 944 and 2 989 744 128 bytes over three consecutive runs, sampled managed memory about 2 422 000 000 bytes, 97 722 280 referenced nodes (sixteen bytes each of store), 17 820 957 footprints in 312-337 s. The store scales with the nodes the buildings reference, never with the extract - the reference implementation's unbounded 10 M-node cache alone reached 3.4 GB, which extrapolates to tens of gigabytes at full coverage. The budget carries roughly 10 % margin over the measurement.</para>
        /// <para>Guarded machine-specific input: the fact returns without asserting when <c>user files/DiGi.OSM.xUnit.conf</c> is absent or names no existing file (the extract lives outside the workspace), so the suite runs everywhere.</para>
        /// <para>Long test (measured 5-6 min): runs when DIGI_TEST_MAX_DURATION is Long.</para>
        /// </summary>
        [LongFact]
        public void Footprints_Pbf_Poland_Memory()
        {
            string? userFilesDirectory = Core.xUnit.Query.UserFilesDirectory(Assembly.GetExecutingAssembly());
            if (userFilesDirectory == null)
            {
                return;
            }

            string confPath = Path.Combine(userFilesDirectory, "DiGi.OSM.xUnit.conf");
            if (!File.Exists(confPath))
            {
                return;
            }

            string? extractPath = null;
            foreach (string line in File.ReadAllLines(confPath))
            {
                string line_Temp = line.Trim();
                if (line_Temp.Length == 0 || line_Temp.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                extractPath = line_Temp;
                break;
            }

            if (extractPath == null || !File.Exists(extractPath))
            {
                return;
            }

            BuildingReadResult? result = Query.Footprints_Pbf(extractPath);
            Assert.NotNull(result);

            Stopwatch stopwatch = Stopwatch.StartNew();
            long maxManaged = 0;
            long footprintCount = 0;

            IEnumerable<DiGi.GIS.IO.Classes.Footprint>? footprints = result!.Footprints;
            if (footprints != null)
            {
                foreach (DiGi.GIS.IO.Classes.Footprint footprint in footprints)
                {
                    _ = footprint;
                    footprintCount++;
                    if (footprintCount == 1 || footprintCount % 250000 == 0)
                    {
                        long managed = GC.GetTotalMemory(false);
                        if (managed > maxManaged)
                        {
                            maxManaged = managed;
                        }
                    }
                }
            }

            stopwatch.Stop();

            Classes.BuildingReadSummary summary = result.Summary;
            using Process process = Process.GetCurrentProcess();
            long peakWorkingSet = process.PeakWorkingSet64;

            string? reportsDirectory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(reportsDirectory);

            string reportPath = Path.Combine(reportsDirectory!, "Footprints_Pbf_Poland_Memory.txt");
            List<string> lines =
            [
                string.Format(CultureInfo.InvariantCulture, "File: {0}", extractPath),
                string.Format(CultureInfo.InvariantCulture, "SizeBytes: {0}", new FileInfo(extractPath).Length),
                string.Format(CultureInfo.InvariantCulture, "ElapsedSeconds: {0:F1}", stopwatch.Elapsed.TotalSeconds),
                string.Format(CultureInfo.InvariantCulture, "Footprints: {0}", summary.FootprintCount),
                string.Format(CultureInfo.InvariantCulture, "WayCount: {0}", summary.WayCount),
                string.Format(CultureInfo.InvariantCulture, "RelationCount: {0}", summary.RelationCount),
                string.Format(CultureInfo.InvariantCulture, "RejectionCount: {0}", summary.RejectionCount),
                string.Format(CultureInfo.InvariantCulture, "ReferencedNodeCount: {0}", summary.ReferencedNodeCount),
                string.Format(CultureInfo.InvariantCulture, "MaxManagedBytes_Sampled: {0}", maxManaged),
                string.Format(CultureInfo.InvariantCulture, "ManagedBytesPerReferencedNode_Sampled: {0:F1}", summary.ReferencedNodeCount == 0 ? 0 : (double)maxManaged / summary.ReferencedNodeCount),
                string.Format(CultureInfo.InvariantCulture, "PeakWorkingSetBytes: {0}", peakWorkingSet)
            ];
            File.WriteAllLines(reportPath, lines);

            Assert.True(summary.FootprintCount > 0, string.Format(CultureInfo.InvariantCulture, "No footprints were read from {0}.", extractPath));
            Assert.True(peakWorkingSet <= 3300000000L, string.Format(CultureInfo.InvariantCulture, "Peak working set {0} bytes exceeds the documented 3.3 GB budget; see {1}.", peakWorkingSet, reportPath));
        }
    }
}
