using DiGi.Geometry.Planar.Classes;
using DiGi.PRG.Classes;
using DiGi.PRG.Enums;
using DiGi.PRG.Geometry.Classes;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the attributes of three address points against values read by hand from <c>0262_PRG_AddressPoints.xml</c>: a street address (pl. Słowiański 7), a house number with a slash on a street whose ULIC id has a leading zero (Czysta 2/4), and a village address without a street and with a suffix (Babin 6A).
        /// <para>Covers the resolved street and locality, both code lists, a <c>wersjaId</c> whose <c>+02:00</c> offset must survive, a <c>dataNadania</c> present and absent, and the swapped axis order of the geometry.</para>
        /// </summary>
        [Fact]
        public void PunktAdresowy_Attributes()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_PRG_AddressPoints.xml");

            List<PunktAdresowy>? punktyAdresowe = Create.PunktyAdresowe(path);
            Assert.NotNull(punktyAdresowe);

            PunktAdresowy? punktAdresowy_Slowianski = punktyAdresowe.Find(x => x.lokalnyId == "fad42bf4-a0e8-4fe6-85d7-a3b043b185ca");
            Assert.NotNull(punktAdresowy_Slowianski);
            Assert.Equal("7", punktAdresowy_Slowianski.numerPorzadkowy);
            Assert.Equal("59-220", punktAdresowy_Slowianski.kodPocztowy);
            Assert.Equal(new DateTimeOffset(2026, 10, 20, 13, 14, 22, TimeSpan.FromHours(2)), punktAdresowy_Slowianski.wersjaId);
            Assert.Equal(TimeSpan.FromHours(2), punktAdresowy_Slowianski.wersjaId?.Offset);
            Assert.Equal("Słowiański", punktAdresowy_Slowianski.ulicaNazwaPelna);
            Assert.Equal(AD_RodzajObiektu.plac, punktAdresowy_Slowianski.ulicaRodzaj);
            Assert.Equal("20295", punktAdresowy_Slowianski.identyfikatorULIC);
            Assert.Equal("Legnica", punktAdresowy_Slowianski.miejscowoscNazwa);
            Assert.Equal(AD_RodzajMiejscowosci.miasto, punktAdresowy_Slowianski.miejscowoscRodzaj);
            Assert.Equal("0954047", punktAdresowy_Slowianski.identyfikatorSIMC);
            Assert.Equal("0262011", punktAdresowy_Slowianski.TERYTGminy);
            Assert.Equal(new Point2D(301858.38, 374887.73), punktAdresowy_Slowianski.Geometry);

            AD_PunktAdresowy? aD_PunktAdresowy_Slowianski = punktAdresowy_Slowianski.AD_PunktAdresowy;
            Assert.NotNull(aD_PunktAdresowy_Slowianski);
            Assert.Equal(new DateTime(2026, 10, 20, 13, 14, 22), aD_PunktAdresowy_Slowianski.poczatekWersjiObiektu);
            Assert.Null(aD_PunktAdresowy_Slowianski.dataNadania);
            Assert.Equal("PL.PZGIK.200", aD_PunktAdresowy_Slowianski.idIIP?.przestrzenNazw);
            Assert.Equal("#PL.ZIPIN.1050.EMUiA_7310b7a5-cae3-4ba2-bdfe-b6dbdb10d05c_2026-10-20T13_07_28_02_00", aD_PunktAdresowy_Slowianski.ulica2);
            Assert.NotSame(aD_PunktAdresowy_Slowianski, punktAdresowy_Slowianski.AD_PunktAdresowy);

            AD_UlicaPlac? aD_UlicaPlac_Slowianski = punktAdresowy_Slowianski.AD_UlicaPlac;
            Assert.NotNull(aD_UlicaPlac_Slowianski);
            Assert.Equal("Słowiański", aD_UlicaPlac_Slowianski.TERYTNazwa1);
            Assert.Null(aD_UlicaPlac_Slowianski.TERYTNazwa2);
            Assert.Equal(["#PL.ZIPIN.1050.EMUiA_1b220bb5-0618-4ed3-bfa8-7ae554dd3952_2026-10-20T13_07_00_02_00"], aD_UlicaPlac_Slowianski.miejsce);
            Assert.Equal(4, aD_UlicaPlac_Slowianski.adres2?.Count);
            Assert.Equal("#PL.ZIPIN.1050.EMUiA_6930ebf0-5efb-414f-ba69-1968b63bcace_2026-10-20T13_10_29_02_00", aD_UlicaPlac_Slowianski.adres2?[3]);

            PunktAdresowy? punktAdresowy_Czysta = punktyAdresowe.Find(x => x.lokalnyId == "8ba20226-6e94-4a81-a018-9cd4e3a5e0d7");
            Assert.NotNull(punktAdresowy_Czysta);
            Assert.Equal("2/4", punktAdresowy_Czysta.numerPorzadkowy);
            Assert.Equal("Czysta", punktAdresowy_Czysta.ulicaNazwaPelna);
            Assert.Equal(AD_RodzajObiektu.ulica, punktAdresowy_Czysta.ulicaRodzaj);
            Assert.Equal("03513", punktAdresowy_Czysta.identyfikatorULIC);
            Assert.Equal(new DateTime(2025, 9, 26), punktAdresowy_Czysta.AD_PunktAdresowy?.dataNadania);
            Assert.Equal(new Point2D(304439.23, 375437.23), punktAdresowy_Czysta.Geometry);

            PunktAdresowy? punktAdresowy_Babin = punktyAdresowe.Find(x => x.lokalnyId == "5632bb59-2978-44b1-b71f-fc2da86a0f03");
            Assert.NotNull(punktAdresowy_Babin);
            Assert.Equal("6A", punktAdresowy_Babin.numerPorzadkowy);
            Assert.Equal("59-223", punktAdresowy_Babin.kodPocztowy);
            Assert.Null(punktAdresowy_Babin.AD_UlicaPlac);
            Assert.Null(punktAdresowy_Babin.AD_PunktAdresowy?.ulica2);
            Assert.Null(punktAdresowy_Babin.ulicaNazwaPelna);
            Assert.Null(punktAdresowy_Babin.ulicaRodzaj);
            Assert.Null(punktAdresowy_Babin.identyfikatorULIC);
            Assert.Equal("Babin", punktAdresowy_Babin.miejscowoscNazwa);
            Assert.Equal(AD_RodzajMiejscowosci.wies, punktAdresowy_Babin.miejscowoscRodzaj);
            Assert.Equal("0364618", punktAdresowy_Babin.identyfikatorSIMC);
            Assert.Equal("0209032", punktAdresowy_Babin.TERYTGminy);
            Assert.Equal(new DateTime(2023, 9, 20), punktAdresowy_Babin.AD_PunktAdresowy?.dataNadania);
            Assert.Equal(new DateTimeOffset(2025, 7, 24, 21, 38, 46, TimeSpan.FromHours(2)), punktAdresowy_Babin.wersjaId);
            Assert.Equal(new Point2D(299495.12, 368685.01), punktAdresowy_Babin.Geometry);
        }
    }
}
