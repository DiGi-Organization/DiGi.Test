using DiGi.GIS.IO.Classes;
using DiGi.GIS.IO.Enums;
using DiGi.Geometry.Planar.Classes;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.IO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Round-trips a <see cref="Footprint"/> through the DiGi serializer and back, pinning the properties the readers depend on: the attribute and provenance bags keep their raw JSON shape (numbers stay JSON numbers, a nested sources array stays an array of objects, an explicit null key survives), the interface-typed geometry comes back as the concrete point it was, the licence exception travels, and a clone keeps all of it.
        /// <para>This is the fact that would fail if the bags were ever stored as plain <c>JsonObject</c> members: the serializer writes those as Key/Value pair arrays and reads them back empty, which would silently empty every attribute on the first round trip.</para>
        /// </summary>
        [Fact]
        public void Footprint_SerializationRoundTrip()
        {
            JsonObject attributes = new()
            {
                ["id"] = "abc-123",
                ["height"] = 12.5,
                ["floors"] = 4,
                ["type"] = "residential",
                ["note"] = null,
                ["sources"] = new JsonArray(
                    new JsonObject { ["dataset"] = "osm", ["id"] = "osm-1" },
                    new JsonObject { ["dataset"] = "esri", ["id"] = "esri-1" })
            };

            JsonObject provenance = new()
            {
                ["geometry_source"] = "gov-czechia-prague"
            };

            Footprint footprint = new("abc-123", 3035, new Point2D(14.42, 50.08), attributes, provenance, "CC-BY-SA");

            string? json = DiGi.Core.Convert.ToSystem_String(footprint);
            Assert.NotNull(json);

            List<Footprint>? roundTrips = DiGi.Core.Convert.ToDiGi<Footprint>(json);
            Assert.NotNull(roundTrips);
            Assert.NotEmpty(roundTrips!);
            Footprint roundTrip = roundTrips[0];

            Assert.Equal("abc-123", roundTrip.SourceId);
            Assert.Equal(3035, roundTrip.SourceSrid);
            Assert.Equal("CC-BY-SA", roundTrip.LicenceException);

            Point2D? point2D = roundTrip.Geometry as Point2D;
            Assert.NotNull(point2D);
            Assert.Equal(14.42, point2D!.X);
            Assert.Equal(50.08, point2D.Y);

            Assert.Equal(JsonValueKind.Number, roundTrip.Attributes?["height"]?.GetValueKind());
            Assert.Equal(12.5, roundTrip.Attributes?["height"]?.GetValue<double>());
            Assert.Equal(4L, roundTrip.Attributes?["floors"]?.GetValue<long>());
            Assert.Equal("residential", roundTrip.Attributes?["type"]?.GetValue<string>());
            Assert.True(roundTrip.Attributes?.ContainsKey("note"));
            Assert.True(roundTrip.Attributes?["sources"] is JsonArray sources && sources.Count == 2, "nested sources array");
            Assert.Equal("osm", roundTrip.Attributes?["sources"]?[0]?["dataset"]?.GetValue<string>());

            Assert.Equal("gov-czechia-prague", roundTrip.Provenance?["geometry_source"]?.GetValue<string>());

            Footprint? cloned = roundTrip.Clone() as Footprint;
            Assert.NotNull(cloned);
            Assert.Equal("abc-123", cloned!.SourceId);
            Assert.Equal(3035, cloned.SourceSrid);
            Assert.Equal("CC-BY-SA", cloned.LicenceException);
            Assert.Equal(JsonValueKind.Number, cloned.Attributes?["height"]?.GetValueKind());
            Assert.True(cloned.Geometry is Point2D);

            // A footprint without attributes, provenance, geometry or an identifier survives the same trip as an all-null object.
            Footprint empty = new(null, 4326, null, null, null, null);
            string? emptyJson = DiGi.Core.Convert.ToSystem_String(empty);
            List<Footprint>? emptyRoundTrips = DiGi.Core.Convert.ToDiGi<Footprint>(emptyJson);
            Assert.NotNull(emptyRoundTrips);
            Assert.NotEmpty(emptyRoundTrips!);
            Footprint emptyRoundTrip = emptyRoundTrips[0];
            Assert.Null(emptyRoundTrip.SourceId);
            Assert.Equal(4326, emptyRoundTrip.SourceSrid);
            Assert.Null(emptyRoundTrip.Geometry);
            Assert.Null(emptyRoundTrip.Attributes);
            Assert.Null(emptyRoundTrip.Provenance);
            Assert.Null(emptyRoundTrip.LicenceException);
        }

        /// <summary>
        /// Pins the reader guards that run before any file is opened: a null or blank path returns null for every reader, a dataset that belongs to another format fails immediately with the offending value named, and a Microsoft read without a quadkey refuses to synthesize identifiers.
        /// <para>The dataset checks are eager rather than deferred to enumeration so a mistyped combination cannot open a file at all.</para>
        /// </summary>
        [Fact]
        public void Footprints_Readers_Guards()
        {
            Assert.Null(Query.Footprints_GeoPackageAsync(null, BuildingDataset.Eubucco));
            Assert.Null(Query.Footprints_GeoPackageAsync(" ", BuildingDataset.Eubucco));
            Assert.Null(Query.Footprints_GeoParquetAsync(null, BuildingDataset.Overture));
            Assert.Null(Query.Footprints_CsvAsync(null, BuildingDataset.GhsObat));
            Assert.Null(Query.Footprints_GeoJsonSequenceAsync(null, BuildingDataset.Microsoft, "0320101"));

            ArgumentException exception_Parquet = Assert.Throws<ArgumentException>(() => Query.Footprints_GeoParquetAsync("sample.parquet", BuildingDataset.Eubucco));
            Assert.Contains("Eubucco", exception_Parquet.Message);

            ArgumentException exception_Csv = Assert.Throws<ArgumentException>(() => Query.Footprints_CsvAsync("sample.csv", BuildingDataset.Overture));
            Assert.Contains("GhsObat", exception_Csv.Message);

            ArgumentException exception_GeoJson = Assert.Throws<ArgumentException>(() => Query.Footprints_GeoJsonSequenceAsync("sample.gz", BuildingDataset.GhsObat, "0320101"));
            Assert.Contains("Microsoft", exception_GeoJson.Message);

            ArgumentException exception_GeoPackage = Assert.Throws<ArgumentException>(() => Query.Footprints_GeoPackageAsync("sample.gpkg", BuildingDataset.Overture));
            Assert.Contains("Overture", exception_GeoPackage.Message);

            ArgumentException exception_Quadkey = Assert.Throws<ArgumentException>(() => Query.Footprints_GeoJsonSequenceAsync("sample.gz", BuildingDataset.Microsoft, " "));
            Assert.Contains("quadkey", exception_Quadkey.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
