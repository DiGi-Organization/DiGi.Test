using DiGi.Geometry.Planar.Classes;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Convert.ToDiGi(GML.Classes.Point?)"/> swaps the northing-first <c>gml:pos</c> of PRG into an easting-first <see cref="Point2D"/>, using the position of pl. Słowiański 7 in Legnica.
        /// <para>Also covers each <see langword="null"/> return: no point, the legacy <c>EPSG:2180</c> name BDOT10k uses (whose axis order differs), no <c>srsName</c>, one or three values, and a value that is not finite.</para>
        /// </summary>
        [Fact]
        public void Point2D_ToDiGi()
        {
            GML.Classes.Point point = new() { srsName = Convert.SrsName, pos = [374887.73, 301858.38] };
            Assert.Equal(new Point2D(301858.38, 374887.73), point.ToDiGi());

            Assert.Null(Convert.ToDiGi(null));
            Assert.Null(new GML.Classes.Point() { srsName = "EPSG:2180", pos = [374887.73, 301858.38] }.ToDiGi());
            Assert.Null(new GML.Classes.Point() { pos = [374887.73, 301858.38] }.ToDiGi());
            Assert.Null(new GML.Classes.Point() { srsName = Convert.SrsName, pos = [374887.73] }.ToDiGi());
            Assert.Null(new GML.Classes.Point() { srsName = Convert.SrsName, pos = [374887.73, 301858.38, 120.0] }.ToDiGi());
            Assert.Null(new GML.Classes.Point() { srsName = Convert.SrsName, pos = [double.NaN, 301858.38] }.ToDiGi());
            Assert.Null(new GML.Classes.Point() { srsName = Convert.SrsName, pos = [374887.73, double.PositiveInfinity] }.ToDiGi());
        }
    }
}
