using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway main test database, the database half of the <c>retryOutside</c> path of <c>Modify.UpdateAddressPointsAsync</c>: a point in none of the given county parts is found in a neighbouring county part by the bounding box lookup and the polygon read, filed under that part and matched against that part's buildings, while a point in no county part stays outside.
        /// <para>The scratch counties sit at X = -9 000 000 so the lookup reaches no row anything else stored. Without the retry the neighbour's point is outside and nothing is written for it; with it, the point is rescued and its row carries the neighbour's county id. The rows (<c>XUNIT-RESCUE-*</c>, the scratch counties and their <c>building_2d</c> partitions) are removed before and in <c>finally</c>. Reference: DiGi.GIS.PostgreSQL#109.</para>
        /// <para>Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task UpdateAddressPointsAsync_RetryOutside_TestDatabase()
        {
            const string code_A = "99901";
            const string code_B = "99902";
            const string reference_Building_A = "XUNIT-RESCUE-BUILDING-A";
            const string reference_Building_B = "XUNIT-RESCUE-BUILDING-B";
            const string referenceId_InB = "XUNIT-RESCUE-IN-B";
            const string referenceId_InA = "XUNIT-RESCUE-IN-A";
            const string referenceId_Nowhere = "XUNIT-RESCUE-NOWHERE";
            const double x = -9000000;

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            AdministrativeAreal2DPostgreSQLConverter administrativeAreal2DPostgreSQLConverter = new(connectionData_Main);
            Building2DPostgreSQLConverter building2DPostgreSQLConverter = new(connectionData_Main);
            AddressPointPostgreSQLConverter addressPointPostgreSQLConverter = new(connectionData_Main);

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await administrativeAreal2DPostgreSQLConverter.CreateTableAsync());
            Assert.True(await npgsqlConnection.TableAsync_AddressPoint());

            await CleanupAsync();
            try
            {
                // County A (the commune's) and county B (the neighbour) share the edge x + 100.
                AdministrativeAreal2D administrativeAreal2D_A = AdministrativeAreal2D_SquareWithReferenceAndBoundingBox2D("XUNIT-RESCUE-REF-A", code_A, "rescue A", x, 0, 100);
                AdministrativeAreal2D administrativeAreal2D_B = AdministrativeAreal2D_SquareWithReferenceAndBoundingBox2D("XUNIT-RESCUE-REF-B", code_B, "rescue B", x + 100, 0, 100);

                HashSet<int>? ids = await administrativeAreal2DPostgreSQLConverter.UpdateAsync([administrativeAreal2D_A, administrativeAreal2D_B]);
                Assert.NotNull(ids);
                Assert.Equal(2, ids.Count);

                int countyId_A = Assert.Single(await ScratchCountyIdsAsync(code_A));
                int countyId_B = Assert.Single(await ScratchCountyIdsAsync(code_B));

                // A building in each county; the point of county B's building belongs to the commune of county A.
                await AddBuildingAsync(reference_Building_A, code_A, countyId_A, x + 10, 10);
                await AddBuildingAsync(reference_Building_B, code_B, countyId_B, x + 140, 40);

                DateTimeOffset version = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                List<GIS.Classes.AddressPoint> addressPoints =
                [
                    ScratchAddressPoint(referenceId_InB, new Point2D(x + 145, 45), version),
                    ScratchAddressPoint(referenceId_InA, new Point2D(x + 80, 80), version),
                    ScratchAddressPoint(referenceId_Nowhere, new Point2D(x - 500000, 0), version),
                ];

                // Without the retry: the point lying in county B is outside, together with the one in no county, and nothing is written for it.
                AddressPointUpdateResult? addressPointUpdateResult = await addressPointPostgreSQLConverter.UpdateAddressPointsAsync(building2DPostgreSQLConverter, administrativeAreal2DPostgreSQLConverter, addressPoints, [countyId_A], retryOutside: false);
                Assert.NotNull(addressPointUpdateResult);
                Assert.Equal(2, addressPointUpdateResult.Outside);
                Assert.Equal(0, addressPointUpdateResult.Rescued);
                Assert.Equal(1, addressPointUpdateResult.Unmatched);
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.AddressPoint} WHERE reference_id = '{referenceId_InB}';"));

                // With the retry: the point is filed under county B and matched against its building; the one in no county stays outside.
                addressPointUpdateResult = await addressPointPostgreSQLConverter.UpdateAddressPointsAsync(building2DPostgreSQLConverter, administrativeAreal2DPostgreSQLConverter, addressPoints, [countyId_A], retryOutside: true);
                Assert.NotNull(addressPointUpdateResult);
                Assert.Equal(1, addressPointUpdateResult.Outside);
                Assert.Equal(1, addressPointUpdateResult.Rescued);
                Assert.Equal(1, addressPointUpdateResult.Inside);
                Assert.Contains(countyId_B, addressPointUpdateResult.CountyIds ?? []);
                Assert.Equal(countyId_B, await ScalarAsync(npgsqlConnection, $"SELECT county_id FROM {Constants.TableName.AddressPoint} WHERE reference_id = '{referenceId_InB}';"));
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.AddressPoint} WHERE reference_id = '{referenceId_Nowhere}';"));

                List<GIS.Classes.AddressPointMatch>? addressPointMatches = await addressPointPostgreSQLConverter.GetAddressPointMatchesByReferencesAsync([reference_Building_B]);
                Assert.NotNull(addressPointMatches);
                Assert.Equal(referenceId_InB, Assert.Single(addressPointMatches).AddressPoint?.ReferenceId);
            }
            finally
            {
                await CleanupAsync();
            }

            async Task AddBuildingAsync(string reference, string code, int countyId, double x_Building, double y_Building)
            {
                IPolygonal2D_Square(x_Building, y_Building, 10, out Polygon2D polygon2D);

                PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
                Assert.NotNull(polygonalFace2D);

                GIS.Classes.Building2D building2D_GIS = new(Guid.NewGuid(), reference, polygonalFace2D, 1, null, null, []);

                Building2D? building2D = building2D_GIS.ToPostgreSQL(code);
                Assert.NotNull(building2D);

                building2D.CountyId = countyId;

                PostgreSQLUpdateResult? postgreSQLUpdateResult = await building2DPostgreSQLConverter.UpdateAsync([building2D]);
                Assert.NotNull(postgreSQLUpdateResult);
                Assert.Single(postgreSQLUpdateResult.Ids);
            }

            async Task<List<int>> ScratchCountyIdsAsync(string code)
            {
                List<int> countyIds = [];

                await using NpgsqlCommand npgsqlCommand = new($"SELECT id FROM {Constants.TableName.AdministrativeAreal2D} WHERE code = '{code}';", npgsqlConnection);
                await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
                while (await npgsqlDataReader.ReadAsync())
                {
                    countyIds.Add(npgsqlDataReader.GetInt32(0));
                }

                return countyIds;
            }

            async Task CleanupAsync()
            {
                foreach (string code in new string[] { code_A, code_B })
                {
                    foreach (int countyId in await ScratchCountyIdsAsync(code))
                    {
                        await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.Building2D}_{countyId};");
                    }

                    await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE code = '{code}';");
                }

                await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AddressPoint} WHERE reference_id LIKE 'XUNIT-RESCUE-%';");
            }

            static GIS.Classes.AddressPoint ScratchAddressPoint(string referenceId, Point2D point2D, DateTimeOffset version)
            {
                return new GIS.Classes.AddressPoint(point2D, "Street", null, "1", "Locality", null, null, referenceId, version);
            }
        }
    }
}
