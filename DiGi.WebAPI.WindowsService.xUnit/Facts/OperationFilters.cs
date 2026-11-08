using DiGi.GIS.WebAPI.Classes;
using DiGi.WebAPI.WindowsService.Modify;
using Microsoft.OpenApi;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.WebAPI.WindowsService.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reproduces the document-level half of DiGi-Organization/DiGi.WebAPI.WindowsService#8 for the two operation filters ported with <c>AddExamplesOperationFilter</c>: every 4xx and 5xx response of the real controllers points at the <c>ProblemDetails</c> component (<see cref="ProblemDetailsResponseOperationFilter"/>), and every operation that declares a 401 carries the <c>apiKey</c> security requirement (<see cref="SecurityRequirementOperationFilter"/>).
        /// <para>Neither filter had a served document to be checked against while the host did not build. The 401 side is asserted over the whole document rather than a single route: the filters attach by response code, so a route-specific assertion would miss an operation the requirement was dropped from.</para>
        /// <para><see cref="BuildingDataController"/> is the controller whose update operations declare a 401 and stay visible to ApiExplorer - the base <c>WebAPIController</c> is <c>[ApiExplorerSettings(IgnoreApi = true)]</c>, so an update action that does not opt back in (as <c>AdministrativeAreal2DController</c>'s do not) never reaches the document.</para>
        /// </summary>
        [Fact]
        public void OperationFilters_ProblemDetailsAndSecurityRequirement()
        {
            OpenApiDocument openApiDocument = SchemaGeneratorFixture_Document([typeof(BuildingDataController)], types_OperationFilter: [typeof(SecurityRequirementOperationFilter), typeof(ProblemDetailsResponseOperationFilter)]);

            Assert.NotNull(openApiDocument.Paths);
            Assert.NotNull(openApiDocument.Components);
            Assert.NotNull(openApiDocument.Components.Schemas);
            Assert.True(openApiDocument.Components.Schemas.ContainsKey("ProblemDetails"), "The ProblemDetails component the 4xx and 5xx responses reference was not generated, so the reference dangles.");

            int referencesToProblemDetails = 0;
            int responsesWith401 = 0;
            int securityRequirements = 0;
            List<string> routesWith401WithoutRequirement = [];
            List<string> routesWithRequirementWithout401 = [];

            foreach (var pathPair in openApiDocument.Paths)
            {
                if (pathPair.Value.Operations is null)
                {
                    continue;
                }

                foreach (var operationPair in pathPair.Value.Operations)
                {
                    OpenApiOperation operation = operationPair.Value;
                    if (operation.Responses is null)
                    {
                        continue;
                    }

                    bool has401 = operation.Responses.ContainsKey("401");
                    bool hasSecurityRequirement = operation.Security is not null && operation.Security.Any(requirement => requirement.Keys.Any(key => key.Reference.Id == "apiKey"));

                    if (has401)
                    {
                        responsesWith401++;
                    }

                    if (hasSecurityRequirement)
                    {
                        securityRequirements++;
                    }

                    if (has401 && !hasSecurityRequirement)
                    {
                        routesWith401WithoutRequirement.Add(operationPair.Key + " " + pathPair.Key);
                    }

                    if (hasSecurityRequirement && !has401)
                    {
                        routesWithRequirementWithout401.Add(operationPair.Key + " " + pathPair.Key);
                    }

                    foreach (var responsePair in operation.Responses)
                    {
                        if ((responsePair.Key.StartsWith("4", StringComparison.CurrentCulture) || responsePair.Key.StartsWith("5", StringComparison.CurrentCulture))
                            && responsePair.Value.Content is not null
                            && responsePair.Value.Content.TryGetValue("application/json", out OpenApiMediaType? mediaType)
                            && mediaType.Schema is OpenApiSchemaReference openApiSchemaReference
                            && openApiSchemaReference.Reference.Id == "ProblemDetails")
                        {
                            referencesToProblemDetails++;
                        }
                    }
                }
            }

            Assert.True(referencesToProblemDetails > 0, "No 4xx or 5xx response referenced the ProblemDetails component.");
            Assert.True(responsesWith401 > 0, "No operation of the generated document declared a 401 response.");
            Assert.Empty(routesWith401WithoutRequirement);
            Assert.Empty(routesWithRequirementWithout401);
            Assert.Equal(responsesWith401, securityRequirements);
        }
    }
}
