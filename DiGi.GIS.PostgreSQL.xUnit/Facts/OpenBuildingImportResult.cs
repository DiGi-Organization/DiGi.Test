using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the per-rejection-reason tally of <see cref="OpenBuildingImportResult"/> survives the JSON round trip the result's persistence and cloning are built on, and that its entries still sum to the rejection count afterwards (DiGi.GIS.PostgreSQL#172).
        /// <para>The instance is realistic: a run that stored ten rows, filtered twenty and rejected 53,361 - three reasons, the sum exact - so a tally that lost an entry or deserialized an enum key by the wrong name fails the sum rather than merely looking different. The member-by-member assertion on the round-tripped result covers every public member, the new one included.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImportResult_RejectionCountsRoundTrip()
        {
            Dictionary<UpdateRejectionReason, long> rejectionCounts = new()
            {
                [UpdateRejectionReason.CountyUnresolved] = 52000,
                [UpdateRejectionReason.GeometryRejected] = 1360,
                [UpdateRejectionReason.Undefined] = 1,
            };

            OpenBuildingImportResult openBuildingImportResult = new(10, 20, 53361, [new Rejection("xunit-1", UpdateRejectionReason.CountyUnresolved)], 7, rejectionCounts);

            Assert.Equal(53361, openBuildingImportResult.RejectionCount);
            Assert.Equal(3, openBuildingImportResult.RejectionCounts.Count);
            Assert.Equal(53361, openBuildingImportResult.RejectionCounts.Values.Sum());

            Core.xUnit.Query.SerializationCheck(openBuildingImportResult);

            JsonObject? jsonObject = openBuildingImportResult.ToJsonObject();
            Assert.NotNull(jsonObject);

            OpenBuildingImportResult openBuildingImportResult_Trip = new(jsonObject);

            Assert.Equal(openBuildingImportResult.RowCount, openBuildingImportResult_Trip.RowCount);
            Assert.Equal(openBuildingImportResult.FilteredCount, openBuildingImportResult_Trip.FilteredCount);
            Assert.Equal(openBuildingImportResult.RejectionCount, openBuildingImportResult_Trip.RejectionCount);
            Assert.Equal(openBuildingImportResult.DatasetId, openBuildingImportResult_Trip.DatasetId);
            Assert.Equal(openBuildingImportResult.Rejections.Count, openBuildingImportResult_Trip.Rejections.Count);
            Assert.Equal(openBuildingImportResult.Rejections[0].Reference, openBuildingImportResult_Trip.Rejections[0].Reference);
            Assert.Equal(openBuildingImportResult.Rejections[0].UpdateRejectionReason, openBuildingImportResult_Trip.Rejections[0].UpdateRejectionReason);

            Assert.Equal(3, openBuildingImportResult_Trip.RejectionCounts.Count);
            foreach (KeyValuePair<UpdateRejectionReason, long> pair in openBuildingImportResult.RejectionCounts)
            {
                Assert.Equal(pair.Value, openBuildingImportResult_Trip.RejectionCounts.GetValueOrDefault(pair.Key));
            }

            Assert.Equal(openBuildingImportResult_Trip.RejectionCount, openBuildingImportResult_Trip.RejectionCounts.Values.Sum());
        }

        /// <summary>
        /// Verifies that a copied <see cref="OpenBuildingImportResult"/> carries the same tally member for member and never aliases the dictionary it was handed (DiGi.GIS.PostgreSQL#172).
        /// <para>The import constructs the result from its own locals, but a result that shared the caller's dictionary would let any later edit rewrite an outcome already reported - the same rule the rejection list follows, so both collections are copied entry by entry.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImportResult_RejectionCountsCopyAndNoAliasing()
        {
            Dictionary<UpdateRejectionReason, long> rejectionCounts = new()
            {
                [UpdateRejectionReason.GeometryRejected] = 2,
                [UpdateRejectionReason.PartitionUnavailable] = 1,
            };

            OpenBuildingImportResult openBuildingImportResult = new(5, 0, 3, null, null, rejectionCounts);
            OpenBuildingImportResult openBuildingImportResult_Copy = new(openBuildingImportResult);

            Assert.Equal(openBuildingImportResult.RejectionCount, openBuildingImportResult_Copy.RejectionCount);
            Assert.Equal(2, openBuildingImportResult_Copy.RejectionCounts.Count);
            Assert.Equal(2, openBuildingImportResult_Copy.RejectionCounts.GetValueOrDefault(UpdateRejectionReason.GeometryRejected));
            Assert.Equal(1, openBuildingImportResult_Copy.RejectionCounts.GetValueOrDefault(UpdateRejectionReason.PartitionUnavailable));

            // The caller keeps its dictionary: neither an added reason nor a changed count reaches the result.
            rejectionCounts[UpdateRejectionReason.Undefined] = 4;
            rejectionCounts[UpdateRejectionReason.GeometryRejected] = 99;

            Assert.Equal(2, openBuildingImportResult.RejectionCounts.Count);
            Assert.Equal(2, openBuildingImportResult.RejectionCounts[UpdateRejectionReason.GeometryRejected]);
            Assert.Equal(3, openBuildingImportResult.RejectionCounts.Values.Sum());
        }
    }
}
