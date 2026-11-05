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
        /// Verifies, on the throwaway main test database, the label upsert of the full-interpretation test areas (DiGi.GIS.PostgreSQL#129): one row per (area, object identity, epoch, interpreter) behind the <c>NULLS NOT DISTINCT</c> identity index.
        /// <para>A footprint label (county part with reference, drawn-object identifier null) inserts one row; the same identity re-upserted by the same interpreter with a different state still answers one row with the state, the dates, the note and <c>updated_at</c> replaced; a second interpreter's upsert adds a parallel row; a drawn-object label (identifier set, county part and reference null) re-upserts into its own row instead of appending a duplicate, which is what the <c>NULLS NOT DISTINCT</c> index exists for. A batch of three distinct identities written with <c>batchSize: 2</c> writes three rows in two statements, and its re-run writes none new. The observation dates are stored as native <c>date</c> columns and come back as <c>DateOnly</c>. The fact writes scratch rows under a guid-based <c>xunit-orto-area-</c> key, removes them before writing and again in <c>finally</c>. Medium test (a live database server).</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task OrtoAreaLabel_Upsert()
        {
            string areaKey = $"xunit-orto-area-{Guid.NewGuid():N}";

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_OrtoAreaLabel());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.OrtoAreaLabel} WHERE area_key = '{areaKey}';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                const int countyId = 990701;
                const string reference = "XUNIT-ORTO-LABEL-REF-A";
                const string interpreter_A = "xunit-orto-label-a@example.invalid";
                const string interpreter_B = "xunit-orto-label-b@example.invalid";
                string objectId = $"xunit-orto-object-{Guid.NewGuid():N}";

                DateTimeOffset updatedAt_1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                DateTimeOffset updatedAt_2 = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
                DateTimeOffset updatedAt_3 = new(2026, 1, 3, 0, 0, 0, TimeSpan.Zero);

                OrtoAreaLabelPostgreSQLConverter ortoAreaLabelPostgreSQLConverter = new(connectionData_Main);

                // The footprint identity inserts one row, with its dates stored as native date.
                OrtoAreaLabel ortoAreaLabel_Footprint_A1 = new(areaKey, countyId, reference, null, 2023, 0, new DateOnly(2023, 5, 1), new DateOnly(2023, 9, 30), interpreter_A, "initial", updatedAt_1);
                Assert.Equal(1L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync([ortoAreaLabel_Footprint_A1]));

                List<OrtoAreaLabel>? ortoAreaLabels = await OrtoAreaLabelPostgreSQLConverter.GetOrtoAreaLabelsAsync(npgsqlConnection, areaKey);
                Assert.NotNull(ortoAreaLabels);
                OrtoAreaLabel ortoAreaLabel_Read = Assert.Single(ortoAreaLabels);
                Assert.Equal(areaKey, ortoAreaLabel_Read.AreaKey);
                Assert.Equal(countyId, ortoAreaLabel_Read.CountyId);
                Assert.Equal(reference, ortoAreaLabel_Read.Reference);
                Assert.Null(ortoAreaLabel_Read.ObjectId);
                Assert.Equal(2023, ortoAreaLabel_Read.EpochYear);
                Assert.Equal(0, ortoAreaLabel_Read.State);
                Assert.Equal(new DateOnly(2023, 5, 1), ortoAreaLabel_Read.ObservationDateMin);
                Assert.Equal(new DateOnly(2023, 9, 30), ortoAreaLabel_Read.ObservationDateMax);
                Assert.Equal(interpreter_A, ortoAreaLabel_Read.Interpreter);
                Assert.Equal("initial", ortoAreaLabel_Read.Note);
                Assert.Equal(updatedAt_1, ortoAreaLabel_Read.UpdatedAt);

                object? dataType = await ScalarAsync(npgsqlConnection, $"SELECT data_type FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{Constants.TableName.OrtoAreaLabel}' AND column_name = 'observation_date_min';");
                Assert.Equal("date", dataType);

                // The same identity under the same interpreter revises its own row: still one row, state replaced, updated_at moved.
                OrtoAreaLabel ortoAreaLabel_Footprint_A2 = new(areaKey, countyId, reference, null, 2023, 1, new DateOnly(2023, 5, 1), new DateOnly(2023, 10, 15), interpreter_A, "revised", updatedAt_2);
                Assert.Equal(1L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync([ortoAreaLabel_Footprint_A2]));

                ortoAreaLabels = await OrtoAreaLabelPostgreSQLConverter.GetOrtoAreaLabelsAsync(npgsqlConnection, areaKey);
                ortoAreaLabel_Read = Assert.Single(ortoAreaLabels!);
                Assert.Equal(1, ortoAreaLabel_Read.State);
                Assert.Equal("revised", ortoAreaLabel_Read.Note);
                Assert.Equal(new DateOnly(2023, 5, 1), ortoAreaLabel_Read.ObservationDateMin);
                Assert.Equal(new DateOnly(2023, 10, 15), ortoAreaLabel_Read.ObservationDateMax);
                Assert.Equal(updatedAt_2, ortoAreaLabel_Read.UpdatedAt);

                // A second interpreter files a parallel row of her or his own, undated.
                OrtoAreaLabel ortoAreaLabel_Footprint_B = new(areaKey, countyId, reference, null, 2023, 2, null, null, interpreter_B, null, updatedAt_2);
                Assert.Equal(1L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync([ortoAreaLabel_Footprint_B]));

                ortoAreaLabels = await OrtoAreaLabelPostgreSQLConverter.GetOrtoAreaLabelsAsync(npgsqlConnection, areaKey);
                Assert.NotNull(ortoAreaLabels);
                Assert.Equal(2, ortoAreaLabels.Count);
                Assert.Equal([interpreter_A, interpreter_B], ortoAreaLabels.Select(x => x.Interpreter));
                Assert.Equal(1, ortoAreaLabels[0].State);
                Assert.Equal(2, ortoAreaLabels[1].State);
                Assert.Null(ortoAreaLabels[1].ObservationDateMin);
                Assert.Null(ortoAreaLabels[1].ObservationDateMax);
                Assert.Null(ortoAreaLabels[1].Note);

                // The drawn identity: county part and reference are NULL for both rows, so only NULLS NOT DISTINCT can make
                // the re-upsert conflict with its own row instead of appending a duplicate.
                OrtoAreaLabel ortoAreaLabel_Drawn_1 = new(areaKey, null, null, objectId, 2023, 0, null, null, interpreter_A, null, updatedAt_1);
                Assert.Equal(1L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync([ortoAreaLabel_Drawn_1]));
                Assert.Equal(3L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoAreaLabel} WHERE area_key = '{areaKey}';"));

                OrtoAreaLabel ortoAreaLabel_Drawn_2 = new(areaKey, null, null, objectId, 2023, 1, new DateOnly(2023, 6, 1), new DateOnly(2023, 6, 30), interpreter_A, "absent", updatedAt_3);
                Assert.Equal(1L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync([ortoAreaLabel_Drawn_2]));
                Assert.Equal(3L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoAreaLabel} WHERE area_key = '{areaKey}';"));

                ortoAreaLabels = await OrtoAreaLabelPostgreSQLConverter.GetOrtoAreaLabelsAsync(npgsqlConnection, areaKey);
                Assert.NotNull(ortoAreaLabels);
                Assert.Equal(3, ortoAreaLabels.Count);
                OrtoAreaLabel? ortoAreaLabel_Drawn_Read = ortoAreaLabels.FirstOrDefault(x => x.ObjectId == objectId);
                Assert.NotNull(ortoAreaLabel_Drawn_Read);
                Assert.Equal(areaKey, ortoAreaLabel_Drawn_Read.AreaKey);
                Assert.Null(ortoAreaLabel_Drawn_Read.CountyId);
                Assert.Null(ortoAreaLabel_Drawn_Read.Reference);
                Assert.Equal(1, ortoAreaLabel_Drawn_Read.State);
                Assert.Equal(new DateOnly(2023, 6, 1), ortoAreaLabel_Drawn_Read.ObservationDateMin);
                Assert.Equal(new DateOnly(2023, 6, 30), ortoAreaLabel_Drawn_Read.ObservationDateMax);
                Assert.Equal("absent", ortoAreaLabel_Drawn_Read.Note);
                Assert.Equal(updatedAt_3, ortoAreaLabel_Drawn_Read.UpdatedAt);

                // A batch of three distinct identities written two at a time; a null entry is skipped.
                OrtoAreaLabel ortoAreaLabel_Batch_1 = new(areaKey, countyId, "XUNIT-ORTO-LABEL-REF-B", null, 2024, 0, new DateOnly(2024, 5, 1), new DateOnly(2024, 9, 30), interpreter_A, "batch", updatedAt_1);
                OrtoAreaLabel ortoAreaLabel_Batch_2 = new(areaKey, countyId, "XUNIT-ORTO-LABEL-REF-B", null, 2024, 0, null, null, interpreter_B, "batch", updatedAt_1);
                OrtoAreaLabel ortoAreaLabel_Batch_3 = new(areaKey, null, null, objectId, 2024, 2, null, null, interpreter_A, "batch", updatedAt_1);
                Assert.Equal(3L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync([ortoAreaLabel_Batch_1, null, ortoAreaLabel_Batch_2, ortoAreaLabel_Batch_3], batchSize: 2));
                Assert.Equal(6L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoAreaLabel} WHERE area_key = '{areaKey}';"));

                ortoAreaLabels = await OrtoAreaLabelPostgreSQLConverter.GetOrtoAreaLabelsAsync(npgsqlConnection, areaKey);
                Assert.NotNull(ortoAreaLabels);
                Assert.Equal(6, ortoAreaLabels.Count);
                Assert.Equal([2023, 2023, 2023, 2024, 2024, 2024], ortoAreaLabels.Select(x => (int)x.EpochYear));

                // The batch re-run revises the same six rows: still six.
                Assert.Equal(3L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync([ortoAreaLabel_Batch_1, ortoAreaLabel_Batch_2, ortoAreaLabel_Batch_3], batchSize: 2));
                Assert.Equal(6L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.OrtoAreaLabel} WHERE area_key = '{areaKey}';"));

                // Labels without an area key or without an interpreter are skipped, and null arguments answer null.
                Assert.Equal(0L, await ortoAreaLabelPostgreSQLConverter.UpdateAsync([null, new OrtoAreaLabel(null, countyId, reference, null, 2024, 0, null, null, interpreter_A, null, updatedAt_1)]));
                Assert.Null(await OrtoAreaLabelPostgreSQLConverter.UpdateAsync(npgsqlConnection, null));
                Assert.Null(await OrtoAreaLabelPostgreSQLConverter.GetOrtoAreaLabelsAsync(npgsqlConnection, null));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
