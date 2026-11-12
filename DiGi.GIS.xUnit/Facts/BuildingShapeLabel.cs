using DiGi.Core;
using System;
using System.Linq;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Builds a building shape label with every member populated, asserts each property, round-trips the string form (including the timestamp), asserts a direct copy, and runs the serialization check.
        /// </summary>
        [Fact]
        public void BuildingShapeLabel()
        {
            DateTimeOffset dateTime = new(2026, 11, 12, 9, 41, 47, TimeSpan.FromHours(1));

            Classes.BuildingShapeLabel buildingShapeLabel = new("gis12-2026", 1465, "0201_1.0001.123_BUD", Enums.BuildingShape.U, "annotator@example.com", "Courtyard open to the street", dateTime);

            Assert.Equal("gis12-2026", buildingShapeLabel.SampleKey);
            Assert.Equal(1465, buildingShapeLabel.CountyId);
            Assert.Equal("0201_1.0001.123_BUD", buildingShapeLabel.Reference);
            Assert.Equal(Enums.BuildingShape.U, buildingShapeLabel.BuildingShape);
            Assert.Equal("annotator@example.com", buildingShapeLabel.Interpreter);
            Assert.Equal("Courtyard open to the street", buildingShapeLabel.Note);
            Assert.Equal(dateTime, buildingShapeLabel.DateTime);

            string? json = buildingShapeLabel.ToSystem_String();
            Assert.NotNull(json);

            Classes.BuildingShapeLabel? buildingShapeLabel_RoundTripped = Core.Convert.ToDiGi<Classes.BuildingShapeLabel>(json)?.FirstOrDefault();
            Assert.NotNull(buildingShapeLabel_RoundTripped);

            Assert.Equal(buildingShapeLabel.SampleKey, buildingShapeLabel_RoundTripped.SampleKey);
            Assert.Equal(buildingShapeLabel.CountyId, buildingShapeLabel_RoundTripped.CountyId);
            Assert.Equal(buildingShapeLabel.Reference, buildingShapeLabel_RoundTripped.Reference);
            Assert.Equal(buildingShapeLabel.BuildingShape, buildingShapeLabel_RoundTripped.BuildingShape);
            Assert.Equal(buildingShapeLabel.Interpreter, buildingShapeLabel_RoundTripped.Interpreter);
            Assert.Equal(buildingShapeLabel.Note, buildingShapeLabel_RoundTripped.Note);
            Assert.Equal(buildingShapeLabel.DateTime, buildingShapeLabel_RoundTripped.DateTime);

            //Copy constructor: Clone() (used by SerializationCheck) is a JSON round-trip, not the copy constructor, so the copy leg needs its own direct assertion.
            Classes.BuildingShapeLabel buildingShapeLabel_Copy = new(buildingShapeLabel);

            Assert.Equal(buildingShapeLabel.SampleKey, buildingShapeLabel_Copy.SampleKey);
            Assert.Equal(buildingShapeLabel.CountyId, buildingShapeLabel_Copy.CountyId);
            Assert.Equal(buildingShapeLabel.Reference, buildingShapeLabel_Copy.Reference);
            Assert.Equal(buildingShapeLabel.BuildingShape, buildingShapeLabel_Copy.BuildingShape);
            Assert.Equal(buildingShapeLabel.Interpreter, buildingShapeLabel_Copy.Interpreter);
            Assert.Equal(buildingShapeLabel.Note, buildingShapeLabel_Copy.Note);
            Assert.Equal(buildingShapeLabel.DateTime, buildingShapeLabel_Copy.DateTime);

            Core.xUnit.Query.SerializationCheck(buildingShapeLabel);
        }

        /// <summary>
        /// Builds a building shape label without a note (an annotator who could not classify the footprint) and runs the serialization check, so the null optional member survives the round trip.
        /// </summary>
        [Fact]
        public void BuildingShapeLabel_Undefined()
        {
            Classes.BuildingShapeLabel buildingShapeLabel = new("gis12-2026", 1432, "0201_1.0001.124_BUD", Enums.BuildingShape.Undefined, "annotator@example.com", null, new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero));

            Classes.BuildingShapeLabel? buildingShapeLabel_RoundTripped = Core.Convert.ToDiGi<Classes.BuildingShapeLabel>(buildingShapeLabel.ToSystem_String())?.FirstOrDefault();
            Assert.NotNull(buildingShapeLabel_RoundTripped);

            Assert.Equal(Enums.BuildingShape.Undefined, buildingShapeLabel_RoundTripped.BuildingShape);
            Assert.Null(buildingShapeLabel_RoundTripped.Note);

            Core.xUnit.Query.SerializationCheck(buildingShapeLabel);
        }
    }
}
