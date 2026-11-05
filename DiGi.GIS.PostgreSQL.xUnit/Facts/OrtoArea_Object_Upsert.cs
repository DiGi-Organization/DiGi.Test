using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway main test database, the area and object storage of the full-interpretation test areas (DiGi.GIS.PostgreSQL#129): <see cref="OrtoAreaPostgreSQLConverter"/> stores one row per area with its sampled cells inside <c>object</c>, and <see cref="OrtoAreaObjectPostgreSQLConverter"/> stores the drawn polygons keyed by their stable identifier.
        /// <para>The area round-trips with its bounding box, name, stratum, seed and its cell list in index order with every cell geometry intact; an unknown key answers null; re-sampling replaces the row as a whole, so the cell list is replaced, never appended to. The objects round-trip with their polygons as written (trapezoids, never rectangles), a cell assignment of 0 and a null one for a centroid outside every sampled cell, and a drawing moment kept as the same instant although it arrived with a +01:00 offset; a revision replaces the row of its identifier. The read by area answers only that area's polygons - the second scratch area keeps its own - and an area with none answers an empty list. A batch with <c>batchSize: 2</c> writes three rows and skips a null entry. The fact writes scratch rows under guid-based <c>xunit-orto-area-</c> keys, removes them from all three Main tables before writing and again in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OrtoArea_Object_Upsert()
        {
            string areaKey_1 = $"xunit-orto-area-{Guid.NewGuid():N}";
            string areaKey_2 = $"xunit-orto-area-{Guid.NewGuid():N}";

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_OrtoArea());
            Assert.True(await npgsqlConnection.TableAsync_OrtoAreaObject());
            Assert.True(await npgsqlConnection.TableAsync_OrtoAreaLabel());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.OrtoAreaLabel} WHERE area_key IN ('{areaKey_1}', '{areaKey_2}');"
                + $" DELETE FROM {Constants.TableName.OrtoAreaObject} WHERE area_key IN ('{areaKey_1}', '{areaKey_2}');"
                + $" DELETE FROM {Constants.TableName.OrtoArea} WHERE area_key IN ('{areaKey_1}', '{areaKey_2}');";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                OrtoAreaPostgreSQLConverter ortoAreaPostgreSQLConverter = new(connectionData_Main);
                OrtoAreaObjectPostgreSQLConverter ortoAreaObjectPostgreSQLConverter = new(connectionData_Main);

                // The area with three sampled cells.
                BoundingBox2D boundingBox2D_1 = new(new Point2D(500000, 300000), new Point2D(500300, 300200));
                List<OrtoAreaCell> cells =
                [
                    new OrtoAreaCell(0, new BoundingBox2D(new Point2D(500000, 300000), new Point2D(500100, 300100))),
                    new OrtoAreaCell(1, new BoundingBox2D(new Point2D(500100, 300000), new Point2D(500200, 300100))),
                    new OrtoAreaCell(2, new BoundingBox2D(new Point2D(500200, 300000), new Point2D(500300, 300100))),
                ];

                OrtoArea ortoArea_1 = new(areaKey_1, boundingBox2D_1, "xunit area 1", "xunit stratum", 100.0, 3, 987654321L, cells);
                Assert.Equal(1L, await ortoAreaPostgreSQLConverter.UpdateAsync(ortoArea_1));

                OrtoArea? ortoArea_Read = await OrtoAreaPostgreSQLConverter.GetOrtoAreaAsync(npgsqlConnection, areaKey_1);
                Assert.NotNull(ortoArea_Read);
                Assert.Equal(areaKey_1, ortoArea_Read.AreaKey);
                Assert.Equal("xunit area 1", ortoArea_Read.Name);
                Assert.Equal("xunit stratum", ortoArea_Read.Stratum);
                Assert.Equal(100.0, ortoArea_Read.CellSize);
                Assert.Equal(3, ortoArea_Read.CellCount);
                Assert.Equal(987654321L, ortoArea_Read.Seed);
                AssertBoundingBox(boundingBox2D_1, ortoArea_Read.BoundingBox2D);
                Assert.Equal(3, ortoArea_Read.Cells.Count);
                Assert.Equal([0, 1, 2], ortoArea_Read.Cells.Select(x => x.Index));
                for (int i = 0; i < cells.Count; i++)
                {
                    AssertBoundingBox(cells[i].BoundingBox2D, ortoArea_Read.Cells[i].BoundingBox2D);
                }

                // A key that was never stored answers nothing.
                Assert.Null(await OrtoAreaPostgreSQLConverter.GetOrtoAreaAsync(npgsqlConnection, $"xunit-orto-area-{Guid.NewGuid():N}"));

                // The second scratch area, a different extent with no cells and no name.
                BoundingBox2D boundingBox2D_2 = new(new Point2D(600000, 400000), new Point2D(600175, 400130));
                OrtoArea ortoArea_2 = new(areaKey_2, boundingBox2D_2, null, null, 50.0, 0, 0L, []);
                Assert.Equal(1L, await ortoAreaPostgreSQLConverter.UpdateAsync(ortoArea_2));

                OrtoArea? ortoArea_2_Read = await OrtoAreaPostgreSQLConverter.GetOrtoAreaAsync(npgsqlConnection, areaKey_2);
                Assert.NotNull(ortoArea_2_Read);
                Assert.Null(ortoArea_2_Read.Name);
                Assert.Null(ortoArea_2_Read.Stratum);
                Assert.Empty(ortoArea_2_Read.Cells);
                AssertBoundingBox(boundingBox2D_2, ortoArea_2_Read.BoundingBox2D);

                // The drawn polygons: one in cell 0, one outside every cell, one under the second area.
                string objectId_1 = $"xunit-orto-object-{Guid.NewGuid():N}";
                string objectId_2 = $"xunit-orto-object-{Guid.NewGuid():N}";
                string objectId_3 = $"xunit-orto-object-{Guid.NewGuid():N}";

                Polygon2D polygon2D_1 = new([new Point2D(500010, 300010), new Point2D(500070, 300015), new Point2D(500060, 300060), new Point2D(500020, 300055)]);
                Polygon2D polygon2D_2 = new([new Point2D(500210, 300010), new Point2D(500250, 300020), new Point2D(500195, 300075)]);
                Polygon2D polygon2D_3 = new([new Point2D(600010, 400010), new Point2D(600080, 400018), new Point2D(600055, 400070), new Point2D(600025, 400045)]);

                DateTimeOffset createdAt = new(2026, 2, 1, 12, 30, 0, TimeSpan.FromHours(1));

                OrtoAreaObject ortoAreaObject_1 = new(objectId_1, areaKey_1, 0, polygon2D_1, "xunit-orto-a@example.invalid", createdAt);
                OrtoAreaObject ortoAreaObject_2 = new(objectId_2, areaKey_1, null, polygon2D_2, "xunit-orto-a@example.invalid", createdAt.AddHours(2));
                OrtoAreaObject ortoAreaObject_3 = new(objectId_3, areaKey_2, 0, polygon2D_3, "xunit-orto-b@example.invalid", createdAt.AddHours(4));

                Assert.Equal(3L, await ortoAreaObjectPostgreSQLConverter.UpdateAsync([ortoAreaObject_1, null, ortoAreaObject_2, ortoAreaObject_3], batchSize: 2));

                List<OrtoAreaObject>? ortoAreaObjects = await OrtoAreaObjectPostgreSQLConverter.GetOrtoAreaObjectsAsync(npgsqlConnection, areaKey_1);
                Assert.NotNull(ortoAreaObjects);
                Assert.Equal(2, ortoAreaObjects.Count);

                OrtoAreaObject? ortoAreaObject_Read_1 = ortoAreaObjects.FirstOrDefault(x => x.ObjectId == objectId_1);
                Assert.NotNull(ortoAreaObject_Read_1);
                Assert.Equal(areaKey_1, ortoAreaObject_Read_1.AreaKey);
                Assert.Equal(0, ortoAreaObject_Read_1.CellIndex);
                Assert.Equal("xunit-orto-a@example.invalid", ortoAreaObject_Read_1.CreatedBy);
                Assert.Equal(createdAt, ortoAreaObject_Read_1.CreatedAt);
                AssertPolygonEqual(polygon2D_1, ortoAreaObject_Read_1.Polygon2D);

                OrtoAreaObject? ortoAreaObject_Read_2 = ortoAreaObjects.FirstOrDefault(x => x.ObjectId == objectId_2);
                Assert.NotNull(ortoAreaObject_Read_2);
                Assert.Equal(areaKey_1, ortoAreaObject_Read_2.AreaKey);
                Assert.Null(ortoAreaObject_Read_2.CellIndex);
                Assert.Equal(createdAt.AddHours(2), ortoAreaObject_Read_2.CreatedAt);
                AssertPolygonEqual(polygon2D_2, ortoAreaObject_Read_2.Polygon2D);

                // The read by area answers only its own area: the second area's polygon stays out.
                List<OrtoAreaObject>? ortoAreaObjects_2 = await OrtoAreaObjectPostgreSQLConverter.GetOrtoAreaObjectsAsync(npgsqlConnection, areaKey_2);
                Assert.NotNull(ortoAreaObjects_2);
                OrtoAreaObject ortoAreaObject_Read_3 = Assert.Single(ortoAreaObjects_2);
                Assert.Equal(objectId_3, ortoAreaObject_Read_3.ObjectId);
                Assert.Equal(areaKey_2, ortoAreaObject_Read_3.AreaKey);
                AssertPolygonEqual(polygon2D_3, ortoAreaObject_Read_3.Polygon2D);
                Assert.DoesNotContain(ortoAreaObjects, x => x.ObjectId == objectId_3);

                List<OrtoAreaObject>? ortoAreaObjects_None = await OrtoAreaObjectPostgreSQLConverter.GetOrtoAreaObjectsAsync(npgsqlConnection, $"xunit-orto-area-{Guid.NewGuid():N}");
                Assert.NotNull(ortoAreaObjects_None);
                Assert.Empty(ortoAreaObjects_None);

                // A revision replaces the row of its stable identifier: the cell assignment moves outside every cell.
                OrtoAreaObject ortoAreaObject_1_Revision = new(objectId_1, areaKey_1, null, polygon2D_1, "xunit-orto-c@example.invalid", createdAt.AddHours(6));
                Assert.Equal(1L, await ortoAreaObjectPostgreSQLConverter.UpdateAsync([ortoAreaObject_1_Revision]));

                ortoAreaObjects = await OrtoAreaObjectPostgreSQLConverter.GetOrtoAreaObjectsAsync(npgsqlConnection, areaKey_1);
                Assert.NotNull(ortoAreaObjects);
                Assert.Equal(2, ortoAreaObjects.Count);
                ortoAreaObject_Read_1 = ortoAreaObjects.FirstOrDefault(x => x.ObjectId == objectId_1);
                Assert.NotNull(ortoAreaObject_Read_1);
                Assert.Null(ortoAreaObject_Read_1.CellIndex);
                Assert.Equal("xunit-orto-c@example.invalid", ortoAreaObject_Read_1.CreatedBy);
                Assert.Equal(createdAt.AddHours(6), ortoAreaObject_Read_1.CreatedAt);
                AssertPolygonEqual(polygon2D_1, ortoAreaObject_Read_1.Polygon2D);

                // The row counts straight from the tables.
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoAreaObject} WHERE area_key = '{areaKey_1}';"));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoAreaObject} WHERE area_key = '{areaKey_2}';"));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoArea} WHERE area_key IN ('{areaKey_1}', '{areaKey_2}');"));

                // Null arguments answer null.
                Assert.Null(await OrtoAreaObjectPostgreSQLConverter.GetOrtoAreaObjectsAsync(npgsqlConnection, null));
                Assert.Null(await OrtoAreaObjectPostgreSQLConverter.UpdateAsync(npgsqlConnection, null));

                // Re-sampling replaces the area row as a whole: one cell left, and the row count did not move.
                OrtoArea ortoArea_1_Resampled = new(areaKey_1, boundingBox2D_1, "xunit area 1 resampled", null, 300.0, 1, 111L, [cells[0]]);
                Assert.Equal(1L, await ortoAreaPostgreSQLConverter.UpdateAsync(ortoArea_1_Resampled));

                ortoArea_Read = await OrtoAreaPostgreSQLConverter.GetOrtoAreaAsync(npgsqlConnection, areaKey_1);
                Assert.NotNull(ortoArea_Read);
                Assert.Equal("xunit area 1 resampled", ortoArea_Read.Name);
                Assert.Null(ortoArea_Read.Stratum);
                Assert.Equal(300.0, ortoArea_Read.CellSize);
                Assert.Equal(1, ortoArea_Read.CellCount);
                Assert.Equal(111L, ortoArea_Read.Seed);
                Assert.Single(ortoArea_Read.Cells);
                Assert.Equal([0], ortoArea_Read.Cells.Select(x => x.Index));
                AssertBoundingBox(cells[0].BoundingBox2D, ortoArea_Read.Cells[0].BoundingBox2D);
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoArea} WHERE area_key IN ('{areaKey_1}', '{areaKey_2}');"));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
