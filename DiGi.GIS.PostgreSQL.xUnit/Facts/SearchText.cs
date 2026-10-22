using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using System.Globalization;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Query.SearchText(string?)"/>, the one normaliser the stored <c>search_text</c> of <c>address_point</c> and every address search query go through: lower case, diacritics removed including <c>ł</c>, street-type tokens dropped as whole tokens only, punctuation and whitespace collapsed.
        /// <para>The assertions run under the invariant culture and again under <c>pl-PL</c>, because a normaliser that followed <see cref="CultureInfo.CurrentCulture"/> would store one text and query another. Reference: DiGi.GIS.PostgreSQL#103.</para>
        /// </summary>
        [Fact]
        public void SearchText()
        {
            CultureInfo cultureInfo_Current = CultureInfo.CurrentCulture;
            try
            {
                foreach (string cultureName in new string[] { string.Empty, "pl-PL" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

                    Assert.Equal("lodz", Query.SearchText("Łódź"));
                    Assert.Equal("3 maja", Query.SearchText("ul. 3 Maja"));
                    Assert.Equal("jana pawla ii", Query.SearchText("al. Jana Pawła II"));
                    Assert.Equal("plac wolnosci", Query.SearchText("Plac   Wolności"));
                    Assert.Equal("onz", Query.SearchText("rondo ONZ"));
                    Assert.Equal("grunwaldzki", Query.SearchText("pl.Grunwaldzki"));
                    Assert.Equal("ulica dluga", Query.SearchText("Ulica Długa"));
                    Assert.Equal("bielsko biala", Query.SearchText("Bielsko-Biała"));
                    Assert.Equal("zazolc gesla jazn", Query.SearchText("ZAŻÓŁĆ GĘŚLĄ JAŹŃ"));
                    Assert.Equal("rondo", Query.SearchText("Rondo"));

                    Assert.Null(Query.SearchText((string?)null));
                    Assert.Null(Query.SearchText("   "));
                    Assert.Null(Query.SearchText("%_."));

                    Assert.Equal("marszalkowska warszawa", Query.SearchText(new AddressPoint(new Point2D(0, 0), "Marszałkowska", "12345", "12", "Warszawa", "0918123", "00-001", "XUNIT", null)));
                    Assert.Equal("zoltki", Query.SearchText(new AddressPoint(new Point2D(0, 0), null, null, "7", "Żółtki", "0001234", null, "XUNIT", null)));
                    Assert.Null(Query.SearchText((AddressPoint?)null));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo_Current;
            }
        }
    }
}
