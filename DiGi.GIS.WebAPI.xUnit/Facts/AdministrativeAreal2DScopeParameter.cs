using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Table.Classes;
using DiGi.PostgreSQL.Table.Enums;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Builds the caller filter group carried by the area-scoped parameter facts.
        /// <para>The DiGi round trip (<c>SerializationCheck</c>) runs without the filter group, the measures and the sort conditions: <see cref="FilterGroup"/> is not a serializable object and reloads as null, an <see cref="AggregateMeasure"/> list fails to reload, and a <see cref="SortCondition"/> list reloads its items as null - none affect the wire, where the body is bound by System.Text.Json, which the binding assertion covers.</para>
        /// </summary>
        private static FilterGroup AdministrativeAreal2DScopeParameter_FilterGroup()
        {
            return new FilterGroup() { LogicalOperator = FilterLogicalOperator.Or, FilterConditions = [new FilterCondition() { ColumnUniqueId = "building_specific_functions", FilterOperator = FilterOperator.Contains, Value = "church" }] };
        }

        /// <summary>
        /// Verifies the area-scoped <see cref="BuildingDataByFilterGroupParameter"/> (DiGi-Organization/DiGi.GIS.WebAPI#83) binds from a request body, survives the DiGi string round trip on the members that round trip supports, and its copy constructor copies every member, the column and sort lists into new lists (the sort items shared).
        /// </summary>
        [Fact]
        public void BuildingDataByFilterGroupParameter_AdministrativeAreal2DId_CopyAndSerialization()
        {
            BuildingDataByFilterGroupParameter parameter_Source = new() { FilterGroup = AdministrativeAreal2DScopeParameter_FilterGroup(), ColumnUniqueIds = ["reference", "floor_area"], AdministrativeAreal2DId = 55413, SortConditions = [new SortCondition() { ColumnUniqueId = "storeys", Descending = true }, new SortCondition() { ColumnUniqueId = "reference" }], Limit = 1 };

            BuildingDataByFilterGroupParameter parameter_RoundTrip = new(parameter_Source) { FilterGroup = null!, SortConditions = [] };
            Core.xUnit.Query.SerializationCheck(parameter_RoundTrip);

            BuildingDataByFilterGroupParameter? parameter_Bound = System.Text.Json.JsonSerializer.Deserialize<BuildingDataByFilterGroupParameter>(System.Text.Json.JsonSerializer.Serialize(parameter_Source), System.Text.Json.JsonSerializerOptions.Web);
            Assert.NotNull(parameter_Bound);
            Assert.Equal(55413, parameter_Bound.AdministrativeAreal2DId);
            Assert.Null(parameter_Bound.CountyId);
            Assert.Equal("church", Assert.Single(parameter_Bound.FilterGroup!.FilterConditions).Value?.ToString());
            Assert.Equal(2, parameter_Bound.SortConditions!.Count);
            Assert.Equal("storeys", parameter_Bound.SortConditions[0].ColumnUniqueId);
            Assert.True(parameter_Bound.SortConditions[0].Descending);
            Assert.Equal("reference", parameter_Bound.SortConditions[1].ColumnUniqueId);
            Assert.False(parameter_Bound.SortConditions[1].Descending);
            Assert.Equal(1, parameter_Bound.Limit);

            BuildingDataByFilterGroupParameter parameter_Copy = new(parameter_Source);
            Assert.Same(parameter_Source.FilterGroup, parameter_Copy.FilterGroup);
            Assert.Equal(parameter_Source.ColumnUniqueIds, parameter_Copy.ColumnUniqueIds);
            Assert.NotSame(parameter_Source.ColumnUniqueIds, parameter_Copy.ColumnUniqueIds);
            Assert.Null(parameter_Copy.CountyId);
            Assert.Equal(55413, parameter_Copy.AdministrativeAreal2DId);
            Assert.Equal(parameter_Source.SortConditions, parameter_Copy.SortConditions);
            Assert.NotSame(parameter_Source.SortConditions, parameter_Copy.SortConditions);
            Assert.Same(parameter_Source.SortConditions![0], parameter_Copy.SortConditions![0]);
            Assert.Equal(1, parameter_Copy.Limit);
        }

        /// <summary>
        /// Verifies the area-scoped <see cref="SinglevalueAggregateRequestParameter"/> (DiGi-Organization/DiGi.GIS.WebAPI#83) binds from a request body, survives the DiGi string round trip on the members that round trip supports, and its copy constructor copies every member.
        /// </summary>
        [Fact]
        public void SinglevalueAggregateRequestParameter_AdministrativeAreal2DId_CopyAndSerialization()
        {
            SinglevalueAggregateRequestParameter parameter_Source = new() { ColumnUniqueId = "reference", SinglevalueAggregateFunction = SinglevalueAggregateFunction.Count, FilterGroup = AdministrativeAreal2DScopeParameter_FilterGroup(), AdministrativeAreal2DId = 55413 };

            SinglevalueAggregateRequestParameter parameter_RoundTrip = new(parameter_Source) { FilterGroup = null };
            Core.xUnit.Query.SerializationCheck(parameter_RoundTrip);

            SinglevalueAggregateRequestParameter? parameter_Bound = System.Text.Json.JsonSerializer.Deserialize<SinglevalueAggregateRequestParameter>(System.Text.Json.JsonSerializer.Serialize(parameter_Source), System.Text.Json.JsonSerializerOptions.Web);
            Assert.NotNull(parameter_Bound);
            Assert.Equal(55413, parameter_Bound.AdministrativeAreal2DId);
            Assert.Null(parameter_Bound.CountyId);
            Assert.Equal("church", Assert.Single(parameter_Bound.FilterGroup!.FilterConditions).Value?.ToString());

            SinglevalueAggregateRequestParameter parameter_Copy = new(parameter_Source);
            Assert.Null(parameter_Copy.CountyId);
            Assert.Equal(55413, parameter_Copy.AdministrativeAreal2DId);
            Assert.Equal("reference", parameter_Copy.ColumnUniqueId);
            Assert.Equal(SinglevalueAggregateFunction.Count, parameter_Copy.SinglevalueAggregateFunction);
            Assert.Same(parameter_Source.FilterGroup, parameter_Copy.FilterGroup);
        }

        /// <summary>
        /// Verifies the area-scoped <see cref="MultivalueAggregateRequestParameter"/> (DiGi-Organization/DiGi.GIS.WebAPI#83) binds from a request body, survives the DiGi string round trip on the members that round trip supports, and its copy constructor copies every member.
        /// </summary>
        [Fact]
        public void MultivalueAggregateRequestParameter_AdministrativeAreal2DId_CopyAndSerialization()
        {
            MultivalueAggregateRequestParameter parameter_Source = new() { ColumnUniqueId = "building_specific_functions", MultivalueAggregateFunction = MultivalueAggregateFunction.SplitValueDistribution, Separator = ";", MaxGroups = 50, FilterGroup = AdministrativeAreal2DScopeParameter_FilterGroup(), AdministrativeAreal2DId = 55413 };

            MultivalueAggregateRequestParameter parameter_RoundTrip = new(parameter_Source) { FilterGroup = null };
            Core.xUnit.Query.SerializationCheck(parameter_RoundTrip);

            MultivalueAggregateRequestParameter? parameter_Bound = System.Text.Json.JsonSerializer.Deserialize<MultivalueAggregateRequestParameter>(System.Text.Json.JsonSerializer.Serialize(parameter_Source), System.Text.Json.JsonSerializerOptions.Web);
            Assert.NotNull(parameter_Bound);
            Assert.Equal(55413, parameter_Bound.AdministrativeAreal2DId);
            Assert.Null(parameter_Bound.CountyId);
            Assert.Equal("church", Assert.Single(parameter_Bound.FilterGroup!.FilterConditions).Value?.ToString());

            MultivalueAggregateRequestParameter parameter_Copy = new(parameter_Source);
            Assert.Null(parameter_Copy.CountyId);
            Assert.Equal(55413, parameter_Copy.AdministrativeAreal2DId);
            Assert.Equal("building_specific_functions", parameter_Copy.ColumnUniqueId);
            Assert.Equal(MultivalueAggregateFunction.SplitValueDistribution, parameter_Copy.MultivalueAggregateFunction);
            Assert.Equal(";", parameter_Copy.Separator);
            Assert.Equal(50, parameter_Copy.MaxGroups);
            Assert.Same(parameter_Source.FilterGroup, parameter_Copy.FilterGroup);
        }

        /// <summary>
        /// Verifies the area-scoped <see cref="AggregateSummariesRequestParameter"/> (DiGi-Organization/DiGi.GIS.WebAPI#83) binds from a request body, survives the DiGi string round trip on the members that round trip supports, and its copy constructor copies every member, the measures into a new list.
        /// </summary>
        [Fact]
        public void AggregateSummariesRequestParameter_AdministrativeAreal2DId_CopyAndSerialization()
        {
            AggregateSummariesRequestParameter parameter_Source = new() { FilterGroup = AdministrativeAreal2DScopeParameter_FilterGroup(), Measures = [new AggregateMeasure("reference", SinglevalueAggregateFunction.Count), new AggregateMeasure("floor_area", SinglevalueAggregateFunction.Max)], AdministrativeAreal2DId = 55413 };

            AggregateSummariesRequestParameter parameter_RoundTrip = new(parameter_Source) { FilterGroup = null, Measures = [] };
            Core.xUnit.Query.SerializationCheck(parameter_RoundTrip);

            AggregateSummariesRequestParameter? parameter_Bound = System.Text.Json.JsonSerializer.Deserialize<AggregateSummariesRequestParameter>(System.Text.Json.JsonSerializer.Serialize(parameter_Source), System.Text.Json.JsonSerializerOptions.Web);
            Assert.NotNull(parameter_Bound);
            Assert.Equal(55413, parameter_Bound.AdministrativeAreal2DId);
            Assert.Null(parameter_Bound.CountyId);
            Assert.Equal("church", Assert.Single(parameter_Bound.FilterGroup!.FilterConditions).Value?.ToString());

            AggregateSummariesRequestParameter parameter_Copy = new(parameter_Source);
            Assert.Null(parameter_Copy.CountyId);
            Assert.Equal(55413, parameter_Copy.AdministrativeAreal2DId);
            Assert.Same(parameter_Source.FilterGroup, parameter_Copy.FilterGroup);
            Assert.Equal(parameter_Source.Measures, parameter_Copy.Measures);
            Assert.NotSame(parameter_Source.Measures, parameter_Copy.Measures);
        }

        /// <summary>
        /// Verifies the area-scoped <see cref="HistogramRequestParameter"/> (DiGi-Organization/DiGi.GIS.WebAPI#83) binds from a request body, survives the DiGi string round trip on the members that round trip supports, and its copy constructor copies every member.
        /// </summary>
        [Fact]
        public void HistogramRequestParameter_AdministrativeAreal2DId_CopyAndSerialization()
        {
            HistogramRequestParameter parameter_Source = new() { ColumnUniqueId = "floor_area", BucketCount = 20, HistogramBucketing = HistogramBucketing.EqualCount, FilterGroup = AdministrativeAreal2DScopeParameter_FilterGroup(), AdministrativeAreal2DId = 55413 };

            HistogramRequestParameter parameter_RoundTrip = new(parameter_Source) { FilterGroup = null };
            Core.xUnit.Query.SerializationCheck(parameter_RoundTrip);

            HistogramRequestParameter? parameter_Bound = System.Text.Json.JsonSerializer.Deserialize<HistogramRequestParameter>(System.Text.Json.JsonSerializer.Serialize(parameter_Source), System.Text.Json.JsonSerializerOptions.Web);
            Assert.NotNull(parameter_Bound);
            Assert.Equal(55413, parameter_Bound.AdministrativeAreal2DId);
            Assert.Null(parameter_Bound.CountyId);
            Assert.Equal("church", Assert.Single(parameter_Bound.FilterGroup!.FilterConditions).Value?.ToString());

            HistogramRequestParameter parameter_Copy = new(parameter_Source);
            Assert.Null(parameter_Copy.CountyId);
            Assert.Equal(55413, parameter_Copy.AdministrativeAreal2DId);
            Assert.Equal("floor_area", parameter_Copy.ColumnUniqueId);
            Assert.Equal(20, parameter_Copy.BucketCount);
            Assert.Equal(HistogramBucketing.EqualCount, parameter_Copy.HistogramBucketing);
            Assert.Same(parameter_Source.FilterGroup, parameter_Copy.FilterGroup);
        }

        /// <summary>
        /// Verifies the area-scoped <see cref="UniqueValuesByColumnUniqueIdParameter"/> (DiGi-Organization/DiGi.GIS.WebAPI#83) binds from a request body, survives the DiGi string round trip on the members that round trip supports, and its copy constructor copies every member.
        /// </summary>
        [Fact]
        public void UniqueValuesByColumnUniqueIdParameter_AdministrativeAreal2DId_CopyAndSerialization()
        {
            UniqueValuesByColumnUniqueIdParameter parameter_Source = new() { ColumnUniqueId = "municipality_name", FilterGroup = AdministrativeAreal2DScopeParameter_FilterGroup(), AdministrativeAreal2DId = 55413 };

            UniqueValuesByColumnUniqueIdParameter parameter_RoundTrip = new(parameter_Source) { FilterGroup = null };
            Core.xUnit.Query.SerializationCheck(parameter_RoundTrip);

            UniqueValuesByColumnUniqueIdParameter? parameter_Bound = System.Text.Json.JsonSerializer.Deserialize<UniqueValuesByColumnUniqueIdParameter>(System.Text.Json.JsonSerializer.Serialize(parameter_Source), System.Text.Json.JsonSerializerOptions.Web);
            Assert.NotNull(parameter_Bound);
            Assert.Equal(55413, parameter_Bound.AdministrativeAreal2DId);
            Assert.Null(parameter_Bound.CountyId);
            Assert.Equal("church", Assert.Single(parameter_Bound.FilterGroup!.FilterConditions).Value?.ToString());

            UniqueValuesByColumnUniqueIdParameter parameter_Copy = new(parameter_Source);
            Assert.Null(parameter_Copy.CountyId);
            Assert.Equal(55413, parameter_Copy.AdministrativeAreal2DId);
            Assert.Equal("municipality_name", parameter_Copy.ColumnUniqueId);
            Assert.Same(parameter_Source.FilterGroup, parameter_Copy.FilterGroup);
        }
    }
}
