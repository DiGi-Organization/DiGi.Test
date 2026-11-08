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
        /// Tests that no two reference types declare the same discriminator token.
        /// <para>Kind tokens share one flat namespace across every repository, and the manager resolves a duplicate
        /// first-come rather than failing, so a clash would otherwise surface as one type silently parsing as
        /// another. This test is the guard; it covers whichever DiGi assemblies this test project loads.</para>
        /// </summary>
        [Fact]
        public void ReferenceKind_AreUnique()
        {
            Dictionary<string, Type> dictionary = [];

            foreach (ReferenceConstructor referenceConstructor in Settings.ReferenceManager.GetReferenceConstructors())
            {
                string? kind = referenceConstructor.Kind;
                Type? type = referenceConstructor.ReferenceType;

                if (string.IsNullOrWhiteSpace(kind) || type == null)
                {
                    continue;
                }

                Assert.False(
                    dictionary.TryGetValue(kind!, out Type? type_Existing),
                    string.Format(CultureInfo.CurrentCulture, "Duplicate reference kind '{0}' declared by {1} and {2}.", kind, type_Existing?.FullName, type.FullName));

                dictionary[kind!] = type;
            }

            Assert.NotEmpty(dictionary);
        }

        /// <summary>
        /// Tests that no discriminator token contains a comma or a colon.
        /// <para>A comma would make the token parse as an assembly-qualified full type name, and a colon would let it
        /// be split as a separator. Both assumptions underpin the grammar.</para>
        /// </summary>
        [Fact]
        public void ReferenceKind_ContainNoCommaOrColon()
        {
            foreach (ReferenceConstructor referenceConstructor in Settings.ReferenceManager.GetReferenceConstructors())
            {
                string? kind = referenceConstructor.Kind;
                if (string.IsNullOrWhiteSpace(kind))
                {
                    continue;
                }

                Assert.DoesNotContain(",", kind!);
                Assert.DoesNotContain(":", kind!);
                Assert.Equal(kind, kind!.Trim());
            }
        }

        /// <summary>
        /// Tests that every PUBLIC concrete reference type in the loaded DiGi assemblies has a registered factory, so
        /// a new reference type cannot ship silently unparseable.
        /// <para>Internal reference types are excluded on purpose. The only ones are the DiGi.Core.IO wrappers, which
        /// render identically to the reference they wrap and are resolved through their own delegating parser rather
        /// than through a factory; parsing a wrapper's string yields the wrapped public type by design.</para>
        /// </summary>
        [Fact]
        public void ReferenceKind_EveryConcreteReferenceHasFactory()
        {
            List<Type> types = [];

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string? name = assembly.GetName()?.Name;
                if (name == null || !name.StartsWith("DiGi.", StringComparison.Ordinal) || name.EndsWith(".xUnit", StringComparison.Ordinal))
                {
                    continue;
                }

                Type[] types_Assembly;
                try
                {
                    types_Assembly = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    continue;
                }

                foreach (Type type in types_Assembly)
                {
                    if (!type.IsPublic || type.IsAbstract || type.IsGenericTypeDefinition || !typeof(IReference).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    types.Add(type);
                }
            }

            Assert.NotEmpty(types);

            foreach (Type type in types)
            {
                Assert.True(
                    Settings.ReferenceManager.GetReferenceConstructor(type) != null,
                    string.Format(CultureInfo.CurrentCulture, "{0} is a public concrete IReference with no [ReferenceFactory]; it cannot be parsed back.", type.FullName));
            }
        }

        /// <summary>
        /// Tests that every registered reference type renders with its declared discriminator, so the token written
        /// into stored strings is the one the constants declare.
        /// </summary>
        [Fact]
        public void ReferenceKind_MatchesRenderedDiscriminator()
        {
            foreach (IReference reference in References_Core())
            {
                ReferenceConstructor? referenceConstructor = Settings.ReferenceManager.GetReferenceConstructor(reference.GetType());
                Assert.NotNull(referenceConstructor);

                string? discriminator = referenceConstructor.Discriminator;
                Assert.False(string.IsNullOrWhiteSpace(discriminator));

                Assert.StartsWith(discriminator + Constants.Reference.Separator, reference.ToString());
            }
        }
    }
}