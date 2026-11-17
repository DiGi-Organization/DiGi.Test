using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using System.Collections.Generic;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a result built from nothing exposes usable, empty collections.
        /// <para>Both are read-only properties the callers enumerate directly, so a null one would throw where a clean write of nothing should simply report nothing.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLUpdateResult_Empty()
        {
            PostgreSQLUpdateResult postgreSQLUpdateResult = new(null, null);

            Assert.NotNull(postgreSQLUpdateResult.Ids);
            Assert.NotNull(postgreSQLUpdateResult.Rejections);
            Assert.Empty(postgreSQLUpdateResult.Ids);
            Assert.Empty(postgreSQLUpdateResult.Rejections);

            Core.xUnit.Query.SerializationCheck(postgreSQLUpdateResult);
        }

        /// <summary>
        /// Verifies that rejections are counted one per dropped row while identifiers deduplicate.
        /// <para>This asymmetry is the reason the drop has to be returned rather than inferred: identifiers arrive as a set, and two rows colliding on <c>(reference, county_id)</c> return the same one, so a shortfall in the identifier count proves nothing. The rejection count is the exact figure.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLUpdateResult_IdsDeduplicateRejectionsDoNot()
        {
            PostgreSQLUpdateResult postgreSQLUpdateResult = new(
                [11, 11, 12],
                [
                    new Rejection("1234.5678.AB_12", UpdateRejectionReason.CountyUnresolved),
                    new Rejection("1234.5678.AB_12", UpdateRejectionReason.CountyUnresolved),
                ]);

            Assert.Equal(2, postgreSQLUpdateResult.Ids.Count);
            Assert.Equal(2, postgreSQLUpdateResult.Rejections.Count);
        }

        /// <summary>
        /// Verifies that the result does not alias the collections it was handed.
        /// <para>The converters build these locally and keep adding to nothing afterwards, but a result that shared the caller's list would let any later edit rewrite an outcome already reported.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLUpdateResult_DoesNotAliasSourceCollections()
        {
            HashSet<long> ids = [11];
            List<Rejection> rejections = [new Rejection("A", UpdateRejectionReason.CountyUnresolved)];

            PostgreSQLUpdateResult postgreSQLUpdateResult = new(ids, rejections);

            ids.Add(12);
            rejections.Add(new Rejection("B", UpdateRejectionReason.MissingGeometry));

            Assert.Single(postgreSQLUpdateResult.Ids);
            Assert.Single(postgreSQLUpdateResult.Rejections);
        }

        /// <summary>
        /// Verifies that a rejection with no reference is still a rejection, and survives a round trip.
        /// <para>A null element carries nothing to name, and dropping it silently would break the one guarantee the rejection list offers - that it accounts for every row which never reached the database.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLUpdateResult_RejectionWithoutReference()
        {
            PostgreSQLUpdateResult postgreSQLUpdateResult = new([], [new Rejection(null, UpdateRejectionReason.Undefined)]);

            Assert.Single(postgreSQLUpdateResult.Rejections);
            Assert.Null(postgreSQLUpdateResult.Rejections[0].Reference);
            Assert.Equal(UpdateRejectionReason.Undefined, postgreSQLUpdateResult.Rejections[0].UpdateRejectionReason);

            Core.xUnit.Query.SerializationCheck(postgreSQLUpdateResult);
        }

        /// <summary>
        /// Verifies that a populated result survives a JSON round trip and a clone.
        /// </summary>
        [Fact]
        public void PostgreSQLUpdateResult_Serialization()
        {
            PostgreSQLUpdateResult postgreSQLUpdateResult = new(
                [11, 12],
                [
                    new Rejection("1234.5678.AB_12", UpdateRejectionReason.CountyUnresolved),
                    new Rejection(null, UpdateRejectionReason.Undefined),
                    new Rejection("1234.5678.AB_13", UpdateRejectionReason.PartitionUnavailable),
                ]);

            Core.xUnit.Query.SerializationCheck(postgreSQLUpdateResult);

            PostgreSQLUpdateResult postgreSQLUpdateResult_Clone = new(postgreSQLUpdateResult);

            Assert.Equal(postgreSQLUpdateResult.Ids, postgreSQLUpdateResult_Clone.Ids);
            Assert.Equal(3, postgreSQLUpdateResult_Clone.Rejections.Count);
            Assert.Equal("1234.5678.AB_12", postgreSQLUpdateResult_Clone.Rejections[0].Reference);
            Assert.Equal(UpdateRejectionReason.PartitionUnavailable, postgreSQLUpdateResult_Clone.Rejections[2].UpdateRejectionReason);

            // A cloned rejection has to be a new instance, or the copy constructor is only pretending.
            Assert.NotSame(postgreSQLUpdateResult.Rejections[0], postgreSQLUpdateResult_Clone.Rejections[0]);
        }

        /// <summary>
        /// Verifies that a rejection survives a JSON round trip on its own.
        /// </summary>
        [Fact]
        public void Rejection_Serialization()
        {
            Rejection rejection = new("1234.5678.AB_12", UpdateRejectionReason.MissingGeometry);

            Assert.Equal("1234.5678.AB_12", rejection.Reference);
            Assert.Equal(UpdateRejectionReason.MissingGeometry, rejection.UpdateRejectionReason);

            Core.xUnit.Query.SerializationCheck(rejection);
        }

        /// <summary>
        /// Verifies the numeric values of <see cref="UpdateRejectionReason"/>.
        /// <para>The reason reaches the wire through a write endpoint's response, so renumbering it is a breaking API change even though the host renders it as a string.</para>
        /// </summary>
        [Fact]
        public void UpdateRejectionReason_Values()
        {
            Assert.Equal(0, (int)UpdateRejectionReason.Undefined);
            Assert.Equal(1, (int)UpdateRejectionReason.MissingGeometry);
            Assert.Equal(2, (int)UpdateRejectionReason.CountyUnresolved);
            Assert.Equal(3, (int)UpdateRejectionReason.PartitionUnavailable);
            Assert.Equal(4, (int)UpdateRejectionReason.MissingCounty);
            Assert.Equal(5, (int)UpdateRejectionReason.GeometryRejected);
            Assert.Equal(6, (int)UpdateRejectionReason.NoImages);
        }

        /// <summary>
        /// Verifies that a rejection with the <c>MissingCounty</c> reason survives a JSON round trip.
        /// <para>This is the reason the <c>Building2DReferencedObjectPostgreSQLConverter</c> records when a row arrives with no county identifier - the caller failed to state which part the row belongs to.</para>
        /// </summary>
        [Fact]
        public void Rejection_MissingCounty_Serialization()
        {
            Rejection rejection = new("1234.5678.AB_12", UpdateRejectionReason.MissingCounty);

            Assert.Equal("1234.5678.AB_12", rejection.Reference);
            Assert.Equal(UpdateRejectionReason.MissingCounty, rejection.UpdateRejectionReason);

            Core.xUnit.Query.SerializationCheck(rejection);
        }
    }
}
