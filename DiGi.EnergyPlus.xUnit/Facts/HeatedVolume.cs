using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Energy;
using DiGi.Analytical.Building.Energy.Classes;
using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Core.Interfaces;
using DiGi.GIS.Analytical.Energy.Classes;
using DiGi.Geometry.Spatial.Classes;
using System.Globalization;
using System.Reflection;

namespace DiGi.EnergyPlus.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that sample 53477_2 of <c>files/EnergyPlus_Samples.zip</c> gives a heated volume inside its own bounding box (DiGi-Organization/DiGi.Analytical#22): the terraced house whose shell the far plane origin of its floor turned into 215 017 m3.
        /// <para>The guards pin the sample as the one the defect was found on: its model's 591.695 m3 bounding box (the 13 x 15 x 3 m of the issue, to the millimetre), its 112.343109 m2 of reference floor area, and the captured volume source - the residual bound rejects this shell outright, so the result is the recorded footprint area times height (337 m3), where the defect read 215 017 m3. Both the captured and the recomputed volume must lie inside the box and at no more than 10 m3 per m2, the figure the defect exceeded by two orders of magnitude. The sample needs no EnergyPlus executable: it is a DiGi-side check.</para>
        /// </summary>
        [Fact]
        public void HeatedVolume_Sample53477_2()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? path = Core.xUnit.Query.FilePath(assembly, "EnergyPlus_Samples.zip");
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path));

            string? text = null;
            using (System.IO.Compression.ZipArchive zipArchive = System.IO.Compression.ZipFile.OpenRead(path))
            {
                System.IO.Compression.ZipArchiveEntry? zipArchiveEntry = zipArchive.GetEntry("EnergyPlus_Sample_53477_2.json");
                Assert.NotNull(zipArchiveEntry);
                using StreamReader streamReader = new(zipArchiveEntry.Open());
                text = streamReader.ReadToEnd();
            }

            List<ISerializableObject>? serializableObjects = Core.Convert.ToDiGi<ISerializableObject>(text);
            Assert.NotNull(serializableObjects);

            HeatDemandPreparationResult heatDemandPreparationResult = serializableObjects.OfType<HeatDemandPreparationResult>().Single();
            BuildingHeatDemandResult buildingHeatDemandResult = serializableObjects.OfType<BuildingHeatDemandResult>().Single();

            BuildingModel? buildingModel = heatDemandPreparationResult.BuildingModel;
            MonthlyClimate? monthlyClimate = heatDemandPreparationResult.MonthlyClimate;
            TypologyData? typologyData = heatDemandPreparationResult.OpeningGenerationResult?.TypologyData;
            Assert.NotNull(buildingModel);
            Assert.NotNull(monthlyClimate);

            BoundingBox3D? boundingBox3D = buildingModel.GetBoundingBox();
            Assert.NotNull(boundingBox3D);

            // Guard: this is the terraced house of the defect - the 591.695 m3 box and the 112.343109 m2 of floor its capture carries.
            Assert.Equal(591.69479999942405, boundingBox3D.GetVolume(), 3);
            HeatDemandResult? heatDemandResult_Stored = buildingHeatDemandResult.HeatDemandResult;
            Assert.NotNull(heatDemandResult_Stored);
            Assert.Equal(112.34310913085938, heatDemandResult_Stored.ReferenceFloorArea, 6);

            // Guard: the capture records the fallback, because the residual bound rejects this shell - the outcome of the fix, not of the old area screen.
            Dictionary<string, string>? sources = heatDemandResult_Stored.Sources;
            Assert.NotNull(sources);
            Assert.True(sources.TryGetValue("HeatedVolume", out string? source));
            Assert.Equal("footprint area times height (external shell open)", source);

            AssertInsideBoundingBox(heatDemandResult_Stored, boundingBox3D);

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(monthlyClimate, typologyData);
            Assert.NotNull(heatDemandResult);
            AssertInsideBoundingBox(heatDemandResult, boundingBox3D);
        }

        /// <summary>
        /// Asserts that a heat-demand result's heated volume is positive, no larger than the given bounding box and no more than 10 m3 per m2 of reference floor area - the three properties sample 53477_2 violated before the fix (DiGi-Organization/DiGi.Analytical#22).
        /// </summary>
        /// <param name="heatDemandResult">The result to check. This value can be null.</param>
        /// <param name="boundingBox3D">The bounding box of the model the result was computed from.</param>
        private static void AssertInsideBoundingBox(HeatDemandResult? heatDemandResult, BoundingBox3D boundingBox3D)
        {
            Assert.NotNull(heatDemandResult);

            double volume = heatDemandResult.HeatedVolume;
            Assert.True(volume > 0, "The engine gave no heated volume for sample 53477_2.");
            Assert.True(volume <= boundingBox3D.GetVolume(), string.Format(CultureInfo.InvariantCulture, "The heated volume {0} m3 is outside the bounding box of {1} m3.", volume, boundingBox3D.GetVolume()));
            Assert.True(volume <= (10.0 * heatDemandResult.ReferenceFloorArea), string.Format(CultureInfo.InvariantCulture, "The heated volume {0} m3 is more than 10 m3 per m2 of the {1} m2 of floor.", volume, heatDemandResult.ReferenceFloorArea));
        }
    }
}
