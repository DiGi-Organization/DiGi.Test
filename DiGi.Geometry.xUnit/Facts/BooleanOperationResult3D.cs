using DiGi.Core.Interfaces;
using DiGi.Geometry.Core.Enums;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the behavior of <see cref="BooleanOperationResult3D"/> derived classes (IntersectionResult3D, DifferenceResult3D, UnionResult3D).
        /// </summary>
        [Fact]
        public void BooleanOperationResult3D_Correctness()
        {
            // 1. Setup sample geometry
            Point3D point3D_1 = new(1, 2, 3);
            Point3D point3D_2 = new(4, 5, 6);
            List<IGeometry3D> geometry3Ds = [point3D_1, point3D_2];

            // 2. Instantiate and check IntersectionResult3D
            IntersectionResult3D intersectionResult3D = new(geometry3Ds);
            Assert.Equal(BooleanOpertaionType.Intersection, intersectionResult3D.BooleanOpertaionType);
            Assert.True(intersectionResult3D.Any());
            Assert.Equal(2, intersectionResult3D.Count);
            Assert.True(intersectionResult3D.Contains<Point3D>());
            Assert.False(intersectionResult3D.Contains<Line3D>());

            List<Point3D>? points_Intersection = intersectionResult3D.GetGeometry3Ds<Point3D>();
            Assert.NotNull(points_Intersection);
            Assert.Equal(2, points_Intersection.Count);
            Assert.Equal(1.0, points_Intersection[0].X);

            // 3. Instantiate and check DifferenceResult3D
            DifferenceResult3D differenceResult3D = new(geometry3Ds);
            Assert.Equal(BooleanOpertaionType.Difference, differenceResult3D.BooleanOpertaionType);
            Assert.True(differenceResult3D.Any());
            Assert.Equal(2, differenceResult3D.Count);

            // Check indexer and cloning/encapsulation
            IGeometry3D? geometry3D_AtZero = differenceResult3D[0];
            Assert.NotNull(geometry3D_AtZero);
            Assert.IsType<Point3D>(geometry3D_AtZero);
            Assert.NotSame(point3D_1, geometry3D_AtZero); // Must be a clone

            // 4. Instantiate and check UnionResult3D
            UnionResult3D unionResult3D = new(geometry3Ds);
            Assert.Equal(BooleanOpertaionType.Union, unionResult3D.BooleanOpertaionType);
            Assert.True(unionResult3D.Any());
            Assert.Equal(2, unionResult3D.Count);

            // 5. Test Copy Constructors
            UnionResult3D unionResult3D_Copy = new(unionResult3D);
            Assert.Equal(unionResult3D.Count, unionResult3D_Copy.Count);
            Assert.Equal(unionResult3D.BooleanOpertaionType, unionResult3D_Copy.BooleanOpertaionType);

            // 6. Test Cloning
            ISerializableObject? cloneResult = differenceResult3D.Clone();
            Assert.NotNull(cloneResult);
            Assert.IsType<DifferenceResult3D>(cloneResult);
            DifferenceResult3D differenceResult3D_Clone = (DifferenceResult3D)cloneResult;
            Assert.Equal(differenceResult3D.Count, differenceResult3D_Clone.Count);
            Assert.Equal(differenceResult3D.BooleanOpertaionType, differenceResult3D_Clone.BooleanOpertaionType);

            // 7. Test JSON serialization and deserialization via DiGi.Core.Query.Clone
            UnionResult3D? unionResult3D_Deserialized = DiGi.Core.Query.Clone(unionResult3D);
            Assert.NotNull(unionResult3D_Deserialized);
            Assert.Equal(unionResult3D.Count, unionResult3D_Deserialized.Count);
            Assert.Equal(unionResult3D.BooleanOpertaionType, unionResult3D_Deserialized.BooleanOpertaionType);
        }
    }
}