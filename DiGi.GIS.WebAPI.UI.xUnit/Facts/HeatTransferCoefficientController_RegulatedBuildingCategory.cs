using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.GIS.Enums;
using DiGi.GIS.WebAPI.UI.Controllers;
using DiGi.GIS.WebAPI.UI.Enums;
using DiGi.GIS.WebAPI.UI.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that the regulated heat transfer coefficient panel lists only the limits that applied to the building's category (#82), with the real stages of the regulations.
        /// <para>A 2010 building (2008 regulation) gets its residential or its public rows, not both. A 2005 multi-family building (2002 regulation) gets the residential window and door rows and the floor-on-ground resistance, without the single-family wall, roof and basement ceiling rows, and the E ≤ E0 note. A 2005 single-family building gets all residential rows. A 2000 multi-family building (1997 amendment, single-family limits only) gets no limits and the note. A 2015 building (2014 stage, one table for every building) never reads the footprint: a multi-family footprint is served, yet the category stays undefined and the footprint is never requested.</para>
        /// </summary>
        [Fact]
        public async Task GetRegulatedHeatTransferCoefficientsByReferenceAsync_RegulatedBuildingCategory()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = DiGi.Analytical.Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();

            GIS.Classes.Building2D building2D_SingleFamily = new(Guid.NewGuid(), "3020", null, 2, BuildingPhase.occupied, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]);
            GIS.Classes.Building2D building2D_MultiFamily = new(Guid.NewGuid(), "3020", null, 5, BuildingPhase.occupied, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.multi_family_building]);
            GIS.Classes.Building2D building2D_Office = new(Guid.NewGuid(), "3020", null, 4, BuildingPhase.occupied, BuildingGeneralFunction.office_buildings, []);

            async Task<RegulatedHeatTransferCoefficientsViewModel> RegulatedHeatTransferCoefficientsViewModelAsync(short year, GIS.Classes.Building2D building2D, bool footprintRead)
            {
                DiGi.GIS.Classes.YearBuiltData yearBuiltData = new("3020");
                yearBuiltData.SetUserYearBuilt(year);

                RouteStubWebApi routeStubWebApi = new RouteStubWebApi()
                    .Answer("yearbuiltdata/itemsbyreference", HttpStatusCode.OK, Core.Convert.ToSystem_String(new List<DiGi.GIS.Classes.YearBuiltData> { yearBuiltData }) ?? string.Empty)
                    .Answer("regulatedheattransfercoefficientsbyyear", HttpStatusCode.OK, Core.Convert.ToSystem_String(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(year, 1, 1))) ?? string.Empty)
                    .Answer("building2d/itembyreference", HttpStatusCode.OK, Core.Convert.ToSystem_String(building2D) ?? string.Empty);

                IActionResult result = await new HeatTransferCoefficientController(new HandlerHttpClientFactory(routeStubWebApi)).GetRegulatedHeatTransferCoefficientsByReferenceAsync("3020", null);
                PartialViewResult partialViewResult = Assert.IsType<PartialViewResult>(result);
                RegulatedHeatTransferCoefficientsViewModel regulatedHeatTransferCoefficientsViewModel = Assert.IsType<RegulatedHeatTransferCoefficientsViewModel>(partialViewResult.Model);
                Assert.Equal(year, regulatedHeatTransferCoefficientsViewModel.Year);

                // The footprint is the last read when it is read at all.
                Assert.Equal(footprintRead, routeStubWebApi.LastRequest?.RequestUri?.AbsolutePath.Contains("building2d/itembyreference") == true);
                return regulatedHeatTransferCoefficientsViewModel;
            }

            // 2010, 2008 regulation, residential building: the residential rows only.
            RegulatedHeatTransferCoefficientsViewModel regulatedHeatTransferCoefficientsViewModel_2010_Residential = await RegulatedHeatTransferCoefficientsViewModelAsync(2010, building2D_SingleFamily, true);
            Assert.IsType<RegulatedHeatTransferCoefficients_2009>(regulatedHeatTransferCoefficientsViewModel_2010_Residential.RegulatedHeatTransferCoefficients);
            Assert.Equal(RegulatedBuildingCategory.SingleFamily, regulatedHeatTransferCoefficientsViewModel_2010_Residential.RegulatedBuildingCategory);
            Assert.Equal(8, regulatedHeatTransferCoefficientsViewModel_2010_Residential.Names.Count);
            Assert.All(regulatedHeatTransferCoefficientsViewModel_2010_Residential.Names, x => Assert.StartsWith("Residential building", x));
            Assert.Contains("Residential building roof window or skylight", regulatedHeatTransferCoefficientsViewModel_2010_Residential.Names);
            Assert.False(regulatedHeatTransferCoefficientsViewModel_2010_Residential.EnergyIndicatorRequirement);

            // 2010, 2008 regulation, public building: the public rows only.
            RegulatedHeatTransferCoefficientsViewModel regulatedHeatTransferCoefficientsViewModel_2010_Public = await RegulatedHeatTransferCoefficientsViewModelAsync(2010, building2D_Office, true);
            Assert.Equal(RegulatedBuildingCategory.NonResidential, regulatedHeatTransferCoefficientsViewModel_2010_Public.RegulatedBuildingCategory);
            Assert.Equal(7, regulatedHeatTransferCoefficientsViewModel_2010_Public.Names.Count);
            Assert.All(regulatedHeatTransferCoefficientsViewModel_2010_Public.Names, x => Assert.StartsWith("Public building", x));
            Assert.False(regulatedHeatTransferCoefficientsViewModel_2010_Public.EnergyIndicatorRequirement);

            // 2005, 2002 regulation, multi-family building: window and door rows, the floor-on-ground resistance and the E0 note.
            RegulatedHeatTransferCoefficientsViewModel regulatedHeatTransferCoefficientsViewModel_2005_MultiFamily = await RegulatedHeatTransferCoefficientsViewModelAsync(2005, building2D_MultiFamily, true);
            Assert.IsType<RegulatedHeatTransferCoefficients_2002>(regulatedHeatTransferCoefficientsViewModel_2005_MultiFamily.RegulatedHeatTransferCoefficients);
            Assert.Equal(RegulatedBuildingCategory.MultiFamily, regulatedHeatTransferCoefficientsViewModel_2005_MultiFamily.RegulatedBuildingCategory);
            List<string> names_2005_MultiFamily = ["Residential building window or balcony door (climatic zones I, II, III)", "Residential building window or balcony door (climatic zones IV, V)", "Residential building roof window or skylight", "Residential building door"];
            Assert.Equal(names_2005_MultiFamily.OrderBy(x => x, StringComparer.Ordinal), regulatedHeatTransferCoefficientsViewModel_2005_MultiFamily.Names.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Equal(["Residential building floor on ground"], regulatedHeatTransferCoefficientsViewModel_2005_MultiFamily.MinimumThermalResistanceNames);
            Assert.True(regulatedHeatTransferCoefficientsViewModel_2005_MultiFamily.EnergyIndicatorRequirement);

            // 2005, 2002 regulation, single-family building: the single-family rows as well.
            RegulatedHeatTransferCoefficientsViewModel regulatedHeatTransferCoefficientsViewModel_2005_SingleFamily = await RegulatedHeatTransferCoefficientsViewModelAsync(2005, building2D_SingleFamily, true);
            Assert.Equal(8, regulatedHeatTransferCoefficientsViewModel_2005_SingleFamily.Names.Count);
            Assert.Equal(4, regulatedHeatTransferCoefficientsViewModel_2005_SingleFamily.Names.Count(x => x.StartsWith("Residential building (single-family)")));
            Assert.Contains("Residential building (single-family) multilayer wall", regulatedHeatTransferCoefficientsViewModel_2005_SingleFamily.Names);
            Assert.Contains("Residential building door", regulatedHeatTransferCoefficientsViewModel_2005_SingleFamily.Names);
            Assert.DoesNotContain(regulatedHeatTransferCoefficientsViewModel_2005_SingleFamily.Names, x => x.StartsWith("Public building"));
            Assert.Equal(["Residential building floor on ground"], regulatedHeatTransferCoefficientsViewModel_2005_SingleFamily.MinimumThermalResistanceNames);
            Assert.False(regulatedHeatTransferCoefficientsViewModel_2005_SingleFamily.EnergyIndicatorRequirement);

            // 2000, 1997 amendment, multi-family building: no limit applies, only the E0 note.
            RegulatedHeatTransferCoefficientsViewModel regulatedHeatTransferCoefficientsViewModel_2000_MultiFamily = await RegulatedHeatTransferCoefficientsViewModelAsync(2000, building2D_MultiFamily, true);
            Assert.IsType<RegulatedHeatTransferCoefficients_1997>(regulatedHeatTransferCoefficientsViewModel_2000_MultiFamily.RegulatedHeatTransferCoefficients);
            Assert.Empty(regulatedHeatTransferCoefficientsViewModel_2000_MultiFamily.Names);
            Assert.Empty(regulatedHeatTransferCoefficientsViewModel_2000_MultiFamily.MinimumThermalResistanceNames);
            Assert.True(regulatedHeatTransferCoefficientsViewModel_2000_MultiFamily.EnergyIndicatorRequirement);

            // 2000, 1997 amendment, public building: the public rows and the public floor-on-ground resistance.
            RegulatedHeatTransferCoefficientsViewModel regulatedHeatTransferCoefficientsViewModel_2000_Public = await RegulatedHeatTransferCoefficientsViewModelAsync(2000, building2D_Office, true);
            Assert.Equal(7, regulatedHeatTransferCoefficientsViewModel_2000_Public.Names.Count);
            Assert.Equal(["Public building floor on ground"], regulatedHeatTransferCoefficientsViewModel_2000_Public.MinimumThermalResistanceNames);

            // 2015, 2014 stage: one table for every building, so the footprint is never read.
            RegulatedHeatTransferCoefficientsViewModel regulatedHeatTransferCoefficientsViewModel_2015 = await RegulatedHeatTransferCoefficientsViewModelAsync(2015, building2D_MultiFamily, false);
            Assert.Equal(RegulatedBuildingCategory.Undefined, regulatedHeatTransferCoefficientsViewModel_2015.RegulatedBuildingCategory);
            Assert.Equal(7, regulatedHeatTransferCoefficientsViewModel_2015.Names.Count);
            Assert.False(regulatedHeatTransferCoefficientsViewModel_2015.EnergyIndicatorRequirement);
        }

        /// <summary>
        /// Tests <see cref="Query.RegulatedBuildingCategory(GIS.Classes.Building2D?)"/> and <see cref="Query.IsRegulated(string?, RegulatedBuildingCategory)"/> (#82): a residential building is single-family or multi-family only when exactly one of the two specific functions is set; the limit names select the categories they apply to.
        /// </summary>
        [Fact]
        public void Query_RegulatedBuildingCategory()
        {
            Assert.Equal(RegulatedBuildingCategory.Undefined, Query.RegulatedBuildingCategory(null));
            Assert.Equal(RegulatedBuildingCategory.Residential, new GIS.Classes.Building2D(Guid.NewGuid(), "1", null, 3, BuildingPhase.occupied, BuildingGeneralFunction.residential_buildings, []).RegulatedBuildingCategory());
            Assert.Equal(RegulatedBuildingCategory.Residential, new GIS.Classes.Building2D(Guid.NewGuid(), "1", null, 3, BuildingPhase.occupied, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building, BuildingSpecificFunction.multi_family_building]).RegulatedBuildingCategory());
            Assert.Equal(RegulatedBuildingCategory.MultiFamily, new GIS.Classes.Building2D(Guid.NewGuid(), "1", null, 3, BuildingPhase.occupied, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.multi_family_building]).RegulatedBuildingCategory());
            Assert.Equal(RegulatedBuildingCategory.NonResidential, new GIS.Classes.Building2D(Guid.NewGuid(), "1", null, 3, BuildingPhase.occupied, BuildingGeneralFunction.industrial_buildings, []).RegulatedBuildingCategory());

            (string Name, RegulatedBuildingCategory RegulatedBuildingCategory, bool Expected)[] cases =
            [
                ("Residential building (single-family) solid wall", RegulatedBuildingCategory.SingleFamily, true),
                ("Residential building (single-family) solid wall", RegulatedBuildingCategory.MultiFamily, false),
                ("Residential building (single-family) solid wall", RegulatedBuildingCategory.Residential, true),
                ("Residential building (single-family) solid wall", RegulatedBuildingCategory.NonResidential, false),
                ("Residential building door", RegulatedBuildingCategory.MultiFamily, true),
                ("Residential building door", RegulatedBuildingCategory.NonResidential, false),
                ("Public building door", RegulatedBuildingCategory.NonResidential, true),
                ("Public building door", RegulatedBuildingCategory.SingleFamily, false),
                ("Wall", RegulatedBuildingCategory.MultiFamily, true),
                ("Public building door", RegulatedBuildingCategory.Undefined, true),
                (" ", RegulatedBuildingCategory.Undefined, false),
            ];

            foreach ((string name, RegulatedBuildingCategory regulatedBuildingCategory, bool expected) in cases)
            {
                Assert.True(Query.IsRegulated(name, regulatedBuildingCategory) == expected, $"'{name}' for {regulatedBuildingCategory}: {!expected} instead of {expected}");
            }
        }
    }
}
