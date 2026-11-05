using DiGi.GIS.IO.Classes;
using DiGi.GIS.IO.Enums;
using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.IO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Streams the Overture GeoParquet sample across both of its row groups and pins the shape a consumer sees: EPSG:4326 from the PROJJSON CRS, the identifier as the source id, height as a JSON number, the repeated <c>sources</c> grouped back into per-row arrays, and the covering <c>bbox</c> columns kept out of the attributes because they are derived rather than source data.
        /// <para>The bounding box sits wholly inside the first row group, so the second group has to be skipped by its covering statistics and the count of three doubles as a second, weaker pruning observation.</para>
        /// </summary>
        [Fact]
        public async Task Footprints_GeoParquetAsync_Overture()
        {
            string path = FixturePath("Overture_Sample.parquet");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_GeoParquetAsync(path, BuildingDataset.Overture));
            Assert.Equal(5, footprints.Count);
            Assert.All(footprints, x => Assert.Equal(4326, x.SourceSrid));
            Assert.All(footprints, x => Assert.IsType<PolygonalFace2D>(x.Geometry));
            string[] sourceIds = ["overture-1", "overture-2", "overture-3", "overture-4", "overture-5"];
            Assert.Equal(sourceIds, footprints.Select(x => x.SourceId).ToList());

            Footprint footprint = footprints[0];
            Assert.Equal("overture-1", footprint.Attributes?["id"]?.GetValue<string>());
            Assert.Equal(JsonValueKind.Number, footprint.Attributes?["height"]?.GetValueKind());
            Assert.Equal(10.5, footprint.Attributes?["height"]?.GetValue<double>());
            Assert.False(footprint.Attributes?.ContainsKey("bbox"));

            Assert.True(footprint.Attributes?["sources"] is JsonArray sources && sources.Count == 2, $"sources row 0: {footprint.Attributes?["sources"]}");
            Assert.Equal("osm", footprint.Attributes?["sources"]?[0]?["dataset"]?.GetValue<string>());
            Assert.True(footprints[2].Attributes?["sources"] is JsonArray sources_3 && sources_3.Count == 3, "sources row 2 count");

            Assert.Equal("overture-1", footprint.Provenance?["id"]?.GetValue<string>());
            Assert.True(footprint.Provenance?["sources"] is JsonArray);

            List<Footprint> footprints_Near = await CollectAsync(Query.Footprints_GeoParquetAsync(path, BuildingDataset.Overture, new BoundingBox2D(14.399, 50.079, 0.004, 0.004)));
            Assert.Equal(3, footprints_Near.Count);
            Assert.Equal(["overture-1", "overture-2", "overture-3"], footprints_Near.Select(x => x.SourceId).ToList());
        }

        /// <summary>
        /// Proves row-group pruning differentially: the second row group holds deliberately invalid WKB behind far-away covering statistics, so a read bounded to the first group must succeed with its two rows (the corrupt group was never opened), while the same file read without a bounding box must fail loudly when it reaches the corrupt geometry.
        /// <para>A filter that only dropped rows after decoding would throw in both cases; the split outcome is what shows the footer statistics pruned the group before any decode happened.</para>
        /// </summary>
        [Fact]
        public async Task Footprints_GeoParquetAsync_RowGroupPruning()
        {
            string path = FixturePath("Overture_RowGroupPruning_Sample.parquet");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_GeoParquetAsync(path, BuildingDataset.Overture, new BoundingBox2D(14.399, 50.079, 0.004, 0.004)));
            Assert.Equal(2, footprints.Count);
            Assert.Equal(["pruning-1", "pruning-2"], footprints.Select(x => x.SourceId).ToList());
            Assert.All(footprints, x => Assert.Equal(4326, x.SourceSrid));

            await Assert.ThrowsAnyAsync<Exception>(() => CollectAsync(Query.Footprints_GeoParquetAsync(path, BuildingDataset.Overture)));
        }

        /// <summary>
        /// Fails loudly, at enumeration start, on a GeoParquet file whose <c>geo</c> metadata declares EPSG:3857 rather than silently reading coordinates in a reference system the reader never converted.
        /// </summary>
        [Fact]
        public async Task Footprints_GeoParquetAsync_UnsupportedCrs()
        {
            string path = FixturePath("Unsupported_Crs_Sample.parquet");

            NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => CollectAsync(Query.Footprints_GeoParquetAsync(path, BuildingDataset.Overture)));

            Assert.Contains("3857", exception.Message);
            Assert.Contains("4326", exception.Message);
            Assert.Contains(path, exception.Message);
        }
    }
}
