using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the party wall overload of <see cref="Modify.Update_ExternalComponentsArea(Table?, IEnumerable{BuildingModel}?, IEnumerable{DiGi.Analytical.Building.Classes.PartyWallResult?}?)"/> files the shared area of a wall under that wall's sector and leaves the gross wall columns alone (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// <para>The box's east wall shares 40 m2: the party east column reads 40, the total 40, every other party column 0, and the external east column still reads the gross 100. Two results for the same wall summing to 150 m2 are capped at the wall's 100. With the party walls null the party columns are not written at all; with an empty collection they are written as 0.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_PartyWalls()
        {
            string[] columnNames_Party =
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

            IComponent[] components = Box();
            IComponent wall_East = System.Array.Find(components, x => x is IWall && x.GetBoundingBox()?.Min.X > 9.9)!;
            Assert.NotNull(wall_East);

            BuildingModel model = Model("ref_party", 5, components);

            DiGi.Analytical.Building.Classes.PartyWallResult partyWallResult = new(new Core.Classes.GuidReference(wall_East), null, null, null, 40.0);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model], [partyWallResult]);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(i == 2 ? 40.0 : 0.0, Value(table, row, columnNames_Party[i]), 1e-3);
            }

            Assert.Equal(40.0, Value(table, row, columnNames_Party[8]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[2]), 1e-3);
            Assert.Equal(600.0, Value(table, row, ColumnNames_External[34]), 1e-3);

            // Two results for the same wall summing to more than the wall are capped at the wall.
            Table table_Capped = new();
            Modify.Update_ExternalComponentsArea(table_Capped, [model], [partyWallResult, new DiGi.Analytical.Building.Classes.PartyWallResult(new Core.Classes.GuidReference(wall_East), null, null, null, 110.0)]);
            Assert.Equal(100.0, Value(table_Capped, table_Capped.GetRow(0)!, columnNames_Party[2]), 1e-3);

            // Null leaves the party columns alone; empty writes them as 0.
            Table table_Null = new();
            Modify.Update_ExternalComponentsArea(table_Null, [model], null);
            Assert.False(table_Null.TryGetColumn(columnNames_Party[8], out Column? _));

            Table table_Empty = new();
            Modify.Update_ExternalComponentsArea(table_Empty, [model], []);
            Assert.True(table_Empty.TryGetColumn(columnNames_Party[8], out Column? column_Total));
            Assert.Equal(0f, table_Empty.GetRow(0)!.GetValue<float?>(column_Total!.Index));
        }
    }
}
