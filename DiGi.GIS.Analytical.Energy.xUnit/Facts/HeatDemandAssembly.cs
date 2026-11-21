using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Classes;
using DiGi.Core.Parameter.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Energy.Classes;
using DiGi.GIS.Analytical.Energy.Enums;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.Analytical.Solar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.Weather;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Regulation = DiGi.Analytical.Building.Regulation;

namespace DiGi.GIS.Analytical.Energy.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The EPSG:2180 position of the fixtures: central Warsaw, where the IMGW weather fixture's station is.
        /// </summary>
        private static readonly Point2D heatDemandAssembly_Origin = new(638000, 486000);

        /// <summary>
        /// Tests <see cref="Create.MonthlyClimate"/> on a split building (DiGi-Organization/DiGi.GIS.Analytical#14).
        /// <para>The neighbour stands against the middle of the north wall of both storeys, so the split leaves two external pieces of that wall per storey: the first keeps the wall's identifier and its stored surface, the second is new and must be matched to the same stored surface - both get exactly the irradiation of the stored north wall. The stored result leaves one east wall out, which takes the unshaded irradiation of its orientation. The shared pieces are party walls and get none.</para>
        /// </summary>
        [MediumFact]
        public void MonthlyClimate_SplitWalls()
        {
            (BuildingModel buildingModel, BuildingModel buildingModel_Neighbour, Building2D _) = HeatDemandAssembly_Fixture();
            EPWFile ePWFile = HeatDemandAssembly_EPWFile();
            BuildingInformation buildingInformation = buildingModel.BuildingInformation;

            (BuildingSolarRadiationResult buildingSolarRadiationResult, Guid guid_Dropped) = HeatDemandAssembly_SolarResult(buildingModel, ePWFile);

            // An isolated copy, so the split leaves the fixture's model untouched (DiGi-Organization/DiGi.Core#14).
            BuildingModel? buildingModel_Split = Core.Query.Clone(buildingModel);
            Assert.NotNull(buildingModel_Split);
            List<PartyWallResult>? partyWallResults = DiGi.Analytical.Building.Query.PartyWalls(buildingModel_Split, [buildingModel_Neighbour]);
            Assert.NotNull(partyWallResults);
            Assert.True(DiGi.Analytical.Building.Modify.SplitPartyWalls(buildingModel_Split, partyWallResults));

            DiGi.Analytical.Building.Energy.Classes.MonthlyClimate? monthlyClimate = buildingModel_Split.MonthlyClimate(buildingModel, buildingInformation, buildingSolarRadiationResult, ePWFile, Regulation.Enums.ClimateZone.III, out int matchedSurfaceCount, out int unshadedSurfaceCount);
            Assert.NotNull(monthlyClimate);
            Assert.Equal(2, matchedSurfaceCount);
            Assert.Equal(1, unshadedSurfaceCount);
            Assert.Equal(8, buildingModel.GetComponents<IWall>()?.Count);

            Assert.Equal(ePWFile.MonthlyMeanDryBulbTemperatures(), monthlyClimate.ExternalTemperatures);
            Assert.Equal(-20.0, monthlyClimate.DesignExternalTemperature);
            Assert.Equal(7.6, monthlyClimate.AnnualMeanExternalTemperature);

            SolarHourSeries? solarHourSeries = ((Weather.Classes.Weather)ePWFile).SolarHourSeries(buildingInformation.Coordinates, buildingInformation.UTC);
            Assert.NotNull(solarHourSeries);

            Dictionary<string, List<double>>? surfaceIrradiations = monthlyClimate.SurfaceIrradiations;
            Assert.NotNull(surfaceIrradiations);

            // The stored north walls, one per storey, by the elevation of their base.
            Dictionary<long, SurfaceSolarRadiation> surfaceSolarRadiations_North = [];
            foreach (IWall wall in buildingModel.GetComponents<IWall>() ?? [])
            {
                SurfaceSolarRadiation? surfaceSolarRadiation = buildingSolarRadiationResult.SurfaceSolarRadiations?.Find(x => x.Reference == new GuidReference(wall).ToString());
                if (surfaceSolarRadiation?.Normal is Vector3D normal && normal.Y > 0.9)
                {
                    surfaceSolarRadiations_North[(long)Math.Round(wall.GetBoundingBox()!.Min.Z)] = surfaceSolarRadiation;
                }
            }

            Assert.Equal(2, surfaceSolarRadiations_North.Count);

            int count_NorthPieces = 0;
            int count_Party = 0;
            foreach (IWall wall in buildingModel_Split.GetComponents<IWall>() ?? [])
            {
                string key = wall.Guid.ToString("D");
                if (wall is Component component && component.TryGetValue(new EnumParameterDefinition(ComponentParameter.PartyWall), out Guid _))
                {
                    count_Party++;
                    Assert.False(surfaceIrradiations.ContainsKey(key));
                    continue;
                }

                Assert.True(surfaceIrradiations.ContainsKey(key), $"Wall {key} has no irradiation.");

                BoundingBox3D boundingBox3D = wall.GetBoundingBox()!;
                bool north = Math.Abs(boundingBox3D.Min.Y - (heatDemandAssembly_Origin.Y + 10)) < 1e-6 && Math.Abs(boundingBox3D.Max.Y - (heatDemandAssembly_Origin.Y + 10)) < 1e-6;
                if (north)
                {
                    count_NorthPieces++;
                    double[]? monthlyIrradiations = solarHourSeries.MonthlyIrradiations(surfaceSolarRadiations_North[(long)Math.Round(boundingBox3D.Min.Z)]);
                    Assert.NotNull(monthlyIrradiations);
                    Assert.Equal(monthlyIrradiations, surfaceIrradiations[key]);
                }

                if (wall.Guid == guid_Dropped)
                {
                    double[]? monthlyIrradiations_Unshaded = solarHourSeries.MonthlyIrradiations(1.0, new Vector3D(1, 0, 0), 1.0, 1.0, null);
                    Assert.NotNull(monthlyIrradiations_Unshaded);
                    Assert.Equal(monthlyIrradiations_Unshaded, surfaceIrradiations[key]);
                }
            }

            Assert.Equal(4, count_NorthPieces);
            Assert.Equal(2, count_Party);

            // Without a solar result every surface is unshaded.
            Assert.NotNull(buildingModel_Split.MonthlyClimate(buildingModel, buildingInformation, null, ePWFile, Regulation.Enums.ClimateZone.III, out int matchedSurfaceCount_None, out int unshadedSurfaceCount_None));
            Assert.Equal(0, matchedSurfaceCount_None);
            Assert.Equal(surfaceIrradiations.Count, unshadedSurfaceCount_None);

            Assert.Null(((BuildingModel?)null).MonthlyClimate(buildingModel, buildingInformation, null, ePWFile, Regulation.Enums.ClimateZone.III, out _, out _));
            Assert.Null(buildingModel_Split.MonthlyClimate(buildingModel, buildingInformation, null, null, Regulation.Enums.ClimateZone.III, out _, out _));
        }

        /// <summary>
        /// Tests <see cref="Create.BuildingHeatDemandResult"/> end to end on the fixture building (DiGi-Organization/DiGi.GIS.Analytical#14): party walls split, openings, constructions and internal conditions assigned, the climate assembled from the stored solar result, the demand calculated, and the provenance recorded; the stored model is left as it was.
        /// <para>Against the same building without its neighbour, the party walls remove heat-loss area, so H_D is smaller with it - a differential that fails if the split or the adiabatic rule is lost.</para>
        /// </summary>
        [MediumFact]
        public void BuildingHeatDemandResult_EndToEnd()
        {
            (BuildingModel buildingModel, BuildingModel buildingModel_Neighbour, Building2D building2D) = HeatDemandAssembly_Fixture();
            EPWFile ePWFile = HeatDemandAssembly_EPWFile();
            (BuildingSolarRadiationResult buildingSolarRadiationResult, Guid _) = HeatDemandAssembly_SolarResult(buildingModel, ePWFile);

            HeatDemandInput heatDemandInput = new(new OpeningGenerationInput(1975, YearBuiltSource.User, null, Regulation.Enums.ClimateZone.III, null, null), new OccupancyData(building2D.Reference, null, 6), "test", 1465, WeatherSourceType.EPW, "16");
            HeatDemandContext heatDemandContext = new(Regulation.Query.EmbeddedConstructionLibrary_PL(), null, null, null);

            BuildingHeatDemandResult? buildingHeatDemandResult = buildingModel.BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], buildingSolarRadiationResult, ePWFile, heatDemandContext);
            Assert.NotNull(buildingHeatDemandResult);
            Assert.Equal(HeatDemandInputVariant.Generated, buildingHeatDemandResult.HeatDemandInputVariant);

            DiGi.Analytical.Building.Energy.Classes.HeatDemandResult? heatDemandResult = buildingHeatDemandResult.HeatDemandResult;
            Assert.NotNull(heatDemandResult);
            Assert.Equal(DiGi.Analytical.Building.Energy.Enums.HeatDemandMethod.Monthly, heatDemandResult.HeatDemandMethod);
            Assert.Equal(12, heatDemandResult.HeatingEnergyNeeds?.Count);
            Assert.True(heatDemandResult.HeatingEnergyNeed > 0);
            Assert.True(heatDemandResult.DesignHeatLoad > 0);
            Assert.Empty(heatDemandResult.ExcludedComponents ?? ["missing"]);
            Assert.Equal(0, heatDemandResult.MissingIrradiationCount);
            Assert.True(heatDemandResult.SolarHeatGains!.Sum() > 0, "No solar gains: the openings or the irradiation are missing.");
            Assert.True(heatDemandResult.GroundHeatTransferCoefficient > 0);

            Assert.Equal(building2D.Reference, buildingHeatDemandResult.Reference);
            Assert.Equal(1465, buildingHeatDemandResult.CountyId);
            Assert.Equal(HeatDemandSolarSource.Shaded, buildingHeatDemandResult.SolarSource);
            Assert.Equal(WeatherSourceType.EPW, buildingHeatDemandResult.WeatherSourceType);
            Assert.Equal("16", buildingHeatDemandResult.WeatherSourceId);
            Assert.Equal(2, buildingHeatDemandResult.MatchedSurfaceCount);
            Assert.Equal(1, buildingHeatDemandResult.UnshadedSurfaceCount);
            Assert.Equal(Regulation.Enums.ConstructionFlavour.TypicalAsBuilt, buildingHeatDemandResult.ConstructionFlavour);
            Assert.False(string.IsNullOrWhiteSpace(buildingHeatDemandResult.ConstructionLibraryVersion));
            Assert.False(string.IsNullOrWhiteSpace(buildingHeatDemandResult.ConstructionSetSource));
            Assert.Equal((short)1975, buildingHeatDemandResult.YearBuilt);
            Assert.False(string.IsNullOrWhiteSpace(buildingHeatDemandResult.UsageSource));

            // The stored model is untouched: no openings, no constructions.
            Assert.Empty(buildingModel.GetOpenings<IOpening>() ?? []);
            Assert.All(buildingModel.GetComponents<IWall>() ?? [], x => Assert.Null(buildingModel.GetWallConstruction(x)));

            BuildingHeatDemandResult? buildingHeatDemandResult_Alone = buildingModel.BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, null, buildingSolarRadiationResult, ePWFile, heatDemandContext);
            Assert.NotNull(buildingHeatDemandResult_Alone?.HeatDemandResult);
            Assert.True(heatDemandResult.DirectHeatTransferCoefficient < buildingHeatDemandResult_Alone.HeatDemandResult.DirectHeatTransferCoefficient);
            Assert.Equal(0, buildingHeatDemandResult_Alone.MatchedSurfaceCount);

            Assert.Null(buildingModel.BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, null, buildingSolarRadiationResult, null, heatDemandContext));
        }

        /// <summary>
        /// Tests the serialization and copying of <see cref="HeatDemandInput"/> and <see cref="BuildingHeatDemandResult"/> (DiGi-Organization/DiGi.GIS.Analytical#14): a string round trip, <see cref="Core.xUnit.Query.SerializationCheck"/> and a member-by-member <c>new T(source)</c> assertion. The result's <see cref="BuildingHeatDemandResult.HeatDemandInputVariant"/> is read from the JSON and copied, and a JSON without it reads as <see cref="HeatDemandInputVariant.Generated"/> (DiGi-Organization/DiGi.GIS.Analytical#20).
        /// </summary>
        [Fact]
        public void HeatDemandAssembly_Serialization()
        {
            HeatDemandInput heatDemandInput = new(new OpeningGenerationInput(1975, YearBuiltSource.Prediction, new Point2D(5, -3), Regulation.Enums.ClimateZone.IV, "OWT-67", null), new OccupancyData("REF", 240, 8), "occupancy_data_building_2d", 1465, WeatherSourceType.EPW, "16");
            HeatDemandInput heatDemandInput_Copy = new(heatDemandInput);
            Assert.Equal(Core.Convert.ToSystem_String(heatDemandInput.OpeningGenerationInput), Core.Convert.ToSystem_String(heatDemandInput_Copy.OpeningGenerationInput));
            Assert.Equal(heatDemandInput.OccupancyData?.Occupancy, heatDemandInput_Copy.OccupancyData?.Occupancy);
            Assert.Equal(heatDemandInput.OccupancySource, heatDemandInput_Copy.OccupancySource);
            Assert.Equal(heatDemandInput.CountyId, heatDemandInput_Copy.CountyId);
            Assert.Equal(heatDemandInput.WeatherSourceType, heatDemandInput_Copy.WeatherSourceType);
            Assert.Equal(heatDemandInput.WeatherSourceId, heatDemandInput_Copy.WeatherSourceId);
            Core.xUnit.Query.SerializationCheck(heatDemandInput);

            HeatDemandInput? heatDemandInput_Json = Core.Convert.ToDiGi<HeatDemandInput>(Core.Convert.ToSystem_String(heatDemandInput))?.FirstOrDefault();
            Assert.NotNull(heatDemandInput_Json);
            Assert.Equal(Regulation.Enums.ClimateZone.IV, heatDemandInput_Json.OpeningGenerationInput?.ClimateZone);
            Assert.Equal((uint?)8, heatDemandInput_Json.OccupancyData?.Occupancy);

            DiGi.Analytical.Building.Energy.Classes.HeatDemandResult heatDemandResult = new(1000, 50, [100, 90, 80, 60, 30, 10, 5, 5, 20, 50, 80, 100], null, null, null, null, null, null, null, 100, 20, 30, 0, 40, 1e7, 50, 20, 60, 20, 5000, 4000, 1000, "method", DiGi.Analytical.Building.Energy.Enums.HeatDemandMethod.Monthly, 1, new Dictionary<string, string>() { ["Method"] = "method" }, ["guid"], 1, 2);
            BuildingHeatDemandResult buildingHeatDemandResult = new("REF", 1465, 42, WeatherSourceType.EPW, "16", HeatDemandSolarSource.Shaded, 1, 50.0, 2, 1, Regulation.Enums.ConstructionFlavour.Regulation, "1.7.1", "TABULA", Regulation.Enums.BuildingType.SingleFamily, 1975, YearBuiltSource.User, "PL-376", DiGi.GIS.Analytical.Energy.Enums.HeatDemandInputVariant.Generated, new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero), heatDemandResult);

            BuildingHeatDemandResult buildingHeatDemandResult_Copy = new(buildingHeatDemandResult);
            Assert.Equal(buildingHeatDemandResult.Reference, buildingHeatDemandResult_Copy.Reference);
            Assert.Equal(buildingHeatDemandResult.CountyId, buildingHeatDemandResult_Copy.CountyId);
            Assert.Equal(buildingHeatDemandResult.BuildingModelId, buildingHeatDemandResult_Copy.BuildingModelId);
            Assert.Equal(buildingHeatDemandResult.WeatherSourceType, buildingHeatDemandResult_Copy.WeatherSourceType);
            Assert.Equal(buildingHeatDemandResult.WeatherSourceId, buildingHeatDemandResult_Copy.WeatherSourceId);
            Assert.Equal(buildingHeatDemandResult.SolarSource, buildingHeatDemandResult_Copy.SolarSource);
            Assert.Equal(buildingHeatDemandResult.SolarSolverVersion, buildingHeatDemandResult_Copy.SolarSolverVersion);
            Assert.Equal(buildingHeatDemandResult.SolarRadius, buildingHeatDemandResult_Copy.SolarRadius);
            Assert.Equal(buildingHeatDemandResult.MatchedSurfaceCount, buildingHeatDemandResult_Copy.MatchedSurfaceCount);
            Assert.Equal(buildingHeatDemandResult.UnshadedSurfaceCount, buildingHeatDemandResult_Copy.UnshadedSurfaceCount);
            Assert.Equal(buildingHeatDemandResult.ConstructionFlavour, buildingHeatDemandResult_Copy.ConstructionFlavour);
            Assert.Equal(buildingHeatDemandResult.ConstructionLibraryVersion, buildingHeatDemandResult_Copy.ConstructionLibraryVersion);
            Assert.Equal(buildingHeatDemandResult.ConstructionSetSource, buildingHeatDemandResult_Copy.ConstructionSetSource);
            Assert.Equal(buildingHeatDemandResult.BuildingType, buildingHeatDemandResult_Copy.BuildingType);
            Assert.Equal(buildingHeatDemandResult.YearBuilt, buildingHeatDemandResult_Copy.YearBuilt);
            Assert.Equal(buildingHeatDemandResult.YearBuiltSource, buildingHeatDemandResult_Copy.YearBuiltSource);
            Assert.Equal(buildingHeatDemandResult.UsageSource, buildingHeatDemandResult_Copy.UsageSource);
            Assert.Equal(buildingHeatDemandResult.HeatDemandInputVariant, buildingHeatDemandResult_Copy.HeatDemandInputVariant);
            Assert.Equal(buildingHeatDemandResult.CreatedAt, buildingHeatDemandResult_Copy.CreatedAt);
            Assert.Equal(buildingHeatDemandResult.HeatDemandResult?.HeatingEnergyNeeds, buildingHeatDemandResult_Copy.HeatDemandResult?.HeatingEnergyNeeds);
            Core.xUnit.Query.SerializationCheck(buildingHeatDemandResult);

            BuildingHeatDemandResult? buildingHeatDemandResult_Json = Core.Convert.ToDiGi<BuildingHeatDemandResult>(Core.Convert.ToSystem_String(buildingHeatDemandResult))?.FirstOrDefault();
            Assert.NotNull(buildingHeatDemandResult_Json);
            Assert.Equal(HeatDemandSolarSource.Shaded, buildingHeatDemandResult_Json.SolarSource);
            Assert.Equal(50.0, buildingHeatDemandResult_Json.SolarRadius);
            Assert.Equal(1000, buildingHeatDemandResult_Json.HeatDemandResult?.HeatingEnergyNeed);
            Assert.Equal(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero), buildingHeatDemandResult_Json.CreatedAt);
            Assert.Equal(HeatDemandInputVariant.Generated, buildingHeatDemandResult_Json.HeatDemandInputVariant);

            // The input variant is read from the JSON (DiGi-Organization/DiGi.GIS.Analytical#20): a stored value wins, and
            // a result serialized before the input variant was recorded - no member - reads as Generated, the only one there was.
            JsonObject jsonObject = JsonNode.Parse(Core.Convert.ToSystem_String(buildingHeatDemandResult)!)!.AsObject();
            Assert.True(jsonObject.ContainsKey(nameof(BuildingHeatDemandResult.HeatDemandInputVariant)));
            jsonObject[nameof(BuildingHeatDemandResult.HeatDemandInputVariant)] = jsonObject[nameof(BuildingHeatDemandResult.HeatDemandInputVariant)]!.GetValueKind() == JsonValueKind.String ? nameof(HeatDemandInputVariant.Undefined) : (int)HeatDemandInputVariant.Undefined;
            BuildingHeatDemandResult buildingHeatDemandResult_Undefined = new(jsonObject);
            Assert.Equal(HeatDemandInputVariant.Undefined, buildingHeatDemandResult_Undefined.HeatDemandInputVariant);
            Assert.Equal(HeatDemandInputVariant.Undefined, new BuildingHeatDemandResult(buildingHeatDemandResult_Undefined).HeatDemandInputVariant);

            Assert.True(jsonObject.Remove(nameof(BuildingHeatDemandResult.HeatDemandInputVariant)));
            BuildingHeatDemandResult buildingHeatDemandResult_Legacy = new(jsonObject);
            Assert.Equal(HeatDemandInputVariant.Generated, buildingHeatDemandResult_Legacy.HeatDemandInputVariant);
            Assert.Equal(1000, buildingHeatDemandResult_Legacy.HeatDemandResult?.HeatingEnergyNeed);
        }

        /// <summary>
        /// Builds the fixture: a 12 × 10 m building of two 3 m storeys in central Warsaw, stamped with its coordinates, its 2D building (single-family), and a 4 × 6 m two-storey neighbour against the middle of its north wall.
        /// </summary>
        /// <returns>The building model, the neighbour's model and the 2D building.</returns>
        private static (BuildingModel BuildingModel, BuildingModel BuildingModel_Neighbour, Building2D Building2D) HeatDemandAssembly_Fixture()
        {
            static BuildingModel Model(double xMin, double yMin, double xMax, double yMax)
            {
                Point2D[] point2Ds = [new(heatDemandAssembly_Origin.X + xMin, heatDemandAssembly_Origin.Y + yMin), new(heatDemandAssembly_Origin.X + xMax, heatDemandAssembly_Origin.Y + yMin), new(heatDemandAssembly_Origin.X + xMax, heatDemandAssembly_Origin.Y + yMax), new(heatDemandAssembly_Origin.X + xMin, heatDemandAssembly_Origin.Y + yMax)];
                PolygonalFace3D? polygonalFace3D = Geometry.Spatial.Create.PolygonalFace3D(Geometry.Spatial.Create.Plane(0.0)!, point2Ds);
                Assert.NotNull(polygonalFace3D);

                BuildingModel? buildingModel = DiGi.GIS.Analytical.Create.BuildingModel(polygonalFace3D, 2, 3.0);
                Assert.NotNull(buildingModel);
                Assert.True(DiGi.GIS.Analytical.Modify.UpdateBuildingInformation(buildingModel));
                return buildingModel;
            }

            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(new Point2D(heatDemandAssembly_Origin.X, heatDemandAssembly_Origin.Y), new Point2D(heatDemandAssembly_Origin.X + 12, heatDemandAssembly_Origin.Y), new Point2D(heatDemandAssembly_Origin.X + 12, heatDemandAssembly_Origin.Y + 10), new Point2D(heatDemandAssembly_Origin.X, heatDemandAssembly_Origin.Y + 10));
            Assert.NotNull(polygonalFace2D);
            Building2D building2D = new(Guid.NewGuid(), "B7", polygonalFace2D, 2, null, BuildingGeneralFunction.residential_buildings, [BuildingSpecificFunction.single_family_building]);

            return (Model(0, 0, 12, 10), Model(4, 10, 8, 16), building2D);
        }

        /// <summary>
        /// Reads the Warsaw IMGW weather fixture.
        /// </summary>
        /// <returns>The EPW file.</returns>
        private static EPWFile HeatDemandAssembly_EPWFile()
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.False(string.IsNullOrWhiteSpace(path));

            EPWFile? ePWFile = EPW.Modify.Read(path);
            Assert.NotNull(ePWFile);
            return ePWFile;
        }

        /// <summary>
        /// Builds a stored solar result of the unsplit fixture building without a solve: every receiving surface of <see cref="Solar.Query.SolarReceiverNormals"/> with full visibilities, the north walls shaded by half in every hour and the others unshaded, except one east wall, which is left out.
        /// </summary>
        /// <param name="buildingModel">The unsplit building model.</param>
        /// <param name="ePWFile">The weather.</param>
        /// <returns>The result and the identifier of the east wall left out.</returns>
        private static (BuildingSolarRadiationResult BuildingSolarRadiationResult, Guid Guid_Dropped) HeatDemandAssembly_SolarResult(BuildingModel buildingModel, EPWFile ePWFile)
        {
            BuildingInformation buildingInformation = buildingModel.BuildingInformation;
            SolarHourSeries? solarHourSeries = ((Weather.Classes.Weather)ePWFile).SolarHourSeries(buildingInformation.Coordinates, buildingInformation.UTC);
            Assert.NotNull(solarHourSeries);

            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            Guid guid_Dropped = Guid.Empty;
            List<IWall> walls_East = (buildingModel.GetComponents<IWall>() ?? []).FindAll(x => normals.TryGetValue(new GuidReference(x).ToString()!, out Vector3D? normal) && normal.X > 0.9);
            walls_East.Sort((x, y) => x.GetBoundingBox()!.Min.Z.CompareTo(y.GetBoundingBox()!.Min.Z));
            Assert.NotEmpty(walls_East);
            guid_Dropped = walls_East[0].Guid;
            string reference_Dropped = new GuidReference(walls_East[0]).ToString()!;

            List<SurfaceSolarRadiation> surfaceSolarRadiations = [];
            foreach (KeyValuePair<string, Vector3D> keyValuePair in normals)
            {
                if (keyValuePair.Key == reference_Dropped)
                {
                    continue;
                }

                double shadingFactor = keyValuePair.Value.Y > 0.9 ? 0.5 : 0.0;
                SurfaceSolarRadiation? surfaceSolarRadiation = solarHourSeries.SurfaceSolarRadiation(keyValuePair.Key, 10.0, keyValuePair.Value, 1.0, 1.0, Enumerable.Repeat(shadingFactor, Solar.Constants.Default.SolarReferenceHourCount).ToList());
                Assert.NotNull(surfaceSolarRadiation);
                surfaceSolarRadiations.Add(surfaceSolarRadiation);
            }

            return (new BuildingSolarRadiationResult("B7", 1465, 1, 50, Solar.Constants.Default.SolarSolverVersion, WeatherSourceType.EPW, "16", null, null, null, null, surfaceSolarRadiations), guid_Dropped);
        }
    }
}
