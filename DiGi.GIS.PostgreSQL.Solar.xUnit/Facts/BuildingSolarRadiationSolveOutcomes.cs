using DiGi.Analytical.Building.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.PostgreSQL.Solar.Classes;
using DiGi.GIS.PostgreSQL.Solar.Enums;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DiGi.GIS.PostgreSQL.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the parallel batch solve stores what the sequential one stores: two receivers among two other buildings, one of them a tall tower, each receiver at radii 0 and 50 m, solved with a degree of parallelism of 1 and of 4, give the same outcomes in job order and byte-identical results - every irradiation and shading-factor value of every surface (DiGi.GIS.PostgreSQL#139).
        /// </summary>
        [MediumFact]
        public void BuildingSolarRadiationSolveOutcomes_Parity()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            List<BuildingModel> buildingModels =
            [
                SolarFixture_Box("R0", 0, 0, 10, 10, 10),
                SolarFixture_Box("R1", 20, 0, 10, 10, 15),
                SolarFixture_Box("R2", 0, 25, 10, 10, 8),
                SolarFixture_Box("N", 12, 12, 6, 6, 30),
            ];

            List<(BuildingModel BuildingModel, double XMin, double YMin, double XMax, double YMax)> neighbours = [];
            foreach (BuildingModel buildingModel in buildingModels)
            {
                neighbours.Add(SolarFixture_Neighbour(buildingModel));
            }

            List<BuildingSolarRadiationSolveJob> buildingSolarRadiationSolveJobs = [];
            for (int i = 0; i < 2; i++)
            {
                Dictionary<string, Vector3D>? normals = buildingModels[i].SolarReceiverNormals();
                Assert.NotNull(normals);

                foreach (double radius in new double[] { 0, 50 })
                {
                    buildingSolarRadiationSolveJobs.Add(new BuildingSolarRadiationSolveJob(buildingModels[i], "R" + i, normals, radius, 1, 7, ePWFile));
                }
            }

            List<BuildingSolarRadiationSolveOutcome> buildingSolarRadiationSolveOutcomes_Sequential = buildingSolarRadiationSolveJobs.BuildingSolarRadiationSolveOutcomes(neighbours, 32_000, maxDegreeOfParallelism: 1);
            List<BuildingSolarRadiationSolveOutcome> buildingSolarRadiationSolveOutcomes_Parallel = buildingSolarRadiationSolveJobs.BuildingSolarRadiationSolveOutcomes(neighbours, 32_000, maxDegreeOfParallelism: 4);

            Assert.Equal(buildingSolarRadiationSolveJobs.Count, buildingSolarRadiationSolveOutcomes_Sequential.Count);
            Assert.Equal(buildingSolarRadiationSolveJobs.Count, buildingSolarRadiationSolveOutcomes_Parallel.Count);

            for (int i = 0; i < buildingSolarRadiationSolveJobs.Count; i++)
            {
                BuildingSolarRadiationSolveOutcome buildingSolarRadiationSolveOutcome_Sequential = buildingSolarRadiationSolveOutcomes_Sequential[i];
                BuildingSolarRadiationSolveOutcome buildingSolarRadiationSolveOutcome_Parallel = buildingSolarRadiationSolveOutcomes_Parallel[i];

                Assert.Same(buildingSolarRadiationSolveJobs[i], buildingSolarRadiationSolveOutcome_Sequential.BuildingSolarRadiationSolveJob);
                Assert.Same(buildingSolarRadiationSolveJobs[i], buildingSolarRadiationSolveOutcome_Parallel.BuildingSolarRadiationSolveJob);
                Assert.Equal(BuildingSolarRadiationSolveOutcomeType.Solved, buildingSolarRadiationSolveOutcome_Sequential.BuildingSolarRadiationSolveOutcomeType);
                Assert.Equal(BuildingSolarRadiationSolveOutcomeType.Solved, buildingSolarRadiationSolveOutcome_Parallel.BuildingSolarRadiationSolveOutcomeType);

                GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult_Sequential = buildingSolarRadiationSolveOutcome_Sequential.BuildingSolarRadiationResult;
                GIS.Classes.BuildingSolarRadiationResult? buildingSolarRadiationResult_Parallel = buildingSolarRadiationSolveOutcome_Parallel.BuildingSolarRadiationResult;
                Assert.NotNull(buildingSolarRadiationResult_Sequential);
                Assert.NotNull(buildingSolarRadiationResult_Parallel);
                Assert.NotEmpty(buildingSolarRadiationResult_Sequential.SurfaceSolarRadiations ?? []);

                Assert.Equal(Core.Convert.ToSystem_String(buildingSolarRadiationResult_Sequential), Core.Convert.ToSystem_String(buildingSolarRadiationResult_Parallel));
            }

            // The neighbour shades at 50 m: a result that ignored the radius would make the parity above vacuous.
            Assert.NotEqual(string.Join("|", buildingSolarRadiationSolveOutcomes_Sequential[0].BuildingSolarRadiationResult!.SurfaceSolarRadiations!.ConvertAll(x => Core.Convert.ToSystem_String(x))), string.Join("|", buildingSolarRadiationSolveOutcomes_Sequential[1].BuildingSolarRadiationResult!.SurfaceSolarRadiations!.ConvertAll(x => Core.Convert.ToSystem_String(x))));
        }

        /// <summary>
        /// Verifies that outcomes keep job order whatever ends each job: with a caster ceiling between the building alone and the building among its neighbours, the 0 m jobs are solved, the 50 m job is over the ceiling and a job with no receiving surface fails - in the order the jobs were given (DiGi.GIS.PostgreSQL#139).
        /// </summary>
        [MediumFact]
        public void BuildingSolarRadiationSolveOutcomes_Order()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            BuildingModel buildingModel_1 = SolarFixture_Box("R1", 0, 0, 10, 10, 10);
            BuildingModel buildingModel_2 = SolarFixture_Box("R2", 20, 0, 10, 10, 15);

            List<(BuildingModel BuildingModel, double XMin, double YMin, double XMax, double YMax)> neighbours = [SolarFixture_Neighbour(buildingModel_1), SolarFixture_Neighbour(buildingModel_2)];

            Dictionary<string, Vector3D>? normals_1 = buildingModel_1.SolarReceiverNormals();
            Dictionary<string, Vector3D>? normals_2 = buildingModel_2.SolarReceiverNormals();
            Assert.NotNull(normals_1);
            Assert.NotNull(normals_2);

            List<BuildingSolarRadiationSolveJob> buildingSolarRadiationSolveJobs =
            [
                new BuildingSolarRadiationSolveJob(buildingModel_1, "R1", normals_1, 0, 1, 7, ePWFile),
                new BuildingSolarRadiationSolveJob(buildingModel_1, "R1", normals_1, 50, 1, 7, ePWFile),
                new BuildingSolarRadiationSolveJob(buildingModel_2, "R2", [], 0, 1, 7, ePWFile),
                new BuildingSolarRadiationSolveJob(buildingModel_2, "R2", normals_2, 0, 1, 7, ePWFile),
            ];

            // Alone, a box casts with its floor only (2 triangles); at 50 m the other box adds its six faces (12 triangles); a
            // box with no receiving surface casts with all six of its own. A ceiling of 13 leaves only the 50 m job over it.
            List<BuildingSolarRadiationSolveOutcome> buildingSolarRadiationSolveOutcomes = buildingSolarRadiationSolveJobs.BuildingSolarRadiationSolveOutcomes(neighbours, 13, maxDegreeOfParallelism: 4);

            Assert.Equal(4, buildingSolarRadiationSolveOutcomes.Count);
            for (int i = 0; i < buildingSolarRadiationSolveJobs.Count; i++)
            {
                Assert.Same(buildingSolarRadiationSolveJobs[i], buildingSolarRadiationSolveOutcomes[i].BuildingSolarRadiationSolveJob);
            }

            Assert.Equal(BuildingSolarRadiationSolveOutcomeType.Solved, buildingSolarRadiationSolveOutcomes[0].BuildingSolarRadiationSolveOutcomeType);
            Assert.Equal(BuildingSolarRadiationSolveOutcomeType.OverCeiling, buildingSolarRadiationSolveOutcomes[1].BuildingSolarRadiationSolveOutcomeType);
            Assert.Equal(BuildingSolarRadiationSolveOutcomeType.Failed, buildingSolarRadiationSolveOutcomes[2].BuildingSolarRadiationSolveOutcomeType);
            Assert.False(string.IsNullOrWhiteSpace(buildingSolarRadiationSolveOutcomes[2].Reason));
            Assert.Equal(BuildingSolarRadiationSolveOutcomeType.Solved, buildingSolarRadiationSolveOutcomes[3].BuildingSolarRadiationSolveOutcomeType);

            Assert.NotNull(buildingSolarRadiationSolveOutcomes[0].BuildingSolarRadiationResult);
            Assert.Null(buildingSolarRadiationSolveOutcomes[1].BuildingSolarRadiationResult);
            Assert.Null(buildingSolarRadiationSolveOutcomes[2].BuildingSolarRadiationResult);
        }

        /// <summary>
        /// Verifies that a cancelled token leaves the batch solve with <see cref="OperationCanceledException"/> rather than returning outcomes (DiGi.GIS.PostgreSQL#139).
        /// </summary>
        [Fact]
        public void BuildingSolarRadiationSolveOutcomes_Cancelled()
        {
            BuildingModel buildingModel = SolarFixture_Box("R1", 0, 0, 10, 10, 10);
            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            List<BuildingSolarRadiationSolveJob> buildingSolarRadiationSolveJobs = [new BuildingSolarRadiationSolveJob(buildingModel, "R1", normals, 0, 1, 7, SolarFixture_EPWFile())];

            using CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.Cancel();

            Assert.ThrowsAny<OperationCanceledException>(() => buildingSolarRadiationSolveJobs.BuildingSolarRadiationSolveOutcomes(null, 32_000, maxDegreeOfParallelism: 2, cancellationToken: cancellationTokenSource.Token));
        }

        /// <summary>
        /// Verifies that the batch solve returns no outcomes for no jobs.
        /// </summary>
        [Fact]
        public void BuildingSolarRadiationSolveOutcomes_Empty()
        {
            Assert.Empty(Query.BuildingSolarRadiationSolveOutcomes(null, null, 32_000));
            Assert.Empty(new List<BuildingSolarRadiationSolveJob>().BuildingSolarRadiationSolveOutcomes(null, 32_000));
        }
    }
}
