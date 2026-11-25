using DiGi.Solar.Classes;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="ShadingSolver.ParallelOptions"/> runs the solver's loop over the receivers with the options given (DiGi-Organization/DiGi.GIS.PostgreSQL#167).
        /// <para>A solve on a scheduler that runs one work item at a time gives every receiver bit for bit the shading factors of the default solve, and a solve whose options carry a cancelled token stops instead of finishing - the default options would have ignored it.</para>
        /// </summary>
        [Fact]
        public void ShadingSolver_ParallelOptions()
        {
            ShadingModel shadingModel = CreatePerformanceShadingModel(2, 2);
            DateTime[] dateTimes = CreateDaytimeSeries(120);

            ShadingModel shadingModel_Default = new(shadingModel);
            Assert.True(new ShadingSolver(shadingModel_Default, dateTimes).Solve());

            ConcurrentExclusiveSchedulerPair concurrentExclusiveSchedulerPair = new(TaskScheduler.Default, 1);
            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 4, TaskScheduler = concurrentExclusiveSchedulerPair.ConcurrentScheduler };

            ShadingModel shadingModel_Shared = new(shadingModel);
            Assert.True(new ShadingSolver(shadingModel_Shared, dateTimes) { ParallelOptions = parallelOptions }.Solve());
            concurrentExclusiveSchedulerPair.Complete();

            List<ShadingElement>? receivers = shadingModel_Default.GetShadingElements<ShadingElement>(shadingOnly: false);
            Assert.NotNull(receivers);
            Assert.NotEmpty(receivers);

            int count = 0;
            foreach (ShadingElement receiver in receivers)
            {
                foreach (DateTime dateTime in dateTimes)
                {
                    bool hasFactor_Default = shadingModel_Default.TryGetShadingFactor(receiver, dateTime, out double factor_Default, false);
                    bool hasFactor_Shared = shadingModel_Shared.TryGetShadingFactor(receiver, dateTime, out double factor_Shared, false);

                    Assert.Equal(hasFactor_Default, hasFactor_Shared);
                    Assert.Equal(factor_Default, factor_Shared);
                    if (hasFactor_Default)
                    {
                        count++;
                    }
                }
            }

            Assert.True(count > 0);

            using CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.Cancel();

            ShadingSolver shadingSolver_Cancelled = new(new ShadingModel(shadingModel), dateTimes) { ParallelOptions = new ParallelOptions() { CancellationToken = cancellationTokenSource.Token } };
            Assert.Throws<OperationCanceledException>(() => shadingSolver_Cancelled.Solve());
        }

        /// <summary>
        /// Tests that the <see cref="Create.ViewFactorResults(ShadingModel?, IDictionary{string, Geometry.Spatial.Classes.Vector3D}?, double, int, int, ParallelOptions?)"/> overload runs its loop over the receivers with the options given (DiGi-Organization/DiGi.GIS.PostgreSQL#167).
        /// <para>On a scheduler that runs one work item at a time it gives every receiver bit for bit the visibilities of the default overload, and options carrying a cancelled token stop it instead of letting it finish.</para>
        /// </summary>
        [Fact]
        public void ViewFactorResults_ParallelOptions()
        {
            ShadingModel shadingModel = CreatePerformanceShadingModel(2, 2);

            List<ViewFactorResult>? viewFactorResults_Default = shadingModel.ViewFactorResults(null, Core.Constants.Tolerance.Distance);
            Assert.NotNull(viewFactorResults_Default);
            Assert.NotEmpty(viewFactorResults_Default);

            ConcurrentExclusiveSchedulerPair concurrentExclusiveSchedulerPair = new(TaskScheduler.Default, 1);
            ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = 4, TaskScheduler = concurrentExclusiveSchedulerPair.ConcurrentScheduler };

            List<ViewFactorResult>? viewFactorResults_Shared = shadingModel.ViewFactorResults(null, Core.Constants.Tolerance.Distance, 6, 24, parallelOptions);
            concurrentExclusiveSchedulerPair.Complete();

            Assert.NotNull(viewFactorResults_Shared);
            Assert.Equal(viewFactorResults_Default.Count, viewFactorResults_Shared.Count);
            for (int i = 0; i < viewFactorResults_Default.Count; i++)
            {
                Assert.Equal(viewFactorResults_Default[i].Reference, viewFactorResults_Shared[i].Reference);
                Assert.Equal(viewFactorResults_Default[i].SkyVisibility, viewFactorResults_Shared[i].SkyVisibility);
                Assert.Equal(viewFactorResults_Default[i].GroundVisibility, viewFactorResults_Shared[i].GroundVisibility);
            }

            using CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.Cancel();

            Assert.Throws<OperationCanceledException>(() => shadingModel.ViewFactorResults(null, Core.Constants.Tolerance.Distance, 6, 24, new ParallelOptions() { CancellationToken = cancellationTokenSource.Token }));
        }
    }
}
