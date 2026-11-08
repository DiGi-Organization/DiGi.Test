using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;
using System.Diagnostics;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the functionality of splitting a <see cref="Polyhedron"/> using a plane to ensure that the polyhedron is correctly divided into multiple parts.
        /// </summary>
        [Fact]
        public void Split()
        {
            PolygonalFace3D? polygonalFace3D = Create.PolygonalFace3D(Spatial.Constants.Plane.WorldZ, new Point2D(0, 0), new Point2D(0, 10), new Point2D(10, 10), new Point2D(10, 0));

            Assert.NotNull(polygonalFace3D);

            if (polygonalFace3D is null)
            {
                return;
            }

            PolygonalFaceExtrusion polygonalFaceExtrusion = new(polygonalFace3D, Spatial.Constants.Vector3D.WorldZ * 10);

            Assert.NotNull(polygonalFaceExtrusion);

            if (polygonalFaceExtrusion is null)
            {
                return;
            }

            Polyhedron? polyhedron = Create.Polyhedron(polygonalFaceExtrusion);
            Assert.NotNull(polyhedron);

            Assert.True(Query.TrySplit(new Plane(Create.Plane(1.0)), polyhedron, out List<Polyhedron>? polyhedrons));

            Assert.NotNull(polyhedrons);
            if (polyhedrons is null)
            {
                return;
            }

            foreach (Polyhedron polyhedron_Split in polyhedrons)
            {
                Point3D? internalPoint = polyhedron_Split?.GetInternalPoint();
                Assert.NotNull(internalPoint);

                if (internalPoint is null)
                {
                    continue;
                }

                bool inside = polyhedron.Inside(internalPoint);
                Assert.True(inside);
            }
        }

        /// <summary>
        /// Tests splitting a 3D polygonal face with other polygonal faces.
        /// </summary>
        [Fact]
        public void SplitFace()
        {
            Polygon2D polygon2D_1 = new(new List<Point2D>()
            {
                new(0, 0),
                new(10, 0),
                new(10, 10),
                new(0, 10)
            });

            PolygonalFace2D? polygonalFace2D_1 = Planar.Create.PolygonalFace2D(polygon2D_1);
            Assert.NotNull(polygonalFace2D_1);

            PolygonalFace3D polygonalFace3D_1 = new(Spatial.Constants.Plane.WorldZ, polygonalFace2D_1);

            Polygon2D polygon2D_2 = new(new List<Point2D>()
            {
                new(5, 5),
                new(20, 5),
                new(20, 20),
                new(5, 20)
            });

            Polygon3D? polygon3D_2 = Create.Polygon3D(new List<Point3D>()
            {
                new(2, 5, -1),
                new(2, 20, -1),
                new(2, 20, 1),
                new(2, 5, 1)
            });
            Assert.NotNull(polygon3D_2);

            PolygonalFace3D? polygonalFace3D_2 = Create.PolygonalFace3D(polygon3D_2);
            Assert.NotNull(polygonalFace3D_2);

            Polygon3D? polygon3D_3 = Create.Polygon3D(new List<Point3D>()
            {
                new(2, 5, -1),
                new(2, 5, 1),
                new(10, 5, 1),
                new(10, 5, -1)
            });
            Assert.NotNull(polygon3D_3);

            PolygonalFace3D? polygonalFace3D_3 = Create.PolygonalFace3D(polygon3D_3);
            Assert.NotNull(polygonalFace3D_3);

            bool splitSuccess = polygonalFace3D_1.TrySplit(new IPolygonalFace3D[] { polygonalFace3D_2!, polygonalFace3D_3! }, out List<PolygonalFace3D>? polygonalFace3Ds);
            Assert.True(splitSuccess);
            Assert.NotNull(polygonalFace3Ds);
            Assert.NotEmpty(polygonalFace3Ds);
        }

        /// <summary>
        /// Tests the segment split functionality under various geometric conditions and edge cases.
        /// <para>Verifies correct behavior for overlapping, collinear, intersecting, and zero-length segments, as well as duplicate filtering and tolerance boundaries.</para>
        /// </summary>
        [Fact]
        public void Split_Segments()
        {
            double tolerance = 1e-5;

            // Edge Case 1: Null or empty input
            List<Segment2D>? segment2Ds_Null = null;
            Assert.Null(Planar.Query.Split(segment2Ds_Null, tolerance));

            List<Segment2D> segment2Ds_Empty = [];
            List<Segment2D>? segment2Ds_EmptyResult = Planar.Query.Split(segment2Ds_Empty, tolerance);
            Assert.NotNull(segment2Ds_EmptyResult);
            Assert.Empty(segment2Ds_EmptyResult);

            // Edge Case 2: Segment shorter than tolerance (should be ignored)
            List<Segment2D> segment2Ds_Short = [new Segment2D(new Point2D(0, 0), new Point2D(0, tolerance * 0.5))];
            List<Segment2D>? segment2Ds_ShortResult = Planar.Query.Split(segment2Ds_Short, tolerance);
            Assert.NotNull(segment2Ds_ShortResult);
            Assert.Empty(segment2Ds_ShortResult);

            // Edge Case 3: Duplicate segments (should be deduplicated)
            List<Segment2D> segment2Ds_Duplicates =
            [
                new Segment2D(new Point2D(0, 0), new Point2D(10, 0)),
                new Segment2D(new Point2D(0, 0), new Point2D(10, 0)),
                new Segment2D(new Point2D(10, 0), new Point2D(0, 0)) // Reversed duplicate
            ];
            List<Segment2D>? segment2Ds_DuplicatesResult = Planar.Query.Split(segment2Ds_Duplicates, tolerance);
            Assert.NotNull(segment2Ds_DuplicatesResult);
            Assert.Single(segment2Ds_DuplicatesResult);
            Assert.True(Planar.Query.Similar(segment2Ds_DuplicatesResult[0], new Segment2D(new Point2D(0, 0), new Point2D(10, 0)), tolerance));

            // Edge Case 4: Non-intersecting segments
            List<Segment2D> segment2Ds_NoIntersection =
            [
                new Segment2D(new Point2D(0, 0), new Point2D(10, 0)),
                new Segment2D(new Point2D(0, 5), new Point2D(10, 5))
            ];
            List<Segment2D>? segment2Ds_NoIntersectionResult = Planar.Query.Split(segment2Ds_NoIntersection, tolerance);
            Assert.NotNull(segment2Ds_NoIntersectionResult);
            Assert.Equal(2, segment2Ds_NoIntersectionResult.Count);

            // Edge Case 5: T-junction (endpoint on interior of another segment)
            List<Segment2D> segment2Ds_TJunction =
            [
                new Segment2D(new Point2D(0, 0), new Point2D(10, 0)),
                new Segment2D(new Point2D(5, 0), new Point2D(5, 5))
            ];
            List<Segment2D>? segment2Ds_TJunctionResult = Planar.Query.Split(segment2Ds_TJunction, tolerance);
            Assert.NotNull(segment2Ds_TJunctionResult);
            // The horizontal segment should be split into two: (0,0)->(5,0) and (5,0)->(10,0).
            // The vertical segment is untouched. Total: 3 segments.
            Assert.Equal(3, segment2Ds_TJunctionResult.Count);

            // Edge Case 6: Cross-intersection (interior intersection)
            List<Segment2D> segment2Ds_Cross =
            [
                new Segment2D(new Point2D(0, 5), new Point2D(10, 5)),
                new Segment2D(new Point2D(5, 0), new Point2D(5, 10))
            ];
            List<Segment2D>? segment2Ds_CrossResult = Planar.Query.Split(segment2Ds_Cross, tolerance);
            Assert.NotNull(segment2Ds_CrossResult);
            // Both segments split at (5,5) into 2 parts each. Total: 4 segments.
            Assert.Equal(4, segment2Ds_CrossResult.Count);

            // Edge Case 7: Collinear overlapping segments
            List<Segment2D> segment2Ds_CollinearOverlapping =
            [
                new Segment2D(new Point2D(0, 0), new Point2D(6, 0)),
                new Segment2D(new Point2D(4, 0), new Point2D(10, 0))
            ];
            List<Segment2D>? segment2Ds_CollinearOverlappingResult = Planar.Query.Split(segment2Ds_CollinearOverlapping, tolerance);
            Assert.NotNull(segment2Ds_CollinearOverlappingResult);
            // Expected pieces: (0,0)->(4,0), (4,0)->(6,0), and (6,0)->(10,0). Total: 3 segments.
            Assert.Equal(3, segment2Ds_CollinearOverlappingResult.Count);
        }

        /// <summary>
        /// Tests splitting an ellipsoid mesh by a horizontal plane through its center.
        /// <para>Verifies that both above and below meshes are produced, each side is non-empty,
        /// and the total number of triangles increases due to splitting of crossing triangles.</para>
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_Basic()
        {
            Ellipsoid ellipsoid = new(new Point3D(0, 0, 0), 2, 2, 2);

            Mesh3D? mesh3D = Create.Mesh3D(ellipsoid, 8, 12);
            Assert.NotNull(mesh3D);

            Plane plane = Spatial.Constants.Plane.WorldZ;

            Assert.True(Query.TrySplit(plane, mesh3D, out List<Mesh3D>? mesh3Ds_Above, out List<Mesh3D>? mesh3Ds_Below));

            Assert.NotNull(mesh3Ds_Above);
            Assert.NotEmpty(mesh3Ds_Above);
            Assert.NotNull(mesh3Ds_Below);
            Assert.NotEmpty(mesh3Ds_Below);

            int totalTriangles = 0;
            foreach (Mesh3D mesh in mesh3Ds_Above)
            {
                Assert.True(mesh.TrianglesCount > 0);
                totalTriangles += mesh.TrianglesCount;
            }
            foreach (Mesh3D mesh in mesh3Ds_Below)
            {
                Assert.True(mesh.TrianglesCount > 0);
                totalTriangles += mesh.TrianglesCount;
            }

            Assert.True(totalTriangles >= mesh3D.TrianglesCount);

            // Test convenience overload
            Assert.True(Query.TrySplit(plane, mesh3D, out List<Mesh3D>? result));
            Assert.NotNull(result);
            Assert.Equal(mesh3Ds_Above.Count + mesh3Ds_Below.Count, result.Count);
        }

        /// <summary>
        /// Tests splitting a mesh that is entirely above the plane.
        /// <para>All triangles go to the above list; below is empty.</para>
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_FullyAbove()
        {
            Ellipsoid ellipsoid = new(new Point3D(0, 0, 10), 2, 2, 2);

            Mesh3D? mesh3D = Create.Mesh3D(ellipsoid, 8, 12);
            Assert.NotNull(mesh3D);

            Plane plane = Spatial.Constants.Plane.WorldZ;

            Assert.True(Query.TrySplit(plane, mesh3D, out List<Mesh3D>? mesh3Ds_Above, out List<Mesh3D>? mesh3Ds_Below));

            Assert.NotNull(mesh3Ds_Above);
            Assert.NotEmpty(mesh3Ds_Above);
            Assert.NotNull(mesh3Ds_Below);
            Assert.Empty(mesh3Ds_Below);
        }

        /// <summary>
        /// Tests splitting a mesh that is entirely below the plane.
        /// <para>All triangles go to the below list; above is empty.</para>
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_FullyBelow()
        {
            Ellipsoid ellipsoid = new(new Point3D(0, 0, -10), 2, 2, 2);

            Mesh3D? mesh3D = Create.Mesh3D(ellipsoid, 8, 12);
            Assert.NotNull(mesh3D);

            Plane plane = Spatial.Constants.Plane.WorldZ;

            Assert.True(Query.TrySplit(plane, mesh3D, out List<Mesh3D>? mesh3Ds_Above, out List<Mesh3D>? mesh3Ds_Below));

            Assert.NotNull(mesh3Ds_Above);
            Assert.Empty(mesh3Ds_Above);
            Assert.NotNull(mesh3Ds_Below);
            Assert.NotEmpty(mesh3Ds_Below);
        }

        /// <summary>
        /// Tests splitting with null and invalid inputs.
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_NullInputs()
        {
            Ellipsoid ellipsoid = new(new Point3D(0, 0, 0), 2, 2, 2);

            Mesh3D? mesh3D = Create.Mesh3D(ellipsoid, 8, 12);
            Assert.NotNull(mesh3D);

            Plane? plane = Spatial.Constants.Plane.WorldZ;

            Assert.False(Query.TrySplit(null as Plane, mesh3D, out List<Mesh3D>? mesh3Ds_Above_Null, out List<Mesh3D>? mesh3Ds_Below_Null));
            Assert.Null(mesh3Ds_Above_Null);
            Assert.Null(mesh3Ds_Below_Null);

            Assert.False(Query.TrySplit(plane, null as Mesh3D, out List<Mesh3D>? mesh3Ds_Above_Null2, out List<Mesh3D>? mesh3Ds_Below_Null2));
            Assert.Null(mesh3Ds_Above_Null2);
            Assert.Null(mesh3Ds_Below_Null2);

            Assert.False(Query.TrySplit(null as Plane, null as Mesh3D, out List<Mesh3D>? mesh3Ds_Above_Null3, out List<Mesh3D>? mesh3Ds_Below_Null3));
            Assert.Null(mesh3Ds_Above_Null3);
            Assert.Null(mesh3Ds_Below_Null3);
        }

        /// <summary>
        /// Tests splitting a coplanar mesh where all triangles lie on the plane.
        /// <para>The split returns false since no triangles are on either side.</para>
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_Coplanar()
        {
            Point3D point3D_1 = new(0, 0, 0);
            Point3D point3D_2 = new(10, 0, 0);
            Point3D point3D_3 = new(0, 10, 0);

            Mesh3D mesh3D = new(new List<Point3D> { point3D_1, point3D_2, point3D_3 }, new List<int[]> { new int[] { 0, 1, 2 } });

            Plane plane = Spatial.Constants.Plane.WorldZ;

            Assert.False(Query.TrySplit(plane, mesh3D, out List<Mesh3D>? mesh3Ds_Above, out List<Mesh3D>? mesh3Ds_Below));
            Assert.Null(mesh3Ds_Above);
            Assert.Null(mesh3Ds_Below);
        }

        /// <summary>
        /// Tests that vertices in the above mesh are never below the plane beyond tolerance.
        /// <para>After splitting a box mesh by a planar plane, iterates all vertices in the above
        /// and below meshes to verify correct classification.</para>
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_VertexClassification()
        {
            BoundingBox3D boundingBox3D = new(new Point3D(0, 0, 0), new Point3D(6, 6, 6));
            Polyhedron? polyhedron = Create.Polyhedron(boundingBox3D);
            Assert.NotNull(polyhedron);

            Mesh3D? mesh3D = Create.Mesh3D(polyhedron);
            Assert.NotNull(mesh3D);

            double tolerance = DiGi.Core.Constants.Tolerance.Distance;
            Plane plane = new(new Point3D(0, 0, 3), Spatial.Constants.Vector3D.WorldZ);

            Assert.True(Query.TrySplit(plane, mesh3D, out List<Mesh3D>? mesh3Ds_Above, out List<Mesh3D>? mesh3Ds_Below, tolerance));

            Assert.NotNull(mesh3Ds_Above);
            Assert.NotEmpty(mesh3Ds_Above);

            Spatial.Classes.Vector3D? normal = plane.Normal;
            Point3D? origin = plane.Origin;
            Assert.NotNull(normal);
            Assert.NotNull(origin);

            double SignedDistance(Point3D point3D)
            {
                return normal.X * (point3D.X - origin.X) + normal.Y * (point3D.Y - origin.Y) + normal.Z * (point3D.Z - origin.Z);
            }

            foreach (Mesh3D mesh in mesh3Ds_Above)
            {
                List<Point3D>? point3Ds = mesh.GetPoints();
                Assert.NotNull(point3Ds);
                foreach (Point3D point3D in point3Ds)
                {
                    Assert.True(SignedDistance(point3D) >= -tolerance);
                }
            }

            Assert.NotNull(mesh3Ds_Below);
            Assert.NotEmpty(mesh3Ds_Below);

            foreach (Mesh3D mesh in mesh3Ds_Below)
            {
                List<Point3D>? point3Ds = mesh.GetPoints();
                Assert.NotNull(point3Ds);
                foreach (Point3D point3D in point3Ds)
                {
                    Assert.True(SignedDistance(point3D) <= tolerance);
                }
            }
        }

        /// <summary>
        /// Tests splitting a mesh that yields multiple disconnected components per side.
        /// <para>Two separated boxes straddle the plane at different XY locations, producing
        /// at least two connected components on each side.</para>
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_DisconnectedComponents()
        {
            BoundingBox3D box_1 = new(new Point3D(-10, -10, -10), new Point3D(-2, -2, 10));
            BoundingBox3D box_2 = new(new Point3D(2, 2, -10), new Point3D(10, 10, 10));

            Polyhedron? polyhedron_1 = Create.Polyhedron(box_1);
            Assert.NotNull(polyhedron_1);
            Polyhedron? polyhedron_2 = Create.Polyhedron(box_2);
            Assert.NotNull(polyhedron_2);

            Mesh3D? mesh3D_1 = Create.Mesh3D(polyhedron_1);
            Assert.NotNull(mesh3D_1);
            Mesh3D? mesh3D_2 = Create.Mesh3D(polyhedron_2);
            Assert.NotNull(mesh3D_2);

            List<Triangle3D>? triangle3Ds_1 = mesh3D_1.GetTriangles();
            List<Triangle3D>? triangle3Ds_2 = mesh3D_2.GetTriangles();
            Assert.NotNull(triangle3Ds_1);
            Assert.NotNull(triangle3Ds_2);

            List<Triangle3D> allTriangles = [.. triangle3Ds_1, .. triangle3Ds_2];
            Mesh3D? combinedMesh = Create.Mesh3D(allTriangles);
            Assert.NotNull(combinedMesh);

            Plane plane = Spatial.Constants.Plane.WorldZ;

            Assert.True(Query.TrySplit(plane, combinedMesh, out List<Mesh3D>? mesh3Ds_Above, out List<Mesh3D>? mesh3Ds_Below));

            Assert.NotNull(mesh3Ds_Above);
            Assert.True(mesh3Ds_Above.Count >= 2);
            Assert.NotNull(mesh3Ds_Below);
            Assert.True(mesh3Ds_Below.Count >= 2);
        }

        /// <summary>
        /// Tests that splitting a single triangle crossing the plane produces the correct number of sub-triangles.
        /// <para>A triangle with one vertex above, one below, and one on the plane should produce one triangle above and one below.</para>
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_SingleTriangle()
        {
            Point3D point3D_Above = new(0, 0, 5);
            Point3D point3D_Below = new(0, 0, -5);
            Point3D point3D_On = new(10, 0, 0);

            Mesh3D mesh3D = new(new List<Point3D> { point3D_Above, point3D_Below, point3D_On }, new List<int[]> { new int[] { 0, 1, 2 } });

            Plane plane = Spatial.Constants.Plane.WorldZ;

            Assert.True(Query.TrySplit(plane, mesh3D, out List<Mesh3D>? mesh3Ds_Above, out List<Mesh3D>? mesh3Ds_Below));

            Assert.NotNull(mesh3Ds_Above);
            Assert.Single(mesh3Ds_Above);
            Assert.True(mesh3Ds_Above[0].TrianglesCount >= 1);
            Assert.NotNull(mesh3Ds_Below);
            Assert.Single(mesh3Ds_Below);
            Assert.True(mesh3Ds_Below[0].TrianglesCount >= 1);
        }

        /// <summary>
        /// Tests the decomposition of a mesh into its connected components.
        /// <para>Verifies that a connected sphere yields one component, a mesh with two disjoint triangles yields two,
        /// and null/empty inputs return null.</para>
        /// </summary>
        [Fact]
        public void Mesh3Ds_ConnectedComponents()
        {
            Ellipsoid ellipsoid = new(new Point3D(0, 0, 0), 2, 2, 2);

            Mesh3D? mesh3D_Sphere = Create.Mesh3D(ellipsoid, 8, 12);
            Assert.NotNull(mesh3D_Sphere);

            List<Mesh3D>? components_Sphere = Create.Mesh3Ds(mesh3D_Sphere);
            Assert.NotNull(components_Sphere);
            Assert.Single(components_Sphere);

            Point3D point3D_A1 = new(0, 0, 0);
            Point3D point3D_A2 = new(1, 0, 0);
            Point3D point3D_A3 = new(0, 1, 0);
            Point3D point3D_B1 = new(10, 10, 0);
            Point3D point3D_B2 = new(11, 10, 0);
            Point3D point3D_B3 = new(10, 11, 0);

            Mesh3D mesh3D_Disjoint = new(new List<Point3D> { point3D_A1, point3D_A2, point3D_A3, point3D_B1, point3D_B2, point3D_B3 }, new List<int[]> { new int[] { 0, 1, 2 }, new int[] { 3, 4, 5 } });

            List<Mesh3D>? components_Disjoint = Create.Mesh3Ds(mesh3D_Disjoint);
            Assert.NotNull(components_Disjoint);
            Assert.Equal(2, components_Disjoint.Count);

            Assert.Null(Create.Mesh3Ds(null as Mesh3D));

            Mesh3D mesh3D_Empty = new(new List<Point3D>(), new List<int[]>());
            Assert.Null(Create.Mesh3Ds(mesh3D_Empty));
        }

        /// <summary>
        /// Tests the performance of splitting a large mesh by a plane.
        /// <para>A sphere mesh with approximately 40 thousand triangles is split and the operation must complete within the stated threshold.</para>
        /// </summary>
        [Fact]
        public void Split_Mesh3D_Plane_Performance()
        {
            Ellipsoid ellipsoid = new(new Point3D(1, 2, 3), 3, 2, 1);
            Plane plane = new(new Point3D(0, 0, 3), Spatial.Constants.Vector3D.WorldX);

            // Warm-up
            Mesh3D? mesh3D_WarmUp = Create.Mesh3D(ellipsoid, 8, 12);
            Assert.NotNull(mesh3D_WarmUp);
            Assert.True(Query.TrySplit(plane, mesh3D_WarmUp, out List<Mesh3D>? _, out List<Mesh3D>? _));

            Mesh3D? mesh3D = Create.Mesh3D(ellipsoid, 100, 200);
            Assert.NotNull(mesh3D);

            Stopwatch stopwatch = Stopwatch.StartNew();
            bool result = Query.TrySplit(plane, mesh3D, out List<Mesh3D>? mesh3Ds_Above, out List<Mesh3D>? mesh3Ds_Below);
            stopwatch.Stop();

            Assert.True(result);
            Assert.NotNull(mesh3Ds_Above);
            Assert.NotNull(mesh3Ds_Below);

            Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"Mesh3D split took {stopwatch.ElapsedMilliseconds} ms, expected less than 1000 ms.");
        }

        /// <summary>
        /// Tests <see cref="Query.TrySplit(IPolygonalFace3D?, IEnumerable{IPolygonalFace3D}?, out List{PolygonalFace3D}?, double)"/> with edge cases including null inputs, non-intersecting faces, and valid splits.
        /// </summary>
        [Fact]
        public void TrySplit_PolygonalFace3D_EdgeCases()
        {
            // Null inputs
            Assert.False(Query.TrySplit(null as IPolygonalFace3D, [], out List<PolygonalFace3D>? result_NullFace));
            Assert.Null(result_NullFace);

            Polygon2D polygon2D = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);
            PolygonalFace2D? face2D = Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(face2D);
            PolygonalFace3D face3D = new(Spatial.Constants.Plane.WorldZ, face2D);

            Assert.False(face3D.TrySplit(null as IEnumerable<IPolygonalFace3D>, out List<PolygonalFace3D>? result_NullCutters));
            Assert.Null(result_NullCutters);

            // Empty cutters
            Assert.False(face3D.TrySplit([], out List<PolygonalFace3D>? result_EmptyCutters));
            Assert.Null(result_EmptyCutters);

            // Non-intersecting cutter face (completely outside)
            Polygon3D? polygon3D_Far = Create.Polygon3D([new Point3D(100, 100, -5), new Point3D(110, 100, -5), new Point3D(110, 100, 5), new Point3D(100, 100, 5)]);
            Assert.NotNull(polygon3D_Far);
            PolygonalFace3D? face3D_Far = Create.PolygonalFace3D(polygon3D_Far);
            Assert.NotNull(face3D_Far);

            Assert.False(face3D.TrySplit([face3D_Far], out List<PolygonalFace3D>? result_FarCutter));
            Assert.Null(result_FarCutter);
        }

        /// <summary>
        /// Tests <see cref="Query.TrySplit{TPolyhedron}(IPolyhedron?, IEnumerable{TPolyhedron}?, out Polyhedron?, double)"/> ensuring that unsplit faces of the polyhedron are preserved when only a subset of faces is cut.
        /// </summary>
        [Fact]
        public void TrySplit_Polyhedron_PreservesUnsplitFaces()
        {
            // Null and empty inputs
            Assert.False(Query.TrySplit<Polyhedron>(null, [], out Polyhedron? result_NullPoly));
            Assert.Null(result_NullPoly);

            BoundingBox3D box = new(new Point3D(0, 0, 0), new Point3D(10, 10, 10));
            Polyhedron? targetPolyhedron = Create.Polyhedron(box);
            Assert.NotNull(targetPolyhedron);

            Assert.False(targetPolyhedron.TrySplit(null as IEnumerable<Polyhedron>, out Polyhedron? result_NullCutters));
            Assert.Null(result_NullCutters);

            // Create a small cutter box that intersects only the bottom/side region of targetPolyhedron
            BoundingBox3D cutterBox = new(new Point3D(5, -5, -5), new Point3D(15, 5, 5));
            Polyhedron? cutterPolyhedron = Create.Polyhedron(cutterBox);
            Assert.NotNull(cutterPolyhedron);

            bool splitResult = targetPolyhedron.TrySplit([cutterPolyhedron], out Polyhedron? resultPolyhedron);
            Assert.True(splitResult);
            Assert.NotNull(resultPolyhedron);

            // The original box had 6 faces. The cutter intersects some faces, splitting them into multiple sub-faces,
            // while the untouched faces (e.g. top face at Z=10) are preserved.
            // Total face count of the resulting polyhedron must be greater than 6.
            Assert.True(resultPolyhedron.Count > 6);
        }

        /// <summary>
        /// Tests <see cref="Query.TrySplit{TPolyhedron}(IEnumerable{TPolyhedron}, out List{Polyhedron}?, double)"/> ensuring that unsplit polyhedrons are preserved in the returned collection when intersecting polyhedrons are split.
        /// </summary>
        [Fact]
        public void TrySplit_PolyhedronsCollection_PreservesUnsplitPolyhedrons()
        {
            // Null input
            Assert.False(Query.TrySplit(null as IEnumerable<Polyhedron>, out List<Polyhedron>? result_Null));
            Assert.Null(result_Null);

            // Create two polyhedrons that intersect each other, plus a third isolated polyhedron
            BoundingBox3D box1 = new(new Point3D(0, 0, 0), new Point3D(10, 10, 10));
            BoundingBox3D box2 = new(new Point3D(5, 5, -5), new Point3D(15, 15, 5));
            BoundingBox3D box3 = new(new Point3D(100, 100, 100), new Point3D(110, 110, 110));

            Polyhedron? poly1 = Create.Polyhedron(box1);
            Polyhedron? poly2 = Create.Polyhedron(box2);
            Polyhedron? poly3 = Create.Polyhedron(box3);
            Assert.NotNull(poly1);
            Assert.NotNull(poly2);
            Assert.NotNull(poly3);

            List<Polyhedron> polyhedrons = [poly1, poly2, poly3];

            bool splitSuccess = polyhedrons.TrySplit(out List<Polyhedron>? resultCollection);
            Assert.True(splitSuccess);
            Assert.NotNull(resultCollection);

            // All 3 polyhedrons (the 2 split ones and the 1 isolated unsplit one) must be present in the output
            Assert.Equal(3, resultCollection.Count);
        }

        /// <summary>
        /// Tests <see cref="Query.TrySplit(Plane?, IPolyhedron?, out List{Polyhedron}?, double)"/> with various geometric conditions including null inputs, non-cutting planes, and valid cuts.
        /// </summary>
        [Fact]
        public void TrySplit_Plane_Polyhedron_EdgeCases()
        {
            BoundingBox3D box = new(new Point3D(0, 0, 0), new Point3D(10, 10, 10));
            Polyhedron? polyhedron = Create.Polyhedron(box);
            Assert.NotNull(polyhedron);

            Plane plane_Z5 = new(new Point3D(0, 0, 5), Spatial.Constants.Vector3D.WorldZ);

            // Null inputs
            Assert.False(Query.TrySplit(null as Plane, polyhedron, out List<Polyhedron>? result_NullPlane));
            Assert.Null(result_NullPlane);

            Assert.False(plane_Z5.TrySplit(null as IPolyhedron, out List<Polyhedron>? result_NullPoly));
            Assert.Null(result_NullPoly);

            // Plane outside polyhedron
            Plane plane_Far = new(new Point3D(0, 0, 100), Spatial.Constants.Vector3D.WorldZ);
            Assert.False(plane_Far.TrySplit(polyhedron, out List<Polyhedron>? result_FarPlane));
            Assert.Null(result_FarPlane);

            // Valid split
            Assert.True(plane_Z5.TrySplit(polyhedron, out List<Polyhedron>? result_Split));
            Assert.NotNull(result_Split);
            Assert.Equal(2, result_Split.Count);
        }

        /// <summary>
        /// Tests <see cref="Create.PolygonalFace3Ds(Plane?, IPolyhedron?, double)"/> on a courtyard prism - an annulus (20 x 20 outer ring, 10 x 10 inner ring) extruded by 10.
        /// <para>The section on the mid plane has to be a single face carrying the courtyard as its internal edge, area 300. Converting each section loop separately used to return a solid outer face plus a solid courtyard face, which is how the storey split of DiGi.GIS.WebAPI.UI#45 filled the courtyard of the stored building model.</para>
        /// </summary>
        [Fact]
        public void PolygonalFace3Ds_Plane_Polyhedron_Courtyard()
        {
            double tolerance = DiGi.Core.Constants.Tolerance.Distance;

            Polyhedron? polyhedron = CourtyardPrism();
            Assert.NotNull(polyhedron);
            Assert.Equal(10, polyhedron.Count);
            Assert.True(polyhedron.IsClosed(tolerance));

            Plane plane = new(new Point3D(0, 0, 5), Spatial.Constants.Vector3D.WorldZ);

            List<PolygonalFace3D>? polygonalFace3Ds = Create.PolygonalFace3Ds(plane, polyhedron, tolerance);
            Assert.NotNull(polygonalFace3Ds);
            Assert.Single(polygonalFace3Ds);

            PolygonalFace3D polygonalFace3D = polygonalFace3Ds[0];
            Assert.NotNull(polygonalFace3D.InternalEdges);
            Assert.Single(polygonalFace3D.InternalEdges);
            Assert.Equal(300, polygonalFace3D.GetArea(), 6);

            // The courtyard centre lies on the plane but not on the section face
            Assert.False(polygonalFace3D.InRange(new Point3D(10, 10, 5), tolerance));
            Assert.True(polygonalFace3D.InRange(new Point3D(2.5, 10, 5), tolerance));

            // A plane above the prism cuts nothing
            Plane plane_Far = new(new Point3D(0, 0, 100), Spatial.Constants.Vector3D.WorldZ);
            Assert.Null(Create.PolygonalFace3Ds(plane_Far, polyhedron, tolerance));

            // Null inputs
            Assert.Null(Create.PolygonalFace3Ds(null, polyhedron, tolerance));
            Assert.Null(Create.PolygonalFace3Ds(plane, null, tolerance));

            // A hole-free box still yields one solid section face
            Polyhedron? polyhedron_Box = Create.Polyhedron(new BoundingBox3D(new Point3D(0, 0, 0), new Point3D(10, 10, 10)));
            Assert.NotNull(polyhedron_Box);

            List<PolygonalFace3D>? polygonalFace3Ds_Box = Create.PolygonalFace3Ds(plane, polyhedron_Box, tolerance);
            Assert.NotNull(polygonalFace3Ds_Box);
            Assert.Single(polygonalFace3Ds_Box);
            Assert.True(polygonalFace3Ds_Box[0].InternalEdges is not { Count: > 0 });
            Assert.Equal(100, polygonalFace3Ds_Box[0].GetArea(), 6);
        }

        /// <summary>
        /// Tests <see cref="Query.TrySplit(Plane?, IPolyhedron?, out List{Polyhedron}?, double)"/> on a courtyard prism: both parts stay closed, each carries exactly one cap on the cutting plane, the cap keeps the courtyard as an internal edge and no face of either part covers the courtyard.
        /// </summary>
        [Fact]
        public void TrySplit_Plane_Polyhedron_Courtyard()
        {
            double tolerance = DiGi.Core.Constants.Tolerance.Distance;

            Polyhedron? polyhedron = CourtyardPrism();
            Assert.NotNull(polyhedron);

            Plane plane = new(new Point3D(0, 0, 5), Spatial.Constants.Vector3D.WorldZ);

            Assert.True(plane.TrySplit(polyhedron, out List<Polyhedron>? polyhedrons));
            Assert.NotNull(polyhedrons);
            Assert.Equal(2, polyhedrons.Count);

            Point3D point3D_Courtyard = new(10, 10, 5);

            foreach (Polyhedron polyhedron_Part in polyhedrons)
            {
                Assert.True(polyhedron_Part.IsClosed(tolerance));

                // Annulus cap + annulus base + 4 outer walls + 4 courtyard walls
                Assert.Equal(10, polyhedron_Part.Count);

                int count_Cap = 0;

                List<IPolygonalFace3D>? polygonalFace3Ds = polyhedron_Part.PolygonalFaces;
                Assert.NotNull(polygonalFace3Ds);

                foreach (IPolygonalFace3D polygonalFace3D in polygonalFace3Ds)
                {
                    BoundingBox3D? boundingBox3D = polygonalFace3D.GetBoundingBox();
                    Assert.NotNull(boundingBox3D);

                    if (System.Math.Abs(boundingBox3D.MinZ - 5) <= tolerance && System.Math.Abs(boundingBox3D.MaxZ - 5) <= tolerance)
                    {
                        count_Cap++;

                        Assert.NotNull(polygonalFace3D.InternalEdges);
                        Assert.Single(polygonalFace3D.InternalEdges);
                        Assert.Equal(300, polygonalFace3D.GetArea(), 6);
                        Assert.False(polygonalFace3D.InRange(point3D_Courtyard, tolerance));
                    }
                }

                Assert.Equal(1, count_Cap);

                // The courtyard stays outside both parts
                Assert.False(polyhedron_Part.InRange(new Point3D(10, 10, 2.5), tolerance));
                Assert.False(polyhedron_Part.InRange(new Point3D(10, 10, 7.5), tolerance));
            }
        }

        /// <summary>
        /// Creates a closed courtyard prism: a 20 x 20 square with a centred 10 x 10 hole extruded by 10 along Z (10 faces).
        /// </summary>
        private static Polyhedron? CourtyardPrism()
        {
            Polygon2D polygon2D_External = new([new Point2D(0, 0), new Point2D(20, 0), new Point2D(20, 20), new Point2D(0, 20)]);
            Polygon2D polygon2D_Internal = new([new Point2D(5, 5), new Point2D(15, 5), new Point2D(15, 15), new Point2D(5, 15)]);

            PolygonalFace2D? polygonalFace2D = Planar.Create.PolygonalFace2D(polygon2D_External, [polygon2D_Internal]);
            if (polygonalFace2D is null)
            {
                return null;
            }

            PolygonalFace3D polygonalFace3D = new(new Plane(new Point3D(0, 0, 0), Spatial.Constants.Vector3D.WorldZ), polygonalFace2D);

            return Create.Polyhedron(polygonalFace3D, new Spatial.Classes.Vector3D(0, 0, 10));
        }

        /// <summary>
        /// Tests <see cref="Query.TrySplit(Plane?, IPolygonalFace3D?, out List{PolygonalFace3D}?, double)"/> with edge cases including null inputs, coplanar face, and valid face split.
        /// </summary>
        [Fact]
        public void TrySplit_Plane_PolygonalFace3D_EdgeCases()
        {
            Polygon2D polygon2D = new([new Point2D(0, 0), new Point2D(10, 0), new Point2D(10, 10), new Point2D(0, 10)]);
            PolygonalFace2D? face2D = Planar.Create.PolygonalFace2D(polygon2D);
            Assert.NotNull(face2D);
            PolygonalFace3D face3D = new(Spatial.Constants.Plane.WorldZ, face2D);

            Plane plane_Cut = new(new Point3D(5, 0, 0), Spatial.Constants.Vector3D.WorldX);

            // Null inputs
            Assert.False(Query.TrySplit(null as Plane, face3D, out List<PolygonalFace3D>? result_NullPlane));
            Assert.Null(result_NullPlane);

            Assert.False(plane_Cut.TrySplit(null as IPolygonalFace3D, out List<PolygonalFace3D>? result_NullFace));
            Assert.Null(result_NullFace);

            // Plane parallel / non-intersecting
            Plane plane_Parallel = new(new Point3D(0, 0, 5), Spatial.Constants.Vector3D.WorldZ);
            Assert.False(plane_Parallel.TrySplit(face3D, out List<PolygonalFace3D>? result_Parallel));
            Assert.Null(result_Parallel);

            // Valid face split
            Assert.True(plane_Cut.TrySplit(face3D, out List<PolygonalFace3D>? result_Cut));
            Assert.NotNull(result_Cut);
            Assert.Equal(2, result_Cut.Count);
        }

        /// <summary>
        /// Tests splitting a 3D polygonal face with custom tolerance larger than default.
        /// <para>Verifies that when a custom tolerance is specified, all split faces are preserved without dropping pieces due to nodalization precision mismatches.</para>
        /// </summary>
        [Fact]
        public void TrySplit_PolygonalFace3D_CustomTolerance()
        {
            // Face with vertices having slight misalignments within custom tolerance 0.05
            Polygon2D polygon2D = new(
            [
                new Point2D(0, 0),
                new Point2D(10, 0.02),
                new Point2D(10.02, 10),
                new Point2D(0, 9.98)
            ]);

            PolygonalFace2D? face2D = Planar.Create.PolygonalFace2D(polygon2D, null, 0.05);
            Assert.NotNull(face2D);

            PolygonalFace3D face3D = new(Spatial.Constants.Plane.WorldZ, face2D);
            Plane plane_Cut = new(new Point3D(5, 0, 0), Spatial.Constants.Vector3D.WorldX);

            Assert.True(plane_Cut.TrySplit(face3D, out List<PolygonalFace3D>? result_Cut, tolerance: 0.05));
            Assert.NotNull(result_Cut);
            Assert.Equal(2, result_Cut.Count);

            double faceArea = face3D.GetArea();
            double totalArea = 0;
            foreach (PolygonalFace3D piece in result_Cut)
            {
                totalArea += piece.GetArea();
            }

            Assert.True(System.Math.Abs(totalArea - faceArea) < 0.5, $"totalArea={totalArea}, faceArea={faceArea}");
        }

        /// <summary>
        /// Tests that splitting a collection of segments hands back one shared position where two segments meet within the tolerance.
        /// <para>The four segments of this fact carry a square whose corners were each recorded twice, two centimetres apart, which is the shape survey data arrives in. Splitting them at a tolerance that covers the gap has to draw both readings onto one position, because a ring whose corners still differ cannot be polygonized and the face assembled from it is dropped - the defect behind issue 3 of DiGi.Geometry, where a roof surface and the walls beside it went missing from a building model cut into storeys.</para>
        /// <para>The polygonization is left on its own default tolerance on purpose: it is the segments which have to come out consistent, not the polygonizer which has to be told to snap them together.</para>
        /// </summary>
        [Fact]
        public void Split_Segment2Ds_SharedEndsAreWelded()
        {
            List<Segment2D> segment2Ds =
            [
                new(new Point2D(0, 0), new Point2D(10, 0.01)),
                new(new Point2D(10.02, 0), new Point2D(10, 10.01)),
                new(new Point2D(10.01, 10.02), new Point2D(0.02, 10)),
                new(new Point2D(0, 10.02), new Point2D(0.01, 0.02))
            ];

            List<Segment2D>? segment2Ds_Split = Planar.Query.Split(segment2Ds, 0.05);

            Assert.NotNull(segment2Ds_Split);
            Assert.Equal(4, segment2Ds_Split.Count);

            List<Point2D> point2Ds = [];
            for (int i = 0; i < segment2Ds_Split.Count; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    Point2D? point2D = segment2Ds_Split[i][j];
                    Assert.NotNull(point2D);

                    if (!point2Ds.Contains(point2D))
                    {
                        point2Ds.Add(point2D);
                    }
                }
            }

            // Eight ends against four corners, so every corner is one position rather than the two it was given as.
            Assert.Equal(4, point2Ds.Count);

            List<PolygonalFace2D>? polygonalFace2Ds = Planar.Create.PolygonalFace2Ds(segment2Ds_Split);

            Assert.NotNull(polygonalFace2Ds);
            Assert.Single(polygonalFace2Ds);
            Assert.True(System.Math.Abs(polygonalFace2Ds[0].GetArea() - 100) < 1, $"The face carries {polygonalFace2Ds[0].GetArea()} square metres instead of about 100.");
        }

        /// <summary>
        /// Measures and reports execution time gains and performance metrics for TrySplit methods across polyhedrons and mesh operations.
        /// </summary>
        [Fact]
        public void TrySplit_PerformanceBenchmark()
        {
            // 1. Mesh3D plane split performance (~40,000 triangles)
            Ellipsoid ellipsoid = new(new Point3D(0, 0, 0), 5, 5, 5);
            Mesh3D? largeMesh = Create.Mesh3D(ellipsoid, 100, 200);
            Assert.NotNull(largeMesh);

            Plane plane = Spatial.Constants.Plane.WorldZ;

            // Warm-up
            Query.TrySplit(plane, largeMesh, out List<Mesh3D>? _, out List<Mesh3D>? _);

            Stopwatch swMesh = Stopwatch.StartNew();
            bool meshSuccess = Query.TrySplit(plane, largeMesh, out List<Mesh3D>? meshAbove, out List<Mesh3D>? meshBelow);
            swMesh.Stop();

            Assert.True(meshSuccess);
            Assert.NotNull(meshAbove);
            Assert.NotNull(meshBelow);

            // 2. Collection of polyhedrons TrySplit performance (50 intersecting box pairs)
            List<Polyhedron> polyhedrons = [];
            for (int i = 0; i < 50; i++)
            {
                BoundingBox3D box = new(new Point3D(i * 2, 0, 0), new Point3D(i * 2 + 5, 5, 5));
                Polyhedron? poly = Create.Polyhedron(box);
                if (poly != null)
                {
                    polyhedrons.Add(poly);
                }
            }

            // Warm-up
            polyhedrons.TrySplit(out List<Polyhedron>? _);

            Stopwatch swPolyCollection = Stopwatch.StartNew();
            bool polyCollectionSuccess = polyhedrons.TrySplit(out List<Polyhedron>? resultPolyCollection);
            swPolyCollection.Stop();

            Assert.True(polyCollectionSuccess);
            Assert.NotNull(resultPolyCollection);

            // 3. Single Polyhedron TrySplit performance against 20 cutting polyhedrons
            BoundingBox3D mainBox = new(new Point3D(0, 0, 0), new Point3D(20, 20, 20));
            Polyhedron? mainPolyhedron = Create.Polyhedron(mainBox);
            Assert.NotNull(mainPolyhedron);

            List<Polyhedron> cutters = [];
            for (int i = 0; i < 20; i++)
            {
                BoundingBox3D cutterBox = new(new Point3D(i, i, -5), new Point3D(i + 2, i + 2, 25));
                Polyhedron? cutter = Create.Polyhedron(cutterBox);
                if (cutter != null)
                {
                    cutters.Add(cutter);
                }
            }

            Stopwatch swPolySingle = Stopwatch.StartNew();
            bool polySingleSuccess = mainPolyhedron.TrySplit(cutters, out Polyhedron? resultSinglePoly);
            swPolySingle.Stop();

            Assert.True(polySingleSuccess);
            Assert.NotNull(resultSinglePoly);

            // Assert performance thresholds
            Assert.True(swMesh.ElapsedMilliseconds < 500, $"Mesh3D split took {swMesh.ElapsedMilliseconds} ms");
            // Collection split: 307-345 ms isolated (Debug), 505 ms in the parallel full-suite run - the ceiling clears the in-suite time.
            Assert.True(swPolyCollection.ElapsedMilliseconds < 1000, $"Polyhedrons collection split took {swPolyCollection.ElapsedMilliseconds} ms");
            Assert.True(swPolySingle.ElapsedMilliseconds < 500, $"Single Polyhedron split took {swPolySingle.ElapsedMilliseconds} ms");
        }
    }
}