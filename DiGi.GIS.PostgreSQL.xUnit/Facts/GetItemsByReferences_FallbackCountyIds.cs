using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Building2DReferencedObjectPostgreSQLConverter{TBuilding2DReferencedObject, TBuilding2DReferencedObject_DiGi}.GetItemsByReferencesAsync(NpgsqlConnection?, IEnumerable{string}?, int?, long?, bool, IEnumerable{int}?, int, System.Threading.CancellationToken)"/> restricts its fallback to the county parts it is given.
        /// <para>A reference missing under the asked part is searched only under the given sibling parts, so a row of the same reference filed under an unrelated county is not returned; without parts the fallback keeps searching every county, as it did before; naming only the asked part runs no fallback at all; and with the fallback off the parts are ignored. The narrowed search is what keeps a county's first prediction run - every reference missing - pruned to the county's own partitions instead of reading the whole table (29 s for 10 095 references, timing out under load).</para>
        /// <para>Medium test (a live database server): it writes the scratch partitions of 990201, 990202 and 990203 and drops them before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task GetItemsByReferences_FallbackCountyIds()
        {
            const int countyId_Other = 990203;

            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            YearBuiltDataPostgreSQLConverter yearBuiltDataPostgreSQLConverter = new(connectionData_Main);

            async Task CleanupAsync()
            {
                await CleanupYearBuiltDataScratchAsync(connectionData_Main, connectionData_Storage);

                await using NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
                Assert.NotNull(npgsqlConnection_Cleanup);
                await npgsqlConnection_Cleanup.OpenAsync();
                await ExecuteAsync(npgsqlConnection_Cleanup, $"DROP TABLE IF EXISTS {Constants.TableName.YearBuiltData}_{countyId_Other};");
            }

            await CleanupAsync();
            try
            {
                await SeedYearBuiltDatasAsync(yearBuiltDataPostgreSQLConverter, countyId_YearBuilt_A, ScratchYearBuiltData("XUNIT-FB-ASKED", 1950, "reviewer@example.com"));
                await SeedYearBuiltDatasAsync(yearBuiltDataPostgreSQLConverter, countyId_YearBuilt_B, ScratchYearBuiltData("XUNIT-FB-SIBLING", 1960, "reviewer@example.com"));
                await SeedYearBuiltDatasAsync(yearBuiltDataPostgreSQLConverter, countyId_Other, ScratchYearBuiltData("XUNIT-FB-OTHER", 1970, "reviewer@example.com"));

                string[] references = ["XUNIT-FB-ASKED", "XUNIT-FB-SIBLING", "XUNIT-FB-OTHER", "XUNIT-FB-NONE"];

                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();

                async Task<List<string>> ReferencesAsync(bool fallbackByReference, IEnumerable<int>? fallbackCountyIds)
                {
                    List<YearBuiltData>? yearBuiltDatas = await yearBuiltDataPostgreSQLConverter.GetItemsByReferencesAsync(npgsqlConnection, references, countyId_YearBuilt_A, null, fallbackByReference, fallbackCountyIds);
                    Assert.NotNull(yearBuiltDatas);

                    return [.. yearBuiltDatas.Select(x => x.Reference).OfType<string>().OrderBy(x => x, System.StringComparer.Ordinal)];
                }

                // The sibling parts only: the row under an unrelated county is not reached.
                Assert.Equal(["XUNIT-FB-ASKED", "XUNIT-FB-SIBLING"], await ReferencesAsync(true, [countyId_YearBuilt_A, countyId_YearBuilt_B]));

                // No parts: the whole-table search as before.
                Assert.Equal(["XUNIT-FB-ASKED", "XUNIT-FB-OTHER", "XUNIT-FB-SIBLING"], await ReferencesAsync(true, null));

                // Only the asked part: a single-part county, nothing to fall back to.
                Assert.Equal(["XUNIT-FB-ASKED"], await ReferencesAsync(true, [countyId_YearBuilt_A]));

                // Fallback off: the parts are ignored.
                Assert.Equal(["XUNIT-FB-ASKED"], await ReferencesAsync(false, [countyId_YearBuilt_A, countyId_YearBuilt_B]));
            }
            finally
            {
                await CleanupAsync();
            }
        }
    }
}
