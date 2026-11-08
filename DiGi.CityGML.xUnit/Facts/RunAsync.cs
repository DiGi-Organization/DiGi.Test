using DiGi.CityGML.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.CityGML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that returning false from the walk callback stops the walk before the next file is parsed.
        /// <para>The callback previously returned void, so a caller that had already failed could only discard work the walker had gone on to do - on a large directory that meant parsing the entire remainder for nothing.</para>
        /// </summary>
        [Fact]
        public async Task RunAsync_StopsOnFalse()
        {
            string path_Zip = ZipWithEntries(5);

            try
            {
                int count = 0;

                bool result = await Query.RunAsync(path_Zip, (path, cityModel) =>
                {
                    count++;
                    return Task.FromResult(false);
                });

                Assert.False(result);
                Assert.Equal(1, count);
            }
            finally
            {
                File.Delete(path_Zip);
            }
        }

        /// <summary>
        /// Tests that the walk observes cancellation before parsing each file rather than after the whole tree has been read.
        /// <para>The token is checked ahead of the parse because the parse is the expensive part; a cancelled import must stop within one file.</para>
        /// </summary>
        [Fact]
        public async Task RunAsync_StopsOnCancellation()
        {
            string path_Zip = ZipWithEntries(5);

            try
            {
                using CancellationTokenSource cancellationTokenSource = new();

                int count = 0;

                await Assert.ThrowsAnyAsync<System.OperationCanceledException>(async () =>
                {
                    await Query.RunAsync(path_Zip, (path, cityModel) =>
                    {
                        count++;
                        cancellationTokenSource.Cancel();
                        return Task.FromResult(true);
                    }, cancellationToken: cancellationTokenSource.Token);
                });

                Assert.Equal(1, count);
            }
            finally
            {
                File.Delete(path_Zip);
            }
        }

        /// <summary>
        /// Tests that a walk which is neither cancelled nor stopped still visits every entry.
        /// <para>Counterpart to the two stop cases, so the early-abort checks cannot pass by aborting a run that should have completed.</para>
        /// </summary>
        [Fact]
        public async Task RunAsync_VisitsEveryEntry()
        {
            const int count_Expected = 5;

            string path_Zip = ZipWithEntries(count_Expected);

            try
            {
                int count = 0;

                bool result = await Query.RunAsync(path_Zip, (path, cityModel) =>
                {
                    count++;

                    Assert.NotNull(cityModel);

                    return Task.FromResult(true);
                });

                Assert.True(result);
                Assert.Equal(count_Expected, count);
            }
            finally
            {
                File.Delete(path_Zip);
            }
        }

        /// <summary>
        /// Tests that the filter predicate rejects entries before they are parsed, not after.
        /// <para>Skipping after the parse would save nothing - the parse is the expensive part - so this asserts the callback never sees a rejected path and that the predicate was consulted for every entry.</para>
        /// </summary>
        [Fact]
        public async Task RunAsync_FilterSkipsBeforeParsing()
        {
            const int count_Expected = 5;

            string path_Zip = ZipWithEntries(count_Expected);

            try
            {
                List<string> paths_Filtered = [];
                List<string> paths_Parsed = [];

                bool result = await Query.RunAsync(path_Zip, (path, cityModel) =>
                {
                    paths_Parsed.Add(path);
                    return Task.FromResult(true);
                }, path =>
                {
                    paths_Filtered.Add(path);

                    // Admit only the third entry.
                    return path.EndsWith("-2.gml", System.StringComparison.OrdinalIgnoreCase);
                });

                Assert.True(result);

                // Every entry was offered to the predicate...
                Assert.Equal(count_Expected, paths_Filtered.Count);

                // ...but only the admitted one was ever parsed.
                Assert.Single(paths_Parsed);
                Assert.EndsWith("-2.gml", paths_Parsed[0]);
            }
            finally
            {
                File.Delete(path_Zip);
            }
        }

        /// <summary>
        /// Tests that a filter rejecting every entry parses nothing at all while still reporting a completed walk.
        /// <para>This is the resume case where the recorded position sits past the end of the tree; it must not be mistaken for a failure.</para>
        /// </summary>
        [Fact]
        public async Task RunAsync_FilterRejectingEverything()
        {
            string path_Zip = ZipWithEntries(5);

            try
            {
                int count = 0;

                bool result = await Query.RunAsync(path_Zip, (path, cityModel) =>
                {
                    count++;
                    return Task.FromResult(true);
                }, path => false);

                Assert.True(result);
                Assert.Equal(0, count);
            }
            finally
            {
                File.Delete(path_Zip);
            }
        }

        /// <summary>
        /// Tests that walking a directory visits its archives in the same order every time.
        /// <para>Directory.GetFiles makes no ordering guarantee, and resuming from a recorded position is only meaningful if the walk is reproducible.</para>
        /// </summary>
        [Fact]
        public async Task RunAsync_StableOrdering()
        {
            string directory = Path.Combine(Path.GetTempPath(), string.Format(CultureInfo.CurrentCulture, "DiGi_CityGML_Order_{0}", System.Guid.NewGuid()));

            Directory.CreateDirectory(directory);

            try
            {
                // Created out of order on purpose - the walk must not depend on creation order.
                foreach (string name in new string[] { "c", "a", "b" })
                {
                    string path_Zip = ZipWithEntries(1);
                    File.Move(path_Zip, Path.Combine(directory, string.Format(CultureInfo.CurrentCulture, "{0}.zip", name)));
                }

                List<string> paths_1 = await Paths(directory);
                List<string> paths_2 = await Paths(directory);

                Assert.Equal(3, paths_1.Count);
                Assert.Equal(paths_1, paths_2);

                Assert.Contains("a.zip", paths_1[0]);
                Assert.Contains("b.zip", paths_1[1]);
                Assert.Contains("c.zip", paths_1[2]);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Tests the resume pattern the import uses: skip every file until a recorded one is reached, then take the rest.
        /// <para>Mirrors what UIBuildingsFromDirectoryPostTask does after the user chooses to continue - the recorded file is itself re-parsed, because the run that wrote it was interrupted and may have uploaded only part of it.</para>
        /// </summary>
        [Fact]
        public async Task RunAsync_ResumeFromRecordedPath()
        {
            string directory = Path.Combine(Path.GetTempPath(), string.Format(CultureInfo.CurrentCulture, "DiGi_CityGML_Resume_{0}", System.Guid.NewGuid()));

            Directory.CreateDirectory(directory);

            try
            {
                foreach (string name in new string[] { "a", "b", "c" })
                {
                    string path_Zip = ZipWithEntries(2);
                    File.Move(path_Zip, Path.Combine(directory, string.Format(CultureInfo.CurrentCulture, "{0}.zip", name)));
                }

                List<string> paths_All = await Paths(directory);

                Assert.Equal(6, paths_All.Count);

                // Resume from the second entry of the middle archive.
                string source = paths_All[3];

                List<string> paths_Resumed = [];
                bool matched = false;

                bool result = await Query.RunAsync(directory, (path, cityModel) =>
                {
                    paths_Resumed.Add(path);
                    return Task.FromResult(true);
                }, path =>
                {
                    if (!matched && string.Equals(path, source, System.StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                    }

                    return matched;
                });

                Assert.True(result);
                Assert.True(matched);

                // The recorded file is included, followed by everything after it - and nothing before.
                Assert.Equal(paths_All.GetRange(3, 3), paths_Resumed);
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Walks the directory and collects the source path of every visited city model.
        /// </summary>
        /// <param name="directory">The directory to walk.</param>
        /// <returns>A task whose result is the visited paths, in walk order.</returns>
        private static async Task<List<string>> Paths(string directory)
        {
            List<string> result = [];

            await Query.RunAsync(directory, (path, cityModel) =>
            {
                result.Add(path);
                return Task.FromResult(true);
            });

            return result;
        }

        /// <summary>
        /// Builds a temporary zip archive holding the given number of copies of the CityGML fixture.
        /// </summary>
        /// <param name="count">The number of entries to write.</param>
        /// <returns>The path of the created archive.</returns>
        private static string ZipWithEntries(int count)
        {
            string? path_Gml = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "2862_N-34-77-D-b-1-1.gml");

            Assert.True(File.Exists(path_Gml));

            string result = Path.Combine(Path.GetTempPath(), string.Format(CultureInfo.CurrentCulture, "DiGi_CityGML_RunAsync_{0}.zip", System.Guid.NewGuid()));

            using FileStream fileStream = new(result, FileMode.Create);
            using ZipArchive zipArchive = new(fileStream, ZipArchiveMode.Create);

            for (int i = 0; i < count; i++)
            {
                ZipArchiveEntry zipArchiveEntry = zipArchive.CreateEntry(string.Format(CultureInfo.CurrentCulture, "2862_N-34-77-D-b-1-{0}.gml", i));

                using Stream stream = zipArchiveEntry.Open();
                using FileStream fileStream_Gml = new(path_Gml!, FileMode.Open, FileAccess.Read);

                fileStream_Gml.CopyTo(stream);
            }

            return result;
        }
    }
}
