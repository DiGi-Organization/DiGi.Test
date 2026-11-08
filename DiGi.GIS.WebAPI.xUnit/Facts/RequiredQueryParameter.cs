using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Asserts every query parameter a request cannot be answered correctly without carries <see cref="BindRequiredAttribute"/>.
        /// <para>The attribute is the only machine-readable "required" signal the served OpenAPI document has: Swashbuckle reads it off the controller parameter, and ASP.NET Core turns its absence into a model-state error - HTTP 400 - instead of silently binding <c>default(T)</c>. A non-nullable value type parameter (a coordinate, a county id, a lattice spacing) is the case that used to succeed with the wrong scope, so it is what these assertions exist to pin.</para>
        /// </summary>
        [Fact]
        public void GisControllers_MandatoryQueryParameters_AreBindRequired()
        {
            (Type Type, string Method, string Parameter)[] parameters =
            [
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetAdministrativeAreal2DReferenceByCodeAsync), "code"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetAdministrativeAreal2DReferenceByIdAsync), "id"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetAdministrativeAreal2DReferencePathByIdAsync), "id"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetAdministrativeAreal2DReferencesByAdministrativeArealTypeAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetAdministrativeAreal2DReferencesByCodeAsync), "code"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetIdByCodeAsync), "code"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetIdsByAdministrativeArealTypeAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetIdsByCodeAsync), "code"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetSubCodesAsync), "code"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetReferencePathsByNameAsync), "text"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetReferencesByNameAsync), "text"),
                (typeof(BuildingController), nameof(BuildingController.GetItemByReferenceAsync), "reference"),
                (typeof(BuildingController), nameof(BuildingController.GetItemByReferenceAsync), "countyId"),
                (typeof(BuildingController), nameof(BuildingController.GetItemsByReferenceAsync), "reference"),
                (typeof(Building2DController), nameof(Building2DController.GetBuilding2DReferenceByIdAsync), "id"),
                (typeof(Building2DController), nameof(Building2DController.GetBuilding2DReferenceByReferenceAsync), "reference"),
                (typeof(Building2DController), nameof(Building2DController.GetBuilding2DReferencesByAdministrativeAreal2DIdAsync), "administrativeAreal2DId"),
                (typeof(Building2DController), nameof(Building2DController.GetCentroidsByAdministrativeAreal2DIdAsync), "administrativeAreal2DId"),
                (typeof(Building2DController), nameof(Building2DController.GetPoint2DsByAdministrativeAreal2DIdAsync), "administrativeAreal2DId"),
                (typeof(Building2DController), nameof(Building2DController.GetReferencesByCountyIdAsync), "countyId"),
                (typeof(BuildingDataController), nameof(BuildingDataController.GetCountByCountyIdAsync), "countyId"),
                (typeof(BuildingDataController), nameof(BuildingDataController.GetCountyIdsByReferenceAsync), "reference"),
                (typeof(BuildingDataController), nameof(BuildingDataController.GetCoverageByCountyIdAsync), "countyId"),
                (typeof(BuildingDataController), nameof(BuildingDataController.GetTableByReferenceAsync), "reference"),
                (typeof(BuildingDataController), nameof(BuildingDataController.GetUniqueValuesAsync), "columnUniqueId"),
                (typeof(BuildingDataController), nameof(BuildingDataController.UpdateItemsByCountyIdsAsync), "countyIds"),
                (typeof(BuildingModelController), nameof(BuildingModelController.GetCountByCountyIdAsync), "countyId"),
                (typeof(BuildingModelController), nameof(BuildingModelController.GetItemsByCircleAsync), "x"),
                (typeof(BuildingModelController), nameof(BuildingModelController.GetItemsByCircleAsync), "y"),
                (typeof(BuildingModelController), nameof(BuildingModelController.GetItemsByReferencesAsync), "references"),
                (typeof(BuildingModelController), nameof(BuildingModelController.GetLatestCreatedAtByCountyIdAsync), "countyId"),
                (typeof(EPWFileController), nameof(EPWFileController.GetEPWFileAsync), "x"),
                (typeof(EPWFileController), nameof(EPWFileController.GetEPWFileAsync), "y"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetCountByCountyIdAsync), "countyId"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetEstimatedCoverageFactorAsync), "administrativeAreal2DId"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetImageByReferenceAsync), "reference"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetImageByReferenceAsync), "year"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetItemByReferenceAsync), "reference"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetOrtoDatasReferenceByReferenceAsync), "reference"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetOrtoDatasReferencesByCountyIdAsync), "countyId"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetSubdivisionLinksByCountyIdAsync), "countyId"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetYearsByReferenceAsync), "reference"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.UpdateItemsByCodeAsync), "code"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.UpdateItemsByCountyIdsAsync), "countyIds"),
                (typeof(TerrainController), nameof(TerrainController.GetCountByCountyIdAsync), "countyId"),
                (typeof(TerrainController), nameof(TerrainController.GetCoverageByCountyIdAsync), "countyId"),
                (typeof(TerrainController), nameof(TerrainController.GetCoverageByCountyIdAsync), "gridSize"),
                (typeof(TerrainController), nameof(TerrainController.GetDensitiesByCountyIdsAsync), "countyIds"),
                (typeof(TerrainController), nameof(TerrainController.GetGapsByBoundingBoxAsync), "x_1"),
                (typeof(TerrainController), nameof(TerrainController.GetGapsByBoundingBoxAsync), "y_1"),
                (typeof(TerrainController), nameof(TerrainController.GetGapsByBoundingBoxAsync), "x_2"),
                (typeof(TerrainController), nameof(TerrainController.GetGapsByBoundingBoxAsync), "y_2"),
                (typeof(TerrainController), nameof(TerrainController.GetGapsByBoundingBoxAsync), "gridSize"),
                (typeof(TerrainController), nameof(TerrainController.GetMesh3DByBoundingBoxAsync), "x_1"),
                (typeof(TerrainController), nameof(TerrainController.GetMesh3DByBoundingBoxAsync), "y_1"),
                (typeof(TerrainController), nameof(TerrainController.GetMesh3DByBoundingBoxAsync), "x_2"),
                (typeof(TerrainController), nameof(TerrainController.GetMesh3DByBoundingBoxAsync), "y_2"),
                (typeof(TerrainController), nameof(TerrainController.GetMesh3DByCircleAsync), "x"),
                (typeof(TerrainController), nameof(TerrainController.GetMesh3DByCircleAsync), "y"),
                (typeof(UnitController), nameof(UnitController.GetComplianceAsync), "administrativeArealType"),
                (typeof(UnitController), nameof(UnitController.GetItemByIdAsync), "id"),
                (typeof(YearBuiltDataController), nameof(YearBuiltDataController.GetCountByCountyIdAsync), "countyId"),
                (typeof(YearBuiltDataController), nameof(YearBuiltDataController.GetReferencesByCountyIdAsync), "countyId")
            ];

            Assert.NotEmpty(parameters);

            foreach ((Type type, string method, string parameter) in parameters)
            {
                ParameterInfo parameterInfo = FindQueryParameter(type, method, parameter);

                Assert.NotNull(parameterInfo.GetCustomAttribute<FromQueryAttribute>());
                Assert.NotNull(parameterInfo.GetCustomAttribute<BindRequiredAttribute>());
            }
        }

        /// <summary>
        /// Asserts the query parameters whose absence is meaningful stay optional, so no caller that legitimately omits one is turned into HTTP 400.
        /// <para>Named explicitly rather than derived, because the classification is a per-endpoint decision: a county identifier that means "every county" when omitted, a type filter that may be left off, and the lattice origin whose documented default is zero all look like a mandatory selector to a rule and are not.</para>
        /// </summary>
        [Fact]
        public void GisControllers_OptionalQueryParameters_AreNotBindRequired()
        {
            (Type Type, string Method, string Parameter)[] parameters =
            [
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetAdministrativeAreal2DReferenceByCodeAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetAdministrativeAreal2DReferencesByCodeAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetIdByCodeAsync), "administrativeArealType"),
                (typeof(BuildingController), nameof(BuildingController.GetCountAsync), "countyId"),
                (typeof(BuildingController), nameof(BuildingController.GetItemByReferenceAsync), "x"),
                (typeof(Building2DController), nameof(Building2DController.GetCountyPartMismatchesAsync), "code"),
                (typeof(OrtoDatasController), nameof(OrtoDatasController.GetRandomBuilding2DReferenceAsync), "countyIds"),
                (typeof(TerrainController), nameof(TerrainController.GetMesh3DByCircleAsync), "tolerance"),
                (typeof(TerrainController), nameof(TerrainController.GetCoverageByCountyIdAsync), "originX"),
                (typeof(TerrainController), nameof(TerrainController.GetCoverageByCountyIdAsync), "originY"),
                (typeof(TerrainController), nameof(TerrainController.GetSummariesByCountyIdsAsync), "countyIds")
            ];

            Assert.NotEmpty(parameters);

            foreach ((Type type, string method, string parameter) in parameters)
            {
                ParameterInfo parameterInfo = FindQueryParameter(type, method, parameter);

                Assert.Null(parameterInfo.GetCustomAttribute<BindRequiredAttribute>());
            }
        }

        /// <summary>
        /// Asserts the terrain mesh circle carries the radius and diameter ceilings as schema constraints.
        /// <para>The action rejects a radius beyond <see cref="Constants.Terrain.MaximumRadius"/> at runtime, but without the attribute a client generator reads an unbounded number. The diameter is capped at twice the radius, because it is halved before the same check.</para>
        /// </summary>
        [Fact]
        public void TerrainController_TerrainMeshRadii_CarryMaximum()
        {
            RangeAttribute? rangeAttribute_Radius = FindRange(typeof(TerrainController), nameof(TerrainController.GetMesh3DByCircleAsync), "radius");
            Assert.NotNull(rangeAttribute_Radius);
            Assert.Equal(Constants.Terrain.MaximumRadius, System.Convert.ToDouble(rangeAttribute_Radius.Maximum, CultureInfo.CurrentCulture));

            RangeAttribute? rangeAttribute_Diameter = FindRange(typeof(TerrainController), nameof(TerrainController.GetMesh3DByCircleAsync), "diameter");
            Assert.NotNull(rangeAttribute_Diameter);
            Assert.Equal(2 * Constants.Terrain.MaximumRadius, System.Convert.ToDouble(rangeAttribute_Diameter.Maximum, CultureInfo.CurrentCulture));
        }

        /// <summary>
        /// Asserts every genuinely mandatory query parameter on the endpoints hidden from the OpenAPI document (those inheriting <c>IgnoreApi = true</c>) carries <see cref="BindRequiredAttribute"/>.
        /// <para>The attribute is invisible in the document for these actions, so it is the reflection fact - not Swashbuckle - that keeps the requirement declared. The case that used to answer with the wrong scope is the non-nullable coordinate: an omitted <c>x</c> or <c>y</c> binds <c>0.0</c>, which passes an <c>IsNaN</c> guard and runs the query at the origin. The key-gated write selectors are included because the action already rejects a missing one with 400, so the attribute only moves that decision ahead of the authorization check.</para>
        /// </summary>
        [Fact]
        public void GisControllers_HiddenMandatoryQueryParameters_AreBindRequired()
        {
            (Type Type, string Method, string Parameter)[] parameters =
            [
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemByCodeAsync), "code"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemByIdAsync), "id"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByAdministrativeArealTypeAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByBoundingBoxAsync), "x_1"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByBoundingBoxAsync), "y_1"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByBoundingBoxAsync), "x_2"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByBoundingBoxAsync), "y_2"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCircleAsync), "x"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCircleAsync), "y"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCodeAsync), "code"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByPointAsync), "x"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByPointAsync), "y"),
                (typeof(Building2DController), nameof(Building2DController.GetItemByIdAsync), "id"),
                (typeof(Building2DController), nameof(Building2DController.GetItemByPointAsync), "x"),
                (typeof(Building2DController), nameof(Building2DController.GetItemByPointAsync), "y"),
                (typeof(Building2DController), nameof(Building2DController.GetItemByReferenceAsync), "reference"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByBoundingBoxAsync), "x_1"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByBoundingBoxAsync), "y_1"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByBoundingBoxAsync), "x_2"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByBoundingBoxAsync), "y_2"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByCircleAsync), "x"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByCircleAsync), "y"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByCountyIdAsync), "countyId"),
                (typeof(HeatTransferCoefficientController), nameof(HeatTransferCoefficientController.GetRegulatedHeatTransferCoefficientsByYearAsync), "year"),
                (typeof(OccupancyDataController), nameof(OccupancyDataController.GetAdministrativeAreal2DItemsByReferenceAsync), "reference"),
                (typeof(OccupancyDataController), nameof(OccupancyDataController.GetBuilding2DItemsByReferenceAsync), "reference"),
                (typeof(YearBuiltDataController), nameof(YearBuiltDataController.GetItemsByReferenceAsync), "reference"),
                (typeof(BuildingController), nameof(BuildingController.UpdateItemsAsync), "code"),
                (typeof(BuildingController), nameof(BuildingController.UpdateItemsByCountyIdsAsync), "countyIds"),
                (typeof(Building2DController), nameof(Building2DController.UpdateItemsByCountyIdsAsync), "countyIds"),
                (typeof(BuildingModelController), nameof(BuildingModelController.UpdateItemsAsync), "code"),
                (typeof(BuildingModelController), nameof(BuildingModelController.UpdateItemsByCountyIdsAsync), "countyIds"),
                (typeof(OccupancyDataController), nameof(OccupancyDataController.Building2DUpdateItemsAsync), "code"),
                (typeof(OccupancyDataController), nameof(OccupancyDataController.Building2DUpdateItemsByCountyIdsAsync), "countyIds"),
                (typeof(YearBuiltDataController), nameof(YearBuiltDataController.UpdateItemsAsync), "code"),
                (typeof(YearBuiltDataController), nameof(YearBuiltDataController.UpdateItemsByCountyIdsAsync), "countyIds")
            ];

            Assert.NotEmpty(parameters);

            foreach ((Type type, string method, string parameter) in parameters)
            {
                ParameterInfo parameterInfo = FindQueryParameter(type, method, parameter);

                Assert.NotNull(parameterInfo.GetCustomAttribute<FromQueryAttribute>());
                Assert.True(parameterInfo.GetCustomAttribute<BindRequiredAttribute>() is not null, $"Missing [BindRequired] on {type.Name}.{method}({parameter}).");
            }
        }

        /// <summary>
        /// Asserts the hidden endpoints' query parameters whose absence is meaningful stay optional, so the hidden set is pinned the way the document set is.
        /// <para>Named explicitly rather than derived, because the classification is a per-endpoint decision: a type filter that means every type when omitted, the optional county the reference may be narrowed to, the circle tolerance, and the two write endpoints where <c>code</c> genuinely may be absent all look like a mandatory selector to a rule and are not.</para>
        /// </summary>
        [Fact]
        public void GisControllers_HiddenOptionalQueryParameters_AreNotBindRequired()
        {
            (Type Type, string Method, string Parameter)[] parameters =
            [
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemByCodeAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCodeAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByBoundingBoxAsync), "tolerance"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByBoundingBoxAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCircleAsync), "radius"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCircleAsync), "diameter"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCircleAsync), "tolerance"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCircleAsync), "administrativeArealType"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByPointAsync), "tolerance"),
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByPointAsync), "administrativeArealType"),
                (typeof(Building2DController), nameof(Building2DController.GetItemByIdAsync), "countyId"),
                (typeof(Building2DController), nameof(Building2DController.GetItemByPointAsync), "tolerance"),
                (typeof(Building2DController), nameof(Building2DController.GetItemByReferenceAsync), "countyId"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByBoundingBoxAsync), "tolerance"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByCircleAsync), "radius"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByCircleAsync), "diameter"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByCircleAsync), "tolerance"),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByReferencesAsync), "countyId"),
                (typeof(Building2DController), nameof(Building2DController.GetPoint2DsByReferencesAsync), "countyId"),
                (typeof(Building2DController), nameof(Building2DController.UpdateItemAsync), "code"),
                (typeof(Building2DController), nameof(Building2DController.UpdateItemAsync), "countyId"),
                (typeof(Building2DController), nameof(Building2DController.UpdateItemsAsync), "code"),
                (typeof(Building2DController), nameof(Building2DController.UpdateItemsByCountyIdsAsync), "code"),
                (typeof(OccupancyDataController), nameof(OccupancyDataController.GetBuilding2DItemsByReferenceAsync), "countyId"),
                (typeof(YearBuiltDataController), nameof(YearBuiltDataController.GetItemsByReferenceAsync), "countyId")
            ];

            Assert.NotEmpty(parameters);

            foreach ((Type type, string method, string parameter) in parameters)
            {
                ParameterInfo parameterInfo = FindQueryParameter(type, method, parameter);

                Assert.Null(parameterInfo.GetCustomAttribute<BindRequiredAttribute>());
            }
        }

        /// <summary>
        /// Asserts the two hidden circle endpoints carry the <see cref="Constants.Terrain.MaximumRadius"/> ceiling as a <see cref="RangeAttribute"/> maximum, mirroring the terrain mesh circle.
        /// <para>Unlike the terrain action these endpoints do not enforce the cap in their own body - <c>[ApiController]</c> model-state validation is the enforcement - so the attribute is the cap rather than a description of one. Radius and diameter are both optional (either may be supplied) but each is bounded; the diameter uses twice the radius because it is halved before use.</para>
        /// </summary>
        [Fact]
        public void GisControllers_HiddenCircleRadii_CarryMaximum()
        {
            (Type Type, string Method)[] actions =
            [
                (typeof(AdministrativeAreal2DController), nameof(AdministrativeAreal2DController.GetItemsByCircleAsync)),
                (typeof(Building2DController), nameof(Building2DController.GetItemsByCircleAsync))
            ];

            Assert.NotEmpty(actions);

            foreach ((Type type, string method) in actions)
            {
                RangeAttribute? rangeAttribute_Radius = FindRange(type, method, "radius");
                Assert.NotNull(rangeAttribute_Radius);
                Assert.Equal(Constants.Terrain.MaximumRadius, System.Convert.ToDouble(rangeAttribute_Radius.Maximum, CultureInfo.CurrentCulture));

                RangeAttribute? rangeAttribute_Diameter = FindRange(type, method, "diameter");
                Assert.NotNull(rangeAttribute_Diameter);
                Assert.Equal(2 * Constants.Terrain.MaximumRadius, System.Convert.ToDouble(rangeAttribute_Diameter.Maximum, CultureInfo.CurrentCulture));
            }
        }

        private static ParameterInfo FindQueryParameter(Type type, string method, string parameter)
        {
            MethodInfo? methodInfo = type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.NotNull(methodInfo);

            ParameterInfo? parameterInfo = Array.Find(methodInfo!.GetParameters(), x => string.Equals(x.Name, parameter, StringComparison.Ordinal));
            Assert.NotNull(parameterInfo);

            return parameterInfo!;
        }

        private static RangeAttribute? FindRange(Type type, string method, string parameter)
        {
            return FindQueryParameter(type, method, parameter).GetCustomAttribute<RangeAttribute>();
        }
    }
}
