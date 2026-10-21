using System;
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

        /// <summary>
        /// Verifies that <see cref="Query.TryConvert(string?, System.Type?, out object?)"/> parses ISO 8601 text into a <see cref="System.DateTimeOffset"/> keeping the written offset, works for <c>DateTimeOffset?</c> and returns <c>false</c> for invalid text instead of throwing (DiGi-Organization/DiGi.GML#3).
        /// </summary>
        [Fact]
        public void TryConvert_DateTimeOffset()
        {
            Assert.True(Query.TryConvert("2024-11-21T19:10:44+02:00", typeof(DateTimeOffset), out object? value));
            DateTimeOffset dateTimeOffset = Assert.IsType<DateTimeOffset>(value);
            Assert.Equal(TimeSpan.FromHours(2), dateTimeOffset.Offset);
            Assert.Equal(new DateTimeOffset(2024, 11, 21, 19, 10, 44, TimeSpan.FromHours(2)), dateTimeOffset);

            Assert.True(Query.TryConvert("2024-11-21T19:10:44+02:00", typeof(DateTimeOffset?), out object? value_Nullable));
            Assert.Equal(TimeSpan.FromHours(2), Assert.IsType<DateTimeOffset>(value_Nullable).Offset);

            Assert.False(Query.TryConvert("not a date", typeof(DateTimeOffset), out object? value_Invalid));
            Assert.Null(value_Invalid);
        }

        /// <summary>
        /// Verifies that <see cref="Query.TryConvert(string?, System.Type?, out object?)"/> parses GML dates with the invariant culture whatever the current culture is (DiGi-Organization/DiGi.GML#3).
        /// <para>ISO 8601 text must give the same wall-clock result under <c>pl-PL</c> and <c>en-US</c>.</para>
        /// </summary>
        [Fact]
        public void TryConvert_DateTime_InvariantCulture()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            string[] names = ["pl-PL", "en-US"];

            try
            {
                foreach (string name in names)
                {
                    CultureInfo.CurrentCulture = new CultureInfo(name);

                    Assert.True(Query.TryConvert("2012-10-30", typeof(DateTime), out object? value_Date));
                    Assert.Equal(new DateTime(2012, 10, 30), Assert.IsType<DateTime>(value_Date));

                    Assert.True(Query.TryConvert("2017-04-07T15:13:07", typeof(DateTime), out object? value_DateTime));
                    Assert.Equal(new DateTime(2017, 4, 7, 15, 13, 7), Assert.IsType<DateTime>(value_DateTime));
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }
    }
}
