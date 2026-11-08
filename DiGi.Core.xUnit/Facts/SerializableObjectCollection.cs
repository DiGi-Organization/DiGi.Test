using System.Text.Json.Nodes;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the serialization and deserialization process of the <see cref="Core.Classes.SerializableObjectCollection"/> class to ensure that data integrity is maintained throughout the cycle.
        /// </summary>
        [Fact]
        public void SerializableObjectCollection()
        {
            int count = 4;

            Core.Classes.SerializableObjectCollection serializableObjectCollection_1 = new();
            for (int i = 0; i < count; i++)
            {
                serializableObjectCollection_1.Add(new Core.Classes.Size(i, 1000 + i));
            }

            string? jsonString_1 = serializableObjectCollection_1.ToJsonObject()?.ToJsonString();
            Assert.NotNull(jsonString_1);

            JsonObject? jsonObject = JsonNode.Parse(jsonString_1) as JsonObject;
            Assert.NotNull(jsonObject);

            Core.Classes.SerializableObjectCollection? serializableObjectCollection_2 = Create.SerializableObject<Core.Classes.SerializableObjectCollection>(jsonObject);
            Assert.NotNull(serializableObjectCollection_2);

            for (int i = 0; i < count; i++)
            {
                Core.Classes.Size? size = serializableObjectCollection_2[i] as Core.Classes.Size;
                Assert.NotNull(size);
                Assert.Equal(i, size.Width);
                Assert.Equal(1000 + i, size.Height);
            }

            string? jsonString_2 = serializableObjectCollection_2.ToJsonObject()?.ToJsonString();
            Assert.NotNull(jsonString_2);

            Assert.Equal(jsonString_1, jsonString_2);

            Query.SerializationCheck(serializableObjectCollection_1);
        }
    }
}