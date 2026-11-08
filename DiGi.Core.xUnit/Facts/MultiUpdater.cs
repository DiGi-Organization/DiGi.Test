namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the functionality of the MultiUpdater class to ensure that it correctly aggregates updates from multiple updater instances and modifies its value accordingly.
        /// </summary>
        [Fact]
        public void MultiUpdater()
        {
            Core.Classes.MultiUpdater<double> multiUpdater = new([new TestUpdater(1), new TestUpdater(2)])
            {
                Value = 10
            };

            Assert.True(multiUpdater.Update());

            Assert.Equal(13, multiUpdater.Value);
        }
    }
}