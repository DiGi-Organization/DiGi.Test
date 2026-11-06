using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway main test database, the removal rules for polygons drawn in full-interpretation test areas (<see cref="OrtoAreaObjectPostgreSQLConverter.RemoveAsync(NpgsqlConnection?, string?, string?, string?, int, System.Threading.CancellationToken)"/>, DiGi.GIS.WebAPI.UI#85).
        /// <para>Only the interpreter who drew a polygon may remove it, and only while no other interpreter has labelled it. A polygon drawn by A with A's own labels on two epochs is removed by A, compared case-insensitively, and A's labels go with it; a second removal answers NotFound. B cannot remove A's polygon (NotCreator), and nothing is touched. A polygon labelled by both A and B is refused for A (LabelledByOthers), and the whole transaction rolls back, so A's own label stays as well. A polygon of another area under the same identifier is not found from the wrong area. Null or blank arguments answer null.</para>
        /// <para>The fact writes scratch rows under guid-based <c>xunit-orto-area-</c> keys, removes them from all three Main tables before writing and again in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OrtoArea_Object_Remove()
        {
            string areaKey = $"xunit-orto-area-{Guid.NewGuid():N}";
            string areaKey_Other = $"xunit-orto-area-{Guid.NewGuid():N}";
            const string interpreter_A = "xunit-orto-a@example.invalid";
            const string interpreter_B = "xunit-orto-b@example.invalid";

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_OrtoArea());
            Assert.True(await npgsqlConnection.TableAsync_OrtoAreaObject());
            Assert.True(await npgsqlConnection.TableAsync_OrtoAreaLabel());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.OrtoAreaLabel} WHERE area_key IN ('{areaKey}', '{areaKey_Other}');"
                + $" DELETE FROM {Constants.TableName.OrtoAreaObject} WHERE area_key IN ('{areaKey}', '{areaKey_Other}');"
                + $" DELETE FROM {Constants.TableName.OrtoArea} WHERE area_key IN ('{areaKey}', '{areaKey_Other}');";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                OrtoAreaObjectPostgreSQLConverter ortoAreaObjectPostgreSQLConverter = new(connectionData_Main);
                OrtoAreaLabelPostgreSQLConverter ortoAreaLabelPostgreSQLConverter = new(connectionData_Main);

                Polygon2D polygon2D = new([new Point2D(500010, 300010), new Point2D(500070, 300015), new Point2D(500060, 300060)]);
                DateTimeOffset now = DateTimeOffset.UtcNow;

                string objectId_Own = $"xunit-orto-object-{Guid.NewGuid():N}";
                string objectId_Shared = $"xunit-orto-object-{Guid.NewGuid():N}";
                string objectId_Other = $"xunit-orto-object-{Guid.NewGuid():N}";

                Assert.Equal(3L, await ortoAreaObjectPostgreSQLConverter.UpdateAsync(
                [
                    new OrtoAreaObject(objectId_Own, areaKey, 0, polygon2D, interpreter_A, now),
                    new OrtoAreaObject(objectId_Shared, areaKey, 0, polygon2D, interpreter_A, now),
                    new OrtoAreaObject(objectId_Other, areaKey_Other, 0, polygon2D, interpreter_A, now),
                ]));

                Assert.Equal(4L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync(
                [
                    new OrtoAreaLabel(areaKey, null, null, objectId_Own, 2001, 0, null, null, interpreter_A, null, now),
                    new OrtoAreaLabel(areaKey, null, null, objectId_Own, 2005, 1, null, null, interpreter_A, null, now),
                    new OrtoAreaLabel(areaKey, null, null, objectId_Shared, 2001, 0, null, null, interpreter_A, null, now),
                    new OrtoAreaLabel(areaKey, null, null, objectId_Shared, 2001, 2, null, null, interpreter_B, null, now),
                ]));

                string CountObjects(string objectId) => $"SELECT count(*) FROM {Constants.TableName.OrtoAreaObject} WHERE object_id = '{objectId}';";
                string CountLabels(string objectId) => $"SELECT count(*) FROM {Constants.TableName.OrtoAreaLabel} WHERE area_key = '{areaKey}' AND object_id = '{objectId}';";

                // B did not draw it: refused, nothing touched.
                Assert.Equal(OrtoAreaObjectRemoval.NotCreator, await ortoAreaObjectPostgreSQLConverter.RemoveAsync(areaKey, objectId_Own, interpreter_B));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, CountObjects(objectId_Own)));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, CountLabels(objectId_Own)));

                // B has labelled the shared polygon: refused for A, and A's own label stays too (rolled back).
                Assert.Equal(OrtoAreaObjectRemoval.LabelledByOthers, await ortoAreaObjectPostgreSQLConverter.RemoveAsync(areaKey, objectId_Shared, interpreter_A));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, CountObjects(objectId_Shared)));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, CountLabels(objectId_Shared)));

                // The wrong area does not find a polygon of another area.
                Assert.Equal(OrtoAreaObjectRemoval.NotFound, await ortoAreaObjectPostgreSQLConverter.RemoveAsync(areaKey, objectId_Other, interpreter_A));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, CountObjects(objectId_Other)));

                // A removes A's own polygon, the interpreter compared case-insensitively; A's labels go with it.
                Assert.Equal(OrtoAreaObjectRemoval.Removed, await ortoAreaObjectPostgreSQLConverter.RemoveAsync(areaKey, objectId_Own, " XUNIT-ORTO-A@example.invalid "));
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection, CountObjects(objectId_Own)));
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection, CountLabels(objectId_Own)));

                // The shared polygon and its labels are untouched by the removal beside it.
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, CountObjects(objectId_Shared)));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, CountLabels(objectId_Shared)));

                // Removed already.
                Assert.Equal(OrtoAreaObjectRemoval.NotFound, await ortoAreaObjectPostgreSQLConverter.RemoveAsync(areaKey, objectId_Own, interpreter_A));

                // Null or blank arguments.
                Assert.Null(await OrtoAreaObjectPostgreSQLConverter.RemoveAsync(npgsqlConnection, null, objectId_Shared, interpreter_A));
                Assert.Null(await OrtoAreaObjectPostgreSQLConverter.RemoveAsync(npgsqlConnection, areaKey, " ", interpreter_A));
                Assert.Null(await OrtoAreaObjectPostgreSQLConverter.RemoveAsync(npgsqlConnection, areaKey, objectId_Shared, null));
                Assert.Null(await OrtoAreaObjectPostgreSQLConverter.RemoveAsync(null, areaKey, objectId_Shared, interpreter_A));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
