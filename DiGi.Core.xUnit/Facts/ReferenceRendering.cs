using DiGi.Core.Classes;
using DiGi.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that rendering never returns null, including for the shapes that used to: no type reference, a blank
        /// unique identifier, and a null property name. A null string previously propagated into GetHashCode and threw.
        /// </summary>
        [Fact]
        public void ReferenceRendering_ToString_IsNeverNull()
        {
            List<IReference> references = References_Core();
            references.Add(new UniqueIdReference((TypeReference?)null, string.Empty));
            references.Add(new UniqueIdReference((TypeReference?)null, "   "));
            references.Add(new TypeReference((string?)null));
            references.Add(new TypePropertyReference(null, null));
            List<ISerializableReference?> references_Empty = [];
            references.Add(new ComplexReference(references_Empty));

            foreach (IReference reference in references)
            {
                string? value = reference.ToString();

                Assert.NotNull(value);
                Assert.NotEqual(string.Empty, value);

                // The previously throwing path: hashing a reference that rendered to null.
                Assert.Equal(value!.GetHashCode(), reference.GetHashCode());
            }
        }

        /// <summary>
        /// Tests that the cached string is stable across repeated calls and consistent with the cached hash code.
        /// </summary>
        [Fact]
        public void ReferenceRendering_Cache_IsStable()
        {
            foreach (IReference reference in References_Core())
            {
                string? value_1 = reference.ToString();
                string? value_2 = reference.ToString();

                Assert.Equal(value_1, value_2);
                Assert.Equal(reference.GetHashCode(), reference.GetHashCode());
                Assert.Equal(value_1!.GetHashCode(), reference.GetHashCode());
            }
        }

        /// <summary>
        /// Tests that a clone renders and hashes identically to its source.
        /// <para>The base copy constructor deliberately no longer copies the caches, because it runs before the
        /// derived constructor assigns the fields they are derived from. This asserts the lazy rebuild produces the
        /// same answer.</para>
        /// </summary>
        [Fact]
        public void ReferenceRendering_Clone_RendersEqual()
        {
            foreach (IReference reference in References_Core())
            {
                if (reference is not ISerializableObject serializableObject)
                {
                    continue;
                }

                // Render the source first, so its caches are populated before the copy is taken.
                string? value = reference.ToString();

                ISerializableObject? serializableObject_Clone = serializableObject.Clone();
                Assert.NotNull(serializableObject_Clone);

                Assert.Equal(reference.GetType(), serializableObject_Clone.GetType());
                Assert.Equal(value, serializableObject_Clone.ToString());
                Assert.Equal(reference.GetHashCode(), serializableObject_Clone.GetHashCode());
                Assert.True(reference.Equals(serializableObject_Clone as IReference));
            }
        }

        /// <summary>
        /// Tests that a reference rebuilt from JSON renders identically to the original, so nothing poisons the cache
        /// while the base constructor is populating fields from the JSON object.
        /// </summary>
        [Fact]
        public void ReferenceRendering_FromJsonObject_RendersEqual()
        {
            foreach (IReference reference in References_Core())
            {
                if (reference is not ISerializableObject serializableObject)
                {
                    continue;
                }

                JsonObject? jsonObject = serializableObject.ToJsonObject();
                Assert.NotNull(jsonObject);

                ISerializableObject? serializableObject_Parsed = Create.SerializableObject<ISerializableObject>(jsonObject);
                Assert.NotNull(serializableObject_Parsed);

                Assert.Equal(reference.GetType(), serializableObject_Parsed.GetType());
                Assert.Equal(reference.ToString(), serializableObject_Parsed.ToString());
            }
        }

        /// <summary>
        /// Tests that no concrete serializable reference declares its own ToString, and that each declares Segments
        /// instead.
        /// <para>This is what keeps the grammar unforgeable: rendering is owned by the sealed base, so a reference
        /// type cannot omit its discriminator or its escaping. A type that reintroduces a ToString override would not
        /// fail any round-trip test on its own, which is why this is asserted structurally.</para>
        /// </summary>
        [Fact]
        public void ReferenceRendering_ConcreteTypes_DeclareSegmentsNotToString()
        {
            List<Type> types = [];
            foreach (Type type in typeof(SerializableReference).Assembly.GetTypes())
            {
                if (type.IsAbstract || !typeof(SerializableReference).IsAssignableFrom(type))
                {
                    continue;
                }

                types.Add(type);
            }

            Assert.NotEmpty(types);

            foreach (Type type in types)
            {
                MethodInfo? methodInfo_ToString = type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                Assert.True(methodInfo_ToString == null, string.Format(CultureInfo.CurrentCulture, "{0} declares its own ToString; declare Segments instead.", type.Name));

                PropertyInfo? propertyInfo_Segments = type.GetProperty("Segments", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.True(propertyInfo_Segments != null, string.Format(CultureInfo.CurrentCulture, "{0} does not declare Segments.", type.Name));
            }
        }
    }
}