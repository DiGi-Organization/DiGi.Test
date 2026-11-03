using DiGi.Core.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that orthophoto images are dated by the index sheet they show, not by the year requested from the time-enabled WMS (DiGi.GIS#19).
        /// <para>The sheets reproduce the index at county 1465 probe point (637500, 487500) on 2026-11-03: <c>N-34-139-A-c-1</c> 2001-05-13 (0.75 m), <c>N-34-139-A-c-1-1</c> 2007-01-01 at year precision (0.5 m), 2011-04-21 as CIR and RGB (0.25 m) and 2013-06-14 (0.1 m). There, requests 2001–2005 answered one image, 2006–2010 another and 2011–2012 a third, so the 2006 request already shows the sheet filed under 2007.</para>
        /// <para>Covers the <see cref="OrtoData"/> members (serialization, copy constructor, and a row stored before them reading as undated rather than <see cref="OrtoDatePrecision.Day"/>), <see cref="Query.AcquisitionOrtoSheet"/>, <see cref="Query.ContentRunEndYears"/>, <see cref="Create.OrtoData(int, byte[], double, BoundingBox2D, IEnumerable{OrtoSheet}, int?)"/> and <see cref="Query.OrtoObservations"/>.</para>
        /// <para>Two guards were shown to fail: dating by the first requested year resolves the 2006 image to the 2001 sheet, and without the initializer an old row reads as <see cref="OrtoDatePrecision.Day"/>.</para>
        /// </summary>
        [Fact]
        public void OrtoData_Acquisition()
        {
            Point2D point2D = new(637500, 487500);

            OrtoSheet ortoSheet_2001 = Sheet("N-34-139-A-c-1", 2001, new DateTimeOffset(2001, 5, 13, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.75, OrtoColorMode.RGB, OrtoType.Standard, 2000);
            OrtoSheet ortoSheet_2007 = Sheet("N-34-139-A-c-1-1", 2007, new DateTimeOffset(2007, 1, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Year, 0.5, OrtoColorMode.RGB, OrtoType.Standard, 1000);
            OrtoSheet ortoSheet_2011_CIR = Sheet("N-34-139-A-c-1-1", 2011, new DateTimeOffset(2011, 4, 21, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.25, OrtoColorMode.CIR, OrtoType.Standard, 1000);
            OrtoSheet ortoSheet_2011_RGB = Sheet("N-34-139-A-c-1-1", 2011, new DateTimeOffset(2011, 4, 21, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.25, OrtoColorMode.RGB, OrtoType.Standard, 1000);
            OrtoSheet ortoSheet_2013 = Sheet("N-34-139-A-c-1-1", 2013, new DateTimeOffset(2013, 6, 14, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.1, OrtoColorMode.RGB, OrtoType.Standard, 1000);
            OrtoSheet ortoSheet_TrueOrtho = Sheet("N-34-139-A-c-1-1-1", 2025, new DateTimeOffset(2008, 6, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.05, OrtoColorMode.Undefined, OrtoType.TrueOrtho, 1000);
            OrtoSheet ortoSheet_Elsewhere = Sheet("N-34-139-A-c-2-1", 2009, new DateTimeOffset(2009, 6, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.25, OrtoColorMode.RGB, OrtoType.Standard, 1000, 5000);

            List<OrtoSheet?> ortoSheets = [ortoSheet_2013, null, ortoSheet_2011_CIR, ortoSheet_2007, ortoSheet_TrueOrtho, ortoSheet_2011_RGB, ortoSheet_Elsewhere, ortoSheet_2001];

            // Latest sheet in or before the year; true orthophotos and sheets elsewhere never qualify.
            Assert.Null(Query.AcquisitionOrtoSheet(ortoSheets, point2D, 2000));
            Assert.Same(ortoSheet_2001, Query.AcquisitionOrtoSheet(ortoSheets, point2D, 2006));
            Assert.Same(ortoSheet_2007, Query.AcquisitionOrtoSheet(ortoSheets, point2D, 2010));
            Assert.Same(ortoSheet_2011_RGB, Query.AcquisitionOrtoSheet(ortoSheets, point2D, 2012));
            Assert.Same(ortoSheet_2013, Query.AcquisitionOrtoSheet(ortoSheets, point2D, 2026));
            Assert.Null(Query.AcquisitionOrtoSheet(ortoSheets, new Point2D(600000, 400000), 2026));
            Assert.Null(Query.AcquisitionOrtoSheet(null, point2D, 2026));

            // A point on the footprint edge is inside.
            Assert.Same(ortoSheet_2013, Query.AcquisitionOrtoSheet(ortoSheets, new Point2D(point2D.X + 1000, point2D.Y), 2026));

            byte[] bytes_A = [255, 216, 255, 1];
            byte[] bytes_B = [255, 216, 255, 2];
            byte[] bytes_C = [255, 216, 255, 3];

            Dictionary<int, byte[]> bytesDictionary = [];
            for (int year = 2001; year <= 2012; year++)
            {
                bytesDictionary[year] = year <= 2005 ? bytes_A : year <= 2010 ? bytes_B : bytes_C;
            }

            Dictionary<int, int> lastYears = Query.ContentRunEndYears(bytesDictionary)!;
            Assert.Equal(2005, lastYears[2001]);
            Assert.Equal(2005, lastYears[2005]);
            Assert.Equal(2010, lastYears[2006]);
            Assert.Equal(2012, lastYears[2011]);

            // A missing year breaks a run.
            bytesDictionary.Remove(2008);
            Dictionary<int, int> lastYears_Gap = Query.ContentRunEndYears(bytesDictionary)!;
            Assert.Equal(2007, lastYears_Gap[2006]);
            Assert.Equal(2010, lastYears_Gap[2009]);
            Assert.Null(Query.ContentRunEndYears(null));

            BoundingBox2D boundingBox2D = new(new Point2D(point2D.X - 128, point2D.Y - 128), new Point2D(point2D.X + 128, point2D.Y + 128));

            // The 2006 request shows the sheet filed under 2007: dated by the last year of its run, not by 2006.
            OrtoData? ortoData_2006 = Create.OrtoData(2006, bytes_B, 2, boundingBox2D, ortoSheets, lastYears[2006]);
            Assert.NotNull(ortoData_2006);
            Assert.Equal(new DateTime(2006, 1, 1), ortoData_2006.DateTime);
            Assert.Equal(ortoSheet_2007.Date, ortoData_2006.AcquisitionDate);
            Assert.Equal(OrtoDatePrecision.Year, ortoData_2006.AcquisitionDatePrecision);
            Assert.Equal("N-34-139-A-c-1-1", ortoData_2006.SheetId);
            Assert.Equal(Query.ContentHash(bytes_B), ortoData_2006.ContentHash);

            OrtoData? ortoData_Undated = Create.OrtoData(2006, bytes_B, 2, boundingBox2D);
            Assert.NotNull(ortoData_Undated);
            Assert.Null(ortoData_Undated.AcquisitionDate);
            Assert.Equal(OrtoDatePrecision.Undefined, ortoData_Undated.AcquisitionDatePrecision);
            Assert.Null(ortoData_Undated.SheetId);
            Assert.NotNull(ortoData_Undated.ContentHash);
            Assert.Null(Create.OrtoData(2006, bytes_B, 2, null, ortoSheets));

            // Serialization of every member, the copy constructor, and a row stored before the members existed.
            Assert.Equal(new DateTime(2006, 1, 1), ortoData_2006.DateTime);
            Assert.Equal(bytes_B, ortoData_2006.Bytes);
            Assert.Equal(2, ortoData_2006.Scale);
            Assert.Equal(boundingBox2D.TopLeft, ortoData_2006.Location);

            OrtoData? ortoData_Json = Core.Convert.ToDiGi<OrtoData>(Core.Convert.ToSystem_String(ortoData_2006))?.FirstOrDefault();
            Assert.NotNull(ortoData_Json);
            Assert.Equal(ortoData_2006.AcquisitionDate, ortoData_Json.AcquisitionDate);
            Assert.Equal(ortoData_2006.AcquisitionDatePrecision, ortoData_Json.AcquisitionDatePrecision);
            Assert.Equal(ortoData_2006.SheetId, ortoData_Json.SheetId);
            Assert.Equal(ortoData_2006.ContentHash, ortoData_Json.ContentHash);
            Assert.Equal(ortoData_2006.Bytes, ortoData_Json.Bytes);
            Core.xUnit.Query.SerializationCheck(ortoData_2006);
            Core.xUnit.Query.SerializationCheck(ortoData_Undated);

            OrtoData ortoData_Copy = new(ortoData_2006);
            Assert.Equal(ortoData_2006.DateTime, ortoData_Copy.DateTime);
            Assert.Equal(ortoData_2006.AcquisitionDate, ortoData_Copy.AcquisitionDate);
            Assert.Equal(ortoData_2006.AcquisitionDatePrecision, ortoData_Copy.AcquisitionDatePrecision);
            Assert.Equal(ortoData_2006.SheetId, ortoData_Copy.SheetId);
            Assert.Equal(ortoData_2006.ContentHash, ortoData_Copy.ContentHash);
            Assert.Equal(ortoData_2006.Location, ortoData_Copy.Location);

            JsonObject jsonObject_Old = ortoData_2006.ToJsonObject()!;
            jsonObject_Old.Remove(nameof(OrtoData.AcquisitionDate));
            jsonObject_Old.Remove(nameof(OrtoData.AcquisitionDatePrecision));
            jsonObject_Old.Remove(nameof(OrtoData.SheetId));
            jsonObject_Old.Remove(nameof(OrtoData.ContentHash));
            OrtoData? ortoData_Old = Core.Convert.ToDiGi<OrtoData>(jsonObject_Old.ToJsonString())?.FirstOrDefault();
            Assert.NotNull(ortoData_Old);
            Assert.Equal(new DateTime(2006, 1, 1), ortoData_Old.DateTime);
            Assert.Null(ortoData_Old.AcquisitionDate);
            Assert.Equal(OrtoDatePrecision.Undefined, ortoData_Old.AcquisitionDatePrecision);
            Assert.Null(ortoData_Old.ContentHash);

            // A, B, A: the repeat is one observation, dated by its earliest acquisition date; old rows are hashed from their bytes.
            OrtoData ortoData_A_2001 = Create.OrtoData(2001, bytes_A, 2, boundingBox2D, ortoSheets, 2005)!;
            OrtoData ortoData_A_2012 = Create.OrtoData(2012, bytes_A, 2, boundingBox2D, ortoSheets, 2012)!;
            OrtoData ortoData_A_Old = new(new DateTime(2003, 1, 1), bytes_A, 2, boundingBox2D.TopLeft);
            OrtoData ortoData_Empty = new(new DateTime(2004, 1, 1), [], 2, boundingBox2D.TopLeft);

            List<List<OrtoData>>? ortoObservations = Query.OrtoObservations([ortoData_A_2012, ortoData_2006, null, ortoData_A_Old, ortoData_Empty, ortoData_A_2001]);
            Assert.NotNull(ortoObservations);
            Assert.Equal(2, ortoObservations.Count);
            Assert.Equal(3, ortoObservations[0].Count);
            Assert.Same(ortoData_A_2001, ortoObservations[0][0]);
            Assert.Same(ortoData_A_Old, ortoObservations[0][2]);
            Assert.Same(ortoData_2006, Assert.Single(ortoObservations[1]));
            Assert.Null(Query.OrtoObservations(null));

            static OrtoSheet Sheet(string sheetId, int year, DateTimeOffset date, OrtoDatePrecision ortoDatePrecision, double pixelSize, OrtoColorMode ortoColorMode, OrtoType ortoType, double halfSize, double offsetX = 0)
            {
                double x = 637500 + offsetX;
                double y = 487500;
                Polygon2D polygon2D = new([new Point2D(x - halfSize, y - halfSize), new Point2D(x + halfSize, y - halfSize), new Point2D(x + halfSize, y + halfSize), new Point2D(x - halfSize, y + halfSize)]);
                return new OrtoSheet(sheetId, "SkorowidzOrtofomapy" + year, year, date, ortoDatePrecision, pixelSize, ortoColorMode, "Zdj. cyfrowe", "PL-1992", null, null, true, "https://opendata.geoportal.gov.pl/ortofotomapa/" + sheetId + "-" + year + "-" + ortoColorMode + ".tif", null, null, polygon2D, ortoType, null);
            }
        }

        /// <summary>
        /// Dates the live images of county 1465 probe point (637500, 487500), years 2001–2012, by the live sheet index (DiGi.GIS#19).
        /// <para>On 2026-11-03 these requests answered three images (2001–2005, 2006–2010, 2011–2012). After <see cref="OrtoDatas.Reduce"/> every remaining image must be dated by a sheet whose date lies in or before the last year of its run, and the image first requested for 2006 by the sheet filed under 2007, not by the 2001 sheet the first year would give. A blank answer from the service can drop a year, so the 2006 assertion runs only when that image is present.</para>
        /// <para>Medium test (calls a live external service): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task OrtoData_Acquisition_Live()
        {
            Point2D point2D = new(637500, 487500);
            BoundingBox2D boundingBox2D = new(new Point2D(point2D.X - 128, point2D.Y - 128), new Point2D(point2D.X + 128, point2D.Y + 128));

            using HttpClient httpClient = new() { Timeout = TimeSpan.FromMinutes(2) };

            List<OrtoSheet> ortoSheets = [];
            for (int year = 2001; year <= 2012; year++)
            {
                List<OrtoSheet>? ortoSheets_Year = await Query.OrtoSheetsAsync(httpClient, Query.OrtoSheetTypeName(year), boundingBox2D, Constants.WebService.OrtoSheetPageSize, 3, TimeSpan.FromSeconds(2));
                Assert.NotNull(ortoSheets_Year);
                ortoSheets.AddRange(ortoSheets_Year);
            }

            OrtoDatas? ortoDatas = await Create.OrtoDatas(httpClient, boundingBox2D, "OrtoData_Acquisition_Live", new Range<int>(2001, 2012), 2, true, 8, ortoSheets);
            Assert.NotNull(ortoDatas);
            Assert.NotEmpty(ortoDatas);

            foreach (OrtoData ortoData in ortoDatas)
            {
                Assert.NotNull(ortoData.ContentHash);
                Assert.NotNull(ortoData.AcquisitionDate);
                Assert.NotEqual(OrtoDatePrecision.Undefined, ortoData.AcquisitionDatePrecision);
                Assert.True(ortoData.AcquisitionDate.Value.Year <= 2012);
            }

            OrtoData? ortoData_2006 = ortoDatas.FirstOrDefault(x => x.DateTime.Year == 2006);
            if (ortoData_2006 != null)
            {
                Assert.Equal(2007, ortoData_2006.AcquisitionDate!.Value.Year);
                Assert.Equal("N-34-139-A-c-1-1", ortoData_2006.SheetId);
            }
        }
    }
}
