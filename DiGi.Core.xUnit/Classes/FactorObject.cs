using DiGi.Core.Classes;
using DiGi.Core.Interfaces;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DiGi.Core.xUnit
{
    /// <summary>Represents a serializable object that holds <see cref="Core.Classes.Factor"/> members, used to verify that a nested factor survives a round trip.</summary>
    public class FactorObject : SerializableObject, ISerializableObject
    {
        [JsonInclude, JsonPropertyName(nameof(Factor))]
        private readonly Factor factor = new();

        [JsonInclude, JsonPropertyName(nameof(Factor_Undefined))]
        private readonly Factor factor_Undefined = new();

        /// <summary>Initializes a new instance of the <see cref="FactorObject"/> class.</summary>
        /// <param name="factor">The defined factor.</param>
        /// <param name="factor_Undefined">The factor expected to stay <see cref="double.NaN"/>.</param>
        public FactorObject(Factor factor, Factor factor_Undefined)
        {
            this.factor = factor;
            this.factor_Undefined = factor_Undefined;
        }

        /// <summary>Initializes a new instance of the <see cref="FactorObject"/> class by copying another <see cref="FactorObject"/>.</summary>
        /// <param name="factorObject">The <see cref="FactorObject"/> to copy from.</param>
        public FactorObject(FactorObject? factorObject)
            : base(factorObject)
        {
            if (factorObject is not null)
            {
                factor = factorObject.factor;
                factor_Undefined = factorObject.factor_Undefined;
            }
        }

        /// <summary>Initializes a new instance of the <see cref="FactorObject"/> class from a <see cref="JsonObject"/>.</summary>
        /// <param name="jsonObject">The <see cref="JsonObject"/> used to initialize the current instance.</param>
        public FactorObject(JsonObject? jsonObject)
            : base(jsonObject)
        {
        }

        /// <summary>Gets the defined factor.</summary>
        [JsonIgnore]
        public Factor Factor => factor;

        /// <summary>Gets the factor expected to stay <see cref="double.NaN"/>.</summary>
        [JsonIgnore]
        public Factor Factor_Undefined => factor_Undefined;
    }
}
