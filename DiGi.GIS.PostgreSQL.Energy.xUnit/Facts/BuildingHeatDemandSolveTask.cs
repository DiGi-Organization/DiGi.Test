using DiGi.Analytical.Building.Classes;
using DiGi.Core.IO.Table.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Energy.Classes;
using DiGi.GIS.Analytical.Energy.Enums;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.IO;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Energy.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Building2D = DiGi.GIS.Classes.Building2D;
using BuildingModel = DiGi.Analytical.Building.Classes.BuildingModel;

namespace DiGi.GIS.PostgreSQL.Energy.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The EPSG:2180 position of the fixtures: central Warsaw, where the IMGW weather fixture's station is.
        /// </summary>
        private static readonly Point2D heatDemandSolve_Origin = new(638000, 486000);

        /// <summary>
        /// Verifies a run of <see cref="BuildingHeatDemandSolveTask"/> over two county parts, its counts and what it writes (DiGi.GIS.PostgreSQL#143).
        /// <para>Part 1 holds R0 and R1, two two-storey houses sharing a wall, and M, a centroid with no stored model. Neither house has a stored solar result. Part 2 cannot have its centroids read. Expected: R0 and R1 are calculated, unshaded, and handed to one write of part 1, each filed under part 1 with method version 1 and a party wall that lowers its direct heat transfer below that of the same house alone; M is not solvable with a sample; part 2 is failed with a sample and the run reports not succeeded while still counting part 1. A second run with <see cref="PostgreSQLBuildingHeatDemandSolveOptions.SkipCompleted"/> steps over R0 and R1 and writes nothing.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingHeatDemandSolveTask_Run()
        {
            MemoryBuildingHeatDemandSolveTask memoryBuildingHeatDemandSolveTask = HeatDemandSolve_Task(1);

            bool succeeded = await memoryBuildingHeatDemandSolveTask.RunAsync(CancellationToken.None);

            Assert.False(succeeded);
            Assert.Equal(1, memoryBuildingHeatDemandSolveTask.ProcessedCountyCount);
            Assert.Equal(1, memoryBuildingHeatDemandSolveTask.FailedCountyCount);
            Assert.Equal(3, memoryBuildingHeatDemandSolveTask.ProcessedBuildingCount);
            Assert.Equal(2, memoryBuildingHeatDemandSolveTask.SolvedCount);
            Assert.Equal(2, memoryBuildingHeatDemandSolveTask.UnshadedBuildingCount);
            Assert.Equal(1, memoryBuildingHeatDemandSolveTask.NotSolvableCount);
            Assert.Equal(0, memoryBuildingHeatDemandSolveTask.FailedCount);
            Assert.Equal(0, memoryBuildingHeatDemandSolveTask.SkippedCompletedCount);
            Assert.Contains(memoryBuildingHeatDemandSolveTask.FailureSamples, x => x.StartsWith("M: not solvable", StringComparison.Ordinal));
            Assert.Contains(memoryBuildingHeatDemandSolveTask.FailureSamples, x => x.StartsWith("county 2:", StringComparison.Ordinal));

            (int CountyId, List<BuildingHeatDemandResult> BuildingHeatDemandResults) write = Assert.Single(memoryBuildingHeatDemandSolveTask.Writes);
            Assert.Equal(1, write.CountyId);
            Assert.Equal(["R0", "R1"], write.BuildingHeatDemandResults.Select(x => x.Reference).OrderBy(x => x));
            Assert.All(write.BuildingHeatDemandResults, x =>
            {
                Assert.Equal(1, x.CountyId);
                Assert.Equal(HeatDemandSolarSource.Unshaded, x.SolarSource);
                Assert.Equal(WeatherSourceType.EPW, x.WeatherSourceType);
                Assert.Equal("7", x.WeatherSourceId);
                Assert.Equal((short)1975, x.YearBuilt);
                Assert.Equal(YearBuiltSource.User, x.YearBuiltSource);
                Assert.Equal(DiGi.Analytical.Building.Energy.Constants.Default.HeatDemandMethodVersion, x.HeatDemandResult?.MethodVersion);
                Assert.True(x.HeatDemandResult?.HeatingEnergyNeed > 0);
            });

            // The party wall: R0 alongside R1 loses less through its walls than R0 alone.
            MemoryBuildingHeatDemandSolveTask memoryBuildingHeatDemandSolveTask_Alone = HeatDemandSolve_Task(1);
            memoryBuildingHeatDemandSolveTask_Alone.BuildingModels.RemoveAll(x => x.TryGetValue(GIS.Analytical.Enums.BuildingModelParameter.Reference, out string? reference) && reference == "R1");
            memoryBuildingHeatDemandSolveTask_Alone.Building2DCentroids.RemoveAll(x => x.Reference != "R0");
            Assert.False(await memoryBuildingHeatDemandSolveTask_Alone.RunAsync(CancellationToken.None));
            BuildingHeatDemandResult buildingHeatDemandResult_Alone = Assert.Single(Assert.Single(memoryBuildingHeatDemandSolveTask_Alone.Writes).BuildingHeatDemandResults);
            BuildingHeatDemandResult buildingHeatDemandResult_Attached = write.BuildingHeatDemandResults.Single(x => x.Reference == "R0");
            Assert.True(buildingHeatDemandResult_Attached.HeatDemandResult!.DirectHeatTransferCoefficient < buildingHeatDemandResult_Alone.HeatDemandResult!.DirectHeatTransferCoefficient);

            // A resumed run steps over what is stored.
            foreach (BuildingHeatDemandResult buildingHeatDemandResult in write.BuildingHeatDemandResults)
            {
                memoryBuildingHeatDemandSolveTask.CompletedReferences.Add(buildingHeatDemandResult.Reference!);
            }

            memoryBuildingHeatDemandSolveTask.Writes.Clear();
            Assert.False(await memoryBuildingHeatDemandSolveTask.RunAsync(CancellationToken.None));
            Assert.Equal(2, memoryBuildingHeatDemandSolveTask.SkippedCompletedCount);
            Assert.Equal(0, memoryBuildingHeatDemandSolveTask.SolvedCount);
            Assert.Empty(memoryBuildingHeatDemandSolveTask.Writes);
        }

        /// <summary>
        /// Verifies that <see cref="BuildingHeatDemandSolveTask"/> calculates a building predicted at the predictor's first year as the fallback year, in both construction flavours (DiGi-Organization/DiGi.GIS.Analytical#16).
        /// <para>R0 is read as Prediction 2008 and R1 as a user year equal to the fallback. Expected: R0 is written with the fallback year and source Fallback, and with the construction set of R1, in a TypicalAsBuilt run and in a Regulation run, whose regime the task selects itself.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingHeatDemandSolveTask_CensoredPrediction()
        {
            short fallbackYearBuilt = new OpeningGenerationOptions().FallbackYearBuilt;
            foreach (DiGi.Analytical.Building.Regulation.Enums.ConstructionFlavour constructionFlavour in new[] { DiGi.Analytical.Building.Regulation.Enums.ConstructionFlavour.TypicalAsBuilt, DiGi.Analytical.Building.Regulation.Enums.ConstructionFlavour.Regulation })
            {
                MemoryBuildingHeatDemandSolveTask memoryBuildingHeatDemandSolveTask = HeatDemandSolve_Task(1);
                memoryBuildingHeatDemandSolveTask.YearBuilts["R0"] = (2008, YearBuiltSource.Prediction, null);
                memoryBuildingHeatDemandSolveTask.YearBuilts["R1"] = (fallbackYearBuilt, YearBuiltSource.User, null);
                memoryBuildingHeatDemandSolveTask.PostgreSQLBuildingHeatDemandSolveOptions.ConstructionFlavour = constructionFlavour;

                await memoryBuildingHeatDemandSolveTask.RunAsync(CancellationToken.None);

                Dictionary<string, BuildingHeatDemandResult> buildingHeatDemandResults = Assert.Single(memoryBuildingHeatDemandSolveTask.Writes).BuildingHeatDemandResults.ToDictionary(x => x.Reference!);
                BuildingHeatDemandResult buildingHeatDemandResult_Prediction = buildingHeatDemandResults["R0"];
                BuildingHeatDemandResult buildingHeatDemandResult_User = buildingHeatDemandResults["R1"];
                Assert.Equal(fallbackYearBuilt, buildingHeatDemandResult_Prediction.YearBuilt);
                Assert.Equal(YearBuiltSource.Fallback, buildingHeatDemandResult_Prediction.YearBuiltSource);
                Assert.Equal(constructionFlavour, buildingHeatDemandResult_Prediction.ConstructionFlavour);
                Assert.NotNull(buildingHeatDemandResult_Prediction.ConstructionSetSource);
                Assert.Equal(buildingHeatDemandResult_User.ConstructionSetSource, buildingHeatDemandResult_Prediction.ConstructionSetSource);
            }
        }

        /// <summary>
        /// Verifies that <see cref="BuildingHeatDemandSolveTask"/> calculates a building predicted at the predictor's first year as the census year built of its area, read with its year built (DiGi-Organization/DiGi.GIS.Analytical#17).
        /// <para>First run: R0 and R1 are Prediction 2008 with census years 1960 and 1995; both are written with their census year and source Census, and with different construction sets. Second run: R0 is Prediction 2008 with no census year and takes the fallback year; R1 is a user year of 2008 and keeps it whatever its census year.</para>
        /// </summary>
        [MediumFact]
        public async Task BuildingHeatDemandSolveTask_CensusYearBuilt()
        {
            MemoryBuildingHeatDemandSolveTask memoryBuildingHeatDemandSolveTask = HeatDemandSolve_Task(1);
            memoryBuildingHeatDemandSolveTask.YearBuilts["R0"] = (2008, YearBuiltSource.Prediction, 1960);
            memoryBuildingHeatDemandSolveTask.YearBuilts["R1"] = (2008, YearBuiltSource.Prediction, 1995);

            await memoryBuildingHeatDemandSolveTask.RunAsync(CancellationToken.None);

            Dictionary<string, BuildingHeatDemandResult> buildingHeatDemandResults = Assert.Single(memoryBuildingHeatDemandSolveTask.Writes).BuildingHeatDemandResults.ToDictionary(x => x.Reference!);
            Assert.Equal((short)1960, buildingHeatDemandResults["R0"].YearBuilt);
            Assert.Equal(YearBuiltSource.Census, buildingHeatDemandResults["R0"].YearBuiltSource);
            Assert.Equal((short)1995, buildingHeatDemandResults["R1"].YearBuilt);
            Assert.Equal(YearBuiltSource.Census, buildingHeatDemandResults["R1"].YearBuiltSource);
            Assert.NotNull(buildingHeatDemandResults["R0"].ConstructionSetSource);
            Assert.NotEqual(buildingHeatDemandResults["R0"].ConstructionSetSource, buildingHeatDemandResults["R1"].ConstructionSetSource);

            memoryBuildingHeatDemandSolveTask = HeatDemandSolve_Task(1);
            memoryBuildingHeatDemandSolveTask.YearBuilts["R0"] = (2008, YearBuiltSource.Prediction, null);
            memoryBuildingHeatDemandSolveTask.YearBuilts["R1"] = (2008, YearBuiltSource.User, 1960);

            await memoryBuildingHeatDemandSolveTask.RunAsync(CancellationToken.None);

            buildingHeatDemandResults = Assert.Single(memoryBuildingHeatDemandSolveTask.Writes).BuildingHeatDemandResults.ToDictionary(x => x.Reference!);
            Assert.Equal(new OpeningGenerationOptions().FallbackYearBuilt, buildingHeatDemandResults["R0"].YearBuilt);
            Assert.Equal(YearBuiltSource.Fallback, buildingHeatDemandResults["R0"].YearBuiltSource);
            Assert.Equal((short)2008, buildingHeatDemandResults["R1"].YearBuilt);
            Assert.Equal(YearBuiltSource.User, buildingHeatDemandResults["R1"].YearBuiltSource);
        }

        /// <summary>
        /// Verifies that the parallel calculation of a batch gives the same results as one building at a time (DiGi.GIS.PostgreSQL#143): the construction library, the opening-size library, the climate-zone map and the weather are shared by every calculation of a run, so a calculation that wrote to one of them would show here.
        /// </summary>
        [MediumFact]
        public async Task BuildingHeatDemandSolveTask_Parallel()
        {
            MemoryBuildingHeatDemandSolveTask memoryBuildingHeatDemandSolveTask_Sequential = HeatDemandSolve_Task(1);
            MemoryBuildingHeatDemandSolveTask memoryBuildingHeatDemandSolveTask_Parallel = HeatDemandSolve_Task(4);

            await memoryBuildingHeatDemandSolveTask_Sequential.RunAsync(CancellationToken.None);
            await memoryBuildingHeatDemandSolveTask_Parallel.RunAsync(CancellationToken.None);

            Dictionary<string, BuildingHeatDemandResult> results_Sequential = Assert.Single(memoryBuildingHeatDemandSolveTask_Sequential.Writes).BuildingHeatDemandResults.ToDictionary(x => x.Reference!);
            Dictionary<string, BuildingHeatDemandResult> results_Parallel = Assert.Single(memoryBuildingHeatDemandSolveTask_Parallel.Writes).BuildingHeatDemandResults.ToDictionary(x => x.Reference!);
            Assert.Equal(results_Sequential.Keys.OrderBy(x => x), results_Parallel.Keys.OrderBy(x => x));
            foreach (string reference in results_Sequential.Keys)
            {
                Assert.Equal(results_Sequential[reference].HeatDemandResult!.HeatingEnergyNeeds, results_Parallel[reference].HeatDemandResult!.HeatingEnergyNeeds);
                Assert.Equal(results_Sequential[reference].HeatDemandResult!.DesignHeatLoad, results_Parallel[reference].HeatDemandResult!.DesignHeatLoad);
            }
        }

        /// <summary>
        /// Verifies how <see cref="BuildingHeatDemandSolveTask"/> ends a run without calculating (DiGi.GIS.PostgreSQL#143): a transport refusing in <c>TryInitialize</c> ends it before any read, and a requested cancellation propagates out of the run instead of being counted.
        /// </summary>
        [Fact]
        public async Task BuildingHeatDemandSolveTask_Stop()
        {
            MemoryBuildingHeatDemandSolveTask memoryBuildingHeatDemandSolveTask_Refused = HeatDemandSolve_Task(1);
            memoryBuildingHeatDemandSolveTask_Refused.Initialized = false;
            Assert.False(await memoryBuildingHeatDemandSolveTask_Refused.RunAsync(CancellationToken.None));
            Assert.Equal(0, memoryBuildingHeatDemandSolveTask_Refused.ReadCount);

            using CancellationTokenSource cancellationTokenSource = new();
            cancellationTokenSource.Cancel();
            MemoryBuildingHeatDemandSolveTask memoryBuildingHeatDemandSolveTask_Cancelled = HeatDemandSolve_Task(1);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => memoryBuildingHeatDemandSolveTask_Cancelled.RunAsync(cancellationTokenSource.Token));
            Assert.Equal(0, memoryBuildingHeatDemandSolveTask_Cancelled.FailedCountyCount);
        }

        /// <summary>
        /// Verifies <see cref="Modify.Update_HeatDemand"/> (DiGi.GIS.PostgreSQL#143): the four heat-demand columns of a <c>building_data</c> table are filled per county part and reference, a non-finite value is left as a null cell, the first result of a key wins, and a result without a heat demand is stepped over.
        /// </summary>
        [Fact]
        public void Modify_Update_HeatDemand()
        {
            BuildingHeatDemandResult buildingHeatDemandResult_1 = HeatDemandSolve_Result("A", 1, 12000, 120, 8000);
            BuildingHeatDemandResult buildingHeatDemandResult_Duplicate = HeatDemandSolve_Result("A", 1, 99999, 999, 99999);
            BuildingHeatDemandResult buildingHeatDemandResult_2 = HeatDemandSolve_Result("B", 2, 6000, double.NaN, 4000);
            BuildingHeatDemandResult buildingHeatDemandResult_Empty = new("C", 1, 3, WeatherSourceType.EPW, "7", HeatDemandSolarSource.Unshaded, null, null, 0, 0, DiGi.Analytical.Building.Regulation.Enums.ConstructionFlavour.TypicalAsBuilt, null, null, DiGi.Analytical.Building.Regulation.Enums.BuildingType.SingleFamily, null, null, null, null, null);

            Table table = new();
            Assert.Equal(2, table.Update_HeatDemand([buildingHeatDemandResult_1, buildingHeatDemandResult_Duplicate, buildingHeatDemandResult_2, buildingHeatDemandResult_Empty, null]));
            Assert.Equal(2, table.RowCount);
            Assert.Equal(0, ((Table?)null).Update_HeatDemand([buildingHeatDemandResult_1]));

            Dictionary<string, Column> columns = [];
            foreach (Column column in table.Columns)
            {
                columns[Core.IO.Query.UniqueId(column)!] = column;
            }

            Dictionary<(int, string), Row> rows = PostgreSQL.Query.RowsByCountyIdAndReference(table, columns[Core.IO.Query.UniqueId(IO.Constants.Column.CountyId)!], columns[Core.IO.Query.UniqueId(IO.Constants.Column.Reference)!]);
            Row row_A = rows[(1, "A")];
            Row row_B = rows[(2, "B")];

            Assert.Equal(12000f, Value(row_A, IO.Constants.Column.HeatingEnergyNeed));
            Assert.Equal(120f, Value(row_A, IO.Constants.Column.HeatingEnergyNeedPerArea));
            Assert.Equal(8000f, Value(row_A, IO.Constants.Column.DesignHeatLoad));
            Assert.Equal(1, System.Convert.ToInt32(Value(row_A, IO.Constants.Column.HeatDemandMethodVersion), CultureInfo.InvariantCulture));
            Assert.Null(Value(row_B, IO.Constants.Column.HeatingEnergyNeedPerArea));
            Assert.Equal(6000f, Value(row_B, IO.Constants.Column.HeatingEnergyNeed));

            object? Value(Row row, Column column_Constant)
            {
                return row.TryGetValue(columns[Core.IO.Query.UniqueId(column_Constant)!].Index, out object? value) ? value : null;
            }
        }

        /// <summary>
        /// Verifies the serialization and copy of <see cref="PostgreSQLBuildingHeatDemandSolveOptions"/> (DiGi.GIS.PostgreSQL#143): a string round trip, <see cref="Core.xUnit.Query.SerializationCheck"/> and a member-by-member <c>new T(source)</c> assertion; the default neighbour radius is the opening generator's obstruction distance.
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingHeatDemandSolveOptions_Serialization()
        {
            Assert.Equal(new OpeningGenerationOptions().ObstructionMaxDistance, new PostgreSQLBuildingHeatDemandSolveOptions().NeighbourRadius);

            PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions = new()
            {
                BatchSize = 25,
                BuildingCountMax = 40,
                CommandTimeout = 120,
                ConstructionFlavour = DiGi.Analytical.Building.Regulation.Enums.ConstructionFlavour.Regulation,
                CountyIds = [1465, 1466],
                HeatDemandOptions = new DiGi.Analytical.Building.Energy.Classes.HeatDemandOptions() { IncludeSkyRadiation = false },
                MaxDegreeOfParallelism = 3,
                NeighbourRadius = 20,
                SkipCompleted = false,
                SolarRadius = 50,
                TileSize = 125,
            };

            PostgreSQLBuildingHeatDemandSolveOptions postgreSQLBuildingHeatDemandSolveOptions_Copy = new(postgreSQLBuildingHeatDemandSolveOptions);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.BatchSize, postgreSQLBuildingHeatDemandSolveOptions_Copy.BatchSize);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.BuildingCountMax, postgreSQLBuildingHeatDemandSolveOptions_Copy.BuildingCountMax);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.CommandTimeout, postgreSQLBuildingHeatDemandSolveOptions_Copy.CommandTimeout);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.ConstructionFlavour, postgreSQLBuildingHeatDemandSolveOptions_Copy.ConstructionFlavour);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.CountyIds, postgreSQLBuildingHeatDemandSolveOptions_Copy.CountyIds);
            Assert.NotSame(postgreSQLBuildingHeatDemandSolveOptions.CountyIds, postgreSQLBuildingHeatDemandSolveOptions_Copy.CountyIds);
            Assert.False(postgreSQLBuildingHeatDemandSolveOptions_Copy.HeatDemandOptions?.IncludeSkyRadiation);
            Assert.NotSame(postgreSQLBuildingHeatDemandSolveOptions.HeatDemandOptions, postgreSQLBuildingHeatDemandSolveOptions_Copy.HeatDemandOptions);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.MaxDegreeOfParallelism, postgreSQLBuildingHeatDemandSolveOptions_Copy.MaxDegreeOfParallelism);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.NeighbourRadius, postgreSQLBuildingHeatDemandSolveOptions_Copy.NeighbourRadius);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.SkipCompleted, postgreSQLBuildingHeatDemandSolveOptions_Copy.SkipCompleted);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.SolarRadius, postgreSQLBuildingHeatDemandSolveOptions_Copy.SolarRadius);
            Assert.Equal(postgreSQLBuildingHeatDemandSolveOptions.TileSize, postgreSQLBuildingHeatDemandSolveOptions_Copy.TileSize);
            Core.xUnit.Query.SerializationCheck(postgreSQLBuildingHeatDemandSolveOptions);

            PostgreSQLBuildingHeatDemandSolveOptions? postgreSQLBuildingHeatDemandSolveOptions_Json = Core.Convert.ToDiGi<PostgreSQLBuildingHeatDemandSolveOptions>(Core.Convert.ToSystem_String(postgreSQLBuildingHeatDemandSolveOptions))?.FirstOrDefault();
            Assert.NotNull(postgreSQLBuildingHeatDemandSolveOptions_Json);
            Assert.Equal([1465, 1466], postgreSQLBuildingHeatDemandSolveOptions_Json.CountyIds?.OrderBy(x => x));
            Assert.Equal(DiGi.Analytical.Building.Regulation.Enums.ConstructionFlavour.Regulation, postgreSQLBuildingHeatDemandSolveOptions_Json.ConstructionFlavour);
            Assert.False(postgreSQLBuildingHeatDemandSolveOptions_Json.HeatDemandOptions?.IncludeSkyRadiation);
        }

        /// <summary>
        /// Builds the in-memory task of the run facts: part 1 holds R0 and R1, two 10 × 8 m houses of two 3 m storeys sharing the wall at x = 10, and M, a centroid with no stored model; part 2 cannot have its centroids read. Both houses were built in 1975 by the user's year; the weather is the Warsaw EPW under id 7, the only station.
        /// </summary>
        /// <param name="maxDegreeOfParallelism">The largest number of buildings calculated at once.</param>
        /// <returns>The task.</returns>
        private static MemoryBuildingHeatDemandSolveTask HeatDemandSolve_Task(int maxDegreeOfParallelism)
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));
            EPWFile? ePWFile = EPW.Modify.Read(path);
            Assert.NotNull(ePWFile);

            MemoryBuildingHeatDemandSolveTask result = new(ePWFile);
            result.BuildingModels.Add(HeatDemandSolve_BuildingModel("R0", 0, 0, 10, 8));
            result.BuildingModels.Add(HeatDemandSolve_BuildingModel("R1", 10, 0, 20, 8));
            result.Building2Ds.Add(HeatDemandSolve_Building2D("R0", 0, 0, 10, 8));
            result.Building2Ds.Add(HeatDemandSolve_Building2D("R1", 10, 0, 20, 8));
            result.Building2DCentroids.Add(new Building2DCentroid() { CountyId = 1, Reference = "R0", X = heatDemandSolve_Origin.X + 5, Y = heatDemandSolve_Origin.Y + 4 });
            result.Building2DCentroids.Add(new Building2DCentroid() { CountyId = 1, Reference = "R1", X = heatDemandSolve_Origin.X + 15, Y = heatDemandSolve_Origin.Y + 4 });
            result.Building2DCentroids.Add(new Building2DCentroid() { CountyId = 1, Reference = "M", X = heatDemandSolve_Origin.X + 40, Y = heatDemandSolve_Origin.Y + 4 });
            result.YearBuilts["R0"] = (1975, YearBuiltSource.User, null);
            result.YearBuilts["R1"] = (1975, YearBuiltSource.User, null);
            result.PostgreSQLBuildingHeatDemandSolveOptions = new PostgreSQLBuildingHeatDemandSolveOptions() { MaxDegreeOfParallelism = maxDegreeOfParallelism };
            return result;
        }

        /// <summary>
        /// Builds a two-storey stored model over a rectangle of the fixture, stamped with its reference.
        /// </summary>
        private static BuildingModel HeatDemandSolve_BuildingModel(string reference, double xMin, double yMin, double xMax, double yMax)
        {
            Point2D[] point2Ds = [new(heatDemandSolve_Origin.X + xMin, heatDemandSolve_Origin.Y + yMin), new(heatDemandSolve_Origin.X + xMax, heatDemandSolve_Origin.Y + yMin), new(heatDemandSolve_Origin.X + xMax, heatDemandSolve_Origin.Y + yMax), new(heatDemandSolve_Origin.X + xMin, heatDemandSolve_Origin.Y + yMax)];
            PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(Geometry.Spatial.Create.Plane(0.0)!, point2Ds);
            Assert.NotNull(polygonalFace3D);

            BuildingModel? buildingModel = GIS.Analytical.Create.BuildingModel(polygonalFace3D, 2, 3.0);
            Assert.NotNull(buildingModel);
            Assert.True(buildingModel.SetValue(GIS.Analytical.Enums.BuildingModelParameter.Reference, reference, new Core.Parameter.Classes.SetValueSettings(true, false)));
            return buildingModel;
        }

        /// <summary>
        /// Builds the single-family 2D building of a rectangle of the fixture.
        /// </summary>
        private static Building2D HeatDemandSolve_Building2D(string reference, double xMin, double yMin, double xMax, double yMax)
        {
            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(new Point2D(heatDemandSolve_Origin.X + xMin, heatDemandSolve_Origin.Y + yMin), new Point2D(heatDemandSolve_Origin.X + xMax, heatDemandSolve_Origin.Y + yMin), new Point2D(heatDemandSolve_Origin.X + xMax, heatDemandSolve_Origin.Y + yMax), new Point2D(heatDemandSolve_Origin.X + xMin, heatDemandSolve_Origin.Y + yMax));
            Assert.NotNull(polygonalFace2D);
            return new Building2D(Guid.NewGuid(), reference, polygonalFace2D, 2, null, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]);
        }

        /// <summary>
        /// Builds a stored result carrying only the values the <c>building_data</c> columns take.
        /// </summary>
        private static BuildingHeatDemandResult HeatDemandSolve_Result(string reference, int countyId, double heatingEnergyNeed, double heatingEnergyNeedPerArea, double designHeatLoad)
        {
            DiGi.Analytical.Building.Energy.Classes.HeatDemandResult heatDemandResult = new(heatingEnergyNeed, heatingEnergyNeedPerArea, null, null, null, null, null, null, null, null, 0, 0, 0, 0, 0, 0, 0, 100, 300, 20, designHeatLoad, designHeatLoad, 0, "method", 1, null, null, 0, 0);
            return new BuildingHeatDemandResult(reference, countyId, 1, WeatherSourceType.EPW, "7", HeatDemandSolarSource.Unshaded, null, null, 0, 0, DiGi.Analytical.Building.Regulation.Enums.ConstructionFlavour.TypicalAsBuilt, "1.7.1", "TABULA", DiGi.Analytical.Building.Regulation.Enums.BuildingType.SingleFamily, 1975, YearBuiltSource.User, "PL-376", null, heatDemandResult);
        }

        /// <summary>
        /// A <see cref="BuildingHeatDemandSolveTask"/> whose reads and write work on lists in memory; every read hands out copies, as a database read does.
        /// </summary>
        private sealed class MemoryBuildingHeatDemandSolveTask : BuildingHeatDemandSolveTask
        {
            private readonly EPWFile ePWFile;

            public MemoryBuildingHeatDemandSolveTask(EPWFile ePWFile)
            {
                this.ePWFile = ePWFile;
            }

            public List<Building2DCentroid> Building2DCentroids { get; } = [];

            public List<Building2D> Building2Ds { get; } = [];

            public List<BuildingModel> BuildingModels { get; } = [];

            public HashSet<string> CompletedReferences { get; } = [];

            public bool Initialized { get; set; } = true;

            public int ReadCount { get; private set; }

            public List<(int CountyId, List<BuildingHeatDemandResult> BuildingHeatDemandResults)> Writes { get; } = [];

            public Dictionary<string, (short? YearBuilt, YearBuiltSource? YearBuiltSource, short? CensusYearBuilt)> YearBuilts { get; } = [];

            public Task<bool> RunAsync(CancellationToken cancellationToken)
            {
                return ExecuteAsync(new Progress<long>(), cancellationToken);
            }

            protected override Task<Dictionary<string, Point2D>?> ReadAddressPointsAsync(int countyId, List<string> references, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<Dictionary<string, Point2D>?>([]);
            }

            protected override Task<List<Building2D?>?> ReadBuilding2DsAsync(int countyId, List<string> references, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<List<Building2D?>?>([.. Building2Ds.Where(x => references.Contains(x.Reference!)).Select(x => new Building2D(x))]);
            }

            protected override Task<List<Building2DCentroid>?> ReadBuilding2DCentroidsAsync(int countyId, int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(countyId == 1 ? [.. Building2DCentroids] : (List<Building2DCentroid>?)null);
            }

            protected override Task<List<BuildingModel?>?> ReadBuildingModelsAsync(int countyId, List<string> references, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<List<BuildingModel?>?>([.. Copies(references)]);
            }

            protected override Task<List<BuildingSolarRadiationResult>?> ReadBuildingSolarRadiationResultsAsync(int countyId, List<string> references, double? radius, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<List<BuildingSolarRadiationResult>?>([]);
            }

            protected override Task<HashSet<string>?> ReadCompletedReferencesAsync(int countyId, int methodVersion, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<HashSet<string>?>([.. CompletedReferences]);
            }

            protected override Task<List<AdministrativeAreal2DReference>?> ReadCountyReferencesAsync(int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                return Task.FromResult<List<AdministrativeAreal2DReference>?>([new() { Id = 2 }, new() { Id = 1 }]);
            }

            protected override Task<EPWFile?> ReadEPWFileAsync(int id, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<EPWFile?>(id == 7 ? ePWFile : null);
            }

            protected override Task<List<(int Id, double X, double Y)>?> ReadEPWFileLocationsAsync(int commandTimeout, CancellationToken cancellationToken)
            {
                ReadCount++;
                return Task.FromResult<List<(int Id, double X, double Y)>?>([(7, heatDemandSolve_Origin.X, heatDemandSolve_Origin.Y)]);
            }

            protected override Task<List<BuildingModel?>?> ReadNeighbourBuildingModelsAsync(Circle2D circle2D, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<List<BuildingModel?>?>([.. Copies(null)]);
            }

            protected override Task<Dictionary<string, OccupancyData>?> ReadOccupancyDatasAsync(int countyId, List<string> references, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<Dictionary<string, OccupancyData>?>([]);
            }

            protected override Task<Dictionary<string, (short? YearBuilt, YearBuiltSource? YearBuiltSource, short? CensusYearBuilt)>?> ReadYearBuiltsAsync(int countyId, List<string> references, int commandTimeout, CancellationToken cancellationToken)
            {
                return Task.FromResult<Dictionary<string, (short? YearBuilt, YearBuiltSource? YearBuiltSource, short? CensusYearBuilt)>?>(YearBuilts.Where(x => references.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value));
            }

            protected override bool TryInitialize()
            {
                return Initialized;
            }

            protected override Task<int?> WriteAsync(int countyId, List<BuildingHeatDemandResult> buildingHeatDemandResults, int commandTimeout, CancellationToken cancellationToken)
            {
                Writes.Add((countyId, [.. buildingHeatDemandResults]));
                return Task.FromResult<int?>(buildingHeatDemandResults.Count);
            }

            private IEnumerable<BuildingModel?> Copies(List<string>? references)
            {
                foreach (BuildingModel buildingModel in BuildingModels)
                {
                    if (references is null || (buildingModel.TryGetValue(GIS.Analytical.Enums.BuildingModelParameter.Reference, out string? reference) && references.Contains(reference!)))
                    {
                        yield return new BuildingModel(buildingModel);
                    }
                }
            }
        }
    }
}
