using DiGi.Core.Classes;
using System;
using System.Globalization;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a unique reference survives the encode/decode pair used to name entries inside a storage file.
        /// <para>Query.Encode is url-encoded ToString and becomes the ZIP entry name; Query.Decode parses it back.
        /// A mismatch here does not throw - the entry is simply never found and the archive reads back empty - so
        /// this is asserted directly.</para>
        /// </summary>
        [Fact]
        public void WrapperMetadataReferences_EncodeDecode_RoundTrip()
        {
            TypeReference typeReference = new(typeof(TestObject));

            UniqueReference[] uniqueReferences =
            [
                new GuidReference(typeReference, Guid.NewGuid()),
                new UniqueIdReference(typeReference, "BLD-001"),
                new UniqueIdReference(typeReference, "A::B\"C[D]"),
            ];

            foreach (UniqueReference uniqueReference in uniqueReferences)
            {
                string? value_Encoded = IO.File.Query.Encode(uniqueReference);
                Assert.False(string.IsNullOrWhiteSpace(value_Encoded));

                UniqueReference? uniqueReference_Decoded = IO.File.Query.Decode(value_Encoded);
                Assert.NotNull(uniqueReference_Decoded);

                Assert.Equal(uniqueReference.GetType(), uniqueReference_Decoded.GetType());
                Assert.Equal(uniqueReference.ToString(), uniqueReference_Decoded.ToString());
                Assert.True(uniqueReference.Equals(uniqueReference_Decoded));
            }
        }

        /// <summary>
        /// Tests that an entry name stays a plausible length once encoded, since it is used as a ZIP entry name under
        /// a directory prefix. The discriminator makes strings longer than the previous format did.
        /// </summary>
        [Fact]
        public void WrapperMetadataReferences_EncodedLength_IsReasonable()
        {
            GuidReference guidReference = new(new TypeReference(typeof(TestObject)), Guid.NewGuid());

            string? value_Encoded = IO.File.Query.Encode(guidReference);
            Assert.NotNull(value_Encoded);

            Assert.True(
                value_Encoded.Length < 512,
                string.Format(CultureInfo.CurrentCulture, "Encoded entry name is {0} characters: {1}", value_Encoded.Length, value_Encoded));
        }
    }
}