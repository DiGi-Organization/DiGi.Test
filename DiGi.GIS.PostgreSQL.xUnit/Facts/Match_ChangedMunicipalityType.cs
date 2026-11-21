using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a municipality is matched by its TERYT code before its name (DiGi-Organization/DiGi.GIS.PostgreSQL#149).
        /// <para>Czerwińsk nad Wisłą and Sochocin became urban-rural gminas after the 2021 census. BDL lists each under two units of one name - the historical rural gmina (type 2, …042 and …112), which holds no data from then on, and the current urban-rural one (type 3, …043 and …113), which holds the census and the population - the historical one first. PRG names them by the current codes 1420043 and 1420113, which must win over a name that the earlier unit also carries. Without a code, or with a code no unit carries, the name still matches (the first unit of that name), and a plain municipality is unaffected.</para>
        /// </summary>
        [Fact]
        public void Match_ChangedMunicipalityType()
        {
            List<BDL.Classes.Unit> units =
            [
                new BDL.Classes.Unit { id = "000000000000", name = "POLSKA", level = 0 },
                new BDL.Classes.Unit { id = "070000000000", name = "MAKROREGION WOJEWÓDZTWO MAZOWIECKIE", level = 1 },
                new BDL.Classes.Unit { id = "071400000000", name = "MAZOWIECKIE", level = 2 },
                new BDL.Classes.Unit { id = "071420000000", name = "REGION MAZOWIECKI REGIONALNY", level = 3 },
                new BDL.Classes.Unit { id = "071422500000", name = "PODREGION CIECHANOWSKI", level = 4 },
                new BDL.Classes.Unit { id = "071422520000", name = "POWIAT PŁOŃSKI", level = 5 },
                new BDL.Classes.Unit { id = "071422520032", name = "Baboszewo", level = 6 },
                new BDL.Classes.Unit { id = "071422520042", name = "Czerwińsk nad Wisłą", level = 6 },
                new BDL.Classes.Unit { id = "071422520043", name = "Czerwińsk nad Wisłą", level = 6 },
                new BDL.Classes.Unit { id = "071422520044", name = "Czerwińsk nad Wisłą - miasto", level = 6 },
                new BDL.Classes.Unit { id = "071422520045", name = "Czerwińsk nad Wisłą - obszar wiejski", level = 6 },
                new BDL.Classes.Unit { id = "071422520112", name = "Sochocin", level = 6 },
                new BDL.Classes.Unit { id = "071422520113", name = "Sochocin", level = 6 },
                new BDL.Classes.Unit { id = "071422520114", name = "Sochocin - miasto", level = 6 },
                new BDL.Classes.Unit { id = "071422520115", name = "Sochocin - obszar wiejski", level = 6 }
            ];

            GIS.Classes.StatisticalUnit? rootStatisticalUnit = GIS.Create.StatisticalUnit(units);
            Assert.NotNull(rootStatisticalUnit);

            // The current code wins over the name the earlier, historical unit also carries.
            Assert.Equal("071422520043", Query.Match(rootStatisticalUnit, "Czerwińsk nad Wisłą", "1420043", AdministrativeArealType.Municipality)?.Code);
            Assert.Equal("071422520113", Query.Match(rootStatisticalUnit, "Sochocin", "1420113", AdministrativeArealType.Municipality)?.Code);
            Assert.Equal("071422520113", Query.Match(rootStatisticalUnit, new AdministrativeAreal2DReference { Id = 49660, Code = "1420113", Name = "Sochocin", AdministrativeArealType = AdministrativeArealType.Municipality })?.Code);

            // A code names a unit whatever the name says.
            Assert.Equal("071422520042", Query.Match(rootStatisticalUnit, "Czerwińsk nad Wisłą", "1420042", AdministrativeArealType.Municipality)?.Code);

            // Without a code, or with a code no unit carries, the name still matches: the first unit of that name.
            Assert.Equal("071422520042", Query.Match(rootStatisticalUnit, "Czerwińsk nad Wisłą", null, AdministrativeArealType.Municipality)?.Code);
            Assert.Equal("071422520112", Query.Match(rootStatisticalUnit, "Sochocin", "1499999", AdministrativeArealType.Municipality)?.Code);

            // A municipality with one unit is matched by code or by name alike.
            Assert.Equal("071422520032", Query.Match(rootStatisticalUnit, "Baboszewo", "1420032", AdministrativeArealType.Municipality)?.Code);
            Assert.Equal("071422520032", Query.Match(rootStatisticalUnit, "Baboszewo", null, AdministrativeArealType.Municipality)?.Code);
        }
    }
}
