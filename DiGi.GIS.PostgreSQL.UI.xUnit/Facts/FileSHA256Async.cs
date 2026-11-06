using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the streaming SHA-256 the open building wrapper computes for a release (DiGi-Organization/DiGi.GIS.PostgreSQL.UI#38): the FIPS 180-2 vector for "abc" as the exact bytes the digest is defined over, a file larger than the read buffer so the digest crosses a block boundary, and absent input answering null rather than throwing.
        /// <para>The large file is compared against <c>SHA256.HashData</c> over the same bytes - the one-shot API the helper is expected to agree with whatever way the caller happened to write the file (no BOM, no newline appended).</para>
        /// </summary>
        [Fact]
        public async Task FileSHA256Async()
        {
            string path = Path.Combine(Path.GetTempPath(), $"xunit-filesha256-{Guid.NewGuid():N}.bin");
            string path_Large = Path.Combine(Path.GetTempPath(), $"xunit-filesha256-large-{Guid.NewGuid():N}.bin");

            try
            {
                // The FIPS 180-2 vector for "abc", written as the bytes the digest is defined over.
                await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes("abc"));
                Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", await Query.FileSHA256Async(path));

                // A file larger than the helper's read buffer, so the digest is fed over several blocks.
                byte[] bytes_Large = new byte[300000];
                Random.Shared.NextBytes(bytes_Large);
                await File.WriteAllBytesAsync(path_Large, bytes_Large);

                Assert.Equal(System.Convert.ToHexStringLower(SHA256.HashData(bytes_Large)), await Query.FileSHA256Async(path_Large));

                // Absent input answers null rather than throwing: a missing file is not a hashing failure, and
                // the caller refuses the run on the null instead of storing a digest it never computed.
                Assert.Null(await Query.FileSHA256Async(null));
                Assert.Null(await Query.FileSHA256Async(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (File.Exists(path_Large))
                {
                    File.Delete(path_Large);
                }
            }
        }
    }
}
