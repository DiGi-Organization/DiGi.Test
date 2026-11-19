using DiGi.PostgreSQL.Classes;
using Npgsql;
using NpgsqlTypes;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway storage test database, that the poison-row retirement of the OrtoDatas download queue (<see cref="Query.RetirePoisonReferencesCommandText(string)"/>) is served by the index on <c>attempts</c>, not by a sequential scan of the queue (DiGi.GIS.PostgreSQL#145).
        /// <para>The retirement runs at the start of every claim, so a walk of the whole table is paid once per claimed batch: with <c>COALESCE(attempts, 0)</c> in the predicate no index could serve it, the walk outlasted the command timeout on the 5.6-million-row production queue, the claim returned null and fetched nothing, and exhausted entries were never retired. The fact seeds 100 000 ready scratch entries beside five exhausted ones, analyses the table, and runs <c>EXPLAIN (ANALYZE, BUFFERS)</c> for <c>attempts &gt;= 5</c> - the exhausted few must be found through an index without reading the ready rows beside them.</para>
        /// <para>The <c>EXPLAIN ANALYZE</c> executes the DELETE, so it runs inside a transaction that is rolled back: the statement is the shipped one, and nothing stays deleted - neither the scratch entries nor anything the table already held. The plan is written to <c>user files/reports/RetirePoisonReferencesCommandText_Plan.txt</c>, labelled with the database it describes. Scratch entries are removed before and in <c>finally</c>. Medium test (10-20 s, a live database server): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task RetirePoisonReferencesCommandText_Plan()
        {
            const string referencePrefix = "XUNIT-RETIRE-";
            const int countyId = 5;

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_Building2DReference(Constants.TableName.OrtoDatas_Building2DReference_Update));

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.OrtoDatas_Building2DReference_Update} WHERE reference LIKE '{referencePrefix}%';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                await ExecuteAsync(npgsqlConnection, $@"
                    INSERT INTO {Constants.TableName.OrtoDatas_Building2DReference_Update} (county_id, reference, attempts)
                    SELECT {countyId}, '{referencePrefix}ready-' || i, 0
                    FROM generate_series(1, 100000) AS i;");

                await ExecuteAsync(npgsqlConnection, $@"
                    INSERT INTO {Constants.TableName.OrtoDatas_Building2DReference_Update} (county_id, reference, attempts)
                    SELECT {countyId}, '{referencePrefix}exhausted-' || i, 5
                    FROM generate_series(1, 5) AS i;");

                await ExecuteAsync(npgsqlConnection, $"ANALYZE {Constants.TableName.OrtoDatas_Building2DReference_Update};");

                List<string> lines = [];
                await using (NpgsqlTransaction npgsqlTransaction = await npgsqlConnection.BeginTransactionAsync())
                {
                    // The reader and the command are closed before the rollback: Npgsql runs one command at a
                    // time per connection, and a rollback attempted while the EXPLAIN is still being read
                    // fails with NpgsqlOperationInProgressException.
                    await using (NpgsqlCommand npgsqlCommand = new("EXPLAIN (ANALYZE, BUFFERS) " + Query.RetirePoisonReferencesCommandText(Constants.TableName.OrtoDatas_Building2DReference_Update), npgsqlConnection))
                    {
                        npgsqlCommand.Transaction = npgsqlTransaction;
                        npgsqlCommand.Parameters.Add(new NpgsqlParameter("maxAttempts", NpgsqlDbType.Integer) { Value = 5 });

                        await using (NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync())
                        {
                            while (await npgsqlDataReader.ReadAsync())
                            {
                                lines.Add(npgsqlDataReader.GetString(0));
                            }
                        }
                    }

                    await npgsqlTransaction.RollbackAsync();
                }

                string plan = string.Join("\n", lines);

                string directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!;
                StringBuilder stringBuilder = new();
                stringBuilder.AppendLine("Database: the throwaway STORAGE TEST database (GIS_PostgreSQL_Storage_Test.conf), seeded with 100 005 scratch queue entries. Not production.");
                stringBuilder.AppendLine(plan);
                File.WriteAllText(Path.Combine(directory, "RetirePoisonReferencesCommandText_Plan.txt"), stringBuilder.ToString());

                Assert.Contains("Index", plan);
                Assert.DoesNotContain("Seq Scan", plan);
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
