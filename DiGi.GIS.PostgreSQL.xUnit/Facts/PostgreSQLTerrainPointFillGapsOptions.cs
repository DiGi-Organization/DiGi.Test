using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the defaults, which are what a repair uses when nothing is set.
        /// <para>The grid size is the one that matters here, and it defaults to 100 rather than to the 50 the sampling task uses. A repair measures what is stored against a lattice, so a spacing finer than the store actually holds makes every node in between read as a gap - which turns going back for a few thousand lost points into sampling the country again.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptions_Defaults()
        {
            PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions = new();

            Assert.Null(postgreSQLTerrainPointFillGapsOptions.CountyIds);
            Assert.Equal(100, postgreSQLTerrainPointFillGapsOptions.GridSize);
            Assert.Equal(1024, postgreSQLTerrainPointFillGapsOptions.BatchSize);
            Assert.Equal(0, postgreSQLTerrainPointFillGapsOptions.OriginX);
            Assert.Equal(0, postgreSQLTerrainPointFillGapsOptions.OriginY);
            Assert.Equal(128, postgreSQLTerrainPointFillGapsOptions.TileSize);
            Assert.Equal(16, postgreSQLTerrainPointFillGapsOptions.MaxConcurrentRequests);
            Assert.Equal(3, postgreSQLTerrainPointFillGapsOptions.RetryCount);
            Assert.Equal(500, postgreSQLTerrainPointFillGapsOptions.RetryDelayMilliseconds);
            Assert.Equal(Core.Constants.Tolerance.MacroDistance, postgreSQLTerrainPointFillGapsOptions.Tolerance);
        }

        /// <summary>
        /// Verifies that the lattice a repair measures against is anchored exactly where the sampling task anchors its own.
        /// <para>A different anchor describes a different lattice, on which every stored point is off grid and every node is missing. The two defaults have to agree, and nothing else in the code makes them.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptions_LatticeAgreesWithSampling()
        {
            PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions = new();
            PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions = new();

            Assert.Equal(postgreSQLTerrainPointUpdateOptions.OriginX, postgreSQLTerrainPointFillGapsOptions.OriginX);
            Assert.Equal(postgreSQLTerrainPointUpdateOptions.OriginY, postgreSQLTerrainPointFillGapsOptions.OriginY);
            Assert.Equal(postgreSQLTerrainPointUpdateOptions.TileSize, postgreSQLTerrainPointFillGapsOptions.TileSize);

            // The repair default has to be a whole multiple of the sampling default, or a county sampled at the
            // finer of the two could never be measured by it.
            Assert.Equal(0d, postgreSQLTerrainPointFillGapsOptions.GridSize % postgreSQLTerrainPointUpdateOptions.GridSize);
        }

        /// <summary>
        /// Verifies that a populated instance survives a JSON round trip and a clone, with every property carried over.
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptions_Serialization()
        {
            PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions = new()
            {
                BatchSize = 256,
                CountyIds = [55417, 56029, 53477],
                GridSize = 50,
                MaxConcurrentRequests = 24,
                OriginX = 5,
                OriginY = 5,
                RetryCount = 5,
                RetryDelayMilliseconds = 250,
                TileSize = 64,
                Tolerance = 0.25
            };

            string? json = Core.Convert.ToSystem_String(postgreSQLTerrainPointFillGapsOptions);
            Assert.NotNull(json);

            PostgreSQLTerrainPointFillGapsOptions? postgreSQLTerrainPointFillGapsOptions_Json = Core.Convert.ToDiGi<PostgreSQLTerrainPointFillGapsOptions>(json)?.FirstOrDefault();
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions_Json);
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions_Json.CountyIds);
            Assert.Equal(3, postgreSQLTerrainPointFillGapsOptions_Json.CountyIds.Count);
            Assert.Contains(55417, postgreSQLTerrainPointFillGapsOptions_Json.CountyIds);
            Assert.Equal(256, postgreSQLTerrainPointFillGapsOptions_Json.BatchSize);
            Assert.Equal(50, postgreSQLTerrainPointFillGapsOptions_Json.GridSize);
            Assert.Equal(24, postgreSQLTerrainPointFillGapsOptions_Json.MaxConcurrentRequests);
            Assert.Equal(5, postgreSQLTerrainPointFillGapsOptions_Json.OriginX);
            Assert.Equal(5, postgreSQLTerrainPointFillGapsOptions_Json.OriginY);
            Assert.Equal(5, postgreSQLTerrainPointFillGapsOptions_Json.RetryCount);
            Assert.Equal(250, postgreSQLTerrainPointFillGapsOptions_Json.RetryDelayMilliseconds);
            Assert.Equal(64, postgreSQLTerrainPointFillGapsOptions_Json.TileSize);
            Assert.Equal(0.25, postgreSQLTerrainPointFillGapsOptions_Json.Tolerance);

            PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions_Clone = new(postgreSQLTerrainPointFillGapsOptions);

            Assert.Equal(256, postgreSQLTerrainPointFillGapsOptions_Clone.BatchSize);
            Assert.Equal(50, postgreSQLTerrainPointFillGapsOptions_Clone.GridSize);
            Assert.Equal(24, postgreSQLTerrainPointFillGapsOptions_Clone.MaxConcurrentRequests);
            Assert.Equal(5, postgreSQLTerrainPointFillGapsOptions_Clone.OriginX);
            Assert.Equal(5, postgreSQLTerrainPointFillGapsOptions_Clone.OriginY);
            Assert.Equal(5, postgreSQLTerrainPointFillGapsOptions_Clone.RetryCount);
            Assert.Equal(250, postgreSQLTerrainPointFillGapsOptions_Clone.RetryDelayMilliseconds);
            Assert.Equal(64, postgreSQLTerrainPointFillGapsOptions_Clone.TileSize);
            Assert.Equal(0.25, postgreSQLTerrainPointFillGapsOptions_Clone.Tolerance);

            // The clone has to hold its own set, or editing one set of options would rewrite the other.
            Assert.NotNull(postgreSQLTerrainPointFillGapsOptions_Clone.CountyIds);
            Assert.Equal(3, postgreSQLTerrainPointFillGapsOptions_Clone.CountyIds.Count);
            Assert.NotSame(postgreSQLTerrainPointFillGapsOptions.CountyIds, postgreSQLTerrainPointFillGapsOptions_Clone.CountyIds);

            Core.xUnit.Query.SerializationCheck(postgreSQLTerrainPointFillGapsOptions);
        }

        /// <summary>
        /// Verifies that a null set of counties survives a round trip and a clone as null, which is what asks for every county rather than for none.
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointFillGapsOptions_NullCountyIds()
        {
            PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions = new()
            {
                CountyIds = null,
                GridSize = 100
            };

            PostgreSQLTerrainPointFillGapsOptions postgreSQLTerrainPointFillGapsOptions_Clone = new(postgreSQLTerrainPointFillGapsOptions);

            Assert.Null(postgreSQLTerrainPointFillGapsOptions_Clone.CountyIds);
            Assert.Equal(100, postgreSQLTerrainPointFillGapsOptions_Clone.GridSize);

            Core.xUnit.Query.SerializationCheck(postgreSQLTerrainPointFillGapsOptions);
        }
    }
}
