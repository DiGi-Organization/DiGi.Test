using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.IO.Table.Classes;
using DiGi.Core.Parameter.Classes;
using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.Geometry.Spatial.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Modify.Update_ExternalComponentsArea(Table?, IEnumerable{BuildingModel}?, IEnumerable{DiGi.Analytical.Building.Classes.PartyWallResult?}?)"/> gives the same cells when the models are split into disjoint batches classified concurrently into separate tables as when they are classified together in one table (DiGi-Organization/DiGi.GIS.PostgreSQL#154).
        /// <para>This is the thread-safety guard for the model code under the parallel batches of <see cref="PostgreSQLBuildingDataExternalComponentsUpdateTask"/>: a row of twelve cubes of different sizes, each sharing walls with its neighbours, measured for party walls in one call, then classified once sequentially and ten times as six batches of two on four threads. Every row and every cell must match. The guard can fail: the same models without the party walls give different cells.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_ConcurrentBatches()
        {
            const int count = 12;
            const int batchSize = 2;

            List<BuildingModel> buildingModels = ExternalComponents_Row(count, 7);

            List<DiGi.Analytical.Building.Classes.BuildingModel> buildingModels_Analytical = [];
            foreach (BuildingModel buildingModel in buildingModels)
            {
                DiGi.Analytical.Building.Classes.BuildingModel? buildingModel_Analytical = buildingModel.ToDiGi();
                Assert.NotNull(buildingModel_Analytical);
                buildingModels_Analytical.Add(buildingModel_Analytical);
            }

            Dictionary<Guid, List<DiGi.Analytical.Building.Classes.PartyWallResult>>? partyWallResults_ByGuid = DiGi.Analytical.Building.Query.PartyWalls(buildingModels_Analytical, buildingModels_Analytical);
            Assert.NotNull(partyWallResults_ByGuid);

            List<DiGi.Analytical.Building.Classes.PartyWallResult> partyWallResults = [];
            foreach (List<DiGi.Analytical.Building.Classes.PartyWallResult> partyWallResults_Model in partyWallResults_ByGuid.Values)
            {
                partyWallResults.AddRange(partyWallResults_Model);
            }

            Assert.NotEmpty(partyWallResults);

            Table table_Sequential = new();
            Modify.Update_ExternalComponentsArea(table_Sequential, buildingModels, partyWallResults);
            Assert.Equal(count, table_Sequential.RowCount);

            List<string> cells_Sequential = ExternalComponents_Cells([table_Sequential]);

            int batchCount = count / batchSize;
            for (int run = 0; run < 10; run++)
            {
                Table[] tables = new Table[batchCount];
                Parallel.For(0, batchCount, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, i =>
                {
                    Table table = new();
                    Modify.Update_ExternalComponentsArea(table, buildingModels.GetRange(i * batchSize, batchSize), partyWallResults);
                    tables[i] = table;
                });

                Assert.Equal(cells_Sequential, ExternalComponents_Cells(tables));
            }

            // The guard: the comparison sees the party wall cells.
            Table table_NoPartyWalls = new();
            Modify.Update_ExternalComponentsArea(table_NoPartyWalls, buildingModels, []);
            Assert.NotEqual(cells_Sequential, ExternalComponents_Cells([table_NoPartyWalls]));
        }

        /// <summary>
        /// Builds a row of cubes along the X axis, each touching the next, with sizes cycling from 6 to 10 m so that neighbouring cubes share walls of different areas and no two rows read the same.
        /// </summary>
        /// <param name="count">The number of cubes.</param>
        /// <param name="countyId">The county identifier the models are stored under.</param>
        /// <returns>The storage envelopes, referenced <c>ref_row_0</c> onwards.</returns>
        private static List<BuildingModel> ExternalComponents_Row(int count, int countyId)
        {
            List<BuildingModel> result = [];

            double x = 0.0;
            for (int i = 0; i < count; i++)
            {
                double size = 6.0 + (i % 5);

                IComponent[] components =
                [
                    Wall(new Point3D(x, 0, size), new Vector3D(-1, 0, 0), size),
                    Wall(new Point3D(x + size, size, size), new Vector3D(1, 0, 0), size),
                    Wall(new Point3D(x + size, 0, size), new Vector3D(0, -1, 0), size),
                    Wall(new Point3D(x, size, size), new Vector3D(0, 1, 0), size),
                    Floor(new Point3D(x, size, 0), new Vector3D(0, 0, -1), size),
                    Roof(new Point3D(x, 0, size), new Vector3D(0, 0, 1), size)
                ];

                DiGi.Analytical.Building.Classes.BuildingModel buildingModel = new();
                DiGi.Analytical.Building.Classes.Space space = new(new Point3D(x + (size / 2.0), size / 2.0, size / 2.0), "Space 1");
                Assert.True(buildingModel.Update(space));

                foreach (IComponent component in components)
                {
                    Assert.True(buildingModel.Assign(component, space));
                }

                Assert.True(buildingModel.SetValue(BuildingModelParameter.Reference, "ref_row_" + i.ToString(CultureInfo.InvariantCulture), new SetValueSettings(true, false)));

                BuildingModel? envelope = buildingModel.ToPostgreSQL(countyId);
                Assert.NotNull(envelope);
                result.Add(envelope);

                x += size;
            }

            return result;
        }

        /// <summary>
        /// Lists every cell of the tables, in table, row and column order, as <c>row|column=value</c> with the value written in the invariant culture, so two sets of tables compare cell by cell whatever their column order.
        /// </summary>
        /// <param name="tables">The tables, in the order their rows follow one another.</param>
        /// <returns>One entry per cell.</returns>
        private static List<string> ExternalComponents_Cells(IEnumerable<Table> tables)
        {
            List<string> result = [];

            int index = 0;
            foreach (Table table in tables)
            {
                List<Column> columns = [.. table.Columns];
                columns.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));

                for (int i = 0; i < table.RowCount; i++)
                {
                    Row? row = table.GetRow(i);
                    Assert.NotNull(row);

                    foreach (Column column in columns)
                    {
                        row.TryGetValue(column.Index, out object? value);
                        result.Add(string.Format(CultureInfo.InvariantCulture, "{0}|{1}={2}", index, column.Name, value is null ? "null" : System.Convert.ToString(value, CultureInfo.InvariantCulture)));
                    }

                    index++;
                }
            }

            return result;
        }
    }
}
