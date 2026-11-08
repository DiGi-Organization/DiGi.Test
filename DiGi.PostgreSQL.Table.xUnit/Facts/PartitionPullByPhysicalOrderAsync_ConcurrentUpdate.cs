using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.xUnit.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace DiGi.PostgreSQL.Table.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies what <c>TablePostgreSQLConverter.PullByPhysicalOrderAsync</c> documents about concurrent writes (DiGi.PostgreSQL#8).
        /// <para>The walk reads its first page, the rows 0-3. A second connection then updates those rows. Each block is nearly full (four rows of ~1.8 kB), so the new versions cannot stay in their block and are written at the end of the table, ahead of the walk.</para>
        /// <para>Without a transaction the walk reads those rows a second time: 29 rows for 25 distinct, which is why callers dedup. Inside one <c>REPEATABLE READ</c> transaction the walk keeps the snapshot of its first page and returns every row exactly once. That also proves the converter's commands join the connection's transaction.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task PartitionPullByPhysicalOrderAsync_ConcurrentUpdate()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            PartitionTablePostgreSQLConverter partitionTablePostgreSQLConverter = new(connectionData);
            string tableName = partitionTablePostgreSQLConverter.TableName;
            string columnUniqueId_1 = PartitionTablePostgreSQLConverter.Column_1.UniqueId()!;
            string columnUniqueId_2 = PartitionTablePostgreSQLConverter.Column_2.UniqueId()!;
            string columnUniqueId_3 = PartitionTablePostgreSQLConverter.Column_3.UniqueId()!;

            try
            {
                // Without a transaction: the rows updated after they were read are read again at their new position.
                await ResetAsync();
                List<int> values_Plain = await WalkWithUpdateAsync(null);
                Assert.Equal(29, values_Plain.Count);
                Assert.Equal(25, new HashSet<int>(values_Plain).Count);
                Assert.All(Enumerable.Range(0, 4), x => Assert.Equal(2, values_Plain.Count(y => y == x)));

                // Inside one REPEATABLE READ transaction: one snapshot for the whole walk, every row once.
                await ResetAsync();
                List<int> values_Snapshot = await WalkWithUpdateAsync(IsolationLevel.RepeatableRead);
                Assert.Equal(25, values_Snapshot.Count);
                Assert.Equal(25, new HashSet<int>(values_Snapshot).Count);
            }
            finally
            {
                await PostgreSQL.Modify.RemoveTableAsync(connectionData, tableName);
                await PostgreSQL.Modify.RemoveTableAsync(connectionData, Constants.TableName.Columns);
            }

            async Task ResetAsync()
            {
                await PostgreSQL.Modify.RemoveTableAsync(connectionData, tableName);

                Core.IO.Table.Classes.Table table = new();
                table.AddColumn(PartitionTablePostgreSQLConverter.Column_1);
                table.AddColumn(PartitionTablePostgreSQLConverter.Column_2);
                table.AddColumn(PartitionTablePostgreSQLConverter.Column_3);
                for (int i = 0; i < 25; i++)
                {
                    table.AddRow([i, "AAA", string.Concat(Enumerable.Range(0, 56).Select(_ => Guid.NewGuid().ToString("N")))]);
                }

                Assert.True(await partitionTablePostgreSQLConverter.PushAsync(table));
            }

            async Task<List<int>> WalkWithUpdateAsync(IsolationLevel? isolationLevel)
            {
                await using NpgsqlConnection? npgsqlConnection_Walk = PostgreSQL.Create.NpgsqlConnection(connectionData);
                await using NpgsqlConnection? npgsqlConnection_Update = PostgreSQL.Create.NpgsqlConnection(connectionData);
                Assert.NotNull(npgsqlConnection_Walk);
                Assert.NotNull(npgsqlConnection_Update);
                await npgsqlConnection_Walk.OpenAsync();
                await npgsqlConnection_Update.OpenAsync();

                await using NpgsqlTransaction? npgsqlTransaction = isolationLevel is null ? null : await npgsqlConnection_Walk.BeginTransactionAsync(isolationLevel.Value);

                List<int> values = [];
                string? position = null;
                int calls = 0;
                while (true)
                {
                    Core.IO.Table.Classes.Table table_Page = new();
                    table_Page.AddColumn(PartitionTablePostgreSQLConverter.Column_1);
                    table_Page.AddColumn(PartitionTablePostgreSQLConverter.Column_2);

                    position = await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection_Walk, table_Page, position, 4, "AAA");
                    calls++;
                    Assert.NotNull(position);

                    for (int i = 0; i < table_Page.RowCount; i++)
                    {
                        values.Add(System.Convert.ToInt32(table_Page[i, 0], CultureInfo.CurrentCulture));
                    }

                    if (calls == 1)
                    {
                        Assert.Equal([0, 1, 2, 3], values);

                        // Rewrite the rows just read; the new versions do not fit in their nearly full block.
                        await using NpgsqlCommand npgsqlCommand_Update = new($"UPDATE \"{tableName}\" SET \"{columnUniqueId_3}\" = \"{columnUniqueId_3}\" || 'x' WHERE \"{columnUniqueId_2}\" = 'AAA' AND \"{columnUniqueId_1}\" < 4", npgsqlConnection_Update);
                        Assert.Equal(4, await npgsqlCommand_Update.ExecuteNonQueryAsync());
                    }

                    if (position.Length == 0)
                    {
                        break;
                    }

                    Assert.True(calls < 100, "The walk did not end.");
                }

                if (npgsqlTransaction is not null)
                {
                    await npgsqlTransaction.CommitAsync();
                }

                return values;
            }
        }
    }
}
