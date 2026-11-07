using DiGi.GIS.WebAPI.Classes;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.TerrainPointDensityResultsAsync"/> answers null, without sending anything, when there is no manager or nothing to measure.
        /// <para>Null is what the terrain point options windows read as "the densities could not be read", which annotates no county and says so (DiGi.GIS.PostgreSQL.UI#41). An empty dictionary would read as "measured, and nothing found" - the false answer the window must never show. Identifiers that are not positive are dropped first, so a list of only those is nothing to measure too.</para>
        /// </summary>
        [Fact]
        public async Task TerrainPointDensityResultsAsync()
        {
            GISWebAPIManager gISWebAPIManager = new(null);

            Assert.Null(await Query.TerrainPointDensityResultsAsync(null, [22138], null));
            Assert.Null(await Query.TerrainPointDensityResultsAsync(gISWebAPIManager, null, null));
            Assert.Null(await Query.TerrainPointDensityResultsAsync(gISWebAPIManager, [], 50.0));
            Assert.Null(await Query.TerrainPointDensityResultsAsync(gISWebAPIManager, [0, -1], 50.0));
        }
    }
}
