using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiGi.GIS.WebAPI.UI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        // The status-preserving stand-in for the Orto Data direct-mode read: it answers per request path, so the
        // reference read, the years read and the year-built read can each be configured independently, and it can
        // refuse (throw the way an unreachable service does) for a single path rather than all of them.
        private sealed class RouteStubWebApi : HttpMessageHandler
        {
            private sealed class RouteAnswer
            {
                public RouteAnswer(HttpStatusCode statusCode, string? body, bool refuse)
                {
                    StatusCode = statusCode;
                    Body = body;
                    Refuse = refuse;
                }

                public HttpStatusCode StatusCode { get; }

                public string? Body { get; }

                public bool Refuse { get; }
            }

            private readonly Dictionary<string, RouteAnswer> answers = [];

            public HttpRequestMessage? LastRequest { get; private set; }

            public RouteStubWebApi Answer(string pathFragment, HttpStatusCode statusCode, string body)
            {
                answers[pathFragment] = new RouteAnswer(statusCode, body, false);
                return this;
            }

            public RouteStubWebApi Refuse(string pathFragment)
            {
                answers[pathFragment] = new RouteAnswer(HttpStatusCode.OK, null, true);
                return this;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;

                string path = request.RequestUri?.AbsolutePath ?? string.Empty;
                foreach (KeyValuePair<string, RouteAnswer> answer in answers)
                {
                    if (path.Contains(answer.Key))
                    {
                        if (answer.Value.Refuse)
                        {
                            throw new HttpRequestException("The service is unreachable.");
                        }

                        return Task.FromResult(new HttpResponseMessage(answer.Value.StatusCode) { Content = new StringContent(answer.Value.Body ?? string.Empty) });
                    }
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) });
            }
        }

        private sealed class HandlerHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler httpMessageHandler;

            public HandlerHttpClientFactory(HttpMessageHandler httpMessageHandler)
            {
                this.httpMessageHandler = httpMessageHandler;
            }

            public HttpClient CreateClient(string name)
            {
                return new HttpClient(this.httpMessageHandler);
            }
        }

        /// <summary>
        /// Creates the Orto Data controller under test on the per-path stub, presented as a signed-in visitor - the session gate every Orto Data action reads first.
        /// <para>The shared entry point of the Orto Data facts (DiGi.GIS.WebAPI.UI#48 and #84): this one-argument overload is the unchanged contract the pre-existing <c>GetBuildingAsync</c> facts call; the session and body configuration lives in the parameterized overload below.</para>
        /// </summary>
        /// <param name="routeStubWebApi">The status-preserving stub answering the upstream requests.</param>
        /// <returns>The controller with the session cookie set on its context.</returns>
        private static OrtoDataController CreateOrtoDataController(RouteStubWebApi routeStubWebApi)
        {
            return CreateOrtoDataController(routeStubWebApi, true, null);
        }

        /// <summary>
        /// Creates the Orto Data controller under test on any stub handler, sending the user token cookie only when <paramref name="signedIn"/> is true, and giving the request the raw <paramref name="body"/> when one is provided.
        /// <para>The shared factory of the area-relay facts (DiGi.GIS.WebAPI.UI#84): it takes the stub handler configuration as parameters, so the same facts can drive <see cref="RouteStubWebApi"/> (per-path statuses and refusals) and <see cref="ScriptedWebApi"/> (where the request method, URI, bearer and body are asserted). <paramref name="signedIn"/> false builds a context with no session at all, which the actions must answer 401 on before any upstream request; <paramref name="body"/> is what the two POST relays read verbatim, and the GET actions never read it.</para>
        /// </summary>
        /// <param name="httpMessageHandler">The stub handler answering the upstream requests.</param>
        /// <param name="signedIn">True to send the user token cookie; false for the anonymous visitor.</param>
        /// <param name="body">The raw request body. This value can be null; null leaves the default empty stream.</param>
        /// <returns>The controller.</returns>
        private static OrtoDataController CreateOrtoDataController(HttpMessageHandler httpMessageHandler, bool signedIn, string? body)
        {
            OrtoDataController controller = new(new HandlerHttpClientFactory(httpMessageHandler));

            DefaultHttpContext httpContext = new();
            if (signedIn)
            {
                httpContext.Request.Headers["Cookie"] = $"{DiGi.GIS.WebAPI.UI.Constants.Default.UserTokenCookieName}=test-token";
            }

            if (body is not null)
            {
                httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            }

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            return controller;
        }

        /// <summary>
        /// The seven area-mode relay actions of <see cref="OrtoDataController"/> (issue #84), so a fact can walk every one of them through the same stub configuration instead of spelling out seven near-identical invocations.
        /// </summary>
        private enum OrtoAreaAction
        {
            AvailableYears,
            Image,
            Items,
            Buildings,
            Labels,
            Object,
            Label,
        }

        /// <summary>
        /// The X coordinate of the first bounding-box corner (query name <c>x_1</c>, EPSG:2180) every area-relay fact sends - a decimal whose binary expansion repeats, so an invariant-formatted forward can be told apart from a culture-drifted one.
        /// </summary>
        private const double OrtoAreaX1 = 500000.123456789;

        /// <summary>
        /// The Y coordinate of the first bounding-box corner (query name <c>y_1</c>, EPSG:2180) every area-relay fact sends.
        /// </summary>
        private const double OrtoAreaY1 = 150000.5;

        /// <summary>
        /// The X coordinate of the second bounding-box corner (query name <c>x_2</c>, EPSG:2180) every area-relay fact sends.
        /// </summary>
        private const double OrtoAreaX2 = 500100.25;

        /// <summary>
        /// The Y coordinate of the second bounding-box corner (query name <c>y_2</c>, EPSG:2180) every area-relay fact sends.
        /// </summary>
        private const double OrtoAreaY2 = 150200.75;

        /// <summary>
        /// The epoch year (query name <c>year</c>) the image relay is asked for.
        /// </summary>
        private const short OrtoAreaYear = 2008;

        /// <summary>
        /// The document the area POST relays are driven with: compact on purpose, so any re-serialization that re-orders, re-cases or re-formats a token changes it and the byte-identity assertion fails.
        /// </summary>
        private const string OrtoAreaPostBody = "{\"x_1\":1,\"y_1\":2,\"x_2\":3,\"y_2\":4,\"epochYear\":2008,\"state\":0}";

        /// <summary>
        /// Invokes one area-mode relay action on a controller built from the given stub handler configuration.
        /// <para>The actions are called with the shared <see cref="OrtoAreaX1"/>, <see cref="OrtoAreaY1"/>, <see cref="OrtoAreaX2"/>, <see cref="OrtoAreaY2"/> bounding box and <see cref="OrtoAreaYear"/> epoch, so every fact asserting the forwarded query asserts exactly the values this invoker sent.</para>
        /// </summary>
        /// <param name="areaAction">The action to invoke.</param>
        /// <param name="httpMessageHandler">The stub handler answering the upstream requests.</param>
        /// <param name="signedIn">True to send the user token cookie; false for the anonymous visitor the 401 contract is about.</param>
        /// <param name="body">The raw request body for the POST relays. This value can be null.</param>
        /// <param name="tolerance">The tolerance forwarded by the footprints relay, or null for the action's default.</param>
        /// <returns>The action result.</returns>
        private static Task<IActionResult> InvokeOrtoAreaActionAsync(OrtoAreaAction areaAction, HttpMessageHandler httpMessageHandler, bool signedIn, string? body, double? tolerance = null)
        {
            OrtoDataController ortoDataController = CreateOrtoDataController(httpMessageHandler, signedIn, body);

            switch (areaAction)
            {
                case OrtoAreaAction.AvailableYears:
                    return ortoDataController.GetAreaAvailableYearsAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2);

                case OrtoAreaAction.Image:
                    return ortoDataController.GetAreaImageAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2, OrtoAreaYear);

                case OrtoAreaAction.Items:
                    return ortoDataController.GetAreaItemsAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2);

                case OrtoAreaAction.Buildings:
                    return ortoDataController.GetAreaBuildingsAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2, tolerance);

                case OrtoAreaAction.Labels:
                    return ortoDataController.GetAreaLabelsAsync(OrtoAreaX1, OrtoAreaY1, OrtoAreaX2, OrtoAreaY2);

                case OrtoAreaAction.Object:
                    return ortoDataController.SetAreaObjectAsync();

                case OrtoAreaAction.Label:
                    return ortoDataController.SetAreaLabelAsync();

                default:
                    throw new ArgumentOutOfRangeException(nameof(areaAction), areaAction, "Unknown area action.");
            }
        }

        /// <summary>
        /// The frozen upstream path an area relay must reach, as a literal, so a renamed sibling route fails every stub configuration that keys on it (the contract of DiGi.GIS.WebAPI#74, which has no compile-time link).
        /// </summary>
        /// <param name="areaAction">The action whose upstream path is wanted.</param>
        /// <returns>The path the relay is expected to request.</returns>
        private static string OrtoAreaAction_Path(OrtoAreaAction areaAction)
        {
            switch (areaAction)
            {
                case OrtoAreaAction.AvailableYears:
                    return "/gis/ortoarea/availableyearsbyboundingbox";

                case OrtoAreaAction.Image:
                    return "/gis/ortoarea/imagebyboundingbox";

                case OrtoAreaAction.Items:
                    return "/gis/ortoarea/areabyboundingbox";

                case OrtoAreaAction.Buildings:
                    return "/gis/building2D/itemsbyboundingbox";

                case OrtoAreaAction.Labels:
                    return "/gis/ortoarea/labelsbyboundingbox";

                case OrtoAreaAction.Object:
                    return "/gis/ortoarea/objects";

                case OrtoAreaAction.Label:
                    return "/gis/ortoarea/label";

                default:
                    throw new ArgumentOutOfRangeException(nameof(areaAction), areaAction, "Unknown area action.");
            }
        }

        /// <summary>
        /// The exact query string the shared bounding box must produce: the four corners formatted invariantly (never the current culture), with <paramref name="tail"/> - the epoch, the tolerance - appended.
        /// </summary>
        /// <param name="tail">The parameter that follows the bounding box, including its leading &amp;, or an empty string.</param>
        /// <returns>The expected query string, including the leading ?.</returns>
        private static string OrtoAreaExpectedQuery(string tail)
        {
            CultureInfo cultureInfo = CultureInfo.InvariantCulture;
            return $"?x_1={OrtoAreaX1.ToString(cultureInfo)}&y_1={OrtoAreaY1.ToString(cultureInfo)}&x_2={OrtoAreaX2.ToString(cultureInfo)}&y_2={OrtoAreaY2.ToString(cultureInfo)}{tail}";
        }
    }
}
