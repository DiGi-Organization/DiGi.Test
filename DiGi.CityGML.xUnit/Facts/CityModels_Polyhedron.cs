using DiGi.CityGML.Classes;
using DiGi.CityGML.Interfaces;
using DiGi.Geometry.Spatial;
using DiGi.Geometry.Spatial.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DiGi.CityGML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The distance within which two vertices of neighbouring faces are treated as one when the closure of a polyhedron is evaluated.
        /// <para>Ten millimetres, which is coarse enough to absorb the vertex displacement introduced by fitting a plane to a ring which is not exactly planar, and far below the size of any real feature of a building.</para>
        /// </summary>
        private const double tolerance_Weld = 0.01;

        /// <summary>
        /// The distance within which two vertices of neighbouring faces are treated as one for the buildings which the finer tolerance does not close.
        /// <para>Fifty millimetres, which every building of every fixture closes at. The detailed buildings of the 2476 fixture carry rings which are out of plane by up to 17 mm, so fitting a plane to them displaces a shared vertex by more than the finer tolerance allows.</para>
        /// </summary>
        private const double tolerance_Weld_Coarse = 0.05;

        /// <summary>
        /// Tests that every building of every fixture converts into a closed, two-manifold polyhedron once vertices are welded within fifty millimetres.
        /// <para>This is the guarantee the per-fixture facts below break down: across all eleven buildings of the three fixtures, no polyhedron has a naked edge at this tolerance.</para>
        /// <para>Fifty millimetres is coarse, so the two-manifold form of the check is what makes the result trustworthy. Welding too greedily would merge vertices that belong apart and leave edges shared by three or more faces, which <see cref="Geometry.Spatial.Query.IsClosed{TPolygonalFace3D}(Polyhedron{TPolygonalFace3D}?, bool, double)"/> rejects; requiring every edge to be used exactly twice therefore rules out a closure reached by collapsing the model.</para>
        /// </summary>
        /// <param name="fileName">The fixture to load.</param>
        /// <param name="count_Buildings">The number of buildings expected in the fixture.</param>
        [Theory]
        [InlineData("0201_M-33-19-B-d-3-2.gml", 2)]
        [InlineData("2862_N-34-77-D-b-1-1.gml", 3)]
        [InlineData("2476_CityGML.zip", 6)]
        public void Polyhedron_FromCityGML_Closed(string fileName, int count_Buildings)
        {
            List<Building> buildings = CityGML_Buildings(fileName);

            Assert.Equal(count_Buildings, buildings.Count);

            foreach (Building building in buildings)
            {
                Polyhedron polyhedron = CityGML_Polyhedron(building);

                string message = string.Format(CultureInfo.CurrentCulture, "Building {0} of {1}, {2} faces", building.UniqueId, fileName, polyhedron.Count);

                Assert.True(polyhedron.IsClosed(tolerance_Weld_Coarse), message);
                Assert.True(polyhedron.IsClosed(true, tolerance_Weld_Coarse), message);
            }
        }

        /// <summary>
        /// Tests that the LOD1 fixture converts into closed, two-manifold polyhedra at the default tolerance.
        /// <para>Both buildings are extruded prisms whose rings are planar to within 4E-11 m, so no vertex moves when the ring is fitted to a plane and every shared edge still matches exactly. This is the baseline the LOD2 fixtures are measured against.</para>
        /// <para>The fixture also covers the lod1Solid fallback of <see cref="Convert.ToCityGML_Building(System.Xml.XmlNode?)"/>, which reaches the polygons through a composite surface rather than through boundedBy.</para>
        /// </summary>
        [Fact]
        public void Polyhedron_FromCityGML_0201()
        {
            List<Building> buildings = CityGML_Buildings("0201_M-33-19-B-d-3-2.gml");

            Assert.Equal(2, buildings.Count);

            foreach (Building building in buildings)
            {
                Polyhedron polyhedron = CityGML_Polyhedron(building);

                Assert.Equal(6, polyhedron.Count);

                Assert.True(polyhedron.IsClosed(), building.UniqueId);
                Assert.True(polyhedron.IsClosed(true), building.UniqueId);

                Assert.True(polyhedron.IsClosed(tolerance_Weld), building.UniqueId);
                Assert.True(polyhedron.IsClosed(true, tolerance_Weld), building.UniqueId);
            }
        }

        /// <summary>
        /// Tests that the LOD2 fixture converts into closed, two-manifold polyhedra once vertices are welded within ten millimetres.
        /// <para>All three buildings are watertight in the source file - every edge of every ring is shared by exactly two polygons - but they only report closed at the coarser tolerance.</para>
        /// <para>The reason is that <see cref="Geometry.Spatial.Create.Polygon3D(System.Collections.Generic.IEnumerable{Point3D?}?, double)"/> fits each face its own plane and projects the vertices onto it. Four of the twenty rings of this fixture are not exactly planar, by up to 1.7 mm, so neighbouring faces place a shared vertex at slightly different positions and the edge no longer welds at the default tolerance of 1E-06. The assertion at the default tolerance records that gap rather than hiding it.</para>
        /// </summary>
        [Fact]
        public void Polyhedron_FromCityGML_2862()
        {
            List<Building> buildings = CityGML_Buildings("2862_N-34-77-D-b-1-1.gml");

            Assert.Equal(3, buildings.Count);

            foreach (Building building in buildings)
            {
                Polyhedron polyhedron = CityGML_Polyhedron(building);

                Assert.True(polyhedron.IsClosed(tolerance_Weld), building.UniqueId);
                Assert.True(polyhedron.IsClosed(true, tolerance_Weld), building.UniqueId);

                // Known limitation, not a property worth preserving - see the summary.
                Assert.False(polyhedron.IsClosed(), building.UniqueId);
            }
        }

        /// <summary>
        /// Tests the closure of the larger LOD2 fixture, whose six buildings do not all behave alike.
        /// <para>The four simpler buildings close at ten millimetres, like the buildings of the 2862 fixture. The two detailed ones - 35 and 112 faces - carry rings which are out of plane by up to 17 mm, so the vertex displacement introduced by fitting a plane exceeds the welding distance and they only close at fifty millimetres.</para>
        /// <para>The two are asserted separately rather than excluded, so that a change to the conversion which removes the displacement is visible here as a failure rather than passing unnoticed.</para>
        /// </summary>
        [Fact]
        public void Polyhedron_FromCityGML_2476()
        {
            List<Building> buildings = CityGML_Buildings("2476_CityGML.zip");

            Assert.Equal(6, buildings.Count);

            // The face count identifies each building of this fixture uniquely and survives a change of identifier.
            int[] counts_Face_Coarse = [35, 112];

            int count_Closed = 0;

            foreach (Building building in buildings)
            {
                Polyhedron polyhedron = CityGML_Polyhedron(building);

                if (counts_Face_Coarse.Contains(polyhedron.Count))
                {
                    Assert.False(polyhedron.IsClosed(tolerance_Weld), building.UniqueId);

                    Assert.True(polyhedron.IsClosed(tolerance_Weld_Coarse), building.UniqueId);
                    Assert.True(polyhedron.IsClosed(true, tolerance_Weld_Coarse), building.UniqueId);

                    continue;
                }

                Assert.True(polyhedron.IsClosed(tolerance_Weld), building.UniqueId);
                Assert.True(polyhedron.IsClosed(true, tolerance_Weld), building.UniqueId);

                count_Closed++;
            }

            Assert.Equal(4, count_Closed);
        }

        /// <summary>
        /// Builds the polyhedron of a building and asserts that it carries one face per surface.
        /// <para>A face lost between the surfaces of the building and the polyhedron would otherwise be invisible, and <see cref="Geometry.Spatial.Create.Polyhedron(System.Collections.Generic.IEnumerable{Geometry.Spatial.Interfaces.IPolygonalFace3D}?)"/> returns null rather than throwing when fewer than four faces are supplied.</para>
        /// </summary>
        /// <param name="building">The building to convert.</param>
        /// <returns>The polyhedron of the building.</returns>
        private static Polyhedron CityGML_Polyhedron(Building building)
        {
            List<ISurface>? surfaces = building.Surfaces?.ToList();

            Assert.NotNull(surfaces);
            Assert.NotEmpty(surfaces);

            Polyhedron? result = Query.Polyhedron(building);

            Assert.True(result is not null, building.UniqueId);
            Assert.Equal(surfaces.Count, result!.Count);

            return result;
        }
    }
}
