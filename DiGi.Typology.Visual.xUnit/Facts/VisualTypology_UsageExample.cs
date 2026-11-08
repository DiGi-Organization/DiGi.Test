using DiGi.Core.Classes;
using DiGi.Core.IO.Table.Classes;
using DiGi.Typology.Classes;
using DiGi.Typology.Visual.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace DiGi.Typology.Visual.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Worked example of classifying building data into a Visual typology tree - a tree whose nodes carry the appearance of the bucket they came from - written to be read rather than only run.
        /// <para>It goes end to end: declare the grouping and paint its buckets, classify a table, read a node with its appearance, walk the tree, test membership, solve the same data without references, and check the structure against the plain solver. Every step is the API a caller would actually use, and the assertions state what each one is expected to answer.</para>
        /// <para>The table is built by hand so the example runs with no database. In production it is paged out of building_data the same way DiGi.GIS.PostgreSQL's Create.TypologyAsync pages it for the plain solve, and the Visual chain - the definition - arrives from a client through DiGi.GIS.WebAPI.UI; this factory is the runtime piece that solves one against the other - see the closing note.</para>
        /// <para>The rendered tree is written to user files/reports/VisualTypology_UsageExample.txt, so the shape this produces can be looked at rather than inferred from assertions.</para>
        /// </summary>
        [Fact]
        public void VisualTypology_UsageExample()
        {
            List<string> lines_Report = [];

            // ---------------------------------------------------------------------------------------------
            // 1. The source data.
            //
            //    A DiGi.Core.IO table of five buildings. Column order here deliberately does not match the
            //    order the grouping below names them in, because nothing requires it to: a column is
            //    resolved by its unique id, which is derived from its Name ("Building reference" ->
            //    "building_reference"), not by its position.
            // ---------------------------------------------------------------------------------------------

            Table table = new();

            Assert.NotNull(table.AddColumn("Building reference", typeof(string)));
            Assert.NotNull(table.AddColumn("Year built", typeof(int)));
            Assert.NotNull(table.AddColumn("Occupancy", typeof(string)));

            AddBuilding("BLD-001", 1975, "Residential");
            AddBuilding("BLD-002", 2008, "Residential");
            AddBuilding("BLD-003", 2019, "Industrial");
            AddBuilding("BLD-004", 2022, "Industrial");

            // The last building has no year built. It matters, and step 5 shows where it ends up.
            AddBuilding("BLD-005", null, "Agricultural");

            // ---------------------------------------------------------------------------------------------
            // 2. Declare the grouping, and paint its buckets.
            //
            //    A chain of levels, each naming a column and a rule for turning that column's value into a
            //    bucket. Level N+1 hangs off level N through Filter, so this reads top down: occupancy,
            //    then year band within that.
            //
            //    The chain, the rules and the solved tree are the Visual family. A Visual rule buckets
            //    exactly like its base counterpart - VisualUniqueValueFilterRule buckets by exact value,
            //    VisualIntegerRangeFilterRule buckets by range, CLOSED at both ends, declared in any
            //    order - and adds one thing: a TypologyAppearanceCollection, one appearance per bucket.
            //    Filing an entry is the painting, and for a unique value rule the declaration too: a
            //    value resolves a bucket only when an appearance is filed for it, exactly as a value
            //    outside every declared range resolves nothing.
            //
            //        rule.TypologyAppearanceCollection["Residential"] = appearance;
            //        rule.TypologyAppearanceCollection[range] = appearance;
            //
            //    A range bucket with no entry still solves, with a null appearance - no member of the
            //    level carries a fallback colour. Below, the middle year band is left unpainted on
            //    purpose so the walk in step 5 shows that outcome; every occupancy value is painted,
            //    because an unpainted occupancy value would simply not be classified.
            //
            //    The keys are the bucket's value or range rendered through DiGi.Typology.Visual.Query.Key,
            //    invariantly, so a definition that survived a JSON round trip still paints the same
            //    buckets it was declared with.
            //
            //    Note the chain type. VisualColumnTypologyFilter is a SIBLING of ColumnTypologyFilter,
            //    not a subtype - both bind the same base with a different self-type, so a Visual chain is
            //    not accepted by DiGi.GIS Create.Typology, and this factory is not a drop-in overload of
            //    it: it is the same solve over the Visual family, appearances included. (The same is why
            //    Query.ColumnUniqueIds, which is typed to the plain family, cannot be asked for a Visual
            //    chain's columns; a Visual level resolves its column by unique id exactly as the plain
            //    one does - "Occupancy" -> "occupancy" - which is what makes the Index-less columns
            //    below resolvable.)
            // ---------------------------------------------------------------------------------------------

            TypologyAppearance typologyAppearance_Residential = Create.TypologyAppearance(System.Drawing.Color.Red);
            TypologyAppearance typologyAppearance_Industrial = Create.TypologyAppearance(System.Drawing.Color.Purple);
            TypologyAppearance typologyAppearance_Agricultural = Create.TypologyAppearance(System.Drawing.Color.Yellow);
            TypologyAppearance typologyAppearance_Old = Create.TypologyAppearance(System.Drawing.Color.Brown);
            TypologyAppearance typologyAppearance_New = Create.TypologyAppearance(System.Drawing.Color.Green);

            Range<int> range_Old = new(0, 1989);
            Range<int> range_Middle = new(1990, 2010);
            Range<int> range_New = new(2011, int.MaxValue);

            VisualUniqueValueFilterRule visualUniqueValueFilterRule = new();

            visualUniqueValueFilterRule.TypologyAppearanceCollection["Residential"] = typologyAppearance_Residential;
            visualUniqueValueFilterRule.TypologyAppearanceCollection["Industrial"] = typologyAppearance_Industrial;
            visualUniqueValueFilterRule.TypologyAppearanceCollection["Agricultural"] = typologyAppearance_Agricultural;

            VisualIntegerRangeFilterRule visualIntegerRangeFilterRule = new([range_New, range_Middle, range_Old]);

            visualIntegerRangeFilterRule.TypologyAppearanceCollection[range_Old] = typologyAppearance_Old;
            visualIntegerRangeFilterRule.TypologyAppearanceCollection[range_New] = typologyAppearance_New;

            VisualColumnTypologyFilter<Column> visualColumnTypologyFilter = new()
            {
                Value = new Column(-1, "Occupancy", typeof(string)),
                Rule = visualUniqueValueFilterRule,
                Filter = new VisualColumnTypologyFilter<Column>()
                {
                    Value = new Column(-1, "Year built", typeof(int)),
                    Rule = visualIntegerRangeFilterRule
                }
            };

            // ---------------------------------------------------------------------------------------------
            // 3. Classify.
            //
            //    The third argument names the column that identifies a row - "Building reference" here,
            //    resolved against the table like every other column. Without it a node could not say
            //    which buildings it holds, so it is required whenever references are being stored. The
            //    fourth names the root node; left null the root is unnamed.
            // ---------------------------------------------------------------------------------------------

            VisualTypology? visualTypology = Typology.Visual.Create.VisualTypology(table, visualColumnTypologyFilter, new Column("Building reference", typeof(string)));

            Assert.NotNull(visualTypology);

            // ---------------------------------------------------------------------------------------------
            // 4. Read one node, with its appearance.
            //
            //    A path is a sequence of indexes from the root, so [0] is the first occupancy bucket. A
            //    node is named "{column} {bucket}" as in the plain solver; what the Visual item adds is
            //    the Appearance, read through the node's TypologyItem.
            //
            //    The solved node holds a VALUE copy of the rule's entry, not the entry itself: painting
            //    or repainting the rule after the solve does not repaint a tree already solved. Two
            //    appearances are compared through their serialized form, which is canonical - the
            //    appearances have no value equality of their own.
            //
            //    References accumulate upwards: a node holds the references of everything below it, so
            //    the Residential node lists both of its buildings. The root itself holds none - it is
            //    the tree, not a bucket - which is why the whole set is read with includeNested.
            // ---------------------------------------------------------------------------------------------

            VisualTypology? visualTypology_Residential = Typology.Query.SubTypology(visualTypology, [0]);

            Assert.NotNull(visualTypology_Residential);
            Assert.Equal("Occupancy Residential", visualTypology_Residential.Name);
            Assert.Equal(2, visualTypology_Residential.References.Count);

            Assert.Equal(Core.Convert.ToSystem_String(typologyAppearance_Residential), Core.Convert.ToSystem_String(visualTypology_Residential.TypologyItem?.Appearance));

            Assert.Empty(visualTypology.References);
            Assert.Equal(5, visualTypology.ReferenceSet(true).Count);

            // ---------------------------------------------------------------------------------------------
            // 5. Walk the whole tree.
            //
            //    Note where BLD-005 appears. It has no year built, a range rule resolves nothing for a
            //    missing value, and the solver then drops the row from that level and everything under it
            //    - there is no catch-all bucket. So BLD-005 is listed on its occupancy node and on no
            //    year node at all. A row is not lost, it simply stops being classified further down.
            //
            //    Note also which nodes carry an appearance: the middle band carries none, because
            //    nothing was filed for its bucket - that is the unpainted-bucket outcome from step 2.
            //    Every occupancy node carries one, because a unique value level holds exactly the
            //    values it declares; a value with no filed appearance would not be classified at all.
            // ---------------------------------------------------------------------------------------------

            List<TypologyPath> typologyPaths = Typology.Query.TypologyPaths(visualTypology, true);

            Assert.Equal(6, typologyPaths.Count);

            lines_Report.Add("Tree (path, name, buildings held by that node, appearance):");

            foreach (TypologyPath typologyPath in typologyPaths)
            {
                VisualTypology? visualTypology_Node = Typology.Query.SubTypology(visualTypology, typologyPath);
                Assert.NotNull(visualTypology_Node);

                List<string> references_Node = visualTypology_Node.References;
                references_Node.Sort();

                string appearance_Node = visualTypology_Node.TypologyItem?.Appearance is null ? "no appearance" : "appearance";

                lines_Report.Add(string.Format(CultureInfo.CurrentCulture, "  [{0}]{1}{2} -> {3} ({4})", typologyPath, new string(' ', typologyPath.Count * 2), visualTypology_Node.Name, string.Join(", ", references_Node), appearance_Node));
            }

            lines_Report.Add(string.Empty);

            // BLD-005 reaches its occupancy node and stops there: Agricultural holds it, and has no level below it at all.
            VisualTypology? visualTypology_Agricultural = Typology.Query.SubTypology(visualTypology, [2]);

            Assert.NotNull(visualTypology_Agricultural);
            Assert.Contains("BLD-005", visualTypology_Agricultural.References);
            Assert.Equal(Core.Convert.ToSystem_String(typologyAppearance_Agricultural), Core.Convert.ToSystem_String(visualTypology_Agricultural.TypologyItem?.Appearance));

            Assert.NotNull(visualTypology_Agricultural.SubTypologies);
            Assert.Empty(visualTypology_Agricultural.SubTypologies!);

            // ---------------------------------------------------------------------------------------------
            // 6. Ask whether a building is in a branch.
            //
            //    Contains without includeNested asks about that node's own set; with it, the whole subtree.
            //    The root holds nothing itself, so the two answers differ there.
            // ---------------------------------------------------------------------------------------------

            Assert.True(visualTypology_Residential.Contains("BLD-001"));
            Assert.False(visualTypology_Residential.Contains("BLD-003"));

            Assert.False(visualTypology.Contains("BLD-005"));
            Assert.True(visualTypology.Contains("BLD-005", true));

            // ---------------------------------------------------------------------------------------------
            // 7. The same classification without references.
            //
            //    Every node from a matched row up to the root stores that reference, so a two level chain
            //    stores each one twice. Where the node to building association is kept outside the tree,
            //    solve with includeReferences false and no reference column: identical structure, node
            //    names and appearances, nothing stored. An already-solved tree can be stripped the same
            //    way with Modify.RemoveReferences, which keeps every appearance - the appearance is node
            //    metadata, not link data.
            // ---------------------------------------------------------------------------------------------

            VisualTypology? visualTypology_Metadata = Typology.Visual.Create.VisualTypology(table, visualColumnTypologyFilter, null, null, false);

            Assert.NotNull(visualTypology_Metadata);
            Assert.Empty(visualTypology_Metadata.ReferenceSet(true));
            Assert.Equal(typologyPaths.Count, visualTypology_Metadata.TypologyPaths(true).Count);

            VisualTypology? visualTypology_Residential_Metadata = Typology.Query.SubTypology(visualTypology_Metadata, [0]);

            Assert.NotNull(visualTypology_Residential_Metadata);
            Assert.Equal(Core.Convert.ToSystem_String(typologyAppearance_Residential), Core.Convert.ToSystem_String(visualTypology_Residential_Metadata.TypologyItem?.Appearance));

            lines_Report.Add("Solved again with includeReferences false: same " + typologyPaths.Count + " nodes, 0 references stored, appearances intact.");
            lines_Report.Add(string.Empty);

            // ---------------------------------------------------------------------------------------------
            // 8. The same table through the plain solver.
            //
            //    For the same table and an equivalent plain chain - the same levels and buckets through
            //    base rules carrying no appearance - DiGi.GIS Create.Typology answers the same structure:
            //    the Visual tree is the plain tree plus appearances. VisualTypology_StructureParity walks
            //    every node of both; here the count and the first node are checked, as a caller of the
            //    two side by side would see them agree.
            // ---------------------------------------------------------------------------------------------

            ColumnTypologyFilter<Column> columnTypologyFilter = new()
            {
                Value = new Column(-1, "Occupancy", typeof(string)),
                Rule = new UniqueValueFilterRule(),
                Filter = new ColumnTypologyFilter<Column>()
                {
                    Value = new Column(-1, "Year built", typeof(int)),
                    Rule = new IntegerRangeFilterRule([range_New, range_Middle, range_Old])
                }
            };

            Typology.Classes.Typology? typology = GIS.Create.Typology(table, columnTypologyFilter, new Column("Building reference", typeof(string)));

            Assert.NotNull(typology);
            Assert.Equal(typologyPaths.Count, Typology.Query.TypologyPaths(typology, true).Count);

            Typology.Classes.Typology? typology_Residential = Typology.Query.SubTypology(typology, [0]);

            Assert.NotNull(typology_Residential);
            Assert.Equal(typology_Residential.Name, visualTypology_Residential.Name);

            // ---------------------------------------------------------------------------------------------
            // 9. In production.
            //
            //    The table above is built by hand; in production it is paged out of building_data - the
            //    same paging DiGi.GIS.PostgreSQL's Create.TypologyAsync does for the plain solve, one
            //    connection, one partition at a time, a reference as the cursor:
            //
            //        Table? table_Page = await buildingDataPostgreSQLConverter.PullAsync(npgsqlConnection, countyId, columnUniqueIds, lastReference, pageSize, commandTimeout, cancellationToken);
            //
            //    The Visual chain - the painted definition - is authored on a client and reaches the
            //    server through DiGi.GIS.WebAPI.UI's definition round trip. The pages are assembled into
            //    one table and handed to Create.VisualTypology, which is the runtime piece that solves a
            //    definition against that table; the plain TypologyAsync cannot take the Visual chain,
            //    because the two filter families are siblings rather than a subtype pair. Persisting a
            //    solved VisualTypology (TypologyModelPostgreSQLConverter) is deliberately not part of it.
            // ---------------------------------------------------------------------------------------------

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            if (directory_Reports is not null)
            {
                System.IO.File.WriteAllLines(System.IO.Path.Combine(directory_Reports, "VisualTypology_UsageExample.txt"), lines_Report);
            }

            void AddBuilding(string reference, int? yearBuilt, string occupancy)
            {
                // Values are positional, matching the column order added above.
                List<object?> values = [reference, yearBuilt, occupancy];

                Assert.NotNull(table.AddRow(values));
            }
        }
    }
}
