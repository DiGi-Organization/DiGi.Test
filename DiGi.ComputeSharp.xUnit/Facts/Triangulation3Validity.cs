using DiGi.ComputeSharp.Core.Classes;
using DiGi.ComputeSharp.Core.Constants;
using DiGi.ComputeSharp.Spatial.Classes;

namespace DiGi.ComputeSharp.xUnit
{
    /// <summary>
    /// Property-based validity tests for <see cref="Spatial.Create.Triangulation3(Triangle3, Line3, double)"/>.
    /// A correct triangulation must: tile the original triangle (areas sum), contain no degenerate
    /// sub-triangle, and (when an actual split happens) keep both cut points as vertices.
    /// These invariants are independent of the exact sub-triangle layout, so they objectively detect
    /// the logic bugs in the splitting routine. Triangulation3 is a pure-CPU routine, so no GPU is required.
    /// </summary>
    public partial class Facts
    {
        private const double Triangulation_AreaEps = 1e-3;

        private static double Triangulation_Area(Triangle3 triangle)
        {
            double ux = triangle.Point_2.X - triangle.Point_1.X;
            double uy = triangle.Point_2.Y - triangle.Point_1.Y;
            double uz = triangle.Point_2.Z - triangle.Point_1.Z;

            double vx = triangle.Point_3.X - triangle.Point_1.X;
            double vy = triangle.Point_3.Y - triangle.Point_1.Y;
            double vz = triangle.Point_3.Z - triangle.Point_1.Z;

            double cx = (uy * vz) - (uz * vy);
            double cy = (uz * vx) - (ux * vz);
            double cz = (ux * vy) - (uy * vx);

            return 0.5 * Math.Sqrt((cx * cx) + (cy * cy) + (cz * cz));
        }

        private static List<Triangle3> Triangulation_SubTriangles(Triangulation3 triangulation)
        {
            List<Triangle3> result = [];
            Triangle3[] all = [triangulation.triangle_1, triangulation.triangle_2, triangulation.triangle_3, triangulation.triangle_4, triangulation.triangle_5];
            foreach (Triangle3 triangle in all)
            {
                if (!triangle.IsNaN())
                {
                    result.Add(triangle);
                }
            }

            return result;
        }

