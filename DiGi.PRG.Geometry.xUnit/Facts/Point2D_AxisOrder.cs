using DiGi.BDOT10k.Geometry.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.PRG.Geometry.Classes;
using System.Collections.Generic;
using System.Reflection;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that PRG address points land in the coordinate frame of DiGi.BDOT10k: the four points of pl. Słowiański in Legnica (ULIC 20295) must lie near the BDOT10k carriageway of the same square, <c>OT_SKJZ_L</c> <c>fd53462e-…</c> of <c>0262_BDOT10k_Roads.xml</c>, loaded through <c>DiGi.BDOT10k.Geometry</c>.
        /// <para>Measured on 2026-10-21: 31 to 83 m with the axes swapped, about 103 km read as written. The fact asserts both sides, so it fails if the swap in <see cref="Convert.ToDiGi(GML.Classes.Point?)"/> is removed and the points no longer match the footprints DiGi.GIS matches them against.</para>
        /// </summary>
        [Fact]
        public void Point2D_AxisOrder()
        {
            string? path_BDOT10k = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_BDOT10k_Roads.xml");
            SlownikObiektowGeometrycznych slownikObiektowGeometrycznych = new();
            Assert.True(BDOT10k.Geometry.Modify.Load(slownikObiektowGeometrycznych, path_BDOT10k));

            SKJZ_L? sKJZ_L = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKJZ_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "fd53462e-fa53-40f2-bb71-bad80c33af1e");
            Polyline2D? polyline2D = sKJZ_L?.Geometry;
            Assert.NotNull(polyline2D);

            string? path_PRG = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_PRG_AddressPoints.xml");
            List<PunktAdresowy>? punktyAdresowe = Create.PunktyAdresowe(path_PRG)?.FindAll(x => x.identyfikatorULIC == sKJZ_L!.identyfikatorULIC);
            Assert.NotNull(punktyAdresowe);
            Assert.Equal(4, punktyAdresowe.Count);

            foreach (PunktAdresowy punktAdresowy in punktyAdresowe)
            {
                Point2D? point2D = punktAdresowy.Geometry;
                Assert.NotNull(point2D);

                Assert.True(polyline2D.Distance(point2D) < 100, $"{punktAdresowy.numerPorzadkowy}: {polyline2D.Distance(point2D)} m");

                Point2D point2D_AsWritten = new(point2D.Y, point2D.X);
                Assert.True(polyline2D.Distance(point2D_AsWritten) > 50000, $"{punktAdresowy.numerPorzadkowy} as written: {polyline2D.Distance(point2D_AsWritten)} m");
            }
        }
    }
}
