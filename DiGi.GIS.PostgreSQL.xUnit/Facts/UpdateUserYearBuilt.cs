using DiGi.GIS.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="YearBuiltDataPostgreSQLConverter.UpdateUserYearBuiltAsync"/> answers null when the reference is missing or the entry is null, without touching a database.
        /// </summary>
        [Fact]
        public async Task UpdateUserYearBuiltAsync_NullReferenceOrEntry_ReturnsNull()
        {
            YearBuiltDataPostgreSQLConverter converter = new(null);

            Assert.Null(await converter.UpdateUserYearBuiltAsync(5, null!, new UserYearBuilt(1975)));
            Assert.Null(await converter.UpdateUserYearBuiltAsync(5, "   ", new UserYearBuilt(1975)));
            Assert.Null(await converter.UpdateUserYearBuiltAsync(5, "XUNIT-UYB-NULL", null!));
        }

        /// <summary>
        /// Verifies, measured on the development database, that a user year built write replaces the previous user entry on an existing row, leaving the prediction intact.
        /// <para>The object's entries are keyed by source, so setting the user entry replaces exactly the previous user entry; the prediction is untouched.</para>
        /// <para>Skipped by default: it seeds scratch county 990101 and needs <c>GIS_PostgreSQL_Main.conf</c> beside the test assembly pointing at a scratch database - never the deployed one.</para>
        /// </summary>
        [Fact(Skip = "Seeds scratch county 990101. Point GIS_PostgreSQL_Main.conf at a scratch database before running.")]
        public async Task UpdateUserYearBuilt_ReplacesExisting_DevDb()
        {
            (NpgsqlConnection? npgsqlConnection, YearBuiltDataPostgreSQLConverter? converter) = await ScratchConnectionAsync();
            Assert.NotNull(converter);
            Assert.NotNull(npgsqlConnection);

            int countyId = 0;
            try
            {
                countyId = await SeedScratchCountyAsync(npgsqlConnection);

                await SeedBuilding2DAsync(npgsqlConnection, countyId, "XUNIT-UYB-REPLACE");
                await SeedYearBuiltDataAsync(npgsqlConnection, countyId, "XUNIT-UYB-REPLACE", "xunit-uyb-1", 1950, 2008);

                bool? result = await converter.UpdateUserYearBuiltAsync(countyId, "XUNIT-UYB-REPLACE", new UserYearBuilt(1975));
                Assert.True(result);

                // Exactly one row, the user entry replaced, the prediction intact.
                Assert.Equal(1, await YearBuiltRowCountAsync(npgsqlConnection, countyId, "XUNIT-UYB-REPLACE"));
                Assert.Equal(1975, await UserYearAsync(npgsqlConnection, countyId, "XUNIT-UYB-REPLACE"));
                Assert.Equal(2008, await PredictedYearAsync(npgsqlConnection, countyId, "XUNIT-UYB-REPLACE"));
            }
            finally
            {
                await CleanupScratchCountyAsync(npgsqlConnection, countyId);
                npgsqlConnection?.Dispose();
            }
        }

        /// <summary>
        /// Verifies, measured on the development database, that a user year built write creates a row when the building holds none.
        /// <para>A building with no <c>year_built_data</c> rows gets one fresh object and therefore one new row.</para>
        /// <para>Skipped by default: it seeds scratch county 990101 and needs <c>GIS_PostgreSQL_Main.conf</c> beside the test assembly pointing at a scratch database - never the deployed one.</para>
        /// </summary>
        [Fact(Skip = "Seeds scratch county 990101. Point GIS_PostgreSQL_Main.conf at a scratch database before running.")]
        public async Task UpdateUserYearBuilt_CreatesRow_DevDb()
        {
            (NpgsqlConnection? npgsqlConnection, YearBuiltDataPostgreSQLConverter? converter) = await ScratchConnectionAsync();
            Assert.NotNull(converter);
            Assert.NotNull(npgsqlConnection);

            int countyId = 0;
            try
            {
                countyId = await SeedScratchCountyAsync(npgsqlConnection);

                // A building with no year_built_data row at all.
                await SeedBuilding2DAsync(npgsqlConnection, countyId, "XUNIT-UYB-CREATE");

                bool? result = await converter.UpdateUserYearBuiltAsync(countyId, "XUNIT-UYB-CREATE", new UserYearBuilt(1962));
                Assert.True(result);

                Assert.Equal(1, await YearBuiltRowCountAsync(npgsqlConnection, countyId, "XUNIT-UYB-CREATE"));
                Assert.Equal(1962, await UserYearAsync(npgsqlConnection, countyId, "XUNIT-UYB-CREATE"));
            }
            finally
            {
                await CleanupScratchCountyAsync(npgsqlConnection, countyId);
                npgsqlConnection?.Dispose();
            }
        }

        /// <summary>
        /// Verifies, measured on the development database, that the write resolves the building's county part through <c>building_2d</c>, so a caller naming a sibling part still lands the row under the building's own part.
        /// <para>Two polygon parts share the same county code; the building is filed under part A. Naming part B in the call resolves through the <c>building_2d</c> row to part A, and the <c>year_built_data</c> row lands under A.</para>
        /// <para>Skipped by default: it seeds scratch counties 990101 and 990102 and needs <c>GIS_PostgreSQL_Main.conf</c> beside the test assembly pointing at a scratch database - never the deployed one.</para>
        /// </summary>
        [Fact(Skip = "Seeds scratch counties 990101 and 990102. Point GIS_PostgreSQL_Main.conf at a scratch database before running.")]
        public async Task UpdateUserYearBuilt_ResolvesCountyPart_DevDb()
        {
            (NpgsqlConnection? npgsqlConnection, YearBuiltDataPostgreSQLConverter? converter) = await ScratchConnectionAsync();
            Assert.NotNull(converter);
            Assert.NotNull(npgsqlConnection);

            int countyId = 0;
            int countyId_Sibling = 0;
            try
            {
                countyId = await SeedScratchCountyAsync(npgsqlConnection);

                // A sibling part sharing the same code, as BDOT10k multi-part counties do.
                await ExecuteAsync(npgsqlConnection, "DELETE FROM administrative_areal_2d WHERE reference = 'XUNIT-COUNTY-990102';");
                await ExecuteAsync(npgsqlConnection, "INSERT INTO administrative_areal_2d (reference, code, name, type_id) VALUES ('XUNIT-COUNTY-990102', '990101', 'xunit scratch county sibling', 2);");

                await using NpgsqlCommand npgsqlCommand_Sibling = new("SELECT id FROM administrative_areal_2d WHERE reference = 'XUNIT-COUNTY-990102';", npgsqlConnection);
                object? siblingId = await npgsqlCommand_Sibling.ExecuteScalarAsync();
                Assert.NotNull(siblingId);
                countyId_Sibling = System.Convert.ToInt32(siblingId, CultureInfo.CurrentCulture);

                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS building_2d_{countyId_Sibling};");
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS year_built_data_{countyId_Sibling};");
                Assert.True(await npgsqlConnection.TableAsync_Building2D_Partition(countyId_Sibling));
                Assert.True(await npgsqlConnection.TableAsync_Building2DReferencedObject_Partition("year_built_data", countyId_Sibling));

                // The building is filed under part A, not under the sibling part B.
                await SeedBuilding2DAsync(npgsqlConnection, countyId, "XUNIT-UYB-PART");

                // The call names part B; the resolution through building_2d lands the write under part A.
                bool? result = await converter.UpdateUserYearBuiltAsync(countyId_Sibling, "XUNIT-UYB-PART", new UserYearBuilt(1960));
                Assert.True(result);

                Assert.Equal(1, await YearBuiltRowCountAsync(npgsqlConnection, countyId, "XUNIT-UYB-PART"));
                Assert.Equal(0, await YearBuiltRowCountAsync(npgsqlConnection, countyId_Sibling, "XUNIT-UYB-PART"));
            }
            finally
            {
                if (countyId_Sibling > 0)
                {
                    await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS year_built_data_{countyId_Sibling};");
                    await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS building_2d_{countyId_Sibling};");
                }

                await ExecuteAsync(npgsqlConnection, "DELETE FROM administrative_areal_2d WHERE reference = 'XUNIT-COUNTY-990102';");
                await CleanupScratchCountyAsync(npgsqlConnection, countyId);
                npgsqlConnection?.Dispose();
            }
        }

        /// <summary>
        /// Verifies, measured on the development database, that a failure during the upsert rolls back the transaction and leaves the prior entry intact.
        /// <para>A <c>BEFORE UPDATE</c> trigger on the partition raises an exception, so the <c>ON CONFLICT DO UPDATE</c> path fails after the read and before the commit. The prior user entry survives the rollback.</para>
        /// <para>Skipped by default: it seeds scratch county 990101 and needs <c>GIS_PostgreSQL_Main.conf</c> beside the test assembly pointing at a scratch database - never the deployed one.</para>
        /// </summary>
        [Fact(Skip = "Seeds scratch county 990101. Point GIS_PostgreSQL_Main.conf at a scratch database before running.")]
        public async Task UpdateUserYearBuilt_TransactionRollsBack_DevDb()
        {
            (NpgsqlConnection? npgsqlConnection, YearBuiltDataPostgreSQLConverter? converter) = await ScratchConnectionAsync();
            Assert.NotNull(converter);
            Assert.NotNull(npgsqlConnection);

            int countyId = 0;
            string functionName = "xunit_uyb_block_update_fn";
            string triggerName = "xunit_uyb_block_update";
            try
            {
                countyId = await SeedScratchCountyAsync(npgsqlConnection);

                await SeedBuilding2DAsync(npgsqlConnection, countyId, "XUNIT-UYB-ROLLBACK");
                await SeedYearBuiltDataAsync(npgsqlConnection, countyId, "XUNIT-UYB-ROLLBACK", "xunit-uyb-5", 1940, null);

                // The trigger fires on the ON CONFLICT DO UPDATE path, which is a BEFORE UPDATE, not a BEFORE INSERT.
                await ExecuteAsync(npgsqlConnection, $@"
                    CREATE OR REPLACE FUNCTION {functionName}() RETURNS trigger AS
                    $$ BEGIN
                        RAISE EXCEPTION 'xunit block update';
                    END;
                    $$ LANGUAGE plpgsql;");
                await ExecuteAsync(npgsqlConnection, $@"
                    CREATE TRIGGER {triggerName}
                    BEFORE UPDATE ON year_built_data_{countyId}
                    FOR EACH ROW
                    EXECUTE FUNCTION {functionName}();");

                bool? result = await converter.UpdateUserYearBuiltAsync(countyId, "XUNIT-UYB-ROLLBACK", new UserYearBuilt(1999));
                Assert.False(result);

                // The prior entry survives the rollback.
                Assert.Equal(1, await YearBuiltRowCountAsync(npgsqlConnection, countyId, "XUNIT-UYB-ROLLBACK"));
                Assert.Equal(1940, await UserYearAsync(npgsqlConnection, countyId, "XUNIT-UYB-ROLLBACK"));

                await ExecuteAsync(npgsqlConnection, $"DROP TRIGGER IF EXISTS {triggerName} ON year_built_data_{countyId};");
                await ExecuteAsync(npgsqlConnection, $"DROP FUNCTION IF EXISTS {functionName}();");
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, $"DROP TRIGGER IF EXISTS {triggerName} ON year_built_data_{countyId};");
                await ExecuteAsync(npgsqlConnection, $"DROP FUNCTION IF EXISTS {functionName}();");
                await CleanupScratchCountyAsync(npgsqlConnection, countyId);
                npgsqlConnection?.Dispose();
            }
        }

        /// <summary>
        /// Counts the <c>year_built_data</c> rows of one building under one county part.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection.</param>
        /// <param name="countyId">The county part to count under.</param>
        /// <param name="reference">The building reference to count.</param>
        /// <returns>The row count.</returns>
        private static async Task<int> YearBuiltRowCountAsync(NpgsqlConnection npgsqlConnection, int countyId, string reference)
        {
            string commandText = $@"
                SELECT COUNT(*)
                FROM year_built_data
                WHERE county_id = {countyId}
                  AND reference = '{reference}';";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            object? count = await npgsqlCommand.ExecuteScalarAsync();
            return System.Convert.ToInt32(count, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Reads the user year built entry's year from the stored <c>year_built_data</c> rows of one building, or null when none is held.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection.</param>
        /// <param name="countyId">The county part to read under.</param>
        /// <param name="reference">The building reference to read.</param>
        /// <returns>The user entry year, or null when the building holds no user entry.</returns>
        private static async Task<int?> UserYearAsync(NpgsqlConnection npgsqlConnection, int countyId, string reference)
        {
            string commandText = $@"
                SELECT (value->>'Year')::int
                FROM year_built_data y, jsonb_array_elements(y.object->'YearBuilts') AS entry(value)
                WHERE y.county_id = {countyId}
                  AND y.reference = '{reference}'
                  AND value->>'_type' = 'DiGi.GIS.Classes.UserYearBuilt,DiGi.GIS';";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            object? year = await npgsqlCommand.ExecuteScalarAsync();
            return year is null ? null : System.Convert.ToInt32(year, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Reads the predicted year built entry's year from the stored <c>year_built_data</c> rows of one building, or null when none is held.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection.</param>
        /// <param name="countyId">The county part to read under.</param>
        /// <param name="reference">The building reference to read.</param>
        /// <returns>The prediction entry year, or null when the building holds no prediction.</returns>
        private static async Task<int?> PredictedYearAsync(NpgsqlConnection npgsqlConnection, int countyId, string reference)
        {
            string commandText = $@"
                SELECT (value->>'Year')::int
                FROM year_built_data y, jsonb_array_elements(y.object->'YearBuilts') AS entry(value)
                WHERE y.county_id = {countyId}
                  AND y.reference = '{reference}'
                  AND value->>'_type' = 'DiGi.GIS.Classes.PredictedYearBuilt,DiGi.GIS';";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            object? year = await npgsqlCommand.ExecuteScalarAsync();
            return year is null ? null : System.Convert.ToInt32(year, CultureInfo.CurrentCulture);
        }
    }
}
