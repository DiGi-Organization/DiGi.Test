using DiGi.GIS.WebAPI.UI.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests how an address search match opens its building (#78) on a scripted GIS Web API: the building is resolved by its reference alone and the request is redirected to <c>buildingmodel/buildingmodelbyid</c> with the identifier and county found.
        /// <para>No circle around the address point is searched, so a match farther from its footprint than the old 5 m radius still opens. A reference that does not resolve is a 404, and a missing reference or a missing or non-finite coordinate is a 400. The controller keeps exactly one public constructor.</para>
        /// </summary>
        [Fact]
        public async Task BuildingModelController_GetItemByAddressAsync()
        {
            string json_Reference = Core.Convert.ToSystem_String(new PostgreSQL.Classes.Building2DReference() { Id = 42, CountyId = 9102, Reference = "B42" })!;

            // The point is covered by no county, so the reference alone is looked up; only "B42" is known.
            ScriptedWebApi scriptedWebApi = new((httpRequestMessage, body) =>
            {
                string path = httpRequestMessage.RequestUri!.AbsolutePath;
                Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(httpRequestMessage.RequestUri.Query);

                if (path.EndsWith("/gis/building2D/building2Dreferencebyreference", StringComparison.OrdinalIgnoreCase) && query["reference"] == "B42")
                {
                    return Answer(json_Reference);
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };
            });

            BuildingModelController buildingModelController = new(new ScriptedHttpClientFactory(scriptedWebApi));

            IActionResult result = await buildingModelController.GetItemByAddressAsync("B42", 100.0, 200.0);
            RedirectResult redirectResult = Assert.IsType<RedirectResult>(result);
            Assert.Equal("~/buildingmodel/buildingmodelbyid?id=42&countyid=9102", redirectResult.Url);

            // No search around the address point: the models and footprints near it are never read.
            foreach ((HttpMethod Method, Uri? RequestUri, string? Body) request in scriptedWebApi.Requests)
            {
                Assert.DoesNotContain("itemsbycircle", request.RequestUri!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
            }

            // An unknown reference is not replaced by anything.
            Assert.IsType<NotFoundResult>(await buildingModelController.GetItemByAddressAsync("unknown", 100.0, 200.0));

            // Missing reference or point, and a point that is not finite, are the caller's fault.
            Assert.IsType<BadRequestResult>(await buildingModelController.GetItemByAddressAsync(null, 100.0, 200.0));
            Assert.IsType<BadRequestResult>(await buildingModelController.GetItemByAddressAsync(" ", 100.0, 200.0));
            Assert.IsType<BadRequestResult>(await buildingModelController.GetItemByAddressAsync("B42", null, 200.0));
            Assert.IsType<BadRequestResult>(await buildingModelController.GetItemByAddressAsync("B42", 100.0, null));
            Assert.IsType<BadRequestResult>(await buildingModelController.GetItemByAddressAsync("B42", double.NaN, 200.0));
            Assert.IsType<BadRequestResult>(await buildingModelController.GetItemByAddressAsync("B42", 100.0, double.PositiveInfinity));

            // Exactly one public constructor, or every action 500s at activation (Coding - WebAPI Contracts, section 5).
            Assert.Single(typeof(BuildingModelController).GetConstructors());
        }
    }
}
