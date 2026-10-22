using DiGi.Core.Classes;
using DiGi.Core.Enums;
using DiGi.Geometry.Planar.Classes;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the lossy conversion of an address point to an address: a street address, a streetless village address, an empty house number and null input.
        /// </summary>
        [Fact]
        public void Convert_ToDiGi_Address()
        {
            Classes.AddressPoint addressPoint_Street = new(new Point2D(1, 2), "Słowiański", "20295", "7", "Legnica", "0954047", "59-220", null, null);
            Address? address = Convert.ToDiGi(addressPoint_Street);
            Assert.NotNull(address);
            Assert.Equal("Słowiański 7", address.Street);
            Assert.Equal("Legnica", address.City);
            Assert.Equal("59-220", address.PostalCode);
            Assert.Equal(CountryCode.PL, address.CountryCode);

            Classes.AddressPoint addressPoint_Village = new(new Point2D(1, 2), null, null, "6A", "Babin", "0364618", null, null, null);
            address = Convert.ToDiGi(addressPoint_Village);
            Assert.NotNull(address);
            Assert.Equal("Babin 6A", address.Street);
            Assert.Equal("Babin", address.City);
            Assert.Null(address.PostalCode);

            Classes.AddressPoint addressPoint_NoNumber = new(new Point2D(1, 2), "Czysta", null, string.Empty, "Legnica", null, null, null, null);
            address = Convert.ToDiGi(addressPoint_NoNumber);
            Assert.NotNull(address);
            Assert.Equal("Czysta", address.Street);

            Assert.Null(Convert.ToDiGi((Classes.AddressPoint?)null));
        }
    }
}
