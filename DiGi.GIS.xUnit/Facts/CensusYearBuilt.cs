using DiGi.BDL.Enums;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Two areas whose dwellings were built in different periods get different census years built: the median is interpolated within the period holding half the dwellings (DiGi-Organization/DiGi.GIS#23). The lookup of the BDL variable keys holds under pl-PL too.
        /// </summary>
        [Fact]
        public void CensusYearBuilt_TwoAreas()
        {
            // 1945-1970 holds 800 of 1 000: 1945 + 500 / 800 * 26 = 1961.25.
            StatisticalDataCollection statisticalDataCollection_A = CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_1945_1970_census_2021, 800),
                (Variable.inhabited_dwellings_built_1971_1978_census_2021, 100),
                (Variable.inhabited_dwellings_built_1989_2002_census_2021, 100));

            // 1989-2002 holds 800 of 1 000, 200 before it: 1989 + 300 / 800 * 14 = 1994.25.
            StatisticalDataCollection statisticalDataCollection_B = CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_1945_1970_census_2021, 100),
                (Variable.inhabited_dwellings_built_1971_1978_census_2021, 100),
                (Variable.inhabited_dwellings_built_1989_2002_census_2021, 800));

            Assert.Equal((short)1961, Query.CensusYearBuilt(statisticalDataCollection_A));
            Assert.Equal((short)1994, Query.CensusYearBuilt(statisticalDataCollection_B));

            // The total is not a period and must not change the result.
            statisticalDataCollection_A.Add(CensusYearBuilt_StatisticalYearlyDoubleData(Variable.inhabited_dwellings_total_census_2021, 1000));
            Assert.Equal((short)1961, Query.CensusYearBuilt(statisticalDataCollection_A));

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
                Assert.Equal((short)1961, Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection(
                    (Variable.inhabited_dwellings_built_1945_1970_census_2021, 800),
                    (Variable.inhabited_dwellings_built_1971_1978_census_2021, 100),
                    (Variable.inhabited_dwellings_built_1989_2002_census_2021, 100))));
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }

        /// <summary>
        /// A period running past the last year counted keeps only its share up to that year, and a period starting after it is dropped.
        /// </summary>
        [Fact]
        public void CensusYearBuilt_LastYear()
        {
            // The share decides the period: 900 * 6 / 9 = 600 of 2003-2011 against 650 of 1945-1970, so half of 1 250 falls in 1945-1970 (1945 + 625 / 650 * 26 = 1970.0).
            // Counting all 900 would put half of 1 550 in 2003-2011 instead.
            Assert.Equal((short)1970, Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_1945_1970_census_2021, 650),
                (Variable.inhabited_dwellings_built_2003_2011_census_2021, 900))));

            StatisticalDataCollection statisticalDataCollection = CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_1989_2002_census_2021, 300),
                (Variable.inhabited_dwellings_built_2003_2011_census_2021, 900));

            // Up to 2008: 600 over 2003-2008, 300 before; 2003 + 150 / 600 * 6 = 2004.5 (all 900 would give 2005).
            Assert.Equal((short)2004, Query.CensusYearBuilt(statisticalDataCollection));

            // Up to 2010: 900 * 8 / 9 = 800 over 2003-2010; 2003 + 250 / 800 * 8 = 2005.5.
            Assert.Equal((short)2005, Query.CensusYearBuilt(statisticalDataCollection, lastYear: 2010));

            statisticalDataCollection.Add(CensusYearBuilt_StatisticalYearlyDoubleData(Variable.inhabited_dwellings_built_2012_2016_census_2021, 5000));
            Assert.Equal((short)2004, Query.CensusYearBuilt(statisticalDataCollection));
        }

        /// <summary>
        /// A median exactly on a period boundary is the last year of the earlier period; one dwelling more in the later period moves it into that period.
        /// </summary>
        [Fact]
        public void CensusYearBuilt_Boundary()
        {
            Assert.Equal((short)1970, Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_1945_1970_census_2021, 500),
                (Variable.inhabited_dwellings_built_1971_1978_census_2021, 500))));

            Assert.Equal((short)1971, Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_1945_1970_census_2021, 499),
                (Variable.inhabited_dwellings_built_1971_1978_census_2021, 501))));
        }

        /// <summary>
        /// A median in the period open towards the past ("before 1918") is that period's last year.
        /// </summary>
        [Fact]
        public void CensusYearBuilt_OpenPeriod()
        {
            Assert.Equal((short)1917, Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_before_1918_census_2021, 900),
                (Variable.inhabited_dwellings_built_1918_1944_census_2021, 100))));
        }

        /// <summary>
        /// No census year without a positive count up to the last year: a null or empty collection, the total alone, zero, negative or NaN counts, counts of another year, or only periods after the last year.
        /// </summary>
        [Fact]
        public void CensusYearBuilt_NoData()
        {
            Assert.Null(Query.CensusYearBuilt(null));
            Assert.Null(Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection()));
            Assert.Null(Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection((Variable.inhabited_dwellings_total_census_2021, 1000))));
            Assert.Null(Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection(
                (Variable.inhabited_dwellings_built_1945_1970_census_2021, 0),
                (Variable.inhabited_dwellings_built_1971_1978_census_2021, -5),
                (Variable.inhabited_dwellings_built_1979_1988_census_2021, double.NaN))));
            Assert.Null(Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection((Variable.inhabited_dwellings_built_1945_1970_census_2021, 800)), year: 2020));
            Assert.Null(Query.CensusYearBuilt(CensusYearBuilt_StatisticalDataCollection((Variable.inhabited_dwellings_built_2012_2016_census_2021, 800))));
        }

        /// <summary>
        /// YearBuiltSource.Census is appended: the integer values of the existing members, which are stored and sent, do not move.
        /// </summary>
        [Fact]
        public void YearBuiltSource_Census()
        {
            Assert.Equal(0, (int)YearBuiltSource.User);
            Assert.Equal(1, (int)YearBuiltSource.Prediction);
            Assert.Equal(2, (int)YearBuiltSource.Other);
            Assert.Equal(3, (int)YearBuiltSource.Fallback);
            Assert.Equal(4, (int)YearBuiltSource.Census);
            Assert.Equal("Census", Core.Query.Description(YearBuiltSource.Census));
        }

        private static StatisticalDataCollection CensusYearBuilt_StatisticalDataCollection(params (Variable Variable, double Count)[] counts)
        {
            StatisticalDataCollection result = new(Guid.NewGuid(), new UnitCode("071412865028"));
            foreach ((Variable variable, double count) in counts)
            {
                result.Add(CensusYearBuilt_StatisticalYearlyDoubleData(variable, count));
            }

            return result;
        }

        private static StatisticalYearlyDoubleData CensusYearBuilt_StatisticalYearlyDoubleData(Variable variable, double count)
        {
            return new StatisticalYearlyDoubleData(Core.Query.Description(variable), ((int)variable).ToString(CultureInfo.InvariantCulture), [new KeyValuePair<short, double>(2021, count)]);
        }
    }
}
