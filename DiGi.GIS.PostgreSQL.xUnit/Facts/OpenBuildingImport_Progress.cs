using DiGi.GIS.IO.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies, without a database, that the open building import's footprint stream moves the shared <see cref="OpenBuildingSourcePosition"/> to each source file as the run reads it, so a progress line can name the file and its position (n of N) while the run is open (DiGi.GIS.PostgreSQL#169).
        /// <para>Two copies of the GHS-OBAT CSV fixture are read as one stream: the position is empty before the enumeration starts, the first footprint is read under the first file (1 of 2) and the last under the second (2 of 2), and the two-file stream yields exactly twice the footprints of the same fixture read alone - the position bookkeeping neither drops nor duplicates a footprint. The copies are removed in <c>finally</c>. Short test (no database).</para>
        /// </summary>
        [Fact]
        public async Task OpenBuildingImport_ProgressReportsTheCurrentSourceFile()
        {
            string? path_Fixture = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "GHS_OBAT_Sample.csv");
            Assert.NotNull(path_Fixture);

            string path_First = Path.Combine(Path.GetTempPath(), $"xunit-open-building-progress-first-{Guid.NewGuid():N}.csv");
            string path_Second = Path.Combine(Path.GetTempPath(), $"xunit-open-building-progress-second-{Guid.NewGuid():N}.csv");

            try
            {
                File.Copy(path_Fixture, path_First);
                File.Copy(path_Fixture, path_Second);

                string? tableName = Query.OpenBuildingTableName("ghs_obat");
                Assert.NotNull(tableName);

                // The same fixture read alone: the two-file stream must carry exactly twice as many
                // footprints, so the position bookkeeping is proved to lose and duplicate nothing
                // (a differential assertion rather than a count pinned to the fixture).
                PostgreSQLOpenBuildingImportTaskOptions postgreSQLOpenBuildingImportTaskOptions_Single = new()
                {
                    DatasetKey = "ghs_obat",
                    SourcePaths = [path_First],
                };

                IAsyncEnumerable<Footprint>? footprints_Single = PostgreSQLOpenBuildingImportTask.Footprints(tableName, postgreSQLOpenBuildingImportTaskOptions_Single, null, [], null, default);
                Assert.NotNull(footprints_Single);

                long count_Single = 0;
                await foreach (Footprint footprint in footprints_Single.WithCancellation(default))
                {
                    Assert.NotNull(footprint);
                    count_Single++;
                }

                Assert.True(count_Single > 0, "the GHS-OBAT CSV fixture read alone yielded no footprint.");

                PostgreSQLOpenBuildingImportTaskOptions postgreSQLOpenBuildingImportTaskOptions_Two = new()
                {
                    DatasetKey = "ghs_obat",
                    SourcePaths = [path_First, path_Second],
                };

                OpenBuildingSourcePosition openBuildingSourcePosition = new();

                IAsyncEnumerable<Footprint>? footprints = PostgreSQLOpenBuildingImportTask.Footprints(tableName, postgreSQLOpenBuildingImportTaskOptions_Two, null, [], openBuildingSourcePosition, default);
                Assert.NotNull(footprints);

                // The stream is built but never enumerated: no source file has started yet.
                Assert.Null(openBuildingSourcePosition.Path);
                Assert.Equal(0, openBuildingSourcePosition.Index);
                Assert.Equal(0, openBuildingSourcePosition.Count);

                string? path_Position_First = null;
                int index_Position_First = 0;
                int count_Position_First = 0;

                string? path_Position_Last = null;
                int index_Position_Last = 0;
                int count_Position_Last = 0;

                long count = 0;
                await foreach (Footprint footprint in footprints.WithCancellation(default))
                {
                    Assert.NotNull(footprint);

                    if (count == 0)
                    {
                        path_Position_First = openBuildingSourcePosition.Path;
                        index_Position_First = openBuildingSourcePosition.Index;
                        count_Position_First = openBuildingSourcePosition.Count;
                    }

                    path_Position_Last = openBuildingSourcePosition.Path;
                    index_Position_Last = openBuildingSourcePosition.Index;
                    count_Position_Last = openBuildingSourcePosition.Count;

                    count++;
                }

                Assert.Equal(count_Single * 2, count);

                // The first footprint comes from the first file, the last from the second, and both are
                // read under the same two-file run.
                Assert.Equal(path_First, path_Position_First);
                Assert.Equal(1, index_Position_First);
                Assert.Equal(2, count_Position_First);

                Assert.Equal(path_Second, path_Position_Last);
                Assert.Equal(2, index_Position_Last);
                Assert.Equal(2, count_Position_Last);
            }
            finally
            {
                if (File.Exists(path_First))
                {
                    File.Delete(path_First);
                }

                if (File.Exists(path_Second))
                {
                    File.Delete(path_Second);
                }
            }
        }
    }
}
