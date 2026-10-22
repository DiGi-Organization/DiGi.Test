namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the county code of a PRG address point: the first four digits of the seven-digit TERYT commune code, and null for a missing, short or non-numeric value.
        /// </summary>
        [Fact]
        public void Query_CountyCode()
        {
            Assert.Equal("0262", Query.CountyCode("0262011"));
            Assert.Equal("0209", Query.CountyCode(" 0209032 "));
            Assert.Equal("0209", Query.CountyCode("0209"));

            Assert.Null(Query.CountyCode(null));
            Assert.Null(Query.CountyCode(string.Empty));
            Assert.Null(Query.CountyCode("020"));
            Assert.Null(Query.CountyCode("02A9032"));
        }
    }
}
