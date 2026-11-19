using DiGi.BDL.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.BDL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The NSP 2021 dwellings by construction period (BDL P4386, DiGi-Organization/DiGi.BDL#1) carry contiguous year ranges: the first is open towards the past, each next one starts the year after the previous one ends, and the last ends in the census year 2021.
        /// </summary>
        [Fact]
        public void YearBuiltRange_CensusPeriods()
        {
            List<(Variable Variable, Classes.YearBuiltRange YearBuiltRange)> periods = [];
            foreach (Variable variable in Enum.GetValues<Variable>())
            {
                if (Query.YearBuiltRange(variable) is Classes.YearBuiltRange yearBuiltRange)
                {
                    periods.Add((variable, yearBuiltRange));
                }
            }

            Assert.Equal(9, periods.Count);

            periods = [.. periods.OrderBy(x => x.YearBuiltRange.LastYear)];

            Assert.Null(periods[0].YearBuiltRange.FirstYear);
            Assert.Equal((short)1917, periods[0].YearBuiltRange.LastYear);
            Assert.Equal(Variable.inhabited_dwellings_built_before_1918_census_2021, periods[0].Variable);

            for (int i = 1; i < periods.Count; i++)
            {
                Assert.Equal((short)(periods[i - 1].YearBuiltRange.LastYear + 1), periods[i].YearBuiltRange.FirstYear);
                Assert.True(periods[i].YearBuiltRange.FirstYear <= periods[i].YearBuiltRange.LastYear);
            }

            Assert.Equal((short)2021, periods[periods.Count - 1].YearBuiltRange.LastYear);

            // The period holding 2008, the first year of the year built predictor, runs past it and is pro-rated by DiGi.GIS.
            Assert.Contains(periods, x => x.YearBuiltRange.FirstYear <= 2008 && x.YearBuiltRange.LastYear > 2008);

            foreach ((Variable variable, Classes.YearBuiltRange _) in periods)
            {
                Assert.Equal(Constants.Name.SubGroup.P4386, Query.SubGroup(variable)?.Name);
                Assert.Equal(Constants.Name.Group.G645, Query.Group(variable)?.Name);
                Assert.Equal(Constants.Name.Category.K31, Query.Category(variable)?.Name);
            }
        }

        /// <summary>
        /// A variable that is not a count by construction period has no year range: the census total, population and a null value.
        /// </summary>
        [Fact]
        public void YearBuiltRange_NotPeriod()
        {
            Assert.Null(Query.YearBuiltRange(Variable.inhabited_dwellings_total_census_2021));
            Assert.Null(Query.YearBuiltRange(Variable.population_thousand_persons));
            Assert.Null(Query.YearBuiltRange(null));

            Assert.Equal(Constants.Name.SubGroup.P4386, Query.SubGroup(Variable.inhabited_dwellings_total_census_2021)?.Name);
            Assert.Equal(1723518, (int)Variable.inhabited_dwellings_total_census_2021);
        }
    }
}
