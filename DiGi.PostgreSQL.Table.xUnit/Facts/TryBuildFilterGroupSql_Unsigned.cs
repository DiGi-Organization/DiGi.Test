using DiGi.PostgreSQL.Table.Classes;
using Npgsql;
using Column = DiGi.Core.IO.Table.Classes.Column;

namespace DiGi.PostgreSQL.Table.xUnit
{
    /// <summary>
    /// Facts for <see cref="Query.TryBuildFilterGroupSql{TColumn}"/> on unsigned integer columns (DiGi-Organization/DiGi.PostgreSQL#17).
    /// <para>Npgsql does not write <see cref="ushort"/>, <see cref="uint"/> or <see cref="ulong"/>, so the bound value must be widened to <see cref="int"/>, <see cref="long"/> or <see cref="decimal"/>.</para>
    /// </summary>
    public partial class Facts
    {
        /// <summary>
        /// Every comparison operator, In and NotIn on <c>ushort</c>, <c>uint</c> and <c>ulong</c> columns bind only values of a type Npgsql writes. On the unmodified code this is red (the converted value was bound as <see cref="ushort"/>, <see cref="uint"/> or <see cref="ulong"/>).
        /// </summary>
        [Fact]
        public void TryBuildFilterGroupSql_UnsignedColumns()
        {
            (System.Type columnType, System.Type expectedType)[] cases = [(typeof(ushort), typeof(int)), (typeof(uint), typeof(long)), (typeof(ulong), typeof(decimal))];

            foreach ((System.Type columnType, System.Type expectedType) in cases)
            {
                Column column = new(0, "value", columnType);

                for (int filterOperator = 0; filterOperator <= 5; filterOperator++)
                {
                    FilterGroup filterGroup = FilterGroup_FromJson($$"""{"filterConditions":[{"columnUniqueId":"value","filterOperator":{{filterOperator}},"value":2008}]}""");
                    (_, NpgsqlParameterCollection parameters) = BuildFilterSql(filterGroup, column);

                    NpgsqlParameter parameter = Assert.Single(parameters);
                    Assert.IsType(expectedType, parameter.Value);
                    Assert.Equal(2008m, System.Convert.ToDecimal(parameter.Value));
                }

                foreach (int filterOperator in new int[] { 6, 7 })
                {
                    FilterGroup filterGroup_Single = FilterGroup_FromJson($$"""{"filterConditions":[{"columnUniqueId":"value","filterOperator":{{filterOperator}},"value":[2008]}]}""");
                    (_, NpgsqlParameterCollection parameters_Single) = BuildFilterSql(filterGroup_Single, column);
                    Assert.IsType(expectedType, Assert.Single(parameters_Single).Value);

                    FilterGroup filterGroup_Many = FilterGroup_FromJson($$"""{"filterConditions":[{"columnUniqueId":"value","filterOperator":{{filterOperator}},"value":[2008,2009,2010]}]}""");
                    (_, NpgsqlParameterCollection parameters_Many) = BuildFilterSql(filterGroup_Many, column);
                    object[] values = Assert.IsType<object[]>(Assert.Single(parameters_Many).Value);
                    Assert.Equal(3, values.Length);
                    Assert.All(values, x => Assert.IsType(expectedType, x));

                    FilterGroup filterGroup_Scalar = FilterGroup_FromJson($$"""{"filterConditions":[{"columnUniqueId":"value","filterOperator":{{filterOperator}},"value":2008}]}""");
                    (_, NpgsqlParameterCollection parameters_Scalar) = BuildFilterSql(filterGroup_Scalar, column);
                    Assert.IsType(expectedType, Assert.Single(parameters_Scalar).Value);
                }
            }
        }

        /// <summary>
        /// <see cref="Query.DatabaseValue(object?)"/> widens unsigned integers and passes everything else, including <see langword="null"/>, through unchanged.
        /// </summary>
        [Fact]
        public void DatabaseValue()
        {
            Assert.Equal(65535, Assert.IsType<int>(Query.DatabaseValue((ushort)65535)));
            Assert.Equal(4294967295L, Assert.IsType<long>(Query.DatabaseValue(uint.MaxValue)));
            Assert.Equal(ulong.MaxValue, Assert.IsType<decimal>(Query.DatabaseValue(ulong.MaxValue)));
            Assert.Equal(-5, Assert.IsType<int>(Query.DatabaseValue(-5)));
            Assert.Equal("a", Query.DatabaseValue("a"));
            Assert.Null(Query.DatabaseValue(null));
        }
    }
}
