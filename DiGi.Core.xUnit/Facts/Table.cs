using DiGi.Core.IO.Table.Classes;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests Row and Table functionalities, including empty row handling, column shifting during removal, and JSON serialization.
        /// </summary>
        [Fact]
        public void Table_OperationsAndSerialization()
        {
            // 1. Test Row.GetValues on empty row (Verifying the fix for Bug 2)
            Row row_Empty = new(0);
            object?[] values_Empty = row_Empty.GetValues();
            Assert.NotNull(values_Empty);
            Assert.Empty(values_Empty);

            // 2. Test Table.RemoveColumns and shifting (Verifying the fix for Bug 1)
            Table table_Test = new();

            // Add three columns
            Column? column_0 = table_Test.AddColumn(new Column("Col0", typeof(string)));
            Column? column_1 = table_Test.AddColumn(new Column("Col1", typeof(int)));
            Column? column_2 = table_Test.AddColumn(new Column("Col2", typeof(double)));

            // Add a row with values (using collection plural naming)
            List<object?> rowValues = ["A", 10, 2.5];
            Row? row_Added = table_Test.AddRow(rowValues);
            Assert.NotNull(row_Added);

            // Verify initial values
            Assert.Equal("A", table_Test[0, 0]);
            Assert.Equal(10, table_Test[0, 1]);
            Assert.Equal(2.5, table_Test[0, 2]);

            // Remove column at index 1 (middle column). This should shift Col2 to index 1 and its value 2.5 to index 1
            Assert.True(table_Test.RemoveColumn(1));

            // Verify column count is now 2
            Assert.Equal(2, table_Test.ColumnCount);

            // Verify that the remaining columns and their values are correctly preserved and shifted
            Assert.Equal("A", table_Test[0, 0]);
            Assert.Equal(2.5, table_Test[0, 1]); // Col2 shifted to index 1
            Assert.Null(table_Test[0, 2]);       // Index 2 is now empty/null

            // 3. Test TableConverter deserialization with column/row mismatch (Verifying the fix for Bug 3)
            // Create a JSON representing a table with 2 columns, but where a row has 3 elements
            string string_Json = @"{
                ""Columns"": [
                    { ""Index"": 0, ""Name"": ""Col0"", ""Type"": ""System.String"" },
                    { ""Index"": 1, ""Name"": ""Col1"", ""Type"": ""System.Int32"" }
                ],
                ""Rows"": [
                    [ ""A"", 10, 2.5 ]
                ]
            }";

            // Deserialize using TableConverter
#pragma warning disable CA1869 // the fact deserializes once, with a converter of its own
            JsonSerializerOptions jsonSerializerOptions = new();
