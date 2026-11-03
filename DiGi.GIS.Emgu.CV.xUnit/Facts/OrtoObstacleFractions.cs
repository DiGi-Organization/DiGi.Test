using DiGi.GIS.Enums;
using Emgu.CV;
using Emgu.CV.Structure;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the obstacle fractions and flags on a synthetic 128 px tile whose four 64 px quadrants are cloud, haze, snow and shadow, each aligned to the analysis blocks.
        /// <para>Cloud is smooth bright neutral, haze is smooth mid-grey neutral, snow is a bright neutral checkerboard (textured, so not cloud) and shadow is a dark checkerboard. Each quadrant is a quarter of the tile, so every fraction is 0.25, the total is 1 and every flag is reported.</para>
        /// <para>A textured saturated tile reports no obstacle at all, both as RGB vegetation (green) and as CIR vegetation (red).</para>
        /// </summary>
        [Fact]
        public void OrtoObstacleFractions()
        {
            using Image<Bgr, byte> image = new(128, 128);
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    bool odd = ((x + y) % 2) == 1;
                    byte value;
                    if (y < 64)
                    {
                        value = x < 64 ? (byte)240 : (byte)150;
                    }
                    else
                    {
                        value = x < 64 ? (odd ? (byte)220 : (byte)255) : (odd ? (byte)10 : (byte)30);
                    }

                    image.Data[y, x, 0] = value;
                    image.Data[y, x, 1] = value;
                    image.Data[y, x, 2] = value;
                }
            }

            Classes.OrtoObstacleFractions? ortoObstacleFractions = Create.OrtoObstacleFractions(Png(image));
            Assert.NotNull(ortoObstacleFractions);
            Assert.Equal(0.25, ortoObstacleFractions.Cloud, 9);
            Assert.Equal(0.25, ortoObstacleFractions.Haze, 9);
            Assert.Equal(0.25, ortoObstacleFractions.Snow, 9);
            Assert.Equal(0.25, ortoObstacleFractions.Shadow, 9);
            Assert.Equal(1, ortoObstacleFractions.Total, 9);
            Assert.Equal(OrtoObstacle.Shadow | OrtoObstacle.Snow | OrtoObstacle.Haze | OrtoObstacle.Cloud, Query.OrtoObstacles(ortoObstacleFractions));

            using Image<Bgr, byte> image_RGB = Vegetation(1);
            Classes.OrtoObstacleFractions? ortoObstacleFractions_RGB = Create.OrtoObstacleFractions(Png(image_RGB));
            Assert.NotNull(ortoObstacleFractions_RGB);
            Assert.Equal(0, ortoObstacleFractions_RGB.Total, 9);
            Assert.Equal(OrtoObstacle.None, Query.OrtoObstacles(ortoObstacleFractions_RGB));

            using Image<Bgr, byte> image_CIR = Vegetation(2);
            Classes.OrtoObstacleFractions? ortoObstacleFractions_CIR = Create.OrtoObstacleFractions(Png(image_CIR));
            Assert.NotNull(ortoObstacleFractions_CIR);
            Assert.Equal(0, ortoObstacleFractions_CIR.Total, 9);
            Assert.Equal(OrtoObstacle.None, Query.OrtoObstacles(ortoObstacleFractions_CIR));

            Assert.Null(Create.OrtoObstacleFractions((byte[]?)null));
            Assert.Null(Query.OrtoObstacles(null));

            static Image<Bgr, byte> Vegetation(int channel)
            {
                Image<Bgr, byte> result = new(96, 96);
                for (int y = 0; y < 96; y++)
                {
                    for (int x = 0; x < 96; x++)
                    {
                        result.Data[y, x, 0] = 40;
                        result.Data[y, x, 1] = 40;
                        result.Data[y, x, 2] = 40;
                        result.Data[y, x, channel] = ((x + y) % 2) == 0 ? (byte)120 : (byte)170;
                    }
                }

                return result;
            }
        }
    }
}
