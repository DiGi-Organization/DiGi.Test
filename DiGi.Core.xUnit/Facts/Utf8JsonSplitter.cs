using DiGi.Core.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Utf8JsonSplitter{TSerializableObject}"/> emits every object exactly once, in source order, and that each batch's payload is byte-identical to what <see cref="Convert.ToSystem_String{T}(IEnumerable{T})"/> produces for the same batch.
        /// <para>The splitter replaces a size-then-serialize pair on the bulk POST path, so its payload must be indistinguishable from the one it replaces.</para>
        /// </summary>
        [Fact]
        public void Utf8JsonSplitter_MatchesStringSerialization()
        {
            List<Address> addresses = Addresses(50);

            // A budget well below the total forces several batch boundaries.
            long maxSize = SerializedSize(addresses) / 7;

            List<Address> addresses_Emitted = [];

            Utf8JsonSplitter<Address> utf8JsonSplitter = new(addresses);

            Utf8JsonBatch<Address>? utf8JsonBatch;
            int batchCount = 0;

            while ((utf8JsonBatch = utf8JsonSplitter.Next(maxSize)) is not null)
            {
                batchCount++;

                Assert.NotEmpty(utf8JsonBatch.SerializableObjects);

                string? json_Expected = Convert.ToSystem_String(utf8JsonBatch.SerializableObjects);
                string json_Actual = Encoding.UTF8.GetString(utf8JsonBatch.Utf8Json);

                Assert.Equal(json_Expected, json_Actual);

                addresses_Emitted.AddRange(utf8JsonBatch.SerializableObjects);
            }

            Assert.True(batchCount > 1, string.Format(CultureInfo.CurrentCulture, "Expected the budget to force multiple batches but got {0}.", batchCount));

            Assert.Equal(addresses.Count, addresses_Emitted.Count);

            for (int i = 0; i < addresses.Count; i++)
            {
                Assert.Same(addresses[i], addresses_Emitted[i]);
            }

            Assert.Equal(Convert.ToSystem_String(addresses), Convert.ToSystem_String(addresses_Emitted));
        }

        /// <summary>
        /// Verifies that <see cref="Utf8JsonSplitter{TSerializableObject}"/> emits an object larger than the batch budget on its own rather than dropping it or looping forever.
        /// <para>The batch budget is a soft target; starving on an oversized item would silently lose data on the import path.</para>
        /// </summary>
        [Fact]
        public void Utf8JsonSplitter_OversizedItem()
        {
            List<Address> addresses = Addresses(3);

            Utf8JsonSplitter<Address> utf8JsonSplitter = new(addresses);

            int count = 0;

            Utf8JsonBatch<Address>? utf8JsonBatch;
            while ((utf8JsonBatch = utf8JsonSplitter.Next(1)) is not null)
            {
                Assert.Single(utf8JsonBatch.SerializableObjects);
                count++;
            }

            Assert.Equal(addresses.Count, count);
        }

        /// <summary>
        /// Verifies that <see cref="Utf8JsonSplitter{TSerializableObject}"/> reports no batches for a null or empty source.
        /// </summary>
        [Fact]
        public void Utf8JsonSplitter_Empty()
        {
            Assert.Null(new Utf8JsonSplitter<Address>(null).Next(1024));
            Assert.Null(new Utf8JsonSplitter<Address>([]).Next(1024));
        }

        private static List<Address> Addresses(int count)
        {
            List<Address> result = [];
            for (int i = 0; i < count; i++)
            {
                result.Add(new Address(string.Format(CultureInfo.CurrentCulture, "Street {0}", i), string.Format(CultureInfo.CurrentCulture, "City {0}", i), string.Format(CultureInfo.CurrentCulture, "{0:00}-000", i % 100), Enums.CountryCode.PL));
            }

            return result;
        }

        private static long SerializedSize(IEnumerable<Address> addresses)
        {
            byte[]? bytes = Convert.ToSystem_Bytes(addresses);

            Assert.NotNull(bytes);

            return bytes.Length;
        }
    }
}
