using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.xUnit.Classes;
using DiGi.PostgreSQL.Classes;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SortCondition = DiGi.PostgreSQL.Table.Classes.SortCondition;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the ordered, limited filter-group pull of DiGi-Organization/DiGi.GIS.WebAPI#84 through the building-data converter: descending and ascending order with NULL values last in both directions, equal sort values tie-broken by ascending <c>reference</c>, a limit keeping exactly the top rows, and a limit without sort conditions returning the leading references of the deterministic <c>reference</c> order.
        /// <para>Database fact that seeds its own four rows (a two-row tie on the sort value and one NULL) into the scratch table through <see cref="ScratchBuildingDataPostgreSQLConverter"/> and returns without asserting when the local test database configuration is absent. The rows are pushed in an order no assertion expects, so a pass cannot come from insertion order.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataPostgreSQLConverter_PullByFilterGroupAsync_OrderBy_TiesNullsLimit()
        {
            ConnectionData? connectionData = Create.GISPostgreSQLConverterManager()?.GetPostgreSQLConverter<BuildingDataPostgreSQLConverter>()?.ConnectionData;
            if (connectionData is null)
            {
                return;
            }

            ScratchBuildingDataPostgreSQLConverter converter = new(connectionData);

            // Declared by name, as the other building data facts do; the slug (unique id) is the lowercase name.
            Column column_CountyId = new ExtendedColumn("County Id", typeof(int), null, null);
            Column column_Reference = new ExtendedColumn("Reference", typeof(string), null, null);
            Column column_Value = new ExtendedColumn("value", typeof(double), null, null);

            const int countyId = 999997;

            Table table = new();
            table.AddColumn(column_CountyId);
            table.AddColumn(column_Reference);
            table.AddColumn(column_Value);
            table.AddRow([countyId, "XUNIT-ORDER-B", 10.0]);
            table.AddRow([countyId, "XUNIT-ORDER-A", 10.0]);
            table.AddRow([countyId, "XUNIT-ORDER-C", 30.0]);
            table.AddRow([countyId, "XUNIT-ORDER-D", null]);
            Assert.True(await converter.PushAsync(table));

            try
            {
                SortCondition sortCondition_Value_Descending = new() { ColumnUniqueId = "value", Descending = true };
                SortCondition sortCondition_Value_Ascending = new() { ColumnUniqueId = "value", Descending = false };

                // Descending: C (30.0) first, the tie A/B broken by ascending reference, the NULL row last.
                Table? table_Descending = await converter.PullByFilterGroupAsync(null, countyId, ["reference"], orderBy: [sortCondition_Value_Descending]);
                Assert.Equal(new List<string>() { "XUNIT-ORDER-C", "XUNIT-ORDER-A", "XUNIT-ORDER-B", "XUNIT-ORDER-D" }, PulledReferences(table_Descending));

                // Ascending: the tie stays A before B, the NULL row still sorts last.
                Table? table_Ascending = await converter.PullByFilterGroupAsync(null, countyId, ["reference"], orderBy: [sortCondition_Value_Ascending]);
                Assert.Equal(new List<string>() { "XUNIT-ORDER-A", "XUNIT-ORDER-B", "XUNIT-ORDER-C", "XUNIT-ORDER-D" }, PulledReferences(table_Ascending));

                // A limit keeps exactly the top rows of the order.
                Table? table_Limit = await converter.PullByFilterGroupAsync(null, countyId, ["reference"], orderBy: [sortCondition_Value_Descending], limit: 2);
                Assert.Equal(new List<string>() { "XUNIT-ORDER-C", "XUNIT-ORDER-A" }, PulledReferences(table_Limit));

                // A limit without sort conditions is deterministic: the appended reference tie-break orders the rows.
                Table? table_LimitOnly = await converter.PullByFilterGroupAsync(null, countyId, ["reference"], limit: 3);
                Assert.Equal(new List<string>() { "XUNIT-ORDER-A", "XUNIT-ORDER-B", "XUNIT-ORDER-C" }, PulledReferences(table_LimitOnly));

                // A caller sorting by reference gets no duplicate tie-break appended: descending is honoured as sent.
                Table? table_Reference = await converter.PullByFilterGroupAsync(null, countyId, ["reference"], orderBy: [new SortCondition() { ColumnUniqueId = "reference", Descending = true }], limit: 2);
                Assert.Equal(new List<string>() { "XUNIT-ORDER-D", "XUNIT-ORDER-C" }, PulledReferences(table_Reference));

                // The sort value reads back typed, and the NULL row has no value.
                Table? table_Values = await converter.PullByFilterGroupAsync(null, countyId, ["reference", "value"], orderBy: [sortCondition_Value_Descending]);
                Assert.NotNull(table_Values);
                Column? column_Value_Pulled = table_Values!.Columns.FirstOrDefault(x => x.Name == "value");
                Assert.NotNull(column_Value_Pulled);
                Row row_First = table_Values.Rows.First();
                Assert.True(row_First.TryGetValue(column_Value_Pulled!.Index, out double value_First));
                Assert.Equal(30.0, value_First);
                Row row_Last = table_Values.Rows.Last();
                Assert.False(row_Last.TryGetValue(column_Value_Pulled.Index, out double _));
            }
            finally
            {
                await DiGi.PostgreSQL.Modify.RemoveTableAsync(connectionData, converter.TableName);

                await using Npgsql.NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection_Cleanup is not null)
                {
                    await npgsqlConnection_Cleanup.OpenAsync();
                    await using Npgsql.NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection_Cleanup);
                    npgsqlCommand.Parameters.AddWithValue("tableName", converter.TableName);
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Verifies the refusals of the ordered, limited pull of DiGi-Organization/DiGi.GIS.WebAPI#84: a limit below 1 is refused with a null result (never clamped to an unlimited read), and a sort column the stored table does not hold throws <see cref="DiGi.PostgreSQL.Table.Classes.InvalidFilterException"/> naming it, before any statement runs.
        /// <para>Database fact that seeds its own two rows into the scratch table through <see cref="ScratchBuildingDataPostgreSQLConverter"/> and returns without asserting when the local test database configuration is absent.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataPostgreSQLConverter_PullByFilterGroupAsync_OrderBy_Refusals()
        {
            ConnectionData? connectionData = Create.GISPostgreSQLConverterManager()?.GetPostgreSQLConverter<BuildingDataPostgreSQLConverter>()?.ConnectionData;
            if (connectionData is null)
            {
                return;
            }

            ScratchBuildingDataPostgreSQLConverter converter = new(connectionData);

            Column column_CountyId = new ExtendedColumn("County Id", typeof(int), null, null);
            Column column_Reference = new ExtendedColumn("Reference", typeof(string), null, null);

            const int countyId = 999996;

            Table table = new();
            table.AddColumn(column_CountyId);
            table.AddColumn(column_Reference);
            table.AddRow([countyId, "XUNIT-REFUSE-1"]);
            table.AddRow([countyId, "XUNIT-REFUSE-2"]);
            Assert.True(await converter.PushAsync(table));

            try
            {
                Assert.Null(await converter.PullByFilterGroupAsync(null, countyId, ["reference"], limit: 0));
                Assert.Null(await converter.PullByFilterGroupAsync(null, countyId, ["reference"], limit: -1));

                DiGi.PostgreSQL.Table.Classes.InvalidFilterException exception = await Assert.ThrowsAsync<DiGi.PostgreSQL.Table.Classes.InvalidFilterException>(
                    async () => await converter.PullByFilterGroupAsync(null, countyId, ["reference"], orderBy: [new SortCondition() { ColumnUniqueId = "no_such_sort_column", Descending = true }]));
                Assert.Equal("no_such_sort_column", exception.ColumnUniqueId);
            }
            finally
            {
                await DiGi.PostgreSQL.Modify.RemoveTableAsync(connectionData, converter.TableName);

                await using Npgsql.NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection_Cleanup is not null)
                {
                    await npgsqlConnection_Cleanup.OpenAsync();
                    await using Npgsql.NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection_Cleanup);
                    npgsqlCommand.Parameters.AddWithValue("tableName", converter.TableName);
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Reads the reference column of the pulled rows in row order.
        /// </summary>
        /// <param name="table">The pulled table; null answers an empty list so a failed pull asserts on its rows, not on a null dereference.</param>
        /// <returns>The references in the order the read returned them.</returns>
        private static List<string> PulledReferences(Table? table)
        {
            List<string> result = [];
            if (table is null)
            {
                return result;
            }

            Column? column_Reference = table.Columns.FirstOrDefault(x => x.Name == "Reference");
            foreach (Row row in table.Rows)
            {
                result.Add(column_Reference is not null && row.TryGetValue(column_Reference.Index, out string? reference) && reference is not null ? reference : string.Empty);
            }

            return result;
        }
    }
}
