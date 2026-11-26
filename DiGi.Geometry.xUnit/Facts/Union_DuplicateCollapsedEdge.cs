using DiGi.Geometry.Planar;
using DiGi.Geometry.Planar.Classes;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.Union(IEnumerable{Planar.Interfaces.IPolygonalFace2D}?, double)"/> unions valid faces whose shared vertices differ by a few ulps, on which the classic <c>UnaryUnionOp</c> throws an <c>ArgumentException</c> ("An item with the same key has already been added", <c>EdgeList.Add</c> in <c>OverlayOp.ReplaceCollapsedEdges</c>) instead of a <c>TopologyException</c> (DiGi-Organization/DiGi.Geometry#12).
        /// <para>The 11 triangles are a synthetic reproduction of the production solar solve failures of 2026-11-26 (county part 55417, four buildings lost through <c>DiGi.Solar.Query.ShadedFaces</c>): four corner points, each copy moved by up to three ulps per coordinate. The fact first proves the fixture still triggers the exception, then that the union is one valid face with the area <c>OverlayNGRobust</c> computes (22.466 m²).</para>
        /// </summary>
        [Fact]
        public void Union_DuplicateCollapsedEdge()
        {
            string[] wkts =
            [
                "POLYGON ((0.978219127738019 3.9025843813561765, 5.249136418685847 8.416877723493089, 7.814287761139818 6.3625514257524864, 0.978219127738019 3.9025843813561765))",
                "POLYGON ((0.6214857197466707 9.280424033887885, 0.9782191277380191 3.902584381356175, 7.814287761139819 6.362551425752486, 0.6214857197466707 9.280424033887885))",
                "POLYGON ((0.9782191277380191 3.902584381356175, 5.249136418685846 8.416877723493094, 0.6214857197466702 9.280424033887885, 0.9782191277380191 3.902584381356175))",
                "POLYGON ((5.249136418685847 8.416877723493092, 0.6214857197466707 9.280424033887881, 0.978219127738019 3.9025843813561756, 5.249136418685847 8.416877723493092))",
                "POLYGON ((5.249136418685847 8.416877723493089, 0.6214857197466707 9.280424033887881, 7.814287761139819 6.362551425752487, 5.249136418685847 8.416877723493089))",
                "POLYGON ((0.978219127738019 3.9025843813561765, 7.814287761139819 6.362551425752487, 5.249136418685845 8.416877723493089, 0.978219127738019 3.9025843813561765))",
                "POLYGON ((0.9782191277380191 3.902584381356176, 5.249136418685845 8.416877723493092, 0.6214857197466707 9.280424033887881, 0.9782191277380191 3.902584381356176))",
                "POLYGON ((0.6214857197466703 9.280424033887886, 0.9782191277380191 3.902584381356176, 7.814287761139817 6.362551425752487, 0.6214857197466703 9.280424033887886))",
                "POLYGON ((0.9782191277380189 3.902584381356176, 7.814287761139819 6.362551425752487, 5.249136418685846 8.416877723493087, 0.9782191277380189 3.902584381356176))",
                "POLYGON ((5.249136418685844 8.41687772349309, 7.814287761139818 6.3625514257524864, 0.6214857197466707 9.280424033887881, 5.249136418685844 8.41687772349309))",
                "POLYGON ((0.9782191277380191 3.902584381356176, 5.249136418685847 8.416877723493092, 0.6214857197466704 9.280424033887885, 0.9782191277380191 3.902584381356176))",
            ];

            WKTReader wKTReader = new();
            List<Polygon> polygons = [];
            List<PolygonalFace2D> polygonalFace2Ds = [];
            foreach (string wkt in wkts)
            {
                Polygon polygon = Assert.IsType<Polygon>(wKTReader.Read(wkt));
                Assert.True(polygon.IsValid);
                polygons.Add(polygon);

                PolygonalFace2D? polygonalFace2D = polygon.ToDiGi();
                Assert.NotNull(polygonalFace2D);
                polygonalFace2Ds.Add(polygonalFace2D);
            }

            Assert.Throws<ArgumentException>(() => NetTopologySuite.Operation.Union.UnaryUnionOp.Union(polygons));

            double area_Expected = NetTopologySuite.Operation.OverlayNG.OverlayNGRobust.Union(polygons).Area;

            List<PolygonalFace2D>? polygonalFace2Ds_Union = polygonalFace2Ds.Union();
            Assert.NotNull(polygonalFace2Ds_Union);
            PolygonalFace2D polygonalFace2D_Union = Assert.Single(polygonalFace2Ds_Union);

            Polygon? polygon_Union = polygonalFace2D_Union.ToNTS();
            Assert.NotNull(polygon_Union);
            Assert.True(polygon_Union.IsValid);
            // 1e-4 m² admits the snap-rounding fallback, whose 1e-6 grid moves the area by about 3e-6 m² here.
            Assert.Equal(area_Expected, polygonalFace2D_Union.GetArea(), 1e-4);
        }
    }
}
