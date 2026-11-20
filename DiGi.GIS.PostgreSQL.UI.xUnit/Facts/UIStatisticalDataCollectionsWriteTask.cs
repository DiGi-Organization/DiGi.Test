using DiGi.GIS.Classes;
using DiGi.GIS.PostgreSQL.UI.Classes;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// <see cref="Query.BDLClientId"/> reads the <c>ClientId</c> key of a BDL.conf, trimmed; a missing file, a missing key and an empty value give null (DiGi-Organization/DiGi.GIS.PostgreSQL#149).
        /// </summary>
        [Fact]
        public void Query_BDLClientId()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "BDL.conf");

                Assert.Null(Query.BDLClientId(path));

                File.WriteAllText(path, "ClientId= 0123-abcd \nOther=1\n");
                Assert.Equal("0123-abcd", Query.BDLClientId(path));

                File.WriteAllText(path, "Other=1\n");
                Assert.Null(Query.BDLClientId(path));

                File.WriteAllText(path, "ClientId=\n");
                Assert.Null(Query.BDLClientId(path));

                // The committed template (DiGi.GIS.PostgreSQL.UI/files/BDL.conf): comments and a quoted empty id read as no id.
                File.WriteAllText(path, "# BDL API client id (X-ClientId). Empty means anonymous.\r\nClientId=\"\"\r\n");
                Assert.Null(Query.BDLClientId(path));

                File.WriteAllText(path, "# BDL API client id (X-ClientId). Empty means anonymous.\r\nClientId=\"0123-abcd\"\r\n");
                Assert.Equal("0123-abcd", Query.BDLClientId(path));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// <see cref="UIStatisticalDataCollectionsWriteTask"/> with its files set needs no dialog (DiGi-Organization/DiGi.GIS.PostgreSQL#149): a missing unit file and a unit file holding no units fail the run; a real unit file with no variable to fetch reads its units, sends no request, writes nothing and succeeds.
        /// </summary>
        [Fact]
        public async Task UIStatisticalDataCollectionsWriteTask_Files()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string path_Units = Path.Combine(directory, "Units.suf");
                string path_Collections = Path.Combine(directory, "StatisticalDataCollections.sdcf");

                ProbeUIStatisticalDataCollectionsWriteTask uIStatisticalDataCollectionsWriteTask_Missing = new() { StatisticalUnitFilePath = path_Units, Path = path_Collections, ClientId = "xunit" };
                Assert.False(await uIStatisticalDataCollectionsWriteTask_Missing.RunAsync());

                StatisticalUnit statisticalUnit = new(Guid.NewGuid(), new UnitCode("071412865000"), "POWIAT M. ST. WARSZAWA", [new StatisticalUnit(Guid.NewGuid(), new UnitCode("071412865048"), "Mokotów - dzielnica", null)]);
                using (StatisticalUnitFile statisticalUnitFile = new(path_Units))
                {
                    statisticalUnitFile.Value = statisticalUnit;
                    Assert.True(statisticalUnitFile.Save());
                }

                ProbeUIStatisticalDataCollectionsWriteTask uIStatisticalDataCollectionsWriteTask = new() { StatisticalUnitFilePath = path_Units, Path = path_Collections, ClientId = "xunit", Variables = [] };
                Assert.True(await uIStatisticalDataCollectionsWriteTask.RunAsync());
                Assert.Equal(0, uIStatisticalDataCollectionsWriteTask.CollectionCount);
                Assert.Equal(0, uIStatisticalDataCollectionsWriteTask.FailureCount);
                Assert.False(File.Exists(path_Collections));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Runs <see cref="UIStatisticalDataCollectionsWriteTask"/> in the test thread, without the background-task wrapper.
        /// </summary>
        private sealed class ProbeUIStatisticalDataCollectionsWriteTask : UIStatisticalDataCollectionsWriteTask
        {
            /// <summary>
            /// Runs the task.
            /// </summary>
            /// <returns>The task's own result.</returns>
            public Task<bool> RunAsync()
            {
                return ExecuteAsync(new Progress<long>(), System.Threading.CancellationToken.None);
            }
        }
    }
}
