using DiGi.GIS.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Imports the smallest real index year of the live GUGiK orthophoto index (1998, 353 rows on 2026-11-03) into the throwaway storage test database with <see cref="Modify.UpdateOrtoSheetsAsync"/>, and a year the service does not publish (1900).
        /// <para>Proves the import end to end: the rows read equal the service's count, every row is stored with its footprint box, a re-run writes the same rows again rather than adding any, and the unpublished year is reported as missing rather than failing the import. The fact refuses to run when the test table already holds 1998 rows it did not write, and removes the 1998 rows in <c>finally</c>.</para>
        /// <para>Medium test (a live external service and a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task UpdateOrtoSheetsAsync_Live()
        {
            const int year = 1998;

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();
            Assert.True(await npgsqlConnection.TableAsync_OrtoSheet());

            OrtoSheetPostgreSQLConverter ortoSheetPostgreSQLConverter = new(connectionData_Storage);
            Skip.If(await ortoSheetPostgreSQLConverter.GetCountAsync(year) != 0, $"The test table already holds {year} rows; the fact would remove them.");

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(2) };
            try
            {
                OrtoSheetUpdateResult? ortoSheetUpdateResult = await ortoSheetPostgreSQLConverter.UpdateOrtoSheetsAsync(httpClient, [1900, year], 200, 3, TimeSpan.FromSeconds(2));
                Assert.NotNull(ortoSheetUpdateResult);
                Assert.Equal([1900], ortoSheetUpdateResult.MissingYears);
                Assert.Empty(ortoSheetUpdateResult.MismatchedYears!);
                Assert.True(ortoSheetUpdateResult.Hits > 0);
                Assert.Equal(ortoSheetUpdateResult.Hits, ortoSheetUpdateResult.Rows);
                Assert.True(ortoSheetUpdateResult.Canonical <= ortoSheetUpdateResult.Rows);
                Assert.Equal(ortoSheetUpdateResult.Canonical, ortoSheetUpdateResult.Written);

                long? count = await ortoSheetPostgreSQLConverter.GetCountAsync(year);
                Assert.Equal(ortoSheetUpdateResult.Canonical, count);

                // Re-running replaces every row and adds none.
                OrtoSheetUpdateResult? ortoSheetUpdateResult_Again = await ortoSheetPostgreSQLConverter.UpdateOrtoSheetsAsync(httpClient, [year], 200, 3, TimeSpan.FromSeconds(2));
                Assert.Equal(ortoSheetUpdateResult.Written, ortoSheetUpdateResult_Again!.Written);
                Assert.Equal(count, await ortoSheetPostgreSQLConverter.GetCountAsync(year));

                // A stored row comes back with its footprint, inside Poland's EPSG:2180 extent with easting in X.
                List<OrtoSheet>? ortoSheets = await ortoSheetPostgreSQLConverter.GetOrtoSheetsByBoundingBoxAsync(new Geometry.Planar.Classes.BoundingBox2D(new Geometry.Planar.Classes.Point2D(140000, 120000), new Geometry.Planar.Classes.Point2D(880000, 790000)));
                Assert.Contains(ortoSheets!, x => x.Year == year && x.Footprint is not null);

                Assert.Null(await ortoSheetPostgreSQLConverter.UpdateOrtoSheetsAsync(null, [year], 200, 3, TimeSpan.Zero));
                Assert.Null(await ortoSheetPostgreSQLConverter.UpdateOrtoSheetsAsync(httpClient, [], 200, 3, TimeSpan.Zero));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.OrtoSheet} WHERE year = {year};");
            }
        }
    }
}
