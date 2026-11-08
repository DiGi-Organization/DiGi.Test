using System.Collections.Generic;
using System.Globalization;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the dictionary query extension methods, including TryGetFirstKey, TryGetKeys, and TryGetLowerValue.
        /// </summary>
        [Fact]
        public void DictionaryExtensions_Queries()
        {
            // 1. Test TryGetFirstKey (Verifying case-insensitivity and original key retrieval)
            Dictionary<string, int> scores = new()
            {
                { "Database", 90 },
                { "Server", 85 },
                { "Client", 95 }
            };

            string? string_Key;
            bool bool_Success = scores.TryGetFirstKey("SERVER", out string_Key, x => x?.ToUpper(CultureInfo.CurrentCulture));

            Assert.True(bool_Success);
            Assert.Equal("Server", string_Key); // Verifies the fix: should return original casing "Server", not transformed "SERVER"

            // 2. Test TryGetKeys
            List<string>? keys;
            bool_Success = scores.TryGetKeys("DATABASE", out keys, x => x?.ToUpper(CultureInfo.CurrentCulture));

            Assert.True(bool_Success);
            Assert.NotNull(keys);
            Assert.Single(keys);
            Assert.Equal("Database", keys[0]);

            // 3. Test TryGetLowerValue (Verifying correct boundary and interval lookups)
            SortedDictionary<double, string> sortedNumbers = new()
            {
                { 1.0, "One" },
                { 2.0, "Two" },
                { 5.0, "Five" }
            };

            string? string_Value;

            // Exact match on intermediate key
            bool_Success = sortedNumbers.TryGetLowerValue(2.0, out string_Value);
            Assert.True(bool_Success);
            Assert.Equal("Two", string_Value);

            // Exact match on maximum key with upperLimit = true (Verifying the fix for Bug 5)
            bool_Success = sortedNumbers.TryGetLowerValue(5.0, out string_Value, lowerLimit: true, upperLimit: true);
            Assert.True(bool_Success);
            Assert.Equal("Five", string_Value); // Should succeed and return "Five"

            // Below minimum key with lowerLimit = true (should fail)
            bool_Success = sortedNumbers.TryGetLowerValue(0.5, out string_Value, lowerLimit: true, upperLimit: false);
            Assert.False(bool_Success);

            // Below minimum key with lowerLimit = false (should clamp to minimum)
            bool_Success = sortedNumbers.TryGetLowerValue(0.5, out string_Value, lowerLimit: false, upperLimit: false);
            Assert.True(bool_Success);
            Assert.Equal("One", string_Value);

            // Above maximum key with upperLimit = true (should fail)
            bool_Success = sortedNumbers.TryGetLowerValue(6.0, out string_Value, lowerLimit: true, upperLimit: true);
            Assert.False(bool_Success);

            // Above maximum key with upperLimit = false (should clamp to maximum)
            bool_Success = sortedNumbers.TryGetLowerValue(6.0, out string_Value, lowerLimit: true, upperLimit: false);
            Assert.True(bool_Success);
            Assert.Equal("Five", string_Value);

            // Intermediate unmatched key (should find nearest lower key)
            bool_Success = sortedNumbers.TryGetLowerValue(3.5, out string_Value);
            Assert.True(bool_Success);
            Assert.Equal("Two", string_Value); // Nearest lower key is 2.0
        }
    }
}