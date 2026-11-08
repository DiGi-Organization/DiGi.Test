using DiGi.Core.IO.Table.Interfaces;
using DiGi.PostgreSQL.Table.Classes;
using DiGi.PostgreSQL.Table.Enums;
using Npgsql;
using NpgsqlTypes;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Column = DiGi.Core.IO.Table.Classes.Column;

namespace DiGi.PostgreSQL.Table.xUnit
{
    /// <summary>
    /// Facts for <see cref="Query.TryBuildFilterGroupSql{TColumn}"/> (DiGi-Organization/DiGi.PostgreSQL#10).
    /// <para>The <c>In</c>/<c>NotIn</c> value arrives over HTTP as a <see cref="System.Text.Json.JsonElement"/> (a JSON array), not an <c>IEnumerable</c>. The list branch must still be reached so the filter builds <c>= ANY(@p)</c>/<c>!= ALL(@p)</c> over the array's elements, instead of comparing the whole array as one scalar (text columns silently match nothing; the old int path answered 500).</para>
    /// <para>Each fact feeds a <see cref="FilterGroup"/> deserialized exactly as ASP.NET Core MVC binds the body (camelCase, <c>Value</c> bound to a <see cref="System.Text.Json.JsonElement"/>), then asserts on the produced SQL text and <see cref="NpgsqlParameterCollection"/>.</para>
    /// </summary>
    public partial class Facts
    {
        private static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>
        /// Deserializes a <see cref="FilterGroup"/> from JSON with the ASP.NET Core MVC naming policy, so <c>Value</c> is a real <see cref="System.Text.Json.JsonElement"/> — the actual HTTP binding result, not a hand-built <c>List&lt;T&gt;</c>.
        /// </summary>
        /// <param name="json">The JSON document to deserialize.</param>
        /// <returns>The deserialized, non-null <see cref="FilterGroup"/>.</returns>
        private static FilterGroup FilterGroup_FromJson(string json)
        {
            FilterGroup? filterGroup = JsonSerializer.Deserialize<FilterGroup>(json, jsonSerializerOptions);
            Assert.NotNull(filterGroup);
            return filterGroup;
        }

        /// <summary>
        /// Runs <see cref="Query.TryBuildFilterGroupSql{TColumn}"/> against a single whitelisted column and returns the produced SQL and bound parameters.
        /// </summary>
        /// <param name="filterGroup">The filter group to build SQL for.</param>
        /// <param name="column">The whitelisted column the condition targets.</param>
        /// <returns>The SQL text and the bound <see cref="NpgsqlParameterCollection"/>.</returns>
        private static (string sql, NpgsqlParameterCollection parameters) BuildFilterSql(FilterGroup filterGroup, IColumn column)
        {
            List<IColumn> columns = [column];
            StringBuilder sqlBuilder = new();
            NpgsqlParameterCollection parameters = new NpgsqlCommand().Parameters;
            int parameterIndex = 0;

            bool ok = filterGroup.TryBuildFilterGroupSql(columns, sqlBuilder, parameters, ref parameterIndex);
            Assert.True(ok, "TryBuildFilterGroupSql returned false (whitelist/type violation).");

            return (sqlBuilder.ToString(), parameters);
        }

        /// <summary>
        /// An <c>In</c> over a JSON array of integers on an int column builds <c>= ANY(@p)</c> with one <c>Integer</c> array parameter carrying the three ints. On the unmodified code this is red (the array fell to the scalar branch and answered <c>1=0</c>).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_In_JsonArray_IntColumn()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"subdivision_id","filterOperator":6,"value":[1,2,3]}]}""");
            Column column = new(0, "subdivision_id", typeof(int));

            (string sql, NpgsqlParameterCollection parameters) = BuildFilterSql(filterGroup, column);

            Assert.Contains("\"subdivision_id\" = ANY(@filterParam_0)", sql);
            NpgsqlParameter parameter = Assert.Single(parameters);
            Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Integer, parameter.NpgsqlDbType);

