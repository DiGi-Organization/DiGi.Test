using DiGi.Core.Classes;
using DiGi.Core.Interfaces;
using System.Globalization;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that an assembly-qualified full type name still works as a discriminator on read.
        /// <para>Nothing writes this form - a type declaring a kind is written with its kind - but it is retained as
        /// an accepted input, because it is self-locating: it names the assembly, so a type can be resolved without
        /// that assembly already being loaded, which a short kind cannot do.</para>
        /// </summary>
        [Fact]
        public void ReferenceManager_FullTypeNameDiscriminator_IsAccepted()
        {
            TypeReference typeReference = new(typeof(TestObject));

            string? fullTypeName = Core.Query.FullTypeName(typeof(TypeReference));
            Assert.False(string.IsNullOrWhiteSpace(fullTypeName));

            string value = string.Format(CultureInfo.CurrentCulture, "{0}{1}{2}", fullTypeName, Constants.Reference.Separator, Core.Query.Segment(typeReference.FullTypeName));

            Assert.True(Core.Query.TryParse(value, out TypeReference? typeReference_Parsed));
            Assert.NotNull(typeReference_Parsed);
            Assert.Equal(typeReference.FullTypeName, typeReference_Parsed.FullTypeName);

            // The kind is what gets written, so the two forms are not string-equal even though both parse.
            Assert.NotEqual(value, typeReference_Parsed.ToString());
            Assert.StartsWith(Constants.Reference.Kind.Type, typeReference_Parsed.ToString());
        }

        /// <summary>
        /// Tests that a discriminator naming a type that exists but is not a reference does not resolve, since that is
        /// exactly what a legacy string looks like.
        /// </summary>
        [Fact]
        public void ReferenceManager_NonReferenceType_DoesNotResolve()
        {
            string? fullTypeName = Core.Query.FullTypeName(typeof(TestObject));
            Assert.False(string.IsNullOrWhiteSpace(fullTypeName));

            Assert.Null(Settings.ReferenceManager.GetReferenceConstructor(fullTypeName));
        }

        /// <summary>
        /// Tests that an unknown discriminator resolves to nothing and parses to false, without throwing.
        /// </summary>
        [Fact]
        public void ReferenceManager_UnknownDiscriminator_ReturnsNull()
        {
            Assert.Null(Settings.ReferenceManager.GetReferenceConstructor("ThisKindDoesNotExist"));
            Assert.Null(Settings.ReferenceManager.GetReferenceConstructor((string?)null));
            Assert.Null(Settings.ReferenceManager.GetReferenceConstructor((System.Type?)null));

            Assert.False(Core.Query.TryParse("ThisKindDoesNotExist::x::y", out IReference? reference));
            Assert.Null(reference);
        }

        /// <summary>
        /// Tests that a reference type defined outside DiGi.Core resolves through the reflective factory lookup, which
        /// is what lets DiGi.Core parse types it holds no reference to.
        /// </summary>
        [Fact]
        public void ReferenceManager_ResolvesFactory_FromDownstreamAssembly()
        {
            Relation.Classes.RelationListClusterReference relationListClusterReference = new(
                new TypeReference(typeof(TestObject)),
                new TypeReference(typeof(Classes.UniqueIdObject)),
                3);

            Assert.True(Core.Query.TryParse(relationListClusterReference.ToString(), out IReference? reference));
            Assert.IsType<Relation.Classes.RelationListClusterReference>(reference);
            Assert.Equal(relationListClusterReference.ToString(), reference.ToString());
            Assert.True(relationListClusterReference.Equals(reference));
        }

        /// <summary>
        /// Tests that a relation list cluster reference no longer reports equality against a different reference type,
        /// which its hash-code-only comparison allowed on any collision.
        /// </summary>
        [Fact]
        public void ReferenceManager_RelationListClusterReference_Equality()
        {
            TypeReference typeReference_1 = new(typeof(TestObject));
            TypeReference typeReference_2 = new(typeof(Classes.UniqueIdObject));

            Relation.Classes.RelationListClusterReference relationListClusterReference_1 = new(typeReference_1, typeReference_2, 3);
            Relation.Classes.RelationListClusterReference relationListClusterReference_2 = new(typeReference_1, typeReference_2, 3);
            Relation.Classes.RelationListClusterReference relationListClusterReference_3 = new(typeReference_1, typeReference_2, 4);

            Assert.True(relationListClusterReference_1.Equals(relationListClusterReference_2));
            Assert.False(relationListClusterReference_1.Equals(relationListClusterReference_3));
            Assert.False(relationListClusterReference_1.Equals(typeReference_1));
        }
    }
}