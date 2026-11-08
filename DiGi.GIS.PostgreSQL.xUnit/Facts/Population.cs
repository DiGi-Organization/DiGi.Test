using DiGi.BDL.Enums;
using DiGi.GIS.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that Query.Population extracts and scales demographic population data from BDL variable representations and direct Population names.
        /// </summary>
        [Fact]
        public void Query_Population()
        {
            // Null collection check
            StatisticalDataCollection? collection_Null = null;
            Assert.Null(collection_Null.Population());

            // Empty collection check
            StatisticalDataCollection collection_Empty = new(Guid.NewGuid(), new UnitCode("012345678901"));
            Assert.Null(collection_Empty.Population());

            // Direct "Population" named series
            List<KeyValuePair<short, double>> directValues =
            [
                new(2020, 50000.0),
                new(2021, 51000.0)
            ];
            StatisticalYearlyDoubleData directData = new("Population", "Population", directValues);
            StatisticalDataCollection collection_Direct = new(Guid.NewGuid(), new UnitCode("012345678901"));
            collection_Direct.Add(directData);

            StatisticalYearlyDoubleData? resolvedDirect = collection_Direct.Population();
            Assert.NotNull(resolvedDirect);
            Assert.True(resolvedDirect.TryGetValue(2020, out double val2020));
            Assert.Equal(50000.0, val2020);

            // BDL population_thousand_persons series (e.g. 15.5 thousand persons -> 15500.0 persons)
            List<KeyValuePair<short, double>> bdlValues =
            [
                new(2018, 12.345),
                new(2019, 13.0)
            ];
            string bdlRef = ((int)Variable.population_thousand_persons).ToString(CultureInfo.CurrentCulture);
            StatisticalYearlyDoubleData bdlData = new(Core.Query.Description(Variable.population_thousand_persons), bdlRef, bdlValues);
            StatisticalDataCollection collection_Bdl = new(Guid.NewGuid(), new UnitCode("012345678901"));
            collection_Bdl.Add(bdlData);

            StatisticalYearlyDoubleData? resolvedBdl = collection_Bdl.Population();
            Assert.NotNull(resolvedBdl);
            Assert.True(resolvedBdl.TryGetValue(2018, out double bdlVal2018));
            Assert.Equal(12345.0, bdlVal2018);
            Assert.True(resolvedBdl.TryGetValue(2019, out double bdlVal2019));
            Assert.Equal(13000.0, bdlVal2019);
        }
    }
}
