using DiGi.Core.Classes;
using DiGi.Core.Interfaces;
using DiGi.Core.Relation.Classes;
using DiGi.Core.Relation.Interfaces;
using System;
using System.Collections.Generic;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        private sealed class TestUniqueObject : GuidObject
        {
            public TestUniqueObject() : base()
            {
            }

            public TestUniqueObject(Guid guid) : base(guid)
            {
            }
        }

        /// <summary>
        /// Tests that a one-to-one bidirectional relation correctly synchronizes and maps references on both ends.
        /// </summary>
        [Fact]
        public void OneToOneBidirectionalRelation_Syncs_Correctly()
        {
            TestUniqueObject objA = new();
            TestUniqueObject objB = new();

            OneToOneBidirectionalRelation relation = new(objA, objB);

            // Verify both ends of the relation are set
            Assert.NotNull(relation.UniqueReference_From);
            Assert.NotNull(relation.UniqueReference_To);
            Assert.Equal(objA.UniqueId, relation.UniqueReference_From.UniqueId);
            Assert.Equal(objB.UniqueId, relation.UniqueReference_To.UniqueId);

            // Test if Contains works for both objects using correct RelationSide and UniqueReferences
            Assert.True(relation.Contains(Relation.Enums.RelationSide.From, Create.UniqueReference(objA)));
            Assert.True(relation.Contains(Relation.Enums.RelationSide.To, Create.UniqueReference(objB)));
        }

        /// <summary>
        /// Tests that a relation cluster can successfully add and manage relations between unique objects.
        /// </summary>
        [Fact]
        public void UniqueObjectRelationCluster_ManageRelations()
        {
            UniqueObjectRelationCluster<TestUniqueObject, IRelation> cluster = new();

            TestUniqueObject objA = new();
            TestUniqueObject objB = new();
            cluster.Add(objA);
            cluster.Add(objB);

            OneToOneBidirectionalRelation relation = new(objA, objB);

            // Add relation to the cluster
            IRelation? addedRelation = cluster.AddRelation(relation);
            Assert.NotNull(addedRelation);

            // Verify relation lookup using uniqueObject parameter
            List<OneToOneBidirectionalRelation>? relations = cluster.GetRelations<OneToOneBidirectionalRelation>(objA);
            Assert.NotNull(relations);
            Assert.Single(relations);
            Assert.Equal(relation, relations[0]);
        }

        private sealed class TestManyToOneRelation : ManyToOneRelation
        {
            public TestManyToOneRelation(IEnumerable<IUniqueReference>? uniqueReferences_From, IUniqueReference? uniqueReference_To)
                : base(uniqueReferences_From, uniqueReference_To)
            {
            }
        }

        /// <summary>
        /// Tests that ManyToOneRelation handles null references during removal operations safely without throwing NullReferenceException.
        /// </summary>
        [Fact]
        public void ManyToOneRelation_NullSafety()
        {
            TestManyToOneRelation relation_ManyToOne = new(null, null);

            // Verify that calling Remove with a null uniqueReferences_From list does not throw NullReferenceException
            List<IUniqueReference> uniqueReferences_ToRemove = [Create.UniqueReference(new TestUniqueObject())!];
            List<IUniqueReference>? uniqueReferences_Removed = relation_ManyToOne.Remove(Relation.Enums.RelationSide.From, uniqueReferences_ToRemove);

            Assert.NotNull(uniqueReferences_Removed);
            Assert.Empty(uniqueReferences_Removed);
        }
    }
}