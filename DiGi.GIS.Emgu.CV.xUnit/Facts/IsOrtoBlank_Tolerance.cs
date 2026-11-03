using Emgu.CV;
using Emgu.CV.Structure;

namespace DiGi.GIS.Emgu.CV.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the uniformity tolerance just inside and just outside its value, on a lossless synthetic tile.
        /// <para>The tile is uniform grey with one deviating pixel at the corner, which every sampling grid includes. A spread equal to the tolerance is still blank; one level more is content. The deviation is tested on one channel and below the base value, so a check that only looks at brightness or only upwards would fail.</para>
        /// </summary>
        [Fact]
        public void IsOrtoBlank_Tolerance()
        {
            int tolerance = Constants.OrtoBlank.Tolerance;
            byte value = 128;

            Assert.True(Query.IsOrtoBlank(Png(Tile(value, value))));
            Assert.True(Query.IsOrtoBlank(Png(Tile(value, (byte)(value + tolerance)))));
            Assert.False(Query.IsOrtoBlank(Png(Tile(value, (byte)(value + tolerance + 1)))));
            Assert.True(Query.IsOrtoBlank(Png(Tile(value, (byte)(value - tolerance)))));
            Assert.False(Query.IsOrtoBlank(Png(Tile(value, (byte)(value - tolerance - 1)))));

            Assert.True(Query.IsOrtoBlank(Png(Tile(value, (byte)(value + 3))), 3));
            Assert.False(Query.IsOrtoBlank(Png(Tile(value, (byte)(value + 3))), 2));

            static Image<Bgr, byte> Tile(byte value, byte value_Corner)
            {
                Image<Bgr, byte> result = new(300, 200, new Bgr(value, value, value));
                result.Data[199, 299, 1] = value_Corner;
                return result;
            }
        }
    }
}
