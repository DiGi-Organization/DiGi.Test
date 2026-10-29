using DiGi.GIS.PostgreSQL.Classes;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that an address point count result carries every field through the constructor, a JSON round trip and the copy constructor, for a county part and for the whole table (a null county), and passes <c>SerializationCheck</c>. Every member is populated with a distinct non-zero value so a member dropped by the JSON constructor or the copy constructor fails here. Reference: DiGi.GIS.PostgreSQL#110, DiGi.GIS.PostgreSQL#119.
        /// </summary>
        [Fact]
        public void AddressPointCountResult_Serialization()
        {
            AddressPointCountResult addressPointCountResult = new(75120, 7_320_070, 564_585, 729_527, 8_614_182, 5_812_344, 14_903_211);

            Assert.Equal(75120, addressPointCountResult.CountyId);
            Assert.Equal(7_320_070L, addressPointCountResult.Inside);
            Assert.Equal(564_585L, addressPointCountResult.Nearest);
            Assert.Equal(729_527L, addressPointCountResult.Unmatched);
            Assert.Equal(8_614_182L, addressPointCountResult.Total);
            Assert.Equal(5_812_344L, addressPointCountResult.Buildings);
            Assert.Equal(14_903_211L, addressPointCountResult.Buildings2D);

            string? json = Core.Convert.ToSystem_String(addressPointCountResult);
            Assert.NotNull(json);

            AddressPointCountResult? addressPointCountResult_Json = Core.Convert.ToDiGi<AddressPointCountResult>(json)?.FirstOrDefault();
            Assert.NotNull(addressPointCountResult_Json);
            Assert.Equal(75120, addressPointCountResult_Json.CountyId);
            Assert.Equal(7_320_070L, addressPointCountResult_Json.Inside);
            Assert.Equal(564_585L, addressPointCountResult_Json.Nearest);
            Assert.Equal(729_527L, addressPointCountResult_Json.Unmatched);
            Assert.Equal(8_614_182L, addressPointCountResult_Json.Total);
            Assert.Equal(5_812_344L, addressPointCountResult_Json.Buildings);
            Assert.Equal(14_903_211L, addressPointCountResult_Json.Buildings2D);

            AddressPointCountResult addressPointCountResult_Copy = new(addressPointCountResult);
            Assert.Equal(75120, addressPointCountResult_Copy.CountyId);
            Assert.Equal(7_320_070L, addressPointCountResult_Copy.Inside);
            Assert.Equal(564_585L, addressPointCountResult_Copy.Nearest);
            Assert.Equal(729_527L, addressPointCountResult_Copy.Unmatched);
            Assert.Equal(8_614_182L, addressPointCountResult_Copy.Total);
            Assert.Equal(5_812_344L, addressPointCountResult_Copy.Buildings);
            Assert.Equal(14_903_211L, addressPointCountResult_Copy.Buildings2D);

            Core.xUnit.Query.SerializationCheck(addressPointCountResult);

            AddressPointCountResult addressPointCountResult_Table = new(null, 1, 2, 3, 7, 2, 11);
            Assert.Null(addressPointCountResult_Table.CountyId);
            Assert.Null(new AddressPointCountResult(addressPointCountResult_Table).CountyId);
            Assert.Equal(11L, new AddressPointCountResult(addressPointCountResult_Table).Buildings2D);
            Core.xUnit.Query.SerializationCheck(addressPointCountResult_Table);
        }
    }
}
