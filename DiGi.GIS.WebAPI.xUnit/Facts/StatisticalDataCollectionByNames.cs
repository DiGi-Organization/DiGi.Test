using DiGi.GIS.Classes;
using DiGi.GIS.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the <c>names</c> filter of <c>gis/unit/statisticaldatacollection</c> selects a variable by its stored name or by its BDL variable identifier, keeps the collection's identity, lists a variable once however it was selected, and leaves the collection untouched when nothing is requested.
        /// <para>The deployed 0.8.9 matched names only, so <c>names=60559</c> answered an empty collection for Warsaw although the variable was stored (2026-11-12).</para>
        /// </summary>
        [Fact]
        public void StatisticalDataCollectionByNames_NameOrReference()
        {
            StatisticalYearlyDoubleData statisticalYearlyDoubleData_Density = new("population [person/km2]", "60559", [new KeyValuePair<short, double>(2023, 3599.4)]);
            StatisticalYearlyDoubleData statisticalYearlyDoubleData_Volume = new("one-dwelling residential buildings not adapted to permanent residence [m3]", "746080", [new KeyValuePair<short, double>(2023, 0)]);
            StatisticalYearlyDoubleData statisticalYearlyDoubleData_Area = new("one-dwelling residential buildings not adapted to permanent residence [m2]", "746081", [new KeyValuePair<short, double>(2023, 0)]);

            Guid guid = Guid.NewGuid();
            StatisticalDataCollection statisticalDataCollection = new(guid, GIS.Create.UnitCode("071412865000"), [statisticalYearlyDoubleData_Density, statisticalYearlyDoubleData_Volume, statisticalYearlyDoubleData_Area]);

            StatisticalDataCollection? statisticalDataCollection_ByReference = statisticalDataCollection.StatisticalDataCollectionByNames(["60559"]);
            Assert.NotNull(statisticalDataCollection_ByReference);
            Assert.Equal(guid, statisticalDataCollection_ByReference.Guid);
            Assert.Equal("071412865000", statisticalDataCollection_ByReference.Code);
            Assert.Equal(["population [person/km2]"], statisticalDataCollection_ByReference.Names);

            StatisticalDataCollection? statisticalDataCollection_ByName = statisticalDataCollection.StatisticalDataCollectionByNames(["population [person/km2]"]);
            Assert.Equal(["population [person/km2]"], statisticalDataCollection_ByName?.Names);

            // Name and identifier of the same variable, plus an identifier of another one: each variable once.
            StatisticalDataCollection? statisticalDataCollection_Mixed = statisticalDataCollection.StatisticalDataCollectionByNames(["60559", "population [person/km2]", "746081"]);
            List<IStatisticalData> statisticalDatas_Mixed = [.. statisticalDataCollection_Mixed?.StatisticalDatas ?? []];
            Assert.Equal(2, statisticalDatas_Mixed.Count);
            Assert.Equal(["60559", "746081"], statisticalDatas_Mixed.Select(x => x.Reference).OrderBy(x => x, StringComparer.Ordinal));

            // Matching is exact: a prefix or a different case selects nothing.
            Assert.Empty(statisticalDataCollection.StatisticalDataCollectionByNames(["6055", "POPULATION [person/km2]"])?.StatisticalDatas ?? []);

            // Nothing requested (absent or blank) returns the collection as it is.
            Assert.Same(statisticalDataCollection, statisticalDataCollection.StatisticalDataCollectionByNames(null));
            Assert.Same(statisticalDataCollection, statisticalDataCollection.StatisticalDataCollectionByNames(["", "  "]));

            Assert.Null(((StatisticalDataCollection?)null).StatisticalDataCollectionByNames(["60559"]));
        }
    }
}
