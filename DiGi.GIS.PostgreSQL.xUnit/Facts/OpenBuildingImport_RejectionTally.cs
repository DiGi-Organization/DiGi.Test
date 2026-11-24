using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.IO.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
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
        /// Verifies that the open building import counts every rejection per <see cref="UpdateRejectionReason"/> and carries the tally on the result, so a run report can explain its rejection figure instead of only stating it (DiGi.GIS.PostgreSQL#172).
        /// <para>The fixture is a known mix of four footprints over the scratch county: one valid row, one null footprint (<see cref="UpdateRejectionReason.Undefined"/>), one ring of three ordinates the reprojection refuses as a whole (<see cref="UpdateRejectionReason.GeometryRejected"/>) and one reprojectable footprint handed a county list that holds only the scratch part, so no county envelope reaches it (<see cref="UpdateRejectionReason.CountyUnresolved"/>). The run is deliberately given the scratch part alone: with every county of the test database loaded the fourth footprint would file under a real county outside the scope and be counted as filtered rather than rejected. Asserted: the tally holds exactly those three reasons with one each, its entries sum to <c>RejectionCount</c>, no reason the run never raised appears, the capped sample still names each rejection, and only the valid footprint reached the database. The scratch county, its partition and the register row are removed in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuildingImport_RejectionTally()
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

                AdministrativeAreal2D? administrativeAreal2D_Scratch = counties.Find(x => string.Equals(x.Reference, reference, StringComparison.Ordinal));
                Assert.NotNull(administrativeAreal2D_Scratch);

                List<AdministrativeAreal2D> counties_Import = [administrativeAreal2D_Scratch];

                // Reprojectable and inside the Polish domain, yet ~170 km from the scratch square: with only
                // the scratch part handed to the run, no county envelope reaches this footprint at all.
                Point3D? point3D_Unresolved = GIS.Convert.ToEPSG4326(new Point2D(500000, 400000));
                Assert.NotNull(point3D_Unresolved);

                Footprint? footprint_Unresolved = OpenBuilding_Footprint("xunit-tally-unresolved", point3D_Unresolved.X, point3D_Unresolved.Y, 0.00005);
                Assert.NotNull(footprint_Unresolved);

                Footprint? footprint_ShortRing = OpenBuilding_ShortRingFootprint("xunit-tally-short-ring");
                Assert.NotNull(footprint_ShortRing);

                // The known mix: one row written, three rejections of three different reasons.
                Footprint[] footprints =
                [
                    OpenBuilding_ScratchFootprint("xunit-tally-valid"),
                    null!,
                    footprint_ShortRing,
                    footprint_Unresolved,
                ];

                OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Storage);

                OpenBuildingImportResult? openBuildingImportResult = await openBuildingPostgreSQLConverter.ImportAsync(
                    OpenBuilding_Stream(footprints, default),
                    new BuildingDatasetRelease("eubucco", version, DateTime.Today),
                    [countyId.Value],
                    counties_Import,
                    cancellationToken: default);

                Assert.NotNull(openBuildingImportResult);
                Assert.Equal(1L, openBuildingImportResult.RowCount);
                Assert.Equal(0L, openBuildingImportResult.FilteredCount);
                Assert.Equal(3L, openBuildingImportResult.RejectionCount);

                // The tally names every reason of the mix, sums to the one number the report reads, and
                // carries nothing for a reason the run never raised.
                Assert.Equal(3, openBuildingImportResult.RejectionCounts.Count);
                Assert.Equal(1L, openBuildingImportResult.RejectionCounts.GetValueOrDefault(UpdateRejectionReason.Undefined));
                Assert.Equal(1L, openBuildingImportResult.RejectionCounts.GetValueOrDefault(UpdateRejectionReason.GeometryRejected));
                Assert.Equal(1L, openBuildingImportResult.RejectionCounts.GetValueOrDefault(UpdateRejectionReason.CountyUnresolved));
                Assert.Equal(openBuildingImportResult.RejectionCount, openBuildingImportResult.RejectionCounts.Values.Sum());
                Assert.All(openBuildingImportResult.RejectionCounts, pair => Assert.True(pair.Value > 0, "a reason the run never raised must be absent from the tally, not zero."));

                // The capped sample still names each rejection with the same reason the tally counted.
                Assert.Equal(3, openBuildingImportResult.Rejections.Count);
                Assert.Contains(openBuildingImportResult.Rejections, x => x.Reference is null && x.UpdateRejectionReason == UpdateRejectionReason.Undefined);
                Assert.Contains(openBuildingImportResult.Rejections, x => x.Reference == "xunit-tally-short-ring" && x.UpdateRejectionReason == UpdateRejectionReason.GeometryRejected);
                Assert.Contains(openBuildingImportResult.Rejections, x => x.Reference == "xunit-tally-unresolved" && x.UpdateRejectionReason == UpdateRejectionReason.CountyUnresolved);

                // Exactly the valid footprint reached the database.
                Assert.Equal(1L, await OpenBuildingPostgreSQLConverter.GetRowCountAsync(npgsqlConnection_Storage, "eubucco", [countyId.Value]));
            }
            finally
            {
                await OpenBuilding_CleanupAsync(npgsqlConnection, npgsqlConnection_Storage, [reference], [version], countyId);
            }
        }
    }
}
