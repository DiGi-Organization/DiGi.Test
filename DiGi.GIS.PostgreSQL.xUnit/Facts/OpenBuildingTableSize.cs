using DiGi.GIS.IO.Classes;
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
        /// Verifies that a table size result carries every member through the constructor, a JSON round trip and the copy constructor, for a county partition and for the unpartitioned GHS-OBAT table (DiGi.GIS.PostgreSQL#150).
        /// <para>The null county part and the null estimate are the two shapes a reader must tell apart from a zero, so both instances are asserted member by member.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingTableSizeResult_Serialization()
        {
            OpenBuildingTableSizeResult openBuildingTableSizeResult_Partition = new("eubucco", Constants.TableName.EUBUCCO_Building2D, 55417, 1_234_567_890L, 456_789L);
            OpenBuildingTableSizeResult openBuildingTableSizeResult_Table = new("ghs_obat", Constants.TableName.GHS_OBAT_Building, null, 987_654L, null);

            foreach ((OpenBuildingTableSizeResult openBuildingTableSizeResult, string datasetKey, string tableName, int? countyId, long totalSize, long? estimatedCount) in new[]
            {
                (openBuildingTableSizeResult_Partition, "eubucco", Constants.TableName.EUBUCCO_Building2D, (int?)55417, 1_234_567_890L, (long?)456_789L),
                (openBuildingTableSizeResult_Table, "ghs_obat", Constants.TableName.GHS_OBAT_Building, (int?)null, 987_654L, (long?)null),
            })
            {
                string? json = Core.Convert.ToSystem_String(openBuildingTableSizeResult);
                Assert.NotNull(json);

                OpenBuildingTableSizeResult? openBuildingTableSizeResult_Json = Core.Convert.ToDiGi<OpenBuildingTableSizeResult>(json)?.FirstOrDefault();
                Assert.NotNull(openBuildingTableSizeResult_Json);

                OpenBuildingTableSizeResult openBuildingTableSizeResult_Copy = new(openBuildingTableSizeResult);

                foreach (OpenBuildingTableSizeResult openBuildingTableSizeResult_Temp in new[] { openBuildingTableSizeResult, openBuildingTableSizeResult_Json, openBuildingTableSizeResult_Copy })
                {
                    Assert.Equal(datasetKey, openBuildingTableSizeResult_Temp.DatasetKey);
                    Assert.Equal(tableName, openBuildingTableSizeResult_Temp.TableName);
                    Assert.Equal(countyId, openBuildingTableSizeResult_Temp.CountyId);
                    Assert.Equal(totalSize, openBuildingTableSizeResult_Temp.TotalSize);
                    Assert.Equal(estimatedCount, openBuildingTableSizeResult_Temp.EstimatedCount);
                }

                Core.xUnit.Query.SerializationCheck(openBuildingTableSizeResult);
            }
        }

        /// <summary>
        /// Verifies that the size read refuses a missing connection and an unknown dataset key with null, so the endpoint can tell "nothing to measure" from an empty measurement (DiGi.GIS.PostgreSQL#150).
        /// </summary>
        [Fact]
        public async Task OpenBuilding_GetSizes_RefusesWithoutAConnection()
        {
            Assert.Null(await OpenBuildingPostgreSQLConverter.GetSizesAsync(null, "eubucco"));
            Assert.Null(await OpenBuildingPostgreSQLConverter.GetSizesAsync(null));
        }

        /// <summary>
        /// Verifies, on the throwaway test databases (the scratch county on Main, every open building table on Storage), that the size read reports the partition of an imported county part with exactly the size PostgreSQL gives that partition, and the GHS-OBAT table whole (DiGi.GIS.PostgreSQL#150).
        /// <para>Two footprints are imported into a guid-based scratch county part of <c>eubucco_building_2d</c>. Scoped to that part, the read answers one row whose <c>TotalSize</c> equals <c>pg_total_relation_size</c> of the partition read independently - a differential check, not a plausibility range - and a second, absent county id adds nothing. The key is matched case-insensitively and reported canonical. Unscoped by dataset, the read adds exactly one GHS-OBAT row with a null county part. An unknown key answers null. The scratch county, its partition and the register row are removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuilding_GetSizes_ReportsThePartitionOfTheScope()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection_Storage);
            await npgsqlConnection_Storage.OpenAsync();

            string reference = $"xunit-open-building-{Guid.NewGuid():N}";
            string code = $"TST{Guid.NewGuid():N}";
            string version = $"TST-{Guid.NewGuid():N}";
            int? countyId = null;

            try
            {
                countyId = await OpenBuilding_ScratchCountyIdAsync(npgsqlConnection, reference, code);
                Assert.True(await OpenBuildingPostgreSQLConverter.CreateTablesAsync(npgsqlConnection_Storage), "the open building tables could not be created on the throwaway Storage test database.");
                List<AdministrativeAreal2D> counties = await OpenBuilding_CountiesAsync(npgsqlConnection);

                Footprint[] footprints =
                [
                    OpenBuilding_ScratchFootprint("xunit-size-1"),
                    OpenBuilding_ScratchFootprint("xunit-size-2"),
                ];

                OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Storage);

                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(
                    OpenBuilding_Stream(footprints, default),
                    new BuildingDatasetRelease("eubucco", version, DateTime.Today),
                    [countyId.Value],
                    counties,
                    cancellationToken: default);

                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(2L, openBuildingImportResult.RowCount);

                // An id no county part can have (the scratch id is a fresh identity value, far below it).
                int countyId_Absent = int.MaxValue;

                List<OpenBuildingTableSizeResult>? openBuildingTableSizeResults = await OpenBuildingPostgreSQLConverter.GetSizesAsync(npgsqlConnection_Storage, " EUBUCCO ", [countyId.Value, countyId_Absent]);
                Assert.NotNull(openBuildingTableSizeResults);

                OpenBuildingTableSizeResult openBuildingTableSizeResult = Assert.Single(openBuildingTableSizeResults);
                Assert.Equal("eubucco", openBuildingTableSizeResult.DatasetKey);
                Assert.Equal(Constants.TableName.EUBUCCO_Building2D, openBuildingTableSizeResult.TableName);
                Assert.Equal(countyId.Value, openBuildingTableSizeResult.CountyId);

                object? totalSize_Expected = await ScalarAsync(npgsqlConnection_Storage, $"SELECT pg_total_relation_size('{Constants.TableName.EUBUCCO_Building2D}_{countyId.Value}');");
                Assert.Equal(totalSize_Expected, openBuildingTableSizeResult.TotalSize);
                Assert.True(openBuildingTableSizeResult.TotalSize > 0);

                // Every dataset: the same partition row, plus the GHS-OBAT table whole and never filtered by county.
                List<OpenBuildingTableSizeResult>? openBuildingTableSizeResults_All = await OpenBuildingPostgreSQLConverter.GetSizesAsync(npgsqlConnection_Storage, countyIds: [countyId.Value]);
                Assert.NotNull(openBuildingTableSizeResults_All);
                Assert.Contains(openBuildingTableSizeResults_All, x => x.DatasetKey == "eubucco" && x.CountyId == countyId.Value);

                OpenBuildingTableSizeResult openBuildingTableSizeResult_GHSOBAT = Assert.Single(openBuildingTableSizeResults_All, x => x.DatasetKey == "ghs_obat");
                Assert.Equal(Constants.TableName.GHS_OBAT_Building, openBuildingTableSizeResult_GHSOBAT.TableName);
                Assert.Null(openBuildingTableSizeResult_GHSOBAT.CountyId);

                Assert.All(openBuildingTableSizeResults_All.Where(x => x.CountyId is not null), x => Assert.Equal(countyId.Value, x.CountyId));

                Assert.Null(await OpenBuildingPostgreSQLConverter.GetSizesAsync(npgsqlConnection_Storage, "zzz"));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, npgsqlConnection_Storage, [reference], [version], countyId);
            }
        }
    }
}
