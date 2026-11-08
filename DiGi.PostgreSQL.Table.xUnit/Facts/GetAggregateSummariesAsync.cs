using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.xUnit.Classes;
using Npgsql;
using System.Globalization;

namespace DiGi.PostgreSQL.Table.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the extracted single-value aggregate expression map (DiGi-Organization/DiGi.PostgreSQL#15): every <see cref="DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction"/> value produces its exact SQL expression, and an unmapped value is rejected. The single-measure and the batch paths share this one map, so this fact is the regression guard for both.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Fact]
        public void GetAggregateSummariesAsync_BuildSinglevalueAggregateExpression_ExactSql()
        {
            Assert.Equal("AVG(\"column_1\")", DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.BuildSinglevalueAggregateExpression("column_1", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Avg));
            Assert.Equal("SUM(\"column_1\")", DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.BuildSinglevalueAggregateExpression("column_1", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Sum));
            Assert.Equal("MIN(\"column_1\")", DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.BuildSinglevalueAggregateExpression("column_1", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Min));
            Assert.Equal("MAX(\"column_1\")", DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.BuildSinglevalueAggregateExpression("column_1", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Max));
            Assert.Equal("COUNT(\"column_1\")", DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.BuildSinglevalueAggregateExpression("column_1", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count));
            Assert.Equal("COUNT(DISTINCT \"column_1\")", DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.BuildSinglevalueAggregateExpression("column_1", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.DistinctCount));

            Assert.Throws<System.ComponentModel.InvalidEnumArgumentException>(() => DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.BuildSinglevalueAggregateExpression("column_1", (DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction)999));
        }

        /// <summary>
        /// Verifies the batch SELECT list (DiGi-Organization/DiGi.PostgreSQL#15): one <c>SELECT</c> carries every requested measure's expression, each exactly once, in request order — the measures are requested out of alphabetical column order on purpose.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Fact]
        public void GetAggregateSummariesAsync_BuildAggregateSummariesSelectList_ContainsAllExpressionsInOrder()
        {
            List<DiGi.PostgreSQL.Table.Classes.AggregateMeasure> measures =
            [
                new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("column_c", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Min),
                new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("column_a", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Avg),
                new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("column_b", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.DistinctCount)
            ];

            string selectList = DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.BuildAggregateSummariesSelectList(measures);

            Assert.Equal("MIN(\"column_c\"), AVG(\"column_a\"), COUNT(DISTINCT \"column_b\")", selectList);
        }

        /// <summary>
        /// Verifies the request validation of the batch aggregate (DiGi-Organization/DiGi.PostgreSQL#15): a null measure list and an empty one are rejected with typed exceptions before any database access — distinct from a <c>null</c> result, which in this layer means a declined read.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Fact]
        public async Task GetAggregateSummariesAsync_NullOrEmptyMeasures_Throws()
        {
            NpgsqlConnection npgsqlConnection = new("host=localhost");
            BaseTablePostgreSQLConverter testConverter = new(null);

            await Assert.ThrowsAsync<ArgumentNullException>(() => testConverter.GetAggregateSummariesAsync(npgsqlConnection, null!));
            await Assert.ThrowsAsync<ArgumentException>(() => testConverter.GetAggregateSummariesAsync(npgsqlConnection, []));
        }

        /// <summary>
        /// Verifies the measure-count cap of the batch aggregate (DiGi-Organization/DiGi.PostgreSQL#15): more than the named maximum measures in one call is rejected with <see cref="ArgumentOutOfRangeException"/> before any database access.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [Fact]
        public async Task GetAggregateSummariesAsync_MeasuresAboveCap_Throws()
        {
            NpgsqlConnection npgsqlConnection = new("host=localhost");
            BaseTablePostgreSQLConverter testConverter = new(null);

            int maxCount = DiGi.PostgreSQL.Table.Classes.TablePostgreSQLConverter<Core.IO.Table.Classes.Column>.MaxAggregateSummariesMeasureCount;

            List<DiGi.PostgreSQL.Table.Classes.AggregateMeasure> measures = [];
            for (int i = 0; i <= maxCount; i++)
            {
                measures.Add(new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("column_" + i.ToString(CultureInfo.CurrentCulture), DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count));
            }

            ArgumentOutOfRangeException exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => testConverter.GetAggregateSummariesAsync(npgsqlConnection, measures));

            Assert.Equal("measures", exception.ParamName);
        }

        /// <summary>
        /// Verifies the core contract of the batch aggregate (DiGi-Organization/DiGi.PostgreSQL#15): one call returns all requested measures, with values identical to the equivalent single-measure calls, in request order — the measures are requested out of alphabetical column order on purpose.
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummariesAsync_ThreeMeasures_MatchSingleMeasureCalls()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            // Sparse columns on purpose: AVG and DISTINCT COUNT skip the NULLs, COUNT counts only the non-NULL cells.
            await SeedTwoValueColumnsTableAsync(connectionData, testConverter, [1, null, 3, 4], [10.5, null, 20.5, 30.5]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                List<DiGi.PostgreSQL.Table.Classes.AggregateMeasure> measures =
                [
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("value_b", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Avg),
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("value_a", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count),
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("value_b", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.DistinctCount)
                ];

                System.Text.Json.Nodes.JsonNode? jsonNode_Batch = await testConverter.GetAggregateSummariesAsync(npgsqlConnection, measures);

                System.Text.Json.Nodes.JsonArray? jsonArray_Batch = jsonNode_Batch as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(jsonArray_Batch);
                Assert.Equal(3, jsonArray_Batch.Count);

                System.Text.Json.Nodes.JsonNode? jsonNode_Avg = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value_b", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Avg);
                System.Text.Json.Nodes.JsonNode? jsonNode_Count = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value_a", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count);
                System.Text.Json.Nodes.JsonNode? jsonNode_DistinctCount = await testConverter.GetAggregateSummaryAsync<Core.IO.Table.Classes.Column>(npgsqlConnection, "value_b", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.DistinctCount);

                AssertSameValue(jsonNode_Avg, jsonArray_Batch[0]);
                AssertSameValue(jsonNode_Count, jsonArray_Batch[1]);
                AssertSameValue(jsonNode_DistinctCount, jsonArray_Batch[2]);
            }
            finally
            {
                await UnseedTwoValueColumnsTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies the whitelist contract of the batch aggregate (DiGi-Organization/DiGi.PostgreSQL#15): a measure naming a column the stored table does not hold fails the whole call with the typed <see cref="DiGi.PostgreSQL.Table.Classes.InvalidFilterException"/> naming that column — no partial result, even when the other measures are valid.
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummariesAsync_UnknownColumn_FailsWholeCall()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            await SeedTwoValueColumnsTableAsync(connectionData, testConverter, [1, 2, 3], [10.5, 20.5, 30.5]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                List<DiGi.PostgreSQL.Table.Classes.AggregateMeasure> measures =
                [
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("value_a", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count),
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("no_such_column", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Avg)
                ];

                DiGi.PostgreSQL.Table.Classes.InvalidFilterException exception = await Assert.ThrowsAsync<DiGi.PostgreSQL.Table.Classes.InvalidFilterException>(() => testConverter.GetAggregateSummariesAsync(npgsqlConnection, measures));

                Assert.Equal("no_such_column", exception.ColumnUniqueId);
                Assert.Null(exception.FilterOperator);
            }
            finally
            {
                await UnseedTwoValueColumnsTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies the null-preservation contract of the batch aggregate (DiGi-Organization/DiGi.PostgreSQL#15): over a scope with zero matching rows, <c>AVG</c> is JSON <c>null</c> while <c>COUNT</c> is 0 — each measure keeps its own empty-set semantics inside one shared array.
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummariesAsync_EmptyScope_AvgIsNullCountIsZero()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            BaseTablePostgreSQLConverter testConverter = new(connectionData);

            await SeedTwoValueColumnsTableAsync(connectionData, testConverter, [1, 2, 3], [10.5, 20.5, 30.5]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                // A comparison no seeded row satisfies: the scope is empty without touching the rows.
                DiGi.PostgreSQL.Table.Classes.FilterGroup filterGroup = new()
                {
                    FilterConditions =
                    [
                        new DiGi.PostgreSQL.Table.Classes.FilterCondition
                        {
                            ColumnUniqueId = "value_a",
                            FilterOperator = DiGi.PostgreSQL.Table.Enums.FilterOperator.GreaterThan,
                            Value = 1000
                        }
                    ]
                };

                List<DiGi.PostgreSQL.Table.Classes.AggregateMeasure> measures =
                [
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("value_b", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Avg),
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("value_a", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count)
                ];

                System.Text.Json.Nodes.JsonNode? jsonNode_Batch = await testConverter.GetAggregateSummariesAsync(npgsqlConnection, measures, filterGroup: filterGroup);

                System.Text.Json.Nodes.JsonArray? jsonArray_Batch = jsonNode_Batch as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(jsonArray_Batch);
                Assert.Equal(2, jsonArray_Batch.Count);

                // AVG over zero rows is SQL NULL, preserved as JSON null; COUNT over zero rows is 0.
                Assert.Null(jsonArray_Batch[0]?.GetValue<object?>());
                Assert.Equal(0L, jsonArray_Batch[1]?.GetValue<long>() ?? -1L);
            }
            finally
            {
                await UnseedTwoValueColumnsTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Verifies the partition scope of the batch aggregate (DiGi-Organization/DiGi.PostgreSQL#15): a supplied partition value aggregates only that partition, and a null partition value spans all partitions — matching the partition semantics of the other partition-scoped reads of the converter.
        /// <para>Figures describe the development database resolved from <c>user files/PostgreSQL_Table.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task GetAggregateSummariesAsync_PartitionValue_OnlyThatPartition()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(PostgreSQL.Enums.StorageMethod.Table, out ConnectionData? connectionData))
            {
                return;
            }

            HistogramPartitionTablePostgreSQLConverter testConverter = new(connectionData);

            // County 1 holds two rows (one of them NULL-valued), county 2 holds three: a partition-scoped aggregate must see only its own rows.
            await SeedHistogramPartitionTableAsync(connectionData, testConverter, [(1, 10.0), (1, null), (2, 20.0), (2, 30.0), (2, 40.0)]);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection is null)
                {
                    return;
                }

                await npgsqlConnection.OpenAsync();

                List<DiGi.PostgreSQL.Table.Classes.AggregateMeasure> measures =
                [
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("value", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Avg),
                    new DiGi.PostgreSQL.Table.Classes.AggregateMeasure("value", DiGi.PostgreSQL.Table.Enums.SinglevalueAggregateFunction.Count)
                ];

                // County 1: AVG(10.0) = 10, COUNT = 1 (the NULL-valued row is skipped by both).
                System.Text.Json.Nodes.JsonArray? jsonArray_County1 = await testConverter.GetAggregateSummariesAsync(npgsqlConnection, measures, partitionValue: 1) as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(jsonArray_County1);
                Assert.Equal(10.0, jsonArray_County1[0]?.GetValue<double>() ?? -1.0);
                Assert.Equal(1L, jsonArray_County1[1]?.GetValue<long>() ?? -1L);

                // County 2: AVG(20.0, 30.0, 40.0) = 30, COUNT = 3.
                System.Text.Json.Nodes.JsonArray? jsonArray_County2 = await testConverter.GetAggregateSummariesAsync(npgsqlConnection, measures, partitionValue: 2) as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(jsonArray_County2);
                Assert.Equal(30.0, jsonArray_County2[0]?.GetValue<double>() ?? -1.0);
                Assert.Equal(3L, jsonArray_County2[1]?.GetValue<long>() ?? -1L);

                // All partitions: AVG(10.0, 20.0, 30.0, 40.0) = 25, COUNT = 4.
                System.Text.Json.Nodes.JsonArray? jsonArray_All = await testConverter.GetAggregateSummariesAsync(npgsqlConnection, measures) as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(jsonArray_All);
                Assert.Equal(25.0, jsonArray_All[0]?.GetValue<double>() ?? -1.0);
                Assert.Equal(4L, jsonArray_All[1]?.GetValue<long>() ?? -1L);
            }
            finally
            {
                await UnseedHistogramPartitionTableAsync(connectionData, testConverter);
            }
        }

        /// <summary>
        /// Creates the basetable scratch table (an int primary key plus the <c>value_a</c> int and <c>value_b</c> double columns under test) and seeds the supplied values, dropping any leftovers first so every fact starts from the same state.
        /// </summary>
        /// <param name="connectionData">The connection data for the development database.</param>
        /// <param name="testConverter">The scratch-table converter to seed through.</param>
        /// <param name="valueA">The <c>value_a</c> column entries; a null entry is stored as a database NULL.</param>
        /// <param name="valueB">The <c>value_b</c> column entries; a null entry is stored as a database NULL.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private static async Task SeedTwoValueColumnsTableAsync(ConnectionData connectionData, BaseTablePostgreSQLConverter testConverter, int?[] valueA, double?[] valueB)
        {
            await PostgreSQL.Modify.RemoveTableAsync(connectionData, testConverter.TableName);
            await PostgreSQL.Modify.RemoveTableAsync(connectionData, Constants.TableName.Columns);

            Core.IO.Table.Classes.Table table = new();
            table.AddColumn("Column_1", typeof(int));
            table.AddColumn("value_a", typeof(int));
            table.AddColumn("value_b", typeof(double));

            int key = 1;
            for (int i = 0; i < valueA.Length; i++)
            {
                table.AddRow([key, valueA[i], valueB[i]]);
                key++;
            }

            Assert.True(await testConverter.PushAsync(table));
        }

        /// <summary>
        /// Removes the basetable scratch table and its columns metadata after a GetAggregateSummariesAsync fact, restoring the development database state.
        /// </summary>
        /// <param name="connectionData">The connection data for the development database.</param>
        /// <param name="testConverter">The scratch-table converter whose table is removed.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        private static async Task UnseedTwoValueColumnsTableAsync(ConnectionData connectionData, BaseTablePostgreSQLConverter testConverter)
        {
            await PostgreSQL.Modify.RemoveTableAsync(connectionData, testConverter.TableName);
            await PostgreSQL.Modify.RemoveTableAsync(connectionData, Constants.TableName.Columns);
        }

        /// <summary>
        /// Asserts that two scalar aggregate results carry the same JSON value (both <c>null</c> counts as equal).
        /// </summary>
        /// <param name="expected">The expected scalar result, from the single-measure call.</param>
        /// <param name="actual">The actual scalar result, one element of the batch array.</param>
        private static void AssertSameValue(System.Text.Json.Nodes.JsonNode? expected, System.Text.Json.Nodes.JsonNode? actual)
        {
            Assert.Equal(expected?.ToJsonString(), actual?.ToJsonString());
        }
    }
}
