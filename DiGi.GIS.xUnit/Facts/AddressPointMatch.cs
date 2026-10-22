using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Enums;
using System;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that an address point match survives a JSON round trip and a copy, both for a matched point and for an unmatched one with every nullable member null.
        /// <para>The copy is asserted directly because SerializationCheck never runs the copy constructor. Reference: DiGi.GIS#10.</para>
        /// </summary>
        [Fact]
        public void AddressPointMatch()
        {
            Guid guid = new("4a701343-0b8e-4c5e-bfde-77dcf9b04e68");
            Classes.AddressPoint addressPoint = new(new Point2D(301858.38, 374887.73), "Słowiański", "20295", "7", "Legnica", "0954047", "59-220", "fad42bf4-a0e8-4fe6-85d7-a3b043b185ca", null);

            Classes.AddressPointMatch addressPointMatch = new(addressPoint, AddressPointMatchType.Nearest, "2A522112-FC32-62E0-E053-CA2BA8C0E403", guid, 1.25);

            Assert.Equal(AddressPointMatchType.Nearest, addressPointMatch.AddressPointMatchType);
            Assert.Equal("2A522112-FC32-62E0-E053-CA2BA8C0E403", addressPointMatch.Reference);
            Assert.Equal(guid, addressPointMatch.Guid);
            Assert.Equal(1.25, addressPointMatch.Distance);
            Assert.Equal("fad42bf4-a0e8-4fe6-85d7-a3b043b185ca", addressPointMatch.AddressPoint?.ReferenceId);

            string? json = Core.Convert.ToSystem_String(addressPointMatch);
            Assert.False(string.IsNullOrWhiteSpace(json));

            Classes.AddressPointMatch? addressPointMatch_Json = Core.Convert.ToDiGi<Classes.AddressPointMatch>(json)?.FirstOrDefault();
            Assert.NotNull(addressPointMatch_Json);
            Assert.Equal(addressPointMatch.AddressPointMatchType, addressPointMatch_Json.AddressPointMatchType);
            Assert.Equal(addressPointMatch.Reference, addressPointMatch_Json.Reference);
            Assert.Equal(addressPointMatch.Guid, addressPointMatch_Json.Guid);
            Assert.Equal(addressPointMatch.Distance, addressPointMatch_Json.Distance);
            Assert.Equal(addressPointMatch.AddressPoint?.HouseNumber, addressPointMatch_Json.AddressPoint?.HouseNumber);
            Assert.Equal(addressPointMatch.AddressPoint?.Location?.X, addressPointMatch_Json.AddressPoint?.Location?.X);

            Core.xUnit.Query.SerializationCheck(addressPointMatch);

            Classes.AddressPointMatch addressPointMatch_Copy = new(addressPointMatch);
            Assert.Equal(addressPointMatch.AddressPointMatchType, addressPointMatch_Copy.AddressPointMatchType);
            Assert.Equal(addressPointMatch.Reference, addressPointMatch_Copy.Reference);
            Assert.Equal(addressPointMatch.Guid, addressPointMatch_Copy.Guid);
            Assert.Equal(addressPointMatch.Distance, addressPointMatch_Copy.Distance);
            Assert.Equal(addressPointMatch.AddressPoint?.ReferenceId, addressPointMatch_Copy.AddressPoint?.ReferenceId);

            Classes.AddressPointMatch addressPointMatch_Unmatched = new(addressPoint, AddressPointMatchType.Unmatched, null, null, null);

            Assert.Null(addressPointMatch_Unmatched.Reference);
            Assert.Null(addressPointMatch_Unmatched.Guid);
            Assert.Null(addressPointMatch_Unmatched.Distance);

            Core.xUnit.Query.SerializationCheck(addressPointMatch_Unmatched);

            Classes.AddressPointMatch addressPointMatch_UnmatchedCopy = new(addressPointMatch_Unmatched);
            Assert.Equal(AddressPointMatchType.Unmatched, addressPointMatch_UnmatchedCopy.AddressPointMatchType);
            Assert.Null(addressPointMatch_UnmatchedCopy.Reference);
            Assert.Null(addressPointMatch_UnmatchedCopy.Guid);
            Assert.Null(addressPointMatch_UnmatchedCopy.Distance);
        }
    }
}
