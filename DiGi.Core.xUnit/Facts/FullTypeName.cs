using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the FullTypeName queries, verifying correct formatting for primitive types,
        /// generic types, and serializable objects, as well as checking robustness against null
        /// and invalid JsonObject inputs.
        /// </summary>
        [Fact]
        public void FullTypeName()
        {
            // 1. Test Null Inputs
            JsonObject? jsonObject_Null = null;
            string? string_NullResultForJsonObject = Core.Query.FullTypeName(jsonObject_Null);
            Assert.Null(string_NullResultForJsonObject);

            System.Type? type_Null = null;
            string? string_NullResultForType = Core.Query.FullTypeName(type_Null);
            Assert.Null(string_NullResultForType);

            Interfaces.ISerializableObject? serializableObject_Null = null;
            string? string_NullResultForSerializableObject = Core.Query.FullTypeName(serializableObject_Null);
            Assert.Null(string_NullResultForSerializableObject);

            // 2. Test JsonObject with valid and invalid type properties
            JsonObject jsonObject_Valid = new();
            jsonObject_Valid.Add(Constants.Serialization.PropertyName.Type, JsonValue.Create("TestNamespace.TestType, TestAssembly"));
            string? string_ValidJsonObjectResult = Core.Query.FullTypeName(jsonObject_Valid);
            Assert.Equal("TestNamespace.TestType, TestAssembly", string_ValidJsonObjectResult);

            JsonObject jsonObject_InvalidObject = new();
            jsonObject_InvalidObject.Add(Constants.Serialization.PropertyName.Type, new JsonObject());
            string? string_InvalidObjectResult = Core.Query.FullTypeName(jsonObject_InvalidObject);
            Assert.Null(string_InvalidObjectResult);

            JsonObject jsonObject_InvalidArray = new();
            jsonObject_InvalidArray.Add(Constants.Serialization.PropertyName.Type, new JsonArray());
            string? string_InvalidArrayResult = Core.Query.FullTypeName(jsonObject_InvalidArray);
            Assert.Null(string_InvalidArrayResult);

            // 3. Test Type Formatting (Primitive and Generic)
            System.Type type_Int = typeof(int);
            string? string_IntResult1 = Core.Query.FullTypeName(type_Int);
            string? string_IntResult2 = Core.Query.FullTypeName(type_Int);
            Assert.NotNull(string_IntResult1);
            Assert.Equal(string_IntResult1, string_IntResult2);

            string? string_IntAssemblyFullName = typeof(int).Assembly.FullName;
            Assert.NotNull(string_IntAssemblyFullName);
            int int_IntCommaIndex = string_IntAssemblyFullName.IndexOf(',');
            string string_IntAssemblyShortName = int_IntCommaIndex > 0 ? string_IntAssemblyFullName.Substring(0, int_IntCommaIndex) : string_IntAssemblyFullName;
            string string_ExpectedIntName = "System.Int32," + string_IntAssemblyShortName;
            Assert.Equal(string_ExpectedIntName, string_IntResult1);

            System.Type type_Generic = typeof(List<double>);
            string? string_GenericResult1 = Core.Query.FullTypeName(type_Generic);
            string? string_GenericResult2 = Core.Query.FullTypeName(type_Generic);
            Assert.NotNull(string_GenericResult1);
            Assert.Equal(string_GenericResult1, string_GenericResult2);

            string? string_ListAssemblyFullName = typeof(List<double>).Assembly.FullName;
            Assert.NotNull(string_ListAssemblyFullName);
            int int_ListCommaIndex = string_ListAssemblyFullName.IndexOf(',');
            string string_ListAssemblyShortName = int_ListCommaIndex > 0 ? string_ListAssemblyFullName.Substring(0, int_ListCommaIndex) : string_ListAssemblyFullName;

            string? string_DoubleAssemblyFullName = typeof(double).Assembly.FullName;
            Assert.NotNull(string_DoubleAssemblyFullName);
            int int_DoubleCommaIndex = string_DoubleAssemblyFullName.IndexOf(',');
            string string_DoubleAssemblyShortName = int_DoubleCommaIndex > 0 ? string_DoubleAssemblyFullName.Substring(0, int_DoubleCommaIndex) : string_DoubleAssemblyFullName;

            string string_ExpectedGenericName = "System.Collections.Generic.List`1[[System.Double," + string_DoubleAssemblyShortName + "]]," + string_ListAssemblyShortName;
            Assert.Equal(string_ExpectedGenericName, string_GenericResult1);

            // 4. Test ISerializableObject
            Core.Classes.Color color_Instance = new(System.Drawing.Color.Blue);
            string? string_ColorResult = Core.Query.FullTypeName(color_Instance);
            string? string_ExpectedColorName = Core.Query.FullTypeName(typeof(Core.Classes.Color));
            Assert.Equal(string_ExpectedColorName, string_ColorResult);
        }
    }
}