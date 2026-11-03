using System.Globalization;
using System.IO;
using System.Reflection;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the obstacle heuristics on the real Warsaw fixtures (see <see cref="IsOrtoBlank"/>): the clear, sunny 2023 city tile reports no obstacle flag, no haze or cloud, and building shadows (about 16 %) below the shadow flag threshold, while the uniform white blank tile reads as all cloud.
        /// <para>The blank case documents why the blank check comes first: the obstacle heuristics alone cannot tell "no imagery" from "overcast".</para>
        /// </summary>
        [Fact]
        public void OrtoObstacleFractions_Content()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string? path_Content = Core.xUnit.Query.FilePath(assembly, "OrtoImage_Warszawa_2023_Content.jpg");
            Assert.NotNull(path_Content);

            Classes.OrtoObstacleFractions? ortoObstacleFractions = Create.OrtoObstacleFractions(File.ReadAllBytes(path_Content));
            Assert.NotNull(ortoObstacleFractions);
            Assert.Equal(Enums.OrtoObstacle.None, Query.OrtoObstacles(ortoObstacleFractions));
            string message = string.Format(CultureInfo.InvariantCulture, "shadow {0} snow {1} haze {2} cloud {3}", ortoObstacleFractions.Shadow, ortoObstacleFractions.Snow, ortoObstacleFractions.Haze, ortoObstacleFractions.Cloud);
            Assert.True(ortoObstacleFractions.Shadow > 0.05, message);
            Assert.True(ortoObstacleFractions.Total < Constants.OrtoObstacleThreshold.ShadowFraction, message);
            Assert.Equal(0, ortoObstacleFractions.Haze, 9);
            Assert.Equal(0, ortoObstacleFractions.Cloud, 9);

            string? path_Blank = Core.xUnit.Query.FilePath(assembly, "OrtoImage_Warszawa_1995_Blank.jpg");
            Assert.NotNull(path_Blank);

            Classes.OrtoObstacleFractions? ortoObstacleFractions_Blank = Create.OrtoObstacleFractions(File.ReadAllBytes(path_Blank));
            Assert.NotNull(ortoObstacleFractions_Blank);
            Assert.Equal(1, ortoObstacleFractions_Blank.Cloud, 9);
        }
    }
}
