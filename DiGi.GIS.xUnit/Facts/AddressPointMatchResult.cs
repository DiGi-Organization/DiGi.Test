using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Create.AddressPointMatchResult"/> on synthetic footprints: a point inside, a point on the boundary, a point in a courtyard hole, points just inside and just outside the maximum distance, a point equidistant from two footprints, a point inside two overlapping footprints, a point without a location, and a building receiving several points.
        /// <para>The tie cases are run again with the buildings in reverse order and must pick the same building. Reference: DiGi.GIS#10.</para>
        /// </summary>
        [Fact]
        public void AddressPointMatchResult()
        {
            const double maxDistance = 5;

            Building2D Building2D(string reference, double x, double y, double width, double height, IPolygonal2D? polygonal2D_Hole = null)
            {
                Polygon2D polygon2D = new([new Point2D(x, y), new Point2D(x + width, y), new Point2D(x + width, y + height), new Point2D(x, y + height)]);
                PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D, polygonal2D_Hole == null ? null : [polygonal2D_Hole]);
                Assert.NotNull(polygonalFace2D);

                return new Building2D(Guid.NewGuid(), reference, polygonalFace2D, 1, BuildingPhase.occupied, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]);
            }

            Classes.AddressPoint AddressPoint(string referenceId, Point2D? point2D)
            {
                return new Classes.AddressPoint(point2D, "Testowa", "00001", "1", "Testowo", "0000001", "00-001", referenceId, null);
            }

            Polygon2D polygon2D_Courtyard = new([new Point2D(110, 10), new Point2D(130, 10), new Point2D(130, 30), new Point2D(110, 30)]);

            List<Building2D?> building2Ds =
            [
                Building2D("A", 0, 0, 10, 10),
                Building2D("C", 100, 0, 40, 40, polygon2D_Courtyard),
                Building2D("D2", 200, 0, 10, 10),
                Building2D("D1", 216, 0, 10, 10),
                Building2D("F2", 300, 0, 10, 10),
                Building2D("F1", 305, 0, 10, 10),
                null,
            ];

            List<Classes.AddressPoint?> addressPoints =
            [
                AddressPoint("inside", new Point2D(5, 5)),
                AddressPoint("boundary", new Point2D(10 + (Core.Constants.Tolerance.Distance / 2), 5)),
                AddressPoint("nearest_InRange", new Point2D(10 + maxDistance - 1e-3, 5)),
                AddressPoint("nearest_OutOfRange", new Point2D(10 + maxDistance + 1e-3, 5)),
                AddressPoint("courtyard", new Point2D(112, 20)),
                AddressPoint("equidistant", new Point2D(213, 5)),
                AddressPoint("overlap", new Point2D(307, 5)),
                AddressPoint("noLocation", null),
                null,
                AddressPoint("unmatched", new Point2D(1000, 1000)),
            ];

            Classes.AddressPointMatchResult? addressPointMatchResult = Create.AddressPointMatchResult(addressPoints, building2Ds, maxDistance);
            Assert.NotNull(addressPointMatchResult);
            Assert.Equal(maxDistance, addressPointMatchResult.MaxDistance);
            Assert.Equal(Core.Constants.Tolerance.Distance, addressPointMatchResult.Tolerance);

            List<AddressPointMatch>? addressPointMatches = addressPointMatchResult.AddressPointMatches;
            Assert.NotNull(addressPointMatches);

            // One match per non-null point, in input order; the point without a location is reported, not dropped.
            Assert.Equal(9, addressPointMatches.Count);
            Assert.Equal(["inside", "boundary", "nearest_InRange", "nearest_OutOfRange", "courtyard", "equidistant", "overlap", "noLocation", "unmatched"], addressPointMatches.Select(x => x.AddressPoint?.ReferenceId));

            Dictionary<string, AddressPointMatch> dictionary = addressPointMatches.ToDictionary(x => x.AddressPoint!.ReferenceId!);

            Assert.Equal(AddressPointMatchType.Inside, dictionary["inside"].AddressPointMatchType);
            Assert.Equal("A", dictionary["inside"].Reference);
            Assert.Equal(0, dictionary["inside"].Distance);

            Assert.Equal(AddressPointMatchType.Inside, dictionary["boundary"].AddressPointMatchType);
            Assert.Equal("A", dictionary["boundary"].Reference);

            // Both sides of maxDistance.
            Assert.Equal(AddressPointMatchType.Nearest, dictionary["nearest_InRange"].AddressPointMatchType);
            Assert.Equal("A", dictionary["nearest_InRange"].Reference);
            Assert.NotNull(dictionary["nearest_InRange"].Distance);
            Assert.Equal(maxDistance - 1e-3, dictionary["nearest_InRange"].Distance!.Value, 6);

            Assert.Equal(AddressPointMatchType.Unmatched, dictionary["nearest_OutOfRange"].AddressPointMatchType);
            Assert.Null(dictionary["nearest_OutOfRange"].Reference);
            Assert.Null(dictionary["nearest_OutOfRange"].Guid);
            Assert.Null(dictionary["nearest_OutOfRange"].Distance);

            // A courtyard is not inside the footprint; the distance is to the courtyard ring.
            Assert.Equal(AddressPointMatchType.Nearest, dictionary["courtyard"].AddressPointMatchType);
            Assert.Equal("C", dictionary["courtyard"].Reference);
            Assert.Equal(2, dictionary["courtyard"].Distance!.Value, 6);

            // Ties go to the ordinally smallest reference.
            Assert.Equal(AddressPointMatchType.Nearest, dictionary["equidistant"].AddressPointMatchType);
            Assert.Equal("D1", dictionary["equidistant"].Reference);
            Assert.Equal(3, dictionary["equidistant"].Distance!.Value, 6);

            Assert.Equal(AddressPointMatchType.Inside, dictionary["overlap"].AddressPointMatchType);
            Assert.Equal("F1", dictionary["overlap"].Reference);

            Assert.Equal(AddressPointMatchType.Unmatched, dictionary["noLocation"].AddressPointMatchType);
            Assert.Equal(AddressPointMatchType.Unmatched, dictionary["unmatched"].AddressPointMatchType);

            // A building receives several points.
            Assert.Equal(3, addressPointMatches.Count(x => x.Reference == "A"));

            // The same ties are resolved the same way whatever the building order.
            List<Building2D?> building2Ds_Reversed = [.. building2Ds];
            building2Ds_Reversed.Reverse();

            List<AddressPointMatch>? addressPointMatches_Reversed = Create.AddressPointMatchResult(addressPoints, building2Ds_Reversed, maxDistance)?.AddressPointMatches;
            Assert.NotNull(addressPointMatches_Reversed);
            Assert.Equal(addressPointMatches.Select(x => x.Reference), addressPointMatches_Reversed.Select(x => x.Reference));
            Assert.Equal(addressPointMatches.Select(x => x.AddressPointMatchType), addressPointMatches_Reversed.Select(x => x.AddressPointMatchType));

            // Invalid input.
            Assert.Null(Create.AddressPointMatchResult(null, building2Ds, maxDistance));
            Assert.Null(Create.AddressPointMatchResult(addressPoints, building2Ds, -1));
            Assert.Null(Create.AddressPointMatchResult(addressPoints, building2Ds, double.NaN));
            Assert.All(Create.AddressPointMatchResult(addressPoints, null, maxDistance)?.AddressPointMatches ?? [], x => Assert.Equal(AddressPointMatchType.Unmatched, x.AddressPointMatchType));

            // Serialization.
            string? json = Core.Convert.ToSystem_String(addressPointMatchResult);
            Assert.False(string.IsNullOrWhiteSpace(json));

            Classes.AddressPointMatchResult? addressPointMatchResult_Json = Core.Convert.ToDiGi<Classes.AddressPointMatchResult>(json)?.FirstOrDefault();
            Assert.NotNull(addressPointMatchResult_Json);
            Assert.Equal(maxDistance, addressPointMatchResult_Json.MaxDistance);
            Assert.Equal(addressPointMatches.Select(x => x.Reference), addressPointMatchResult_Json.AddressPointMatches?.Select(x => x.Reference));
            Assert.Equal(addressPointMatches.Select(x => x.Distance), addressPointMatchResult_Json.AddressPointMatches?.Select(x => x.Distance));

            Core.xUnit.Query.SerializationCheck(addressPointMatchResult);

            Classes.AddressPointMatchResult addressPointMatchResult_Copy = new(addressPointMatchResult);
            Assert.Equal(addressPointMatchResult.MaxDistance, addressPointMatchResult_Copy.MaxDistance);
            Assert.Equal(addressPointMatchResult.Tolerance, addressPointMatchResult_Copy.Tolerance);
            Assert.Equal(addressPointMatches.Select(x => x.Guid), addressPointMatchResult_Copy.AddressPointMatches?.Select(x => x.Guid));
        }
    }
}
