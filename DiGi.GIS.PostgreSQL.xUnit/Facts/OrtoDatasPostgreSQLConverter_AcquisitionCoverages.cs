using DiGi.GIS.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch county part holding backfilled, partly backfilled and never backfilled series in the acquisition coverage fact.
        /// </summary>
        private const int countyId_AcquisitionCoverage = 990604;

        /// <summary>
        /// The scratch county part the backfill never reached in the acquisition coverage fact.
        /// </summary>
        private const int countyId_AcquisitionCoverage_NotBackfilled = 990605;

        /// <summary>
        /// Measures <see cref="OrtoDatasPostgreSQLConverter.GetAcquisitionCoveragesByCountyIdsAsync(NpgsqlConnection?, IEnumerable{int}?, int, System.Threading.CancellationToken)"/> on the throwaway storage test database against scratch series whose answer is known.
        /// <para>Part 990604 holds three series: one with a day-dated and an undated image, one with a single undated image, and one with no acquisition row; an acquisition row of a reference the part does not store is not counted. Part 990605 holds one series and no acquisition row. A named part holding nothing is absent from the result. The fact removes its partitions before and in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OrtoDatasPostgreSQLConverter_AcquisitionCoverages()
        {
            const int countyId_Empty = 990606;

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();
            Assert.True(await npgsqlConnection.TableAsync_OrtoDatas());
            Assert.True(await npgsqlConnection.TableAsync_OrtoDataAcquisition());

            await CleanupAsync();
            try
            {
                Assert.True(await npgsqlConnection.TableAsync_OrtoDatas_Partition(countyId_AcquisitionCoverage));
                Assert.True(await npgsqlConnection.TableAsync_OrtoDatas_Partition(countyId_AcquisitionCoverage_NotBackfilled));
                Assert.True(await npgsqlConnection.TableAsync_OrtoDataAcquisition_Partition(countyId_AcquisitionCoverage));

                await InsertAsync(countyId_AcquisitionCoverage, "XUNIT-ACQ-A");
                await InsertAsync(countyId_AcquisitionCoverage, "XUNIT-ACQ-B");
                await InsertAsync(countyId_AcquisitionCoverage, "XUNIT-ACQ-C");
                await InsertAsync(countyId_AcquisitionCoverage_NotBackfilled, "XUNIT-ACQ-D");

                DateTimeOffset dateTimeOffset_Day = new(2015, 6, 12, 0, 0, 0, TimeSpan.Zero);
                List<OrtoDataAcquisition> ortoDataAcquisitions =
                [
                    new(countyId_AcquisitionCoverage, "XUNIT-ACQ-A", 2015, dateTimeOffset_Day, OrtoDatePrecision.Day, "XUNIT-SHEET", "hash-a-2015"),
                    new(countyId_AcquisitionCoverage, "XUNIT-ACQ-A", 2018, null, OrtoDatePrecision.Undefined, null, "hash-a-2018"),
                    new(countyId_AcquisitionCoverage, "XUNIT-ACQ-B", 2018, null, OrtoDatePrecision.Undefined, null, "hash-b-2018"),
                    new(countyId_AcquisitionCoverage, "XUNIT-ACQ-ORPHAN", 2010, new DateTimeOffset(2010, 1, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Year, "XUNIT-SHEET", "hash-orphan"),
                ];
                // The write names the references it changed: A, B and the orphan.
                Assert.Equal(3, (await OrtoDatasPostgreSQLConverter.UpdateOrtoDataAcquisitionsAsync(npgsqlConnection, countyId_AcquisitionCoverage, ortoDataAcquisitions))?.Count);

                List<OrtoDataAcquisitionCoverageResult>? ortoDataAcquisitionCoverageResults = await OrtoDatasPostgreSQLConverter.GetAcquisitionCoveragesByCountyIdsAsync(npgsqlConnection, [countyId_AcquisitionCoverage, countyId_AcquisitionCoverage_NotBackfilled, countyId_Empty]);
                Assert.NotNull(ortoDataAcquisitionCoverageResults);
                Assert.Equal(2, ortoDataAcquisitionCoverageResults.Count);

                OrtoDataAcquisitionCoverageResult ortoDataAcquisitionCoverageResult = ortoDataAcquisitionCoverageResults[0];
                Assert.Equal(countyId_AcquisitionCoverage, ortoDataAcquisitionCoverageResult.CountyId);
                Assert.Equal(3, ortoDataAcquisitionCoverageResult.SeriesCount);
                Assert.Equal(2, ortoDataAcquisitionCoverageResult.WithAcquisitionSeriesCount);
                Assert.Equal(1, ortoDataAcquisitionCoverageResult.WithoutAcquisitionSeriesCount);
                Assert.Equal(1, ortoDataAcquisitionCoverageResult.DatedSeriesCount);
                Assert.Equal(1, ortoDataAcquisitionCoverageResult.DayCount);
                Assert.Equal(0, ortoDataAcquisitionCoverageResult.MonthCount);
                Assert.Equal(0, ortoDataAcquisitionCoverageResult.YearCount);
                Assert.Equal(2, ortoDataAcquisitionCoverageResult.UndefinedCount);
                Assert.Equal(3, ortoDataAcquisitionCoverageResult.ImageCount);
                Assert.Equal(dateTimeOffset_Day, ortoDataAcquisitionCoverageResult.AcquisitionDate_First);
                Assert.Equal(dateTimeOffset_Day, ortoDataAcquisitionCoverageResult.AcquisitionDate_Last);
                Assert.NotNull(ortoDataAcquisitionCoverageResult.CreatedAt_Last);

                OrtoDataAcquisitionCoverageResult ortoDataAcquisitionCoverageResult_NotBackfilled = ortoDataAcquisitionCoverageResults[1];
                Assert.Equal(countyId_AcquisitionCoverage_NotBackfilled, ortoDataAcquisitionCoverageResult_NotBackfilled.CountyId);
                Assert.Equal(1, ortoDataAcquisitionCoverageResult_NotBackfilled.SeriesCount);
                Assert.Equal(1, ortoDataAcquisitionCoverageResult_NotBackfilled.WithoutAcquisitionSeriesCount);
                Assert.Equal(0, ortoDataAcquisitionCoverageResult_NotBackfilled.ImageCount);
                Assert.Null(ortoDataAcquisitionCoverageResult_NotBackfilled.CreatedAt_Last);
            }
            finally
            {
                await CleanupAsync();
            }

            async Task InsertAsync(int countyId, string reference)
            {
                GIS.Classes.OrtoDatas ortoDatas = new(reference, []);
                await using NpgsqlCommand npgsqlCommand = new($"INSERT INTO {Constants.TableName.OrtoDatas} (county_id, reference, object) VALUES (@county_id, @reference, @object);", npgsqlConnection);
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = reference });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = ortoDatas.ToJsonObject()!.ToJsonString() });
                await npgsqlCommand.ExecuteNonQueryAsync();
            }

            async Task CleanupAsync()
            {
                int[] countyIds = [countyId_AcquisitionCoverage, countyId_AcquisitionCoverage_NotBackfilled];
                foreach (int countyId in countyIds)
                {
                    await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDatas}_{countyId};");
                    await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDataAcquisition}_{countyId};");
                }
            }
        }
    }
}
