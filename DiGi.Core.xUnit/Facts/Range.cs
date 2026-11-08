using DiGi.Core.Classes;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the Range constructor, verifying that min and max boundaries are correctly ordered,
        /// and that properties like Length compute correctly.
        /// </summary>
        [Fact]
        public void Range_Constructor_And_Properties()
        {
            Range<int> range_Int = new(10, 5);
            Assert.Equal(5, range_Int.Min);
            Assert.Equal(10, range_Int.Max);
            Assert.Equal(5, range_Int.Length);

            Range<double> range_Double = new(1.5, 4.5);
            Assert.Equal(1.5, range_Double.Min);
            Assert.Equal(4.5, range_Double.Max);
            Assert.Equal(3.0, range_Double.Length);
        }

        /// <summary>
        /// Tests containment methods for values and other ranges without tolerance.
        /// </summary>
        [Fact]
        public void Range_In_Out_Containing()
        {
            Range<int> range_Base = new(0, 10);

            // Value containment
            Assert.True(range_Base.In(5));
            Assert.True(range_Base.In(0));
            Assert.True(range_Base.In(10));
            Assert.False(range_Base.In(-1));
            Assert.False(range_Base.In(11));

            // Range containment (parameter is entirely within range_Base)
            Range<int> range_Inside = new(2, 8);
            Range<int> range_Touching = new(0, 10);
            Range<int> range_Outside = new(5, 12);
            Range<int> range_Disjoint = new(12, 15);

            Assert.True(range_Base.In(range_Inside));
            Assert.True(range_Base.In(range_Touching));
            Assert.False(range_Base.In(range_Outside));
            Assert.False(range_Base.In(range_Disjoint));

            // Out checks
            Assert.True(range_Base.Out(-5));
            Assert.False(range_Base.Out(5));
            Assert.True(range_Base.Out(range_Disjoint));
            Assert.False(range_Base.Out(range_Inside));
        }

        /// <summary>
        /// Tests containment, separation, and intersection methods with tolerance.
        /// </summary>
        [Fact]
        public void Range_In_Out_Tolerance()
        {
            Range<double> range_Base = new(2.0, 8.0);
            double tolerance = 1.0;

            // Value In/Out with tolerance
            Assert.True(range_Base.In(1.5, tolerance));
            Assert.True(range_Base.In(9.0, tolerance));
            Assert.False(range_Base.In(0.9, tolerance));
            Assert.False(range_Base.In(9.1, tolerance));

            // Range In/Out with tolerance
            Range<double> range_InsideWithTolerance = new(1.5, 8.5);
            Range<double> range_OutsideWithTolerance = new(0.5, 9.5);

            Assert.True(range_Base.In(range_InsideWithTolerance, tolerance));
            Assert.False(range_Base.In(range_OutsideWithTolerance, tolerance));

            // Out with tolerance (separated by > tolerance)
            Range<double> range_DisjointClose = new(9.5, 12.0); // distance is 1.5 > 1.0
            Range<double> range_DisjointVeryClose = new(8.5, 12.0); // distance is 0.5 <= 1.0

            Assert.True(range_Base.Out(range_DisjointClose, tolerance));
            Assert.False(range_Base.Out(range_DisjointVeryClose, tolerance));

            // New InRange method (not completely outside with tolerance)
            Assert.True(range_Base.InRange(range_DisjointVeryClose, tolerance));
            Assert.False(range_Base.InRange(range_DisjointClose, tolerance));
        }

        /// <summary>
        /// Tests the Intersect method, verifying overlapping, touching, and disjoint configurations.
        /// </summary>
        [Fact]
        public void Range_Intersect()
        {
            Range<int> range_Base = new(5, 15);

            Range<int> range_Overlap = new(10, 20);
            Range<int> range_Touch = new(15, 25);
            Range<int> range_Disjoint = new(16, 25);

            Assert.True(range_Base.Intersect(range_Overlap));
            Assert.False(range_Base.Intersect(range_Touch)); // Touching at 15 is considered outside (not intersecting) in this codebase
            Assert.False(range_Base.Intersect(range_Disjoint));
        }

        /// <summary>
        /// Verifies that Equals and GetHashCode handle null range bounds safely without throwing exceptions.
        /// </summary>
        [Fact]
        public void Range_Equals_And_GetHashCode_NullSafety()
        {
            Range<int?> range_Nulls_1 = new(null, null);
            Range<int?> range_Nulls_2 = new(null, null);
            Range<int?> range_Mixed_1 = new(null, 10);
            Range<int?> range_Mixed_2 = new(null, 10);

            // Null-safe Equals
            Assert.True(range_Nulls_1.Equals(range_Nulls_2));
            Assert.True(range_Mixed_1.Equals(range_Mixed_2));
            Assert.False(range_Nulls_1.Equals(range_Mixed_1));
            Assert.False(range_Nulls_1.Equals(null));

            // Null-safe GetHashCode
            int int_Hash_1 = range_Nulls_1.GetHashCode();
            int int_Hash_2 = range_Nulls_2.GetHashCode();
            Assert.Equal(int_Hash_1, int_Hash_2);

            int int_Hash_3 = range_Mixed_1.GetHashCode();
            int int_Hash_4 = range_Mixed_2.GetHashCode();
            Assert.Equal(int_Hash_3, int_Hash_4);
        }

        /// <summary>
        /// Tests the BoundedIndex extension methods for Range and count overloads.
        /// </summary>
        [Fact]
        public void BoundedIndex()
        {
            Range<int> range_Bound = new(0, 2); // 0, 1, 2 (count = 3)

            // Index inside bounds
            Assert.Equal(0, range_Bound.BoundedIndex(0));
            Assert.Equal(1, range_Bound.BoundedIndex(1));
            Assert.Equal(2, range_Bound.BoundedIndex(2));

            // Underflow wrap-around
            Assert.Equal(2, range_Bound.BoundedIndex(-1));
            Assert.Equal(1, range_Bound.BoundedIndex(-2));
            Assert.Equal(0, range_Bound.BoundedIndex(-3));
            Assert.Equal(2, range_Bound.BoundedIndex(-4));

            // Overflow wrap-around
            Assert.Equal(0, range_Bound.BoundedIndex(3));
            Assert.Equal(1, range_Bound.BoundedIndex(4));
            Assert.Equal(2, range_Bound.BoundedIndex(5));
            Assert.Equal(0, range_Bound.BoundedIndex(6));

            // Test count overload
            int count = 3;
            Assert.Equal(0, count.BoundedIndex(0));
            Assert.Equal(2, count.BoundedIndex(-1));
            Assert.Equal(0, count.BoundedIndex(3));

            // Test null range safety
            Range<int>? range_Null = null;
            Assert.Equal(int.MinValue, range_Null.BoundedIndex(5));

            // Test invalid count safety
            int invalidCount = 0;
            Assert.Equal(int.MinValue, invalidCount.BoundedIndex(5));
        }
    }
}