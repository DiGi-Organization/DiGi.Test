using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that an orthophoto index row survives a JSON round trip and a copy with every member populated, and with the nullable members null.
        /// <para>The copy is asserted directly because SerializationCheck never runs the copy constructor.</para>
        /// </summary>
        [Fact]
        public void OrtoSheet()
        {
            DateTimeOffset date = new(2024, 7, 18, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset registerDate = new(2024, 10, 2, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset probeDate = new(2026, 11, 3, 13, 15, 0, TimeSpan.FromHours(1));
            Polygon2D polygon2D = new([new Point2D(500000, 300000), new Point2D(500600, 300020), new Point2D(500580, 300540), new Point2D(500010, 300500)]);

            Classes.OrtoSheet ortoSheet = new(
                "N-34-138-B-d-1-4",
                "SkorowidzOrtofomapy2024",
                2024,
                date,
                OrtoDatePrecision.Day,
                0.25,
                OrtoColorMode.CIR,
                "Zdj. cyfrowe",
                "PL-1992",
                "1:5000",
                "DFT.7201.001.2024",
                true,
                "https://opendata.geoportal.gov.pl/ortofotomapa/81434/81434_1416790_N-34-138-B-d-1-4.tif",
                registerDate,
                512,
                polygon2D,
                OrtoType.Standard,
                probeDate);

            Assert.Equal("N-34-138-B-d-1-4", ortoSheet.SheetId);
            Assert.Equal("SkorowidzOrtofomapy2024", ortoSheet.TypeName);
            Assert.Equal(2024, ortoSheet.Year);
            Assert.Equal(date, ortoSheet.Date);
            Assert.Equal(OrtoDatePrecision.Day, ortoSheet.DatePrecision);
            Assert.Equal(0.25, ortoSheet.PixelSize);
            Assert.Equal(OrtoColorMode.CIR, ortoSheet.ColorMode);
            Assert.Equal("Zdj. cyfrowe", ortoSheet.Source);
            Assert.Equal("PL-1992", ortoSheet.CoordinateSystem);
            Assert.Equal("1:5000", ortoSheet.ArchiveModule);
            Assert.Equal("DFT.7201.001.2024", ortoSheet.ApplicationNumber);
            Assert.True(ortoSheet.SheetFilled);
            Assert.EndsWith("81434_1416790_N-34-138-B-d-1-4.tif", ortoSheet.DownloadUrl);
            Assert.Equal(registerDate, ortoSheet.RegisterDate);
            Assert.Equal(512, ortoSheet.FileSize);
            Assert.Equal(4, ortoSheet.Footprint?.GetPoints()?.Count);
            Assert.Equal(OrtoType.Standard, ortoSheet.OrtoType);
            Assert.Equal(probeDate, ortoSheet.ProbeDate);

            string? json = Core.Convert.ToSystem_String(ortoSheet);
            Assert.False(string.IsNullOrWhiteSpace(json));

            Classes.OrtoSheet? ortoSheet_Json = Core.Convert.ToDiGi<Classes.OrtoSheet>(json)?.FirstOrDefault();
            Assert.NotNull(ortoSheet_Json);
            Assert.Equal(ortoSheet.SheetId, ortoSheet_Json.SheetId);
            Assert.Equal(ortoSheet.Date, ortoSheet_Json.Date);
            Assert.Equal(ortoSheet.ProbeDate?.Offset, ortoSheet_Json.ProbeDate?.Offset);
            Assert.Equal(ortoSheet.DatePrecision, ortoSheet_Json.DatePrecision);
            Assert.Equal(ortoSheet.ColorMode, ortoSheet_Json.ColorMode);
            Assert.Equal(ortoSheet.OrtoType, ortoSheet_Json.OrtoType);
            Assert.Equal(ortoSheet.SheetFilled, ortoSheet_Json.SheetFilled);
            Assert.Equal(ortoSheet.Footprint?.GetArea(), ortoSheet_Json.Footprint?.GetArea());

            Core.xUnit.Query.SerializationCheck(ortoSheet);

            Classes.OrtoSheet ortoSheet_Copy = new(ortoSheet);
            Assert.Equal(ortoSheet.SheetId, ortoSheet_Copy.SheetId);
            Assert.Equal(ortoSheet.TypeName, ortoSheet_Copy.TypeName);
            Assert.Equal(ortoSheet.Year, ortoSheet_Copy.Year);
            Assert.Equal(ortoSheet.Date, ortoSheet_Copy.Date);
            Assert.Equal(ortoSheet.DatePrecision, ortoSheet_Copy.DatePrecision);
            Assert.Equal(ortoSheet.PixelSize, ortoSheet_Copy.PixelSize);
            Assert.Equal(ortoSheet.ColorMode, ortoSheet_Copy.ColorMode);
            Assert.Equal(ortoSheet.Source, ortoSheet_Copy.Source);
            Assert.Equal(ortoSheet.CoordinateSystem, ortoSheet_Copy.CoordinateSystem);
            Assert.Equal(ortoSheet.ArchiveModule, ortoSheet_Copy.ArchiveModule);
            Assert.Equal(ortoSheet.ApplicationNumber, ortoSheet_Copy.ApplicationNumber);
            Assert.Equal(ortoSheet.SheetFilled, ortoSheet_Copy.SheetFilled);
            Assert.Equal(ortoSheet.DownloadUrl, ortoSheet_Copy.DownloadUrl);
            Assert.Equal(ortoSheet.RegisterDate, ortoSheet_Copy.RegisterDate);
            Assert.Equal(ortoSheet.FileSize, ortoSheet_Copy.FileSize);
            Assert.Equal(ortoSheet.Footprint?.GetArea(), ortoSheet_Copy.Footprint?.GetArea());
            Assert.Equal(ortoSheet.OrtoType, ortoSheet_Copy.OrtoType);
            Assert.Equal(ortoSheet.ProbeDate, ortoSheet_Copy.ProbeDate);

            Classes.OrtoSheet ortoSheet_Null = new(null, null, 1957, null, OrtoDatePrecision.Undefined, null, OrtoColorMode.Undefined, null, null, null, null, null, null, null, null, null, OrtoType.Undefined, null);

            Core.xUnit.Query.SerializationCheck(ortoSheet_Null);

            Classes.OrtoSheet ortoSheet_NullCopy = new(ortoSheet_Null);
            Assert.Null(ortoSheet_NullCopy.SheetId);
            Assert.Null(ortoSheet_NullCopy.Date);
            Assert.Null(ortoSheet_NullCopy.Footprint);
            Assert.Null(ortoSheet_NullCopy.SheetFilled);
            Assert.Equal(1957, ortoSheet_NullCopy.Year);
        }
    }
}
