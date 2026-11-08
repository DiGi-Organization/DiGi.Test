using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.xUnit.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.Table.Enums;
using Npgsql;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="BuildingDataPostgreSQLConverter.PullAsync(Npgsql.NpgsqlConnection?, IEnumerable{string}, int?, IEnumerable{string}?, int, bool, int, System.Threading.CancellationToken)"/>
        /// and its instance overload return null when inputs or connection are null or empty.
        /// </summary>
        [Fact]
        public async Task BuildingDataPostgreSQLConverter_PullAsync_NullOrEmpty_ReturnsNull()
        {
            BuildingDataPostgreSQLConverter buildingDataPostgreSQLConverter = new(null);

            Table? result_NullConn = await buildingDataPostgreSQLConverter.PullAsync(null, ["ref_1"], 10, fallbackByReference: true);
            Assert.Null(result_NullConn);

            Table? result_NullRefs = await buildingDataPostgreSQLConverter.PullAsync(null, null!, 10, fallbackByReference: true);
            Assert.Null(result_NullRefs);

            Table? result_EmptyRefs = await buildingDataPostgreSQLConverter.PullAsync(null, [], 10, fallbackByReference: true);
            Assert.Null(result_EmptyRefs);

            Table? result_InstanceNullRefs = await buildingDataPostgreSQLConverter.PullAsync((IEnumerable<string>)null!, 10, fallbackByReference: true);
            Assert.Null(result_InstanceNullRefs);

            Table? result_InstanceEmptyRefs = await buildingDataPostgreSQLConverter.PullAsync([], 10, fallbackByReference: true);
            Assert.Null(result_InstanceEmptyRefs);
        }

        /// <summary>
        /// Verifies that the reads added for the coverage and duplicate checks return the "not available" answer rather than throwing when there is no connection.
        /// </summary>
        [Fact]
        public async Task BuildingDataPostgreSQLConverter_NewReads_NoConnection_ReturnDefaults()
        {
            BuildingDataPostgreSQLConverter buildingDataPostgreSQLConverter = new(null);

            Assert.Equal(-1, await buildingDataPostgreSQLConverter.GetCountAsync(55417));
            Assert.Null(await buildingDataPostgreSQLConverter.GetEstimatedCountAsync(55417));
            Assert.Null(await buildingDataPostgreSQLConverter.GetReferencesByCountyIdAsync(55417));
            Assert.Null(await buildingDataPostgreSQLConverter.GetDuplicateReferencesAsync());
            Assert.Null(await buildingDataPostgreSQLConverter.GetCountyIdsByReferenceAsync("some_reference"));

            // A blank reference is rejected before a connection is even attempted.
            Assert.Null(await buildingDataPostgreSQLConverter.GetCountyIdsByReferenceAsync(null));
            Assert.Null(await buildingDataPostgreSQLConverter.GetCountyIdsByReferenceAsync("   "));

            Assert.Null(await BuildingDataPostgreSQLConverter.GetDuplicateReferencesAsync(null));
            Assert.Equal(-1, await Building2DPostgreSQLConverter.GetCountWithoutSubdivisionAsync(null, 55417));
            Assert.Null(await Building2DPostgreSQLConverter.GetBuilding2DsWithoutSubdivisionAsync(null, 55417));
        }

        /// <summary>
        /// Verifies that <see cref="PostgreSQLBuildingDataUpdateTask.UnassignedSubdivisionBuildingCount"/> starts at 0 upon instantiation.
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataUpdateTask_UnassignedSubdivisionBuildingCount_InitialState()
        {
            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            PostgreSQLBuildingDataUpdateTask task = new(gISPostgreSQLConverterManager);

            Assert.Equal(0, task.UnassignedSubdivisionBuildingCount);
        }

        /// <summary>
        /// Verifies that every statement behind the new reads is accepted by the database and answers the shape the callers expect.
        /// <para>These queries are written by hand rather than built from the column metadata, so a wrong column name or a malformed clause would only ever surface here or in production. A county holding no rows is still a complete check of that: the server parses and plans the statement either way.</para>
        /// <para>Skipped by default: it executes integration queries requiring the confs to point at a database.</para>
        /// </summary>
        [Fact(Skip = "Requires database connection")]
        public async Task BuildingDataPostgreSQLConverter_NewReads_Database_AcceptsStatements()
        {
            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            Assert.NotNull(gISPostgreSQLConverterManager);

            BuildingDataPostgreSQLConverter? buildingDataPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<BuildingDataPostgreSQLConverter>();
            Assert.NotNull(buildingDataPostgreSQLConverter);

            Building2DPostgreSQLConverter? building2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<Building2DPostgreSQLConverter>();
            Assert.NotNull(building2DPostgreSQLConverter);

            int countyId = 55417;

            // The whole table always exists, so this one separates a working statement from a missing partition.
            long count_Total = await buildingDataPostgreSQLConverter.GetCountAsync(null);
            Assert.True(count_Total >= 0);

            // The estimate reads pg_class.reltuples, which a partitioned parent carries as -1 until it is
            // analysed. What is being checked here is that the statement runs, not that the answer is a count.
            long? count_Estimated = await buildingDataPostgreSQLConverter.GetEstimatedCountAsync(null);
            Assert.True(count_Estimated is null || count_Estimated.Value >= -1);

            HashSet<string>? references = await buildingDataPostgreSQLConverter.GetReferencesByCountyIdAsync(countyId);
            Assert.NotNull(references);

            long count_County = await buildingDataPostgreSQLConverter.GetCountAsync(countyId);
            if (count_County >= 0)
            {
                // Rows are one per county and reference, so the references cannot outnumber them.
                Assert.True(references.Count <= count_County);
            }

            List<Building2DReferenceDuplicate>? building2DReferenceDuplicates = await buildingDataPostgreSQLConverter.GetDuplicateReferencesAsync(10);
            Assert.NotNull(building2DReferenceDuplicates);
            foreach (Building2DReferenceDuplicate building2DReferenceDuplicate in building2DReferenceDuplicates)
            {
                Assert.NotNull(building2DReferenceDuplicate.CountyIds);
                Assert.True(building2DReferenceDuplicate.CountyIds.Count > 1);
            }

            Assert.True(await Building2DPostgreSQLConverter.GetCountWithoutSubdivisionAsync(null, countyId) == -1);
            Assert.True(await building2DPostgreSQLConverter.GetCountWithoutSubdivisionAsync(countyId) >= 0);

            string? reference = references.FirstOrDefault();
            if (reference is not null)
            {
                List<int>? countyIds = await buildingDataPostgreSQLConverter.GetCountyIdsByReferenceAsync(reference);
                Assert.NotNull(countyIds);
                Assert.Contains(countyId, countyIds);
            }
        }
        /// <summary>
        /// Verifies the filtered <c>COUNT(*)</c> of DiGi-Organization/DiGi.GIS.WebAPI#52 through the building-data converter, on a scratch table under the converter's own primary key and county partitioning: a null filter group answers the partition's row count, a filter matching two of three rows answers 2, a filter matching nothing answers 0 (not null, not an error), and the no-filter count equals the partition's direct <c>COUNT(*)</c> - the figure <c>countbycountyid</c> returns on a partitioned deployment.
        /// <para>Database fact that needs no data of its own: it pushes its own three rows into the scratch table and returns without asserting when the local test database configuration is absent.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataPostgreSQLConverter_GetCountAsync_Filtered_CountsRows()
        {
            ConnectionData? connectionData = Create.GISPostgreSQLConverterManager()?.GetPostgreSQLConverter<BuildingDataPostgreSQLConverter>()?.ConnectionData;
            if (connectionData is null)
            {
                return;
            }

            ScratchBuildingDataPostgreSQLConverter converter = new(connectionData);

            // Declared by name, as the other building data facts do, so that this project need not reference DiGi.GIS.IO.
            Column column_CountyId = new ExtendedColumn("County Id", typeof(int), null, null);
            Column column_Reference = new ExtendedColumn("Reference", typeof(string), null, null);
            Column column_Value = new ExtendedColumn("value", typeof(double), null, null);

            const int countyId = 999999;

            Table table = new();
            table.AddColumn(column_CountyId);
            table.AddColumn(column_Reference);
            table.AddColumn(column_Value);
            table.AddRow([countyId, "XUNIT-COUNT-1", 10.0]);
            table.AddRow([countyId, "XUNIT-COUNT-2", null]);
            table.AddRow([countyId, "XUNIT-COUNT-3", 30.0]);
            Assert.True(await converter.PushAsync(table));

            try
            {
                // A null filter group counts the whole partition: three rows, one of them NULL-valued.
                long? count_NoFilter = await converter.GetCountByFilterGroupAsync((DiGi.PostgreSQL.Table.Classes.FilterGroup?)null, countyId);
                Assert.NotNull(count_NoFilter);
                Assert.Equal(3L, count_NoFilter.Value);

                // A filter matching two rows (value IS NOT NULL) answers 2 - the NULL row is not counted away.
                DiGi.PostgreSQL.Table.Classes.FilterGroup filterGroup_NotNull = new()
                {
                    FilterConditions =
                    [
                        new DiGi.PostgreSQL.Table.Classes.FilterCondition
                        {
                            ColumnUniqueId = "value",
                            FilterOperator = FilterOperator.IsNotNull
                        }
                    ]
                };
                long? count_NotNull = await converter.GetCountByFilterGroupAsync(filterGroup_NotNull, countyId);
                Assert.NotNull(count_NotNull);
                Assert.Equal(2L, count_NotNull.Value);

                // A filter matching nothing answers 0 - not null, not an error status.
                DiGi.PostgreSQL.Table.Classes.FilterGroup filterGroup_None = new()
                {
                    FilterConditions =
                    [
                        new DiGi.PostgreSQL.Table.Classes.FilterCondition
                        {
                            ColumnUniqueId = "value",
                            FilterOperator = FilterOperator.GreaterThan,
                            Value = 1000.0
                        }
                    ]
                };
                long? count_None = await converter.GetCountByFilterGroupAsync(filterGroup_None, countyId);
                Assert.NotNull(count_None);
                Assert.Equal(0L, count_None.Value);

                // The no-filter count equals the partition's direct row count, the figure countbycountyid returns on a partitioned deployment.
                // The physical column is the normalized slug county_id (UniqueId), not the display name "County Id" - PushAsync names columns by their slug.
                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();
                await using NpgsqlCommand npgsqlCommand = new($"SELECT COUNT(*) FROM \"{converter.TableName}\" WHERE \"county_id\" = {countyId}", npgsqlConnection);
                object? resultValue = await npgsqlCommand.ExecuteScalarAsync();
                Assert.NotNull(resultValue);
                Assert.Equal(System.Convert.ToInt64(resultValue, CultureInfo.CurrentCulture), count_NoFilter!.Value);
            }
            finally
            {
                await DiGi.PostgreSQL.Modify.RemoveTableAsync(connectionData, converter.TableName);

                await using NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection_Cleanup is not null)
                {
                    await npgsqlConnection_Cleanup.OpenAsync();
                    await using NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection_Cleanup);
                    npgsqlCommand.Parameters.AddWithValue("tableName", converter.TableName);
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Pins DiGi-Organization/DiGi.GIS.WebAPI#59: the GIS multivalue override forwards maxGroups to the base, so a
        /// <see cref="MultivalueAggregateFunction.SplitValueDistribution"/> over a column with more than maxGroups distinct values
        /// answers maxGroups + 1 groups (the extra row being the truncation marker), while
        /// <see cref="MultivalueAggregateFunction.SplitDistinctCount"/> ignores the cap and keeps its full count.
        /// <para>Database fact that seeds its own scratch partition through <see cref="ScratchBuildingDataPostgreSQLConverter"/> and
        /// returns without asserting when the local test database configuration is absent.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataPostgreSQLConverter_GetAggregateSummary_Multivalue_MaxGroups_CapsDistribution()
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
            Column column_Tags = new ExtendedColumn("tags", typeof(string), null, null);

            const int countyId = 999998;

            Table table = new();
            table.AddColumn(column_CountyId);
            table.AddColumn(column_Reference);
            table.AddColumn(column_Tags);
            // One row whose multivalue column splits into five distinct comma-separated values.
            table.AddRow([countyId, "XUNIT-MAXGROUPS-1", "v1,v2,v3,v4,v5"]);
            Assert.True(await converter.PushAsync(table));

            try
            {
                // Capped at 2 -> 2 groups + 1 truncation marker = 3 rows (not the full five).
                System.Text.Json.Nodes.JsonNode? distribution_Capped = await converter.GetAggregateSummaryAsync("tags", MultivalueAggregateFunction.SplitValueDistribution, countyId, ",", null, maxGroups: 2);
                System.Text.Json.Nodes.JsonArray? distributionArray_Capped = distribution_Capped as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(distributionArray_Capped);
                Assert.Equal(3, distributionArray_Capped!.Count);

                // No cap (default 1000) -> all five distinct values, no truncation.
                System.Text.Json.Nodes.JsonNode? distribution_Full = await converter.GetAggregateSummaryAsync("tags", MultivalueAggregateFunction.SplitValueDistribution, countyId, ",");
                System.Text.Json.Nodes.JsonArray? distributionArray_Full = distribution_Full as System.Text.Json.Nodes.JsonArray;
                Assert.NotNull(distributionArray_Full);
                Assert.Equal(5, distributionArray_Full!.Count);

                // SplitDistinctCount ignores the cap -> the full distinct count either way.
                System.Text.Json.Nodes.JsonNode? distinct_Capped = await converter.GetAggregateSummaryAsync("tags", MultivalueAggregateFunction.SplitDistinctCount, countyId, ",", null, maxGroups: 2);
                Assert.NotNull(distinct_Capped);
                Assert.Equal(5L, distinct_Capped!.GetValue<long>());
            }
            finally
            {
                await DiGi.PostgreSQL.Modify.RemoveTableAsync(connectionData, converter.TableName);

                await using NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection_Cleanup is not null)
                {
                    await npgsqlConnection_Cleanup.OpenAsync();
                    await using NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection_Cleanup);
                    npgsqlCommand.Parameters.AddWithValue("tableName", converter.TableName);
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }
            }
        }

    }
}
