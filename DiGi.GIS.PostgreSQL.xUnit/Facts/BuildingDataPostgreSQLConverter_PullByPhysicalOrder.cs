using DiGi.Core.IO.Table.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.xUnit.Classes;
using DiGi.PostgreSQL.Classes;
using DiGi.Typology.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="BuildingDataPostgreSQLConverter.PullByPhysicalOrderAsync(NpgsqlConnection?, int, IEnumerable{string}?, string?, int, int, System.Threading.CancellationToken)"/> and <see cref="Create.TypologyAsync"/> on building data the fact pushes itself (DiGi.GIS.PostgreSQL#96).
        /// <para>The data goes into a scratch table through <see cref="ScratchBuildingDataPostgreSQLConverter"/>, with the converter's own primary key and county partitioning. It covers two county parts, rows of ~1.8 kB (so a part spans several heap blocks), and one reference filed under both parts.</para>
        /// <para>Physical-order walk of one part: every row of the part exactly once, each page carrying Reference and County Id, the last call ending with <see cref="string.Empty"/>, and <c>(null, null)</c> for a missing connection or a position that is not a tid.</para>
        /// <para>TypologyAsync over both parts: the same tree as classifying the pushed table in memory. That means the same references and the same reference set on every node, the shared reference kept once under each part (dedup is per county part, not global), and a page size smaller than a part so the walk spans several pages. Dedup keys on the primary key even when the caller identifies rows by another, non-unique column, and parts holding no rows answer null.</para>
        /// <para>Unlike the other database facts in this project this one is not skipped, because it needs no data of its own in the database - only the local test database of <c>GIS_PostgreSQL_Main.conf</c>. It returns without asserting when that conf is absent. The keyset fallback for servers older than PostgreSQL 14 cannot be reached against the local server.</para>
        /// </summary>
        [Fact]
        public async Task BuildingDataPostgreSQLConverter_PullByPhysicalOrder()
        {
            ConnectionData? connectionData = Create.GISPostgreSQLConverterManager()?.GetPostgreSQLConverter<BuildingDataPostgreSQLConverter>()?.ConnectionData;
            if (connectionData is null)
            {
                return;
            }

            ScratchBuildingDataPostgreSQLConverter scratchBuildingDataPostgreSQLConverter = new(connectionData);

            // Declared by name, as TypologyFilter() does, so that this project need not reference DiGi.GIS.IO.
            Column column_Reference = new ExtendedColumn("Reference", typeof(string), null, null);
            Column column_CountyId = new ExtendedColumn("County Id", typeof(int), null, null);
            Column column_CountyName = new ExtendedColumn("County name", typeof(string), null, null);
            Column column_IsOccupied = new ExtendedColumn("Is occupied", typeof(bool), null, null);
            Column column_Padding = new ExtendedColumn("Scratch padding", typeof(string), null, null);

            const int countyId_A = 900001;
            const int countyId_B = 900002;
            const string reference_Shared = "SCRATCH-SHARED";

            Table table = new();
            table.AddColumn(column_Reference);
            table.AddColumn(column_CountyId);
            table.AddColumn(column_CountyName);
            table.AddColumn(column_IsOccupied);
            table.AddColumn(column_Padding);

            for (int i = 0; i < 30; i++)
            {
                table.AddRow([$"SCRATCH-A-{i:D3}", countyId_A, "Scratch A", i % 3 == 0, Padding()]);
            }

            for (int i = 0; i < 12; i++)
            {
                table.AddRow([$"SCRATCH-B-{i:D3}", countyId_B, "Scratch B", i % 2 == 0, Padding()]);
            }

            table.AddRow([reference_Shared, countyId_A, "Scratch A", true, Padding()]);
            table.AddRow([reference_Shared, countyId_B, "Scratch B", false, Padding()]);

            try
            {
                Assert.True(await scratchBuildingDataPostgreSQLConverter.PushAsync(table));

                await using NpgsqlConnection? npgsqlConnection = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                Assert.NotNull(npgsqlConnection);
                await npgsqlConnection.OpenAsync();
                Assert.True(DiGi.PostgreSQL.Table.Query.IsPhysicalOrderSupported(npgsqlConnection), "The local test server must be PostgreSQL 14 or later.");

                // Physical-order walk of part A with 4-row pages.
                HashSet<string> references_A = [];
                string? position = null;
                int calls = 0;
                while (true)
                {
                    (Table? table_Page, string? position_Next) = await scratchBuildingDataPostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, countyId_A, ["county_name"], position, 4);
                    calls++;
                    Assert.NotNull(table_Page);
                    Assert.NotNull(position_Next);

                    int index_Reference = table_Page.GetColumnIndex("Reference");
                    int index_CountyId = table_Page.GetColumnIndex("County Id");
                    Assert.NotEqual(-1, index_Reference);
                    Assert.NotEqual(-1, index_CountyId);
                    Assert.NotEqual(-1, table_Page.GetColumnIndex("County name"));

                    for (int i = 0; i < table_Page.RowCount; i++)
                    {
                        Assert.Equal(countyId_A, System.Convert.ToInt32(table_Page[i, index_CountyId], CultureInfo.CurrentCulture));
                        Assert.True(references_A.Add(table_Page[i, index_Reference]!.ToString()!), "A row was read twice.");
                    }

                    if (position_Next.Length == 0)
                    {
                        break;
                    }

                    Assert.Equal(4, table_Page.RowCount);
                    position = position_Next;
                    Assert.True(calls < 100, "The walk did not end.");
                }

                Assert.Equal(31, references_A.Count);
                Assert.Contains(reference_Shared, references_A);
                Assert.Equal(8, calls);

                // The connection-opening overload answers the same first page.
                (Table? table_Own, string? position_Own) = await scratchBuildingDataPostgreSQLConverter.PullByPhysicalOrderAsync(countyId_A, ["county_name"], null, 4);
                Assert.NotNull(table_Own);
                Assert.Equal(4, table_Own.RowCount);
                Assert.False(string.IsNullOrEmpty(position_Own));

                // Declined or failed: both halves null.
                Assert.Equal((null, null), await scratchBuildingDataPostgreSQLConverter.PullByPhysicalOrderAsync(null, countyId_A, null, null, 4));
                Assert.Equal((null, null), await scratchBuildingDataPostgreSQLConverter.PullByPhysicalOrderAsync(npgsqlConnection, countyId_A, null, "not a tid", 4));

                // TypologyAsync against the same classification done in memory on the pushed table.
                ColumnTypologyFilter<Column> columnTypologyFilter = new()
                {
                    Value = column_CountyName,
                    Rule = new UniqueValueFilterRule(),
                    Filter = new ColumnTypologyFilter<Column>()
                    {
                        Value = column_IsOccupied,
                        Rule = new UniqueValueFilterRule()
                    }
                };

                Typology.Classes.Typology? typology_Expected = GIS.Create.Typology(table, columnTypologyFilter, column_Reference);
                Assert.NotNull(typology_Expected);

                Typology.Classes.Typology? typology = await scratchBuildingDataPostgreSQLConverter.TypologyAsync(columnTypologyFilter, [countyId_A, countyId_B, countyId_A], pageSize: 5);
                Assert.NotNull(typology);

                Assert.Equal(Typology.Query.ReferenceSet(typology_Expected, true), Typology.Query.ReferenceSet(typology, true));
                Assert.Equal(Typology.Query.TypologyPaths(typology_Expected, true).Count, Typology.Query.TypologyPaths(typology, true).Count);
                Assert.Equal(NodeReferenceSets(typology_Expected), NodeReferenceSets(typology));

                // The shared reference is kept under both parts: dedup is per county part, not across the read.
                List<Typology.Classes.Typology> typologies_County = typology.SubTypologies ?? [];
                Assert.Equal(2, typologies_County.Count);
                Assert.All(typologies_County, x => Assert.Contains(reference_Shared, Typology.Query.ReferenceSet(x, true)));

                // Dedup is on the primary key, never on the identifying column the caller chose: identified by the (non-unique)
                // county name, every county still reaches both occupancy nodes, exactly as the in-memory classification puts it.
                Typology.Classes.Typology? typology_ByName = await scratchBuildingDataPostgreSQLConverter.TypologyAsync(columnTypologyFilter, [countyId_A, countyId_B], column_Reference: column_CountyName, pageSize: 5);
                Assert.NotNull(typology_ByName);
                Typology.Classes.Typology? typology_ByName_Expected = GIS.Create.Typology(table, columnTypologyFilter, column_CountyName);
                Assert.NotNull(typology_ByName_Expected);
                Assert.Equal(NodeReferenceSets(typology_ByName_Expected), NodeReferenceSets(typology_ByName));

                // A part with no rows contributes nothing, and parts that hold no rows at all answer null, as documented.
                Assert.Null(await scratchBuildingDataPostgreSQLConverter.TypologyAsync(columnTypologyFilter, [900009], pageSize: 5));
                Typology.Classes.Typology? typology_WithEmpty = await scratchBuildingDataPostgreSQLConverter.TypologyAsync(columnTypologyFilter, [900009, countyId_B], pageSize: 5);
                Assert.NotNull(typology_WithEmpty);
                Assert.Equal(13, Typology.Query.ReferenceSet(typology_WithEmpty, true).Count);
            }
            finally
            {
                await DiGi.PostgreSQL.Modify.RemoveTableAsync(connectionData, scratchBuildingDataPostgreSQLConverter.TableName);

                await using NpgsqlConnection? npgsqlConnection_Cleanup = DiGi.PostgreSQL.Create.NpgsqlConnection(connectionData);
                if (npgsqlConnection_Cleanup is not null)
                {
                    await npgsqlConnection_Cleanup.OpenAsync();
                    await using NpgsqlCommand npgsqlCommand = new($"DELETE FROM \"{DiGi.PostgreSQL.Table.Constants.TableName.Columns}\" WHERE table_name = @tableName", npgsqlConnection_Cleanup);
                    npgsqlCommand.Parameters.AddWithValue("tableName", scratchBuildingDataPostgreSQLConverter.TableName);
                    await npgsqlCommand.ExecuteNonQueryAsync();
                }
            }

            // Every node of the tree as its sorted reference set, the whole list sorted: equal lists mean the same tree
            // regardless of the order rows were read in.
            static List<string> NodeReferenceSets(Typology.Classes.Typology typology_Root)
            {
                List<string> result = [];
                Collect(typology_Root);
                result.Sort(StringComparer.Ordinal);
                return result;

                void Collect(Typology.Classes.Typology typology_Node)
                {
                    result.Add(string.Join("|", Typology.Query.ReferenceSet(typology_Node, true).OrderBy(x => x, StringComparer.Ordinal)));
                    foreach (Typology.Classes.Typology typology_Sub in typology_Node.SubTypologies ?? [])
                    {
                        Collect(typology_Sub);
                    }
                }
            }

            // 1 792 hex characters: under the 2 kB threshold at which PostgreSQL compresses or moves a value out of line,
            // so a handful of rows fill a heap block and a part spans several blocks.
            static string Padding()
            {
                return string.Concat(Enumerable.Range(0, 56).Select(_ => Guid.NewGuid().ToString("N")));
            }
        }
    }
}
