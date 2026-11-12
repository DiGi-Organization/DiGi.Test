using DiGi.GIS.PostgreSQL.Classes;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the constructor carries every tally of an open building county summary through, and that a populated instance survives a JSON round trip and the copy constructor.
        /// <para>The per-source counts feed the provenance diagram of paper 01, so each one is asserted separately: a swapped pair would silently move footprints between a register and OpenStreetMap.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingCountyResult_Serialization()
        {
            OpenBuildingCountyResult openBuildingCountyResult = new(55417, 3, 120_000, 90_000, 25_000, 4_000, 500, 500, 12);

            string? json = Core.Convert.ToSystem_String(openBuildingCountyResult);
            Assert.NotNull(json);

            OpenBuildingCountyResult? openBuildingCountyResult_Json = Core.Convert.ToDiGi<OpenBuildingCountyResult>(json)?.FirstOrDefault();
            Assert.NotNull(openBuildingCountyResult_Json);

            OpenBuildingCountyResult openBuildingCountyResult_Copy = new(openBuildingCountyResult);

            foreach (OpenBuildingCountyResult openBuildingCountyResult_Temp in new[] { openBuildingCountyResult, openBuildingCountyResult_Json, openBuildingCountyResult_Copy })
            {
                Assert.Equal(55417, openBuildingCountyResult_Temp.CountyId);
                Assert.Equal(3, openBuildingCountyResult_Temp.DatasetId);
                Assert.Equal(120_000, openBuildingCountyResult_Temp.Count);
                Assert.Equal(90_000, openBuildingCountyResult_Temp.RegisterCount);
                Assert.Equal(25_000, openBuildingCountyResult_Temp.OsmCount);
                Assert.Equal(4_000, openBuildingCountyResult_Temp.MicrosoftCount);
                Assert.Equal(500, openBuildingCountyResult_Temp.EsriCount);
                Assert.Equal(500, openBuildingCountyResult_Temp.OtherCount);
                Assert.Equal(12, openBuildingCountyResult_Temp.LicenceExceptionCount);
            }

            Core.xUnit.Query.SerializationCheck(openBuildingCountyResult);
        }

        /// <summary>
        /// Verifies that a release materialised from a <see cref="JsonObject"/> keyed by property names - the path <see cref="OpenBuildingPostgreSQLConverter.GetReleasesAsync(Npgsql.NpgsqlConnection?, string?, int, System.Threading.CancellationToken)"/> takes for a register row - carries the read-back columns the primary constructor does not take.
        /// <para>The identifier, row count, attribute list and creation stamp exist only on a row read back from the database; if the JSON keys drifted from the field names they would come back null without any error.</para>
        /// </summary>
        [Fact]
        public void BuildingDatasetRelease_FromRegisterRow()
        {
            DateTime downloadDate = new(2026, 11, 3, 0, 0, 0, DateTimeKind.Unspecified);
            DateTime createdAt = new(2026, 11, 6, 10, 15, 0, DateTimeKind.Utc);

            JsonObject jsonObject = new()
            {
                [nameof(BuildingDatasetRelease.Id)] = 7,
                [nameof(BuildingDatasetRelease.DatasetKey)] = "eubucco",
                [nameof(BuildingDatasetRelease.Version)] = "v0.2",
                [nameof(BuildingDatasetRelease.DownloadDate)] = downloadDate,
                [nameof(BuildingDatasetRelease.SourceUrl)] = null,
                [nameof(BuildingDatasetRelease.Licence)] = "ODbL-1.0",
                [nameof(BuildingDatasetRelease.SourceObservationDate)] = null,
                [nameof(BuildingDatasetRelease.AttributeNames)] = JsonNode.Parse("[\"height\",\"type\"]"),
                [nameof(BuildingDatasetRelease.FileName)] = "eubucco_PL.gpkg",
                [nameof(BuildingDatasetRelease.Sha256)] = "abc",
                [nameof(BuildingDatasetRelease.RowCount)] = 14_400_000L,
                [nameof(BuildingDatasetRelease.CreatedAt)] = createdAt,
            };

            BuildingDatasetRelease buildingDatasetRelease = new(jsonObject);

            Assert.Equal(7, buildingDatasetRelease.Id);
            Assert.Equal("eubucco", buildingDatasetRelease.DatasetKey);
            Assert.Equal("v0.2", buildingDatasetRelease.Version);
            Assert.Equal(downloadDate.Date, buildingDatasetRelease.DownloadDate?.Date);
            Assert.Null(buildingDatasetRelease.SourceUrl);
            Assert.Equal("ODbL-1.0", buildingDatasetRelease.Licence);
            Assert.Equal(["height", "type"], buildingDatasetRelease.AttributeNames);
            Assert.Equal("eubucco_PL.gpkg", buildingDatasetRelease.FileName);
            Assert.Equal("abc", buildingDatasetRelease.Sha256);
            Assert.Equal(14_400_000L, buildingDatasetRelease.RowCount);
            Assert.NotNull(buildingDatasetRelease.CreatedAt);

            Core.xUnit.Query.SerializationCheck(buildingDatasetRelease);
        }
    }
}
