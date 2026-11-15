using DiGi.GIS.PostgreSQL.Solar.Classes;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the defaults of the batch solve options: every county, the isolated building and the 50 m and 100 m neighbour radii, resuming over stored results, the viewer's background-job ceilings (400 receivers, 32 000 caster triangles), 250 m tiles of at most 100 buildings and a 600 s statement timeout (DiGi.GIS.PostgreSQL#136).
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationSolveOptions_Defaults()
        {
            PostgreSQLBuildingSolarRadiationSolveOptions postgreSQLBuildingSolarRadiationSolveOptions = new();

            Assert.Null(postgreSQLBuildingSolarRadiationSolveOptions.CountyIds);
            Assert.Equal([0, 50, 100], postgreSQLBuildingSolarRadiationSolveOptions.Radii);
            Assert.True(postgreSQLBuildingSolarRadiationSolveOptions.SkipExisting);
            Assert.Equal(400, postgreSQLBuildingSolarRadiationSolveOptions.ReceiverCountMax);
            Assert.Equal(32_000, postgreSQLBuildingSolarRadiationSolveOptions.CasterTriangleCountMax);
            Assert.Equal(250, postgreSQLBuildingSolarRadiationSolveOptions.TileSize);
            Assert.Equal(100, postgreSQLBuildingSolarRadiationSolveOptions.BatchSize);
            Assert.Equal(600, postgreSQLBuildingSolarRadiationSolveOptions.CommandTimeout);
        }

        /// <summary>
        /// Verifies the batch solve options survive a string round trip and <c>SerializationCheck</c>, and that the copy constructor copies every member and holds its own county set and radius list.
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationSolveOptions_Serialization()
        {
            PostgreSQLBuildingSolarRadiationSolveOptions postgreSQLBuildingSolarRadiationSolveOptions = new()
            {
                BatchSize = 40,
                CasterTriangleCountMax = 64_000,
                CommandTimeout = 300,
                CountyIds = [1465, 1432],
                Radii = [0, 50, 200],
                ReceiverCountMax = 250,
                SkipExisting = false,
                TileSize = 150,
            };

            PostgreSQLBuildingSolarRadiationSolveOptions? postgreSQLBuildingSolarRadiationSolveOptions_Json = Core.Convert.ToDiGi<PostgreSQLBuildingSolarRadiationSolveOptions>(Core.Convert.ToSystem_String(postgreSQLBuildingSolarRadiationSolveOptions))?.FirstOrDefault();
            Assert.NotNull(postgreSQLBuildingSolarRadiationSolveOptions_Json);
            Assert.Equal([0, 50, 200], postgreSQLBuildingSolarRadiationSolveOptions_Json.Radii);
            Assert.Equal(64_000, postgreSQLBuildingSolarRadiationSolveOptions_Json.CasterTriangleCountMax);
            Assert.Equal(150, postgreSQLBuildingSolarRadiationSolveOptions_Json.TileSize);
            Assert.False(postgreSQLBuildingSolarRadiationSolveOptions_Json.SkipExisting);

            PostgreSQLBuildingSolarRadiationSolveOptions postgreSQLBuildingSolarRadiationSolveOptions_Clone = new(postgreSQLBuildingSolarRadiationSolveOptions);
            Assert.Equal(40, postgreSQLBuildingSolarRadiationSolveOptions_Clone.BatchSize);
            Assert.Equal(64_000, postgreSQLBuildingSolarRadiationSolveOptions_Clone.CasterTriangleCountMax);
            Assert.Equal(300, postgreSQLBuildingSolarRadiationSolveOptions_Clone.CommandTimeout);
            Assert.Equal(250, postgreSQLBuildingSolarRadiationSolveOptions_Clone.ReceiverCountMax);
            Assert.False(postgreSQLBuildingSolarRadiationSolveOptions_Clone.SkipExisting);
            Assert.Equal(150, postgreSQLBuildingSolarRadiationSolveOptions_Clone.TileSize);
            Assert.Equal(postgreSQLBuildingSolarRadiationSolveOptions.Radii, postgreSQLBuildingSolarRadiationSolveOptions_Clone.Radii);
            Assert.NotSame(postgreSQLBuildingSolarRadiationSolveOptions.Radii, postgreSQLBuildingSolarRadiationSolveOptions_Clone.Radii);
            Assert.Equal(postgreSQLBuildingSolarRadiationSolveOptions.CountyIds, postgreSQLBuildingSolarRadiationSolveOptions_Clone.CountyIds);
            Assert.NotSame(postgreSQLBuildingSolarRadiationSolveOptions.CountyIds, postgreSQLBuildingSolarRadiationSolveOptions_Clone.CountyIds);

            Core.xUnit.Query.SerializationCheck(postgreSQLBuildingSolarRadiationSolveOptions);
        }
    }
}
