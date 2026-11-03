using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pulls the smallest real yearly typename of the live GUGiK orthophoto index (1998, 353 rows on 2026-11-03), checks it against the <c>RESULTTYPE=hits</c> count, builds the availability matrix from it and writes both tables to <c>user files/reports/</c>.
        /// <para>Proves the live path end to end: paging, the GML conversion and the northing-first axis swap (every footprint must land inside Poland's EPSG:2180 extent with easting in X). Image probes are not part of this fact; they need the blank detector of DiGi.GIS.Emgu.CV.</para>
        /// <para>Medium test (calls a live external service): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task OrtoSheetsAsync_Live()
        {
            string typeName = Query.OrtoSheetTypeName(1998);

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

            int? count = await Query.OrtoSheetCountAsync(httpClient, typeName, null, 3, TimeSpan.FromSeconds(2));
            Assert.NotNull(count);
            Assert.True(count > 0);

            DateTimeOffset probeDate = DateTimeOffset.UtcNow;

            List<Classes.OrtoSheet>? ortoSheets = await Query.OrtoSheetsAsync(httpClient, typeName, null, 200, 3, TimeSpan.FromSeconds(2), probeDate);
            Assert.NotNull(ortoSheets);
            Assert.Equal(count, ortoSheets.Count);

            foreach (Classes.OrtoSheet ortoSheet in ortoSheets)
            {
                Assert.Equal(1998, ortoSheet.Year);
                Assert.Equal(probeDate, ortoSheet.ProbeDate);

                Geometry.Planar.Classes.BoundingBox2D? boundingBox2D = ortoSheet.Footprint?.GetBoundingBox();
                Assert.NotNull(boundingBox2D);

                // Inside the PL-1992 extent of Poland. The two ranges overlap, so this cannot catch a swap on its own;
                // the swap is pinned by Convert_ToDiGi_OrtoSheet against a sheet whose position decides it.
                Assert.InRange(boundingBox2D.Min.X, 140000, 880000);
                Assert.InRange(boundingBox2D.Min.Y, 120000, 790000);
            }

            Classes.OrtoAvailabilityMatrix? ortoAvailabilityMatrix = Create.OrtoAvailabilityMatrix(ortoSheets, 5000, 0.9, probeDate);
            Assert.NotNull(ortoAvailabilityMatrix);

            // The 1998 campaign shares one year-placeholder date, so its sheets tile cells together.
            Assert.NotEmpty(ortoAvailabilityMatrix.OrtoAvailabilities!);

            string directory = Path.Combine(Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly())!, "OrtoInventory_Live");
            Directory.CreateDirectory(directory);

            File.WriteAllLines(Path.Combine(directory, "sheets-1998.tsv"), Convert.ToSystem_Strings(ortoSheets)!);
            File.WriteAllLines(Path.Combine(directory, "availability-matrix-1998.tsv"), Convert.ToSystem_Strings(ortoAvailabilityMatrix)!);
            File.WriteAllText(Path.Combine(directory, "availability-matrix-1998.json"), Core.Convert.ToSystem_String(ortoAvailabilityMatrix));

            List<List<Classes.OrtoSheet>> groups = Query.OrtoSheetDuplicateGroups(ortoSheets)!;
            Assert.Equal(ortoSheets.Count, groups.Sum(x => x.Count));
        }
    }
}
