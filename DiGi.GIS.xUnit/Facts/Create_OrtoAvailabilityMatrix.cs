using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the availability matrix on one 100 m cell: the coverage fraction boundary (a footprint covering 91 % counts, 89 % does not), two half sheets of one date covering the cell together while halves of different dates do not, duplicate registrations collapsing into one epoch, the gap in days, the right-censoring interval and the culture-invariant key and table.
        /// <para>The key and the table are asserted again under <c>pl-PL</c>, where the raw number formatting differs.</para>
        /// </summary>
        [Fact]
        public void Create_OrtoAvailabilityMatrix()
        {
            DateTimeOffset date_2010 = new(2010, 5, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset date_2012 = new(2012, 5, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset date_2015 = new(2015, 6, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset probeDate = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

            Polygon2D polygon2D_91 = new([new Point2D(0, 0), new Point2D(91, 0), new Point2D(91, 100), new Point2D(0, 100)]);
            Polygon2D polygon2D_89 = new([new Point2D(0, 0), new Point2D(89, 0), new Point2D(89, 100), new Point2D(0, 100)]);
            Polygon2D polygon2D_Trapezoid = new([new Point2D(-5, -5), new Point2D(100, -5), new Point2D(100, 100), new Point2D(-20, 100)]);

            List<Classes.OrtoSheet> ortoSheets =
            [
                Sheet("A", 2010, date_2010, OrtoDatePrecision.Year, 0.5, "a.tif", polygon2D_91),
                Sheet("A", 2011, date_2010, OrtoDatePrecision.Year, 0.5, "a.tif", polygon2D_91),
                Sheet("B", 2012, date_2012, OrtoDatePrecision.Day, 0.25, "b.tif", polygon2D_89),
                Sheet("C", 2015, date_2015, OrtoDatePrecision.Day, 0.25, "c1.tif", polygon2D_Trapezoid),
                Sheet("C", 2015, date_2015, OrtoDatePrecision.Day, 0.25, "c2.tif", polygon2D_Trapezoid),
            ];

            Classes.OrtoAvailabilityMatrix? ortoAvailabilityMatrix = Create.OrtoAvailabilityMatrix(ortoSheets, 100, 0.9, probeDate);
            Assert.NotNull(ortoAvailabilityMatrix);
            Assert.Equal(100, ortoAvailabilityMatrix.CellSize);
            Assert.Equal(0.9, ortoAvailabilityMatrix.CoverageFraction);
            Assert.Equal(probeDate, ortoAvailabilityMatrix.ProbeDate);

            // The trapezoid reaches into the cells west and south of the origin cell, but covers too little of them to count.
            Classes.OrtoAvailability ortoAvailability = Assert.Single(ortoAvailabilityMatrix.OrtoAvailabilities!);
            Assert.Equal("0_0", ortoAvailability.LocationKey);

            List<Classes.OrtoEpoch> ortoEpochs = ortoAvailability.OrtoEpochs!;
            Assert.Equal(2, ortoEpochs.Count);
            Assert.Equal(date_2010, ortoEpochs[0].Date);
            Assert.Equal(OrtoDatePrecision.Year, ortoEpochs[0].DatePrecision);
            Assert.Equal(["https://example.org/a.tif"], ortoEpochs[0].DownloadUrls!);
            Assert.Equal(date_2015, ortoEpochs[1].Date);
            Assert.Single(ortoEpochs[1].DownloadUrls!);

            int gapDays = (int)Math.Round((date_2015 - date_2010).TotalDays);
            Assert.Equal([gapDays], ortoAvailability.GapDays!);
            Assert.Equal([2010, 2015], Query.AvailableOrtoYears(ortoAvailability)!);

            // Just inside the boundary: at 0.89 the 2012 sheet counts too.
            Classes.OrtoAvailabilityMatrix? ortoAvailabilityMatrix_89 = Create.OrtoAvailabilityMatrix(ortoSheets, 100, 0.89, probeDate);
            Assert.Equal(3, ortoAvailabilityMatrix_89?.OrtoAvailabilities?[0].OrtoEpochs?.Count);

            // Two half sheets of one date tile the cell together; two halves of different dates do not.
            Polygon2D polygon2D_West = new([new Point2D(0, 0), new Point2D(50, 0), new Point2D(50, 100), new Point2D(0, 100)]);
            Polygon2D polygon2D_East = new([new Point2D(50, 0), new Point2D(100, 0), new Point2D(100, 100), new Point2D(50, 100)]);
            DateTimeOffset date_2020 = new(2020, 6, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset date_2021 = new(2021, 6, 1, 0, 0, 0, TimeSpan.Zero);

            Classes.OrtoAvailabilityMatrix? ortoAvailabilityMatrix_Union = Create.OrtoAvailabilityMatrix([Sheet("W", 2020, date_2020, OrtoDatePrecision.Day, 0.25, "w.tif", polygon2D_West), Sheet("E", 2020, date_2020, OrtoDatePrecision.Day, 0.1, "e.tif", polygon2D_East)], 100, 0.9);
            Classes.OrtoEpoch ortoEpoch_Union = Assert.Single(Assert.Single(ortoAvailabilityMatrix_Union!.OrtoAvailabilities!).OrtoEpochs!);
            Assert.Equal(0.1, ortoEpoch_Union.PixelSize);
            Assert.Equal(2, ortoEpoch_Union.DownloadUrls?.Count);

            Classes.OrtoAvailabilityMatrix? ortoAvailabilityMatrix_Split = Create.OrtoAvailabilityMatrix([Sheet("W", 2020, date_2020, OrtoDatePrecision.Day, 0.25, "w.tif", polygon2D_West), Sheet("E", 2021, date_2021, OrtoDatePrecision.Day, 0.25, "e.tif", polygon2D_East)], 100, 0.9);
            Assert.Empty(ortoAvailabilityMatrix_Split!.OrtoAvailabilities!);

            string[]? lines = Convert.ToSystem_Strings(ortoAvailabilityMatrix);
            Assert.NotNull(lines);
            Assert.Equal(3, lines.Length);
            string[] values_Last = lines[2].Split('\t');
            Assert.Equal("0.9", values_Last[4]);
            Assert.Equal(gapDays.ToString(CultureInfo.InvariantCulture), values_Last[10]);
            Assert.Equal(((int)Math.Round((probeDate - date_2015).TotalDays)).ToString(CultureInfo.InvariantCulture), values_Last[11]);
            Assert.Equal(string.Empty, lines[1].Split('\t')[10]);

            BoundingBox2D boundingBox2D_Fraction = new(new Point2D(1234.5, 6.25), new Point2D(1300, 100));

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

                Assert.NotEqual("1234.5", 1234.5.ToString());
                Assert.Equal("1234.5_6.25", Query.OrtoLocationKey(boundingBox2D_Fraction));
                Assert.Equal(lines, Convert.ToSystem_Strings(ortoAvailabilityMatrix)!);
                Assert.Contains("\t0.5\t", Convert.ToSystem_Strings(ortoSheets)![1]);
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }

            Assert.Equal("1234.5_6.25", Query.OrtoLocationKey(boundingBox2D_Fraction));
            Assert.Equal("0_0", Query.OrtoLocationKey(new BoundingBox2D(new Point2D(-0.0, -0.0), new Point2D(1, 1))));

            Assert.Null(Create.OrtoAvailabilityMatrix(ortoSheets, 0, 0.9));
            Assert.Null(Create.OrtoAvailabilityMatrix(ortoSheets, 100, 1.01));
            Assert.Null(Create.OrtoAvailabilityMatrix(null));
            Assert.Empty(Create.OrtoAvailabilityMatrix([], 100)!.OrtoAvailabilities!);

            static Classes.OrtoSheet Sheet(string sheetId, int year, DateTimeOffset date, OrtoDatePrecision ortoDatePrecision, double pixelSize, string fileName, Polygon2D polygon2D)
            {
                return new Classes.OrtoSheet(sheetId, "SkorowidzOrtofomapy" + year, year, date, ortoDatePrecision, pixelSize, OrtoColorMode.RGB, "Zdj. cyfrowe", "PL-1992", null, null, true, "https://example.org/" + fileName, null, null, polygon2D, OrtoType.Standard, null);
            }
        }
    }
}
