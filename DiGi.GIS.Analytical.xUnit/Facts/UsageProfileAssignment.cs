using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.HVAC;
using DiGi.Analytical.Building.HVAC.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Parameter.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System.Collections.Generic;
using System.Linq;
using HVAC = DiGi.Analytical.Building.HVAC;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests Query.UsageType: the BDOT10k specific functions first, in their priority order, then the general function, and Undefined where neither names a usage (DiGi-Organization/DiGi.Analytical#9).
        /// </summary>
        [Fact]
        public void Building2D_UsageType()
        {
            Assert.Equal(UsageType.Undefined, Query.UsageType(null));

            (BuildingGeneralFunction BuildingGeneralFunction, BuildingSpecificFunction[] BuildingSpecificFunctions, UsageType UsageType)[] expectations =
            [
                (BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building], UsageType.SingleFamily),
                (BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.multi_family_building], UsageType.MultiFamily),
                (BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building, BuildingSpecificFunction.multi_family_building], UsageType.MultiFamily),
                (BuildingGeneralFunction.residential_buildings, [], UsageType.Undefined),
                (BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.student_dormitory], UsageType.CollectiveResidence),
                (BuildingGeneralFunction.other_non_residential_buildings, [BuildingSpecificFunction.social_care_home], UsageType.CollectiveResidence),
                (BuildingGeneralFunction.commercial_service_buildings, [BuildingSpecificFunction.hotel], UsageType.CollectiveResidence),
                (BuildingGeneralFunction.commercial_service_buildings, [BuildingSpecificFunction.restaurant], UsageType.Gastronomy),
                (BuildingGeneralFunction.commercial_service_buildings, [BuildingSpecificFunction.restaurant, BuildingSpecificFunction.single_family_building], UsageType.SingleFamily),
                (BuildingGeneralFunction.commercial_service_buildings, [BuildingSpecificFunction.shopping_center], UsageType.Retail),
                (BuildingGeneralFunction.commercial_service_buildings, [], UsageType.Retail),
                (BuildingGeneralFunction.buildings_for_education_science_culture_and_sports, [BuildingSpecificFunction.sports_hall], UsageType.Sport),
                (BuildingGeneralFunction.buildings_for_education_science_culture_and_sports, [BuildingSpecificFunction.primary_school, BuildingSpecificFunction.gym], UsageType.Education),
                (BuildingGeneralFunction.buildings_for_education_science_culture_and_sports, [BuildingSpecificFunction.museum], UsageType.Education),
                (BuildingGeneralFunction.office_buildings, [], UsageType.Office),
                (BuildingGeneralFunction.office_buildings, [BuildingSpecificFunction.city_hall], UsageType.Office),
                (BuildingGeneralFunction.hospital_and_healthcare_buildings, [], UsageType.Healthcare),
                (BuildingGeneralFunction.other_non_residential_buildings, [BuildingSpecificFunction.hospice], UsageType.Healthcare),
                (BuildingGeneralFunction.industrial_buildings, [], UsageType.Production),
                (BuildingGeneralFunction.agricultural_production_service_utility_buildings, [], UsageType.Production),
                (BuildingGeneralFunction.tanks_silos_and_storage_buildings, [], UsageType.Storage),
                (BuildingGeneralFunction.industrial_buildings, [BuildingSpecificFunction.warehouse], UsageType.Storage),
                (BuildingGeneralFunction.transport_and_communication_buildings, [], UsageType.Undefined),
                (BuildingGeneralFunction.transport_and_communication_buildings, [BuildingSpecificFunction.train_station], UsageType.Undefined),
                (BuildingGeneralFunction.other_non_residential_buildings, [], UsageType.Undefined),
            ];

            foreach ((BuildingGeneralFunction buildingGeneralFunction, BuildingSpecificFunction[] buildingSpecificFunctions, UsageType usageType) in expectations)
            {
                Building2D building2D = Building2D_Rectangle(10, 10, 1, buildingGeneralFunction, buildingSpecificFunctions);
                Assert.Equal(usageType, Query.UsageType(building2D));
            }

            // Every specific function maps to one usage type at most, whatever the general function.
            Dictionary<UsageType, int> counts = [];
            foreach (BuildingSpecificFunction buildingSpecificFunction in System.Enum.GetValues(typeof(BuildingSpecificFunction)).Cast<BuildingSpecificFunction>())
            {
                UsageType usageType = Query.UsageType(Building2D_Rectangle(10, 10, 1, BuildingGeneralFunction.other_non_residential_buildings, buildingSpecificFunction));
                counts[usageType] = counts.TryGetValue(usageType, out int count) ? count + 1 : 1;
            }

            Assert.Equal(1, counts[UsageType.SingleFamily]);
            Assert.Equal(1, counts[UsageType.MultiFamily]);
            Assert.Equal(20, counts[UsageType.CollectiveResidence]);
            Assert.Equal(7, counts[UsageType.Healthcare]);
            Assert.Equal(9, counts[UsageType.Education]);
            Assert.Equal(10, counts[UsageType.Sport]);
            Assert.Equal(4, counts[UsageType.Gastronomy]);
            Assert.Equal(7, counts[UsageType.Retail]);
            Assert.Equal(13, counts[UsageType.Office]);
            Assert.Equal(6, counts[UsageType.Production]);
            Assert.Equal(5, counts[UsageType.Storage]);
        }

        /// <summary>
        /// Tests Modify.AssignInternalConditions on BDOT10k buildings: a two-storey single-family house with an occupancy estimate gets the scaled single-family condition in every space, and buildings without a usage type fall back to multi-family (residential) or office (other), with the fallback recorded in the usage source (DiGi-Organization/DiGi.Analytical#9).
        /// </summary>
        [Fact]
        public void BuildingModel_AssignInternalConditions()
        {
            static string? UsageSource(IInternalCondition? internalCondition)
            {
                return internalCondition is not null && internalCondition.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.UsageSource), out string? result, new GetValueSettings(true, false)) ? result : null;
            }

            Building2D building2D = Building2D_Rectangle(10, 10, 2, BuildingGeneralFunction.residential_buildings, BuildingSpecificFunction.single_family_building);
            BuildingModel? buildingModel = Create.BuildingModel(building2D);
            Assert.NotNull(buildingModel);

            Assert.False(Modify.AssignInternalConditions(null, building2D));
            Assert.False(buildingModel.AssignInternalConditions(null));

            OccupancyData occupancyData = new(building2D.Reference, 200.0, 4);
            Assert.True(buildingModel.AssignInternalConditions(building2D, occupancyData, 2005, "test estimate"));

            List<ISpace>? spaces = buildingModel.GetSpaces<ISpace>();
            Assert.NotNull(spaces);
            Assert.NotEmpty(spaces);

            HVAC.Classes.UsageProfile usageProfile = HVAC.Create.UsageProfile(UsageType.SingleFamily)!;
            foreach (ISpace space in spaces)
            {
                IInternalCondition? internalCondition = buildingModel.GetSpaceInternalConditions(space)?.Single().InternalCondition;
                Assert.Equal("SingleFamily", internalCondition?.Name);
                Assert.Equal(0.2, internalCondition.Profile(InternalGainProfileType.Infiltration)!.Values!.Single(), 1e-9);

                // 4 persons on 200 m2 against the standard 30 m2 a person.
                double personsPerArea = 4.0 / 200.0;
                Assert.Equal(6.8 + ((personsPerArea - (1.0 / 30.0)) * usageProfile.PersonGain), internalCondition.Profile(InternalGainProfileType.TotalInternalGain)!.Values!.Single(), 1e-9);
                Assert.Equal(1.40 * 0.90 * personsPerArea * 30.0, internalCondition.SystemProperties()!.DomesticHotWater, 1e-9);
                Assert.DoesNotContain("fallback", UsageSource(internalCondition));
            }

            // Residential without a single- or multi-family function: multi-family, by fallback, unscaled.
            Building2D building2D_Residential = Building2D_Rectangle(10, 10, 1, BuildingGeneralFunction.residential_buildings);
            BuildingModel? buildingModel_Residential = Create.BuildingModel(building2D_Residential);
            Assert.True(buildingModel_Residential.AssignInternalConditions(building2D_Residential));
            IInternalCondition? internalCondition_Residential = buildingModel_Residential!.GetSpaceInternalConditions(buildingModel_Residential.GetSpaces<ISpace>()![0])?.Single().InternalCondition;
            Assert.Equal("MultiFamily", internalCondition_Residential?.Name);
            Assert.StartsWith("fallback", UsageSource(internalCondition_Residential));
            Assert.Equal(7.1, internalCondition_Residential.Profile(InternalGainProfileType.TotalInternalGain)!.Values!.Single(), 1e-9);

            // Transport: office, by fallback.
            Building2D building2D_Transport = Building2D_Rectangle(10, 10, 1, BuildingGeneralFunction.transport_and_communication_buildings);
            BuildingModel? buildingModel_Transport = Create.BuildingModel(building2D_Transport);
            Assert.True(buildingModel_Transport.AssignInternalConditions(building2D_Transport));
            IInternalCondition? internalCondition_Transport = buildingModel_Transport!.GetSpaceInternalConditions(buildingModel_Transport.GetSpaces<ISpace>()![0])?.Single().InternalCondition;
            Assert.Equal("Office", internalCondition_Transport?.Name);
            Assert.StartsWith("fallback", UsageSource(internalCondition_Transport));
        }
    }
}
