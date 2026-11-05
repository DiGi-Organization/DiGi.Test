using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the upstream request contract of the five area-mode GET relays (issue #84): each one reaches its frozen sibling path with exactly the hand-matched query names - <c>x_1</c>, <c>y_1</c>, <c>x_2</c>, <c>y_2</c> everywhere, plus <c>year</c> on the image relay and <c>tolerance</c> on the footprints relay - the session cookie travels on as <c>Bearer test-token</c>, and the pinned UI URI constants still name those paths.
        /// <para>The sibling contract has no compile-time link: ASP.NET ignores a misspelled query parameter and answers with the unfiltered result, so a renamed parameter must fail here (Coding - WebAPI Contracts, section 1).</para>
        /// <para>The assertions run under pl-PL, whose decimal separator is the comma: a relay formatting the coordinates with the current culture would send <c>x_1=500000%2C123456789</c> and fail, while <c>500000.123456789</c> - a value whose binary expansion repeats - proves the whole number survives invariant formatting, not just one decimal place.</para>
        /// </summary>
        [Fact]
        public async Task OrtoDataController_AreaRelays_QueryNames()
        {
            // The contract pin: these URI constants are the only link to the sibling service (Coding - WebAPI Contracts, section 1).
            Assert.Equal("/gis/ortoarea/availableyearsbyboundingbox", new Uri(Constants.Default.OrtoAreaAvailableYearsUri).AbsolutePath);
            Assert.Equal("/gis/ortoarea/imagebyboundingbox", new Uri(Constants.Default.OrtoAreaImageUri).AbsolutePath);
            Assert.Equal("/gis/ortoarea/areabyboundingbox", new Uri(Constants.Default.OrtoAreaItemsUri).AbsolutePath);
            Assert.Equal("/gis/building2D/itemsbyboundingbox", new Uri(Constants.Default.OrtoAreaBuildingsUri).AbsolutePath);
            Assert.Equal("/gis/ortoarea/labelsbyboundingbox", new Uri(Constants.Default.OrtoAreaLabelsUri).AbsolutePath);
            Assert.Equal("/gis/ortoarea/objects", new Uri(Constants.Default.OrtoAreaObjectsUri).AbsolutePath);
            Assert.Equal("/gis/ortoarea/label", new Uri(Constants.Default.OrtoAreaLabelUri).AbsolutePath);

            OrtoAreaAction[] areaActions =
            [
                OrtoAreaAction.AvailableYears,
                OrtoAreaAction.Image,
                OrtoAreaAction.Items,
                OrtoAreaAction.Buildings,
                OrtoAreaAction.Labels,
            ];

            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

                foreach (OrtoAreaAction areaAction in areaActions)
                {
                    string tail = areaAction switch
                    {
                        OrtoAreaAction.Image => $"&year={OrtoAreaYear.ToString(CultureInfo.InvariantCulture)}",
                        OrtoAreaAction.Buildings => $"&tolerance={Constants.Default.OrtoAreaTolerance.ToString(CultureInfo.InvariantCulture)}",
                        _ => string.Empty,
                    };

                    RouteStubWebApi routeStubWebApi = new RouteStubWebApi().Answer(OrtoAreaAction_Path(areaAction), HttpStatusCode.OK, "[]");
                    await InvokeOrtoAreaActionAsync(areaAction, routeStubWebApi, true, null);

                    Assert.True(routeStubWebApi.LastRequest is not null, $"[{areaAction}] the relay never reached the stub.");
                    Uri requestUri = routeStubWebApi.LastRequest!.RequestUri!;
                    Assert.Equal(OrtoAreaAction_Path(areaAction), requestUri.AbsolutePath);
                    Assert.Equal(OrtoAreaExpectedQuery(tail), requestUri.Query);

                    // The session cookie travels on as the bearer token on both relay helpers (ResponseAsync and the image's ResponseBytesAsync).
                    Assert.Equal("Bearer test-token", routeStubWebApi.LastRequest.Headers.Authorization?.ToString());
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }
        }
    }
}
