using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the defaults, which are what an unattended run uses when nothing is set.
        /// <para>The timeout is well above the 30 second Npgsql default on purpose: a county can carry tens of thousands of building models, and the run reads and writes every one of them. A null county set scopes a run to the whole country.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptions_Defaults()
        {
            PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions = new();

            Assert.Equal(600, postgreSQLBuildingDataExternalComponentsUpdateOptions.CommandTimeout);
            Assert.Null(postgreSQLBuildingDataExternalComponentsUpdateOptions.CountyIds);
            Assert.True(postgreSQLBuildingDataExternalComponentsUpdateOptions.PartyWalls);
            Assert.Equal(0.2, postgreSQLBuildingDataExternalComponentsUpdateOptions.PartyWallMaxDistance);
        }

        /// <summary>
        /// Verifies that a populated instance survives a JSON round trip and a clone, with every property carried over.
        /// <para>The county set matters most here: it travels as a collection of identifiers, and a round trip that dropped one of them would silently leave a county out of an unattended run. The clone has to hold its own collection, or editing one set of options would rewrite the other.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptions_Serialization()
        {
            PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions = new()
            {
                CommandTimeout = 120,
                CountyIds = [55417, 56029, 53477],
                PartyWalls = false,
                PartyWallMaxDistance = 0.15
            };

            string? text = Core.Convert.ToSystem_String(postgreSQLBuildingDataExternalComponentsUpdateOptions);
            Assert.False(string.IsNullOrWhiteSpace(text));

            PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed = Core.Convert.ToDiGi<PostgreSQLBuildingDataExternalComponentsUpdateOptions>(text)?.FirstOrDefault();
            Assert.NotNull(postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed);

            Assert.Equal(120, postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed.CommandTimeout);
            Assert.False(postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed.PartyWalls);
            Assert.Equal(0.15, postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed.PartyWallMaxDistance);

            HashSet<int>? countyIds = postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed.CountyIds;
            Assert.NotNull(countyIds);
            Assert.Equal(3, countyIds.Count);
            Assert.Contains(55417, countyIds);
            Assert.Contains(56029, countyIds);
            Assert.Contains(53477, countyIds);

            PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions_Clone = new(postgreSQLBuildingDataExternalComponentsUpdateOptions);

            Assert.Equal(120, postgreSQLBuildingDataExternalComponentsUpdateOptions_Clone.CommandTimeout);
            Assert.False(postgreSQLBuildingDataExternalComponentsUpdateOptions_Clone.PartyWalls);
            Assert.Equal(0.15, postgreSQLBuildingDataExternalComponentsUpdateOptions_Clone.PartyWallMaxDistance);
            Assert.NotNull(postgreSQLBuildingDataExternalComponentsUpdateOptions_Clone.CountyIds);
            Assert.Equal(3, postgreSQLBuildingDataExternalComponentsUpdateOptions_Clone.CountyIds.Count);
            Assert.NotSame(postgreSQLBuildingDataExternalComponentsUpdateOptions.CountyIds, postgreSQLBuildingDataExternalComponentsUpdateOptions_Clone.CountyIds);

            Core.xUnit.Query.SerializationCheck(postgreSQLBuildingDataExternalComponentsUpdateOptions);
        }

        /// <summary>
        /// Verifies that an unset county collection stays unset through a round trip.
        /// <para>Null and empty mean opposite things here: null scopes a run to every county in the country, an empty set scopes it to none. A round trip that turned one into the other would silently change what an unattended run covers.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateOptions_Serialization_NullCountyIds()
        {
            PostgreSQLBuildingDataExternalComponentsUpdateOptions postgreSQLBuildingDataExternalComponentsUpdateOptions = new()
            {
                CountyIds = null
            };

            string? text = Core.Convert.ToSystem_String(postgreSQLBuildingDataExternalComponentsUpdateOptions);
            PostgreSQLBuildingDataExternalComponentsUpdateOptions? postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed = Core.Convert.ToDiGi<PostgreSQLBuildingDataExternalComponentsUpdateOptions>(text)?.FirstOrDefault();

            Assert.NotNull(postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed);
            Assert.Null(postgreSQLBuildingDataExternalComponentsUpdateOptions_Parsed.CountyIds);

            Core.xUnit.Query.SerializationCheck(postgreSQLBuildingDataExternalComponentsUpdateOptions);
        }
    }
}
