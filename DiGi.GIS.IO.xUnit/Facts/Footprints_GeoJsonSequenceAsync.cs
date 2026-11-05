using DiGi.GIS.IO.Classes;
using DiGi.GIS.IO.Enums;
using DiGi.Geometry.Planar.Classes;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiGi.GIS.IO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Streams the gzipped Microsoft sample and pins what the reader has to work out for itself: gzip is detected from the magic bytes although the file is named <c>.csv.gz</c>, the identifier is synthesized as <c>quadkey:index</c> over the zero-based feature index (stable under filtering), attributes are read whether the chunk nests them under <c>properties</c> or at the feature root, numbers stay numbers, and provenance carries the quadkey plus any <c>capture</c> property.
        /// </summary>
        [Fact]
        public async Task Footprints_GeoJsonSequenceAsync_Microsoft()
        {
            string path = FixturePath("Microsoft_Sample.csv.gz");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_GeoJsonSequenceAsync(path, BuildingDataset.Microsoft, "0320101"));
            Assert.Equal(4, footprints.Count);
            Assert.All(footprints, x => Assert.Equal(4326, x.SourceSrid));
            string[] sourceIds = ["0320101:0", "0320101:1", "0320101:2", "0320101:3"];
            Assert.Equal(sourceIds, footprints.Select(x => x.SourceId).ToList());

            // Feature form: attributes under "properties".
            Assert.Equal(JsonValueKind.Number, footprints[0].Attributes?["height"]?.GetValueKind());
            Assert.Equal(12.5, footprints[0].Attributes?["height"]?.GetValue<double>());
            Assert.Equal(0.87, footprints[0].Attributes?["confidence"]?.GetValue<double>());

            // Flat form: attributes at the feature root, geometry and type excluded.
            Assert.Equal(JsonValueKind.Number, footprints[2].Attributes?["height"]?.GetValueKind());
            Assert.False(footprints[2].Attributes?.ContainsKey("geometry"));
            Assert.False(footprints[2].Attributes?.ContainsKey("type"));

            Assert.IsType<PolygonalFace2D>(footprints[0].Geometry);
            Assert.NotNull(footprints[2].Geometry);

            Assert.Equal("0320101", footprints[1].Provenance?["quadkey"]?.GetValue<string>());
            Assert.Equal("2019-07", footprints[1].Provenance?["capture"]?.GetValue<string>());
            Assert.Equal("2021-03", footprints[2].Provenance?["capture"]?.GetValue<string>());
            Assert.True(footprints[0].Provenance != null);
            Assert.False(footprints[0].Provenance?.ContainsKey("capture"));

            List<Footprint> footprints_Near = await CollectAsync(Query.Footprints_GeoJsonSequenceAsync(path, BuildingDataset.Microsoft, "0320101", new BoundingBox2D(14.399, 50.079, 0.004, 0.004)));
            Assert.Equal(3, footprints_Near.Count);

            // The bounding box drops the far feature, but the surviving identifiers keep their line indices.
            Assert.Equal(["0320101:0", "0320101:1", "0320101:2"], footprints_Near.Select(x => x.SourceId).ToList());
        }
    }
}
