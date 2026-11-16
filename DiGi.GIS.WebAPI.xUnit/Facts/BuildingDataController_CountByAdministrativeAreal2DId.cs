using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.GIS.WebAPI.Enums;
using DiGi.PostgreSQL.Table.Classes;
using DiGi.PostgreSQL.Table.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Builds an administrative area reference for the <c>countbyadministrativeareal2did</c> facts.
        /// </summary>
        private static AdministrativeAreal2DReference CountByAdministrativeAreal2DId_Reference(int id, AdministrativeArealType administrativeArealType, int? countyId)
        {
            return new AdministrativeAreal2DReference() { Id = id, AdministrativeArealType = administrativeArealType, CountyId = countyId };
        }

        /// <summary>
        /// Pins the <c>countbyadministrativeareal2did</c> route and documented responses of DiGi-Organization/DiGi.GIS.WebAPI#53: a <c>POST</c> answering a bare <c>long</c> with 204, 400, 404, 500 and 503 documented, <c>commandtimeout</c> bound from the query string and defaulting to 600, a required nullable area id and an optional filter group.
        /// </summary>
        [Fact]
        public void BuildingDataController_CountByAdministrativeAreal2DId_Contract()
        {
            MethodInfo method = typeof(BuildingDataController).GetMethod(nameof(BuildingDataController.GetCountByAdministrativeAreal2DIdAsync))!;
            Assert.NotNull(method);

            HttpPostAttribute? httpPost = method.GetCustomAttribute<HttpPostAttribute>();
            Assert.NotNull(httpPost);
            Assert.Equal("countbyadministrativeareal2did", httpPost.Template);

            Dictionary<int, Type?> produces = [];
            foreach (ProducesResponseTypeAttribute attribute in method.GetCustomAttributes<ProducesResponseTypeAttribute>())
            {
                produces[attribute.StatusCode] = attribute.Type == typeof(void) ? null : attribute.Type;
            }

            Assert.Equal([200, 204, 400, 404, 500, 503], new SortedSet<int>(produces.Keys));
            Assert.Equal(typeof(long), produces[200]);

            ParameterInfo parameterInfo_CommandTimeout = Assert.Single(method.GetParameters(), x => x.Name == "commandTimeout");
            Assert.Equal("commandtimeout", parameterInfo_CommandTimeout.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromQueryAttribute>()?.Name);
            Assert.Equal(600, parameterInfo_CommandTimeout.DefaultValue);
            Assert.Equal(typeof(CancellationToken), method.GetParameters()[^1].ParameterType);

            PropertyInfo propertyInfo_Id = typeof(BuildingDataCountByAdministrativeAreal2DIdParameter).GetProperty(nameof(BuildingDataCountByAdministrativeAreal2DIdParameter.AdministrativeAreal2DId))!;
            Assert.Equal(typeof(int?), propertyInfo_Id.PropertyType);
            Assert.NotNull(propertyInfo_Id.GetCustomAttribute<RequiredAttribute>());

            PropertyInfo propertyInfo_FilterGroup = typeof(BuildingDataCountByAdministrativeAreal2DIdParameter).GetProperty(nameof(BuildingDataCountByAdministrativeAreal2DIdParameter.FilterGroup))!;
            Assert.Null(propertyInfo_FilterGroup.GetCustomAttribute<RequiredAttribute>());
        }

        /// <summary>
        /// Pins the guards of <c>countbyadministrativeareal2did</c>: an absent body, an omitted, zero or negative area id, and a negative <c>commandtimeout</c> are all refused with 400 before any database work, and with no connection to build the area cannot be read, so a valid id answers 404.
        /// </summary>
        [Fact]
        public async Task BuildingDataController_CountByAdministrativeAreal2DId_GuardsAndNoConnection()
        {
            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));

                Assert.IsType<BadRequestResult>(await controller.GetCountByAdministrativeAreal2DIdAsync(null!, 600));
                Assert.IsType<BadRequestResult>(await controller.GetCountByAdministrativeAreal2DIdAsync(new BuildingDataCountByAdministrativeAreal2DIdParameter(), 600));
                Assert.IsType<BadRequestResult>(await controller.GetCountByAdministrativeAreal2DIdAsync(new BuildingDataCountByAdministrativeAreal2DIdParameter() { AdministrativeAreal2DId = 0 }, 600));
                Assert.IsType<BadRequestResult>(await controller.GetCountByAdministrativeAreal2DIdAsync(new BuildingDataCountByAdministrativeAreal2DIdParameter() { AdministrativeAreal2DId = -1 }, 600));

                BuildingDataCountByAdministrativeAreal2DIdParameter parameter = new() { AdministrativeAreal2DId = 53469 };
                Assert.IsType<BadRequestResult>(await controller.GetCountByAdministrativeAreal2DIdAsync(parameter, -1));
                Assert.IsType<NotFoundResult>(await controller.GetCountByAdministrativeAreal2DIdAsync(parameter, 600));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Pins the body contract of <c>countbyadministrativeareal2did</c>: under the web defaults the input formatter deserializes with, the area id and a nested filter group bind, and a body without the area id leaves it <c>null</c> so the action can refuse it rather than count area <c>0</c>.
        /// </summary>
        [Fact]
        public void BuildingDataCountByAdministrativeAreal2DIdParameter_Binds()
        {
            System.Text.Json.JsonSerializerOptions jsonSerializerOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

            BuildingDataCountByAdministrativeAreal2DIdParameter? parameter = System.Text.Json.JsonSerializer.Deserialize<BuildingDataCountByAdministrativeAreal2DIdParameter>("{\"AdministrativeAreal2DId\":53469,\"FilterGroup\":{\"LogicalOperator\":0,\"FilterConditions\":[{\"ColumnUniqueId\":\"is_residential\",\"FilterOperator\":0,\"Value\":true}],\"FilterGroups\":[]}}", jsonSerializerOptions);
            Assert.NotNull(parameter);
            Assert.Equal(53469, parameter.AdministrativeAreal2DId);
            Assert.NotNull(parameter.FilterGroup);
            Assert.Single(parameter.FilterGroup.FilterConditions);

            BuildingDataCountByAdministrativeAreal2DIdParameter? parameter_Omitted = System.Text.Json.JsonSerializer.Deserialize<BuildingDataCountByAdministrativeAreal2DIdParameter>("{}", jsonSerializerOptions);
            Assert.NotNull(parameter_Omitted);
            Assert.Null(parameter_Omitted.AdministrativeAreal2DId);
            Assert.Null(parameter_Omitted.FilterGroup);
        }

        /// <summary>
        /// Verifies <see cref="DiGi.GIS.WebAPI.Query.SubdivisionIdsByCountyId(IEnumerable{AdministrativeAreal2DReference}?)"/> groups ids by county in the order a county was first met, and skips references without a county and identifiers already seen.
        /// </summary>
        [Fact]
        public void Query_SubdivisionIdsByCountyId()
        {
            Assert.Empty(DiGi.GIS.WebAPI.Query.SubdivisionIdsByCountyId(null));

            Dictionary<int, List<int>> subdivisionIds_ByCountyId = DiGi.GIS.WebAPI.Query.SubdivisionIdsByCountyId(
            [
                CountByAdministrativeAreal2DId_Reference(11, AdministrativeArealType.Subdivision, 7),
                CountByAdministrativeAreal2DId_Reference(21, AdministrativeArealType.Subdivision, 3),
                CountByAdministrativeAreal2DId_Reference(12, AdministrativeArealType.Subdivision, 7),
                CountByAdministrativeAreal2DId_Reference(99, AdministrativeArealType.Subdivision, null),
                CountByAdministrativeAreal2DId_Reference(11, AdministrativeArealType.Subdivision, 7),
            ]);

            Assert.Equal([7, 3], subdivisionIds_ByCountyId.Keys);
            Assert.Equal([11, 12], subdivisionIds_ByCountyId[7]);
            Assert.Equal([21], subdivisionIds_ByCountyId[3]);
        }

        /// <summary>
        /// Verifies <see cref="DiGi.GIS.WebAPI.Create.SubdivisionFilterGroup(FilterGroup?, IEnumerable{int}?)"/> confines a count with one <c>And</c> group holding an <c>In</c> condition on the subdivision column, nests the caller's group whole (an <c>Or</c> group is not flattened), omits a null or empty caller group, and answers null for no subdivision.
        /// </summary>
        [Fact]
        public void Create_SubdivisionFilterGroup()
        {
            Assert.Null(DiGi.GIS.WebAPI.Create.SubdivisionFilterGroup(null, null));
            Assert.Null(DiGi.GIS.WebAPI.Create.SubdivisionFilterGroup(null, []));

            FilterGroup? filterGroup_NoCaller = DiGi.GIS.WebAPI.Create.SubdivisionFilterGroup(null, [1, 2]);
            Assert.NotNull(filterGroup_NoCaller);
            Assert.Equal(FilterLogicalOperator.And, filterGroup_NoCaller.LogicalOperator);
            Assert.Empty(filterGroup_NoCaller.FilterGroups);
            FilterCondition filterCondition = Assert.Single(filterGroup_NoCaller.FilterConditions);
            Assert.False(string.IsNullOrWhiteSpace(filterCondition.ColumnUniqueId));
            Assert.Equal(FilterOperator.In, filterCondition.FilterOperator);
            Assert.Equal([1, 2], Assert.IsType<List<int>>(filterCondition.Value));

            Assert.Empty(DiGi.GIS.WebAPI.Create.SubdivisionFilterGroup(new FilterGroup(), [1])!.FilterGroups);

            FilterGroup filterGroup_Caller = new() { LogicalOperator = FilterLogicalOperator.Or, FilterConditions = [new FilterCondition() { ColumnUniqueId = "is_residential", FilterOperator = FilterOperator.Equals, Value = true }] };
            FilterGroup? filterGroup_WithCaller = DiGi.GIS.WebAPI.Create.SubdivisionFilterGroup(filterGroup_Caller, [1]);
            Assert.NotNull(filterGroup_WithCaller);
            Assert.Same(filterGroup_Caller, Assert.Single(filterGroup_WithCaller.FilterGroups));
            Assert.Equal(FilterLogicalOperator.Or, filterGroup_Caller.LogicalOperator);
            Assert.Single(filterGroup_WithCaller.FilterConditions);
        }

        /// <summary>
        /// Builds a building reference for the <c>countbyadministrativeareal2did</c> facts.
        /// </summary>
        private static Building2DReference CountByAdministrativeAreal2DId_Building2DReference(string reference, int? countyId, int? subdivisionId)
        {
            return new Building2DReference() { Reference = reference, CountyId = countyId, SubdivisionId = subdivisionId };
        }

        /// <summary>
        /// A building reference read that must not be reached.
        /// </summary>
        private static Task<List<Building2DReference>?> CountByAdministrativeAreal2DId_NoPolygonRead(int id, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The polygon read must not be reached.");
        }

        /// <summary>
        /// A Subdivision children read that must not be reached.
        /// </summary>
        private static Task<List<AdministrativeAreal2DReference>?> CountByAdministrativeAreal2DId_NoChildrenRead(int id, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The Subdivision children read must not be reached.");
        }

        /// <summary>
        /// Counts the references of the <c>In</c> condition a scope carries, standing in for the database count.
        /// </summary>
        private static int CountByAdministrativeAreal2DId_InCount(FilterGroup? filterGroup)
        {
            FilterCondition filterCondition = Assert.Single(filterGroup!.FilterConditions);
            Assert.Equal(FilterOperator.In, filterCondition.FilterOperator);
            return Assert.IsType<List<string>>(filterCondition.Value).Count;
        }

        /// <summary>
        /// Verifies an area above the Subdivision level is resolved to its subdivisions, grouped by county in first-seen order, counted once per county with that county's ids and the caller's filters, and the per-county counts are summed (3 + 5 = 8, not the maximum), strictly one county after another.
        /// </summary>
        [Fact]
        public async Task Query_CountByAdministrativeAreal2DIdAsync_GroupsByCountySumsAndRunsSequentially()
        {
            FilterGroup filterGroup_Caller = new() { FilterConditions = [new FilterCondition() { ColumnUniqueId = "is_residential", FilterOperator = FilterOperator.Equals, Value = true }] };
            List<string> events = [];
            List<(FilterGroup? FilterGroup, int CountyId)> calls = [];
            int running = 0;
            int running_Maximum = 0;
            Dictionary<int, long> counts = new() { [7] = 3, [3] = 5 };

            (AdministrativeAreal2DCountStatus Status, long Count) result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(
                53469,
                filterGroup_Caller,
                (id, token) => Task.FromResult<AdministrativeAreal2DReference?>(CountByAdministrativeAreal2DId_Reference(id, AdministrativeArealType.Municipality, 7)),
                (id, token) => Task.FromResult<List<AdministrativeAreal2DReference>?>(
                [
                    CountByAdministrativeAreal2DId_Reference(11, AdministrativeArealType.Subdivision, 7),
                    CountByAdministrativeAreal2DId_Reference(21, AdministrativeArealType.Subdivision, 3),
                    CountByAdministrativeAreal2DId_Reference(12, AdministrativeArealType.Subdivision, 7),
                ]),
                CountByAdministrativeAreal2DId_NoPolygonRead,
                async (filterGroup, countyId, token) =>
                {
                    int running_Now = Interlocked.Increment(ref running);
                    running_Maximum = Math.Max(running_Maximum, running_Now);
                    events.Add($"start {countyId}");
                    calls.Add((filterGroup, countyId));
                    await Task.Delay(30, token);
                    events.Add($"end {countyId}");
                    Interlocked.Decrement(ref running);
                    return counts[countyId];
                },
                500,
                200000);

            Assert.Equal(AdministrativeAreal2DCountStatus.Counted, result.Status);
            Assert.Equal(8, result.Count);
            Assert.Equal(["start 7", "end 7", "start 3", "end 3"], events);
            Assert.Equal(1, running_Maximum);

            Assert.Equal([7, 3], calls.ConvertAll(x => x.CountyId));
            Assert.Equal([11, 12], Assert.IsType<List<int>>(Assert.Single(calls[0].FilterGroup!.FilterConditions).Value));
            Assert.Equal([21], Assert.IsType<List<int>>(Assert.Single(calls[1].FilterGroup!.FilterConditions).Value));
            Assert.Same(filterGroup_Caller, Assert.Single(calls[0].FilterGroup!.FilterGroups));
        }

        /// <summary>
        /// Reproduces DiGi-Organization/DiGi.GIS.WebAPI#83: a nested Subdivision (Warsaw's Bemowo district, 55413, which files none of its buildings under its own id) is counted through its polygon, not by membership.
        /// <para>The polygon read answers buildings filed under other subdivisions (or none) and spread over two county partitions; each partition is confined by <c>reference IN (…)</c> with that partition's references only, the caller's group nested whole, a reference repeated inside one partition counted once, the same reference in two partitions counted in each, a building without a county skipped, and the counts summed (5 + 2 = 7). Membership of 55413 would have counted 0. Subdivision children are never read.</para>
        /// </summary>
        [Fact]
        public async Task Query_CountByAdministrativeAreal2DIdAsync_SubdivisionCountsByPolygon()
        {
            FilterGroup filterGroup_Caller = new() { FilterConditions = [new FilterCondition() { ColumnUniqueId = "building_specific_functions", FilterOperator = FilterOperator.Contains, Value = "church" }] };
            List<(FilterGroup? FilterGroup, int CountyId)> calls = [];

            (AdministrativeAreal2DCountStatus Status, long Count) result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(
                55413,
                filterGroup_Caller,
                (id, token) => Task.FromResult<AdministrativeAreal2DReference?>(CountByAdministrativeAreal2DId_Reference(id, AdministrativeArealType.Subdivision, 55417)),
                CountByAdministrativeAreal2DId_NoChildrenRead,
                (id, token) => Task.FromResult<List<Building2DReference>?>(
                [
                    CountByAdministrativeAreal2DId_Building2DReference("A", 55417, 55626),
                    CountByAdministrativeAreal2DId_Building2DReference("B", 55417, 55599),
                    CountByAdministrativeAreal2DId_Building2DReference("C", 55418, 55489),
                    CountByAdministrativeAreal2DId_Building2DReference("D", 55417, 55489),
                    CountByAdministrativeAreal2DId_Building2DReference("A", 55417, 55626),
                    CountByAdministrativeAreal2DId_Building2DReference("E", 55417, null),
                    CountByAdministrativeAreal2DId_Building2DReference("C", 55417, 55599),
                    CountByAdministrativeAreal2DId_Building2DReference("F", null, 55599),
                    CountByAdministrativeAreal2DId_Building2DReference("G", 55418, 55489),
                ]),
                (filterGroup, countyId, token) =>
                {
                    calls.Add((filterGroup, countyId));
                    return Task.FromResult<long?>(CountByAdministrativeAreal2DId_InCount(filterGroup));
                },
                500,
                200000);

            Assert.Equal(AdministrativeAreal2DCountStatus.Counted, result.Status);
            Assert.Equal(7, result.Count);

            Assert.Equal([55417, 55418], calls.ConvertAll(x => x.CountyId));
            Assert.Equal(["A", "B", "D", "E", "C"], Assert.IsType<List<string>>(Assert.Single(calls[0].FilterGroup!.FilterConditions).Value));
            Assert.Equal(["C", "G"], Assert.IsType<List<string>>(Assert.Single(calls[1].FilterGroup!.FilterConditions).Value));
            Assert.Same(filterGroup_Caller, Assert.Single(calls[0].FilterGroup!.FilterGroups));
            Assert.Same(filterGroup_Caller, Assert.Single(calls[1].FilterGroup!.FilterGroups));
        }

        /// <summary>
        /// Verifies a County is counted as its own partition with the caller's filter group passed unchanged (null stays null), reading neither Subdivision children nor any polygon.
        /// </summary>
        [Fact]
        public async Task Query_CountByAdministrativeAreal2DIdAsync_CountyCountsItsPartition()
        {
            FilterGroup filterGroup_Caller = new() { FilterConditions = [new FilterCondition() { ColumnUniqueId = "is_residential", FilterOperator = FilterOperator.Equals, Value = true }] };

            foreach (FilterGroup? filterGroup_Input in new FilterGroup?[] { filterGroup_Caller, null })
            {
                List<(FilterGroup? FilterGroup, int CountyId)> calls = [];

                (AdministrativeAreal2DCountStatus Status, long Count) result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(
                    55417,
                    filterGroup_Input,
                    (id, token) => Task.FromResult<AdministrativeAreal2DReference?>(CountByAdministrativeAreal2DId_Reference(id, AdministrativeArealType.County, null)),
                    CountByAdministrativeAreal2DId_NoChildrenRead,
                    CountByAdministrativeAreal2DId_NoPolygonRead,
                    (filterGroup, countyId, token) =>
                    {
                        calls.Add((filterGroup, countyId));
                        return Task.FromResult<long?>(155307);
                    },
                    500,
                    200000);

                Assert.Equal(AdministrativeAreal2DCountStatus.Counted, result.Status);
                Assert.Equal(155307, result.Count);

                (FilterGroup? filterGroup_Call, int countyId_Call) = Assert.Single(calls);
                Assert.Equal(55417, countyId_Call);
                Assert.Same(filterGroup_Input, filterGroup_Call);
            }
        }

        /// <summary>
        /// Verifies the paths that count nothing: an unknown area, an area with no subdivision (null and empty alike), a Subdivision whose polygon holds no building (or none with a county), a polygon read that could not be made, an area beyond the county or reference cap - all before any count - and a count that could not be made. The reference cap is inclusive: exactly the cap is counted.
        /// </summary>
        [Fact]
        public async Task Query_CountByAdministrativeAreal2DIdAsync_StatusesThatCountNothing()
        {
            int countCalls = 0;
            Func<FilterGroup?, int, CancellationToken, Task<long?>> countAsync = (filterGroup, countyId, token) =>
            {
                countCalls++;
                return Task.FromResult<long?>(1);
            };

            Func<int, CancellationToken, Task<AdministrativeAreal2DReference?>> readMunicipalityAsync = (id, token) => Task.FromResult<AdministrativeAreal2DReference?>(CountByAdministrativeAreal2DId_Reference(id, AdministrativeArealType.Municipality, 7));
            Func<int, CancellationToken, Task<AdministrativeAreal2DReference?>> readSubdivisionAsync = (id, token) => Task.FromResult<AdministrativeAreal2DReference?>(CountByAdministrativeAreal2DId_Reference(id, AdministrativeArealType.Subdivision, 7));
            List<Building2DReference> building2DReferences_TwoCounties = [CountByAdministrativeAreal2DId_Building2DReference("A", 7, 11), CountByAdministrativeAreal2DId_Building2DReference("B", 3, 21)];

            (AdministrativeAreal2DCountStatus Status, long Count) result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, (id, token) => Task.FromResult<AdministrativeAreal2DReference?>(null), CountByAdministrativeAreal2DId_NoChildrenRead, CountByAdministrativeAreal2DId_NoPolygonRead, countAsync, 500, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.AreaNotFound, result.Status);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readMunicipalityAsync, (id, token) => Task.FromResult<List<AdministrativeAreal2DReference>?>(null), CountByAdministrativeAreal2DId_NoPolygonRead, countAsync, 500, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.NoSubdivisions, result.Status);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readMunicipalityAsync, (id, token) => Task.FromResult<List<AdministrativeAreal2DReference>?>([]), CountByAdministrativeAreal2DId_NoPolygonRead, countAsync, 500, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.NoSubdivisions, result.Status);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readMunicipalityAsync, (id, token) => Task.FromResult<List<AdministrativeAreal2DReference>?>([CountByAdministrativeAreal2DId_Reference(11, AdministrativeArealType.Subdivision, 7), CountByAdministrativeAreal2DId_Reference(21, AdministrativeArealType.Subdivision, 3)]), CountByAdministrativeAreal2DId_NoPolygonRead, countAsync, 1, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.TooManyCounties, result.Status);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readSubdivisionAsync, CountByAdministrativeAreal2DId_NoChildrenRead, (id, token) => Task.FromResult<List<Building2DReference>?>([]), countAsync, 500, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.NoBuildings, result.Status);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readSubdivisionAsync, CountByAdministrativeAreal2DId_NoChildrenRead, (id, token) => Task.FromResult<List<Building2DReference>?>([CountByAdministrativeAreal2DId_Building2DReference("A", null, 11)]), countAsync, 500, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.NoBuildings, result.Status);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readSubdivisionAsync, CountByAdministrativeAreal2DId_NoChildrenRead, (id, token) => Task.FromResult<List<Building2DReference>?>(null), countAsync, 500, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.NotResolved, result.Status);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readSubdivisionAsync, CountByAdministrativeAreal2DId_NoChildrenRead, (id, token) => Task.FromResult<List<Building2DReference>?>(building2DReferences_TwoCounties), countAsync, 1, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.TooManyCounties, result.Status);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readSubdivisionAsync, CountByAdministrativeAreal2DId_NoChildrenRead, (id, token) => Task.FromResult<List<Building2DReference>?>(building2DReferences_TwoCounties), countAsync, 500, 1);
            Assert.Equal(AdministrativeAreal2DCountStatus.TooManyReferences, result.Status);

            Assert.Equal(0, countCalls);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readSubdivisionAsync, CountByAdministrativeAreal2DId_NoChildrenRead, (id, token) => Task.FromResult<List<Building2DReference>?>(building2DReferences_TwoCounties), countAsync, 500, 2);
            Assert.Equal(AdministrativeAreal2DCountStatus.Counted, result.Status);
            Assert.Equal(2, result.Count);
            Assert.Equal(2, countCalls);

            result = await DiGi.GIS.WebAPI.Query.CountByAdministrativeAreal2DIdAsync(1, null, readMunicipalityAsync, (id, token) => Task.FromResult<List<AdministrativeAreal2DReference>?>([CountByAdministrativeAreal2DId_Reference(11, AdministrativeArealType.Subdivision, 7)]), CountByAdministrativeAreal2DId_NoPolygonRead, (filterGroup, countyId, token) => Task.FromResult<long?>(null), 500, 200000);
            Assert.Equal(AdministrativeAreal2DCountStatus.NotResolved, result.Status);
        }

        /// <summary>
        /// Verifies <see cref="DiGi.GIS.WebAPI.Query.ReferencesByCountyId(IEnumerable{Building2DReference}?)"/> groups references by county in first-seen order, keeps the same reference under two counties, and skips a missing county, a blank reference and a reference already seen in its county.
        /// </summary>
        [Fact]
        public void Query_ReferencesByCountyId()
        {
            Assert.Empty(DiGi.GIS.WebAPI.Query.ReferencesByCountyId(null));

            Dictionary<int, List<string>> references_ByCountyId = DiGi.GIS.WebAPI.Query.ReferencesByCountyId(
            [
                CountByAdministrativeAreal2DId_Building2DReference("A", 7, 11),
                CountByAdministrativeAreal2DId_Building2DReference("A", 3, 21),
                CountByAdministrativeAreal2DId_Building2DReference("B", 7, 12),
                CountByAdministrativeAreal2DId_Building2DReference("A", 7, 11),
                CountByAdministrativeAreal2DId_Building2DReference("C", null, 11),
                CountByAdministrativeAreal2DId_Building2DReference(" ", 7, 11),
            ]);

            Assert.Equal([7, 3], references_ByCountyId.Keys);
            Assert.Equal(["A", "B"], references_ByCountyId[7]);
            Assert.Equal(["A"], references_ByCountyId[3]);
        }

        /// <summary>
        /// Verifies <see cref="DiGi.GIS.WebAPI.Create.ReferenceFilterGroup(FilterGroup?, IEnumerable{string}?)"/> confines a read with one <c>And</c> group holding an <c>In</c> condition on the reference column (a <c>List&lt;string&gt;</c> without blanks), nests the caller's group whole (an <c>Or</c> group is not flattened), omits a null or empty caller group, and answers null for no reference.
        /// </summary>
        [Fact]
        public void Create_ReferenceFilterGroup()
        {
            Assert.Null(DiGi.GIS.WebAPI.Create.ReferenceFilterGroup(null, null));
            Assert.Null(DiGi.GIS.WebAPI.Create.ReferenceFilterGroup(null, []));
            Assert.Null(DiGi.GIS.WebAPI.Create.ReferenceFilterGroup(null, [" ", ""]));

            FilterGroup? filterGroup_NoCaller = DiGi.GIS.WebAPI.Create.ReferenceFilterGroup(null, ["A", " ", "B"]);
            Assert.NotNull(filterGroup_NoCaller);
            Assert.Equal(FilterLogicalOperator.And, filterGroup_NoCaller.LogicalOperator);
            Assert.Empty(filterGroup_NoCaller.FilterGroups);
            FilterCondition filterCondition = Assert.Single(filterGroup_NoCaller.FilterConditions);
            Assert.Equal("reference", filterCondition.ColumnUniqueId);
            Assert.Equal(FilterOperator.In, filterCondition.FilterOperator);
            Assert.Equal(["A", "B"], Assert.IsType<List<string>>(filterCondition.Value));

            Assert.Empty(DiGi.GIS.WebAPI.Create.ReferenceFilterGroup(new FilterGroup(), ["A"])!.FilterGroups);

            FilterGroup filterGroup_Caller = new() { LogicalOperator = FilterLogicalOperator.Or, FilterConditions = [new FilterCondition() { ColumnUniqueId = "is_residential", FilterOperator = FilterOperator.Equals, Value = true }] };
            FilterGroup? filterGroup_WithCaller = DiGi.GIS.WebAPI.Create.ReferenceFilterGroup(filterGroup_Caller, ["A"]);
            Assert.NotNull(filterGroup_WithCaller);
            Assert.Same(filterGroup_Caller, Assert.Single(filterGroup_WithCaller.FilterGroups));
            Assert.Equal(FilterLogicalOperator.Or, filterGroup_Caller.LogicalOperator);
            Assert.Single(filterGroup_WithCaller.FilterConditions);
        }

        /// <summary>
        /// Pins the area-scope guards of the filtered reads (DiGi-Organization/DiGi.GIS.WebAPI#83): every read that accepts <c>AdministrativeAreal2DId</c> refuses it together with <c>CountyId</c>, and refuses a zero or negative id, with 400 before any database work; with no connection to build the area cannot be read, so a valid id answers 404 rather than an unscoped read.
        /// </summary>
        [Fact]
        public async Task BuildingDataController_AdministrativeAreal2DScope_Guards()
        {
            string path = ConfigurationFilePath();
            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));

                List<Func<int?, int?, Task<IActionResult>>> actions =
                [
                    (countyId, id) => controller.GetTableByFilterGroupAsync(new BuildingDataByFilterGroupParameter() { FilterGroup = new FilterGroup(), CountyId = countyId, AdministrativeAreal2DId = id }, 600),
                    (countyId, id) => controller.GetSinglevalueAggregateSummaryAsync(new SinglevalueAggregateRequestParameter() { ColumnUniqueId = "reference", SinglevalueAggregateFunction = SinglevalueAggregateFunction.Count, CountyId = countyId, AdministrativeAreal2DId = id }, 600),
                    (countyId, id) => controller.GetMultivalueAggregateSummaryAsync(new MultivalueAggregateRequestParameter() { ColumnUniqueId = "municipality_name", CountyId = countyId, AdministrativeAreal2DId = id }, 600),
                    (countyId, id) => controller.GetAggregateSummariesAsync(new AggregateSummariesRequestParameter() { Measures = [new AggregateMeasure("reference", SinglevalueAggregateFunction.Count)], CountyId = countyId, AdministrativeAreal2DId = id }, 600),
                    (countyId, id) => controller.GetHistogramSummaryAsync(new HistogramRequestParameter() { ColumnUniqueId = "floor_area", CountyId = countyId, AdministrativeAreal2DId = id }, 600),
                    (countyId, id) => controller.GetUniqueValuesByColumnUniqueIdParameterAsync(new UniqueValuesByColumnUniqueIdParameter() { ColumnUniqueId = "municipality_name", CountyId = countyId, AdministrativeAreal2DId = id }, 600),
                ];

                foreach (Func<int?, int?, Task<IActionResult>> action in actions)
                {
                    Assert.IsType<BadRequestObjectResult>(await action(55417, 55413));
                    Assert.IsType<BadRequestObjectResult>(await action(null, 0));
                    Assert.IsType<BadRequestObjectResult>(await action(null, -1));
                    Assert.IsType<NotFoundResult>(await action(null, 55413));
                }
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
