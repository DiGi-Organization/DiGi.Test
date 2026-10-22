using DiGi.GIS.PostgreSQL.Classes;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that an address point import result carries every field through the constructor, a JSON round trip and the copy constructor, and passes <c>SerializationCheck</c>. Reference: DiGi.GIS.PostgreSQL#103.
        /// </summary>
        [Fact]
        public void AddressPointUpdateResult_Serialization()
        {
            AddressPointUpdateResult addressPointUpdateResult = new([97358, 97360], 1200, 300, 45, 3, 2, 1_545);

            Assert.Equal(1200, addressPointUpdateResult.Inside);
            Assert.Equal(300, addressPointUpdateResult.Nearest);
            Assert.Equal(45, addressPointUpdateResult.Unmatched);
            Assert.Equal(3, addressPointUpdateResult.Outside);
            Assert.Equal(2, addressPointUpdateResult.Skipped);
            Assert.Equal(1_545, addressPointUpdateResult.Written);
            Assert.Equal([97358, 97360], addressPointUpdateResult.CountyIds);

            string? json = Core.Convert.ToSystem_String(addressPointUpdateResult);
            Assert.NotNull(json);

            AddressPointUpdateResult? addressPointUpdateResult_Json = Core.Convert.ToDiGi<AddressPointUpdateResult>(json)?.FirstOrDefault();
            Assert.NotNull(addressPointUpdateResult_Json);
            Assert.Equal(1200, addressPointUpdateResult_Json.Inside);
            Assert.Equal(300, addressPointUpdateResult_Json.Nearest);
            Assert.Equal(45, addressPointUpdateResult_Json.Unmatched);
            Assert.Equal(3, addressPointUpdateResult_Json.Outside);
            Assert.Equal(2, addressPointUpdateResult_Json.Skipped);
            Assert.Equal(1_545, addressPointUpdateResult_Json.Written);
            Assert.Equal([97358, 97360], addressPointUpdateResult_Json.CountyIds);

            AddressPointUpdateResult addressPointUpdateResult_Copy = new(addressPointUpdateResult);
            Assert.Equal(1200, addressPointUpdateResult_Copy.Inside);
            Assert.Equal(300, addressPointUpdateResult_Copy.Nearest);
            Assert.Equal(45, addressPointUpdateResult_Copy.Unmatched);
            Assert.Equal(3, addressPointUpdateResult_Copy.Outside);
            Assert.Equal(2, addressPointUpdateResult_Copy.Skipped);
            Assert.Equal(1_545, addressPointUpdateResult_Copy.Written);
            Assert.Equal([97358, 97360], addressPointUpdateResult_Copy.CountyIds);

            Core.xUnit.Query.SerializationCheck(addressPointUpdateResult);
            Core.xUnit.Query.SerializationCheck(new AddressPointUpdateResult(null, 0, 0, 0, 0, 0, 0));
        }
    }
}
