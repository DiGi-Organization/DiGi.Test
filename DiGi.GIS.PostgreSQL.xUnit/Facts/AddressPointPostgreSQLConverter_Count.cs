using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The first scratch county part of the address point count fact. Only a value in the throwaway main test database.
        /// </summary>
        private const int countyId_AddressPointCount_A = 990403;

        /// <summary>
        /// The second scratch county part of the address point count fact.
        /// </summary>
        private const int countyId_AddressPointCount_B = 990404;

        /// <summary>
        /// Verifies, on the throwaway main test database, <see cref="AddressPointPostgreSQLConverter.GetCountResultAsync(int?, int, System.Threading.CancellationToken)"/>: the counts per match type for one county part, the whole-table count rising by exactly the scratch rows, a county part with no rows counting zero, and a null connection answering null.
        /// <para>The whole table may hold rows the fact did not write, so the whole-table assertion compares the count before and after the scratch rows are stored, never an absolute figure. The fact removes its rows (<c>XUNIT-ADDRESSCOUNT-*</c>) before and in <c>finally</c>. Medium test (a live database server). Reference: DiGi.GIS.PostgreSQL#110.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task AddressPointPostgreSQLConverter_Count()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_AddressPoint());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.AddressPoint} WHERE reference_id LIKE 'XUNIT-ADDRESSCOUNT-%';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                AddressPointPostgreSQLConverter addressPointPostgreSQLConverter = new(connectionData_Main);

                AddressPointCountResult? addressPointCountResult_Before = await addressPointPostgreSQLConverter.GetCountResultAsync(null);
                Assert.NotNull(addressPointCountResult_Before);
                Assert.Null(addressPointCountResult_Before.CountyId);

                DateTimeOffset version = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

                List<AddressPointMatch> addressPointMatches_A =
                [
                    ScratchAddressPointMatch("XUNIT-ADDRESSCOUNT-1", AddressPointMatchType.Inside, version),
                    ScratchAddressPointMatch("XUNIT-ADDRESSCOUNT-2", AddressPointMatchType.Inside, version),
                    ScratchAddressPointMatch("XUNIT-ADDRESSCOUNT-3", AddressPointMatchType.Nearest, version),
                    ScratchAddressPointMatch("XUNIT-ADDRESSCOUNT-4", AddressPointMatchType.Unmatched, version),
                ];

                List<AddressPointMatch> addressPointMatches_B =
                [
                    ScratchAddressPointMatch("XUNIT-ADDRESSCOUNT-5", AddressPointMatchType.Unmatched, version),
                    ScratchAddressPointMatch("XUNIT-ADDRESSCOUNT-6", AddressPointMatchType.Unmatched, version),
                ];

                Assert.Equal(4L, await addressPointPostgreSQLConverter.UpdateAsync(countyId_AddressPointCount_A, addressPointMatches_A));
                Assert.Equal(2L, await addressPointPostgreSQLConverter.UpdateAsync(countyId_AddressPointCount_B, addressPointMatches_B));

                AddressPointCountResult? addressPointCountResult_A = await addressPointPostgreSQLConverter.GetCountResultAsync(countyId_AddressPointCount_A);
                Assert.NotNull(addressPointCountResult_A);
                Assert.Equal(countyId_AddressPointCount_A, addressPointCountResult_A.CountyId);
                Assert.Equal(2L, addressPointCountResult_A.Inside);
                Assert.Equal(1L, addressPointCountResult_A.Nearest);
                Assert.Equal(1L, addressPointCountResult_A.Unmatched);
                Assert.Equal(4L, addressPointCountResult_A.Total);

                AddressPointCountResult? addressPointCountResult_B = await addressPointPostgreSQLConverter.GetCountResultAsync(countyId_AddressPointCount_B);
                Assert.NotNull(addressPointCountResult_B);
                Assert.Equal(0L, addressPointCountResult_B.Inside);
                Assert.Equal(0L, addressPointCountResult_B.Nearest);
                Assert.Equal(2L, addressPointCountResult_B.Unmatched);
                Assert.Equal(2L, addressPointCountResult_B.Total);

                // The whole table rises by exactly the scratch rows
                AddressPointCountResult? addressPointCountResult_After = await addressPointPostgreSQLConverter.GetCountResultAsync(null);
                Assert.NotNull(addressPointCountResult_After);
                Assert.Equal(addressPointCountResult_Before.Inside + 2L, addressPointCountResult_After.Inside);
                Assert.Equal(addressPointCountResult_Before.Nearest + 1L, addressPointCountResult_After.Nearest);
                Assert.Equal(addressPointCountResult_Before.Unmatched + 3L, addressPointCountResult_After.Unmatched);
                Assert.Equal(addressPointCountResult_Before.Total + 6L, addressPointCountResult_After.Total);

                // A county part with no rows counts zero, not null
                AddressPointCountResult? addressPointCountResult_Empty = await addressPointPostgreSQLConverter.GetCountResultAsync(countyId_AddressPointCount_B + 1);
                Assert.NotNull(addressPointCountResult_Empty);
                Assert.Equal(0L, addressPointCountResult_Empty.Total);

                // The static overload on a null connection
                Assert.Null(await AddressPointPostgreSQLConverter.GetCountResultAsync(null, null));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }

            static AddressPointMatch ScratchAddressPointMatch(string referenceId, AddressPointMatchType addressPointMatchType, DateTimeOffset version)
            {
                AddressPoint addressPoint = new(new Point2D(-9999999, -9999999), "Skrzyzowanie", null, "1", "Testowo", null, null, referenceId, version);
                return addressPointMatchType == AddressPointMatchType.Unmatched
                    ? new AddressPointMatch(addressPoint, addressPointMatchType, null, null, null)
                    : new AddressPointMatch(addressPoint, addressPointMatchType, "XUNIT-ADDRESSCOUNT-BUILDING", Guid.NewGuid(), addressPointMatchType == AddressPointMatchType.Inside ? 0.0 : 5.0);
            }
        }
    }
}
