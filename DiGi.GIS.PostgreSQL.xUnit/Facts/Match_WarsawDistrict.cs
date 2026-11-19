using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a Warsaw subdivision takes the unit of the district it lies in, found by its own name or the names of the subdivisions containing it, and that no other municipality's subdivision can reach a Warsaw district (DiGi-Organization/DiGi.GIS.PostgreSQL#148).
        /// <para>Buildings are filed under the smallest subdivision - Marcelin, inside Sielce, Dolny Mokotów and Mokotów - so the district comes from the containers. A neighbourhood whose containers name no district, or whose containers are not known, takes the city; Gdańsk's Śródmieście takes Gdańsk, not Warsaw's Śródmieście; by name alone a subdivision matches nothing.</para>
        /// </summary>
        [Fact]
        public void Match_WarsawDistrict()
        {
            List<BDL.Classes.Unit> units =
            [
                new BDL.Classes.Unit { id = "000000000000", name = "POLSKA", level = 0 },
                new BDL.Classes.Unit { id = "070000000000", name = "MAKROREGION WOJEWÓDZTWO MAZOWIECKIE", level = 1 },
                new BDL.Classes.Unit { id = "071400000000", name = "MAZOWIECKIE", level = 2 },
                new BDL.Classes.Unit { id = "071410000000", name = "REGION WARSZAWSKI STOŁECZNY", level = 3 },
                new BDL.Classes.Unit { id = "071412800000", name = "PODREGION MIASTO WARSZAWA", level = 4 },
                new BDL.Classes.Unit { id = "071412865000", name = "POWIAT M. ST. WARSZAWA", level = 5 },
                new BDL.Classes.Unit { id = "071412865011", name = "M.st.Warszawa od 2002", level = 6 },
                new BDL.Classes.Unit { id = "071412865048", name = "Mokotów - dzielnica", level = 6 },
                new BDL.Classes.Unit { id = "071412865088", name = "Śródmieście - dzielnica", level = 6 },
                new BDL.Classes.Unit { id = "040000000000", name = "MAKROREGION PÓŁNOCNY", level = 1 },
                new BDL.Classes.Unit { id = "042200000000", name = "POMORSKIE", level = 2 },
                new BDL.Classes.Unit { id = "042210000000", name = "REGION POMORSKIE", level = 3 },
                new BDL.Classes.Unit { id = "042210600000", name = "PODREGION TRÓJMIEJSKI", level = 4 },
                new BDL.Classes.Unit { id = "042210661000", name = "POWIAT M. GDAŃSK", level = 5 },
                new BDL.Classes.Unit { id = "042210661011", name = "GDAŃSK", level = 6 }
            ];

            GIS.Classes.StatisticalUnit? rootStatisticalUnit = GIS.Create.StatisticalUnit(units);
            Assert.NotNull(rootStatisticalUnit);

            AdministrativeAreal2DReferencePath administrativeAreal2DReferencePath_Warsaw = new([new AdministrativeAreal2DReference { Id = 1, Code = "1465011", Name = "M. ST. WARSZAWA", AdministrativeArealType = AdministrativeArealType.Municipality }]);
            AdministrativeAreal2DReferencePath administrativeAreal2DReferencePath_Gdansk = new([new AdministrativeAreal2DReference { Id = 2, Code = "2261011", Name = "GDAŃSK", AdministrativeArealType = AdministrativeArealType.Municipality }]);

            GIS.Classes.StatisticalUnit? statisticalUnit_Warsaw = Query.Match(rootStatisticalUnit, administrativeAreal2DReferencePath_Warsaw[AdministrativeArealType.Municipality]);
            GIS.Classes.StatisticalUnit? statisticalUnit_Gdansk = Query.Match(rootStatisticalUnit, administrativeAreal2DReferencePath_Gdansk[AdministrativeArealType.Municipality]);
            Assert.Equal("071412865011", statisticalUnit_Warsaw?.Code);
            Assert.Equal("042210661011", statisticalUnit_Gdansk?.Code);

            List<GIS.Classes.StatisticalUnit> statisticalUnits_District = Query.DistrictStatisticalUnits(rootStatisticalUnit, statisticalUnit_Warsaw);
            Assert.Equal(["071412865048", "071412865088"], statisticalUnits_District.ConvertAll(x => x.Code));
            Assert.Empty(Query.DistrictStatisticalUnits(rootStatisticalUnit, statisticalUnit_Gdansk));
            Assert.Empty(Query.DistrictStatisticalUnits(rootStatisticalUnit, null));
            Assert.Empty(Query.DistrictStatisticalUnits(null, statisticalUnit_Warsaw));

            AdministrativeAreal2DReference marcelin = new() { Id = 10, Name = "Marcelin", AdministrativeArealType = AdministrativeArealType.Subdivision };
            AdministrativeAreal2DReference mokotow = new() { Id = 11, Name = "Mokotów", AdministrativeArealType = AdministrativeArealType.Subdivision };
            AdministrativeAreal2DReference srodmiescie_Warsaw = new() { Id = 12, Name = "Dzielnica Śródmieście", AdministrativeArealType = AdministrativeArealType.Subdivision };
            AdministrativeAreal2DReference srodmiescie_Gdansk = new() { Id = 13, Name = "Śródmieście", AdministrativeArealType = AdministrativeArealType.Subdivision };

            // The district comes from the containers, smallest first.
            Assert.Equal("071412865048", Query.Match(rootStatisticalUnit, marcelin, administrativeAreal2DReferencePath_Warsaw, ["Sielce", "Dolny Mokotów", "Mokotów"])?.Code);

            // A district subdivision is its own district, with or without containers; a leading "Dzielnica" is ignored.
            Assert.Equal("071412865048", Query.Match(rootStatisticalUnit, mokotow, administrativeAreal2DReferencePath_Warsaw)?.Code);
            Assert.Equal("071412865088", Query.Match(rootStatisticalUnit, srodmiescie_Warsaw, administrativeAreal2DReferencePath_Warsaw, [])?.Code);

            // No district among the names, or the containers not known: the city.
            Assert.Equal("071412865011", Query.Match(rootStatisticalUnit, marcelin, administrativeAreal2DReferencePath_Warsaw, ["Sielce", null])?.Code);
            Assert.Equal("071412865011", Query.Match(rootStatisticalUnit, marcelin, administrativeAreal2DReferencePath_Warsaw)?.Code);

            // Another municipality's subdivision never reaches a Warsaw district, whatever it is called.
            Assert.Equal("042210661011", Query.Match(rootStatisticalUnit, srodmiescie_Gdansk, administrativeAreal2DReferencePath_Gdansk, ["Mokotów"])?.Code);

            // By its name alone, or without a municipality in its path, a subdivision matches nothing.
            Assert.Null(Query.Match(rootStatisticalUnit, mokotow));
            Assert.Null(Query.Match(rootStatisticalUnit, "MOKOTÓW", null, AdministrativeArealType.Subdivision));
            Assert.Null(Query.Match(rootStatisticalUnit, mokotow, null, ["Mokotów"]));
        }
    }
}
