using DiGi.Geometry.Planar.Classes;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the functionality of the planar offset query for both <see cref="Rectangle2D"/> and <see cref="Polygon2D"/> objects, verifying that positive and negative offsets result in expected containment and area properties.
        /// </summary>
        [Fact]
        public void Offset()
        {
            Rectangle2D rectangle2D = new(10, 10);

            Rectangle2D? rectangle2D_Offset_1 = Planar.Query.Offset(rectangle2D, 2);
            Assert.NotNull(rectangle2D_Offset_1);

            Rectangle2D? rectangle2D_Offset_2 = Planar.Query.Offset(rectangle2D, -2);
            Assert.NotNull(rectangle2D_Offset_2);

            Assert.True(rectangle2D.Inside(rectangle2D_Offset_2));
            Assert.True(rectangle2D_Offset_1.Inside(rectangle2D));
            Assert.True(rectangle2D_Offset_1.Inside(rectangle2D_Offset_2));

            Assert.True(rectangle2D_Offset_1.GetArea() > rectangle2D_Offset_2.GetArea());

            Polygon2D polygon2D = new(rectangle2D);

            Polygon2D? polygon2D_Offset_1 = Planar.Query.Offset(polygon2D, 2)?.FirstOrDefault();
            Assert.NotNull(polygon2D_Offset_1);

            Polygon2D? polygon2D_Offset_2 = Planar.Query.Offset(polygon2D, -2)?.FirstOrDefault();
            Assert.NotNull(polygon2D_Offset_2);

            Assert.True(polygon2D.Inside(polygon2D_Offset_2));
            Assert.True(polygon2D_Offset_1.Inside(polygon2D));
            Assert.True(polygon2D_Offset_1.Inside(polygon2D_Offset_2));

            Assert.Equal(rectangle2D_Offset_1.GetArea(), polygon2D_Offset_1.GetArea(), DiGi.Core.Constants.Tolerance.MacroDistance);
            Assert.Equal(rectangle2D_Offset_2.GetArea(), polygon2D_Offset_2.GetArea(), DiGi.Core.Constants.Tolerance.MacroDistance);
        }

        /// <summary>
        /// Tests offset calculation on a complex polygon.
        /// </summary>
        [Fact]
        public void OffsetComplex()
        {
            Polygon2D polygon2D = new(new List<Point2D>()
            {
                new(0, 0),
                new(10, 0),
                new(10, 10),
                new(11, 10),
                new(11, 0),
                new(11, 0),
                new(20, 0),
                new(20, 11),
                new(0, 11)
            });

            List<Polygon2D>? polygon2Ds_Offset = Planar.Query.Offset(polygon2D, -1);
            Assert.NotNull(polygon2Ds_Offset);
        }
    }
}