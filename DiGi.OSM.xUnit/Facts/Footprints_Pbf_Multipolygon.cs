using DiGi.GIS.IO.Classes;
using DiGi.OSM.Classes;
using System.Collections.Generic;

namespace DiGi.OSM.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins how multipolygon relations become geometry: a relation with one outer ring becomes a single polygonal face whose hole survives as an internal edge with its own coordinates, and a relation with two outer rings stays ONE footprint whose geometry is a <c>GeometryCollection2D</c> of two faces, the hole landing on the outer ring that contains it - the containment check on the way to the face is what makes a dropped hole a reported rejection instead of a silent loss.
        /// <para>Short test (under 2 s).</para>
        /// </summary>
        [Fact]
        public void Footprints_Pbf_Multipolygon()
        {
            string path = FixturePath("OSM_Buildings_Sample.osm.pbf");

            BuildingReadResult? result = Query.Footprints_Pbf(path);
            Assert.NotNull(result);
            Assert.Equal(0, result!.Summary.RingNotAssembledCount);

            List<Footprint> footprints = Collect(result.Footprints);
            Assert.Equal(4, footprints.Count);

            Footprint footprint_Holed = footprints[2];
            Assert.Equal("relation/101", footprint_Holed.SourceId);

            DiGi.Geometry.Planar.Classes.PolygonalFace2D face_Holed = Assert.IsType<DiGi.Geometry.Planar.Classes.PolygonalFace2D>(footprint_Holed.Geometry);
            Assert.NotNull(face_Holed.ExternalEdge);
            List<DiGi.Geometry.Planar.Classes.Point2D>? point2Ds_Outer = face_Holed.ExternalEdge!.GetPoints();
            Assert.NotNull(point2Ds_Outer);
            Assert.Equal(4, point2Ds_Outer!.Count);
            Assert.Equal(16.9310, point2Ds_Outer[0].X, 7);
            Assert.Equal(51.1310, point2Ds_Outer[0].Y, 7);

            Assert.NotNull(face_Holed.InternalEdges);
            Assert.Single(face_Holed.InternalEdges!);
            List<DiGi.Geometry.Planar.Classes.Point2D>? point2Ds_Hole = face_Holed.InternalEdges![0].GetPoints();
            Assert.NotNull(point2Ds_Hole);
            Assert.Equal(4, point2Ds_Hole!.Count);
            Assert.Equal(16.9311, point2Ds_Hole[0].X, 7);
            Assert.Equal(51.1311, point2Ds_Hole[0].Y, 7);

            Footprint footprint_Split = footprints[3];
            Assert.Equal("relation/102", footprint_Split.SourceId);

            DiGi.Geometry.Planar.Classes.GeometryCollection2D geometryCollection2D = Assert.IsType<DiGi.Geometry.Planar.Classes.GeometryCollection2D>(footprint_Split.Geometry);
            Assert.Equal(2, geometryCollection2D.Count);

            List<DiGi.Geometry.Planar.Interfaces.ICollectable2D> collectable2Ds = [.. geometryCollection2D];
            DiGi.Geometry.Planar.Classes.PolygonalFace2D face_First = Assert.IsType<DiGi.Geometry.Planar.Classes.PolygonalFace2D>(collectable2Ds[0]);
            DiGi.Geometry.Planar.Classes.PolygonalFace2D face_Second = Assert.IsType<DiGi.Geometry.Planar.Classes.PolygonalFace2D>(collectable2Ds[1]);

            // The hole sits inside the first outer ring, so it lands there; the second outer ring keeps none
            // (a face without holes reports null internal edges rather than an empty list).
            Assert.NotNull(face_First.InternalEdges);
            Assert.Single(face_First.InternalEdges!);
            Assert.True(face_Second.InternalEdges == null || face_Second.InternalEdges.Count == 0, "The second outer ring must carry no hole.");

            List<DiGi.Geometry.Planar.Classes.Point2D>? point2Ds_First = face_First.ExternalEdge!.GetPoints();
            Assert.NotNull(point2Ds_First);
            Assert.Equal(16.9400, point2Ds_First![0].X, 7);
            Assert.Equal(51.1300, point2Ds_First[0].Y, 7);

            List<DiGi.Geometry.Planar.Classes.Point2D>? point2Ds_Second = face_Second.ExternalEdge!.GetPoints();
            Assert.NotNull(point2Ds_Second);
            Assert.Equal(16.9450, point2Ds_Second![0].X, 7);
            Assert.Equal(51.1300, point2Ds_Second[0].Y, 7);
        }
    }
}
