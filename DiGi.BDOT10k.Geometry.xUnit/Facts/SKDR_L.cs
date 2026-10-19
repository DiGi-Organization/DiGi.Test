using DiGi.BDOT10k.Enums;
using DiGi.BDOT10k.Geometry.Classes;
using System.Reflection;

namespace DiGi.BDOT10k.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that road attributes parse to the values written in the fixture, compared by hand with <c>0262_BDOT10k_Roads.xml</c>.
        /// <para>Covers <c>kategoriaIstnienia</c> "w budowie", a road without <c>numerDrogi</c>, and a proper road name (<c>nazwaDrogi</c>).</para>
        /// </summary>
        [Fact]
        public void SKDR_L_Attributes()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_BDOT10k_Roads.xml");

            SlownikObiektowGeometrycznych slownikObiektowGeometrycznych = new();
            Assert.True(slownikObiektowGeometrycznych.Load(path));

            SKDR_L? sKDR_L_WBudowie = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKDR_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "e1e83d27-6771-4c1f-866c-b4211e8e3680");
            Assert.NotNull(sKDR_L_WBudowie);
            Assert.Equal(OT_KatIstnienia.w_budowie, sKDR_L_WBudowie.OT_LiniowyObiektGeometryczny?.kategoriaIstnienia);
            Assert.Equal(OT_KlasaDrogi.droga_wewnetrzna, sKDR_L_WBudowie.klasaDrogi);
            Assert.Equal(OT_KatZarzadzaniaDrogi.wewnetrzna, sKDR_L_WBudowie.kategoriaZarzadzania);
            Assert.Null(sKDR_L_WBudowie.numerDrogi);

            SKDR_L? sKDR_L_Nazwa = slownikObiektowGeometrycznych.GetObiektGeometryczny<SKDR_L>(x => x.OT_LiniowyObiektGeometryczny?.lokalnyId == "28C08C6A-C1B0-CDC0-E053-CA2BA8C07CE4");
            Assert.NotNull(sKDR_L_Nazwa);
            Assert.Equal("Piekary Wielkie-Grzybiany", sKDR_L_Nazwa.nazwaDrogi);
            Assert.Equal(OT_KatZarzadzaniaDrogi.powiatowa, sKDR_L_Nazwa.kategoriaZarzadzania);
            Assert.Equal(["2178D"], sKDR_L_Nazwa.numerDrogi);
        }
    }
}
