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
        /// Verifies, on the throwaway main test database, that both address reads of <see cref="AddressPointPostgreSQLConverter"/> hand back the county part the row is filed under: the free-text search and the reverse lookup by building reference each carry <see cref="AddressPointMatch.CountyId"/> (DiGi-Organization/DiGi.GIS.WebAPI#85).
        /// <para>The search runs without the county filter over a street name nothing else in the test database holds, so the county id asserted is the one read from the row's <c>county_id</c> column and never one passed in. The row is written under scratch county part <see cref="countyId_AddressPoint_A"/> and removed before and in <c>finally</c>. Red on the unmodified code, where the member does not exist and the statement still selected the <c>object</c> column alone.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task AddressPointPostgreSQLConverter_MatchesCarryCountyId()
        {
            const string referenceId = "XUNIT-COUNTYID-1";
            const string reference_Building = "XUNIT-COUNTYID-BUILDING-1";

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_AddressPoint());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.AddressPoint} WHERE reference_id LIKE 'XUNIT-COUNTYID-%';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                DateTimeOffset version = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                AddressPoint addressPoint = new(new Point2D(-9999999, -9999999), "Byaddressowa", null, "1", "Warszawa", null, null, referenceId, version);
                AddressPointMatch addressPointMatch = new(addressPoint, AddressPointMatchType.Inside, reference_Building, Guid.NewGuid(), 0.0, null);

                AddressPointPostgreSQLConverter addressPointPostgreSQLConverter = new(connectionData_Main);
                Assert.Equal(1L, await addressPointPostgreSQLConverter.UpdateAsync(countyId_AddressPoint_A, [addressPointMatch]));

                // No county filter: the only row the text can reach is the scratch one, so CountyId can only come from its column.
                List<AddressPointMatch>? addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("byaddressowa", null, null);
                Assert.NotNull(addressPointMatches);

                AddressPointMatch? addressPointMatch_Search = addressPointMatches.Find(x => x?.AddressPoint?.ReferenceId == referenceId);
                Assert.NotNull(addressPointMatch_Search);
                Assert.Equal(countyId_AddressPoint_A, addressPointMatch_Search.CountyId);
                Assert.Equal(reference_Building, addressPointMatch_Search.Reference);

                List<AddressPointMatch>? addressPointMatches_Reverse = await addressPointPostgreSQLConverter.GetAddressPointMatchesByReferencesAsync([reference_Building]);
                Assert.NotNull(addressPointMatches_Reverse);

                AddressPointMatch? addressPointMatch_Reverse = addressPointMatches_Reverse.Find(x => x?.AddressPoint?.ReferenceId == referenceId);
                Assert.NotNull(addressPointMatch_Reverse);
                Assert.Equal(countyId_AddressPoint_A, addressPointMatch_Reverse.CountyId);
                Assert.Equal(reference_Building, addressPointMatch_Reverse.Reference);
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
