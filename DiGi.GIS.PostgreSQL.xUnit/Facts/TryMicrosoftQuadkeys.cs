using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the per-file quadkey rule of a Microsoft run (DiGi-Organization/DiGi.GIS.PostgreSQL#132): each file's own <c>quadkey=</c> folder first, the typed quadkey only for a file without one, and a refusal - with a message naming the file - when a file resolves to nothing, disagrees with the typed quadkey, or shares its quadkey with another file of the run.
        /// <para>The rule is pure, so the fact asserts it on paths alone; the end-to-end run is <see cref="PostgreSQLOpenBuildingImportTask_Quadkeys"/>.</para>
        /// </summary>
        [Fact]
        public void TryMicrosoftQuadkeys()
        {
            const string path_A = @"D:\Input\Microsoft\2026-02-03\quadkey=120213101\part-1.csv.gz";
            const string path_B = @"D:\Input\Microsoft\2026-02-03\quadkey=120213103\part-2.csv.gz";
            const string path_Plain = @"D:\Input\Microsoft\chunk.csv.gz";

            // Several folders, no typed quadkey: each file keeps its own.
            Assert.True(Query.TryMicrosoftQuadkeys([path_A, path_B], null, out Dictionary<string, string>? quadkeys, out string? message));
            Assert.Null(message);
            Assert.NotNull(quadkeys);
            Assert.Equal(2, quadkeys.Count);
            Assert.Equal("120213101", quadkeys[path_A]);
            Assert.Equal("120213103", quadkeys[path_B]);

            // A single file without a folder takes the typed quadkey - the behaviour before #132.
            Assert.True(Query.TryMicrosoftQuadkeys([path_Plain], " 03210 ", out quadkeys, out message));
            Assert.Equal("03210", quadkeys![path_Plain]);

            // A typed quadkey that agrees with the folder is accepted.
            Assert.True(Query.TryMicrosoftQuadkeys([path_A], "120213101", out quadkeys, out _));
            Assert.Equal("120213101", quadkeys![path_A]);

            // Refusals, each naming the file.
            Assert.False(Query.TryMicrosoftQuadkeys([path_Plain], null, out quadkeys, out message));
            Assert.Null(quadkeys);
            Assert.Contains(path_Plain, message);

            Assert.False(Query.TryMicrosoftQuadkeys([path_A], "120213103", out _, out message));
            Assert.Contains("120213101", message);

            // Two files under one quadkey would restart the feature index and collide on quadkey:index.
            Assert.False(Query.TryMicrosoftQuadkeys([path_Plain, @"D:\Input\Microsoft\other.csv.gz"], "03210", out _, out message));
            Assert.Contains("collide", message);

            Assert.False(Query.TryMicrosoftQuadkeys([path_A, @"D:\Copy\quadkey=120213101\part-1.csv.gz"], null, out _, out message));
            Assert.Contains("collide", message);

            // No paths.
            Assert.False(Query.TryMicrosoftQuadkeys(null, "03210", out _, out message));
            Assert.False(Query.TryMicrosoftQuadkeys([], "03210", out _, out message));
        }
    }
}
