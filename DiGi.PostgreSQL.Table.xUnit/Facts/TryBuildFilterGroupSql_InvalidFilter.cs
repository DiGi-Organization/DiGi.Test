using DiGi.Core.IO.Table.Interfaces;
using DiGi.PostgreSQL.Table.Classes;
using DiGi.PostgreSQL.Table.Enums;
using Npgsql;
using System.Globalization;
using System.Text;
using Column = DiGi.Core.IO.Table.Classes.Column;

namespace DiGi.PostgreSQL.Table.xUnit
{
    /// <summary>
    /// Facts for <see cref="Query.TryBuildFilterGroupSql{TColumn}"/> typed failures (DiGi-Organization/DiGi.PostgreSQL#12).
    /// <para>An unknown column and an unconvertible filter value are request-level failures and must throw <see cref="InvalidFilterException"/>, so the caller can distinguish "the request is wrong" from the valid no-match <c>1=0</c> and the empty-result <c>null</c> it previously could not tell apart. On the unmodified code each of these answers <c>false</c> or <c>1=0</c> and never throws.</para>
    /// </summary>
    public partial class Facts
    {
        /// <summary>
        /// An <c>Equals</c> filter whose column is not on the stored column list throws, naming the column and operator, instead of answering the empty result.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_UnknownFilterColumn_Throws()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"no_such_column","filterOperator":0,"value":"x"}]}""");
            Column column = new(0, "subdivision_id", typeof(int));
            List<IColumn> columns = [column];

            StringBuilder sqlBuilder = new();
            NpgsqlParameterCollection parameters = new NpgsqlCommand().Parameters;
            int parameterIndex = 0;

            InvalidFilterException exception = Assert.Throws<InvalidFilterException>(() => filterGroup.TryBuildFilterGroupSql(columns, sqlBuilder, parameters, ref parameterIndex));

            Assert.Equal("no_such_column", exception.ColumnUniqueId);
            Assert.NotNull(exception.FilterOperator);
            Assert.Equal(FilterOperator.Equals, exception.FilterOperator);
            Assert.Null(exception.Value);
            Assert.Empty(parameters);
        }

        /// <summary>
        /// An <c>Equals</c> value that cannot be converted to an int column throws, naming the column, operator and value, instead of silently answering <c>1=0</c> (a plausible-looking zero).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_UnconvertibleValue_Throws()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"subdivision_id","filterOperator":0,"value":"abc"}]}""");
            Column column = new(0, "subdivision_id", typeof(int));
            List<IColumn> columns = [column];

            StringBuilder sqlBuilder = new();
            NpgsqlParameterCollection parameters = new NpgsqlCommand().Parameters;
            int parameterIndex = 0;

            InvalidFilterException exception = Assert.Throws<InvalidFilterException>(() => filterGroup.TryBuildFilterGroupSql(columns, sqlBuilder, parameters, ref parameterIndex));

            Assert.Equal("subdivision_id", exception.ColumnUniqueId);
            Assert.NotNull(exception.FilterOperator);
            Assert.Equal(FilterOperator.Equals, exception.FilterOperator);
            Assert.NotNull(exception.Value);
            Assert.Empty(parameters);
        }

        /// <summary>
        /// An <c>In</c> list mixing a valid item with an unconvertible one throws (strict), instead of silently matching only the valid subset into a plausible-looking count.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_In_UnconvertibleItem_Throws()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"subdivision_id","filterOperator":6,"value":[1,"abc"]}]}""");
            Column column = new(0, "subdivision_id", typeof(int));
            List<IColumn> columns = [column];

            StringBuilder sqlBuilder = new();
            NpgsqlParameterCollection parameters = new NpgsqlCommand().Parameters;
            int parameterIndex = 0;

            InvalidFilterException exception = Assert.Throws<InvalidFilterException>(() => filterGroup.TryBuildFilterGroupSql(columns, sqlBuilder, parameters, ref parameterIndex));

            Assert.Equal("subdivision_id", exception.ColumnUniqueId);
            Assert.NotNull(exception.FilterOperator);
            Assert.Equal(FilterOperator.In, exception.FilterOperator);
        }

        /// <summary>
        /// A valid <c>Equals</c> over an int column still builds <c>= @p</c> with one bound parameter — the happy path is unchanged by the typed-failure change.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_ValidEquals_Regression()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"subdivision_id","filterOperator":0,"value":5}]}""");
            Column column = new(0, "subdivision_id", typeof(int));
            List<IColumn> columns = [column];

            StringBuilder sqlBuilder = new();
            NpgsqlParameterCollection parameters = new NpgsqlCommand().Parameters;
            int parameterIndex = 0;

            Assert.True(filterGroup.TryBuildFilterGroupSql(columns, sqlBuilder, parameters, ref parameterIndex));

            Assert.Contains("\"subdivision_id\" = @filterParam_0", sqlBuilder.ToString());
            NpgsqlParameter parameter = Assert.Single(parameters);
            Assert.Equal(5L, System.Convert.ToInt64(parameter.Value, CultureInfo.CurrentCulture));
        }
    }
}
