using DiGi.Core.IO.Table.Classes;
using DiGi.Unit.IO.Classes;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that Create.Columns_PartyWallArea returns the 9 party wall area columns in the order of the external wall columns, total last, in the External Components Area category, with unique ids distinct from each other and from the 35 external components area columns (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// </summary>
        [Fact]
        public void Columns_PartyWallArea()
        {
            List<Column> columns = IO.Create.Columns_PartyWallArea();
            Assert.Equal(9, columns.Count);

            List<string> expectedNames =
            [
                "Party north wall area",
                "Party northeast wall area",
                "Party east wall area",
                "Party southeast wall area",
                "Party south wall area",
                "Party southwest wall area",
                "Party west wall area",
                "Party northwest wall area",
                "Party wall area"
            ];

            Assert.Equal(expectedNames, columns.ConvertAll(x => x.Name ?? string.Empty));

            List<string> uniqueIds = columns.ConvertAll(x => Core.IO.Query.UniqueId(x) ?? string.Empty);
            Assert.Equal(["party_north_wall_area", "party_northeast_wall_area", "party_east_wall_area", "party_southeast_wall_area", "party_south_wall_area", "party_southwest_wall_area", "party_west_wall_area", "party_northwest_wall_area", "party_wall_area"], uniqueIds);
            Assert.Equal(9, uniqueIds.Distinct().Count());

            List<string> uniqueIds_External = IO.Create.Columns_ExternalComponentsArea().ConvertAll(x => Core.IO.Query.UniqueId(x) ?? string.Empty);
            Assert.Empty(uniqueIds.Intersect(uniqueIds_External));

            foreach (Column column in columns)
            {
                Assert.IsType<UnitColumn>(column);
                Assert.Equal(typeof(float), ((ExtendedColumn)column).Type);
                Assert.Equal("External Components Area", ((ExtendedColumn)column).Category);
                Assert.Equal(Unit.Enums.UnitDataType.Float, ((UnitColumn)column).UnitDataType);
                Assert.False(string.IsNullOrWhiteSpace(((ExtendedColumn)column).Description));
            }
        }
    }
}
