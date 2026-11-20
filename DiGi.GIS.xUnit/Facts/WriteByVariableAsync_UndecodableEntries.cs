using DiGi.BDL.Classes;
using DiGi.BDL.Enums;
using DiGi.GIS.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// <c>Modify.WriteByVariableAsync</c> on a file holding a collection keyed in a former reference format returns null and fetches nothing (DiGi-Organization/DiGi.GIS.PostgreSQL#149): read through the current keys such a file looks empty, so every variable of every unit would be fetched again - the run of 2026-11-20 spent the anonymous BDL quota that way - and the save would leave the old entries beside the new ones.
        /// </summary>
        [Fact]
        public async Task WriteByVariableAsync_UndecodableEntries()
        {
            string? directory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(directory));
            Directory.CreateDirectory(directory!);
            string path = System.IO.Path.Combine(directory!, "WriteByVariableAsync_UndecodableEntries_" + Guid.NewGuid().ToString("N") + ".sdcf");

            try
            {
                StatisticalDataCollection statisticalDataCollection = new(Guid.NewGuid(), new UnitCode("071412865011"));
                using (StatisticalDataCollectionFile statisticalDataCollectionFile = new(path))
                {
                    statisticalDataCollectionFile.AddValue(statisticalDataCollection);
                    Assert.True(statisticalDataCollectionFile.Save());
                }

                using (ZipArchive zipArchive = ZipFile.Open(path, ZipArchiveMode.Update))
                {
                    string entryName = System.IO.Path.Combine(Core.IO.Constants.EntryName.Values, "DiGi.GIS.Classes.StatisticalDataCollection%2cDiGi.GIS%3a%3a%22071412865048%22");
                    using StreamWriter streamWriter = new(zipArchive.CreateEntry(entryName).Open());
                    streamWriter.Write(Core.Convert.ToSystem_String(new StatisticalDataCollection(Guid.NewGuid(), new UnitCode("071412865048"))));
                }

                byte[] bytes = File.ReadAllBytes(path);
                int callCount = 0;
                Task<Dictionary<string, UnitYearlyValues>?> Fetch(Variable variable, int unitLevel)
                {
                    callCount++;
                    return Task.FromResult<Dictionary<string, UnitYearlyValues>?>([]);
                }

                List<StatisticalUnit> statisticalUnits = [new(Guid.NewGuid(), new UnitCode("071412865011"), "M.st.Warszawa od 2002", null), new(Guid.NewGuid(), new UnitCode("071412865048"), "Mokotów - dzielnica", null)];
                (int CollectionCount, List<(Variable Variable, int UnitLevel)> Failures)? result = await Modify.WriteByVariableAsync(statisticalUnits, path, [Variable.inhabited_dwellings_total_census_2021], [2021], unitYearlyValuesDictionaryFunc: Fetch);

                Assert.Null(result);
                Assert.Equal(0, callCount);
                Assert.Equal(bytes, File.ReadAllBytes(path));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
