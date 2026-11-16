using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.IO;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.xUnit.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="PostgreSQL.Classes.BuildingSolarRadiationCountyResult"/> survives a string round trip and <c>SerializationCheck</c>, and that the copy constructor copies every member (DiGi.GIS.PostgreSQL#138).
        /// </summary>
        [Fact]
        public void BuildingSolarRadiationCountyResult_Serialization()
        {
            DateTimeOffset createdAtMin = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
            DateTimeOffset createdAtMax = new(2026, 11, 16, 9, 30, 0, TimeSpan.Zero);
            BuildingSolarRadiationCountyResult buildingSolarRadiationCountyResult = new(55417, 50.0, 1, 154_938, 120_004, 98_765, createdAtMin, createdAtMax, 3_456_789_012);

            BuildingSolarRadiationCountyResult? buildingSolarRadiationCountyResult_Json = Core.Convert.ToDiGi<BuildingSolarRadiationCountyResult>(Core.Convert.ToSystem_String(buildingSolarRadiationCountyResult))?.FirstOrDefault();
            Assert.NotNull(buildingSolarRadiationCountyResult_Json);
            Assert.Equal(55417, buildingSolarRadiationCountyResult_Json.CountyId);
            Assert.Equal(50.0, buildingSolarRadiationCountyResult_Json.Radius);
            Assert.Equal(98_765L, buildingSolarRadiationCountyResult_Json.ResidentialCoveredCount);
            Assert.Equal(createdAtMax, buildingSolarRadiationCountyResult_Json.CreatedAtMax);
            Assert.Equal(3_456_789_012L, buildingSolarRadiationCountyResult_Json.PartitionSize);

            BuildingSolarRadiationCountyResult buildingSolarRadiationCountyResult_Clone = new(buildingSolarRadiationCountyResult);
            Assert.Equal(55417, buildingSolarRadiationCountyResult_Clone.CountyId);
            Assert.Equal(50.0, buildingSolarRadiationCountyResult_Clone.Radius);
            Assert.Equal(1, buildingSolarRadiationCountyResult_Clone.SolverVersion);
            Assert.Equal(154_938L, buildingSolarRadiationCountyResult_Clone.RowCount);
            Assert.Equal(120_004L, buildingSolarRadiationCountyResult_Clone.ResidentialBuildingCount);
            Assert.Equal(98_765L, buildingSolarRadiationCountyResult_Clone.ResidentialCoveredCount);
            Assert.Equal(createdAtMin, buildingSolarRadiationCountyResult_Clone.CreatedAtMin);
            Assert.Equal(createdAtMax, buildingSolarRadiationCountyResult_Clone.CreatedAtMax);
            Assert.Equal(3_456_789_012L, buildingSolarRadiationCountyResult_Clone.PartitionSize);

            Core.xUnit.Query.SerializationCheck(buildingSolarRadiationCountyResult);
            Core.xUnit.Query.SerializationCheck(new BuildingSolarRadiationCountyResult(1432, 0.0, 1, 3, null, null, null, null, null));
        }

        /// <summary>
        /// Verifies <see cref="BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(NpgsqlConnection?, IEnumerable{int}?, int?, BuildingDataPostgreSQLConverter?, int, System.Threading.CancellationToken)"/> on the throwaway storage test database (DiGi.GIS.PostgreSQL#138).
        /// <para>Three scratch buildings are stored at radii 0 and 50 under part 990304; the scratch building data (<see cref="ScratchBuildingDataPostgreSQLConverter"/>) marks two of them and a fourth building without results as residential. Per radius: 3 rows, 3 residential buildings, 2 of them covered, the same partition size above zero, and a created-at span. An unknown part and an unknown solver version answer an empty list; an unscoped read holds the same rows for the part as the scoped one; without building data the residential counts are null.</para>
        /// <para>Medium test (a live database server): it writes the scratch partition of 990304 and the scratch building data table, and drops both before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationPostgreSQLConverter_Summaries()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            BuildingSolarRadiationPostgreSQLConverter buildingSolarRadiationPostgreSQLConverter = new(connectionData_Storage);
            ScratchBuildingDataPostgreSQLConverter scratchBuildingDataPostgreSQLConverter = new(connectionData_Storage);

            await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);
            await CleanupBuildingSolarRadiationSummaryScratchAsync(connectionData_Storage);
            try
            {
                DateTimeOffset dateTimeOffset = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
                List<GIS.Classes.BuildingSolarRadiationResult?> results = [];
                for (int i = 1; i <= 3; i++)
                {
                    foreach (double radius in new double[] { 0.0, 50.0 })
                    {
                        results.Add(SolarVariant($"XUNIT-SS-{i}", 990304100L + i, radius, 1, dateTimeOffset.AddMinutes(i), 1.0));
                    }
                }

                Assert.Equal(6, await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(countyId_SolarRadiation_Summary, results));

                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();

                // Before any building data exists the residential counts are unknown, not zero.
                List<BuildingSolarRadiationCountyResult>? summaries_NoBuildingData = await BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(npgsqlConnection, [countyId_SolarRadiation_Summary], buildingDataPostgreSQLConverter: scratchBuildingDataPostgreSQLConverter);
                Assert.NotNull(summaries_NoBuildingData);
                Assert.Equal(2, summaries_NoBuildingData.Count);
                Assert.All(summaries_NoBuildingData, x => Assert.Null(x.ResidentialBuildingCount));
                Assert.All(summaries_NoBuildingData, x => Assert.Null(x.ResidentialCoveredCount));

                Table table_BuildingData = new();
                Column column_CountyId = table_BuildingData.UpdateColumn<Column>(IO.Constants.Column.CountyId)!;
                Column column_Reference = table_BuildingData.UpdateColumn<Column>(IO.Constants.Column.Reference)!;
                Column column_IsResidential = table_BuildingData.UpdateColumn<Column>(IO.Constants.Column.IsResidential)!;
                foreach ((string reference, bool isResidential) in new[] { ("XUNIT-SS-1", true), ("XUNIT-SS-2", true), ("XUNIT-SS-3", false), ("XUNIT-SS-4", true) })
                {
                    Row row = table_BuildingData.AddRow();
                    IO.Modify.SetValue(row, column_CountyId, countyId_SolarRadiation_Summary);
                    IO.Modify.SetValue(row, column_Reference, reference);
                    IO.Modify.SetValue(row, column_IsResidential, isResidential);
                    table_BuildingData.AddRow(row, false);
                }

                Assert.True(await scratchBuildingDataPostgreSQLConverter.PushAsync(table_BuildingData));

                List<BuildingSolarRadiationCountyResult>? summaries = await BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(npgsqlConnection, [countyId_SolarRadiation_Summary], buildingDataPostgreSQLConverter: scratchBuildingDataPostgreSQLConverter);
                Assert.NotNull(summaries);
                Assert.Equal([0.0, 50.0], summaries.Select(x => x.Radius));
                Assert.All(summaries, x => Assert.Equal(countyId_SolarRadiation_Summary, x.CountyId));
                Assert.All(summaries, x => Assert.Equal(1, x.SolverVersion));
                Assert.All(summaries, x => Assert.Equal(3L, x.RowCount));
                Assert.All(summaries, x => Assert.Equal(3L, x.ResidentialBuildingCount));
                Assert.All(summaries, x => Assert.Equal(2L, x.ResidentialCoveredCount));
                Assert.All(summaries, x => Assert.True(x.PartitionSize > 0));
                Assert.Single(summaries.Select(x => x.PartitionSize).Distinct());
                Assert.All(summaries, x => Assert.Equal(dateTimeOffset.AddMinutes(1), x.CreatedAtMin));
                Assert.All(summaries, x => Assert.Equal(dateTimeOffset.AddMinutes(3), x.CreatedAtMax));

                Assert.Empty((await BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(npgsqlConnection, [990399], buildingDataPostgreSQLConverter: scratchBuildingDataPostgreSQLConverter))!);
                Assert.Empty((await BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(npgsqlConnection, [countyId_SolarRadiation_Summary], 99, scratchBuildingDataPostgreSQLConverter))!);
                Assert.Empty((await BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(npgsqlConnection, []))!);
                Assert.Equal(2, (await BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(npgsqlConnection, [countyId_SolarRadiation_Summary], 1, scratchBuildingDataPostgreSQLConverter))!.Count);

                List<BuildingSolarRadiationCountyResult>? summaries_Unscoped = await BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(npgsqlConnection, null, buildingDataPostgreSQLConverter: scratchBuildingDataPostgreSQLConverter);
                Assert.NotNull(summaries_Unscoped);
                List<BuildingSolarRadiationCountyResult> summaries_Unscoped_Part = [.. summaries_Unscoped.Where(x => x.CountyId == countyId_SolarRadiation_Summary)];
                Assert.Equal(summaries.Count, summaries_Unscoped_Part.Count);
                for (int i = 0; i < summaries.Count; i++)
                {
                    Assert.Equal(Core.Convert.ToSystem_String(summaries[i]), Core.Convert.ToSystem_String(summaries_Unscoped_Part[i]));
                }

                Assert.Null(await BuildingSolarRadiationPostgreSQLConverter.GetSummariesByCountyIdsAsync(null, [countyId_SolarRadiation_Summary]));
            }
            finally
            {
                await CleanupBuildingSolarRadiationSummaryScratchAsync(connectionData_Storage);
                await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);
            }
        }

        /// <summary>
        /// Drops the scratch building data table of the solar radiation summary fact on the storage test database.
        /// </summary>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task CleanupBuildingSolarRadiationSummaryScratchAsync(ConnectionData connectionData_Storage)
        {
            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {new ScratchBuildingDataPostgreSQLConverter(null).TableName} CASCADE;");
        }
    }
}
