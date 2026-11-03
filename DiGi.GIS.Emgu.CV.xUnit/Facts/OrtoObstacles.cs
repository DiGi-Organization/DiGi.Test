using DiGi.GIS.Enums;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that each obstacle flag is reported from its fraction threshold and not just below it.
        /// </summary>
        [Fact]
        public void OrtoObstacles()
        {
            double epsilon = 1e-9;

            Assert.Equal(OrtoObstacle.Shadow, Query.OrtoObstacles(new Classes.OrtoObstacleFractions(Constants.OrtoObstacleThreshold.ShadowFraction, 0, 0, 0)));
            Assert.Equal(OrtoObstacle.None, Query.OrtoObstacles(new Classes.OrtoObstacleFractions(Constants.OrtoObstacleThreshold.ShadowFraction - epsilon, 0, 0, 0)));

            Assert.Equal(OrtoObstacle.Snow, Query.OrtoObstacles(new Classes.OrtoObstacleFractions(0, Constants.OrtoObstacleThreshold.SnowFraction, 0, 0)));
            Assert.Equal(OrtoObstacle.None, Query.OrtoObstacles(new Classes.OrtoObstacleFractions(0, Constants.OrtoObstacleThreshold.SnowFraction - epsilon, 0, 0)));

            Assert.Equal(OrtoObstacle.Haze, Query.OrtoObstacles(new Classes.OrtoObstacleFractions(0, 0, Constants.OrtoObstacleThreshold.HazeFraction, 0)));
            Assert.Equal(OrtoObstacle.None, Query.OrtoObstacles(new Classes.OrtoObstacleFractions(0, 0, Constants.OrtoObstacleThreshold.HazeFraction - epsilon, 0)));

            Assert.Equal(OrtoObstacle.Cloud, Query.OrtoObstacles(new Classes.OrtoObstacleFractions(0, 0, 0, Constants.OrtoObstacleThreshold.CloudFraction)));
            Assert.Equal(OrtoObstacle.None, Query.OrtoObstacles(new Classes.OrtoObstacleFractions(0, 0, 0, Constants.OrtoObstacleThreshold.CloudFraction - epsilon)));
        }
    }
}
