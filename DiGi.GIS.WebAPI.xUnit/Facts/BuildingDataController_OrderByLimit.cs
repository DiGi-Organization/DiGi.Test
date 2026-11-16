using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;
using SortCondition = DiGi.PostgreSQL.Table.Classes.SortCondition;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the limit-boundary contract of DiGi-Organization/DiGi.GIS.WebAPI#84 on <c>gis/buildingdata/tablebyfiltergroup</c>:
        /// a <c>Limit</c> just outside 1-1000 (0, 1001) is refused with 400 naming the bounds before any connection is
        /// opened, while the values just inside (1, 1000) pass the guard and reach the pull - refused, never clamped.
        /// No database is needed: with no connection the pull answers 404, which is what distinguishes "guard passed"
        /// from "guard refused" here.
        /// </summary>
        [Fact]
        public async Task BuildingDataController_LimitBounds_JustInsideAndJustOutside()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                BuildingDataController controller = new(
                    gISWebAPIConfigurationFileWatcher,
                    new PostgreSQL.Classes.BuildingDataPostgreSQLConverter(null),
                    new PostgreSQL.Classes.Building2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null),
                    new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null));

                foreach (int int_Limit in new int[] { 0, 1001 })
                {
                    IActionResult actionResult = await controller.GetTableByFilterGroupAsync(new BuildingDataByFilterGroupParameter { FilterGroup = new(), Limit = int_Limit });
                    ObjectResult objectResult = Assert.IsType<ObjectResult>(actionResult);
                    Assert.Equal(400, objectResult.StatusCode);
                    ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
                    Assert.False(string.IsNullOrWhiteSpace(problemDetails.Detail));
                    Assert.Contains("Limit", problemDetails.Detail);
                }

                foreach (int int_Limit in new int[] { 1, 1000 })
                {
                    IActionResult actionResult = await controller.GetTableByFilterGroupAsync(new BuildingDataByFilterGroupParameter { FilterGroup = new(), Limit = int_Limit });
                    Assert.IsType<NotFoundResult>(actionResult);
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Pins the sort-column contract of DiGi-Organization/DiGi.GIS.WebAPI#84 on <c>gis/buildingdata/tablebyfiltergroup</c>:
        /// a <c>SortCondition</c> naming a column the stored table does not hold is a client error answered 400 with a
        /// ProblemDetails body naming the column (the #54 contract extended to sort columns), and a valid top-N request
        /// returns exactly the top rows through the action end to end.
        /// <para>Scratch table on the local test database; returns without asserting when the conf is absent or the database is unreachable.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataController_OrderBy_InvalidColumn_Answers400_TopN_Reads()
        {
            InvalidFilterFixture? fixture = await InvalidFilterFixture.TryCreateAsync();
            if (fixture is null)
            {
                return;
            }

            await using (fixture)
            {
                IActionResult invalidColumn = await fixture.Controller.GetTableByFilterGroupAsync(new BuildingDataByFilterGroupParameter
                {
                    FilterGroup = new(),
                    ColumnUniqueIds = ["reference"],
                    SortConditions = [new SortCondition() { ColumnUniqueId = "no_such_sort_column", Descending = true }],
                });
                AssertInvalidFilter(invalidColumn, "no_such_sort_column");

                // The scratch rows carry floor_area 100.0, 101.0, 102.0 as SCRATCH-IF-000..002: the top one is -002.
                IActionResult topOne = await fixture.Controller.GetTableByFilterGroupAsync(new BuildingDataByFilterGroupParameter
                {
                    FilterGroup = new(),
                    ColumnUniqueIds = ["reference"],
                    SortConditions = [new SortCondition() { ColumnUniqueId = "floor_area", Descending = true }],
                    Limit = 1,
                });
                ContentResult contentResult_One = Assert.IsType<ContentResult>(topOne);
                Assert.Contains("SCRATCH-IF-002", contentResult_One.Content);
                Assert.DoesNotContain("SCRATCH-IF-000", contentResult_One.Content);
                Assert.DoesNotContain("SCRATCH-IF-001", contentResult_One.Content);

                // The top two keep the same order and drop only the lowest row.
                IActionResult topTwo = await fixture.Controller.GetTableByFilterGroupAsync(new BuildingDataByFilterGroupParameter
                {
                    FilterGroup = new(),
                    ColumnUniqueIds = ["reference"],
                    SortConditions = [new SortCondition() { ColumnUniqueId = "floor_area", Descending = true }],
                    Limit = 2,
                });
                ContentResult contentResult_Two = Assert.IsType<ContentResult>(topTwo);
                Assert.Contains("SCRATCH-IF-002", contentResult_Two.Content);
                Assert.Contains("SCRATCH-IF-001", contentResult_Two.Content);
                Assert.DoesNotContain("SCRATCH-IF-000", contentResult_Two.Content);
            }
        }
    }
}
