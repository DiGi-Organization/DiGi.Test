using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the defaults, which are what a run uses when nothing is set.
        /// <para>The grid size is the one that governs the cost of a whole run - it enters as a square, so 50 rather than 10 is the difference between days and most of a year - and the origin has to stay at zero for a coarse sampling to be reusable by a finer one.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointUpdateOptions_Defaults()
        {
            PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions = new();

            Assert.Null(postgreSQLTerrainPointUpdateOptions.CountyIds);
            Assert.Equal(50, postgreSQLTerrainPointUpdateOptions.GridSize);
            Assert.Equal(0, postgreSQLTerrainPointUpdateOptions.OriginX);
            Assert.Equal(0, postgreSQLTerrainPointUpdateOptions.OriginY);
            Assert.Equal(128, postgreSQLTerrainPointUpdateOptions.TileSize);
            Assert.Equal(16, postgreSQLTerrainPointUpdateOptions.MaxConcurrentRequests);
            Assert.Equal(3, postgreSQLTerrainPointUpdateOptions.RetryCount);
            Assert.Equal(500, postgreSQLTerrainPointUpdateOptions.RetryDelayMilliseconds);
            Assert.False(postgreSQLTerrainPointUpdateOptions.OverrideExisting);
            Assert.Equal(Core.Constants.Tolerance.MacroDistance, postgreSQLTerrainPointUpdateOptions.Tolerance);
        }

        /// <summary>
        /// Verifies that a populated instance survives a JSON round trip and a clone, with every property carried over.
        /// <para>The tolerance is asserted on the clone in particular: the copy constructor used to leave it out, so a copied set of options silently reverted to the default while every other value came across intact.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointUpdateOptions_Serialization()
        {
            PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions = new()
            {
                CountyIds = [2405, 2412, 1465],
                GridSize = 10,
                MaxConcurrentRequests = 24,
                OriginX = 5,
                OriginY = 5,
                OverrideExisting = true,
                RetryCount = 5,
                RetryDelayMilliseconds = 250,
                TileSize = 64,
                Tolerance = 0.25
            };

            string? json = Core.Convert.ToSystem_String(postgreSQLTerrainPointUpdateOptions);
            Assert.NotNull(json);

            PostgreSQLTerrainPointUpdateOptions? postgreSQLTerrainPointUpdateOptions_Json = Core.Convert.ToDiGi<PostgreSQLTerrainPointUpdateOptions>(json)?.FirstOrDefault();
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions_Json);
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions_Json.CountyIds);
            Assert.Equal(3, postgreSQLTerrainPointUpdateOptions_Json.CountyIds.Count);
            Assert.Contains(2412, postgreSQLTerrainPointUpdateOptions_Json.CountyIds);
            Assert.Equal(10, postgreSQLTerrainPointUpdateOptions_Json.GridSize);
            Assert.Equal(24, postgreSQLTerrainPointUpdateOptions_Json.MaxConcurrentRequests);
            Assert.Equal(5, postgreSQLTerrainPointUpdateOptions_Json.OriginX);
            Assert.Equal(5, postgreSQLTerrainPointUpdateOptions_Json.OriginY);
            Assert.True(postgreSQLTerrainPointUpdateOptions_Json.OverrideExisting);
            Assert.Equal(5, postgreSQLTerrainPointUpdateOptions_Json.RetryCount);
            Assert.Equal(250, postgreSQLTerrainPointUpdateOptions_Json.RetryDelayMilliseconds);
            Assert.Equal(64, postgreSQLTerrainPointUpdateOptions_Json.TileSize);
            Assert.Equal(0.25, postgreSQLTerrainPointUpdateOptions_Json.Tolerance);

            PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions_Clone = new(postgreSQLTerrainPointUpdateOptions);

            Assert.Equal(0.25, postgreSQLTerrainPointUpdateOptions_Clone.Tolerance);
            Assert.Equal(10, postgreSQLTerrainPointUpdateOptions_Clone.GridSize);
            Assert.Equal(24, postgreSQLTerrainPointUpdateOptions_Clone.MaxConcurrentRequests);
            Assert.Equal(5, postgreSQLTerrainPointUpdateOptions_Clone.OriginX);
            Assert.Equal(5, postgreSQLTerrainPointUpdateOptions_Clone.OriginY);
            Assert.True(postgreSQLTerrainPointUpdateOptions_Clone.OverrideExisting);
            Assert.Equal(5, postgreSQLTerrainPointUpdateOptions_Clone.RetryCount);
            Assert.Equal(250, postgreSQLTerrainPointUpdateOptions_Clone.RetryDelayMilliseconds);
            Assert.Equal(64, postgreSQLTerrainPointUpdateOptions_Clone.TileSize);

            // The clone has to hold its own set, or editing one set of options would rewrite the other.
            Assert.NotNull(postgreSQLTerrainPointUpdateOptions_Clone.CountyIds);
            Assert.Equal(3, postgreSQLTerrainPointUpdateOptions_Clone.CountyIds.Count);
            Assert.NotSame(postgreSQLTerrainPointUpdateOptions.CountyIds, postgreSQLTerrainPointUpdateOptions_Clone.CountyIds);

            Core.xUnit.Query.SerializationCheck(postgreSQLTerrainPointUpdateOptions);
        }

        /// <summary>
        /// Verifies that a null set of counties survives a round trip and a clone as null, which is what asks for every county rather than for none.
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointUpdateOptions_NullCountyIds()
        {
            PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions = new()
            {
                CountyIds = null,
                GridSize = 100
            };

            PostgreSQLTerrainPointUpdateOptions postgreSQLTerrainPointUpdateOptions_Clone = new(postgreSQLTerrainPointUpdateOptions);

            Assert.Null(postgreSQLTerrainPointUpdateOptions_Clone.CountyIds);
            Assert.Equal(100, postgreSQLTerrainPointUpdateOptions_Clone.GridSize);

            Core.xUnit.Query.SerializationCheck(postgreSQLTerrainPointUpdateOptions);
        }

        /// <summary>
        /// Verifies that the grid sizes meant to be used together nest, so a county sampled coarsely can later be sampled finely without paying for the points it already holds.
        /// <para>This is a property of the numbers rather than of any one method, and it is the reason the options say to keep every grid size a whole multiple of the finest one intended.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLTerrainPointUpdateOptions_GridSizesNest()
        {
            double[] gridSizes = [100, 50, 10];

            for (int i = 0; i < gridSizes.Length - 1; i++)
            {
                Assert.Equal(0d, gridSizes[i] % gridSizes[i + 1]);
            }

            // A size sharing only a smaller factor does not nest, which is what the guidance warns against.
            Assert.NotEqual(0d, 100d % 30d);
        }
    }
}
