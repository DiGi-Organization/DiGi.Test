using DiGi.EPW.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Solar.Classes;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.Analytical.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that the <c>SurfaceSolarRadiations</c> overload taking parallel options runs the shading solver's and the view factors' loops with the options given (DiGi-Organization/DiGi.GIS.PostgreSQL#167).
        /// <para>On a scheduler that runs one work item at a time the stored form is the one the overload without options produces, and options carrying a cancelled token stop the solve instead of letting it finish.</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiations_ParallelOptions()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            // One fixture, copied: a fixture built twice carries new component identifiers, so its surfaces would differ by reference alone.
            (ShadingModel shadingModel, Dictionary<string, Vector3D> normals) = SolarFixture_IsolatedShadingModel(SolarFixture_Box());

            ShadingModel shadingModel_Default = new(shadingModel);
            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations_Default = shadingModel_Default.SurfaceSolarRadiations(normals, ePWFile, Create.SolarShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiations_Default);
            Assert.NotEmpty(surfaceSolarRadiations_Default);

            ConcurrentExclusiveSchedulerPair concurrentExclusiveSchedulerPair = new(TaskScheduler.Default, 1);
            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 4, TaskScheduler = concurrentExclusiveSchedulerPair.ConcurrentScheduler };

            ShadingModel shadingModel_Shared = new(shadingModel);
            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations_Shared = shadingModel_Shared.SurfaceSolarRadiations(normals, ePWFile, Create.SolarShadingSolverOptions(), null, parallelOptions);
            concurrentExclusiveSchedulerPair.Complete();

            Assert.NotNull(surfaceSolarRadiations_Shared);
            Assert.Equal(surfaceSolarRadiations_Default.ConvertAll(x => Core.Convert.ToSystem_String(x)), surfaceSolarRadiations_Shared.ConvertAll(x => Core.Convert.ToSystem_String(x)));

            using CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.Cancel();

            ShadingModel shadingModel_Cancelled = new(shadingModel);
            Assert.Throws<OperationCanceledException>(() => shadingModel_Cancelled.SurfaceSolarRadiations(normals, ePWFile, Create.SolarShadingSolverOptions(), null, new ParallelOptions() { CancellationToken = cancellationTokenSource.Token }));
        }
    }
}
