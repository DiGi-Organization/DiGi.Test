using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.xUnit.Classes;
using Npgsql;
using System.Globalization;

namespace DiGi.PostgreSQL.Table.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that SinglevalueAggregateFunction.Count answers the number of non-null cells of the requested column instead of the scope's row count, on a partially-NULL column where the two answers differ.
        /// <para>Reproduces DiGi-Organization/DiGi.PostgreSQL#9: the singlevalue switch built COUNT(*), so a sparse column reported the row count of the scope and every requested column answered the same number.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_Count_SparseColumn_CountsNonNullCells()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Three non-NULL values interleaved with two NULLs: COUNT(column) must answer 3 while the scope holds 5 rows.
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

                System.Text.Json.Nodes.JsonNode? jsonNode_Count = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count);

                Assert.NotNull(jsonNode_Count);
                Assert.Equal(5L, rowCount);
                Assert.Equal(3L, jsonNode_Count.GetValue<long>());
                Assert.NotEqual(rowCount, jsonNode_Count.GetValue<long>());
            }
            finally
            {
                await UnseedHistogramTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// A target column the stored table does not hold is a typed failure naming the column, distinct from a valid empty result (DiGi-Organization/DiGi.PostgreSQL#12). On the unmodified code the same call answered <c>null</c>, indistinguishable from an empty partition.
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_UnknownTargetColumn_Throws()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // A scratch table with its column metadata exists (so the stored-column whitelist can be consulted); the requested column is simply not on it.
            await SeedHistogramTableAsync(connectionData, testConverter, [1.0]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                DiGi.PostgreSQL.Table.Classes.InvalidFilterException exception = await Assert.ThrowsAsync<DiGi.PostgreSQL.Table.Classes.InvalidFilterException>(() => testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "no_such_column", PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count));

                Assert.Equal("no_such_column", exception.ColumnUniqueId);
                Assert.Null(exception.FilterOperator);
            }
            finally
            {
                await UnseedHistogramTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that MultivalueAggregateFunction.SplitValueDistribution groups a non-text integer column by its own value and counts the occurrences of each distinct value, instead of failing.
        /// <para>Reproduces DiGi-Organization/DiGi.PostgreSQL#11: the unmodified SplitValueDistribution ran string_to_array on the raw column, so an integer column threw and the caller answered 500; casting the column to text makes the endpoint work for every column type.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitValueDistribution_IntColumn()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Distinct counts with no tie: 1 occurs once, 2 twice, 3 three times, so the ORDER BY count DESC order is deterministic.
            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(int), [1, 2, 2, 3, 3, 3]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                System.Text.Json.Nodes.JsonNode? jsonNode_Result = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution);

                AssertDistribution(jsonNode_Result, [("3", 3L), ("2", 2L), ("1", 1L)]);
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that MultivalueAggregateFunction.SplitValueDistribution groups a non-text boolean column by its own value and counts the occurrences of each distinct value, instead of failing.
        /// <para>Reproduces DiGi-Organization/DiGi.PostgreSQL#11: the unmodified SplitValueDistribution ran string_to_array on the raw column, so a boolean column threw and the caller answered 500; casting the column to text makes the endpoint work for every column type.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitValueDistribution_BoolColumn()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(bool), [true, true, false]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                System.Text.Json.Nodes.JsonNode? jsonNode_Result = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution);

                AssertDistribution(jsonNode_Result, [("true", 2L), ("false", 1L)]);
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that MultivalueAggregateFunction.SplitValueDistribution on a text column still answers the group-by count of its values, unchanged by the non-text fix (the ::text cast is a no-op on text).
        /// <para>Regression guard for DiGi-Organization/DiGi.PostgreSQL#11: a single-value text column such as municipality_name groups by its own value; the cast must leave the text-column document byte-identical to the pre-change output.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitValueDistribution_TextColumn()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Single-value text column (the municipality_name scenario): each row contributes its whole value as one item.
            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(string), ["Apple", "Banana", "Banana"]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                System.Text.Json.Nodes.JsonNode? jsonNode_Result = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution);

                AssertDistribution(jsonNode_Result, [("Banana", 2L), ("Apple", 1L)]);
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that MultivalueAggregateFunction.SplitDistinctCount counts the distinct values of a non-text integer column, instead of failing.
        /// <para>Reproduces DiGi-Organization/DiGi.PostgreSQL#11 on the sibling SplitDistinctCount branch: the unmodified branch ran string_to_array on the raw column, so an integer column threw and the caller answered 500; casting the column to text makes it work for every column type.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitDistinctCount_IntColumn()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Three distinct values (1, 2, 3); the distinct count must answer 3.
            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(int), [1, 2, 2, 3, 3, 3]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                System.Text.Json.Nodes.JsonNode? jsonNode_Result = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitDistinctCount);

                Assert.NotNull(jsonNode_Result);
                Assert.Equal(3L, jsonNode_Result.GetValue<long>());
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that <see cref="PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution"/> is bounded by construction: a scope holding more distinct values than <c>maxGroups</c> returns exactly <c>maxGroups + 1</c> groups - the top <c>maxGroups</c> by count plus one overflow marker row - so the caller detects truncation by the result being longer than <c>maxGroups</c> and drops the last element.
        /// <para>Reproduces DiGi-Organization/DiGi.PostgreSQL#16: before the cap a high-cardinality column returned one row per distinct value with no bound. The five distinct values carry distinct counts (5, 4, 3, 2, 1), so the <c>ORDER BY count DESC</c> order and the overflow row are deterministic.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitValueDistribution_MaxGroups_TruncatesWithOverflowRow()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Five distinct values with distinct counts (a:5, b:4, c:3, d:2, e:1) so the ORDER BY count DESC order and the overflow row are deterministic.
            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(string), ["a", "a", "a", "a", "a", "b", "b", "b", "b", "c", "c", "c", "d", "d", "e"]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                // The default cap (1000) is above the five distinct values: the full distribution comes back, untruncated.
                System.Text.Json.Nodes.JsonNode? jsonNode_Full = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution);
                AssertDistribution(jsonNode_Full, [("a", 5L), ("b", 4L), ("c", 3L), ("d", 2L), ("e", 1L)]);

                // maxGroups 3 is below the five distinct values: the cap is hit, so exactly 3 + 1 groups come back, the last being the overflow marker (the true 4th-most-frequent value "d").
                System.Text.Json.Nodes.JsonNode? jsonNode_Truncated = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution, maxGroups: 3);
                System.Text.Json.Nodes.JsonArray? jsonArray_Truncated = jsonNode_Truncated as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(jsonArray_Truncated);
                Assert.Equal(3 + 1, jsonArray_Truncated.Count);
                Assert.True(jsonArray_Truncated.Count > 3);
                AssertDistribution(jsonNode_Truncated, [("a", 5L), ("b", 4L), ("c", 3L), ("d", 2L)]);
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that a distribution with fewer distinct values than <c>maxGroups</c> is returned in full and unchanged - no overflow marker, no reordering - so a cap that is not reached leaves the response byte-identical to today.
        /// <para>Regression guard for DiGi-Organization/DiGi.PostgreSQL#16: the bound must never truncate a distribution that already fits under it.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitValueDistribution_MaxGroups_BelowCap_IsComplete()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Three distinct values with distinct counts (a:3, b:2, c:1); the cap (1000) is far above them, so nothing is truncated.
            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(string), ["a", "a", "a", "b", "b", "c"]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                // maxGroups 1000 is above the three distinct values: the full distribution comes back, with no overflow marker (exactly 3 groups, not 4).
                System.Text.Json.Nodes.JsonNode? jsonNode_Result = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution, maxGroups: 1000);
                System.Text.Json.Nodes.JsonArray? jsonArray_Result = jsonNode_Result as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(jsonArray_Result);
                Assert.Equal(3, jsonArray_Result.Count);
                AssertDistribution(jsonNode_Result, [("a", 3L), ("b", 2L), ("c", 1L)]);
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies the floor of the documented range: <c>maxGroups</c> of 1 is accepted and, on a scope with more than one distinct value, returns exactly 1 + 1 groups - the single most-frequent value plus the overflow marker - so the truncation signal still fires at the minimum.
        /// <para>Reproduces DiGi-Organization/DiGi.PostgreSQL#16 at the lower bound of the documented range.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitValueDistribution_MaxGroups_AtFloorOne()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Two distinct values with distinct counts (a:2, b:1); the floor maxGroups 1 is hit, so 1 + 1 groups come back.
            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(string), ["a", "a", "b"]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                System.Text.Json.Nodes.JsonNode? jsonNode_Result = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution, maxGroups: 1);
                System.Text.Json.Nodes.JsonArray? jsonArray_Result = jsonNode_Result as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(jsonArray_Result);
                Assert.Equal(1 + 1, jsonArray_Result.Count);
                AssertDistribution(jsonNode_Result, [("a", 2L), ("b", 1L)]);
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that a <c>maxGroups</c> outside the documented range [1, 1000] is a typed failure naming the parameter, distinct from a valid result - 0 below the floor and 1001 above the ceiling both throw <see cref="ArgumentOutOfRangeException"/>.
        /// <para>Reproduces DiGi-Organization/DiGi.PostgreSQL#16: the ceiling is a hard bound the caller cannot raise, and the floor is enforced by the same guard as the neighbouring caps.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitValueDistribution_MaxGroups_OutOfRange_Throws()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // The guard fires before any column whitelist is consulted, so the seeded value is irrelevant; it is present so the converter holds a valid table.
            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(string), ["a"]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                ArgumentOutOfRangeException exception_Below = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution, maxGroups: 0));
                Assert.Equal("maxGroups", exception_Below.ParamName);

                ArgumentOutOfRangeException exception_Above = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitValueDistribution, maxGroups: 1001));
                Assert.Equal("maxGroups", exception_Above.ParamName);
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies that <see cref="PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitDistinctCount"/> - the scalar sibling of <c>SplitValueDistribution</c> - is unaffected by <c>maxGroups</c>: the parameter is accepted but ignored, and the distinct count is answered as before.
        /// <para>Regression guard for DiGi-Organization/DiGi.PostgreSQL#16: the cap must not leak into the sibling branch that returns a single value.</para>
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummaryAsync_SplitDistinctCount_MaxGroups_Ignored()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Three distinct values (a, b, c); the scalar distinct count must answer 3 whatever in-range maxGroups is passed.
            await SeedMultivalueColumnTableAsync(connectionData, testConverter, "value", typeof(string), ["a", "a", "b", "b", "c"]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                System.Text.Json.Nodes.JsonNode? jsonNode_Result = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value", PostgreSQL.Table.Enums.MultivalueAggregateFunction.SplitDistinctCount, maxGroups: 100);

                Assert.NotNull(jsonNode_Result);
                Assert.Equal(3L, jsonNode_Result.GetValue<long>());
            }
            finally
            {
                await UnseedMultivalueColumnTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Creates the basetable scratch table (the int primary key Column_1 plus one column under test of the supplied type) and seeds the supplied values, dropping any leftovers first so every fact starts from the same state.
        /// </summary>
        /// <param name="connectionData">The connection data for the development database.</param>
        /// <param name="testConverter">The scratch-table converter to seed through.</param>
        /// <param name="columnName">The name of the column under test.</param>
        /// <param name="columnType">The .NET type of the column under test (int, bool or string).</param>
        /// <param name="values">The column entries to seed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private static async Task SeedMultivalueColumnTableAsync(ConnectionData connectionData, BaseTablePostgreSQLConverter testConverter, string columnName, Type columnType, object?[] values)
        {
            await PostgreSQL.Modify.RemoveTableAsync(connectionData, testConverter.TableName);
            await PostgreSQL.Modify.RemoveTableAsync(connectionData, Constants.TableName.Columns);

            Core.IO.Table.Classes.Table table = new();
            table.AddColumn("Column_1", typeof(int));
            table.AddColumn(columnName, columnType);

            int key = 1;
            foreach (object? value in values)
            {
                table.AddRow([key, value]);
                key++;
            }

            Assert.True(await testConverter.PushAsync(table));
        }

        /// <summary>
        /// Removes the basetable scratch table and its columns metadata after a SplitValueDistribution fact, restoring the development database state.
        /// </summary>
        /// <param name="connectionData">The connection data for the development database.</param>
        /// <param name="testConverter">The scratch-table converter whose table is removed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private static async Task UnseedMultivalueColumnTableAsync(ConnectionData connectionData, BaseTablePostgreSQLConverter testConverter)
        {
            await PostgreSQL.Modify.RemoveTableAsync(connectionData, testConverter.TableName);
            await PostgreSQL.Modify.RemoveTableAsync(connectionData, Constants.TableName.Columns);
        }

        /// <summary>
        /// Asserts that a SplitValueDistribution result is a JSON array of {item, count} objects matching the expected ordered items.
        /// </summary>
        /// <param name="jsonNode_Result">The result to assert.</param>
        /// <param name="expectedItems">The expected (item, count) pairs in result order.</param>
        private static void AssertDistribution(System.Text.Json.Nodes.JsonNode? jsonNode_Result, (string item, long count)[] expectedItems)
        {
            System.Text.Json.Nodes.JsonArray? jsonArray_Result = jsonNode_Result as System.Text.Json.Nodes.JsonArray;
            Assert.NotNull(jsonArray_Result);
            Assert.Equal(expectedItems.Length, jsonArray_Result.Count);

            int index = 0;
            foreach ((string item, long count) expectedItem in expectedItems)
            {
                System.Text.Json.Nodes.JsonObject? jsonObject_Item = jsonArray_Result[index] as System.Text.Json.Nodes.JsonObject;
                Assert.NotNull(jsonObject_Item);

                string? item_Value = jsonObject_Item["item"]?.GetValue<string>();
                long count_Value = jsonObject_Item["count"]?.GetValue<long>() ?? -1L;
                Assert.Equal(expectedItem.item, item_Value);
                Assert.Equal(expectedItem.count, count_Value);
                index++;
            }
        }
    }
}
