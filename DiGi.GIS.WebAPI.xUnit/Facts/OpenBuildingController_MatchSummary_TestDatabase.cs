using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.WebAPI.Classes;
using DiGi.PostgreSQL.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The scratch county part of the match-summary round trip; no real county part of the throwaway test database uses it.
        /// </summary>
        private const int countyId_MatchSummary = 990721;

        /// <summary>
        /// Reads one box with stored links through <c>gis/openbuilding/matchsummary</c> and <c>matchsummarybycountyids</c> on the throwaway Storage test database (DiGi.GIS.WebAPI#108): the wire body reads back as <see cref="OpenBuildingMatchSummaryResult"/>, the box scope counts a component by its representative point, and the per-source rows sum to the total row where the contract says they do.
        /// <para>Five hand-built link rows of one release, written through <c>ReplaceMatchesAsync</c> into the scratch county part: two one-to-one links (one source each), an omission (no source), a commission and a one-to-one link whose representative point lies outside the box. The box holds the first four, so the total row reads 3 BDOT10k and 3 dataset footprints, 1 omission and 1 commission; the county variant also sees the fifth. Per source, <c>DatasetCount</c> and <c>CommissionCount</c> sum to the total while the BDOT10k-side figures are null; an empty box, a county holding nothing and a dataset without links all answer <c>[]</c>. Everything written is removed before and after.</para>
        /// <para>Medium test (one live database server, the Storage test database). The figures describe the scratch rows, not production.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OpenBuildingController_MatchSummary_RoundTrip()
        {
            (_, ConnectionData connectionData_Storage) = SolarRadiationTestConnectionDatas();

            string version = $"xunit-match-api-{Guid.NewGuid():N}";

            await using NpgsqlConnection? npgsqlConnection_Storage = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection_Storage);
            await npgsqlConnection_Storage.OpenAsync();

            await CleanupMatchSummaryScratchAsync(npgsqlConnection_Storage);

            string path = ConfigurationFilePath();
            try
            {
                Assert.True(await OpenBuildingPostgreSQLConverter.CreateTablesAsync(npgsqlConnection_Storage));

                int? datasetId = await OpenBuildingPostgreSQLConverter.UpsertReleaseAsync(npgsqlConnection_Storage, new BuildingDatasetRelease("eubucco", version, DateTime.Today, licence: "ODbL-1.0"));
                Assert.NotNull(datasetId);

                OpenBuildingPostgreSQLConverter openBuildingPostgreSQLConverter = new(connectionData_Storage);

                List<OpenBuildingMatch> openBuildingMatches =
                [
                    // Inside the box: a one-to-one link with a 1.0 m shift east, from the register.
                    new(countyId_MatchSummary, 1, OpenBuildingMatchClass.OneToOne, countyId_MatchSummary, "b1", countyId_MatchSummary, "e1", "register", 0.9, 1.0, 1.0, 1.0, 0.0, 100.0, 100.0, 100.0, 100.0, 99600, 99600),
                    // Inside the box: a one-to-one link from OpenStreetMap.
                    new(countyId_MatchSummary, 2, OpenBuildingMatchClass.OneToOne, countyId_MatchSummary, "b2", countyId_MatchSummary, "e2", "osm", 0.8, 1.0, 2.0, 2.0, 0.0, 100.0, 100.0, 100.0, 100.0, 99700, 99600),
                    // Inside the box: a BDOT10k footprint no dataset footprint reached - an omission belongs to no source.
                    new(countyId_MatchSummary, 3, OpenBuildingMatchClass.Omission, countyId_MatchSummary, "b3", null, null, null, null, null, null, null, null, 100.0, null, 0.0, null, 99800, 99600),
                    // Inside the box: a dataset footprint no BDOT10k footprint reached.
                    new(countyId_MatchSummary, 4, OpenBuildingMatchClass.Commission, null, null, countyId_MatchSummary, "e4", "osm", null, null, null, null, null, null, 100.0, null, 0.0, 99900, 99600),
                    // Outside the box: counted by the county variant only.
                    new(countyId_MatchSummary, 5, OpenBuildingMatchClass.OneToOne, countyId_MatchSummary, "b5", countyId_MatchSummary, "e5", "register", 0.9, 1.0, 1.0, 1.0, 0.0, 100.0, 100.0, 100.0, 100.0, 500000, 500000),
                ];

                Assert.Equal(5L, await openBuildingPostgreSQLConverter.ReplaceMatchesAsync("eubucco", datasetId.Value, [countyId_MatchSummary], openBuildingMatches, new OpenBuildingMatchParameters()));

                using GISWebAPIConfigurationFileWatcher gISWebAPIConfigurationFileWatcher = new(path);
                OpenBuildingController controller = new(gISWebAPIConfigurationFileWatcher, openBuildingPostgreSQLConverter);
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

                // The box over the scratch square: four components, read back through the wire body.
                ContentResult contentResult_Box = Assert.IsType<ContentResult>(await controller.GetMatchSummariesAsync("eubucco", 99500, 99500, 100500, 100500, 30));
                Assert.Equal("application/json", contentResult_Box.ContentType);

                List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_Box = Core.Convert.ToDiGi<OpenBuildingMatchSummaryResult>(contentResult_Box.Content);
                Assert.NotNull(openBuildingMatchSummaryResults_Box);
                Assert.Equal(3, openBuildingMatchSummaryResults_Box.Count);

                OpenBuildingMatchSummaryResult total_Box = openBuildingMatchSummaryResults_Box[0];
                Assert.Null(total_Box.PrimarySource);
                Assert.Equal(datasetId.Value, total_Box.DatasetId);
                Assert.Equal(3L, total_Box.BDOTCount);
                Assert.Equal(3L, total_Box.DatasetCount);
                Assert.Equal(1L, total_Box.OmissionCount);
                Assert.Equal(1L, total_Box.CommissionCount);
                Assert.Equal(2L, total_Box.OneToOneCount);
                Assert.Equal(2.0 / 3.0, total_Box.Completeness!.Value, 9);
                Assert.Equal(2.0 / 3.0, total_Box.Correctness!.Value, 9);
                Assert.Equal(1.5, total_Box.DxMean!.Value, 9);
                Assert.NotNull(total_Box.AreaCompleteness);
                Assert.NotNull(total_Box.SizeClasses);
                Assert.NotNull(total_Box.OpenBuildingMatchParameters);
                Assert.NotNull(total_Box.ComputedAt);

                // Per source, in ordinal order: the omission belongs to none, so the BDOT10k side is null there.
                OpenBuildingMatchSummaryResult osm_Box = openBuildingMatchSummaryResults_Box[1];
                OpenBuildingMatchSummaryResult register_Box = openBuildingMatchSummaryResults_Box[2];
                Assert.Equal("osm", osm_Box.PrimarySource);
                Assert.Equal("register", register_Box.PrimarySource);
                Assert.All(new OpenBuildingMatchSummaryResult[] { osm_Box, register_Box }, x => Assert.Null(x.BDOTCount));
                Assert.All(new OpenBuildingMatchSummaryResult[] { osm_Box, register_Box }, x => Assert.Null(x.OmissionCount));
                Assert.All(new OpenBuildingMatchSummaryResult[] { osm_Box, register_Box }, x => Assert.Null(x.Completeness));
                Assert.Equal(2L, osm_Box.DatasetCount);
                Assert.Equal(1L, osm_Box.CommissionCount);
                Assert.Equal(1L, register_Box.DatasetCount);
                Assert.Equal(0L, register_Box.CommissionCount);
                Assert.Equal(total_Box.DatasetCount, osm_Box.DatasetCount + register_Box.DatasetCount);
                Assert.Equal(total_Box.CommissionCount, osm_Box.CommissionCount + register_Box.CommissionCount);

                // The county variant is scoped by county part, not by point: it also sees the fifth component, and a repeated id is not counted twice.
                List<OpenBuildingMatchSummaryResult>? openBuildingMatchSummaryResults_County = Core.Convert.ToDiGi<OpenBuildingMatchSummaryResult>(Assert.IsType<ContentResult>(await controller.GetMatchSummariesByCountyIdsAsync("eubucco", [countyId_MatchSummary], 30)).Content);
                Assert.NotNull(openBuildingMatchSummaryResults_County);
                Assert.Equal(3, openBuildingMatchSummaryResults_County.Count);
                Assert.Equal(4L, openBuildingMatchSummaryResults_County[0].BDOTCount);
                Assert.Equal(4L, openBuildingMatchSummaryResults_County[0].DatasetCount);
                Assert.Equal(3L, openBuildingMatchSummaryResults_County[0].OneToOneCount);

                // A box holding no representative point and a county holding no link read as an empty list, never as 404 or as zero rows of figures.
                Assert.Equal("[]", Assert.IsType<ContentResult>(await controller.GetMatchSummariesAsync("eubucco", 99500, 99500, 99550, 99550, 30)).Content);
                Assert.Equal("[]", Assert.IsType<ContentResult>(await controller.GetMatchSummariesByCountyIdsAsync("eubucco", [countyId_MatchSummary + 1], 30)).Content);

                // Another dataset of the same box has no link: still an empty list.
                Assert.Equal("[]", Assert.IsType<ContentResult>(await controller.GetMatchSummariesAsync("osm", 99500, 99500, 100500, 100500, 30)).Content);
            }
            finally
            {
                System.IO.File.Delete(path);
                await CleanupMatchSummaryScratchAsync(npgsqlConnection_Storage);
            }
        }

        /// <summary>
        /// Removes the scratch link partition and the scratch register rows of the match-summary round trip from the Storage test database. Called before seeding and again in <c>finally</c>.
        /// </summary>
        /// <param name="npgsqlConnection_Storage">The open connection to the throwaway storage test database.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task CleanupMatchSummaryScratchAsync(NpgsqlConnection npgsqlConnection_Storage)
        {
            await ExecuteSolarRadiationAsync(npgsqlConnection_Storage, $"DROP TABLE IF EXISTS {PostgreSQL.Constants.TableName.BuildingDatasetMatch}_{countyId_MatchSummary};");

            await ExecuteSolarRadiationAsync(npgsqlConnection_Storage, $"DO $$ BEGIN IF to_regclass('{PostgreSQL.Constants.TableName.BuildingDataset}') IS NOT NULL THEN DELETE FROM {PostgreSQL.Constants.TableName.BuildingDataset} WHERE version LIKE 'xunit-match-api-%'; END IF; END $$;");
        }
    }
}
