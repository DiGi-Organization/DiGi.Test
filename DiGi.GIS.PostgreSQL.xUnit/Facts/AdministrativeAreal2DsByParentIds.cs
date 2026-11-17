using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The X coordinate of the scratch origin of the <see cref="AdministrativeAreal2DsByParentIds"/> fact; far from every other scratch area.
        /// </summary>
        private const double parentIds_X = -6780000;

        /// <summary>
        /// The Y coordinate of the scratch origin of the <see cref="AdministrativeAreal2DsByParentIds"/> fact.
        /// </summary>
        private const double parentIds_Y = -6780000;

        /// <summary>
        /// The reference prefix of every scratch row the <see cref="AdministrativeAreal2DsByParentIds"/> fact writes.
        /// </summary>
        private const string parentIds_ReferencePrefix = "XUNIT-PARENTIDS-";

        /// <summary>
        /// The number of scratch counties the benchmark half of the <see cref="AdministrativeAreal2DsByParentIds"/> fact reads - the size of the production request measured in DiGi.GIS.WebAPI#95.
        /// </summary>
        private const int parentIds_BenchmarkCountyCount = 50;

        /// <summary>
        /// The number of subdivisions of every benchmark county.
        /// </summary>
        private const int parentIds_BenchmarkSubdivisionCount = 10;

        /// <summary>
        /// Verifies, on the throwaway main test database, that <see cref="AdministrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DsByParentIdsAsync(NpgsqlConnection, AdministrativeArealType, IEnumerable{int}, int, int, System.Threading.CancellationToken)"/> answers every parent exactly as the per-parent <see cref="AdministrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DsByAdministrativeArealType(NpgsqlConnection, AdministrativeArealType, int?, int, System.Threading.CancellationToken)"/> does, and benchmarks the two over a 50-county request (DiGi.GIS.WebAPI#95).
        /// <para>Parity covers a single-part county, both parts of a two-part county (each part answers the subdivisions of both, because a part is widened to every part sharing its code), a county without subdivisions (an empty list), an unknown identifier and a subdivision passed as a parent (no entry where the per-parent read answers null), a repeated identifier, the counties already in the test database, and a batch size of one, which splits a county's parts across chunks. Per parent the same subdivision identifiers in the same order and the same summed area are required - the figure <c>densitiesbycountyids</c> divides by.</para>
        /// <para>The benchmark times the per-parent loop the endpoint ran against the batched read, warm-up first, three repeats each, and writes both ranges to the reports directory. Every scratch row is removed before the write and in <c>finally</c>.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task AdministrativeAreal2DsByParentIds()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_AdministrativeArea2D());

            await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference LIKE '{parentIds_ReferencePrefix}%';");

            try
            {
                int countyId_Single = await ParentIds_InsertAsync(npgsqlConnection, "SINGLE", "9071", AdministrativeArealType.County, null, 0, 0, 1000);
                int countyId_Part_1 = await ParentIds_InsertAsync(npgsqlConnection, "PART-1", "9072", AdministrativeArealType.County, null, 2000, 0, 1000);
                int countyId_Part_2 = await ParentIds_InsertAsync(npgsqlConnection, "PART-2", "9072", AdministrativeArealType.County, null, 4000, 0, 1000);
                int countyId_Empty = await ParentIds_InsertAsync(npgsqlConnection, "EMPTY", "9073", AdministrativeArealType.County, null, 6000, 0, 1000);

                int subdivisionId = await ParentIds_InsertAsync(npgsqlConnection, "SINGLE-S1", "9071011", AdministrativeArealType.Subdivision, countyId_Single, 0, 0, 300);
                await ParentIds_InsertAsync(npgsqlConnection, "SINGLE-S2", "9071012", AdministrativeArealType.Subdivision, countyId_Single, 400, 0, 200);
                await ParentIds_InsertAsync(npgsqlConnection, "SINGLE-S3", "9071013", AdministrativeArealType.Subdivision, countyId_Single, 0, 400, 500);
                await ParentIds_InsertAsync(npgsqlConnection, "PART-1-S1", "9072011", AdministrativeArealType.Subdivision, countyId_Part_1, 2000, 0, 300);
                await ParentIds_InsertAsync(npgsqlConnection, "PART-1-S2", "9072012", AdministrativeArealType.Subdivision, countyId_Part_1, 2400, 0, 400);
                await ParentIds_InsertAsync(npgsqlConnection, "PART-2-S1", "9072021", AdministrativeArealType.Subdivision, countyId_Part_2, 4000, 0, 700);

                List<int> countyIds_Benchmark = [];
                for (int i = 0; i < parentIds_BenchmarkCountyCount; i++)
                {
                    double x = (i + 4) * 2000;
                    string code = (9100 + i).ToString(CultureInfo.InvariantCulture);
                    int countyId = await ParentIds_InsertAsync(npgsqlConnection, $"BENCH-{code}", code, AdministrativeArealType.County, null, x, 2000, 1000);
                    countyIds_Benchmark.Add(countyId);

                    for (int j = 0; j < parentIds_BenchmarkSubdivisionCount; j++)
                    {
                        await ParentIds_InsertAsync(npgsqlConnection, $"BENCH-{code}-S{j}", $"{code}{j:000}", AdministrativeArealType.Subdivision, countyId, x + (j * 100), 2000, 90);
                    }
                }

                List<int> countyIds_Existing = [];
                await using (NpgsqlCommand npgsqlCommand = new($"SELECT id FROM {Constants.TableName.AdministrativeAreal2D} WHERE type_id = {(int)AdministrativeArealType.County} AND reference NOT LIKE '{parentIds_ReferencePrefix}%' ORDER BY id LIMIT 50;", npgsqlConnection))
                {
                    await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
                    while (await npgsqlDataReader.ReadAsync())
                    {
                        countyIds_Existing.Add(npgsqlDataReader.GetInt32(0));
                    }
                }

                List<int> parentIds = [countyId_Single, countyId_Part_1, countyId_Part_2, countyId_Empty, subdivisionId, int.MaxValue, countyId_Single, .. countyIds_Benchmark.Take(5), .. countyIds_Existing];

                foreach (int batchSize in new int[] { 1000, 1 })
                {
                    Dictionary<int, List<AdministrativeAreal2D>>? administrativeAreal2Ds_ByParentId = await AdministrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DsByParentIdsAsync(npgsqlConnection, AdministrativeArealType.Subdivision, parentIds, batchSize);
                    Assert.NotNull(administrativeAreal2Ds_ByParentId);

                    foreach (int parentId in parentIds.Distinct())
                    {
                        List<AdministrativeAreal2D>? administrativeAreal2Ds_Expected = await AdministrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DsByAdministrativeArealType(npgsqlConnection, AdministrativeArealType.Subdivision, parentId);
                        if (administrativeAreal2Ds_Expected is null)
                        {
                            Assert.False(administrativeAreal2Ds_ByParentId.ContainsKey(parentId), $"Parent {parentId} has no per-parent answer but has a batched entry (batch size {batchSize}).");
                            continue;
                        }

                        Assert.True(administrativeAreal2Ds_ByParentId.TryGetValue(parentId, out List<AdministrativeAreal2D>? administrativeAreal2Ds_Actual), $"Parent {parentId} has no batched entry (batch size {batchSize}).");
                        Assert.Equal(administrativeAreal2Ds_Expected.Select(x => x.Id), administrativeAreal2Ds_Actual!.Select(x => x.Id));
                        Assert.Equal(ParentIds_Area(administrativeAreal2Ds_Expected), ParentIds_Area(administrativeAreal2Ds_Actual));
                    }

                    Assert.Equal(3, administrativeAreal2Ds_ByParentId[countyId_Single].Count);
                    Assert.Equal(3, administrativeAreal2Ds_ByParentId[countyId_Part_1].Count);
                    Assert.Equal(3, administrativeAreal2Ds_ByParentId[countyId_Part_2].Count);
                    Assert.Empty(administrativeAreal2Ds_ByParentId[countyId_Empty]);
                    Assert.False(administrativeAreal2Ds_ByParentId.ContainsKey(subdivisionId));
                    Assert.False(administrativeAreal2Ds_ByParentId.ContainsKey(int.MaxValue));
                }

                AdministrativeAreal2DPostgreSQLConverter administrativeAreal2DPostgreSQLConverter = new(connectionData_Main);

                // Warm-up of both paths (connection pool, JIT), then three timed repeats each.
                await ParentIds_PerParentAsync(administrativeAreal2DPostgreSQLConverter, countyIds_Benchmark);
                await administrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DsByParentIdsAsync(AdministrativeArealType.Subdivision, countyIds_Benchmark);

                List<long> milliseconds_PerParent = [];
                List<long> milliseconds_Batched = [];
                for (int i = 0; i < 3; i++)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    await ParentIds_PerParentAsync(administrativeAreal2DPostgreSQLConverter, countyIds_Benchmark);
                    milliseconds_PerParent.Add(stopwatch.ElapsedMilliseconds);

                    stopwatch.Restart();
                    Dictionary<int, List<AdministrativeAreal2D>>? administrativeAreal2Ds_ByParentId = await administrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DsByParentIdsAsync(AdministrativeArealType.Subdivision, countyIds_Benchmark);
                    milliseconds_Batched.Add(stopwatch.ElapsedMilliseconds);

                    Assert.NotNull(administrativeAreal2Ds_ByParentId);
                    Assert.Equal(parentIds_BenchmarkCountyCount * parentIds_BenchmarkSubdivisionCount, administrativeAreal2Ds_ByParentId.Values.Sum(x => x.Count));
                }

                string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
                if (directory is not null)
                {
                    StringBuilder stringBuilder = new();
                    stringBuilder.AppendLine("Subdivisions of " + parentIds_BenchmarkCountyCount.ToString(CultureInfo.InvariantCulture) + " counties x " + parentIds_BenchmarkSubdivisionCount.ToString(CultureInfo.InvariantCulture) + " subdivisions (DiGi.GIS.WebAPI#95).");
                    stringBuilder.AppendLine("Database: the throwaway MAIN TEST database (GIS_PostgreSQL_Main_Test.conf). Not production.");
                    stringBuilder.AppendLine("Per-parent loop [ms]: " + string.Join(", ", milliseconds_PerParent.Select(x => x.ToString(CultureInfo.InvariantCulture))));
                    stringBuilder.AppendLine("Batched read [ms]: " + string.Join(", ", milliseconds_Batched.Select(x => x.ToString(CultureInfo.InvariantCulture))));
                    File.WriteAllText(Path.Combine(directory, "AdministrativeAreal2DsByParentIds.txt"), stringBuilder.ToString());
                }

                Assert.True(milliseconds_Batched.Min() < milliseconds_PerParent.Min(), $"Batched {string.Join(", ", milliseconds_Batched)} ms is not faster than per-parent {string.Join(", ", milliseconds_PerParent)} ms.");
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE reference LIKE '{parentIds_ReferencePrefix}%';");
            }
        }

        /// <summary>
        /// Reads the subdivisions of every county one county at a time, each over its own connection - what <c>densitiesbycountyids</c> did before DiGi.GIS.WebAPI#95.
        /// </summary>
        /// <param name="administrativeAreal2DPostgreSQLConverter">The converter reading the throwaway main test database.</param>
        /// <param name="countyIds">The identifiers of the counties.</param>
        /// <returns>The number of subdivisions read.</returns>
        private static async Task<int> ParentIds_PerParentAsync(AdministrativeAreal2DPostgreSQLConverter administrativeAreal2DPostgreSQLConverter, IEnumerable<int> countyIds)
        {
            int count = 0;
            foreach (int countyId in countyIds)
            {
                List<AdministrativeAreal2D>? administrativeAreal2Ds = await administrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DsByAdministrativeArealType(AdministrativeArealType.Subdivision, countyId);
                count += administrativeAreal2Ds?.Count ?? 0;
            }

            return count;
        }

        /// <summary>
        /// Sums the area of the outlines of the given subdivisions, in their order - the figure a density is divided by.
        /// </summary>
        /// <param name="administrativeAreal2Ds">The subdivisions.</param>
        /// <returns>The summed area.</returns>
        private static double ParentIds_Area(IEnumerable<AdministrativeAreal2D> administrativeAreal2Ds)
        {
            double area = 0;
            foreach (PolygonalFace2D polygonalFace2D in administrativeAreal2Ds.PolygonalFace2DsById().Values)
            {
                area += polygonalFace2D.GetArea();
            }

            return area;
        }

        /// <summary>
        /// Inserts one scratch <c>administrative_areal_2d</c> row: a square relative to the scratch origin, a county storing an <see cref="GIS.Classes.AdministrativeDivision"/> or a subdivision storing an <see cref="GIS.Classes.AdministrativeSubdivision"/> filed under the given county part.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to the throwaway main test database.</param>
        /// <param name="suffix">The part of the reference after the scratch prefix.</param>
        /// <param name="code">The code of the row.</param>
        /// <param name="administrativeArealType">The type the row is stored under: county or subdivision.</param>
        /// <param name="countyId">The county part a subdivision is filed under; null for a county.</param>
        /// <param name="x">The X offset of the lower left corner from the scratch origin.</param>
        /// <param name="y">The Y offset of the lower left corner from the scratch origin.</param>
        /// <param name="size">The edge length of the square.</param>
        /// <returns>The identifier of the inserted row.</returns>
        private static async Task<int> ParentIds_InsertAsync(NpgsqlConnection npgsqlConnection, string suffix, string code, AdministrativeArealType administrativeArealType, int? countyId, double x, double y, double size)
        {
            string reference = parentIds_ReferencePrefix + suffix;

            IPolygonal2D_Square(parentIds_X + x, parentIds_Y + y, size, out Polygon2D polygon2D);
            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(polygonalFace2D);

            GIS.Classes.AdministrativeAreal2D administrativeAreal2D = administrativeArealType == AdministrativeArealType.Subdivision
                ? new GIS.Classes.AdministrativeSubdivision(Guid.NewGuid(), reference, code, polygonalFace2D, GIS.Enums.AdministrativeSubdivisionType.part_of_city, reference, null)
                : new GIS.Classes.AdministrativeDivision(Guid.NewGuid(), reference, code, polygonalFace2D, GIS.Enums.AdministrativeDivisionType.county, reference);

            string? objectJson = administrativeAreal2D.ToJsonObject()?.ToJsonString();
            Assert.NotNull(objectJson);

            string commandText = string.Format(
                CultureInfo.InvariantCulture,
                "INSERT INTO {0} (reference, code, name, type_id, min_x, min_y, max_x, max_y, county_id, object) VALUES ('{1}', '{2}', '{1}', {3}, {4}, {5}, {6}, {7}, {8}, '{9}') RETURNING id;",
                Constants.TableName.AdministrativeAreal2D,
                reference,
                code,
                (int)administrativeArealType,
                parentIds_X + x,
                parentIds_Y + y,
                parentIds_X + x + size,
                parentIds_Y + y + size,
                countyId.HasValue ? countyId.Value.ToString(CultureInfo.InvariantCulture) : "NULL",
                objectJson);

            object? result = await ScalarAsync(npgsqlConnection, commandText);
            Assert.NotNull(result);

            return System.Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
    }
}
