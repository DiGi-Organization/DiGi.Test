using DiGi.GIS.PostgreSQL.Classes;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="EPWFileLocationResult"/> survives a string round trip and <c>SerializationCheck</c>, and that the copy constructor copies every member (DiGi.GIS.WebAPI#93).
        /// </summary>
        [Fact]
        public void EPWFileLocationResult_Serialization()
        {
            EPWFileLocationResult ePWFileLocationResult = new(17, 637512.25, 486103.75);

            EPWFileLocationResult? ePWFileLocationResult_Json = Core.Convert.ToDiGi<EPWFileLocationResult>(Core.Convert.ToSystem_String(ePWFileLocationResult))?.FirstOrDefault();
            Assert.NotNull(ePWFileLocationResult_Json);
            Assert.Equal(17, ePWFileLocationResult_Json.Id);
            Assert.Equal(637512.25, ePWFileLocationResult_Json.X);
            Assert.Equal(486103.75, ePWFileLocationResult_Json.Y);

            EPWFileLocationResult ePWFileLocationResult_Clone = new(ePWFileLocationResult);
            Assert.Equal(17, ePWFileLocationResult_Clone.Id);
            Assert.Equal(637512.25, ePWFileLocationResult_Clone.X);
            Assert.Equal(486103.75, ePWFileLocationResult_Clone.Y);

            Core.xUnit.Query.SerializationCheck(ePWFileLocationResult);
            Core.xUnit.Query.SerializationCheck(new EPWFileLocationResult(1, -1.5, 0));
        }
    }
}
