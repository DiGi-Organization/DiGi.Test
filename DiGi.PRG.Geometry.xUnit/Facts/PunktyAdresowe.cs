using DiGi.BDOT10k.Classes;
using DiGi.GML.Classes;
using DiGi.GML.Interfaces;
using DiGi.PRG.Classes;
using DiGi.PRG.Geometry.Classes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Create.PunktyAdresowe(string?)"/> returns every address point of the fixture, in document order, each with a geometry and its locality.
        /// <para>The fixture <c>0262_PRG_AddressPoints.xml</c> holds features copied verbatim from <c>02_dolnoslaskie_21.10.2026_12.51.04.gml</c> of the PRG national package of 21.10.2026: 2 <c>AD_Miejscowosc</c> (Babin, Legnica), 2 <c>AD_UlicaPlac</c> (pl. Słowiański, Czysta) and 7 <c>AD_PunktAdresowy</c> (Babin 6A, the 4 points of pl. Słowiański and the 2 of Czysta). Every <c>xlink:href</c> in it names a feature of the same file.</para>
        /// </summary>
        [Fact]
        public void PunktyAdresowe_Load()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_PRG_AddressPoints.xml");

            List<PunktAdresowy>? punktyAdresowe = Create.PunktyAdresowe(path);
            Assert.NotNull(punktyAdresowe);
            Assert.Equal(7, punktyAdresowe.Count);

            Assert.Equal(["6A", "7", "1", "5", "8", "2/4", "1"], punktyAdresowe.ConvertAll(x => x.numerPorzadkowy));
            Assert.All(punktyAdresowe, x => Assert.NotNull(x.Geometry));
            Assert.All(punktyAdresowe, x => Assert.NotNull(x.AD_Miejscowosc));

            Assert.Equal(6, punktyAdresowe.Count(x => x.AD_UlicaPlac != null));
            Assert.Equal(4, punktyAdresowe.Count(x => x.identyfikatorULIC == "20295"));
            Assert.Equal(2, punktyAdresowe.Count(x => x.identyfikatorULIC == "03513"));
        }

        /// <summary>
        /// Tests that a feature member of another class is skipped without throwing, here the real BDOT10k carriageway <c>OT_SKJZ_L</c> <c>fd53462e-…</c> placed between the streets and the points of the PRG fixture.
        /// <para>This project references DiGi.BDOT10k, so the member does resolve to <see cref="OT_SKJZ_L"/>: the fact checks that first, so it covers the case of a known foreign class rather than only an element no assembly models.</para>
        /// </summary>
        [Fact]
        public void PunktyAdresowe_UnsupportedClass_Skipped()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_PRG_AddressPoints.xml");

            FeatureCollection? featureCollection = GML.Convert.ToGML<FeatureCollection>(path)?.FirstOrDefault();
            Assert.NotNull(featureCollection);

            List<IFeatureMember> featureMembers = featureCollection.featureMember;
            Assert.Equal(12, featureMembers.Count);
            Assert.Single(featureMembers.OfType<OT_SKJZ_L>());
            Assert.Equal(2, featureMembers.OfType<AD_Miejscowosc>().Count());
            Assert.Equal(2, featureMembers.OfType<AD_UlicaPlac>().Count());
            Assert.Equal(7, featureMembers.OfType<AD_PunktAdresowy>().Count());

            List<PunktAdresowy>? punktyAdresowe = Create.PunktyAdresowe(featureCollection);
            Assert.NotNull(punktyAdresowe);
            Assert.Equal(7, punktyAdresowe.Count);
            Assert.DoesNotContain(punktyAdresowe, x => x.lokalnyId == "fd53462e-fa53-40f2-bb71-bad80c33af1e");
        }

        /// <summary>
        /// Tests the <see cref="Stream"/> overload against the path overload, and the <see langword="null"/> returns for a missing input.
        /// </summary>
        [Fact]
        public void PunktyAdresowe_StreamAndNull()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_PRG_AddressPoints.xml");
            Assert.NotNull(path);

            using (FileStream fileStream = File.OpenRead(path))
            {
                List<PunktAdresowy>? punktyAdresowe = Create.PunktyAdresowe(fileStream);
                Assert.NotNull(punktyAdresowe);
                Assert.Equal(Create.PunktyAdresowe(path)?.ConvertAll(x => x.lokalnyId), punktyAdresowe.ConvertAll(x => x.lokalnyId));
            }

            Assert.Null(Create.PunktyAdresowe((string?)null));
            Assert.Null(Create.PunktyAdresowe(Path.Combine(Path.GetDirectoryName(path)!, "0262_PRG_AddressPoints_Missing.xml")));
            Assert.Null(Create.PunktyAdresowe((Stream?)null));
            Assert.Null(Create.PunktyAdresowe((FeatureCollection?)null));
        }

        /// <summary>
        /// Tests that a reference naming no feature of the collection, or a non-local reference, resolves to <see langword="null"/> while the point is still returned.
        /// <para>The fixture is closed under <c>xlink:href</c>, so the dangling references are made by removing the localities and by rewriting the street reference of one point to a remote URL.</para>
        /// </summary>
        [Fact]
        public void PunktyAdresowe_UnresolvedReference()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "0262_PRG_AddressPoints.xml");

            FeatureCollection? featureCollection = GML.Convert.ToGML<FeatureCollection>(path)?.FirstOrDefault();
            Assert.NotNull(featureCollection);

            featureCollection.featureMember.RemoveAll(x => x is AD_Miejscowosc);
            AD_PunktAdresowy aD_PunktAdresowy = featureCollection.featureMember.OfType<AD_PunktAdresowy>().First(x => x.numerPorzadkowy == "7");
            string? ulica2 = aD_PunktAdresowy.ulica2;
            Assert.NotNull(ulica2);
            aD_PunktAdresowy.ulica2 = "https://example.org/prg.gml" + ulica2;

            List<PunktAdresowy>? punktyAdresowe = Create.PunktyAdresowe(featureCollection);
            Assert.NotNull(punktyAdresowe);
            Assert.Equal(7, punktyAdresowe.Count);
            Assert.All(punktyAdresowe, x => Assert.Null(x.AD_Miejscowosc));

            PunktAdresowy punktAdresowy = punktyAdresowe.First(x => x.lokalnyId == "fad42bf4-a0e8-4fe6-85d7-a3b043b185ca");
            Assert.Null(punktAdresowy.AD_UlicaPlac);
            Assert.NotNull(punktAdresowy.Geometry);
            Assert.Equal(5, punktyAdresowe.Count(x => x.AD_UlicaPlac != null));
        }
    }
}
