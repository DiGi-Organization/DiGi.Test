using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, on the throwaway storage test database, the materialised area imagery of the full-interpretation test areas (DiGi.GIS.PostgreSQL#129): one row per (area, epoch) behind the <c>UNIQUE (area_key, epoch_year)</c> key of <see cref="OrtoAreaImagePostgreSQLConverter"/>.
        /// <para>The positive row round-trips with its bytes, its box and its source URL; a negative row (status 1, null bytes) re-fetches the same (area, epoch) and replaces it, so exactly one row answers and the cached "no imagery" answer sits where the positive row was; a different epoch is a row of its own and the negative row of the first epoch is untouched. A pair that was never fetched answers null, which is a different answer from the stored negative row. The fact writes scratch rows under a guid-based <c>xunit-orto-image-</c> key, removes them before writing and again in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OrtoAreaImage_Upsert()
        {
            string areaKey = $"xunit-orto-image-{Guid.NewGuid():N}";

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_OrtoAreaImage());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.OrtoAreaImage} WHERE area_key = '{areaKey}';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                OrtoAreaImagePostgreSQLConverter ortoAreaImagePostgreSQLConverter = new(connectionData_Storage);

                BoundingBox2D boundingBox2D = new(new Point2D(510000, 430000), new Point2D(510500, 430400));
                DateTimeOffset fetchedAt = new(2026, 4, 1, 6, 0, 0, TimeSpan.Zero);

                byte[] bytes = new byte[1024];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = (byte)(i % 251);
                }

                // The positive row: the bytes come back byte for byte.
                OrtoAreaImage ortoAreaImage_Positive = new(areaKey, boundingBox2D, 2023, 256, 256, 0, "https://xunit.invalid/orto_area/2023.png", fetchedAt, bytes);
                Assert.Equal(1L, await ortoAreaImagePostgreSQLConverter.UpdateAsync(ortoAreaImage_Positive));

                OrtoAreaImage? ortoAreaImage = await OrtoAreaImagePostgreSQLConverter.GetOrtoAreaImageAsync(npgsqlConnection, areaKey, 2023);
                Assert.NotNull(ortoAreaImage);
                Assert.Equal(areaKey, ortoAreaImage.AreaKey);
                AssertBoundingBox(boundingBox2D, ortoAreaImage.BoundingBox2D);
                Assert.Equal(2023, ortoAreaImage.EpochYear);
                Assert.Equal(256, ortoAreaImage.Width);
                Assert.Equal(256, ortoAreaImage.Height);
                Assert.Equal(0, ortoAreaImage.Status);
                Assert.Equal("https://xunit.invalid/orto_area/2023.png", ortoAreaImage.SourceUrl);
                Assert.Equal(fetchedAt, ortoAreaImage.FetchedAt);
                Assert.NotNull(ortoAreaImage.Bytes);
                Assert.Equal(bytes, ortoAreaImage.Bytes);

                // The negative row replaces the positive one for the same (area, epoch): exactly one row answers.
                OrtoAreaImage ortoAreaImage_Negative = new(areaKey, boundingBox2D, 2023, 0, 0, 1, null, fetchedAt.AddMinutes(5), null);
                Assert.Equal(1L, await ortoAreaImagePostgreSQLConverter.UpdateAsync(ortoAreaImage_Negative));
                Assert.Equal(1L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoAreaImage} WHERE area_key = '{areaKey}';"));

                ortoAreaImage = await OrtoAreaImagePostgreSQLConverter.GetOrtoAreaImageAsync(npgsqlConnection, areaKey, 2023);
                Assert.NotNull(ortoAreaImage);
                Assert.Equal(1, ortoAreaImage.Status);
                Assert.Equal(0, ortoAreaImage.Width);
                Assert.Equal(0, ortoAreaImage.Height);
                Assert.Null(ortoAreaImage.Bytes);
                Assert.Null(ortoAreaImage.SourceUrl);
                Assert.Equal(fetchedAt.AddMinutes(5), ortoAreaImage.FetchedAt);

                // A different epoch is a row of its own, and the negative row of the first epoch is untouched.
                byte[] bytes_2024 = [1, 2, 3, 4, 5, 255];
                OrtoAreaImage ortoAreaImage_OtherEpoch = new(areaKey, boundingBox2D, 2024, 128, 128, 0, "https://xunit.invalid/orto_area/2024.png", fetchedAt.AddDays(1), bytes_2024);
                Assert.Equal(1L, await ortoAreaImagePostgreSQLConverter.UpdateAsync(ortoAreaImage_OtherEpoch));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoAreaImage} WHERE area_key = '{areaKey}';"));

                OrtoAreaImage? ortoAreaImage_2024 = await OrtoAreaImagePostgreSQLConverter.GetOrtoAreaImageAsync(npgsqlConnection, areaKey, 2024);
                Assert.NotNull(ortoAreaImage_2024);
                Assert.Equal(2024, ortoAreaImage_2024.EpochYear);
                Assert.Equal(128, ortoAreaImage_2024.Width);
                Assert.Equal(128, ortoAreaImage_2024.Height);
                Assert.Equal(0, ortoAreaImage_2024.Status);
                Assert.Equal("https://xunit.invalid/orto_area/2024.png", ortoAreaImage_2024.SourceUrl);
                Assert.Equal(bytes_2024, ortoAreaImage_2024.Bytes);

                ortoAreaImage = await OrtoAreaImagePostgreSQLConverter.GetOrtoAreaImageAsync(npgsqlConnection, areaKey, 2023);
                Assert.NotNull(ortoAreaImage);
                Assert.Equal(1, ortoAreaImage.Status);
                Assert.Null(ortoAreaImage.Bytes);

                // A pair that was never fetched answers null, and so do null arguments - a missing row, not a negative one.
                Assert.Null(await OrtoAreaImagePostgreSQLConverter.GetOrtoAreaImageAsync(npgsqlConnection, areaKey, 1999));
                Assert.Null(await OrtoAreaImagePostgreSQLConverter.GetOrtoAreaImageAsync(npgsqlConnection, null, 2023));
                Assert.Null(await OrtoAreaImagePostgreSQLConverter.UpdateAsync(npgsqlConnection, null));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
