using DiGi.Core.IO.Table.Classes;
using DiGi.Unit.IO.Classes;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that Constants.Column defines the 35 External Components Area columns, that Create.Columns_ExternalComponentsArea returns them in the stable order, that Create.Column_ClosingTolerance returns the open-envelope signal of the same classification, and that their unique ids are distinct and disjoint from every pre-existing column.
        /// </summary>
        [Fact]
        public void Columns_ExternalComponentsArea()
        {
            List<Column> columns = IO.Create.Columns_ExternalComponentsArea();
            Assert.NotNull(columns);
            Assert.Equal(35, columns.Count);

            List<string> expectedNames =
            [
                "External north wall area",
                "External northeast wall area",
                "External east wall area",
                "External southeast wall area",
                "External south wall area",
                "External southwest wall area",
                "External west wall area",
                "External northwest wall area",
                "External flat roof area",
                "External north tilted roof area up to 20",
                "External northeast tilted roof area up to 20",
                "External east tilted roof area up to 20",
                "External southeast tilted roof area up to 20",
                "External south tilted roof area up to 20",
                "External southwest tilted roof area up to 20",
                "External west tilted roof area up to 20",
                "External northwest tilted roof area up to 20",
                "External north tilted roof area between 20 and 45",
                "External northeast tilted roof area between 20 and 45",
                "External east tilted roof area between 20 and 45",
                "External southeast tilted roof area between 20 and 45",
                "External south tilted roof area between 20 and 45",
                "External southwest tilted roof area between 20 and 45",
                "External west tilted roof area between 20 and 45",
                "External northwest tilted roof area between 20 and 45",
                "External north tilted roof area above 45",
                "External northeast tilted roof area above 45",
                "External east tilted roof area above 45",
                "External southeast tilted roof area above 45",
                "External south tilted roof area above 45",
                "External southwest tilted roof area above 45",
                "External west tilted roof area above 45",
                "External northwest tilted roof area above 45",
                "External floor area",
                "External components area"
            ];

            List<string> expectedUniqueIds =
            [
                "external_north_wall_area",
                "external_northeast_wall_area",
                "external_east_wall_area",
                "external_southeast_wall_area",
                "external_south_wall_area",
                "external_southwest_wall_area",
                "external_west_wall_area",
                "external_northwest_wall_area",
                "external_flat_roof_area",
                "external_north_tilted_roof_area_up_to_20",
                "external_northeast_tilted_roof_area_up_to_20",
                "external_east_tilted_roof_area_up_to_20",
                "external_southeast_tilted_roof_area_up_to_20",
                "external_south_tilted_roof_area_up_to_20",
                "external_southwest_tilted_roof_area_up_to_20",
                "external_west_tilted_roof_area_up_to_20",
                "external_northwest_tilted_roof_area_up_to_20",
                "external_north_tilted_roof_area_between_20_and_45",
                "external_northeast_tilted_roof_area_between_20_and_45",
                "external_east_tilted_roof_area_between_20_and_45",
                "external_southeast_tilted_roof_area_between_20_and_45",
                "external_south_tilted_roof_area_between_20_and_45",
                "external_southwest_tilted_roof_area_between_20_and_45",
                "external_west_tilted_roof_area_between_20_and_45",
                "external_northwest_tilted_roof_area_between_20_and_45",
                "external_north_tilted_roof_area_above_45",
                "external_northeast_tilted_roof_area_above_45",
                "external_east_tilted_roof_area_above_45",
                "external_southeast_tilted_roof_area_above_45",
                "external_south_tilted_roof_area_above_45",
                "external_southwest_tilted_roof_area_above_45",
                "external_west_tilted_roof_area_above_45",
                "external_northwest_tilted_roof_area_above_45",
                "external_floor_area",
                "external_components_area"
            ];

            List<string> names = [];
            foreach (Column column in columns)
            {
                Assert.False(string.IsNullOrWhiteSpace(column.Name));
                names.Add(column.Name);
            }

            Assert.Equal(expectedNames, names);

            List<string> uniqueIds = [];
            foreach (Column column in columns)
            {
                string? uniqueId = Core.IO.Query.UniqueId(column);
                Assert.False(string.IsNullOrWhiteSpace(uniqueId));
                uniqueIds.Add(uniqueId);
            }

            Assert.Equal(expectedUniqueIds, uniqueIds);
            Assert.Equal(35, uniqueIds.Distinct().Count());

            // The closing tolerance column of the same classification: a distance signal addressed by its own accessor, separate from the 35 area columns.
            Column column_ClosingTolerance = IO.Create.Column_ClosingTolerance();
            Assert.Equal("Closing tolerance", column_ClosingTolerance.Name);
            Assert.Equal("closing_tolerance", Core.IO.Query.UniqueId(column_ClosingTolerance));
            Assert.IsType<UnitColumn>(column_ClosingTolerance);
            Assert.Equal(typeof(float), ((ExtendedColumn)column_ClosingTolerance).Type);
            Assert.Equal("External Components Area", ((ExtendedColumn)column_ClosingTolerance).Category);
            Assert.Equal(Unit.Enums.UnitDataType.Float, ((UnitColumn)column_ClosingTolerance).UnitDataType);
            Assert.False(string.IsNullOrWhiteSpace(((ExtendedColumn)column_ClosingTolerance).Description));
            Assert.DoesNotContain("closing_tolerance", uniqueIds);

            Assert.DoesNotContain("floor_area", uniqueIds);
            Assert.DoesNotContain("total_area", uniqueIds);

            // NOTE: the Unit property is deliberately not asserted. The UnitColumn constructor stores it via
            // Core.Query.Clone(unit), but DiGi.Unit.Classes.Unit has no Unit(JsonObject) constructor, so the
            // serialization infrastructure cannot reconstruct it and the clone is null for EVERY UnitColumn
            // instance (verified 2026-09-17 on the pre-existing Column.FloorArea as well). Asserting it here
            // would encode a known DiGi.Unit defect into the contract of this column set; the constructor still
            // passes AreaUnit.SquareMeter, per the issue's definition of these columns.
            Assert.IsType<UnitColumn>(columns[0]);
            Assert.Equal(typeof(float), ((ExtendedColumn)columns[0]).Type);
            Assert.Equal("External Components Area", ((ExtendedColumn)columns[0]).Category);
            Assert.Equal(Unit.Enums.UnitDataType.Float, ((UnitColumn)columns[0]).UnitDataType);

            Assert.IsType<UnitColumn>(columns[34]);
            Assert.Equal(typeof(float), ((ExtendedColumn)columns[34]).Type);
            Assert.Equal("External Components Area", ((ExtendedColumn)columns[34]).Category);
            Assert.Equal(Unit.Enums.UnitDataType.Float, ((UnitColumn)columns[34]).UnitDataType);

            Assert.Equal("External wall area with outward normal azimuth in the north sector ([337.5°, 360°) ∪ [0°, 22.5°))", ((ExtendedColumn)columns[0]).Description);
            Assert.Equal("External roof area with tilt below 5 degrees (flat, no directional split)", ((ExtendedColumn)columns[8]).Description);
            Assert.Equal("External roof area with tilt from 5 up to 20 degrees, facing north ([5°, 20°])", ((ExtendedColumn)columns[9]).Description);
            Assert.Equal("External roof area with tilt greater than 45 degrees, facing northwest (> 45°)", ((ExtendedColumn)columns[32]).Description);
            Assert.Equal("External floor area (ground-facing external floor components)", ((ExtendedColumn)columns[33]).Description);
            Assert.Equal("Sum of all external wall, roof and floor component areas", ((ExtendedColumn)columns[34]).Description);

            foreach (Column column in columns)
            {
                Assert.False(string.IsNullOrWhiteSpace(((ExtendedColumn)column).Description));
            }

            List<Column> tiltedRoofColumns = columns.GetRange(9, 24);
            foreach (Column tiltedRoofColumn in tiltedRoofColumns)
            {
                Assert.Contains("degrees", ((ExtendedColumn)tiltedRoofColumn).Description);
            }

            // The 35 new fields are themselves members of Constants.Column, as is the closing tolerance column the classification records beside them;
            // the disjointness claim is against the pre-existing columns only.
            List<string> preExistingUniqueIds = [];
            foreach (FieldInfo fieldInfo in typeof(IO.Constants.Column).GetFields(BindingFlags.Public | BindingFlags.Static))
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

            // 39 columns before the Solar radiation set landed in Constants.Column (a7134d7 added 22 Solar columns, d4ec436 added 3 more),
            // and the 9 party wall area columns of DiGi-Organization/DiGi.GIS.Analytical#4.
            Assert.Equal(73, preExistingUniqueIds.Count);
            Assert.Empty(uniqueIds.Intersect(preExistingUniqueIds));

            List<Column> dynamicColumns =
            [
                IO.Create.Column_GridCellCoverage(0, 0),
                IO.Create.Column_OrthophotomapData(2019, 2020, "P", "X"),
                IO.Create.Column_OrthophotomapImage(2019),
                IO.Create.Column_RadialBuildingCoverageRatio(200),
                IO.Create.Column_RadialFloorAreaRatio(200),
                IO.Create.Column_PredictionYearBuit(IO.Constants.ColumnNamePrefix.PredictionConfidence, 2020),
                IO.Create.Column_Population(2020)
            ];

            List<string> dynamicUniqueIds = [];
            foreach (Column dynamicColumn in dynamicColumns)
            {
                string? uniqueId = Core.IO.Query.UniqueId(dynamicColumn);
                Assert.False(string.IsNullOrWhiteSpace(uniqueId));
                dynamicUniqueIds.Add(uniqueId);
            }

            Assert.Empty(uniqueIds.Intersect(dynamicUniqueIds));
        }
    }
}
