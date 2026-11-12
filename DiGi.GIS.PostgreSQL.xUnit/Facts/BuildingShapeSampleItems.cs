using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the stratified draw of a building shape labelling sample (DiGi.GIS.PostgreSQL#135) on synthetic candidates: 300 Square, 200 Rectangular, 10 L, 3 O and 50 Undefined.
        /// <para>With size 100 the four defined classes get a quota of 25: L and O are taken whole (10 and 3), Square and Rectangular get 25 each, and the 37 left over are spread over their spares (275 and 175) by largest remainder - 22.61 and 14.39 - so Square ends at 48 and Rectangular at 39. Undefined candidates and a duplicated (county, reference) are dropped, ordinals run 0…99, the order does not depend on the input order, a different seed changes it, and a size above the candidate count takes every candidate.</para>
        /// </summary>
        [Fact]
        public void BuildingShapeSampleItems()
        {
            List<BuildingShapeSampleItem> buildingShapeSampleItems_Candidate = [];

            void AddCandidates(BuildingShape buildingShape, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    buildingShapeSampleItems_Candidate.Add(new BuildingShapeSampleItem(null, 0, 1400 + (i % 3), $"{buildingShape}-{i:D4}", buildingShape));
                }
            }

            AddCandidates(BuildingShape.Square, 300);
            AddCandidates(BuildingShape.Rectangular, 200);
            AddCandidates(BuildingShape.L, 10);
            AddCandidates(BuildingShape.O, 3);
            AddCandidates(BuildingShape.Undefined, 50);

            // A duplicate of an existing (county, reference) pair is dropped, whatever its solver shape.
            buildingShapeSampleItems_Candidate.Add(new BuildingShapeSampleItem(null, 0, 1400, "L-0000", BuildingShape.L));

            List<BuildingShapeSampleItem>? buildingShapeSampleItems = Create.BuildingShapeSampleItems(buildingShapeSampleItems_Candidate, "xunit-sample", 100, 12);
            Assert.NotNull(buildingShapeSampleItems);
            Assert.Equal(100, buildingShapeSampleItems.Count);

            Dictionary<BuildingShape, int> counts = buildingShapeSampleItems.GroupBy(x => x.SolverShape).ToDictionary(x => x.Key, x => x.Count());
            Assert.Equal(48, counts[BuildingShape.Square]);
            Assert.Equal(39, counts[BuildingShape.Rectangular]);
            Assert.Equal(10, counts[BuildingShape.L]);
            Assert.Equal(3, counts[BuildingShape.O]);
            Assert.False(counts.ContainsKey(BuildingShape.Undefined));

            Assert.Equal(Enumerable.Range(0, 100), buildingShapeSampleItems.Select(x => x.Ordinal));
            Assert.All(buildingShapeSampleItems, x => Assert.Equal("xunit-sample", x.SampleKey));
            Assert.Equal(100, buildingShapeSampleItems.Select(x => (x.CountyId, x.Reference)).Distinct().Count());

            // Deterministic for the same seed, whatever the input order; another seed gives another order.
            List<BuildingShapeSampleItem>? buildingShapeSampleItems_Reversed = Create.BuildingShapeSampleItems(Enumerable.Reverse(buildingShapeSampleItems_Candidate), "xunit-sample", 100, 12);
            Assert.NotNull(buildingShapeSampleItems_Reversed);
            Assert.Equal(buildingShapeSampleItems.Select(x => (x.CountyId, x.Reference)), buildingShapeSampleItems_Reversed.Select(x => (x.CountyId, x.Reference)));

            List<BuildingShapeSampleItem>? buildingShapeSampleItems_OtherSeed = Create.BuildingShapeSampleItems(buildingShapeSampleItems_Candidate, "xunit-sample", 100, 13);
            Assert.NotNull(buildingShapeSampleItems_OtherSeed);
            Assert.NotEqual(buildingShapeSampleItems.Select(x => (x.CountyId, x.Reference)), buildingShapeSampleItems_OtherSeed.Select(x => (x.CountyId, x.Reference)));

            // A size above the 513 distinct defined candidates takes every one of them.
            List<BuildingShapeSampleItem>? buildingShapeSampleItems_All = Create.BuildingShapeSampleItems(buildingShapeSampleItems_Candidate, "xunit-sample", 10000, 12);
            Assert.NotNull(buildingShapeSampleItems_All);
            Assert.Equal(513, buildingShapeSampleItems_All.Count);

            Assert.Empty(Create.BuildingShapeSampleItems([new BuildingShapeSampleItem(null, 0, 1400, "U-0000", BuildingShape.Undefined)], "xunit-sample", 100, 12)!);
            Assert.Null(Create.BuildingShapeSampleItems(null, "xunit-sample", 100, 12));
            Assert.Null(Create.BuildingShapeSampleItems(buildingShapeSampleItems_Candidate, " ", 100, 12));
            Assert.Null(Create.BuildingShapeSampleItems(buildingShapeSampleItems_Candidate, "xunit-sample", 0, 12));
        }
    }
}
