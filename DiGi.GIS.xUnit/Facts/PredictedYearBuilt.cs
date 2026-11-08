using DiGi.Core;
using System;
using System.Globalization;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Builds a predicted year built entry with every member populated, asserts each property, round-trips the string form, checks the copy constructor directly and runs the serialization check.
        /// <para>The model identifier is provenance only: the entry must stay keyed by its prediction time, so the same stamp with or without an identifier yields the same <see cref="Classes.PredictedYearBuilt.Source"/>.</para>
        /// </summary>
        [Fact]
        public void PredictedYearBuilt()
        {
            DateTime dateTime = new(2026, 10, 8, 7, 15, 30, DateTimeKind.Utc);
            string modelId = "2e120f49c3a0b7d4e5f61728394a5b6c7d8e9f00112233445566778899aabbcc";

            Classes.PredictedYearBuilt predictedYearBuilt = new(dateTime, (short)1974, modelId);

            Assert.Equal((short)1974, predictedYearBuilt.Year);
            Assert.Equal(dateTime, predictedYearBuilt.DateTime);
            Assert.Equal(modelId, predictedYearBuilt.ModelId);
            Assert.Equal(Enums.YearBuiltSource.Prediction, predictedYearBuilt.YearBuiltSource);
            Assert.Equal(new Classes.PredictedYearBuilt(dateTime, (short)1974).Source, predictedYearBuilt.Source);

            string? json = predictedYearBuilt.ToSystem_String();
            Assert.NotNull(json);

            Classes.PredictedYearBuilt? predictedYearBuilt_RoundTripped = Core.Convert.ToDiGi<Classes.PredictedYearBuilt>(json)?.FirstOrDefault();
            Assert.NotNull(predictedYearBuilt_RoundTripped);

            Assert.Equal(predictedYearBuilt.Year, predictedYearBuilt_RoundTripped.Year);
            Assert.Equal(predictedYearBuilt.DateTime, predictedYearBuilt_RoundTripped.DateTime);
            Assert.Equal(predictedYearBuilt.ModelId, predictedYearBuilt_RoundTripped.ModelId);
            Assert.Equal(predictedYearBuilt.Source, predictedYearBuilt_RoundTripped.Source);

            //Copy constructor: Clone() (used by SerializationCheck) is a JSON round-trip, not the copy constructor, so the copy leg needs its own direct assertion.
            Classes.PredictedYearBuilt predictedYearBuilt_Copy = new(predictedYearBuilt);

            Assert.Equal(predictedYearBuilt.Year, predictedYearBuilt_Copy.Year);
            Assert.Equal(predictedYearBuilt.DateTime, predictedYearBuilt_Copy.DateTime);
            Assert.Equal(predictedYearBuilt.ModelId, predictedYearBuilt_Copy.ModelId);

            Core.xUnit.Query.SerializationCheck(predictedYearBuilt);

            Classes.YearBuiltData yearBuiltData = new("272D6AAF-9D86-9B0E-E053-CC2BA8C0B5EA");
            Assert.True(yearBuiltData.SetPredictedYearBuilt(dateTime, (short)1974, modelId));
            Assert.Equal(modelId, yearBuiltData.GetPredictedYearBuilt(dateTime)?.ModelId);

            Core.xUnit.Query.SerializationCheck(yearBuiltData);
        }

        /// <summary>
        /// Parses a predicted year built entry written before the model identifier existed and asserts it loads with a null identifier and the key it had before.
        /// </summary>
        [Fact]
        public void PredictedYearBuilt_Legacy()
        {
            DateTime dateTime = new(2025, 5, 29, 9, 41, 47);

            string? json = new Classes.PredictedYearBuilt(dateTime, (short)1960).ToSystem_String();
            Assert.NotNull(json);
            Assert.DoesNotContain("\"ModelId\":\"", json);

            Classes.PredictedYearBuilt? predictedYearBuilt = Core.Convert.ToDiGi<Classes.PredictedYearBuilt>(json)?.FirstOrDefault();
            Assert.NotNull(predictedYearBuilt);

            Assert.Null(predictedYearBuilt.ModelId);
            Assert.Equal((short)1960, predictedYearBuilt.Year);
            Assert.Equal(string.Format(CultureInfo.CurrentCulture, "Prediction_{0}", dateTime.Ticks), predictedYearBuilt.Source);
        }
    }
}
