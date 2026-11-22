using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the JSON round trip, the clone paths and the copy constructor of <see cref="PopulationGridCell"/>: identifier, census year, population and the EPSG:2180 outline survive, and a masked cell keeps its null population rather than a zero standing in for it.
        /// </summary>
        [Fact]
        public void PopulationGridCell_Serialization()
        {
            PolygonalFace2D? polygonalFace2D = DiGi.Geometry.Planar.Create.PolygonalFace2D(new Point2D(0, 0), new Point2D(1000, 0), new Point2D(1000, 1000), new Point2D(0, 1000), new Point2D(0, 0));
            Assert.NotNull(polygonalFace2D);

            PopulationGridCell populationGridCell = new("XUNIT-GRID-CELL", 2021, 4321, polygonalFace2D);
            Core.xUnit.Query.SerializationCheck(populationGridCell);

            PopulationGridCell populationGridCell_Copy = new(populationGridCell);
            Assert.Equal("XUNIT-GRID-CELL", populationGridCell_Copy.CellId);
            Assert.Equal(2021, populationGridCell_Copy.CensusYear);
            Assert.Equal(4321, populationGridCell_Copy.Population);
            Assert.NotNull(populationGridCell_Copy.PolygonalFace2D);
            Assert.Equal(0d, populationGridCell_Copy.PolygonalFace2D.GetBoundingBox()!.Min.X);
            Assert.Equal(1000d, populationGridCell_Copy.PolygonalFace2D.GetBoundingBox()!.Max.Y);

            PopulationGridCell populationGridCell_Masked = new("XUNIT-GRID-CELL-MASKED", 2021, null, polygonalFace2D);
            Core.xUnit.Query.SerializationCheck(populationGridCell_Masked);
            Assert.Null(new PopulationGridCell(populationGridCell_Masked).Population);
        }

        /// <summary>
        /// Verifies the JSON round trip, the clone paths and the copy constructor of <see cref="PopulationGridComparisonItem"/>: every per-cell figure survives, and a masked row keeps its null census population and null error.
        /// </summary>
        [Fact]
        public void PopulationGridComparisonItem_Serialization()
        {
            PopulationGridComparisonItem populationGridComparisonItem = new("XUNIT-GRID-ITEM", 500, 480, -20, 3, Enums.PopulationDensityClass.Medium);
            Core.xUnit.Query.SerializationCheck(populationGridComparisonItem);

            PopulationGridComparisonItem populationGridComparisonItem_Copy = new(populationGridComparisonItem);
            Assert.Equal("XUNIT-GRID-ITEM", populationGridComparisonItem_Copy.CellId);
            Assert.Equal(500, populationGridComparisonItem_Copy.CensusPopulation);
            Assert.Equal(480, populationGridComparisonItem_Copy.ModelledPopulation);
            Assert.Equal(-20, populationGridComparisonItem_Copy.Error);
            Assert.Equal(3, populationGridComparisonItem_Copy.BuildingCount);
            Assert.Equal(Enums.PopulationDensityClass.Medium, populationGridComparisonItem_Copy.DensityClass);

            PopulationGridComparisonItem populationGridComparisonItem_Masked = new("XUNIT-GRID-ITEM-MASKED", null, 40, null, 1, Enums.PopulationDensityClass.Masked);
            Core.xUnit.Query.SerializationCheck(populationGridComparisonItem_Masked);

            PopulationGridComparisonItem populationGridComparisonItem_Masked_Copy = new(populationGridComparisonItem_Masked);
            Assert.Null(populationGridComparisonItem_Masked_Copy.CensusPopulation);
            Assert.Null(populationGridComparisonItem_Masked_Copy.Error);
            Assert.Equal(Enums.PopulationDensityClass.Masked, populationGridComparisonItem_Masked_Copy.DensityClass);
        }

        /// <summary>
        /// Verifies the JSON round trip, the clone paths and the copy constructor of <see cref="PopulationGridComparisonResult"/>: the county, the census year, the three metrics, the three counts and every per-cell row survive, the masked row among them.
        /// </summary>
        [Fact]
        public void PopulationGridComparisonResult_Serialization()
        {
            List<PopulationGridComparisonItem> populationGridComparisonItems =
            [
                new("XUNIT-GRID-ITEM", 500, 480, -20, 3, Enums.PopulationDensityClass.Medium),
                new("XUNIT-GRID-ITEM-MASKED", null, 40, null, 1, Enums.PopulationDensityClass.Masked),
            ];

            PopulationGridComparisonResult populationGridComparisonResult = new(990106, 2021, 12.5, -3.25, 0.75, 1, 1, 2, populationGridComparisonItems);
            Core.xUnit.Query.SerializationCheck(populationGridComparisonResult);

            PopulationGridComparisonResult populationGridComparisonResult_Copy = new(populationGridComparisonResult);
            Assert.Equal(990106, populationGridComparisonResult_Copy.CountyId);
            Assert.Equal(2021, populationGridComparisonResult_Copy.CensusYear);
            Assert.Equal(12.5, populationGridComparisonResult_Copy.Mae);
            Assert.Equal(-3.25, populationGridComparisonResult_Copy.Bias);
            Assert.Equal(0.75, populationGridComparisonResult_Copy.R2);
            Assert.Equal(1, populationGridComparisonResult_Copy.ComparedCellCount);
            Assert.Equal(1, populationGridComparisonResult_Copy.MaskedCellCount);
            Assert.Equal(2, populationGridComparisonResult_Copy.BuildingOutsideCellCount);

            Assert.NotNull(populationGridComparisonResult_Copy.Items);
            Assert.Equal(2, populationGridComparisonResult_Copy.Items.Count);
            Assert.Equal("XUNIT-GRID-ITEM", populationGridComparisonResult_Copy.Items[0].CellId);
            Assert.Equal(500, populationGridComparisonResult_Copy.Items[0].CensusPopulation);
            Assert.Null(populationGridComparisonResult_Copy.Items[1].CensusPopulation);
        }
    }
}
