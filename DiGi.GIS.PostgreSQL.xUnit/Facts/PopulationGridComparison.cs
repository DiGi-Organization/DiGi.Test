using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch county part of the census grid comparison fact. Only a value in the throwaway main test database.
        /// </summary>
        private const int countyId_PopulationGridComparison = 990106;

        /// <summary>
        /// The reference of the scratch county row of the census grid comparison fact.
        /// </summary>
        private const string countyReference_PopulationGridComparison = "XUNIT-GRID-COUNTY";

        /// <summary>
        /// Verifies, on the throwaway main test database, <see cref="Create.PopulationGridComparisonResultAsync"/>: a 3 km county against a 10-cell census grid, cell by cell.
        /// <para>The fixture pins every rule of the comparison: the cell set is the union of the cells whose centre lies in the county outline and the cells holding a county building (a building outside the outline brings its cell in, a cell of the neighbour county's does not come in on its own); the modelled sum per cell is the stored occupancy of the county buildings whose centre lies in the cell, a building without an occupancy row contributing nothing while still counting as a building; a masked cell is a row with a null error and a <c>Masked</c> class, excluded from the metrics; a building whose centre reaches no stored cell is counted, not dropped; and the metrics are the hand-computed <c>MAE</c>, <c>bias</c> and predictive <c>R²</c> of the eight comparable cells. The second run against a census year the grid does not hold proves the year is part of the key: no cells, null metrics, every building reported outside. The fact removes its rows and its two scratch partitions before and in <c>finally</c>. Medium test (a live database server). Reference: DiGi.GIS.PostgreSQL#106.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PopulationGridComparison()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            // The fact creates its own preconditions: every table it writes or reads exists before anything
            // is cleaned - a fresh test database gets them from the idempotent DDL, a used one keeps them

            Assert.True(await npgsqlConnection.TableAsync_AdministrativeArea2D());
            Assert.True(await npgsqlConnection.TableAsync_Building2D());
            Assert.True(await npgsqlConnection.TableAsync_Building2DReferencedObject(Constants.TableName.OccupancyData_Building2D));
            Assert.True(await npgsqlConnection.TableAsync_PopulationGrid());

            string commandText_Cleanup_Occupancy = $"DELETE FROM {Constants.TableName.OccupancyData_Building2D} WHERE county_id = {countyId_PopulationGridComparison};";
            string commandText_Cleanup_Building2D = $"DELETE FROM {Constants.TableName.Building2D} WHERE county_id = {countyId_PopulationGridComparison};";
            string commandText_Cleanup_AdministrativeAreal2D = $"DELETE FROM {Constants.TableName.AdministrativeAreal2D} WHERE id = {countyId_PopulationGridComparison} OR reference = '{countyReference_PopulationGridComparison}';";
            string commandText_Cleanup_PopulationGrid = $"DELETE FROM {Constants.TableName.PopulationGrid} WHERE cell_id LIKE 'XUNIT-GRID-%';";

            await ExecuteAsync(npgsqlConnection, commandText_Cleanup_Occupancy);
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup_Building2D);
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup_AdministrativeAreal2D);
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup_PopulationGrid);

            try
            {
                // The county: a 3 km square outline, the only row of its id

                PolygonalFace2D? countyPolygonalFace2D = DiGi.Geometry.Planar.Create.PolygonalFace2D(new Point2D(0, 0), new Point2D(3000, 0), new Point2D(3000, 3000), new Point2D(0, 3000), new Point2D(0, 0));
                Assert.NotNull(countyPolygonalFace2D);
                await InsertCountyAsync(npgsqlConnection, countyPolygonalFace2D);

                // The buildings: two sharing cell C00, one with no occupancy row, one with a high value in C22,
                // one outside every cell, one in a masked cell, one outside the outline but inside cell C30

                Assert.True(await npgsqlConnection.TableAsync_Building2D_Partition(countyId_PopulationGridComparison));

                await InsertBuildingAsync(npgsqlConnection, "XUNIT-GRID-B1", 400, 400);
                await InsertBuildingAsync(npgsqlConnection, "XUNIT-GRID-B2", 600, 600);
                await InsertBuildingAsync(npgsqlConnection, "XUNIT-GRID-B3", 1500, 1500);
                await InsertBuildingAsync(npgsqlConnection, "XUNIT-GRID-B4", 2500, 2500);
                await InsertBuildingAsync(npgsqlConnection, "XUNIT-GRID-B5", 5000, 5000);
                await InsertBuildingAsync(npgsqlConnection, "XUNIT-GRID-B6", 2500, 50);
                await InsertBuildingAsync(npgsqlConnection, "XUNIT-GRID-B7", 3500, 500);

                // The stored occupancy: every building but B3, which has no row at all

                Assert.True(await npgsqlConnection.TableAsync_Building2DReferencedObject_Partition(Constants.TableName.OccupancyData_Building2D, countyId_PopulationGridComparison));

                await InsertOccupancyAsync(npgsqlConnection, "XUNIT-GRID-B1", 60);
                await InsertOccupancyAsync(npgsqlConnection, "XUNIT-GRID-B2", 40);
                await InsertOccupancyAsync(npgsqlConnection, "XUNIT-GRID-B4", 750);
                await InsertOccupancyAsync(npgsqlConnection, "XUNIT-GRID-B5", 10);
                await InsertOccupancyAsync(npgsqlConnection, "XUNIT-GRID-B6", 50);
                await InsertOccupancyAsync(npgsqlConnection, "XUNIT-GRID-B7", 30);

                // The census grid: nine cells tiling the county plus one cell beyond its eastern edge,
                // its centre outside the outline and its census value masked

                PopulationGridPostgreSQLConverter populationGridPostgreSQLConverter = new(connectionData_Main);

                List<PopulationGridCell> populationGridCells =
                [
                    ScratchCell("XUNIT-GRID-C00", 100, 0, 0)!,
                    ScratchCell("XUNIT-GRID-C10", 200, 1000, 0)!,
                    ScratchCell("XUNIT-GRID-C20", null, 2000, 0)!,
                    ScratchCell("XUNIT-GRID-C01", 0, 0, 1000)!,
                    ScratchCell("XUNIT-GRID-C11", 300, 1000, 1000)!,
                    ScratchCell("XUNIT-GRID-C21", 400, 2000, 1000)!,
                    ScratchCell("XUNIT-GRID-C02", 500, 0, 2000)!,
                    ScratchCell("XUNIT-GRID-C12", 600, 1000, 2000)!,
                    ScratchCell("XUNIT-GRID-C22", 700, 2000, 2000)!,
                    ScratchCell("XUNIT-GRID-C30", null, 3000, 0)!,
                ];

                Assert.Equal(10L, await populationGridPostgreSQLConverter.UpsertAsync(populationGridCells));

                // The comparison

                Building2DPostgreSQLConverter building2DPostgreSQLConverter = new(connectionData_Main);
                Building2DOccupancyDataPostgreSQLConverter building2DOccupancyDataPostgreSQLConverter = new(connectionData_Main);
                AdministrativeAreal2DPostgreSQLConverter administrativeAreal2DPostgreSQLConverter = new(connectionData_Main);

                PopulationGridComparisonResult? populationGridComparisonResult = await building2DPostgreSQLConverter.PopulationGridComparisonResultAsync(building2DOccupancyDataPostgreSQLConverter, populationGridPostgreSQLConverter, administrativeAreal2DPostgreSQLConverter, countyId_PopulationGridComparison, 2021);
                Assert.NotNull(populationGridComparisonResult);
                Assert.Equal(countyId_PopulationGridComparison, populationGridComparisonResult.CountyId);
                Assert.Equal(2021, populationGridComparisonResult.CensusYear);

                // Eight cells carry a census value, two are masked, one building reaches no stored cell

                Assert.Equal(8, populationGridComparisonResult.ComparedCellCount);
                Assert.Equal(2, populationGridComparisonResult.MaskedCellCount);
                Assert.Equal(1, populationGridComparisonResult.BuildingOutsideCellCount);

                Assert.NotNull(populationGridComparisonResult.Items);
                Assert.Equal(10, populationGridComparisonResult.Items.Count);

                // Ordered by cell identifier
                Assert.Equal("XUNIT-GRID-C00", populationGridComparisonResult.Items[0].CellId);
                Assert.Equal("XUNIT-GRID-C30", populationGridComparisonResult.Items[9].CellId);

                // Two buildings sum to exactly the census value of their cell
                PopulationGridComparisonItem? populationGridComparisonItem_C00 = FindPopulationGridComparisonItem(populationGridComparisonResult, "XUNIT-GRID-C00");
                Assert.NotNull(populationGridComparisonItem_C00);
                Assert.Equal(100, populationGridComparisonItem_C00.CensusPopulation);
                Assert.Equal(100, populationGridComparisonItem_C00.ModelledPopulation);
                Assert.Equal(0, populationGridComparisonItem_C00.Error);
                Assert.Equal(2, populationGridComparisonItem_C00.BuildingCount);
                Assert.Equal(Enums.PopulationDensityClass.Low, populationGridComparisonItem_C00.DensityClass);

                // A cell the model never reaches keeps its census value as a real negative error
                PopulationGridComparisonItem? populationGridComparisonItem_C10 = FindPopulationGridComparisonItem(populationGridComparisonResult, "XUNIT-GRID-C10");
                Assert.NotNull(populationGridComparisonItem_C10);
                Assert.Equal(0, populationGridComparisonItem_C10.ModelledPopulation);
                Assert.Equal(-200, populationGridComparisonItem_C10.Error);
                Assert.Equal(0, populationGridComparisonItem_C10.BuildingCount);

                // A building with no occupancy row counts as a building and contributes nothing
                PopulationGridComparisonItem? populationGridComparisonItem_C11 = FindPopulationGridComparisonItem(populationGridComparisonResult, "XUNIT-GRID-C11");
                Assert.NotNull(populationGridComparisonItem_C11);
                Assert.Equal(300, populationGridComparisonItem_C11.CensusPopulation);
                Assert.Equal(0, populationGridComparisonItem_C11.ModelledPopulation);
                Assert.Equal(-300, populationGridComparisonItem_C11.Error);
                Assert.Equal(1, populationGridComparisonItem_C11.BuildingCount);

                // An empty cell is a real zero, not a masked one
                PopulationGridComparisonItem? populationGridComparisonItem_C01 = FindPopulationGridComparisonItem(populationGridComparisonResult, "XUNIT-GRID-C01");
                Assert.NotNull(populationGridComparisonItem_C01);
                Assert.Equal(0, populationGridComparisonItem_C01.CensusPopulation);
                Assert.Equal(0, populationGridComparisonItem_C01.Error);
                Assert.Equal(Enums.PopulationDensityClass.Zero, populationGridComparisonItem_C01.DensityClass);

                // A masked cell: the modelled sum stays real, the census side and the error stay null
                PopulationGridComparisonItem? populationGridComparisonItem_C20 = FindPopulationGridComparisonItem(populationGridComparisonResult, "XUNIT-GRID-C20");
                Assert.NotNull(populationGridComparisonItem_C20);
                Assert.Null(populationGridComparisonItem_C20.CensusPopulation);
                Assert.Equal(50, populationGridComparisonItem_C20.ModelledPopulation);
                Assert.Null(populationGridComparisonItem_C20.Error);
                Assert.Equal(1, populationGridComparisonItem_C20.BuildingCount);
                Assert.Equal(Enums.PopulationDensityClass.Masked, populationGridComparisonItem_C20.DensityClass);

                // The model over-counts one cell by fifty
                PopulationGridComparisonItem? populationGridComparisonItem_C22 = FindPopulationGridComparisonItem(populationGridComparisonResult, "XUNIT-GRID-C22");
                Assert.NotNull(populationGridComparisonItem_C22);
                Assert.Equal(700, populationGridComparisonItem_C22.CensusPopulation);
                Assert.Equal(750, populationGridComparisonItem_C22.ModelledPopulation);
                Assert.Equal(50, populationGridComparisonItem_C22.Error);
                Assert.Equal(1, populationGridComparisonItem_C22.BuildingCount);
                Assert.Equal(Enums.PopulationDensityClass.Medium, populationGridComparisonItem_C22.DensityClass);

                // The cell beyond the eastern edge: its centre is outside the county outline, so rule (a) misses
                // it - it is in the comparison only because a county building landed in it (rule (b))
                PopulationGridComparisonItem? populationGridComparisonItem_C30 = FindPopulationGridComparisonItem(populationGridComparisonResult, "XUNIT-GRID-C30");
                Assert.NotNull(populationGridComparisonItem_C30);
                Assert.Null(populationGridComparisonItem_C30.CensusPopulation);
                Assert.Equal(30, populationGridComparisonItem_C30.ModelledPopulation);
                Assert.Equal(1, populationGridComparisonItem_C30.BuildingCount);
                Assert.Equal(Enums.PopulationDensityClass.Masked, populationGridComparisonItem_C30.DensityClass);

                // The metrics over the eight comparable cells, hand-computed from the fixture:
                // errors 0, -200, 0, -300, -400, -500, -600, +50 - absolute sum 2050, signed sum -1950
                Assert.NotNull(populationGridComparisonResult.Mae);
                Assert.NotNull(populationGridComparisonResult.Bias);
                Assert.NotNull(populationGridComparisonResult.R2);
                Assert.Equal(256.25, populationGridComparisonResult.Mae.Value, 10);
                Assert.Equal(-243.75, populationGridComparisonResult.Bias.Value, 10);

                // census mean 350, variance sum 420000, squared error sum 902500 - 1 - 902500/420000 = -193/168
                Assert.Equal(-193d / 168d, populationGridComparisonResult.R2.Value, 10);

                // A census year the grid does not hold: no cells, null metrics, every building reported outside
                PopulationGridComparisonResult? populationGridComparisonResult_OtherYear = await building2DPostgreSQLConverter.PopulationGridComparisonResultAsync(building2DOccupancyDataPostgreSQLConverter, populationGridPostgreSQLConverter, administrativeAreal2DPostgreSQLConverter, countyId_PopulationGridComparison, 2011);
                Assert.NotNull(populationGridComparisonResult_OtherYear);
                Assert.Equal(0, populationGridComparisonResult_OtherYear.ComparedCellCount);
                Assert.Equal(0, populationGridComparisonResult_OtherYear.MaskedCellCount);
                Assert.Equal(7, populationGridComparisonResult_OtherYear.BuildingOutsideCellCount);
                Assert.NotNull(populationGridComparisonResult_OtherYear.Items);
                Assert.Empty(populationGridComparisonResult_OtherYear.Items);
                Assert.Null(populationGridComparisonResult_OtherYear.Mae);
                Assert.Null(populationGridComparisonResult_OtherYear.Bias);
                Assert.Null(populationGridComparisonResult_OtherYear.R2);

                // A null converter answers null
                Assert.Null(await Create.PopulationGridComparisonResultAsync(null, building2DOccupancyDataPostgreSQLConverter, populationGridPostgreSQLConverter, administrativeAreal2DPostgreSQLConverter, countyId_PopulationGridComparison, 2021));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup_Occupancy);
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup_Building2D);
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup_AdministrativeAreal2D);
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup_PopulationGrid);
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.Building2D}_{countyId_PopulationGridComparison};");
                await ExecuteAsync(npgsqlConnection, $"DROP TABLE IF EXISTS {Constants.TableName.OccupancyData_Building2D}_{countyId_PopulationGridComparison};");
            }

            static async Task InsertCountyAsync(NpgsqlConnection npgsqlConnection, PolygonalFace2D polygonalFace2D)
            {
                AdministrativeDivision administrativeDivision = new(Guid.NewGuid(), countyReference_PopulationGridComparison, "99999901", polygonalFace2D, DiGi.GIS.Enums.AdministrativeDivisionType.county, "Xunit County 106");

                string commandText = $@"
                    INSERT INTO {Constants.TableName.AdministrativeAreal2D} (id, reference, code, name, type_id, min_x, min_y, max_x, max_y, object)
                    VALUES (@id, @reference, @code, @name, @type_id, @min_x, @min_y, @max_x, @max_y, @object);";

                BoundingBox2D boundingBox2D = polygonalFace2D.GetBoundingBox()!;

                await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Integer) { Value = countyId_PopulationGridComparison });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = countyReference_PopulationGridComparison });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("code", NpgsqlDbType.Text) { Value = "99999901" });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Text) { Value = "Xunit County 106" });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("type_id", NpgsqlDbType.Smallint) { Value = (short)DiGi.GIS.PostgreSQL.Enums.AdministrativeArealType.County });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = boundingBox2D.Min.X });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = boundingBox2D.Min.Y });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = boundingBox2D.Max.X });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = boundingBox2D.Max.Y });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = administrativeDivision.ToJsonObject()?.ToJsonString() });

                await npgsqlCommand.ExecuteNonQueryAsync();
            }

            static async Task InsertBuildingAsync(NpgsqlConnection npgsqlConnection, string reference, double x, double y)
            {
                string commandText = $@"
                    INSERT INTO {Constants.TableName.Building2D} (county_id, reference, min_x, min_y, max_x, max_y)
                    VALUES (@county_id, @reference, @min_x, @min_y, @max_x, @max_y);";

                await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId_PopulationGridComparison });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = reference });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_x", NpgsqlDbType.Double) { Value = x - 10 });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("min_y", NpgsqlDbType.Double) { Value = y - 10 });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_x", NpgsqlDbType.Double) { Value = x + 10 });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("max_y", NpgsqlDbType.Double) { Value = y + 10 });

                await npgsqlCommand.ExecuteNonQueryAsync();
            }

            static async Task InsertOccupancyAsync(NpgsqlConnection npgsqlConnection, string reference, uint occupancy)
            {
                OccupancyData occupancyData = new(reference, 100, occupancy);
                string? json = occupancyData.ToJsonObject()?.ToJsonString();
                Assert.NotNull(json);

                string commandText = $@"
                    INSERT INTO {Constants.TableName.OccupancyData_Building2D} (unique_id, county_id, reference, object)
                    VALUES (@unique_id, @county_id, @reference, @object);";

                await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("unique_id", NpgsqlDbType.Text) { Value = reference });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("county_id", NpgsqlDbType.Integer) { Value = countyId_PopulationGridComparison });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("reference", NpgsqlDbType.Text) { Value = reference });
                npgsqlCommand.Parameters.Add(new NpgsqlParameter("object", NpgsqlDbType.Jsonb) { Value = json });

                await npgsqlCommand.ExecuteNonQueryAsync();
            }

            static PopulationGridCell? ScratchCell(string cellId, int? population, double x, double y)
            {
                PolygonalFace2D? polygonalFace2D = DiGi.Geometry.Planar.Create.PolygonalFace2D(new Point2D(x, y), new Point2D(x + 1000, y), new Point2D(x + 1000, y + 1000), new Point2D(x, y + 1000), new Point2D(x, y));
                return polygonalFace2D is null ? null : new PopulationGridCell(cellId, 2021, population, polygonalFace2D);
            }

            static PopulationGridComparisonItem? FindPopulationGridComparisonItem(PopulationGridComparisonResult? populationGridComparisonResult, string cellId)
            {
                if (populationGridComparisonResult?.Items is not IReadOnlyList<PopulationGridComparisonItem> populationGridComparisonItems)
                {
                    return null;
                }

                foreach (PopulationGridComparisonItem? populationGridComparisonItem in populationGridComparisonItems)
                {
                    if (string.Equals(populationGridComparisonItem?.CellId, cellId, StringComparison.Ordinal))
                    {
                        return populationGridComparisonItem;
                    }
                }

                return null;
            }
        }
    }
}
