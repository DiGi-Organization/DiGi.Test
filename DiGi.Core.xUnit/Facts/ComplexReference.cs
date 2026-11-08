using DiGi.Core.Classes;
using DiGi.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Builds the worked example from the reference grammar: a wall inside a building inside an area inside a
        /// country, rooted at an external reference.
        /// </summary>
        /// <returns>The chain.</returns>
        public static ComplexReference ComplexReference_CountryToWall()
        {
            return new ComplexReference(
            [
                new TypeRelatedExternalReference("POLAND", new TypeReference("DiGi.GIS.Classes.Country,DiGi.GIS")),
                new UniqueIdReference(new TypeReference("DiGi.GIS.Classes.Area,DiGi.GIS"), "Mazowieckie"),
                new UniqueIdReference(new TypeReference("DiGi.GIS.Classes.Building,DiGi.GIS"), "BLD-001"),
                new GuidReference(new TypeReference("DiGi.Analytical.Building.Classes.Wall,DiGi.Analytical.Building"), Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e")),
            ]);
        }

        /// <summary>
        /// Tests that a four-level containment chain round-trips: same runtime type, same string, equal, and equal
        /// step for step.
        /// </summary>
        [Fact]
        public void ComplexReference_RoundTrip()
        {
            ComplexReference complexReference = ComplexReference_CountryToWall();

            Assert.True(Core.Query.TryParse(complexReference.ToString(), out ComplexReference? complexReference_Parsed));
            Assert.NotNull(complexReference_Parsed);

            Assert.Equal(complexReference.ToString(), complexReference_Parsed.ToString());
            Assert.True(complexReference.Equals(complexReference_Parsed));
            Assert.Equal(complexReference.Count, complexReference_Parsed.Count);

            for (int i = 0; i < complexReference.Count; i++)
            {
                ISerializableReference? reference = complexReference[i];
                ISerializableReference? reference_Parsed = complexReference_Parsed[i];

                Assert.NotNull(reference);
                Assert.NotNull(reference_Parsed);
                Assert.Equal(reference.GetType(), reference_Parsed.GetType());
                Assert.Equal(reference.ToString(), reference_Parsed.ToString());
            }
        }

        /// <summary>
        /// Tests that a chain is not an <see cref="IUniqueReference"/>. A path has no unique identifier of its own,
        /// and this is what keeps a long path out of the unique-reference APIs that name storage entries.
        /// </summary>
        [Fact]
        public void ComplexReference_IsNotUniqueReference()
        {
            ComplexReference complexReference = ComplexReference_CountryToWall();

            Assert.IsAssignableFrom<IComplexReference>(complexReference);
            Assert.False(complexReference is IUniqueReference);
        }

        /// <summary>
        /// Tests the depth extremes: an empty chain, a single step, and a ten-step chain.
        /// </summary>
        [Fact]
        public void ComplexReference_Depth()
        {
            TypeReference typeReference = new(typeof(TestObject));

            List<ISerializableReference?> references_Empty = [];
            ComplexReference complexReference_Empty = new(references_Empty);
            Assert.True(Core.Query.TryParse(complexReference_Empty.ToString(), out ComplexReference? complexReference_Empty_Parsed));
            Assert.NotNull(complexReference_Empty_Parsed);
            Assert.Empty(complexReference_Empty_Parsed.References);

            ComplexReference complexReference_Single = new([typeReference]);
            Assert.True(Core.Query.TryParse(complexReference_Single.ToString(), out ComplexReference? complexReference_Single_Parsed));
            Assert.NotNull(complexReference_Single_Parsed);
            Assert.Single(complexReference_Single_Parsed.References);

            List<ISerializableReference?> references = [];
            for (int i = 0; i < 10; i++)
            {
                references.Add(new UniqueIdReference(typeReference, string.Format(CultureInfo.CurrentCulture, "STEP-{0}", i)));
            }

            ComplexReference complexReference_Deep = new(references);
            Assert.True(Core.Query.TryParse(complexReference_Deep.ToString(), out ComplexReference? complexReference_Deep_Parsed));
            Assert.NotNull(complexReference_Deep_Parsed);
            Assert.Equal(10, complexReference_Deep_Parsed.Count);
            Assert.Equal(complexReference_Deep.ToString(), complexReference_Deep_Parsed.ToString());
        }

        /// <summary>
        /// Tests that a chain nests recursively - a step that is itself a chain - since nesting is defined
        /// recursively and nothing forbids it.
        /// </summary>
        [Fact]
        public void ComplexReference_NestedComplexReference()
        {
            ComplexReference complexReference_Inner = ComplexReference_CountryToWall();
            ComplexReference complexReference = new([new TypeReference(typeof(TestObject)), complexReference_Inner]);

            Assert.True(Core.Query.TryParse(complexReference.ToString(), out ComplexReference? complexReference_Parsed));
            Assert.NotNull(complexReference_Parsed);
            Assert.Equal(complexReference.ToString(), complexReference_Parsed.ToString());

            ComplexReference? complexReference_Inner_Parsed = complexReference_Parsed[1] as ComplexReference;
            Assert.NotNull(complexReference_Inner_Parsed);
            Assert.Equal(complexReference_Inner.Count, complexReference_Inner_Parsed.Count);
        }

        /// <summary>
        /// Tests that a chain may end on a property reference, so a path can address a property of a nested object.
        /// </summary>
        [Fact]
        public void ComplexReference_EndingOnPropertyReference()
        {
            TypeReference typeReference = new(typeof(TestObject));
            GuidReference guidReference = new(typeReference, Guid.NewGuid());

            ComplexReference complexReference = new(
            [
                new UniqueIdReference(typeReference, "BLD-001"),
                new GuidPropertyReference(guidReference, "Name"),
            ]);

            Assert.True(Core.Query.TryParse(complexReference.ToString(), out ComplexReference? complexReference_Parsed));
            Assert.NotNull(complexReference_Parsed);
            Assert.IsType<GuidPropertyReference>(complexReference_Parsed[1]);
        }

        /// <summary>
        /// Tests that a chain whose steps do not parse fails rather than silently returning a shorter chain, which
        /// would address a different object.
        /// </summary>
        [Fact]
        public void ComplexReference_MalformedStep_ReturnsFalse()
        {
            Assert.False(Core.Query.TryParse("Complex::(Guid::(Type::T)::notaguid)", out IReference? reference));
            Assert.Null(reference);

            Assert.False(Core.Query.TryParse("Complex::notnested", out reference));
            Assert.Null(reference);
        }

        /// <summary>
        /// Tests that a chain survives JSON serialization and cloning with its steps intact.
        /// </summary>
        [Fact]
        public void ComplexReference_Serialization()
        {
            ComplexReference complexReference = ComplexReference_CountryToWall();

            Query.SerializationCheck(complexReference);

            ComplexReference? complexReference_Clone = complexReference.Clone() as ComplexReference;
            Assert.NotNull(complexReference_Clone);
            Assert.Equal(complexReference.Count, complexReference_Clone.Count);
            Assert.Equal(complexReference.ToString(), complexReference_Clone.ToString());
        }
    }
}