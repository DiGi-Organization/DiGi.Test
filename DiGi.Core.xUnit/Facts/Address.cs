using DiGi.Core.Enums;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the serialization of the <see cref="Core.Classes.Address"/> class to ensure it is correctly handled by the serialization check.
        /// </summary>
        [Fact]
        public void Address()
        {
            Core.Classes.Address address = new("123 Main St", "Anytown", "12345", CountryCode.US);

            Query.SerializationCheck(address);
        }
    }
}