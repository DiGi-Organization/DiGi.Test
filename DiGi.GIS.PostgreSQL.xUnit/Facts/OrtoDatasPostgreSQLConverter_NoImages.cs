using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch county part the image-less series fact writes to.
        /// </summary>
        private const int countyId_NoImages = 990607;

        /// <summary>
        /// Verifies <see cref="Query.HasImages(OrtoDatas?)"/> on series whose answer is known: null, no object, an object without <c>Values</c>, an empty series and a one-image series.
        /// <para>An image-less series is a Geoportal fetch in which every request failed (DiGi.GIS.WebAPI#81). The fact needs no server.</para>
        /// </summary>
        [Fact]
        public void Query_HasImages()
        {
            Assert.False(Query.HasImages(null));
            Assert.False(new OrtoDatas().HasImages());
            Assert.False(new OrtoDatas() { Object = new JsonObject() }.HasImages());
            Assert.False(new GIS.Classes.OrtoDatas("XUNIT-NOIMAGES-EMPTY", []).ToPostgreSQL(countyId_NoImages)!.HasImages());
            Assert.True(OrtoDatas_OneImage("XUNIT-NOIMAGES-ONE").ToPostgreSQL(countyId_NoImages)!.HasImages());
        }

        /// <summary>
        /// Verifies that <see cref="OrtoDatasPostgreSQLConverter.UpdateAsync(System.Collections.Generic.IEnumerable{OrtoDatas}?, double, int, System.Threading.CancellationToken)"/> refuses to store a series holding no image and stores a one-image series of the same batch.
        /// <para>Reproduces DiGi.GIS.WebAPI#81: an image-less series named its county, so no geometry check stopped it, it was stored, and the download queue acknowledged its claim - 407 such series floored the acquisition-coverage backlog. Now it comes back as a <see cref="UpdateRejectionReason.NoImages"/> rejection and no row exists; the one-image series beside it proves the write itself works. Runs on the throwaway storage test database (<c>GIS_PostgreSQL_Storage_Test.conf</c>) and drops its partitions before and in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OrtoDatasPostgreSQLConverter_UpdateAsync_NoImages()
        {
            const string reference_Empty = "XUNIT-NOIMAGES-EMPTY";
            const string reference_One = "XUNIT-NOIMAGES-ONE";

            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();
            Assert.True(await npgsqlConnection.TableAsync_OrtoDatas());
            Assert.True(await npgsqlConnection.TableAsync_OrtoDataAcquisition());

            await CleanupAsync();
            try
            {
                OrtoDatas? ortoDatas_Empty = new GIS.Classes.OrtoDatas(reference_Empty, []).ToPostgreSQL(countyId_NoImages);
                OrtoDatas? ortoDatas_One = OrtoDatas_OneImage(reference_One).ToPostgreSQL(countyId_NoImages);
                Assert.NotNull(ortoDatas_Empty);
                Assert.NotNull(ortoDatas_One);

                OrtoDatasPostgreSQLConverter ortoDatasPostgreSQLConverter = new(connectionData_Storage);

                PostgreSQLUpdateResult? postgreSQLUpdateResult = await ortoDatasPostgreSQLConverter.UpdateAsync([ortoDatas_Empty, ortoDatas_One]);
                Assert.NotNull(postgreSQLUpdateResult);
                Assert.Single(postgreSQLUpdateResult.Ids);

                Rejection rejection = Assert.Single(postgreSQLUpdateResult.Rejections);
                Assert.Equal(reference_Empty, rejection.Reference);
                Assert.Equal(UpdateRejectionReason.NoImages, rejection.UpdateRejectionReason);

                Assert.Null(await ortoDatasPostgreSQLConverter.GetOrtoDatasReferenceByReferenceAsync(reference_Empty, countyId_NoImages));
                Assert.NotNull(await ortoDatasPostgreSQLConverter.GetOrtoDatasReferenceByReferenceAsync(reference_One, countyId_NoImages));
            }
            finally
            {
                await CleanupAsync();
            }

            async Task CleanupAsync()
            {
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDatas}_{countyId_NoImages};");
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OrtoDataAcquisition}_{countyId_NoImages};");
            }
        }

        /// <summary>
        /// Builds a series holding a single (not decodable) image of a 10 m square, enough for every check that only asks whether an image exists.
        /// </summary>
        /// <param name="reference">The building reference of the series.</param>
        /// <returns>The series.</returns>
        private static GIS.Classes.OrtoDatas OrtoDatas_OneImage(string reference)
        {
            GIS.Classes.OrtoData ortoData = new(new DateTime(2020, 1, 1), [1, 2, 3], 1.0, new Point2D(500000, 500000));

            return new GIS.Classes.OrtoDatas(reference, [ortoData]);
        }
    }
}
