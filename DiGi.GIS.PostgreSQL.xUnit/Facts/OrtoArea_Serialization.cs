using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the JSON round trip, the copy constructor and <c>SerializationCheck</c> of the five storage classes of the full-interpretation test areas (DiGi.GIS.PostgreSQL#129): <see cref="OrtoArea"/> carrying its sampled cells in index order, a standalone <see cref="OrtoAreaCell"/>, a polygon-carrying <see cref="OrtoAreaObject"/> with a cell assignment and one without, a dated <see cref="OrtoAreaLabel"/> and an undated one, and <see cref="OrtoAreaImage"/> with bytes and as a negative row without them.
        /// <para>The fact needs no database. The polygons are trapezoids, never rectangles, so a mirrored, reordered or axis-aligned fixture could not pass for the original. The shared geometry assertions of the storage facts are <c>AssertBoundingBox</c> and <c>AssertPolygonEqual</c>. Short test.</para>
        /// <para>The dated label is the one place where <c>SerializationCheck</c> cannot stand in: DiGi.Core's read path (the <c>JsonObject</c> constructor) drops <c>DateOnly?</c> members, so the check fails on <c>ObservationDateMin/Max</c> alone (DiGi.GIS.WebAPI.UI#81). The fact pins that behaviour explicitly - the source JSON must carry the dates, the JSON round trip must carry every other member and come back with both dates null, and the copy constructor, which assigns the fields directly and never passes through JSON, must keep them - instead of hiding the gap behind a weakened check.</para>
        /// </summary>
        [Fact]
        public void OrtoArea_Serialization()
        {
            const string areaKey = "xunit-orto-area-serialization";

            BoundingBox2D boundingBox2D = new(new Point2D(500000, 300000), new Point2D(500300, 300200));

            List<OrtoAreaCell> cells =
            [
                new OrtoAreaCell(0, new BoundingBox2D(new Point2D(500000, 300000), new Point2D(500100, 300100))),
                new OrtoAreaCell(1, new BoundingBox2D(new Point2D(500100, 300000), new Point2D(500200, 300100))),
                new OrtoAreaCell(2, new BoundingBox2D(new Point2D(500200, 300000), new Point2D(500300, 300100))),
            ];

            Polygon2D polygon2D = new([new Point2D(500010, 300010), new Point2D(500070, 300015), new Point2D(500060, 300060), new Point2D(500020, 300055)]);

            DateTimeOffset createdAt = new(2026, 3, 1, 10, 30, 0, TimeSpan.FromHours(1));
            DateTimeOffset updatedAt = new(2026, 3, 2, 8, 15, 0, TimeSpan.Zero);
            DateTimeOffset fetchedAt = new(2026, 3, 3, 4, 30, 0, TimeSpan.Zero);

            // The area with its sampled cells in index order.
            OrtoArea ortoArea = new(areaKey, boundingBox2D, "xunit area", "xunit stratum", 100.0, 3, 424242424242L, cells);
            Core.xUnit.Query.SerializationCheck(ortoArea);
            OrtoArea? ortoArea_RoundTrip = Core.Convert.ToDiGi<OrtoArea>(Core.Convert.ToSystem_String(ortoArea))?.FirstOrDefault();
            Assert.NotNull(ortoArea_RoundTrip);
            AssertArea(ortoArea, ortoArea_RoundTrip);
            AssertArea(ortoArea, new OrtoArea(ortoArea));

            // A standalone cell.
            OrtoAreaCell ortoAreaCell = new(7, cells[2].BoundingBox2D);
            Core.xUnit.Query.SerializationCheck(ortoAreaCell);
            OrtoAreaCell? ortoAreaCell_RoundTrip = Core.Convert.ToDiGi<OrtoAreaCell>(Core.Convert.ToSystem_String(ortoAreaCell))?.FirstOrDefault();
            Assert.NotNull(ortoAreaCell_RoundTrip);
            Assert.Equal(ortoAreaCell.Index, ortoAreaCell_RoundTrip.Index);
            AssertBoundingBox(ortoAreaCell.BoundingBox2D, ortoAreaCell_RoundTrip.BoundingBox2D);
            OrtoAreaCell ortoAreaCell_Copy = new(ortoAreaCell);
            Assert.Equal(ortoAreaCell.Index, ortoAreaCell_Copy.Index);
            AssertBoundingBox(ortoAreaCell.BoundingBox2D, ortoAreaCell_Copy.BoundingBox2D);

            // The drawn polygon, with and without the cell holding its centroid.
            OrtoAreaObject ortoAreaObject = new("xunit-orto-object-serialization", areaKey, 1, polygon2D, "xunit-orto@example.invalid", createdAt);
            Core.xUnit.Query.SerializationCheck(ortoAreaObject);
            OrtoAreaObject? ortoAreaObject_RoundTrip = Core.Convert.ToDiGi<OrtoAreaObject>(Core.Convert.ToSystem_String(ortoAreaObject))?.FirstOrDefault();
            Assert.NotNull(ortoAreaObject_RoundTrip);
            AssertObject(ortoAreaObject, ortoAreaObject_RoundTrip);
            AssertObject(ortoAreaObject, new OrtoAreaObject(ortoAreaObject));

            OrtoAreaObject ortoAreaObject_NoCell = new("xunit-orto-object-serialization-2", areaKey, null, polygon2D, "xunit-orto@example.invalid", createdAt);
            Core.xUnit.Query.SerializationCheck(ortoAreaObject_NoCell);
            OrtoAreaObject? ortoAreaObject_NoCell_RoundTrip = Core.Convert.ToDiGi<OrtoAreaObject>(Core.Convert.ToSystem_String(ortoAreaObject_NoCell))?.FirstOrDefault();
            Assert.NotNull(ortoAreaObject_NoCell_RoundTrip);
            Assert.Null(ortoAreaObject_NoCell_RoundTrip.CellIndex);
            AssertPolygonEqual(ortoAreaObject_NoCell.Polygon2D, ortoAreaObject_NoCell_RoundTrip.Polygon2D);

            // The label with native date observation dates (DateOnly). SerializationCheck would fail here on
            // ObservationDateMin/Max alone (the source serializes the dates, the JsonObject-constructor read leg
            // drops them - DiGi.GIS.WebAPI.UI#81), so the gap is pinned explicitly instead of hidden:
            // the source JSON carries both dates, the JSON round trip carries every other member and comes back
            // with both dates null, and the copy constructor keeps them because it never passes through JSON.
            OrtoAreaLabel ortoAreaLabel = new(areaKey, 990701, "XUNIT-REF", null, 2023, 1, new DateOnly(2023, 5, 1), new DateOnly(2023, 9, 30), "xunit-orto@example.invalid", "note", updatedAt);
            string? json_ortoAreaLabel = Core.Convert.ToSystem_String(ortoAreaLabel);
            Assert.NotNull(json_ortoAreaLabel);
            Assert.Contains("\"ObservationDateMin\":\"2023-05-01\"", json_ortoAreaLabel);
            Assert.Contains("\"ObservationDateMax\":\"2023-09-30\"", json_ortoAreaLabel);

            OrtoAreaLabel? ortoAreaLabel_RoundTrip = Core.Convert.ToDiGi<OrtoAreaLabel>(json_ortoAreaLabel)?.FirstOrDefault();
            Assert.NotNull(ortoAreaLabel_RoundTrip);
            AssertLabelExceptDates(ortoAreaLabel, ortoAreaLabel_RoundTrip);
            Assert.Null(ortoAreaLabel_RoundTrip.ObservationDateMin);
            Assert.Null(ortoAreaLabel_RoundTrip.ObservationDateMax);
            AssertLabel(ortoAreaLabel, new OrtoAreaLabel(ortoAreaLabel));

            // The undated label of a drawn object: the identity pair and the dates are all null.
            OrtoAreaLabel ortoAreaLabel_Undated = new(areaKey, null, null, "xunit-orto-object-serialization-label", 2024, 0, null, null, "xunit-orto@example.invalid", null, updatedAt);
            Core.xUnit.Query.SerializationCheck(ortoAreaLabel_Undated);
            OrtoAreaLabel? ortoAreaLabel_Undated_RoundTrip = Core.Convert.ToDiGi<OrtoAreaLabel>(Core.Convert.ToSystem_String(ortoAreaLabel_Undated))?.FirstOrDefault();
            Assert.NotNull(ortoAreaLabel_Undated_RoundTrip);
            AssertLabel(ortoAreaLabel_Undated, ortoAreaLabel_Undated_RoundTrip);

            // The materialised tile with its bytes, and the negative row without them.
            byte[] bytes = new byte[256];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(i % 251);
            }

            OrtoAreaImage ortoAreaImage = new(areaKey, boundingBox2D, 2023, 256, 256, 0, "https://xunit.invalid/orto_area/2023.png", fetchedAt, bytes);
            Core.xUnit.Query.SerializationCheck(ortoAreaImage);
            OrtoAreaImage? ortoAreaImage_RoundTrip = Core.Convert.ToDiGi<OrtoAreaImage>(Core.Convert.ToSystem_String(ortoAreaImage))?.FirstOrDefault();
            Assert.NotNull(ortoAreaImage_RoundTrip);
            AssertImage(ortoAreaImage, ortoAreaImage_RoundTrip);
            AssertImage(ortoAreaImage, new OrtoAreaImage(ortoAreaImage));

            OrtoAreaImage ortoAreaImage_Negative = new(areaKey, boundingBox2D, 2024, 0, 0, 1, null, fetchedAt, null);
            Core.xUnit.Query.SerializationCheck(ortoAreaImage_Negative);
            OrtoAreaImage? ortoAreaImage_Negative_RoundTrip = Core.Convert.ToDiGi<OrtoAreaImage>(Core.Convert.ToSystem_String(ortoAreaImage_Negative))?.FirstOrDefault();
            Assert.NotNull(ortoAreaImage_Negative_RoundTrip);
            AssertImage(ortoAreaImage_Negative, ortoAreaImage_Negative_RoundTrip);

            static void AssertArea(OrtoArea ortoArea_Expected, OrtoArea ortoArea_Actual)
            {
                Assert.Equal(ortoArea_Expected.AreaKey, ortoArea_Actual.AreaKey);
                Assert.Equal(ortoArea_Expected.Name, ortoArea_Actual.Name);
                Assert.Equal(ortoArea_Expected.Stratum, ortoArea_Actual.Stratum);
                Assert.Equal(ortoArea_Expected.CellSize, ortoArea_Actual.CellSize);
                Assert.Equal(ortoArea_Expected.CellCount, ortoArea_Actual.CellCount);
                Assert.Equal(ortoArea_Expected.Seed, ortoArea_Actual.Seed);
                AssertBoundingBox(ortoArea_Expected.BoundingBox2D, ortoArea_Actual.BoundingBox2D);
                Assert.Equal(ortoArea_Expected.Cells.Count, ortoArea_Actual.Cells.Count);
                for (int i = 0; i < ortoArea_Expected.Cells.Count; i++)
                {
                    Assert.Equal(ortoArea_Expected.Cells[i].Index, ortoArea_Actual.Cells[i].Index);
                    AssertBoundingBox(ortoArea_Expected.Cells[i].BoundingBox2D, ortoArea_Actual.Cells[i].BoundingBox2D);
                }
            }

            static void AssertObject(OrtoAreaObject ortoAreaObject_Expected, OrtoAreaObject ortoAreaObject_Actual)
            {
                Assert.Equal(ortoAreaObject_Expected.ObjectId, ortoAreaObject_Actual.ObjectId);
                Assert.Equal(ortoAreaObject_Expected.AreaKey, ortoAreaObject_Actual.AreaKey);
                Assert.Equal(ortoAreaObject_Expected.CellIndex, ortoAreaObject_Actual.CellIndex);
                Assert.Equal(ortoAreaObject_Expected.CreatedBy, ortoAreaObject_Actual.CreatedBy);
                Assert.Equal(ortoAreaObject_Expected.CreatedAt, ortoAreaObject_Actual.CreatedAt);
                AssertPolygonEqual(ortoAreaObject_Expected.Polygon2D, ortoAreaObject_Actual.Polygon2D);
            }

            static void AssertLabelExceptDates(OrtoAreaLabel ortoAreaLabel_Expected, OrtoAreaLabel ortoAreaLabel_Actual)
            {
                Assert.Equal(ortoAreaLabel_Expected.AreaKey, ortoAreaLabel_Actual.AreaKey);
                Assert.Equal(ortoAreaLabel_Expected.CountyId, ortoAreaLabel_Actual.CountyId);
                Assert.Equal(ortoAreaLabel_Expected.Reference, ortoAreaLabel_Actual.Reference);
                Assert.Equal(ortoAreaLabel_Expected.ObjectId, ortoAreaLabel_Actual.ObjectId);
                Assert.Equal(ortoAreaLabel_Expected.EpochYear, ortoAreaLabel_Actual.EpochYear);
                Assert.Equal(ortoAreaLabel_Expected.State, ortoAreaLabel_Actual.State);
                Assert.Equal(ortoAreaLabel_Expected.Interpreter, ortoAreaLabel_Actual.Interpreter);
                Assert.Equal(ortoAreaLabel_Expected.Note, ortoAreaLabel_Actual.Note);
                Assert.Equal(ortoAreaLabel_Expected.UpdatedAt, ortoAreaLabel_Actual.UpdatedAt);
            }

            static void AssertLabel(OrtoAreaLabel ortoAreaLabel_Expected, OrtoAreaLabel ortoAreaLabel_Actual)
            {
                Assert.Equal(ortoAreaLabel_Expected.AreaKey, ortoAreaLabel_Actual.AreaKey);
                Assert.Equal(ortoAreaLabel_Expected.CountyId, ortoAreaLabel_Actual.CountyId);
                Assert.Equal(ortoAreaLabel_Expected.Reference, ortoAreaLabel_Actual.Reference);
                Assert.Equal(ortoAreaLabel_Expected.ObjectId, ortoAreaLabel_Actual.ObjectId);
                Assert.Equal(ortoAreaLabel_Expected.EpochYear, ortoAreaLabel_Actual.EpochYear);
                Assert.Equal(ortoAreaLabel_Expected.State, ortoAreaLabel_Actual.State);
                Assert.Equal(ortoAreaLabel_Expected.ObservationDateMin, ortoAreaLabel_Actual.ObservationDateMin);
                Assert.Equal(ortoAreaLabel_Expected.ObservationDateMax, ortoAreaLabel_Actual.ObservationDateMax);
                Assert.Equal(ortoAreaLabel_Expected.Interpreter, ortoAreaLabel_Actual.Interpreter);
                Assert.Equal(ortoAreaLabel_Expected.Note, ortoAreaLabel_Actual.Note);
                Assert.Equal(ortoAreaLabel_Expected.UpdatedAt, ortoAreaLabel_Actual.UpdatedAt);
            }

            static void AssertImage(OrtoAreaImage ortoAreaImage_Expected, OrtoAreaImage ortoAreaImage_Actual)
            {
                Assert.Equal(ortoAreaImage_Expected.AreaKey, ortoAreaImage_Actual.AreaKey);
                AssertBoundingBox(ortoAreaImage_Expected.BoundingBox2D, ortoAreaImage_Actual.BoundingBox2D);
                Assert.Equal(ortoAreaImage_Expected.EpochYear, ortoAreaImage_Actual.EpochYear);
                Assert.Equal(ortoAreaImage_Expected.Width, ortoAreaImage_Actual.Width);
                Assert.Equal(ortoAreaImage_Expected.Height, ortoAreaImage_Actual.Height);
                Assert.Equal(ortoAreaImage_Expected.Status, ortoAreaImage_Actual.Status);
                Assert.Equal(ortoAreaImage_Expected.SourceUrl, ortoAreaImage_Actual.SourceUrl);
                Assert.Equal(ortoAreaImage_Expected.FetchedAt, ortoAreaImage_Actual.FetchedAt);
                Assert.Equal(ortoAreaImage_Expected.Bytes, ortoAreaImage_Actual.Bytes);
            }
        }

        /// <summary>
        /// Asserts that two bounding boxes carry the same minimum and maximum corners.
        /// </summary>
        /// <param name="boundingBox2D_Expected">The box that was written.</param>
        /// <param name="boundingBox2D_Actual">The box that came back.</param>
        private static void AssertBoundingBox(BoundingBox2D? boundingBox2D_Expected, BoundingBox2D? boundingBox2D_Actual)
        {
            Assert.NotNull(boundingBox2D_Expected);
            Assert.NotNull(boundingBox2D_Actual);
            Assert.Equal(boundingBox2D_Expected.Min.X, boundingBox2D_Actual.Min.X);
            Assert.Equal(boundingBox2D_Expected.Min.Y, boundingBox2D_Actual.Min.Y);
            Assert.Equal(boundingBox2D_Expected.Max.X, boundingBox2D_Actual.Max.X);
            Assert.Equal(boundingBox2D_Expected.Max.Y, boundingBox2D_Actual.Max.Y);
        }

        /// <summary>
        /// Asserts that two polygons carry the same points in the same order, walking both through their own point lists so the comparison is independent of the storage path.
        /// </summary>
        /// <param name="polygon2D_Expected">The polygon that was written.</param>
        /// <param name="polygon2D_Actual">The polygon that came back.</param>
        private static void AssertPolygonEqual(Polygon2D? polygon2D_Expected, Polygon2D? polygon2D_Actual)
        {
            Assert.NotNull(polygon2D_Expected);
            Assert.NotNull(polygon2D_Actual);

            List<Point2D>? point2Ds_Expected = polygon2D_Expected.GetPoints();
            List<Point2D>? point2Ds_Actual = polygon2D_Actual.GetPoints();
            Assert.NotNull(point2Ds_Expected);
            Assert.NotNull(point2Ds_Actual);
            Assert.Equal(point2Ds_Expected.Count, point2Ds_Actual.Count);
            for (int i = 0; i < point2Ds_Expected.Count; i++)
            {
                Assert.Equal(point2Ds_Expected[i].X, point2Ds_Actual[i].X);
                Assert.Equal(point2Ds_Expected[i].Y, point2Ds_Actual[i].Y);
            }
        }
    }
}
