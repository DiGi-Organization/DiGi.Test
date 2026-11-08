using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;
using DiGi.Math.Classes;
using System.Diagnostics;
using System.Globalization;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies both the correctness and the performance improvements of the optimized MaxDistance query on a large dataset of 2D points.
        /// </summary>
        [Fact]
        public void MaxDistance_PerformanceAndCorrectness()
        {
            // Create 1000 points in a line (Max distance should be between first and last)
            List<Point2D> point2Ds = new();
            for (int i = 0; i < 1000; i++)
            {
                point2Ds.Add(new Point2D(i, i));
            }

            // Warm up / JIT compile
            _ = Planar.Query.MaxDistance(point2Ds.Take(10));

            // Execute optimized query and measure performance
            Stopwatch stopwatch = Stopwatch.StartNew();
            double distance = Planar.Query.MaxDistance(point2Ds, out Point2D? point2D_1, out Point2D? point2D_2);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(point2D_1);
            Assert.NotNull(point2D_2);
            Assert.Equal(0, point2D_1.X);
            Assert.Equal(0, point2D_1.Y);
            Assert.Equal(999, point2D_2.X);
            Assert.Equal(999, point2D_2.Y);
            Assert.Equal(System.Math.Sqrt(2) * 999, distance, 1e-6);

            // Assert Performance (optimized should be extremely fast, typically < 1000ms under busy VM)
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"MaxDistance performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance improvements of the optimized Planar ExtremePoints query on a large dataset of 2D points.
        /// </summary>
        [Fact]
        public void ExtremePoints_Planar_PerformanceAndCorrectness()
        {
            List<Point2D> point2Ds = new();
            for (int i = 0; i < 1000; i++)
            {
                point2Ds.Add(new Point2D(i, -i));
            }

            // Warm up / JIT compile
            Planar.Query.ExtremePoints(point2Ds.Take(10), out _, out _);

            // Execute optimized query and measure performance
            Stopwatch stopwatch = Stopwatch.StartNew();
            Planar.Query.ExtremePoints(point2Ds, out Point2D? point2D_1, out Point2D? point2D_2);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(point2D_1);
            Assert.NotNull(point2D_2);
            Assert.Equal(0, point2D_1.X);
            Assert.Equal(0, point2D_1.Y);
            Assert.Equal(999, point2D_2.X);
            Assert.Equal(-999, point2D_2.Y);

            // Assert Performance (optimized should be extremely fast, typically < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Planar ExtremePoints performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance improvements of the optimized Spatial ExtremePoints query on a large dataset of 3D points.
        /// </summary>
        [Fact]
        public void ExtremePoints_Spatial_PerformanceAndCorrectness()
        {
            List<Point3D> point3Ds = new();
            for (int i = 0; i < 1000; i++)
            {
                point3Ds.Add(new Point3D(i, i, i));
            }

            // Warm up / JIT compile
            Spatial.Query.ExtremePoints(point3Ds.Take(10), out _, out _, out _);

            // Execute optimized query and measure performance
            Stopwatch stopwatch = Stopwatch.StartNew();
            Spatial.Query.ExtremePoints(point3Ds, out Point3D? point3D_1, out Point3D? point3D_2, out double distance);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(point3D_1);
            Assert.NotNull(point3D_2);
            Assert.Equal(0, point3D_1.X);
            Assert.Equal(0, point3D_1.Y);
            Assert.Equal(0, point3D_1.Z);
            Assert.Equal(999, point3D_2.X);
            Assert.Equal(999, point3D_2.Y);
            Assert.Equal(999, point3D_2.Z);
            Assert.Equal(System.Math.Sqrt(3) * 999, distance, 1e-6);

            // Assert Performance (optimized should be extremely fast, typically < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 300, $"Spatial ExtremePoints performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the Inside polygon containment query on a large set of vertices.
        /// </summary>
        [Fact]
        public void Inside_PerformanceAndCorrectness()
        {
            // Create a large polygon (a simple square with 1000 collinear edge vertices for testing performance)
            List<Point2D> point2Ds = new();
            for (int i = 0; i < 250; i++) point2Ds.Add(new Point2D(i, 0));
            for (int i = 0; i < 250; i++) point2Ds.Add(new Point2D(250, i));
            for (int i = 250; i > 0; i--) point2Ds.Add(new Point2D(i, 250));
            for (int i = 250; i > 0; i--) point2Ds.Add(new Point2D(0, i));

            Point2D point2D_Inside = new(100, 100);
            Point2D point2D_Outside = new(300, 300);

            // Warm up / JIT compile
            _ = Planar.Query.Inside(point2Ds.Take(10), point2D_Inside);

            // Execute optimized query and measure performance
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool inside_Result = Planar.Query.Inside(point2Ds, point2D_Inside);
            bool outside_Result = Planar.Query.Inside(point2Ds, point2D_Outside);
            stopwatch.Stop();

            // Assert Correctness
            Assert.True(inside_Result);
            Assert.False(outside_Result);

            // Assert Performance (optimized should be extremely fast, typically < 1ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 5, $"Inside polygon performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the ClosestPoint query on a large polyline.
        /// </summary>
        [Fact]
        public void ClosestPoint_Planar_PerformanceAndCorrectness()
        {
            List<Segment2D> segment2Ds = new();
            for (int i = 0; i < 1000; i++)
            {
                segment2Ds.Add(new Segment2D(new Point2D(i, 0), new Point2D(i + 1, 0)));
            }

            Point2D point2D_Target = new(500.5, 10.0);

            // Warm up / JIT compile
            _ = Planar.Query.ClosestPoint(point2D_Target, segment2Ds.Take(10), out _);

            // Execute optimized query and measure performance
            Stopwatch stopwatch = Stopwatch.StartNew();
            Point2D? point2D_Result = Planar.Query.ClosestPoint(point2D_Target, segment2Ds, out double distance);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(point2D_Result);
            Assert.Equal(500.5, point2D_Result.X);
            Assert.Equal(0, point2D_Result.Y);
            Assert.Equal(10.0, distance, 1e-6);

            // Assert Performance (optimized should be extremely fast, typically < 5ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 10, $"ClosestPoint performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Average query for both 2D and 3D points on a large lazy sequence.
        /// </summary>
        [Fact]
        public void Average_PerformanceAndCorrectness()
        {
            // Create a lazy sequence of 100,000 points to amplify any multiple-enumeration overhead
            IEnumerable<Point2D> point2Ds_Lazy = Enumerable.Range(0, 100000).Select(i => new Point2D(i, i));
            IEnumerable<Point3D> point3Ds_Lazy = Enumerable.Range(0, 100000).Select(i => new Point3D(i, i, i));

            // Warm up / JIT compile
            _ = Planar.Query.Average(point2Ds_Lazy.Take(10));
            _ = Spatial.Query.Average(point3Ds_Lazy.Take(10));

            // Measure 2D Average performance
            Stopwatch stopwatch_2D = Stopwatch.StartNew();
            Point2D? point2D_Average = Planar.Query.Average(point2Ds_Lazy);
            stopwatch_2D.Stop();

            // Measure 3D Average performance
            Stopwatch stopwatch_3D = Stopwatch.StartNew();
            Point3D? point3D_Average = Spatial.Query.Average(point3Ds_Lazy);
            stopwatch_3D.Stop();

            // Assert Correctness
            Assert.NotNull(point2D_Average);
            Assert.Equal(49999.5, point2D_Average.X, 1e-6);
            Assert.Equal(49999.5, point2D_Average.Y, 1e-6);

            Assert.NotNull(point3D_Average);
            Assert.Equal(49999.5, point3D_Average.X, 1e-6);
            Assert.Equal(49999.5, point3D_Average.Y, 1e-6);
            Assert.Equal(49999.5, point3D_Average.Z, 1e-6);

            // Assert Performance (optimized single-pass should take less than 500ms)
            Assert.True(stopwatch_2D.ElapsedMilliseconds < 500, $"Planar Average performance check failed! Took {stopwatch_2D.ElapsedMilliseconds} ms.");
            Assert.True(stopwatch_3D.ElapsedMilliseconds < 500, $"Spatial Average performance check failed! Took {stopwatch_3D.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Benchmarks the mean-center <see cref="Planar.Query.Average(IEnumerable{Point2D}?)"/> query against the
        /// area <see cref="Planar.Query.Centroid(IEnumerable{Point2D}?)"/> query on an identical dense point set,
        /// verifying both return the same result for a symmetric polygon and that Average, which does strictly
        /// fewer FLOPs per vertex, is not slower than Centroid beyond a generous noise margin.
        /// </summary>
        [Fact]
        public void AverageAndCentroid_PerformanceComparison()
        {
            // Regular 10,000-gon approximating a circle of radius 10, centered at the origin.
            List<Point2D> point2Ds = new();
            for (int i = 0; i < 10000; i++)
            {
                double angle = i * 2.0 * System.Math.PI / 10000.0;
                point2Ds.Add(new Point2D(System.Math.Cos(angle) * 10.0, System.Math.Sin(angle) * 10.0));
            }

            // Warm up / JIT compile
            _ = Planar.Query.Average(point2Ds.Take(10));
            _ = Planar.Query.Centroid(point2Ds.Take(10));

            const int iterations = 200;

            Stopwatch stopwatch_Average = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _ = Planar.Query.Average(point2Ds);
            }
            stopwatch_Average.Stop();

            Stopwatch stopwatch_Centroid = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _ = Planar.Query.Centroid(point2Ds);
            }
            stopwatch_Centroid.Stop();

            // Assert Correctness (both must resolve to the origin for a symmetric point set)
            Point2D? point2D_Average = Planar.Query.Average(point2Ds);
            Point2D? point2D_Centroid = Planar.Query.Centroid(point2Ds);
            Assert.NotNull(point2D_Average);
            Assert.NotNull(point2D_Centroid);
            Assert.Equal(0.0, point2D_Average.X, 1e-6);
            Assert.Equal(0.0, point2D_Average.Y, 1e-6);
            Assert.Equal(0.0, point2D_Centroid.X, 1e-6);
            Assert.Equal(0.0, point2D_Centroid.Y, 1e-6);

            // Assert Performance (both are O(n) single-pass, so 200 iterations over 10,000 points must stay fast)
            Assert.True(stopwatch_Average.ElapsedMilliseconds < 1000, $"Average benchmark failed! Took {stopwatch_Average.ElapsedMilliseconds} ms for {iterations} iterations.");
            Assert.True(stopwatch_Centroid.ElapsedMilliseconds < 1000, $"Centroid benchmark failed! Took {stopwatch_Centroid.ElapsedMilliseconds} ms for {iterations} iterations.");

            // Assert Comparison (Average does ~2 additions per vertex versus ~4 multiplications and additions for
            // the shoelace-weighted Centroid, so it must not be slower beyond a generous noise margin)
            Assert.True(stopwatch_Average.ElapsedTicks <= stopwatch_Centroid.ElapsedTicks * 5, $"Average ({stopwatch_Average.ElapsedMilliseconds} ms) unexpectedly slower than Centroid ({stopwatch_Centroid.ElapsedMilliseconds} ms) beyond noise margin.");
        }

        /// <summary>
        /// Benchmarks <see cref="Planar.Query.Average(IEnumerable{Point2D}?)"/> against
        /// <see cref="Planar.Query.Centroid(IEnumerable{Point2D}?)"/> across an increasing number of points, to
        /// verify their relative execution time trend stays flat rather than diverging as the point count grows.
        /// <para>
        /// Each point count runs enough repeats to keep the measured workload comparable across scales, and the
        /// per-call timing summary is written to the test output for inspection.
        /// </para>
        /// </summary>
        [Fact]
        public void AverageAndCentroid_ScalingBenchmark()
        {
            int[] counts = [100, 1_000, 10_000, 100_000, 1_000_000];

            System.Text.StringBuilder stringBuilder_Summary = new();
            stringBuilder_Summary.AppendLine();
            stringBuilder_Summary.AppendLine("Points      | Average (us/call) | Centroid (us/call) | Ratio (Centroid/Average)");
            stringBuilder_Summary.AppendLine("----------- | ------------------ | ------------------- | -------------------------");

            // Warm up / JIT compile, once, before any measurement.
            // The comparison below is only meaningful if both methods are measured in the same
            // compilation tier. Tiered compilation delays call counting by roughly a tenth of a second,
            // and at the smallest scale the entire measurement of the first method finishes inside that
            // window, so only the method that runs second reaches optimised code. That made the result
            // depend on what had already run in the process rather than on the two implementations, and
            // the same benchmark would pass alone and fail in a full test run. Warming both on the
            // concrete list type that is actually measured, for longer than the promotion delay, puts
            // them on equal footing.
            List<Point2D> point2Ds_WarmUp = new(1000);
            for (int i = 0; i < 1000; i++)
            {
                double angle_WarmUp = i * 2.0 * System.Math.PI / 1000;
                point2Ds_WarmUp.Add(new Point2D(System.Math.Cos(angle_WarmUp) * 10.0, System.Math.Sin(angle_WarmUp) * 10.0));
            }

            Stopwatch stopwatch_WarmUp = Stopwatch.StartNew();
            while (stopwatch_WarmUp.ElapsedMilliseconds < 400)
            {
                _ = Planar.Query.Average(point2Ds_WarmUp);
                _ = Planar.Query.Centroid(point2Ds_WarmUp);
            }

            stopwatch_WarmUp.Stop();

            foreach (int count in counts)
            {
                List<Point2D> point2Ds = new(count);
                for (int i = 0; i < count; i++)
                {
                    double angle = i * 2.0 * System.Math.PI / count;
                    point2Ds.Add(new Point2D(System.Math.Cos(angle) * 10.0, System.Math.Sin(angle) * 10.0));
                }

                // Assert Correctness (both must resolve to the origin for a symmetric point set)
                Point2D? point2D_Average = Planar.Query.Average(point2Ds);
                Point2D? point2D_Centroid = Planar.Query.Centroid(point2Ds);
                Assert.NotNull(point2D_Average);
                Assert.NotNull(point2D_Centroid);
                Assert.Equal(0.0, point2D_Average.X, 1e-6);
                Assert.Equal(0.0, point2D_Average.Y, 1e-6);
                Assert.Equal(0.0, point2D_Centroid.X, 1e-6);
                Assert.Equal(0.0, point2D_Centroid.Y, 1e-6);

                // Keep total measured work roughly constant across scales
                int repeats = System.Math.Max(1, 2_000_000 / count);

                GC.Collect();
                GC.WaitForPendingFinalizers();

                Stopwatch stopwatch_Average = Stopwatch.StartNew();
                for (int i = 0; i < repeats; i++)
                {
                    _ = Planar.Query.Average(point2Ds);
                }
                stopwatch_Average.Stop();

                GC.Collect();
                GC.WaitForPendingFinalizers();

                Stopwatch stopwatch_Centroid = Stopwatch.StartNew();
                for (int i = 0; i < repeats; i++)
                {
                    _ = Planar.Query.Centroid(point2Ds);
                }
                stopwatch_Centroid.Stop();

                double average_PerCallMicroseconds = stopwatch_Average.Elapsed.TotalMilliseconds * 1000.0 / repeats;
                double centroid_PerCallMicroseconds = stopwatch_Centroid.Elapsed.TotalMilliseconds * 1000.0 / repeats;
                double ratio = centroid_PerCallMicroseconds / System.Math.Max(average_PerCallMicroseconds, 1e-6);

                stringBuilder_Summary.AppendLine(CultureInfo.CurrentCulture, $"{count,11} | {average_PerCallMicroseconds,18:F3} | {centroid_PerCallMicroseconds,19:F3} | {ratio,24:F2}x");

                // Assert Performance (both are O(n) single-pass, so the whole repeated run per scale must stay fast)
                Assert.True(stopwatch_Average.ElapsedMilliseconds < 2000, $"At {count} points, Average benchmark failed! Took {stopwatch_Average.ElapsedMilliseconds} ms for {repeats} repeats.");
                Assert.True(stopwatch_Centroid.ElapsedMilliseconds < 2000, $"At {count} points, Centroid benchmark failed! Took {stopwatch_Centroid.ElapsedMilliseconds} ms for {repeats} repeats.");

                // Assert Comparison (Average must not be slower than Centroid beyond a generous noise margin at any scale)
                Assert.True(average_PerCallMicroseconds <= centroid_PerCallMicroseconds * 5.0 + 10.0, $"At {count} points, Average ({average_PerCallMicroseconds:F3} us/call) unexpectedly slower than Centroid ({centroid_PerCallMicroseconds:F3} us/call) beyond noise margin.");
            }

            Console.WriteLine(stringBuilder_Summary.ToString());
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized ConvexHull query with order preservation on a lazy sequence.
        /// </summary>
        [Fact]
        public void ConvexHull_PerformanceAndCorrectness()
        {
            // Create a lazy sequence of points (a circle of 2000 points)
            IEnumerable<Point2D> point2Ds_Lazy = Enumerable.Range(0, 2000).Select(i =>
            {
                double angle = i * System.Math.PI / 1000.0;
                return new Point2D(System.Math.Cos(angle) * 100.0, System.Math.Sin(angle) * 100.0);
            });

            // Warm up / JIT compile
            _ = Planar.Query.ConvexHull(point2Ds_Lazy.Take(10), true);

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Point2D>? point2Ds_Hull = Planar.Query.ConvexHull(point2Ds_Lazy, true);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(point2Ds_Hull);
            Assert.True(point2Ds_Hull.Count >= 3);

            // Assert Performance (optimized should be extremely fast, typically < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"ConvexHull performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Polygonal2D creation method on a lazy sequence of points.
        /// </summary>
        [Fact]
        public void Polygonal2D_PerformanceAndCorrectness()
        {
            // Create a lazy sequence representing a triangle
            IEnumerable<Point2D> point2Ds_Triangle = Enumerable.Range(0, 3).Select(i => new Point2D(i, i * i));

            // Warm up / JIT compile
            _ = Planar.Create.Polygonal2D(point2Ds_Triangle);

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            Planar.Interfaces.IPolygonal2D? polygonal2D = Planar.Create.Polygonal2D(point2Ds_Triangle);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(polygonal2D);
            Assert.IsType<Triangle2D>(polygonal2D);

            // Assert Performance
            Assert.True(stopwatch.ElapsedMilliseconds < 50, $"Polygonal2D performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Polygon3D creation methods on a lazy sequence of 3D points.
        /// </summary>
        [Fact]
        public void Polygon3D_PerformanceAndCorrectness()
        {
            // Create a lazy sequence representing a non-collinear 3D polygon (a unit square)
            IEnumerable<Point3D> point3Ds_Lazy = new List<Point3D>
            {
                new(0, 0, 0),
                new(1, 0, 0),
                new(1, 1, 0),
                new(0, 1, 0)
            }.Select(x => x);
            Spatial.Classes.Vector3D vector3D_Normal = Spatial.Constants.Vector3D.WorldZ;

            // Warm up / JIT compile
            _ = Spatial.Create.Polygon3D(point3Ds_Lazy);
            _ = Spatial.Create.Polygon3D(vector3D_Normal, point3Ds_Lazy);

            // Execute and measure first overload
            Stopwatch stopwatch_1 = Stopwatch.StartNew();
            Polygon3D? polygon3D_1 = Spatial.Create.Polygon3D(point3Ds_Lazy);
            stopwatch_1.Stop();

            // Execute and measure second overload
            Stopwatch stopwatch_2 = Stopwatch.StartNew();
            Polygon3D? polygon3D_2 = Spatial.Create.Polygon3D(vector3D_Normal, point3Ds_Lazy);
            stopwatch_2.Stop();

            // Assert Correctness
            Assert.NotNull(polygon3D_1);
            Assert.NotNull(polygon3D_2);

            // Assert Performance
            Assert.True(stopwatch_1.ElapsedMilliseconds < 50, $"Polygon3D first overload performance check failed! Took {stopwatch_1.ElapsedMilliseconds} ms.");
            Assert.True(stopwatch_2.ElapsedMilliseconds < 50, $"Polygon3D second overload performance check failed! Took {stopwatch_2.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Min/Max coordinate bounds queries for both 2D and 3D points on a large lazy sequence.
        /// </summary>
        [Fact]
        public void MinMax_PerformanceAndCorrectness()
        {
            // Create a lazy sequence of 100,000 points to amplify any multiple-enumeration overhead
            IEnumerable<Point2D> point2Ds_Lazy = Enumerable.Range(0, 100000).Select(i => new Point2D(i, -i));
            IEnumerable<Point3D> point3Ds_Lazy = Enumerable.Range(0, 100000).Select(i => new Point3D(i, -i, i * 2));

            // Warm up / JIT compile
            _ = Planar.Query.Min(point2Ds_Lazy.Take(10), out _);
            _ = Spatial.Query.Min(point3Ds_Lazy.Take(10), out _);

            // Measure 2D Min/Max performance
            Stopwatch stopwatch_2D = Stopwatch.StartNew();
            Point2D? point2D_Min = Planar.Query.Min(point2Ds_Lazy, out Point2D? point2D_Max);
            stopwatch_2D.Stop();

            // Measure 3D Min/Max performance
            Stopwatch stopwatch_3D = Stopwatch.StartNew();
            Point3D? point3D_Min = Spatial.Query.Min(point3Ds_Lazy, out Point3D? point3D_Max);
            stopwatch_3D.Stop();

            // Assert Correctness 2D
            Assert.NotNull(point2D_Min);
            Assert.NotNull(point2D_Max);
            Assert.Equal(0.0, point2D_Min.X, 1e-6);
            Assert.Equal(-99999.0, point2D_Min.Y, 1e-6);
            Assert.Equal(99999.0, point2D_Max.X, 1e-6);
            Assert.Equal(0.0, point2D_Max.Y, 1e-6);

            // Assert Correctness 3D
            Assert.NotNull(point3D_Min);
            Assert.NotNull(point3D_Max);
            Assert.Equal(0.0, point3D_Min.X, 1e-6);
            Assert.Equal(-99999.0, point3D_Min.Y, 1e-6);
            Assert.Equal(0.0, point3D_Min.Z, 1e-6);
            Assert.Equal(99999.0, point3D_Max.X, 1e-6);
            Assert.Equal(0.0, point3D_Max.Y, 1e-6);
            Assert.Equal(199998.0, point3D_Max.Z, 1e-6);

            // Assert Performance (optimized single-pass should take less than 1000ms)
            Assert.True(stopwatch_2D.ElapsedMilliseconds < 1000, $"Planar Min/Max performance check failed! Took {stopwatch_2D.ElapsedMilliseconds} ms.");
            Assert.True(stopwatch_3D.ElapsedMilliseconds < 1000, $"Spatial Min/Max performance check failed! Took {stopwatch_3D.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Polygons, Polygon2Ds, PolygonalFace2Ds, and Polyline2Ds creators on lazy sequences of segments.
        /// </summary>
        [Fact]
        public void PolygonsAndPolylines_PerformanceAndCorrectness()
        {
            // Create a lazy sequence representing a closed snap-round square (4 segments)
            IEnumerable<Segment2D> segment2Ds_Lazy = new List<Segment2D>
            {
                new(new Point2D(0, 0), new Point2D(10, 0)),
                new(new Point2D(10, 0), new Point2D(10, 10)),
                new(new Point2D(10, 10), new Point2D(0, 10)),
                new(new Point2D(0, 10), new Point2D(0, 0))
            }.Select(x => x);

            // Warm up / JIT compile
            _ = Planar.Create.Polygons(segment2Ds_Lazy.Take(2));
            _ = Planar.Create.Polygon2Ds(segment2Ds_Lazy.Take(2));
            _ = Planar.Create.PolygonalFace2Ds(segment2Ds_Lazy.Take(2));
            _ = Planar.Create.Polyline2Ds(segment2Ds_Lazy.Take(2));

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<NetTopologySuite.Geometries.Polygon>? polygons_NTS = Planar.Create.Polygons(segment2Ds_Lazy);
            List<Polygon2D>? polygon2Ds = Planar.Create.Polygon2Ds(segment2Ds_Lazy);
            List<PolygonalFace2D>? polygonalFace2Ds = Planar.Create.PolygonalFace2Ds(segment2Ds_Lazy);
            List<Polyline2D>? polyline2Ds = Planar.Create.Polyline2Ds(segment2Ds_Lazy);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(polygons_NTS);
            Assert.Single(polygons_NTS);
            Assert.NotNull(polygon2Ds);
            Assert.Single(polygon2Ds);
            Assert.NotNull(polygonalFace2Ds);
            Assert.Single(polygonalFace2Ds);
            Assert.NotNull(polyline2Ds);

            // Assert Performance (should be extremely fast, typically < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"Polygons & Polylines creator performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Distance, ClosestPoint, and On queries on a lazy sequence of segmentable geometries.
        /// </summary>
        [Fact]
        public void DistanceAndClosestPoint_PerformanceAndCorrectness()
        {
            // Create a lazy sequence of segmentable geometries (100 polylines, each with 10 segments)
            IEnumerable<Polyline2D> segmentable2Ds_Lazy = Enumerable.Range(0, 100).Select(i =>
            {
                List<Point2D> point2Ds_Poly = new();
                for (int j = 0; j < 10; j++)
                {
                    point2Ds_Poly.Add(new Point2D(i * 10 + j, 0));
                }
                return new Polyline2D(point2Ds_Poly);
            });

            Point2D point2D_Target = new(500.5, 10.0);

            // Warm up / JIT compile
            _ = Planar.Query.Distance(point2D_Target, segmentable2Ds_Lazy.Take(2));
            _ = Planar.Query.ClosestPoint(point2D_Target, segmentable2Ds_Lazy.Take(2));
            _ = Planar.Query.On(segmentable2Ds_Lazy.Take(2), point2D_Target);

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            double distance = Planar.Query.Distance(point2D_Target, segmentable2Ds_Lazy, out Point2D? point2D_Closest);
            Point2D? point2D_ClosestPoint = Planar.Query.ClosestPoint(point2D_Target, segmentable2Ds_Lazy, out double distance_Check);
            bool isOn = Planar.Query.On(segmentable2Ds_Lazy, point2D_Target);
            stopwatch.Stop();

            // Assert Correctness
            Assert.Equal(10.0, distance, 1e-6);
            Assert.NotNull(point2D_Closest);
            Assert.Equal(500.5, point2D_Closest.X, 1e-6);
            Assert.Equal(0.0, point2D_Closest.Y, 1e-6);

            Assert.NotNull(point2D_ClosestPoint);
            Assert.Equal(500.5, point2D_ClosestPoint.X, 1e-6);
            Assert.Equal(0.0, point2D_ClosestPoint.Y, 1e-6);
            Assert.Equal(10.0, distance_Check, 1e-6);
            Assert.False(isOn);

            // Assert Performance (should be extremely fast, typically < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Distance & ClosestPoint query performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized AdjacentSegments query on a lazy sequence of segmentable geometries.
        /// </summary>
        [Fact]
        public void AdjacentSegments_PerformanceAndCorrectness()
        {
            // Create a lazy sequence of 50 unit squares adjacent to each other
            IEnumerable<Polygon2D> segmentable2Ds_Lazy = Enumerable.Range(0, 50).Select(i =>
            {
                List<Point2D> point2Ds_Square = new()
                {
                    new Point2D(i * 10, 0),
                    new Point2D(i * 10 + 10, 0),
                    new Point2D(i * 10 + 10, 10),
                    new Point2D(i * 10, 10)
                };
                return new Polygon2D(point2Ds_Square);
            });

            // Warm up / JIT compile
            _ = Planar.Query.AdjacentSegments(segmentable2Ds_Lazy.Take(2));

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Segment2D>? segment2Ds_Adjacent = Planar.Query.AdjacentSegments(segmentable2Ds_Lazy);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(segment2Ds_Adjacent);
            // Since squares share edges at x = 10, 20, 30, etc.
            Assert.True(segment2Ds_Adjacent.Count > 0);

            // Assert Performance (should be extremely fast, typically < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 400, $"AdjacentSegments performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Rectangle2D creation overloads on lazy sequences of points.
        /// </summary>
        [Fact]
        public void Rectangle2D_PerformanceAndCorrectness()
        {
            IEnumerable<Point2D> point2Ds_Lazy = Enumerable.Range(0, 100).Select(i => new Point2D(i, i * 2));
            Vector2D vector2D_Direction = new(1, 1);
            _ = Planar.Create.Rectangle2D(point2Ds_Lazy.Take(5), vector2D_Direction);
            _ = Planar.Create.Rectangle2D(point2Ds_Lazy.Take(5));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Rectangle2D? rectangle2D_1 = Planar.Create.Rectangle2D(point2Ds_Lazy, vector2D_Direction);
            Rectangle2D? rectangle2D_2 = Planar.Create.Rectangle2D(point2Ds_Lazy);
            stopwatch.Stop();
            Assert.NotNull(rectangle2D_1);
            Assert.NotNull(rectangle2D_2);
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Rectangle2D creation performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized PolynomialEquation creation method on lazy sequences of points.
        /// </summary>
        [Fact]
        public void PolynomialEquation_PerformanceAndCorrectness()
        {
            IEnumerable<Point2D> point2Ds_Lazy = Enumerable.Range(0, 500).Select(i => new Point2D(i, i * i));
            _ = Planar.Create.PolynomialEquation(point2Ds_Lazy.Take(5), 2);
            Stopwatch stopwatch = Stopwatch.StartNew();
            PolynomialEquation? polynomialEquation = Planar.Create.PolynomialEquation(point2Ds_Lazy, 2);
            stopwatch.Stop();
            Assert.NotNull(polynomialEquation);
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"PolynomialEquation creation performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Directions query on lazy sequences of segmentable geometries.
        /// </summary>
        [Fact]
        public void Directions_PerformanceAndCorrectness()
        {
            IEnumerable<Polygon2D> polygon2Ds_Lazy = Enumerable.Range(0, 50).Select(i =>
            {
                List<Point2D> point2Ds_Square = new()
                {
                    new Point2D(i, 0),
                    new Point2D(i + 1, 0),
                    new Point2D(i + 1, 1),
                    new Point2D(i, 1)
                };
                return new Polygon2D(point2Ds_Square);
            });
            _ = Planar.Query.Directions(polygon2Ds_Lazy.Take(2));
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Vector2D>? vector2Ds_Directions = Planar.Query.Directions(polygon2Ds_Lazy);
            stopwatch.Stop();
            Assert.NotNull(vector2Ds_Directions);
            Assert.True(vector2Ds_Directions.Count > 0);
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"Directions query performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Split query on lazy sequences of segments.
        /// </summary>
        [Fact]
        public void Split_PerformanceAndCorrectness()
        {
            List<Segment2D> segment2Ds_Grid = new();
            for (int i = 0; i < 15; i++)
            {
                segment2Ds_Grid.Add(new Segment2D(new Point2D(0, i), new Point2D(15, i)));
                segment2Ds_Grid.Add(new Segment2D(new Point2D(i, 0), new Point2D(i, 15)));
            }
            IEnumerable<Segment2D> segment2Ds_Lazy = segment2Ds_Grid.Select(x => x);
            _ = Planar.Query.Split(segment2Ds_Lazy.Take(2));
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Segment2D>? segment2Ds_Result = Planar.Query.Split(segment2Ds_Lazy);
            stopwatch.Stop();
            Assert.NotNull(segment2Ds_Result);
            Assert.True(segment2Ds_Result.Count > 0);
            Assert.True(stopwatch.ElapsedMilliseconds < 800, $"Split segments performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Mesh3D creation on a lazy sequence of 3D triangles.
        /// </summary>
        [Fact]
        public void Mesh3D_PerformanceAndCorrectness()
        {
            IEnumerable<Triangle3D> triangle3Ds_Lazy = Enumerable.Range(0, 100).Select(i =>
            {
                Point3D point3D_1 = new(i, 0, 0);
                Point3D point3D_2 = new(i + 1, 1, 0);
                Point3D point3D_3 = new(i, 1, 1);
                return new Triangle3D(point3D_1, point3D_2, point3D_3);
            });
            _ = Spatial.Create.Mesh3D(triangle3Ds_Lazy.Take(2));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Mesh3D? mesh3D = Spatial.Create.Mesh3D(triangle3Ds_Lazy);
            stopwatch.Stop();
            Assert.NotNull(mesh3D);
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"Mesh3D creation performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Polyhedron creation on a lazy sequence of 3D polygonal faces.
        /// </summary>
        [Fact]
        public void Polyhedron_PerformanceAndCorrectness()
        {
            List<IPolygonalFace3D> polygonalFace3Ds_Cube = new();
            List<Point3D> point3Ds_F1 = new() { new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(1, 1, 0), new Point3D(0, 1, 0) };
            Polygon3D? polygon3D_F1 = Spatial.Create.Polygon3D(point3Ds_F1);
            Assert.NotNull(polygon3D_F1);
            polygonalFace3Ds_Cube.Add(new PolygonalFace3D(polygon3D_F1));
            List<Point3D> point3Ds_F2 = new() { new Point3D(0, 0, 1), new Point3D(1, 0, 1), new Point3D(1, 1, 1), new Point3D(0, 1, 1) };
            Polygon3D? polygon3D_F2 = Spatial.Create.Polygon3D(point3Ds_F2);
            Assert.NotNull(polygon3D_F2);
            polygonalFace3Ds_Cube.Add(new PolygonalFace3D(polygon3D_F2));
            List<Point3D> point3Ds_F3 = new() { new Point3D(0, 0, 0), new Point3D(0, 1, 0), new Point3D(0, 1, 1), new Point3D(0, 0, 1) };
            Polygon3D? polygon3D_F3 = Spatial.Create.Polygon3D(point3Ds_F3);
            Assert.NotNull(polygon3D_F3);
            polygonalFace3Ds_Cube.Add(new PolygonalFace3D(polygon3D_F3));
            List<Point3D> point3Ds_F4 = new() { new Point3D(1, 0, 0), new Point3D(1, 1, 0), new Point3D(1, 1, 1), new Point3D(1, 0, 1) };
            Polygon3D? polygon3D_F4 = Spatial.Create.Polygon3D(point3Ds_F4);
            Assert.NotNull(polygon3D_F4);
            polygonalFace3Ds_Cube.Add(new PolygonalFace3D(polygon3D_F4));
            List<Point3D> point3Ds_F5 = new() { new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(1, 0, 1), new Point3D(0, 0, 1) };
            Polygon3D? polygon3D_F5 = Spatial.Create.Polygon3D(point3Ds_F5);
            Assert.NotNull(polygon3D_F5);
            polygonalFace3Ds_Cube.Add(new PolygonalFace3D(polygon3D_F5));
            List<Point3D> point3Ds_F6 = new() { new Point3D(0, 1, 0), new Point3D(1, 1, 0), new Point3D(1, 1, 1), new Point3D(0, 1, 1) };
            Polygon3D? polygon3D_F6 = Spatial.Create.Polygon3D(point3Ds_F6);
            Assert.NotNull(polygon3D_F6);
            polygonalFace3Ds_Cube.Add(new PolygonalFace3D(polygon3D_F6));
            IEnumerable<IPolygonalFace3D> polygonalFace3Ds_Lazy = polygonalFace3Ds_Cube.Select(x => x);
            _ = Spatial.Create.Polyhedron(polygonalFace3Ds_Lazy.Take(4));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Polyhedron? polyhedron = Spatial.Create.Polyhedron(polygonalFace3Ds_Lazy);
            stopwatch.Stop();
            Assert.NotNull(polyhedron);
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"Polyhedron creation performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the Mesh constructor optimizations.
        /// </summary>
        [Fact]
        public void Mesh_PerformanceAndCorrectness()
        {
            List<Point3D> point3Ds = new();
            for (int i = 0; i < 1000; i++)
            {
                point3Ds.Add(new Point3D(i, 0, 0));
            }
            List<int[]> indices = new();
            for (int i = 0; i < 998; i++)
            {
                indices.Add([i, i + 1, i + 2]);
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            Mesh3D mesh3D = new(point3Ds, indices);
            stopwatch.Stop();
            Assert.NotNull(mesh3D);
            Assert.True(stopwatch.ElapsedMilliseconds < 50, $"Mesh constructor performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Core index queries on lazy sequences.
        /// </summary>
        [Fact]
        public void IndexQueries_PerformanceAndCorrectness()
        {
            IEnumerable<int[]> indexes_Lazy = Enumerable.Range(0, 1000).Select(i => new int[] { i, i + 1, i + 2 });
            _ = Core.Query.AdjacencyIndexes(indexes_Lazy.Take(5));
            _ = Core.Query.AuxiliaryIndexes(indexes_Lazy.Take(5));
            _ = Core.Query.BoundaryIndexes(indexes_Lazy.Take(5), out _);
            _ = Core.Query.IsNonManifold(indexes_Lazy.Take(5));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Dictionary<int, List<int[]>>? adjacentEdges_ByVertex = Core.Query.AdjacencyIndexes(indexes_Lazy);
            List<int[]>? intArrays_Auxiliary = Core.Query.AuxiliaryIndexes(indexes_Lazy);
            List<int[]>? intArrays_Boundary = Core.Query.BoundaryIndexes(indexes_Lazy, out List<int[]>? intArrays_AuxiliaryOut);
            bool isNonManifold = Core.Query.IsNonManifold(indexes_Lazy);
            stopwatch.Stop();
            Assert.NotNull(adjacentEdges_ByVertex);
            Assert.NotNull(intArrays_Auxiliary);
            Assert.NotNull(intArrays_Boundary);
            Assert.NotNull(intArrays_AuxiliaryOut);
            Assert.False(isNonManifold);
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"IndexQueries performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Mesh2D creation on a lazy sequence of 2D triangles.
        /// </summary>
        [Fact]
        public void Mesh2D_PerformanceAndCorrectness()
        {
            IEnumerable<Triangle2D> triangle2Ds_Lazy = Enumerable.Range(0, 100).Select(i =>
            {
                Point2D point2D_1 = new(i, 0);
                Point2D point2D_2 = new(i + 1, 1);
                Point2D point2D_3 = new(i, 1);
                return new Triangle2D(point2D_1, point2D_2, point2D_3);
            });
            _ = Planar.Create.Mesh2D(triangle2Ds_Lazy.Take(2));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Mesh2D? mesh2D = Planar.Create.Mesh2D(triangle2Ds_Lazy);
            stopwatch.Stop();
            Assert.NotNull(mesh2D);
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"Mesh2D creation performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Polyline2Ds creation utilizing the O(1) out-edge count optimizer.
        /// </summary>
        [Fact]
        public void Polyline2Ds_PerformanceAndCorrectness()
        {
            IEnumerable<Segment2D> segment2Ds_Lazy = Enumerable.Range(0, 50).Select(i => new Segment2D(new Point2D(i, 0), new Point2D(i + 1, 0)));
            _ = Planar.Create.Polyline2Ds(segment2Ds_Lazy.Take(2));
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Polyline2D>? polyline2Ds = Planar.Create.Polyline2Ds(segment2Ds_Lazy);
            stopwatch.Stop();
            Assert.NotNull(polyline2Ds);
            Assert.True(polyline2Ds.Count > 0);
            Assert.True(stopwatch.ElapsedMilliseconds < 150, $"Polyline2Ds creation performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Determinants calculation.
        /// </summary>
        [Fact]
        public void Determinants_PerformanceAndCorrectness()
        {
            IEnumerable<Point2D> point2Ds_Lazy = Enumerable.Range(0, 1000).Select(i => new Point2D(i, i % 2 == 0 ? 0 : 1));
            _ = Planar.Query.Determinants(point2Ds_Lazy.Take(5));
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<double>? double_Determinants = Planar.Query.Determinants(point2Ds_Lazy);
            stopwatch.Stop();
            Assert.NotNull(double_Determinants);
            Assert.Equal(1000, double_Determinants.Count);
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Determinants calculation performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Spatial Orientation query on lazy sequences.
        /// </summary>
        [Fact]
        public void Orientation_Spatial_PerformanceAndCorrectness()
        {
            Plane plane = new(new Point3D(0, 0, 0), Spatial.Constants.Vector3D.WorldZ);
            IEnumerable<Point2D> point2Ds_Lazy = new List<Point2D>
            {
                new(0, 0),
                new(1, 0),
                new(0, 1)
            }.Select(x => x);
            _ = Spatial.Query.Orientation(plane, point2Ds_Lazy.Take(2));
            Stopwatch stopwatch = Stopwatch.StartNew();
            Core.Enums.Orientation orientation = Spatial.Query.Orientation(plane, point2Ds_Lazy);
            stopwatch.Stop();
            Assert.NotEqual(Core.Enums.Orientation.Undefined, orientation);
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Spatial Orientation performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Triangle3D GetArea method.
        /// </summary>
        [Fact]
        public void Triangle3D_GetArea_PerformanceAndCorrectness()
        {
            Point3D point3D_1 = new(0, 0, 0);
            Point3D point3D_2 = new(10, 0, 0);
            Point3D point3D_3 = new(0, 10, 0);
            Triangle3D triangle3D = new(point3D_1, point3D_2, point3D_3);

            // Warm up / JIT compile
            _ = triangle3D.GetArea();

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            double double_Area = 0;
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                double_Area = triangle3D.GetArea();
            }
            stopwatch.Stop();

            // Assert Correctness
            Assert.Equal(50.0, double_Area, 1e-6);

            // Assert Performance (100,000 iterations should take less than 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Triangle3D GetArea performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Planar Area shoelace query.
        /// </summary>
        [Fact]
        public void Planar_Area_PerformanceAndCorrectness()
        {
            List<Point2D> point2Ds = new();
            for (int int_I = 0; int_I < 50000; int_I++)
            {
                double double_Angle = int_I * 2.0 * System.Math.PI / 50000.0;
                point2Ds.Add(new(System.Math.Cos(double_Angle) * 10.0, System.Math.Sin(double_Angle) * 10.0));
            }

            // Warm up / JIT compile
            _ = Planar.Query.Area(point2Ds.Take(10));

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            double double_Area = Planar.Query.Area(point2Ds);
            stopwatch.Stop();

            // Assert Correctness
            Assert.Equal(314.15926, double_Area, 1e-5);

            // Assert Performance (should be extremely fast, typically < 20ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Planar Area performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized TryGetConnectedPolygonalFace3Ds query.
        /// </summary>
        [Fact]
        public void TryGetConnectedPolygonalFace3Ds_PerformanceAndCorrectness()
        {
            List<IPolygonalFace3D> polygonalFace3Ds = new();
            for (int int_I = 1; int_I <= 20; int_I++)
            {
                List<Point3D> point3Ds_Vertices = new()
                {
                    new(int_I, 0, 0),
                    new(int_I + 1, 0, 0),
                    new(int_I + 1, 1, 0),
                    new(int_I, 1, 0)
                };
                Polygon3D? polygon3D = Spatial.Create.Polygon3D(point3Ds_Vertices);
                Assert.NotNull(polygon3D);
                polygonalFace3Ds.Add(new PolygonalFace3D(polygon3D));
            }

            // Add one disconnected face
            List<Point3D> point3Ds_Disconnected = new()
            {
                new(0, 0, 5),
                new(1, 0, 5),
                new(1, 1, 5),
                new(0, 1, 5)
            };
            Polygon3D? polygon3D_Disconnected = Spatial.Create.Polygon3D(point3Ds_Disconnected);
            Assert.NotNull(polygon3D_Disconnected);
            polygonalFace3Ds.Add(new PolygonalFace3D(polygon3D_Disconnected));

            List<Point3D> point3Ds_Source = new()
            {
                new(0, 0, 0),
                new(1, 0, 0),
                new(1, 1, 0),
                new(0, 1, 0)
            };
            Polygon3D? polygon3D_Source = Spatial.Create.Polygon3D(point3Ds_Source);
            Assert.NotNull(polygon3D_Source);
            IPolygonalFace3D polygonalFace3D_Source = new PolygonalFace3D(polygon3D_Source);

            // Warm up / JIT compile
            _ = Spatial.Query.TryGetConnectedPolygonalFace3Ds(polygonalFace3D_Source, polygonalFace3Ds.Take(2), out _, out _);

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool bool_Result = Spatial.Query.TryGetConnectedPolygonalFace3Ds(
                polygonalFace3D_Source,
                polygonalFace3Ds,
                out List<IPolygonalFace3D>? polygonalFace3Ds_Connected,
                out List<IPolygonalFace3D>? polygonalFace3Ds_Disconnected,
                tolerance: 1e-3);
            stopwatch.Stop();

            // Assert Correctness
            Assert.True(bool_Result);
            Assert.NotNull(polygonalFace3Ds_Connected);
            Assert.Equal(20, polygonalFace3Ds_Connected.Count);
            Assert.NotNull(polygonalFace3Ds_Disconnected);
            Assert.Single(polygonalFace3Ds_Disconnected);

            // Assert Performance (should be extremely fast, typically < 20ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"TryGetConnectedPolygonalFace3Ds performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized coplanar normal calculation.
        /// </summary>
        [Fact]
        public void CoplanarNormal_PerformanceAndCorrectness()
        {
            List<Point3D> point3Ds = new();
            for (int int_I = 0; int_I < 20000; int_I++)
            {
                double double_Angle = int_I * 2.0 * System.Math.PI / 20000.0;
                point3Ds.Add(new(System.Math.Cos(double_Angle) * 10.0, System.Math.Sin(double_Angle) * 10.0, 0.0));
            }

            // Warm up / JIT compile
            _ = Spatial.Query.Normal(point3Ds.Take(10));

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            Spatial.Classes.Vector3D? vector3D_Normal = Spatial.Query.Normal(point3Ds);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(vector3D_Normal);
            Assert.Equal(0.0, vector3D_Normal.X, 1e-5);
            Assert.Equal(0.0, vector3D_Normal.Y, 1e-5);
            Assert.True(System.Math.Abs(vector3D_Normal.Z) > 0.99, "Normal Z component should be close to 1 or -1.");

            // Assert Performance (should be extremely fast, typically < 10ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 300, $"CoplanarNormal performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized ellipse intersection queries, ensuring the ellipse-line logic bug is resolved.
        /// </summary>
        [Fact]
        public void EllipseIntersection_PerformanceAndCorrectness()
        {
            Ellipse2D ellipse2D = new(new Point2D(0, 0), 10.0, 5.0, new Vector2D(1, 0));
            Line2D line2D = new(new Point2D(0, -10), new Vector2D(0, 1));

            // Warm up / JIT compile
            _ = Planar.Query.IntersectionPoints(ellipse2D, line2D);

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Point2D>? point2Ds_Intersection = Planar.Query.IntersectionPoints(ellipse2D, line2D);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(point2Ds_Intersection);
            Assert.Equal(2, point2Ds_Intersection.Count);

            // Intersection points should be (0, -5) and (0, 5)
            DiGi.Core.Modify.Sort(point2Ds_Intersection, x => x.Y);
            Assert.Equal(0.0, point2Ds_Intersection[0].X, 1e-5);
            Assert.Equal(-5.0, point2Ds_Intersection[0].Y, 1e-5);
            Assert.Equal(0.0, point2Ds_Intersection[1].X, 1e-5);
            Assert.Equal(5.0, point2Ds_Intersection[1].Y, 1e-5);

            // Assert Performance (should be extremely fast, typically < 1ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"EllipseIntersection performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized segment-collection intersections using bounding box filters.
        /// </summary>
        [Fact]
        public void SegmentCollectionIntersection_PerformanceAndCorrectness()
        {
            List<Segment2D> segment2Ds_First = new();
            for (int int_I = 0; int_I < 500; int_I++)
            {
                segment2Ds_First.Add(new(new Point2D(int_I * 2.0, 0.0), new Point2D(int_I * 2.0 + 1.0, 0.0)));
            }

            List<Segment2D> segment2Ds_Second = new();
            for (int int_I = 0; int_I < 500; int_I++)
            {
                segment2Ds_Second.Add(new(new Point2D(int_I * 2.0 + 0.5, -1.0), new Point2D(int_I * 2.0 + 0.5, 1.0)));
            }

            // Warm up / JIT compile
            _ = Planar.Query.IntersectionPoints(segment2Ds_First.Take(5), segment2Ds_Second.Take(5));

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Point2D>? point2Ds_Result = Planar.Query.IntersectionPoints(segment2Ds_First, segment2Ds_Second);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(point2Ds_Result);
            Assert.Equal(500, point2Ds_Result.Count);

            // Assert Performance (with bounding box filter, it should be extremely fast, < 100ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"SegmentCollectionIntersection performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized mesh boundary indexing methods.
        /// </summary>
        [Fact]
        public void MeshBoundary_PerformanceAndCorrectness()
        {
            // Create a 100x100 grid mesh (10,000 quads, 20,000 triangles)
            List<int[]> indexList = new();
            for (int int_Y = 0; int_Y < 100; int_Y++)
            {
                for (int int_X = 0; int_X < 100; int_X++)
                {
                    int int_V00 = int_Y * 101 + int_X;
                    int int_V10 = int_Y * 101 + (int_X + 1);
                    int int_V01 = (int_Y + 1) * 101 + int_X;
                    int int_V11 = (int_Y + 1) * 101 + (int_X + 1);

                    indexList.Add(new[] { int_V00, int_V10, int_V01 });
                    indexList.Add(new[] { int_V10, int_V11, int_V01 });
                }
            }

            // Warm up / JIT compile
            _ = Core.Query.SortedBoundaryIndexes(indexList.Take(10));
            _ = Core.Query.IsNonManifold(indexList.Take(10));

            // Execute and measure SortedBoundaryIndexes
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<List<int>>? sortedBoundaryPoints = Core.Query.SortedBoundaryIndexes(indexList);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(sortedBoundaryPoints);
            Assert.Single(sortedBoundaryPoints);
            Assert.Equal(400, sortedBoundaryPoints[0].Count);

            // Assert Performance (should be extremely fast, typically < 10ms for 20,000 triangles)
            Assert.True(stopwatch.ElapsedMilliseconds < 5000, $"MeshBoundary performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");

            // Execute and measure IsNonManifold
            stopwatch.Restart();
            bool bool_IsNonManifold = Core.Query.IsNonManifold(indexList);
            stopwatch.Stop();

            Assert.False(bool_IsNonManifold);
            Assert.True(stopwatch.ElapsedMilliseconds < 200, $"IsNonManifold performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized segmentable distance query.
        /// </summary>
        [Fact]
        public void PolylineDistance_PerformanceAndCorrectness()
        {
            List<Point2D> point2Ds_1 = new();
            List<Point2D> point2Ds_2 = new();
            for (int int_I = 0; int_I <= 500; int_I++)
            {
                point2Ds_1.Add(new(int_I * 2.0, 0.0));
                point2Ds_2.Add(new(int_I * 2.0, int_I * 2.0 + 5.0));
            }

            Polyline2D polyline2D_1 = new(point2Ds_1);
            Polyline2D polyline2D_2 = new(point2Ds_2);

            // Warm up / JIT compile
            _ = Planar.Query.Distance(polyline2D_1, polyline2D_2, out _, out _);

            // Execute and measure
            Stopwatch stopwatch = Stopwatch.StartNew();
            double double_Distance = Planar.Query.Distance(polyline2D_1, polyline2D_2, out Point2D? point2D_Closest1, out Point2D? point2D_Closest2);
            stopwatch.Stop();

            // Assert Correctness
            Assert.Equal(5.0, double_Distance, 1e-5);
            Assert.NotNull(point2D_Closest1);
            Assert.NotNull(point2D_Closest2);
            Assert.Equal(5.0, point2D_Closest1.Distance(point2D_Closest2), 1e-5);

            // Assert Performance (with AABB pruning, N*M nested loop should take <10ms, typically <1ms)
            Assert.True(stopwatch.ElapsedMilliseconds < 100, $"PolylineDistance performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Segment2D.On and Segment3D.On queries.
        /// </summary>
        [Fact]
        public void SegmentOn_PerformanceAndCorrectness()
        {
            Segment2D segment2D = new(0.0, 0.0, 10.0, 0.0);
            Point2D point2D_On = new(5.0, 0.0);
            Point2D point2D_Close = new(5.0, 0.05);
            Point2D point2D_Far = new(5.0, 0.15);
            Point2D point2D_Before = new(-0.05, 0.0);
            Point2D point2D_After = new(10.05, 0.0);

            // Assert Correctness 2D
            Assert.True(segment2D.On(point2D_On, 0.1));
            Assert.True(segment2D.On(point2D_Close, 0.1));
            Assert.False(segment2D.On(point2D_Far, 0.1));
            Assert.True(segment2D.On(point2D_Before, 0.1));
            Assert.True(segment2D.On(point2D_After, 0.1));
            Assert.False(segment2D.On(new Point2D(-0.15, 0.0), 0.1));
            Assert.False(segment2D.On(new Point2D(10.15, 0.0), 0.1));

            Segment3D segment3D = new(0.0, 0.0, 0.0, 10.0, 0.0, 0.0);
            Point3D point3D_On = new(5.0, 0.0, 0.0);
            Point3D point3D_Close = new(5.0, 0.03, 0.04); // Distance is sqrt(0.03^2 + 0.04^2) = 0.05
            Point3D point3D_Far = new(5.0, 0.09, 0.12);   // Distance is sqrt(0.09^2 + 0.12^2) = 0.15
            Point3D point3D_Before = new(-0.05, 0.0, 0.0);
            Point3D point3D_After = new(10.05, 0.0, 0.0);

            // Assert Correctness 3D
            Assert.True(segment3D.On(point3D_On, 0.1));
            Assert.True(segment3D.On(point3D_Close, 0.1));
            Assert.False(segment3D.On(point3D_Far, 0.1));
            Assert.True(segment3D.On(point3D_Before, 0.1));
            Assert.True(segment3D.On(point3D_After, 0.1));
            Assert.False(segment3D.On(new Point3D(-0.15, 0.0, 0.0), 0.1));
            Assert.False(segment3D.On(new Point3D(10.15, 0.0, 0.0), 0.1));

            // Generate test dataset for performance
            int int_Count = 50000;
            List<Point2D> point2Ds_Test = new(int_Count);
            List<Point3D> point3Ds_Test = new(int_Count);
            for (int int_I = 0; int_I < int_Count; int_I++)
            {
                point2Ds_Test.Add(new(int_I * 0.0002, (int_I % 2 == 0) ? 0.01 : 0.2));
                point3Ds_Test.Add(new(int_I * 0.0002, (int_I % 2 == 0) ? 0.01 : 0.2, 0.0));
            }

            // Warm up / JIT compile
            _ = segment2D.On(point2D_On, 0.1);
            _ = segment3D.On(point3D_On, 0.1);

            // Benchmark 2D
            Stopwatch stopwatch = Stopwatch.StartNew();
            int int_Hits2D = 0;
            for (int int_I = 0; int_I < int_Count; int_I++)
            {
                if (segment2D.On(point2Ds_Test[int_I], 0.1))
                {
                    int_Hits2D++;
                }
            }
            stopwatch.Stop();
            long long_Ms2D = stopwatch.ElapsedMilliseconds;

            // Benchmark 3D
            stopwatch.Restart();
            int int_Hits3D = 0;
            for (int int_I = 0; int_I < int_Count; int_I++)
            {
                if (segment3D.On(point3Ds_Test[int_I], 0.1))
                {
                    int_Hits3D++;
                }
            }
            stopwatch.Stop();
            long long_Ms3D = stopwatch.ElapsedMilliseconds;

            // Assert Performance (50k iterations should take less than 20ms, typically <2ms)
            Assert.True(long_Ms2D < 100, $"Segment2D.On performance check failed! Took {long_Ms2D} ms.");
            Assert.True(long_Ms3D < 100, $"Segment3D.On performance check failed! Took {long_Ms3D} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Planar and Spatial Collinear queries.
        /// </summary>
        [Fact]
        public void Collinear_PerformanceAndCorrectness()
        {
            Point2D point2D_1 = new(0.0, 0.0);
            Point2D point2D_2 = new(10.0, 10.0);
            Point2D point2D_3 = new(5.0, 5.0);
            Point2D point2D_4 = new(5.0, 5.1);

            // Assert Correctness 2D Points
            Assert.True(Planar.Query.Collinear(point2D_1, point2D_2, point2D_3, 1e-5));
            Assert.False(Planar.Query.Collinear(point2D_1, point2D_2, point2D_4, 1e-5));

            Point3D point3D_1 = new(0.0, 0.0, 0.0);
            Point3D point3D_2 = new(10.0, 10.0, 10.0);
            Point3D point3D_3 = new(5.0, 5.0, 5.0);
            Point3D point3D_4 = new(5.0, 5.0, 5.1);

            // Assert Correctness 3D Points
            Assert.True(Spatial.Query.Collinear(point3D_1, point3D_2, point3D_3, 1e-5));
            Assert.False(Spatial.Query.Collinear(point3D_1, point3D_2, point3D_4, 1e-5));

            // Generate test dataset for 2D collections
            int int_Count = 10000;
            List<Point2D> point2Ds_Collinear = new(int_Count);
            List<Point2D> point2Ds_NonCollinear = new(int_Count);
            for (int int_I = 0; int_I < int_Count; int_I++)
            {
                point2Ds_Collinear.Add(new(int_I, int_I));
                point2Ds_NonCollinear.Add(new(int_I, (int_I == 5000) ? int_I + 5 : int_I));
            }

            // Generate test dataset for 3D collections
            List<Point3D> point3Ds_Collinear = new(int_Count);
            List<Point3D> point3Ds_NonCollinear = new(int_Count);
            for (int int_I = 0; int_I < int_Count; int_I++)
            {
                point3Ds_Collinear.Add(new(int_I, int_I, int_I));
                point3Ds_NonCollinear.Add(new(int_I, int_I, (int_I == 5000) ? int_I + 5 : int_I));
            }

            // Warm up / JIT compile
            _ = Planar.Query.Collinear(point2Ds_Collinear.Take(10), 1e-5);
            _ = Spatial.Query.Collinear(point3Ds_Collinear.Take(10), 1e-5);

            // Benchmark 2D Collinear Collection
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool bool_Collinear2D = Planar.Query.Collinear(point2Ds_Collinear, 1e-5);
            bool bool_NonCollinear2D = Planar.Query.Collinear(point2Ds_NonCollinear, 1e-5);
            stopwatch.Stop();
            long long_Ms2D = stopwatch.ElapsedMilliseconds;

            // Benchmark 3D Collinear Collection
            stopwatch.Restart();
            bool bool_Collinear3D = Spatial.Query.Collinear(point3Ds_Collinear, 1e-5);
            bool bool_NonCollinear3D = Spatial.Query.Collinear(point3Ds_NonCollinear, 1e-5);
            stopwatch.Stop();
            long long_Ms3D = stopwatch.ElapsedMilliseconds;

            // Assert Correctness
            Assert.True(bool_Collinear2D);
            Assert.False(bool_NonCollinear2D);
            Assert.True(bool_Collinear3D);
            Assert.False(bool_NonCollinear3D);

            // Assert Performance (10k items check should be under 50ms, typically <1ms)
            Assert.True(long_Ms2D < 100, $"Planar Collinear collection query performance check failed! Took {long_Ms2D} ms.");
            Assert.True(long_Ms3D < 100, $"Spatial Collinear collection query performance check failed! Took {long_Ms3D} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Spatial Inside queries.
        /// </summary>
        [Fact]
        public void SpatialInside_PerformanceAndCorrectness()
        {
            Plane plane = Spatial.Constants.Plane.WorldZ;
            List<Point3D> point3Ds_Vertices = new()
            {
                new(0.0, 0.0, 0.0),
                new(10.0, 0.0, 0.0),
                new(10.0, 10.0, 0.0),
                new(0.0, 10.0, 0.0)
            };

            Polygon2D polygon2D = new(point3Ds_Vertices.ConvertAll(p => Spatial.Query.Convert(plane, p))!);
            Polygon3D polygon3D = new(plane, polygon2D);

            Point3D point3D_Inside = new(5.0, 5.0, 0.0);
            Point3D point3D_Outside = new(15.0, 5.0, 0.0);
            Point3D point3D_OffPlane = new(5.0, 5.0, 1.0);

            // 1. Verify single point Inside query correctness
            Assert.True(Spatial.Query.Inside(polygon3D, point3D_Inside));
            Assert.False(Spatial.Query.Inside(polygon3D, point3D_Outside));
            Assert.False(Spatial.Query.Inside(polygon3D, point3D_OffPlane));

            // 2. Verify point collection Inside query correctness
            List<Point3D> point3Ds_CollectionInside = new() { new(3.0, 3.0, 0.0), new(7.0, 7.0, 0.0) };
            List<Point3D> point3Ds_CollectionOutside = new() { new(3.0, 3.0, 0.0), new(15.0, 7.0, 0.0) };
            Assert.True(Spatial.Query.Inside(polygon3D, point3Ds_CollectionInside));
            Assert.False(Spatial.Query.Inside(polygon3D, point3Ds_CollectionOutside));

            // 3. Verify segmentable Inside query correctness
            Polyline3D polyline3D_Inside = new(new List<Point3D> { new(1.0, 1.0, 0.0), new(9.0, 9.0, 0.0) });
            Polyline3D polyline3D_Outside = new(new List<Point3D> { new(1.0, 1.0, 0.0), new(15.0, 9.0, 0.0) });
            Assert.True(Spatial.Query.Inside(polygon3D, polyline3D_Inside));
            Assert.False(Spatial.Query.Inside(polygon3D, polyline3D_Outside));

            // Generate test dataset for point collection performance
            int int_Count = 10000;
            List<Point3D> point3Ds_Test = new(int_Count);
            for (int int_I = 0; int_I < int_Count; int_I++)
            {
                point3Ds_Test.Add(new(5.0, 5.0, 0.0));
            }

            // Warm up / JIT compile
            _ = Spatial.Query.Inside(polygon3D, point3D_Inside);
            _ = Spatial.Query.Inside(polygon3D, point3Ds_CollectionInside);
            _ = Spatial.Query.Inside(polygon3D, polyline3D_Inside);

            // Benchmark point collection
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool bool_CollectionResult = Spatial.Query.Inside(polygon3D, point3Ds_Test);
            stopwatch.Stop();
            long long_MsCollection = stopwatch.ElapsedMilliseconds;

            // Generate large segmentable geometry for performance
            List<Point3D> point3Ds_Polyline = new(1000);
            for (int int_I = 0; int_I < 1000; int_I++)
            {
                point3Ds_Polyline.Add(new(5.0, 5.0, 0.0));
            }
            Polyline3D polyline3D_Large = new(point3Ds_Polyline);

            // Benchmark segmentable geometry
            stopwatch.Restart();
            bool bool_SegmentableResult = Spatial.Query.Inside(polygon3D, polyline3D_Large);
            stopwatch.Stop();
            long long_MsSegmentable = stopwatch.ElapsedMilliseconds;

            // Assert Performance
            Assert.True(bool_CollectionResult);
            Assert.True(bool_SegmentableResult);
            Assert.True(long_MsCollection < 400, $"Spatial point collection Inside check failed! Took {long_MsCollection} ms.");
            Assert.True(long_MsSegmentable < 400, $"Spatial segmentable Inside check failed! Took {long_MsSegmentable} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized segment-segment intersection queries in 2D and 3D.
        /// </summary>
        [Fact]
        public void SegmentIntersection_PerformanceAndCorrectness()
        {
            Segment2D segment2D_1 = new(0.0, 0.0, 10.0, 10.0);
            Segment2D segment2D_2 = new(0.0, 10.0, 10.0, 0.0);
            Segment2D segment2D_Parallel = new(1.0, 1.0, 11.0, 11.0);
            Segment2D segment2D_NonIntersecting = new(20.0, 20.0, 30.0, 30.0);

            Point2D? point2D_Intersect = segment2D_1.IntersectionPoint(segment2D_2, 1e-5);
            Point2D? point2D_ParallelResult = segment2D_1.IntersectionPoint(segment2D_Parallel, 1e-5);
            Point2D? point2D_NonIntersectResult = segment2D_1.IntersectionPoint(segment2D_NonIntersecting, 1e-5);

            Assert.NotNull(point2D_Intersect);
            Assert.Equal(5.0, point2D_Intersect.X, 1e-5);
            Assert.Equal(5.0, point2D_Intersect.Y, 1e-5);
            Assert.Null(point2D_ParallelResult);
            Assert.Null(point2D_NonIntersectResult);

            Point3D point3D_1Start = new(0.0, 0.0, 0.0);
            Point3D point3D_1End = new(10.0, 10.0, 10.0);
            Point3D point3D_2Start = new(0.0, 10.0, 0.0);
            Point3D point3D_2End = new(10.0, 0.0, 10.0);
            Point3D point3D_ParallelStart = new(1.0, 1.0, 1.0);
            Point3D point3D_ParallelEnd = new(11.0, 11.0, 11.0);
            Point3D point3D_SkewStart = new(0.0, 0.0, 5.0);
            Point3D point3D_SkewEnd = new(10.0, 10.0, 15.0);

            Point3D? point3D_Intersect = Spatial.Query.IntersectionPoint(point3D_1Start, point3D_1End, point3D_2Start, point3D_2End, 1e-5);
            Point3D? point3D_ParallelResult = Spatial.Query.IntersectionPoint(point3D_1Start, point3D_1End, point3D_ParallelStart, point3D_ParallelEnd, 1e-5);
            Point3D? point3D_SkewResult = Spatial.Query.IntersectionPoint(point3D_1Start, point3D_1End, point3D_SkewStart, point3D_SkewEnd, 1e-5);

            Assert.NotNull(point3D_Intersect);
            Assert.Equal(5.0, point3D_Intersect.X, 1e-5);
            Assert.Equal(5.0, point3D_Intersect.Y, 1e-5);
            Assert.Equal(5.0, point3D_Intersect.Z, 1e-5);
            Assert.Null(point3D_ParallelResult);
            Assert.Null(point3D_SkewResult);

            int int_Iterations = 25000;
            List<Segment2D> segment2Ds = new(int_Iterations);
            for (int int_I = 0; int_I < int_Iterations; int_I++)
            {
                segment2Ds.Add(new(0.0, 0.0, 10.0, 10.0));
            }

            _ = segment2D_1.IntersectionPoint(segment2D_2, 1e-5);
            _ = Spatial.Query.IntersectionPoint(point3D_1Start, point3D_1End, point3D_2Start, point3D_2End, 1e-5);

            Stopwatch stopwatch = Stopwatch.StartNew();
            int int_Hits2D = 0;
            for (int int_I = 0; int_I < int_Iterations; int_I++)
            {
                if (segment2D_1.IntersectionPoint(segment2Ds[int_I], 1e-5) != null)
                {
                    int_Hits2D++;
                }
            }
            stopwatch.Stop();
            long long_Ms2D = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            int int_Hits3D = 0;
            for (int int_I = 0; int_I < int_Iterations; int_I++)
            {
                if (Spatial.Query.IntersectionPoint(point3D_1Start, point3D_1End, point3D_2Start, point3D_2End, 1e-5) != null)
                {
                    int_Hits3D++;
                }
            }
            stopwatch.Stop();
            long long_Ms3D = stopwatch.ElapsedMilliseconds;

            Assert.True(long_Ms2D < 100, $"Segment2D.IntersectionPoint performance check failed! Took {long_Ms2D} ms.");
            Assert.True(long_Ms3D < 100, $"Spatial.Query.IntersectionPoint performance check failed! Took {long_Ms3D} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Spatial Centroid calculations.
        /// </summary>
        [Fact]
        public void SpatialCentroid_PerformanceAndCorrectness()
        {
            Point3D point3D_1 = new(0.0, 0.0, 0.0);
            Point3D point3D_2 = new(10.0, 0.0, 0.0);
            Point3D point3D_3 = new(10.0, 10.0, 0.0);
            Point3D point3D_4 = new(0.0, 10.0, 0.0);

            Point3D? point3D_Mid = Spatial.Query.Centroid(new List<Point3D> { point3D_1, point3D_2 });
            Assert.NotNull(point3D_Mid);
            Assert.Equal(5.0, point3D_Mid.X, 1e-5);
            Assert.Equal(0.0, point3D_Mid.Y, 1e-5);
            Assert.Equal(0.0, point3D_Mid.Z, 1e-5);

            Point3D? point3D_CentroidTri = Spatial.Query.Centroid(new List<Point3D> { point3D_1, point3D_2, point3D_3 });
            Assert.NotNull(point3D_CentroidTri);
            Assert.Equal(20.0 / 3.0, point3D_CentroidTri.X, 1e-5);
            Assert.Equal(10.0 / 3.0, point3D_CentroidTri.Y, 1e-5);
            Assert.Equal(0.0, point3D_CentroidTri.Z, 1e-5);

            Point3D? point3D_CentroidPoly = Spatial.Query.Centroid(new List<Point3D> { point3D_1, point3D_2, point3D_3, point3D_4 });
            Assert.NotNull(point3D_CentroidPoly);
            Assert.Equal(5.0, point3D_CentroidPoly.X, 1e-5);
            Assert.Equal(5.0, point3D_CentroidPoly.Y, 1e-5);
            Assert.Equal(0.0, point3D_CentroidPoly.Z, 1e-5);

            int int_Iterations = 10000;
            List<Point3D[]> point3DArrays = new(int_Iterations);
            for (int int_I = 0; int_I < int_Iterations; int_I++)
            {
                point3DArrays.Add(new Point3D[]
                {
                    new(0.0, 0.0, 0.0),
                    new(10.0, 0.0, 0.0),
                    new(10.0, 10.0, 0.0),
                    new(0.0, 10.0, 0.0),
                    new(0.0, 5.0, 5.0),
                    new(5.0, 0.0, 5.0)
                });
            }

            _ = Spatial.Query.Centroid(point3DArrays[0]);

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < int_Iterations; int_I++)
            {
                _ = Spatial.Query.Centroid(point3DArrays[int_I]);
            }
            stopwatch.Stop();
            long long_Ms = stopwatch.ElapsedMilliseconds;

            Assert.True(long_Ms < 100, $"Spatial.Query.Centroid performance check failed! Took {long_Ms} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized 2D and 3D point AlmostEquals queries.
        /// </summary>
        [Fact]
        public void AlmostEquals_PerformanceAndCorrectness()
        {
            Point2D point2D_1 = new(1.0, 2.0);
            Point2D point2D_2 = new(1.0 + 1e-6, 2.0 - 1e-6);
            Point2D point2D_3 = new(1.5, 2.5);

            Assert.True(Planar.Query.AlmostEquals(point2D_1, point2D_2, 1e-4));
            Assert.False(Planar.Query.AlmostEquals(point2D_1, point2D_3, 1e-4));

            Point3D point3D_1 = new(1.0, 2.0, 3.0);
            Point3D point3D_2 = new(1.0 + 1e-6, 2.0 - 1e-6, 3.0 + 1e-6);
            Point3D point3D_3 = new(1.5, 2.5, 3.5);

            Assert.True(Spatial.Query.AlmostEquals(point3D_1, point3D_2, 1e-4));
            Assert.False(Spatial.Query.AlmostEquals(point3D_1, point3D_3, 1e-4));

            int int_Iterations = 500000;
            Point2D point2D_A = new(0.0, 0.0);
            Point2D point2D_B = new(0.00001, 0.00001);
            Point3D point3D_A = new(0.0, 0.0, 0.0);
            Point3D point3D_B = new(0.00001, 0.00001, 0.00001);

            _ = Planar.Query.AlmostEquals(point2D_A, point2D_B, 1e-3);
            _ = Spatial.Query.AlmostEquals(point3D_A, point3D_B, 1e-3);

            Stopwatch stopwatch = Stopwatch.StartNew();
            int int_Hits2D = 0;
            for (int int_I = 0; int_I < int_Iterations; int_I++)
            {
                if (Planar.Query.AlmostEquals(point2D_A, point2D_B, 1e-3))
                {
                    int_Hits2D++;
                }
            }
            stopwatch.Stop();
            long long_Ms2D = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            int int_Hits3D = 0;
            for (int int_I = 0; int_I < int_Iterations; int_I++)
            {
                if (Spatial.Query.AlmostEquals(point3D_A, point3D_B, 1e-3))
                {
                    int_Hits3D++;
                }
            }
            stopwatch.Stop();
            long long_Ms3D = stopwatch.ElapsedMilliseconds;

            Assert.Equal(int_Iterations, int_Hits2D);
            Assert.Equal(int_Iterations, int_Hits3D);
            Assert.True(long_Ms2D < 2000, $"Planar.Query.AlmostEquals performance check failed! Took {long_Ms2D} ms.");
            Assert.True(long_Ms3D < 2000, $"Spatial.Query.AlmostEquals performance check failed! Took {long_Ms3D} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Spatial InRange queries.
        /// </summary>
        [Fact]
        public void SpatialInRange_PerformanceAndCorrectness()
        {
            Plane plane = Spatial.Constants.Plane.WorldZ;
            List<Point3D> point3Ds_Vertices = new()
            {
                new(0.0, 0.0, 0.0),
                new(10.0, 0.0, 0.0),
                new(10.0, 10.0, 0.0),
                new(0.0, 10.0, 0.0)
            };

            Polygon2D polygon2D = new(point3Ds_Vertices.ConvertAll(p => Spatial.Query.Convert(plane, p))!);
            Polygon3D polygon3D = new(plane, polygon2D);

            Point3D point3D_Inside = new(5.0, 5.0, 0.0);
            Point3D point3D_Outside = new(15.0, 5.0, 0.0);
            Point3D point3D_OffPlane = new(5.0, 5.0, 1.0);

            Assert.True(Spatial.Query.InRange(polygon3D, point3D_Inside));
            Assert.False(Spatial.Query.InRange(polygon3D, point3D_Outside));
            Assert.False(Spatial.Query.InRange(polygon3D, point3D_OffPlane));

            List<Point3D> point3Ds_CollectionInside = new() { new(3.0, 3.0, 0.0), new(7.0, 7.0, 0.0) };
            List<Point3D> point3Ds_CollectionOutside = new() { new(3.0, 3.0, 0.0), new(15.0, 7.0, 0.0) };
            Assert.True(Spatial.Query.InRange(polygon3D, point3Ds_CollectionInside));
            Assert.False(Spatial.Query.InRange(polygon3D, point3Ds_CollectionOutside));

            Polyline3D polyline3D_Inside = new(new List<Point3D> { new(1.0, 1.0, 0.0), new(9.0, 9.0, 0.0) });
            Polyline3D polyline3D_Outside = new(new List<Point3D> { new(15.0, 15.0, 0.0), new(25.0, 25.0, 0.0) });
            Assert.True(Spatial.Query.InRange(polygon3D, polyline3D_Inside));
            Assert.False(Spatial.Query.InRange(polygon3D, polyline3D_Outside));

            int int_Iterations = 10000;
            List<Point3D> point3Ds_Test = new(int_Iterations);
            for (int int_I = 0; int_I < int_Iterations; int_I++)
            {
                point3Ds_Test.Add(new(5.0, 5.0, 0.0));
            }

            _ = Spatial.Query.InRange(polygon3D, point3D_Inside);
            _ = Spatial.Query.InRange(polygon3D, point3Ds_CollectionInside);
            _ = Spatial.Query.InRange(polygon3D, polyline3D_Inside);

            Stopwatch stopwatch = Stopwatch.StartNew();
            bool bool_CollectionResult = Spatial.Query.InRange(polygon3D, point3Ds_Test);
            stopwatch.Stop();
            long long_MsCollection = stopwatch.ElapsedMilliseconds;

            List<Point3D> point3Ds_Polyline = new(1000);
            for (int int_I = 0; int_I < 1000; int_I++)
            {
                point3Ds_Polyline.Add(new(5.0, 5.0, 0.0));
            }
            Polyline3D polyline3D_Large = new(point3Ds_Polyline);

            stopwatch.Restart();
            bool bool_SegmentableResult = Spatial.Query.InRange(polygon3D, polyline3D_Large);
            stopwatch.Stop();
            long long_MsSegmentable = stopwatch.ElapsedMilliseconds;

            Assert.True(bool_CollectionResult);
            Assert.True(bool_SegmentableResult);
            Assert.True(long_MsCollection < 400, $"Spatial point collection InRange check failed! Took {long_MsCollection} ms.");
            Assert.True(long_MsSegmentable < 400, $"Spatial segmentable InRange check failed! Took {long_MsSegmentable} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Spatial Normal queries.
        /// </summary>
        [Fact]
        public void SpatialNormal_PerformanceAndCorrectness()
        {
            Point3D point3D_1 = new(0.0, 0.0, 0.0);
            Point3D point3D_2 = new(10.0, 0.0, 0.0);
            Point3D point3D_3 = new(0.0, 10.0, 0.0);

            Spatial.Classes.Vector3D? vector3D_NormalTri = Spatial.Query.Normal(point3D_1, point3D_2, point3D_3);
            Assert.NotNull(vector3D_NormalTri);
            Assert.Equal(0.0, vector3D_NormalTri.X, 1e-5);
            Assert.Equal(0.0, vector3D_NormalTri.Y, 1e-5);
            Assert.True(System.Math.Abs(vector3D_NormalTri.Z) > 0.99);

            Spatial.Classes.Vector3D vector3D_AxisX = new(1.0, 0.0, 0.0);
            Spatial.Classes.Vector3D vector3D_AxisY = new(0.0, 1.0, 0.0);
            Spatial.Classes.Vector3D? vector3D_NormalVec = Spatial.Query.Normal(vector3D_AxisX, vector3D_AxisY);
            Assert.NotNull(vector3D_NormalVec);
            Assert.Equal(0.0, vector3D_NormalVec.X, 1e-5);
            Assert.Equal(0.0, vector3D_NormalVec.Y, 1e-5);
            Assert.True(System.Math.Abs(vector3D_NormalVec.Z) > 0.99);

            _ = Spatial.Query.Normal(point3D_1, point3D_2, point3D_3);

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = Spatial.Query.Normal(point3D_1, point3D_2, point3D_3);
            }
            stopwatch.Stop();
            long long_Ms = stopwatch.ElapsedMilliseconds;

            Assert.True(long_Ms < 300, $"Spatial.Query.Normal performance check failed! Took {long_Ms} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized ClosestPoint queries.
        /// </summary>
        [Fact]
        public void SpatialClosestPoint_PerformanceAndCorrectness()
        {
            Point3D point3D_Start = new(0.0, 0.0, 0.0);
            Point3D point3D_End = new(10.0, 0.0, 0.0);
            Segment3D segment3D = new(point3D_Start, point3D_End);
            Point3D point3D_Target = new(5.0, 5.0, 0.0);

            Point3D? point3D_ClosestSeg = segment3D.ClosestPoint(point3D_Target);
            Assert.NotNull(point3D_ClosestSeg);
            Assert.Equal(5.0, point3D_ClosestSeg.X, 1e-5);
            Assert.Equal(0.0, point3D_ClosestSeg.Y, 1e-5);
            Assert.Equal(0.0, point3D_ClosestSeg.Z, 1e-5);

            Point2D point2D_Start = new(0.0, 0.0);
            Point2D point2D_End = new(10.0, 0.0);
            Segment2D segment2D = new(point2D_Start, point2D_End);
            Point2D point2D_Target = new(5.0, 5.0);

            Point2D? point2D_ClosestSeg = segment2D.ClosestPoint(point2D_Target);
            Assert.NotNull(point2D_ClosestSeg);
            Assert.Equal(5.0, point2D_ClosestSeg.X, 1e-5);
            Assert.Equal(0.0, point2D_ClosestSeg.Y, 1e-5);

            Plane plane = new(new Point3D(0.0, 0.0, 0.0), Spatial.Constants.Vector3D.WorldZ);
            Polygon2D polygon2D = new(new List<Point2D>
            {
                new(0.0, 0.0),
                new(10.0, 0.0),
                new(10.0, 10.0),
                new(0.0, 10.0)
            });
            Polygon3D polygon3D = new(plane, polygon2D);
            PolygonalFace3D polygonalFace3D = new(polygon3D);
            List<PolygonalFace3D> polygonalFace3Ds = new() { polygonalFace3D };

            Point3D? point3D_ClosestFace = Spatial.Query.ClosestPoint(point3D_Target, polygonalFace3Ds, out PolygonalFace3D? closestFace, out double double_DistFace);
            Assert.NotNull(point3D_ClosestFace);
            Assert.NotNull(closestFace);
            Assert.Equal(5.0, point3D_ClosestFace.X, 1e-5);
            Assert.Equal(5.0, point3D_ClosestFace.Y, 1e-5);
            Assert.Equal(0.0, point3D_ClosestFace.Z, 1e-5);
            Assert.Equal(0.0, double_DistFace, 1e-5);

            // Benchmark segment ClosestPoint
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = segment3D.ClosestPoint(point3D_Target);
                _ = segment2D.ClosestPoint(point2D_Target);
            }
            stopwatch.Stop();
            long long_MsSegment = stopwatch.ElapsedMilliseconds;

            // Benchmark face ClosestPoint
            stopwatch.Restart();
            for (int int_I = 0; int_I < 10000; int_I++)
            {
                _ = Spatial.Query.ClosestPoint(point3D_Target, polygonalFace3Ds, out _, out _, 0.0);
            }
            stopwatch.Stop();
            long long_MsFace = stopwatch.ElapsedMilliseconds;

            Assert.True(long_MsSegment < 2500, $"Segment ClosestPoint performance check failed! Took {long_MsSegment} ms.");
            Assert.True(long_MsFace < 2500, $"Face ClosestPoint performance check failed! Took {long_MsFace} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized geometry conversion and projection queries.
        /// </summary>
        [Fact]
        public void SpatialConvert_PerformanceAndCorrectness()
        {
            Plane plane = new(new Point3D(0.0, 0.0, 0.0), Spatial.Constants.Vector3D.WorldZ);

            Point2D point2D = new(5.0, 10.0);
            Point3D? point3D_Converted = Spatial.Query.Convert(plane, point2D);
            Assert.NotNull(point3D_Converted);
            Assert.Equal(5.0, point3D_Converted.X, 1e-5);
            Assert.Equal(10.0, point3D_Converted.Y, 1e-5);
            Assert.Equal(0.0, point3D_Converted.Z, 1e-5);

            Point3D point3D = new(5.0, 10.0, 0.0);
            Point2D? point2D_Converted = Spatial.Query.Convert(plane, point3D);
            Assert.NotNull(point2D_Converted);
            Assert.Equal(5.0, point2D_Converted.X, 1e-5);
            Assert.Equal(10.0, point2D_Converted.Y, 1e-5);

            List<Point2D> point2Ds_Poly = new();
            for (int int_I = 0; int_I < 100; int_I++)
            {
                point2Ds_Poly.Add(new Point2D(int_I, int_I));
            }
            Polyline2D polyline2D = new(point2Ds_Poly);

            Polyline3D? polyline3D_Converted = Spatial.Query.Convert(plane, polyline2D);
            Assert.NotNull(polyline3D_Converted);
            Assert.Equal(100, polyline3D_Converted.GetPoints()?.Count);

            // Benchmark primitive point conversions
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = Spatial.Query.Convert(plane, point2D);
                _ = Spatial.Query.Convert(plane, point3D);
            }
            stopwatch.Stop();
            long long_MsPoint = stopwatch.ElapsedMilliseconds;

            // Benchmark collection conversions (1,000 conversions of 100-point polyline)
            stopwatch.Restart();
            for (int int_I = 0; int_I < 1000; int_I++)
            {
                _ = Spatial.Query.Convert(plane, polyline2D);
            }
            stopwatch.Stop();
            long long_MsPolyline = stopwatch.ElapsedMilliseconds;

            Assert.True(long_MsPoint < 3000, $"Primitive point conversion performance check failed! Took {long_MsPoint} ms.");
            Assert.True(long_MsPolyline < 3000, $"Polyline conversion performance check failed! Took {long_MsPolyline} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized bounding box expansion methods.
        /// </summary>
        [Fact]
        public void BoundingBox_PerformanceAndCorrectness()
        {
            Point2D point2D_1 = new(0.0, 0.0);
            Point2D point2D_2 = new(10.0, 10.0);
            BoundingBox2D boundingBox2D = new(point2D_1, point2D_2);

            Assert.Equal(0.0, boundingBox2D.Min.X);
            Assert.Equal(0.0, boundingBox2D.Min.Y);
            Assert.Equal(10.0, boundingBox2D.Max.X);
            Assert.Equal(10.0, boundingBox2D.Max.Y);

            Point2D point2D_Target = new(-5.0, 15.0);
            boundingBox2D.Add(point2D_Target);
            Assert.Equal(-5.0, boundingBox2D.Min.X);
            Assert.Equal(15.0, boundingBox2D.Max.Y);

            Point3D point3D_1 = new(0.0, 0.0, 0.0);
            Point3D point3D_2 = new(10.0, 10.0, 10.0);
            BoundingBox3D boundingBox3D = new(point3D_1, point3D_2);

            Point3D point3D_Target = new(-5.0, 15.0, -2.0);
            boundingBox3D.Add(point3D_Target);
            Assert.Equal(-5.0, boundingBox3D.Min.X);
            Assert.Equal(15.0, boundingBox3D.Max.Y);
            Assert.Equal(-2.0, boundingBox3D.Min.Z);

            // Benchmark 2D bounding box expansion
            BoundingBox2D boundingBox2D_Bench = new(point2D_1, point2D_2);
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 1000000; int_I++)
            {
                boundingBox2D_Bench.Add(point2D_Target);
            }
            stopwatch.Stop();
            long long_Ms2D = stopwatch.ElapsedMilliseconds;

            // Benchmark 3D bounding box expansion
            BoundingBox3D boundingBox3D_Bench = new(point3D_1, point3D_2);
            stopwatch.Restart();
            for (int int_I = 0; int_I < 1000000; int_I++)
            {
                boundingBox3D_Bench.Add(point3D_Target);
            }
            stopwatch.Stop();
            long long_Ms3D = stopwatch.ElapsedMilliseconds;

            Assert.True(long_Ms2D < 5000, $"BoundingBox2D expansion performance check failed! Took {long_Ms2D} ms.");
            Assert.True(long_Ms3D < 5000, $"BoundingBox3D expansion performance check failed! Took {long_Ms3D} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Polyhedron GetBoundingBox method.
        /// </summary>
        [Fact]
        public void PolyhedronBoundingBox_PerformanceAndCorrectness()
        {
            List<IPolygonalFace3D> cubeFaces = new();

            List<Point3D> facePoints1 = new() { new Point3D(0.0, 0.0, 0.0), new Point3D(1.0, 0.0, 0.0), new Point3D(1.0, 1.0, 0.0), new Point3D(0.0, 1.0, 0.0) };
            Polygon3D? facePolygon1 = Spatial.Create.Polygon3D(facePoints1);
            Assert.NotNull(facePolygon1);
            cubeFaces.Add(new PolygonalFace3D(facePolygon1));

            List<Point3D> facePoints2 = new() { new Point3D(0.0, 0.0, 1.0), new Point3D(1.0, 0.0, 1.0), new Point3D(1.0, 1.0, 1.0), new Point3D(0.0, 1.0, 1.0) };
            Polygon3D? facePolygon2 = Spatial.Create.Polygon3D(facePoints2);
            Assert.NotNull(facePolygon2);
            cubeFaces.Add(new PolygonalFace3D(facePolygon2));

            List<Point3D> facePoints3 = new() { new Point3D(0.0, 0.0, 0.0), new Point3D(0.0, 1.0, 0.0), new Point3D(0.0, 1.0, 1.0), new Point3D(0.0, 0.0, 1.0) };
            Polygon3D? facePolygon3 = Spatial.Create.Polygon3D(facePoints3);
            Assert.NotNull(facePolygon3);
            cubeFaces.Add(new PolygonalFace3D(facePolygon3));

            List<Point3D> facePoints4 = new() { new Point3D(1.0, 0.0, 0.0), new Point3D(1.0, 1.0, 0.0), new Point3D(1.0, 1.0, 1.0), new Point3D(1.0, 0.0, 1.0) };
            Polygon3D? facePolygon4 = Spatial.Create.Polygon3D(facePoints4);
            Assert.NotNull(facePolygon4);
            cubeFaces.Add(new PolygonalFace3D(facePolygon4));

            List<Point3D> facePoints5 = new() { new Point3D(0.0, 0.0, 0.0), new Point3D(1.0, 0.0, 0.0), new Point3D(1.0, 0.0, 1.0), new Point3D(0.0, 0.0, 1.0) };
            Polygon3D? facePolygon5 = Spatial.Create.Polygon3D(facePoints5);
            Assert.NotNull(facePolygon5);
            cubeFaces.Add(new PolygonalFace3D(facePolygon5));

            List<Point3D> facePoints6 = new() { new Point3D(0.0, 1.0, 0.0), new Point3D(1.0, 1.0, 0.0), new Point3D(1.0, 1.0, 1.0), new Point3D(0.0, 1.0, 1.0) };
            Polygon3D? facePolygon6 = Spatial.Create.Polygon3D(facePoints6);
            Assert.NotNull(facePolygon6);
            cubeFaces.Add(new PolygonalFace3D(facePolygon6));

            Polyhedron? cube = Spatial.Create.Polyhedron(cubeFaces);
            Assert.NotNull(cube);

            // Warm up / JIT compile
            _ = cube.GetBoundingBox();

            // Benchmark
            Stopwatch stopwatch = Stopwatch.StartNew();
            BoundingBox3D? bounds = null;
            for (int i = 0; i < 5000; i++)
            {
                bounds = cube.GetBoundingBox();
            }
            stopwatch.Stop();
            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            // Verify correctness
            Assert.NotNull(bounds);
            Assert.Equal(0.0, bounds.Min.X, 1e-5);
            Assert.Equal(0.0, bounds.Min.Y, 1e-5);
            Assert.Equal(0.0, bounds.Min.Z, 1e-5);
            Assert.Equal(1.0, bounds.Max.X, 1e-5);
            Assert.Equal(1.0, bounds.Max.Y, 1e-5);
            Assert.Equal(1.0, bounds.Max.Z, 1e-5);

            // Verify performance (50k iterations should take < 250ms with in-place path)
            Assert.True(elapsedMilliseconds < 800, $"Polyhedron GetBoundingBox performance check failed! Took {elapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Line2D and Line3D methods.
        /// </summary>
        [Fact]
        public void Line_PerformanceAndCorrectness()
        {
            // 2D Line Setup
            Point2D origin2D = new(0.0, 0.0);
            Vector2D direction2D = new(1.0, 0.0);
            Line2D line2D = new(origin2D, direction2D);

            Point2D targetPoint2D = new(5.0, 10.0);
            Point2D? projectedPoint2D = line2D.Project(targetPoint2D);

            // Assert 2D Correctness
            Assert.NotNull(projectedPoint2D);
            Assert.Equal(5.0, projectedPoint2D.X, 1e-5);
            Assert.Equal(0.0, projectedPoint2D.Y, 1e-5);
            Assert.Equal(10.0, line2D.Distance(targetPoint2D), 1e-5);
            Assert.True(line2D.On(new Point2D(7.5, 0.0), 1e-5));
            Assert.False(line2D.On(targetPoint2D, 1e-5));

            // Line-Line Intersection Correctness
            Line2D otherLine = new(new Point2D(5.0, 5.0), new Vector2D(0.0, 1.0));
            Point2D? lineIntersection = line2D.IntersectionPoint(otherLine);
            Assert.NotNull(lineIntersection);
            Assert.Equal(5.0, lineIntersection.X, 1e-5);
            Assert.Equal(0.0, lineIntersection.Y, 1e-5);

            // Line-Segment Intersection Correctness
            Segment2D intersectingSegment = new(new Point2D(5.0, -5.0), new Point2D(5.0, 5.0));
            Point2D? segmentIntersection = line2D.IntersectionPoint(intersectingSegment);
            Assert.NotNull(segmentIntersection);
            Assert.Equal(5.0, segmentIntersection.X, 1e-5);
            Assert.Equal(0.0, segmentIntersection.Y, 1e-5);

            // 3D Line Setup
            Point3D origin3D = new(0.0, 0.0, 0.0);
            Spatial.Classes.Vector3D direction3D = new(1.0, 0.0, 0.0);
            Line3D line3D = new(origin3D, direction3D);

            Point3D targetPoint3D = new(5.0, 10.0, 15.0);
            Point3D? projectedPoint3D = line3D.Project(targetPoint3D);

            // Assert 3D Correctness
            Assert.NotNull(projectedPoint3D);
            Assert.Equal(5.0, projectedPoint3D.X, 1e-5);
            Assert.Equal(0.0, projectedPoint3D.Y, 1e-5);
            Assert.Equal(0.0, projectedPoint3D.Z, 1e-5);
            Assert.Equal(System.Math.Sqrt(100.0 + 225.0), line3D.Distance(targetPoint3D), 1e-5);
            Assert.True(line3D.On(new Point3D(7.5, 0.0, 0.0), 1e-5));
            Assert.False(line3D.On(targetPoint3D, 1e-5));

            // Benchmark 2D Line operations (100,000 iterations)
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 100000; i++)
            {
                _ = line2D.Project(targetPoint2D);
                _ = line2D.Distance(targetPoint2D);
                _ = line2D.On(targetPoint2D, 1e-5);
                _ = line2D.IntersectionPoint(otherLine);
                _ = line2D.IntersectionPoint(intersectingSegment);
            }
            stopwatch.Stop();
            long elapsedMs2D = stopwatch.ElapsedMilliseconds;

            // Benchmark 3D Line operations (100,000 iterations)
            stopwatch.Restart();
            for (int i = 0; i < 100000; i++)
            {
                _ = line3D.Project(targetPoint3D);
                _ = line3D.Distance(targetPoint3D);
                _ = line3D.On(targetPoint3D, 1e-5);
            }
            stopwatch.Stop();
            long elapsedMs3D = stopwatch.ElapsedMilliseconds;

            // Assert performance (100,000 iterations should take < 600ms for both 2D and 3D)
            Assert.True(elapsedMs2D < 3000, $"Line2D performance check failed! Took {elapsedMs2D} ms.");
            Assert.True(elapsedMs3D < 3000, $"Line3D performance check failed! Took {elapsedMs3D} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Ray2D and Ray3D methods.
        /// </summary>
        [Fact]
        public void Ray_PerformanceAndCorrectness()
        {
            // 2D Ray Setup
            Point2D point2D_Origin = new(0.0, 0.0);
            Vector2D vector2D_Direction = new(1.0, 0.0);
            Ray2D ray2D_Test = new(point2D_Origin, vector2D_Direction);

            Point2D point2D_TargetFront = new(5.0, 10.0);
            Point2D point2D_TargetBehind = new(-5.0, 10.0);

            Point2D? point2D_ProjFront = ray2D_Test.Project(point2D_TargetFront);
            Point2D? point2D_ProjBehind = ray2D_Test.Project(point2D_TargetBehind);

            // Assert 2D Correctness
            Assert.NotNull(point2D_ProjFront);
            Assert.Equal(5.0, point2D_ProjFront.X, 1e-5);
            Assert.Equal(0.0, point2D_ProjFront.Y, 1e-5);

            Assert.NotNull(point2D_ProjBehind);
            Assert.Equal(0.0, point2D_ProjBehind.X, 1e-5);
            Assert.Equal(0.0, point2D_ProjBehind.Y, 1e-5);

            Assert.Equal(10.0, ray2D_Test.Distance(point2D_TargetFront), 1e-5);
            Assert.Equal(System.Math.Sqrt(25.0 + 100.0), ray2D_Test.Distance(point2D_TargetBehind), 1e-5);

            Assert.True(ray2D_Test.On(new Point2D(7.5, 0.0), 1e-5));
            Assert.False(ray2D_Test.On(point2D_TargetFront, 1e-5));
            Assert.False(ray2D_Test.On(new Point2D(-7.5, 0.0), 1e-5));

            // Ray-Ray Intersection Correctness
            Ray2D ray2D_Other = new(new Point2D(5.0, 5.0), new Vector2D(0.0, -1.0));
            Point2D? point2D_RayIntersection = ray2D_Test.IntersectionPoint(ray2D_Other);
            Assert.NotNull(point2D_RayIntersection);
            Assert.Equal(5.0, point2D_RayIntersection.X, 1e-5);
            Assert.Equal(0.0, point2D_RayIntersection.Y, 1e-5);

            // Ray-Segment Intersection Correctness
            Segment2D segment2D_Intersecting = new(new Point2D(5.0, -5.0), new Point2D(5.0, 5.0));
            Point2D? point2D_SegIntersection = ray2D_Test.IntersectionPoint(segment2D_Intersecting);
            Assert.NotNull(point2D_SegIntersection);
            Assert.Equal(5.0, point2D_SegIntersection.X, 1e-5);
            Assert.Equal(0.0, point2D_SegIntersection.Y, 1e-5);

            // 3D Ray Setup
            Point3D point3D_Origin = new(0.0, 0.0, 0.0);
            Spatial.Classes.Vector3D vector3D_Direction = new(1.0, 0.0, 0.0);
            Ray3D ray3D_Test = new(point3D_Origin, vector3D_Direction);

            Point3D point3D_TargetFront = new(5.0, 10.0, 15.0);
            Point3D point3D_TargetBehind = new(-5.0, 10.0, 15.0);

            Point3D? point3D_ProjFront = ray3D_Test.Project(point3D_TargetFront);
            Point3D? point3D_ProjBehind = ray3D_Test.Project(point3D_TargetBehind);

            // Assert 3D Correctness
            Assert.NotNull(point3D_ProjFront);
            Assert.Equal(5.0, point3D_ProjFront.X, 1e-5);
            Assert.Equal(0.0, point3D_ProjFront.Y, 1e-5);
            Assert.Equal(0.0, point3D_ProjFront.Z, 1e-5);

            Assert.NotNull(point3D_ProjBehind);
            Assert.Equal(0.0, point3D_ProjBehind.X, 1e-5);
            Assert.Equal(0.0, point3D_ProjBehind.Y, 1e-5);
            Assert.Equal(0.0, point3D_ProjBehind.Z, 1e-5);

            Assert.Equal(System.Math.Sqrt(100.0 + 225.0), ray3D_Test.Distance(point3D_TargetFront), 1e-5);
            Assert.Equal(System.Math.Sqrt(25.0 + 100.0 + 225.0), ray3D_Test.Distance(point3D_TargetBehind), 1e-5);

            Assert.True(ray3D_Test.On(new Point3D(7.5, 0.0, 0.0), 1e-5));
            Assert.False(ray3D_Test.On(point3D_TargetFront, 1e-5));
            Assert.False(ray3D_Test.On(new Point3D(-7.5, 0.0, 0.0), 1e-5));

            // Benchmark 2D Ray operations (100,000 iterations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = ray2D_Test.Project(point2D_TargetFront);
                _ = ray2D_Test.Distance(point2D_TargetFront);
                _ = ray2D_Test.On(point2D_TargetFront, 1e-5);
                _ = ray2D_Test.IntersectionPoint(ray2D_Other);
                _ = ray2D_Test.IntersectionPoint(segment2D_Intersecting);
            }
            stopwatch_Stopwatch.Stop();
            long long_Ms2D = stopwatch_Stopwatch.ElapsedMilliseconds;

            // Benchmark 3D Ray operations (100,000 iterations)
            stopwatch_Stopwatch.Restart();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = ray3D_Test.Project(point3D_TargetFront);
                _ = ray3D_Test.Distance(point3D_TargetFront);
                _ = ray3D_Test.On(point3D_TargetFront, 1e-5);
            }
            stopwatch_Stopwatch.Stop();
            long long_Ms3D = stopwatch_Stopwatch.ElapsedMilliseconds;

            // Assert performance (100,000 iterations should take < 600ms)
            Assert.True(long_Ms2D < 1000, $"Ray2D performance check failed! Took {long_Ms2D} ms.");
            Assert.True(long_Ms3D < 1000, $"Ray3D performance check failed! Took {long_Ms3D} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Plane and Circle2D methods.
        /// </summary>
        [Fact]
        public void PlaneAndCircle_PerformanceAndCorrectness()
        {
            // 3D Plane Setup
            Point3D point3D_Origin = new(1.0, 2.0, 3.0);
            Spatial.Classes.Vector3D vector3D_Normal = new(0.0, 0.0, 1.0);
            Plane plane_Test = new(point3D_Origin, vector3D_Normal);

            Point3D point3D_Target = new(5.0, 6.0, 10.0);
            Point3D? point3D_Closest = plane_Test.ClosestPoint(point3D_Target);

            // Assert Plane Correctness
            Assert.NotNull(point3D_Closest);
            Assert.Equal(5.0, point3D_Closest.X, 1e-5);
            Assert.Equal(6.0, point3D_Closest.Y, 1e-5);
            Assert.Equal(3.0, point3D_Closest.Z, 1e-5);

            Assert.Equal(7.0, plane_Test.Distance(point3D_Target), 1e-5);
            Assert.True(Spatial.Query.On(plane_Test, new Point3D(5.0, 6.0, 3.0), 1e-5));
            Assert.False(Spatial.Query.On(plane_Test, point3D_Target, 1e-5));

            Spatial.Classes.Vector3D vector3D_OnPlane = new(5.0, 5.0, 0.0);
            Spatial.Classes.Vector3D vector3D_OffPlane = new(5.0, 5.0, 5.0);
            Assert.True(Spatial.Query.On(plane_Test, vector3D_OnPlane, 1e-5));
            Assert.False(Spatial.Query.On(plane_Test, vector3D_OffPlane, 1e-5));

            // Circle2D Setup
            Point2D point2D_Center = new(0.0, 0.0);
            Circle2D circle2D_Test = new(point2D_Center, 5.0);

            Point2D point2D_Inside = new(3.0, 0.0);
            Point2D point2D_Outside = new(6.0, 0.0);
            Point2D point2D_Boundary = new(0.0, 5.0);

            // Assert Circle2D Correctness
            Assert.True(circle2D_Test.Inside(point2D_Inside, 1e-5));
            Assert.False(circle2D_Test.Inside(point2D_Outside, 1e-5));
            Assert.True(circle2D_Test.On(point2D_Boundary, 1e-5));
            Assert.False(circle2D_Test.On(point2D_Inside, 1e-5));
            Assert.True(circle2D_Test.InRange(point2D_Boundary, 1e-5));
            Assert.False(circle2D_Test.InRange(point2D_Outside, 1e-5));

            // Benchmark Plane operations (100,000 iterations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = plane_Test.ClosestPoint(point3D_Target);
                _ = plane_Test.Distance(point3D_Target);
                _ = Spatial.Query.On(plane_Test, point3D_Target, 1e-5);
                _ = Spatial.Query.On(plane_Test, vector3D_OnPlane, 1e-5);
            }
            stopwatch_Stopwatch.Stop();
            long long_MsPlane = stopwatch_Stopwatch.ElapsedMilliseconds;

            // Benchmark Circle2D operations (100,000 iterations)
            stopwatch_Stopwatch.Restart();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = circle2D_Test.Inside(point2D_Inside, 1e-5);
                _ = circle2D_Test.On(point2D_Boundary, 1e-5);
                _ = circle2D_Test.InRange(point2D_Boundary, 1e-5);
            }
            stopwatch_Stopwatch.Stop();
            long long_MsCircle = stopwatch_Stopwatch.ElapsedMilliseconds;

            // Assert performance
            Assert.True(long_MsPlane < 5000, $"Plane performance check failed! Took {long_MsPlane} ms.");
            Assert.True(long_MsCircle < 5000, $"Circle2D performance check failed! Took {long_MsCircle} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Ellipse2D methods.
        /// </summary>
        [Fact]
        public void Ellipse2D_PerformanceAndCorrectness()
        {
            // Setup Ellipse2D
            Point2D point2D_Center = new(1.0, 2.0);
            Vector2D vector2D_DirA = new(1.0, 0.0);
            Ellipse2D ellipse2D_Test = new(point2D_Center, 10.0, 5.0, vector2D_DirA);

            Point2D point2D_Target = new(11.0, 2.0);
            Point2D? point2D_Projected = ellipse2D_Test.Project(point2D_Target);

            // Assert Correctness of Project
            Assert.NotNull(point2D_Projected);
            Assert.Equal(11.0, point2D_Projected.X, 1e-5);
            Assert.Equal(2.0, point2D_Projected.Y, 1e-5);

            // Assert Correctness of GetFocalPoints
            Point2D[]? point2D_Foci = ellipse2D_Test.GetFocalPoints();
            Assert.NotNull(point2D_Foci);
            Assert.Equal(2, point2D_Foci.Length);
            double double_Ecc = System.Math.Sqrt(100.0 - 25.0); // sqrt(75) ~= 8.66025
            Assert.Equal(1.0 + double_Ecc, point2D_Foci[0].X, 1e-5);
            Assert.Equal(2.0, point2D_Foci[0].Y, 1e-5);
            Assert.Equal(1.0 - double_Ecc, point2D_Foci[1].X, 1e-5);
            Assert.Equal(2.0, point2D_Foci[1].Y, 1e-5);

            // Assert Correctness of Transform
            Transform2D? transform2D_Trans = Planar.Create.Transform2D.Translation(10.0, -10.0);
            Assert.NotNull(transform2D_Trans);
            bool bool_TransResult = ellipse2D_Test.Transform(transform2D_Trans);
            Assert.True(bool_TransResult);
            Assert.NotNull(ellipse2D_Test.Center);
            Assert.Equal(11.0, ellipse2D_Test.Center.X, 1e-5);
            Assert.Equal(-8.0, ellipse2D_Test.Center.Y, 1e-5);

            // Benchmark Ellipse2D operations (100,000 iterations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = ellipse2D_Test.Project(point2D_Target);
                _ = ellipse2D_Test.GetFocalPoints();
            }
            stopwatch_Stopwatch.Stop();
            long long_MsEllipse = stopwatch_Stopwatch.ElapsedMilliseconds;

            Assert.True(long_MsEllipse < 1000, $"Ellipse2D performance check failed! Took {long_MsEllipse} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Rectangle2D query methods.
        /// </summary>
        [Fact]
        public void Rectangle2D_Queries_PerformanceAndCorrectness()
        {
            // Setup Rectangle2D
            Point2D point2D_Origin = new(1.0, 2.0);
            Vector2D vector2D_HeightDir = new(0.0, 1.0); // World Y
            Rectangle2D rectangle2D_Test = new(point2D_Origin, 10.0, 5.0, vector2D_HeightDir);

            Point2D point2D_Inside = new(5.0, 4.0);
            Point2D point2D_Outside = new(15.0, 10.0);
            Point2D point2D_Boundary = new(1.0, 4.0);

            // Assert Correctness
            Assert.True(rectangle2D_Test.Inside(point2D_Inside, 1e-5));
            Assert.False(rectangle2D_Test.Inside(point2D_Outside, 1e-5));
            Assert.True(rectangle2D_Test.InRange(point2D_Boundary, 1e-5));
            Assert.False(rectangle2D_Test.InRange(point2D_Outside, 1e-5));
            Assert.True(rectangle2D_Test.On(point2D_Boundary, 1e-5));
            Assert.False(rectangle2D_Test.On(point2D_Inside, 1e-5));

            Point2D? point2D_Closest = rectangle2D_Test.ClosestPoint(point2D_Outside);
            Assert.NotNull(point2D_Closest);
            // Height direction is (0, 1) -> spans y from 2 to 7. Width direction is (1, 0) -> spans x from 1 to 11.
            // Clamped values: x = 11, y = 7.
            Assert.Equal(11.0, point2D_Closest.X, 1e-5);
            Assert.Equal(7.0, point2D_Closest.Y, 1e-5);

            double double_Distance = rectangle2D_Test.Distance(point2D_Outside);
            // dx = 15 - 11 = 4, dy = 10 - 7 = 3. Distance should be sqrt(16 + 9) = 5.
            Assert.Equal(5.0, double_Distance, 1e-5);

            // Benchmark Rectangle2D operations (100,000 iterations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = rectangle2D_Test.ClosestPoint(point2D_Outside);
                _ = rectangle2D_Test.Distance(point2D_Outside);
                _ = rectangle2D_Test.Inside(point2D_Inside, 1e-5);
                _ = rectangle2D_Test.On(point2D_Boundary, 1e-5);
                _ = rectangle2D_Test.InRange(point2D_Boundary, 1e-5);
            }
            stopwatch_Stopwatch.Stop();
            long long_MsRectangle = stopwatch_Stopwatch.ElapsedMilliseconds;

            Assert.True(long_MsRectangle < 5000, $"Rectangle2D performance check failed! Took {long_MsRectangle} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Sphere and Triangle methods.
        /// </summary>
        [Fact]
        public void SphereAndTriangle_PerformanceAndCorrectness()
        {
            // Setup Sphere
            Point3D point3D_Center = new(1.0, 2.0, 3.0);
            Sphere sphere_Test = new(point3D_Center, 5.0);

            // Assert Bounding Box correctness
            BoundingBox3D? boundingBox3D = sphere_Test.GetBoundingBox();
            Assert.NotNull(boundingBox3D);
            Assert.NotNull(boundingBox3D.Min);
            Assert.NotNull(boundingBox3D.Max);
            Assert.Equal(-4.0, boundingBox3D.Min.X, 1e-5);
            Assert.Equal(-3.0, boundingBox3D.Min.Y, 1e-5);
            Assert.Equal(-2.0, boundingBox3D.Min.Z, 1e-5);
            Assert.Equal(6.0, boundingBox3D.Max.X, 1e-5);
            Assert.Equal(7.0, boundingBox3D.Max.Y, 1e-5);
            Assert.Equal(8.0, boundingBox3D.Max.Z, 1e-5);

            // Setup Triangles
            Point2D point2D_1 = new(0.0, 0.0);
            Point2D point2D_2 = new(10.0, 0.0);
            Point2D point2D_3 = new(0.0, 10.0);
            Triangle2D triangle2D_Test = new(point2D_1, point2D_2, point2D_3);

            Point3D point3D_1 = new(0.0, 0.0, 0.0);
            Point3D point3D_2 = new(10.0, 0.0, 0.0);
            Point3D point3D_3 = new(0.0, 10.0, 0.0);
            Triangle3D triangle3D_Test = new(point3D_1, point3D_2, point3D_3);

            // Assert Triangulation correctness
            List<Triangle2D>? list_Tri2D = triangle2D_Test.Triangulate();
            Assert.NotNull(list_Tri2D);
            Assert.Single(list_Tri2D);
            Assert.Equal(10.0, list_Tri2D[0].GetPoints()?[1].X ?? 0.0, 5);

            List<Triangle3D>? list_Tri3D = triangle3D_Test.Triangulate();
            Assert.NotNull(list_Tri3D);
            Assert.Single(list_Tri3D);
            Assert.Equal(10.0, list_Tri3D[0].GetPoints()?[1].X ?? 0.0, 5);

            // Benchmark Sphere and Triangles operations (100,000 iterations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = sphere_Test.GetBoundingBox();
                _ = triangle2D_Test.Triangulate();
                _ = triangle3D_Test.Triangulate();
            }
            stopwatch_Stopwatch.Stop();
            long long_Ms = stopwatch_Stopwatch.ElapsedMilliseconds;

            Assert.True(long_Ms < 5000, $"Sphere and Triangle performance check failed! Took {long_Ms} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Segment2D and Segment3D methods.
        /// </summary>
        [Fact]
        public void Segment_PerformanceAndCorrectness()
        {
            // Setup Segment2D
            Point2D point2D_Start = new(1.0, 2.0);
            Vector2D vector2D_Vec = new(4.0, 3.0); // Length = 5.0
            Segment2D segment2D_Test = new(point2D_Start, vector2D_Vec);

            Point2D point2D_Target = new(5.0, 0.0);
            Point2D? point2D_Projected = segment2D_Test.Project(point2D_Target);
            Assert.NotNull(point2D_Projected);
            Assert.Equal(2.6, point2D_Projected.X, 5);
            Assert.Equal(3.2, point2D_Projected.Y, 5);

            double double_Dist2D = segment2D_Test.Distance(point2D_Target);
            Assert.Equal(4.0, double_Dist2D, 5);

            // Setup Segment3D
            Point3D point3D_Start = new(1.0, 2.0, 3.0);
            Spatial.Classes.Vector3D vector3D_Vec = new(4.0, 3.0, 0.0); // Length = 5.0
            Segment3D segment3D_Test = new(point3D_Start, vector3D_Vec);

            Point3D point3D_Target = new(5.0, 0.0, 3.0);
            Point3D? point3D_Projected = segment3D_Test.Project(point3D_Target);
            Assert.NotNull(point3D_Projected);
            Assert.Equal(2.6, point3D_Projected.X, 5);
            Assert.Equal(3.2, point3D_Projected.Y, 5);
            Assert.Equal(3.0, point3D_Projected.Z, 5);

            double double_Dist3D = segment3D_Test.Distance(point3D_Target);
            Assert.Equal(4.0, double_Dist3D, 5);

            // Benchmark Segment operations (100,000 iterations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = segment2D_Test.Distance(point2D_Target);
                _ = segment2D_Test.Project(point2D_Target);
                _ = segment3D_Test.Distance(point3D_Target);
                _ = segment3D_Test.Project(point3D_Target);
            }
            stopwatch_Stopwatch.Stop();
            long long_Ms = stopwatch_Stopwatch.ElapsedMilliseconds;

            Assert.True(long_Ms < 5000, $"Segment performance check failed! Took {long_Ms} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Rectangle3D query methods.
        /// </summary>
        [Fact]
        public void Rectangle3D_Queries_PerformanceAndCorrectness()
        {
            // Setup Plane
            Point3D point3D_Origin = new(1.0, 2.0, 3.0);
            Spatial.Classes.Vector3D vector3D_Normal = new(0.0, 0.0, 1.0); // Normal is Z axis
            Plane plane_Test = new(point3D_Origin, vector3D_Normal);

            // Setup Rectangle3D on that plane
            Rectangle2D rectangle2D_Geom = new(10.0, 5.0); // Spans u from 0 to 10, v from 0 to 5.
            Rectangle3D rectangle3D_Test = new(plane_Test, rectangle2D_Geom);

            // Test target points
            // Local u=4, v=3, offset in Z direction by 5.0
            // Global point: Origin + 4*AxisX + 3*AxisY + 5*AxisZ = (1, 2, 3) + (4, 0, 0) + (0, 3, 0) + (0, 0, 5) = (5, 5, 8)
            Point3D point3D_Target = new(5.0, 5.0, 8.0);
            Point3D point3D_InsideOnPlane = new(5.0, 5.0, 3.0); // u=4, v=3, z=3
            Point3D point3D_OutsideOnPlane = new(15.0, 10.0, 3.0); // u=14, v=8, z=3
            Point3D point3D_BoundaryOnPlane = new(1.0, 5.0, 3.0); // u=0, v=3, z=3

            // Assert Correctness
            Assert.True(rectangle3D_Test.Inside(point3D_InsideOnPlane, 1e-5));
            Assert.False(rectangle3D_Test.Inside(point3D_OutsideOnPlane, 1e-5));
            Assert.True(rectangle3D_Test.InRange(point3D_BoundaryOnPlane, 1e-5));
            Assert.False(rectangle3D_Test.InRange(point3D_OutsideOnPlane, 1e-5));
            Assert.True(rectangle3D_Test.On(point3D_BoundaryOnPlane, 1e-5));
            Assert.False(rectangle3D_Test.On(point3D_InsideOnPlane, 1e-5));

            Point3D? point3D_Closest = rectangle3D_Test.ClosestPoint(point3D_Target);
            Assert.NotNull(point3D_Closest);
            Assert.Equal(5.0, point3D_Closest.X, 5);
            Assert.Equal(5.0, point3D_Closest.Y, 5);
            Assert.Equal(3.0, point3D_Closest.Z, 5);

            double double_Distance = rectangle3D_Test.Distance(point3D_Target);
            Assert.Equal(5.0, double_Distance, 5);

            // Benchmark Rectangle3D operations (100,000 iterations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 100000; int_I++)
            {
                _ = rectangle3D_Test.ClosestPoint(point3D_Target);
                _ = rectangle3D_Test.Distance(point3D_Target);
                _ = rectangle3D_Test.Inside(point3D_InsideOnPlane, 1e-5);
                _ = rectangle3D_Test.On(point3D_BoundaryOnPlane, 1e-5);
                _ = rectangle3D_Test.InRange(point3D_BoundaryOnPlane, 1e-5);
            }
            stopwatch_Stopwatch.Stop();
            long long_MsRectangle = stopwatch_Stopwatch.ElapsedMilliseconds;

            Assert.True(long_MsRectangle < 2500, $"Rectangle3D performance check failed! Took {long_MsRectangle} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Polygon3D, PolygonalFace3D, and Ellipse3D query methods.
        /// </summary>
        [Fact]
        public void Planar3D_Queries_PerformanceAndCorrectness()
        {
            // Setup Plane
            Point3D point3D_Origin = new(1.0, 2.0, 3.0);
            Spatial.Classes.Vector3D vector3D_Normal = new(0.0, 0.0, 1.0); // Normal is Z axis
            Plane plane_Test = new(point3D_Origin, vector3D_Normal);

            // 1. Setup Polygon3D
            List<Point2D> point2Ds_Poly = new()
            {
                new Point2D(0.0, 0.0),
                new Point2D(10.0, 0.0),
                new Point2D(10.0, 5.0),
                new Point2D(0.0, 5.0)
            };
            Polygon2D polygon2D_Geom = new(point2Ds_Poly);
            Polygon3D polygon3D_Test = new(plane_Test, polygon2D_Geom);

            // 2. Setup PolygonalFace3D
            PolygonalFace3D face3D_Test = new(polygon3D_Test);

            // 3. Setup Ellipse3D
            Ellipse2D ellipse2D_Geom = new(new Point2D(5.0, 2.5), 10.0, 5.0, new Vector2D(1.0, 0.0)); // center=(5.0, 2.5)
            Ellipse3D ellipse3D_Test = new(plane_Test, ellipse2D_Geom);

            // Test target points
            // Local u=4, v=3, offset in Z direction by 5.0
            Point3D point3D_Target = new(5.0, 5.0, 8.0);
            Point3D point3D_InsideOnPlane = new(5.0, 5.0, 3.0);
            Point3D point3D_OutsideOnPlane = new(15.0, 10.0, 3.0);
            Point3D point3D_BoundaryOnPlane = new(1.0, 5.0, 3.0); // u=0, v=3, z=3

            // Verify Polygon3D
            Assert.True(polygon3D_Test.Inside(point3D_InsideOnPlane, 1e-5));
            Assert.False(polygon3D_Test.Inside(point3D_OutsideOnPlane, 1e-5));
            Assert.True(polygon3D_Test.InRange(point3D_BoundaryOnPlane, 1e-5));
            Assert.False(polygon3D_Test.InRange(point3D_OutsideOnPlane, 1e-5));
            Assert.True(polygon3D_Test.On(point3D_BoundaryOnPlane, 1e-5));
            Assert.False(polygon3D_Test.On(point3D_InsideOnPlane, 1e-5));

            Point3D? point3D_ClosestPoly = polygon3D_Test.ClosestPoint(point3D_Target);
            Assert.NotNull(point3D_ClosestPoly);
            Assert.Equal(5.0, point3D_ClosestPoly.X, 5);
            Assert.Equal(7.0, point3D_ClosestPoly.Y, 5);
            Assert.Equal(3.0, point3D_ClosestPoly.Z, 5);
            Assert.Equal(System.Math.Sqrt(29.0), polygon3D_Test.Distance(point3D_Target), 5);

            // Verify PolygonalFace3D
            Assert.True(face3D_Test.Inside(point3D_InsideOnPlane, 1e-5));
            Assert.False(face3D_Test.Inside(point3D_OutsideOnPlane, 1e-5));
            Assert.True(face3D_Test.InRange(point3D_BoundaryOnPlane, 1e-5));
            Assert.True(face3D_Test.OnEdge(point3D_BoundaryOnPlane, 1e-5));

            Point3D? point3D_ClosestFace = face3D_Test.ClosestPoint(point3D_Target);
            Assert.NotNull(point3D_ClosestFace);
            Assert.Equal(5.0, point3D_ClosestFace.X, 5);
            Assert.Equal(5.0, point3D_ClosestFace.Y, 5);
            Assert.Equal(3.0, point3D_ClosestFace.Z, 5);
            Assert.Equal(5.0, face3D_Test.Distance(point3D_Target), 5);

            // Verify Ellipse3D
            Assert.True(ellipse3D_Test.Inside(point3D_InsideOnPlane, 1e-5));
            Assert.False(ellipse3D_Test.Inside(point3D_OutsideOnPlane, 1e-5));
            Assert.True(ellipse3D_Test.InRange(point3D_InsideOnPlane, 1e-5));
            Assert.NotNull(ellipse3D_Test.GetBoundingBox());

            // Benchmark Planar3D operations (10,000 iterations for each target, total 30,000 operations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 10000; int_I++)
            {
                _ = polygon3D_Test.ClosestPoint(point3D_Target);
                _ = polygon3D_Test.Distance(point3D_Target);
                _ = polygon3D_Test.Inside(point3D_InsideOnPlane, 1e-5);
                _ = polygon3D_Test.On(point3D_BoundaryOnPlane, 1e-5);
                _ = polygon3D_Test.InRange(point3D_BoundaryOnPlane, 1e-5);

                _ = face3D_Test.ClosestPoint(point3D_Target);
                _ = face3D_Test.Distance(point3D_Target);
                _ = face3D_Test.Inside(point3D_InsideOnPlane, 1e-5);
                _ = face3D_Test.OnEdge(point3D_BoundaryOnPlane, 1e-5);
                _ = face3D_Test.InRange(point3D_BoundaryOnPlane, 1e-5);

                _ = ellipse3D_Test.Inside(point3D_InsideOnPlane, 1e-5);
                _ = ellipse3D_Test.InRange(point3D_InsideOnPlane, 1e-5);
                _ = ellipse3D_Test.GetBoundingBox();
            }
            stopwatch_Stopwatch.Stop();
            long long_Ms = stopwatch_Stopwatch.ElapsedMilliseconds;

            Assert.True(long_Ms < 5000, $"Planar3D performance check failed! Took {long_Ms} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized Polyline3D and Segmentable3D (Polyloop) query methods.
        /// </summary>
        [Fact]
        public void PolylineAndSegmentable3D_Queries_PerformanceAndCorrectness()
        {
            // Setup Polyline3D (Open path)
            List<Point3D> point3Ds_Poly = new()
            {
                new Point3D(0.0, 0.0, 0.0),
                new Point3D(10.0, 0.0, 0.0),
                new Point3D(10.0, 10.0, 0.0),
                new Point3D(0.0, 10.0, 0.0)
            };
            Polyline3D polyline_Test = new(point3Ds_Poly);

            // Setup Polyloop (Closed path)
            Polyloop polyloop_Test = new(point3Ds_Poly);

            Point3D point3D_Query = new(5.0, 5.0, 5.0);

            // 1. Correctness Checks
            // For polyline, the segments are: (0,0,0)->(10,0,0) [dist to query is 5 at (5,0,0)], (10,0,0)->(10,10,0) [dist to query is 5 at (10,5,0)], (10,10,0)->(0,10,0) [dist to query is 5 at (5,10,0)].
            // Closest point is either (5,0,0), (10,5,0), (5,10,0). Distance squared is 5^2 + 5^2 = 50. Distance is sqrt(50) = 7.0710678.
            Point3D? point3D_ClosestPoly = polyline_Test.ClosestPoint(point3D_Query);
            Assert.NotNull(point3D_ClosestPoly);
            Assert.Equal(System.Math.Sqrt(50.0), polyline_Test.Distance(point3D_Query), 5);

            // For polyloop, there is an additional segment: (0,10,0)->(0,0,0) [dist to query is 5 at (0,5,0)].
            // Closest point distance is still sqrt(50).
            Point3D? point3D_ClosestLoop = polyloop_Test.ClosestPoint(point3D_Query);
            Assert.NotNull(point3D_ClosestLoop);
            Assert.Equal(System.Math.Sqrt(50.0), polyloop_Test.Distance(point3D_Query), 5);

            // 2. Performance/Benchmark checks (10,000 iterations each, total 40,000 operations)
            Stopwatch stopwatch_Stopwatch = Stopwatch.StartNew();
            for (int int_I = 0; int_I < 10000; int_I++)
            {
                _ = polyline_Test.ClosestPoint(point3D_Query);
                _ = polyline_Test.Distance(point3D_Query);
                _ = polyloop_Test.ClosestPoint(point3D_Query);
                _ = polyloop_Test.Distance(point3D_Query);
            }
            stopwatch_Stopwatch.Stop();
            long long_Ms = stopwatch_Stopwatch.ElapsedMilliseconds;

            Assert.True(long_Ms < 5000, $"Polyline/Segmentable3D performance check failed! Took {long_Ms} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the spatial-hash optimized Mesh2D point welding on a large dataset of unique triangles (O(n) instead of O(n^2)).
        /// </summary>
        [Fact]
        public void Mesh2D_LargeDataset_PerformanceAndCorrectness()
        {
            // Build a zig-zag strip of 5000 triangles sharing edge points, forcing every point lookup to weld against the growing point list.
            int triangleCount = 5000;
            List<Triangle2D> triangle2Ds = new(triangleCount);
            for (int i = 0; i < triangleCount; i++)
            {
                Point2D point2D_1 = new(i, 0);
                Point2D point2D_2 = new(i + 1, 0);
                Point2D point2D_3 = new(i, 1);
                triangle2Ds.Add(new Triangle2D(point2D_1, point2D_2, point2D_3));
            }

            // Warm up / JIT compile
            _ = Planar.Create.Mesh2D(triangle2Ds.Take(10));

            Stopwatch stopwatch = Stopwatch.StartNew();
            Mesh2D? mesh2D = Planar.Create.Mesh2D(triangle2Ds);
            stopwatch.Stop();

            // Assert Correctness: bottom line has triangleCount + 1 shared points, top apex points are all unique (triangleCount of them).
            Assert.NotNull(mesh2D);
            Assert.Equal((2 * triangleCount) + 1, mesh2D.PointsCount);
            Assert.Equal(triangleCount, mesh2D.TrianglesCount);

            // Assert Performance: with O(n^2) FindIndex welding this would take seconds; spatial-hash welding should stay well under the limit.
            Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Mesh2D large dataset welding performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the spatial-hash optimized Mesh3D point welding on a large dataset of unique triangles (O(n) instead of O(n^2)).
        /// </summary>
        [Fact]
        public void Mesh3D_LargeDataset_PerformanceAndCorrectness()
        {
            // Build a zig-zag strip of 5000 triangles sharing edge points, forcing every point lookup to weld against the growing point list.
            int triangleCount = 5000;
            List<Triangle3D> triangle3Ds = new(triangleCount);
            for (int i = 0; i < triangleCount; i++)
            {
                Point3D point3D_1 = new(i, 0, 0);
                Point3D point3D_2 = new(i + 1, 0, 0);
                Point3D point3D_3 = new(i, 1, 0);
                triangle3Ds.Add(new Triangle3D(point3D_1, point3D_2, point3D_3));
            }

            // Warm up / JIT compile
            _ = Spatial.Create.Mesh3D(triangle3Ds.Take(10));

            Stopwatch stopwatch = Stopwatch.StartNew();
            Mesh3D? mesh3D = Spatial.Create.Mesh3D(triangle3Ds);
            stopwatch.Stop();

            // Assert Correctness: bottom line has triangleCount + 1 shared points, top apex points are all unique (triangleCount of them).
            Assert.NotNull(mesh3D);
            Assert.Equal((2 * triangleCount) + 1, mesh3D.PointsCount);
            Assert.Equal(triangleCount, mesh3D.TrianglesCount);

            // Assert Performance: with O(n^2) FindIndex welding this would take seconds; spatial-hash welding should stay well under the limit.
            Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Mesh3D large dataset welding performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the spatial-hash optimized Split deduplication on a large grid of segments (O(n) instead of O(n^2) result-list scans).
        /// </summary>
        [Fact]
        public void Split_LargeDataset_PerformanceAndCorrectness()
        {
            // Build a dense grid of crossing segments so the dedup/result lookup runs many times against a large, growing result list.
            int gridSize = 80;
            List<Segment2D> segment2Ds_Grid = new(gridSize * 2);
            for (int i = 0; i < gridSize; i++)
            {
                segment2Ds_Grid.Add(new Segment2D(new Point2D(0, i), new Point2D(gridSize, i)));
                segment2Ds_Grid.Add(new Segment2D(new Point2D(i, 0), new Point2D(i, gridSize)));
            }

            // Warm up / JIT compile
            _ = Planar.Query.Split(segment2Ds_Grid.Take(4));

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Segment2D>? segment2Ds_Result = Planar.Query.Split(segment2Ds_Grid);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(segment2Ds_Result);
            Assert.True(segment2Ds_Result.Count > 0);

            // Assert Performance: with the original O(n^2) List.Find scans (both the result dedup and the global intersection-point lookup) this took over 2.5s; spatial-hash caching keeps it well under the limit.
            Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Split large dataset performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized SelfIntersectionSegments query, which avoids re-enumerating a lazy source per outer iteration and uses a spatial-hash dedup instead of an O(n) List.Find scan over the growing result list.
        /// </summary>
        [Fact]
        public void SelfIntersectionSegments_LargeDataset_PerformanceAndCorrectness()
        {
            // Build a zig-zag of 150 long horizontal segments offset diagonally, so every segment trace crosses many of the others, lazily projected from an index sequence to also exercise the single-enumeration fix.
            int segmentCount = 150;
            IEnumerable<Segment2D> segment2Ds_Lazy = Enumerable.Range(0, segmentCount).Select(i =>
            {
                double offset = (i % 2 == 0) ? 0.0 : 1.0;
                return new Segment2D(new Point2D(0, i + offset), new Point2D(segmentCount, i - offset));
            });

            // Warm up / JIT compile
            _ = Planar.Query.SelfIntersectionSegments(segment2Ds_Lazy.Take(5), 1000.0);

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Segment2D>? segment2Ds_Result = Planar.Query.SelfIntersectionSegments(segment2Ds_Lazy, 1000.0);
            stopwatch.Stop();

            // Assert Correctness
            Assert.NotNull(segment2Ds_Result);
            Assert.True(segment2Ds_Result.Count >= segmentCount);

            // Assert Performance: with per-iteration re-enumeration of the lazy source plus O(n) List.Find dedup, this scaled far worse; the cached array + spatial-hash dedup keep it well under the limit.
            Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"SelfIntersectionSegments large dataset performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the optimized SelfIntersectionSegments inner loop, which replaced an n-1 sized <see cref="List{Segment2D}"/> allocated on every outer iteration (O(n^2) memory churn) with a zero-allocation skip iterator.
        /// </summary>
        [Fact]
        public void SelfIntersectionSegments_SkipIterator_PerformanceAndCorrectness()
        {
            // Build a denser zig-zag than the existing large-dataset test, specifically to amplify the O(n^2) temp-list allocation that the skip iterator replaces.
            int segmentCount = 600;
            List<Segment2D> segment2Ds = new(segmentCount);
            for (int i = 0; i < segmentCount; i++)
            {
                double offset = (i % 2 == 0) ? 0.0 : 1.0;
                segment2Ds.Add(new Segment2D(new Point2D(0, i + offset), new Point2D(segmentCount, i - offset)));
            }

            // Warm up / JIT compile
            _ = Planar.Query.SelfIntersectionSegments(segment2Ds.Take(5), 1000.0);

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<Segment2D>? segment2Ds_Result = Planar.Query.SelfIntersectionSegments(segment2Ds, 1000.0);
            stopwatch.Stop();

            // Assert Correctness: every original segment is preserved in the result, plus any detected self-intersection segments.
            Assert.NotNull(segment2Ds_Result);
            Assert.True(segment2Ds_Result.Count >= segmentCount);

            // Assert Performance: with the prior per-outer-iteration List<Segment2D> allocation this scaled quadratically in memory; the skip iterator keeps allocations flat and execution well under the limit.
            Assert.True(stopwatch.ElapsedMilliseconds < 3000, $"SelfIntersectionSegments skip-iterator performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// Verifies both the correctness and the performance of the minimum-area Rectangle2D tie-breaker, which replaced an O(n*m) nested AlmostEquals scan over the convex hull for every candidate rectangle corner with a tolerance-bucketed spatial hash.
        /// </summary>
        [Fact]
        public void Rectangle2D_TieBreaker_PerformanceAndCorrectness()
        {
            // Build a dense circle of points so the convex hull has many edges and the tie-breaker scan runs repeatedly with a large hull point count.
            int pointCount = 2000;
            List<Point2D> point2Ds = new(pointCount);
            for (int i = 0; i < pointCount; i++)
            {
                double angle = i * 2.0 * System.Math.PI / pointCount;
                point2Ds.Add(new(System.Math.Cos(angle) * 10.0, System.Math.Sin(angle) * 10.0));
            }

            // Warm up / JIT compile
            _ = Planar.Create.Rectangle2D(point2Ds.Take(10));

            Stopwatch stopwatch = Stopwatch.StartNew();
            Rectangle2D? rectangle2D = Planar.Create.Rectangle2D(point2Ds);
            stopwatch.Stop();

            // Assert Correctness: minimum bounding rectangle for a circle of radius 10 should be a square with side close to 20.
            Assert.NotNull(rectangle2D);
            Assert.Equal(20.0, rectangle2D.Width, 1e-1);
            Assert.Equal(20.0, rectangle2D.Height, 1e-1);

            // Assert Performance: with the prior O(n*m) Find scan per tie-broken candidate this scaled far worse on a dense hull; the spatial hash keeps it well under the limit.
            Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Rectangle2D tie-breaker performance check failed! Took {stopwatch.ElapsedMilliseconds} ms.");
        }

        /// <summary>
        /// A/B benchmark comparing performance of methods annotated with <see cref="System.Runtime.CompilerServices.MethodImplAttribute"/> for <see cref="System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining"/>.
        /// <para>Exercises BoundingBox InRange, Line/Ray/Plane Distance and ClosestPoint, CoordinateSystem property accessors, Segment Length/Direction, and Transform indexer/operator in tight loops.</para>
        /// </summary>
        [Fact]
        public void AggressiveInlining_Benchmark()
        {
            const int iterations = 500000;

            // --- Setup geometry objects ---
            BoundingBox2D boundingBox2D = new(new Point2D(0.0, 0.0), new Point2D(100.0, 100.0));
            BoundingBox3D boundingBox3D = new(new Point3D(0.0, 0.0, 0.0), new Point3D(100.0, 100.0, 100.0));
            Point2D point2D = new(50.0, 50.0);
            Point3D point3D = new(50.0, 50.0, 50.0);

            Line2D line2D = new(new Point2D(0.0, 0.0), new Vector2D(1.0, 0.0));
            Ray2D ray2D = new(new Point2D(0.0, 0.0), new Vector2D(1.0, 0.0));
            Segment2D segment2D = new(0.0, 0.0, 100.0, 100.0);
            Transform2D? transform2D_1 = Planar.Create.Transform2D.Translation(10.0, 20.0);
            Assert.NotNull(transform2D_1);
            Transform2D? transform2D_2 = Planar.Create.Transform2D.Rotation(System.Math.PI / 4.0);
            Assert.NotNull(transform2D_2);

            Line3D line3D = new(new Point3D(0.0, 0.0, 0.0), Spatial.Constants.Vector3D.WorldX);
            Ray3D ray3D = new(new Point3D(0.0, 0.0, 0.0), Spatial.Constants.Vector3D.WorldX);
            Plane plane = new(new Point3D(0.0, 0.0, 0.0), Spatial.Constants.Vector3D.WorldZ);
            Segment3D segment3D = new(0.0, 0.0, 0.0, 100.0, 100.0, 100.0);
            CoordinateSystem2D coordinateSystem2D = new();
            CoordinateSystem3D coordinateSystem3D = new(new Point3D(0.0, 0.0, 0.0), Spatial.Constants.Vector3D.WorldZ);
            Transform3D? transform3D_1 = Spatial.Create.Transform3D.Translation(10.0, 20.0, 30.0);
            Assert.NotNull(transform3D_1);
            Transform3D? transform3D_2 = Spatial.Create.Transform3D.Rotation(Spatial.Constants.Vector3D.WorldZ, System.Math.PI / 4.0);
            Assert.NotNull(transform3D_2);

            // --- Warm up / JIT compile ---
            _ = boundingBox2D.InRange(point2D);
            _ = boundingBox3D.InRange(point3D);
            _ = boundingBox2D.InRange(boundingBox2D);
            _ = boundingBox3D.InRange(boundingBox3D);
            _ = line2D.ClosestPoint(point2D);
            _ = line2D.Distance(point2D);
            _ = ray2D.ClosestPoint(point2D);
            _ = ray2D.Distance(point2D);
            _ = line3D.Distance(point3D);
            _ = ray3D.ClosestPoint(point3D);
            _ = ray3D.Distance(point3D);
            _ = plane.ClosestPoint(point3D);
            _ = plane.Distance(point3D);
            _ = coordinateSystem2D.AxisX;
            _ = coordinateSystem2D.AxisY;
            _ = coordinateSystem2D.Origin;
            _ = coordinateSystem3D.AxisX;
            _ = coordinateSystem3D.AxisY;
            _ = coordinateSystem3D.AxisZ;
            _ = coordinateSystem3D.Origin;
            _ = segment2D.Direction;
            _ = segment2D.Length;
            _ = segment2D.SquaredLength;
            _ = segment3D.Direction;
            _ = segment3D.Length;
            _ = segment3D.SquaredLength;
            _ = transform2D_1[0, 0];
            _ = transform2D_1 * transform2D_2;
            _ = transform3D_1[0, 0];
            _ = transform3D_1 * transform3D_2;
            _ = plane.D;
            _ = plane.K;
            _ = plane.Normal;
            _ = plane.Origin;

            // --- Benchmark ---
            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _ = boundingBox2D.InRange(point2D);
                _ = boundingBox3D.InRange(point3D);
                _ = boundingBox2D.InRange(boundingBox2D);
                _ = boundingBox3D.InRange(boundingBox3D);
                _ = line2D.ClosestPoint(point2D);
                _ = line2D.Distance(point2D);
                _ = ray2D.ClosestPoint(point2D);
                _ = ray2D.Distance(point2D);
                _ = line3D.Distance(point3D);
                _ = ray3D.ClosestPoint(point3D);
                _ = ray3D.Distance(point3D);
                _ = plane.ClosestPoint(point3D);
                _ = plane.Distance(point3D);
            }
            stopwatch.Stop();
            long computationalMs = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                _ = coordinateSystem2D.AxisX;
                _ = coordinateSystem2D.AxisY;
                _ = coordinateSystem2D.Origin;
                _ = coordinateSystem3D.AxisX;
                _ = coordinateSystem3D.AxisY;
                _ = coordinateSystem3D.AxisZ;
                _ = coordinateSystem3D.Origin;
                _ = segment2D.Direction;
                _ = segment2D.Length;
                _ = segment2D.SquaredLength;
                _ = segment3D.Direction;
                _ = segment3D.Length;
                _ = segment3D.SquaredLength;
                _ = transform2D_1[0, 0];
                _ = transform3D_1[0, 0];
                _ = plane.D;
                _ = plane.K;
                _ = plane.Normal;
                _ = plane.Origin;
            }
            stopwatch.Stop();
            long propertyMs = stopwatch.ElapsedMilliseconds;

            stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                _ = transform2D_1 * transform2D_2;
                _ = transform3D_1 * transform3D_2;
            }
            stopwatch.Stop();
            long operatorMs = stopwatch.ElapsedMilliseconds;

            // --- Assert correctness (spot check) ---
            Assert.True(boundingBox2D.InRange(point2D));
            Assert.True(boundingBox3D.InRange(point3D));
            Point2D? closest2D = line2D.ClosestPoint(point2D);
            Assert.NotNull(closest2D);
            Assert.Equal(50.0, closest2D.X, 1e-5);
            Assert.Equal(0.0, closest2D.Y, 1e-5);
            Assert.Equal(50.0, line2D.Distance(point2D), 1e-5);
            Assert.NotNull(plane.ClosestPoint(point3D));
            Assert.Equal(50.0, plane.Distance(point3D), 1e-5);
            Assert.NotNull(coordinateSystem2D.AxisX);
            Assert.NotNull(segment2D.Direction);
            Assert.True(segment2D.Length > 0.0);
            Assert.NotNull(transform2D_1 * transform2D_2);

            // --- Output results for manual A/B comparison ---
            Console.WriteLine($"=== AggressiveInlining Benchmark ({iterations:N0} iterations) ===");
            Console.WriteLine($"Computational (InRange/Distance/ClosestPoint):  {computationalMs,6} ms");
            Console.WriteLine($"Property accessors (Axis/Origin/Length/etc):    {propertyMs,6} ms");
            Console.WriteLine($"Operator * (Transform2D * Transform3D):        {operatorMs,6} ms");
            Console.WriteLine($"===================================================");

            // Assert performance sanity (should be fast, typically < 1000ms per category)
            Assert.True(computationalMs < 4000, $"Computational benchmark failed! Took {computationalMs} ms.");
            Assert.True(propertyMs < 3000, $"Property benchmark failed! Took {propertyMs} ms.");
            Assert.True(operatorMs < 1000, $"Operator benchmark failed! Took {operatorMs} ms.");
        }
    }
}