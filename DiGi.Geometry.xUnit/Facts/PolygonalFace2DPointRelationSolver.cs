using DiGi.Geometry.Core.Enums;
using DiGi.Geometry.Planar;
using DiGi.Geometry.Planar.Classes;
using System.Diagnostics;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="PolygonalFace2DPointRelationSolver"/> classifies the same way as <see cref="PolygonalFace2D.Inside(Point2D, double)"/> and <see cref="PolygonalFace2D.InRange(Point2D, double)"/> on a face with a hole: random points over the bounding box and its margin, plus the boundary cases the tolerance decides (on an edge, just beside it within tolerance, just beyond, on a vertex, on the hole's rim).
        /// </summary>
        [Fact]
        public void PolygonalFace2DPointRelationSolver_MatchesPolygonalFace2D()
        {
            double tolerance = 0.01;

            Polygon2D externalEdge = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 4), new Point2D(6, 4), new Point2D(6, 8), new Point2D(10, 8), new Point2D(10, 12), new Point2D(0, 12)]);
            Polygon2D internalEdge = new([new Point2D(2, 2), new Point2D(4, 2), new Point2D(4, 4), new Point2D(2, 4)]);
            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(externalEdge, [internalEdge]);
            Assert.NotNull(polygonalFace2D);

            PolygonalFace2DPointRelationSolver polygonalFace2DPointRelationSolver = new(polygonalFace2D, tolerance);
            Assert.Equal(12, polygonalFace2DPointRelationSolver.EdgeCount);

            List<Point2D> point2Ds =
            [
                new Point2D(1, 1),          // inside
                new Point2D(3, 3),          // in the hole
                new Point2D(8, 6),          // in the notch, outside
                new Point2D(20, 20),        // far outside
                new Point2D(5, 0.005),      // on the bottom edge, within tolerance
                new Point2D(5, 0.02),       // beside the bottom edge, beyond tolerance
                new Point2D(10, 4),         // on a vertex
                new Point2D(2.005, 3),      // on the hole's rim
                new Point2D(1.98, 3),       // beside the hole's rim, beyond tolerance, inside
                new Point2D(6, 6),          // on the notch's vertical edge
                new Point2D(-0.005, 6),     // beside the left edge, within tolerance
                new Point2D(0, 12),         // on a vertex
            ];

            System.Random random = new(5);
            for (int i = 0; i < 2000; i++)
            {
                point2Ds.Add(new Point2D(-1 + random.NextDouble() * 12, -1 + random.NextDouble() * 14));
            }

            foreach (Point2D point2D in point2Ds)
            {
                polygonalFace2DPointRelationSolver.Input = point2D;
                Assert.True(polygonalFace2DPointRelationSolver.Solve());

                PointRelation pointRelation = polygonalFace2DPointRelationSolver.Output;
                bool inside = polygonalFace2D.Inside(point2D, tolerance);
                bool inRange = polygonalFace2D.InRange(point2D, tolerance);

                Assert.True(inside == (pointRelation == PointRelation.Inside), $"Inside differs at ({point2D.X}, {point2D.Y}): face {inside}, solver {pointRelation}.");
                Assert.True(inRange == (pointRelation != PointRelation.Outside), $"InRange differs at ({point2D.X}, {point2D.Y}): face {inRange}, solver {pointRelation}.");
            }

            polygonalFace2DPointRelationSolver.Input = new Point2D(5, 0.005);
            polygonalFace2DPointRelationSolver.Solve();
            Assert.Equal(PointRelation.On, polygonalFace2DPointRelationSolver.Output);

            polygonalFace2DPointRelationSolver.Input = new Point2D(3, 3);
            polygonalFace2DPointRelationSolver.Solve();
            Assert.Equal(PointRelation.Outside, polygonalFace2DPointRelationSolver.Output);

            polygonalFace2DPointRelationSolver.Input = new Point2D(1, 1);
            polygonalFace2DPointRelationSolver.Solve();
            Assert.Equal(PointRelation.Inside, polygonalFace2DPointRelationSolver.Output);

            polygonalFace2DPointRelationSolver.Input = null;
            Assert.False(polygonalFace2DPointRelationSolver.Solve());
            Assert.Equal(PointRelation.Undefined, polygonalFace2DPointRelationSolver.Output);
        }

        /// <summary>
        /// Verifies that a solver built over a null face, or a face without a usable ring, never classifies and reports no edges.
        /// </summary>
        [Fact]
        public void PolygonalFace2DPointRelationSolver_NoFace()
        {
            PolygonalFace2DPointRelationSolver polygonalFace2DPointRelationSolver = new(null);
            Assert.Equal(0, polygonalFace2DPointRelationSolver.EdgeCount);

            polygonalFace2DPointRelationSolver.Input = new Point2D(1, 1);
            Assert.False(polygonalFace2DPointRelationSolver.Solve());
            Assert.Equal(PointRelation.Undefined, polygonalFace2DPointRelationSolver.Output);
        }

        /// <summary>
        /// Verifies that the solver stays cheap on a large outline: a 4 000-vertex ring against 100 000 points classifies in well under a second after a warm-up, where <see cref="PolygonalFace2D.Inside(Point2D, double)"/> walks every vertex per point, and that both agree on every point (DiGi-Organization/DiGi.Geometry#5).
        /// </summary>
        [Fact]
        public void PolygonalFace2DPointRelationSolver_LargeOutline()
        {
            double tolerance = 0.01;
            int vertexCount = 4000;
            int pointCount = 100000;

            // A star-shaped ring with a jagged radius, so every query crosses many edges' vertical spans.
            System.Random random = new(7);
            List<Point2D> ring = [];
            for (int i = 0; i < vertexCount; i++)
            {
                double angle = 2 * System.Math.PI * i / vertexCount;
                double radius = 900 + 100 * random.NextDouble();
                ring.Add(new Point2D(1000 + radius * System.Math.Cos(angle), 1000 + radius * System.Math.Sin(angle)));
            }

            PolygonalFace2D? polygonalFace2D = Create.PolygonalFace2D(new Polygon2D(ring));
            Assert.NotNull(polygonalFace2D);

            List<Point2D> point2Ds = [];
            for (int i = 0; i < pointCount; i++)
            {
                point2Ds.Add(new Point2D(random.NextDouble() * 2000, random.NextDouble() * 2000));
            }

            PolygonalFace2DPointRelationSolver polygonalFace2DPointRelationSolver = new(polygonalFace2D, tolerance);
            Assert.Equal(vertexCount, polygonalFace2DPointRelationSolver.EdgeCount);

            // Warm-up
            polygonalFace2DPointRelationSolver.Input = point2Ds[0];
            polygonalFace2DPointRelationSolver.Solve();

            Stopwatch stopwatch = Stopwatch.StartNew();
            int insideCount = 0;
            for (int i = 0; i < pointCount; i++)
            {
                polygonalFace2DPointRelationSolver.Input = point2Ds[i];
                polygonalFace2DPointRelationSolver.Solve();
                if (polygonalFace2DPointRelationSolver.Output == PointRelation.Inside)
                {
                    insideCount++;
                }
            }
            stopwatch.Stop();

            Assert.True(insideCount > pointCount / 2, $"Expected most points inside the star, got {insideCount}.");
            Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Solver sweep took {stopwatch.ElapsedMilliseconds} ms.");

            for (int i = 0; i < 2000; i++)
            {
                polygonalFace2DPointRelationSolver.Input = point2Ds[i];
                polygonalFace2DPointRelationSolver.Solve();
                Assert.Equal(polygonalFace2D.Inside(point2Ds[i], tolerance), polygonalFace2DPointRelationSolver.Output == PointRelation.Inside);
            }
        }
    }
}
