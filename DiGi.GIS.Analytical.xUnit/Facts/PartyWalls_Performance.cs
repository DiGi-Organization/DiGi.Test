using DiGi.Analytical.Building.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Measures <see cref="DiGi.Analytical.Building.Query.PartyWalls(BuildingModel?, IEnumerable{BuildingModel?}?, double, double, double, double)"/>, its collection overload <see cref="DiGi.Analytical.Building.Query.PartyWalls(IEnumerable{BuildingModel?}?, IEnumerable{BuildingModel?}?, double, double, double, double)"/> and <see cref="DiGi.Analytical.Building.Modify.SplitPartyWalls"/> over 10 terraces of 30 three-storey buildings (DiGi-Organization/DiGi.GIS.Analytical#4).
        /// <para>Per building, each one is compared with the buildings whose outline lies within 0.2 m; the collection overload takes all 300 at once, as the building data task does per batch, and builds each envelope once instead of once per call that touches it. Every shared line gives one party wall per storey and direction: 10 x 29 x 2 x 3 = 1740 results. The timings are written to <c>reports/PartyWalls_Performance.txt</c>; the thresholds are generous, the report is what tracks the cost.</para>
        /// </summary>
        [MediumFact]
        public void PartyWalls_Performance()
        {
            const int rowCount = 10;
            const int columnCount = 30;

            List<BuildingModel> CreateBuildingModels()
            {
                List<BuildingModel> result = [];
                for (int row = 0; row < rowCount; row++)
                {
                    for (int column = 0; column < columnCount; column++)
                    {
                        double x = column * 8.0;
                        double y = row * 30.0;
                        result.Add(PartyWalls_BuildingModel(3, new Point2D(x, y), new Point2D(x + 8.0, y), new Point2D(x + 8.0, y + 10.0), new Point2D(x, y + 10.0)));
                    }
                }

                return result;
            }

            List<List<BuildingModel>> Neighbours(List<BuildingModel> buildingModels)
            {
                List<BoundingBox3D?> boundingBox3Ds = buildingModels.ConvertAll(x => x.GetBoundingBox());
                List<List<BuildingModel>> result = [];
                for (int i = 0; i < buildingModels.Count; i++)
                {
                    List<BuildingModel> neighbours = [];
                    for (int j = 0; j < buildingModels.Count; j++)
                    {
                        if (i != j && boundingBox3Ds[i] is BoundingBox3D boundingBox3D && boundingBox3Ds[j] is BoundingBox3D boundingBox3D_Neighbour && boundingBox3D.InRange(boundingBox3D_Neighbour, 0.2))
                        {
                            neighbours.Add(buildingModels[j]);
                        }
                    }

                    result.Add(neighbours);
                }

                return result;
            }

            // Warm-up: JIT and the geometry library's first calls.
            List<BuildingModel> buildingModels_WarmUp = CreateBuildingModels().GetRange(0, 3);
            DiGi.Analytical.Building.Query.PartyWalls(buildingModels_WarmUp[1], buildingModels_WarmUp);

            List<BuildingModel> buildingModels = CreateBuildingModels();
            List<List<BuildingModel>> neighbours_All = Neighbours(buildingModels);

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<List<PartyWallResult>> partyWallResults_All = [];
            int count = 0;
            for (int i = 0; i < buildingModels.Count; i++)
            {
                List<PartyWallResult> partyWallResults = DiGi.Analytical.Building.Query.PartyWalls(buildingModels[i], neighbours_All[i])!;
                partyWallResults_All.Add(partyWallResults);
                count += partyWallResults.Count;
            }

            long elapsed_Detection = stopwatch.ElapsedMilliseconds;
            Assert.Equal(rowCount * (columnCount - 1) * 2 * 3, count);

            stopwatch.Restart();
            Dictionary<System.Guid, List<PartyWallResult>>? dictionary = DiGi.Analytical.Building.Query.PartyWalls(buildingModels, null);
            long elapsed_Collection = stopwatch.ElapsedMilliseconds;

            Assert.NotNull(dictionary);
            int count_Collection = 0;
            foreach (List<PartyWallResult> partyWallResults in dictionary.Values)
            {
                count_Collection += partyWallResults.Count;
            }

            Assert.Equal(count, count_Collection);

            stopwatch.Restart();
            for (int i = 0; i < buildingModels.Count; i++)
            {
                Assert.True(DiGi.Analytical.Building.Modify.SplitPartyWalls(buildingModels[i], partyWallResults_All[i]));
            }

            long elapsed_Split = stopwatch.ElapsedMilliseconds;

            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(directory_Reports);
            System.IO.File.AppendAllLines(System.IO.Path.Combine(directory_Reports, "PartyWalls_Performance.txt"), [$"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss} buildings {buildingModels.Count} party walls {count} detection {elapsed_Detection} ms collection {elapsed_Collection} ms split {elapsed_Split} ms"]);

            Assert.True(elapsed_Detection < 20000, $"detection took {elapsed_Detection} ms");
            Assert.True(elapsed_Collection < 20000, $"collection detection took {elapsed_Collection} ms");
            Assert.True(elapsed_Split < 20000, $"split took {elapsed_Split} ms");
        }
    }
}
