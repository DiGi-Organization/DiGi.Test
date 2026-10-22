using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.PostgreSQL.Classes;
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
        /// The first scratch county part of the address point fact. Only a value in the throwaway main test database.
        /// </summary>
        private const int countyId_AddressPoint_A = 990401;

        /// <summary>
        /// The second scratch county part of the address point fact, standing for a sibling polygon part.
        /// </summary>
        private const int countyId_AddressPoint_B = 990402;

        /// <summary>
        /// Verifies, on the throwaway main test database, the storage and search of <see cref="AddressPointPostgreSQLConverter"/>: the upsert, the fuzzy and partial search over the normalised <c>search_text</c>, the house number exact-then-prefix match, the county part filter, the version guard of a re-import, the reverse lookup by building reference and the delete.
        /// <para>The fact writes scratch rows (<c>XUNIT-ADDRESS-*</c>) under scratch county parts and searches with the county filter, so rows anything else stored in the table cannot reach the assertions; it removes its rows before and in <c>finally</c>. The table and the GIN trigram index are created when the test database has none. A table this small is always read by a sequential scan, so the fact asserts that the index exists, not that the planner uses it. Medium test (a live database server). Reference: DiGi.GIS.PostgreSQL#103.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task AddressPointPostgreSQLConverter_SearchAndUpdate()
        {
            const string reference_Building_1 = "XUNIT-ADDRESS-BUILDING-1";
            const string reference_Building_2 = "XUNIT-ADDRESS-BUILDING-2";

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_AddressPoint());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.AddressPoint} WHERE reference_id LIKE 'XUNIT-ADDRESS-%';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                DateTimeOffset version = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

                List<AddressPointMatch> addressPointMatches_A =
                [
                    ScratchAddressPointMatch("XUNIT-ADDRESS-1", "Marszałkowska", "12", "Warszawa", version, reference_Building_1),
                    ScratchAddressPointMatch("XUNIT-ADDRESS-2", "Marszałkowska", "12A", "Warszawa", version, reference_Building_1),
                    ScratchAddressPointMatch("XUNIT-ADDRESS-3", "Marszałkowska", "12/3", "Warszawa", version, reference_Building_1),
                    ScratchAddressPointMatch("XUNIT-ADDRESS-4", "Marszałkowska", "120", "Warszawa", version, reference_Building_2),
                    ScratchAddressPointMatch("XUNIT-ADDRESS-5", "Łódzka", "5", "Zgierz", version, reference_Building_2),
                    ScratchAddressPointMatch("XUNIT-ADDRESS-6", "al. Jana Pawła II", "1", "Kraków", version, null),
                ];

                List<AddressPointMatch> addressPointMatches_B =
                [
                    ScratchAddressPointMatch("XUNIT-ADDRESS-7", null, "7", "Żółtki", version, null),
                    ScratchAddressPointMatch("XUNIT-ADDRESS-8", "Marszałkowska", "14", "Warszawa", version, null),
                ];

                AddressPointPostgreSQLConverter addressPointPostgreSQLConverter = new(connectionData_Main);

                Assert.Equal(6L, await addressPointPostgreSQLConverter.UpdateAsync(countyId_AddressPoint_A, addressPointMatches_A));
                Assert.Equal(2L, await addressPointPostgreSQLConverter.UpdateAsync(countyId_AddressPoint_B, addressPointMatches_B, batchSize: 1));

                object? count_Index = await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM pg_indexes WHERE indexname = 'idx_{Constants.TableName.AddressPoint}_search_text';");
                Assert.Equal(1L, count_Index);

                // A typo
                List<AddressPointMatch>? addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("marszalkowsak", null, countyId_AddressPoint_A);
                Assert.NotNull(addressPointMatches);
                Assert.Equal(4, addressPointMatches.Count);
                Assert.All(addressPointMatches, x => Assert.Equal("Marszałkowska", x.AddressPoint?.StreetName));

                // A missing diacritic
                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("lodzka", null, countyId_AddressPoint_A);
                Assert.NotNull(addressPointMatches);
                Assert.Equal("XUNIT-ADDRESS-5", addressPointMatches.FirstOrDefault()?.AddressPoint?.ReferenceId);

                // A partial street name
                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("Pawła", null, countyId_AddressPoint_A);
                Assert.NotNull(addressPointMatches);
                Assert.Equal("XUNIT-ADDRESS-6", Assert.Single(addressPointMatches).AddressPoint?.ReferenceId);

                // House number: the exact match first, then the prefix matches, never a longer number
                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("ul. Marszałkowska", "12", countyId_AddressPoint_A);
                Assert.NotNull(addressPointMatches);
                Assert.Equal(["XUNIT-ADDRESS-1", "XUNIT-ADDRESS-3", "XUNIT-ADDRESS-2"], addressPointMatches.Select(x => x.AddressPoint?.ReferenceId));

                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("Marszałkowska", "12a", countyId_AddressPoint_A);
                Assert.NotNull(addressPointMatches);
                Assert.Equal("XUNIT-ADDRESS-2", Assert.Single(addressPointMatches).AddressPoint?.ReferenceId);

                // The county part filter and a streetless locality
                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("Marszałkowska", null, countyId_AddressPoint_B);
                Assert.NotNull(addressPointMatches);
                AddressPointMatch addressPointMatch_Unmatched = Assert.Single(addressPointMatches);
                Assert.Equal("XUNIT-ADDRESS-8", addressPointMatch_Unmatched.AddressPoint?.ReferenceId);
                Assert.Equal(AddressPointMatchType.Unmatched, addressPointMatch_Unmatched.AddressPointMatchType);
                Assert.Null(addressPointMatch_Unmatched.Reference);

                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("zoltki", "7", countyId_AddressPoint_B);
                Assert.NotNull(addressPointMatches);
                Assert.Equal("XUNIT-ADDRESS-7", Assert.Single(addressPointMatches).AddressPoint?.ReferenceId);

                // Text that normalises to nothing, and the limit
                Assert.Null(await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("%", null, null));
                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("Marszałkowska", null, countyId_AddressPoint_A, limit: 2);
                Assert.NotNull(addressPointMatches);
                Assert.Equal(2, addressPointMatches.Count);

                // The reverse lookup
                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByReferencesAsync([reference_Building_1, reference_Building_2, "XUNIT-ADDRESS-BUILDING-NONE"]);
                Assert.NotNull(addressPointMatches);
                Assert.Equal(5, addressPointMatches.Count);
                Assert.Equal(3, addressPointMatches.Count(x => x.Reference == reference_Building_1));
                Assert.Equal(AddressPointMatchType.Inside, addressPointMatches[0].AddressPointMatchType);
                Assert.Equal(0.0, addressPointMatches[0].Distance);

                // A re-import of an older version does not overwrite; a newer one does, and moves the point to its new county part
                AddressPointMatch addressPointMatch_Old = ScratchAddressPointMatch("XUNIT-ADDRESS-1", "Marszałkowska", "99", "Warszawa", version.AddYears(-1), reference_Building_1);
                Assert.Equal(0L, await addressPointPostgreSQLConverter.UpdateAsync(countyId_AddressPoint_A, [addressPointMatch_Old]));
                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("Marszałkowska", "12", countyId_AddressPoint_A);
                Assert.Equal("XUNIT-ADDRESS-1", addressPointMatches?.FirstOrDefault()?.AddressPoint?.ReferenceId);

                AddressPointMatch addressPointMatch_New = ScratchAddressPointMatch("XUNIT-ADDRESS-1", "Marszałkowska", "99", "Warszawa", version.AddYears(1), reference_Building_1);
                Assert.Equal(1L, await addressPointPostgreSQLConverter.UpdateAsync(countyId_AddressPoint_B, [addressPointMatch_Old, addressPointMatch_New]));
                addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByAddressAsync("Marszałkowska", "99", countyId_AddressPoint_B);
                Assert.NotNull(addressPointMatches);
                Assert.Equal(version.AddYears(1), Assert.Single(addressPointMatches).AddressPoint?.Version);
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.AddressPoint} WHERE reference_id = 'XUNIT-ADDRESS-1';"));

                // Skipped input and null arguments
                Assert.Equal(0L, await addressPointPostgreSQLConverter.UpdateAsync(countyId_AddressPoint_A, [null, new AddressPointMatch(null, AddressPointMatchType.Unmatched, null, null, null)]));
                Assert.Null(await AddressPointPostgreSQLConverter.UpdateAsync(null, countyId_AddressPoint_A, addressPointMatches_A));
                Assert.Null(await AddressPointPostgreSQLConverter.GetAddressPointMatchesByReferencesAsync(npgsqlConnection, null));

                // The delete
                Assert.Equal(8L, await addressPointPostgreSQLConverter.DeleteByReferenceIdsAsync([.. addressPointMatches_A.Concat(addressPointMatches_B).Select(x => x.AddressPoint!.ReferenceId!), "XUNIT-ADDRESS-NONE"]));
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.AddressPoint} WHERE reference_id LIKE 'XUNIT-ADDRESS-%';"));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }

            static AddressPointMatch ScratchAddressPointMatch(string referenceId, string? streetName, string houseNumber, string localityName, DateTimeOffset version, string? reference)
            {
                AddressPoint addressPoint = new(new Point2D(-9999999, -9999999), streetName, null, houseNumber, localityName, null, null, referenceId, version);
                return reference is null
                    ? new AddressPointMatch(addressPoint, AddressPointMatchType.Unmatched, null, null, null)
                    : new AddressPointMatch(addressPoint, AddressPointMatchType.Inside, reference, Guid.NewGuid(), 0.0);
            }
        }
    }
}
