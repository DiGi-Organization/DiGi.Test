using System;
using System.Collections.Generic;

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
    }
}
