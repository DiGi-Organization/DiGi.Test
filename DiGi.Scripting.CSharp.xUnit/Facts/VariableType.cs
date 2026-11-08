using DiGi.Core;
using DiGi.Scripting.Classes;

namespace DiGi.Scripting.CSharp.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the serialization and deserialization process of the <see cref="VariableType"/> class to ensure that an object can be converted to a string and back without loss of data integrity.
        /// </summary>
        [Fact]
        public void VariableType()
        {
            VariableType variableType_1 = new("Test", typeof(int));

            string? json_1 = variableType_1.ToSystem_String();
            Assert.NotNull(json_1);

            VariableType? variableType_2 = DiGi.Core.Convert.ToDiGi<VariableType>(json_1)?.FirstOrDefault();
            Assert.NotNull(variableType_2);

            string? json_2 = variableType_2.ToSystem_String();

            Assert.Equal(json_1, json_2);
        }
    }
}