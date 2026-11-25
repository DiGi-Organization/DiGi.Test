using DiGi.Analytical.Building.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.PostgreSQL.Solar.Classes;
using DiGi.GIS.PostgreSQL.Solar.Enums;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the jobs of a batch and the solver loops inside them share one scheduler without starving each other (DiGi.GIS.PostgreSQL#167): six jobs on two slots - both slots held by jobs whose receiver loops ask the same scheduler for helpers - finish, and store byte for byte what the sequential solve stores.
        /// <para>A job that waited for a helper no slot could run would hang the batch, so the solve runs under a time limit that turns a hang into a failure.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingSolarRadiationSolveOutcomes_SharedScheduler()
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
            for (int i = 0; i < 3; i++)
            {
                Dictionary<string, Vector3D>? normals = buildingModels[i].SolarReceiverNormals();
                Assert.NotNull(normals);

                foreach (double radius in new double[] { 0, 50 })
                {
                    buildingSolarRadiationSolveJobs.Add(new BuildingSolarRadiationSolveJob(buildingModels[i], "R" + i, normals, radius, 1, 7, ePWFile));
                }
            }

            List<BuildingSolarRadiationSolveOutcome> buildingSolarRadiationSolveOutcomes_Sequential = buildingSolarRadiationSolveJobs.BuildingSolarRadiationSolveOutcomes(neighbours, 32_000, maxDegreeOfParallelism: 1);
            List<BuildingSolarRadiationSolveOutcome> buildingSolarRadiationSolveOutcomes_Shared = await Task.Run(() => buildingSolarRadiationSolveJobs.BuildingSolarRadiationSolveOutcomes(neighbours, 32_000, maxDegreeOfParallelism: 2)).WaitAsync(TimeSpan.FromMinutes(2));

            Assert.Equal(buildingSolarRadiationSolveJobs.Count, buildingSolarRadiationSolveOutcomes_Shared.Count);
            for (int i = 0; i < buildingSolarRadiationSolveJobs.Count; i++)
            {
                Assert.Same(buildingSolarRadiationSolveJobs[i], buildingSolarRadiationSolveOutcomes_Shared[i].BuildingSolarRadiationSolveJob);
                Assert.Equal(BuildingSolarRadiationSolveOutcomeType.Solved, buildingSolarRadiationSolveOutcomes_Sequential[i].BuildingSolarRadiationSolveOutcomeType);
                Assert.Equal(BuildingSolarRadiationSolveOutcomeType.Solved, buildingSolarRadiationSolveOutcomes_Shared[i].BuildingSolarRadiationSolveOutcomeType);
                Assert.Equal(buildingSolarRadiationSolveOutcomes_Sequential[i].CasterTriangleCount, buildingSolarRadiationSolveOutcomes_Shared[i].CasterTriangleCount);
                Assert.Equal(Core.Convert.ToSystem_String(buildingSolarRadiationSolveOutcomes_Sequential[i].BuildingSolarRadiationResult), Core.Convert.ToSystem_String(buildingSolarRadiationSolveOutcomes_Shared[i].BuildingSolarRadiationResult));
            }
        }
    }
}
