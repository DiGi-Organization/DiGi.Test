using DiGi.Core.Parameter.Classes;
using System;
using System.Linq;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the functionality of the <see cref="ParametrizedObject"/> class, specifically verifying value assignment via both simple keys and external parameter definitions, as well as ensuring serialization round-trip consistency.
        /// </summary>
        [Fact]
        public void ParametrizedObject()
        {
            string? json = null;

            ParametrizedObject parametrizedObject = new();
            parametrizedObject.SetValue("Test", "Some Text", new SetValueSettings() { CheckAccessType = false });

            ExternalParameterDefinition? externalParameterDefinition = Core.Parameter.Create.ExternalParameterDefinition(Guid.NewGuid(), "Test 2", "Test description", Core.Parameter.Enums.ParameterType.Double, typeof(ParametrizedObject), nullable: false);
            Assert.NotNull(externalParameterDefinition);

            parametrizedObject.SetValue(externalParameterDefinition, 20);

            json = Convert.ToSystem_String(parametrizedObject);
            Assert.NotNull(json);

            ParametrizedObject? parametrizedObject_Temp = Convert.ToDiGi<ParametrizedObject>(json)?.FirstOrDefault();
            Assert.NotNull(parametrizedObject_Temp);

            Assert.Equal(json, Convert.ToSystem_String(parametrizedObject_Temp));
        }

        /// <summary>
        /// Pins DiGi-Organization/DiGi.Core#6 for the public API: a <c>Double</c>-typed parameter holding a whole number reloads as an
        /// <c>int</c> raw value on the text leg (Scope-B canonicalization), yet <c>GetValue&lt;double&gt;</c> still returns the expected
        /// <c>double</c>.
        /// </summary>
        [Fact]
        public void ParametrizedObject_DoubleWholeNumberRoundTrip()
        {
            ExternalParameterDefinition? externalParameterDefinition = Core.Parameter.Create.ExternalParameterDefinition(Guid.NewGuid(), "D", "desc", Core.Parameter.Enums.ParameterType.Double, typeof(ParametrizedObject), nullable: false);
            Assert.NotNull(externalParameterDefinition);

            ParametrizedObject source = new();
            Assert.True(source.SetValue(externalParameterDefinition, 20.0));

            string? json = Convert.ToSystem_String(source);
            Assert.NotNull(json);

            ParametrizedObject? reloaded = Convert.ToDiGi<ParametrizedObject>(json)?.FirstOrDefault();
            Assert.NotNull(reloaded);

            DiGi.Core.Parameter.Classes.Parameter? parameter = reloaded.ParameterGroupCollection.SelectMany(g => g).FirstOrDefault();
            Assert.NotNull(parameter);

            // The raw object value canonicalizes to an int for a whole number on the text leg, but the typed API recovers the double.
            Assert.IsType<int>(parameter.Value);
            double? value = parameter.GetValue<double>();
            Assert.Equal(20.0, value);
        }

        /// <summary>
        /// Tests that ParametrizedObject.TryGetValue successfully retrieves a set parameter value and safely returns false on a non-existent parameter without throwing an exception or causing a stack overflow.
        /// </summary>
        [Fact]
        public void ParametrizedObject_TryGetValue_ShouldNotStackOverflow()
        {
            ParametrizedObject parametrizedObject = new();
            parametrizedObject.SetValue("ExistingKey", "Value123");

            bool result_Exists = parametrizedObject.TryGetValue("ExistingKey", out object? value_Exists);
            Assert.True(result_Exists);
            Assert.Equal("Value123", value_Exists);

            bool result_Missing = parametrizedObject.TryGetValue("NonExistentKey", out object? value_Missing);
            Assert.False(result_Missing);
            Assert.Null(value_Missing);
        }

        /// <summary>
        /// Verifies the performance and correctness of cloning a ParametrizedObject.
        /// </summary>
        [Fact]
        public void ParametrizedObject_Clone_Performance()
        {
            ParametrizedObject parametrizedObject = new();
            parametrizedObject.SetValue("StringParam", "Test");
            parametrizedObject.SetValue("IntParam", 42);
            parametrizedObject.SetValue("DoubleParam", 3.14);

            // Warm-up
            ParametrizedObject? clone_WarmUp = parametrizedObject.Clone<ParametrizedObject>();
            Assert.NotNull(clone_WarmUp);

            int count = 10000;
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < count; i++)
            {
                ParametrizedObject? clone = parametrizedObject.Clone<ParametrizedObject>();
                Assert.NotNull(clone);
                Assert.Equal("Test", clone.GetValue("StringParam"));
            }

            stopwatch.Stop();

            // The optimized clone should easily complete 10,000 deep clones of ParametrizedObject in less than 50 milliseconds.
            Assert.True(stopwatch.ElapsedMilliseconds < 50, $"Cloning {count} ParametrizedObject instances took {stopwatch.ElapsedMilliseconds} ms, which exceeds the 50 ms threshold.");
        }

        /// <summary>
        /// Tests retrieving enumeration values from a <see cref="ParametrizedObject"/> using <see cref="ParametrizedObject.GetValue{T}(GetValueSettings?)"/> with <see cref="Enums.CountryCode"/>.
        /// </summary>
        [Fact]
        public void ParametrizedObject_GetValue_Enum()
        {
            ParametrizedObject parametrizedObject = new();

            // Initially, retrieving an unset enum should return null.
            Enums.CountryCode? countryCode_Initial = parametrizedObject.GetValue<Enums.CountryCode?>();
            Assert.Null(countryCode_Initial);

            // Set value via Enum definition key
            bool result_SetEnumKey = parametrizedObject.SetValue(Enums.CountryCode.PL, Enums.CountryCode.PL);
            Assert.True(result_SetEnumKey);

            // Test GetValue<CountryCode>() without key argument
            Enums.CountryCode? countryCode_GetValueNoArg = parametrizedObject.GetValue<Enums.CountryCode>();
            Assert.Equal(Enums.CountryCode.PL, countryCode_GetValueNoArg);

            // Test GetValue<CountryCode>(Enum) with enum member key
            Enums.CountryCode? countryCode_GetValueByEnumKey = parametrizedObject.GetValue<Enums.CountryCode>(Enums.CountryCode.PL);
            Assert.Equal(Enums.CountryCode.PL, countryCode_GetValueByEnumKey);

            // Test GetValue(Enum) returning object
            object? object_GetValueByEnumKey = parametrizedObject.GetValue(Enums.CountryCode.PL);
            Assert.Equal(Enums.CountryCode.PL, object_GetValueByEnumKey);

            // Set another parameter with simple string key and string enum name value
            parametrizedObject.SetValue("CountryParam", "DE");

            // Test GetValue<CountryCode>(string) by string parameter name with TryConvert enabled
            GetValueSettings getValueSettings = new(tryConvert: true, checkAccessType: false);
            Enums.CountryCode? countryCode_GetValueByStringKey = parametrizedObject.GetValue<Enums.CountryCode>("CountryParam", getValueSettings);
            Assert.Equal(Enums.CountryCode.DE, countryCode_GetValueByStringKey);
        }

        /// <summary>
        /// Tests the in-memory JSON round trip - the <see cref="DiGi.Core.Classes.SerializableObject.Clone"/> path every type derived from <see cref="ParametrizedObject"/> takes - of a <see cref="Guid"/> parameter: its node has kind <c>String</c> but holds a boxed <see cref="Guid"/>, and <see cref="DiGi.Core.Query.Value(System.Text.Json.Nodes.JsonNode, Type)"/> reads it the way the text leg does (DiGi-Organization/DiGi.Core#14).
        /// </summary>
        [Fact]
        public void ParametrizedObject_JsonObjectRoundTrip_GuidParameter()
        {
            Guid guid = Guid.NewGuid();

            ParametrizedObject source = new();
            Assert.True(source.SetValue("GuidParam", guid, new SetValueSettings() { CheckAccessType = false }));

            ParametrizedObject? copy = Create.SerializableObject<ParametrizedObject>(source.ToJsonObject());
            Assert.NotNull(copy);

            // An untyped parameter reloads its Guid as the string of the JSON text.
            Assert.Equal(guid.ToString(), copy.GetValue("GuidParam"));

            // The in-memory leg and the text leg agree.
            ParametrizedObject? reloaded = Convert.ToDiGi<ParametrizedObject>(Convert.ToSystem_String(source))?.FirstOrDefault();
            Assert.NotNull(reloaded);
            Assert.Equal(Convert.ToSystem_String(reloaded), Convert.ToSystem_String(copy));
        }
    }
}