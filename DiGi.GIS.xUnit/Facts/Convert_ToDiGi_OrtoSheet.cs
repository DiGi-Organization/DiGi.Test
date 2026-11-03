using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the conversion of real GUGiK orthophoto index responses (captured 2026-11-03) to <see cref="Classes.OrtoSheet"/>: every field, the northing-first axis swap, and the date precision rule.
        /// <para>The 2005 row is analog with a <c>2005-01-01</c> placeholder (year precision); the 2023 row is digital with a real day. A feature without a sheet identifier converts to null, and a TERYT-like identifier is kept verbatim.</para>
        /// </summary>
        [Fact]
        public void Convert_ToDiGi_OrtoSheet()
        {
            DateTimeOffset probeDate = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

            List<Classes.OrtoSheet> ortoSheets_2005;
            using (FileStream fileStream = File.OpenRead(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoSheets_2005.gml")!))
            {
                ortoSheets_2005 = [.. Convert.ToDiGi_OrtoSheets(fileStream, probeDate)];
            }

            Assert.Equal(2, ortoSheets_2005.Count);

            Classes.OrtoSheet ortoSheet_2005 = ortoSheets_2005[0];
            Assert.Equal("N-33-117-D-a-3-4", ortoSheet_2005.SheetId);
            Assert.Equal("SkorowidzOrtofomapy2005", ortoSheet_2005.TypeName);
            Assert.Equal(2005, ortoSheet_2005.Year);
            Assert.Equal(new DateTimeOffset(2005, 1, 1, 0, 0, 0, TimeSpan.Zero), ortoSheet_2005.Date);
            Assert.Equal(OrtoDatePrecision.Year, ortoSheet_2005.DatePrecision);
            Assert.Equal(0.5, ortoSheet_2005.PixelSize);
            Assert.Equal(OrtoColorMode.BlackAndWhite, ortoSheet_2005.ColorMode);
            Assert.Equal("Zdj. analogowe", ortoSheet_2005.Source);
            Assert.Equal("PL-1992", ortoSheet_2005.CoordinateSystem);
            Assert.Equal("1:5000", ortoSheet_2005.ArchiveModule);
            Assert.Equal("N-33-020/04", ortoSheet_2005.ApplicationNumber);
            Assert.True(ortoSheet_2005.SheetFilled);
            Assert.Equal("https://opendata.geoportal.gov.pl/ortofotomapa/37/37_62336_N-33-117-D-a-3-4.tif", ortoSheet_2005.DownloadUrl);
            Assert.Equal(new DateTimeOffset(2006, 1, 18, 0, 0, 0, TimeSpan.Zero), ortoSheet_2005.RegisterDate);
            Assert.Equal(9, ortoSheet_2005.FileSize);
            Assert.Equal(OrtoType.Standard, ortoSheet_2005.OrtoType);
            Assert.Equal(probeDate, ortoSheet_2005.ProbeDate);

            // The feature envelope reads "546092.83 316564.96" / "548488.17 318759.55": northing first. Easting must land in X.
            BoundingBox2D? boundingBox2D = ortoSheet_2005.Footprint?.GetBoundingBox();
            Assert.NotNull(boundingBox2D);
            Assert.Equal(316564.96, boundingBox2D.Min.X, 6);
            Assert.Equal(546092.83, boundingBox2D.Min.Y, 6);
            Assert.Equal(318759.55, boundingBox2D.Max.X, 6);
            Assert.Equal(548488.17, boundingBox2D.Max.Y, 6);

            List<Classes.OrtoSheet> ortoSheets_2023;
            using (FileStream fileStream = File.OpenRead(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoSheets_2023.gml")!))
            {
                ortoSheets_2023 = [.. Convert.ToDiGi_OrtoSheets(fileStream, probeDate)];
            }

            Assert.Equal(2, ortoSheets_2023.Count);

            Classes.OrtoSheet ortoSheet_2023 = ortoSheets_2023[0];
            Assert.Equal("M-34-76-B-a-1-2", ortoSheet_2023.SheetId);
            Assert.Equal(new DateTimeOffset(2023, 3, 1, 0, 0, 0, TimeSpan.Zero), ortoSheet_2023.Date);
            Assert.Equal(OrtoDatePrecision.Day, ortoSheet_2023.DatePrecision);
            Assert.Equal(0.05, ortoSheet_2023.PixelSize);
            Assert.Equal(OrtoColorMode.RGB, ortoSheet_2023.ColorMode);
            Assert.Equal("Zdj. cyfrowe", ortoSheet_2023.Source);
            Assert.Equal(1169, ortoSheet_2023.FileSize);

            BoundingBox2D? boundingBox2D_2023 = ortoSheet_2023.Footprint?.GetBoundingBox();
            Assert.NotNull(boundingBox2D_2023);
            Assert.Equal(555972.80, boundingBox2D_2023.Min.X, 6);
            Assert.Equal(234945.10, boundingBox2D_2023.Min.Y, 6);

            XNamespace xNamespace = "http://www.gugik.gov.pl";

            // A digital 1 January is a real day: only the analog source makes it a placeholder.
            XElement xElement_Teryt = new(
                xNamespace + "SkorowidzOrtofomapy2021",
                new XElement(xNamespace + "godlo", "7.173.20.12"),
                new XElement(xNamespace + "akt_rok", "2021"),
                new XElement(xNamespace + "akt_data", "2021-01-01"),
                new XElement(xNamespace + "zrodlo_danych", "Zdj. cyfrowe"),
                new XElement(xNamespace + "kolor", "CIR"),
                new XElement(xNamespace + "czy_ark_wypelniony", "NIE"));

            Classes.OrtoSheet? ortoSheet_Teryt = Convert.ToDiGi_OrtoSheet(xElement_Teryt);
            Assert.NotNull(ortoSheet_Teryt);
            Assert.Equal("7.173.20.12", ortoSheet_Teryt.SheetId);
            Assert.Equal(OrtoDatePrecision.Day, ortoSheet_Teryt.DatePrecision);
            Assert.Equal(OrtoColorMode.CIR, ortoSheet_Teryt.ColorMode);
            Assert.False(ortoSheet_Teryt.SheetFilled);
            Assert.Null(ortoSheet_Teryt.Footprint);
            Assert.Null(ortoSheet_Teryt.PixelSize);

            XElement xElement_Unknown = new(xNamespace + "SkorowidzOrtofomapy2021", new XElement(xNamespace + "godlo", "X"), new XElement(xNamespace + "akt_rok", "2021"), new XElement(xNamespace + "kolor", "Sepia"));
            Assert.Equal(OrtoColorMode.Undefined, Convert.ToDiGi_OrtoSheet(xElement_Unknown)?.ColorMode);
            Assert.Equal(OrtoDatePrecision.Undefined, Convert.ToDiGi_OrtoSheet(xElement_Unknown)?.DatePrecision);

            XElement xElement_Malformed = new(xNamespace + "SkorowidzOrtofomapy2021", new XElement(xNamespace + "akt_rok", "2021"));
            Assert.Null(Convert.ToDiGi_OrtoSheet(xElement_Malformed));
            Assert.Null(Convert.ToDiGi_OrtoSheet(null));

            using MemoryStream memoryStream_Exception = new(System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><ows:ExceptionReport xmlns:ows=\"http://www.opengis.net/ows\"><ows:Exception exceptionCode=\"MissingParameterValue\"/></ows:ExceptionReport>"));
            Assert.Empty(Convert.ToDiGi_OrtoSheets(memoryStream_Exception).ToList());
        }
    }
}
