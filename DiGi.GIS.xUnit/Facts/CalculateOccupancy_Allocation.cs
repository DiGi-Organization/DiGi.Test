using DiGi.Geometry.Planar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System;
using System.Collections.Generic;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Recalculates occupancy for one subdivision with buildings of the given occupancy areas (footprint area, one storey each), in the given order, and returns the occupancy assigned to each building.
        /// </summary>
        /// <param name="occupancy">The population of the subdivision.</param>
        /// <param name="occupancyAreas">The footprint areas of the buildings, in relation order.</param>
        /// <returns>The occupancy of each building, in the order of <paramref name="occupancyAreas"/>.</returns>
        private static List<uint> CalculateOccupancy_Building2DOccupancies(uint occupancy, params double[] occupancyAreas)
        {
            GISModel gISModel = new();

            List<Building2D> building2Ds = [];
            double x = 0;
            foreach (double occupancyArea in occupancyAreas)
            {
                Polygon2D polygon2D = new([new Point2D(x, 0), new Point2D(x + occupancyArea, 0), new Point2D(x + occupancyArea, 1), new Point2D(x, 1)]);
                PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
                Assert.NotNull(polygonalFace2D);

                Building2D building2D = new(Guid.NewGuid(), Guid.NewGuid().ToString(), polygonalFace2D, 1, BuildingPhase.occupied, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]);
                Assert.True(gISModel.Update(building2D));
                building2Ds.Add(building2D);

                x += occupancyArea + 1;
            }

            Polygon2D polygon2D_Subdivision = new([new Point2D(-1, -1), new Point2D(x + 1, -1), new Point2D(x + 1, 2), new Point2D(-1, 2)]);
            PolygonalFace2D? polygonalFace2D_Subdivision = Geometry.Planar.Create.PolygonalFace2D(polygon2D_Subdivision);
            Assert.NotNull(polygonalFace2D_Subdivision);

            AdministrativeSubdivision administrativeSubdivision = new(Guid.NewGuid(), "reference_Subdivision", "code_Subdivision", polygonalFace2D_Subdivision, default, "name_Subdivision", occupancy);
            Assert.True(gISModel.Update(administrativeSubdivision));
            Assert.True(gISModel.Update(administrativeSubdivision, building2Ds));

            gISModel.CalculateOccupancy();

            List<uint> result = [];
            foreach (Building2D building2D in building2Ds)
            {
                OccupancyCalculationResult? occupancyCalculationResult = gISModel.GetRelatedObject<OccupancyCalculationResult>(building2D);
                Assert.NotNull(occupancyCalculationResult);
                Assert.NotNull(occupancyCalculationResult.Occupancy);

                result.Add(occupancyCalculationResult.Occupancy.Value);
            }

            return result;
        }

        /// <summary>
        /// Verifies that <see cref="Modify.CalculateOccupancy"/> distributes exactly the subdivision population over its buildings.
        /// <para>Cases: a forced minimum of one consumes the last resident before the last building (areas 100, 1, 1; population 3), population equal to, larger than and much larger than the number of buildings, and a population of zero. Reference: DiGi.GIS#11 — the remainder reached zero and every further building still received one resident, so the buildings held more people than the subdivision.</para>
        /// </summary>
        [Fact]
        public void CalculateOccupancy_Allocation()
        {
            List<uint> occupancies_OverAllocation = CalculateOccupancy_Building2DOccupancies(3, 100, 1, 1);
            Assert.Equal(3U, Sum(occupancies_OverAllocation));

            List<uint> occupancies_Equal = CalculateOccupancy_Building2DOccupancies(3, 1, 1, 1);
            Assert.Equal([1U, 1U, 1U], occupancies_Equal);

            List<uint> occupancies_Larger = CalculateOccupancy_Building2DOccupancies(10, 50, 30, 20);
            Assert.Equal(10U, Sum(occupancies_Larger));
            Assert.All(occupancies_Larger, x => Assert.True(x >= 1));

            List<uint> occupancies_MuchLarger = CalculateOccupancy_Building2DOccupancies(1000, 7, 3, 11, 1, 5);
            Assert.Equal(1000U, Sum(occupancies_MuchLarger));

            List<uint> occupancies_Zero = CalculateOccupancy_Building2DOccupancies(0, 10, 20);
            Assert.Equal([0U, 0U], occupancies_Zero);
        }

        /// <summary>
        /// Verifies that <see cref="Modify.CalculateOccupancy"/> does not wrap the remaining population when forced minimums consume it before a building whose proportional share exceeds what is left (areas 1, 1, 100; population 3; fewer residents than buildings in a second case).
        /// <para>Reference: DiGi.GIS#11 — the unsigned remainder wrapped to about 4.29 × 10⁹ and the remainder loop then added one resident roughly four billion times, so the calculation did not return.</para>
        /// </summary>
        [Fact]
        public void CalculateOccupancy_Underflow()
        {
            List<uint> occupancies = CalculateOccupancy_Building2DOccupancies(3, 1, 1, 100);
            Assert.Equal(3U, Sum(occupancies));
            Assert.All(occupancies, x => Assert.True(x <= 3));

            List<uint> occupancies_FewerThanBuildings = CalculateOccupancy_Building2DOccupancies(2, 1, 1, 100, 1, 1);
            Assert.Equal(2U, Sum(occupancies_FewerThanBuildings));
        }

        private static uint Sum(IEnumerable<uint> values)
        {
            uint result = 0;
            foreach (uint value in values)
            {
                result += value;
            }

            return result;
        }
    }
}
