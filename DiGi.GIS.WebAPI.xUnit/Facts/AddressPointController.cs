using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="AddressPointController"/> answers BadRequest for a missing or blank text, a limit outside 1-100, a non-positive county id, a blank reference, missing county ids, and a non-positive county part or negative timeout of the count (a valid count request with no database answers 500).
        /// <para>Every guard runs before the database is touched, so converters without connection data suffice.</para>
        /// </summary>
        [Fact]
        public async Task AddressPointController_Validation_AnswersBadRequest()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                AddressPointController addressPointController = AddressPointController_Default(gISWebAPIConfigurationFileWatcher);

                Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync(null, null, null));
                Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync(" ", null, null));
                Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync("Marszałkowska", null, null, null));
                Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync("Marszałkowska", null, null, 0));
                Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync("Marszałkowska", null, null, 101));
                Assert.IsType<BadRequestResult>(await addressPointController.GetBuildingsByAddressAsync("Marszałkowska", "12", 0));
                Assert.IsType<BadRequestResult>(await addressPointController.GetAddressPointsByReferenceAsync(null));
                Assert.IsType<BadRequestResult>(await addressPointController.GetAddressPointsByReferenceAsync(" "));
                Assert.IsType<BadRequestResult>(await addressPointController.UpdateItemsByCountyIdsAsync(new JsonArray(), null));
                Assert.IsType<BadRequestResult>(await addressPointController.UpdateItemsByCountyIdsAsync(new JsonArray(), []));
                Assert.IsType<BadRequestResult>(await addressPointController.UpdateItemsByCountyIdsAsync(new JsonArray(), [0]));
                Assert.IsType<NoContentResult>(await addressPointController.UpdateItemsByCountyIdsAsync(new JsonArray(), [1]));

                // The count: a non-positive county part and a negative timeout are refused; a valid request with no database answers 500, never an empty count
                Assert.IsType<BadRequestResult>(await addressPointController.GetCountByCountyIdAsync(0));
                Assert.IsType<BadRequestResult>(await addressPointController.GetCountByCountyIdAsync(-5));
                Assert.IsType<BadRequestResult>(await addressPointController.GetCountByCountyIdAsync(1, -1));
                Assert.Equal(500, Assert.IsType<ObjectResult>(await addressPointController.GetCountByCountyIdAsync(null)).StatusCode);
                Assert.Equal(500, Assert.IsType<ObjectResult>(await addressPointController.GetCountByCountyIdAsync(1)).StatusCode);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// Verifies that the <see cref="AddressPointController"/> write endpoint answers Unauthorized for a missing or wrong key, and - with the correct key - while <see cref="GISWebAPIConfigurationFileWatcher.AllowUpdateAddressPoint"/> is off or absent.
        /// <para>An absent flag denies, so a deployed configuration written before the flag existed rejects every write whatever key is sent.</para>
        /// </summary>
        [Fact]
        public async Task AddressPointController_Unauthorized_AnswersUnauthorized()
        {
            string path_Allowed = System.IO.Path.GetTempFileName();
            string path_Disallowed = System.IO.Path.GetTempFileName();
            string path_Absent = System.IO.Path.GetTempFileName();

            try
            {
                System.IO.File.WriteAllLines(path_Allowed,
                [
                    $"{nameof(GISWebAPIConfigurationFileWatcher.Enabled)}=true",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.Key)}=\"authorized-token\"",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateAddressPoint)}=true",
                ]);

                System.IO.File.WriteAllLines(path_Disallowed,
                [
                    $"{nameof(GISWebAPIConfigurationFileWatcher.Enabled)}=true",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.Key)}=\"authorized-token\"",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.AllowUpdateAddressPoint)}=false",
                ]);

                System.IO.File.WriteAllLines(path_Absent,
                [
                    $"{nameof(GISWebAPIConfigurationFileWatcher.Enabled)}=true",
                    $"{nameof(GISWebAPIConfigurationFileWatcher.Key)}=\"authorized-token\"",
                ]);

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher_Allowed = new(path_Allowed);
                AddressPointController addressPointController_Allowed = AddressPointController_Default(gISWebAPIConfigurationFileWatcher_Allowed);

                Assert.IsType<UnauthorizedResult>(await addressPointController_Allowed.UpdateItemsByCountyIdsAsync(new JsonArray(), [1]));
                Assert.IsType<UnauthorizedResult>(await addressPointController_Allowed.UpdateItemsByCountyIdsAsync(new JsonArray(), [1], "wrong-token"));
                Assert.IsType<NoContentResult>(await addressPointController_Allowed.UpdateItemsByCountyIdsAsync(new JsonArray(), [1], "authorized-token"));

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher_Disallowed = new(path_Disallowed);
                Assert.IsType<UnauthorizedResult>(await AddressPointController_Default(gISWebAPIConfigurationFileWatcher_Disallowed).UpdateItemsByCountyIdsAsync(new JsonArray(), [1], "authorized-token"));

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher_Absent = new(path_Absent);
                Assert.IsType<UnauthorizedResult>(await AddressPointController_Default(gISWebAPIConfigurationFileWatcher_Absent).UpdateItemsByCountyIdsAsync(new JsonArray(), [1], "authorized-token"));
            }
            finally
            {
                System.IO.File.Delete(path_Allowed);
                System.IO.File.Delete(path_Disallowed);
                System.IO.File.Delete(path_Absent);
            }
        }

        /// <summary>
        /// Pins the query parameter names of every <see cref="AddressPointController"/> action to the names its clients send.
        /// <para>A client reaches the API over HTTP only, and ASP.NET silently ignores a query parameter it does not recognise - a rename answers 200 with an unfiltered result (<c>Coding - WebAPI Contracts.md</c> §1). <c>countyids</c> is the name <c>Modify.UpdateItemsAsync</c> sends for <see cref="AddressPointsPostTask"/>; the rest are the names the address search clients send.</para>
        /// <para>Also asserts that <c>text</c> and <c>limit</c> are nullable, so an omitted parameter is detectable instead of binding to <c>default(T)</c>.</para>
        /// </summary>
        [Fact]
        public void AddressPointController_QueryParameterNames()
        {
            Dictionary<string, string[]> names_ByMethodName = new()
            {
                [nameof(AddressPointController.UpdateItemsByCountyIdsAsync)] = ["countyids"],
                [nameof(AddressPointController.GetBuildingsByAddressAsync)] = ["text", "housenumber", "countyid", "limit"],
                [nameof(AddressPointController.GetAddressPointsByReferenceAsync)] = ["reference"],
                [nameof(AddressPointController.GetCountByCountyIdAsync)] = ["countyid", "commandtimeout"],
            };

            foreach (KeyValuePair<string, string[]> keyValuePair in names_ByMethodName)
            {
                MethodInfo? methodInfo = typeof(AddressPointController).GetMethod(keyValuePair.Key);
                Assert.NotNull(methodInfo);

                List<string> names = [];
                foreach (ParameterInfo parameterInfo in methodInfo.GetParameters())
                {
                    FromQueryAttribute? fromQueryAttribute = parameterInfo.GetCustomAttribute<FromQueryAttribute>();
                    if (fromQueryAttribute?.Name is string name)
                    {
                        names.Add(name);
                    }
                }

                Assert.Equal(keyValuePair.Value, names);
            }

            MethodInfo? methodInfo_Search = typeof(AddressPointController).GetMethod(nameof(AddressPointController.GetBuildingsByAddressAsync));
            Assert.NotNull(methodInfo_Search);

            NullabilityInfoContext nullabilityInfoContext = new();
            foreach (ParameterInfo parameterInfo in methodInfo_Search.GetParameters())
            {
                if (parameterInfo.Name == "text")
                {
                    Assert.Equal(NullabilityState.Nullable, nullabilityInfoContext.Create(parameterInfo).ReadState);
                }

                if (parameterInfo.Name == "limit")
                {
                    Assert.NotNull(Nullable.GetUnderlyingType(parameterInfo.ParameterType));
                }
            }

            // The count's county id is optional - omitting it means the whole table - so it must be nullable, not default(int)
            MethodInfo? methodInfo_Count = typeof(AddressPointController).GetMethod(nameof(AddressPointController.GetCountByCountyIdAsync));
            Assert.NotNull(methodInfo_Count);
            Assert.NotNull(Nullable.GetUnderlyingType(methodInfo_Count.GetParameters()[0].ParameterType));
        }

        private static AddressPointController AddressPointController_Default(GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher)
        {
            return new AddressPointController(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.AddressPointPostgreSQLConverter(null), new PostgreSQL.Classes.Building2DPostgreSQLConverter(null), new PostgreSQL.Classes.AdministrativeAreal2DPostgreSQLConverter(null));
        }
    }
}
