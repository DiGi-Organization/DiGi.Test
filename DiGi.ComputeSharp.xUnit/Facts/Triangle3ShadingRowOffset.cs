using ComputeSharp;
using DiGi.ComputeSharp.Core.Constants;
using DiGi.ComputeSharp.Spatial.Classes;
using Bool = DiGi.ComputeSharp.Core.Classes.Bool;

namespace DiGi.ComputeSharp.xUnit
{
    /// <summary>
    /// Contains unit tests for <see cref="Triangle3ShadingRowOffsetComputeShader"/>.
    /// </summary>
    public partial class Facts
    {
        /// <summary>
        /// Verifies that row-offset dispatches of the shading shader reproduce the single full dispatch:
        /// for a seeded random triangle set and a fixed direction, the concatenation of blocked dispatches
        /// (block sizes 1, 7 and N, with a partial last block for size 7) equals, cell by cell, the output
        /// of one full dispatch (rowOffset = 0) through the same shader, and the out-of-range guard leaves
        /// the sentinel pre-fill of the rows beyond the block's valid range untouched.
        /// <para>The blocked reference is this shader's own full dispatch, so that part isolates the row-offset mechanics
        /// (block slicing, row stride and offset indexing) from the intersection computation. Separately, the full dispatch
        /// must equal <see cref="Triangle3ShadingComputeShader"/> cell for cell for an axis-aligned and an oblique direction
        /// and two tolerances: the row-offset struct has its own constant buffer layout, and a layout the driver mishandles
        /// corrupts the results without failing (DiGi-Organization/DiGi.ComputeSharp#3).</para>
        /// Handles UnsupportedDoubleOperationException gracefully for FP64 unsupported GPUs.
        /// </summary>
        [Fact]
        public void Triangle3ShadingRowOffset()
        {
            if (!Query.IsComputeSharpSupported(testOutputHelper))
            {
                return;
            }

            double tolerance = Tolerance.Distance;
            Coordinate3 vector = new(0, 0, 1);
            int count = 23;

            Random random = new(42);

            Triangle3 CreateRandomTriangle(double xMax, double zMin, double zMax)
            {
                Coordinate3 coordinate3_1 = new(random.NextDouble() * xMax, random.NextDouble() * xMax, zMin + (random.NextDouble() * (zMax - zMin)));
                Coordinate3 coordinate3_2 = new(random.NextDouble() * xMax, random.NextDouble() * xMax, zMin + (random.NextDouble() * (zMax - zMin)));
                Coordinate3 coordinate3_3 = new(random.NextDouble() * xMax, random.NextDouble() * xMax, zMin + (random.NextDouble() * (zMax - zMin)));
                return new Triangle3(new Bool(true), coordinate3_1, coordinate3_2, coordinate3_3);
            }

            bool AreEqual(Triangle3Intersection expected, Triangle3Intersection actual)
            {
                return expected.Point_1.Equals(actual.Point_1)
                    && expected.Point_2.Equals(actual.Point_2)
                    && expected.Point_3.Equals(actual.Point_3)
                    && expected.Point_4.Equals(actual.Point_4)
                    && expected.Point_5.Equals(actual.Point_5)
                    && expected.Point_6.Equals(actual.Point_6)
                    && expected.Solid.Value == actual.Solid.Value;
            }

            Triangle3[] triangles = new Triangle3[count];
            for (int i = 0; i < count; i++)
            {
                triangles[i] = CreateRandomTriangle(30.0, 0.5, 1.5);
            }

            try
            {
                GraphicsDevice graphicsDevice = GraphicsDevice.GetDefault();

                using ReadOnlyBuffer<Triangle3> trianglesBuffer = graphicsDevice.AllocateReadOnlyBuffer(triangles);

                // Reference: one full dispatch through the same shader (rowOffset = 0, full width).
                using ReadWriteBuffer<Triangle3Intersection> fullBuffer = graphicsDevice.AllocateReadWriteBuffer<Triangle3Intersection>(count * count);
                graphicsDevice.For(count, count, new Triangle3ShadingRowOffsetComputeShader(trianglesBuffer, fullBuffer, vector, 0, tolerance));
                List<Triangle3Intersection>? fullResults = Core.Create.List(fullBuffer);
                Assert.NotNull(fullResults);

                // Sanity: the scenario must contain both hits and misses, otherwise the comparison is vacuous.
                int nonNaN = 0;
                for (int i = 0; i < fullResults!.Count; i++)
                {
                    if (!fullResults[i].IsNaN())
                    {
                        nonNaN++;
                    }
                }

                Assert.True(nonNaN > 0 && nonNaN < fullResults.Count, "Expected a mix of intersecting and non-intersecting cells.");

                // Parity with the existing struct, which pins the constant buffer layout (vector and tolerance must be read correctly).
                double length = Math.Sqrt((0.3 * 0.3) + (0.2 * 0.2) + 1.0);
                Coordinate3[] parityVectors = [vector, new(0.3 / length, -0.2 / length, -1.0 / length)];
                double[] parityTolerances = [tolerance, 1e-3];
                foreach (Coordinate3 parityVector in parityVectors)
                {
                    foreach (double parityTolerance in parityTolerances)
                    {
                        using ReadWriteBuffer<Triangle3Intersection> oldBuffer = graphicsDevice.AllocateReadWriteBuffer<Triangle3Intersection>(count * count);
                        using ReadWriteBuffer<Triangle3Intersection> newBuffer = graphicsDevice.AllocateReadWriteBuffer<Triangle3Intersection>(count * count);
                        graphicsDevice.For(count, count, new Triangle3ShadingComputeShader(trianglesBuffer, oldBuffer, parityVector, parityTolerance));
                        graphicsDevice.For(count, count, new Triangle3ShadingRowOffsetComputeShader(trianglesBuffer, newBuffer, parityVector, 0, parityTolerance));
                        List<Triangle3Intersection>? oldResults = Core.Create.List(oldBuffer);
                        List<Triangle3Intersection>? newResults = Core.Create.List(newBuffer);
                        Assert.NotNull(oldResults);
                        Assert.NotNull(newResults);
                        Assert.Equal(oldResults!.Count, newResults!.Count);

                        for (int i = 0; i < oldResults.Count; i++)
                        {
                            Assert.True(AreEqual(oldResults[i], newResults[i]), $"Cell {i} (vector ({parityVector.X}, {parityVector.Y}, {parityVector.Z}), tolerance {parityTolerance}) differs from Triangle3ShadingComputeShader.");
                        }
                    }
                }

                // A negative offset must be refused rather than read out of bounds.
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                {
                    _ = new Triangle3ShadingRowOffsetComputeShader(trianglesBuffer, fullBuffer, vector, -1, tolerance);
                });

                Triangle3Intersection sentinel = new(new Bool(true), new Coordinate3(9999, 9999, 9999), new Coordinate3(9999, 9999, 9999), new Coordinate3(9999, 9999, 9999), new Coordinate3(9999, 9999, 9999), new Coordinate3(9999, 9999, 9999), new Coordinate3(9999, 9999, 9999));

                int[] blockSizes = [1, 7, count];
                foreach (int blockSize in blockSizes)
                {
                    for (int rowOffset = 0; rowOffset < count; rowOffset += blockSize)
                    {
                        int rowCount = Math.Min(blockSize, count - rowOffset);

                        // Dispatch a full block width over a sentinel pre-fill; the shader's guard must ignore
                        // the rows beyond rowCount so the partial last block is safe and verifiable.
                        Triangle3Intersection[] sentinelArray = new Triangle3Intersection[blockSize * count];
                        for (int i = 0; i < sentinelArray.Length; i++)
                        {
                            sentinelArray[i] = sentinel;
                        }

                        using ReadWriteBuffer<Triangle3Intersection> blockBuffer = graphicsDevice.AllocateReadWriteBuffer(sentinelArray);
                        graphicsDevice.For(blockSize, count, new Triangle3ShadingRowOffsetComputeShader(trianglesBuffer, blockBuffer, vector, rowOffset, tolerance));
                        List<Triangle3Intersection>? blockResults = Core.Create.List(blockBuffer);
                        Assert.NotNull(blockResults);

                        for (int row = 0; row < rowCount; row++)
                        {
                            for (int column = 0; column < count; column++)
                            {
                                Triangle3Intersection expected = fullResults[(rowOffset + row) * count + column];
                                Triangle3Intersection actual = blockResults![row * count + column];
                                Assert.True(AreEqual(expected, actual), $"Block (offset {rowOffset}, size {blockSize}) cell ({row}, {column}) differs from the full dispatch.");
                            }
                        }

                        for (int row = rowCount; row < blockSize; row++)
                        {
                            for (int column = 0; column < count; column++)
                            {
                                Triangle3Intersection untouched = blockResults![row * count + column];
                                Assert.True(AreEqual(sentinel, untouched), $"Block (offset {rowOffset}, size {blockSize}) row {row} was written despite rowOffset + row being out of range.");
                            }
                        }
                    }
                }
            }
            catch (Exception exception) when (exception.GetType().Name == "UnsupportedDoubleOperationException")
            {
                testOutputHelper.WriteLine("WARNING: GPU FP64 double-precision operations are not supported on this graphics card: " + exception.Message);
            }
        }
    }
}
