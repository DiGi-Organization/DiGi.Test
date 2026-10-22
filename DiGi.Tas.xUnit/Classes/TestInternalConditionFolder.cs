using System.Collections.Generic;

namespace DiGi.Tas.xUnit
{
    /// <summary>
    /// Managed test double of the TIC <c>InternalConditionFolder</c> COM interface. Indices are one-based, as in TIC, and an index past the end returns null. Every indexed read spends one call of the shared <see cref="TestCallBudget"/>.
    /// </summary>
    public class TestInternalConditionFolder : global::TIC.InternalConditionFolder
    {
        private readonly TestCallBudget testCallBudget;
        private readonly List<TestInternalConditionFolder> testInternalConditionFolders = [];
        private readonly List<TestInternalCondition> testInternalConditions = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="TestInternalConditionFolder"/> class with the specified name and call budget.
        /// </summary>
        /// <param name="name">The name of the folder.</param>
        /// <param name="testCallBudget">The call budget shared by the whole folder tree.</param>
        public TestInternalConditionFolder(string name, TestCallBudget testCallBudget)
        {
            this.name = name;
            this.testCallBudget = testCallBudget;
        }

        /// <summary>
        /// Gets or sets the name of the folder.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Gets or sets the description of the folder.
        /// </summary>
        public string description { get; set; } = string.Empty;

        /// <summary>
        /// Adds a child folder named "Folder" and returns it.
        /// </summary>
        /// <returns>The new child folder.</returns>
        public global::TIC.InternalConditionFolder AddChildFolder()
        {
            return AddChildFolder("Folder");
        }

        /// <summary>
        /// Adds a child folder with the specified name and returns it.
        /// </summary>
        /// <param name="name">The name of the child folder.</param>
        /// <returns>The new child folder.</returns>
        public TestInternalConditionFolder AddChildFolder(string name)
        {
            TestInternalConditionFolder testInternalConditionFolder = new(name, testCallBudget);
            testInternalConditionFolders.Add(testInternalConditionFolder);
            return testInternalConditionFolder;
        }

        /// <summary>
        /// Adds an internal condition named "InternalCondition" and returns it.
        /// </summary>
        /// <returns>The new internal condition.</returns>
        public global::TIC.InternalCondition AddInternalCondition()
        {
            return AddInternalCondition("InternalCondition");
        }

        /// <summary>
        /// Adds an internal condition with the specified name and returns it.
        /// </summary>
        /// <param name="name">The name of the internal condition.</param>
        /// <returns>The new internal condition.</returns>
        public TestInternalCondition AddInternalCondition(string name)
        {
            TestInternalCondition testInternalCondition = new(name);
            testInternalConditions.Add(testInternalCondition);
            return testInternalCondition;
        }

        /// <summary>
        /// Returns the child folder at the specified one-based index.
        /// </summary>
        /// <param name="index">The one-based index.</param>
        /// <returns>The child folder, or null when the index is past the end.</returns>
        public global::TIC.InternalConditionFolder? childFolders(int index)
        {
            testCallBudget.Spend();

            return index >= 1 && index <= testInternalConditionFolders.Count ? testInternalConditionFolders[index - 1] : null;
        }

        /// <summary>
        /// Returns the internal condition at the specified one-based index.
        /// </summary>
        /// <param name="index">The one-based index.</param>
        /// <returns>The internal condition, or null when the index is past the end.</returns>
        public global::TIC.InternalCondition? internalConditions(int index)
        {
            testCallBudget.Spend();

            return index >= 1 && index <= testInternalConditions.Count ? testInternalConditions[index - 1] : null;
        }
    }
}