        private static bool Triangulation_HasVertex(List<Triangle3> triangles, Coordinate3 point, double tolerance)
        {
            foreach (Triangle3 triangle in triangles)
            {
                if (triangle.Point_1.AlmostEquals(point, tolerance) || triangle.Point_2.AlmostEquals(point, tolerance) || triangle.Point_3.AlmostEquals(point, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertTilesAndInheritsSolid(Triangle3 original, Triangulation3 triangulation)
        {
            Assert.False(triangulation.IsNaN(), "Triangulation is NaN (no valid sub-triangles).");

            List<Triangle3> subTriangles = Triangulation_SubTriangles(triangulation);
            Assert.NotEmpty(subTriangles);

            bool originalSolid = original.Solid.ToBool();
            double areaSum = 0;
            foreach (Triangle3 triangle in subTriangles)
            {
                double area = Triangulation_Area(triangle);
                Assert.True(area > Triangulation_AreaEps, $"Degenerate sub-triangle (area={area}): {triangle}");
                areaSum += area;

                Assert.True(triangle.Solid.ToBool() == originalSolid, $"Sub-triangle did not inherit Solid={originalSolid}: {triangle}");
            }

            double originalArea = Triangulation_Area(original);
            double areaTolerance = 1e-3 * Math.Max(1.0, originalArea);
            Assert.True(Math.Abs(areaSum - originalArea) <= areaTolerance, $"Area not conserved: sub-triangle sum={areaSum}, original={originalArea}.");
        }

        private static void AssertValidTriangulation(Triangle3 original, Line3 line, Triangulation3 triangulation, double tolerance)
        {
            AssertTilesAndInheritsSolid(original, triangulation);

            List<Triangle3> subTriangles = Triangulation_SubTriangles(triangulation);

            // When an actual split occurred, both cut points must survive as vertices of the triangulation.
            if (subTriangles.Count > 1)
            {
                Line3Intersection cut = Spatial.Create.Line3Intersection(line, original, tolerance);
                Assert.False(cut.IsNaN());

                Assert.True(Triangulation_HasVertex(subTriangles, cut.Point_1, 1e-4), $"Cut point {cut.Point_1} is missing from the triangulation vertices.");
                if (!cut.Point_2.IsNaN())
                {
                    Assert.True(Triangulation_HasVertex(subTriangles, cut.Point_2, 1e-4), $"Cut point {cut.Point_2} is missing from the triangulation vertices.");
                }
            }
        }

        /// <summary>
        /// A line lying along edge 2 (P3-P1) must return the triangle unsplit, exactly as edges 0 and 1 do.
        /// Exposes the copy-paste bug at Triangulation3.cs:110 where the edge-2 guard tests edge-1 flags.
        /// </summary>
        [Fact]
        public void Triangulation3_LineAlongEdge2_ReturnsUnsplit()
        {
            double tolerance = Tolerance.Distance;

            // P1=(0,0,0) P2=(0,10,0) P3=(10,0,0) -> edge2 (P3-P1) lies on the X axis.
            Triangle3 triangle = new(new Bool(true), 0, 0, 0, 0, 10, 0, 10, 0, 0);

            // Segment lying on edge 2.
            Line3 line = new(new Coordinate3(2, 0, 0), new Coordinate3(8, 0, 0));

            Triangulation3 triangulation = Spatial.Create.Triangulation3(triangle, line, tolerance);

            AssertValidTriangulation(triangle, line, triangulation, tolerance);
        }

        /// <summary>
        /// A line with one cut point on an edge (mid-edge, not a vertex) and the other strictly interior must
        /// keep both cut points. Exposes the OR-instead-of-AND bugs at Triangulation3.cs:224 / :249, which
        /// route mid-edge cases into the "point is on a vertex" branch and drop one of the two cut points.
        /// </summary>
        [Fact]
        public void Triangulation3_PointOnEdgePointInterior_KeepsBothCutPoints()
        {
            double tolerance = Tolerance.Distance;

            Triangle3 triangle = new(new Bool(true), 0, 0, 0, 0, 10, 0, 10, 0, 0);

            // Case 1: segment starts outside (crossing edge 0, the Y axis) and ends strictly interior.
            Line3 line_1 = new(new Coordinate3(-3, 5, 0), new Coordinate3(3, 3, 0));
            Triangulation3 triangulation_1 = Spatial.Create.Triangulation3(triangle, line_1, tolerance);
            AssertValidTriangulation(triangle, line_1, triangulation_1, tolerance);

            // Case 2: segment starts outside (crossing edge 2, the X axis) and ends strictly interior.
            Line3 line_2 = new(new Coordinate3(5, -3, 0), new Coordinate3(3, 3, 0));
            Triangulation3 triangulation_2 = Spatial.Create.Triangulation3(triangle, line_2, tolerance);
            AssertValidTriangulation(triangle, line_2, triangulation_2, tolerance);
        }

        /// <summary>
        /// A line cutting off a corner (one cut point on edge 0, the other on edge 1) must produce a valid,
        /// non-degenerate tiling. Exposes the duplicated-vertex degenerate triangle in the corner-cut block
        /// (e.g. Triangulation3.cs:148) when that candidate triangulation is selected.
        /// </summary>
        [Fact]
        public void Triangulation3_CornerCut_NoDegenerateTriangle()
        {
            double tolerance = Tolerance.Distance;

            // P1=(0,0,0) P2=(10,0,0) P3=(0,10,0). Cut from edge0 (7,0,0) to edge1/hypotenuse (3,7,0), slicing corner P2.
            Triangle3 triangle = new(new Bool(true), 0, 0, 0, 10, 0, 0, 0, 10, 0);
            Line3 line = new(new Coordinate3(7, 0, 0), new Coordinate3(3, 7, 0));

            Triangulation3 triangulation = Spatial.Create.Triangulation3(triangle, line, tolerance);

            AssertValidTriangulation(triangle, line, triangulation, tolerance);
        }

        /// <summary>
        /// Splitting a solid triangle at a point on one of its edges must produce solid sub-triangles.
        /// Exposes the sub-triangles built with the 3-argument Triangle3 constructor (e.g. Triangulation3.cs:37),
        /// which silently default Solid to false instead of inheriting the source triangle's flag.
        /// </summary>
        [Fact]
        public void Triangulation3_PointSplit_InheritsSolid()
        {
            double tolerance = Tolerance.Distance;

            Triangle3 triangle = new(new Bool(true), 0, 0, 0, 0, 10, 0, 10, 0, 0);

            // Point on edge 0 (P1-P2, the Y axis), not a vertex -> splits into two sub-triangles.
            Coordinate3 pointOnEdge = new(0, 5, 0);
            Triangulation3 triangulation = Spatial.Create.Triangulation3(triangle, pointOnEdge, tolerance);

            Assert.True(Triangulation_SubTriangles(triangulation).Count >= 2, "Expected the point-on-edge split to produce multiple sub-triangles.");
            AssertTilesAndInheritsSolid(triangle, triangulation);
        }

        /// <summary>
        /// A through-line that crosses two edges, cutting off each of the three corners in turn, must produce
        /// a valid non-degenerate tiling. Exercises all corner orientations of the split logic (several of which
        /// contained degenerate-vertex copy-paste typos, e.g. Triangulation3.cs:176 / :180).
        /// </summary>
        [Fact]
        public void Triangulation3_CornerCuts_AllCorners()
        {
            double tolerance = Tolerance.Distance;

            Triangle3 triangle = new(new Bool(true), 0, 0, 0, 10, 0, 0, 0, 10, 0);

            // Corner P1 (between the X-axis edge and the Y-axis edge).
            Line3 line_P1 = new(new Coordinate3(5, -2, 0), new Coordinate3(-2, 5, 0));
            AssertValidTriangulation(triangle, line_P1, Spatial.Create.Triangulation3(triangle, line_P1, tolerance), tolerance);

            // Corner P2 (between the X-axis edge and the hypotenuse).
            Line3 line_P2 = new(new Coordinate3(9, -1, 0), new Coordinate3(3, 8, 0));
            AssertValidTriangulation(triangle, line_P2, Spatial.Create.Triangulation3(triangle, line_P2, tolerance), tolerance);

            // Corner P3 (between the hypotenuse and the Y-axis edge).
            Line3 line_P3 = new(new Coordinate3(-2, 8, 0), new Coordinate3(4, 8, 0));
            AssertValidTriangulation(triangle, line_P3, Spatial.Create.Triangulation3(triangle, line_P3, tolerance), tolerance);
        }
    }
}