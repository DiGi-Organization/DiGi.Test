using DiGi.Core;
using DiGi.Geometry.Planar.Classes;
using System;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that an address point survives a JSON round trip and a copy, with every member populated and with the nullable members null.
        /// <para>The copy is asserted directly because SerializationCheck never runs the copy constructor.</para>
        /// </summary>
        [Fact]
        public void AddressPoint()
        {
            DateTimeOffset dateTimeOffset = new(2024, 5, 17, 10, 30, 0, TimeSpan.FromHours(2));

            Classes.AddressPoint addressPoint = new(new Point2D(301858.38, 374887.73), "Słowiański", "03513", "2/4", "Legnica", "0954047", "59-220", "fad42bf4-a0e8-4fe6-85d7-a3b043b185ca", dateTimeOffset);

            Assert.Equal(301858.38, addressPoint.Location?.X);
            Assert.Equal(374887.73, addressPoint.Location?.Y);
            Assert.Equal("Słowiański", addressPoint.StreetName);
            Assert.Equal("03513", addressPoint.ULIC);
            Assert.Equal("2/4", addressPoint.HouseNumber);
            Assert.Equal("Legnica", addressPoint.LocalityName);
            Assert.Equal("0954047", addressPoint.SIMC);
            Assert.Equal("59-220", addressPoint.PostalCode);
            Assert.Equal("fad42bf4-a0e8-4fe6-85d7-a3b043b185ca", addressPoint.ReferenceId);
            Assert.Equal(dateTimeOffset, addressPoint.Version);

            string? json = Core.Convert.ToSystem_String(addressPoint);
            Assert.False(string.IsNullOrWhiteSpace(json));

            Classes.AddressPoint? addressPoint_Json = Core.Convert.ToDiGi<Classes.AddressPoint>(json)?.FirstOrDefault();
            Assert.NotNull(addressPoint_Json);
            Assert.Equal(addressPoint.StreetName, addressPoint_Json.StreetName);
            Assert.Equal(addressPoint.ULIC, addressPoint_Json.ULIC);
            Assert.Equal(addressPoint.HouseNumber, addressPoint_Json.HouseNumber);
            Assert.Equal(addressPoint.SIMC, addressPoint_Json.SIMC);
            Assert.Equal(addressPoint.Version, addressPoint_Json.Version);
            Assert.Equal(addressPoint.Version?.Offset, addressPoint_Json.Version?.Offset);
            Assert.NotNull(addressPoint_Json.Location);
            Assert.Equal(addressPoint.Location?.X, addressPoint_Json.Location.X);

            Core.xUnit.Query.SerializationCheck(addressPoint);

            Classes.AddressPoint addressPoint_Copy = new(addressPoint);
            Assert.Equal(addressPoint.Location?.X, addressPoint_Copy.Location?.X);
            Assert.Equal(addressPoint.Location?.Y, addressPoint_Copy.Location?.Y);
            Assert.Equal(addressPoint.StreetName, addressPoint_Copy.StreetName);
            Assert.Equal(addressPoint.ULIC, addressPoint_Copy.ULIC);
            Assert.Equal(addressPoint.HouseNumber, addressPoint_Copy.HouseNumber);
            Assert.Equal(addressPoint.LocalityName, addressPoint_Copy.LocalityName);
            Assert.Equal(addressPoint.SIMC, addressPoint_Copy.SIMC);
            Assert.Equal(addressPoint.PostalCode, addressPoint_Copy.PostalCode);
            Assert.Equal(addressPoint.ReferenceId, addressPoint_Copy.ReferenceId);
            Assert.Equal(addressPoint.Version, addressPoint_Copy.Version);

            Classes.AddressPoint addressPoint_Null = new(new Point2D(1, 2), null, null, "6A", "Babin", "0364618", null, null, null);

            Assert.Null(addressPoint_Null.StreetName);
            Assert.Null(addressPoint_Null.ULIC);
            Assert.Null(addressPoint_Null.PostalCode);
            Assert.Null(addressPoint_Null.ReferenceId);
            Assert.Null(addressPoint_Null.Version);

            Core.xUnit.Query.SerializationCheck(addressPoint_Null);

            Classes.AddressPoint addressPoint_NullCopy = new(addressPoint_Null);
            Assert.Null(addressPoint_NullCopy.StreetName);
            Assert.Null(addressPoint_NullCopy.Version);
            Assert.Equal("6A", addressPoint_NullCopy.HouseNumber);
        }
    }
}
