using DiGi.BDOT10k.Classes;
using DiGi.BDOT10k.Enums;
using DiGi.BDOT10k.Geometry.Classes;
using System.Reflection;

namespace DiGi.BDOT10k.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that carriageway attributes parse to the values written in the fixture, compared by hand with <c>0262_BDOT10k_Roads.xml</c>.
        /// <para>Covers a multi-valued <c>numerDrogi</c> (E65, S3), the code-list value spelled "przyśpieszonego", a TERYT street id with a leading zero, and a <c>cechaGeometrii</c> both present and absent.</para>
        /// </summary>
        [Fact]
        public void SKJZ_L_Attributes()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_BDOT10k_Roads.xml");

            SlownikObiektowGeometrycznych slownikObiektowGeometrycznych = new();
            Assert.True(slownikObiektowGeometrycznych.Load(path));

            SKJZ_L? sKJZ_L_Ekspresowa = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKJZ_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "9fade3bd-220c-43e6-b5a4-115f1bf29b48");
            Assert.NotNull(sKJZ_L_Ekspresowa);
            Assert.Equal(OT_KlasaDrogi.droga_ekspresowa, sKJZ_L_Ekspresowa.klasaDrogi);
            Assert.Equal(OT_KatZarzadzaniaDrogi.krajowa, sKJZ_L_Ekspresowa.kategoriaZarzadzania);
            Assert.Equal(["E65", "S3"], sKJZ_L_Ekspresowa.numerDrogi);
            Assert.Null(sKJZ_L_Ekspresowa.ulicaNazwa1);

            OT_SKJZ_L? oT_SKJZ_L_Ekspresowa = sKJZ_L_Ekspresowa.OT_LiniowyObiektGeometryczny;
            Assert.NotNull(oT_SKJZ_L_Ekspresowa);
            Assert.Equal(OT_KatIstnienia.eksploatowany, oT_SKJZ_L_Ekspresowa.kategoriaIstnienia);
            Assert.Equal(OT_MaterialNawierzchni.masa_bitumiczna, oT_SKJZ_L_Ekspresowa.materialNawierzchni);
            Assert.Equal(OT_PolozenieObiektuSieciTransportowej.na_powierzchni_gruntu, oT_SKJZ_L_Ekspresowa.polozenie);
            Assert.Equal(2, oT_SKJZ_L_Ekspresowa.liczbaJezdniDrogi);
            Assert.Equal(12.0, oT_SKJZ_L_Ekspresowa.szerokoscNawierzchni);
            Assert.Null(oT_SKJZ_L_Ekspresowa.cechaGeometrii);

            SKJZ_L? sKJZ_L_Lokalna = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKJZ_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "fd53462e-fa53-40f2-bb71-bad80c33af1e");
            OT_SKJZ_L? oT_SKJZ_L_Lokalna = sKJZ_L_Lokalna?.OT_LiniowyObiektGeometryczny;
            Assert.NotNull(oT_SKJZ_L_Lokalna);
            Assert.Equal(OT_RodzajRepGeom.linia_umowna, oT_SKJZ_L_Lokalna.cechaGeometrii);
            Assert.Equal("20295", oT_SKJZ_L_Lokalna.identyfikatorULIC);
            Assert.Equal("0954047", oT_SKJZ_L_Lokalna.identyfikatorSIMC);
            Assert.Equal("pl.", oT_SKJZ_L_Lokalna.ulicaCecha);
            Assert.Equal("Słowiański", oT_SKJZ_L_Lokalna.ulicaNazwa1);
            Assert.Null(oT_SKJZ_L_Lokalna.szerokoscNawierzchni);

            SKJZ_L? sKJZ_L_Wiadukt = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKJZ_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "28C08C6A-CD70-CDC0-E053-CA2BA8C07CE4");
            Assert.NotNull(sKJZ_L_Wiadukt);
            Assert.Equal(OT_KlasaDrogi.droga_glowna_ruchu_przyspieszonego, sKJZ_L_Wiadukt.klasaDrogi);
            Assert.Equal(OT_KatZarzadzaniaDrogi.wojewodzka, sKJZ_L_Wiadukt.kategoriaZarzadzania);
            Assert.Equal(OT_PolozenieObiektuSieciTransportowej.ponad_powierzchnia_gruntu_poziom_1, sKJZ_L_Wiadukt.OT_LiniowyObiektGeometryczny?.polozenie);
            Assert.Equal("07409", sKJZ_L_Wiadukt.identyfikatorULIC);
            Assert.Equal("Jaworzyńska", sKJZ_L_Wiadukt.ulicaNazwa1);
        }
    }
}
