using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.xUnit.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DiGi.PostgreSQL.Table.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <c>TablePostgreSQLConverter.PullByPhysicalOrderAsync</c> walks one partition page by page in physical order and returns every row of that partition exactly once (DiGi.PostgreSQL#8).
        /// <para>The rows carry about 1.8 kB of incompressible text each, so a partition spans several heap blocks and a page covers more than one window. After a first walk, rows in the middle of the partition are deleted and the partition is walked again. The windows over the emptied blocks then come back short, and the page has to continue into the next window instead of ending early.</para>
        /// <para>The walk must also keep out the rows of the sibling partition, return <see cref="string.Empty"/> only on the last call, and decline with <see langword="null"/> for a missing partition value, a non-positive page size or a position that is not a valid tid. The partition is walked once before it is analysed too, when the window is sized from a row count. A partition value with no rows is an exhausted partition, not a failure.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task PartitionPullByPhysicalOrderAsyncCheck()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            PartitionTablePostgreSQLConverter partitionTablePostgreSQLConverter = new(connectionData);

            Core.IO.Table.Classes.Table table = new();
            table.AddColumn(PartitionTablePostgreSQLConverter.Column_1);
            table.AddColumn(PartitionTablePostgreSQLConverter.Column_2);
            table.AddColumn(PartitionTablePostgreSQLConverter.Column_3);
            table.AddColumn(PartitionTablePostgreSQLConverter.Column_4);

            for (int i = 0; i < 25; i++)
            {
                table.AddRow([i, "AAA", Padding(), i % 2 == 0]);
            }

            for (int i = 0; i < 10; i++)
            {
                table.AddRow([100 + i, "BBB", Padding(), true]);
            }

            try
            {
                Assert.True(await partitionTablePostgreSQLConverter.PushAsync(table));

                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();

                // Before any ANALYZE the partition reports no statistics and the window is sized from a row count.
                (List<int> values_Unanalysed, int calls_Unanalysed) = await WalkAsync(npgsqlConnection, 4);
                Assert.Equal(7, calls_Unanalysed);
                Assert.Equal(25, new HashSet<int>(values_Unanalysed).Count);

                // Statistics make the window size follow the real row density (a few rows per block).
                await using (NpgsqlCommand npgsqlCommand_Analyze = new($"ANALYZE \"{partitionTablePostgreSQLConverter.TableName}\"", npgsqlConnection))
                {
                    await npgsqlCommand_Analyze.ExecuteNonQueryAsync();
                }

                // A full walk with 4-row pages: 6 full pages, then the 25th row on a short last page.
                (List<int> values_Walk, int calls_Walk) = await WalkAsync(npgsqlConnection, 4);
                Assert.Equal(7, calls_Walk);
                Assert.Equal(25, values_Walk.Count);
                Assert.Equal(25, new HashSet<int>(values_Walk).Count);
                Assert.All(values_Walk, x => Assert.InRange(x, 0, 24));

                // A page larger than the partition: one call, already exhausted.
                (List<int> values_Single, int calls_Single) = await WalkAsync(npgsqlConnection, 100);
                Assert.Equal(1, calls_Single);
                Assert.Equal(25, values_Single.Count);

                // Empty the middle of the partition: the windows over those blocks come back short and the page must continue past them.
                await using (NpgsqlCommand npgsqlCommand_Delete = new($"DELETE FROM \"{partitionTablePostgreSQLConverter.TableName}\" WHERE \"{PartitionTablePostgreSQLConverter.Column_2.UniqueId()}\" = 'AAA' AND \"{PartitionTablePostgreSQLConverter.Column_1.UniqueId()}\" BETWEEN 4 AND 15", npgsqlConnection))
                {
                    Assert.Equal(12, await npgsqlCommand_Delete.ExecuteNonQueryAsync());
                }

                (List<int> values_Sparse, int calls_Sparse) = await WalkAsync(npgsqlConnection, 4);
                Assert.Equal(13, values_Sparse.Count);
                Assert.Equal(13, new HashSet<int>(values_Sparse).Count);
                Assert.DoesNotContain(values_Sparse, x => x >= 4 && x <= 15);
                Assert.Equal(4, calls_Sparse);

                // A partition value with no rows is exhausted, not a failure.
                Core.IO.Table.Classes.Table table_Missing = NewTable();
                Assert.Equal(string.Empty, await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, table_Missing, null, 4, "ZZZ"));
                Assert.Equal(0, table_Missing.RowCount);

                // Declined without querying.
                Assert.Null(await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, NewTable(), "abc", 4, "AAA"));
                Assert.Null(await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, NewTable(), "(1,2", 4, "AAA"));
                Assert.Null(await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, NewTable(), "(1,70000)", 4, "AAA"));
                Assert.Null(await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, NewTable(), "(4294967296,1)", 4, "AAA"));
                Assert.Equal(string.Empty, await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, NewTable(), "(4294967295,65535)", 4, "AAA"));
                Assert.Null(await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, NewTable(), null, 0, "AAA"));
                Assert.Null(await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, NewTable(), null, 4, null));
                Assert.Null(await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync<Core.IO.Table.Classes.Column, Core.IO.Table.Classes.Row>(npgsqlConnection, null, null, 4, "AAA"));

                async Task<(List<int> Values, int Calls)> WalkAsync(NpgsqlConnection npgsqlConnection_Walk, int pageSize)
                {
                    List<int> values = [];
                    string? position = null;
                    int calls = 0;
                    while (true)
                    {
                        Core.IO.Table.Classes.Table table_Page = NewTable();
                        position = await partitionTablePostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection_Walk, table_Page, position, pageSize, "AAA");
                        calls++;

                        Assert.NotNull(position);
                        Assert.True(table_Page.RowCount <= pageSize);

                        for (int i = 0; i < table_Page.RowCount; i++)
                        {
                            Assert.Equal("AAA", table_Page[i, 1]);
                            values.Add(System.Convert.ToInt32(table_Page[i, 0], CultureInfo.CurrentCulture));
                        }

                        if (position.Length == 0)
                        {
                            return (values, calls);
                        }

                        // Only a full page may hand out a position.
                        Assert.Equal(pageSize, table_Page.RowCount);
                        Assert.True(calls < 100, "The walk did not end.");
                    }
                }
            }
            finally
            {
                await PostgreSQL.Modify.RemoveTableAsync(connectionData, partitionTablePostgreSQLConverter.TableName);
                await PostgreSQL.Modify.RemoveTableAsync(connectionData, Constants.TableName.Columns);
            }

            static Core.IO.Table.Classes.Table NewTable()
            {
                Core.IO.Table.Classes.Table result = new();
                result.AddColumn(PartitionTablePostgreSQLConverter.Column_1);
                result.AddColumn(PartitionTablePostgreSQLConverter.Column_2);
                return result;
            }

            // 1 792 hex characters: under the 2 kB threshold at which PostgreSQL starts compressing or moving a value out of
            // line, so each row keeps its full size in the heap and a handful of rows fill a block.
            static string Padding()
            {
                return string.Concat(Enumerable.Range(0, 56).Select(_ => Guid.NewGuid().ToString("N")));
            }
        }
    }
}
