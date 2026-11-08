using DiGi.Core.Classes;
using DiGi.Core.IO.Table.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DiGi.GIS.YOLO.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that naming the scored references turns the detection write into a replacement of each scored building's detection columns (DiGi-Organization/DiGi.GIS.YOLO.UI#21).
        /// <para>A scored building the detector did not fire on gets a row with every detection column of the range unset, a detected building's undetected years are unset, and a year no building of the batch was detected in is still a column - so the endpoint, which writes an unset cell as NULL and leaves a row or column it is not sent alone, clears what earlier weights left. The table is also carried through the wire form the endpoint parses, so the empty columns and the row with nothing but its identity survive the trip.</para>
        /// <para>Without references the table is the one the detection write has always sent.</para>
        /// </summary>
        [Fact]
        public void BuildingDataYearBuiltPredictionsTable()
        {
            int countyId = 73485;

            Building2DYearBuiltPredictions building2DYearBuiltPredictions = new("b_ref_001", [new YearBuiltPrediction(2020, new BoundingBox2D(10, 20, 30, 40), 0.9)]);

            Assert.Null(Create.BuildingDataYearBuiltPredictionsTable(0, [building2DYearBuiltPredictions], ["b_ref_001"]));

            //No references: the detected building and its detected year only
            Table? table_Detected = Create.BuildingDataYearBuiltPredictionsTable(countyId, [building2DYearBuiltPredictions]);
            Assert.NotNull(table_Detected);
            Assert.Equal(1, table_Detected!.RowCount);
            Assert.Equal(2 + 5, table_Detected.Columns.Count());
            Assert.False(table_Detected.TryGetColumn("Prediction Confidence 2019", out _));

            //References named: every detection column of 2019..2021 and a row for the building the detector did not fire on
            Table? table_Replace = Create.BuildingDataYearBuiltPredictionsTable(countyId, [building2DYearBuiltPredictions], ["b_ref_001", "b_ref_002", "b_ref_001", " "], new Range<int>(2019, 2021));
            Assert.NotNull(table_Replace);

            void Check(Table table)
            {
                Assert.Equal(2, table.RowCount);
                Assert.Equal(2 + (3 * 5), table.Columns.Count());

                Assert.True(table.TryGetColumn(IO.Constants.Column.Reference.Name, out Column? column_Reference));
                Assert.True(table.TryGetColumn("Prediction Confidence 2019", out Column? column_Confidence2019));
                Assert.True(table.TryGetColumn("Prediction Confidence 2020", out Column? column_Confidence2020));
                Assert.True(table.TryGetColumn("Prediction BoundingBox Height 2021", out Column? column_Height2021));

                Dictionary<string, Row> rows_ByReference = [];
                for (int i = 0; i < table.RowCount; i++)
                {
                    Row? row = table.GetRow(i);
                    if (row is not null && row.TryGetValue(column_Reference!.Index, out string? reference) && reference is not null)
                    {
                        rows_ByReference[reference] = row;
                    }
                }

                Assert.Equal(["b_ref_001", "b_ref_002"], rows_ByReference.Keys.OrderBy(x => x));

                Row row_Detected = rows_ByReference["b_ref_001"];
                Assert.True(row_Detected.TryGetValue(column_Confidence2020!.Index, out double confidence));
                Assert.Equal(0.9, confidence, 6);
                Assert.False(row_Detected.TryGetValue(column_Confidence2019!.Index, out double _));

                Row row_Cleared = rows_ByReference["b_ref_002"];
                foreach (Column column in table.Columns)
                {
                    if (column.Name is string name && name.StartsWith("Prediction ", StringComparison.CurrentCulture))
                    {
                        Assert.False(row_Cleared.TryGetValue(column.Index, out object? _), $"{name} carries a value on a building the detector did not fire on.");
                    }
                }

                Assert.False(row_Cleared.TryGetValue(column_Height2021!.Index, out double _));
            }

            Check(table_Replace!);

            //The endpoint parses the body with GIS.WebAPI.Create.Table
            string? json = Core.IO.Table.Convert.ToSystem_String<Table, Column, Row>(table_Replace!);
            Assert.False(string.IsNullOrWhiteSpace(json));

            Table? table_Wire = GIS.WebAPI.Create.Table(JsonNode.Parse(json!) as JsonObject);
            Assert.NotNull(table_Wire);
            Check(table_Wire!);

            //Null years: the range the scoring step projects, 2008..2025
            Table? table_Default = Create.BuildingDataYearBuiltPredictionsTable(countyId, [], ["b_ref_002"]);
            Assert.NotNull(table_Default);
            Assert.Equal(1, table_Default!.RowCount);
            Assert.Equal(2 + (18 * 5), table_Default.Columns.Count());
            Assert.True(table_Default.TryGetColumn("Prediction Confidence 2008", out _));
            Assert.True(table_Default.TryGetColumn("Prediction Confidence 2025", out _));
        }

        /// <summary>
        /// Verifies that Query.PredictionImageReferences lists the buildings of a prediction image folder by the same last-underscore rule the detector's output is read with, once per building, and ignores what is not an orthophoto image.
        /// </summary>
        [Fact]
        public void PredictionImageReferences()
        {
            string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{nameof(PredictionImageReferences)}_{System.Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(directory);

            try
            {
                List<string> fileNames = ["ABC_1_2015.jpeg", "ABC_1_2016.jpeg", "XYZ_2020.jpeg", "notes.txt", "noyear.jpeg", "QRS_2019.png"];
                foreach (string fileName in fileNames)
                {
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, fileName), []);
                }

                HashSet<string> references = Query.PredictionImageReferences(directory);
                Assert.Equal(["ABC_1", "XYZ"], references.OrderBy(x => x));

                Assert.Empty(Query.PredictionImageReferences(System.IO.Path.Combine(directory, "missing")));
                Assert.Empty(Query.PredictionImageReferences(null));
            }
            finally
            {
                System.IO.Directory.Delete(directory, true);
            }
        }
    }
}