            object[] values = Assert.IsType<object[]>(parameter.Value);
            Assert.Equal(3, values.Length);
            Assert.Equal(1L, System.Convert.ToInt64(values[0], CultureInfo.CurrentCulture));
            Assert.Equal(2L, System.Convert.ToInt64(values[1], CultureInfo.CurrentCulture));
            Assert.Equal(3L, System.Convert.ToInt64(values[2], CultureInfo.CurrentCulture));
        }

        /// <summary>
        /// An <c>In</c> over a JSON array of strings on a text column builds <c>= ANY(@p)</c> with one <c>Text</c> array parameter carrying the two strings. On the unmodified code this is red (the array was compared as the literal <c>["a","b"]</c> text).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_In_JsonArray_TextColumn()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"municipality_name","filterOperator":6,"value":["a","b"]}]}""");
            Column column = new(0, "municipality_name", typeof(string));

            (string sql, NpgsqlParameterCollection parameters) = BuildFilterSql(filterGroup, column);

            Assert.Contains("\"municipality_name\" = ANY(@filterParam_0)", sql);
            NpgsqlParameter parameter = Assert.Single(parameters);
            Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Text, parameter.NpgsqlDbType);

            object[] values = Assert.IsType<object[]>(parameter.Value);
            Assert.Equal(2, values.Length);
            Assert.Equal("a", values[0]);
            Assert.Equal("b", values[1]);
        }

        /// <summary>
        /// Regression: an <c>In</c> over a single scalar string still builds <c>= @p</c> (one value, not <c>ANY</c>). This must be green both before and after the change — the scalar path is untouched.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_In_Scalar_Regression()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"municipality_name","filterOperator":6,"value":"Łomianki"}]}""");
            Column column = new(0, "municipality_name", typeof(string));

            (string sql, NpgsqlParameterCollection parameters) = BuildFilterSql(filterGroup, column);

            Assert.Contains("\"municipality_name\" = @filterParam_0", sql);
            Assert.DoesNotContain("ANY", sql);
            NpgsqlParameter parameter = Assert.Single(parameters);
            Assert.Equal(NpgsqlDbType.Text, parameter.NpgsqlDbType);
            Assert.Equal("Łomianki", parameter.Value);
        }

        /// <summary>
        /// An <c>In</c> over an empty JSON array degrades cleanly to <c>1=0</c> with no bound parameter and no exception.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_In_EmptyArray()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"subdivision_id","filterOperator":6,"value":[]}]}""");
            Column column = new(0, "subdivision_id", typeof(int));

            (string sql, NpgsqlParameterCollection parameters) = BuildFilterSql(filterGroup, column);

            Assert.Contains("1=0", sql);
            Assert.Empty(parameters);
        }

        /// <summary>
        /// An <c>In</c> whose value is a JSON object (neither a scalar nor a list) is a request-level failure: it throws <see cref="InvalidFilterException"/> naming the column, instead of degrading to <c>1=0</c> (a plausible-looking zero) or becoming a raw-text <c>= @p</c> scalar.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_In_JsonObject_Throws()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"municipality_name","filterOperator":6,"value":{"a":1}}]}""");
            Column column = new(0, "municipality_name", typeof(string));
            List<IColumn> columns = [column];

            StringBuilder sqlBuilder = new();
            NpgsqlParameterCollection parameters = new NpgsqlCommand().Parameters;
            int parameterIndex = 0;

            InvalidFilterException exception = Assert.Throws<InvalidFilterException>(() => filterGroup.TryBuildFilterGroupSql(columns, sqlBuilder, parameters, ref parameterIndex));

            Assert.Equal("municipality_name", exception.ColumnUniqueId);
            Assert.Equal(FilterOperator.In, exception.FilterOperator);
            Assert.Empty(parameters);
        }

        /// <summary>
        /// A <c>NotIn</c> over a JSON array of integers on an int column builds <c>!= ALL(@p)</c> with one <c>Integer</c> array parameter. On the unmodified code this is red (the array fell to the scalar branch and answered <c>1=1</c>).
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the test.</returns>
        [Fact]
        public void TryBuildFilterGroupSql_NotIn_JsonArray_IntColumn()
        {
            FilterGroup filterGroup = FilterGroup_FromJson("""{"filterConditions":[{"columnUniqueId":"subdivision_id","filterOperator":7,"value":[5,6]}]}""");
            Column column = new(0, "subdivision_id", typeof(int));

            (string sql, NpgsqlParameterCollection parameters) = BuildFilterSql(filterGroup, column);

            Assert.Contains("\"subdivision_id\" != ALL(@filterParam_0)", sql);
            NpgsqlParameter parameter = Assert.Single(parameters);
            Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Integer, parameter.NpgsqlDbType);

            object[] values = Assert.IsType<object[]>(parameter.Value);
            Assert.Equal(2, values.Length);
            Assert.Equal(5L, System.Convert.ToInt64(values[0], CultureInfo.CurrentCulture));
            Assert.Equal(6L, System.Convert.ToInt64(values[1], CultureInfo.CurrentCulture));
        }
    }
}
