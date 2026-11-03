using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests re-dating a stored, reduced orthophoto series by the sheet index (DiGi.GIS#20): <see cref="Query.OrtoDataRunEndYears"/> and <see cref="Create.OrtoDatas(OrtoDatas, IEnumerable{OrtoSheet}, int)"/>.
        /// <para>The series reproduces county 1465 probe point (637500, 487500) after <see cref="OrtoDatas.Reduce"/>: images first requested for 2001, 2006 and 2011, fetched up to 2012. Re-dated against the sheets of <see cref="OrtoData_Acquisition"/>, they resolve to 2001-05-13, the sheet filed under 2007 (year precision) and 2011-04-21 (RGB). Images are the <c>OrtoImage_Warszawa_2023_Content</c> fixture with one byte before the end-of-image marker changed, so the header (and the image size the area is rebuilt from) is shared and the content hash is not.</para>
        /// <para>Also covers a single-image series (its run ends at the last year), an image whose size cannot be read (kept unchanged), null input, idempotence, and that the reference and Guid are kept. The run-end guard was shown to fail: dating the stored 2006 image by its own year resolves it to the 2001 sheet.</para>
        /// </summary>
        [Fact]
        public void OrtoDatas_Redate()
        {
            Point2D point2D = new(637500, 487500);

            List<OrtoSheet?> ortoSheets =
            [
                Sheet("N-34-139-A-c-1", 2001, new DateTimeOffset(2001, 5, 13, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.75, OrtoColorMode.RGB),
                Sheet("N-34-139-A-c-1-1", 2007, new DateTimeOffset(2007, 1, 1, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Year, 0.5, OrtoColorMode.RGB),
                Sheet("N-34-139-A-c-1-1", 2011, new DateTimeOffset(2011, 4, 21, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.25, OrtoColorMode.CIR),
                Sheet("N-34-139-A-c-1-1", 2011, new DateTimeOffset(2011, 4, 21, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.25, OrtoColorMode.RGB),
                Sheet("N-34-139-A-c-1-1", 2013, new DateTimeOffset(2013, 6, 14, 0, 0, 0, TimeSpan.Zero), OrtoDatePrecision.Day, 0.1, OrtoColorMode.RGB),
            ];

            byte[] bytes = File.ReadAllBytes(Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "OrtoImage_Warszawa_2023_Content.jpg")!);

            // The image covers 256 m around the point at 2 px/m, as the probe tiles did.
            double scale = 2;
            Point2D location = new(point2D.X - 128, point2D.Y + 128);

            OrtoData ortoData_2001 = new(new DateTime(2001, 1, 1), Variant(1), scale, location);
            OrtoData ortoData_2006 = new(new DateTime(2006, 1, 1), Variant(2), scale, location);
            OrtoData ortoData_2011 = new(new DateTime(2011, 1, 1), Variant(3), scale, location);
            OrtoDatas ortoDatas = new("Building_637500_487500", [ortoData_2011, ortoData_2001, ortoData_2006]);

            Dictionary<DateTime, int> lastYears = Query.OrtoDataRunEndYears(ortoDatas, 2012)!;
            Assert.Equal(2005, lastYears[new DateTime(2001, 1, 1)]);
            Assert.Equal(2010, lastYears[new DateTime(2006, 1, 1)]);
            Assert.Equal(2012, lastYears[new DateTime(2011, 1, 1)]);
            Assert.Null(Query.OrtoDataRunEndYears(null, 2012));

            OrtoDatas? ortoDatas_Redated = Create.OrtoDatas(ortoDatas, ortoSheets, 2012);
            Assert.NotNull(ortoDatas_Redated);
            Assert.Equal(ortoDatas.Reference, ortoDatas_Redated.Reference);
            Assert.Equal(ortoDatas.Guid, ortoDatas_Redated.Guid);

            List<OrtoData> ortoDatas_List = [.. ortoDatas_Redated];
            Assert.Equal(3, ortoDatas_List.Count);

            Assert.Equal(new DateTimeOffset(2001, 5, 13, 0, 0, 0, TimeSpan.Zero), ortoDatas_List[0].AcquisitionDate);
            Assert.Equal(OrtoDatePrecision.Day, ortoDatas_List[0].AcquisitionDatePrecision);

            Assert.Equal(new DateTime(2006, 1, 1), ortoDatas_List[1].DateTime);
            Assert.Equal(new DateTimeOffset(2007, 1, 1, 0, 0, 0, TimeSpan.Zero), ortoDatas_List[1].AcquisitionDate);
            Assert.Equal(OrtoDatePrecision.Year, ortoDatas_List[1].AcquisitionDatePrecision);
            Assert.Equal("N-34-139-A-c-1-1", ortoDatas_List[1].SheetId);

            Assert.Equal(new DateTimeOffset(2011, 4, 21, 0, 0, 0, TimeSpan.Zero), ortoDatas_List[2].AcquisitionDate);

            // Bytes, scale and location are carried over; every image is hashed.
            for (int i = 0; i < 3; i++)
            {
                OrtoData ortoData = new[] { ortoData_2001, ortoData_2006, ortoData_2011 }[i];
                Assert.Equal(ortoData.Bytes, ortoDatas_List[i].Bytes);
                Assert.Equal(ortoData.Scale, ortoDatas_List[i].Scale);
                Assert.Equal(ortoData.Location, ortoDatas_List[i].Location);
                Assert.Equal(Query.ContentHash(ortoData.Bytes), ortoDatas_List[i].ContentHash);
            }

            // Idempotent.
            OrtoDatas? ortoDatas_Again = Create.OrtoDatas(ortoDatas_Redated, ortoSheets, 2012);
            Assert.Equal(Core.Convert.ToSystem_String((Core.Interfaces.ISerializableObject)ortoDatas_Redated), Core.Convert.ToSystem_String((Core.Interfaces.ISerializableObject?)ortoDatas_Again));

            // A single image stands for every year up to the last one.
            OrtoDatas ortoDatas_Single = new("Single", [ortoData_2006]);
            Assert.Equal(2012, Query.OrtoDataRunEndYears(ortoDatas_Single, 2012)![new DateTime(2006, 1, 1)]);
            Assert.Equal(new DateTimeOffset(2011, 4, 21, 0, 0, 0, TimeSpan.Zero), Create.OrtoDatas(ortoDatas_Single, ortoSheets, 2012)!.Single().AcquisitionDate);

            // An image whose size cannot be read is kept as it was; no sheets leave the images undated.
            OrtoData ortoData_Unreadable = new(new DateTime(2003, 1, 1), [1, 2, 3], scale, location);
            OrtoData ortoData_Kept = Create.OrtoDatas(new OrtoDatas("Unreadable", [ortoData_Unreadable]), ortoSheets, 2012)!.Single();
            Assert.Equal(ortoData_Unreadable.Bytes, ortoData_Kept.Bytes);
            Assert.Null(ortoData_Kept.AcquisitionDate);

            Assert.All(Create.OrtoDatas(ortoDatas, null, 2012)!, x => Assert.Equal(OrtoDatePrecision.Undefined, x.AcquisitionDatePrecision));
            Assert.Null(Create.OrtoDatas((OrtoDatas?)null, ortoSheets, 2012));
            Assert.Empty(Create.OrtoDatas(new OrtoDatas("Empty", []), ortoSheets, 2012)!);

            byte[] Variant(byte value)
            {
                byte[] result = [.. bytes];
                result[^3] = value;
                return result;
            }

            static OrtoSheet Sheet(string sheetId, int year, DateTimeOffset date, OrtoDatePrecision ortoDatePrecision, double pixelSize, OrtoColorMode ortoColorMode)
            {
                Polygon2D polygon2D = new([new Point2D(636500, 486500), new Point2D(638500, 486500), new Point2D(638500, 488500), new Point2D(636500, 488500)]);
                return new OrtoSheet(sheetId, "SkorowidzOrtofomapy" + year, year, date, ortoDatePrecision, pixelSize, ortoColorMode, "Zdj. cyfrowe", "PL-1992", null, null, true, "https://opendata.geoportal.gov.pl/ortofotomapa/" + sheetId + "-" + year + "-" + ortoColorMode + ".tif", null, null, polygon2D, OrtoType.Standard, null);
            }
        }
    }
}
