using DiGi.EPW.Classes;
using System.Reflection;

namespace DiGi.EPW.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests reading a sample EPW file and verifying that the location, header records, and hourly data records are parsed correctly.
        /// <para>The sample is the IMGW typical year of Warszawa-Okęcie (WMO 123750; ministry data, CC0 1.0), which replaced the ASHRAE-copyrighted IWEC file of the same station (DiGi-Organization/DiGi.Test#4).</para>
        /// </summary>
        [Fact]
        public void Read()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(System.IO.File.Exists(path));

            EPWFile? ePWFile = Modify.Read(path);
            Assert.NotNull(ePWFile);

            Location? location = ePWFile.Location;
            Assert.NotNull(location);
            Assert.Equal("Warszawa-Chopina Intl AP", location.City);
            Assert.Equal(52.25, location.Latitude);
            Assert.Equal(21.0, location.Longitude);
            Assert.Equal(106.0, location.Elevation);

            Assert.NotNull(ePWFile.DesignConditions);

            Assert.NotNull(ePWFile.TypicalExtremePeriods);
            Assert.Equal(6, ePWFile.TypicalExtremePeriods.Count);

            Assert.NotNull(ePWFile.GroundTemperatures);
            Assert.Equal(3, ePWFile.GroundTemperatures.Count);

            Assert.NotNull(ePWFile.HolidaysDaylightSaving);

            Assert.False(string.IsNullOrWhiteSpace(ePWFile.Comments1));
            Assert.False(string.IsNullOrWhiteSpace(ePWFile.Comments2));

            Assert.NotNull(ePWFile.DataPeriods);
            Assert.Single(ePWFile.DataPeriods);

            Assert.NotNull(ePWFile.DataRecords);
            Assert.Equal(8760, ePWFile.DataRecords.Count);

            DataRecord dataRecord_First = ePWFile.DataRecords[0];
            Assert.Equal(new System.DateTime(2005, 1, 1, 1, 0, 0, System.DateTimeKind.Unspecified), dataRecord_First.DateTime);
            Assert.Equal(0.6f, dataRecord_First.DryBulbTemperature);
            Assert.Equal(-0.7f, dataRecord_First.DewPointTemperature);
            Assert.Equal(91f, dataRecord_First.RelativeHumidity);
        }

        /// <summary>
        /// Tests that reading an EPW file, writing it back to a new file, and reading it again produces equivalent data.
        /// </summary>
        [Fact]
        public void Write()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));

            EPWFile? ePWFile_1 = Modify.Read(path);
            Assert.NotNull(ePWFile_1);

            string path_Temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + ".epw");

            bool result = Modify.Write(ePWFile_1, path_Temp);
            Assert.True(result);

            EPWFile? ePWFile_2 = Modify.Read(path_Temp);
            Assert.NotNull(ePWFile_2);

            System.IO.File.Delete(path_Temp);

            Assert.Equal(ePWFile_1.Location?.City, ePWFile_2.Location?.City);
            Assert.Equal(ePWFile_1.Location?.Latitude, ePWFile_2.Location?.Latitude);
            Assert.Equal(ePWFile_1.Location?.Longitude, ePWFile_2.Location?.Longitude);
            Assert.Equal(ePWFile_1.Location?.Elevation, ePWFile_2.Location?.Elevation);

            Assert.Equal(ePWFile_1.TypicalExtremePeriods?.Count, ePWFile_2.TypicalExtremePeriods?.Count);
            Assert.Equal(ePWFile_1.GroundTemperatures?.Count, ePWFile_2.GroundTemperatures?.Count);
            Assert.Equal(ePWFile_1.DataPeriods?.Count, ePWFile_2.DataPeriods?.Count);
            Assert.Equal(ePWFile_1.DataRecords?.Count, ePWFile_2.DataRecords?.Count);

            Assert.NotNull(ePWFile_1.DataRecords);
            Assert.NotNull(ePWFile_2.DataRecords);

            DataRecord dataRecord_1 = ePWFile_1.DataRecords[0];
            DataRecord dataRecord_2 = ePWFile_2.DataRecords[0];

            Assert.Equal(dataRecord_1.DateTime, dataRecord_2.DateTime);
            Assert.Equal(dataRecord_1.DryBulbTemperature, dataRecord_2.DryBulbTemperature);
            Assert.Equal(dataRecord_1.DewPointTemperature, dataRecord_2.DewPointTemperature);
            Assert.Equal(dataRecord_1.RelativeHumidity, dataRecord_2.RelativeHumidity);
        }

        /// <summary>
        /// Tests that an <see cref="EPWFile"/> instance read from a sample EPW file can be cloned and round-tripped through JSON serialization without loss of data.
        /// </summary>
        [Fact]
        public void SerializationCheck()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));

            EPWFile? ePWFile = Modify.Read(path);
            Assert.NotNull(ePWFile);

            Core.xUnit.Query.SerializationCheck(ePWFile);

            EPWFile? ePWFile_Clone = Core.Query.Clone(ePWFile);
            Assert.NotNull(ePWFile_Clone);

            Assert.Equal(ePWFile.Location?.City, ePWFile_Clone.Location?.City);
            Assert.Equal(ePWFile.DataRecords?.Count, ePWFile_Clone.DataRecords?.Count);
            Assert.Equal(ePWFile.TypicalExtremePeriods?.Count, ePWFile_Clone.TypicalExtremePeriods?.Count);
            Assert.Equal(ePWFile.GroundTemperatures?.Count, ePWFile_Clone.GroundTemperatures?.Count);
            Assert.Equal(ePWFile.HolidaysDaylightSaving?.LeapYearObserved, ePWFile_Clone.HolidaysDaylightSaving?.LeapYearObserved);
        }
    }
}