using DiGi.OSM.Classes;

namespace DiGi.OSM.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the summary contract: a snapshot taken from a counter carries every count, <see cref="BuildingReadSummary.RejectionCount"/> equals the per-reason sums, the rejection sample survives a JSON round trip (<c>SerializationCheck</c>), the copy constructor carries every member across (a path the round trip never runs), and a null counter yields an all-zero summary rather than nulls.
        /// <para>Short test (under 2 s).</para>
        /// </summary>
        [Fact]
        public void BuildingReadSummary()
        {
            BuildingReadCounter buildingReadCounter = new()
            {
                WayCount = 7,
                RelationCount = 5,
                FootprintCount = 4,
                ReferencedNodeCount = 45,
                NotBuildingCount = 2,
                UnclosedGeometryCount = 1,
                TooFewNodesCount = 1,
                MissingNodeCount = 1,
                InvalidCoordinateCount = 1,
                MissingMemberWayCount = 1,
                RingNotAssembledCount = 1,
                MalformedObjectCount = 0
            };
            buildingReadCounter.Rejections.Add("way/2: UnclosedGeometry");

            BuildingReadSummary buildingReadSummary = new(buildingReadCounter);
            Assert.Equal(7, buildingReadSummary.WayCount);
            Assert.Equal(5, buildingReadSummary.RelationCount);
            Assert.Equal(4, buildingReadSummary.FootprintCount);
            Assert.Equal(45, buildingReadSummary.ReferencedNodeCount);
            Assert.Equal(2, buildingReadSummary.NotBuildingCount);
            Assert.Equal(1, buildingReadSummary.UnclosedGeometryCount);
            Assert.Equal(1, buildingReadSummary.TooFewNodesCount);
            Assert.Equal(1, buildingReadSummary.MissingNodeCount);
            Assert.Equal(1, buildingReadSummary.InvalidCoordinateCount);
            Assert.Equal(1, buildingReadSummary.MissingMemberWayCount);
            Assert.Equal(1, buildingReadSummary.RingNotAssembledCount);
            Assert.Equal(0, buildingReadSummary.MalformedObjectCount);
            Assert.Equal(8, buildingReadSummary.RejectionCount);
            Assert.Equal("way/2: UnclosedGeometry", Assert.Single(buildingReadSummary.Rejections!));

            Core.xUnit.Query.SerializationCheck(buildingReadSummary);

            // SerializationCheck never runs the copy constructor: assert it member by member.
            BuildingReadSummary buildingReadSummary_Copy = new(buildingReadSummary);
            Assert.Equal(buildingReadSummary.WayCount, buildingReadSummary_Copy.WayCount);
            Assert.Equal(buildingReadSummary.RelationCount, buildingReadSummary_Copy.RelationCount);
            Assert.Equal(buildingReadSummary.FootprintCount, buildingReadSummary_Copy.FootprintCount);
            Assert.Equal(buildingReadSummary.ReferencedNodeCount, buildingReadSummary_Copy.ReferencedNodeCount);
            Assert.Equal(buildingReadSummary.NotBuildingCount, buildingReadSummary_Copy.NotBuildingCount);
            Assert.Equal(buildingReadSummary.UnclosedGeometryCount, buildingReadSummary_Copy.UnclosedGeometryCount);
            Assert.Equal(buildingReadSummary.TooFewNodesCount, buildingReadSummary_Copy.TooFewNodesCount);
            Assert.Equal(buildingReadSummary.MissingNodeCount, buildingReadSummary_Copy.MissingNodeCount);
            Assert.Equal(buildingReadSummary.InvalidCoordinateCount, buildingReadSummary_Copy.InvalidCoordinateCount);
            Assert.Equal(buildingReadSummary.MissingMemberWayCount, buildingReadSummary_Copy.MissingMemberWayCount);
            Assert.Equal(buildingReadSummary.RingNotAssembledCount, buildingReadSummary_Copy.RingNotAssembledCount);
            Assert.Equal(buildingReadSummary.MalformedObjectCount, buildingReadSummary_Copy.MalformedObjectCount);
            Assert.Equal(buildingReadSummary.RejectionCount, buildingReadSummary_Copy.RejectionCount);
            Assert.Equal(buildingReadSummary.Rejections, buildingReadSummary_Copy.Rejections);

            BuildingReadSummary buildingReadSummary_Empty = new((BuildingReadCounter?)null);
            Assert.Equal(0, buildingReadSummary_Empty.WayCount);
            Assert.Equal(0, buildingReadSummary_Empty.RejectionCount);
            Assert.Null(buildingReadSummary_Empty.Rejections);
        }
    }
}
