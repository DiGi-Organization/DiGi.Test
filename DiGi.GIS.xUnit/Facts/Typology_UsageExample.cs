using DiGi.Core.Classes;
using DiGi.Core.IO.Table.Classes;
using DiGi.Typology.Classes;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Worked example of classifying building data into a typology tree, written to be read rather than only run.
        /// <para>It goes end to end: declare the grouping, ask what columns it needs, classify a table, then read a node, walk the tree, test membership, and solve the same data without references. Every step is the API a caller would actually use, and the assertions state what each one is expected to answer.</para>
        /// <para>The table is built by hand so the example runs with no database. In production it arrives from DiGi.GIS.PostgreSQL's Create.TypologyAsync, which pages it out of building_data and then calls the same Create.Typology used here - see the closing note.</para>
        /// <para>The rendered tree is written to user files/reports/Typology_UsageExample.txt, so the shape this produces can be looked at rather than inferred from assertions.</para>
        /// </summary>
        [Fact]
        public void Typology_UsageExample()
        {
            List<string> lines_Report = [];

            // ---------------------------------------------------------------------------------------------
            // 1. The source data.
            //
            //    A DiGi.Core.IO table of five buildings. Column order here deliberately does not match the
            //    order the grouping below names them in, because nothing requires it to: a column is resolved
            //    by its unique id, which is derived from its Name ("County name" -> "county_name").
            // ---------------------------------------------------------------------------------------------

            Table table = new();

            Assert.NotNull(table.AddColumn(IO.Constants.Column.Reference));
            Assert.NotNull(table.AddColumn(IO.Constants.Column.PredictedYearBuilt));
            Assert.NotNull(table.AddColumn(IO.Constants.Column.CountyName));
            Assert.NotNull(table.AddColumn(IO.Constants.Column.IsOccupied));

            AddBuilding("BLD-001", (ushort)1975, "Poznanski", true);
            AddBuilding("BLD-002", (ushort)2008, "Poznanski", true);
            AddBuilding("BLD-003", (ushort)2019, "Poznanski", false);
            AddBuilding("BLD-004", (ushort)2022, "Wroclawski", true);

            // The last building has no predicted year. It matters, and step 6 shows where it ends up.
            AddBuilding("BLD-005", null, "Wroclawski", true);

            // ---------------------------------------------------------------------------------------------
            // 2. Declare the grouping.
            //
            //    A chain of levels, each naming a column and a rule for turning that column's value into a
            //    bucket. Level N+1 hangs off level N through Filter, so this reads top down: county, then
            //    occupancy within a county, then year band within that.
            //
            //    UniqueValueFilterRule buckets by exact value, and buckets a null as its own "null" bucket.
            //    IntegerRangeFilterRule buckets by range. Ranges are CLOSED at both ends - 1989 and 1990 are
            //    each in exactly one of the first two below - and may be declared in any order.
            //
            //    Reading the report, note that a bucket renders as "[0, 1989]" - the closed-interval
            //    notation matching the rule's closed matching on both ends: 0 is in that bucket, and the
            //    name says so. The name is what a consumer displays. (It used to render "(0,1989>", which
            //    looked half-open while 0 still matched - DiGi-Organization/DiGi.Typology#16, now fixed.)
            //
            //    Grouping by County name rather than County Id is deliberate: an id addresses one polygon
            //    part, and 18 counties have several, so id-grouping would split a county across sibling
            //    nodes. The name re-merges them.
            //
            //    The shared column constants are used as-is. Their Index is -1, because an index belongs to
            //    the table a column was added to rather than to the column - Create.Typology resolves each
            //    one against the table and leaves these constants untouched.
            // ---------------------------------------------------------------------------------------------

            ColumnTypologyFilter<Column> columnTypologyFilter = new()
            {
                Value = IO.Constants.Column.CountyName,
                Rule = new UniqueValueFilterRule(),
                Filter = new ColumnTypologyFilter<Column>()
                {
                    Value = IO.Constants.Column.IsOccupied,
                    Rule = new UniqueValueFilterRule(),
                    Filter = new ColumnTypologyFilter<Column>()
                    {
                        Value = IO.Constants.Column.PredictedYearBuilt,
                        Rule = new IntegerRangeFilterRule([new Range<int>(0, 1989), new Range<int>(1990, 2010), new Range<int>(2011, int.MaxValue)])
                    }
                }
            };

            // ---------------------------------------------------------------------------------------------
            // 3. Ask the chain which columns it needs.
            //
            //    This is what to request from a data source, so a pull stays narrow instead of fetching every
            //    column a table happens to have. Create.TypologyAsync uses it for exactly that, adding the
            //    reference and county id columns it needs to page and identify rows.
            // ---------------------------------------------------------------------------------------------

            List<string>? columnUniqueIds = Query.ColumnUniqueIds(columnTypologyFilter);

            Assert.NotNull(columnUniqueIds);
            Assert.Equal(["county_name", "is_occupied", "predicted_year_built"], columnUniqueIds);

            lines_Report.Add("Columns the chain groups by: " + string.Join(", ", columnUniqueIds));
            lines_Report.Add(string.Empty);

            // ---------------------------------------------------------------------------------------------
            // 4. Classify.
            //
            //    The third argument names the column that identifies a row. Without it a node could not say
            //    which buildings it holds, so it is required whenever references are being stored.
            // ---------------------------------------------------------------------------------------------

            Typology.Classes.Typology? typology = Create.Typology(table, columnTypologyFilter, IO.Constants.Column.Reference);

            Assert.NotNull(typology);

            // ---------------------------------------------------------------------------------------------
            // 5. Read one node.
            //
            //    A path is a sequence of indexes from the root, so [0] is the first county and [0, 0] the
            //    first occupancy bucket inside it. A node is named "{column} {bucket}".
            //
            //    References accumulate upwards: a node holds the references of everything below it, so the
            //    county node already lists all three of its buildings. The root itself holds none - it is
            //    the tree, not a bucket - which is why the whole set is read with includeNested.
            // ---------------------------------------------------------------------------------------------

            Typology.Classes.Typology? typology_County = Typology.Query.SubTypology(typology, [0]);

            Assert.NotNull(typology_County);
            Assert.Equal("County name Poznanski", typology_County.Name);
            Assert.Equal(3, typology_County.References.Count);

            Assert.Empty(typology.References);
            Assert.Equal(5, Typology.Query.ReferenceSet(typology, true).Count);

            // ---------------------------------------------------------------------------------------------
            // 6. Walk the whole tree.
            //
            //    Note where BLD-005 appears. It has no predicted year, a range rule resolves nothing for a
            //    missing value, and the solver then drops the row from that level and everything under it -
            //    there is no catch-all bucket. So BLD-005 is listed on its county and occupancy nodes and on
            //    no year node at all. A row is not lost, it simply stops being classified further down.
            // ---------------------------------------------------------------------------------------------

            List<TypologyPath> typologyPaths = Typology.Query.TypologyPaths(typology, true);

            Assert.NotEmpty(typologyPaths);

            lines_Report.Add("Tree (path, name, buildings held by that node):");

            foreach (TypologyPath typologyPath in typologyPaths)
            {
                Typology.Classes.Typology? typology_Node = Typology.Query.SubTypology(typology, typologyPath);
                Assert.NotNull(typology_Node);

                List<string> references_Node = typology_Node.References;
                references_Node.Sort();

                lines_Report.Add(string.Format(CultureInfo.CurrentCulture, "  [{0}]{1}{2} -> {3}", typologyPath, new string(' ', typologyPath.Count * 2), typology_Node.Name, string.Join(", ", references_Node)));
            }

            lines_Report.Add(string.Empty);

            // BLD-005 reaches its occupancy node and stops there.
            Typology.Classes.Typology? typology_Occupied = Typology.Query.SubTypology(typology, [1, 0]);
            Assert.NotNull(typology_Occupied);
            Assert.Contains("BLD-005", typology_Occupied.References);

            foreach (Typology.Classes.Typology subTypology in typology_Occupied.SubTypologies ?? [])
            {
                Assert.DoesNotContain("BLD-005", Typology.Query.ReferenceSet(subTypology, true));
            }

            // ---------------------------------------------------------------------------------------------
            // 7. Ask whether a building is in a branch.
            //
            //    Contains without includeNested asks about that node's own set; with it, the whole subtree.
            //    The root holds nothing itself, so the two answers differ there.
            // ---------------------------------------------------------------------------------------------

            Assert.True(Typology.Query.Contains(typology_County, "BLD-001"));
            Assert.False(Typology.Query.Contains(typology_County, "BLD-004"));

            Assert.False(Typology.Query.Contains(typology, "BLD-001"));
            Assert.True(Typology.Query.Contains(typology, "BLD-001", true));

            // ---------------------------------------------------------------------------------------------
            // 8. The same classification without references.
            //
            //    Every node from a matched row up to the root stores that reference, so a three level chain
            //    stores each one three times. Where the node to building association is kept outside the
            //    tree, solve with includeReferences false: identical structure and node names, nothing
            //    stored. An already-solved tree can be stripped the same way with Modify.RemoveReferences,
            //    on the whole tree or on one branch.
            // ---------------------------------------------------------------------------------------------

            Typology.Classes.Typology? typology_Metadata = Create.Typology(table, columnTypologyFilter, IO.Constants.Column.Reference, null, false);

            Assert.NotNull(typology_Metadata);
            Assert.Empty(Typology.Query.ReferenceSet(typology_Metadata, true));
            Assert.Equal(typologyPaths.Count, Typology.Query.TypologyPaths(typology_Metadata, true).Count);

            Typology.Classes.Typology? typology_Stripped = Create.Typology(table, columnTypologyFilter, IO.Constants.Column.Reference);
            Assert.NotNull(typology_Stripped);
            Assert.True(Typology.Modify.RemoveReferences(typology_Stripped, true));
            Assert.Empty(Typology.Query.ReferenceSet(typology_Stripped, true));

            lines_Report.Add("Solved again with includeReferences false: same " + typologyPaths.Count + " nodes, 0 references stored.");
            lines_Report.Add(string.Empty);

            // ---------------------------------------------------------------------------------------------
            // 9. Against a real database.
            //
            //    The DiGi.GIS.PostgreSQL entry point takes the same chain and does steps 1, 3 and 4 for you:
            //
            //        Typology? typology = await buildingDataPostgreSQLConverter.TypologyAsync(columnTypologyFilter, countyIds);
            //
            //    countyIds is required and is a list of polygon parts, not counties - there are 406 of them
            //    for 380 counties, and one part holds tens of thousands of rows, so what to read is always
            //    stated rather than defaulted.
            // ---------------------------------------------------------------------------------------------

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            if (directory_Reports is not null)
            {
                System.IO.File.WriteAllLines(System.IO.Path.Combine(directory_Reports, "Typology_UsageExample.txt"), lines_Report);
            }

            void AddBuilding(string reference, object? predictedYearBuilt, string countyName, bool isOccupied)
            {
                // Values are positional, matching the column order added above.
                List<object?> values = [reference, predictedYearBuilt, countyName, isOccupied];

                Assert.NotNull(table.AddRow(values));
            }
        }
    }
}
