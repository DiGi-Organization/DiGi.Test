using DiGi.Core.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DiGi.Core.xUnit
{
    /// <summary>Represents a test object used for verifying the serialization and deserialization of various data types, inheriting from <see cref="GuidObject"/>.</summary>
    public class TestObject : GuidObject
    {
        [JsonInclude, JsonPropertyName("Name")]
        private string? name;

        [JsonInclude, JsonPropertyName("UniqueReference")]
        private UniqueReference? uniqueReference;

        [JsonInclude, JsonPropertyName("Range")]
        private Range<double>? range;

        [JsonInclude, JsonPropertyName("DateTime")]
        private DateTime dateTime = DateTime.MinValue;

        [JsonInclude, JsonPropertyName("Color")]
        private System.Drawing.Color color = System.Drawing.Color.Empty;

        [JsonInclude, JsonPropertyName("IndexedDoubles")]
        private IndexedObjects<double>? indexedDoubles = null;

        [JsonInclude, JsonPropertyName("TestEnum")]
        private TestEnum? testEnum = null;

        [JsonInclude, JsonPropertyName("KeyValuePair")]
        private KeyValuePair<int, string>? KeyValuePair = new KeyValuePair<int, string>(1, "AAA");

        /// <summary>A dictionary containing integer keys and string values used for testing purposes.</summary>
        [JsonInclude, JsonPropertyName("Dictionary")]
        public Dictionary<int, string>? Dictionary = [];

        /// <summary>A sorted dictionary containing integer keys and string values used for testing purposes.</summary>
        [JsonInclude, JsonPropertyName("SortedDictionary")]
        public SortedDictionary<int, string>? SortedDictionary = [];

        /// <summary>Initializes a new instance of the <see cref="TestObject"/> class with randomized default values.</summary>
        public TestObject()
        {
            Random random = new();

            name = Guid.NewGuid().ToString();
            uniqueReference = new GuidReference(new TypeReference(typeof(TestObject)), Guid.NewGuid());
            range = new Range<double>(random.NextDouble(), random.NextDouble());
            dateTime = DateTime.Now;

            System.Drawing.Color[] colors = typeof(System.Drawing.Color)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(System.Drawing.Color))
                .Select(p => (System.Drawing.Color)p.GetValue(null)!)
                .ToArray();

            color = colors[random.Next(colors.Length)];

            indexedDoubles = new IndexedObjects<double>
            {
                { 0, random.NextDouble() },
                { 1, random.NextDouble() }
            };

            testEnum = (TestEnum)random.Next(Enum.GetValues<TestEnum>().Length);

            KeyValuePair = new KeyValuePair<int, string>(random.Next(), Guid.NewGuid().ToString());

            Dictionary[random.Next()] = Guid.NewGuid().ToString();

            SortedDictionary[random.Next()] = Guid.NewGuid().ToString();
        }

        /// <summary>Initializes a new instance of the <see cref="TestObject"/> class.</summary>
        /// <param name="name">The name of the test object.</param>
        public TestObject(string? name)
            : base()
        {
            this.name = name;
            uniqueReference = new GuidReference(this);
        }

        /// <summary>Initializes a new instance of the <see cref="TestObject"/> class.</summary>
        /// <param name="name">The name of the test object.</param>
        /// <param name="min">The minimum value for the range.</param>
        /// <param name="max">The maximum value for the range.</param>
        public TestObject(string? name, double min, double max)
            : base()
        {
            this.name = name;
            uniqueReference = new GuidReference(this);
            range = new Range<double>(min, max);
            dateTime = DateTime.Now;
            color = System.Drawing.Color.Blue;

            indexedDoubles = new IndexedObjects<double>
            {
                { 0, 10 },
                { 1, 20 }
            };
        }

        /// <summary>Initializes a new instance of the <see cref="TestObject"/> class.</summary>
        /// <param name="guid">The unique identifier for the test object.</param>
        /// <param name="name">The name of the test object.</param>
        public TestObject(Guid guid, string? name)
            : base(guid)
        {
            this.name = name;
            uniqueReference = new GuidReference(this);
        }

        /// <summary>Initializes a new instance of the <see cref="TestObject"/> class.</summary>
        /// <param name="jsonObject">The <see cref="JsonObject"/> used to initialize the object.</param>
        public TestObject(JsonObject? jsonObject)
            : base(jsonObject)
        {
        }

        /// <summary>Gets the name of the test object.</summary>
        [JsonIgnore]
        public string? Name
        {
            get
            {
                return name;
            }
        }

        /// <summary>Gets the unique reference associated with the object.</summary>
        [JsonIgnore]
        public UniqueReference? UniqueReference
        {
            get
            {
                return uniqueReference;
            }
        }

        /// <summary>Gets or sets the <see cref="TestEnum"/> value.</summary>
        [JsonIgnore]
        public TestEnum? TestEnum
        {
            get
            {
                return testEnum;
            }

            set
            {
                testEnum = value;
            }
        }
    }
}