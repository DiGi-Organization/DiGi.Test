using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.xUnit.Classes;
using Npgsql;
using System.Globalization;

namespace DiGi.PostgreSQL.Table.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a row count without a filter answers the scope's row count — the same figure a direct <c>COUNT(*)</c> gives — with no column involved at all.
        /// <para>Implements DiGi-Organization/DiGi.PostgreSQL#14: a filtered row count is a first-class converter method instead of a hand-rolled <c>COUNT("reference")</c> through the aggregate enum.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetCountAsync_NoFilter_MatchesDirectCountStar()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Five rows, two of them with a NULL in the nullable column: the row count is 5 either way.
            await SeedHistogramTableAsync(connectionData, testConverter, [10.0, null, 20.0, null, 30.0]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                await using NpgsqlCommand npgsqlCommand_RowCount = new($"SELECT COUNT(*) FROM \"{testConverter.TableName}\"", npgsqlConnection);
                object? resultValue_RowCount = await npgsqlCommand_RowCount.ExecuteScalarAsync();
                Assert.NotNull(resultValue_RowCount);

                long rowCount = System.Convert.ToInt64(resultValue_RowCount, CultureInfo.CurrentCulture);

                long? countValue = await testConverter.GetCountAsync(npgsqlConnection);

                Assert.NotNull(countValue);
                Assert.Equal(5L, rowCount);
                Assert.Equal(rowCount, countValue.Value);
            }
            finally
            {
                await UnseedHistogramTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that a supplied partition value counts only that partition, while a null partition value counts across all partitions.
        /// <para>Implements DiGi-Organization/DiGi.PostgreSQL#14: the partition semantics match the other partition-scoped reads of the converter.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetCountAsync_PartitionValue_OnlyThatPartition()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            HistogramPartitionTablePostgreSQLConverter testConverter = new(connectionData);

            // County 1 holds two rows, county 2 holds three (one of them NULL-valued): a partition-scoped count must see only its own rows.
            await SeedHistogramPartitionTableAsync(connectionData, testConverter, [(1, 10.0), (1, 20.0), (2, 30.0), (2, null), (2, 40.0)]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                long? countValue_County1 = await testConverter.GetCountAsync(npgsqlConnection, partitionValue: 1);
                long? countValue_County2 = await testConverter.GetCountAsync(npgsqlConnection, partitionValue: 2);
                long? countValue_All = await testConverter.GetCountAsync(npgsqlConnection);

                Assert.NotNull(countValue_County1);
                Assert.NotNull(countValue_County2);
                Assert.NotNull(countValue_All);
                Assert.Equal(2L, countValue_County1.Value);
                Assert.Equal(3L, countValue_County2.Value);
                Assert.Equal(countValue_County1.Value + countValue_County2.Value, countValue_All.Value);
            }
            finally
            {
                await UnseedHistogramPartitionTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that a filter matching every row answers the same count as the no-filter call.
        /// <para>Implements DiGi-Organization/DiGi.PostgreSQL#14: a valid <c>FilterGroup</c> composes with the partition scope exactly like on the other filtered reads.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetCountAsync_FilterAllRows_SameAsNoFilter()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Three non-NULL rows: the IsNotNull filter matches every row, so the filtered and unfiltered counts must agree.
            await SeedHistogramTableAsync(connectionData, testConverter, [1.0, 2.0, 3.0]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                PostgreSQL.Table.Classes.FilterGroup filterGroup = new()
                {
                    FilterConditions =
                    [
                        new PostgreSQL.Table.Classes.FilterCondition
                        {
                            ColumnUniqueId = "value",
                            FilterOperator = PostgreSQL.Table.Enums.FilterOperator.IsNotNull
                        }
                    ]
                };

                long? countValue_NoFilter = await testConverter.GetCountAsync(npgsqlConnection);
                long? countValue_Filtered = await testConverter.GetCountAsync(npgsqlConnection, filterGroup);

                Assert.NotNull(countValue_NoFilter);
                Assert.NotNull(countValue_Filtered);
                Assert.Equal(3L, countValue_NoFilter.Value);
                Assert.Equal(countValue_NoFilter.Value, countValue_Filtered.Value);
            }
            finally
            {
                await UnseedHistogramTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that a filter matching no row answers <c>0</c> — an empty match is an answer, not a failure.
        /// <para>Implements DiGi-Organization/DiGi.PostgreSQL#14: <c>0</c> is never <c>null</c>, so the caller can distinguish "no rows matched" from "the count could not be determined".</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetCountAsync_FilterNoMatch_ReturnsZero()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // No row holds the compared value.
            await SeedHistogramTableAsync(connectionData, testConverter, [1.0, 2.0, 3.0]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                PostgreSQL.Table.Classes.FilterGroup filterGroup = new()
                {
                    FilterConditions =
                    [
                        new PostgreSQL.Table.Classes.FilterCondition
                        {
                            ColumnUniqueId = "value",
                            FilterOperator = PostgreSQL.Table.Enums.FilterOperator.Equals,
                            Value = 9999.0
                        }
                    ]
                };

                long? countValue = await testConverter.GetCountAsync(npgsqlConnection, filterGroup);

                Assert.NotNull(countValue);
                Assert.Equal(0L, countValue.Value);
            }
            finally
            {
                await UnseedHistogramTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that a filter naming a column the stored table does not hold is the same typed failure as on every other filtered read.
        /// <para>Implements DiGi-Organization/DiGi.PostgreSQL#14 on the failure side: the validation is the #12 <c>InvalidFilterException</c> naming the column, not a second mechanism and not <c>null</c>.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetCountAsync_UnknownFilterColumn_ThrowsInvalidFilterException()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // A scratch table with its column metadata exists (so the stored-column whitelist can be consulted); the filter simply names a column that is not on it.
            await SeedHistogramTableAsync(connectionData, testConverter, [1.0]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                PostgreSQL.Table.Classes.FilterGroup filterGroup = new()
                {
                    FilterConditions =
                    [
                        new PostgreSQL.Table.Classes.FilterCondition
                        {
                            ColumnUniqueId = "no_such_column",
                            FilterOperator = PostgreSQL.Table.Enums.FilterOperator.Equals,
                            Value = 1
                        }
                    ]
                };

                PostgreSQL.Table.Classes.InvalidFilterException exception = await Assert.ThrowsAsync<PostgreSQL.Table.Classes.InvalidFilterException>(() => testConverter.GetCountAsync(npgsqlConnection, filterGroup));

                Assert.Equal("no_such_column", exception.ColumnUniqueId);
                Assert.Equal(PostgreSQL.Table.Enums.FilterOperator.Equals, exception.FilterOperator);
            }
            finally
            {
                await UnseedHistogramTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that a row with a NULL in a nullable column is still counted — the answer is <c>COUNT(*)</c>, not <c>COUNT(column)</c>.
        /// <para>Implements DiGi-Organization/DiGi.PostgreSQL#14: on the same fixture the count answers 5 while the aggregate <c>Count</c> on the nullable column answers 3, so the two APIs are provably different.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetCountAsync_NullCell_RowStillCounted()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Three non-NULL values interleaved with two NULLs: the row count must stay 5 while COUNT(column) answers 3.
            await SeedHistogramTableAsync(connectionData, testConverter, [10.0, null, 20.0, null, 30.0]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                long? countValue = await testConverter.GetCountAsync(npgsqlConnection);
                System.Text.Json.Nodes.JsonNode? jsonNode_Count = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count);

                Assert.NotNull(countValue);
                Assert.NotNull(jsonNode_Count);
                Assert.Equal(5L, countValue.Value);
                Assert.Equal(3L, jsonNode_Count.GetValue<long>());
                Assert.NotEqual(countValue.Value, jsonNode_Count.GetValue<long>());
            }
            finally
            {
                await UnseedHistogramTableAsync(connectionData, testConverter);
            }
        }
    }
}
