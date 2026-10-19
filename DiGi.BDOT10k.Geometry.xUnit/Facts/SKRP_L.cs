using DiGi.BDOT10k.Classes;
using DiGi.BDOT10k.Enums;
using DiGi.BDOT10k.Geometry.Classes;
using System.Reflection;

namespace DiGi.BDOT10k.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that pedestrian and cycle route attributes parse to the values written in the fixture, compared by hand with <c>0262_BDOT10k_Roads.xml</c>.
        /// <para>The <c>rodzaj</c> values follow Annex 3, Table 8 ("ścieżka", "droga dla rowerów"), not the UML diagram. The data confirms that.</para>
        /// </summary>
        [Fact]
        public void SKRP_L_Attributes()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_BDOT10k_Roads.xml");

            SlownikObiektowGeometrycznych slownikObiektowGeometrycznych = new();
            Assert.True(slownikObiektowGeometrycznych.Load(path));

            SKRP_L? sKRP_L_Sciezka = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKRP_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "28C08C6A-DFCC-CDC0-E053-CA2BA8C07CE4");
            Assert.NotNull(sKRP_L_Sciezka);
            Assert.Equal(OT_RodzajCiaguRuchuPieszego.sciezka, sKRP_L_Sciezka.rodzaj);
            Assert.Equal("Leśna", sKRP_L_Sciezka.ulicaNazwa1);
            Assert.Equal("10898", sKRP_L_Sciezka.identyfikatorULIC);
            Assert.NotNull(sKRP_L_Sciezka.Geometry);

            OT_SKRP_L? oT_SKRP_L_Sciezka = sKRP_L_Sciezka.OT_LiniowyObiektGeometryczny;
            Assert.NotNull(oT_SKRP_L_Sciezka);
            Assert.Equal(OT_MaterialNawierzchni.grunt_naturalny, oT_SKRP_L_Sciezka.materialNawierzchni);
            Assert.Equal("107820D", oT_SKRP_L_Sciezka.informacjaDodatkowa);
            Assert.Null(oT_SKRP_L_Sciezka.szerokosc);

            SKRP_L? sKRP_L_Rowerowa = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKRP_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "34F0C939-015E-4C0D-A401-7DD190B08B95");
            Assert.NotNull(sKRP_L_Rowerowa);
            Assert.Equal(OT_RodzajCiaguRuchuPieszego.droga_dla_rowerow, sKRP_L_Rowerowa.rodzaj);
            Assert.Equal("Szczytnicka", sKRP_L_Rowerowa.ulicaNazwa1);
            Assert.Equal(OT_MaterialNawierzchni.kostka_prefabrykowana, sKRP_L_Rowerowa.OT_LiniowyObiektGeometryczny?.materialNawierzchni);
        }
    }
}
