using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reads the committed building shape sample fixture <c>files/BuildingShapeSample/gis12-2026.json</c> and verifies its structural invariants: the item count matches the recorded count and the sample size of 500, and the items are ordered by a gapless zero-based ordinal so that every per-item list built from them stays aligned.
        /// </summary>
        /// <returns>The parsed fixture document.</returns>
        private static JsonObject LoadBuildingShapeSample()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "BuildingShapeSample/gis12-2026.json");
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path));

            string text = File.ReadAllText(path!);
            JsonNode? jsonNode = JsonNode.Parse(text);
            Assert.NotNull(jsonNode);

            JsonObject fixture = jsonNode!.AsObject();

            JsonNode? itemsNode = fixture["Items"];
            Assert.NotNull(itemsNode);

            JsonArray items = itemsNode.AsArray();
            Assert.Equal(500, items.Count);

            JsonNode? itemCountNode = fixture["ItemCount"];
            Assert.NotNull(itemCountNode);
            Assert.Equal(itemCountNode.GetValue<int>(), items.Count);

            for (int index = 0; index < items.Count; index++)
            {
                JsonNode? itemNode = items[index];
                Assert.NotNull(itemNode);

                JsonNode? ordinalNode = itemNode!["Ordinal"];
                Assert.NotNull(ordinalNode);
                Assert.Equal(index, ordinalNode.GetValue<int>());
            }

            return fixture;
        }
    }
}
