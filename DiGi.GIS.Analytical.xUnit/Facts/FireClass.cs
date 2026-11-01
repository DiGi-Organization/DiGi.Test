using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.Enums;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Query.FireClass(BuildingGeneralFunction?)"/>: § 209 ust. 1 of WT 2002 puts residential, office, commercial, education, healthcare, transport and other non-residential buildings in ZL, agricultural production and industrial, tanks/silos/storage in PM, and an unknown function yields no kind (DiGi-Organization/DiGi.GIS.Analytical#9).
        /// </summary>
        [Fact]
        public void Query_FireClass()
        {
            Assert.Equal(FireClass.ZL, Query.FireClass(BuildingGeneralFunction.residential_buildings));
            Assert.Equal(FireClass.ZL, Query.FireClass(BuildingGeneralFunction.office_buildings));
            Assert.Equal(FireClass.ZL, Query.FireClass(BuildingGeneralFunction.commercial_service_buildings));
            Assert.Equal(FireClass.ZL, Query.FireClass(BuildingGeneralFunction.buildings_for_education_science_culture_and_sports));
            Assert.Equal(FireClass.ZL, Query.FireClass(BuildingGeneralFunction.hospital_and_healthcare_buildings));
            Assert.Equal(FireClass.ZL, Query.FireClass(BuildingGeneralFunction.transport_and_communication_buildings));
            Assert.Equal(FireClass.ZL, Query.FireClass(BuildingGeneralFunction.other_non_residential_buildings));
            Assert.Equal(FireClass.PM, Query.FireClass(BuildingGeneralFunction.agricultural_production_service_utility_buildings));
            Assert.Equal(FireClass.PM, Query.FireClass(BuildingGeneralFunction.industrial_buildings));
            Assert.Equal(FireClass.PM, Query.FireClass(BuildingGeneralFunction.tanks_silos_and_storage_buildings));
            Assert.Null(Query.FireClass(null));
        }
    }
}
