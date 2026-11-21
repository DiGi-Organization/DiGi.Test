using DiGi.Core.IO.Table.Classes;
using DiGi.Unit.IO.Classes;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that Create.Columns_HeatDemand returns the 6 heat demand columns in a stable order, in the Heat Demand category, with unique ids distinct from each other and from every pre-existing column (DiGi-Organization/DiGi.GIS.IO#18); the method and the input variant that identify the result are int columns (DiGi-Organization/DiGi.GIS.IO#21).
        /// </summary>
        [Fact]
        public void Columns_HeatDemand()
        {
            List<Column> columns = IO.Create.Columns_HeatDemand();
            Assert.Equal(6, columns.Count);

            List<string> expectedNames =
            [
                "Heating energy need",
                "Heating energy need per area",
                "Design heat load",
                "Heat demand method version",
                "Heat demand method",
                "Heat demand input variant"
            ];

            Assert.Equal(expectedNames, columns.ConvertAll(x => x.Name ?? string.Empty));

            List<string> uniqueIds = columns.ConvertAll(x => Core.IO.Query.UniqueId(x) ?? string.Empty);
            Assert.Equal(["heating_energy_need", "heating_energy_need_per_area", "design_heat_load", "heat_demand_method_version", "heat_demand_method", "heat_demand_input_variant"], uniqueIds);
            Assert.Equal(6, uniqueIds.Distinct().Count());

            // The three energy and power columns are UnitColumns with Float precision; the version column is a plain ExtendedColumn of int.
            foreach (Column column in columns.GetRange(0, 3))
            {
                Assert.IsType<UnitColumn>(column);
                Assert.Equal(typeof(float), ((ExtendedColumn)column).Type);
                Assert.Equal("Heat Demand", ((ExtendedColumn)column).Category);
                Assert.Equal(Unit.Enums.UnitDataType.Float, ((UnitColumn)column).UnitDataType);
                Assert.False(string.IsNullOrWhiteSpace(((ExtendedColumn)column).Description));
            }

            // The version, the method and the input variant are plain ExtendedColumns of int: the integers of the enums, never their names.
            foreach (Column column in columns.GetRange(3, 3))
            {
                Assert.IsNotType<UnitColumn>(column);
                Assert.IsType<ExtendedColumn>(column);
                Assert.Equal(typeof(int), ((ExtendedColumn)column).Type);
                Assert.Equal("Heat Demand", ((ExtendedColumn)column).Category);
                Assert.False(string.IsNullOrWhiteSpace(((ExtendedColumn)column).Description));
            }

            // Disjointness from every pre-existing column.
            List<string> preExistingUniqueIds = [];
            foreach (System.Reflection.FieldInfo fieldInfo in typeof(IO.Constants.Column).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (fieldInfo.GetValue(null) is not Column column_Temp)
                {
                    continue;
                }

                string? uniqueId_Temp = Core.IO.Query.UniqueId(column_Temp);
                if (uniqueId_Temp == null || uniqueIds.Contains(uniqueId_Temp))
                {
                    continue;
                }

                preExistingUniqueIds.Add(uniqueId_Temp);
            }

            Assert.Empty(uniqueIds.Intersect(preExistingUniqueIds));
        }
    }
}
