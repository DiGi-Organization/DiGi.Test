using DiGi.Core.Interfaces;
using DiGi.Core.Relation.Classes;
using DiGi.Core.Relation.Interfaces;
using System.Collections.Generic;

namespace DiGi.Core.xUnit
{
    public partial class Facts
    {
        private sealed class TestOneToOneRelation : OneToOneRelation
        {
            public TestOneToOneRelation(IUniqueObject? uniqueObject_From, IUniqueObject? uniqueObject_To)
                : base(uniqueObject_From, uniqueObject_To)
            {
            }
        }

        private sealed class TestOneToManyRelation : OneToManyRelation
        {
            public TestOneToManyRelation(IUniqueObject? uniqueObject_From, IEnumerable<IUniqueObject>? uniqueObjects_To)
                : base(uniqueObject_From, uniqueObjects_To)
            {
            }
        }

        private sealed class TestManyToManyRelation : ManyToManyRelation
        {
            public TestManyToManyRelation(IEnumerable<IUniqueObject>? uniqueObjects_From, IEnumerable<IUniqueObject>? uniqueObjects_To)
                : base(uniqueObjects_From, uniqueObjects_To)
            {
            }
        }

        /// <summary>
        /// Tests that RelationCollection correctly removes a relation when removing a reference from a OneToOne relation, and validates index bounds safety.
        /// </summary>
        [Fact]
        public void RelationCollection_OneToOne_Removal()
        {
            TestUniqueObject testUniqueObject_A = new();
            TestUniqueObject testUniqueObject_B = new();

            TestOneToOneRelation testOneToOneRelation = new(testUniqueObject_A, testUniqueObject_B);
            RelationCollection<IRelation> relationCollection = [];
            relationCollection.Add(testOneToOneRelation);

            Assert.Single(relationCollection);

            // Removing one side should entirely remove the OneToOneRelation from the collection
            IUniqueReference? uniqueReference_A = Create.UniqueReference(testUniqueObject_A);
            Assert.True(relationCollection.Remove(uniqueReference_A));
            Assert.Empty(relationCollection);
        }

        /// <summary>
        /// Tests that RelationCollection correctly handles reference removals from a OneToMany relation, updating the relation and only removing it when a side becomes empty.
        /// </summary>
        [Fact]
        public void RelationCollection_OneToMany_Removal()
        {
            TestUniqueObject testUniqueObject_Parent = new();
            TestUniqueObject testUniqueObject_Child1 = new();
            TestUniqueObject testUniqueObject_Child2 = new();

            List<IUniqueObject> testUniqueObjects_Children = [testUniqueObject_Child1, testUniqueObject_Child2];
            TestOneToManyRelation testOneToManyRelation = new(testUniqueObject_Parent, testUniqueObjects_Children);
            RelationCollection<IRelation> relationCollection = [];
            relationCollection.Add(testOneToManyRelation);

            Assert.Single(relationCollection);

            // Removing the first child should modify the relation but keep the relation in the collection
            IUniqueReference? uniqueReference_Child1 = Create.UniqueReference(testUniqueObject_Child1);
            Assert.True(relationCollection.Remove(uniqueReference_Child1));
            Assert.Single(relationCollection);

            List<IUniqueReference>? uniqueReferences_To = testOneToManyRelation.UniqueReferences_To;
            Assert.NotNull(uniqueReferences_To);
            Assert.Single(uniqueReferences_To);
            Assert.Equal(testUniqueObject_Child2.UniqueId, uniqueReferences_To[0].UniqueId);

            // Removing the second child should make the 'To' side empty, causing the entire relation to be removed from the collection
            IUniqueReference? uniqueReference_Child2 = Create.UniqueReference(testUniqueObject_Child2);
            Assert.True(relationCollection.Remove(uniqueReference_Child2));
            Assert.Empty(relationCollection);

            // Test removing the 'From' (parent) side of a OneToManyRelation, which should immediately remove the relation from the collection
            TestOneToManyRelation testOneToManyRelation_Second = new(testUniqueObject_Parent, testUniqueObjects_Children);
            relationCollection.Add(testOneToManyRelation_Second);
            Assert.Single(relationCollection);

            IUniqueReference? uniqueReference_Parent = Create.UniqueReference(testUniqueObject_Parent);
            Assert.True(relationCollection.Remove(uniqueReference_Parent));
            Assert.Empty(relationCollection);
        }

        /// <summary>
        /// Tests that RelationCollection correctly handles reference removals from a ManyToMany relation, updating the relation and only removing it when a side becomes empty.
        /// </summary>
        [Fact]
        public void RelationCollection_ManyToMany_Removal()
        {
            TestUniqueObject testUniqueObject_From1 = new();
            TestUniqueObject testUniqueObject_From2 = new();
            TestUniqueObject testUniqueObject_To1 = new();
            TestUniqueObject testUniqueObject_To2 = new();

            List<IUniqueObject> testUniqueObjects_From = [testUniqueObject_From1, testUniqueObject_From2];
            List<IUniqueObject> testUniqueObjects_To = [testUniqueObject_To1, testUniqueObject_To2];

            TestManyToManyRelation testManyToManyRelation = new(testUniqueObjects_From, testUniqueObjects_To);
            RelationCollection<IRelation> relationCollection = [];
            relationCollection.Add(testManyToManyRelation);

            Assert.Single(relationCollection);

            // Removing from1 should modify the relation but keep it in the collection
            IUniqueReference? uniqueReference_From1 = Create.UniqueReference(testUniqueObject_From1);
            Assert.True(relationCollection.Remove(uniqueReference_From1));
            Assert.Single(relationCollection);

            // Removing from2 should empty the 'From' side, causing the entire relation to be removed from the collection
            IUniqueReference? uniqueReference_From2 = Create.UniqueReference(testUniqueObject_From2);
            Assert.True(relationCollection.Remove(uniqueReference_From2));
            Assert.Empty(relationCollection);
        }
    }
}