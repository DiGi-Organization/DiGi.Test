using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.GIS.PostgreSQL.Classes;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The seed of the synthetic footprints of <see cref="OpenBuildingCountyId_IndexedParity"/>.
        /// </summary>
        private const int seed_IndexedParity = 20261124;

        /// <summary>
        /// The number of random footprints of <see cref="OpenBuildingCountyId_IndexedParity"/>, besides the targeted ones.
        /// </summary>
        private const int count_IndexedParity_Footprints = 3000;

        /// <summary>
        /// Verifies that filing a footprint against the run-local county indexes (<see cref="Query.CountyFilingGeometriesByCountyId"/>) gives exactly the outcome of filing it against the county rings alone - the same county part, the same filtered or rejected - including when the indexes are read concurrently.
        /// <para>The county parts are built to reach every branch of the decision: two parts of 200 points sharing a zigzag boundary (straddling footprints go through the <see cref="Query.CountyId(IDictionary{int, IPolygonal2D}, IPolygonal2D, double)"/> tie-break), a part nested inside one of them (the covering rule), a 21-point island small enough to lie inside a footprint (the one configuration where outline and area distances differ, decided by the legacy step), and a four-point part that is never indexed (the fallback to the ring). The footprints are seeded random rectangles and 24-point circles over the whole area, footprints at gaps around the tolerance from a straight edge, and a degenerate two-point ring NetTopologySuite cannot convert. Every footprint is filed with no scope and with a county-list scope. The fact needs no database.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingCountyId_IndexedParity()
        {
            double tolerance = Core.Constants.Tolerance.MacroDistance;

            Dictionary<int, IPolygonal2D> polygonal2Ds_ByCountyId = IndexedParity_Counties();

            Dictionary<int, BoundingBox2D> boundingBox2Ds_ByCountyId = [];
            foreach (KeyValuePair<int, IPolygonal2D> pair in polygonal2Ds_ByCountyId)
            {
                boundingBox2Ds_ByCountyId[pair.Key] = pair.Value.GetBoundingBox()!;
            }

            Dictionary<int, CountyFilingGeometry> countyFilingGeometries_ByCountyId = polygonal2Ds_ByCountyId.CountyFilingGeometriesByCountyId();

            // Indexed where the legacy call converts the county ring (more than 16 points); the square stays on its ring.
            Assert.Equal([10, 20, 30, 50], countyFilingGeometries_ByCountyId.Keys.Order());

            List<Polygon2D> polygon2Ds = IndexedParity_Footprints();

            foreach (HashSet<int>? countyIds_Scope in new HashSet<int>?[] { null, [10, 30, 40] })
            {
                int count = polygon2Ds.Count;

                (int? CountyId, bool Rejected)[] outcomes_Legacy = new (int?, bool)[count];
                for (int i = 0; i < count; i++)
                {
                    int? countyId = Query.OpenBuildingCountyId(polygon2Ds[i].GetBoundingBox(), polygon2Ds[i], null, polygonal2Ds_ByCountyId, null, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, tolerance, out bool rejected);
                    outcomes_Legacy[i] = (countyId, rejected);
                }

                // The indexed side concurrently, the way the conversion workers of DiGi.GIS.PostgreSQL#170 read it.
                (int? CountyId, bool Rejected)[] outcomes_Indexed = new (int?, bool)[count];
                Parallel.For(0, count, new ParallelOptions() { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) }, i =>
                {
                    int? countyId = Query.OpenBuildingCountyId(polygon2Ds[i].GetBoundingBox(), polygon2Ds[i], null, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, tolerance, out bool rejected);
                    outcomes_Indexed[i] = (countyId, rejected);
                });

                for (int i = 0; i < count; i++)
                {
                    Assert.True(outcomes_Legacy[i] == outcomes_Indexed[i], $"Footprint {i} (scope {(countyIds_Scope is null ? "none" : string.Join(",", countyIds_Scope))}): legacy {outcomes_Legacy[i]}, indexed {outcomes_Indexed[i]}");
                }

                // The stream reached every outcome, so the agreement above covers them all.
                Assert.Contains(outcomes_Legacy, x => x.CountyId is not null);
                Assert.Contains(outcomes_Legacy, x => x.CountyId is null && x.Rejected);

                if (countyIds_Scope is null)
                {
                    foreach (int countyId in new int[] { 10, 20, 30, 40, 50 })
                    {
                        Assert.Contains(outcomes_Legacy, x => x.CountyId == countyId);
                    }
                }
                else
                {
                    Assert.Contains(outcomes_Legacy, x => x.CountyId is null && !x.Rejected);
                }
            }
        }

        /// <summary>
        /// Builds the county rings of <see cref="OpenBuildingCountyId_IndexedParity"/>: parts 10 and 20 sharing a zigzag boundary at x = 1000, part 50 nested in part 10, the 21-point island 30 and the four-point square 40.
        /// </summary>
        /// <returns>The rings keyed by part identifier.</returns>
        private static Dictionary<int, IPolygonal2D> IndexedParity_Counties()
        {
            // The shared boundary, bottom to top: a zigzag of 50 steps, used forwards by part 10 and backwards by part 20.
            List<Point2D> point2Ds_Shared = [];
            for (int i = 0; i <= 50; i++)
            {
                point2Ds_Shared.Add(new Point2D(1000 + (i % 2 == 0 ? 0 : 7.5), i * 20.0));
            }

            List<Point2D> point2Ds_10 = [];
            for (int i = 0; i < 50; i++)
            {
                point2Ds_10.Add(new Point2D(i * 20.0, 0));
            }

            point2Ds_10.AddRange(point2Ds_Shared);
            for (int i = 49; i >= 1; i--)
            {
                point2Ds_10.Add(new Point2D(i * 20.0, 1000));
            }

            // The left edge stays straight, so the gap footprints below sit at an exact distance from it.
            for (int i = 50; i >= 1; i--)
            {
                point2Ds_10.Add(new Point2D(0, i * 20.0));
            }

            List<Point2D> point2Ds_20 = [];
            for (int i = 50; i >= 0; i--)
            {
                point2Ds_20.Add(point2Ds_Shared[i]);
            }

            for (int i = 1; i <= 50; i++)
            {
                point2Ds_20.Add(new Point2D(1000 + (i * 20.0), 0));
            }

            for (int i = 1; i <= 50; i++)
            {
                point2Ds_20.Add(new Point2D(2000, i * 20.0));
            }

            for (int i = 49; i >= 1; i--)
            {
                point2Ds_20.Add(new Point2D(1000 + (i * 20.0), 1000));
            }

            return new Dictionary<int, IPolygonal2D>()
            {
                [10] = new Polygon2D(point2Ds_10),
                [20] = new Polygon2D(point2Ds_20),
                [30] = new Polygon2D(IndexedParity_Circle(500, 2500, 5, 21)),
                [40] = new Polygon2D([new Point2D(3000, 0), new Point2D(3100, 0), new Point2D(3100, 100), new Point2D(3000, 100)]),
                [50] = new Polygon2D(IndexedParity_Circle(300, 300, 100, 40)),
            };
        }

        /// <summary>
        /// Builds the footprints of <see cref="OpenBuildingCountyId_IndexedParity"/>: seeded random rectangles and circles, gap footprints around the tolerance, squares holding the island and a degenerate ring.
        /// </summary>
        /// <returns>The footprint rings.</returns>
        private static List<Polygon2D> IndexedParity_Footprints()
        {
            Random random = new(seed_IndexedParity);
            List<Polygon2D> result = [];

            for (int i = 0; i < count_IndexedParity_Footprints; i++)
            {
                double x = -100 + (random.NextDouble() * 3300);
                double y = -100 + (random.NextDouble() * 2800);
                double size = 2 + (random.NextDouble() * 28);

                if (random.NextDouble() < 0.2)
                {
                    result.Add(new Polygon2D(IndexedParity_Circle(x, y, size / 2, 24)));
                    continue;
                }

                double angle = random.NextDouble() * Math.PI;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                double width = size;
                double height = size * (0.3 + (random.NextDouble() * 0.7));

                List<Point2D> point2Ds = [];
                foreach ((double u, double v) in new (double, double)[] { (0, 0), (width, 0), (width, height), (0, height) })
                {
                    point2Ds.Add(new Point2D(x + (u * cos) - (v * sin), y + (u * sin) + (v * cos)));
                }

                result.Add(new Polygon2D(point2Ds));
            }

            // Gaps around the tolerance (1 mm) left of the straight edge x = 0 of part 10.
            foreach (double gap in new double[] { 0, 1e-4, 5e-4, 9.99e-4, 1e-3, 1.001e-3, 2e-3, 1e-2, 1 })
            {
                result.Add(new Polygon2D([new Point2D(-gap - 10, 500), new Point2D(-gap, 500), new Point2D(-gap, 510), new Point2D(-gap - 10, 510)]));
            }

            // Squares around the island of part 30: its outline lies inside the footprint's area.
            result.Add(new Polygon2D([new Point2D(450, 2450), new Point2D(550, 2450), new Point2D(550, 2550), new Point2D(450, 2550)]));
            result.Add(new Polygon2D([new Point2D(490, 2490), new Point2D(510, 2490), new Point2D(510, 2510), new Point2D(490, 2510)]));

            // One footprint per part, inside it, so every part is reached whatever the random stream draws, and
            // footprints straddling the shared zigzag boundary at x = 1000.
            result.Add(new Polygon2D([new Point2D(3040, 40), new Point2D(3060, 40), new Point2D(3060, 60), new Point2D(3040, 60)]));
            result.Add(new Polygon2D([new Point2D(290, 290), new Point2D(310, 290), new Point2D(310, 310), new Point2D(290, 310)]));
            result.Add(new Polygon2D([new Point2D(700, 600), new Point2D(720, 600), new Point2D(720, 620), new Point2D(700, 620)]));
            result.Add(new Polygon2D([new Point2D(1500, 600), new Point2D(1520, 600), new Point2D(1520, 620), new Point2D(1500, 620)]));
            foreach (double offset in new double[] { -12, -4, 0, 3.75, 6, 11 })
            {
                result.Add(new Polygon2D([new Point2D(1000 + offset - 5, 503), new Point2D(1000 + offset + 5, 503), new Point2D(1000 + offset + 5, 517), new Point2D(1000 + offset - 5, 517)]));
            }

            // A ring NetTopologySuite refuses (fewer than four ordinates): the legacy call decides it on both sides.
            result.Add(new Polygon2D([new Point2D(400, 400), new Point2D(410, 410)]));

            return result;
        }

        /// <summary>
        /// Builds a regular polygon approximating a circle.
        /// </summary>
        /// <param name="x">The X coordinate of the centre.</param>
        /// <param name="y">The Y coordinate of the centre.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="count">The number of vertices.</param>
        /// <returns>The vertices, counter-clockwise.</returns>
        private static List<Point2D> IndexedParity_Circle(double x, double y, double radius, int count)
        {
            List<Point2D> result = [];
            for (int i = 0; i < count; i++)
            {
                double angle = 2 * Math.PI * i / count;
                result.Add(new Point2D(x + (radius * Math.Cos(angle)), y + (radius * Math.Sin(angle))));
            }

            return result;
        }
    }
}
