using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.IO.Classes;
using DiGi.GIS.IO.Enums;
using Microsoft.Data.Sqlite;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.IO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Round-trips a <see cref="SourceExtent"/> through the DiGi serializer and pins its copy constructor member by member, so a member added later without its copy line fails here.
        /// </summary>
        [Fact]
        public void SourceExtent_Serialization()
        {
            SourceExtent sourceExtent = new(3035, new BoundingBox2D(new Point2D(4971256.5, 2948576.5), new Point2D(5142696.5, 3092886.0)));

            Core.xUnit.Query.SerializationCheck(sourceExtent);

            SourceExtent sourceExtent_Copy = new(sourceExtent);
            Assert.Equal(sourceExtent.Srid, sourceExtent_Copy.Srid);
            Assert.NotNull(sourceExtent_Copy.BoundingBox2D);
            Assert.NotSame(sourceExtent.BoundingBox2D, sourceExtent_Copy.BoundingBox2D);
            Assert.Equal(sourceExtent.BoundingBox2D!.Min.X, sourceExtent_Copy.BoundingBox2D!.Min.X);
            Assert.Equal(sourceExtent.BoundingBox2D.Min.Y, sourceExtent_Copy.BoundingBox2D.Min.Y);
            Assert.Equal(sourceExtent.BoundingBox2D.Max.X, sourceExtent_Copy.BoundingBox2D.Max.X);
            Assert.Equal(sourceExtent.BoundingBox2D.Max.Y, sourceExtent_Copy.BoundingBox2D.Max.Y);
        }

        /// <summary>
        /// Pins <see cref="Query.QuadkeyBoundingBox2D(string?)"/> on the worked example of the Bing Maps tile system - quadkey 213 is tile (3, 5) of level 3, so its X digits and Y digits differ and a swapped axis or a north/south flip fails - and on the first-level quadrants.
        /// <para>A child tile lies inside its parent, and a quadkey that is not one answers null.</para>
        /// </summary>
        [Fact]
        public void QuadkeyBoundingBox2D()
        {
            const double tolerance = 1e-9;

            // Tile (3, 5) of level 3: longitude -45..0, latitude from the inverse Mercator of rows 5 and 6.
            BoundingBox2D? boundingBox2D = Query.QuadkeyBoundingBox2D("213");
            Assert.NotNull(boundingBox2D);
            Assert.Equal(-45.0, boundingBox2D.Min.X, tolerance);
            Assert.Equal(0.0, boundingBox2D.Max.X, tolerance);
            Assert.Equal(Math.Atan(Math.Sinh(-Math.PI / 2)) * 180 / Math.PI, boundingBox2D.Min.Y, tolerance);
            Assert.Equal(Math.Atan(Math.Sinh(-Math.PI / 4)) * 180 / Math.PI, boundingBox2D.Max.Y, tolerance);

            // Quadrant 1 is the north-east one, quadrant 2 the south-west one.
            BoundingBox2D? boundingBox2D_1 = Query.QuadkeyBoundingBox2D("1");
            Assert.NotNull(boundingBox2D_1);
            Assert.Equal(0.0, boundingBox2D_1.Min.X, tolerance);
            Assert.Equal(180.0, boundingBox2D_1.Max.X, tolerance);
            Assert.Equal(0.0, boundingBox2D_1.Min.Y, tolerance);
            Assert.Equal(85.0511287798, boundingBox2D_1.Max.Y, 1e-9);

            BoundingBox2D? boundingBox2D_2 = Query.QuadkeyBoundingBox2D("2");
            Assert.NotNull(boundingBox2D_2);
            Assert.Equal(-180.0, boundingBox2D_2.Min.X, tolerance);
            Assert.Equal(0.0, boundingBox2D_2.Max.X, tolerance);
            Assert.Equal(-85.0511287798, boundingBox2D_2.Min.Y, 1e-9);
            Assert.Equal(0.0, boundingBox2D_2.Max.Y, tolerance);

            // A Polish chunk of the Microsoft release (DiGi.GIS.PostgreSQL#126) lies in Poland and inside its parent tile.
            BoundingBox2D? boundingBox2D_Poland = Query.QuadkeyBoundingBox2D("120213101");
            BoundingBox2D? boundingBox2D_Parent = Query.QuadkeyBoundingBox2D("12021310");
            Assert.NotNull(boundingBox2D_Poland);
            Assert.NotNull(boundingBox2D_Parent);
            Assert.InRange(boundingBox2D_Poland.Min.X, 14.0, 24.2);
            Assert.InRange(boundingBox2D_Poland.Max.Y, 49.0, 54.9);
            Assert.True(boundingBox2D_Parent.Min.X <= boundingBox2D_Poland.Min.X && boundingBox2D_Poland.Max.X <= boundingBox2D_Parent.Max.X);
            Assert.True(boundingBox2D_Parent.Min.Y <= boundingBox2D_Poland.Min.Y && boundingBox2D_Poland.Max.Y <= boundingBox2D_Parent.Max.Y);

            Assert.Null(Query.QuadkeyBoundingBox2D(null));
            Assert.Null(Query.QuadkeyBoundingBox2D(string.Empty));
            Assert.Null(Query.QuadkeyBoundingBox2D("124"));
            Assert.Null(Query.QuadkeyBoundingBox2D("12a"));
            Assert.Null(Query.QuadkeyBoundingBox2D(new string('1', 31)));
        }

        /// <summary>
        /// Verifies <see cref="Query.SourceExtentAsync(string?, BuildingDataset, System.Threading.CancellationToken)"/> per format: the Microsoft extent is the tile of the path's quadkey; a GeoPackage extent is read from its R-tree only while the <c>gpkg_rtree_index</c> extension is registered for the geometry column, and contains every footprint the reader yields; a GeoParquet extent is the declared column <c>bbox</c>; everything that states no trustworthy extent answers null.
        /// <para>The GeoPackage and GeoParquet cases are built from the committed samples in <c>ReportsDirectory()</c>, because no sample carries an index or a declared box.</para>
        /// </summary>
        [Fact]
        public async Task SourceExtentAsync()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
            Assert.NotNull(directory_Reports);

            string directory = Path.Combine(directory_Reports, $"SourceExtentAsync-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            try
            {
                // Microsoft: the tile of the quadkey directory, in EPSG:4326; no quadkey directory, no extent.
                string path_Microsoft = Path.Combine(directory, "quadkey=120213101", "part-0.csv.gz");
                Directory.CreateDirectory(Path.GetDirectoryName(path_Microsoft)!);
                File.Copy(Core.xUnit.Query.FilePath(assembly, "Microsoft_Sample.csv.gz")!, path_Microsoft);

                SourceExtent? sourceExtent_Microsoft = await Query.SourceExtentAsync(path_Microsoft, BuildingDataset.Microsoft);
                Assert.NotNull(sourceExtent_Microsoft);
                Assert.Equal(4326, sourceExtent_Microsoft.Srid);
                Assert.Equal(Query.QuadkeyBoundingBox2D("120213101")!.Min.X, sourceExtent_Microsoft.BoundingBox2D!.Min.X);
                Assert.Null(await Query.SourceExtentAsync(Core.xUnit.Query.FilePath(assembly, "Microsoft_Sample.csv.gz"), BuildingDataset.Microsoft));

                // GeoPackage without an index: no extent.
                string path_GeoPackage = Path.Combine(directory, "eubucco.gpkg");
                File.Copy(Core.xUnit.Query.FilePath(assembly, "EUBUCCO_Sample.gpkg")!, path_GeoPackage);
                Assert.Null(await Query.SourceExtentAsync(path_GeoPackage, BuildingDataset.Eubucco));

                // The envelope of every footprint the reader yields: the extent must contain it.
                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;
                await foreach (Footprint footprint in Query.Footprints_GeoPackageAsync(path_GeoPackage, BuildingDataset.Eubucco)!)
                {
                    BoundingBox2D? boundingBox2D = (footprint.Geometry as PolygonalFace2D)?.GetBoundingBox();
                    Assert.NotNull(boundingBox2D);
                    minX = Math.Min(minX, boundingBox2D.Min.X);
                    minY = Math.Min(minY, boundingBox2D.Min.Y);
                    maxX = Math.Max(maxX, boundingBox2D.Max.X);
                    maxY = Math.Max(maxY, boundingBox2D.Max.Y);
                }

                // An index the extension is not registered for - registered only for another table, as a table
                // renamed after indexing leaves it: still no extent.
                await SourceExtent_ExecuteAsync(path_GeoPackage, string.Format(
                    CultureInfo.InvariantCulture,
                    "CREATE VIRTUAL TABLE rtree_eubucco_geom USING rtree(id, minx, maxx, miny, maxy); INSERT INTO rtree_eubucco_geom VALUES (1, {0}, {1}, {2}, {3}); CREATE TABLE gpkg_extensions (table_name TEXT, column_name TEXT, extension_name TEXT NOT NULL, definition TEXT NOT NULL, scope TEXT NOT NULL); INSERT INTO gpkg_extensions VALUES ('eubucco_old', 'geom', 'gpkg_rtree_index', 'http://www.geopackage.org/spec120/#extension_rtree', 'write-only');",
                    minX - 1, maxX + 1, minY - 1, maxY + 1));
                Assert.Null(await Query.SourceExtentAsync(path_GeoPackage, BuildingDataset.Eubucco));

                // Registered: the extent is the index root, in the file's EPSG:3035, and contains every footprint.
                await SourceExtent_ExecuteAsync(path_GeoPackage, "INSERT INTO gpkg_extensions VALUES ('eubucco', 'geom', 'gpkg_rtree_index', 'http://www.geopackage.org/spec120/#extension_rtree', 'write-only');");

                SourceExtent? sourceExtent_GeoPackage = await Query.SourceExtentAsync(path_GeoPackage, BuildingDataset.Eubucco);
                Assert.NotNull(sourceExtent_GeoPackage);
                Assert.Equal(3035, sourceExtent_GeoPackage.Srid);
                Assert.True(sourceExtent_GeoPackage.BoundingBox2D!.Min.X <= minX && sourceExtent_GeoPackage.BoundingBox2D.Min.Y <= minY);
                Assert.True(sourceExtent_GeoPackage.BoundingBox2D.Max.X >= maxX && sourceExtent_GeoPackage.BoundingBox2D.Max.Y >= maxY);

                // A GeoPackage read as CSV (the GHS-OBAT CSV branch) and a CSV file: no extent.
                Assert.Null(await Query.SourceExtentAsync(Core.xUnit.Query.FilePath(assembly, "GHS_OBAT_Sample.csv"), BuildingDataset.GhsObat));
                Assert.Null(await Query.SourceExtentAsync(Path.Combine(directory, "missing.gpkg"), BuildingDataset.Eubucco));

                // GeoParquet: the declared bbox, 2D or 3D; a missing, short or inverted one declares nothing.
                Assert.Null(await Query.SourceExtentAsync(Core.xUnit.Query.FilePath(assembly, "Overture_Sample.parquet"), BuildingDataset.Overture));

                SourceExtent? sourceExtent_Parquet = await Query.SourceExtentAsync(await SourceExtent_ParquetAsync(directory, "bbox2d.parquet", "[18.6, 49.29, 22.64, 52.38]"), BuildingDataset.Overture);
                Assert.NotNull(sourceExtent_Parquet);
                Assert.Equal(4326, sourceExtent_Parquet.Srid);
                Assert.Equal(18.6, sourceExtent_Parquet.BoundingBox2D!.Min.X);
                Assert.Equal(49.29, sourceExtent_Parquet.BoundingBox2D.Min.Y);
                Assert.Equal(22.64, sourceExtent_Parquet.BoundingBox2D.Max.X);
                Assert.Equal(52.38, sourceExtent_Parquet.BoundingBox2D.Max.Y);

                SourceExtent? sourceExtent_Parquet3D = await Query.SourceExtentAsync(await SourceExtent_ParquetAsync(directory, "bbox3d.parquet", "[18.6, 49.29, 0, 22.64, 52.38, 100]"), BuildingDataset.Overture);
                Assert.NotNull(sourceExtent_Parquet3D);
                Assert.Equal(22.64, sourceExtent_Parquet3D.BoundingBox2D!.Max.X);
                Assert.Equal(52.38, sourceExtent_Parquet3D.BoundingBox2D.Max.Y);

                Assert.Null(await Query.SourceExtentAsync(await SourceExtent_ParquetAsync(directory, "short.parquet", "[18.6, 49.29, 22.64]"), BuildingDataset.Overture));
                Assert.Null(await Query.SourceExtentAsync(await SourceExtent_ParquetAsync(directory, "inverted.parquet", "[22.64, 49.29, 18.6, 52.38]"), BuildingDataset.Overture));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Runs SQL against a GeoPackage copy, so a fact can give it the index and extension rows no committed sample carries.
        /// </summary>
        /// <param name="path">The GeoPackage copy.</param>
        /// <param name="commandText">The statements.</param>
        /// <returns>A task that completes when the statements have run.</returns>
        private static async Task SourceExtent_ExecuteAsync(string path, string commandText)
        {
            using SqliteConnection sqliteConnection = new(new SqliteConnectionStringBuilder() { DataSource = path, Pooling = false }.ConnectionString);
            await sqliteConnection.OpenAsync();

            using SqliteCommand sqliteCommand = new(commandText, sqliteConnection);
            await sqliteCommand.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Writes a minimal GeoParquet file - one WKB point - whose geometry column declares the given <c>bbox</c>.
        /// </summary>
        /// <param name="directory">The scratch directory.</param>
        /// <param name="fileName">The file name.</param>
        /// <param name="bbox">The JSON array written as the column's <c>bbox</c>.</param>
        /// <returns>A task whose result is the path of the file.</returns>
        private static async Task<string> SourceExtent_ParquetAsync(string directory, string fileName, string bbox)
        {
            string path = Path.Combine(directory, fileName);

            DataField<byte[]> dataField = new("geometry");
            ParquetSchema parquetSchema = new(dataField);

            byte[] point = new byte[21];
            point[0] = 1;
            BitConverter.GetBytes(1).CopyTo(point, 1);
            BitConverter.GetBytes(20.0).CopyTo(point, 5);
            BitConverter.GetBytes(50.0).CopyTo(point, 13);

            using FileStream fileStream = File.Create(path);
            using ParquetWriter parquetWriter = await ParquetWriter.CreateAsync(parquetSchema, fileStream);
            parquetWriter.CustomMetadata = new Dictionary<string, string>()
            {
                ["geo"] = "{\"version\":\"1.0.0\",\"primary_column\":\"geometry\",\"columns\":{\"geometry\":{\"encoding\":\"WKB\",\"geometry_types\":[\"Point\"],\"bbox\":" + bbox + "}}}",
            };

            using ParquetRowGroupWriter parquetRowGroupWriter = parquetWriter.CreateRowGroup();
            await parquetRowGroupWriter.WriteColumnAsync(new DataColumn(dataField, new byte[][] { point }));

            return path;
        }
    }
}
