using DiGi.Core.Classes;
using DiGi.Core.Enums;
using DiGi.Typology.Classes;
using DiGi.Typology.Visual.Classes;
using System.Globalization;

namespace DiGi.Typology.Visual.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Query.Key(object)"/> agrees with <see cref="object.Equals(object)"/> and does not depend
        /// on the current culture, so that a key written by one process is found by another.
        /// <para>Each case pairs the key with the raw <c>ToString()</c> it replaces, showing where that form would have
        /// broken the invariant: a decimal keeps its scale, negative zero prints its sign, a DateTime drops its ticks,
        /// every floating-point and date form follows the current culture, and so does <see cref="Range{T}.ToString()"/>,
        /// which a range bucket would otherwise be keyed by.</para>
        /// </summary>
        [Fact]
        public void Query_Key()
        {
            // Integers of any width, and the string form, share a key.
            Assert.Equal("2010", Query.Key(2010));
            Assert.Equal("2010", Query.Key(2010L));
            Assert.Equal("2010", Query.Key((short)2010));
            Assert.Equal("210", Query.Key((byte)210));
            Assert.Equal("2010", Query.Key("2010"));
            Assert.Equal("-5", Query.Key(-5));
            Assert.Equal("2010", Query.Key(2010.0));
            Assert.Equal("2010", Query.Key(2010m));

            // A decimal is keyed without its scale, because 1.10m equals 1.1m.
            Assert.True(1.10m.Equals(1.1m));
            Assert.NotEqual(1.10m.ToString(CultureInfo.InvariantCulture), 1.1m.ToString(CultureInfo.InvariantCulture));
            Assert.Equal("1.1", Query.Key(1.10m));
            Assert.Equal(Query.Key(1.1m), Query.Key(1.10m));
            Assert.Equal("2010", Query.Key(2010.00m));

            // Negative zero equals zero, so it is keyed as zero.
            Assert.True((-0.0).Equals(0.0));
            Assert.NotEqual((0.0).ToString(CultureInfo.InvariantCulture), (-0.0).ToString(CultureInfo.InvariantCulture));
            Assert.Equal("0", Query.Key(-0.0));
            Assert.Equal(Query.Key(0.0), Query.Key(-0.0));
            Assert.Equal("0", Query.Key(-0.0f));
            Assert.Equal("1.5", Query.Key(1.5));
            Assert.Equal("1.5", Query.Key(1.5f));
            Assert.Equal("NaN", Query.Key(double.NaN));
            Assert.Equal("Infinity", Query.Key(double.PositiveInfinity));

            // A DateTime is keyed to the tick with its kind ignored, as its equality is.
            DateTime dateTime_Utc = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            DateTime dateTime_Local = new(dateTime_Utc.Ticks, DateTimeKind.Local);
            DateTime dateTime_Tick = dateTime_Utc.AddTicks(1);

            Assert.True(dateTime_Utc.Equals(dateTime_Local));
            Assert.False(dateTime_Utc.Equals(dateTime_Tick));
            Assert.Equal(dateTime_Utc.ToString(CultureInfo.InvariantCulture), dateTime_Tick.ToString(CultureInfo.InvariantCulture));
            Assert.Equal("2024-01-15T10:30:00.0000000", Query.Key(dateTime_Utc));
            Assert.Equal(Query.Key(dateTime_Utc), Query.Key(dateTime_Local));
            Assert.NotEqual(Query.Key(dateTime_Utc), Query.Key(dateTime_Tick));

            // A DateTimeOffset is keyed as the UTC instant its equality compares.
            DateTimeOffset dateTimeOffset_1 = new(2024, 1, 15, 10, 0, 0, TimeSpan.FromHours(1));
            DateTimeOffset dateTimeOffset_2 = new(2024, 1, 15, 9, 0, 0, TimeSpan.Zero);

            Assert.True(dateTimeOffset_1.Equals(dateTimeOffset_2));
            Assert.NotEqual(dateTimeOffset_1.ToString(CultureInfo.InvariantCulture), dateTimeOffset_2.ToString(CultureInfo.InvariantCulture));
            Assert.Equal("2024-01-15T09:00:00.0000000Z", Query.Key(dateTimeOffset_1));
            Assert.Equal(Query.Key(dateTimeOffset_1), Query.Key(dateTimeOffset_2));

            // Culture-free types keep their ToString form.
            Guid guid = Guid.NewGuid();

            Assert.Equal("True", Query.Key(true));
            Assert.Equal(guid.ToString(), Query.Key(guid));
            Assert.Equal("Int", Query.Key(DataType.Int));
            Assert.Equal("01:30:00", Query.Key(TimeSpan.FromMinutes(90)));

            // Null and its documented collision with the literal text "null"; unique value rule data of either kind resolves to its value.
            Assert.Equal("null", Query.Key(null));
            Assert.Equal(Query.Key(null), Query.Key("null"));
            Assert.Equal(Query.Key(2010), Query.Key(new UniqueValueRuleData(2010)));
            Assert.Equal(Query.Key(null), Query.Key(new UniqueValueRuleData((object?)null)));
            Assert.Equal(Query.Key(2010), Query.Key(new UniqueValueRuleData(2010).ToString()));
            Assert.Equal(Query.Key(2010.5), Query.Key(new VisualUniqueValueRuleData(2010.5)));
            Assert.Equal(Query.Key(dateTime_Utc), Query.Key(new VisualUniqueValueRuleData(dateTime_Local, Create.TypologyAppearance(System.Drawing.Color.Red))));

            // A range is keyed by its bounds, each rendered as a value is, and so is the rule data of either kind wrapping it.
            Range<int> range_Int = new(2004, 2020);
            Range<double> range_Double = new(0.5, 12.25);
            DateTimeRange dateTimeRange = new(dateTime_Utc, dateTime_Utc.AddDays(1));

            Assert.Equal("[2004, 2020]", Query.Key(range_Int));
            Assert.Equal("[0.5, 12.25]", Query.Key(range_Double));
            Assert.Equal("[2024-01-15T10:30:00.0000000, 2024-01-16T10:30:00.0000000]", Query.Key(dateTimeRange));
            Assert.Equal(Query.Key(range_Double), Query.Key(new RangeValueRuleData<double>(range_Double)));
            Assert.Equal(Query.Key(range_Double), Query.Key(new VisualRangeValueRuleData<double>(range_Double, null)));
            Assert.Equal(Query.Key(range_Double), Query.Key(new Range<double>(12.25, 0.5)));
            Assert.NotEqual(Query.Key(range_Double), Query.Key(new Range<double>(0.5, 12.5)));
            Assert.Equal(Query.Key(new Range<double>(0.5, -0.0)), Query.Key(new Range<double>(0.0, 0.5)));
            Assert.Equal("null", Query.Key(new VisualRangeValueRuleData<int>()));

            // The key does not follow the current culture, while the raw ToString does. CurrentCulture is per thread.
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

                Assert.NotEqual("1.5", 1.5.ToString(CultureInfo.CurrentCulture));
                Assert.NotEqual("1.5", 1.5m.ToString(CultureInfo.CurrentCulture));
                Assert.NotEqual("Infinity", double.PositiveInfinity.ToString(CultureInfo.CurrentCulture));
                Assert.NotEqual("2024-01-15T10:30:00.0000000", dateTime_Utc.ToString(CultureInfo.CurrentCulture));
                Assert.NotEqual("[0.5, 12.25]", range_Double.ToString());
                Assert.NotEqual("[0.5, 12.25]", new RangeValueRuleData<double>(range_Double).ToString());

                Assert.Equal("1.5", Query.Key(1.5));
                Assert.Equal("1.5", Query.Key(1.5m));
                Assert.Equal("Infinity", Query.Key(double.PositiveInfinity));
                Assert.Equal("2024-01-15T10:30:00.0000000", Query.Key(dateTime_Utc));
                Assert.Equal("-5", Query.Key(-5));
                Assert.Equal("[0.5, 12.25]", Query.Key(range_Double));
                Assert.Equal("[0.5, 12.25]", Query.Key(new VisualRangeValueRuleData<double>(range_Double, null)));
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }
    }
}
