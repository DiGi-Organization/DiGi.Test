using DiGi.Geometry.Planar.Classes;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Planar.Query.ZOrderComparison{TKey}"/>: boxes in the four 100 m cells of a 2 x 2 grid come out lower-left, lower-right, upper-left, upper-right; two boxes of one cell come out by key; a key with no box comes last.
        /// <para>Offset from the origin, so the cells are measured from the lowest box rather than from zero.</para>
        /// </summary>
        [Fact]
        public void ZOrderComparison()
        {
            static BoundingBox2D Box(double x, double y)
            {
                return new BoundingBox2D(new Point2D(x, y), new Point2D(x + 10, y + 10));
            }

            Dictionary<string, BoundingBox2D> boundingBox2Ds = new()
            {
                ["d"] = Box(500150, 300150),
                ["c"] = Box(500000, 300150),
                ["b"] = Box(500150, 300000),
                ["a2"] = Box(500020, 300020),
                ["a1"] = Box(500000, 300000),
            };

            List<string> keys = ["none", "d", "c", "b", "a2", "a1"];
            keys.Sort(Planar.Query.ZOrderComparison(boundingBox2Ds, 100.0));

            Assert.Equal(["a1", "a2", "b", "c", "d", "none"], keys);

            // Without boxes the keys keep their natural order.
            List<string> keys_Plain = ["b", "a"];
            keys_Plain.Sort(Planar.Query.ZOrderComparison<string>(null, 100.0));
            Assert.Equal(["a", "b"], keys_Plain);
        }
    }
}
