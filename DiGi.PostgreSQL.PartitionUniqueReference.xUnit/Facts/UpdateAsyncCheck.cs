using DiGi.Core;
using DiGi.Core.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.PostgreSQL.PartitionUniqueReference.xUnit.Classes;

namespace DiGi.PostgreSQL.PartitionUniqueReference.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that an object can be updated, retrieved, and subsequently removed using the partition unique reference PostgreSQL converter.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task UpdateAsyncCheck()
        {
            if (!PostgreSQL.xUnit.Create.IsAvailable(Enums.StorageMethod.PartitionUniqueReference, out ConnectionData? connectionData))
            {
                return;
            }

            TestPartitionUniqueReferencePostgreSQLConverter testPartitionUniqueReferencePostgreSQLConverter = new(connectionData);

            Address address_1 = new("123 Main St", "Anytown", "CA", Core.Enums.CountryCode.Undefined);

            PartitionUniqueReference.Classes.PartitionUniqueReference? partitionUniqueReference_1 = await testPartitionUniqueReferencePostgreSQLConverter.UpdateAsync(address_1);
            Assert.NotNull(partitionUniqueReference_1);

            Address? address_2 = await testPartitionUniqueReferencePostgreSQLConverter.GetSerializableObjectAsync<Address>(partitionUniqueReference_1);
            Assert.NotNull(address_2);

            Assert.Equal(address_1.ToSystem_String(), address_2.ToSystem_String());

            PartitionUniqueReference.Classes.PartitionUniqueReference? partitionUniqueReference_2 = await testPartitionUniqueReferencePostgreSQLConverter.RemoveAsync(partitionUniqueReference_1);

            Assert.Equal(partitionUniqueReference_1.ToSystem_String(), partitionUniqueReference_2.ToSystem_String());
        }
    }
}