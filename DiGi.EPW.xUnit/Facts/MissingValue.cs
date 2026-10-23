using DiGi.EPW.Classes;
using System;
using System.Reflection;

namespace DiGi.EPW.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that EPW missing-value markers (9999 for the radiation fields, 999 for snow depth and albedo) are decoded as null by the value queries, while values just below the markers remain measurements and the raw properties and EPW text keep the markers.
        /// </summary>
        [Fact]
        public void MissingValue_Decoding()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));

            string[] lines = System.IO.File.ReadAllLines(path);
            int index_Data = Array.FindIndex(lines, x =>
            {
                string[] tokens = x.Split(',');
                return tokens.Length >= 35 && int.TryParse(tokens[0], out _);
            });
            Assert.True(index_Data >= 0);

            string[] values = lines[index_Data].Split(',');
            values[13] = "9999";
            values[14] = "9999";
            values[15] = "9999";
            values[30] = "999";
            values[32] = "999";

            DataRecord? dataRecord_Missing = Create.DataRecord([string.Join(",", values)], 0, out int index_Next);
            Assert.NotNull(dataRecord_Missing);
            Assert.Equal(1, index_Next);

            Assert.Null(dataRecord_Missing.GlobalHorizontalRadiationValue());
            Assert.Null(dataRecord_Missing.DirectNormalRadiationValue());
            Assert.Null(dataRecord_Missing.DiffuseHorizontalRadiationValue());
            Assert.Null(dataRecord_Missing.SnowDepthValue());
            Assert.Null(dataRecord_Missing.AlbedoValue());

            Assert.Equal(9999f, dataRecord_Missing.GlobalHorizontalRadiation);
            Assert.Equal(9999f, dataRecord_Missing.DirectNormalRadiation);
            Assert.Equal(9999f, dataRecord_Missing.DiffuseHorizontalRadiation);
            Assert.Equal(999f, dataRecord_Missing.SnowDepth);
            Assert.Equal(999f, dataRecord_Missing.Albedo);

            string[] values_Written = dataRecord_Missing.ToSystem_String().Split(',');
            Assert.Equal("9999", values_Written[13]);
            Assert.Equal("9999", values_Written[14]);
            Assert.Equal("9999", values_Written[15]);
            Assert.Equal("999", values_Written[30]);
            Assert.Equal("999", values_Written[32]);

            string[] values_Boundary = lines[index_Data].Split(',');
            values_Boundary[13] = "9998.9";
            values_Boundary[14] = "9998.9";
            values_Boundary[15] = "9998.9";
            values_Boundary[30] = "998.9";
            values_Boundary[32] = "0.9";

            DataRecord? dataRecord_Boundary = Create.DataRecord([string.Join(",", values_Boundary)], 0, out _);
            Assert.NotNull(dataRecord_Boundary);

            Assert.Equal(9998.9f, dataRecord_Boundary.GlobalHorizontalRadiationValue());
            Assert.Equal(9998.9f, dataRecord_Boundary.DirectNormalRadiationValue());
            Assert.Equal(9998.9f, dataRecord_Boundary.DiffuseHorizontalRadiationValue());
            Assert.Equal(998.9f, dataRecord_Boundary.SnowDepthValue());
            Assert.Equal(0.9f, dataRecord_Boundary.AlbedoValue());
        }

        /// <summary>
        /// Tests that the Warsaw sample EPW file carries no missing-value markers, so every hourly record reports a measured value for all five decoded fields and the decoder produces no false positives on good data.
        /// </summary>
        [Fact]
        public void MissingValue_WarsawNone()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));

            EPWFile? ePWFile = Modify.Read(path);
            Assert.NotNull(ePWFile);

            Assert.NotNull(ePWFile.DataRecords);
            Assert.Equal(8760, ePWFile.DataRecords.Count);

            foreach (DataRecord dataRecord in ePWFile.DataRecords)
            {
                Assert.NotNull(dataRecord.GlobalHorizontalRadiationValue());
                Assert.NotNull(dataRecord.DirectNormalRadiationValue());
                Assert.NotNull(dataRecord.DiffuseHorizontalRadiationValue());
                Assert.NotNull(dataRecord.SnowDepthValue());
                Assert.NotNull(dataRecord.AlbedoValue());
            }
        }
    }
}
