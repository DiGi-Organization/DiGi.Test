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
        /// Streams the GHS-OBAT CSV attribute table as point footprints in EPSG:4326 and pins the properties that a numeric inference would break: every cell stays the published text (an all-digit identifier keeps its leading zero and JSON string kind), the quoted comma inside a region name survives one column, and the bounding box drops only the far row.
        /// </summary>
        [Fact]
        public async Task Footprints_CsvAsync_GhsObat()
        {
            string path = FixturePath("GHS_OBAT_Sample.csv");

            List<Footprint> footprints = await CollectAsync(Query.Footprints_CsvAsync(path, BuildingDataset.GhsObat));
            Assert.Equal(4, footprints.Count);
            Assert.All(footprints, x => Assert.Equal(4326, x.SourceSrid));
            Assert.All(footprints, x => Assert.IsType<Point2D>(x.Geometry));

            Assert.Equal("08f17a2c9b3e4d5f", footprints[0].SourceId);
            Assert.Equal("Praha, hlavni mesto", footprints[0].Attributes?["adm1"]?.GetValue<string>());
            Assert.Equal("12.5", footprints[0].Attributes?["height"]?.GetValue<string>());
            Assert.Equal("08f17a2c9b3e4d5f", footprints[0].Provenance?["overture_id"]?.GetValue<string>());

            // All digits, leading zero: an inferred number would lose both.
            Assert.Equal("8877665544332211", footprints[1].SourceId);
            Assert.Equal(JsonValueKind.String, footprints[1].Attributes?["id"]?.GetValueKind());

            Assert.Equal("2020", footprints[2].Attributes?["epoch"]?.GetValue<string>());

            List<Footprint> footprints_Near = await CollectAsync(Query.Footprints_CsvAsync(path, BuildingDataset.GhsObat, new BoundingBox2D(14.39, 50.07, 0.04, 0.02)));
            Assert.Equal(3, footprints_Near.Count);
            Assert.Equal(["08f17a2c9b3e4d5f", "8877665544332211", "0123456789abcdef"], footprints_Near.Select(x => x.SourceId).ToList());
        }
    }
}
