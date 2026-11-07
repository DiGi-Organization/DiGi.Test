using DiGi.GIS.IO.Classes;
using DiGi.GIS.IO.Enums;
using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiGi.GIS.IO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Materializes a streaming reader sequence into a list so a fact can assert on counts and per-row values.
        /// </summary>
        /// <param name="footprints">The sequence returned by a reader, or null.</param>
        /// <returns>The footprints in order.</returns>
        private static async Task<List<Footprint>> CollectAsync(IAsyncEnumerable<Footprint>? footprints)
        {
            List<Footprint> result = [];
            if (footprints == null)
            {
                return result;
            }

            await foreach (Footprint footprint in footprints)
            {
                result.Add(footprint);
            }

            return result;
        }

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
        /// Streams the EUBUCCO GeoPackage sample end to end: six rows all read (proving every GeoPackageBinary header combination in the fixture - both byte orders, envelope present and absent, both WKB byte orders), each footprint in the file's own EPSG:3035 with a polygonal geometry, and the bounding box, expressed in EPSG:3035, keeping only the four rows inside it.
        /// <para>The fixture places two rows half a million metres away, so a filter that silently reinterpreted coordinates to another reference system would return the wrong set rather than an obviously empty one.</para>
        /// </summary>
        [Fact]
        public async Task Footprints_GeoPackageAsync_Eubucco()
        {
            string path = FixturePath("EUBUCCO_Sample.gpkg");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_GeoPackageAsync(path, BuildingDataset.Eubucco));
            Assert.Equal(6, footprints.Count);
            Assert.All(footprints, x => Assert.Equal(3035, x.SourceSrid));
            Assert.All(footprints, x => Assert.IsType<PolygonalFace2D>(x.Geometry));
            string[] sourceIds = ["3ba70f9963714924-0", "3ba70f9963714924-1", "3ba70f9963714924-2", "3ba70f9963714924-3", "3ba70f9963714924-4", "3ba70f9963714924-5"];
            Assert.Equal(sourceIds, footprints.Select(x => x.SourceId).ToList());

            List<Footprint> footprints_Near = await CollectAsync(Query.Footprints_GeoPackageAsync(path, BuildingDataset.Eubucco, new BoundingBox2D(4599990, 2999990, 100, 100)));
            Assert.Equal(4, footprints_Near.Count);
            string[] sourceIds_Near = ["3ba70f9963714924-0", "3ba70f9963714924-1", "3ba70f9963714924-2", "3ba70f9963714924-3"];
            Assert.Equal(sourceIds_Near, footprints_Near.Select(x => x.SourceId).ToList());
        }

        /// <summary>
        /// Round-trips the EUBUCCO attributes verbatim (text stays text, numbers stay numbers), derives provenance from the <c>_source</c> columns, and flags - without dropping - the two licence-exception rows: Prague is CC-BY-SA, Abruzzo is CC-BY-NC, every other row carries no exception.
        /// <para>The row count staying at six while exactly two rows are flagged is the property the issue asks for: a consumer can filter to the ODbL-only subset, but a plain read sees the whole file.</para>
        /// </summary>
        [Fact]
        public async Task Footprints_GeoPackageAsync_EubuccoAttributesAndProvenance()
        {
            string path = FixturePath("EUBUCCO_Sample.gpkg");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_GeoPackageAsync(path, BuildingDataset.Eubucco));

            Footprint footprint = footprints[0];
            Assert.Equal("3ba70f9963714924-0", footprint.SourceId);
            Assert.Equal("residential", footprint.Attributes?["type"]?.GetValue<string>());
            Assert.Equal("CZ010", footprint.Attributes?["region_id"]?.GetValue<string>());
            Assert.Equal(JsonValueKind.Number, footprint.Attributes?["height"]?.GetValueKind());
            Assert.Equal(12.3, footprint.Attributes?["height"]?.GetValue<double>());
            Assert.Equal(JsonValueKind.Number, footprint.Attributes?["floors"]?.GetValueKind());
            Assert.Equal(4L, footprint.Attributes?["floors"]?.GetValue<long>());
            Assert.Equal("gov-czechia-generic", footprint.Attributes?["geometry_source"]?.GetValue<string>());
            Assert.Equal("gov-czechia-generic", footprint.Provenance?["geometry_source"]?.GetValue<string>());
            Assert.Null(footprint.LicenceException);

            Assert.Equal("CC-BY-SA", footprints[1].LicenceException);
            Assert.Equal("gov-czechia-prague", footprints[1].Provenance?["geometry_source"]?.GetValue<string>());
            Assert.Equal("CC-BY-NC", footprints[2].LicenceException);
            Assert.Equal("gov-italy-abruzzo", footprints[2].Provenance?["geometry_source"]?.GetValue<string>());

            Assert.Equal(6, footprints.Count);
            Assert.Equal(2, footprints.Count(x => x.LicenceException != null));
            Assert.All(footprints.Where(x => x.LicenceException != null), x => Assert.Contains(x.LicenceException, new[] { "CC-BY-SA", "CC-BY-NC" }));
        }

        /// <summary>
        /// Streams the DBSM sample and pins its provenance shape: <c>source</c> always present, <c>eub_json</c> parsed to an object when it parses, kept as raw text when it does not, and absent rather than null when the column was null.
        /// </summary>
        [Fact]
        public async Task Footprints_GeoPackageAsync_Dbsm()
        {
            string path = FixturePath("DBSM_Sample.gpkg");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_GeoPackageAsync(path, BuildingDataset.Dbsm));
            Assert.Equal(3, footprints.Count);
            Assert.All(footprints, x => Assert.Equal(4326, x.SourceSrid));
            Assert.Equal("dbsm-1", footprints[0].SourceId);

            Assert.Equal("OSM+ESA", footprints[0].Provenance?["source"]?.GetValue<string>());
            Assert.Equal("EUBUCCO", footprints[0].Provenance?["eub_json"]?["src"]?.GetValue<string>());

            Assert.Equal("not-a-json", footprints[1].Provenance?["eub_json"]?.GetValue<string>());

            Assert.False(footprints[2].Provenance?.ContainsKey("eub_json"));
            Assert.Equal("ESA", footprints[2].Provenance?["source"]?.GetValue<string>());

            Assert.Equal("Praha-vychod", footprints[0].Attributes?["name"]?.GetValue<string>());
        }

        /// <summary>
        /// Streams three real DBSM R2025 rows (one per <c>source</c> value: <c>osm</c>, <c>eub</c>, <c>msb</c>) cut from <c>dbsm-poland-R2025.gpkg</c> and pins what the release looks like (DiGi-Organization/DiGi.GIS.PostgreSQL#133): EPSG:3035 multipolygons, the source identifier from <c>unique_id</c> rather than the file's <c>fid</c>, and provenance carrying <c>source</c> plus the <c>eub_data</c> / <c>osm_data</c> / <c>msb_data</c> links.
        /// </summary>
        [Fact]
        public async Task Footprints_GeoPackageAsync_DbsmR2025()
        {
            string path = FixturePath("DBSM_R2025_Sample.gpkg");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_GeoPackageAsync(path, BuildingDataset.Dbsm));
            Assert.Equal(3, footprints.Count);
            Assert.All(footprints, x => Assert.Equal(3035, x.SourceSrid));

            Assert.Equal("PL517_N34E478_Y987.9112_X649.3339", footprints[0].SourceId);
            Assert.Equal("PL517_N34E478_Y1105.6454_X531.0632", footprints[1].SourceId);
            Assert.Equal("PL517_N34E478_Y1325.7293_X295.9786", footprints[2].SourceId);

            Assert.Equal("osm", footprints[0].Provenance?["source"]?.GetValue<string>());
            Assert.Equal("eub", footprints[1].Provenance?["source"]?.GetValue<string>());
            Assert.Equal("msb", footprints[2].Provenance?["source"]?.GetValue<string>());

            Assert.Equal("height: 3.68, age: , type:", footprints[1].Provenance?["eub_data"]?.GetValue<string>());
            Assert.Equal("building: yes, levels: , roof-shape: , height: , date:", footprints[0].Provenance?["osm_data"]?.GetValue<string>());
            Assert.Equal("height:", footprints[2].Provenance?["msb_data"]?.GetValue<string>());
            Assert.False(footprints[0].Provenance?.ContainsKey("eub_json"));
        }

        /// <summary>
        /// Streams the GHS-OBAT GeoPackage centroids as points in EPSG:4326, exposes the provenance link under the single key <c>overture_id</c> even though the source column is named <c>id</c>, and filters to the three rows inside the bounding box.
        /// </summary>
        [Fact]
        public async Task Footprints_GeoPackageAsync_GhsObat()
        {
            string path = FixturePath("GHS_OBAT_Sample.gpkg");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_GeoPackageAsync(path, BuildingDataset.GhsObat));
            Assert.Equal(4, footprints.Count);
            Assert.All(footprints, x => Assert.Equal(4326, x.SourceSrid));
            Assert.All(footprints, x => Assert.IsType<Point2D>(x.Geometry));
            Assert.Equal("08f17a2c9b3e4d5f", footprints[0].SourceId);
            Assert.Equal("08f17a2c9b3e4d5f", footprints[0].Provenance?["overture_id"]?.GetValue<string>());
            Assert.Equal(JsonValueKind.Number, footprints[0].Attributes?["height"]?.GetValueKind());

            List<Footprint> footprints_Near = await CollectAsync(Query.Footprints_GeoPackageAsync(path, BuildingDataset.GhsObat, new BoundingBox2D(14.39, 50.07, 0.04, 0.02)));
            Assert.Equal(3, footprints_Near.Count);
            Assert.DoesNotContain("0899aabbccddeeff", footprints_Near.Select(x => x.SourceId));
        }

        /// <summary>
        /// Fails loudly, at enumeration start, on a GeoPackage whose geometry table declares an SRID outside the supported set, naming the file, the SRID found and the set accepted instead of reinterpreting coordinates.
        /// </summary>
        [Fact]
        public async Task Footprints_GeoPackageAsync_UnsupportedSrid()
        {
            string path = FixturePath("Unsupported_Srid_Sample.gpkg");

            NotSupportedException exception = await Assert.ThrowsAsync<NotSupportedException>(
                () => CollectAsync(Query.Footprints_GeoPackageAsync(path, BuildingDataset.Eubucco)));

            Assert.Contains("9999", exception.Message);
            Assert.Contains("4326", exception.Message);
            Assert.Contains("3035", exception.Message);
            Assert.Contains(path, exception.Message);
        }
    }
}
