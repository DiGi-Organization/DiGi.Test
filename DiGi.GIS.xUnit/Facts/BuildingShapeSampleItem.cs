using DiGi.Core;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Builds a building shape sample item with every member populated, asserts each property, round-trips the string form, asserts a direct copy, and runs the serialization check.
        /// </summary>
        [Fact]
        public void BuildingShapeSampleItem()
        {
            Classes.BuildingShapeSampleItem buildingShapeSampleItem = new("gis12-2026", 17, 1465, "0201_1.0001.123_BUD", Enums.BuildingShape.L);

            Assert.Equal("gis12-2026", buildingShapeSampleItem.SampleKey);
            Assert.Equal(17, buildingShapeSampleItem.Ordinal);
            Assert.Equal(1465, buildingShapeSampleItem.CountyId);
            Assert.Equal("0201_1.0001.123_BUD", buildingShapeSampleItem.Reference);
            Assert.Equal(Enums.BuildingShape.L, buildingShapeSampleItem.SolverShape);

            string? json = buildingShapeSampleItem.ToSystem_String();
            Assert.NotNull(json);

            Classes.BuildingShapeSampleItem? buildingShapeSampleItem_RoundTripped = Core.Convert.ToDiGi<Classes.BuildingShapeSampleItem>(json)?.FirstOrDefault();
            Assert.NotNull(buildingShapeSampleItem_RoundTripped);

            Assert.Equal(buildingShapeSampleItem.SampleKey, buildingShapeSampleItem_RoundTripped.SampleKey);
            Assert.Equal(buildingShapeSampleItem.Ordinal, buildingShapeSampleItem_RoundTripped.Ordinal);
            Assert.Equal(buildingShapeSampleItem.CountyId, buildingShapeSampleItem_RoundTripped.CountyId);
            Assert.Equal(buildingShapeSampleItem.Reference, buildingShapeSampleItem_RoundTripped.Reference);
            Assert.Equal(buildingShapeSampleItem.SolverShape, buildingShapeSampleItem_RoundTripped.SolverShape);

            //Copy constructor: Clone() (used by SerializationCheck) is a JSON round-trip, not the copy constructor, so the copy leg needs its own direct assertion.
            Classes.BuildingShapeSampleItem buildingShapeSampleItem_Copy = new(buildingShapeSampleItem);

            Assert.Equal(buildingShapeSampleItem.SampleKey, buildingShapeSampleItem_Copy.SampleKey);
            Assert.Equal(buildingShapeSampleItem.Ordinal, buildingShapeSampleItem_Copy.Ordinal);
            Assert.Equal(buildingShapeSampleItem.CountyId, buildingShapeSampleItem_Copy.CountyId);
            Assert.Equal(buildingShapeSampleItem.Reference, buildingShapeSampleItem_Copy.Reference);
            Assert.Equal(buildingShapeSampleItem.SolverShape, buildingShapeSampleItem_Copy.SolverShape);

            Core.xUnit.Query.SerializationCheck(buildingShapeSampleItem);
        }
    }
}
