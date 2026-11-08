using DiGi.Core.Classes;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DiGi.Core.xUnit
{
    /// <summary>Represents a test object whose unique identifier is a plain string rather than a GUID, inheriting from <see cref="UniqueObject"/>. Used to exercise the <see cref="UniqueIdReference"/> path, which cannot be covered by <see cref="TestObject"/> because that object derives from <see cref="GuidObject"/>.</summary>
    public class TestUniqueIdObject : UniqueObject
    {
        [JsonInclude, JsonPropertyName("Name")]
        private readonly string? name;

        /// <summary>Initializes a new instance of the <see cref="TestUniqueIdObject"/> class.</summary>
        /// <param name="name">The name of the test object, which also drives its unique identifier.</param>
        public TestUniqueIdObject(string? name)
            : base()
        {
            this.name = name;
        }

        /// <summary>Initializes a new instance of the <see cref="TestUniqueIdObject"/> class by copying an existing instance.</summary>
        /// <param name="testUniqueIdObject">The existing instance to copy.</param>
        public TestUniqueIdObject(TestUniqueIdObject? testUniqueIdObject)
            : base(testUniqueIdObject)
        {
            if (testUniqueIdObject is not null)
            {
                name = testUniqueIdObject.name;
            }
        }

        /// <summary>Initializes a new instance of the <see cref="TestUniqueIdObject"/> class.</summary>
        /// <param name="jsonObject">The <see cref="JsonObject"/> used to initialize the object.</param>
        public TestUniqueIdObject(JsonObject? jsonObject)
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

        /// <summary>Gets the unique identifier of the test object, derived from its name so that it stays stable across serialization.</summary>
        public override string? UniqueId
        {
            get
            {
                return string.Format(CultureInfo.CurrentCulture, "TestUniqueIdObject_{0}", name);
            }
        }
    }
}