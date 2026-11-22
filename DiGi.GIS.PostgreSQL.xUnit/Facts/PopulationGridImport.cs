using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch census year of the census grid import facts. Only a value in the throwaway main test database.
        /// </summary>
        private const int censusYear_PopulationGridImport = 2099;

        /// <summary>
        /// Verifies <see cref="PopulationGridPostgreSQLConverter.ReadCells(string?, int?, CancellationToken)"/> on the shared sample export: four cells in file order, the census year stored on every cell, <c>tot</c> read as the population (41, a real zero, a value hidden by statistical secrecy read as null, 7), and every outline reprojected to a one-square-kilometre polygon inside the EPSG:2180 domain. The fourth cell carries a ragged clipped ring in the file and still stores the full lattice square its code names, because the reader derives the geometry from <c>code</c> and never parses the export's coordinates. Absence answers null: no path, no census year, a file that is not there.
        /// </summary>
        [Fact]
        public void PopulationGridImport_ReadCells()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "PopulationGrid_NSP2021_sample.geojson");
            Assert.NotNull(path);

            Assert.Null(PopulationGridPostgreSQLConverter.ReadCells(null, 2021));
            Assert.Null(PopulationGridPostgreSQLConverter.ReadCells(path, null));
            Assert.Null(PopulationGridPostgreSQLConverter.ReadCells(Path.Combine(Path.GetDirectoryName(path!)!, "no_such_export.geojson"), 2021));

            List<PopulationGridCell>? populationGridCells = PopulationGridPostgreSQLConverter.ReadCells(path, 2021);
            Assert.NotNull(populationGridCells);
            Assert.Equal(4, populationGridCells.Count);

            string[] expectedCellIds =
            [
                "CRS3035RES1000mN3325000E5112000",
                "CRS3035RES1000mN3326000E5112000",
                "CRS3035RES1000mN3327000E5112000",
                "CRS3035RES1000mN3328000E5112000",
            ];
            int?[] expectedPopulations = [41, 0, null, 7];

            for (int i = 0; i < populationGridCells.Count; i++)
            {
                PopulationGridCell? populationGridCell = populationGridCells[i];
                Assert.NotNull(populationGridCell);
                Assert.Equal(expectedCellIds[i], populationGridCell!.CellId);
                Assert.Equal(2021, populationGridCell.CensusYear);
                Assert.Equal(expectedPopulations[i], populationGridCell.Population);

                PolygonalFace2D? polygonalFace2D = populationGridCell.PolygonalFace2D;
                Assert.NotNull(polygonalFace2D);

                // The lattice square survives the reprojection: one square kilometre within the distortion of the
                // EPSG:3035 to EPSG:2180 conversion, and a footprint inside the PL-1992 domain
                Assert.InRange(polygonalFace2D!.GetArea(), 990000d, 1010000d);

                BoundingBox2D? boundingBox2D = polygonalFace2D.GetBoundingBox();
                Assert.NotNull(boundingBox2D);
                Assert.InRange(boundingBox2D!.Min.X, 100000d, 900000d);
                Assert.InRange(boundingBox2D.Min.Y, 100000d, 900000d);
                Assert.InRange(boundingBox2D.Max.X, 100000d, 900000d);
                Assert.InRange(boundingBox2D.Max.Y, 100000d, 900000d);
            }

            // The cells stack northward along their northing codes
            Assert.True(populationGridCells[1].PolygonalFace2D!.GetBoundingBox()!.Min.Y > populationGridCells[0].PolygonalFace2D!.GetBoundingBox()!.Min.Y);
        }

        /// <summary>
        /// Verifies that <see cref="PopulationGridPostgreSQLConverter.ReadCells(string?, int?, CancellationToken)"/> fails loudly, naming the reason, on every export shape it does not recognise: a file that is not a FeatureCollection, a declared coordinate reference system other than EPSG:3035, a cell code off the expected lattice form, a feature without the <c>tot</c> property, a <c>tot</c> that is not a number, and malformed JSON. The invalid files are written to the reports folder by the fact itself, so the shared fixture folder holds only the export that must succeed.
        /// </summary>
        [Fact]
        public void PopulationGridImport_Invalid()
        {
            string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(directory);

            (string Path, string Content, string Reason)[] invalidFiles =
            [
                ("populationgrid_notcollection.geojson", "{\"type\":\"NotACollection\"}", "FeatureCollection"),
                ("populationgrid_wrongcrs.geojson", "{\"type\":\"FeatureCollection\",\"crs\":{\"type\":\"name\",\"properties\":{\"name\":\"EPSG:4326\"}},\"features\":[]}", "EPSG:3035"),
                ("populationgrid_badcode.geojson", "{\"type\":\"FeatureCollection\",\"crs\":{\"type\":\"name\",\"properties\":{\"name\":\"EPSG:3035\"}},\"features\":[{\"type\":\"Feature\",\"id\":1,\"properties\":{\"code\":\"SOMETHING_ELSE\",\"tot\":1}}]}", "not of the expected"),
                ("populationgrid_missingtot.geojson", "{\"type\":\"FeatureCollection\",\"crs\":{\"type\":\"name\",\"properties\":{\"name\":\"EPSG:3035\"}},\"features\":[{\"type\":\"Feature\",\"id\":2,\"properties\":{\"code\":\"CRS3035RES1000mN3325000E5112000\"}}]}", "no 'tot'"),
                ("populationgrid_badtot.geojson", "{\"type\":\"FeatureCollection\",\"crs\":{\"type\":\"name\",\"properties\":{\"name\":\"EPSG:3035\"}},\"features\":[{\"type\":\"Feature\",\"id\":3,\"properties\":{\"code\":\"CRS3035RES1000mN3325000E5112000\",\"tot\":\"twelve\"}}]}", "'tot' is"),
                ("populationgrid_malformed.geojson", "{\"type\":\"FeatureCollection\",", "not valid JSON"),
            ];

            foreach ((string fileName, string content, string reason) in invalidFiles)
            {
                string path = Path.Combine(directory!, fileName);
                File.WriteAllText(path, content);

                if (string.Equals(reason, "not valid JSON", StringComparison.Ordinal))
                {
                    // Malformed JSON is reported by the parser itself, with the line and position it stopped at
                    Assert.ThrowsAny<JsonException>(() => PopulationGridPostgreSQLConverter.ReadCells(path, 2021));
                    continue;
                }

                InvalidDataException invalidDataException = Assert.Throws<InvalidDataException>(() => PopulationGridPostgreSQLConverter.ReadCells(path, 2021));
                Assert.Contains(reason, invalidDataException.Message, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Verifies, on the throwaway main test database, <see cref="PostgreSQLPopulationGridImportTask"/>: a run without a <c>SourcePath</c> refuses instead of writing, a run over the shared sample writes its four cells under the scratch census year, a clear run replaces rather than duplicates them, another census year does not see them, and the masked cell keeps its null population through the round trip. The fact removes its rows (<c>census_year = 2099</c>) before and in <c>finally</c>. Medium test (a live database server). Reference: DiGi.GIS.PostgreSQL#106.
        /// </summary>
        [MediumSkippableFact]
        public async Task PopulationGridImport_Task()
        {
            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            // The fact creates its own precondition: the table exists before anything is cleaned or written
            Assert.True(await npgsqlConnection.TableAsync_PopulationGrid());

            await DeletePopulationGridYearAsync(npgsqlConnection, censusYear_PopulationGridImport);

            try
            {
                string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "PopulationGrid_NSP2021_sample.geojson");
                Assert.NotNull(path);

                // A run without a SourcePath refuses and writes nothing
                PostgreSQLPopulationGridImportTask refusalTask = new(new PopulationGridPostgreSQLConverter(connectionData_Main));
                TaskCompletionSource<bool> refusalCompletion = new();
                refusalTask.Stopped += (object? sender, EventArgs e) => refusalCompletion.TrySetResult(true);
                refusalTask.Start();
                await refusalCompletion.Task;
                Assert.False(refusalTask.IsSucceeded);
                Assert.Equal(0L, refusalTask.ImportedCount);

                // The import itself
                PostgreSQLPopulationGridImportTask importTask = new(new PopulationGridPostgreSQLConverter(connectionData_Main))
                {
                    PostgreSQLPopulationGridImportTaskOptions =
                    {
                        SourcePath = path,
                        CensusYear = censusYear_PopulationGridImport,
                        Clear = true,
                    },
                };

                TaskCompletionSource<bool> importCompletion = new();
                importTask.Stopped += (object? sender, EventArgs e) => importCompletion.TrySetResult(true);
                importTask.Start();
                await importCompletion.Task;
                Assert.True(importTask.IsSucceeded, importTask.Exception?.ToString());
                Assert.Equal(4L, importTask.ImportedCount);

                PopulationGridPostgreSQLConverter populationGridPostgreSQLConverter = new(connectionData_Main);
                BoundingBox2D boundingBox2D = new(new Point2D(100000, 100000), new Point2D(900000, 900000));

                List<PopulationGridCell>? populationGridCells = await populationGridPostgreSQLConverter.GetCellsByBoundingBox2DAsync(boundingBox2D, censusYear_PopulationGridImport);
                Assert.NotNull(populationGridCells);
                Assert.Equal(4, populationGridCells.Count);

                PopulationGridCell? populationGridCell_Masked = populationGridCells.Find(x => string.Equals(x?.CellId, "CRS3035RES1000mN3327000E5112000", StringComparison.Ordinal));
                Assert.NotNull(populationGridCell_Masked);
                Assert.Null(populationGridCell_Masked.Population);

                PopulationGridCell? populationGridCell_Zero = populationGridCells.Find(x => string.Equals(x?.CellId, "CRS3035RES1000mN3326000E5112000", StringComparison.Ordinal));
                Assert.NotNull(populationGridCell_Zero);
                Assert.Equal(0, populationGridCell_Zero.Population);

                // Another census year does not see the rows
                List<PopulationGridCell>? populationGridCells_OtherYear = await populationGridPostgreSQLConverter.GetCellsByBoundingBox2DAsync(boundingBox2D, 2011);
                Assert.NotNull(populationGridCells_OtherYear);
                Assert.Empty(populationGridCells_OtherYear);

                // A second clear run replaces the rows instead of duplicating them
                long? writtenCount = await populationGridPostgreSQLConverter.PopulateAsync(path, censusYear_PopulationGridImport, clear: true);
                Assert.Equal(4L, writtenCount);

                populationGridCells = await populationGridPostgreSQLConverter.GetCellsByBoundingBox2DAsync(boundingBox2D, censusYear_PopulationGridImport);
                Assert.NotNull(populationGridCells);
                Assert.Equal(4, populationGridCells.Count);
            }
            finally
            {
                await DeletePopulationGridYearAsync(npgsqlConnection, censusYear_PopulationGridImport);
            }
        }

        /// <summary>
        /// Deletes every stored grid cell of one census year.
        /// </summary>
        /// <param name="npgsqlConnection">The open connection to delete through.</param>
        /// <param name="censusYear">The census year whose rows are deleted.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task DeletePopulationGridYearAsync(NpgsqlConnection npgsqlConnection, int censusYear)
        {
            string commandText = $"DELETE FROM {Constants.TableName.PopulationGrid} WHERE census_year = @census_year;";

            await using NpgsqlCommand npgsqlCommand = new(commandText, npgsqlConnection);
            npgsqlCommand.Parameters.AddWithValue("census_year", (short)censusYear);
            await npgsqlCommand.ExecuteNonQueryAsync();
        }
    }
}
