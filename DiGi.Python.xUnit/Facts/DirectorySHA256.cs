using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DiGi.Python.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.DirectorySHA256(string?, System.Collections.Generic.IEnumerable{string}?)"/> hashes the documented manifest (relative paths with "/" separators, ordinal order, each followed by the file digest), so the digest does not depend on the order files were created in, on the directory's location or on a trailing separator, changes with a file's content or name, honours search patterns, and is <c>null</c> for a missing directory.
        /// </summary>
        [Fact]
        public void DirectorySHA256()
        {
            string directory = Path.Combine(Path.GetTempPath(), "DiGi_Python_Test_" + Path.GetRandomFileName());
            string directory_1 = Path.Combine(directory, "first");
            string directory_2 = Path.Combine(directory, "second");

            static string Hex(byte[] bytes)
            {
                StringBuilder stringBuilder = new();
                foreach (byte @byte in bytes)
                {
                    stringBuilder.Append(@byte.ToString("x2", CultureInfo.InvariantCulture));
                }

                return stringBuilder.ToString();
            }

            static string SHA256Hex(string text)
            {
                return Hex(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
            }

            try
            {
                //The same content, created in opposite orders
                Directory.CreateDirectory(Path.Combine(directory_1, "sub"));
                File.WriteAllText(Path.Combine(directory_1, "sub", "b.txt"), "beta");
                File.WriteAllText(Path.Combine(directory_1, "a.txt"), "alpha");
                File.WriteAllText(Path.Combine(directory_1, "B.txt"), "upper");

                Directory.CreateDirectory(directory_2);
                File.WriteAllText(Path.Combine(directory_2, "B.txt"), "upper");
                File.WriteAllText(Path.Combine(directory_2, "a.txt"), "alpha");
                Directory.CreateDirectory(Path.Combine(directory_2, "sub"));
                File.WriteAllText(Path.Combine(directory_2, "sub", "b.txt"), "beta");

                //Ordinal order puts "B.txt" before "a.txt"; the subdirectory is written with "/"
                string manifest = string.Concat(
                    "B.txt\n", SHA256Hex("upper"), "\n",
                    "a.txt\n", SHA256Hex("alpha"), "\n",
                    "sub/b.txt\n", SHA256Hex("beta"), "\n");

                string? sHA256_1 = Query.DirectorySHA256(directory_1);
                Assert.Equal(SHA256Hex(manifest), sHA256_1);
                Assert.Equal(sHA256_1, Query.DirectorySHA256(directory_2));
                Assert.Equal(sHA256_1, Query.DirectorySHA256(directory_1 + Path.DirectorySeparatorChar));
                Assert.Equal(sHA256_1, Query.DirectorySHA256(directory_1.Replace('\\', '/')));

                //A file matched by two patterns counts once; a pattern leaves other files out
                Assert.Equal(sHA256_1, Query.DirectorySHA256(directory_1, ["*.txt", "a.*"]));
                File.WriteAllText(Path.Combine(directory_2, "weights.bin"), "binary");
                Assert.Equal(sHA256_1, Query.DirectorySHA256(directory_2, ["*.txt"]));
                Assert.NotEqual(sHA256_1, Query.DirectorySHA256(directory_2));
                File.Delete(Path.Combine(directory_2, "weights.bin"));

                //Content and name both count
                File.WriteAllText(Path.Combine(directory_2, "a.txt"), "ALPHA");
                Assert.NotEqual(sHA256_1, Query.DirectorySHA256(directory_2));
                File.WriteAllText(Path.Combine(directory_2, "a.txt"), "alpha");
                Assert.Equal(sHA256_1, Query.DirectorySHA256(directory_2));

                File.Move(Path.Combine(directory_2, "a.txt"), Path.Combine(directory_2, "c.txt"));
                Assert.NotEqual(sHA256_1, Query.DirectorySHA256(directory_2));

                //An empty directory hashes the empty manifest
                string directory_Empty = Path.Combine(directory, "empty");
                Directory.CreateDirectory(directory_Empty);
                Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Query.DirectorySHA256(directory_Empty));

                Assert.Null(Query.DirectorySHA256(Path.Combine(directory, "missing")));
                Assert.Null(Query.DirectorySHA256(null));
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
