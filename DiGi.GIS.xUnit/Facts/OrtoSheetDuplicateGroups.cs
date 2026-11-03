using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests duplicate grouping: a sheet registered twice in one year with different files on the same day, resolution and colour mode is one image (the real case <c>N-34-139-C-c-1-1</c>, 1976), its re-acquisition at another date and resolution is another, and a file shared across two yearly typenames is one image.
        /// <para>The CIR and RGB composites of one flight stay apart: the national index of 2026-11-03 holds <c>N-34-138-B-d-1-4</c> in 2024 as a CIR and an RGB row with the same date and resolution (DiGi-Organization/DiGi.GIS#17).</para>
        /// <para>Grouping by download URL alone would miss the same-colour pair; grouping by sheet identifier alone would merge the re-acquisition; leaving the colour mode out of the key would merge the CIR row. The CIR assertion was shown to fail with the colour mode removed from the key.</para>
        /// </summary>
        [Fact]
        public void OrtoSheetDuplicateGroups()
        {
            DateTimeOffset date_2024 = new(2024, 7, 18, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset date_2025 = new(2025, 4, 27, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset date_2005 = new(2005, 1, 1, 0, 0, 0, TimeSpan.Zero);

            Classes.OrtoSheet ortoSheet_2024_A = Sheet("N-34-138-B-d-1-4", 2024, date_2024, 0.25, "81434_1416790_N-34-138-B-d-1-4.tif", new DateTimeOffset(2024, 9, 1, 0, 0, 0, TimeSpan.Zero));
            Classes.OrtoSheet ortoSheet_2024_B = Sheet("N-34-138-B-d-1-4", 2024, date_2024, 0.25, "81435_1420741_N-34-138-B-d-1-4.tif", new DateTimeOffset(2024, 8, 1, 0, 0, 0, TimeSpan.Zero));
            Classes.OrtoSheet ortoSheet_2025 = Sheet("N-34-138-B-d-1-4", 2025, date_2025, 0.05, "83235_1485372_N-34-138-B-d-1-4.tif", null);
            Classes.OrtoSheet ortoSheet_2005 = Sheet("N-33-117-D-a-3-4", 2005, date_2005, 0.5, "37_62336_N-33-117-D-a-3-4.tif", null);
            Classes.OrtoSheet ortoSheet_2006 = Sheet("N-33-117-D-a-3-4-bis", 2006, new DateTimeOffset(2006, 1, 1, 0, 0, 0, TimeSpan.Zero), 0.5, "37_62336_N-33-117-D-a-3-4.tif", null);
            Classes.OrtoSheet ortoSheet_Resolution = Sheet("N-34-138-B-d-1-4", 2024, date_2024, 0.10, "81500_1_N-34-138-B-d-1-4.tif", null);
            Classes.OrtoSheet ortoSheet_CIR = Sheet("N-34-138-B-d-1-4", 2024, date_2024, 0.25, "81436_1420742_N-34-138-B-d-1-4.tif", null, OrtoColorMode.CIR);

            List<List<Classes.OrtoSheet>>? groups = Query.OrtoSheetDuplicateGroups([ortoSheet_2025, ortoSheet_2024_A, null, ortoSheet_2006, ortoSheet_2024_B, ortoSheet_2005, ortoSheet_Resolution, ortoSheet_CIR]);
            Assert.NotNull(groups);
            Assert.Equal(5, groups.Count);
            Assert.Equal(7, groups.Sum(x => x.Count));

            List<Classes.OrtoSheet> group_2005 = groups.Single(x => x.Contains(ortoSheet_2005));
            Assert.Equal(2, group_2005.Count);
            Assert.Same(ortoSheet_2005, group_2005[0]);
            Assert.Contains(ortoSheet_2006, group_2005);

            // Same year: the earlier registration is canonical.
            List<Classes.OrtoSheet> group_2024 = groups.Single(x => x.Contains(ortoSheet_2024_A));
            Assert.Equal(2, group_2024.Count);
            Assert.Same(ortoSheet_2024_B, group_2024[0]);

            Assert.Single(groups.Single(x => x.Contains(ortoSheet_2025)));
            Assert.Single(groups.Single(x => x.Contains(ortoSheet_Resolution)));
            Assert.Single(groups.Single(x => x.Contains(ortoSheet_CIR)));

            Assert.Null(Query.OrtoSheetDuplicateGroups(null));
            Assert.Empty(Query.OrtoSheetDuplicateGroups([])!);

            static Classes.OrtoSheet Sheet(string sheetId, int year, DateTimeOffset date, double pixelSize, string fileName, DateTimeOffset? registerDate, OrtoColorMode ortoColorMode = OrtoColorMode.RGB)
            {
                return new Classes.OrtoSheet(sheetId, "SkorowidzOrtofomapy" + year, year, date, OrtoDatePrecision.Day, pixelSize, ortoColorMode, "Zdj. cyfrowe", "PL-1992", null, null, true, "https://opendata.geoportal.gov.pl/ortofotomapa/" + fileName, registerDate, null, new Polygon2D([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]), OrtoType.Standard, null);
            }
        }
    }
}
