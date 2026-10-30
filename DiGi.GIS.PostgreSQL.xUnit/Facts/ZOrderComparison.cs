using DiGi.Geometry.Planar.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.ZOrderComparison"/> orders references along the Z-order curve of their outlines (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// <para>Outlines in the four 100 m cells of a 2 x 2 grid come out lower-left, lower-right, upper-left, upper-right; two outlines in one cell come out by reference; a reference with no outline comes last.</para>
        /// </summary>
        [Fact]
        public void Query_ZOrderComparison()
        {
            static GIS.Classes.Building2D Building2D(string reference, double x, double y)
            {
                PolygonalFace2D? polygonalFace2D = DiGi.Geometry.Planar.Create.PolygonalFace2D(new Point2D(x, y), new Point2D(x + 10, y), new Point2D(x + 10, y + 10), new Point2D(x, y + 10));
                Assert.NotNull(polygonalFace2D);

                return new GIS.Classes.Building2D(System.Guid.NewGuid(), reference, polygonalFace2D, 1, null, null, []);
            }

            Dictionary<string, GIS.Classes.Building2D> building2Ds_ByReference = new()
            {
                ["d"] = Building2D("d", 150, 150),
                ["c"] = Building2D("c", 0, 150),
                ["b"] = Building2D("b", 150, 0),
                ["a2"] = Building2D("a2", 20, 20),
                ["a1"] = Building2D("a1", 0, 0),
            };

            List<string> references = ["none", "d", "c", "b", "a2", "a1"];
            references.Sort(Query.ZOrderComparison(building2Ds_ByReference));

            Assert.Equal(["a1", "a2", "b", "c", "d", "none"], references);
        }
    }
}
