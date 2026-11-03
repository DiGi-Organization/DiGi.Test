using System.Linq;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the construction, copy and serialization round trip of <see cref="Classes.OrtoObstacleFractions"/>.
        /// <para>The copy is asserted directly because SerializationCheck never runs the copy constructor.</para>
        /// </summary>
        [Fact]
        public void OrtoObstacleFractions_Serialization()
        {
            Classes.OrtoObstacleFractions ortoObstacleFractions = new(0.125, 0.0625, 0.25, 0.03125);
            Assert.Equal(0.125, ortoObstacleFractions.Shadow);
            Assert.Equal(0.0625, ortoObstacleFractions.Snow);
            Assert.Equal(0.25, ortoObstacleFractions.Haze);
            Assert.Equal(0.03125, ortoObstacleFractions.Cloud);
            Assert.Equal(0.46875, ortoObstacleFractions.Total);

            Classes.OrtoObstacleFractions ortoObstacleFractions_Copy = new(ortoObstacleFractions);
            Assert.Equal(ortoObstacleFractions.Shadow, ortoObstacleFractions_Copy.Shadow);
            Assert.Equal(ortoObstacleFractions.Snow, ortoObstacleFractions_Copy.Snow);
            Assert.Equal(ortoObstacleFractions.Haze, ortoObstacleFractions_Copy.Haze);
            Assert.Equal(ortoObstacleFractions.Cloud, ortoObstacleFractions_Copy.Cloud);

            string? json = Core.Convert.ToSystem_String(ortoObstacleFractions);
            Assert.False(string.IsNullOrWhiteSpace(json));

            Classes.OrtoObstacleFractions? ortoObstacleFractions_Json = Core.Convert.ToDiGi<Classes.OrtoObstacleFractions>(json)?.FirstOrDefault();
            Assert.NotNull(ortoObstacleFractions_Json);
            Assert.Equal(ortoObstacleFractions.Shadow, ortoObstacleFractions_Json.Shadow);
            Assert.Equal(ortoObstacleFractions.Snow, ortoObstacleFractions_Json.Snow);
            Assert.Equal(ortoObstacleFractions.Haze, ortoObstacleFractions_Json.Haze);
            Assert.Equal(ortoObstacleFractions.Cloud, ortoObstacleFractions_Json.Cloud);

            Core.xUnit.Query.SerializationCheck(ortoObstacleFractions);
        }
    }
}
