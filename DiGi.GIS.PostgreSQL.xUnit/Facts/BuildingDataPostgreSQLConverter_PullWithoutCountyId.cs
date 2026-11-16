using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.xUnit.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the reference read of the building-data converter without a county id (DiGi.GIS.PostgreSQL#140): with <c>county_id</c> in the projection it answered a row of nulls for a stored reference, because the seeded row carried <c>county_id = NULL</c> into the merge predicate.
        /// <para>Covers the narrowed projection holding <c>county_id</c>, the full projection (the GET <c>tablebyreference</c> without <c>countyid</c>), a reference stored nowhere (one reference-only row), a reference stored in two county parts (both rows), <c>fallbackByReference</c> from the wrong part, and - unchanged - the county-scoped read and the projection without <c>county_id</c>.</para>
        /// <para>Medium test (a live database server): it seeds the scratch table <c>zz_building_data_scratch</c> on the throwaway storage test database (<c>GIS_PostgreSQL_Storage_Test.conf</c>) and removes it before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingDataPostgreSQLConverter_PullAsync_WithoutCountyId()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            ScratchBuildingDataPostgreSQLConverter converter = new(connectionData_Storage);

            const int countyId_1 = 999994;
            const int countyId_2 = 999993;
            const string reference_Single = "XUNIT-NOCOUNTY-A";
            const string reference_TwoParts = "XUNIT-NOCOUNTY-B";
            const string reference_Missing = "XUNIT-NOCOUNTY-MISSING";

            List<string> columnUniqueIds = ["reference", "storeys", "county_id"];

            await RemoveBuildingDataScratchAsync(connectionData_Storage, converter.TableName);
            try
            {
                Table table = new();
                table.AddColumn(new ExtendedColumn("County Id", typeof(int), null, null));
                table.AddColumn(new ExtendedColumn("Reference", typeof(string), null, null));
                table.AddColumn(new ExtendedColumn("storeys", typeof(int), null, null));
                table.AddRow([countyId_1, reference_Single, 4]);
                table.AddRow([countyId_1, reference_TwoParts, 2]);
                table.AddRow([countyId_2, reference_TwoParts, 7]);
                Assert.True(await converter.PushAsync(table));

                // The defect: county_id projected, no county id given.
                Table? table_Narrowed = await converter.PullAsync([reference_Single], null, columnUniqueIds);
                Assert.Equal([(reference_Single, (int?)countyId_1, (int?)4)], PulledReferenceCountyStoreys(table_Narrowed));

                // Every stored column, as the GET tablebyreference without countyid asks.
                Table? table_Full = await converter.PullAsync([reference_Single], null);
                Assert.Equal([(reference_Single, (int?)countyId_1, (int?)4)], PulledReferenceCountyStoreys(table_Full));

                // A reference stored nowhere keeps its reference-only row, in request order.
                Table? table_Missing = await converter.PullAsync([reference_Missing, reference_Single], null, columnUniqueIds);
                Assert.Equal([(reference_Missing, (int?)null, (int?)null), (reference_Single, (int?)countyId_1, (int?)4)], PulledReferenceCountyStoreys(table_Missing));

                // A reference is unique only per county part: both stored rows are answered.
                Table? table_TwoParts = await converter.PullAsync([reference_TwoParts], null, columnUniqueIds);
                Assert.Equal([(reference_TwoParts, (int?)countyId_2, (int?)7), (reference_TwoParts, (int?)countyId_1, (int?)2)], PulledReferenceCountyStoreys(table_TwoParts).OrderBy(x => x.CountyId).ToList());

                // The fallback from a part that does not hold the reference re-reads it without a county id.
                Table? table_Fallback = await converter.PullAsync([reference_Single], countyId_2, columnUniqueIds, fallbackByReference: true);
                Assert.Equal([(reference_Single, (int?)countyId_1, (int?)4)], PulledReferenceCountyStoreys(table_Fallback));

                // Unchanged: the county-scoped read, and the projection without county_id.
                Table? table_Scoped = await converter.PullAsync([reference_Single], countyId_1, columnUniqueIds);
                Assert.Equal([(reference_Single, (int?)countyId_1, (int?)4)], PulledReferenceCountyStoreys(table_Scoped));

                Table? table_WithoutCountyId = await converter.PullAsync([reference_Single], null, ["reference", "storeys"]);
                Assert.NotNull(table_WithoutCountyId);
                Assert.DoesNotContain(table_WithoutCountyId!.Columns, x => x.UniqueId() == "county_id");
                Assert.Equal([(reference_Single, (int?)null, (int?)4)], PulledReferenceCountyStoreys(table_WithoutCountyId));
            }
            finally
            {
                await RemoveBuildingDataScratchAsync(connectionData_Storage, converter.TableName);
            }
        }

        /// <summary>
        /// Removes a building-data scratch table and its rows in the column catalogue.
        /// </summary>
        /// <param name="connectionData">The connection settings of the throwaway test database.</param>
        /// <param name="tableName">The name of the scratch table.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task RemoveBuildingDataScratchAsync(ConnectionData connectionData, string tableName)
        {
            await DiGi.PostgreSQL.Modify.RemoveTableAsync(connectionData, tableName);

            await using Npgsql.NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
            if (npgsqlConnection is null)
            {
                return;
            }

            await npgsqlConnection.OpenAsync();
            await using Npgsql.NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection);
            npgsqlCommand.Parameters.AddWithValue("tableName", tableName);
            await npgsqlCommand.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Reads the reference, county id and storeys of every row of a pulled building-data table, in row order; a column the table does not hold reads as null.
        /// </summary>
        /// <param name="table">The pulled table.</param>
        /// <returns>One tuple per row.</returns>
        private static List<(string? Reference, int? CountyId, int? Storeys)> PulledReferenceCountyStoreys(Table? table)
        {
            Assert.NotNull(table);

            Column? column_Reference = table!.Columns.FirstOrDefault(x => x.UniqueId() == "reference");
            Column? column_CountyId = table.Columns.FirstOrDefault(x => x.UniqueId() == "county_id");
            Column? column_Storeys = table.Columns.FirstOrDefault(x => x.UniqueId() == "storeys");
            Assert.NotNull(column_Reference);

            List<(string? Reference, int? CountyId, int? Storeys)> result = [];
            foreach (Row row in table.Rows)
            {
                string? reference = row[column_Reference!.Index] as string;
                int? countyId = column_CountyId is not null && row.TryGetValue(column_CountyId.Index, out int countyId_Value) ? countyId_Value : null;
                int? storeys = column_Storeys is not null && row.TryGetValue(column_Storeys.Index, out int storeys_Value) ? storeys_Value : null;
                result.Add((reference, countyId, storeys));
            }

            return result;
        }
    }
}
