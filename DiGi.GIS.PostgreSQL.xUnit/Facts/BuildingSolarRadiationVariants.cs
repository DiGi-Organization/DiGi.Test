using DiGi.Core.IO.Table.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.IO;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the solar radiation summary records the isolated-building variant: a result stored for radius 0 writes 0 to <c>SolarRadius</c>, not null, which would read as "no result" (DiGi.GIS.PostgreSQL#136).
        /// </summary>
        [Fact]
        public void Update_SolarRadiation_RadiusZero()
        {
            GIS.Classes.BuildingSolarRadiationResult result = new("isolated-building", countyId_SolarAggregation, 42L, 0.0, 1, GIS.Enums.WeatherSourceType.EPW, "1", null, null, null, new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero), [SolarWall(180, 10, 600, 6000)]);

            Table table = new();
            Modify.Update_SolarRadiation(table, [result]);

            List<Column> columns = IO.Create.Columns_SolarRadiation();
            Assert.Equal(0f, SolarFloat(table, columns[24]));
        }

        /// <summary>
        /// Verifies the in-memory station pick of a batch solve, <see cref="Query.NearestEPWFileId"/>: the smallest planar distance wins as in the SQL nearest-neighbour lookup, a tie resolves to the lower id, and a missing list or a non-finite point answers null.
        /// </summary>
        [Fact]
        public void Query_NearestEPWFileId()
        {
            List<(int Id, double X, double Y)> ePWFileLocations = [(3, 0, 0), (5, 1000, 0), (7, 0, 1000), (9, -1000, 0)];

            Assert.Equal(3, ePWFileLocations.NearestEPWFileId(100, 100));
            Assert.Equal(5, ePWFileLocations.NearestEPWFileId(900, -50));
            Assert.Equal(7, ePWFileLocations.NearestEPWFileId(10, 990));

            // Exactly between stations 5 and 9 is a tie of 3 and nothing nearer: 3 is at 0 distance from the origin.
            Assert.Equal(3, ePWFileLocations.NearestEPWFileId(0, 0));
            Assert.Equal(5, new List<(int Id, double X, double Y)> { (9, -1000, 500), (5, 1000, 500) }.NearestEPWFileId(0, 500));

            Assert.Null(((List<(int Id, double X, double Y)>?)null).NearestEPWFileId(0, 0));
            Assert.Null(new List<(int Id, double X, double Y)>().NearestEPWFileId(0, 0));
            Assert.Null(ePWFileLocations.NearestEPWFileId(double.NaN, 0));
        }

        /// <summary>
        /// Verifies the urban-context table, <see cref="Create.Table_SolarRadiationContext"/>: irradiation is area-weighted per wall sector, all walls, the roof (|n<sub>z</sub>| ≥ 0.5) and the whole building for each radius; the reduction is <c>1 - E(r) / E(0)</c>; the descriptors and every <c>Radial …</c> column are copied; a non-residential building is left out with <c>residentialOnly</c> and kept without; a building with no result is left out.
        /// </summary>
        [Fact]
        public void Create_Table_SolarRadiationContext()
        {
            Column column_Radial = IO.Create.Column_RadialBuildingCoverageRatio(100);

            Table table_BuildingData = new();
            Column column_CountyId = table_BuildingData.UpdateColumn<Column>(IO.Constants.Column.CountyId)!;
            Column column_Reference = table_BuildingData.UpdateColumn<Column>(IO.Constants.Column.Reference)!;
            Column column_IsResidential = table_BuildingData.UpdateColumn<Column>(IO.Constants.Column.IsResidential)!;
            Column column_YearBuilt = table_BuildingData.UpdateColumn<Column>(IO.Constants.Column.CalculatedYearBuilt)!;
            Column column_Radial_Table = table_BuildingData.UpdateColumn<Column>(column_Radial)!;
            Column column_BoundingBoxX = table_BuildingData.UpdateColumn<Column>(IO.Constants.Column.BoundingBoxX)!;

            foreach ((string reference, bool isResidential) in new[] { ("house", true), ("shop", false), ("no-result", true) })
            {
                Row row = table_BuildingData.AddRow();
                IO.Modify.SetValue(row, column_CountyId, countyId_SolarAggregation);
                IO.Modify.SetValue(row, column_Reference, reference);
                IO.Modify.SetValue(row, column_IsResidential, isResidential);
                IO.Modify.SetValue(row, column_YearBuilt, 1975);
                IO.Modify.SetValue(row, column_Radial_Table, 0.25f);
                IO.Modify.SetValue(row, column_BoundingBoxX, 12.5f);
                table_BuildingData.AddRow(row, false);
            }

            // The house: a north wall (10 m²), a south wall (20 m²) and a flat roof (30 m²), isolated and at 50 m.
            List<GIS.Classes.BuildingSolarRadiationResult> results =
            [
                SolarContextResult("house", 0, (0, 10, 400), (180, 20, 600), (null, 30, 1000)),
                SolarContextResult("house", 50, (0, 10, 300), (180, 20, 600), (null, 30, 900)),
                SolarContextResult("shop", 0, (90, 10, 500)),
            ];

            Table? table = Create.Table_SolarRadiationContext(results, [0, 50], table_BuildingData, true);
            Assert.NotNull(table);
            Assert.Equal(1, table.RowCount);
            Assert.Equal("house", SolarCell(table, IO.Constants.Column.Reference));

            Assert.Equal(1975, System.Convert.ToInt32(SolarCell(table, IO.Constants.Column.CalculatedYearBuilt), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(0.25f, SolarFloat(table, column_Radial)!.Value, 6);
            Assert.False(table.TryGetColumn(IO.Constants.Column.BoundingBoxX.Name, out _));

            Assert.Equal(400f, SolarContextFloat(table, "Wall North Irradiation [r=0 m]")!.Value, 3);
            Assert.Equal(600f, SolarContextFloat(table, "Wall South Irradiation [r=0 m]")!.Value, 3);
            Assert.Null(SolarContextFloat(table, "Wall East Irradiation [r=0 m]"));
            Assert.Equal((float)(((10 * 400) + (20 * 600)) / 30.0), SolarContextFloat(table, "Wall Irradiation [r=0 m]")!.Value, 3);
            Assert.Equal(1000f, SolarContextFloat(table, "Roof Irradiation [r=0 m]")!.Value, 3);
            Assert.Equal((float)(((10 * 400) + (20 * 600) + (30 * 1000)) / 60.0), SolarContextFloat(table, "Building Irradiation [r=0 m]")!.Value, 3);
            Assert.Equal(900f, SolarContextFloat(table, "Roof Irradiation [r=50 m]")!.Value, 3);

            Assert.Equal(0.25f, SolarContextFloat(table, "Wall North Reduction [r=50 m]")!.Value, 5);
            Assert.Equal(0f, SolarContextFloat(table, "Wall South Reduction [r=50 m]")!.Value, 5);
            Assert.Equal(0.1f, SolarContextFloat(table, "Roof Reduction [r=50 m]")!.Value, 5);
            Assert.False(table.TryGetColumn("Roof Reduction [r=0 m]", out _));

            // Without the residential filter the shop is written too; the building without a result never is.
            Table? table_All = Create.Table_SolarRadiationContext(results, [0, 50], table_BuildingData, false);
            Assert.NotNull(table_All);
            Assert.Equal(2, table_All.RowCount);

            Assert.Null(Create.Table_SolarRadiationContext(results, [0, 50], new Table(), true));
        }

        /// <summary>
        /// Verifies the options of the solar radiation summary and the context export: their defaults, a string round trip, <c>SerializationCheck</c> and a member-by-member copy that holds its own collections.
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingSolarRadiationOptions_Serialization()
        {
            Assert.Null(new PostgreSQLBuildingDataSolarRadiationUpdateOptions().Radius);

            PostgreSQLBuildingDataSolarRadiationUpdateOptions postgreSQLBuildingDataSolarRadiationUpdateOptions = new() { BatchSize = 250, CommandTimeout = 120, CountyIds = [1465, 1432], Radius = 0, SkipCompleted = true };
            PostgreSQLBuildingDataSolarRadiationUpdateOptions? postgreSQLBuildingDataSolarRadiationUpdateOptions_Json = Core.Convert.ToDiGi<PostgreSQLBuildingDataSolarRadiationUpdateOptions>(Core.Convert.ToSystem_String(postgreSQLBuildingDataSolarRadiationUpdateOptions))?.FirstOrDefault();
            Assert.NotNull(postgreSQLBuildingDataSolarRadiationUpdateOptions_Json);
            Assert.Equal(0, postgreSQLBuildingDataSolarRadiationUpdateOptions_Json.Radius);

            PostgreSQLBuildingDataSolarRadiationUpdateOptions postgreSQLBuildingDataSolarRadiationUpdateOptions_Clone = new(postgreSQLBuildingDataSolarRadiationUpdateOptions);
            Assert.Equal(0, postgreSQLBuildingDataSolarRadiationUpdateOptions_Clone.Radius);
            Assert.Equal(250, postgreSQLBuildingDataSolarRadiationUpdateOptions_Clone.BatchSize);
            Assert.Equal(120, postgreSQLBuildingDataSolarRadiationUpdateOptions_Clone.CommandTimeout);
            Assert.True(postgreSQLBuildingDataSolarRadiationUpdateOptions_Clone.SkipCompleted);
            Assert.Equal(postgreSQLBuildingDataSolarRadiationUpdateOptions.CountyIds, postgreSQLBuildingDataSolarRadiationUpdateOptions_Clone.CountyIds);
            Assert.NotSame(postgreSQLBuildingDataSolarRadiationUpdateOptions.CountyIds, postgreSQLBuildingDataSolarRadiationUpdateOptions_Clone.CountyIds);
            Core.xUnit.Query.SerializationCheck(postgreSQLBuildingDataSolarRadiationUpdateOptions);

            PostgreSQLBuildingSolarRadiationContextExportOptions postgreSQLBuildingSolarRadiationContextExportOptions_Default = new();
            Assert.Equal([0, 50, 100], postgreSQLBuildingSolarRadiationContextExportOptions_Default.Radii);
            Assert.True(postgreSQLBuildingSolarRadiationContextExportOptions_Default.ResidentialOnly);
            Assert.Null(postgreSQLBuildingSolarRadiationContextExportOptions_Default.OutputDirectory);
            Assert.Null(postgreSQLBuildingSolarRadiationContextExportOptions_Default.CountyIds);

            PostgreSQLBuildingSolarRadiationContextExportOptions postgreSQLBuildingSolarRadiationContextExportOptions = new() { BatchSize = 500, CommandTimeout = 90, CountyIds = [1465], OutputDirectory = "out", Radii = [0, 50, 200], ResidentialOnly = false };
            PostgreSQLBuildingSolarRadiationContextExportOptions? postgreSQLBuildingSolarRadiationContextExportOptions_Json = Core.Convert.ToDiGi<PostgreSQLBuildingSolarRadiationContextExportOptions>(Core.Convert.ToSystem_String(postgreSQLBuildingSolarRadiationContextExportOptions))?.FirstOrDefault();
            Assert.NotNull(postgreSQLBuildingSolarRadiationContextExportOptions_Json);
            Assert.Equal([0, 50, 200], postgreSQLBuildingSolarRadiationContextExportOptions_Json.Radii);
            Assert.Equal("out", postgreSQLBuildingSolarRadiationContextExportOptions_Json.OutputDirectory);

            PostgreSQLBuildingSolarRadiationContextExportOptions postgreSQLBuildingSolarRadiationContextExportOptions_Clone = new(postgreSQLBuildingSolarRadiationContextExportOptions);
            Assert.Equal(500, postgreSQLBuildingSolarRadiationContextExportOptions_Clone.BatchSize);
            Assert.Equal(90, postgreSQLBuildingSolarRadiationContextExportOptions_Clone.CommandTimeout);
            Assert.Equal("out", postgreSQLBuildingSolarRadiationContextExportOptions_Clone.OutputDirectory);
            Assert.False(postgreSQLBuildingSolarRadiationContextExportOptions_Clone.ResidentialOnly);
            Assert.Equal(postgreSQLBuildingSolarRadiationContextExportOptions.Radii, postgreSQLBuildingSolarRadiationContextExportOptions_Clone.Radii);
            Assert.NotSame(postgreSQLBuildingSolarRadiationContextExportOptions.Radii, postgreSQLBuildingSolarRadiationContextExportOptions_Clone.Radii);
            Assert.Equal(postgreSQLBuildingSolarRadiationContextExportOptions.CountyIds, postgreSQLBuildingSolarRadiationContextExportOptions_Clone.CountyIds);
            Assert.NotSame(postgreSQLBuildingSolarRadiationContextExportOptions.CountyIds, postgreSQLBuildingSolarRadiationContextExportOptions_Clone.CountyIds);
            Core.xUnit.Query.SerializationCheck(postgreSQLBuildingSolarRadiationContextExportOptions);
        }

        /// <summary>
        /// Verifies the radius variants of <see cref="BuildingSolarRadiationPostgreSQLConverter"/> on the throwaway storage test database: the batched <c>UpdateAsync</c> writes several buildings and radii in one call and keeps the later of two results with one key; <c>GetItemsByReferencesAsync</c> with a radius reads that variant only, while without one the newest variant of each building wins; <c>GetReferencesAsync</c> narrows by radius and solver version; a repeated batch replaces its rows.
        /// <para>Medium test (a live database server): it writes the scratch partition of 990301 and drops it before and after. Reference: DiGi.GIS.PostgreSQL#136.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingSolarRadiationPostgreSQLConverter_Variants()
        {
            (_, ConnectionData connectionData_Storage) = TestConnectionDatas();

            BuildingSolarRadiationPostgreSQLConverter buildingSolarRadiationPostgreSQLConverter = new(connectionData_Storage);

            await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);
            try
            {
                DateTimeOffset dateTimeOffset = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
                List<GIS.Classes.BuildingSolarRadiationResult?> results =
                [
                    SolarVariant("XUNIT-SV-1", 990301101L, 0.0, 1, dateTimeOffset.AddMinutes(1), 1.0),
                    SolarVariant("XUNIT-SV-1", 990301101L, 50.0, 1, dateTimeOffset, 0.8),
                    SolarVariant("XUNIT-SV-2", 990301102L, 50.0, 1, dateTimeOffset, 0.7),
                    SolarVariant("XUNIT-SV-2", 990301102L, 50.0, 1, dateTimeOffset, 0.6), // the same key again: the later wins
                    SolarVariant("XUNIT-SV-3", 990301103L, 0.0, 2, dateTimeOffset, 0.9),
                    null,
                ];

                int? written = await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(countyId_SolarRadiation_A, results);
                Assert.Equal(4, written);

                List<GIS.Classes.BuildingSolarRadiationResult>? results_Isolated = await buildingSolarRadiationPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-SV-1", "XUNIT-SV-2", "XUNIT-SV-3"], countyId_SolarRadiation_A, radius: 0.0);
                Assert.NotNull(results_Isolated);
                Assert.Equal(["XUNIT-SV-1", "XUNIT-SV-3"], results_Isolated.Select(x => x.Reference).OrderBy(x => x));
                Assert.All(results_Isolated, x => Assert.Equal(0.0, x.Radius));

                List<GIS.Classes.BuildingSolarRadiationResult>? results_50 = await buildingSolarRadiationPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-SV-1", "XUNIT-SV-2"], countyId_SolarRadiation_A, radius: 50.0);
                Assert.NotNull(results_50);
                Assert.Equal(2, results_50.Count);
                Assert.All(results_50, x => Assert.Equal(50.0, x.Radius));
                Assert.Equal(0.6 * 600, results_50.Single(x => x.Reference == "XUNIT-SV-2").SurfaceSolarRadiations![0].Irradiation, 6);

                // Without a radius the variants compete: building 1's radius-0 result is the newer.
                List<GIS.Classes.BuildingSolarRadiationResult>? results_Any = await buildingSolarRadiationPostgreSQLConverter.GetItemsByReferencesAsync(["XUNIT-SV-1"], countyId_SolarRadiation_A);
                Assert.NotNull(results_Any);
                Assert.Equal(0.0, Assert.Single(results_Any).Radius);

                Assert.Equal(["XUNIT-SV-1", "XUNIT-SV-3"], (await buildingSolarRadiationPostgreSQLConverter.GetReferencesAsync(countyId_SolarRadiation_A, radius: 0.0))!.OrderBy(x => x));
                Assert.Equal(["XUNIT-SV-1"], (await buildingSolarRadiationPostgreSQLConverter.GetReferencesAsync(countyId_SolarRadiation_A, radius: 0.0, solverVersion: 1))!.OrderBy(x => x));
                Assert.Equal(3, (await buildingSolarRadiationPostgreSQLConverter.GetReferencesAsync(countyId_SolarRadiation_A))!.Count);

                // A repeated batch replaces its rows rather than adding to them.
                Assert.Equal(4, await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(countyId_SolarRadiation_A, results));
                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();
                Assert.Equal(4L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.BuildingSolarRadiation} WHERE county_id = {countyId_SolarRadiation_A};"));

                Assert.Equal(0, await buildingSolarRadiationPostgreSQLConverter.UpdateAsync(countyId_SolarRadiation_A, [null]));
            }
            finally
            {
                await CleanupBuildingSolarRadiationScratchAsync(connectionData_Storage);
            }
        }

        // A scratch result of one reference, building model, radius and solver version, with packable factors.
        private static GIS.Classes.BuildingSolarRadiationResult SolarVariant(string reference, long buildingModelId, double radius, int solverVersion, DateTimeOffset createdAt, double irradiationScale)
        {
            GIS.Classes.BuildingSolarRadiationResult buildingSolarRadiationResult = ScratchBuildingSolarRadiationResult(reference, buildingModelId, "17", null, irradiationScale, 180);
            return new GIS.Classes.BuildingSolarRadiationResult(reference, countyId_SolarRadiation_A, buildingModelId, radius, solverVersion, GIS.Enums.WeatherSourceType.EPW, "17", null, null, null, createdAt, buildingSolarRadiationResult.SurfaceSolarRadiations);
        }

        // A result of one building and radius from (azimuth, area, irradiation) surfaces; a null azimuth is a flat roof.
        private static GIS.Classes.BuildingSolarRadiationResult SolarContextResult(string reference, double radius, params (double? Azimuth, double Area, double Irradiation)[] surfaces)
        {
            List<GIS.Classes.SurfaceSolarRadiation> surfaceSolarRadiations = [];
            foreach ((double? azimuth, double area, double irradiation) in surfaces)
            {
                Vector3D normal = azimuth is null ? new Vector3D(0, 0, 1) : new Vector3D(Math.Sin(azimuth.Value * Math.PI / 180), Math.Cos(azimuth.Value * Math.PI / 180), 0);
                surfaceSolarRadiations.Add(new GIS.Classes.SurfaceSolarRadiation($"{reference}-{azimuth}", area, normal, 1, 1, irradiation, 0, 0, 0, irradiation, irradiation * area, null));
            }

            return new GIS.Classes.BuildingSolarRadiationResult(reference, countyId_SolarAggregation, 42L, radius, 1, GIS.Enums.WeatherSourceType.EPW, "1", null, null, null, new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero), surfaceSolarRadiations);
        }

        private static float? SolarContextFloat(Table table, string columnName)
        {
            return table.TryGetColumn(columnName, out Column? column) && column is not null ? SolarFloat(table, column) : null;
        }
    }
}
