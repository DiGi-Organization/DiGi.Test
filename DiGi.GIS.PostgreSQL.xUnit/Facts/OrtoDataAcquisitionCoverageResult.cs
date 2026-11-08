using DiGi.GIS.PostgreSQL.Classes;
using System;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the constructor carries every tally through, that the derived counts agree with the stored ones, and that a populated instance survives a JSON round trip, the copy constructor and a clone.
        /// <para><see cref="OrtoDataAcquisitionCoverageResult.WithoutAcquisitionSeriesCount"/> and <see cref="OrtoDataAcquisitionCoverageResult.ImageCount"/> are derived rather than stored so they cannot drift from what they are computed from, and that is what makes them worth asserting.</para>
        /// </summary>
        [Fact]
        public void OrtoDataAcquisitionCoverageResult_Serialization()
        {
            DateTimeOffset dateTimeOffset_AcquisitionFirst = new(2009, 5, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset dateTimeOffset_AcquisitionLast = new(2024, 7, 18, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset dateTimeOffset_CreatedLast = new(2026, 10, 2, 14, 12, 0, TimeSpan.FromHours(2));

            OrtoDataAcquisitionCoverageResult ortoDataAcquisitionCoverageResult = new(73485, 44_809, 40_000, 39_500, 300_000, 20_000, 5_000, 1_200, dateTimeOffset_AcquisitionFirst, dateTimeOffset_AcquisitionLast, dateTimeOffset_CreatedLast);

            Assert.Equal(73485, ortoDataAcquisitionCoverageResult.CountyId);
            Assert.Equal(44_809, ortoDataAcquisitionCoverageResult.SeriesCount);
            Assert.Equal(40_000, ortoDataAcquisitionCoverageResult.WithAcquisitionSeriesCount);
            Assert.Equal(4_809, ortoDataAcquisitionCoverageResult.WithoutAcquisitionSeriesCount);
            Assert.Equal(39_500, ortoDataAcquisitionCoverageResult.DatedSeriesCount);
            Assert.Equal(326_200, ortoDataAcquisitionCoverageResult.ImageCount);

            string? json = Core.Convert.ToSystem_String(ortoDataAcquisitionCoverageResult);
            Assert.NotNull(json);

            OrtoDataAcquisitionCoverageResult? ortoDataAcquisitionCoverageResult_Json = Core.Convert.ToDiGi<OrtoDataAcquisitionCoverageResult>(json)?.FirstOrDefault();
            Assert.NotNull(ortoDataAcquisitionCoverageResult_Json);

            OrtoDataAcquisitionCoverageResult ortoDataAcquisitionCoverageResult_Copy = new(ortoDataAcquisitionCoverageResult);

            OrtoDataAcquisitionCoverageResult[] ortoDataAcquisitionCoverageResults = [ortoDataAcquisitionCoverageResult_Json, ortoDataAcquisitionCoverageResult_Copy];
            foreach (OrtoDataAcquisitionCoverageResult ortoDataAcquisitionCoverageResult_Check in ortoDataAcquisitionCoverageResults)
            {
                Assert.Equal(73485, ortoDataAcquisitionCoverageResult_Check.CountyId);
                Assert.Equal(44_809, ortoDataAcquisitionCoverageResult_Check.SeriesCount);
                Assert.Equal(40_000, ortoDataAcquisitionCoverageResult_Check.WithAcquisitionSeriesCount);
                Assert.Equal(39_500, ortoDataAcquisitionCoverageResult_Check.DatedSeriesCount);
                Assert.Equal(300_000, ortoDataAcquisitionCoverageResult_Check.DayCount);
                Assert.Equal(20_000, ortoDataAcquisitionCoverageResult_Check.MonthCount);
                Assert.Equal(5_000, ortoDataAcquisitionCoverageResult_Check.YearCount);
                Assert.Equal(1_200, ortoDataAcquisitionCoverageResult_Check.UndefinedCount);
                Assert.Equal(dateTimeOffset_AcquisitionFirst, ortoDataAcquisitionCoverageResult_Check.AcquisitionDate_First);
                Assert.Equal(dateTimeOffset_AcquisitionLast, ortoDataAcquisitionCoverageResult_Check.AcquisitionDate_Last);
                Assert.Equal(dateTimeOffset_CreatedLast, ortoDataAcquisitionCoverageResult_Check.CreatedAt_Last);
            }

            Core.xUnit.Query.SerializationCheck(ortoDataAcquisitionCoverageResult);
        }

        /// <summary>
        /// Verifies that a part the backfill never reached round trips with null dates and every series counted as backlog.
        /// </summary>
        [Fact]
        public void OrtoDataAcquisitionCoverageResult_NotBackfilled()
        {
            OrtoDataAcquisitionCoverageResult ortoDataAcquisitionCoverageResult = new(73482, 1, 0, 0, 0, 0, 0, 0, null, null, null);

            Assert.Equal(1, ortoDataAcquisitionCoverageResult.WithoutAcquisitionSeriesCount);
            Assert.Equal(0, ortoDataAcquisitionCoverageResult.ImageCount);

            string? json = Core.Convert.ToSystem_String(ortoDataAcquisitionCoverageResult);
            OrtoDataAcquisitionCoverageResult? ortoDataAcquisitionCoverageResult_Json = Core.Convert.ToDiGi<OrtoDataAcquisitionCoverageResult>(json)?.FirstOrDefault();

            Assert.NotNull(ortoDataAcquisitionCoverageResult_Json);
            Assert.Null(ortoDataAcquisitionCoverageResult_Json.AcquisitionDate_First);
            Assert.Null(ortoDataAcquisitionCoverageResult_Json.CreatedAt_Last);

            Core.xUnit.Query.SerializationCheck(ortoDataAcquisitionCoverageResult);
        }
    }
}
