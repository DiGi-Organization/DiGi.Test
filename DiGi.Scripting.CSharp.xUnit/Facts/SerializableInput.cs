using DiGi.Core;
using DiGi.Scripting.Classes;

namespace DiGi.Scripting.CSharp.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests whether the <see cref="SerializableInput"/> class can be correctly serialized to a string and deserialized back into an object while maintaining data integrity.
        /// </summary>
        [Fact]
        public void SerializableInput()
        {
            SerializableInput serializableInput_1 = new("AAA", 7);

            string? json_1 = serializableInput_1.ToSystem_String();
            Assert.NotNull(json_1);

            SerializableInput? serializableInput_2 = DiGi.Core.Convert.ToDiGi<SerializableInput>(json_1)?.FirstOrDefault();
            Assert.NotNull(serializableInput_2);

            string? json_2 = serializableInput_2.ToSystem_String();

            Assert.Equal(json_1, json_2);
        }
    }
}