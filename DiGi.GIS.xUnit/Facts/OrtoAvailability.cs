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
        /// Tests that an epoch, an availability cell and an availability matrix survive a JSON round trip and a copy with every member populated (nested epochs and cells included), and with the nullable members null.
        /// <para>The copies are asserted directly because SerializationCheck never runs the copy constructor.</para>
        /// </summary>
        [Fact]
        public void OrtoAvailability()
        {
            DateTimeOffset date_1 = new(2010, 5, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset date_2 = new(2015, 6, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset probeDate = new(2026, 11, 3, 13, 15, 0, TimeSpan.FromHours(1));

            Classes.OrtoEpoch ortoEpoch_1 = new(date_1, OrtoDatePrecision.Year, 0.5, ["https://example.org/a.tif"]);
            Classes.OrtoEpoch ortoEpoch_2 = new(date_2, OrtoDatePrecision.Day, 0.25, ["https://example.org/b.tif", "https://example.org/c.tif"]);

            Assert.Equal(date_1, ortoEpoch_1.Date);
            Assert.Equal(OrtoDatePrecision.Year, ortoEpoch_1.DatePrecision);
            Assert.Equal(0.5, ortoEpoch_1.PixelSize);
            Assert.Equal(["https://example.org/a.tif"], ortoEpoch_1.DownloadUrls!);

            Core.xUnit.Query.SerializationCheck(ortoEpoch_2);

            Classes.OrtoEpoch? ortoEpoch_Json = Core.Convert.ToDiGi<Classes.OrtoEpoch>(Core.Convert.ToSystem_String(ortoEpoch_2))?.FirstOrDefault();
            Assert.NotNull(ortoEpoch_Json);
            Assert.Equal(date_2, ortoEpoch_Json.Date);
            Assert.Equal(2, ortoEpoch_Json.DownloadUrls?.Count);

            Classes.OrtoEpoch ortoEpoch_Copy = new(ortoEpoch_2);
            Assert.Equal(ortoEpoch_2.Date, ortoEpoch_Copy.Date);
            Assert.Equal(ortoEpoch_2.DatePrecision, ortoEpoch_Copy.DatePrecision);
            Assert.Equal(ortoEpoch_2.PixelSize, ortoEpoch_Copy.PixelSize);
            Assert.Equal(ortoEpoch_2.DownloadUrls!, ortoEpoch_Copy.DownloadUrls!);

            Classes.OrtoEpoch ortoEpoch_Null = new(date_1, OrtoDatePrecision.Undefined, null, null);
            Core.xUnit.Query.SerializationCheck(ortoEpoch_Null);
            Assert.Null(new Classes.OrtoEpoch(ortoEpoch_Null).DownloadUrls);

            BoundingBox2D boundingBox2D = new(new Point2D(500000, 300000), new Point2D(505000, 305000));
            List<int> gapDays = [(int)Math.Round((date_2 - date_1).TotalDays)];

            Classes.OrtoAvailability ortoAvailability = new("500000_300000", boundingBox2D, [ortoEpoch_1, ortoEpoch_2], gapDays, probeDate);

            Assert.Equal("500000_300000", ortoAvailability.LocationKey);
            Assert.Equal(505000, ortoAvailability.BoundingBox2D?.Max.X);
            Assert.Equal(2, ortoAvailability.OrtoEpochs?.Count);
            Assert.Equal(gapDays, ortoAvailability.GapDays!);
            Assert.Equal(probeDate, ortoAvailability.ProbeDate);

            Core.xUnit.Query.SerializationCheck(ortoAvailability);

            Classes.OrtoAvailability? ortoAvailability_Json = Core.Convert.ToDiGi<Classes.OrtoAvailability>(Core.Convert.ToSystem_String(ortoAvailability))?.FirstOrDefault();
            Assert.NotNull(ortoAvailability_Json);
            Assert.Equal(date_2, ortoAvailability_Json.OrtoEpochs?[1].Date);
            Assert.Equal(gapDays, ortoAvailability_Json.GapDays!);

            Classes.OrtoAvailability ortoAvailability_Copy = new(ortoAvailability);
            Assert.Equal(ortoAvailability.LocationKey, ortoAvailability_Copy.LocationKey);
            Assert.Equal(ortoAvailability.BoundingBox2D?.Min.Y, ortoAvailability_Copy.BoundingBox2D?.Min.Y);
            Assert.Equal(ortoAvailability.OrtoEpochs?[0].Date, ortoAvailability_Copy.OrtoEpochs?[0].Date);
            Assert.Equal(ortoAvailability.OrtoEpochs?.Count, ortoAvailability_Copy.OrtoEpochs?.Count);
            Assert.Equal(ortoAvailability.GapDays!, ortoAvailability_Copy.GapDays!);
            Assert.Equal(ortoAvailability.ProbeDate, ortoAvailability_Copy.ProbeDate);

            Classes.OrtoAvailability ortoAvailability_Null = new(null, null, null, null, null);
            Core.xUnit.Query.SerializationCheck(ortoAvailability_Null);
            Assert.Null(new Classes.OrtoAvailability(ortoAvailability_Null).OrtoEpochs);

            Classes.OrtoAvailabilityMatrix ortoAvailabilityMatrix = new(5000, 0.9, [ortoAvailability], probeDate);

            Assert.Equal(5000, ortoAvailabilityMatrix.CellSize);
            Assert.Equal(0.9, ortoAvailabilityMatrix.CoverageFraction);
            Assert.Single(ortoAvailabilityMatrix.OrtoAvailabilities!);
            Assert.Equal(probeDate, ortoAvailabilityMatrix.ProbeDate);

            Core.xUnit.Query.SerializationCheck(ortoAvailabilityMatrix);

            Classes.OrtoAvailabilityMatrix? ortoAvailabilityMatrix_Json = Core.Convert.ToDiGi<Classes.OrtoAvailabilityMatrix>(Core.Convert.ToSystem_String(ortoAvailabilityMatrix))?.FirstOrDefault();
            Assert.NotNull(ortoAvailabilityMatrix_Json);
            Assert.Equal(2, ortoAvailabilityMatrix_Json.OrtoAvailabilities?[0].OrtoEpochs?.Count);

            Classes.OrtoAvailabilityMatrix ortoAvailabilityMatrix_Copy = new(ortoAvailabilityMatrix);
            Assert.Equal(ortoAvailabilityMatrix.CellSize, ortoAvailabilityMatrix_Copy.CellSize);
            Assert.Equal(ortoAvailabilityMatrix.CoverageFraction, ortoAvailabilityMatrix_Copy.CoverageFraction);
            Assert.Equal(ortoAvailabilityMatrix.OrtoAvailabilities?[0].LocationKey, ortoAvailabilityMatrix_Copy.OrtoAvailabilities?[0].LocationKey);
            Assert.Equal(ortoAvailabilityMatrix.ProbeDate, ortoAvailabilityMatrix_Copy.ProbeDate);

            Classes.OrtoAvailabilityMatrix ortoAvailabilityMatrix_Null = new(1, 1, null, null);
            Core.xUnit.Query.SerializationCheck(ortoAvailabilityMatrix_Null);
            Assert.Null(new Classes.OrtoAvailabilityMatrix(ortoAvailabilityMatrix_Null).OrtoAvailabilities);
        }
    }
}
