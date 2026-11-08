using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.WebAPI.UI.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <c>GET /typology/childareas</c>, the list the area view offers when an area is above the solve ceiling (DiGi.GIS.WebAPI.UI#51).
        /// <para>A voivodeship lists the counties whose code starts with its own. A two-part county is one entry, with its lowest part identifier and the count summed over both parts. Entries are sorted by name under Polish collation, so <c>Łódzki</c> follows <c>Lubański</c>. The country lists its voivodeships, uncounted. A county whose count cannot be read stays listed with a null count. An unreadable area list is a 503. A missing identifier, a type without children here, or a voivodeship without a code is a 400.</para>
        /// </summary>
        [Fact]
        public async Task TypologyController_GetChildAreasAsync()
        {
            List<AdministrativeAreal2DReference> counties =
            [
                new() { Id = 78244, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 78238, Code = "2412", Name = "rybnicki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 75133, Code = "2401", Name = "Łódzki", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 75348, Code = "2402", Name = "Lubański", AdministrativeArealType = AdministrativeArealType.County },
                new() { Id = 50427, Code = "1423", Name = "outside the voivodeship", AdministrativeArealType = AdministrativeArealType.County },
            ];
            string json_Counties = Core.Convert.ToSystem_String(counties)!;

            // Part 75348 cannot be counted; every other part holds 100 buildings.
            ScriptedWebApi scriptedWebApi = new((request, body) =>
            {
                string path = request.RequestUri!.AbsolutePath;
                if (path.EndsWith("/administrativeareal2Dreferencesbyadministrativearealtype", StringComparison.CurrentCulture))
                {
                    return Answer(json_Counties);
                }

                return request.RequestUri.Query.Contains("countyid=75348") ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Answer("100");
            });

            List<TypologyChildAreaViewModel> typologyChildAreaViewModels = Children(await Controller(scriptedWebApi).GetChildAreasAsync(24, "24", AdministrativeArealType.Voivodeship));
            Assert.Equal(["Lubański", "Łódzki", "rybnicki"], typologyChildAreaViewModels.Select(x => x.Name));
            Assert.Equal(["2402", "2401", "2412"], typologyChildAreaViewModels.Select(x => x.Code));

            TypologyChildAreaViewModel typologyChildAreaViewModel_Rybnicki = typologyChildAreaViewModels[2];
            Assert.Equal(78238, typologyChildAreaViewModel_Rybnicki.Id);
            Assert.Equal(200, typologyChildAreaViewModel_Rybnicki.Count);
            Assert.Equal((int)AdministrativeArealType.County, typologyChildAreaViewModel_Rybnicki.AdministrativeArealType);
            Assert.Equal(100, typologyChildAreaViewModels[1].Count);
            Assert.Null(typologyChildAreaViewModels[0].Count);

            // The county list was asked for by the integer county type (Coding - WebAPI Contracts, section 2).
            Assert.Contains(scriptedWebApi.Requests, x => x.RequestUri!.Query.Contains("administrativearealtype=2"));

            // The country lists voivodeships, merged by code and uncounted: one request, no counts.
            List<AdministrativeAreal2DReference> voivodeships =
            [
                new() { Id = 3, Code = "24", Name = "śląskie", AdministrativeArealType = AdministrativeArealType.Voivodeship },
                new() { Id = 4, Code = "24", Name = "śląskie", AdministrativeArealType = AdministrativeArealType.Voivodeship },
                new() { Id = 1, Code = "02", Name = "dolnośląskie", AdministrativeArealType = AdministrativeArealType.Voivodeship },
            ];
            ScriptedWebApi scriptedWebApi_Country = new((request, body) => Answer(Core.Convert.ToSystem_String(voivodeships)!));
            List<TypologyChildAreaViewModel> typologyChildAreaViewModels_Country = Children(await Controller(scriptedWebApi_Country).GetChildAreasAsync(1, "10", AdministrativeArealType.Country));
            Assert.Equal(["dolnośląskie", "śląskie"], typologyChildAreaViewModels_Country.Select(x => x.Name));
            Assert.Equal(3, typologyChildAreaViewModels_Country[1].Id);
            Assert.All(typologyChildAreaViewModels_Country, x => Assert.Null(x.Count));
            Assert.Single(scriptedWebApi_Country.Requests);
            Assert.Contains("administrativearealtype=1", scriptedWebApi_Country.Requests[0].RequestUri!.Query);

            // An unreadable area list is a 503.
            IActionResult actionResult_Failed = await Controller(new ScriptedWebApi((request, body) => new HttpResponseMessage(HttpStatusCode.InternalServerError))).GetChildAreasAsync(24, "24", AdministrativeArealType.Voivodeship);
            Assert.Equal(503, Assert.IsType<StatusCodeResult>(actionResult_Failed).StatusCode);

            // Refused before anything is read.
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetChildAreasAsync(0, "24", AdministrativeArealType.Voivodeship));
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetChildAreasAsync(24, null, AdministrativeArealType.Voivodeship));
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetChildAreasAsync(78238, "2412", AdministrativeArealType.County));
            Assert.IsType<BadRequestResult>(await Controller(scriptedWebApi).GetChildAreasAsync(24, "24", null));

            static List<TypologyChildAreaViewModel> Children(IActionResult actionResult)
            {
                return Assert.IsType<List<TypologyChildAreaViewModel>>(Assert.IsType<OkObjectResult>(actionResult).Value);
            }
        }
    }
}
