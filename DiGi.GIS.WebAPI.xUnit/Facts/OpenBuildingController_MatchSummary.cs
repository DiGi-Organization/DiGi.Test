using DiGi.GIS.WebAPI.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Walks the validation chain of the two match-summary routes of <see cref="OpenBuildingController"/> (DiGi.GIS.WebAPI#108): every refusal is the guard under test, and a request that passes the guards is never a 400.
        /// <para><c>matchsummary</c> refuses an unknown or missing dataset key, <c>ghs_obat</c> (it holds attributes, not footprints), a non-finite or unordered box, a side over <see cref="Constants.OrtoArea.MaximumAreaSide"/> and a negative timeout; a side of exactly the maximum and a zero-area box pass. <c>matchsummarybycountyids</c> refuses the same keys, a missing or empty county list - the converter would answer null for it, which must not read as a 404 - and more counties than <see cref="Constants.OrtoDatas.MaximumSummaryCountyCount"/>.</para>
        /// <para>The converter carries no connection data, so past the guards both routes answer 404 (the link table cannot be read); no assertion depends on a database.</para>
        /// </summary>
        [Fact]
        public async Task OpenBuildingController_MatchSummary_Validation()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OpenBuildingController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.OpenBuildingPostgreSQLConverter(null));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                // Dataset key: missing, unknown and the attribute-only table are all refused before the box is looked at.
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync(null, 0, 0, 1000, 1000));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync("nonsense", 0, 0, 1000, 1000));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync("ghs_obat", 0, 0, 1000, 1000));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesByCountyIdsAsync(null, [1]));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesByCountyIdsAsync("nonsense", [1]));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesByCountyIdsAsync("ghs_obat", [1]));

                // Box: non-finite, inverted on either axis and over the side limit by one metre.
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync("eubucco", double.NaN, 0, 1000, 1000));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync("eubucco", 0, 0, 1000, double.PositiveInfinity));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync("eubucco", 1000, 0, 0, 1000));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync("eubucco", 0, 1000, 1000, 0));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync("eubucco", 0, 0, Constants.OrtoArea.MaximumAreaSide + 1, 1000));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesAsync("eubucco", 0, 0, 1000, Constants.OrtoArea.MaximumAreaSide + 1));

                // Timeout: negative is refused, zero (no timeout) passes.
                Assert.IsType<BadRequestResult>(await controller.GetMatchSummariesAsync("eubucco", 0, 0, 1000, 1000, -1));
                Assert.IsType<BadRequestResult>(await controller.GetMatchSummariesByCountyIdsAsync("eubucco", [1], -1));

                // A side of exactly the maximum, a zero-area box and a zero timeout pass the guards; the unreadable link table then answers 404.
                Assert.IsType<NotFoundResult>(await controller.GetMatchSummariesAsync("eubucco", 0, 0, Constants.OrtoArea.MaximumAreaSide, Constants.OrtoArea.MaximumAreaSide));
                Assert.IsType<NotFoundResult>(await controller.GetMatchSummariesAsync("eubucco", 500, 500, 500, 500));
                Assert.IsType<NotFoundResult>(await controller.GetMatchSummariesAsync("eubucco", 0, 0, 1000, 1000, 0));

                // County list: missing and empty are refused (null would otherwise read as 404), the cap is inclusive.
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesByCountyIdsAsync("eubucco", null));
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesByCountyIdsAsync("eubucco", []));

                List<int> countyIds = [];
                for (int i = 1; i <= Constants.OrtoDatas.MaximumSummaryCountyCount; i++)
                {
                    countyIds.Add(i);
                }

                Assert.IsType<NotFoundResult>(await controller.GetMatchSummariesByCountyIdsAsync("eubucco", countyIds));

                countyIds.Add(Constants.OrtoDatas.MaximumSummaryCountyCount + 1);
                Assert.IsType<BadRequestObjectResult>(await controller.GetMatchSummariesByCountyIdsAsync("eubucco", countyIds));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        /// <summary>
        /// A transient database failure on either match-summary route is a "retry can succeed" class, so it answers 503 with <c>Retry-After: 30</c> rather than the uniform 500 or an empty result (DiGi.GIS.WebAPI#108).
        /// <para>Drives a real transient failure with no server: a dead loopback host makes the converter's OpenAsync throw a genuine NpgsqlException, which Npgsql classifies IsTransient = true.</para>
        /// <para>Medium test: runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public async Task OpenBuildingController_MatchSummary_TransientDatabaseFailure_Answers503()
        {
            string path = ConfigurationFilePath();

            try
            {
                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OpenBuildingController controller = new(gISWebAPIConfigurationFileWatcher, new PostgreSQL.Classes.OpenBuildingPostgreSQLConverter(new DiGi.PostgreSQL.Classes.ConnectionData("127.0.0.1", "user", "pass", "db", 1)));
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                AssertTransient503(controller, await controller.GetMatchSummariesAsync("eubucco", 0, 0, 1000, 1000));
                AssertTransient503(controller, await controller.GetMatchSummariesByCountyIdsAsync("eubucco", [1]));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
    }
}
