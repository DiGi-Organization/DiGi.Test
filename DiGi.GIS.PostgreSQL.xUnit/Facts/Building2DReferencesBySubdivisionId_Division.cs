using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The X coordinate of the lower left corner of the scratch county of the <see cref="Building2DReferencesBySubdivisionId_Division"/> fact; far from every other scratch area, so no other county part reaches its box.
        /// </summary>
        private const double subdivisionDivision_X = -7890000;

        /// <summary>
        /// The Y coordinate of the lower left corner of the scratch county of the <see cref="Building2DReferencesBySubdivisionId_Division"/> fact.
        /// </summary>
        private const double subdivisionDivision_Y = -7890000;

        /// <summary>
        /// The reference prefix of every scratch row the <see cref="Building2DReferencesBySubdivisionId_Division"/> fact writes.
        /// </summary>
        private const string subdivisionDivision_ReferencePrefix = "XUNIT-SUBDIVISION-DIVISION-";

        /// <summary>
        /// Verifies, on the throwaway main test database, that <see cref="Building2DPostgreSQLConverter.GetBuilding2DReferencesBySubdivisionIdAsync(int, int, System.Threading.CancellationToken)"/> resolves a Subdivision row by its polygon whatever area object it stores (DiGi-Organization/DiGi.GIS.WebAPI#89).
        /// <para>Warsaw's districts are stored under the Subdivision type as an <see cref="GIS.Classes.AdministrativeDivision"/> of <see cref="GIS.Enums.AdministrativeDivisionType.district_or_delegation"/>, not as an <see cref="GIS.Classes.AdministrativeSubdivision"/>. A scratch county holds such an outer district with a nested <see cref="GIS.Classes.AdministrativeSubdivision"/> inside it, two buildings in the inner one, one in the outer one only and one outside both. The outer district must answer its three buildings - the same set <see cref="Building2DPostgreSQLConverter.GetBuilding2DReferencesByAdministrativeAreal2DIdsAsync(IEnumerable{int}, int, System.Threading.CancellationToken)"/> answers - the inner one its two, and a Municipality row over the same polygon nothing, because the row type, not the stored class, decides what is a Subdivision.</para>
        /// <para>Red on the unmodified code, where the polygon was taken only from an <see cref="GIS.Classes.AdministrativeSubdivision"/> and the district answered an empty list - <c>countbyadministrativeareal2did</c> then answered 204. Every scratch row is removed before the write and in <c>finally</c>.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task Building2DReferencesBySubdivisionId_Division()
        {
            string code = "9089";

            string reference_County = $"{subdivisionDivision_ReferencePrefix}COUNTY";
            string reference_Outer = $"{subdivisionDivision_ReferencePrefix}OUTER";
            string reference_Inner = $"{subdivisionDivision_ReferencePrefix}INNER";
            string reference_Municipality = $"{subdivisionDivision_ReferencePrefix}MUNICIPALITY";

            string reference_Inner_1 = $"{subdivisionDivision_ReferencePrefix}B-INNER-1";
            string reference_Inner_2 = $"{subdivisionDivision_ReferencePrefix}B-INNER-2";
            string reference_Outer_1 = $"{subdivisionDivision_ReferencePrefix}B-OUTER-1";
            string reference_Outside_1 = $"{subdivisionDivision_ReferencePrefix}B-OUTSIDE-1";

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_AdministrativeArea2D());
            Assert.True(await npgsqlConnection.TableAsync_Building2D());

            await SubdivisionDivision_CleanupAsync(npgsqlConnection);

            int? countyId = null;
            try
            {
                countyId = await SubdivisionDivision_InsertAsync(npgsqlConnection, reference_County, code, AdministrativeArealType.County, new GIS.Classes.AdministrativeDivision(Guid.NewGuid(), reference_County, code, SubdivisionDivision_Square(0, 0, 1000), GIS.Enums.AdministrativeDivisionType.county, "county"), 0, 0, 1000);

                int outerId = await SubdivisionDivision_InsertAsync(npgsqlConnection, reference_Outer, $"{code}028", AdministrativeArealType.Subdivision, new GIS.Classes.AdministrativeDivision(Guid.NewGuid(), reference_Outer, $"{code}028", SubdivisionDivision_Square(100, 100, 600), GIS.Enums.AdministrativeDivisionType.district_or_delegation, "outer district"), 100, 100, 600);
                int innerId = await SubdivisionDivision_InsertAsync(npgsqlConnection, reference_Inner, $"{code}011", AdministrativeArealType.Subdivision, new GIS.Classes.AdministrativeSubdivision(Guid.NewGuid(), reference_Inner, $"{code}011", SubdivisionDivision_Square(200, 200, 200), GIS.Enums.AdministrativeSubdivisionType.part_of_city, "inner part of city", null), 200, 200, 200);
                int municipalityId = await SubdivisionDivision_InsertAsync(npgsqlConnection, reference_Municipality, $"{code}01", AdministrativeArealType.Municipality, new GIS.Classes.AdministrativeDivision(Guid.NewGuid(), reference_Municipality, $"{code}01", SubdivisionDivision_Square(100, 100, 600), GIS.Enums.AdministrativeDivisionType.municipality, "municipality"), 100, 100, 600);

                Assert.True(await npgsqlConnection.TableAsync_Building2D_Partition(countyId.Value));

                await SubdivisionDivision_InsertBuilding2DAsync(npgsqlConnection, countyId.Value, reference_Inner_1, 250, 250, 10);
                await SubdivisionDivision_InsertBuilding2DAsync(npgsqlConnection, countyId.Value, reference_Inner_2, 300, 300, 10);
                await SubdivisionDivision_InsertBuilding2DAsync(npgsqlConnection, countyId.Value, reference_Outer_1, 500, 500, 10);
                await SubdivisionDivision_InsertBuilding2DAsync(npgsqlConnection, countyId.Value, reference_Outside_1, 800, 800, 10);

                Building2DPostgreSQLConverter building2DPostgreSQLConverter = new(connectionData_Main);

                List<Building2DReference>? building2DReferences_Outer = await building2DPostgreSQLConverter.GetBuilding2DReferencesBySubdivisionIdAsync(outerId);
                Assert.NotNull(building2DReferences_Outer);
                Assert.Equal(new SortedSet<string>([reference_Inner_1, reference_Inner_2, reference_Outer_1], StringComparer.Ordinal), SubdivisionDivision_References(building2DReferences_Outer));

                // The two Subdivision paths must agree: the one behind building2Dreferencesbyadministrativeareal2did took the polygon from any stored area all along.
                List<Building2DReference>? building2DReferences_Outer_ByIds = await building2DPostgreSQLConverter.GetBuilding2DReferencesByAdministrativeAreal2DIdsAsync([outerId]);
                Assert.NotNull(building2DReferences_Outer_ByIds);
                Assert.Equal(SubdivisionDivision_References(building2DReferences_Outer_ByIds), SubdivisionDivision_References(building2DReferences_Outer));

                List<Building2DReference>? building2DReferences_Inner = await building2DPostgreSQLConverter.GetBuilding2DReferencesBySubdivisionIdAsync(innerId);
                Assert.NotNull(building2DReferences_Inner);
                Assert.Equal(new SortedSet<string>([reference_Inner_1, reference_Inner_2], StringComparer.Ordinal), SubdivisionDivision_References(building2DReferences_Inner));

                // The row type decides: a Municipality over the same polygon is not read by polygon on this path.
                List<Building2DReference>? building2DReferences_Municipality = await building2DPostgreSQLConverter.GetBuilding2DReferencesBySubdivisionIdAsync(municipalityId);
                Assert.NotNull(building2DReferences_Municipality);
                Assert.Empty(building2DReferences_Municipality);
            }
            finally
            {
                await SubdivisionDivision_CleanupAsync(npgsqlConnection);
                if (countyId is int countyId_Temp)
                {
                    await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.Building2D}_{countyId_Temp};");
                }
            }
        }

        /// <summary>
        /// Removes every scratch row the <see cref="Building2DReferencesBySubdivisionId_Division"/> fact writes - its buildings first, then its areas.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <returns>A task that completes when the rows are removed.</returns>
        private static async Task SubdivisionDivision_CleanupAsync(NpgsqlConnection npgsqlConnection)
        {
            await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.Building2D} WHERE reference LIKE '{subdivisionDivision_ReferencePrefix}%';");
            await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference LIKE '{subdivisionDivision_ReferencePrefix}%';");
        }

        /// <summary>
        /// Inserts one scratch <c>administrative_areal_2d</c> row: a square relative to the scratch origin, typed as given and storing the given area object.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="reference">The unique reference of the row.</param>
        /// <param name="code">The code of the row.</param>
        /// <param name="administrativeArealType">The type the row is stored under.</param>
        /// <param name="administrativeAreal2D">The area object the row stores.</param>
        /// <param name="x">The X offset of the lower left corner from the scratch origin.</param>
        /// <param name="y">The Y offset of the lower left corner from the scratch origin.</param>
        /// <param name="size">The edge length of the square.</param>
        /// <returns>The identifier of the inserted row.</returns>
        private static async Task<int> SubdivisionDivision_InsertAsync(NpgsqlConnection npgsqlConnection, string reference, string code, AdministrativeArealType administrativeArealType, GIS.Classes.AdministrativeAreal2D administrativeAreal2D, double x, double y, double size)
        {
            string? objectJson = administrativeAreal2D.ToJsonObject()?.ToJsonString();
            Assert.NotNull(objectJson);

            string commandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO {0} (reference, code, name, type_id, min_x, min_y, max_x, max_y, object) VALUES ('{1}', '{2}', '{1}', {3}, {4}, {5}, {6}, {7}, '{8}') RETURNING id;",
                Constants.TableName.AdministrativeAreal2D,
                reference,
                code,
                (int)administrativeArealType,
                subdivisionDivision_X + x,
                subdivisionDivision_Y + y,
                subdivisionDivision_X + x + size,
                subdivisionDivision_Y + y + size,
                objectJson);

            object? result = await ScalarAsync(npgsqlConnection, commandText);
            Assert.NotNull(result);

            return System.Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Inserts one scratch <c>building_2d</c> row carrying only its county part, reference and bounding box - the columns the polygon read uses.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="countyId">The county part the building is filed under.</param>
        /// <param name="reference">The reference of the building.</param>
        /// <param name="x">The X offset of the lower left corner from the scratch origin.</param>
        /// <param name="y">The Y offset of the lower left corner from the scratch origin.</param>
        /// <param name="size">The edge length of the square footprint.</param>
        /// <returns>A task that completes when the row is inserted.</returns>
        private static async Task SubdivisionDivision_InsertBuilding2DAsync(NpgsqlConnection npgsqlConnection, int countyId, string reference, double x, double y, double size)
        {
            string commandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO {0} (county_id, reference, min_x, min_y, max_x, max_y) VALUES ({1}, '{2}', {3}, {4}, {5}, {6});",
                Constants.TableName.Building2D,
                countyId,
                reference,
                subdivisionDivision_X + x,
                subdivisionDivision_Y + y,
                subdivisionDivision_X + x + size,
                subdivisionDivision_Y + y + size);

            await ExecuteAsync(npgsqlConnection, commandText);
        }

        /// <summary>
        /// Builds a square polygon relative to the scratch origin of the <see cref="Building2DReferencesBySubdivisionId_Division"/> fact.
        /// </summary>
        /// <param name="x">The X offset of the lower left corner from the scratch origin.</param>
        /// <param name="y">The Y offset of the lower left corner from the scratch origin.</param>
        /// <param name="size">The edge length of the square.</param>
        /// <returns>The square polygon.</returns>
        private static PolygonalFace2D SubdivisionDivision_Square(double x, double y, double size)
        {
            IPolygonal2D_Square(subdivisionDivision_X + x, subdivisionDivision_Y + y, size, out Polygon2D polygon2D);

            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(polygonalFace2D);

            return polygonalFace2D;
        }

        /// <summary>
        /// Collects the references of the given building references, ordered for comparison.
        /// </summary>
        /// <param name="building2DReferences">The building references.</param>
        /// <returns>The distinct references.</returns>
        private static SortedSet<string> SubdivisionDivision_References(IEnumerable<Building2DReference> building2DReferences)
        {
            return new SortedSet<string>(building2DReferences.Select(x => x.Reference).OfType<string>(), StringComparer.Ordinal);
        }
    }
}
