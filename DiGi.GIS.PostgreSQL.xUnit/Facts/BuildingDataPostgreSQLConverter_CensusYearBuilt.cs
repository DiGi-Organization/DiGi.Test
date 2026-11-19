using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.xUnit.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the census year built column through a building-data push and pull (DiGi-Organization/DiGi.GIS.PostgreSQL#148): a pull naming the column before it exists does not fail, the first push of <c>IO.Modify.Update_Building2D_CensusYearBuilt</c> adds the column with the year, and a push with no census year writes NULL over the stored year - a cleared cell reaches the database - while the predicted year of the same row is not touched.
        /// <para>Medium test (a live database server): it seeds the scratch table <c>zz_building_data_scratch</c> on the throwaway storage test database (<c>GIS_PostgreSQL_Storage_Test.conf</c>) and removes it before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingDataPostgreSQLConverter_CensusYearBuilt()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            ScratchBuildingDataPostgreSQLConverter converter = new(connectionData_Storage);

            const int countyId = 999992;
            const string reference = "XUNIT-CENSUS-A";
            string uniqueId_CensusYearBuilt = Core.IO.Query.UniqueId(IO.Constants.Column.CensusYearBuilt)!;
            string uniqueId_PredictedYearBuilt = Core.IO.Query.UniqueId(IO.Constants.Column.PredictedYearBuilt)!;
            string[] columnUniqueIds = ["county_id", "reference", uniqueId_PredictedYearBuilt, uniqueId_CensusYearBuilt];

            GIS.Classes.Building2D building2D = new(Guid.NewGuid(), reference, null, 2, null, null, []);

            await RemoveBuildingDataScratchAsync(connectionData_Storage, converter.TableName);
            try
            {
                Table table_Seed = new();
                Column column_CountyId = table_Seed.AddColumn(IO.Constants.Column.CountyId)!;
                Column column_Reference = table_Seed.AddColumn(IO.Constants.Column.Reference)!;
                Column column_PredictedYearBuilt = table_Seed.AddColumn(IO.Constants.Column.PredictedYearBuilt)!;
                Row row_Seed = table_Seed.AddRow();
                row_Seed[column_CountyId.Index] = countyId;
                row_Seed[column_Reference.Index] = reference;
                row_Seed[column_PredictedYearBuilt.Index] = (ushort)2008;
                Assert.True(await converter.PushAsync(table_Seed));

                // Before the first Statistical run the column does not exist: a pull naming it still answers.
                Assert.Equal(((ushort?)2008, (ushort?)null), BuildingDataPostgreSQLConverter_CensusYearBuilt_Values(await converter.PullAsync([reference], countyId, columnUniqueIds)));

                Table table_Census = new();
                IO.Modify.Update_Building2D_CensusYearBuilt(table_Census, countyId, [building2D], 1961);
                Assert.True(await converter.PushAsync(table_Census));
                Assert.Equal(((ushort?)2008, (ushort?)1961), BuildingDataPostgreSQLConverter_CensusYearBuilt_Values(await converter.PullAsync([reference], countyId, columnUniqueIds)));

                Table table_Cleared = new();
                IO.Modify.Update_Building2D_CensusYearBuilt(table_Cleared, countyId, [building2D], (short?)null);
                Assert.True(await converter.PushAsync(table_Cleared));
                Assert.Equal(((ushort?)2008, (ushort?)null), BuildingDataPostgreSQLConverter_CensusYearBuilt_Values(await converter.PullAsync([reference], countyId, columnUniqueIds)));
            }
            finally
            {
                await RemoveBuildingDataScratchAsync(connectionData_Storage, converter.TableName);
            }
        }

        /// <summary>
        /// Reads the predicted and census years built of the single row of a pulled building-data table; a column the table does not hold, or an empty cell, reads as null.
        /// </summary>
        /// <param name="table">The pulled table.</param>
        /// <returns>The predicted and census years built.</returns>
        private static (ushort? PredictedYearBuilt, ushort? CensusYearBuilt) BuildingDataPostgreSQLConverter_CensusYearBuilt_Values(Table? table)
        {
            Assert.NotNull(table);
            Row row = Assert.Single(table!.Rows);

            Column? column_PredictedYearBuilt = table.Columns.FirstOrDefault(x => x.UniqueId() == Core.IO.Query.UniqueId(IO.Constants.Column.PredictedYearBuilt));
            Column? column_CensusYearBuilt = table.Columns.FirstOrDefault(x => x.UniqueId() == Core.IO.Query.UniqueId(IO.Constants.Column.CensusYearBuilt));

            ushort? predictedYearBuilt = column_PredictedYearBuilt is not null && row.TryGetValue(column_PredictedYearBuilt.Index, out object? value_Predicted) && Core.Query.TryConvert(value_Predicted, out ushort predictedYearBuilt_Value) ? predictedYearBuilt_Value : null;
            ushort? censusYearBuilt = column_CensusYearBuilt is not null && row.TryGetValue(column_CensusYearBuilt.Index, out object? value_Census) && Core.Query.TryConvert(value_Census, out ushort censusYearBuilt_Value) ? censusYearBuilt_Value : null;
            return (predictedYearBuilt, censusYearBuilt);
        }
    }
}