#pragma warning restore CA1869
            jsonSerializerOptions.Converters.Add(new TableConverter<Table, Column, Row>());
            Table? table_Deserialized = JsonSerializer.Deserialize<Table>(string_Json, jsonSerializerOptions);
            Assert.NotNull(table_Deserialized);
            Assert.Equal(2, table_Deserialized.ColumnCount);
            Assert.Equal(1, table_Deserialized.RowCount);

            // Verify that the extra element was ignored or handled and didn't crash
            Assert.Equal("A", table_Deserialized[0, 0]);
            Assert.Equal(10, table_Deserialized[0, 1]);
        }

        /// <summary>
        /// Verifies that enumerating a Table yields live row references instead of a materialized cloned snapshot.
        /// <para>Fails on the unmodified code, where GetEnumerator() delegates to the cloning Rows property, so a mutation of an enumerated row is invisible to the table.</para>
        /// </summary>
        [Fact]
        public void Table_GetEnumeratorStreamsLiveRows()
        {
            Table table = new();
            table.AddColumn(new Column("Col0", typeof(string)));
            table.AddColumn(new Column("Col1", typeof(int)));

            table.AddRow(["A", 1]);
            table.AddRow(["B", 2]);

            foreach (Row row in table)
            {
                row[0] = "Mutated";
                break;
            }

            Assert.Equal("Mutated", table[0, 0]);
        }

        /// <summary>
        /// Benchmarks enumeration of a 20,000-row Table and appends the measured per-run times to the reports directory.
        /// <para>Before the fix the same loop paid for a full deep-clone pass over every row via the Rows property; measure in isolation and A/B against the pre-change baseline.</para>
        /// </summary>
        [Fact]
        public void Table_Enumeration_Performance()
        {
            const int rowCount = 20000;
            const int columnCount = 4;

            Table table = new();
            for (int i = 0; i < columnCount; i++)
            {
                table.AddColumn(new Column($"Col{i}", typeof(int)));
            }

            for (int i = 0; i < rowCount; i++)
            {
                List<object?> values = [];
                for (int j = 0; j < columnCount; j++)
                {
                    values.Add(i * columnCount + j);
                }

                table.AddRow(values);
            }

            // Warm-up run (JIT compilation).
            int count = 0;
            foreach (Row row in table)
            {
                count++;
            }

            Assert.Equal(rowCount, count);

            List<long> elapsedMilliseconds = [];
            for (int run = 0; run < 3; run++)
            {
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                count = 0;
                foreach (Row row in table)
                {
                    count++;
                }

                stopwatch.Stop();
                elapsedMilliseconds.Add(stopwatch.ElapsedMilliseconds);
            }

            string? pathReportsDir = Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(pathReportsDir));

            List<string> reportLines =
            [
                $"Table_Enumeration_Performance: rowCount={rowCount} columnCount={columnCount} runs=[{string.Join(", ", elapsedMilliseconds)}] ms",
            ];
            string reportFilePath = System.IO.Path.Combine(pathReportsDir!, "Table_Enumeration_Performance.txt");
            System.IO.File.AppendAllLines(reportFilePath, reportLines);

            long max = elapsedMilliseconds.Max();
            Assert.True(max < 2000, $"Enumeration exceeded the 2000 ms threshold: [{string.Join(", ", elapsedMilliseconds)}] ms");
        }

        /// <summary>
        /// Benchmarks filling a Table row by row and appends the measured per-run times to the reports directory.
        /// <para>Every AddRow asks the table for its next row index. Before the fix that was Enumerable.Last over a SortedDictionary - a walk of every row already held - so a table of n rows cost O(n^2) to fill: 100 000 rows took tens of seconds, and the Typology solve of a large county never finished. The rows now sit in a SortedList whose last key is O(1), so the fill is O(n log n) and the bound below holds with a wide margin; measure in isolation and A/B against the pre-change baseline.</para>
        /// </summary>
        [Fact]
        public void Table_AddRow_Performance()
        {
            const int rowCount = 100000;
            const int columnCount = 4;

            // Warm-up run (JIT compilation).
            Fill(1000);

            List<long> elapsedMilliseconds = [];
            for (int run = 0; run < 3; run++)
            {
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                Table table = Fill(rowCount);

                stopwatch.Stop();
                elapsedMilliseconds.Add(stopwatch.ElapsedMilliseconds);

                Assert.Equal(rowCount, table.RowCount);
                Assert.Equal(rowCount, table.GetNextRowIndex());
            }

            string? pathReportsDir = Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(pathReportsDir));

            List<string> reportLines =
            [
                $"Table_AddRow_Performance: rowCount={rowCount} columnCount={columnCount} runs=[{string.Join(", ", elapsedMilliseconds)}] ms",
            ];
            string reportFilePath = System.IO.Path.Combine(pathReportsDir!, "Table_AddRow_Performance.txt");
            System.IO.File.AppendAllLines(reportFilePath, reportLines);

            long max = elapsedMilliseconds.Max();
            Assert.True(max < 3000, $"AddRow exceeded the 3000 ms threshold: [{string.Join(", ", elapsedMilliseconds)}] ms");

            static Table Fill(int count)
            {
                Table table = new();
                for (int i = 0; i < columnCount; i++)
                {
                    table.AddColumn(new Column($"Col{i}", typeof(int)));
                }

                for (int i = 0; i < count; i++)
                {
                    List<object?> values = [];
                    for (int j = 0; j < columnCount; j++)
                    {
                        values.Add(i * columnCount + j);
                    }

                    table.AddRow(values);
                }

                return table;
            }
        }
    }
}