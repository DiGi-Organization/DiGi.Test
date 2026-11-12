using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
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
        /// Verifies, on the throwaway main test database, the building shape labelling storage (DiGi.GIS.PostgreSQL#135): sample insert, next unlabelled item per interpreter, label upsert and progress counts.
        /// <para>A three-item sample is written once; a second insert of the same key, and an insert mixing two keys, are refused. The next item of an interpreter is the lowest unlabelled ordinal; after a label it moves on, and with an ordinal to start after it searches forward and wraps around. The same interpreter's re-label replaces her or his row, a second interpreter files a parallel one, and a label for a reference outside the sample is refused. Progress counts are per interpreter, labels read back in ordinal then interpreter order, and an interpreter who has labelled every item gets no next item. The fact writes scratch rows under a guid-based <c>xunit-building-shape-</c> key, removes them before writing and again in <c>finally</c>.</para>
        /// <para>Medium test (0.3 s, a live database server): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumSkippableFact]
        public async Task BuildingShapePostgreSQLConverter_Labelling()
        {
            string sampleKey = $"xunit-building-shape-{Guid.NewGuid():N}";

            (ConnectionData connectionData_Main, _) = TestConnectionDatas();

            await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData_Main);
            Assert.NotNull(npgsqlConnection);
            await npgsqlConnection.OpenAsync();

            Assert.True(await npgsqlConnection.TableAsync_BuildingShapeSample());
            Assert.True(await npgsqlConnection.TableAsync_BuildingShapeLabel());

            string commandText_Cleanup = $"DELETE FROM {Constants.TableName.BuildingShapeLabel} WHERE sample_key = '{sampleKey}'; DELETE FROM {Constants.TableName.BuildingShapeSample} WHERE sample_key = '{sampleKey}';";
            await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            try
            {
                const int countyId = 990702;
                const string interpreter_A = "xunit-building-shape-a@example.invalid";
                const string interpreter_B = "xunit-building-shape-b@example.invalid";

                DateTimeOffset dateTime_1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
                DateTimeOffset dateTime_2 = new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

                BuildingShapePostgreSQLConverter buildingShapePostgreSQLConverter = new(connectionData_Main);

                List<BuildingShapeSampleItem> buildingShapeSampleItems =
                [
                    new(sampleKey, 0, countyId, "XUNIT-SHAPE-REF-0", BuildingShape.L),
                    new(sampleKey, 1, countyId, "XUNIT-SHAPE-REF-1", BuildingShape.Rectangular),
                    new(sampleKey, 2, countyId, "XUNIT-SHAPE-REF-2", BuildingShape.O),
                ];

                // Written once; a second insert of the same key and a mix of keys are refused.
                Assert.True(await buildingShapePostgreSQLConverter.InsertSampleAsync(buildingShapeSampleItems));
                Assert.False(await buildingShapePostgreSQLConverter.InsertSampleAsync(buildingShapeSampleItems));
                Assert.False(await buildingShapePostgreSQLConverter.InsertSampleAsync([new BuildingShapeSampleItem($"{sampleKey}-x", 0, countyId, "XUNIT-SHAPE-REF-X", BuildingShape.L), new BuildingShapeSampleItem($"{sampleKey}-y", 1, countyId, "XUNIT-SHAPE-REF-Y", BuildingShape.L)]));
                Assert.Equal(0L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.BuildingShapeSample} WHERE sample_key LIKE '{sampleKey}-%';"));

                Assert.Equal(3, await buildingShapePostgreSQLConverter.GetSampleItemCountAsync(sampleKey));

                List<BuildingShapeSampleItem>? buildingShapeSampleItems_Read = await buildingShapePostgreSQLConverter.GetSampleItemsAsync(sampleKey);
                Assert.NotNull(buildingShapeSampleItems_Read);
                Assert.Equal([0, 1, 2], buildingShapeSampleItems_Read.Select(x => x.Ordinal));
                Assert.Equal([BuildingShape.L, BuildingShape.Rectangular, BuildingShape.O], buildingShapeSampleItems_Read.Select(x => x.SolverShape));

                // The first unlabelled item is ordinal 0; after its label the next is ordinal 1.
                BuildingShapeSampleItem? buildingShapeSampleItem_Next = await buildingShapePostgreSQLConverter.GetNextSampleItemAsync(sampleKey, interpreter_A);
                Assert.NotNull(buildingShapeSampleItem_Next);
                Assert.Equal(0, buildingShapeSampleItem_Next.Ordinal);
                Assert.Equal(countyId, buildingShapeSampleItem_Next.CountyId);
                Assert.Equal("XUNIT-SHAPE-REF-0", buildingShapeSampleItem_Next.Reference);

                Assert.True(await buildingShapePostgreSQLConverter.UpsertLabelAsync(new BuildingShapeLabel(sampleKey, countyId, "XUNIT-SHAPE-REF-0", BuildingShape.U, interpreter_A, "first", dateTime_1)));
                Assert.Equal(1, (await buildingShapePostgreSQLConverter.GetNextSampleItemAsync(sampleKey, interpreter_A))?.Ordinal);

                // Searching after an ordinal moves forward and wraps around to the lowest unlabelled item.
                Assert.Equal(2, (await buildingShapePostgreSQLConverter.GetNextSampleItemAsync(sampleKey, interpreter_A, afterOrdinal: 1))?.Ordinal);
                Assert.Equal(1, (await buildingShapePostgreSQLConverter.GetNextSampleItemAsync(sampleKey, interpreter_A, afterOrdinal: 2))?.Ordinal);

                // The same interpreter's re-label replaces the row; a second interpreter files a parallel one.
                Assert.True(await buildingShapePostgreSQLConverter.UpsertLabelAsync(new BuildingShapeLabel(sampleKey, countyId, "XUNIT-SHAPE-REF-0", BuildingShape.L, interpreter_A, null, dateTime_2)));
                Assert.True(await buildingShapePostgreSQLConverter.UpsertLabelAsync(new BuildingShapeLabel(sampleKey, countyId, "XUNIT-SHAPE-REF-0", BuildingShape.Undefined, interpreter_B, "unclear", dateTime_1)));
                Assert.Equal(2L, await ScalarAsync(npgsqlConnection, $"SELECT count(*) FROM {Constants.TableName.BuildingShapeLabel} WHERE sample_key = '{sampleKey}';"));

                List<BuildingShapeLabel>? buildingShapeLabels = await buildingShapePostgreSQLConverter.GetLabelsAsync(sampleKey);
                Assert.NotNull(buildingShapeLabels);
                Assert.Equal(2, buildingShapeLabels.Count);
                Assert.Equal([interpreter_A, interpreter_B], buildingShapeLabels.Select(x => x.Interpreter));
                Assert.Equal(BuildingShape.L, buildingShapeLabels[0].BuildingShape);
                Assert.Null(buildingShapeLabels[0].Note);
                Assert.Equal(dateTime_2, buildingShapeLabels[0].DateTime);
                Assert.Equal(BuildingShape.Undefined, buildingShapeLabels[1].BuildingShape);
                Assert.Equal("unclear", buildingShapeLabels[1].Note);

                // A label outside the sample is refused, and invalid labels answer null.
                Assert.False(await buildingShapePostgreSQLConverter.UpsertLabelAsync(new BuildingShapeLabel(sampleKey, countyId, "XUNIT-SHAPE-REF-OUTSIDE", BuildingShape.L, interpreter_A, null, dateTime_1)));
                Assert.Null(await buildingShapePostgreSQLConverter.UpsertLabelAsync(new BuildingShapeLabel(sampleKey, countyId, "XUNIT-SHAPE-REF-1", BuildingShape.L, " ", null, dateTime_1)));
                Assert.Null(await buildingShapePostgreSQLConverter.UpsertLabelAsync(null));

                // Progress is per interpreter.
                Assert.Equal(1, await buildingShapePostgreSQLConverter.GetLabelCountAsync(sampleKey, interpreter_A));
                Assert.Equal(1, await buildingShapePostgreSQLConverter.GetLabelCountAsync(sampleKey, interpreter_B));

                // Once every item is labelled there is no next item for that interpreter, while the other still has two.
                Assert.True(await buildingShapePostgreSQLConverter.UpsertLabelAsync(new BuildingShapeLabel(sampleKey, countyId, "XUNIT-SHAPE-REF-1", BuildingShape.Rectangular, interpreter_A, null, dateTime_1)));
                Assert.True(await buildingShapePostgreSQLConverter.UpsertLabelAsync(new BuildingShapeLabel(sampleKey, countyId, "XUNIT-SHAPE-REF-2", BuildingShape.O, interpreter_A, null, dateTime_1)));
                Assert.Equal(3, await buildingShapePostgreSQLConverter.GetLabelCountAsync(sampleKey, interpreter_A));
                Assert.Null(await buildingShapePostgreSQLConverter.GetNextSampleItemAsync(sampleKey, interpreter_A));
                Assert.Equal(1, (await buildingShapePostgreSQLConverter.GetNextSampleItemAsync(sampleKey, interpreter_B))?.Ordinal);

                // An unknown sample has nothing to label, and blank arguments answer null.
                Assert.Null(await buildingShapePostgreSQLConverter.GetNextSampleItemAsync($"{sampleKey}-unknown", interpreter_A));
                Assert.Equal(0, await buildingShapePostgreSQLConverter.GetSampleItemCountAsync($"{sampleKey}-unknown"));
                Assert.Null(await buildingShapePostgreSQLConverter.GetNextSampleItemAsync(sampleKey, null));
                Assert.Null(await BuildingShapePostgreSQLConverter.InsertSampleAsync(npgsqlConnection, null));
            }
            finally
            {
                await ExecuteAsync(npgsqlConnection, commandText_Cleanup);
            }
        }
    }
}
