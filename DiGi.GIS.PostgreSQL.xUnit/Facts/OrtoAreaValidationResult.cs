using DiGi.GIS.PostgreSQL.Classes;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a test-area validation result carries every tally and its nested per-dataset results through the constructor, a JSON round trip and the copy constructor, and that the derived rates follow from the stored counts.
        /// <para>The rates are what paper 01 reports (BDOT10k commission and omission, the indeterminate share, each dataset's omission), so each is asserted against the counts it is computed from.</para>
        /// </summary>
        [Fact]
        public void OrtoAreaValidationResult_Serialization()
        {
            OrtoAreaDatasetValidationResult ortoAreaDatasetValidationResult = new("osm", 100, 80, 95, 78, 2, 1, 14);

            OrtoAreaValidationResult ortoAreaValidationResult = new("P01", "duze_miasto", 2024, 1, 90, 10, 5, 10, 3, 2, 1, [ortoAreaDatasetValidationResult]);

            string? json = Core.Convert.ToSystem_String(ortoAreaValidationResult);
            Assert.NotNull(json);

            OrtoAreaValidationResult? ortoAreaValidationResult_Json = Core.Convert.ToDiGi<OrtoAreaValidationResult>(json)?.FirstOrDefault();
            Assert.NotNull(ortoAreaValidationResult_Json);

            OrtoAreaValidationResult ortoAreaValidationResult_Copy = new(ortoAreaValidationResult);

            foreach (OrtoAreaValidationResult ortoAreaValidationResult_Temp in new[] { ortoAreaValidationResult, ortoAreaValidationResult_Json, ortoAreaValidationResult_Copy })
            {
                Assert.Equal("P01", ortoAreaValidationResult_Temp.AreaKey);
                Assert.Equal("duze_miasto", ortoAreaValidationResult_Temp.Stratum);
                Assert.Equal((short)2024, ortoAreaValidationResult_Temp.EpochYear);
                Assert.Equal(1, ortoAreaValidationResult_Temp.InterpreterCount);
                Assert.Equal(90, ortoAreaValidationResult_Temp.FootprintPresentCount);
                Assert.Equal(10, ortoAreaValidationResult_Temp.FootprintAbsentCount);
                Assert.Equal(5, ortoAreaValidationResult_Temp.FootprintIndeterminateCount);
                Assert.Equal(10, ortoAreaValidationResult_Temp.DrawnPresentCount);
                Assert.Equal(3, ortoAreaValidationResult_Temp.DrawnAbsentCount);
                Assert.Equal(2, ortoAreaValidationResult_Temp.DrawnIndeterminateCount);
                Assert.Equal(1, ortoAreaValidationResult_Temp.DisagreementCount);

                Assert.Equal(100, ortoAreaValidationResult_Temp.ReferenceCount);
                Assert.Equal(0.1, ortoAreaValidationResult_Temp.BDOT10kCommissionRate!.Value, 12);
                Assert.Equal(0.1, ortoAreaValidationResult_Temp.BDOT10kOmissionRate!.Value, 12);
                Assert.Equal(7.0 / 120.0, ortoAreaValidationResult_Temp.IndeterminateRate!.Value, 12);

                List<OrtoAreaDatasetValidationResult>? ortoAreaDatasetValidationResults = ortoAreaValidationResult_Temp.DatasetValidationResults;
                Assert.NotNull(ortoAreaDatasetValidationResults);
                OrtoAreaDatasetValidationResult ortoAreaDatasetValidationResult_Temp = Assert.Single(ortoAreaDatasetValidationResults);

                Assert.Equal("osm", ortoAreaDatasetValidationResult_Temp.DatasetKey);
                Assert.Equal(100, ortoAreaDatasetValidationResult_Temp.ReferenceCount);
                Assert.Equal(80, ortoAreaDatasetValidationResult_Temp.DetectedReferenceCount);
                Assert.Equal(95, ortoAreaDatasetValidationResult_Temp.FootprintCount);
                Assert.Equal(78, ortoAreaDatasetValidationResult_Temp.MatchedPresentFootprintCount);
                Assert.Equal(2, ortoAreaDatasetValidationResult_Temp.MatchedAbsentFootprintCount);
                Assert.Equal(1, ortoAreaDatasetValidationResult_Temp.MatchedIndeterminateFootprintCount);
                Assert.Equal(14, ortoAreaDatasetValidationResult_Temp.UnmatchedFootprintCount);
                Assert.Equal(0.2, ortoAreaDatasetValidationResult_Temp.OmissionRate!.Value, 12);
            }

            Core.xUnit.Query.SerializationCheck(ortoAreaValidationResult);
            Core.xUnit.Query.SerializationCheck(ortoAreaDatasetValidationResult);
        }

        /// <summary>
        /// Verifies that an area with no label yields null rates and a null epoch rather than division by zero or a default year.
        /// </summary>
        [Fact]
        public void OrtoAreaValidationResult_Empty()
        {
            OrtoAreaValidationResult ortoAreaValidationResult = new("P02", null, null, 0, 0, 0, 0, 0, 0, 0, 0, null);

            Assert.Null(ortoAreaValidationResult.EpochYear);
            Assert.Null(ortoAreaValidationResult.BDOT10kCommissionRate);
            Assert.Null(ortoAreaValidationResult.BDOT10kOmissionRate);
            Assert.Null(ortoAreaValidationResult.IndeterminateRate);
            Assert.Null(ortoAreaValidationResult.DatasetValidationResults);

            string? json = Core.Convert.ToSystem_String(ortoAreaValidationResult);
            OrtoAreaValidationResult? ortoAreaValidationResult_Json = Core.Convert.ToDiGi<OrtoAreaValidationResult>(json)?.FirstOrDefault();

            Assert.NotNull(ortoAreaValidationResult_Json);
            Assert.Null(ortoAreaValidationResult_Json.EpochYear);
            Assert.Null(ortoAreaValidationResult_Json.DatasetValidationResults);

            Core.xUnit.Query.SerializationCheck(ortoAreaValidationResult);
        }
    }
}
