using DiGi.EPW.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using NpgsqlTypes;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway main test database, that <see cref="EPWFilePostgreSQLConverter.GetEPWFileWithIdAsync(double, double, int, System.Threading.CancellationToken)"/> returns the id of the very row whose file it returns, and the same file <see cref="EPWFilePostgreSQLConverter.GetEPWFileAsync(double, double, int, System.Threading.CancellationToken)"/> returns.
        /// <para>The fact stores the Warsaw fixture as a scratch row (<c>XUNIT-EPW-*</c>) at a location far outside Poland, so the nearest row to that point is the scratch row whatever else the table holds, and removes it before and after. It creates the table when the test database has none. Medium test (a live database server). Reference: DiGi.GIS.PostgreSQL#100.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task EPWFilePostgreSQLConverter_GetEPWFileWithIdAsync()
        {
            const double x = -9999999;
            const double y = -9999999;

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            EPWFile? ePWFile_Fixture = EPW.Modify.Read(path);
            Assert.NotNull(ePWFile_Fixture);

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_EPWFile());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.EPWFile} WHERE name LIKE 'XUNIT-EPW-%';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                int id;
                await using (NpgsqlCommand npgsqlCommand = new($"INSERT INTO {Constants.TableName.EPWFile} (name, x, y, object) VALUES ('XUNIT-EPW-1', @x, @y, @object) RETURNING id;", npgsqlConnection))
                {
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("x", NpgsqlDbType.Double) { Value = x });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("y", NpgsqlDbType.Double) { Value = y });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = ePWFile_Fixture.ToJsonObject()!.ToJsonString() });
                    id = (int)(await npgsqlCommand.ExecuteScalarAsync())!;
                }

                EPWFilePostgreSQLConverter ePWFilePostgreSQLConverter = new(connectionData_Main);

                (int Id, EPWFile EPWFile)? ePWFileWithId = await ePWFilePostgreSQLConverter.GetEPWFileWithIdAsync(x, y);
                Assert.NotNull(ePWFileWithId);
                Assert.Equal(id, ePWFileWithId.Value.Id);

                string? json = ePWFileWithId.Value.EPWFile.ToJsonObject()?.ToJsonString();
                Assert.NotNull(json);
                Assert.Equal((await ePWFilePostgreSQLConverter.GetEPWFileAsync(x, y))?.ToJsonObject()?.ToJsonString(), json);
                Assert.Equal(ePWFile_Fixture.Location?.City, ePWFileWithId.Value.EPWFile.Location?.City);
                Assert.Equal(ePWFile_Fixture.DataRecords?.Count, ePWFileWithId.Value.EPWFile.DataRecords?.Count);

                Assert.Null(await EPWFilePostgreSQLConverter.GetEPWFileWithIdAsync(null, x, y));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
