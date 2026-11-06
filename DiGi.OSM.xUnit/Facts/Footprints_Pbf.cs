using DiGi.GIS.IO.Classes;
using DiGi.OSM.Classes;
using System.Collections.Generic;
using System.Reflection;

namespace DiGi.OSM.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Resolves a committed fixture in <c>DiGi.Test/files/</c> for the executing test assembly.
        /// </summary>
        /// <param name="fileName">The fixture file name.</param>
        /// <returns>The full path.</returns>
        private static string FixturePath(string fileName)
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), fileName);
            Assert.NotNull(path);
            return path!;
        }

        /// <summary>
        /// Materialises a lazily streamed footprint sequence into a list so a fact can assert on counts and per-footprint values.
        /// </summary>
        /// <param name="footprints">The sequence returned by the reader, or null.</param>
        /// <returns>The footprints in order.</returns>
        private static List<Footprint> Collect(IEnumerable<Footprint>? footprints)
        {
            List<Footprint> result = [];
            if (footprints == null)
            {
                return result;
            }

            foreach (Footprint footprint in footprints)
            {
                result.Add(footprint);
            }

            return result;
        }

        /// <summary>
        /// Reads the committed PBF fixture end to end and pins the run contract: four footprints emitted in file order (two ways, two multipolygon relations), every in-scope object counted, and every refusal reported under exactly one rejection reason - building=no, an unclosed way, a too-short closed way, a missing node, an out-of-range coordinate, a missing relation member and an unassemblable ring, eight rejections in total with no silent drop.
        /// <para>Short test (under 2 s).</para>
        /// </summary>
        [Fact]
        public void Footprints_Pbf()
        {
            string path = FixturePath("OSM_Buildings_Sample.osm.pbf");

            BuildingReadResult? result = Query.Footprints_Pbf(path);
            Assert.NotNull(result);

            List<Footprint> footprints = Collect(result!.Footprints);
            Assert.Equal(4, footprints.Count);
            Assert.Equal(["way/1", "way/6", "relation/101", "relation/102"], footprints.ConvertAll(x => x.SourceId ?? string.Empty));
            Assert.All(footprints, x => Assert.Equal(4326, x.SourceSrid));

            BuildingReadSummary summary = result.Summary;
            Assert.Equal(7, summary.WayCount);
            Assert.Equal(5, summary.RelationCount);
            Assert.Equal(4, summary.FootprintCount);
            // Distinct nodes the candidates reference: 19 from the building ways (way 3's nodes are never
            // collected, way 7 carries no building tag) plus 26 from the relation member ways (way 206
            // belongs to the non-building relation 104); node 32 is referenced but absent from the file.
            Assert.Equal(45, summary.ReferencedNodeCount);

            Assert.Equal(2, summary.NotBuildingCount);
            Assert.Equal(1, summary.UnclosedGeometryCount);
            Assert.Equal(1, summary.TooFewNodesCount);
            Assert.Equal(1, summary.MissingNodeCount);
            Assert.Equal(1, summary.InvalidCoordinateCount);
            Assert.Equal(1, summary.MissingMemberWayCount);
            Assert.Equal(1, summary.RingNotAssembledCount);
            Assert.Equal(0, summary.MalformedObjectCount);
            Assert.Equal(8, summary.RejectionCount);

            Assert.NotNull(summary.Rejections);
            Assert.Equal(8, summary.Rejections!.Count);
            Assert.Contains("way/3: NotBuilding", summary.Rejections);
            Assert.Contains("relation/105: NotBuilding", summary.Rejections);
            Assert.Contains("way/2: UnclosedGeometry", summary.Rejections);
            Assert.Contains("way/5: TooFewNodes", summary.Rejections);
            Assert.Contains("way/4: MissingNode", summary.Rejections);
            Assert.Contains("way/8: InvalidCoordinate", summary.Rejections);
            Assert.Contains("relation/103: MissingMemberWay", summary.Rejections);
            Assert.Contains("relation/106: RingNotAssembled", summary.Rejections);
        }

        /// <summary>
        /// Pins what travels with one footprint: the attribute bag holds every <c>building*</c> key and <c>source</c> but no other tag, the source identifier is the OSM type and id, and the provenance object carries the dataset key, the OSM type and id plus the PBF header timestamp and replication sequence in the shape the building_dataset register expects.
        /// <para>Short test (under 2 s).</para>
        /// </summary>
        [Fact]
        public void Footprints_Pbf_AttributesAndProvenance()
        {
            string path = FixturePath("OSM_Buildings_Sample.osm.pbf");

            BuildingReadResult? result = Query.Footprints_Pbf(path);
            Assert.NotNull(result);
            Assert.NotNull(result!.Header);

            List<Footprint> footprints = Collect(result.Footprints);
            Assert.Equal(4, footprints.Count);

            Footprint footprint_Way = footprints[0];
            Assert.Equal("way/1", footprint_Way.SourceId);
            Assert.Equal(4326, footprint_Way.SourceSrid);
            Assert.IsType<DiGi.Geometry.Planar.Classes.PolygonalFace2D>(footprint_Way.Geometry);

            Assert.NotNull(footprint_Way.Attributes);
            Assert.Equal(3, footprint_Way.Attributes!.Count);
            Assert.Equal("yes", footprint_Way.Attributes["building"]?.GetValue<string>());
            Assert.Equal("2", footprint_Way.Attributes["building:levels"]?.GetValue<string>());
            Assert.Equal("orthophoto", footprint_Way.Attributes["source"]?.GetValue<string>());
            Assert.Null(footprint_Way.Attributes["height"]);

            Assert.NotNull(footprint_Way.Provenance);
            Assert.Equal("osm", footprint_Way.Provenance!["dataset"]?.GetValue<string>());
            Assert.Equal("way", footprint_Way.Provenance["osm_type"]?.GetValue<string>());
            Assert.Equal(1, footprint_Way.Provenance["osm_id"]?.GetValue<long>());
            Assert.Equal("2026-11-04T21:21:03Z", footprint_Way.Provenance["header_timestamp"]?.GetValue<string>());
            Assert.Equal(5123, footprint_Way.Provenance["replication_sequence"]?.GetValue<long>());
            Assert.Equal(PbfFixture.ReplicationBaseUrl, footprint_Way.Provenance["replication_base_url"]?.GetValue<string>());

            Footprint footprint_Relation = footprints[2];
            Assert.Equal("relation/101", footprint_Relation.SourceId);
            Assert.Equal("relation", footprint_Relation.Provenance?["osm_type"]?.GetValue<string>());
            Assert.Equal(101, footprint_Relation.Provenance?["osm_id"]?.GetValue<long>());
        }

        /// <summary>
        /// Pins the bounding box filter: a box around the cluster A building keeps only it, a box around the cluster B building keeps only that one, a box touching nothing keeps zero footprints - and in every case the summary stays complete (objects read and rejections counted), proving a filtered footprint is a filter rather than a rejection. A null or whitespace path returns null.
        /// <para>Short test (under 2 s).</para>
        /// </summary>
        [Fact]
        public void Footprints_Pbf_BoundingBox()
        {
            Assert.Null(Query.Footprints_Pbf(null));
            Assert.Null(Query.Footprints_Pbf("   "));

            string path = FixturePath("OSM_Buildings_Sample.osm.pbf");

            DiGi.Geometry.Planar.Classes.BoundingBox2D boundingBox_A = new(new DiGi.Geometry.Planar.Classes.Point2D(16.9299, 51.1299), new DiGi.Geometry.Planar.Classes.Point2D(16.9302, 51.1302));
            BuildingReadResult? result_A = Query.Footprints_Pbf(path, boundingBox_A);
            Assert.NotNull(result_A);
            List<Footprint> footprints_A = Collect(result_A!.Footprints);
            Footprint footprint_A = Assert.Single(footprints_A);
            Assert.Equal("way/1", footprint_A.SourceId);
            Assert.Equal(1, result_A.Summary.FootprintCount);
            Assert.Equal(7, result_A.Summary.WayCount);
            Assert.Equal(8, result_A.Summary.RejectionCount);

            DiGi.Geometry.Planar.Classes.BoundingBox2D boundingBox_B = new(new DiGi.Geometry.Planar.Classes.Point2D(21.0099, 52.2399), new DiGi.Geometry.Planar.Classes.Point2D(21.0102, 52.2402));
            BuildingReadResult? result_B = Query.Footprints_Pbf(path, boundingBox_B);
            Assert.NotNull(result_B);
            List<Footprint> footprints_B = Collect(result_B!.Footprints);
            Footprint footprint_B = Assert.Single(footprints_B);
            Assert.Equal("way/6", footprint_B.SourceId);
            Assert.Equal(8, result_B.Summary.RejectionCount);

            DiGi.Geometry.Planar.Classes.BoundingBox2D boundingBox_None = new(new DiGi.Geometry.Planar.Classes.Point2D(0, 0), new DiGi.Geometry.Planar.Classes.Point2D(1, 1));
            BuildingReadResult? result_None = Query.Footprints_Pbf(path, boundingBox_None);
            Assert.NotNull(result_None);
            Assert.Empty(Collect(result_None!.Footprints));
            Assert.Equal(0, result_None.Summary.FootprintCount);
            Assert.Equal(7, result_None.Summary.WayCount);
            Assert.Equal(5, result_None.Summary.RelationCount);
            Assert.Equal(8, result_None.Summary.RejectionCount);
        }
    }
}
