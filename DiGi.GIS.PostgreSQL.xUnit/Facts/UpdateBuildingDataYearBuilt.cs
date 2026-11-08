using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.xUnit.Classes;
using DiGi.PostgreSQL.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Modify.UpdateBuildingDataYearBuiltAsync"/> on the throwaway main and storage test databases.
        /// <para>The building data goes into the scratch table of <see cref="ScratchBuildingDataPostgreSQLConverter"/>. Four buildings across two county parts: one whose stored columns describe a removed prediction run and whose history is now empty (cleared to NULL), one whose history sits under the sibling part of its building data row (written under the row's part), one with history but no building data row yet (appended under the part <c>building_2d</c> files it under), and one with neither (not written - a recompute never adds empty rows).</para>
        /// <para>A second call narrowed to one reference recomputes just that building.</para>
        /// <para>Medium test (a live database server; under 0.5 s measured alone): it writes the scratch partitions of 990201 and 990202 and the scratch building data table, and drops them before and after.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task UpdateBuildingDataYearBuilt()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            YearBuiltDataPostgreSQLConverter yearBuiltDataPostgreSQLConverter = new(connectionData_Main);
            Building2DPostgreSQLConverter building2DPostgreSQLConverter = new(connectionData_Main);
            ScratchBuildingDataPostgreSQLConverter scratchBuildingDataPostgreSQLConverter = new(connectionData_Storage);

            DateTime dateTime = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

            // Declared by name, as the other building data facts do, so that this project need not reference DiGi.GIS.IO.
            Column column_Reference = new ExtendedColumn("Reference", typeof(string), null, null);
            Column column_CountyId = new ExtendedColumn("County Id", typeof(int), null, null);
            Column column_PredictedYearBuilt = new ExtendedColumn("Predicted year built", typeof(ushort), null, null);
            Column column_UserYearBuilt = new ExtendedColumn("User year built", typeof(ushort), null, null);
            Column column_CalculatedYearBuilt = new ExtendedColumn("Calculated year built", typeof(ushort), null, null);

            await CleanupYearBuiltDataScratchAsync(connectionData_Main, connectionData_Storage);
            try
            {
                Table table = new();
                table.AddColumn(column_Reference);
                table.AddColumn(column_CountyId);
                table.AddColumn(column_PredictedYearBuilt);
                table.AddColumn(column_UserYearBuilt);
                table.AddColumn(column_CalculatedYearBuilt);
                table.AddRow(["XUNIT-BD-STALE", countyId_YearBuilt_A, (ushort)1950, null, (ushort)1950]);
                table.AddRow(["XUNIT-BD-SIBLING", countyId_YearBuilt_B, (ushort)1900, null, (ushort)1900]);
                Assert.True(await scratchBuildingDataPostgreSQLConverter.PushAsync(table));

                await SeedYearBuiltDatasAsync(yearBuiltDataPostgreSQLConverter, countyId_YearBuilt_A,
                    ScratchYearBuiltData("XUNIT-BD-STALE", null, null),
                    ScratchYearBuiltData("XUNIT-BD-SIBLING", 1961, "reviewer@example.com", new GIS.Classes.PredictedYearBuilt(dateTime, 1999)),
                    ScratchYearBuiltData("XUNIT-BD-NOTHING", null, null));

                await SeedYearBuiltDatasAsync(yearBuiltDataPostgreSQLConverter, countyId_YearBuilt_B,
                    ScratchYearBuiltData("XUNIT-BD-NEW", null, null, new GIS.Classes.PredictedYearBuilt(dateTime, 2001)));

                await using (NpgsqlConnection? npgsqlConnection_Main = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main))
                {
                    Assert.NotNull(npgsqlConnection_Main);
                    await npgsqlConnection_Main.OpenAsync();

                    Assert.True(await npgsqlConnection_Main.TableAsync_Building2D());
                    Assert.True(await npgsqlConnection_Main.TableAsync_Building2D_Partition(countyId_YearBuilt_B));
                    await ExecuteAsync(npgsqlConnection_Main, $"INSERT INTO {Constants.TableName.Building2D} (county_id, reference) VALUES ({countyId_YearBuilt_B}, 'XUNIT-BD-NEW');");
                }

                BuildingDataYearBuiltUpdateResult? buildingDataYearBuiltUpdateResult = await scratchBuildingDataPostgreSQLConverter.UpdateBuildingDataYearBuiltAsync(yearBuiltDataPostgreSQLConverter, building2DPostgreSQLConverter, [countyId_YearBuilt_A, countyId_YearBuilt_B], null);
                Assert.NotNull(buildingDataYearBuiltUpdateResult);
                Assert.Equal(4, buildingDataYearBuiltUpdateResult.Matched);
                Assert.Equal(2, buildingDataYearBuiltUpdateResult.Updated);
                Assert.Equal(1, buildingDataYearBuiltUpdateResult.Cleared);

                Dictionary<string, Tuple<int, int?, int?, int?>> rows = await ScratchBuildingDataYearBuiltAsync(connectionData_Storage, scratchBuildingDataPostgreSQLConverter.TableName);
                Assert.Equal(3, rows.Count);
                Assert.False(rows.ContainsKey("XUNIT-BD-NOTHING"));

                Assert.Equal(new Tuple<int, int?, int?, int?>(countyId_YearBuilt_A, null, null, null), rows["XUNIT-BD-STALE"]);
                Assert.Equal(new Tuple<int, int?, int?, int?>(countyId_YearBuilt_B, 1999, 1961, 1961), rows["XUNIT-BD-SIBLING"]);
                Assert.Equal(new Tuple<int, int?, int?, int?>(countyId_YearBuilt_B, 2001, null, 2001), rows["XUNIT-BD-NEW"]);

                // Narrowed to one building: the user entry is withdrawn, the recompute follows it for that building only.
                UserYearBuiltRemoveResult? userYearBuiltRemoveResult = await yearBuiltDataPostgreSQLConverter.RemoveUserYearBuiltsAsync([countyId_YearBuilt_A, countyId_YearBuilt_B], ["XUNIT-BD-SIBLING"], null, false);
                Assert.NotNull(userYearBuiltRemoveResult);
                Assert.Single(userYearBuiltRemoveResult.RemovedReferences);

                buildingDataYearBuiltUpdateResult = await scratchBuildingDataPostgreSQLConverter.UpdateBuildingDataYearBuiltAsync(yearBuiltDataPostgreSQLConverter, building2DPostgreSQLConverter, [countyId_YearBuilt_A, countyId_YearBuilt_B], ["XUNIT-BD-SIBLING"]);
                Assert.NotNull(buildingDataYearBuiltUpdateResult);
                Assert.Equal(1, buildingDataYearBuiltUpdateResult.Matched);
                Assert.Equal(1, buildingDataYearBuiltUpdateResult.Updated);

                rows = await ScratchBuildingDataYearBuiltAsync(connectionData_Storage, scratchBuildingDataPostgreSQLConverter.TableName);
                Assert.Equal(new Tuple<int, int?, int?, int?>(countyId_YearBuilt_B, 1999, null, 1999), rows["XUNIT-BD-SIBLING"]);

                Assert.Null(await scratchBuildingDataPostgreSQLConverter.UpdateBuildingDataYearBuiltAsync(yearBuiltDataPostgreSQLConverter, building2DPostgreSQLConverter, [], null));
            }
            finally
            {
                await CleanupYearBuiltDataScratchAsync(connectionData_Main, connectionData_Storage);
            }
        }

        /// <summary>
        /// Reads the three year built columns of every row of the scratch building data table.
        /// </summary>
        /// <param name="connectionData_Storage">The storage test database.</param>
        /// <param name="tableName">The name of the scratch table.</param>
        /// <returns>Reference => (county part, predicted, user, calculated).</returns>
        private static async Task<Dictionary<string, Tuple<int, int?, int?, int?>>> ScratchBuildingDataYearBuiltAsync(ConnectionData connectionData_Storage, string tableName)
        {
            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Storage);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            await using NpgsqlCommand npgsqlCommand = new($@"SELECT reference, county_id, predicted_year_built, user_year_built, calculated_year_built FROM ""{tableName}"";", npgsqlConnection);

            Dictionary<string, Tuple<int, int?, int?, int?>> result = [];

            await using NpgsqlDataReader npgsqlDataReader = await npgsqlCommand.ExecuteReaderAsync();
            while (await npgsqlDataReader.ReadAsync())
            {
                result[npgsqlDataReader.GetString(0)] = new Tuple<int, int?, int?, int?>(
                    npgsqlDataReader.GetInt32(1),
                    npgsqlDataReader.IsDBNull(2) ? null : System.Convert.ToInt32(npgsqlDataReader.GetValue(2), CultureInfo.CurrentCulture),
                    npgsqlDataReader.IsDBNull(3) ? null : System.Convert.ToInt32(npgsqlDataReader.GetValue(3), CultureInfo.CurrentCulture),
                    npgsqlDataReader.IsDBNull(4) ? null : System.Convert.ToInt32(npgsqlDataReader.GetValue(4), CultureInfo.CurrentCulture));
            }

            return result;
        }
    }
}
