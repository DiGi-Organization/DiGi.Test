using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Constants;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Claims from a queue table left in the shape it had before claiming existed, and checks that the claim brings it up to date rather than failing on it.
        /// <para>This is the regression guard for a deployed failure. Claiming was added with a backward-compatible <c>ADD COLUMN IF NOT EXISTS claimed_at</c> in the DDL, but the claim path guarded itself with a table-existence check - <c>to_regclass</c>, which answers for the table and knows nothing about its columns - and only the enqueue path ever ran the DDL. Every queue table already deployed therefore passed the guard and then raised <c>42703</c> on a column that was not there, which the converter logged and returned as null, and which the download task reported as an outright failure.</para>
        /// <para>The ordering is asserted for a second defect found with it: claims were ordered on the queuing time alone, so a row whose lease expired came back ahead of every row that had never been attempted. A run failing on what it reached first could re-attempt those forever without ever reaching the rest of the queue.</para>
        /// <para>The retirement is asserted for a third defect found there (DiGi.GIS.PostgreSQL#145): the poison-row <c>DELETE</c> that runs before every claim could not be served by any index, walked the whole growing queue on every claim and timed out on production - so an entry seeded at the attempt ceiling is now checked to be deleted by the claim that would have reached it, never returned and never left behind, and the migration is checked to create the index on <c>attempts</c> the retirement needs.</para>
        /// <para>Skipped by default: it drops and recreates the real queue table, so it needs <c>GIS_PostgreSQL_Storage.conf</c> beside the test assembly pointing at a scratch database. Never point it at a database whose queue holds work - the drop is not recoverable.</para>
        /// </summary>
        [Fact(Skip = "Drops and recreates the OrtoDatas queue table. Point GIS_PostgreSQL_Storage.conf at a scratch database before running.")]
        public async Task TableAsync_Building2DReference_Integration()
        {
            const string tableName = TableName.OrtoDatas_Building2DReference_Update;
            const int countyId = 5;

            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager();
            Assert.NotNull(gISPostgreSQLConverterManager);

            OrtoDatasPostgreSQLConverter? ortoDatasPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<OrtoDatasPostgreSQLConverter>();
            Assert.NotNull(ortoDatasPostgreSQLConverter);

            ConnectionData? connectionData = ortoDatasPostgreSQLConverter.ConnectionData;
            Assert.NotNull(connectionData);

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
            Assert.NotNull(npgsqlConnection);

            await npgsqlConnection.OpenAsync();

            try
            {
                //A queue table in the shape every deployed one was in: no claim column, no claim index

                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {tableName};");
                await ExecuteAsync(npgsqlConnection, CommandText_Building2DReference_Legacy(tableName));

                await ExecuteAsync(npgsqlConnection, $"INSERT INTO {tableName} (county_id, reference, created_at) VALUES ({countyId}, 'reference_older', now() - interval '2 hours');");
                await ExecuteAsync(npgsqlConnection, $"INSERT INTO {tableName} (county_id, reference, created_at) VALUES ({countyId}, 'reference_newer', now() - interval '1 hour');");

                Assert.DoesNotContain("claimed_at", await ColumnNamesAsync(npgsqlConnection, tableName));

                //Claiming migrates the table on its way through, rather than failing on the column it is missing

                List<Building2DReference>? building2DReferences = await ortoDatasPostgreSQLConverter.GetNextBuilding2DReferencesAsync(1);
                Assert.NotNull(building2DReferences);
                Assert.Single(building2DReferences);

                Assert.Contains("claimed_at", await ColumnNamesAsync(npgsqlConnection, tableName));
                Assert.Contains($"idx_{tableName}_claimed_at", await IndexNamesAsync(npgsqlConnection, tableName));
                Assert.Contains($"idx_{tableName}_attempts", await IndexNamesAsync(npgsqlConnection, tableName));

                //Queuing order decides which of the two is taken first, and the rows survive the migration

                Assert.Equal("reference_older", building2DReferences[0].Reference);
                Assert.Equal(2, await DiGi.PostgreSQL.Query.CountAsync(npgsqlConnection, tableName));

                //A row whose claim has expired goes behind one that has never been claimed, not ahead of it.
                //Asked with a zero minute lease, the first row is expired the moment it was claimed, so
                //ordering on the queuing time alone would hand it back rather than the untouched one.

                List<Building2DReference>? building2DReferences_Second = await ortoDatasPostgreSQLConverter.GetNextBuilding2DReferencesAsync(1, claimTimeoutMinutes: 0);
                Assert.NotNull(building2DReferences_Second);
                Assert.Single(building2DReferences_Second);
                Assert.Equal("reference_newer", building2DReferences_Second[0].Reference);

                //Acknowledging retires exactly what it was given and leaves the rest claimed

                long count_Acknowledged = await ortoDatasPostgreSQLConverter.AcknowledgeBuilding2DReferencesAsync([building2DReferences[0].Id]);
                Assert.Equal(1, count_Acknowledged);
                Assert.Equal(1, await DiGi.PostgreSQL.Query.CountAsync(npgsqlConnection, tableName));

                //Running the DDL again is a no-op on a table already in shape

                Assert.True(await npgsqlConnection.TableAsync_Building2DReference(tableName));
                Assert.Equal(1, await DiGi.PostgreSQL.Query.CountAsync(npgsqlConnection, tableName));

                //An entry that reached the attempt ceiling is retired by the claim that would have reached
                //it, never handed out and never left behind: the claim excludes it through its own ceiling,
                //and the retirement DELETE is what finally removes it (DiGi.GIS.PostgreSQL#145)

                await ExecuteAsync(npgsqlConnection, $"INSERT INTO {tableName} (county_id, reference, attempts) VALUES ({countyId}, 'reference_poison', 5);");
                Assert.Equal(2, await DiGi.PostgreSQL.Query.CountAsync(npgsqlConnection, tableName));

                List<Building2DReference>? building2DReferences_Retire = await ortoDatasPostgreSQLConverter.GetNextBuilding2DReferencesAsync(1, claimTimeoutMinutes: 0);
                Assert.NotNull(building2DReferences_Retire);
                Assert.Single(building2DReferences_Retire);
                Assert.Equal("reference_newer", building2DReferences_Retire[0].Reference);
                Assert.Equal(1, await DiGi.PostgreSQL.Query.CountAsync(npgsqlConnection, tableName));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {tableName};");
            }
        }

        /// <summary>
        /// Returns the names of the columns one table currently holds.
        /// <para>Read from <c>information_schema</c> rather than from what the DDL asked for, because the whole point of the assertions using it is what the server ended up with.</para>
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to read through.</param>
        /// <param name="tableName">The table whose columns are wanted.</param>
        /// <returns>The column names held for the table.</returns>
        private static async Task<HashSet<string>> ColumnNamesAsync(NpgsqlConnection npgsqlConnection, string tableName)
        {
            string commandText = @"
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tableName;";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            npgsqlCommand.Parameters.AddWithValue("tableName", tableName);

            HashSet<string> result = [];

            await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
            while (await npgsqlDataReader.ReadAsync())
            {
                result.Add(npgsqlDataReader.GetString(0));
            }

            return result;
        }

        /// <summary>
        /// Builds the queue DDL as it stood before claiming was added, so a table in the state every deployed one was in can be created to migrate.
        /// <para>A copy rather than a call, deliberately: the shipped helper no longer produces this shape, and reaching a table it did not produce is the entire point.</para>
        /// </summary>
        /// <param name="tableName">The name of the table to create.</param>
        /// <returns>The statements creating the queue table in its pre-claim shape.</returns>
        private static string CommandText_Building2DReference_Legacy(string tableName)
        {
            return $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    id BIGINT GENERATED ALWAYS AS IDENTITY,
                    county_id INT NOT NULL,
                    reference TEXT NOT NULL,
                    subdivision_id INT,
                    created_at timestamptz DEFAULT now(),
                    PRIMARY KEY (id, county_id)
                );

                CREATE UNIQUE INDEX IF NOT EXISTS idx_{tableName}_county_id_reference
                    ON {tableName} (county_id, reference);

                CREATE INDEX IF NOT EXISTS idx_{tableName}_created_at
                    ON {tableName} (created_at ASC);";
        }
    }
}
