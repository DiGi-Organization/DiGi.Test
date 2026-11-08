using DiGi.Core.Classes;
using System;
using System.Text.Json.Nodes;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Create.SerializationMethodCollection(Type)"/> still places ordered members
        /// (by ascending <see cref="System.Text.Json.Serialization.JsonPropertyOrderAttribute"/> value) before
        /// unordered members, after replacing the O(n^2) `tuples.Insert(0, ...)` loop with a single
        /// `InsertRange(0, ...)` call.
        /// </summary>
        [Fact]
        public void SerializationMethodCollection_OrdersMembersCorrectly()
        {
            Classes.TestOrderedSerializationObject testObject = new();

            SerializationMethodCollection? serializationMethodCollection = typeof(Classes.TestOrderedSerializationObject).SerializationMethodCollection();
            Assert.NotNull(serializationMethodCollection);

            JsonObject? jsonObject = serializationMethodCollection.Create(testObject);
            Assert.NotNull(jsonObject);

            string json = jsonObject.ToJsonString();

            int index_Alpha = json.IndexOf("\"Alpha\"", StringComparison.CurrentCulture);
            int index_Beta = json.IndexOf("\"Beta\"", StringComparison.CurrentCulture);
            int index_Zulu = json.IndexOf("\"Zulu\"", StringComparison.CurrentCulture);
            int index_Yankee = json.IndexOf("\"Yankee\"", StringComparison.CurrentCulture);

            Assert.True(index_Alpha >= 0 && index_Beta >= 0 && index_Zulu >= 0 && index_Yankee >= 0);

            // Ordered members (Alpha=1, Beta=2) come first, in ascending order...
            Assert.True(index_Alpha < index_Beta);

            // ...and both come before the unordered members.
            Assert.True(index_Beta < index_Zulu);
            Assert.True(index_Beta < index_Yankee);
        }

        /// <summary>
        /// Tests that <see cref="SerializationMethodCollection.Create(Interfaces.ISerializableObject)"/> writes the runtime type of the instance
        /// into the type property, both when the collection matches the exact runtime type (cached full type name fast path)
        /// and when a base-type collection is used for a derived instance (reflection fallback).
        /// </summary>
        [Fact]
        public void SerializationMethodCollection_FullTypeName()
        {
            TestObject testObject = new();

            string? fullTypeName_Expected = Core.Query.FullTypeName(typeof(TestObject));
            Assert.NotNull(fullTypeName_Expected);

            // Exact runtime type: the cached full type name is used.
            SerializationMethodCollection? serializationMethodCollection = typeof(TestObject).SerializationMethodCollection();
            Assert.NotNull(serializationMethodCollection);

            JsonObject? jsonObject = serializationMethodCollection.Create(testObject);
            Assert.NotNull(jsonObject);
            Assert.Equal(fullTypeName_Expected, jsonObject[Constants.Serialization.PropertyName.Type]?.GetValue<string>());

            // Base-type collection with derived instance: the runtime type must still be written.
            SerializationMethodCollection? serializationMethodCollection_Base = typeof(GuidObject).SerializationMethodCollection();
            Assert.NotNull(serializationMethodCollection_Base);

            JsonObject? jsonObject_Base = serializationMethodCollection_Base.Create(testObject);
            Assert.NotNull(jsonObject_Base);
            Assert.Equal(fullTypeName_Expected, jsonObject_Base[Constants.Serialization.PropertyName.Type]?.GetValue<string>());
        }
    }
}