using DiGi.Core.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch county parts of the acquisition-backfill parallel-parity fact (DiGi.GIS.PostgreSQL#177).
        /// </summary>
        private const int countyId_AcquisitionParity_1 = 990611;
        private const int countyId_AcquisitionParity_2 = 990612;
        private const int countyId_AcquisitionParity_3 = 990613;

        /// <summary>
        /// Walks the same three scratch county parts twice through <see cref="PostgreSQLOrtoDatasAcquisitionUpdateTask"/> - once at <c>MaxDegreeOfParallelism = 1</c>, once at <c>4</c> (three parts, so the degree really runs three workers) - and asserts the two runs leave byte-identical acquisition rows and identical counters (DiGi.GIS.PostgreSQL#177).
        /// <para>Each part carries two one-image series: one inside the scratch sheet's footprint, dated, and one outside it, written as an Undefined row - so rows, images and dated images are all non-trivial and the undated half is asserted too. The acquisition partitions are dropped between the runs while the <c>orto_datas</c> rows stay, so the second run also exercises its own parallel path of creating the side table and every partition before the workers start. The fact removes its scratch sheets, series and partitions before and in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PostgreSQLOrtoDatasAcquisitionUpdateTask_ParallelParity()
        {
            const string downloadUrlPrefix = "https://xunit.invalid/acq_parity/";

            int[] countyIds = [countyId_AcquisitionParity_1, countyId_AcquisitionParity_2, countyId_AcquisitionParity_3];

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();
            Assert.True(await npgsqlConnection.TableAsync_OrtoSheet());
            Assert.True(await npgsqlConnection.TableAsync_OrtoDatas());

            OrtoSheetPostgreSQLConverter ortoSheetPostgreSQLConverter = new(connectionData_Storage);
            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            Assert.True(gISPostgreSQLConverterManager.Add(ortoSheetPostgreSQLConverter));
            Assert.True(gISPostgreSQLConverterManager.Add(new OrtoDatasPostgreSQLConverter(connectionData_Storage)));

            // The options contract: zero means half the processors, and a copy keeps the degree (Coding - Serialization.md).
            Assert.Equal(0, new PostgreSQLOrtoDatasAcquisitionUpdateOptions().MaxDegreeOfParallelism);
            PostgreSQLOrtoDatasAcquisitionUpdateOptions postgreSQLOrtoDatasAcquisitionUpdateOptions_Copy = new(new PostgreSQLOrtoDatasAcquisitionUpdateOptions() { MaxDegreeOfParallelism = 4, LastYear = 2012 });
            Assert.Equal(4, postgreSQLOrtoDatasAcquisitionUpdateOptions_Copy.MaxDegreeOfParallelism);

            byte[] bytes = File.ReadAllBytes(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoImage_Warszawa_2023_Content.jpg")!);

            await CleanupAsync();
            try
            {
                // One scratch sheet; its footprint covers every part's inside location, and no cell of the outside
                // location, so the inside series date and the outside series are written as Undefined rows.
                Polygon2D polygon2D = new([new Point2D(79500, 79500), new Point2D(81500, 79500), new Point2D(81500, 81500), new Point2D(79500, 81500)]);
                OrtoSheet ortoSheet = new("XUNIT-PARITY", "SkorowidzOrtofomapy2007", 2007, new DateTimeOffset(2007, 1, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Year, 0.5, OrtoColorMode.RGB, "Zdj. cyfrowe", "PL-1992", null, null, true, downloadUrlPrefix + "2007", null, null, polygon2D, OrtoType.Standard, null);
                Assert.Equal(1L, await ortoSheetPostgreSQLConverter.UpdateAsync([ortoSheet]));

                foreach (int countyId in countyIds)
                {
                    Assert.True(await npgsqlConnection.TableAsync_OrtoDatas_Partition(countyId));

                    // Two series per part: IN is covered by the sheet, OUT falls outside every footprint cell.
                    foreach (string suffix in new string[] { "IN", "OUT" })
                    {
                        Point2D location = suffix == "IN" ? new Point2D(80500 - 128, 80500 + 128) : new Point2D(100500, 100500);
                        GIS.Classes.OrtoDatas ortoDatas = new($"XUNIT-PARITY-{countyId}-{suffix}", [new OrtoData(new DateTime(2006, 1, 1), bytes, 2, location)]);
                        await using (NpgsqlCommand npgsqlCommand = new($"INSERT INTO {Constants.TableName.OrtoDatas} (county_id, reference, object) VALUES (@county_id, @reference, @object);", npgsqlConnection))
                        {
                            npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId });
                            npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = ortoDatas.Reference! });
                            npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = ortoDatas.ToJsonObject()!.ToJsonString() });
                            await npgsqlCommand.ExecuteNonQueryAsync();
                        }
                    }

                    // min_x ... max_y are what the backfill reads the part's box from: both locations, so the
                    // sheet is found for the part and only the outside cell misses it.
                    await ExecuteAsync(npgsqlConnection, $"UPDATE {Constants.TableName.OrtoDatas} SET min_x = 80372, min_y = 80372, max_x = 100628, max_y = 100628 WHERE county_id = {countyId};");
                }

                // Run one: one part after another, exactly as before the change.
                PostgreSQLOrtoDatasAcquisitionUpdateTask postgreSQLOrtoDatasAcquisitionUpdateTask_Sequential = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOrtoDatasAcquisitionUpdateOptions = new PostgreSQLOrtoDatasAcquisitionUpdateOptions() { CountyIds = [.. countyIds], LastYear = 2012, MaxDegreeOfParallelism = 1 }
                };

                await CoreBackgroundTaskStart(postgreSQLOrtoDatasAcquisitionUpdateTask_Sequential);
                Assert.Equal(BackgroundTaskStatus.Completed, postgreSQLOrtoDatasAcquisitionUpdateTask_Sequential.BackgroundTaskStatus);
                OrtoDatasAcquisitionUpdateResult? result_Sequential = postgreSQLOrtoDatasAcquisitionUpdateTask_Sequential.OrtoDatasAcquisitionUpdateResult;
                Assert.NotNull(result_Sequential);

                List<string> rows_Sequential = await SnapshotAsync();

                // A guard that cannot pass vacuously: two dated + one undefined row per part.
                Assert.Equal(6, result_Sequential.Rows);
                Assert.Equal(6, result_Sequential.Changed);
                Assert.Equal(6, result_Sequential.Images);
                Assert.Equal(3, result_Sequential.Dated);
                Assert.Equal(3, result_Sequential.Sheets);

                // Run two: the same parts through three workers; the acquisition partitions are gone, so the
                // parallel path creates the side table and every partition before the workers start.
                foreach (int countyId in countyIds)
                {
                    await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDataAcquisition}_{countyId};");
                }

                PostgreSQLOrtoDatasAcquisitionUpdateTask postgreSQLOrtoDatasAcquisitionUpdateTask_Parallel = new(gISPostgreSQLConverterManager)
                {
                    PostgreSQLOrtoDatasAcquisitionUpdateOptions = new PostgreSQLOrtoDatasAcquisitionUpdateOptions() { CountyIds = [.. countyIds], LastYear = 2012, MaxDegreeOfParallelism = 4 }
                };

                await CoreBackgroundTaskStart(postgreSQLOrtoDatasAcquisitionUpdateTask_Parallel);
                Assert.Equal(BackgroundTaskStatus.Completed, postgreSQLOrtoDatasAcquisitionUpdateTask_Parallel.BackgroundTaskStatus);
                OrtoDatasAcquisitionUpdateResult? result_Parallel = postgreSQLOrtoDatasAcquisitionUpdateTask_Parallel.OrtoDatasAcquisitionUpdateResult;
                Assert.NotNull(result_Parallel);

                List<string> rows_Parallel = await SnapshotAsync();

                // The differential assertion: identical counters, then row for row the same side tables.
                Assert.Equal(result_Sequential.Sheets, result_Parallel.Sheets);
                Assert.Equal(result_Sequential.Rows, result_Parallel.Rows);
                Assert.Equal(result_Sequential.Changed, result_Parallel.Changed);
                Assert.Equal(result_Sequential.Images, result_Parallel.Images);
                Assert.Equal(result_Sequential.Dated, result_Parallel.Dated);
                Assert.Equal(6, rows_Parallel.Count);
                Assert.Equal(rows_Sequential, rows_Parallel);
            }
            finally
            {
                await CleanupAsync();
            }

            async Task<List<string>> SnapshotAsync()
            {
                List<string> rows = [];
                foreach (int countyId in countyIds)
                {
                    await using NpgsqlCommand npgsqlCommand = new($"SELECT reference, year, acquisition_date, date_precision, sheet_id, content_hash FROM {Constants.TableName.OrtoDataAcquisition}_{countyId} ORDER BY reference, year;", npgsqlConnection);
                    await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
                    while (await npgsqlDataReader.ReadAsync())
                    {
                        rows.Add(string.Create(
                            System.Globalization.CultureInfo.InvariantCulture,
                            $"{countyId}|{npgsqlDataReader.GetString(0)}|{npgsqlDataReader.GetInt32(1)}|{(npgsqlDataReader.IsDBNull(2) ? "-" : npgsqlDataReader.GetDateTime(2).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture))}|{npgsqlDataReader.GetInt16(3)}|{(npgsqlDataReader.IsDBNull(4) ? "-" : npgsqlDataReader.GetString(4))}|{(npgsqlDataReader.IsDBNull(5) ? "-" : npgsqlDataReader.GetString(5))}"));
                    }
                }

                return rows;
            }

            async Task CleanupAsync()
            {
                foreach (int countyId in countyIds)
                {
                    await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.OrtoDatas} WHERE reference LIKE 'XUNIT-PARITY-%';");
                    await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDatas}_{countyId};");
                    await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDataAcquisition}_{countyId};");
                }

                await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.OrtoSheet} WHERE download_url LIKE '{downloadUrlPrefix}%';");
            }
        }
    }
}
