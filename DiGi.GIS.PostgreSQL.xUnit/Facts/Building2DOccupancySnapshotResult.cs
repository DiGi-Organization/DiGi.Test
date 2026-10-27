using DiGi.GIS.PostgreSQL.Classes;
using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Building2DOccupancySnapshotResult"/> properties are correctly initialized and survive serialization and copying.
        /// </summary>
        [Fact]
        public void Building2DOccupancySnapshotResult_Serialization()
        {
            int countyId = 55417;
            long count = 33687;
            double occupancySum = 101234.5;
            string checksum = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";
            long figuredSubdivisionCount = 12;

            Building2DOccupancySnapshotResult building2DOccupancySnapshotResult = new(countyId, count, occupancySum, checksum, figuredSubdivisionCount);

            Assert.Equal(countyId, building2DOccupancySnapshotResult.CountyId);
            Assert.Equal(count, building2DOccupancySnapshotResult.Count);
            Assert.Equal(occupancySum, building2DOccupancySnapshotResult.OccupancySum);
            Assert.Equal(checksum, building2DOccupancySnapshotResult.Checksum);
            Assert.Equal(figuredSubdivisionCount, building2DOccupancySnapshotResult.FiguredSubdivisionCount);

            Core.xUnit.Query.SerializationCheck(building2DOccupancySnapshotResult);

            Building2DOccupancySnapshotResult building2DOccupancySnapshotResult_Clone = new(building2DOccupancySnapshotResult);

            Assert.Equal(countyId, building2DOccupancySnapshotResult_Clone.CountyId);
            Assert.Equal(count, building2DOccupancySnapshotResult_Clone.Count);
            Assert.Equal(occupancySum, building2DOccupancySnapshotResult_Clone.OccupancySum);
            Assert.Equal(checksum, building2DOccupancySnapshotResult_Clone.Checksum);
            Assert.Equal(figuredSubdivisionCount, building2DOccupancySnapshotResult_Clone.FiguredSubdivisionCount);
        }

        /// <summary>
        /// Verifies that <see cref="Building2DOccupancyDataPostgreSQLConverter.GetOccupancySnapshotsAsync(NpgsqlConnection?, int?, int, System.Threading.CancellationToken)"/>
        /// and its parameterless overload return null when given a null connection, no connection data or a negative timeout.
        /// </summary>
        [Fact]
        public async Task GetOccupancySnapshotsAsync_Guards()
        {
            Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(null);

            List<Building2DOccupancySnapshotResult>? building2DOccupancySnapshotResults_NullConnection = await building2DOccupancyDataPostgreSQLConverter.GetOccupancySnapshotsAsync((NpgsqlConnection?)null);
            Assert.Null(building2DOccupancySnapshotResults_NullConnection);

            List<Building2DOccupancySnapshotResult>? building2DOccupancySnapshotResults_NullConnectionData = await building2DOccupancyDataPostgreSQLConverter.GetOccupancySnapshotsAsync(countyId: null);
            Assert.Null(building2DOccupancySnapshotResults_NullConnectionData);

            List<Building2DOccupancySnapshotResult>? building2DOccupancySnapshotResults_NegativeTimeout = await building2DOccupancyDataPostgreSQLConverter.GetOccupancySnapshotsAsync(countyId: null, commandTimeout: -1);
            Assert.Null(building2DOccupancySnapshotResults_NegativeTimeout);
        }

        /// <summary>
        /// Verifies that <see cref="Building2DOccupancySubdivisionResult"/> properties, including a null figure and county, survive serialization and copying.
        /// </summary>
        [Fact]
        public void Building2DOccupancySubdivisionResult_Serialization()
        {
            Building2DOccupancySubdivisionResult building2DOccupancySubdivisionResult_Figured = new(4101, 55417, 2310.0, 2310.0, 118, 0);
            Building2DOccupancySubdivisionResult building2DOccupancySubdivisionResult_Unfigured = new(4102, null, null, 0, 3, 2);

            Assert.Equal(4101, building2DOccupancySubdivisionResult_Figured.SubdivisionId);
            Assert.Equal(55417, building2DOccupancySubdivisionResult_Figured.CountyId);
            Assert.Equal(2310.0, building2DOccupancySubdivisionResult_Figured.Figure);
            Assert.Equal(2310.0, building2DOccupancySubdivisionResult_Figured.StoredSum);
            Assert.Equal(118, building2DOccupancySubdivisionResult_Figured.StoredCount);
            Assert.Equal(0, building2DOccupancySubdivisionResult_Figured.ContainedSubdivisionCount);
            Assert.Null(building2DOccupancySubdivisionResult_Unfigured.CountyId);
            Assert.Null(building2DOccupancySubdivisionResult_Unfigured.Figure);

            Core.xUnit.Query.SerializationCheck(building2DOccupancySubdivisionResult_Figured);
            Core.xUnit.Query.SerializationCheck(building2DOccupancySubdivisionResult_Unfigured);

            Building2DOccupancySubdivisionResult building2DOccupancySubdivisionResult_Clone = new(building2DOccupancySubdivisionResult_Figured);

            Assert.Equal(building2DOccupancySubdivisionResult_Figured.SubdivisionId, building2DOccupancySubdivisionResult_Clone.SubdivisionId);
            Assert.Equal(building2DOccupancySubdivisionResult_Figured.CountyId, building2DOccupancySubdivisionResult_Clone.CountyId);
            Assert.Equal(building2DOccupancySubdivisionResult_Figured.Figure, building2DOccupancySubdivisionResult_Clone.Figure);
            Assert.Equal(building2DOccupancySubdivisionResult_Figured.StoredSum, building2DOccupancySubdivisionResult_Clone.StoredSum);
            Assert.Equal(building2DOccupancySubdivisionResult_Figured.StoredCount, building2DOccupancySubdivisionResult_Clone.StoredCount);
            Assert.Equal(building2DOccupancySubdivisionResult_Figured.ContainedSubdivisionCount, building2DOccupancySubdivisionResult_Clone.ContainedSubdivisionCount);
        }

        /// <summary>
        /// Verifies that <see cref="Building2DOccupancyDataPostgreSQLConverter.GetOccupancySubdivisionSnapshotsAsync(NpgsqlConnection?, int, int, System.Threading.CancellationToken)"/>
        /// and its connection-building overload return null when given a null connection, no connection data or a negative timeout.
        /// </summary>
        [Fact]
        public async Task GetOccupancySubdivisionSnapshotsAsync_Guards()
        {
            Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(null);

            Assert.Null(await building2DOccupancyDataPostgreSQLConverter.GetOccupancySubdivisionSnapshotsAsync((NpgsqlConnection?)null, 1));
            Assert.Null(await building2DOccupancyDataPostgreSQLConverter.GetOccupancySubdivisionSnapshotsAsync(1));
            Assert.Null(await building2DOccupancyDataPostgreSQLConverter.GetOccupancySubdivisionSnapshotsAsync(1, commandTimeout: -1));
        }

        /// <summary>
        /// Verifies that <see cref="Building2DOccupancyDataPostgreSQLConverter.GetOccupancySnapshotsAsync(int?, int, System.Threading.CancellationToken)"/>
        /// executes against a live database and gives an identical snapshot on a second read of unchanged data.
        /// <para>Skipped by default: it executes an integration query requiring <c>GIS_PostgreSQL_Storage.conf</c> pointing at a database.</para>
        /// </summary>
        [Fact(Skip = "Executes an integration query. Point GIS_PostgreSQL_Storage.conf at a database before running.")]
        public async Task GetOccupancySnapshotsAsync_Integration()
        {
            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager();
            Assert.NotNull(gISPostgreSQLConverterManager);

            Building2DOccupancyDataPostgreSQLConverter? building2DOccupancyDataPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<Building2DOccupancyDataPostgreSQLConverter>();
            Assert.NotNull(building2DOccupancyDataPostgreSQLConverter);

            List<Building2DOccupancySnapshotResult>? building2DOccupancySnapshotResults_1 = await building2DOccupancyDataPostgreSQLConverter.GetOccupancySnapshotsAsync(countyId: null, commandTimeout: 600);
            Assert.NotNull(building2DOccupancySnapshotResults_1);

            List<Building2DOccupancySnapshotResult>? building2DOccupancySnapshotResults_2 = await building2DOccupancyDataPostgreSQLConverter.GetOccupancySnapshotsAsync(countyId: null, commandTimeout: 600);
            Assert.NotNull(building2DOccupancySnapshotResults_2);

            Assert.Equal(building2DOccupancySnapshotResults_1.Count, building2DOccupancySnapshotResults_2.Count);
            for (int i = 0; i < building2DOccupancySnapshotResults_1.Count; i++)
            {
                Assert.Equal(building2DOccupancySnapshotResults_1[i].CountyId, building2DOccupancySnapshotResults_2[i].CountyId);
                Assert.Equal(building2DOccupancySnapshotResults_1[i].Checksum, building2DOccupancySnapshotResults_2[i].Checksum);
            }
        }
    }
}
