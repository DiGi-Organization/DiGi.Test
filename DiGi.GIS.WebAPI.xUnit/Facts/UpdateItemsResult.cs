using DiGi.GIS.PostgreSQL.Enums;
using DiGi.GIS.WebAPI.Classes;
using System.Collections.Generic;
using System.Globalization;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a converter which could not attempt the update maps to null rather than an empty success.
        /// <para>The converters return null only when there was no connection or the table could not be created. Mapping that onto a result reporting zero rejections would put a database outage behind the same payload as a clean write of nothing.</para>
        /// </summary>
        [Fact]
        public void UpdateItemsResult_NullConverterResult()
        {
            Assert.Null(Create.UpdateItemsResult(null, 5));
        }

        /// <summary>
        /// Verifies that identifiers and rejections are carried across intact.
        /// </summary>
        [Fact]
        public void UpdateItemsResult_FromPostgreSQLUpdateResult()
        {
            PostgreSQL.Classes.PostgreSQLUpdateResult postgreSQLUpdateResult = new([11, 12], [new PostgreSQL.Classes.Rejection("1234.5678.AB_12", UpdateRejectionReason.CountyUnresolved)]);

            UpdateItemsResult? updateItemsResult = Create.UpdateItemsResult(postgreSQLUpdateResult, 3);

            Assert.NotNull(updateItemsResult);
            Assert.Equal(3, updateItemsResult!.Sent);
            Assert.Equal(2, updateItemsResult.Updated);
            Assert.Single(updateItemsResult.Rejected);
            Assert.Equal("1234.5678.AB_12", updateItemsResult.Rejected[0].Reference);
            Assert.Equal(UpdateRejectionReason.CountyUnresolved, updateItemsResult.Rejected[0].Reason);
        }

        /// <summary>
        /// Verifies that a write storing everything reports no rejections.
        /// </summary>
        [Fact]
        public void UpdateItemsResult_NothingRejected()
        {
            PostgreSQL.Classes.PostgreSQLUpdateResult postgreSQLUpdateResult = new([11], null);

            UpdateItemsResult? updateItemsResult = Create.UpdateItemsResult(postgreSQLUpdateResult, 1);

            Assert.NotNull(updateItemsResult);
            Assert.Empty(updateItemsResult!.Rejected);
            Assert.Equal(updateItemsResult.Sent, updateItemsResult.Updated);
        }

        /// <summary>
        /// Verifies that the log fragment names each rejected row with its reason.
        /// </summary>
        [Fact]
        public void RejectionSample_ReferenceAndReason()
        {
            List<UpdateItemsResult.Rejection> rejections =
            [
                new UpdateItemsResult.Rejection { Reference = "A", Reason = UpdateRejectionReason.CountyUnresolved },
                new UpdateItemsResult.Rejection { Reference = "B", Reason = UpdateRejectionReason.MissingGeometry },
            ];

            Assert.Equal("A (CountyUnresolved), B (MissingGeometry)", Query.RejectionSample(rejections));
        }

        /// <summary>
        /// Verifies that a rejection carrying no reference still appears in the sample.
        /// <para>Omitting it would make the sample disagree with the count beside it in the same log line.</para>
        /// </summary>
        [Fact]
        public void RejectionSample_MissingReference()
        {
            List<UpdateItemsResult.Rejection> rejections =
            [
                new UpdateItemsResult.Rejection { Reference = null, Reason = UpdateRejectionReason.Undefined },
            ];

            Assert.Equal("??? (Undefined)", Query.RejectionSample(rejections));
        }

        /// <summary>
        /// Verifies that a long list is truncated and marked as truncated.
        /// <para>A batch can reject thousands of rows, and a log line naming all of them is unreadable - but one that silently stops short reads as the whole story.</para>
        /// </summary>
        [Fact]
        public void RejectionSample_Truncated()
        {
            List<UpdateItemsResult.Rejection> rejections = [];
            for (int i = 0; i < 25; i++)
            {
                rejections.Add(new UpdateItemsResult.Rejection { Reference = i.ToString(CultureInfo.CurrentCulture), Reason = UpdateRejectionReason.CountyUnresolved });
            }

            string sample = Query.RejectionSample(rejections);

            Assert.EndsWith(", ...", sample);
            Assert.Contains("19 (CountyUnresolved)", sample);
            Assert.DoesNotContain("20 (CountyUnresolved)", sample);
        }

        /// <summary>
        /// Verifies that nothing to render produces an empty fragment rather than a throw.
        /// </summary>
        [Fact]
        public void RejectionSample_Empty()
        {
            Assert.Equal(string.Empty, Query.RejectionSample(null));
            Assert.Equal(string.Empty, Query.RejectionSample([]));
        }
    }
}
