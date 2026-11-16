using DiGi.Core.IO.Table.Interfaces;
using DiGi.PostgreSQL.Table.Classes;
using System.Collections.Generic;
using System.Text;
using Column = DiGi.Core.IO.Table.Classes.Column;

namespace DiGi.PostgreSQL.Table.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Two sort conditions render in list order as quoted <c>"column" ASC|DESC NULLS LAST</c> terms joined by <c>, </c> - null values sort last in both directions (DiGi-Organization/DiGi.GIS.WebAPI#84).
        /// </summary>
        [Fact]
        public void TryBuildOrderBySql_DescendingAndAscending_RendersNullsLast()
        {
            List<SortCondition> sortConditions = [new SortCondition() { ColumnUniqueId = "value", Descending = true }, new SortCondition() { ColumnUniqueId = "subdivision_id", Descending = false }];
            Dictionary<string, IColumn> dictionary_Columns = new()
            {
                ["value"] = new Column(0, "value", typeof(double)),
                ["subdivision_id"] = new Column(1, "subdivision_id", typeof(int))
            };

            StringBuilder sqlBuilder = new();

            Assert.True(sortConditions.TryBuildOrderBySql(dictionary_Columns, sqlBuilder));
            Assert.Equal("\"value\" DESC NULLS LAST, \"subdivision_id\" ASC NULLS LAST", sqlBuilder.ToString());
        }

        /// <summary>
        /// A null, empty or blank-column sort list builds nothing and answers false, so the caller leaves its statement unordered instead of appending an empty clause (DiGi-Organization/DiGi.GIS.WebAPI#84).
        /// </summary>
        [Fact]
        public void TryBuildOrderBySql_NullOrEmptyOrBlank_ReturnsFalse()
        {
            Dictionary<string, IColumn> dictionary_Columns = new()
            {
                ["value"] = new Column(0, "value", typeof(double))
            };

            List<SortCondition> sortConditions_Blank = [new SortCondition() { ColumnUniqueId = " " }];

            StringBuilder sqlBuilder = new();

            Assert.False(((IEnumerable<SortCondition>?)null).TryBuildOrderBySql(dictionary_Columns, sqlBuilder));
            Assert.False(new List<SortCondition>().TryBuildOrderBySql(dictionary_Columns, sqlBuilder));
            Assert.False(sortConditions_Blank.TryBuildOrderBySql(dictionary_Columns, sqlBuilder));
            Assert.Equal(string.Empty, sqlBuilder.ToString());
        }

        /// <summary>
        /// A sort condition naming a column the stored table does not hold throws <see cref="InvalidFilterException"/> naming the column - the whitelist contract of the filter builder extended to sort columns, so an unknown identifier never reaches the statement (DiGi-Organization/DiGi.GIS.WebAPI#84).
        /// </summary>
        [Fact]
        public void TryBuildOrderBySql_UnknownColumn_Throws()
        {
            List<SortCondition> sortConditions = [new SortCondition() { ColumnUniqueId = "no_such_column", Descending = true }];
            Dictionary<string, IColumn> dictionary_Columns = new()
            {
                ["value"] = new Column(0, "value", typeof(double))
            };

            StringBuilder sqlBuilder = new();

            InvalidFilterException exception = Assert.Throws<InvalidFilterException>(() => sortConditions.TryBuildOrderBySql(dictionary_Columns, sqlBuilder));
            Assert.Equal("no_such_column", exception.ColumnUniqueId);
        }
    }
}
