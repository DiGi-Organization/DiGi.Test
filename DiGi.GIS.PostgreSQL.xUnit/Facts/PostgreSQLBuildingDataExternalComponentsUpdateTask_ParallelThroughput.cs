using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Parameter.Classes;
using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Measures the whole run of <see cref="PostgreSQLBuildingDataExternalComponentsUpdateTask"/> - read of the batch, neighbour reads, classification and upsert - over a dense and a sparse scratch county of 600 generated models each, at a degree of 1, 2, 4 and 8, on the throwaway test databases (DiGi-Organization/DiGi.GIS.PostgreSQL#153).
        /// <para>Opt-in: skipped unless the environment variable <c>DIGI_PARALLEL_THROUGHPUT</c> carries a value, because it is a measurement rather than a guard - it writes two scratch counties with their outlines, stored models and building data.</para>
        /// <para>The dense county stands its cubes in one touching row, so with party walls on every batch also reads its neighbours' outlines and models and every run is asserted to have found shared walls - a figure from a run that measured nothing would say nothing. The sparse county stands the same cubes 20 m apart, so no two share a wall and the run is the read, the classification and the write of a rural county. Every run drops the county's building data partition first, so each writes all of its rows and its first push - the one that creates the partition - is inside the measured window; one run at degree 1 comes before the measurement, so the connection pools and the first partition DDL are not billed to the slowest figure. The county polygon covers the row it holds because the neighbour search prunes to the counties whose polygon covers the box, and a county that does not cover it finds no neighbour at all - silently, which is what the asserted party wall counts are there to catch. The figures and the peak working set of the process go to a report in the reports directory; the scratch data is removed before and after each county.</para>
        /// <para>Medium test (10-11 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long, and only with DIGI_PARALLEL_THROUGHPUT set.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PostgreSQLBuildingDataExternalComponentsUpdateTask_ParallelThroughput()
        {
            Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DIGI_PARALLEL_THROUGHPUT")), "Opt-in measurement: set DIGI_PARALLEL_THROUGHPUT to any value to run it.");

            const int count = 600;

            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection_Main = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection_Main);
            await npgsqlConnection_Main.OpenAsync();

            List<string> reportLines = [];

            (string Label, double Gap)[] configurations =
            [
                ("dense", 0.0),
                ("sparse", 20.0)
            ];

            try
            {
                foreach ((string label, double gap) in configurations)
                {
                    string reference_County = $"xunit-external-throughput-{Guid.NewGuid():N}";
                    int? countyId = null;

                    try
                    {
                        // The row runs from 0 to about 4 800 m along X plus the gaps between the cubes, and from 0 to
                        // 10 m along Y; the county has to cover that much of the plane, or the neighbour search - which
                        // prunes to the counties whose polygon covers the box - finds nothing.
                        countyId = await ExternalComponents_ThroughputCountyIdAsync(npgsqlConnection_Main, reference_County, $"TST{Guid.NewGuid():N}", (4800.0 * (count / 600.0)) + (gap * (count - 1)));

                        (GISPostgreSQLConverterManager gISPostgreSQLConverterManager, List<string> references) = await ExternalComponents_ThroughputScratchAsync(connectionData_Main, connectionData_Storage, countyId.Value, gap);

                        // The outlines of the cubes, on the main database, at the coordinates the models were built
                        // at: the task finds the neighbours by outline, not by model, and it orders the batches along
                        // the Z-order curve of the outlines it read for the county.
                        Building2DPostgreSQLConverter building2DPostgreSQLConverter = new(connectionData_Main);
                        Assert.True(gISPostgreSQLConverterManager.Add(building2DPostgreSQLConverter));

                        List<Building2D> building2Ds = [];
                        double x = 0.0;
                        for (int i = 0; i < references.Count; i++)
                        {
                            double size = 6.0 + (i % 5);

                            DiGi.GIS.Classes.Building2D building2D_GIS = new(Guid.NewGuid(), references[i], PolygonalFace2D_Square(x, 0.0, size), 1, null, null, []);
                            Building2D? building2D = building2D_GIS.ToPostgreSQL($"TST{Guid.NewGuid():N}");
                            Assert.NotNull(building2D);
                            building2D.CountyId = countyId.Value;
                            building2Ds.Add(building2D);

                            x += size + gap;
                        }

                        Assert.NotNull(await building2DPostgreSQLConverter.UpdateAsync(building2Ds));

                        if (gap == 0.0)
                        {
                            // Once, before the measurement: the connection pools of both databases and the very first
                            // partition DDL would otherwise sit inside the degree-1 figure alone.
                            await ExternalComponents_ThroughputRunAsync(connectionData_Storage, gISPostgreSQLConverterManager, countyId.Value, 1, count);
                        }

                        foreach (int degree in new int[] { 1, 2, 4, 8 })
                        {
                            (PostgreSQLBuildingDataExternalComponentsUpdateTask postgreSQLBuildingDataExternalComponentsUpdateTask, TimeSpan elapsed) = await ExternalComponents_ThroughputRunAsync(connectionData_Storage, gISPostgreSQLConverterManager, countyId.Value, degree, count);

                            // The dense cubes touch along the whole row and the sparse ones stand 20 m apart, so the
                            // two counts are decided by the geometry either way: a dense run that found no shared wall
                            // measured nothing, and a sparse run that found one measured something that is not there.
                            if (gap == 0.0)
                            {
                                Assert.True(postgreSQLBuildingDataExternalComponentsUpdateTask.PartyWallBuildingCount > 0, $"degree {degree}: no building was found to share a wall, so the dense run measured no party wall at all.");
                            }
                            else
                            {
                                Assert.Equal(0L, postgreSQLBuildingDataExternalComponentsUpdateTask.PartyWallBuildingCount);
                            }

                            Assert.Equal(0L, postgreSQLBuildingDataExternalComponentsUpdateTask.PartyWallFailedBatchCount);
                            Assert.Equal(0L, postgreSQLBuildingDataExternalComponentsUpdateTask.MissingNeighbourModelCount);

                            reportLines.Add(string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}, degree {1}: {2:0.00} s, {3:0.0} models/s, {4} rows, party walls {5} buildings, missing neighbour models {6}, failed party wall batches {7}, peak working set {8:0} MB",
                                label,
                                degree,
                                elapsed.TotalSeconds,
                                count / elapsed.TotalSeconds,
                                postgreSQLBuildingDataExternalComponentsUpdateTask.UpdatedRowCount,
                                postgreSQLBuildingDataExternalComponentsUpdateTask.PartyWallBuildingCount,
                                postgreSQLBuildingDataExternalComponentsUpdateTask.MissingNeighbourModelCount,
                                postgreSQLBuildingDataExternalComponentsUpdateTask.PartyWallFailedBatchCount,
                                Process.GetCurrentProcess().PeakWorkingSet64 / (1024.0 * 1024.0)));
                        }
                    }
                    finally
                    {
                        await ExternalComponents_CleanupAsync(npgsqlConnection_Main, connectionData_Storage, reference_County, countyId);

                        if (countyId is int countyId_Temp)
                        {
                            // The outlines are written here, so they are removed here too; the partition is named
                            // after its parent and county, and it exists only because this fact created it.
                            await ExternalComponents_ExecuteAsync(connectionData_Main, $"DROP TABLE IF EXISTS {Constants.TableName.Building2D}_{countyId_Temp};");
                        }
                    }
                }
            }
            finally
            {
                string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
                if (!string.IsNullOrWhiteSpace(directory) && reportLines.Count != 0)
                {
                    File.WriteAllLines(Path.Combine(directory!, "ExternalComponentsParallelThroughput.txt"), reportLines);
                }
            }
        }

        /// <summary>
        /// Drops the building data partition of the scratch county, runs the task over it at the given degree of parallelism and returns the stopped task with the seconds the run took - dropping the partition first makes the run write all of its rows and puts its first push, the one that creates the partition, inside the measured window.
        /// </summary>
        /// <param name="connectionData_Storage">The storage test database, which holds the building data.</param>
        /// <param name="gISPostgreSQLConverterManager">The converter manager wired to the test databases and carrying the outlines.</param>
        /// <param name="countyId">The identifier of the scratch county part.</param>
        /// <param name="maxDegreeOfParallelism">The degree of parallelism.</param>
        /// <param name="expectedRowCount">The rows the run has to write.</param>
        /// <returns>The stopped task and the elapsed seconds of its run.</returns>
        private static async Task<(PostgreSQLBuildingDataExternalComponentsUpdateTask Task, TimeSpan Elapsed)> ExternalComponents_ThroughputRunAsync(ConnectionData connectionData_Storage, GISPostgreSQLConverterManager gISPostgreSQLConverterManager, int countyId, int maxDegreeOfParallelism, int expectedRowCount)
        {
            await ExternalComponents_ExecuteAsync(connectionData_Storage, $"DROP TABLE IF EXISTS {Constants.TableName.BuildingData}_{countyId};");

            PostgreSQLBuildingDataExternalComponentsUpdateTask postgreSQLBuildingDataExternalComponentsUpdateTask = new(gISPostgreSQLConverterManager)
            {
                PostgreSQLBuildingDataExternalComponentsUpdateOptions = new PostgreSQLBuildingDataExternalComponentsUpdateOptions()
                {
                    CountyIds = [countyId],
                    BatchSize = 50,
                    PartyWalls = true,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism
                }
            };

            Stopwatch stopwatch = Stopwatch.StartNew();
            await ExternalComponents_RunAsync(postgreSQLBuildingDataExternalComponentsUpdateTask);
            stopwatch.Stop();

            Assert.Equal((long)expectedRowCount, postgreSQLBuildingDataExternalComponentsUpdateTask.UpdatedRowCount);
            Assert.Equal(0L, postgreSQLBuildingDataExternalComponentsUpdateTask.FailedCountyCount);

            return (postgreSQLBuildingDataExternalComponentsUpdateTask, stopwatch.Elapsed);
        }

        /// <summary>
        /// Inserts a scratch county part whose polygon covers the row of cubes the throughput fact generates, and returns its identifier.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="reference">The guid-based reference of the row.</param>
        /// <param name="code">The guid-based code of the row.</param>
        /// <param name="extent_X">The end of the row along X, which the county polygon has to cover.</param>
        /// <returns>The identifier of the inserted county part.</returns>
        private static async Task<int> ExternalComponents_ThroughputCountyIdAsync(NpgsqlConnection npgsqlConnection, string reference, string code, double extent_X)
        {
            Assert.True(await ScalarAsync(npgsqlConnection, $"SELECT to_regclass('{Constants.TableName.AdministrativeAreal2D}') IS NOT NULL;") is bool administrativeExists && administrativeExists, "the administrative table is missing on the throwaway main test database.");

            // A previous run of this fact cleans up after itself, but a run that was killed leaves its county behind.
            await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference LIKE 'xunit-external-throughput-%';");

            double min_X = -1000.0;
            double min_Y = -500.0;
            double size = extent_X - (2.0 * min_X);

            IPolygonal2D_Square(min_X, min_Y, size, out Geometry.Planar.Classes.Polygon2D polygon2D);

            Geometry.Planar.Classes.PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(polygonalFace2D);

            GIS.Classes.AdministrativeDivision administrativeDivision = new(Guid.NewGuid(), reference, code, polygonalFace2D, GIS.Enums.AdministrativeDivisionType.county, "xunit external components throughput county");

            string? objectJson = administrativeDivision.ToJsonObject()?.ToJsonString();
            Assert.NotNull(objectJson);

            string commandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO {0} (reference, code, name, type_id, min_x, min_y, max_x, max_y, object) VALUES ('{1}', '{2}', 'xunit external components throughput county', {3}, {4}, {5}, {6}, {7}, '{8}') RETURNING id;",
                Constants.TableName.AdministrativeAreal2D,
                reference,
                code,
                (int)Enums.AdministrativeArealType.County,
                min_X,
                min_Y,
                min_X + size,
                min_Y + size,
                objectJson);

            object? result = await ScalarAsync(npgsqlConnection, commandText);
            Assert.NotNull(result);

            return System.Convert.ToInt32(result, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Stores the row of generated models under the scratch county on the storage test database and builds the converter manager the task runs with, wired to the test databases only.
        /// </summary>
        /// <param name="connectionData_Main">The main test database, which holds the counties and the outlines.</param>
        /// <param name="connectionData_Storage">The storage test database, which holds the stored models and the building data.</param>
        /// <param name="countyId">The identifier of the scratch county part.</param>
        /// <param name="gap">The distance left between the cubes: 0 makes them touch, which is what a dense county does.</param>
        /// <returns>The converter manager and the references of the stored models.</returns>
        private static async Task<(GISPostgreSQLConverterManager GISPostgreSQLConverterManager, List<string> References)> ExternalComponents_ThroughputScratchAsync(ConnectionData connectionData_Main, ConnectionData connectionData_Storage, int countyId, double gap)
        {
            BuildingModelPostgreSQLConverter buildingModelPostgreSQLConverter = new(connectionData_Storage, BuildingModelDetailLevel.Component);

            List<BuildingModel> buildingModels = ExternalComponents_SpacedRow(600, countyId, gap);
            Assert.NotNull(await buildingModelPostgreSQLConverter.UpdateAsync(buildingModels));

            List<string> references = [];
            foreach (BuildingModel buildingModel in buildingModels)
            {
                Assert.NotNull(buildingModel.Reference);
                references.Add(buildingModel.Reference!);
            }

            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            Assert.True(gISPostgreSQLConverterManager.Add(new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main)));
            Assert.True(gISPostgreSQLConverterManager.Add(buildingModelPostgreSQLConverter));
            Assert.True(gISPostgreSQLConverterManager.Add(new BuildingDataPostgreSQLConverter(connectionData_Storage)));

            return (gISPostgreSQLConverterManager, references);
        }

        /// <summary>
        /// Builds a row of cubes along the X axis with the same sizes as <see cref="ExternalComponents_Row(int, int)"/>, but standing <paramref name="gap"/> metres apart from one another, so that no two share a wall.
        /// </summary>
        /// <param name="count">The number of cubes.</param>
        /// <param name="countyId">The county identifier the models are stored under.</param>
        /// <param name="gap">The distance left between two neighbouring cubes.</param>
        /// <returns>The storage envelopes, referenced <c>ref_row_0</c> onwards.</returns>
        private static List<BuildingModel> ExternalComponents_SpacedRow(int count, int countyId, double gap)
        {
            List<BuildingModel> result = [];

            double x = 0.0;
            for (int i = 0; i < count; i++)
            {
                double size = 6.0 + (i % 5);

                IComponent[] components =
                [
                    Wall(new Point3D(x, 0, size), new Vector3D(-1, 0, 0), size),
                    Wall(new Point3D(x + size, size, size), new Vector3D(1, 0, 0), size),
                    Wall(new Point3D(x + size, 0, size), new Vector3D(0, -1, 0), size),
                    Wall(new Point3D(x, size, size), new Vector3D(0, 1, 0), size),
                    Floor(new Point3D(x, size, 0), new Vector3D(0, 0, -1), size),
                    Roof(new Point3D(x, 0, size), new Vector3D(0, 0, 1), size)
                ];

                DiGi.Analytical.Building.Classes.BuildingModel buildingModel = new();
                DiGi.Analytical.Building.Classes.Space space = new(new Point3D(x + (size / 2.0), size / 2.0, size / 2.0), "Space 1");
                Assert.True(buildingModel.Update(space));

                foreach (IComponent component in components)
                {
                    Assert.True(buildingModel.Assign(component, space));
                }

                Assert.True(buildingModel.SetValue(BuildingModelParameter.Reference, "ref_row_" + i.ToString(CultureInfo.InvariantCulture), new SetValueSettings(true, false)));

                BuildingModel? envelope = buildingModel.ToPostgreSQL(countyId);
                Assert.NotNull(envelope);
                result.Add(envelope);

                x += size + gap;
            }

            return result;
        }
    }
}
