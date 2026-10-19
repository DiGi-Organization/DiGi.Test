using System.Globalization;

namespace DiGi.GML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.TryConvert(string?, System.Type?, out object?)"/> parses GML numbers with the invariant culture whatever the current culture is.
        /// <para>Under <c>de-DE</c> the period is the group separator, so a current-culture parse would read <c>"301061.69"</c> as 30106169; <c>pl-PL</c> and <c>en-GB</c> are checked as well.</para>
        /// </summary>
        [Fact]
        public void TryConvert_Double_InvariantCulture()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            string[] names = ["de-DE", "pl-PL", "en-GB"];

            try
            {
                foreach (string name in names)
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);

                    Assert.True(Query.TryConvert("301061.69", typeof(double), out object? value_Coordinate));
                    Assert.Equal(301061.69, Assert.IsType<double>(value_Coordinate));

                    Assert.True(Query.TryConvert("5.5", typeof(double), out object? value_Small));
                    Assert.Equal(5.5, Assert.IsType<double>(value_Small));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }
    }
}
