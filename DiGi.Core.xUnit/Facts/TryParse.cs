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
        /// Builds one instance of every concrete reference type defined in DiGi.Core, all from the same underlying
        /// data, so that round-trip and injectivity can be asserted over the whole family at once.
        /// </summary>
        /// <returns>The references.</returns>
        public static List<IReference> References_Core()
        {
            TypeReference typeReference = new(typeof(TestObject));
            Guid guid = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

            GuidReference guidReference = new(typeReference, guid);
            UniqueIdReference uniqueIdReference = new(typeReference, "BLD-001");

            return
            [
                typeReference,
                guidReference,
                uniqueIdReference,
                new TypePropertyReference(typeReference, "Name"),
                new GuidPropertyReference(guidReference, "Name"),
                new UniqueIdPropertyReference(uniqueIdReference, "Name"),
                new TypeRelatedExternalReference("Revit", typeReference),
                new InstanceRelatedExternalReference("Revit", guidReference),
                new GuidExternalReference("Revit", guidReference),
                new ComplexReference([guidReference, uniqueIdReference]),
            ];
        }

        /// <summary>
        /// Tests that every concrete reference type in DiGi.Core survives a string round trip: the parsed reference
        /// has the same runtime type, renders to the same string, and is equal to the original.
        /// </summary>
        [Fact]
        public void TryParse_RoundTrip_AllConcreteReferenceTypes()
        {
            foreach (IReference reference in References_Core())
            {
                string? value = reference.ToString();
                Assert.False(string.IsNullOrWhiteSpace(value));

                Assert.True(Core.Query.TryParse(value, out IReference? reference_Parsed), value);
                Assert.NotNull(reference_Parsed);

                Assert.Equal(reference.GetType(), reference_Parsed.GetType());
                Assert.Equal(value, reference_Parsed.ToString());
                Assert.True(reference.Equals(reference_Parsed));
                Assert.True(reference_Parsed.Equals(reference));
            }
        }

        /// <summary>
        /// Tests that no two reference types render to the same string when built from identical data.
        /// <para>This is the guard against the collisions the discriminator was introduced to remove: a GUID property
        /// reference against a closed generic unique property reference, and a GUID external reference against an
        /// instance-related external reference, were previously byte-identical.</para>
        /// </summary>
        [Fact]
        public void TryParse_Injectivity_NoCollisionAcrossTypes()
        {
            List<IReference> references = References_Core();

            HashSet<string> strings = [];
            foreach (IReference reference in references)
            {
                Assert.True(strings.Add(reference.ToString()!), string.Format(CultureInfo.CurrentCulture, "Collision on {0}", reference.GetType().Name));
            }

            Assert.Equal(references.Count, strings.Count);
        }

        /// <summary>
        /// Tests that parsing a property reference does not throw. The previous parser sliced the property name using
        /// the unique identifier's length, so every property reference raised an ArgumentOutOfRangeException from a
        /// method following the Try pattern.
        /// </summary>
        [Fact]
        public void TryParse_PropertyReference_DoesNotThrow()
        {
            TypeReference typeReference = new(typeof(TestObject));
            GuidReference guidReference = new(typeReference, Guid.NewGuid());

            foreach (IReference reference in new IReference[]
            {
                new TypePropertyReference(typeReference, "Name"),
                new GuidPropertyReference(guidReference, "Name"),
                new UniqueIdPropertyReference(new UniqueIdReference(typeReference, "BLD-001"), "Name"),
            })
            {
                Assert.True(Core.Query.TryParse(reference.ToString(), out IReference? reference_Parsed));
                Assert.Equal(reference.GetType(), reference_Parsed?.GetType());
            }
        }

        /// <summary>
        /// Tests that malformed input returns false rather than throwing, for every way a reference string can be
        /// wrong: absent, empty, bare, an unknown discriminator, unbalanced nesting, the wrong segment count, an
        /// unparsable payload, and a trailing lone escape character.
        /// </summary>
        [Fact]
        public void TryParse_Malformed_ReturnsFalse()
        {
            string?[] values =
            [
                null,
                string.Empty,
                "   ",
                "Guid",
                "Guid::(",
                "Guid::)",
                "NotAKind::x",
                "Guid::(Type::T)",
                "Guid::(Type::T)::notaguid",
                "Guid::(Type::T)::0f8fad5bd9cb469fa16570867728950e::extra",
                "Type::a\\",
            ];

            foreach (string? value in values)
            {
                Assert.False(Core.Query.TryParse(value, out IReference? reference), value ?? "<null>");
                Assert.Null(reference);
            }
        }

        /// <summary>
        /// Tests that a comma-bearing string that is not a valid assembly-qualified type name - such as a JSON blob -
        /// does not throw. The discriminator branch treats a comma as a full type name and resolves it, which once
        /// threw from the underlying type lookup; parsing must stay total now that it runs on arbitrary input through
        /// <see cref="Core.Query.TryConvert{T}(object, out T)"/>.
        /// </summary>
        [Fact]
        public void TryParse_CommaBearingNonTypeName_DoesNotThrow()
        {
            string json = "{\"_type\":\"DiGi.Core.Classes.GuidReference,DiGi.Core\",\"Guid\":\"x\"}";

            Exception? exception = Record.Exception(() => Core.Query.TryParse(json, out IReference? _));
            Assert.Null(exception);
        }

        /// <summary>
        /// Tests that the generic overload narrows by type, so a string naming one reference type does not parse into
        /// another. This only became reliable once the discriminator named the concrete type.
        /// </summary>
        [Fact]
        public void TryParse_Generic_FiltersByType()
        {
            TypeReference typeReference = new(typeof(TestObject));
            UniqueIdReference uniqueIdReference = new(typeReference, "BLD-001");

            Assert.False(Core.Query.TryParse(uniqueIdReference.ToString(), out GuidReference? guidReference));
            Assert.Null(guidReference);

            Assert.True(Core.Query.TryParse(uniqueIdReference.ToString(), out UniqueIdReference? uniqueIdReference_Parsed));
            Assert.NotNull(uniqueIdReference_Parsed);

            Assert.True(Core.Query.TryParse(uniqueIdReference.ToString(), out UniqueReference? uniqueReference_Parsed));
            Assert.NotNull(uniqueReference_Parsed);
        }

        /// <summary>
        /// Tests that a type reference whose name carries no assembly still round-trips. The previous parser rejected
        /// any string without a comma, so such a reference could never be read back.
        /// </summary>
        [Fact]
        public void TryParse_TypeReference_WithoutComma()
        {
            TypeReference typeReference = new("NoAssemblyQualifiedName");

            Assert.True(Core.Query.TryParse(typeReference.ToString(), out TypeReference? typeReference_Parsed));
            Assert.NotNull(typeReference_Parsed);
            Assert.Equal("NoAssemblyQualifiedName", typeReference_Parsed.FullTypeName);
        }

        /// <summary>
        /// Tests that <see cref="Core.Query.TryConvert{T}(object, out T)"/> converts a reference string into the
        /// requested reference type, for both a concrete type and the base interfaces, and rejects a string naming a
        /// different reference kind.
        /// </summary>
        [Fact]
        public void TryConvert_Reference_FromString()
        {
            TypeReference typeReference = new(typeof(TestObject));
            GuidReference guidReference = new(typeReference, Guid.NewGuid());
            string value = guidReference.ToString()!;

            Assert.True(Core.Query.TryConvert(value, out GuidReference? guidReference_Converted));
            Assert.NotNull(guidReference_Converted);
            Assert.Equal(guidReference.ToString(), guidReference_Converted.ToString());

            Assert.True(Core.Query.TryConvert(value, out UniqueReference? uniqueReference_Converted));
            Assert.IsType<GuidReference>(uniqueReference_Converted);

            Assert.True(Core.Query.TryConvert(value, out IReference? reference_Converted));
            Assert.IsType<GuidReference>(reference_Converted);

            // A GUID reference string is not a unique-id reference.
            Assert.False(Core.Query.TryConvert(value, out UniqueIdReference? uniqueIdReference_Converted));
            Assert.Null(uniqueIdReference_Converted);
        }

        /// <summary>
        /// Tests that a reference type not derived from <see cref="ISerializableObject"/> - here a relation list
        /// cluster reference - still converts from its string, which only the reference path can do since there is no
        /// JSON fallback for it.
        /// </summary>
        [Fact]
        public void TryConvert_Reference_NonSerializableReference()
        {
            Relation.Classes.RelationListClusterReference relationListClusterReference = new(
                new TypeReference(typeof(TestObject)),
                new TypeReference(typeof(Classes.UniqueIdObject)),
                3);

            Assert.True(Core.Query.TryConvert(relationListClusterReference.ToString(), out Relation.Classes.RelationListClusterReference? reference_Converted));
            Assert.NotNull(reference_Converted);
            Assert.Equal(relationListClusterReference.ToString(), reference_Converted.ToString());
        }

        /// <summary>
        /// Tests that converting to a reference type still accepts a JSON serialization, so the reference path does
        /// not regress the existing JSON-to-object behaviour when the string is not the reference grammar.
        /// </summary>
        [Fact]
        public void TryConvert_Reference_FromJson()
        {
            GuidReference guidReference = new(new TypeReference(typeof(TestObject)), Guid.NewGuid());
            string? json = Convert.ToSystem_String(guidReference);
            Assert.False(string.IsNullOrWhiteSpace(json));

            Assert.True(Core.Query.TryConvert(json, out GuidReference? guidReference_Converted));
            Assert.NotNull(guidReference_Converted);
            Assert.Equal(guidReference.ToString(), guidReference_Converted.ToString());
        }
    }
}