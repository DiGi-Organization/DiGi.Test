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
        /// Tests duplicate grouping on the case recorded on DiGi-Organization/DiGi.GIS#15 (2026-11-03): sheet <c>N-34-138-B-d-1-4</c> registered twice in 2024 with different files on the same day and resolution is one image, its 2025 re-acquisition is another, and a file shared across two yearly typenames is one image.
        /// <para>Grouping by download URL alone would miss the 2024 pair; grouping by sheet identifier alone would merge the 2025 re-acquisition.</para>
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

            List<List<Classes.OrtoSheet>>? groups = Query.OrtoSheetDuplicateGroups([ortoSheet_2025, ortoSheet_2024_A, null, ortoSheet_2006, ortoSheet_2024_B, ortoSheet_2005, ortoSheet_Resolution]);
            Assert.NotNull(groups);
            Assert.Equal(4, groups.Count);
            Assert.Equal(6, groups.Sum(x => x.Count));

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

            Assert.Null(Query.OrtoSheetDuplicateGroups(null));
            Assert.Empty(Query.OrtoSheetDuplicateGroups([])!);

            static Classes.OrtoSheet Sheet(string sheetId, int year, DateTimeOffset date, double pixelSize, string fileName, DateTimeOffset? registerDate)
            {
                return new Classes.OrtoSheet(sheetId, "SkorowidzOrtofomapy" + year, year, date, OrtoDatePrecision.Day, pixelSize, OrtoColorMode.RGB, "Zdj. cyfrowe", "PL-1992", null, null, true, "https://opendata.geoportal.gov.pl/ortofotomapa/" + fileName, registerDate, null, new Polygon2D([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]), OrtoType.Standard, null);
            }
        }
    }
}
