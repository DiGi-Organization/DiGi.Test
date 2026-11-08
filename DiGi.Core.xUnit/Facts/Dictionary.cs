using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the serialization and deserialization of dictionary objects, verifying that they can be correctly converted between system strings and DiGi types.
        /// </summary>
        [Fact]
        public void Dictionary()
        {
            Dictionary<string, int> dictionary = new()
            {
                { "AAA", 1 },
                { "BBB", 2 }
            };

#pragma warning disable CA1869 // the fact serializes once, with options of its own
            JsonSerializerOptions jsonSerializerOptions = new() { WriteIndented = true };
#pragma warning restore CA1869

            string json = JsonSerializer.Serialize(dictionary, jsonSerializerOptions);

            Assert.NotNull(json);

            DictionaryObject dictionaryObject_1 = new();
            dictionaryObject_1.Add("AAAA");
            dictionaryObject_1.Add("BBBB");

            TestObject testObject = new("AAAA");

            string? json_TestObject_1 = Convert.ToSystem_String(testObject);
            Assert.NotNull(json_TestObject_1);

            TestObject? testObject_2 = Convert.ToDiGi<TestObject>(json_TestObject_1)?.FirstOrDefault();
            Assert.NotNull(testObject_2);

            string? json_TestObject_2 = Convert.ToSystem_String(testObject_2);
            Assert.NotNull(json_TestObject_2);

            string? json_1 = Convert.ToSystem_String(dictionaryObject_1);
            Assert.NotNull(json_1);

            DictionaryObject? dictionaryObject_2 = Convert.ToDiGi<DictionaryObject>(json_1)?.FirstOrDefault();
            Assert.NotNull(dictionaryObject_2);

            string? json_2 = Convert.ToSystem_String(dictionaryObject_2);
            Assert.NotNull(json_2);

            Assert.Equal(json_1, json_2);
        }
    }
}