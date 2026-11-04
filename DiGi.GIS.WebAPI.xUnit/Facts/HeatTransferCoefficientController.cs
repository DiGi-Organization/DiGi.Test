using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the regulation each year built answers from <see cref="HeatTransferCoefficientController.GetRegulatedHeatTransferCoefficientsByYearAsync"/> (DiGi-Organization/DiGi.Analytical#14, DiGi-Organization/DiGi.Analytical#20).
        /// <para>The year is read as 1 January, so 1998 answers the PN-91/B-02020 norm stage - the 1997 amendment (Dz.U. 1997 nr 132 poz. 878) applies from 29 April 1998 - while 1999 to 2002 answer its k(max) annex and 2003 the 2002 regulation. Only a year before the first norm stage (1955-01-01) answers NoContent; before the norm stages were added, 1998 did too.</para>
        /// </summary>
        [Fact]
        public async Task HeatTransferCoefficientController_GetRegulatedHeatTransferCoefficientsByYearAsync()
        {
            HeatTransferCoefficientController heatTransferCoefficientController = new();

            Assert.IsType<NoContentResult>(await heatTransferCoefficientController.GetRegulatedHeatTransferCoefficientsByYearAsync(1954));

            ContentResult contentResult_1998 = Assert.IsType<ContentResult>(await heatTransferCoefficientController.GetRegulatedHeatTransferCoefficientsByYearAsync(1998));
            Assert.NotNull(contentResult_1998.Content);
            Assert.Contains("RegulatedHeatTransferCoefficients_1991", contentResult_1998.Content);
            Assert.Contains("PN-91/B-02020", contentResult_1998.Content);

            short[] years_1997 = [1999, 2000, 2002];
            foreach (short year in years_1997)
            {
                ContentResult contentResult = Assert.IsType<ContentResult>(await heatTransferCoefficientController.GetRegulatedHeatTransferCoefficientsByYearAsync(year));
                Assert.Equal("application/json", contentResult.ContentType);
                Assert.NotNull(contentResult.Content);
                Assert.Contains("RegulatedHeatTransferCoefficients_1997", contentResult.Content);
                Assert.Contains("Dz.U. 1997 nr 132 poz. 878", contentResult.Content);
            }

            ContentResult contentResult_2003 = Assert.IsType<ContentResult>(await heatTransferCoefficientController.GetRegulatedHeatTransferCoefficientsByYearAsync(2003));
            Assert.NotNull(contentResult_2003.Content);
            Assert.Contains("RegulatedHeatTransferCoefficients_2002", contentResult_2003.Content);
        }
    }
}
