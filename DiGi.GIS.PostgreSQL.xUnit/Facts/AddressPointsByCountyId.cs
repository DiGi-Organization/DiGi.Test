using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a point is filed under the part containing it, and that a point in none of the parts is returned as outside rather than filed.
        /// <para>Reference: DiGi.GIS.PostgreSQL#109.</para>
        /// </summary>
        [Fact]
        public void AddressPointsByCountyId_InsideAndOutside()
        {
            Dictionary<int, PolygonalFace2D> polygonalFace2Ds_ById = new() { { 10, PolygonalFace2D_Square(0, 0, 100) } };

            AddressPoint addressPoint_Inside = AddressPoint_At(new Point2D(50, 50), "inside");
            AddressPoint addressPoint_Outside = AddressPoint_At(new Point2D(5000, 5000), "outside");
            AddressPoint addressPoint_NoLocation = AddressPoint_At(null, "none");

            (Dictionary<int, List<AddressPoint>> AddressPoints_ByCountyId, List<AddressPoint> AddressPoints_Outside)? result = polygonalFace2Ds_ById.AddressPointsByCountyId([addressPoint_Inside, null, addressPoint_Outside, addressPoint_NoLocation]);

            Assert.NotNull(result);
            Assert.Single(result.Value.AddressPoints_ByCountyId);
            Assert.Equal("inside", Assert.Single(result.Value.AddressPoints_ByCountyId[10]).ReferenceId);
            Assert.Equal(2, result.Value.AddressPoints_Outside.Count);
            Assert.Contains(result.Value.AddressPoints_Outside, x => x.ReferenceId == "outside");
            Assert.Contains(result.Value.AddressPoints_Outside, x => x.ReferenceId == "none");

            Assert.Null(((IDictionary<int, PolygonalFace2D>?)null).AddressPointsByCountyId([addressPoint_Inside]));
            Assert.Null(polygonalFace2Ds_ById.AddressPointsByCountyId(null));
        }

        /// <summary>
        /// Verifies the retry case of <c>Modify.UpdateAddressPointsAsync</c>: a point outside the part of the county its commune code names is returned as outside by the first pass, and filed under the neighbouring county part that contains it by the second.
        /// <para>The tolerance pair checks the boundary: a point a hair inside the neighbour's edge is rescued, one beyond the tolerance is not. Reference: DiGi.GIS.PostgreSQL#109.</para>
        /// </summary>
        [Fact]
        public void AddressPointsByCountyId_RetryAgainstNeighbouringCounty()
        {
            // County A (the commune's) and county B (the neighbour) share the edge x = 100.
            Dictionary<int, PolygonalFace2D> polygonalFace2Ds_A = new() { { 1, PolygonalFace2D_Square(0, 0, 100) } };
            Dictionary<int, PolygonalFace2D> polygonalFace2Ds_B = new() { { 2, PolygonalFace2D_Square(100, 0, 100) } };

            double tolerance = 0.01;
            AddressPoint addressPoint_InB = AddressPoint_At(new Point2D(150, 50), "in-b");
            AddressPoint addressPoint_NearEdge = AddressPoint_At(new Point2D(100 - (tolerance / 2), 50), "near-edge");
            AddressPoint addressPoint_Beyond = AddressPoint_At(new Point2D(-1000, 50), "beyond");

            (Dictionary<int, List<AddressPoint>> AddressPoints_ByCountyId, List<AddressPoint> AddressPoints_Outside)? result_A = polygonalFace2Ds_A.AddressPointsByCountyId([addressPoint_InB, addressPoint_Beyond], tolerance);
            Assert.NotNull(result_A);
            Assert.Empty(result_A.Value.AddressPoints_ByCountyId);
            Assert.Equal(2, result_A.Value.AddressPoints_Outside.Count);

            (Dictionary<int, List<AddressPoint>> AddressPoints_ByCountyId, List<AddressPoint> AddressPoints_Outside)? result_B = polygonalFace2Ds_B.AddressPointsByCountyId(result_A.Value.AddressPoints_Outside, tolerance);
            Assert.NotNull(result_B);
            Assert.Equal("in-b", Assert.Single(result_B.Value.AddressPoints_ByCountyId[2]).ReferenceId);
            Assert.Equal("beyond", Assert.Single(result_B.Value.AddressPoints_Outside).ReferenceId);

            // Just inside the tolerance of B's edge, and just outside it.
            AddressPoint addressPoint_JustInside = AddressPoint_At(new Point2D(100 - (tolerance * 0.9), 50), "just-inside");
            AddressPoint addressPoint_JustOutside = AddressPoint_At(new Point2D(100 - (tolerance * 1.5), 50), "just-outside");

            (Dictionary<int, List<AddressPoint>> AddressPoints_ByCountyId, List<AddressPoint> AddressPoints_Outside)? result_Edge = polygonalFace2Ds_B.AddressPointsByCountyId([addressPoint_JustInside, addressPoint_JustOutside, addressPoint_NearEdge], tolerance);
            Assert.NotNull(result_Edge);
            Assert.Contains(result_Edge.Value.AddressPoints_ByCountyId[2], x => x.ReferenceId == "just-inside");
            Assert.Contains(result_Edge.Value.AddressPoints_Outside, x => x.ReferenceId == "just-outside");
        }

        private static AddressPoint AddressPoint_At(Point2D? point2D, string referenceId)
        {
            return new AddressPoint(point2D, "Street", null, "1", "Locality", null, null, referenceId, null);
        }
    }
}
