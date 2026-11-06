using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the three outcomes of the open building filing decision - filed, filtered, rejected - and that they are told apart by the <c>rejected</c> channel rather than by a null alone (DiGi.GIS.PostgreSQL#125).
        /// <para>A footprint inside a county part files under it; a footprint that resolves to a county part outside the run's scope, or that falls outside the scope bounding box, is filtered - the expected residue of a nationwide file read for pilot areas and never a rejection; a footprint no county envelope reaches is rejected, which is the Geofabrik border-buffer case. The scope bounding box is consulted before the border test, so the residue of a scoped run does not count as rejections, and a centroid files through the lowest-numbered face containing it or files nowhere. The fact needs no database.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingCountyId_FiledFilteredRejected()
        {
            // Two county parts in the classic fact space: no reprojection is involved here, the decision is pure geometry.
            AdministrativeAreal2D administrativeAreal2D_A = AdministrativeAreal2D_Square(10, "9901", 0, 0, 100);
            AdministrativeAreal2D administrativeAreal2D_B = AdministrativeAreal2D_Square(20, "9901", 1000, 0, 100);

            List<AdministrativeAreal2D> administrativeAreal2Ds = [administrativeAreal2D_A, administrativeAreal2D_B];

            Dictionary<int, IPolygonal2D> polygonal2Ds_ByCountyId = administrativeAreal2Ds.Polygonal2DsByCountyId();
            Assert.Equal(2, polygonal2Ds_ByCountyId.Count);

            Dictionary<int, BoundingBox2D> boundingBox2Ds_ByCountyId = [];
            foreach (KeyValuePair<int, PolygonalFace2D> pair in administrativeAreal2Ds.PolygonalFace2DsById())
            {
                if (pair.Value.GetBoundingBox() is BoundingBox2D boundingBox2D)
                {
                    boundingBox2Ds_ByCountyId[pair.Key] = boundingBox2D;
                }
            }

            Assert.Equal(2, boundingBox2Ds_ByCountyId.Count);

            // Filed: inside part A, no scope restriction.
            IPolygonal2D_Square(40, 40, 10, out Polygon2D polygon2D_Inside);
            BoundingBox2D boundingBox2D_Inside = new(new Point2D(40, 40), new Point2D(50, 50));

            int? countyId = Query.OpenBuildingCountyId(boundingBox2D_Inside, polygon2D_Inside, null, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, null, null, Core.Constants.Tolerance.MacroDistance, out bool rejected);
            Assert.Equal(10, countyId);
            Assert.False(rejected);

            // Filtered: part A resolves, but part A is not in this run's scope - the row belongs to a later
            // run, not to a rejection list.
            HashSet<int> countyIds_Scope = [20];
            countyId = Query.OpenBuildingCountyId(boundingBox2D_Inside, polygon2D_Inside, null, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out rejected);
            Assert.Null(countyId);
            Assert.False(rejected);

            // Rejected: no county envelope reaches the footprint - a footprint outside the digitised
            // territory, such as one carried by the Geofabrik border buffer.
            IPolygonal2D_Square(50000, 50000, 10, out Polygon2D polygon2D_Outside);
            BoundingBox2D boundingBox2D_Outside = new(new Point2D(50000, 50000), new Point2D(50010, 50010));

            countyId = Query.OpenBuildingCountyId(boundingBox2D_Outside, polygon2D_Outside, null, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, null, null, Core.Constants.Tolerance.MacroDistance, out rejected);
            Assert.Null(countyId);
            Assert.True(rejected);

            // Filtered before the border test: a footprint outside the scope bounding box was never in
            // scope, so its rejection count stays clean even though it lies outside every county.
            BoundingBox2D boundingBox2D_Scope = new(new Point2D(0, 0), new Point2D(200, 200));

            countyId = Query.OpenBuildingCountyId(boundingBox2D_Outside, polygon2D_Outside, null, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, null, boundingBox2D_Scope, Core.Constants.Tolerance.MacroDistance, out rejected);
            Assert.Null(countyId);
            Assert.False(rejected);

            // A footprint inside the scope box but outside every county is still rejected: the box narrows
            // the run, it does not vouch for the footprint. The box spans (0,0)-(200,200) while part A ends
            // at 100, so a footprint at (150,150) is in scope and in no county at all.
            IPolygonal2D_Square(150, 150, 10, out Polygon2D polygon2D_Gap);
            BoundingBox2D boundingBox2D_Gap = new(new Point2D(150, 150), new Point2D(160, 160));

            countyId = Query.OpenBuildingCountyId(boundingBox2D_Gap, polygon2D_Gap, null, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, null, boundingBox2D_Scope, Core.Constants.Tolerance.MacroDistance, out rejected);
            Assert.Null(countyId);
            Assert.True(rejected);

            // Centroid filing: the lowest-numbered face containing the point, and rejection when no face
            // contains it - a centroid carries no extent to widen to a nearest part.
            Dictionary<int, PolygonalFace2D> polygonalFace2Ds_ByCountyId = administrativeAreal2Ds.PolygonalFace2DsById();
            BoundingBox2D boundingBox2D_Point = new(new Point2D(45, 45), new Point2D(45, 45));

            countyId = Query.OpenBuildingCountyId(boundingBox2D_Point, null, new Point2D(45, 45), polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, polygonalFace2Ds_ByCountyId, null, null, Core.Constants.Tolerance.MacroDistance, out rejected);
            Assert.Equal(10, countyId);
            Assert.False(rejected);

            BoundingBox2D boundingBox2D_PointOutside = new(new Point2D(50000, 50000), new Point2D(50000, 50000));
            countyId = Query.OpenBuildingCountyId(boundingBox2D_PointOutside, null, new Point2D(50000, 50000), polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, polygonalFace2Ds_ByCountyId, null, null, Core.Constants.Tolerance.MacroDistance, out rejected);
            Assert.Null(countyId);
            Assert.True(rejected);

            // A centroid run without faces cannot file anything, and a null envelope is always a rejection:
            // there is nothing to place.
            countyId = Query.OpenBuildingCountyId(boundingBox2D_Point, null, new Point2D(45, 45), polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, null, null, Core.Constants.Tolerance.MacroDistance, out rejected);
            Assert.Null(countyId);
            Assert.True(rejected);

            countyId = Query.OpenBuildingCountyId(null, polygon2D_Inside, null, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, null, null, Core.Constants.Tolerance.MacroDistance, out rejected);
            Assert.Null(countyId);
            Assert.True(rejected);
        }
    }
}
