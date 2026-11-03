using DiGi.Core.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch county part of the orthophoto task fact.
        /// </summary>
        private const int countyId_OrtoTasks = 990603;

        /// <summary>
        /// Runs the two background tasks behind the DiGi.GIS.PostgreSQL.UI#37 operator tasks end to end on the throwaway storage test database, through <see cref="CoreBackgroundTaskStart"/> as the operator interface starts them: <see cref="PostgreSQLOrtoSheetUpdateTask"/> imports the live 1998 index year, and <see cref="PostgreSQLOrtoDatasAcquisitionUpdateTask"/> backfills one scratch county part against scratch sheets.
        /// <para>Also: a backfill with no county part selected fails rather than walking anything, and its totals stay unset. The fact refuses to run when the test table already holds 1998 rows it did not write; it removes its 1998 rows, its scratch sheets and its partition before and in <c>finally</c>. Medium test (a live external service and a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PostgreSQLOrtoTasks_Run()
        {
            const string downloadUrlPrefix = "https://xunit.invalid/orto_tasks/";

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();
            Assert.True(await npgsqlConnection.TableAsync_OrtoSheet());
            Assert.True(await npgsqlConnection.TableAsync_OrtoDatas());

            OrtoSheetPostgreSQLConverter ortoSheetPostgreSQLConverter = new(connectionData_Storage);
            Skip.If(await ortoSheetPostgreSQLConverter.GetCountAsync(1998) != 0, "The test table already holds 1998 rows; the fact would remove them.");

            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            Assert.True(gISPostgreSQLConverterManager.Add(ortoSheetPostgreSQLConverter));
            Assert.True(gISPostgreSQLConverterManager.Add(new OrtoDatasPostgreSQLConverter(connectionData_Storage)));

            byte[] bytes = File.ReadAllBytes(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoImage_Warszawa_2023_Content.jpg")!);

            await CleanupAsync();
            try
            {
                // The import task, one real year.
                using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(2) };
                PostgreSQLOrtoSheetUpdateTask postgreSQLOrtoSheetUpdateTask = new(httpClient, gISPostgreSQLConverterManager)
                {
                    PostgreSQLOrtoSheetUpdateOptions = new PostgreSQLOrtoSheetUpdateOptions() { FirstYear = 1998, LastYear = 1998, PageSize = 200, RetryCount = 3, RetryDelaySeconds = 2 }
                };

                await CoreBackgroundTaskStart(postgreSQLOrtoSheetUpdateTask);
                Assert.Equal(BackgroundTaskStatus.Completed, postgreSQLOrtoSheetUpdateTask.BackgroundTaskStatus);
                Assert.NotNull(postgreSQLOrtoSheetUpdateTask.OrtoSheetUpdateResult);
                Assert.Equal([1998], postgreSQLOrtoSheetUpdateTask.OrtoSheetUpdateResult.Years);
                Assert.Equal(postgreSQLOrtoSheetUpdateTask.OrtoSheetUpdateResult.Hits, postgreSQLOrtoSheetUpdateTask.OrtoSheetUpdateResult.Rows);
                Assert.True(postgreSQLOrtoSheetUpdateTask.OrtoSheetUpdateResult.Written > 0);
                Assert.Equal(postgreSQLOrtoSheetUpdateTask.OrtoSheetUpdateResult.Canonical, await ortoSheetPostgreSQLConverter.GetCountAsync(1998));

                // The backfill task, one scratch county part.
                Polygon2D polygon2D = new([new Point2D(79500, 79500), new Point2D(81500, 79500), new Point2D(81500, 81500), new Point2D(79500, 81500)]);
                OrtoSheet ortoSheet = new("XUNIT-TASKS", "SkorowidzOrtofomapy2007", 2007, new DateTimeOffset(2007, 1, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Year, 0.5, OrtoColorMode.RGB, "Zdj. cyfrowe", "PL-1992", null, null, true, downloadUrlPrefix + "2007", null, null, polygon2D, OrtoType.Standard, null);
                Assert.Equal(1L, await ortoSheetPostgreSQLConverter.UpdateAsync([ortoSheet]));

                Assert.True(await npgsqlConnection.TableAsync_OrtoDatas_Partition(countyId_OrtoTasks));
                Point2D location = new(80500 - 128, 80500 + 128);
                byte[] bytes_Variant = [.. bytes];
                bytes_Variant[^3] = 7;
                GIS.Classes.OrtoDatas ortoDatas = new("XUNIT-ORTO-TASKS", [new OrtoData(new DateTime(2006, 1, 1), bytes_Variant, 2, location)]);
                await using (NpgsqlCommand npgsqlCommand = new($"INSERT INTO {Constants.TableName.OrtoDatas} (county_id, reference, object) VALUES (@county_id, @reference, @object);", npgsqlConnection))
                {
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId_OrtoTasks });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = ortoDatas.Reference! });
                    npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = ortoDatas.ToJsonObject()!.ToJsonString() });
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }

                // min_x ... max_y are what the backfill reads the part's box from.
                await ExecuteAsync(npgsqlConnection, $"UPDATE {Constants.TableName.OrtoDatas} SET min_x = 80372, min_y = 80372, max_x = 80628, max_y = 80628 WHERE county_id = {countyId_OrtoTasks};");

                PostgreSQLOrtoDatasAcquisitionUpdateTask postgreSQLOrtoDatasAcquisitionUpdateTask = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOrtoDatasAcquisitionUpdateOptions = new PostgreSQLOrtoDatasAcquisitionUpdateOptions() { CountyIds = [countyId_OrtoTasks], LastYear = 2012 }
                };

                await CoreBackgroundTaskStart(postgreSQLOrtoDatasAcquisitionUpdateTask);
                Assert.Equal(BackgroundTaskStatus.Completed, postgreSQLOrtoDatasAcquisitionUpdateTask.BackgroundTaskStatus);
                OrtoDatasAcquisitionUpdateResult? ortoDatasAcquisitionUpdateResult = postgreSQLOrtoDatasAcquisitionUpdateTask.OrtoDatasAcquisitionUpdateResult;
                Assert.NotNull(ortoDatasAcquisitionUpdateResult);
                Assert.Equal(1, ortoDatasAcquisitionUpdateResult.Rows);
                Assert.Equal(1, ortoDatasAcquisitionUpdateResult.Changed);
                Assert.Equal(1, ortoDatasAcquisitionUpdateResult.Dated);

                object? json = await ScalarAsync(npgsqlConnection, $"SELECT object::text FROM {Constants.TableName.OrtoDatas} WHERE county_id = {countyId_OrtoTasks};");
                OrtoData ortoData_Stored = new GIS.Classes.OrtoDatas(JsonNode.Parse((string)json!) as JsonObject).Single();
                Assert.Equal(ortoSheet.Date, ortoData_Stored.AcquisitionDate);
                Assert.Equal("XUNIT-TASKS", ortoData_Stored.SheetId);

                // No county part selected: the task fails instead of walking anything.
                PostgreSQLOrtoDatasAcquisitionUpdateTask postgreSQLOrtoDatasAcquisitionUpdateTask_Empty = new(gISPostgreSQLConverterManager);
                await CoreBackgroundTaskStart(postgreSQLOrtoDatasAcquisitionUpdateTask_Empty);
                Assert.Equal(BackgroundTaskStatus.Failed, postgreSQLOrtoDatasAcquisitionUpdateTask_Empty.BackgroundTaskStatus);
                Assert.Null(postgreSQLOrtoDatasAcquisitionUpdateTask_Empty.OrtoDatasAcquisitionUpdateResult);

                Assert.Throws<ArgumentNullException>(() => new PostgreSQLOrtoDatasAcquisitionUpdateTask(null));

                PostgreSQLOrtoDatasAcquisitionUpdateOptions postgreSQLOrtoDatasAcquisitionUpdateOptions = new(postgreSQLOrtoDatasAcquisitionUpdateTask.PostgreSQLOrtoDatasAcquisitionUpdateOptions);
                Assert.Equal([countyId_OrtoTasks], postgreSQLOrtoDatasAcquisitionUpdateOptions.CountyIds!);
                Assert.Equal(2012, postgreSQLOrtoDatasAcquisitionUpdateOptions.LastYear);
                Assert.Equal(1998, new PostgreSQLOrtoSheetUpdateOptions(postgreSQLOrtoSheetUpdateTask.PostgreSQLOrtoSheetUpdateOptions).FirstYear);
            }
            finally
            {
                await CleanupAsync();
            }

            async Task CleanupAsync()
            {
                await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.OrtoSheet} WHERE year = 1998 OR download_url LIKE '{downloadUrlPrefix}%';");
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDatas}_{countyId_OrtoTasks};");
            }
        }

        /// <summary>
        /// Starts a background task as the operator interface does and waits, up to five minutes, until it stops.
        /// </summary>
        /// <param name="backgroundTask">The task to run.</param>
        /// <returns>A task that completes when the background task has stopped.</returns>
        private static async Task CoreBackgroundTaskStart(Core.Classes.BackgroundTask backgroundTask)
        {
            backgroundTask.Start();

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!backgroundTask.IsCompleted && stopwatch.Elapsed < TimeSpan.FromMinutes(5))
            {
                await Task.Delay(100);
            }

            Assert.True(backgroundTask.IsCompleted);
        }
    }
}
