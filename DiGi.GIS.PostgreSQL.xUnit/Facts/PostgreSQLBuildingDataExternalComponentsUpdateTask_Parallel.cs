using DiGi.Analytical.Building.Enums;
using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway test databases, that <see cref="PostgreSQLBuildingDataExternalComponentsUpdateTask"/> writes the same building data rows and reports the same counters with the batches of a county run one at a time and four at a time (DiGi-Organization/DiGi.GIS.PostgreSQL#154).
        /// <para>A scratch county part is inserted on the main test database and 25 generated models are stored under it on the storage test database; the task runs over that county alone with a batch size of 3 (nine batches), party walls off, once with a degree of 1 and once with 4. The county's partition of the building data table is dropped before the second run, so the second has to create it and write every row itself - two concurrent first pushes used to race on that DDL and fail the county; then every cell and every counter must match. The scratch county, its stored models, its building data rows and both partitions are removed before and after. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PostgreSQLBuildingDataExternalComponentsUpdateTask_Parallel()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection_Main = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection_Main);
            await npgsqlConnection_Main.OpenAsync();

            string reference_County = $"xunit-open-building-{Guid.NewGuid():N}";
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection_Main, reference_County, $"TST{Guid.NewGuid():N}");

                (GISPostgreSQLConverterManager gISPostgreSQLConverterManager, List<string> references) = await ExternalComponents_ScratchAsync(connectionData_Main, connectionData_Storage, countyId.Value, 25);

                PostgreSQLBuildingDataExternalComponentsUpdateTask postgreSQLBuildingDataExternalComponentsUpdateTask_1 = ExternalComponents_Task(gISPostgreSQLConverterManager, countyId.Value, 1);
                await ExternalComponents_RunAsync(postgreSQLBuildingDataExternalComponentsUpdateTask_1);
                List<string> cells_1 = await ExternalComponents_StoredCellsAsync(connectionData_Storage, references, countyId.Value);

                // The partition goes, not only its rows: the parallel run then has to create it, which is where two first
                // pushes racing on the DDL rolled one back and failed the county.
                await ExternalComponents_ExecuteAsync(connectionData_Storage, $"DROP TABLE IF EXISTS {Constants.TableName.BuildingData}_{countyId.Value};");

                PostgreSQLBuildingDataExternalComponentsUpdateTask postgreSQLBuildingDataExternalComponentsUpdateTask_4 = ExternalComponents_Task(gISPostgreSQLConverterManager, countyId.Value, 4);
                await ExternalComponents_RunAsync(postgreSQLBuildingDataExternalComponentsUpdateTask_4);
                List<string> cells_4 = await ExternalComponents_StoredCellsAsync(connectionData_Storage, references, countyId.Value);

                Assert.Equal(25L, postgreSQLBuildingDataExternalComponentsUpdateTask_1.UpdatedRowCount);
                Assert.NotEmpty(cells_1);
                Assert.Equal(cells_1, cells_4);

                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.ProcessedCountyCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.ProcessedCountyCount);
                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.FailedCountyCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.FailedCountyCount);
                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.ProcessedModelCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.ProcessedModelCount);
                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.UpdatedRowCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.UpdatedRowCount);
                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.DegenerateModelCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.DegenerateModelCount);
                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.FailedModelCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.FailedModelCount);
                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.OpenEnvelopeCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.OpenEnvelopeCount);
                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.SkippedComponentCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.SkippedComponentCount);
                Assert.Equal(postgreSQLBuildingDataExternalComponentsUpdateTask_1.SkippedReferenceCount, postgreSQLBuildingDataExternalComponentsUpdateTask_4.SkippedReferenceCount);
            }
            finally
            {
                await ExternalComponents_CleanupAsync(npgsqlConnection_Main, connectionData_Storage, reference_County, countyId);
            }
        }

        /// <summary>
        /// Verifies, on the throwaway test databases, that a stop during a run of <see cref="PostgreSQLBuildingDataExternalComponentsUpdateTask"/> with parallel batches ends the run as cancelled and leaves only whole rows (DiGi-Organization/DiGi.GIS.PostgreSQL#154).
        /// <para>60 generated models under a scratch county, one model per batch, two batches at a time; the first progress report stops the task. The run must report itself cancelled with no exception, fewer rows than models must be stored, and every stored row must carry all of its external components area cells - a batch is written in one transaction, so a stop cannot leave half a row. The scratch data is removed before and after. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PostgreSQLBuildingDataExternalComponentsUpdateTask_Parallel_Stop()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection_Main = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection_Main);
            await npgsqlConnection_Main.OpenAsync();

            string reference_County = $"xunit-open-building-{Guid.NewGuid():N}";
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection_Main, reference_County, $"TST{Guid.NewGuid():N}");

                (GISPostgreSQLConverterManager gISPostgreSQLConverterManager, List<string> references) = await ExternalComponents_ScratchAsync(connectionData_Main, connectionData_Storage, countyId.Value, 60);

                PostgreSQLBuildingDataExternalComponentsUpdateTask postgreSQLBuildingDataExternalComponentsUpdateTask = ExternalComponents_Task(gISPostgreSQLConverterManager, countyId.Value, 2);
                postgreSQLBuildingDataExternalComponentsUpdateTask.PostgreSQLBuildingDataExternalComponentsUpdateOptions.BatchSize = 1;

                TaskCompletionSource<bool> taskCompletionSource = new();
                postgreSQLBuildingDataExternalComponentsUpdateTask.Stopped += (object? sender, EventArgs e) => taskCompletionSource.TrySetResult(true);

                int stopRequested = 0;
                postgreSQLBuildingDataExternalComponentsUpdateTask.ProgressChanged += (object? sender, long value) =>
                {
                    if (System.Threading.Interlocked.Exchange(ref stopRequested, 1) == 0)
                    {
                        _ = postgreSQLBuildingDataExternalComponentsUpdateTask.StopAsync();
                    }
                };

                postgreSQLBuildingDataExternalComponentsUpdateTask.Start();
                await taskCompletionSource.Task;

                Assert.Null(postgreSQLBuildingDataExternalComponentsUpdateTask.Exception);
                Assert.True(postgreSQLBuildingDataExternalComponentsUpdateTask.IsCanceled, $"not cancelled: succeeded {postgreSQLBuildingDataExternalComponentsUpdateTask.IsSucceeded}, {postgreSQLBuildingDataExternalComponentsUpdateTask.UpdatedRowCount} rows, stop requested {stopRequested}");

                // Every stored row is whole: either all of its area cells are written or none (the reference alone
                // comes back for a building no batch reached).
                Table? table = await new BuildingDataPostgreSQLConverter(connectionData_Storage).PullAsync(references, countyId.Value);
                Assert.NotNull(table);

                List<Column> columns_Area = ExternalComponents_AreaColumns(table);
                int rowCount_Whole = 0;
                for (int i = 0; i < table.RowCount; i++)
                {
                    Row? row = table.GetRow(i);
                    Assert.NotNull(row);

                    int cellCount = 0;
                    foreach (Column column in columns_Area)
                    {
                        if (row.TryGetValue(column.Index, out object? value) && value is not null && value is not DBNull)
                        {
                            cellCount++;
                        }
                    }

                    Assert.True(cellCount == 0 || cellCount == columns_Area.Count, $"a stored row carries {cellCount} of {columns_Area.Count} area cells");
                    if (cellCount != 0)
                    {
                        rowCount_Whole++;
                    }
                }

                Assert.True(rowCount_Whole > 0, "no row was written before the stop");
                Assert.True(rowCount_Whole < references.Count, $"{rowCount_Whole} of {references.Count} rows were written - the stop did not take effect.");
            }
            finally
            {
                await ExternalComponents_CleanupAsync(npgsqlConnection_Main, connectionData_Storage, reference_County, countyId);
            }
        }

        /// <summary>
        /// Stores a row of generated models under the scratch county on the storage test database and builds the converter manager the task runs with, wired to the test databases only.
        /// </summary>
        /// <param name="connectionData_Main">The main test database, which holds the counties.</param>
        /// <param name="connectionData_Storage">The storage test database, which holds the stored models and the building data.</param>
        /// <param name="countyId">The identifier of the scratch county part.</param>
        /// <param name="count">The number of models to store.</param>
        /// <returns>The converter manager and the references of the stored models.</returns>
        private static async Task<(GISPostgreSQLConverterManager GISPostgreSQLConverterManager, List<string> References)> ExternalComponents_ScratchAsync(ConnectionData connectionData_Main, ConnectionData connectionData_Storage, int countyId, int count)
        {
            BuildingModelPostgreSQLConverter buildingModelPostgreSQLConverter = new(connectionData_Storage, BuildingModelDetailLevel.Component);

            // The scratch county is new, so nothing may be filed under its identifier yet: a collision would make the
            // fact compare and then delete someone else's rows.
            // A missing table holds nothing; UpdateAsync creates it.
            await using (NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage))
            {
                Assert.NotNull(npgsqlConnection_Storage);
                await npgsqlConnection_Storage.OpenAsync();

                if (await ScalarAsync(npgsqlConnection_Storage, $"SELECT to_regclass('{buildingModelPostgreSQLConverter.TableName}') IS NOT NULL;") is bool tableExists && tableExists)
                {
                    HashSet<string>? references_Existing = await buildingModelPostgreSQLConverter.GetReferencesAsync(countyId);
                    Skip.If(references_Existing is not null && references_Existing.Count != 0, $"The storage test database already holds models under county {countyId}.");
                }
            }

            List<BuildingModel> buildingModels = ExternalComponents_Row(count, countyId);
            Assert.NotNull(await buildingModelPostgreSQLConverter.UpdateAsync(buildingModels));

            List<string> references = [];
            foreach (BuildingModel buildingModel in buildingModels)
            {
                references.Add(buildingModel.Reference!);
            }

            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            Assert.True(gISPostgreSQLConverterManager.Add(new AdministrativeAreal2DPostgreSQLConverter(connectionData_Main)));
            Assert.True(gISPostgreSQLConverterManager.Add(buildingModelPostgreSQLConverter));
            Assert.True(gISPostgreSQLConverterManager.Add(new BuildingDataPostgreSQLConverter(connectionData_Storage)));

            return (gISPostgreSQLConverterManager, references);
        }

        /// <summary>
        /// Builds a task scoped to the scratch county, three references per batch, party walls off, with the given degree of parallelism.
        /// </summary>
        /// <param name="gISPostgreSQLConverterManager">The converter manager wired to the test databases.</param>
        /// <param name="countyId">The identifier of the scratch county part.</param>
        /// <param name="maxDegreeOfParallelism">The degree of parallelism.</param>
        /// <returns>The task, not started.</returns>
        private static PostgreSQLBuildingDataExternalComponentsUpdateTask ExternalComponents_Task(GISPostgreSQLConverterManager gISPostgreSQLConverterManager, int countyId, int maxDegreeOfParallelism)
        {
            return new PostgreSQLBuildingDataExternalComponentsUpdateTask(gISPostgreSQLConverterManager)
            {
                PostgreSQLBuildingDataExternalComponentsUpdateOptions = new PostgreSQLBuildingDataExternalComponentsUpdateOptions()
                {
                    CountyIds = [countyId],
                    BatchSize = 3,
                    PartyWalls = false,
                    MaxDegreeOfParallelism = maxDegreeOfParallelism
                }
            };
        }

        /// <summary>
        /// Starts the task and waits for it to stop, then asserts it completed without an exception and reported success.
        /// </summary>
        /// <param name="postgreSQLBuildingDataExternalComponentsUpdateTask">The task to run.</param>
        /// <returns>A task that completes when the run has finished and its outcome has been asserted.</returns>
        private static async Task ExternalComponents_RunAsync(PostgreSQLBuildingDataExternalComponentsUpdateTask postgreSQLBuildingDataExternalComponentsUpdateTask)
        {
            TaskCompletionSource<bool> taskCompletionSource = new();
            postgreSQLBuildingDataExternalComponentsUpdateTask.Stopped += (object? sender, EventArgs e) => taskCompletionSource.TrySetResult(true);

            postgreSQLBuildingDataExternalComponentsUpdateTask.Start();
            await taskCompletionSource.Task;

            Assert.Null(postgreSQLBuildingDataExternalComponentsUpdateTask.Exception);
            Assert.True(postgreSQLBuildingDataExternalComponentsUpdateTask.IsSucceeded);
        }

        /// <summary>
        /// Finds the external components area columns of a pulled building data table, matched by unique identifier against the columns the task writes, and asserts every one is present.
        /// </summary>
        /// <param name="table">The pulled table.</param>
        /// <returns>The columns of the table, one per external components area column.</returns>
        private static List<Column> ExternalComponents_AreaColumns(Table table)
        {
            HashSet<string> uniqueIds = [];
            foreach (Column column_Expected in IO.Create.Columns_ExternalComponentsArea())
            {
                if (Core.IO.Query.UniqueId(column_Expected) is string uniqueId)
                {
                    uniqueIds.Add(uniqueId);
                }
            }

            List<Column> result = [];
            foreach (Column column in table.Columns)
            {
                if (Core.IO.Query.UniqueId(column) is string uniqueId && uniqueIds.Contains(uniqueId))
                {
                    result.Add(column);
                }
            }

            Assert.NotEmpty(uniqueIds);
            Assert.Equal(uniqueIds.Count, result.Count);

            return result;
        }

        /// <summary>
        /// Reads back every cell of the stored building data rows of the scratch county, in request order, as <c>row|column=value</c>.
        /// </summary>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <param name="references">The references to read.</param>
        /// <param name="countyId">The identifier of the scratch county part.</param>
        /// <returns>One entry per cell.</returns>
        private static async Task<List<string>> ExternalComponents_StoredCellsAsync(ConnectionData connectionData_Storage, List<string> references, int countyId)
        {
            Table? table = await new BuildingDataPostgreSQLConverter(connectionData_Storage).PullAsync(references, countyId);
            Assert.NotNull(table);

            return ExternalComponents_Cells([table]);
        }

        /// <summary>
        /// Runs one statement on a database.
        /// </summary>
        /// <param name="connectionData">The database.</param>
        /// <param name="commandText">The statement.</param>
        /// <returns>A task that completes when the statement has run.</returns>
        private static async Task ExternalComponents_ExecuteAsync(ConnectionData connectionData, string commandText)
        {
            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            await npgsqlCommand.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Removes everything the external components facts wrote: the scratch county partitions of the stored models and of the building data on the storage test database, and the scratch county part on the main test database.
        /// </summary>
        /// <param name="npgsqlConnection_Main">The open connection to the main test database.</param>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <param name="reference_County">The reference of the scratch county part.</param>
        /// <param name="countyId">The identifier of the scratch county part, when one was inserted.</param>
        /// <returns>A task that completes when the cleanup has run.</returns>
        private static async Task ExternalComponents_CleanupAsync(NpgsqlConnection npgsqlConnection_Main, ConnectionData connectionData_Storage, string reference_County, int? countyId)
        {
            if (countyId is int countyId_Temp)
            {
                string tableName_BuildingModel = new BuildingModelPostgreSQLConverter(null, BuildingModelDetailLevel.Component).TableName;

                // Partitions are named after their parent and county (Query.PartitionCommandText); the scratch county is
                // new, so they hold nothing but what the fact wrote.
                await ExternalComponents_ExecuteAsync(connectionData_Storage, string.Format(CultureInfo.InvariantCulture, "DROP TABLE IF EXISTS {0}_{1}; DROP TABLE IF EXISTS {2}_{1};", tableName_BuildingModel, countyId_Temp, Constants.TableName.BuildingData));
            }

            if (await ScalarAsync(npgsqlConnection_Main, $"SELECT to_regclass('{Constants.TableName.AdministrativeAreal2D}') IS NOT NULL;") is bool administrativeExists && administrativeExists)
            {
                await ExecuteAsync(npgsqlConnection_Main, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference = '{reference_County}';");
            }
        }
    }
}
