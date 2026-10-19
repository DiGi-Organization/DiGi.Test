using DiGi.BDOT10k.Classes;
using DiGi.BDOT10k.Geometry.Classes;
using DiGi.BDOT10k.Geometry.Interfaces;
using DiGi.BDOT10k.Geometry.xUnit.Classes;
using DiGi.GML.Classes;

namespace DiGi.BDOT10k.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that an administrative unit converts to <see cref="ADJA_A"/>.
        /// <para><see cref="OT_ADJA_A"/> implements the surface interface but had no converter branch, so loading an <c>OT_ADJA_A.xml</c> file threw <see cref="System.NotImplementedException"/> on its first feature.</para>
        /// </summary>
        [Fact]
        public void ObiektGeometryczny_ADJA_A()
        {
            OT_ADJA_A oT_ADJA_A = new()
            {
                nazwa = "Test",
                geometria = new Polygon()
                {
                    exterior = new LinearRing() { posList = [0, 0, 10, 0, 10, 10, 0, 10, 0, 0] }
                }
            };

            IObiektGeometryczny? obiektGeometryczny = Convert.ToDiGi(oT_ADJA_A);

            ADJA_A aDJA_A = Assert.IsType<ADJA_A>(obiektGeometryczny);
            Assert.NotNull(aDJA_A.Geometry);
            Assert.Equal(100, aDJA_A.Area, 9);
            Assert.Equal("Test", aDJA_A.OT_PowierzchniowyObiektGeometryczny?.nazwa);
        }

        /// <summary>
        /// Tests that a BDOT10k class without a converter branch returns null instead of throwing.
        /// <para><see cref="Modify.Load(SlownikObiektowGeometrycznych, FeatureCollection)"/> converts every feature member, so a throw here would abort the load of a whole file.</para>
        /// </summary>
        [Fact]
        public void ObiektGeometryczny_UnsupportedClass_ReturnsNull()
        {
            UnsupportedObiektGeometryczny unsupportedObiektGeometryczny = new();

            Assert.Null(Convert.ToDiGi(unsupportedObiektGeometryczny));
        }
    }
}
