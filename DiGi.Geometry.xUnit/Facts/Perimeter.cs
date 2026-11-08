using DiGi.Geometry.Planar.Classes;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests whether the perimeter of a <see cref="Polygon2D"/> is calculated correctly.
        /// </summary>
        [Fact]
        public void Perimeter()
        {
            Polygon2D polygon2D = new([new Point2D(0, 0), new Point2D(0, 2), new Point2D(2, 2), new Point2D(2, 0)]);

            double perimeter = polygon2D.GetPerimeter();

            Assert.Equal(8, perimeter);
        }
    }
}