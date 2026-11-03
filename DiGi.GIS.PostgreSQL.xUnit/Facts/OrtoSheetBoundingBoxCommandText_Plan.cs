using DiGi.PostgreSQL.Classes;
using Npgsql;
using NpgsqlTypes;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway storage test database, that the sheet box lookup (<see cref="Query.OrtoSheetBoundingBoxCommandText"/>) is served by an index on a populated table, not by a sequential scan (DiGi.GIS.PostgreSQL#122).
        /// <para>The fact seeds 40 000 scratch sheets of 2 km on a 400 × 200 km grid, analyses the table, and runs <c>EXPLAIN (ANALYZE, BUFFERS)</c> for a 10 km box. An intersection test alone (<c>min_x &lt;= @max_x</c>) would read every sheet west of the box; the bounded ranges keep the read to a strip. The plan is written to <c>user files/reports/OrtoSheetBoundingBoxCommandText_Plan.txt</c>, labelled with the database it describes. Scratch rows are removed before and in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OrtoSheetBoundingBoxCommandText_Plan()
        {
            const string downloadUrlPrefix = "https://xunit.invalid/orto_sheet_plan/";

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_OrtoSheet());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.OrtoSheet} WHERE download_url LIKE '{downloadUrlPrefix}%';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                await ExecuteAsync(npgsqlConnection, $@"
                    INSERT INTO {Constants.TableName.OrtoSheet} (sheet_id, year, date_precision, color_mode, orto_type, min_x, min_y, max_x, max_y, download_url, object)
                    SELECT 'XUNIT-PLAN', 2020, 0, 0, 0, 100000 + i * 2000, 100000 + j * 2000, 102000 + i * 2000, 102000 + j * 2000, '{downloadUrlPrefix}' || i || '_' || j, '{{}}'::jsonb
                    FROM generate_series(0, 199) AS i, generate_series(0, 199) AS j;");
                await ExecuteAsync(npgsqlConnection, $"ANALYZE {Constants.TableName.OrtoSheet};");

                List<string> lines = [];
                await using (NpgsqlCommand npgsqlCommand = new("EXPLAIN (ANALYZE, BUFFERS) " + Query.OrtoSheetBoundingBoxCommandText(), npgsqlConnection))
                {
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = 300000.0 });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = 300000.0 });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = 310000.0 });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = 310000.0 });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("extent", NpgsqlDbType.Double) { Value = 10000.0 });

                    await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
                    while (await npgsqlDataReader.ReadAsync())
                    {
                        lines.Add(npgsqlDataReader.GetString(0));
                    }
                }

                string plan = string.Join("\n", lines);

                string directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!;
                StringBuilder stringBuilder = new();
                stringBuilder.AppendLine("Database: the throwaway STORAGE TEST database (GIS_PostgreSQL_Storage_Test.conf), seeded with 40 000 scratch sheets. Not production.");
                stringBuilder.AppendLine(plan);
                File.WriteAllText(Path.Combine(directory, "OrtoSheetBoundingBoxCommandText_Plan.txt"), stringBuilder.ToString());

                Assert.Contains("Index", plan);
                Assert.DoesNotContain("Seq Scan", plan);
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
