using DiGi.BDL.Classes;
using DiGi.BDL.Enums;
using DiGi.Core.Classes;
using DiGi.GIS.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// <c>Modify.WriteByVariableAsync</c> with a recorded fetch instead of BDL (DiGi-Organization/DiGi.GIS.PostgreSQL#149).
        /// <para>Units: a county (level 5), Warsaw and Mokotów (level 6); Warsaw already holds the population, at level 6 only. Expected: the gmina level goes first; the population is asked at level 6 only (Mokotów lacks it) and never at level 5, where no unit holds it; the two census variables no unit holds are asked at both levels, the level-5 fetches failing and being reported; Warsaw keeps its population and gains the census, Mokotów gets a new collection, the county none, and a unit not given is not written.</para>
        /// <para>A fetch that keeps failing stops the run after three failures in a row, the pairs not attempted reported as failed.</para>
        /// </summary>
        [Fact]
        public async Task WriteByVariableAsync_RecordedFetch()
        {
            string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory));
            Directory.CreateDirectory(directory!);
            string path = System.IO.Path.Combine(directory!, "WriteByVariableAsync_" + Guid.NewGuid().ToString("N") + ".sdcf");

            StatisticalUnit statisticalUnit_County = new(Guid.NewGuid(), new UnitCode("071412865000"), "POWIAT M. ST. WARSZAWA", null);
            StatisticalUnit statisticalUnit_Warsaw = new(Guid.NewGuid(), new UnitCode("071412865011"), "M.st.Warszawa od 2002", null);
            StatisticalUnit statisticalUnit_Mokotow = new(Guid.NewGuid(), new UnitCode("071412865048"), "Mokotów - dzielnica", null);
            List<StatisticalUnit> statisticalUnits = [statisticalUnit_County, statisticalUnit_Warsaw, statisticalUnit_Mokotow];

            try
            {
                StatisticalDataCollection statisticalDataCollection_Warsaw = new(Guid.NewGuid(), new UnitCode("071412865011"));
                statisticalDataCollection_Warsaw.Add(WriteByVariableAsync_StatisticalYearlyDoubleData(Variable.population_thousand_persons, 2020, 1860.0));
                using (StatisticalDataCollectionFile statisticalDataCollectionFile = new(path))
                {
                    statisticalDataCollectionFile.AddValue(statisticalDataCollection_Warsaw);
                    Assert.True(statisticalDataCollectionFile.Save());
                }

                List<(Variable Variable, int UnitLevel)> calls = [];
                Task<Dictionary<string, UnitYearlyValues>?> Fetch(Variable variable, int unitLevel)
                {
                    calls.Add((variable, unitLevel));
                    if (unitLevel != 6)
                    {
                        return Task.FromResult<Dictionary<string, UnitYearlyValues>?>(null);
                    }

                    Dictionary<string, UnitYearlyValues> result = [];
                    foreach ((string unitId, double value) in new[] { ("071412865011", 829426.0), ("071412865048", 120000.0), ("999999999999", 1.0) })
                    {
                        result[unitId] = new UnitYearlyValues() { unitId = unitId, results = [new YearlyValues() { id = (int)variable, values = [new YearlyValue() { year = "2021", val = value }] }] };
                    }

                    return Task.FromResult<Dictionary<string, UnitYearlyValues>?>(result);
                }

                Variable[] variables = [Variable.population_thousand_persons, Variable.inhabited_dwellings_total_census_2021, Variable.inhabited_dwellings_built_1945_1970_census_2021];
                (int CollectionCount, List<(Variable Variable, int UnitLevel)> Failures)? result = await Modify.WriteByVariableAsync(statisticalUnits, path, variables, [2021], unitYearlyValuesDictionaryFunc: Fetch);

                Assert.NotNull(result);
                Assert.Equal(
                    [(Variable.population_thousand_persons, 6), (Variable.inhabited_dwellings_total_census_2021, 6), (Variable.inhabited_dwellings_built_1945_1970_census_2021, 6), (Variable.inhabited_dwellings_total_census_2021, 5), (Variable.inhabited_dwellings_built_1945_1970_census_2021, 5)],
                    calls);
                Assert.Equal([(Variable.inhabited_dwellings_total_census_2021, 5), (Variable.inhabited_dwellings_built_1945_1970_census_2021, 5)], result.Value.Failures);
                Assert.Equal(2, result.Value.CollectionCount);

                using (StatisticalDataCollectionFile statisticalDataCollectionFile = new(path))
                {
                    statisticalDataCollectionFile.Open();
                    StatisticalDataCollection? statisticalDataCollection_Warsaw_Read = statisticalDataCollectionFile.GetValue(StatisticalDataCollectionFile.GetUniqueReference("071412865011")!);
                    StatisticalDataCollection? statisticalDataCollection_Mokotow_Read = statisticalDataCollectionFile.GetValue(StatisticalDataCollectionFile.GetUniqueReference("071412865048")!);
                    Assert.NotNull(statisticalDataCollection_Warsaw_Read);
                    Assert.NotNull(statisticalDataCollection_Mokotow_Read);
                    Assert.Null(statisticalDataCollectionFile.GetValue(StatisticalDataCollectionFile.GetUniqueReference("071412865000")!));
                    Assert.Null(statisticalDataCollectionFile.GetValue(StatisticalDataCollectionFile.GetUniqueReference("999999999999")!));

                    Assert.Equal(1860.0, WriteByVariableAsync_Value(statisticalDataCollection_Warsaw_Read, Variable.population_thousand_persons, 2020));
                    Assert.Equal(829426.0, WriteByVariableAsync_Value(statisticalDataCollection_Warsaw_Read, Variable.inhabited_dwellings_total_census_2021, 2021));
                    Assert.Equal(829426.0, WriteByVariableAsync_Value(statisticalDataCollection_Warsaw_Read, Variable.inhabited_dwellings_built_1945_1970_census_2021, 2021));
                    Assert.Equal(120000.0, WriteByVariableAsync_Value(statisticalDataCollection_Mokotow_Read, Variable.population_thousand_persons, 2021));
                    Assert.Equal(120000.0, WriteByVariableAsync_Value(statisticalDataCollection_Mokotow_Read, Variable.inhabited_dwellings_total_census_2021, 2021));
                }

                // A fetch that always fails stops after three failures in a row; nothing is written.
                calls.Clear();
                Task<Dictionary<string, UnitYearlyValues>?> Fail(Variable variable, int unitLevel)
                {
                    calls.Add((variable, unitLevel));
                    return Task.FromResult<Dictionary<string, UnitYearlyValues>?>(null);
                }

                Variable[] variables_Census = [.. Enum.GetValues<Variable>().Where(x => BDL.Query.YearBuiltRange(x) is not null)];
                (int CollectionCount, List<(Variable Variable, int UnitLevel)> Failures)? result_Failed = await Modify.WriteByVariableAsync(statisticalUnits, path, variables_Census, [2021], overrideExisting: true, unitYearlyValuesDictionaryFunc: Fail);
                Assert.NotNull(result_Failed);
                Assert.Equal(3, calls.Count);
                Assert.Equal(0, result_Failed.Value.CollectionCount);
                Assert.True(result_Failed.Value.Failures.Count > 3);
                Assert.Equal(calls, result_Failed.Value.Failures.Take(3));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static StatisticalYearlyDoubleData WriteByVariableAsync_StatisticalYearlyDoubleData(Variable variable, short year, double value)
        {
            return new StatisticalYearlyDoubleData(Core.Query.Description(variable), ((int)variable).ToString(System.Globalization.CultureInfo.InvariantCulture), [new KeyValuePair<short, double>(year, value)]);
        }

        private static double? WriteByVariableAsync_Value(StatisticalDataCollection statisticalDataCollection, Variable variable, short year)
        {
            StatisticalYearlyDoubleData? statisticalYearlyDoubleData = Query.StatisticalData<StatisticalYearlyDoubleData>(statisticalDataCollection, variable);
            return statisticalYearlyDoubleData is not null && statisticalYearlyDoubleData.TryGetValue(year, out double value) ? value : null;
        }
    }
}
