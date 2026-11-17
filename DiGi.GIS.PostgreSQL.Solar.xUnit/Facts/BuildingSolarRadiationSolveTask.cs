using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Solar.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingModel = DiGi.Analytical.Building.Classes.BuildingModel;

namespace DiGi.GIS.PostgreSQL.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the run of <see cref="BuildingSolarRadiationSolveTask"/> over an in-memory transport, the contract every transport - the storage database or the GIS Web API (DiGi.GIS.PostgreSQL.UI#48) - is held to.
        /// <para>Two county parts at radii 0 and 50 m. Part 1 holds R0 and R1, both solvable, and M, a centroid without a stored model; R1 already carries a result at radius 0. Part 2 cannot have its centroids read. Expected: R1 at 0 m is skipped, M is not solvable at both radii, the three remaining jobs are solved and handed to the write of part 1 in job order (R0 0, R0 50, R1 50), every result filed under part 1; part 2 is failed with a sample and the run reports not succeeded while still counting part 1. The neighbours of the batch are read once.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingSolarRadiationSolveTask_Run()
        {
            MemoryBuildingSolarRadiationSolveTask memoryBuildingSolarRadiationSolveTask = new(SolarFixture_EPWFile());
            memoryBuildingSolarRadiationSolveTask.BuildingModels.Add(SolarFixture_Box("R0", 0, 0, 10, 10, 10));
            memoryBuildingSolarRadiationSolveTask.BuildingModels.Add(SolarFixture_Box("R1", 20, 0, 10, 10, 15));
            memoryBuildingSolarRadiationSolveTask.Building2DCentroids.Add(SolarFixture_Centroid("R0", 5, 5));
            memoryBuildingSolarRadiationSolveTask.Building2DCentroids.Add(SolarFixture_Centroid("R1", 25, 5));
            memoryBuildingSolarRadiationSolveTask.Building2DCentroids.Add(SolarFixture_Centroid("M", 45, 5));
            memoryBuildingSolarRadiationSolveTask.SolvedReferences[0] = ["R1"];
            memoryBuildingSolarRadiationSolveTask.PostgreSQLBuildingSolarRadiationSolveOptions = new() { Radii = [0, 50], MaxDegreeOfParallelism = 1 };

            bool succeeded = await memoryBuildingSolarRadiationSolveTask.RunAsync(CancellationToken.None);

            Assert.False(succeeded);
            Assert.Equal(1, memoryBuildingSolarRadiationSolveTask.ProcessedCountyCount);
            Assert.Equal(1, memoryBuildingSolarRadiationSolveTask.FailedCountyCount);
            Assert.Equal(3, memoryBuildingSolarRadiationSolveTask.ProcessedBuildingCount);
            Assert.Equal(1, memoryBuildingSolarRadiationSolveTask.SkippedExistingCount);
            Assert.Equal(2, memoryBuildingSolarRadiationSolveTask.NotSolvableCount);
            Assert.Equal(0, memoryBuildingSolarRadiationSolveTask.OverCeilingCount);
            Assert.Equal(0, memoryBuildingSolarRadiationSolveTask.FailedCount);
            Assert.Equal(3, memoryBuildingSolarRadiationSolveTask.SolvedCount);
            Assert.Contains(memoryBuildingSolarRadiationSolveTask.FailureSamples, x => x.StartsWith("county 2:", StringComparison.Ordinal));
            Assert.Equal(1, memoryBuildingSolarRadiationSolveTask.NeighbourReadCount);

            (int CountyId, List<GIS.Classes.BuildingSolarRadiationResult> BuildingSolarRadiationResults) write = Assert.Single(memoryBuildingSolarRadiationSolveTask.Writes);
            Assert.Equal(1, write.CountyId);
            Assert.Equal(new string[] { "R0 0", "R0 50", "R1 50" }, write.BuildingSolarRadiationResults.Select(x => x.Reference + " " + x.Radius.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.All(write.BuildingSolarRadiationResults, x => Assert.Equal(1, x.CountyId));
        }

        /// <summary>
        /// Verifies how <see cref="BuildingSolarRadiationSolveTask"/> ends a run without solving: a transport refusing in <c>TryInitialize</c> ends it before any read; a requested cancellation raised inside a read propagates out of the run; a cancellation nobody requested (a timed-out request) fails only its county part, and the run carries on to the next one.
        /// </summary>
        [Fact]
        public async Task BuildingSolarRadiationSolveTask_Stop()
        {
            MemoryBuildingSolarRadiationSolveTask memoryBuildingSolarRadiationSolveTask_Refused = new(null) { Initialized = false };
            Assert.False(await memoryBuildingSolarRadiationSolveTask_Refused.RunAsync(CancellationToken.None));
            Assert.Equal(0, memoryBuildingSolarRadiationSolveTask_Refused.ReadCount);

            using CancellationTokenSource cancellationTokenSource = new();
            MemoryBuildingSolarRadiationSolveTask memoryBuildingSolarRadiationSolveTask_Canceled = new(null) { CentroidsCancellationTokenSource = cancellationTokenSource };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => memoryBuildingSolarRadiationSolveTask_Canceled.RunAsync(cancellationTokenSource.Token));
            Assert.Equal(0, memoryBuildingSolarRadiationSolveTask_Canceled.FailedCountyCount);

            MemoryBuildingSolarRadiationSolveTask memoryBuildingSolarRadiationSolveTask_TimedOut = new(null) { CentroidsTimeOut = true };
            Assert.False(await memoryBuildingSolarRadiationSolveTask_TimedOut.RunAsync(CancellationToken.None));
            Assert.Equal(2, memoryBuildingSolarRadiationSolveTask_TimedOut.FailedCountyCount);
            Assert.Equal(0, memoryBuildingSolarRadiationSolveTask_TimedOut.ProcessedCountyCount);
        }

        private static Building2DCentroid SolarFixture_Centroid(string reference, double x, double y)
        {
            return new Building2DCentroid() { Reference = reference, CountyId = 1, X = SolarFixture_Origin.X + x, Y = SolarFixture_Origin.Y + y };
        }

        /// <summary>
        /// A transport holding everything in memory: county parts 1 and 2, one EPW station (id 7) at the fixture origin, the buildings of part 1 only - part 2's centroids cannot be read - and every write recorded.
        /// </summary>
        private sealed class MemoryBuildingSolarRadiationSolveTask : BuildingSolarRadiationSolveTask
        {
            private readonly EPWFile? ePWFile;

            public MemoryBuildingSolarRadiationSolveTask(EPWFile? ePWFile)
            {
                this.ePWFile = ePWFile;
            }

            public List<Building2DCentroid> Building2DCentroids { get; } = [];

            public List<BuildingModel> BuildingModels { get; } = [];

            public CancellationTokenSource? CentroidsCancellationTokenSource { get; set; }

            public bool CentroidsTimeOut { get; set; }

            public bool Initialized { get; set; } = true;

            public int NeighbourReadCount { get; private set; }

            public int ReadCount { get; private set; }

            public Dictionary<double, HashSet<string>> SolvedReferences { get; } = [];

            public List<(int CountyId, List<GIS.Classes.BuildingSolarRadiationResult> BuildingSolarRadiationResults)> Writes { get; } = [];

            public Task<bool> RunAsync(CancellationToken cancellationToken)
            {
                return ExecuteAsync(new Progress<long>(), cancellationToken);
            }

            protected override Task<List<BuildingModel?>?> ReadBuildingModelsAsync(int countyId, List<string> references, int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                List<BuildingModel?> buildingModels = [.. BuildingModels.Where(x => x.TryGetValue(GIS.Analytical.Enums.BuildingModelParameter.Reference, out string? reference) && references.Contains(reference!))];
                return Task.FromResult<List<BuildingModel?>?>(buildingModels);
            }

            protected override Task<List<Building2DCentroid>?> ReadBuilding2DCentroidsAsync(int countyId, int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                if (CentroidsCancellationTokenSource is not null)
                {
                    CentroidsCancellationTokenSource.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (CentroidsTimeOut)
                {
                    throw new TaskCanceledException("The request timed out.");
                }

                if (countyId != 1)
                {
                    return Task.FromResult<List<Building2DCentroid>?>(null);
                }

                return Task.FromResult<List<Building2DCentroid>?>([.. Building2DCentroids]);
            }

            protected override Task<List<AdministrativeAreal2DReference>?> ReadCountyReferencesAsync(int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                return Task.FromResult<List<AdministrativeAreal2DReference>?>([new AdministrativeAreal2DReference() { Id = 2 }, new AdministrativeAreal2DReference() { Id = 1 }]);
            }

            protected override Task<EPWFile?> ReadEPWFileAsync(int id, int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                return Task.FromResult(id == 7 ? ePWFile : null);
            }

            protected override Task<List<(int Id, double X, double Y)>?> ReadEPWFileLocationsAsync(int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                return Task.FromResult<List<(int Id, double X, double Y)>?>([(7, SolarFixture_Origin.X, SolarFixture_Origin.Y)]);
            }

            protected override Task<List<BuildingModel?>?> ReadNeighbourBuildingModelsAsync(Circle2D circle2D, int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                NeighbourReadCount++;
                return Task.FromResult<List<BuildingModel?>?>([.. BuildingModels]);
            }

            protected override Task<HashSet<string>?> ReadSolvedReferencesAsync(int countyId, double radius, int solverVersion, int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                return Task.FromResult<HashSet<string>?>(SolvedReferences.TryGetValue(radius, out HashSet<string>? references) ? [.. references] : []);
            }

            protected override bool TryInitialize()
            {
                return Initialized;
            }

            protected override Task<int?> WriteAsync(int countyId, List<GIS.Classes.BuildingSolarRadiationResult> buildingSolarRadiationResults, int commandTimeout, CancellationToken cancellationToken)
            {
                Writes.Add((countyId, [.. buildingSolarRadiationResults]));
                return Task.FromResult<int?>(buildingSolarRadiationResults.Count);
            }
        }
    }
}
