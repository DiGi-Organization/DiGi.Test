using System.Collections.Generic;
using System.Linq;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetEnumerator()"/> streams values
        /// directly from derived storage without failing or dropping elements.
        /// </summary>
        [Fact]
        public void Cluster_GetEnumerator_StreamsAllValues()
        {
            Classes.TestKeyedListCluster testKeyedListCluster = new();
            Assert.True(testKeyedListCluster.Add("groupA:sub1:val1"));
            Assert.True(testKeyedListCluster.Add("groupA:sub2:val2"));
            Assert.True(testKeyedListCluster.Add("groupB:sub1:val3"));

            List<string> enumerated = [];
            foreach (string item in testKeyedListCluster)
            {
                enumerated.Add(item);
            }

            Assert.Equal(3, enumerated.Count);
            Assert.Contains("groupA:sub1:val1", enumerated);
            Assert.Contains("groupA:sub2:val2", enumerated);
            Assert.Contains("groupB:sub1:val3", enumerated);

            Classes.TestValueCluster testValueCluster = new();
            Assert.True(testValueCluster.Add("groupA:sub1:val1"));
            Assert.True(testValueCluster.Add("groupB:sub2:val2"));

            List<string> enumeratedValues = [];
            foreach (string item in testValueCluster)
            {
                enumeratedValues.Add(item);
            }

            Assert.Equal(2, enumeratedValues.Count);
            Assert.Contains("groupA:sub1:val1", enumeratedValues);
            Assert.Contains("groupB:sub2:val2", enumeratedValues);
        }

        /// <summary>
        /// Tests that <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetValues{UValue}(System.Func{UValue, bool})"/>
        /// filters elements correctly using in-place compaction.
        /// </summary>
        [Fact]
        public void Cluster_GetValues_Predicate_FiltersCorrectly()
        {
            Classes.TestKeyedListCluster testKeyedListCluster = new();
            testKeyedListCluster.Add("groupA:sub1:apple");
            testKeyedListCluster.Add("groupA:sub2:apricot");
            testKeyedListCluster.Add("groupB:sub1:banana");

            List<string>? filtered = testKeyedListCluster.GetValues<string>(x => x != null && x.Contains("ap"));

            Assert.NotNull(filtered);
            Assert.Equal(2, filtered.Count);
            Assert.Contains("groupA:sub1:apple", filtered);
            Assert.Contains("groupA:sub2:apricot", filtered);
            Assert.DoesNotContain("groupB:sub1:banana", filtered);
        }

        /// <summary>
        /// Tests that <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetValue{UValue}(System.Func{UValue, bool})"/>
        /// returns the first matching element via early-exit evaluation.
        /// </summary>
        [Fact]
        public void Cluster_GetValue_Predicate_EarlyExits()
        {
            Classes.TestKeyedListCluster testKeyedListCluster = new();
            testKeyedListCluster.Add("groupA:sub1:first");
            testKeyedListCluster.Add("groupA:sub2:second");
            testKeyedListCluster.Add("groupB:sub1:third");

            string? found = testKeyedListCluster.GetValue<string>(x => x != null && x.Contains("second"));
            Assert.Equal("groupA:sub2:second", found);

            string? notFound = testKeyedListCluster.GetValue<string>(x => x != null && x.Contains("nonexistent"));
            Assert.Null(notFound);
        }

        /// <summary>
        /// Tests that passing a null primary key to <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetValues{UValue}(TKey_1)"/>
        /// returns null on both base reference and derived instances (LSP compliance).
        /// </summary>
        [Fact]
        public void Cluster_GetValues_NullKey_ReturnsNull()
        {
            Classes.TestKeyedListCluster testKeyedListCluster = new();
            testKeyedListCluster.Add("groupA:sub1:item1");

            Core.Classes.Cluster<string, string, string> clusterBase = testKeyedListCluster;

            Assert.Null(testKeyedListCluster.GetValues<string>((string?)null));
            Assert.Null(clusterBase.GetValues<string>((string?)null));

            Classes.TestValueCluster testValueCluster = new();
            testValueCluster.Add("groupA:sub1:item1");

            Core.Classes.Cluster<string, string, string> valueClusterBase = testValueCluster;

            Assert.Null(testValueCluster.GetValues<string>((string?)null));
            Assert.Null(valueClusterBase.GetValues<string>((string?)null));
        }

        /// <summary>
        /// Tests that assigning null to <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.Values"/> clears the cluster.
        /// </summary>
        [Fact]
        public void Cluster_SetValues_Null_ClearsCluster()
        {
            Classes.TestListCluster testListCluster = new();
            testListCluster.Add("item1");
            testListCluster.Add("item2");
            Assert.Equal(2, testListCluster.Values?.Count);

            testListCluster.Values = null;

            Assert.Empty(testListCluster);
            Assert.Empty(testListCluster.Values ?? []);
        }

        /// <summary>
        /// Tests that self-assignment and deferred LINQ evaluation do not result in data loss during
        /// <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.Values"/> setter or constructor execution.
        /// </summary>
        [Fact]
        public void Cluster_SetValues_SelfAssignment_AndDeferredQuery()
        {
            Classes.TestKeyedListCluster testKeyedListCluster = new();
            testKeyedListCluster.Add("groupA:sub1:item1");
            testKeyedListCluster.Add("groupA:sub2:item2");
            testKeyedListCluster.Add("groupB:sub1:item3");

            // Self assignment via SetValues
            Assert.True(testKeyedListCluster.SetValues_Test(testKeyedListCluster));
            Assert.Equal(3, testKeyedListCluster.Count());

            // Deferred LINQ evaluation passed to constructor
            Classes.TestKeyedListCluster fromDeferred = new(testKeyedListCluster.Where(x => x.EndsWith("1", StringComparison.CurrentCulture) || x.EndsWith("3", StringComparison.CurrentCulture)));
            Assert.Equal(2, fromDeferred.Count());
            Assert.Contains("groupA:sub1:item1", fromDeferred);
            Assert.Contains("groupB:sub1:item3", fromDeferred);
            Assert.DoesNotContain("groupA:sub2:item2", fromDeferred);

            // Deferred LINQ evaluation assigned to Values property via collection expression
            testKeyedListCluster.Values = [.. testKeyedListCluster.Where(x => x.EndsWith("1", StringComparison.CurrentCulture) || x.EndsWith("3", StringComparison.CurrentCulture))];
            Assert.Equal(2, testKeyedListCluster.Count());
            Assert.Contains("groupA:sub1:item1", testKeyedListCluster);
            Assert.Contains("groupB:sub1:item3", testKeyedListCluster);
            Assert.DoesNotContain("groupA:sub2:item2", testKeyedListCluster);
        }

        /// <summary>
        /// Tests the trailing-out companion overloads for <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.TryGetValue{UValue}(System.Func{UValue, bool}, out UValue)"/>
        /// and <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.TryGetValues{UValue}(System.Func{UValue, bool}, out List{UValue})"/>.
        /// </summary>
        [Fact]
        public void Cluster_TryGetValue_And_TryGetValues_TrailingOut()
        {
            Classes.TestKeyedListCluster testKeyedListCluster = new();
            testKeyedListCluster.Add("groupA:sub1:alpha");
            testKeyedListCluster.Add("groupA:sub2:beta");
            testKeyedListCluster.Add("groupB:sub1:gamma");

            // Trailing out TryGetValue
            Assert.True(testKeyedListCluster.TryGetValue(x => x != null && x.Contains("beta"), out string? matchedValue));
            Assert.Equal("groupA:sub2:beta", matchedValue);

            Assert.False(testKeyedListCluster.TryGetValue(x => x != null && x.Contains("delta"), out string? unmatchedValue));
            Assert.Null(unmatchedValue);

            // Leading out TryGetValue (backward compatibility)
            Assert.True(testKeyedListCluster.TryGetValue(out string? matchedLeading, x => x != null && x.Contains("alpha")));
            Assert.Equal("groupA:sub1:alpha", matchedLeading);

            // Trailing out TryGetValues
            Assert.True(testKeyedListCluster.TryGetValues(x => x != null && x.StartsWith("groupA", StringComparison.CurrentCulture), out List<string>? matchedValues));
            Assert.NotNull(matchedValues);
            Assert.Equal(2, matchedValues.Count);

            // Leading out TryGetValues (backward compatibility)
            Assert.True(testKeyedListCluster.TryGetValues(out List<string>? matchedValuesLeading, x => x != null && x.StartsWith("groupB", StringComparison.CurrentCulture)));
            Assert.NotNull(matchedValuesLeading);
            Assert.Single(matchedValuesLeading);
        }

        /// <summary>
        /// Tests retrieving keys via <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetKeys_1"/>
        /// and <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetKeys_2(TKey_1)"/>.
        /// </summary>
        [Fact]
        public void Cluster_GetKeys_1_And_GetKeys_2()
        {
            Classes.TestKeyedListCluster testKeyedListCluster = new();
            testKeyedListCluster.Add("k1:subA:val1");
            testKeyedListCluster.Add("k1:subB:val2");
            testKeyedListCluster.Add("k2:subC:val3");

            List<string>? keys_1 = testKeyedListCluster.GetKeys_1();
            Assert.NotNull(keys_1);
            Assert.Equal(2, keys_1.Count);
            Assert.Contains("k1", keys_1);
            Assert.Contains("k2", keys_1);

            List<string>? keys_2 = testKeyedListCluster.GetKeys_2("k1");
            Assert.NotNull(keys_2);
            Assert.Equal(2, keys_2.Count);
            Assert.Contains("subA", keys_2);
            Assert.Contains("subB", keys_2);

            Assert.Null(testKeyedListCluster.GetKeys_2(null));
        }

        /// <summary>
        /// Pins the base-class <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetKeys_1"/>
        /// and <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetKeys_2(TKey_1)"/> implementations
        /// on a derivative that does not override them, drawn from the cluster's enumeration.
        /// </summary>
        [Fact]
        public void Cluster_Base_GetKeys_StreamsOverEnumeration()
        {
            Classes.TestBaseCluster testBaseCluster = new();
            Assert.True(testBaseCluster.Add("k1:subA:val1"));
            Assert.True(testBaseCluster.Add("k1:subB:val2"));
            Assert.True(testBaseCluster.Add("k2:subC:val3"));

            List<int>? keys_1 = testBaseCluster.GetKeys_1();
            Assert.NotNull(keys_1);
            Assert.Single(keys_1);
            Assert.Equal(1, keys_1[0]);

            List<string>? keys_2 = testBaseCluster.GetKeys_2(1);
            Assert.NotNull(keys_2);
            Assert.Equal(3, keys_2.Count);
            Assert.Contains("subA", keys_2);
            Assert.Contains("subB", keys_2);
            Assert.Contains("subC", keys_2);

            List<string>? keys_2_NoMatch = testBaseCluster.GetKeys_2(99);
            Assert.NotNull(keys_2_NoMatch);
            Assert.Empty(keys_2_NoMatch);

            Classes.TestBaseCluster testBaseCluster_Empty = new();
            List<int>? keys_1_Empty = testBaseCluster_Empty.GetKeys_1();
            Assert.NotNull(keys_1_Empty);
            Assert.Empty(keys_1_Empty);
        }

        /// <summary>
        /// Pins the base-class <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetValues{UValue}(TKey_1)"/>
        /// and <see cref="Core.Classes.Cluster{TKey_1, TKey_2, TValue}.GetValues{UValue}(System.Func{UValue, bool}?)"/> implementations
        /// on a derivative that does not override them, built in a single pass over the cluster's enumeration.
        /// </summary>
        [Fact]
        public void Cluster_Base_GetValues_SinglePassOverEnumeration()
        {
            Classes.TestBaseCluster testBaseCluster = new();
            testBaseCluster.Add("groupA:sub1:apple");
            testBaseCluster.Add("groupA:sub2:apricot");
            testBaseCluster.Add("groupB:sub1:banana");

            List<string>? byKey = testBaseCluster.GetValues<string>(1);
            Assert.NotNull(byKey);
            Assert.Equal(3, byKey.Count);
            Assert.Contains("groupA:sub1:apple", byKey);
            Assert.Contains("groupA:sub2:apricot", byKey);
            Assert.Contains("groupB:sub1:banana", byKey);

            List<string>? byKey_NoMatch = testBaseCluster.GetValues<string>(99);
            Assert.NotNull(byKey_NoMatch);
            Assert.Empty(byKey_NoMatch);

            List<string>? byPredicate = testBaseCluster.GetValues<string>(x => x != null && x.Contains("ap"));
            Assert.NotNull(byPredicate);
            Assert.Equal(2, byPredicate.Count);

            List<string>? all = testBaseCluster.GetValues((System.Func<string?, bool>?)null);
            Assert.NotNull(all);
            Assert.Equal(3, all.Count);
        }
    }
}
