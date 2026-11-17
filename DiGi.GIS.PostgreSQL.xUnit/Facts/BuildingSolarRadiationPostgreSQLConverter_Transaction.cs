using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the all-or-nothing <see cref="BuildingSolarRadiationPostgreSQLConverter"/> write over several county parts on the throwaway storage test database.
        /// <para>The guard first: the per-part batch write steps over a result whose factors cannot be packed and stores the rest, so a failing batch leaves rows behind. The transactional write stores the results of two parts and answers their count. A batch whose second part holds an unpackable result answers null and stores nothing - not the first part's new rows, and not its replacement of a row already stored, which keeps its old summary. So does a part holding two results with one key. An empty batch answers 0 and a null one null.</para>
        /// <para>Medium test (a live database server): it writes the scratch partitions of 990301 and 990302 and drops them before and after. Reference: DiGi.GIS.WebAPI#93.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationPostgreSQLConverter_Transaction()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            BuildingSolarRadiationPostgreSQLConverter buildingSolarRadiationPostgreSQLConverter = new(connectionData_Storage);

            await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);
            try
            {
                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();

                List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations_Unequal =
                [
                    new("b1a5c1de-0000-4000-8000-000000000001", 10, null, 1, 1, 0, 0, 0, 0, 0, 0, new byte[hourCount_SolarReferenceYear]),
                    new("b1a5c1de-0000-4000-8000-000000000002", 10, null, 1, 1, 0, 0, 0, 0, 0, 0, new byte[hourCount_SolarReferenceYear - 1]),
                ];

                // The guard: without the transaction, a batch holding an unpackable result stores the rest of it.
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_Guard = ScratchBuildingSolarRadiationResult("XUNIT-TX-0", 990301100L, "17", null, 1.0, 180);
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_GuardUnequal = new("XUNIT-TX-0U", countyId_SolarRadiation_A, 990301199L, 50.0, 1, GIS.Enums.WeatherSourceType.EPW, "17", null, null, null, null, surfaceSolarRadiations_Unequal);
                Assert.Equal(1, await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(countyId_SolarRadiation_A, [buildingSolarRadiationResult_Guard, buildingSolarRadiationResult_GuardUnequal]));
                Assert.Equal(1L, await CountTransactionScratchRowsAsync(npgsqlConnection));
                await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);

                // Two parts, every result valid: all of them are stored, each under its listed part.
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_A1 = ScratchBuildingSolarRadiationResult("XUNIT-TX-A1", 990301101L, "17", "first@example.com", 1.0, 180, 270);
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_A2 = ScratchBuildingSolarRadiationResult("XUNIT-TX-A2", 990301102L, "17", "first@example.com", 1.0, 90);
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_B1 = ScratchBuildingSolarRadiationResult("XUNIT-TX-B1", 990301103L, "17", "first@example.com", 1.0, 0);

                Dictionary<int, List<GIS.Classes.BuildingSolarRadiationResult>> buildingSolarRadiationResults_ByCountyId = new()
                {
                    [countyId_SolarRadiation_A] = [buildingSolarRadiationResult_A1, buildingSolarRadiationResult_A2],
                    [countyId_SolarRadiation_B] = [buildingSolarRadiationResult_B1],
                };
                Assert.Equal(3, await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(buildingSolarRadiationResults_ByCountyId));
                Assert.Equal(3L, await CountTransactionScratchRowsAsync(npgsqlConnection));
                AssertBuildingSolarRadiationResult(buildingSolarRadiationResult_A1, await buildingSolarRadiationPostgreSQLConverter.GetAsync(countyId_SolarRadiation_A, 990301101L, 50.0, 1, true, false), true);
                AssertBuildingSolarRadiationResult(buildingSolarRadiationResult_B1, await buildingSolarRadiationPostgreSQLConverter.GetAsync(countyId_SolarRadiation_B, 990301103L, 50.0, 1, true, false), true);

                // The second part holds an unpackable result: nothing of the batch is stored, and the replacement of A1 is undone too.
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_A1Replacement = ScratchBuildingSolarRadiationResult("XUNIT-TX-A1", 990301101L, "23", "second@example.com", 2.0, 180, 270);
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_A3 = ScratchBuildingSolarRadiationResult("XUNIT-TX-A3", 990301104L, "23", "second@example.com", 2.0, 45);
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_BUnequal = new("XUNIT-TX-BU", countyId_SolarRadiation_B, 990301105L, 50.0, 1, GIS.Enums.WeatherSourceType.EPW, "23", null, null, null, null, surfaceSolarRadiations_Unequal);

                buildingSolarRadiationResults_ByCountyId = new()
                {
                    [countyId_SolarRadiation_A] = [buildingSolarRadiationResult_A1Replacement, buildingSolarRadiationResult_A3],
                    [countyId_SolarRadiation_B] = [buildingSolarRadiationResult_BUnequal],
                };
                Assert.Null(await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(buildingSolarRadiationResults_ByCountyId));
                Assert.Equal(3L, await CountTransactionScratchRowsAsync(npgsqlConnection));
                Assert.Null(await buildingSolarRadiationPostgreSQLConverter.GetAsync(countyId_SolarRadiation_A, 990301104L, 50.0, 1, false, false));
                AssertBuildingSolarRadiationResult(buildingSolarRadiationResult_A1, await buildingSolarRadiationPostgreSQLConverter.GetAsync(countyId_SolarRadiation_A, 990301101L, 50.0, 1, true, false), true);

                // Two results with one key in a part: the batch is refused rather than reduced to the later one.
                GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult_A4 = ScratchBuildingSolarRadiationResult("XUNIT-TX-A4", 990301106L, "23", "second@example.com", 2.0, 135);
                buildingSolarRadiationResults_ByCountyId = new()
                {
                    [countyId_SolarRadiation_A] = [buildingSolarRadiationResult_A4, buildingSolarRadiationResult_A1Replacement, buildingSolarRadiationResult_A1Replacement],
                };
                Assert.Null(await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(buildingSolarRadiationResults_ByCountyId));
                Assert.Equal(3L, await CountTransactionScratchRowsAsync(npgsqlConnection));
                AssertBuildingSolarRadiationResult(buildingSolarRadiationResult_A1, await buildingSolarRadiationPostgreSQLConverter.GetAsync(countyId_SolarRadiation_A, 990301101L, 50.0, 1, true, false), true);

                // Nothing to store is 0; no batch at all is null.
                Assert.Equal(0, await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(new Dictionary<int, List<GIS.Classes.BuildingSolarRadiationResult>>()));
                Assert.Equal(0, await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(new Dictionary<int, List<GIS.Classes.BuildingSolarRadiationResult>>() { [countyId_SolarRadiation_A] = [] }));
                Assert.Null(await buildingSolarRadiationPostgreSQLConverter.UpdateAsync((IReadOnlyDictionary<int, List<GIS.Classes.BuildingSolarRadiationResult>>?)null));
            }
            finally
            {
                await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);
            }
        }

        /// <summary>
        /// Counts the rows of the transaction fact's scratch building models (ids 990301100 to 990301199) in both scratch county parts.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the storage test database.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of rows.</returns>
        private static async Task<long?> CountTransactionScratchRowsAsync(NpgsqlConnection npgsqlConnection)
        {
            return (long?)await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.BuildingSolarRadiation} WHERE county_id IN ({countyId_SolarRadiation_A}, {countyId_SolarRadiation_B}) AND building_model_id BETWEEN 990301100 AND 990301199;");
        }
    }
}
