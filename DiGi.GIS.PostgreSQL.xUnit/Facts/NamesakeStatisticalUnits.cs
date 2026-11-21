using DiGi.BDL.Enums;
using DiGi.GIS.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Query.NamesakeStatisticalUnits"/> (DiGi-Organization/DiGi.GIS.PostgreSQL#149): the other municipality units of the same name in the same voivodeship, in code order - never the town and rural parts of an urban-rural gmina, never a namesake of another voivodeship, never the unit itself.
        /// </summary>
        [Fact]
        public void Query_NamesakeStatisticalUnits()
        {
            StatisticalUnit? rootStatisticalUnit = NamesakeStatisticalUnits_Root();
            Assert.NotNull(rootStatisticalUnit);

            StatisticalUnit? statisticalUnit_Current = Query.Match(rootStatisticalUnit, "Czerwińsk nad Wisłą", "1420043", Enums.AdministrativeArealType.Municipality);
            Assert.Equal("071422520043", statisticalUnit_Current?.Code);
            Assert.Equal(["071422520042"], Query.NamesakeStatisticalUnits(rootStatisticalUnit, statisticalUnit_Current).ConvertAll(x => x.Code));

            StatisticalUnit? statisticalUnit_NoweMiasto = Query.Match(rootStatisticalUnit, "Nowe Miasto", "1420083", Enums.AdministrativeArealType.Municipality);
            Assert.Equal("071422520083", statisticalUnit_NoweMiasto?.Code);
            Assert.Equal(["071422520082"], Query.NamesakeStatisticalUnits(rootStatisticalUnit, statisticalUnit_NoweMiasto).ConvertAll(x => x.Code));

            StatisticalUnit? statisticalUnit_Baboszewo = Query.Match(rootStatisticalUnit, "Baboszewo", "1420032", Enums.AdministrativeArealType.Municipality);
            Assert.Empty(Query.NamesakeStatisticalUnits(rootStatisticalUnit, statisticalUnit_Baboszewo));

            Assert.Empty(Query.NamesakeStatisticalUnits(null, statisticalUnit_Current));
            Assert.Empty(Query.NamesakeStatisticalUnits(rootStatisticalUnit, null));
        }

        /// <summary>
        /// Verifies <see cref="Create.MergedStatisticalDataCollection"/> on the two shapes BDL leaves a gmina that changed its type in (DiGi-Organization/DiGi.GIS.PostgreSQL#149), each a collection of the unit the code names merged with its historical namesake's.
        /// <para>Czerwińsk nad Wisłą: the current unit holds the census and the population from 2020, 0 before; the historical one the population up to 2019, 0 after. Nowe Miasto: the current unit holds the population from 2022 only and no census; the historical one the census and the population up to 2021. Either way the merge holds the census and every year's non-zero population; the census year built is the one the census gives; the inputs are not changed; and a year every unit leaves at 0 stays 0.</para>
        /// <para>A town and the rural gmina of its name (Płońsk) both hold a census: they are two gminas, and the rural one's population does not fill the town's gap.</para>
        /// </summary>
        [Fact]
        public void Create_MergedStatisticalDataCollection()
        {
            // Czerwińsk nad Wisłą: the census and the late population on the current unit.
            StatisticalDataCollection statisticalDataCollection_Current = new(Guid.NewGuid(), new UnitCode("071422520043"), [MergedStatisticalDataCollection_Population(2008, 2023, 2020, 7200), .. MergedStatisticalDataCollection_Census()]);
            StatisticalDataCollection statisticalDataCollection_Historical = new(Guid.NewGuid(), new UnitCode("071422520042"), [MergedStatisticalDataCollection_Population(2008, 2023, 2008, 7600, 2019)]);

            StatisticalDataCollection? statisticalDataCollection_Merged = Create.MergedStatisticalDataCollection(statisticalDataCollection_Current, [statisticalDataCollection_Historical]);
            Assert.NotNull(statisticalDataCollection_Merged);
            Assert.Equal("071422520043", statisticalDataCollection_Merged.Code);
            Assert.Equal(Enumerable.Range(2008, 16).Select(x => x < 2020 ? 7.6 : 7.2), MergedStatisticalDataCollection_Values(statisticalDataCollection_Merged, 2008, 2023));
            Assert.NotNull(GIS.Query.CensusYearBuilt(statisticalDataCollection_Merged));
            Assert.Equal(GIS.Query.CensusYearBuilt(statisticalDataCollection_Current), GIS.Query.CensusYearBuilt(statisticalDataCollection_Merged));

            // The inputs keep their gaps.
            Assert.Equal(0.0, MergedStatisticalDataCollection_Values(statisticalDataCollection_Current, 2008, 2008).Single());
            Assert.Equal(0.0, MergedStatisticalDataCollection_Values(statisticalDataCollection_Historical, 2021, 2021).Single());

            // Nowe Miasto: nothing up to 2021 on the current unit, the census and the early population on the historical one.
            StatisticalDataCollection statisticalDataCollection_NoweMiasto = new(Guid.NewGuid(), new UnitCode("071422520083"), [MergedStatisticalDataCollection_Population(2008, 2023, 2022, 4550)]);
            StatisticalDataCollection statisticalDataCollection_NoweMiasto_Historical = new(Guid.NewGuid(), new UnitCode("071422520082"), [MergedStatisticalDataCollection_Population(2008, 2023, 2008, 4700, 2021), .. MergedStatisticalDataCollection_Census()]);
            Assert.Null(GIS.Query.CensusYearBuilt(statisticalDataCollection_NoweMiasto));

            StatisticalDataCollection? statisticalDataCollection_NoweMiasto_Merged = Create.MergedStatisticalDataCollection(statisticalDataCollection_NoweMiasto, [statisticalDataCollection_NoweMiasto_Historical]);
            Assert.NotNull(statisticalDataCollection_NoweMiasto_Merged);
            Assert.Equal("071422520083", statisticalDataCollection_NoweMiasto_Merged.Code);
            Assert.Equal(GIS.Query.CensusYearBuilt(statisticalDataCollection_NoweMiasto_Historical), GIS.Query.CensusYearBuilt(statisticalDataCollection_NoweMiasto_Merged));
            Assert.Equal(Enumerable.Range(2008, 16).Select(x => x < 2022 ? 4.7 : 4.55), MergedStatisticalDataCollection_Values(statisticalDataCollection_NoweMiasto_Merged, 2008, 2023));

            // A town and the rural gmina of its name (Płońsk) each hold their own census: the rural gmina is another gmina, so
            // its population never fills a gap of the town's.
            StatisticalDataCollection statisticalDataCollection_Town = new(Guid.NewGuid(), new UnitCode("071422520011"), [MergedStatisticalDataCollection_Population(2008, 2010, 2008, 22000, 2009), .. MergedStatisticalDataCollection_Census()]);
            StatisticalDataCollection statisticalDataCollection_Rural = new(Guid.NewGuid(), new UnitCode("071422520092"), [MergedStatisticalDataCollection_Population(2008, 2010, 2008, 7000), .. MergedStatisticalDataCollection_Census()]);
            Assert.Equal([22.0, 22.0, 0.0], MergedStatisticalDataCollection_Values(Create.MergedStatisticalDataCollection(statisticalDataCollection_Town, [statisticalDataCollection_Rural])!, 2008, 2010));

            // A year no unit holds a value for stays 0; no collection gives null; a missing own collection takes the namesake's.
            StatisticalDataCollection statisticalDataCollection_Gap = new(Guid.NewGuid(), new UnitCode("071422520011"), [MergedStatisticalDataCollection_Population(2008, 2010, 2009, 1000, 2009)]);
            Assert.Equal([0.0, 1.0, 0.0], MergedStatisticalDataCollection_Values(Create.MergedStatisticalDataCollection(statisticalDataCollection_Gap, [statisticalDataCollection_Gap])!, 2008, 2010));
            Assert.Null(Create.MergedStatisticalDataCollection(null, [null]));
            Assert.Equal("071422520082", Create.MergedStatisticalDataCollection(null, [statisticalDataCollection_NoweMiasto_Historical])?.Code);
        }

        /// <summary>
        /// Builds the płoński county hierarchy of the namesake facts: Czerwińsk nad Wisłą and Nowe Miasto each under a historical and a current unit (with the town and rural parts of the current one), Baboszewo under one, and a Nowe Miasto of another voivodeship.
        /// </summary>
        /// <returns>The root statistical unit.</returns>
        private static StatisticalUnit? NamesakeStatisticalUnits_Root()
        {
            List<BDL.Classes.Unit> units =
            [
                new BDL.Classes.Unit { id = "000000000000", name = "POLSKA", level = 0 },
                new BDL.Classes.Unit { id = "070000000000", name = "MAKROREGION WOJEWÓDZTWO MAZOWIECKIE", level = 1 },
                new BDL.Classes.Unit { id = "071400000000", name = "MAZOWIECKIE", level = 2 },
                new BDL.Classes.Unit { id = "071420000000", name = "REGION MAZOWIECKI REGIONALNY", level = 3 },
                new BDL.Classes.Unit { id = "071422500000", name = "PODREGION CIECHANOWSKI", level = 4 },
                new BDL.Classes.Unit { id = "071422520000", name = "POWIAT PŁOŃSKI", level = 5 },
                new BDL.Classes.Unit { id = "071422520032", name = "Baboszewo", level = 6 },
                new BDL.Classes.Unit { id = "071422520042", name = "Czerwińsk nad Wisłą", level = 6 },
                new BDL.Classes.Unit { id = "071422520043", name = "Czerwińsk nad Wisłą", level = 6 },
                new BDL.Classes.Unit { id = "071422520044", name = "Czerwińsk nad Wisłą - miasto", level = 6 },
                new BDL.Classes.Unit { id = "071422520045", name = "Czerwińsk nad Wisłą - obszar wiejski", level = 6 },
                new BDL.Classes.Unit { id = "071422520082", name = "Nowe Miasto", level = 6 },
                new BDL.Classes.Unit { id = "071422520083", name = "Nowe Miasto", level = 6 },
                new BDL.Classes.Unit { id = "071422520084", name = "Nowe Miasto - miasto", level = 6 },
                new BDL.Classes.Unit { id = "071422520085", name = "Nowe Miasto - obszar wiejski", level = 6 },
                new BDL.Classes.Unit { id = "020000000000", name = "MAKROREGION PÓŁNOCNO-ZACHODNI", level = 1 },
                new BDL.Classes.Unit { id = "023000000000", name = "WIELKOPOLSKIE", level = 2 },
                new BDL.Classes.Unit { id = "023010000000", name = "REGION WIELKOPOLSKI", level = 3 },
                new BDL.Classes.Unit { id = "023016100000", name = "PODREGION KONIŃSKI", level = 4 },
                new BDL.Classes.Unit { id = "023016125000", name = "POWIAT SŁUPECKI", level = 5 },
                new BDL.Classes.Unit { id = "023016125092", name = "Nowe Miasto", level = 6 }
            ];

            return GIS.Create.StatisticalUnit(units);
        }

        /// <summary>
        /// Builds a population series in thousands of persons (<see cref="Variable.population_thousand_persons"/>) from <paramref name="firstYear"/> to <paramref name="lastYear"/>: <paramref name="value"/> / 1000 from <paramref name="fromYear"/> to <paramref name="toYear"/>, 0 - BDL's answer for a year it has not published - elsewhere.
        /// </summary>
        /// <param name="firstYear">The first year of the series.</param>
        /// <param name="lastYear">The last year of the series.</param>
        /// <param name="fromYear">The first year with a value.</param>
        /// <param name="value">The population, in persons.</param>
        /// <param name="toYear">The last year with a value; null for <paramref name="lastYear"/>.</param>
        /// <returns>The series.</returns>
        private static StatisticalYearlyDoubleData MergedStatisticalDataCollection_Population(short firstYear, short lastYear, short fromYear, double value, short? toYear = null)
        {
            List<KeyValuePair<short, double>> values = [];
            for (short year = firstYear; year <= lastYear; year++)
            {
                values.Add(new KeyValuePair<short, double>(year, year >= fromYear && year <= (toYear ?? lastYear) ? value / 1000 : 0));
            }

            return new StatisticalYearlyDoubleData(Core.Query.Description(Variable.population_thousand_persons), ((int)Variable.population_thousand_persons).ToString(System.Globalization.CultureInfo.InvariantCulture), values);
        }

        /// <summary>
        /// Builds the ten NSP 2021 dwelling series of a gmina whose stock is mostly of 1971-1978.
        /// </summary>
        /// <returns>The series.</returns>
        private static List<StatisticalYearlyDoubleData> MergedStatisticalDataCollection_Census()
        {
            List<(Variable Variable, double Count)> counts =
            [
                (Variable.inhabited_dwellings_total_census_2021, 2000),
                (Variable.inhabited_dwellings_built_before_1918_census_2021, 50),
                (Variable.inhabited_dwellings_built_1918_1944_census_2021, 100),
                (Variable.inhabited_dwellings_built_1945_1970_census_2021, 400),
                (Variable.inhabited_dwellings_built_1971_1978_census_2021, 900),
                (Variable.inhabited_dwellings_built_1979_1988_census_2021, 300),
                (Variable.inhabited_dwellings_built_1989_2002_census_2021, 150),
                (Variable.inhabited_dwellings_built_2003_2011_census_2021, 60),
                (Variable.inhabited_dwellings_built_2012_2016_census_2021, 20),
                (Variable.inhabited_dwellings_built_2017_2021_census_2021, 20)
            ];

            return counts.ConvertAll(x => new StatisticalYearlyDoubleData(Core.Query.Description(x.Variable), ((int)x.Variable).ToString(System.Globalization.CultureInfo.InvariantCulture), [new KeyValuePair<short, double>(2021, x.Count)]));
        }

        /// <summary>
        /// Reads the population series of a collection in thousands of persons, one value per year, 0 for a year without one.
        /// </summary>
        /// <param name="statisticalDataCollection">The collection.</param>
        /// <param name="firstYear">The first year.</param>
        /// <param name="lastYear">The last year.</param>
        /// <returns>The values, rounded to 0.01.</returns>
        private static List<double> MergedStatisticalDataCollection_Values(StatisticalDataCollection statisticalDataCollection, short firstYear, short lastYear)
        {
            StatisticalYearlyDoubleData? statisticalYearlyDoubleData = GIS.Query.StatisticalData<StatisticalYearlyDoubleData>(statisticalDataCollection, Variable.population_thousand_persons);
            List<double> result = [];
            for (short year = firstYear; year <= lastYear; year++)
            {
                result.Add(statisticalYearlyDoubleData is not null && statisticalYearlyDoubleData.TryGetValue(year, out double value) ? Math.Round(value, 2) : 0);
            }

            return result;
        }
    }
}
