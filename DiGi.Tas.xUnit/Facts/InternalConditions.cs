using DiGi.Core.Parameter.Classes;
using DiGi.Tas.Core.Classes;
using DiGi.Tas.TIC.Enums;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.Tas.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reads internal conditions from a folder tree with nested child folders: Root (A) → Child_1 (B) → Grand (C), and Root → Child_2 (D).
        /// <para>Verifies that the traversal terminates, returns every condition exactly once, records the full folder path of a nested condition, and returns only the root conditions when nesting is disabled. Reference: DiGi.Tas#1 — the child-folder loop recursed on the parent folder and never advanced, so any folder with a child folder never returned.</para>
        /// </summary>
        [Fact]
        public void InternalConditions()
        {
            TestCallBudget testCallBudget = new(100);

            TestInternalConditionFolder testInternalConditionFolder_Root = new("Root", testCallBudget);
            testInternalConditionFolder_Root.AddInternalCondition("A");

            TestInternalConditionFolder testInternalConditionFolder_Child_1 = testInternalConditionFolder_Root.AddChildFolder("Child_1");
            testInternalConditionFolder_Child_1.AddInternalCondition("B");

            TestInternalConditionFolder testInternalConditionFolder_Grand = testInternalConditionFolder_Child_1.AddChildFolder("Grand");
            testInternalConditionFolder_Grand.AddInternalCondition("C");

            TestInternalConditionFolder testInternalConditionFolder_Child_2 = testInternalConditionFolder_Root.AddChildFolder("Child_2");
            testInternalConditionFolder_Child_2.AddInternalCondition("D");

            List<Analytical.Building.Classes.InternalCondition>? internalConditions = TIC.Create.InternalConditions(testInternalConditionFolder_Root, true);
            Assert.NotNull(internalConditions);
            Assert.Equal(["A", "B", "C", "D"], internalConditions.ConvertAll(x => x.Name).OrderBy(x => x));

            Analytical.Building.Classes.InternalCondition internalCondition_C = internalConditions.Single(x => x.Name == "C");
            Assert.True(internalCondition_C.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.FolderPath), out FolderPath? folderPath_C, new GetValueSettings(true, false)));
            Assert.NotNull(folderPath_C);
            Assert.Equal(["Grand", "Child_1", "Root"], folderPath_C.GetNames());

            List<Analytical.Building.Classes.InternalCondition>? internalConditions_NotNested = TIC.Create.InternalConditions(testInternalConditionFolder_Root, false);
            Assert.NotNull(internalConditions_NotNested);
            Assert.Equal(["A"], internalConditions_NotNested.ConvertAll(x => x.Name));
        }
    }
}
