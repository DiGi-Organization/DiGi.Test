using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Classes;
using DiGi.Geometry.Spatial.Classes;
using System.Globalization;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="BuildingModel.GetShells{TSpace}(IEnumerable{TSpace}, Geometry.Core.Enums.Side?, Geometry.Core.Enums.Orientation?, Geometry.Core.Enums.Orientation?, double)"/> throws for a space whose components yield no polygonal face geometry, instead of emitting a zero-face shell.
        /// </summary>
        [Fact]
        public void BuildingModelGetShells_ZeroFaceShell_Throws()
        {
            PointAir pointAir = new(new Point3D(5, 5, 0));

            Space space = new(new Point3D(5, 5, 0), "Space 1");

            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Assign(pointAir, space));

            List<Space> spaces = [space];
            Assert.Throws<InvalidOperationException>(() => buildingModel.GetShells(spaces));
        }

        /// <summary>
        /// Measures the execution time of <see cref="BuildingModel.GetShells{TSpace}(IEnumerable{TSpace}, Geometry.Core.Enums.Side?, Geometry.Core.Enums.Orientation?, Geometry.Core.Enums.Orientation?, double)"/> over a model of three hundred spaces bound by eight floor components each.
        /// <para>The range of three measured runs is written to the test reports directory and the slowest run is asserted against the threshold.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetShells_Performance()
        {
            const int count_Spaces = 300;
            const int count_ComponentsPerSpace = 8;

            PolygonalFace3D polygonalFace3D = BuildingModelUnassign_PolygonalFace3D();

            Point3D point3D = new(5, 5, 0);

            List<Space> spaces = [];
            BuildingModel buildingModel = new();
            for (int i = 0; i < count_Spaces; i++)
            {
                Space space = new(point3D, string.Format(CultureInfo.CurrentCulture, "Space {0}", i));
                spaces.Add(space);
                for (int j = 0; j < count_ComponentsPerSpace; j++)
                {
                    FaceFloor faceFloor = new(polygonalFace3D);
                    Assert.True(buildingModel.Assign(faceFloor, space));
                }
            }

            List<Shell>? shells_Warmup = buildingModel.GetShells(spaces);
            Assert.NotNull(shells_Warmup);
            Assert.Equal(count_Spaces, shells_Warmup!.Count);

            List<long> durations = [];
            for (int i = 0; i < 3; i++)
            {
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
                List<Shell>? shells = buildingModel.GetShells(spaces);
                stopwatch.Stop();

                Assert.NotNull(shells);
                Assert.Equal(count_Spaces, shells!.Count);

                durations.Add(stopwatch.ElapsedMilliseconds);
            }

            string? pathReportsDirectory = Core.xUnit.Query.ReportsDirectory(System.Reflection.Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(pathReportsDirectory));

            string pathReport = Path.Combine(pathReportsDirectory!, "BuildingModelGetShells_Performance.txt");
            File.WriteAllLines(pathReport, [.. durations.Select(x => string.Format(CultureInfo.CurrentCulture, "{0} ms", x))]);

            Assert.True(durations.Max() < 2000, string.Format(CultureInfo.CurrentCulture, "BuildingModel.GetShells failed threshold! Elapsed: {0} ms.", durations.Max()));
        }
    }
}
