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
        /// Verifies, on the throwaway main test database, <see cref="PopulationGridPostgreSQLConverter"/>: the first write creates the table from the current DDL, a masked cell stores a null population rather than a zero, the census year is part of the key, a box no cell reaches answers an empty list rather than null, a null connection answers null, and re-running an upsert replaces the cell's row instead of appending a duplicate.
        /// <para>The fact removes its rows (<c>XUNIT-GRIDTABLE-*</c>) before and in <c>finally</c>, and expects a database without the grid table at the start - a table created by an earlier run is left in shape by the idempotent DDL either way. Medium test (a live database server). Reference: DiGi.GIS.PostgreSQL#106.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task PopulationGridTable()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            // The fact creates its own precondition: the table exists before anything is cleaned or written
            Assert.True(await npgsqlConnection.TableAsync_PopulationGrid());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.PopulationGrid} WHERE cell_id LIKE 'XUNIT-GRIDTABLE-%';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);

            try
            {
                PopulationGridPostgreSQLConverter populationGridPostgreSQLConverter = new(connectionData_Main);

                PolygonalFace2D? polygonalFace2D = DiGi.Geometry.Planar.Create.PolygonalFace2D(new Point2D(0, 0), new Point2D(1000, 0), new Point2D(1000, 1000), new Point2D(0, 1000), new Point2D(0, 0));
                Assert.NotNull(polygonalFace2D);

                // A null connection answers null rather than throwing
                Assert.Null(await PopulationGridPostgreSQLConverter.GetCellsByBoundingBox2DAsync(null, new BoundingBox2D(new Point2D(0, 0), new Point2D(1000, 1000)), 2021));
                Assert.Null(await PopulationGridPostgreSQLConverter.UpsertAsync(null, [new PopulationGridCell("XUNIT-GRIDTABLE-NULL", 2021, 1, polygonalFace2D)]));

                // The first write creates the table from the current DDL
                Assert.Equal(1L, await populationGridPostgreSQLConverter.UpsertAsync([new PopulationGridCell("XUNIT-GRIDTABLE-1", 2021, 500, polygonalFace2D)]));
                Assert.Equal(1L, await populationGridPostgreSQLConverter.UpsertAsync([new PopulationGridCell("XUNIT-GRIDTABLE-MASKED", 2021, null, polygonalFace2D)]));

                BoundingBox2D boundingBox2D = new(new Point2D(-1, -1), new Point2D(1001, 1001));

                List<PopulationGridCell>? populationGridCells = await populationGridPostgreSQLConverter.GetCellsByBoundingBox2DAsync(boundingBox2D, 2021);
                Assert.NotNull(populationGridCells);
                Assert.Equal(2, populationGridCells.Count);

                PopulationGridCell? populationGridCell_Read = populationGridCells.Find(x => string.Equals(x?.CellId, "XUNIT-GRIDTABLE-1", StringComparison.Ordinal));
                Assert.NotNull(populationGridCell_Read);
                Assert.Equal(2021, populationGridCell_Read.CensusYear);
                Assert.Equal(500, populationGridCell_Read.Population);
                Assert.NotNull(populationGridCell_Read.PolygonalFace2D);
                Assert.Equal(0d, populationGridCell_Read.PolygonalFace2D.GetBoundingBox()!.Min.X);
                Assert.Equal(1000d, populationGridCell_Read.PolygonalFace2D.GetBoundingBox()!.Max.X);

                // A masked cell keeps its null population through the read
                PopulationGridCell? populationGridCell_Masked_Read = populationGridCells.Find(x => string.Equals(x?.CellId, "XUNIT-GRIDTABLE-MASKED", StringComparison.Ordinal));
                Assert.NotNull(populationGridCell_Masked_Read);
                Assert.Null(populationGridCell_Masked_Read.Population);

                // The census year is part of the key: another year's grid does not answer for this one
                List<PopulationGridCell>? populationGridCells_OtherYear = await populationGridPostgreSQLConverter.GetCellsByBoundingBox2DAsync(boundingBox2D, 2011);
                Assert.NotNull(populationGridCells_OtherYear);
                Assert.Empty(populationGridCells_OtherYear);

                // A box no cell reaches answers an empty list, not null
                List<PopulationGridCell>? populationGridCells_Far = await populationGridPostgreSQLConverter.GetCellsByBoundingBox2DAsync(new BoundingBox2D(new Point2D(50000, 50000), new Point2D(51000, 51000)), 2021);
                Assert.NotNull(populationGridCells_Far);
                Assert.Empty(populationGridCells_Far);

                // Re-running the upsert replaces the cell's row rather than appending a duplicate
                Assert.Equal(1L, await populationGridPostgreSQLConverter.UpsertAsync([new PopulationGridCell("XUNIT-GRIDTABLE-1", 2021, 600, polygonalFace2D)]));
                Assert.Equal(1, await CellCountAsync(npgsqlConnection, "XUNIT-GRIDTABLE-1"));

                List<PopulationGridCell>? populationGridCells_After = await populationGridPostgreSQLConverter.GetCellsByBoundingBox2DAsync(boundingBox2D, 2021);
                Assert.NotNull(populationGridCells_After);
                Assert.Equal(2, populationGridCells_After.Count);

                PopulationGridCell? populationGridCell_Updated_Read = populationGridCells_After.Find(x => string.Equals(x?.CellId, "XUNIT-GRIDTABLE-1", StringComparison.Ordinal));
                Assert.NotNull(populationGridCell_Updated_Read);
                Assert.Equal(600, populationGridCell_Updated_Read.Population);
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }

        /// <summary>
        /// Returns how many rows one grid cell identifier currently holds.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to count through.</param>
        /// <param name="cellId">The cell identifier to count.</param>
        /// <returns>A task that represents the asynchronous operation. The task result is the number of rows holding the identifier.</returns>
        private static async Task<int> CellCountAsync(NpgsqlConnection npgsqlConnection, string cellId)
        {
            string commandText = $@"
                SELECT COUNT(*)
                FROM {Constants.TableName.PopulationGrid}
                WHERE cell_id = @cell_id;";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            npgsqlCommand.Parameters.AddWithValue("cell_id", cellId);

            object? result = await npgsqlCommand.ExecuteScalarAsync();
            return result is long count ? (int)count : 0;
        }
    }
}
