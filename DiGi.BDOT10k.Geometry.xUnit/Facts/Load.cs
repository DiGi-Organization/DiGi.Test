using DiGi.BDOT10k.Geometry.Classes;
using DiGi.BDOT10k.Geometry.Interfaces;
using System.Collections.Generic;
using System.Reflection;

namespace DiGi.BDOT10k.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Modify.Load(SlownikObiektowGeometrycznych?, string?)"/> fills the dictionary with roads, paths and a building from one file, and that every road has a polyline.
        /// <para>The fixture <c>0262_BDOT10k_Roads.xml</c> holds features copied verbatim from county 0262 of the Geoportal 2025.07 snapshot: 3 <c>OT_SKJZ_L</c>, 3 <c>OT_SKDR_L</c>, 2 <c>OT_SKRP_L</c>, 1 <c>OT_BUBD_A</c> and 1 <c>OT_SKTR_L</c>. Querying the common <see cref="IObiektGeometryczny"/> also covers the early return that made <see cref="SlownikObiektowGeometrycznych.GetObiektyGeometryczne{T}"/> yield only the first matching type.</para>
        /// </summary>
        [Fact]
        public void Load_RoadsAndBuildings()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_BDOT10k_Roads.xml");

            SlownikObiektowGeometrycznych slownikObiektowGeometrycznych = new();
            Assert.True(slownikObiektowGeometrycznych.Load(path));

            List<SKJZ_L>? sKJZ_Ls = slownikObiektowGeometrycznych.GetObiektyGeometryczne<SKJZ_L>();
            List<SKDR_L>? sKDR_Ls = slownikObiektowGeometrycznych.GetObiektyGeometryczne<SKDR_L>();
            List<SKRP_L>? sKRP_Ls = slownikObiektowGeometrycznych.GetObiektyGeometryczne<SKRP_L>();
            List<BUBD_A>? bUBD_As = slownikObiektowGeometrycznych.GetObiektyGeometryczne<BUBD_A>();

            Assert.Equal(3, sKJZ_Ls?.Count);
            Assert.Equal(3, sKDR_Ls?.Count);
            Assert.Equal(2, sKRP_Ls?.Count);
            Assert.Equal(1, bUBD_As?.Count);

            Assert.Equal(9, slownikObiektowGeometrycznych.GetObiektyGeometryczne<IObiektGeometryczny>()?.Count);

            Assert.All(sKJZ_Ls!, x => Assert.NotNull(x.Geometry));
            Assert.All(sKDR_Ls!, x => Assert.NotNull(x.Geometry));
            Assert.All(sKRP_Ls!, x => Assert.NotNull(x.Geometry));

            SKJZ_L? sKJZ_L = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKJZ_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "fd53462e-fa53-40f2-bb71-bad80c33af1e");
            Assert.NotNull(sKJZ_L?.Geometry);

            List<DiGi.Geometry.Planar.Classes.Point2D>? point2Ds = sKJZ_L.Geometry.GetPoints();
            Assert.NotNull(point2Ds);
            Assert.Equal(3, point2Ds.Count);
            Assert.Equal(new DiGi.Geometry.Planar.Classes.Point2D(301843.94, 374859.87), point2Ds[0]);
            Assert.Equal(new DiGi.Geometry.Planar.Classes.Point2D(301852.96, 374837.8), point2Ds[2]);
            Assert.True(sKJZ_L.Length > 0);
        }

        /// <summary>
        /// Tests that a class with no DiGi model in the same file, here a real <c>OT_SKTR_L</c> railway track, is skipped without throwing while every supported feature still loads.
        /// </summary>
        [Fact]
        public void Load_UnsupportedClass_Skipped()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_BDOT10k_Roads.xml");

            SlownikObiektowGeometrycznych slownikObiektowGeometrycznych = new();
            Assert.True(slownikObiektowGeometrycznych.Load(path));

            List<IObiektGeometryczny>? obiektyGeometryczne = slownikObiektowGeometrycznych.GetObiektyGeometryczne<IObiektGeometryczny>();
            Assert.NotNull(obiektyGeometryczne);
            Assert.Equal(9, obiektyGeometryczne.Count);
            Assert.DoesNotContain(obiektyGeometryczne, x => x.GetUnderlyingType()?.Name == "OT_SKTR_L");
        }
    }
}
