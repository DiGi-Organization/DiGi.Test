using System.Diagnostics;
using System.Reflection;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Visual.Planar.Classes;
using DiGi.Geometry.Visual.Planar.Interfaces;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        private static VisualPoint2D CreateVisualPoint2D(double x, double y)
        {
            return new VisualPoint2D(new Point2D(x, y), null);
        }

        /// <summary>
        /// Verifies that <see cref="VisualCollection2D"/> enumeration reflects the live contents of the collection rather than a defensive copy: a value added after the enumerable is captured is still visible when that enumerable is enumerated, and the returned enumerable is not a materialised <see cref="List{T}"/>.
        /// </summary>
        [Fact]
        public void VisualCollection2D_Enumeration_NoSnapshotCopy()
        {
            VisualCollection2D visualCollection2D = new(Array.Empty<IVisual2D>());
            visualCollection2D.Add(CreateVisualPoint2D(0, 0));

            IEnumerable<IVisual2D> view = visualCollection2D.GetValues()!;
            visualCollection2D.Add(CreateVisualPoint2D(1, 0));

            int count = 0;
            foreach (IVisual2D visual2D in view)
            {
                if (visual2D is not null)
                {
                    count++;
                }
            }

            // A snapshot copy captured before the second Add holds only one element; a live view holds both.
            Assert.Equal(2, count);
            Assert.False(view is List<IVisual2D>, "GetValues() must not materialise a List copy of the collection.");
        }

        /// <summary>
        /// Benchmarks enumeration of a 10,000-element <see cref="VisualCollection2D"/>: one warm-up run plus three measured runs, reporting the range to the reports directory and asserting a coarse in-suite budget.
        /// </summary>
        [Fact]
        public void VisualCollection2D_Enumeration_Performance()
        {
            const int elementCount = 10000;

            VisualCollection2D visualCollection2D = new(Array.Empty<IVisual2D>());
            for (int i = 0; i < elementCount; i++)
            {
                visualCollection2D.Add(CreateVisualPoint2D(i, 0));
            }

            // Warm up / JIT compile before measuring performance.
            int warmUp = 0;
            foreach (IVisual2D visual2D in visualCollection2D)
            {
                if (visual2D is not null)
                {
                    warmUp++;
                }
            }
            Assert.Equal(elementCount, warmUp);

            List<long> elapsedMilliseconds = [];
            for (int run = 0; run < 3; run++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                int count = 0;
                foreach (IVisual2D visual2D in visualCollection2D)
                {
                    if (visual2D is not null)
                    {
                        count++;
                    }
                }
                stopwatch.Stop();
                Assert.Equal(elementCount, count);
                elapsedMilliseconds.Add(stopwatch.ElapsedMilliseconds);
            }

            string? pathReportsDir = DiGi.Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(pathReportsDir));

            long min = elapsedMilliseconds.Min();
            long max = elapsedMilliseconds.Max();
            string reportFilePath = Path.Combine(pathReportsDir!, "VisualCollection2D_Enumeration_Performance.txt");
            File.WriteAllLines(reportFilePath, new[]
            {
                $"elementCount = {elementCount}",
                $"min = {min} ms",
                $"max = {max} ms",
                $"runs = [{string.Join(", ", elapsedMilliseconds)}] ms",
            });

            Assert.True(max < 2000, $"Enumeration of {elementCount} visuals took {max} ms (max of 3 runs); budget is 2000 ms.");
        }
    }
}
