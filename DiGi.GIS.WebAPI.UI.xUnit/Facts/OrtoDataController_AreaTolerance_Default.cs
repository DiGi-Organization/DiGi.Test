using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the <c>tolerance</c> the footprints relay forwards (issue #84): a caller who names none gets the spelled-out upstream default <c>tolerance=0.001</c>, a named tolerance passes through unchanged, and a value that binds to null or to NaN relays that same default rather than forwarding <c>NaN</c> or refusing with a 400.
        /// <para>The NaN leg states the action's real behaviour: a query like <c>tolerance=abc</c> leaves the nullable parameter null (a value that arrives as NaN binds as NaN), and the action replaces both with <see cref="Constants.Default.OrtoAreaTolerance"/> - the upstream default, spelled out rather than left implicit.</para>
        /// <para>The whole fact runs under pl-PL, whose decimal separator is the comma, so the forwarded value also proves the tolerance is formatted invariantly.</para>
        /// </summary>
        [Fact]
        public async Task OrtoDataController_AreaTolerance_Default()
        {
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

                // No tolerance named: the upstream default, spelled out as 0.001.
                string query_NoTolerance = await OrtoAreaTolerance_ForwardedQueryAsync(null);
                Assert.Equal(OrtoAreaExpectedQuery("&tolerance=0.001"), query_NoTolerance);

                // A named tolerance passes through, invariantly.
                string query_Named = await OrtoAreaTolerance_ForwardedQueryAsync(1.5);
                Assert.Equal(OrtoAreaExpectedQuery("&tolerance=1.5"), query_Named);

                // NaN (and the null above) relay the default - never "NaN" in the query.
                string query_NaN = await OrtoAreaTolerance_ForwardedQueryAsync(double.NaN);
                Assert.Equal(OrtoAreaExpectedQuery("&tolerance=0.001"), query_NaN);
                Assert.DoesNotContain("NaN", query_NaN);
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }

        /// <summary>
        /// Asks the footprints relay for the shared bounding box with the given tolerance and returns the exact query string it forwarded upstream.
        /// </summary>
        /// <param name="tolerance">The tolerance to relay, or null for the action's default.</param>
        /// <returns>The query string of the request the stub received.</returns>
        private static async Task<string> OrtoAreaTolerance_ForwardedQueryAsync(double? tolerance)
        {
            RouteStubWebApi routeStubWebApi = new RouteStubWebApi().Answer("/gis/building2D/itemsbyboundingbox", HttpStatusCode.OK, "[]");
            IActionResult result = await InvokeOrtoAreaActionAsync(OrtoAreaAction.Buildings, routeStubWebApi, true, null, tolerance);
            Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(routeStubWebApi.LastRequest);
            return routeStubWebApi.LastRequest!.RequestUri!.Query;
        }
    }
}
