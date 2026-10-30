using System.Linq;
using System.Text.Json.Nodes;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a <see cref="Core.Classes.Factor"/> keeps its value through a JSON round trip, both standalone and as a member of a containing object.
        /// <para>The JSON constructor once delegated to <c>Modify.FromJsonObject(this, …)</c>, which populates a boxed copy of the readonly struct, so every deserialized factor came back <see cref="double.NaN"/>.</para>
        /// </summary>
        [Fact]
        public void Factor()
        {
            double[] values = [0, 0.5, 1, double.NaN];
            foreach (double value in values)
            {
                Core.Classes.Factor factor = new(value);

                JsonObject? jsonObject = factor.ToJsonObject();
                Assert.NotNull(jsonObject);

                Core.Classes.Factor factor_JsonObject = new(jsonObject);
                Assert.Equal(value, (double)factor_JsonObject);

                Core.Classes.Factor factor_String = new(JsonNode.Parse(jsonObject.ToJsonString()) as JsonObject);
                Assert.Equal(value, (double)factor_String);

                Query.SerializationCheck(factor);
            }

            Core.Classes.Factor factor_Clamped = new(JsonNode.Parse("{\"Value\":1.5}") as JsonObject);
            Assert.Equal(1, (double)factor_Clamped);

            Core.Classes.Factor factor_Missing = new(new JsonObject());
            Assert.True(double.IsNaN(factor_Missing));

            FactorObject factorObject = new(0.25, new Core.Classes.Factor());

            FactorObject? factorObject_String = Convert.ToDiGi<FactorObject>(Convert.ToSystem_String(factorObject))?.FirstOrDefault();
            Assert.NotNull(factorObject_String);
            Assert.Equal(0.25, (double)factorObject_String.Factor);
            Assert.True(double.IsNaN(factorObject_String.Factor_Undefined));

            FactorObject factorObject_Copy = new(factorObject);
            Assert.Equal(0.25, (double)factorObject_Copy.Factor);

            Query.SerializationCheck(factorObject);
        }
    }
}
