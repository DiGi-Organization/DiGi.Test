using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Measures <see cref="Create.AddressPointMatchResult"/> on a county-sized input: 50 000 footprints on a grid and 50 000 address points, a third inside a footprint, a third just outside one and a third out of range.
        /// <para>A warm-up call on a small input runs first (JIT). Measured alone in Release at about 870 ms; the 2 000 ms threshold leaves room for slower machines. Reference: DiGi.GIS#10.</para>
        /// </summary>
        [Fact]
        public void AddressPointMatchResult_Performance()
        {
            const int count = 50000;
            const int columnCount = 250;
            const double size = 12;
            const double spacing = 40;
            const double maxDistance = 5;

            List<Building2D> building2Ds = [];
            List<Classes.AddressPoint> addressPoints = [];
            for (int i = 0; i < count; i++)
            {
                double x = (i % columnCount) * spacing;
                double y = (i / columnCount) * spacing;

                PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(new Point2D(x, y), new Point2D(x + size, y), new Point2D(x + size, y + size), new Point2D(x, y + size));
                Assert.NotNull(polygonalFace2D);

                building2Ds.Add(new Building2D(Guid.NewGuid(), i.ToString("D6", System.Globalization.CultureInfo.InvariantCulture), polygonalFace2D, 1, BuildingPhase.occupied, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]));

                Point2D point2D = (i % 3) switch
                {
                    0 => new Point2D(x + (size / 2), y + (size / 2)),
                    1 => new Point2D(x + size + 2, y + (size / 2)),
                    _ => new Point2D(x + size + (spacing - size) / 2, y + size + (spacing - size) / 2),
                };

                addressPoints.Add(new Classes.AddressPoint(point2D, "Testowa", "00001", i.ToString(System.Globalization.CultureInfo.InvariantCulture), "Testowo", "0000001", "00-001", null, null));
            }

            Create.AddressPointMatchResult(addressPoints.Take(100), building2Ds.Take(100), maxDistance);

            Stopwatch stopwatch = Stopwatch.StartNew();
            Classes.AddressPointMatchResult? addressPointMatchResult = Create.AddressPointMatchResult(addressPoints, building2Ds, maxDistance);
            stopwatch.Stop();

            Assert.NotNull(addressPointMatchResult);

            List<AddressPointMatch>? addressPointMatches = addressPointMatchResult.AddressPointMatches;
            Assert.NotNull(addressPointMatches);
            Assert.Equal(count, addressPointMatches.Count);
            Assert.Equal((count + 2) / 3, addressPointMatches.Count(x => x.AddressPointMatchType == AddressPointMatchType.Inside));
            Assert.Equal((count + 1) / 3, addressPointMatches.Count(x => x.AddressPointMatchType == AddressPointMatchType.Nearest));
            Assert.Equal(count / 3, addressPointMatches.Count(x => x.AddressPointMatchType == AddressPointMatchType.Unmatched));

            Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"AddressPointMatchResult took {stopwatch.ElapsedMilliseconds} ms for {count} points and {count} footprints.");
        }
    }
}
