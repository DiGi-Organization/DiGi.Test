using DiGi.Analytical.Building.Classes;
using DiGi.Core.Classes;
using DiGi.Core.Interfaces;
using DiGi.Core.Parameter.Classes;
using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.Classes;
using System.Globalization;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests a full round-trip through Create.Reference and Query.TryParse with a BuildingModel that has a
        /// string reference, a separate IBuildingGuidObject, and a county id.
        /// </summary>
        [Fact]
        public void Reference_RoundTrip_Full()
        {
            BuildingModel buildingModel = new();
            buildingModel.SetValue(BuildingModelParameter.Reference, "MODEL_001", new SetValueSettings(true, false));

            BuildingModel buildingGuidObject = new();

            int countyId = 42;

            IReference? reference = Create.Reference(buildingModel, buildingGuidObject, countyId);
            Assert.NotNull(reference);

            string? referenceString = reference.ToString();
            Assert.True(!string.IsNullOrWhiteSpace(referenceString));

            Assert.True(Query.TryParse(referenceString!, out string buildingModelReference, out int? countyId_Parsed, out GuidReference? buildingObjectGuidReference));

            Assert.Equal("MODEL_001", buildingModelReference);
            Assert.Equal(countyId, countyId_Parsed);
            Assert.NotNull(buildingObjectGuidReference);
            Assert.Equal(buildingGuidObject.Guid, buildingObjectGuidReference.Guid);
        }

        /// <summary>
        /// Tests that Create.Reference orders the ComplexReference steps from the root of the containment chain
        /// inwards - administrative area (county), then building, then building element - and not building first.
        /// </summary>
        [Fact]
        public void Reference_Order_AdministrativeAreaBuildingElement()
        {
            BuildingModel buildingModel = new();
            buildingModel.SetValue(BuildingModelParameter.Reference, "MODEL_001", new SetValueSettings(true, false));

            BuildingModel buildingGuidObject = new();

            int countyId = 42;

            IReference? reference = Create.Reference(buildingModel, buildingGuidObject, countyId);
            ComplexReference complexReference = Assert.IsType<ComplexReference>(reference);

            Assert.Equal(3, complexReference.Count);

            UniqueIdReference administrativeAreaReference = Assert.IsType<UniqueIdReference>(complexReference[0]);
            Assert.Equal(new TypeReference(typeof(AdministrativeDivision)).ToString(), administrativeAreaReference.TypeReference?.ToString());
            Assert.Equal(countyId.ToString(CultureInfo.CurrentCulture), administrativeAreaReference.UniqueId);

            UniqueIdReference buildingReference = Assert.IsType<UniqueIdReference>(complexReference[1]);
            Assert.Equal(new TypeReference(typeof(BuildingModel)).ToString(), buildingReference.TypeReference?.ToString());
            Assert.Equal("MODEL_001", buildingReference.UniqueId);

            GuidReference buildingElementReference = Assert.IsType<GuidReference>(complexReference[2]);
            Assert.Equal(buildingGuidObject.Guid, buildingElementReference.Guid);
        }

        /// <summary>
        /// Tests round-trip through Create.Reference and Query.TryParse with BuildingModel that has a string
        /// reference but no IBuildingGuidObject or county id.
        /// </summary>
        [Fact]
        public void Reference_RoundTrip_NoBuildingGuidObject()
        {
            BuildingModel buildingModel = new();
            buildingModel.SetValue(BuildingModelParameter.Reference, "MODEL_ABC", new SetValueSettings(true, false));

            IReference? reference = Create.Reference(buildingModel);
            Assert.NotNull(reference);

            string? referenceString = reference.ToString();
            Assert.True(!string.IsNullOrWhiteSpace(referenceString));

            Assert.True(Query.TryParse(referenceString!, out string buildingModelReference, out int? countyId_Parsed, out GuidReference? buildingObjectGuidReference));

            Assert.Equal("MODEL_ABC", buildingModelReference);
            Assert.Null(countyId_Parsed);
            Assert.Null(buildingObjectGuidReference);
        }

        /// <summary>
        /// Tests that a plain string with no reference discriminator is returned unchanged as
        /// buildingModelReference. Core.Query.TryParse reads such a string as a legacy TypeReference, which is
        /// neither a unique nor a complex reference, so PostgreSQL.Query.TryParse treats it as a plain building
        /// reference.
        /// </summary>
        [Fact]
        public void TryParse_UnparseableString()
        {
            Assert.True(Query.TryParse("PLAIN_STRING_NOT_A_REFERENCE", out string buildingModelReference, out int? countyId_Parsed, out GuidReference? buildingObjectGuidReference));

            Assert.Equal("PLAIN_STRING_NOT_A_REFERENCE", buildingModelReference);
            Assert.Null(countyId_Parsed);
            Assert.Null(buildingObjectGuidReference);
        }

        /// <summary>
        /// Tests that a plain building reference in the real-world EGiB style (no discriminator and no "::"
        /// separator) passes through unchanged as the building model reference, with no county or building object
        /// guid. Guards the regression where such references were rejected because Core.Query.TryParse reads them
        /// as a legacy TypeReference.
        /// </summary>
        [Fact]
        public void TryParse_PlainBuildingReference()
        {
            Assert.True(Query.TryParse("141201_2.0001.1234/5.BUD", out string buildingModelReference, out int? countyId_Parsed, out GuidReference? buildingObjectGuidReference));

            Assert.Equal("141201_2.0001.1234/5.BUD", buildingModelReference);
            Assert.Null(countyId_Parsed);
            Assert.Null(buildingObjectGuidReference);
        }

        /// <summary>
        /// Tests that TryParse returns false when given null or empty input.
        /// </summary>
        [Fact]
        public void TryParse_NullOrEmpty()
        {
            Assert.False(Query.TryParse(null, out string buildingModelReference_1, out int? countyId_1, out GuidReference? buildingObjectGuidReference_1));
            Assert.Equal(string.Empty, buildingModelReference_1);
            Assert.Null(countyId_1);
            Assert.Null(buildingObjectGuidReference_1);

            Assert.False(Query.TryParse(string.Empty, out string buildingModelReference_2, out int? countyId_2, out GuidReference? buildingObjectGuidReference_2));
            Assert.Equal(string.Empty, buildingModelReference_2);
            Assert.Null(countyId_2);
            Assert.Null(buildingObjectGuidReference_2);

            Assert.False(Query.TryParse("   ", out string buildingModelReference_3, out int? countyId_3, out GuidReference? buildingObjectGuidReference_3));
            Assert.Equal(string.Empty, buildingModelReference_3);
            Assert.Null(countyId_3);
            Assert.Null(buildingObjectGuidReference_3);
        }

        /// <summary>
        /// Tests that Create.Reference returns null when given a null BuildingModel.
        /// </summary>
        [Fact]
        public void Reference_NullBuildingModel()
        {
            BuildingModel? buildingModel = null;
            Assert.Null(Create.Reference(buildingModel!));
        }

        /// <summary>
        /// Tests that the reference representation generated by Create.Reference is non-null and non-empty.
        /// </summary>
        [Fact]
        public void Reference_ToString()
        {
            BuildingModel buildingModel = new();
            buildingModel.SetValue(BuildingModelParameter.Reference, "TOSTRING_MODEL", new SetValueSettings(true, false));

            IReference? reference = Create.Reference(buildingModel);
            Assert.NotNull(reference);

            string? referenceString = reference.ToString();
            Assert.False(string.IsNullOrWhiteSpace(referenceString));
        }

        /// <summary>
        /// Tests TryParse with a ComplexReference that contains a GuidReference whose type implements
        /// IBuildingGuidObject. Verifies the GuidReference is correctly extracted even when the TypeReference is
        /// valid.
        /// </summary>
        [Fact]
        public void TryParse_ExtractsBuildingGuidObjectFromComplexReference()
        {
            BuildingModel buildingModel = new();
            buildingModel.SetValue(BuildingModelParameter.Reference, "COMPLEX_MODEL", new SetValueSettings(true, false));

            BuildingModel buildingGuidObject = new();

            IReference? reference = Create.Reference(buildingModel, buildingGuidObject, 7);
            Assert.NotNull(reference);
            Assert.IsType<ComplexReference>(reference);

            Assert.True(Query.TryParse(reference.ToString(), out string buildingModelReference, out int? countyId_Parsed, out GuidReference? buildingObjectGuidReference));

            Assert.Equal("COMPLEX_MODEL", buildingModelReference);
            Assert.Equal(7, countyId_Parsed);
            Assert.NotNull(buildingObjectGuidReference);
            Assert.Equal(buildingGuidObject.Guid, buildingObjectGuidReference.Guid);
        }

        /// <summary>
        /// Tests TryParse with a UniqueIdReference (simple, non-complex) produced when only a BuildingModel
        /// with a reference is provided and no buildingGuidObject.
        /// </summary>
        [Fact]
        public void TryParse_UniqueIdReference()
        {
            BuildingModel buildingModel = new();
            buildingModel.SetValue(BuildingModelParameter.Reference, "SIMPLE_MODEL", new SetValueSettings(true, false));

            IReference? reference = Create.Reference(buildingModel);
            Assert.NotNull(reference);

            Assert.True(Query.TryParse(reference.ToString(), out string buildingModelReference, out int? countyId_Parsed, out GuidReference? buildingObjectGuidReference));

            Assert.Equal("SIMPLE_MODEL", buildingModelReference);
            Assert.Null(countyId_Parsed);
            Assert.Null(buildingObjectGuidReference);
        }
    }
}
