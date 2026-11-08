using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the TryConvert query methods, verifying conversion safety and validating that invalid formats do not throw exceptions.
        /// </summary>
        [Fact]
        public void TryConvert()
        {
            // Test standard conversions
            Assert.True(Core.Query.TryConvert("123", out int? int_Result));
            Assert.Equal(123, int_Result);

            Assert.True(Core.Query.TryConvert("123.45", out double? double_Result));
            Assert.Equal(123.45, double_Result);

            Assert.True(Core.Query.TryConvert("true", out bool? bool_Result));
            Assert.True(bool_Result);

            // Test JsonNode conversion safety with invalid JSON (Verifying the fix for Bug 2)
            string string_InvalidJson = "Not a valid JSON string";
            Assert.False(Core.Query.TryConvert_JsonNode(string_InvalidJson, out JsonNode? jsonNode_Result));
            Assert.Null(jsonNode_Result);

            // Test JsonNode conversion with valid JSON
            string string_ValidJson = "{\"name\":\"test\"}";
            Assert.True(Core.Query.TryConvert_JsonNode(string_ValidJson, out JsonNode? jsonNode_ValidResult));
            Assert.NotNull(jsonNode_ValidResult);
            Assert.Equal("test", jsonNode_ValidResult["name"]?.ToString());
        }

        /// <summary>
        /// A <see cref="System.Text.Json.JsonElement"/> of ValueKind Array or Object must not throw through the numeric
        /// scalar converters; the Try* contract promises a failed conversion (false), not an InvalidOperationException.
        /// Red on the unmodified code (the converters called GetString() without a ValueKind == String guard); green after the fix.
        /// </summary>
        [Fact]
        public void TryConvert_JsonElementArrayAndObject_NumericTargets_DoesNotThrow()
        {
            System.Text.Json.JsonElement jsonElement_Array = System.Text.Json.JsonDocument.Parse("[53429, 53412]").RootElement;
            System.Text.Json.JsonElement jsonElement_Object = System.Text.Json.JsonDocument.Parse("{\"a\":1}").RootElement;
            System.Text.Json.JsonElement[] jsonElements = [jsonElement_Array, jsonElement_Object];

            foreach (System.Text.Json.JsonElement jsonElement in jsonElements)
            {
                // Direct numeric converters - the methods that threw on unmodified code.
                Assert.Null(Record.Exception(() =>
                {
                    bool ok = Core.Query.TryConvert_Int(jsonElement, out _);
                    Assert.False(ok);
                }));

                Assert.Null(Record.Exception(() =>
                {
                    bool ok = Core.Query.TryConvert_Long(jsonElement, out _);
                    Assert.False(ok);
                }));

                Assert.Null(Record.Exception(() =>
                {
                    bool ok = Core.Query.TryConvert_Double(jsonElement, out _);
                    Assert.False(ok);
                }));

                // Public dispatcher - the path FilterCondition.Value takes when MVC binds a JSON array/object.
                Assert.Null(Record.Exception(() =>
                {
                    bool ok = Core.Query.TryConvert(jsonElement, out _, typeof(int));
                    Assert.False(ok);
                }));

                Assert.Null(Record.Exception(() =>
                {
                    bool ok = Core.Query.TryConvert(jsonElement, out _, typeof(long));
                    Assert.False(ok);
                }));

                Assert.Null(Record.Exception(() =>
                {
                    bool ok = Core.Query.TryConvert(jsonElement, out _, typeof(double));
                    Assert.False(ok);
                }));
            }
        }

        /// <summary>
        /// Scalar JSON inputs must convert exactly as before the fix: a Number element and a numeric String
        /// element still produce the same int/long/double values, and an Array element still reaches the
        /// enumerable and object targets (the numeric-scalar short-circuit must not break either).
        /// </summary>
        [Fact]
        public void TryConvert_JsonElement_ScalarInputs_Unchanged()
        {
            System.Text.Json.JsonElement jsonElement_Number = System.Text.Json.JsonDocument.Parse("42").RootElement;
            System.Text.Json.JsonElement jsonElement_String = System.Text.Json.JsonDocument.Parse("\"42\"").RootElement;

            // Number element -> exact numeric values.
            Assert.True(Core.Query.TryConvert(jsonElement_Number, out int? number_Int));
            Assert.Equal(42, number_Int);

            Assert.True(Core.Query.TryConvert(jsonElement_Number, out long? number_Long));
            Assert.Equal(42L, number_Long);

            Assert.True(Core.Query.TryConvert(jsonElement_Number, out double? number_Double));
            Assert.Equal(42.0, number_Double);

            // Numeric String element -> same values (the ValueKind == String path the guard preserves).
            Assert.True(Core.Query.TryConvert(jsonElement_String, out int? string_Int));
            Assert.Equal(42, string_Int);

            Assert.True(Core.Query.TryConvert(jsonElement_String, out long? string_Long));
            Assert.Equal(42L, string_Long);

            Assert.True(Core.Query.TryConvert(jsonElement_String, out double? string_Double));
            Assert.Equal(42.0, string_Double);

            // Array element still reaches the enumerable target (the short-circuit must not break this).
            System.Text.Json.JsonElement jsonElement_Array = System.Text.Json.JsonDocument.Parse("[53429, 53412]").RootElement;
            Assert.True(Core.Query.TryConvert(jsonElement_Array, out object? list_Result, typeof(List<int>)));
            Assert.Equal([53429, 53412], (List<int>)list_Result!);

            // Array element still passes through for an object target (identity), which the short-circuit must not change.
            Assert.True(Core.Query.TryConvert(jsonElement_Array, out object? object_Result));
            Assert.NotNull(object_Result);
        }

        /// <summary>
        /// Guard against the unguarded-GetString pattern reappearing: every JsonValueKind against every scalar
        /// target type must not throw, through both the JsonElement overload and the main dispatcher. A future
        /// converter that calls GetString() without a ValueKind == String guard trips this the moment it is
        /// exercised by a non-string kind.
        /// </summary>
        [Fact]
        public void TryConvert_JsonElement_AllValueKinds_ScalarTargets_NeverThrow()
        {
            // One representative JsonElement per ValueKind.
            System.Text.Json.JsonElement jsonElement_Undefined = default;
            System.Text.Json.JsonElement jsonElement_Null = System.Text.Json.JsonDocument.Parse("null").RootElement;
            System.Text.Json.JsonElement jsonElement_String = System.Text.Json.JsonDocument.Parse("\"42\"").RootElement;
            System.Text.Json.JsonElement jsonElement_Number = System.Text.Json.JsonDocument.Parse("42").RootElement;
            System.Text.Json.JsonElement jsonElement_True = System.Text.Json.JsonDocument.Parse("true").RootElement;
            System.Text.Json.JsonElement jsonElement_False = System.Text.Json.JsonDocument.Parse("false").RootElement;
            System.Text.Json.JsonElement jsonElement_Object = System.Text.Json.JsonDocument.Parse("{\"a\":1}").RootElement;
            System.Text.Json.JsonElement jsonElement_Array = System.Text.Json.JsonDocument.Parse("[1, 2]").RootElement;

            System.Text.Json.JsonElement[] jsonElements = [jsonElement_Undefined, jsonElement_Null, jsonElement_String, jsonElement_Number, jsonElement_True, jsonElement_False, jsonElement_Object, jsonElement_Array];

            // The scalar target types routed to the scalar TryConvert_* converters.
            Type[] targetTypes = [typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(int), typeof(long), typeof(ushort), typeof(uint), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(char)];

            foreach (System.Text.Json.JsonElement jsonElement in jsonElements)
            {
                foreach (Type targetType in targetTypes)
                {
                    // JsonElement overload (TryConvert(JsonElement, out object?, Type)).
                    Assert.Null(Record.Exception(() =>
                    {
                        _ = Core.Query.TryConvert(jsonElement, out object? result, targetType);
                    }));

                    // Main dispatcher (box to object so the object? overload is chosen - the path FilterCondition.Value takes).
                    object? object_Boxed = jsonElement;
                    Assert.Null(Record.Exception(() =>
                    {
                        _ = Core.Query.TryConvert(object_Boxed, out object? result, targetType);
                    }));
                }
            }
        }
    }
}