using DiGi.BDL.Enums;
using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.Classes;
using System;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Update_Building2D_CensusYearBuilt writes one year to every given building of the county; a second call overwrites the year without adding rows, and a row of another county with the same reference is left alone (DiGi-Organization/DiGi.GIS.IO#19).
        /// </summary>
        [Fact]
        public void Update_Building2D_CensusYearBuilt()
        {
            Building2D building2D_1 = new(Guid.NewGuid(), "b_ref_001", null, 2, null, null, []);
            Building2D building2D_2 = new(Guid.NewGuid(), "b_ref_002", null, 1, null, null, []);

            Table table = new();
            IO.Modify.Update_Building2D_CensusYearBuilt(table, 2212, [building2D_1], 1930);
            IO.Modify.Update_Building2D_CensusYearBuilt(table, 1465, [building2D_1, building2D_2], 1961);

            Assert.Equal(3, table.RowCount);
            Assert.Equal((ushort?)1930, Update_Building2D_CensusYearBuilt_Value(table, 2212, "b_ref_001"));
            Assert.Equal((ushort?)1961, Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_001"));
            Assert.Equal((ushort?)1961, Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_002"));

            IO.Modify.Update_Building2D_CensusYearBuilt(table, 1465, [building2D_1, building2D_2], 1994);

            Assert.Equal(3, table.RowCount);
            Assert.Equal((ushort?)1930, Update_Building2D_CensusYearBuilt_Value(table, 2212, "b_ref_001"));
            Assert.Equal((ushort?)1994, Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_001"));
            Assert.Equal((ushort?)1994, Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_002"));
        }

        /// <summary>
        /// A null census year leaves the cell empty, never 0: on a new row, and on a row an earlier call wrote a year to.
        /// </summary>
        [Fact]
        public void Update_Building2D_CensusYearBuilt_Null()
        {
            Building2D building2D_1 = new(Guid.NewGuid(), "b_ref_001", null, 2, null, null, []);
            Building2D building2D_2 = new(Guid.NewGuid(), "b_ref_002", null, 1, null, null, []);

            Table table = new();
            IO.Modify.Update_Building2D_CensusYearBuilt(table, 1465, [building2D_1], 1961);
            IO.Modify.Update_Building2D_CensusYearBuilt(table, 1465, [building2D_1, building2D_2], (short?)null);

            Assert.Equal(2, table.RowCount);
            Assert.Null(Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_001"));
            Assert.Null(Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_002"));
        }

        /// <summary>
        /// The statistical data collection overload writes what GIS.Query.CensusYearBuilt returns for the same collection, and clears the cell for a null collection or one without census counts.
        /// </summary>
        [Fact]
        public void Update_Building2D_CensusYearBuilt_StatisticalDataCollection()
        {
            Building2D building2D = new(Guid.NewGuid(), "b_ref_001", null, 2, null, null, []);

            StatisticalDataCollection statisticalDataCollection = CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_1945_1970_census_2021, 800),
                (Variable.inhabited_dwellings_built_1971_1978_census_2021, 100),
                (Variable.inhabited_dwellings_built_1989_2002_census_2021, 100));
            short? censusYearBuilt = Query.CensusYearBuilt(statisticalDataCollection);
            Assert.Equal((short?)1961, censusYearBuilt);

            Table table = new();
            IO.Modify.Update_Building2D_CensusYearBuilt(table, 1465, [building2D], statisticalDataCollection);
            Assert.Equal((ushort?)1961, Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_001"));

            IO.Modify.Update_Building2D_CensusYearBuilt(table, 1465, [building2D], CensusYearBuilt_StatisticalDataCollection((Variable.inhabited_dwellings_total_census_2021, 1000)));
            Assert.Null(Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_001"));

            IO.Modify.Update_Building2D_CensusYearBuilt(table, 1465, [building2D], statisticalDataCollection);
            IO.Modify.Update_Building2D_CensusYearBuilt(table, 1465, [building2D], (StatisticalDataCollection?)null);
            Assert.Null(Update_Building2D_CensusYearBuilt_Value(table, 1465, "b_ref_001"));
        }

        private static ushort? Update_Building2D_CensusYearBuilt_Value(Table table, int countyId, string reference)
        {
            Column? column_Reference = table.Columns?.FirstOrDefault(x => x.Name == IO.Constants.Column.Reference.Name);
            Column? column_CountyId = table.Columns?.FirstOrDefault(x => x.Name == IO.Constants.Column.CountyId.Name);
            Column? column_CensusYearBuilt = table.Columns?.FirstOrDefault(x => x.Name == IO.Constants.Column.CensusYearBuilt.Name);
            Assert.NotNull(column_Reference);
            Assert.NotNull(column_CountyId);
            Assert.NotNull(column_CensusYearBuilt);

            for (int i = 0; i < table.RowCount; i++)
            {
                Row? row = table.GetRow(i);
                if (row is null || !row.TryGetValue(column_CountyId.Index, out int countyId_Row) || countyId_Row != countyId || !row.TryGetValue(column_Reference.Index, out string? reference_Row) || reference_Row != reference)
                {
                    continue;
                }

                return row.TryGetValue(column_CensusYearBuilt.Index, out ushort value) ? value : null;
            }

            throw new InvalidOperationException("no row for " + reference);
        }
    }
}
