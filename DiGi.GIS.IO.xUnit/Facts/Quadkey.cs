namespace DiGi.GIS.IO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the quadkey of a Microsoft chunk is read from the <c>quadkey=&lt;digits&gt;</c> directory segment of Microsoft's own layout, with Windows and POSIX separators, and that anything else answers null rather than a wrong source identity (DiGi-Organization/DiGi.GIS.PostgreSQL#132).
        /// <para>The file name never counts, the innermost of several segments wins, and a segment holding anything but the quadkey digits 0-3 is not a quadkey.</para>
        /// </summary>
        [Fact]
        public void Quadkey()
        {
            Assert.Equal("120213101", Query.Quadkey(@"D:\Input\Microsoft\2026-02-03\quadkey=120213101\part-00087.c000.csv.gz"));
            Assert.Equal("120213101", Query.Quadkey("/data/global-buildings.geojsonl/RegionName=Poland/quadkey=120213101/part-00087.c000.csv.gz"));
            Assert.Equal("120213101", Query.Quadkey(@"D:\Input\Quadkey=120213101\part.csv.gz"));

            // The innermost directory segment wins.
            Assert.Equal("1202131", Query.Quadkey("/quadkey=12021/quadkey=1202131/part.csv.gz"));

            // No segment, a segment only in the file name, an empty value, a non-quadkey digit, and no path.
            Assert.Null(Query.Quadkey(@"D:\Input\Microsoft\part-00087.c000.csv.gz"));
            Assert.Null(Query.Quadkey(@"D:\Input\quadkey=120213101"));
            Assert.Null(Query.Quadkey(@"D:\Input\quadkey=\part.csv.gz"));
            Assert.Null(Query.Quadkey(@"D:\Input\quadkey=120213401\part.csv.gz"));
            Assert.Null(Query.Quadkey(null));
            Assert.Null(Query.Quadkey("  "));
        }
    }
}
