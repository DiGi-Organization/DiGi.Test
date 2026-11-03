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
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch county part of the orthophoto backfill fact: a partition of its own on the throwaway storage test database.
        /// </summary>
        private const int countyId_OrtoSheet_A = 990601;

        /// <summary>
        /// The second scratch county part of the orthophoto backfill fact, holding no series, so a walk of it is empty.
        /// </summary>
        private const int countyId_OrtoSheet_B = 990602;

        /// <summary>
        /// Verifies, on the throwaway storage test database, the orthophoto sheet table and the acquisition-date backfill of <c>orto_datas</c> (DiGi.GIS.PostgreSQL#122).
        /// <para>Sheets: the upsert, the box lookup (inside, touching, outside), and a file listed in two index years keeping its earlier listing whichever order the years arrive in. Series: a stored, reduced series 2001 / 2006 / 2011 fetched to 2012 at the centre of the scratch sheets, re-dated to 2001-05-13, the sheet filed under 2007 at year precision, and 2011-04-21 (RGB) - the dates of the DiGi.GIS <c>OrtoDatas_Redate</c> fact; a second series outside every sheet is written back undated; a re-run writes nothing. The physical-order walk is driven page by page (page size 1) and ends with an empty position, and an empty part walks to an empty position at once.</para>
        /// <para>Everything sits around (50500, 50500), outside Poland's extent, so no real sheet of the table can reach the assertions. The fact removes its sheets and drops its partitions before and in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OrtoSheetPostgreSQLConverter_Backfill()
        {
            const string downloadUrlPrefix = "https://xunit.invalid/orto_sheet/";

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_OrtoSheet());
            Assert.True(await npgsqlConnection.TableAsync_OrtoDatas());

            byte[] bytes = File.ReadAllBytes(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoImage_Warszawa_2023_Content.jpg")!);

            await CleanupAsync();
            try
            {
                OrtoSheetPostgreSQLConverter ortoSheetPostgreSQLConverter = new(connectionData_Storage);
                OrtoDatasPostgreSQLConverter ortoDatasPostgreSQLConverter = new(connectionData_Storage);

                OrtoSheet ortoSheet_2001 = Sheet("XUNIT-A", 2001, new DateTimeOffset(2001, 5, 13, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.75, OrtoColorMode.RGB, "2001-RGB");
                OrtoSheet ortoSheet_2007 = Sheet("XUNIT-A-1", 2007, new DateTimeOffset(2007, 1, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Year, 0.5, OrtoColorMode.RGB, "2007-RGB");
                OrtoSheet ortoSheet_2011_CIR = Sheet("XUNIT-A-1", 2011, new DateTimeOffset(2011, 4, 21, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.25, OrtoColorMode.CIR, "2011-CIR");
                OrtoSheet ortoSheet_2011_RGB = Sheet("XUNIT-A-1", 2011, new DateTimeOffset(2011, 4, 21, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.25, OrtoColorMode.RGB, "2011-RGB");
                OrtoSheet ortoSheet_2013 = Sheet("XUNIT-A-1", 2013, new DateTimeOffset(2013, 6, 14, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.1, OrtoColorMode.RGB, "2013-RGB");

                Assert.Equal(5L, await ortoSheetPostgreSQLConverter.UpdateAsync([ortoSheet_2001, ortoSheet_2007, null, ortoSheet_2011_CIR, ortoSheet_2011_RGB, ortoSheet_2013], batchSize: 2));

                // The 2007 file listed again in 2009 keeps its 2007 row.
                OrtoSheet ortoSheet_2009_Again = Sheet("XUNIT-A-1", 2009, new DateTimeOffset(2009, 6, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.5, OrtoColorMode.RGB, "2007-RGB");
                Assert.Equal(0L, await ortoSheetPostgreSQLConverter.UpdateAsync([ortoSheet_2009_Again]));
                Assert.Equal(2007L, await ScalarAsync(npgsqlConnection, $"SELECT year::bigint FROM {Constants.TableName.OrtoSheet} WHERE download_url = '{downloadUrlPrefix}2007-RGB';"));

                // Years arriving latest first: a file read in 2009 and then in 2005 keeps the 2005 listing; within one call the earliest wins too.
                OrtoSheet ortoSheet_Far_2009 = Sheet("XUNIT-FAR", 2009, new DateTimeOffset(2009, 6, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.5, OrtoColorMode.RGB, "far", 30000);
                OrtoSheet ortoSheet_Far_2005 = Sheet("XUNIT-FAR", 2005, new DateTimeOffset(2005, 6, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.5, OrtoColorMode.RGB, "far", 30000);
                Assert.Equal(1L, await ortoSheetPostgreSQLConverter.UpdateAsync([ortoSheet_Far_2009]));
                Assert.Equal(1L, await ortoSheetPostgreSQLConverter.UpdateAsync([ortoSheet_Far_2009, ortoSheet_Far_2005]));
                Assert.Equal(2005L, await ScalarAsync(npgsqlConnection, $"SELECT year::bigint FROM {Constants.TableName.OrtoSheet} WHERE download_url = '{downloadUrlPrefix}far';"));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoSheet} WHERE download_url = '{downloadUrlPrefix}far';"));
                Assert.True(await ortoSheetPostgreSQLConverter.GetCountAsync(2005) >= 1);

                // Box lookup: inside, touching the 2 km footprint edge, outside.
                List<OrtoSheet>? ortoSheets_Inside = await ortoSheetPostgreSQLConverter.GetOrtoSheetsByBoundingBoxAsync(new BoundingBox2D(new Point2D(50400, 50400), new Point2D(50600, 50600)));
                Assert.NotNull(ortoSheets_Inside);
                Assert.Equal(5, ortoSheets_Inside.Count(x => x.DownloadUrl!.StartsWith(downloadUrlPrefix, StringComparison.Ordinal)));
                Assert.All(ortoSheets_Inside, x => Assert.NotNull(x.Footprint));

                List<OrtoSheet>? ortoSheets_Edge = await ortoSheetPostgreSQLConverter.GetOrtoSheetsByBoundingBoxAsync(new BoundingBox2D(new Point2D(51500, 50000), new Point2D(51600, 50100)));
                Assert.Equal(5, ortoSheets_Edge!.Count(x => x.DownloadUrl!.StartsWith(downloadUrlPrefix, StringComparison.Ordinal)));

                List<OrtoSheet>? ortoSheets_Outside = await ortoSheetPostgreSQLConverter.GetOrtoSheetsByBoundingBoxAsync(new BoundingBox2D(new Point2D(60000, 60000), new Point2D(60100, 60100)));
                Assert.DoesNotContain(ortoSheets_Outside!, x => x.DownloadUrl!.StartsWith(downloadUrlPrefix, StringComparison.Ordinal));

                // Two stored series of the scratch part: one at the centre of the sheets, one 10 km away from every sheet.
                GIS.Classes.OrtoDatas ortoDatas_Covered = Series("XUNIT-ORTO-COVERED", new Point2D(50500 - 128, 50500 + 128));
                GIS.Classes.OrtoDatas ortoDatas_Uncovered = Series("XUNIT-ORTO-UNCOVERED", new Point2D(60500 - 128, 60500 + 128));

                Assert.True(await npgsqlConnection.TableAsync_OrtoDatas_Partition(countyId_OrtoSheet_A));
                Assert.True(await npgsqlConnection.TableAsync_OrtoDatas_Partition(countyId_OrtoSheet_B));
                long id_Covered = await InsertAsync(ortoDatas_Covered);
                long id_Uncovered = await InsertAsync(ortoDatas_Uncovered);

                // The walk, page by page.
                (Dictionary<long, GIS.Classes.OrtoDatas>? ortoDatas_Page_1, string? position_1) = await OrtoDatasPostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, countyId_OrtoSheet_A, null, 1);
                Assert.Single(ortoDatas_Page_1!);
                Assert.False(string.IsNullOrEmpty(position_1));
                (Dictionary<long, GIS.Classes.OrtoDatas>? ortoDatas_Page_2, string? position_2) = await OrtoDatasPostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, countyId_OrtoSheet_A, position_1, 1);
                Assert.Single(ortoDatas_Page_2!);
                Assert.Equal([id_Covered, id_Uncovered], ortoDatas_Page_1!.Keys.Concat(ortoDatas_Page_2!.Keys).OrderBy(x => x));
                (Dictionary<long, GIS.Classes.OrtoDatas>? ortoDatas_Page_3, string? position_3) = await OrtoDatasPostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, countyId_OrtoSheet_A, position_2, 1);
                Assert.True(ortoDatas_Page_3!.Count == 0 || position_3 == string.Empty);
                Assert.Equal((null, null), await OrtoDatasPostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, countyId_OrtoSheet_A, "not a tid", 1));

                (Dictionary<long, GIS.Classes.OrtoDatas>? ortoDatas_Empty, string? position_Empty) = await OrtoDatasPostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, countyId_OrtoSheet_B, null, 10);
                Assert.Empty(ortoDatas_Empty!);
                Assert.Equal(string.Empty, position_Empty);

                // The backfill.
                OrtoDatasAcquisitionUpdateResult? ortoDatasAcquisitionUpdateResult = await ortoDatasPostgreSQLConverter.UpdateOrtoDatasAcquisitionAsync(ortoSheetPostgreSQLConverter, [countyId_OrtoSheet_A, countyId_OrtoSheet_B], 2012, pageSize: 1);
                Assert.NotNull(ortoDatasAcquisitionUpdateResult);
                Assert.Equal(2, ortoDatasAcquisitionUpdateResult.Rows);
                Assert.Equal(2, ortoDatasAcquisitionUpdateResult.Changed);
                Assert.Equal(6, ortoDatasAcquisitionUpdateResult.Images);
                Assert.Equal(3, ortoDatasAcquisitionUpdateResult.Dated);
                Assert.True(ortoDatasAcquisitionUpdateResult.Sheets >= 5);
                Core.xUnit.Query.SerializationCheck(ortoDatasAcquisitionUpdateResult);

                List<OrtoData> ortoDatas_Covered_Stored = [.. await ReadAsync(id_Covered)];
                Assert.Equal(3, ortoDatas_Covered_Stored.Count);
                Assert.Equal(ortoSheet_2001.Date, ortoDatas_Covered_Stored[0].AcquisitionDate);
                Assert.Equal(ortoSheet_2007.Date, ortoDatas_Covered_Stored[1].AcquisitionDate);
                Assert.Equal(OrtoDatePrecision.Year, ortoDatas_Covered_Stored[1].AcquisitionDatePrecision);
                Assert.Equal("XUNIT-A-1", ortoDatas_Covered_Stored[1].SheetId);
                Assert.Equal(ortoSheet_2011_RGB.Date, ortoDatas_Covered_Stored[2].AcquisitionDate);
                Assert.All(ortoDatas_Covered_Stored, x => Assert.NotNull(x.ContentHash));

                Assert.All(await ReadAsync(id_Uncovered), x => Assert.Equal(OrtoDatePrecision.Undefined, x.AcquisitionDatePrecision));

                // Idempotent: a re-run reads both rows and writes none.
                OrtoDatasAcquisitionUpdateResult? ortoDatasAcquisitionUpdateResult_Again = await ortoDatasPostgreSQLConverter.UpdateOrtoDatasAcquisitionAsync(ortoSheetPostgreSQLConverter, [countyId_OrtoSheet_A], 2012);
                Assert.Equal(2, ortoDatasAcquisitionUpdateResult_Again!.Rows);
                Assert.Equal(0, ortoDatasAcquisitionUpdateResult_Again.Changed);

                Assert.Null(await ortoDatasPostgreSQLConverter.UpdateOrtoDatasAcquisitionAsync(null, [countyId_OrtoSheet_A], 2012));
                Assert.Null(await ortoDatasPostgreSQLConverter.UpdateOrtoDatasAcquisitionAsync(ortoSheetPostgreSQLConverter, [], 2012));

                OrtoSheetUpdateResult ortoSheetUpdateResult = new([2001, 2002], [2002], [], 10, 10, 9, 9);
                Core.xUnit.Query.SerializationCheck(ortoSheetUpdateResult);
                Assert.Equal([2002], ortoSheetUpdateResult.MissingYears);
                Assert.Equal(9, new OrtoSheetUpdateResult(ortoSheetUpdateResult).Written);
            }
            finally
            {
                await CleanupAsync();
            }

            async Task CleanupAsync()
            {
                await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.OrtoSheet} WHERE download_url LIKE '{downloadUrlPrefix}%';");
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDatas}_{countyId_OrtoSheet_A};");
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDatas}_{countyId_OrtoSheet_B};");
            }

            async Task<long> InsertAsync(GIS.Classes.OrtoDatas ortoDatas)
            {
                BoundingBox2D boundingBox2D = ortoDatas.First().GetBoundingBox(GeometryContext.Global)!;
                await using NpgsqlCommand npgsqlCommand = new($"INSERT INTO {Constants.TableName.OrtoDatas} (county_id, reference, min_x, min_y, max_x, max_y, object) VALUES (@county_id, @reference, @min_x, @min_y, @max_x, @max_y, @object) RETURNING id;", npgsqlConnection);
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId_OrtoSheet_A });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = ortoDatas.Reference! });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = boundingBox2D.Min.X });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = boundingBox2D.Min.Y });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = boundingBox2D.Max.X });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = boundingBox2D.Max.Y });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = ortoDatas.ToJsonObject()!.ToJsonString() });
                return System.Convert.ToInt64(await npgsqlCommand.ExecuteScalarAsync());
            }

            async Task<GIS.Classes.OrtoDatas> ReadAsync(long id)
            {
                object? json = await ScalarAsync(npgsqlConnection, $"SELECT object::text FROM {Constants.TableName.OrtoDatas} WHERE county_id = {countyId_OrtoSheet_A} AND id = {id};");
                return new GIS.Classes.OrtoDatas(JsonNode.Parse((string)json!) as JsonObject);
            }

            GIS.Classes.OrtoDatas Series(string reference, Point2D location)
            {
                return new GIS.Classes.OrtoDatas(reference, [new OrtoData(new DateTime(2001, 1, 1), Variant(1), 2, location), new OrtoData(new DateTime(2006, 1, 1), Variant(2), 2, location), new OrtoData(new DateTime(2011, 1, 1), Variant(3), 2, location)]);
            }

            byte[] Variant(byte value)
            {
                byte[] result = [.. bytes];
                result[^3] = value;
                return result;
            }

            static OrtoSheet Sheet(string sheetId, int year, DateTimeOffset date, OrtoDatePrecision ortoDatePrecision, double pixelSize, OrtoColorMode ortoColorMode, string fileName, double offset = 0)
            {
                Polygon2D polygon2D = new([new Point2D(49500 + offset, 49500), new Point2D(51500 + offset, 49500), new Point2D(51500 + offset, 51500), new Point2D(49500 + offset, 51500)]);
                return new OrtoSheet(sheetId, "SkorowidzOrtofomapy" + year, year, date, ortoDatePrecision, pixelSize, ortoColorMode, "Zdj. cyfrowe", "PL-1992", null, null, true, downloadUrlPrefix + fileName, null, null, polygon2D, OrtoType.Standard, null);
            }
        }
    }
}
