using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Query.Occupancies"/>: no areas or a non-positive total give an empty result, and over 2 000 pseudo-random subdivisions (1–40 buildings, areas 1–500, population 0–3 000) the result has one value per building, sums exactly to the population, gives every building at least one resident when the population allows it, and is reproducible.
        /// <para>Reference: DiGi.GIS#11.</para>
        /// </summary>
        [Fact]
        public void Occupancies()
        {
            Assert.Empty(Query.Occupancies(null, 10));
            Assert.Empty(Query.Occupancies([], 10));
            Assert.Empty(Query.Occupancies([0, 0], 10));

            Random random = new(11);
            for (int i = 0; i < 2000; i++)
            {
                int count = random.Next(1, 41);
                List<double> occupancyAreas = [];
                for (int j = 0; j < count; j++)
                {
                    occupancyAreas.Add(1 + (random.NextDouble() * 499));
                }

                uint occupancy = (uint)random.Next(0, 3001);

                List<uint> occupancies = occupancyAreas.Occupancies(occupancy);
                Assert.Equal(count, occupancies.Count);

                long sum = 0;
                foreach (uint occupancy_Building in occupancies)
                {
                    sum += occupancy_Building;
                    if (occupancy >= count)
                    {
                        Assert.True(occupancy_Building >= 1);
                    }
                }

                Assert.Equal(occupancy, sum);
                Assert.Equal(occupancies, occupancyAreas.Occupancies(occupancy));
            }
        }

        /// <summary>
        /// Verifies that the remainder of <see cref="Query.Occupancies"/> can reach the last building. For n = 2 … 200 buildings of equal area and a population of n − 1, every floor share is zero and no minimum applies, so all n − 1 residents are drawn; the last building must receive at least one of them for a share of the subdivisions comparable to the expected 1 − ((n − 1) / n)^(n − 1) ≈ 0.63.
        /// <para>Reference: DiGi.GIS#13 — the draw used an exclusive upper bound, so the last building never received a remainder resident.</para>
        /// </summary>
        [Fact]
        public void Occupancies_LastBuilding()
        {
            int count_LastDrawn = 0;
            for (int count = 2; count <= 200; count++)
            {
                List<double> occupancyAreas = [];
                for (int i = 0; i < count; i++)
                {
                    occupancyAreas.Add(1);
                }

                List<uint> occupancies = occupancyAreas.Occupancies((uint)(count - 1));
                Assert.Equal(count, occupancies.Count);

                if (occupancies[count - 1] > 0)
                {
                    count_LastDrawn++;
                }
            }

            // Measured 135 of 199 (expected about 125 ± 7); the old exclusive bound gave 0.
            Assert.True(count_LastDrawn >= 100, $"The last building received a remainder resident in {count_LastDrawn} of 199 subdivisions.");
        }

        /// <summary>
        /// Verifies <see cref="Query.Building2DOccupancies"/>: for twelve occupied buildings and one non-residential building, passed in the original, reversed and 20 pseudo-randomly shuffled orders, every occupied building receives the same occupancy, the non-residential building is left out, the result is ordered by reference and sums to the population.
        /// <para>Reference: DiGi.GIS#13 — the production task reads buildings in storage order (no ORDER BY), so the distribution must not depend on the input order.</para>
        /// </summary>
        [Fact]
        public void Building2DOccupancies()
        {
            Building2D Building2D(string reference, double occupancyArea, ushort storeys, BuildingGeneralFunction buildingGeneralFunction)
            {
                Polygon2D polygon2D = new([new Point2D(0, 0), new Point2D(occupancyArea, 0), new Point2D(occupancyArea, 1), new Point2D(0, 1)]);
                PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
                Assert.NotNull(polygonalFace2D);

                return new Building2D(Guid.NewGuid(), reference, polygonalFace2D, storeys, BuildingPhase.occupied, buildingGeneralFunction, []);
            }

            List<Building2D> building2Ds = [];
            for (int i = 0; i < 12; i++)
            {
                building2Ds.Add(Building2D($"building_{(i * 7) % 12:D2}", 10 + (i * 13 % 37), (ushort)(i % 3), BuildingGeneralFunction.residential_buildings));
            }

            building2Ds.Add(Building2D("building_non_residential", 500, 2, BuildingGeneralFunction.industrial_buildings));

            const uint occupancy = 47;

            Dictionary<string, uint> Occupancies(IEnumerable<Building2D> building2Ds_Order)
            {
                List<Tuple<Building2D, double, uint>> tuples = Query.Building2DOccupancies(building2Ds_Order, occupancy);
                Assert.Equal(12, tuples.Count);
                Assert.Equal(tuples.ConvertAll(x => x.Item1.Reference).OrderBy(x => x, StringComparer.Ordinal), tuples.ConvertAll(x => x.Item1.Reference));

                Dictionary<string, uint> result = [];
                long sum = 0;
                foreach (Tuple<Building2D, double, uint> tuple in tuples)
                {
                    result[tuple.Item1.Reference!] = tuple.Item3;
                    sum += tuple.Item3;
                }

                Assert.Equal(occupancy, sum);
                Assert.DoesNotContain("building_non_residential", result.Keys);
                return result;
            }

            Dictionary<string, uint> occupancies_Original = Occupancies(building2Ds);

            List<Building2D> building2Ds_Reversed = [.. building2Ds];
            building2Ds_Reversed.Reverse();
            Assert.Equal(occupancies_Original, Occupancies(building2Ds_Reversed));

            Random random = new(13);
            for (int i = 0; i < 20; i++)
            {
                List<Building2D> building2Ds_Shuffled = [.. building2Ds.OrderBy(x => random.Next())];
                Assert.Equal(occupancies_Original, Occupancies(building2Ds_Shuffled));
            }
        }
    }
}
