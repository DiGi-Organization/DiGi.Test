using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.Classes;
using DiGi.GIS.Enums;
using System.Collections.Generic;
using Regulation = DiGi.Analytical.Building.Regulation;

namespace DiGi.GIS.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="Query.CalculationYearBuilt"/> replaces a prediction of the predictor's first year, and nothing else (DiGi-Organization/DiGi.GIS.Analytical#16).
        /// <para>A prediction of 2008 becomes the fallback year with source Fallback; a prediction of 2009, a 1977 estimate of the retired regressor, a user year of 2008, a fallback and an unknown year are returned as given, so the substitution applies once; the options' fallback year is the one taken.</para>
        /// </summary>
        [Fact]
        public void Query_CalculationYearBuilt()
        {
            Assert.Equal(2008, GIS.Constants.YearBuiltPrediction.FirstYear);

            short fallbackYearBuilt = new OpeningGenerationOptions().FallbackYearBuilt;
            Assert.Equal(Regulation.Enums.ConstructionPeriod.From1967To1985, Regulation.Query.ConstructionPeriod(fallbackYearBuilt));

            Assert.Equal(((short?)fallbackYearBuilt, (YearBuiltSource?)YearBuiltSource.Fallback), Query.CalculationYearBuilt(2008, YearBuiltSource.Prediction));
            Assert.Equal(((short?)1977, (YearBuiltSource?)YearBuiltSource.Prediction), Query.CalculationYearBuilt(1977, YearBuiltSource.Prediction));
            Assert.Equal(((short?)2009, (YearBuiltSource?)YearBuiltSource.Prediction), Query.CalculationYearBuilt(2009, YearBuiltSource.Prediction));
            Assert.Equal(((short?)2008, (YearBuiltSource?)YearBuiltSource.User), Query.CalculationYearBuilt(2008, YearBuiltSource.User));
            Assert.Equal(((short?)fallbackYearBuilt, (YearBuiltSource?)YearBuiltSource.Fallback), Query.CalculationYearBuilt(fallbackYearBuilt, YearBuiltSource.Fallback));
            Assert.Equal(((short?)null, (YearBuiltSource?)null), Query.CalculationYearBuilt(null, null));
            Assert.Equal(((short?)null, (YearBuiltSource?)YearBuiltSource.Prediction), Query.CalculationYearBuilt(null, YearBuiltSource.Prediction));

            OpeningGenerationOptions openingGenerationOptions = new() { FallbackYearBuilt = 1950 };
            Assert.Equal(((short?)1950, (YearBuiltSource?)YearBuiltSource.Fallback), Query.CalculationYearBuilt(2008, YearBuiltSource.Prediction, openingGenerationOptions));
        }

        /// <summary>
        /// Tests that a building predicted at the predictor's first year does not get the 2003-2008 construction set (DiGi-Organization/DiGi.GIS.Analytical#16).
        /// <para>A single-family house with Prediction 2008 is generated as the fallback year: the result reports that year with source Fallback, every opening carries Fallback, and the construction set, opening-size set and regime are those of a user year equal to the fallback, not those of a user year of 2008.</para>
        /// </summary>
        [Fact]
        public void Modify_GenerateOpenings_CensoredPrediction()
        {
            short fallbackYearBuilt = new OpeningGenerationOptions().FallbackYearBuilt;

            (BuildingModel buildingModel_Prediction, Building2D building2D_Prediction) = OpeningGeneration_Box(10, 8, 2, BuildingSpecificFunction.single_family_building);
            OpeningGenerationResult? openingGenerationResult_Prediction = buildingModel_Prediction.GenerateOpenings(building2D_Prediction, new OpeningGenerationInput(2008, YearBuiltSource.Prediction, null, Regulation.Enums.ClimateZone.Undefined, null, null));
            Assert.NotNull(openingGenerationResult_Prediction);
            Assert.Equal(fallbackYearBuilt, openingGenerationResult_Prediction.YearBuilt);
            Assert.Equal(YearBuiltSource.Fallback, openingGenerationResult_Prediction.YearBuiltSource);
            Assert.NotEqual(Regulation.Enums.ConstructionPeriod.From2003To2008, Regulation.Query.ConstructionPeriod(openingGenerationResult_Prediction.YearBuilt));

            List<IOpening> openings = buildingModel_Prediction.GetOpenings<IOpening>() ?? [];
            Assert.NotEmpty(openings);
            Assert.All(openings, x => Assert.Equal("Fallback", OpeningGeneration_Value(x, OpeningParameter.YearBuiltSource)));

            (BuildingModel buildingModel_Fallback, Building2D building2D_Fallback) = OpeningGeneration_Box(10, 8, 2, BuildingSpecificFunction.single_family_building);
            OpeningGenerationResult? openingGenerationResult_Fallback = buildingModel_Fallback.GenerateOpenings(building2D_Fallback, OpeningGeneration_Input(fallbackYearBuilt));
            Assert.NotNull(openingGenerationResult_Fallback);

            (BuildingModel buildingModel_2008, Building2D building2D_2008) = OpeningGeneration_Box(10, 8, 2, BuildingSpecificFunction.single_family_building);
            OpeningGenerationResult? openingGenerationResult_2008 = buildingModel_2008.GenerateOpenings(building2D_2008, OpeningGeneration_Input(2008));
            Assert.NotNull(openingGenerationResult_2008);
            Assert.Equal((short)2008, openingGenerationResult_2008.YearBuilt);
            Assert.Equal(YearBuiltSource.User, openingGenerationResult_2008.YearBuiltSource);

            Assert.Equal(openingGenerationResult_Fallback.ConstructionSetSource, openingGenerationResult_Prediction.ConstructionSetSource);
            Assert.Equal(openingGenerationResult_Fallback.OpeningSizeSetSource, openingGenerationResult_Prediction.OpeningSizeSetSource);
            Assert.Equal(openingGenerationResult_Fallback.BuildingRegulation, openingGenerationResult_Prediction.BuildingRegulation);
            Assert.Equal(openingGenerationResult_Fallback.TypologyData?.Source, openingGenerationResult_Prediction.TypologyData?.Source);
            Assert.NotEqual(openingGenerationResult_2008.ConstructionSetSource, openingGenerationResult_Prediction.ConstructionSetSource);
            Assert.NotEqual(openingGenerationResult_2008.TypologyData?.Source, openingGenerationResult_Prediction.TypologyData?.Source);
        }
    }
}
