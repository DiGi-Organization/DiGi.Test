using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the UniqueId property is excluded from JSON serialization for both standard serialization and custom object-to-JSON conversion.
        /// </summary>
        [Fact]
        public void UniqueId()
        {
            string json = JsonSerializer.Serialize(new Classes.UniqueIdObject());
            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.DoesNotContain("UniqueId", json);

            TestObject testObject = new("AAAA");

            JsonObject? jsonObject = testObject.ToJsonObject();

            Assert.NotNull(jsonObject);

            Assert.False(jsonObject.ContainsKey(nameof(testObject.UniqueId)));
        }

        /// <summary>
        /// Tests the UniqueId query method for various types, verifying correct ID generation and ensuring that unsupported types are safely handled without runtime binder exceptions.
        /// </summary>
        [Fact]
        public void UniqueId_Generation()
        {
            // Test standard types
            string string_Val = "Hello World";
            string string_Id1 = string_Val.UniqueId();
            Assert.False(string.IsNullOrWhiteSpace(string_Id1));

            // Test fallback overload for unsupported types (Verifying the fix for Bug 4)
            long long_Val = 9876543210L;
            string string_Id2 = Core.Query.UniqueId(long_Val);
            Assert.Equal(long_Val.ToString(CultureInfo.CurrentCulture), string_Id2);

            // Test JsonValue types
            JsonValue jsonValue_String = JsonValue.Create("JsonString")!;
            string string_Id3 = Core.Query.UniqueId(jsonValue_String);
            Assert.False(string.IsNullOrWhiteSpace(string_Id3));

            JsonValue jsonValue_Long = JsonValue.Create(12345L)!;
            string string_Id4 = Core.Query.UniqueId(jsonValue_Long);
            Assert.Equal("12345", string_Id4);

            // Test custom class fallback
            object object_Custom = new();
            string string_Id5 = Core.Query.UniqueId(object_Custom);
            Assert.Equal(object_Custom.ToString(), string_Id5);

            // Test null safety
            object? object_Null = null;
            string string_IdNull = Core.Query.UniqueId(object_Null);
            Assert.Equal(Constants.UniqueId.Null, string_IdNull);
        }

        /// <summary>
        /// Verifies that unique identifiers of floating point values are identical under a comma-decimal culture (pl-PL) and under the invariant culture.
        /// </summary>
        [Fact]
        public void UniqueId_CultureInvariance()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

                Assert.Equal("1.5", 1.5.UniqueId());
                Assert.Equal("1.5", ((double?)1.5).UniqueId());
                Assert.Equal("1.5", 1.5f.UniqueId());
                Assert.Equal("1.5", ((float?)1.5f).UniqueId());
                Assert.Equal("1.5", 1.5m.UniqueId());
                Assert.Equal("1.5", ((decimal?)1.5m).UniqueId());

                JsonObject jsonObject_Double = new() { ["x"] = 1.5 };
                Assert.Equal(jsonObject_Double.UniqueId(), Core.Query.UniqueId(JsonObject.Parse("{\"x\":1.5}")));
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }

        /// <summary>
        /// Verifies that unique identifiers of DateTime values with identical Ticks but different Kind are distinct.
        /// </summary>
        [Fact]
        public void UniqueId_DateTimeKind()
        {
            DateTime dateTime_Utc = new(638000000000000000L, DateTimeKind.Utc);
            DateTime dateTime_Unspecified = new(638000000000000000L, DateTimeKind.Unspecified);
            Assert.NotEqual(dateTime_Utc.UniqueId(), dateTime_Unspecified.UniqueId());

            DateTime dateTime_Local = new(638000000000000000L, DateTimeKind.Local);
            Assert.NotEqual(dateTime_Unspecified.UniqueId(), dateTime_Local.UniqueId());
        }

        /// <summary>
        /// Verifies that unique identifiers of enums are generated for every backing type without OverflowException, and that int-backed enums keep their numeric identifier.
        /// </summary>
        [Fact]
        public void UniqueId_EnumBackingTypes()
        {
            Assert.Equal("0", TestEnum.Test1.UniqueId());
            Assert.Equal("1", TestEnum.Test2.UniqueId());

            Assert.Equal(long.MaxValue.ToString(CultureInfo.CurrentCulture), TestEnumLong.Max.UniqueId());
            Assert.Equal(uint.MaxValue.ToString(CultureInfo.CurrentCulture), TestEnumUInt.Max.UniqueId());
        }

        /// <summary>
        /// Verifies that unique identifiers of JsonValues carrying types without a dedicated overload (byte, sbyte, ushort) are generated without RuntimeBinderException.
        /// </summary>
        [Fact]
        public void UniqueId_JsonValueUnsupportedTypes()
        {
            Assert.Equal("5", Core.Query.UniqueId(JsonValue.Create((byte)5)));
            Assert.Equal("-5", Core.Query.UniqueId(JsonValue.Create((sbyte)-5)));
            Assert.Equal("7", Core.Query.UniqueId(JsonValue.Create((ushort)7)));
        }

        /// <summary>
        /// Verifies that unique identifiers of JsonArrays are distinct for arrays whose element identifiers concatenate to the same string, and that string and number elements carrying equal text are distinct.
        /// </summary>
        [Fact]
        public void UniqueId_JsonArrayBoundary()
        {
            JsonArray jsonArray_12_3 = [12, 3];
            JsonArray jsonArray_1_23 = [1, 23];
            JsonArray jsonArray_123 = [123];

            string string_12_3 = jsonArray_12_3.UniqueId();
            string string_1_23 = jsonArray_1_23.UniqueId();
            string string_123 = jsonArray_123.UniqueId();

            Assert.NotEqual(string_12_3, string_1_23);
            Assert.NotEqual(string_12_3, string_123);
            Assert.NotEqual(string_1_23, string_123);

            JsonArray jsonArray_StringFirst = ["1", 2];
            JsonArray jsonArray_NumberFirst = [1, "2"];
            Assert.NotEqual(jsonArray_StringFirst.UniqueId(), jsonArray_NumberFirst.UniqueId());

            JsonArray jsonArray_Int = [5];
            JsonArray jsonArray_Double = [5.0];
            Assert.Equal(jsonArray_Int.UniqueId(), jsonArray_Double.UniqueId());
        }

        /// <summary>
        /// Verifies that the unique identifier of a JsonObject built from CLR values equals the unique identifier of the same object parsed back from its JSON text, for every value type a serializable object can carry.
        /// </summary>
        [Fact]
        public void UniqueId_JsonObjectRoundTrip()
        {
            JsonObject jsonObject = new()
            {
                ["double"] = 5.0,
                ["float"] = 1.5f,
                ["short"] = (short)3,
                ["byte"] = (byte)4,
                ["char"] = 'A',
                ["dateTimeOffset"] = new DateTimeOffset(2026, 9, 11, 12, 0, 0, System.TimeSpan.FromHours(2)),
                ["guid"] = Guid.NewGuid(),
                ["bool"] = true,
                ["null"] = null,
                ["nested"] = new JsonObject() { ["b"] = 2, ["a"] = 1 },
            };

            JsonNode? jsonNode_Parsed = JsonNode.Parse(jsonObject.ToJsonString());
            Assert.NotNull(jsonNode_Parsed);

            Assert.Equal(jsonObject.UniqueId(), Core.Query.UniqueId(jsonNode_Parsed));
            Assert.Equal(jsonObject.UniqueId(), jsonNode_Parsed.AsObject().UniqueId());

            // Create.JsonNode boxes every value through JsonValue.Create(object), which is a different JsonValue implementation than the implicit conversions above.
            JsonObject jsonObject_Boxed = [];
            foreach (System.Collections.Generic.KeyValuePair<string, JsonNode?> keyValuePair in jsonObject)
            {
                jsonObject_Boxed[keyValuePair.Key] = keyValuePair.Value is JsonValue jsonValue ? JsonValue.Create(jsonValue.GetValue<object>()) : keyValuePair.Value?.DeepClone();
            }

            Assert.Equal(jsonObject.UniqueId(), jsonObject_Boxed.UniqueId());
        }

        /// <summary>
        /// Verifies that the unique identifier of a JsonValue parsed from JSON text equals the unique identifier of the same value created from a CLR value and of the CLR value itself.
        /// </summary>
        [Fact]
        public void UniqueId_JsonValueParsed()
        {
            JsonNode? jsonNode_String = JsonNode.Parse("\"abc\"");
            Assert.NotNull(jsonNode_String);
            Assert.Equal("abc".UniqueId(), Core.Query.UniqueId(jsonNode_String));
            Assert.Equal(Core.Query.UniqueId(JsonValue.Create("abc")), Core.Query.UniqueId(jsonNode_String.AsValue()));

            JsonNode? jsonNode_Number = JsonNode.Parse("12345");
            Assert.NotNull(jsonNode_Number);
            Assert.Equal("12345", Core.Query.UniqueId(jsonNode_Number));

            JsonNode? jsonNode_Double = JsonNode.Parse("1.5");
            Assert.NotNull(jsonNode_Double);
            Assert.Equal("1.5", Core.Query.UniqueId(jsonNode_Double));
        }

        /// <summary>
        /// Verifies that unique identifiers of JsonObjects are equal for semantically identical objects built with different key insertion orders.
        /// </summary>
        [Fact]
        public void UniqueId_JsonObjectKeyOrder()
        {
            JsonObject jsonObject_NameFirst = new() { ["name"] = "x", ["id"] = 1 };
            JsonObject jsonObject_IdFirst = new() { ["id"] = 1, ["name"] = "x" };
            Assert.Equal(jsonObject_NameFirst.UniqueId(), jsonObject_IdFirst.UniqueId());
        }

        /// <summary>
        /// Verifies that unique identifiers of JsonObjects are distinct when key and value boundaries fall at different positions over the same concatenated characters.
        /// </summary>
        [Fact]
        public void UniqueId_JsonObjectStructure()
        {
            JsonObject jsonObject_ShortKey = new() { ["a"] = "bcd" };
            JsonObject jsonObject_LongKey = new() { ["abc"] = "d" };
            Assert.NotEqual(jsonObject_ShortKey.UniqueId(), jsonObject_LongKey.UniqueId());
        }

        /// <summary>
        /// Verifies that byte, sbyte, char, ushort, nint and nuint generate unique identifiers without falling through to the object overload, and that the DateTimeOffset identifier is a culture invariant round-trip string.
        /// </summary>
        [Fact]
        public void UniqueId_MissingPrimitiveOverloads()
        {
            Assert.Equal("5", ((byte)5).UniqueId());
            Assert.Equal("5", ((byte?)5).UniqueId());
            Assert.Equal("-5", ((sbyte)-5).UniqueId());
            Assert.Equal("-5", ((sbyte?)-5).UniqueId());
            Assert.Equal("A", 'A'.UniqueId());
            Assert.Equal("A", ((char?)'A').UniqueId());
            Assert.Equal("65535", ((ushort)65535).UniqueId());
            Assert.Equal("65535", ((ushort?)65535).UniqueId());
            Assert.Equal("42", ((nint)42).UniqueId());
            Assert.Equal("42", ((nuint)42).UniqueId());

            DateTimeOffset dateTimeOffset = new(2026, 9, 11, 12, 0, 0, System.TimeSpan.FromHours(2));
            Assert.Equal("2026-09-11T12:00:00.0000000+02:00", dateTimeOffset.UniqueId());

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
                Assert.Equal("2026-09-11T12:00:00.0000000+02:00", dateTimeOffset.UniqueId());
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }
    }
}