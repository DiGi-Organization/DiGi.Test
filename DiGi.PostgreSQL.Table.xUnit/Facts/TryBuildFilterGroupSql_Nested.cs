using DiGi.Core.IO.Table.Interfaces;
using DiGi.PostgreSQL.Table.Classes;
using DiGi.PostgreSQL.Table.Enums;
using Npgsql;
using System.Text;
using Column = DiGi.Core.IO.Table.Classes.Column;

namespace DiGi.PostgreSQL.Table.xUnit
{
    /// <summary>
    /// Facts for the text <see cref="Query.TryBuildFilterGroupSql{TColumn}(FilterGroup, List{TColumn}, StringBuilder, NpgsqlParameterCollection, ref int)"/> writes for nested groups.
    /// <para>The builder appends conditions in place, so the separators and parentheses are the part that can go wrong: a group of one condition is written bare, a group of several is parenthesised, and a child that writes nothing leaves no separator behind.</para>
    /// </summary>
    public partial class Facts
    {
        /// <summary>
        /// A nested group is written with its own operator and parentheses, an empty child and a child of one condition add none, and the parameters are numbered in the order they are written.
        /// </summary>
        [Fact]
        public void TryBuildFilterGroupSql_Nested_Text()
        {
            FilterGroup filterGroup = new()
            {
                LogicalOperator = FilterLogicalOperator.And,
                FilterConditions = [new FilterCondition() { ColumnUniqueId = "county_id", FilterOperator = FilterOperator.Equals, Value = 7 }],
                FilterGroups =
                [
                    new FilterGroup(),
                    new FilterGroup()
                    {
                        LogicalOperator = FilterLogicalOperator.Or,
                        FilterConditions =
                        [
                            new FilterCondition() { ColumnUniqueId = "name", FilterOperator = FilterOperator.IsNull },
                            new FilterCondition() { ColumnUniqueId = "storeys", FilterOperator = FilterOperator.GreaterThan, Value = 2 },
                        ],
                    },
                    new FilterGroup() { FilterConditions = [new FilterCondition() { ColumnUniqueId = "name", FilterOperator = FilterOperator.IsNotNull }] },
                ],
            };

            List<IColumn> columns = [new Column(0, "county_id", typeof(int)), new Column(1, "name", typeof(string)), new Column(2, "storeys", typeof(int))];
            StringBuilder stringBuilder = new("WHERE ");
            NpgsqlParameterCollection npgsqlParameterCollection = new NpgsqlCommand().Parameters;
            int parameterIndex = 0;

            Assert.True(filterGroup.TryBuildFilterGroupSql(columns, stringBuilder, npgsqlParameterCollection, ref parameterIndex));

            Assert.Equal("WHERE (\"county_id\" = @filterParam_0 AND (\"name\" IS NULL OR \"storeys\" > @filterParam_1) AND \"name\" IS NOT NULL)", stringBuilder.ToString());
            Assert.Equal(2, parameterIndex);
            Assert.Equal(["filterParam_0", "filterParam_1"], npgsqlParameterCollection.Select(x => x.ParameterName));
        }

        /// <summary>
        /// A group whose only content is empty children writes nothing, and a build that fails on an unresolvable column type restores the builder to what it held on entry.
        /// </summary>
        [Fact]
        public void TryBuildFilterGroupSql_Nested_EmptyAndUnresolvedType()
        {
            StringBuilder stringBuilder = new("WHERE 1=1");
            NpgsqlParameterCollection npgsqlParameterCollection = new NpgsqlCommand().Parameters;
            int parameterIndex = 0;

            FilterGroup filterGroup_Empty = new() { FilterGroups = [new FilterGroup(), new FilterGroup() { FilterGroups = [new FilterGroup()] }] };
            List<IColumn> columns = [new Column(0, "county_id", typeof(int)), new Column(1, "shape", typeof(object))];

            Assert.True(filterGroup_Empty.TryBuildFilterGroupSql(columns, stringBuilder, npgsqlParameterCollection, ref parameterIndex));
            Assert.Equal("WHERE 1=1", stringBuilder.ToString());

            FilterGroup filterGroup_Unresolved = new()
            {
                FilterConditions = [new FilterCondition() { ColumnUniqueId = "county_id", FilterOperator = FilterOperator.IsNull }],
                FilterGroups = [new FilterGroup() { FilterConditions = [new FilterCondition() { ColumnUniqueId = "shape", FilterOperator = FilterOperator.IsNull }] }],
            };

            Assert.False(filterGroup_Unresolved.TryBuildFilterGroupSql(columns, stringBuilder, npgsqlParameterCollection, ref parameterIndex));
            Assert.Equal("WHERE 1=1", stringBuilder.ToString());
        }
    }
}
