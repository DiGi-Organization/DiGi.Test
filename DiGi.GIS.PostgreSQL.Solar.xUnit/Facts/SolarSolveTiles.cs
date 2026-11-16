using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Query.SolarSolveTiles"/>, the walk order of the batch solar radiation solve and its <c>BuildingCountMax</c> cut (DiGi.GIS.PostgreSQL#138).
        /// <para>Five buildings in four 100 m tiles, given out of order with a null, a non-finite and a repeated centroid: tiles go by row and then by column, references ordinally within a tile (B, D | A | C | E). A cap keeps the first buildings of that walk, not of the input (C, A), and may cut a tile; a cap below 1 keeps one building; null keeps all; with the first two removed, as <c>SkipExisting</c> removes stored buildings, a cap of 2 takes the next two.</para>
        /// </summary>
        [Fact]
        public void SolarSolveTiles()
        {
            List<Building2DCentroid?> building2DCentroids =
            [
                Centroid("C", 50, 150),
                Centroid("A", 150, 50),
                null,
                Centroid("E", 160, 170),
                Centroid("NaN", double.NaN, 10),
                Centroid("D", 60, 40),
                Centroid("B", 20, 30),
                Centroid("B", 90, 190),
            ];

            Assert.Null(Query.SolarSolveTiles(null, 100));

            List<List<string>>? tiles = building2DCentroids.SolarSolveTiles(100);
            Assert.NotNull(tiles);
            Assert.Equal(4, tiles.Count);
            Assert.Equal(["B", "D"], tiles[0]);
            Assert.Equal(["A"], tiles[1]);
            Assert.Equal(["C"], tiles[2]);
            Assert.Equal(["E"], tiles[3]);

            Assert.Equal(["B", "D"], Flatten(building2DCentroids.SolarSolveTiles(100, 2)));
            Assert.Equal(["B", "D", "A"], Flatten(building2DCentroids.SolarSolveTiles(100, 3)));
            Assert.Equal(["B"], Flatten(building2DCentroids.SolarSolveTiles(100, 1)));
            Assert.Equal(["B"], Flatten(building2DCentroids.SolarSolveTiles(100, 0)));
            Assert.Equal(["B"], Flatten(building2DCentroids.SolarSolveTiles(100, -5)));
            Assert.Equal(["B", "D", "A", "C", "E"], Flatten(building2DCentroids.SolarSolveTiles(100, 100)));

            // A cut tile is partial: a cap of 1 keeps the first reference of the first tile only.
            Assert.Equal([["B"]], building2DCentroids.SolarSolveTiles(100, 1));

            // The repeat run: the first two are stored, so the cap takes the next two.
            List<Building2DCentroid?> building2DCentroids_Pending = [.. building2DCentroids.Where(x => x?.Reference != "B" && x?.Reference != "D")];
            Assert.Equal(["A", "C"], Flatten(building2DCentroids_Pending.SolarSolveTiles(100, 2)));
            Assert.Equal(["E"], Flatten(building2DCentroids_Pending.SolarSolveTiles(100, 3)).Skip(2));

            static Building2DCentroid Centroid(string reference, double x, double y)
            {
                return new Building2DCentroid() { Reference = reference, CountyId = 1, X = x, Y = y };
            }

            static List<string> Flatten(List<List<string>>? tiles_Flatten)
            {
                Assert.NotNull(tiles_Flatten);
                return [.. tiles_Flatten.SelectMany(x => x)];
            }
        }
    }
}
