using Parquet;
using Parquet.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiGi.GIS.IO.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reports Parquet.NET read throughput for one row group of a real Overture buildings file, writing <c>user files/reports/ParquetThroughput.txt</c>.
        /// <para>Gated on machine-specific input: the file <c>user files/Overture_Buildings.parquet</c> must be placed there by hand (the real release is far too large to commit). Without it the fact returns without measuring or asserting anything, so an ordinary run stays green and side-effect free.</para>
        /// <para>The measurement is deliberately the raw Parquet.NET row-group read rather than a full reader scan, so the number describes the parquet layer alone and stays comparable across machines; megabytes per second are derived from the file size divided by the row-group count, which is an average rather than the compressed size of that specific group.</para>
        /// </summary>
        [MediumFact]
        public async Task ParquetThroughput_OvertureRowGroup()
        {
            string? userFilesDirectory = Core.xUnit.Query.UserFilesDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(userFilesDirectory);

            string path = Path.Combine(userFilesDirectory!, "Overture_Buildings.parquet");
            if (!File.Exists(path))
            {
                return;
            }

            long fileBytes = new FileInfo(path).Length;

            using FileStream fileStream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using ParquetReader parquetReader = await ParquetReader.CreateAsync(fileStream, new ParquetOptions(), false);
            Assert.True(parquetReader.RowGroupCount > 0);

            using ParquetRowGroupReader parquetRowGroupReader = parquetReader.OpenRowGroupReader(0);
            long rowCount = parquetRowGroupReader.RowCount;

            Stopwatch stopwatch = Stopwatch.StartNew();
            DataColumn[] dataColumns = await parquetReader.ReadEntireRowGroupAsync(0, default);
            stopwatch.Stop();

            Assert.NotEmpty(dataColumns);
            Assert.True(rowCount > 0);

            double seconds = stopwatch.ElapsedMilliseconds / 1000.0;
            if (seconds <= 0)
            {
                seconds = 0.001;
            }

            double megabytes = fileBytes / 1048576.0 / parquetReader.RowGroupCount;
            string report = string.Format(
                CultureInfo.InvariantCulture,
                "file={0}\r\nfile_mb={1:F1}\r\nrow_groups={2}\r\nrow_group_index=0\r\nrows={3}\r\nelapsed_ms={4}\r\nrows_per_second={5:F0}\r\nmb_per_second={6:F1} (file size / row groups / elapsed)",
                path,
                fileBytes / 1048576.0,
                parquetReader.RowGroupCount,
                rowCount,
                stopwatch.ElapsedMilliseconds,
                rowCount / seconds,
                megabytes / seconds);

            string? reportsDirectory = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(reportsDirectory);
            File.WriteAllText(Path.Combine(reportsDirectory!, "ParquetThroughput.txt"), report, new UTF8Encoding(false));

            System.Console.WriteLine(report);
        }
    }
}
