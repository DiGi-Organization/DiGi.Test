using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Parameter.Classes;
using DiGi.EPW.Classes;
using DiGi.GIS.Analytical.Energy.Classes;
using DiGi.GIS.Analytical.Energy.Enums;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.Analytical.Solar.Classes;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using DiGi.Weather;
using System;
using System.Collections.Generic;
using Regulation = DiGi.Analytical.Building.Regulation;

namespace DiGi.GIS.Analytical.Energy.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Proves that <see cref="Create.BuildingHeatDemandResult"/> still gives exactly what the pipeline before the preparation split gave (DiGi-Organization/DiGi.GIS.Analytical#15): <see cref="HeatDemandAssembly_HeatDemandResult_PreRefactor"/> reproduces the method as it was - copy, party-wall split, openings and envelope constructions, internal conditions, climate zone, monthly climate, engine, provenance - and both results are compared as their serialized text, which covers every monthly value, every coefficient and every provenance member.
        /// <para>The helper was proven green against the unmodified method first; refactoring the method onto <c>Create.HeatDemandPreparationResult</c> must keep this fact green. The stored model is left untouched by both paths, which the comparison also covers.</para>
        /// </summary>
        [MediumFact]
        public void BuildingHeatDemandResult_Parity()
        {
            (BuildingModel buildingModel, BuildingModel buildingModel_Neighbour, Building2D building2D) = HeatDemandAssembly_Fixture();
            EPWFile ePWFile = HeatDemandAssembly_EPWFile();
            (BuildingSolarRadiationResult buildingSolarRadiationResult, Guid _) = HeatDemandAssembly_SolarResult(buildingModel, ePWFile);

            HeatDemandInput heatDemandInput = new(new OpeningGenerationInput(1975, YearBuiltSource.User, null, Regulation.Enums.ClimateZone.III, null, null), new OccupancyData(building2D.Reference, null, 6), "test", 1465, WeatherSourceType.EPW, "16");
            HeatDemandContext heatDemandContext = new(Regulation.Query.EmbeddedConstructionLibrary_PL(), null, null, null);

            BuildingHeatDemandResult? buildingHeatDemandResult = buildingModel.BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], buildingSolarRadiationResult, ePWFile, heatDemandContext);
            Assert.NotNull(buildingHeatDemandResult);
            Assert.NotNull(buildingHeatDemandResult.HeatDemandResult);

            BuildingHeatDemandResult? buildingHeatDemandResult_PreRefactor = HeatDemandAssembly_HeatDemandResult_PreRefactor(buildingModel, building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], buildingSolarRadiationResult, ePWFile, heatDemandContext);
            Assert.NotNull(buildingHeatDemandResult_PreRefactor);
            Assert.NotNull(buildingHeatDemandResult_PreRefactor.HeatDemandResult);

            // One comparison covering every monthly value, coefficient and provenance member.
            Assert.Equal(Core.Convert.ToSystem_String(buildingHeatDemandResult_PreRefactor), Core.Convert.ToSystem_String(buildingHeatDemandResult));

            // Readable spot checks, so a failure is diagnosable without diffing the text.
            Assert.Equal(buildingHeatDemandResult_PreRefactor.HeatDemandResult?.HeatingEnergyNeeds, buildingHeatDemandResult.HeatDemandResult?.HeatingEnergyNeeds);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.HeatDemandResult?.DesignHeatLoad, buildingHeatDemandResult.HeatDemandResult?.DesignHeatLoad);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.Reference, buildingHeatDemandResult.Reference);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.CountyId, buildingHeatDemandResult.CountyId);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.BuildingModelId, buildingHeatDemandResult.BuildingModelId);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.MatchedSurfaceCount, buildingHeatDemandResult.MatchedSurfaceCount);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.UnshadedSurfaceCount, buildingHeatDemandResult.UnshadedSurfaceCount);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.YearBuilt, buildingHeatDemandResult.YearBuilt);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.YearBuiltSource, buildingHeatDemandResult.YearBuiltSource);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.UsageSource, buildingHeatDemandResult.UsageSource);
            Assert.Equal(buildingHeatDemandResult_PreRefactor.ConstructionLibraryVersion, buildingHeatDemandResult.ConstructionLibraryVersion);

            // The stored model is untouched by either path.
            Assert.Empty(buildingModel.GetOpenings<IOpening>() ?? []);
            Assert.All(buildingModel.GetComponents<IWall>() ?? [], x => Assert.Null(buildingModel.GetWallConstruction(x)));
        }

        /// <summary>
        /// Tests <see cref="Create.HeatDemandPreparationResult"/> end to end on the fixture building (DiGi-Organization/DiGi.GIS.Analytical#15): the prepared model carries the split party walls, the generated openings and the internal conditions; the climate, the zone, the counts, the usage source and the construction-library version are the ones <see cref="Create.BuildingHeatDemandResult"/> records; the engine run on the preparation gives exactly that result; and the stored model passed in is left untouched.
        /// </summary>
        [MediumFact]
        public void HeatDemandPreparationResult_EndToEnd()
        {
            (BuildingModel buildingModel, BuildingModel buildingModel_Neighbour, Building2D building2D) = HeatDemandAssembly_Fixture();
            EPWFile ePWFile = HeatDemandAssembly_EPWFile();
            (BuildingSolarRadiationResult buildingSolarRadiationResult, Guid _) = HeatDemandAssembly_SolarResult(buildingModel, ePWFile);

            HeatDemandInput heatDemandInput = new(new OpeningGenerationInput(1975, YearBuiltSource.User, null, Regulation.Enums.ClimateZone.III, null, null), new OccupancyData(building2D.Reference, null, 6), "test", 1465, WeatherSourceType.EPW, "16");
            HeatDemandContext heatDemandContext = new(Regulation.Query.EmbeddedConstructionLibrary_PL(), null, null, null);

            HeatDemandPreparationResult? heatDemandPreparationResult = buildingModel.HeatDemandPreparationResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], buildingSolarRadiationResult, ePWFile, heatDemandContext);
            Assert.NotNull(heatDemandPreparationResult);

            Assert.Equal(Regulation.Enums.ClimateZone.III, heatDemandPreparationResult.ClimateZone);
            Assert.Equal(2, heatDemandPreparationResult.MatchedSurfaceCount);
            Assert.Equal(1, heatDemandPreparationResult.UnshadedSurfaceCount);
            Assert.Equal(heatDemandContext.ConstructionLibrary?.Version, heatDemandPreparationResult.ConstructionLibraryVersion);

            OpeningGenerationResult? openingGenerationResult = heatDemandPreparationResult.OpeningGenerationResult;
            Assert.NotNull(openingGenerationResult);
            Assert.Equal((short)1975, openingGenerationResult.YearBuilt);
            Assert.Equal(YearBuiltSource.User, openingGenerationResult.YearBuiltSource);
            Assert.Equal(Regulation.Enums.ConstructionFlavour.TypicalAsBuilt, openingGenerationResult.ConstructionFlavour);
            Assert.NotNull(openingGenerationResult.TypologyData);

            DiGi.Analytical.Building.Energy.Classes.MonthlyClimate? monthlyClimate = heatDemandPreparationResult.MonthlyClimate;
            Assert.NotNull(monthlyClimate);
            Assert.Equal(ePWFile.MonthlyMeanDryBulbTemperatures(), monthlyClimate.ExternalTemperatures);
            Assert.Equal(-20.0, monthlyClimate.DesignExternalTemperature);
            Assert.Equal(7.6, monthlyClimate.AnnualMeanExternalTemperature);
            Dictionary<string, List<double>>? surfaceIrradiations = monthlyClimate.SurfaceIrradiations;
            Assert.NotNull(surfaceIrradiations);
            Assert.NotEmpty(surfaceIrradiations);
            Assert.False(string.IsNullOrWhiteSpace(monthlyClimate.Source));

            // The prepared model: split party walls, generated openings, constructions, internal conditions.
            BuildingModel? buildingModel_Prepared = heatDemandPreparationResult.BuildingModel;
            Assert.NotNull(buildingModel_Prepared);
            Assert.NotSame(buildingModel, buildingModel_Prepared);

            List<IWall> walls = buildingModel_Prepared.GetComponents<IWall>() ?? [];
            Assert.Equal(2, walls.FindAll(x => x is Component component && component.TryGetValue(new EnumParameterDefinition(ComponentParameter.PartyWall), out Guid _)).Count);
            Assert.All(walls, x => Assert.NotNull(buildingModel_Prepared.GetWallConstruction(x)));

            List<IOpening> openings = buildingModel_Prepared.GetOpenings<IOpening>() ?? [];
            Assert.NotEmpty(openings);
            foreach (IOpening opening in openings)
            {
                Assert.Equal(Modify.GenerateOpenings_GeneratedBy, opening is Opening opening_Temp && opening_Temp.TryGetValue(new EnumParameterDefinition(DiGi.GIS.Analytical.Enums.OpeningParameter.GeneratedBy), out string? generatedBy) ? generatedBy : null);
            }

            bool hasInternalCondition = false;
            foreach (ISpace space in buildingModel_Prepared.GetSpaces<ISpace>() ?? [])
            {
                if (buildingModel_Prepared.GetSpaceInternalConditions(space) is List<SpaceInternalCondition> spaceInternalConditions && spaceInternalConditions.Count > 0)
                {
                    hasInternalCondition = true;
                    break;
                }
            }
            Assert.True(hasInternalCondition, "The prepared model carries no internal conditions.");
            Assert.False(string.IsNullOrWhiteSpace(heatDemandPreparationResult.UsageSource));

            // The engine run on the preparation gives exactly the result the assembly recorded.
            BuildingHeatDemandResult? buildingHeatDemandResult = buildingModel.BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], buildingSolarRadiationResult, ePWFile, heatDemandContext);
            Assert.NotNull(buildingHeatDemandResult);
            Assert.Equal(buildingHeatDemandResult.UsageSource, heatDemandPreparationResult.UsageSource);
            Assert.Equal(buildingHeatDemandResult.MatchedSurfaceCount, heatDemandPreparationResult.MatchedSurfaceCount);
            Assert.Equal(buildingHeatDemandResult.UnshadedSurfaceCount, heatDemandPreparationResult.UnshadedSurfaceCount);
            Assert.Equal(buildingHeatDemandResult.ConstructionLibraryVersion, heatDemandPreparationResult.ConstructionLibraryVersion);

            DiGi.Analytical.Building.Energy.Classes.HeatDemandResult? heatDemandResult_Engine = DiGi.Analytical.Building.Energy.Create.HeatDemandResult(buildingModel_Prepared, heatDemandPreparationResult.MonthlyClimate, heatDemandPreparationResult.OpeningGenerationResult?.TypologyData, heatDemandContext.HeatDemandOptions);
            Assert.NotNull(heatDemandResult_Engine);
            Assert.NotNull(buildingHeatDemandResult.HeatDemandResult);
            Assert.Equal(Core.Convert.ToSystem_String(heatDemandResult_Engine), Core.Convert.ToSystem_String(buildingHeatDemandResult.HeatDemandResult));

            // The stored model passed in is untouched.
            Assert.Empty(buildingModel.GetOpenings<IOpening>() ?? []);
            Assert.All(buildingModel.GetComponents<IWall>() ?? [], x => Assert.Null(buildingModel.GetWallConstruction(x)));
            Assert.DoesNotContain(buildingModel.GetComponents<IWall>() ?? [], x => x is Component component && component.TryGetValue(new EnumParameterDefinition(ComponentParameter.PartyWall), out Guid _));
            foreach (ISpace space in buildingModel.GetSpaces<ISpace>() ?? [])
            {
                Assert.Empty(buildingModel.GetSpaceInternalConditions(space) ?? []);
            }
        }

        /// <summary>
        /// Tests the null contract of <see cref="Create.HeatDemandPreparationResult"/> and <see cref="Create.BuildingHeatDemandResult"/> side by side (DiGi-Organization/DiGi.GIS.Analytical#15): both are null without a model, without a 2D building, without weather and without an opening-size set, and a weather with no record - which the issue does not list - still yields a preparation carrying the model while only its <see cref="HeatDemandPreparationResult.MonthlyClimate"/> is null, so the result stays null at the engine.
        /// </summary>
        [MediumFact]
        public void HeatDemandPreparationResult_Null()
        {
            (BuildingModel buildingModel, BuildingModel buildingModel_Neighbour, Building2D building2D) = HeatDemandAssembly_Fixture();
            EPWFile ePWFile = HeatDemandAssembly_EPWFile();

            HeatDemandInput heatDemandInput = new(new OpeningGenerationInput(1975, YearBuiltSource.User, null, Regulation.Enums.ClimateZone.III, null, null), new OccupancyData(building2D.Reference, null, 6), "test", 1465, WeatherSourceType.EPW, "16");
            HeatDemandContext heatDemandContext = new(Regulation.Query.EmbeddedConstructionLibrary_PL(), null, null, null);

            // No model.
            Assert.Null(((BuildingModel?)null).HeatDemandPreparationResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, ePWFile, heatDemandContext));
            Assert.Null(((BuildingModel?)null).BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, ePWFile, heatDemandContext));

            // No 2D building.
            Assert.Null(buildingModel.HeatDemandPreparationResult(null, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, ePWFile, heatDemandContext));
            Assert.Null(buildingModel.BuildingHeatDemandResult(null, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, ePWFile, heatDemandContext));

            // No weather.
            Assert.Null(buildingModel.HeatDemandPreparationResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, null, heatDemandContext));
            Assert.Null(buildingModel.BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, null, heatDemandContext));

            // No opening-size set: a library holding no set matches no building, and its null is not replaced by the embedded one.
            HeatDemandContext heatDemandContext_NoSizeSet = new(null, new Regulation.Classes.OpeningSizeLibrary(Guid.NewGuid(), null, null, null), null, null);
            Assert.Null(buildingModel.HeatDemandPreparationResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, ePWFile, heatDemandContext_NoSizeSet));
            Assert.Null(buildingModel.BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, ePWFile, heatDemandContext_NoSizeSet));

            // A weather with no record: the preparation still carries the model, only its climate is null (DiGi-Organization/DiGi.GIS.Analytical#15).
            Weather.Classes.Weather weather_NoRecords = new();
            HeatDemandPreparationResult? heatDemandPreparationResult = buildingModel.HeatDemandPreparationResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, weather_NoRecords, heatDemandContext);
            Assert.NotNull(heatDemandPreparationResult);
            Assert.Null(heatDemandPreparationResult.MonthlyClimate);
            Assert.NotNull(heatDemandPreparationResult.BuildingModel);
            Assert.NotNull(heatDemandPreparationResult.OpeningGenerationResult);
            Assert.Null(buildingModel.BuildingHeatDemandResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], null, weather_NoRecords, heatDemandContext));
        }

        /// <summary>
        /// Tests the serialization and the copying of <see cref="HeatDemandPreparationResult"/> (DiGi-Organization/DiGi.GIS.Analytical#15): a string round trip through <see cref="Core.xUnit.Query.SerializationCheck"/>, a member-by-member <c>new T(source)</c> assertion, and the rule that the copy shares no model with its source - two reads hand out different instances and mutating what the copy handed out leaves the source unchanged.
        /// </summary>
        [MediumFact]
        public void HeatDemandPreparationResult_Serialization()
        {
            (BuildingModel buildingModel, BuildingModel buildingModel_Neighbour, Building2D building2D) = HeatDemandAssembly_Fixture();
            EPWFile ePWFile = HeatDemandAssembly_EPWFile();
            (BuildingSolarRadiationResult buildingSolarRadiationResult, Guid _) = HeatDemandAssembly_SolarResult(buildingModel, ePWFile);

            HeatDemandInput heatDemandInput = new(new OpeningGenerationInput(1975, YearBuiltSource.User, null, Regulation.Enums.ClimateZone.III, null, null), new OccupancyData(building2D.Reference, null, 6), "test", 1465, WeatherSourceType.EPW, "16");
            HeatDemandContext heatDemandContext = new(Regulation.Query.EmbeddedConstructionLibrary_PL(), null, null, null);

            HeatDemandPreparationResult? heatDemandPreparationResult = buildingModel.HeatDemandPreparationResult(building2D, buildingModel.BuildingInformation, heatDemandInput, [buildingModel_Neighbour], buildingSolarRadiationResult, ePWFile, heatDemandContext);
            Assert.NotNull(heatDemandPreparationResult);
            Core.xUnit.Query.SerializationCheck(heatDemandPreparationResult);

            HeatDemandPreparationResult heatDemandPreparationResult_Copy = new(heatDemandPreparationResult);
            Assert.Equal(heatDemandPreparationResult.ClimateZone, heatDemandPreparationResult_Copy.ClimateZone);
            Assert.Equal(heatDemandPreparationResult.ConstructionLibraryVersion, heatDemandPreparationResult_Copy.ConstructionLibraryVersion);
            Assert.Equal(heatDemandPreparationResult.MatchedSurfaceCount, heatDemandPreparationResult_Copy.MatchedSurfaceCount);
            Assert.Equal(heatDemandPreparationResult.UnshadedSurfaceCount, heatDemandPreparationResult_Copy.UnshadedSurfaceCount);
            Assert.Equal(heatDemandPreparationResult.UsageSource, heatDemandPreparationResult_Copy.UsageSource);
            Assert.Equal(heatDemandPreparationResult.MonthlyClimate?.ExternalTemperatures, heatDemandPreparationResult_Copy.MonthlyClimate?.ExternalTemperatures);
            Assert.Equal(heatDemandPreparationResult.MonthlyClimate?.DesignExternalTemperature, heatDemandPreparationResult_Copy.MonthlyClimate?.DesignExternalTemperature);
            Assert.Equal(heatDemandPreparationResult.MonthlyClimate?.AnnualMeanExternalTemperature, heatDemandPreparationResult_Copy.MonthlyClimate?.AnnualMeanExternalTemperature);
            Assert.Equal(heatDemandPreparationResult.MonthlyClimate?.SurfaceIrradiations?.Count, heatDemandPreparationResult_Copy.MonthlyClimate?.SurfaceIrradiations?.Count);
            Assert.Equal(heatDemandPreparationResult.OpeningGenerationResult?.YearBuilt, heatDemandPreparationResult_Copy.OpeningGenerationResult?.YearBuilt);
            Assert.Equal(heatDemandPreparationResult.OpeningGenerationResult?.YearBuiltSource, heatDemandPreparationResult_Copy.OpeningGenerationResult?.YearBuiltSource);
            Assert.Equal(heatDemandPreparationResult.OpeningGenerationResult?.ConstructionFlavour, heatDemandPreparationResult_Copy.OpeningGenerationResult?.ConstructionFlavour);
            Assert.Equal(Core.Convert.ToSystem_String(heatDemandPreparationResult.OpeningGenerationResult?.TypologyData), Core.Convert.ToSystem_String(heatDemandPreparationResult_Copy.OpeningGenerationResult?.TypologyData));

            BuildingModel? buildingModel_Source = heatDemandPreparationResult.BuildingModel;
            BuildingModel? buildingModel_Copy = heatDemandPreparationResult_Copy.BuildingModel;
            Assert.NotNull(buildingModel_Source);
            Assert.NotNull(buildingModel_Copy);
            Assert.Equal(buildingModel_Source.GetComponents<IComponent>()?.Count, buildingModel_Copy.GetComponents<IComponent>()?.Count);
            Assert.Equal(buildingModel_Source.GetOpenings<IOpening>()?.Count, buildingModel_Copy.GetOpenings<IOpening>()?.Count);

            // The copy shares no model with its source: reads hand out different instances, and what the
            // copy handed out cannot be mutated through.
            Assert.NotSame(buildingModel_Source, heatDemandPreparationResult.BuildingModel);
            Assert.NotSame(buildingModel_Source, buildingModel_Copy);
            Assert.NotNull(buildingModel_Source.BuildingInformation.Coordinates);
            buildingModel_Copy.BuildingInformation = new BuildingInformation();
            Assert.NotSame(buildingModel_Copy.BuildingInformation, heatDemandPreparationResult.BuildingModel!.BuildingInformation);
            Assert.NotNull(heatDemandPreparationResult.BuildingModel.BuildingInformation.Coordinates);
        }

        /// <summary>
        /// Reproduces <c>Create.BuildingHeatDemandResult</c> as it was before it was split onto <c>Create.HeatDemandPreparationResult</c> (DiGi-Organization/DiGi.GIS.Analytical#15), step for step and argument for argument, so the parity fact compares the refactored method against executable pre-refactor code rather than a plausible range.
        /// </summary>
        /// <param name="buildingModel">The stored building model.</param>
        /// <param name="building2D">The 2D building of the model.</param>
        /// <param name="buildingInformation">The building's information: coordinates and UTC offset.</param>
        /// <param name="heatDemandInput">The per-building inputs.</param>
        /// <param name="buildingModels_Neighbour">The building models of the adjacent buildings.</param>
        /// <param name="buildingSolarRadiationResult">The stored solar result of the building.</param>
        /// <param name="ePWFile">The weather.</param>
        /// <param name="heatDemandContext">The libraries, the climate-zone map and the options of the batch.</param>
        /// <returns>The result, or null where the original method returned null.</returns>
        private static BuildingHeatDemandResult? HeatDemandAssembly_HeatDemandResult_PreRefactor(
            BuildingModel buildingModel,
            Building2D building2D,
            BuildingInformation buildingInformation,
            HeatDemandInput heatDemandInput,
            List<BuildingModel?> buildingModels_Neighbour,
            BuildingSolarRadiationResult buildingSolarRadiationResult,
            EPWFile ePWFile,
            HeatDemandContext heatDemandContext)
        {
            Weather.Classes.Weather? weather = ePWFile;
            double tolerance = Core.Constants.Tolerance.Distance;

            if (buildingModel is null || building2D is null || weather is null)
            {
                return null;
            }

            // An isolated copy, so the split below leaves the stored model untouched.
            BuildingModel buildingModel_Temp = new(buildingModel);

            List<BuildingModel?> buildingModels_Neighbour_Temp = buildingModels_Neighbour is null ? [] : [.. buildingModels_Neighbour];
            if (buildingModels_Neighbour_Temp.Count > 0)
            {
                List<PartyWallResult>? partyWallResults = DiGi.Analytical.Building.Query.PartyWalls(buildingModel_Temp, buildingModels_Neighbour_Temp, tolerance: tolerance);
                if (partyWallResults is not null && partyWallResults.Count > 0)
                {
                    DiGi.Analytical.Building.Modify.SplitPartyWalls(buildingModel_Temp, partyWallResults, tolerance);
                }
            }

            Regulation.Classes.ConstructionLibrary? constructionLibrary = heatDemandContext?.ConstructionLibrary ?? Regulation.Query.EmbeddedConstructionLibrary_PL();

            OpeningGenerationInput? openingGenerationInput = heatDemandInput?.OpeningGenerationInput;
            OpeningGenerationResult? openingGenerationResult = buildingModel_Temp.GenerateOpenings(building2D, openingGenerationInput, buildingModels_Neighbour_Temp, constructionLibrary, heatDemandContext?.OpeningSizeLibrary, tolerance);
            if (openingGenerationResult is null)
            {
                return null;
            }

            buildingModel_Temp.AssignInternalConditions(building2D, heatDemandInput?.OccupancyData, openingGenerationResult.YearBuilt, heatDemandInput?.OccupancySource);

            Regulation.Enums.ClimateZone climateZone = openingGenerationInput?.ClimateZone ?? Regulation.Enums.ClimateZone.Undefined;
            if (climateZone == Regulation.Enums.ClimateZone.Undefined)
            {
                climateZone = building2D.ClimateZone(heatDemandContext?.ClimateZoneMap, tolerance);
            }

            DiGi.Analytical.Building.Energy.Classes.MonthlyClimate? monthlyClimate = buildingModel_Temp.MonthlyClimate(buildingModel, buildingInformation, buildingSolarRadiationResult, weather, climateZone, out int matchedSurfaceCount, out int unshadedSurfaceCount, tolerance);

            DiGi.Analytical.Building.Energy.Classes.HeatDemandResult? heatDemandResult = DiGi.Analytical.Building.Energy.Create.HeatDemandResult(buildingModel_Temp, monthlyClimate, openingGenerationResult.TypologyData, heatDemandContext?.HeatDemandOptions, tolerance);
            if (heatDemandResult is null)
            {
                return null;
            }

            // The usage source of the first space that has one: every space of a building gets the same condition.
            string? usageSource = null;
            foreach (ISpace space in buildingModel_Temp.GetSpaces<ISpace>() ?? [])
            {
                List<SpaceInternalCondition>? spaceInternalConditions = buildingModel_Temp.GetSpaceInternalConditions(space);
                if (spaceInternalConditions is null)
                {
                    continue;
                }

                foreach (SpaceInternalCondition spaceInternalCondition in spaceInternalConditions)
                {
                    if (spaceInternalCondition?.InternalCondition is IInternalCondition internalCondition && internalCondition.TryGetValue(new EnumParameterDefinition(DiGi.Analytical.Building.HVAC.Enums.InternalConditionParameter.UsageSource), out string? usageSource_Temp) && usageSource_Temp is not null)
                    {
                        usageSource = usageSource_Temp;
                        break;
                    }
                }

                if (usageSource is not null)
                {
                    break;
                }
            }

            return new BuildingHeatDemandResult(
                building2D.Reference,
                heatDemandInput?.CountyId ?? buildingSolarRadiationResult?.CountyId ?? 0,
                buildingModel.SolarBuildingModelId() ?? 0,
                buildingSolarRadiationResult?.WeatherSourceType ?? heatDemandInput?.WeatherSourceType ?? WeatherSourceType.Undefined,
                buildingSolarRadiationResult?.WeatherSourceId ?? heatDemandInput?.WeatherSourceId,
                buildingSolarRadiationResult is null ? HeatDemandSolarSource.Unshaded : HeatDemandSolarSource.Shaded,
                buildingSolarRadiationResult?.SolverVersion,
                buildingSolarRadiationResult?.Radius,
                matchedSurfaceCount,
                unshadedSurfaceCount,
                openingGenerationResult.ConstructionFlavour,
                constructionLibrary?.Version,
                openingGenerationResult.ConstructionSetSource,
                openingGenerationResult.BuildingType,
                openingGenerationResult.YearBuilt,
                openingGenerationResult.YearBuiltSource,
                usageSource,
                null,
                heatDemandResult);
        }
    }
}
