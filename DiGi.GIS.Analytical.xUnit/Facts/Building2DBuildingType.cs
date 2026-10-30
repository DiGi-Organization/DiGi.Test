using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using Regulation = DiGi.Analytical.Building.Regulation;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests Query.BuildingType: single-family or, with party walls, terraced; multi-family below Query.ApartmentBlockStoreys storeys and apartment block from it; multi-family winning when both functions are given; anything else undefined (DiGi-Organization/DiGi.GIS.Analytical#6).
        /// </summary>
        [Fact]
        public void Building2D_BuildingType()
        {
            Assert.Equal(Regulation.Enums.BuildingType.Undefined, Query.BuildingType(null));

            Building2D building2D_SingleFamily = Building2D_Rectangle(10, 10, 2, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building);
            Assert.Equal(Regulation.Enums.BuildingType.SingleFamily, Query.BuildingType(building2D_SingleFamily));
            Assert.Equal(Regulation.Enums.BuildingType.Terraced, Query.BuildingType(building2D_SingleFamily, true));

            Assert.Equal(Regulation.Enums.BuildingType.MultiFamily, Query.BuildingType(Building2D_Rectangle(20, 12, (ushort)(Query.ApartmentBlockStoreys - 1), BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.multi_family_building)));
            Assert.Equal(Regulation.Enums.BuildingType.ApartmentBlock, Query.BuildingType(Building2D_Rectangle(60, 12, Query.ApartmentBlockStoreys, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.multi_family_building)));
            Assert.Equal(Regulation.Enums.BuildingType.ApartmentBlock, Query.BuildingType(Building2D_Rectangle(60, 12, 11, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.multi_family_building), true));
            Assert.Equal(Regulation.Enums.BuildingType.MultiFamily, Query.BuildingType(Building2D_Rectangle(20, 12, 3, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building, BuildingSpecificFunction.multi_family_building)));

            Assert.Equal(Regulation.Enums.BuildingType.Undefined, Query.BuildingType(Building2D_Rectangle(10, 10, 1, BuildingGeneralFunction.residential_buildings)));
            Assert.Equal(Regulation.Enums.BuildingType.Undefined, Query.BuildingType(Building2D_Rectangle(10, 10, 1, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.student_dormitory)));
            Assert.Equal(Regulation.Enums.BuildingType.Undefined, Query.BuildingType(Building2D_Rectangle(10, 10, 1, BuildingGeneralFunction.office_buildings)));
        }
    }
}
