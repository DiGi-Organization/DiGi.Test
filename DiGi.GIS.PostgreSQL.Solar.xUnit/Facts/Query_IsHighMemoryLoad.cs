namespace DiGi.GIS.PostgreSQL.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Query.IsHighMemoryLoad"/>, the decision behind the high memory load warning of the batch solar radiation solve (DiGi.GIS.PostgreSQL#167).
        /// <para>A load one byte below the threshold is not high, a load at it and one byte above it are; a threshold of 0 or less - no collection has run yet - is never high, whatever the load.</para>
        /// </summary>
        [Fact]
        public void Query_IsHighMemoryLoad()
        {
            const long highMemoryLoadThresholdBytes = 55L * 1024 * 1024 * 1024;

            Assert.False(Query.IsHighMemoryLoad(highMemoryLoadThresholdBytes - 1, highMemoryLoadThresholdBytes));
            Assert.True(Query.IsHighMemoryLoad(highMemoryLoadThresholdBytes, highMemoryLoadThresholdBytes));
            Assert.True(Query.IsHighMemoryLoad(highMemoryLoadThresholdBytes + 1, highMemoryLoadThresholdBytes));

            Assert.False(Query.IsHighMemoryLoad(0, 0));
            Assert.False(Query.IsHighMemoryLoad(highMemoryLoadThresholdBytes, 0));
            Assert.False(Query.IsHighMemoryLoad(highMemoryLoadThresholdBytes, -1));
        }
    }
}
