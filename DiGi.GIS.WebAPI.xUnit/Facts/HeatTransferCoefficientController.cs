using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the regulation each year built answers from <see cref="HeatTransferCoefficientController.GetRegulatedHeatTransferCoefficientsByYearAsync"/> (DiGi-Organization/DiGi.Analytical#14).
        /// <para>The year is read as 1 January, so 1998 still answers NoContent - the 1997 amendment (Dz.U. 1997 nr 132 poz. 878) applies from 29 April 1998 - while 1999 to 2002 answer its k(max) annex and 2003 the 2002 regulation. Before the 1997 stage was added, every year up to 2002 answered NoContent.</para>
        /// </summary>
        [Fact]
        public async Task HeatTransferCoefficientController_GetRegulatedHeatTransferCoefficientsByYearAsync()
        {
            HeatTransferCoefficientController heatTransferCoefficientController = new();

            Assert.IsType<NoContentResult>(await heatTransferCoefficientController.GetRegulatedHeatTransferCoefficientsByYearAsync(1998));

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
