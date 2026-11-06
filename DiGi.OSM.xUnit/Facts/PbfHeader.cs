using System.IO;

namespace DiGi.OSM.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reads the replication state from the committed fixture (a zlib header blob, the shape Geofabrik publishes): the UTC timestamp, the replication sequence, the base URL and the writing program all come back as written, while an absent file, a null path and a null stream return null instead of throwing.
        /// <para>Short test (under 2 s).</para>
        /// </summary>
        [Fact]
        public void PbfHeader()
        {
            string path = FixturePath("OSM_Buildings_Sample.osm.pbf");

            Classes.PbfHeader? pbfHeader = Query.PbfHeader(path);
            Assert.NotNull(pbfHeader);
            Assert.Equal(PbfFixture.Timestamp, pbfHeader!.Timestamp);
            Assert.Equal(PbfFixture.ReplicationSequence, pbfHeader.ReplicationSequence);
            Assert.Equal(PbfFixture.ReplicationBaseUrl, pbfHeader.ReplicationBaseUrl);
            Assert.Equal(PbfFixture.WritingProgram, pbfHeader.WritingProgram);

            Assert.Null(Query.PbfHeader(Path.Combine(Path.GetTempPath(), "DiGi_OSM_Definitely_Absent.osm.pbf")));
            Assert.Null(Query.PbfHeader((string?)null));
            Assert.Null(Query.PbfHeader((Stream?)null));
        }

        /// <summary>
        /// Exercises every header-blob branch on a stream: zlib and raw payloads both decode, a blob without the osmosis fields reports nulls rather than defaults (the writing program it does carry stays), a blob whose declared type is not OSMHeader and a truncated length prefix both fail loudly with <see cref="InvalidDataException"/>, and the stream position is left where it was found.
        /// <para>Short test (under 2 s).</para>
        /// </summary>
        [Fact]
        public void PbfHeader_BlobVariants()
        {
            using MemoryStream memoryStream = new(PbfFixture.HeaderBlob(includeReplication: true, compress: true));
            Classes.PbfHeader? pbfHeader = Query.PbfHeader(memoryStream);
            Assert.NotNull(pbfHeader);
            Assert.Equal(PbfFixture.Timestamp, pbfHeader!.Timestamp);
            Assert.Equal(0, memoryStream.Position);

            using MemoryStream memoryStream_Raw = new(PbfFixture.HeaderBlob(includeReplication: true, compress: false));
            Classes.PbfHeader? pbfHeader_Raw = Query.PbfHeader(memoryStream_Raw);
            Assert.NotNull(pbfHeader_Raw);
            Assert.Equal(PbfFixture.Timestamp, pbfHeader_Raw!.Timestamp);
            Assert.Equal(PbfFixture.ReplicationSequence, pbfHeader_Raw.ReplicationSequence);

            using MemoryStream memoryStream_Without = new(PbfFixture.HeaderBlob(includeReplication: false));
            Classes.PbfHeader? pbfHeader_Without = Query.PbfHeader(memoryStream_Without);
            Assert.NotNull(pbfHeader_Without);
            Assert.Null(pbfHeader_Without!.Timestamp);
            Assert.Null(pbfHeader_Without.ReplicationSequence);
            Assert.Null(pbfHeader_Without.ReplicationBaseUrl);
            Assert.Equal(PbfFixture.WritingProgram, pbfHeader_Without.WritingProgram);

            using MemoryStream memoryStream_WrongType = new(PbfFixture.HeaderBlob(includeReplication: true, compress: true, blobType: "OSMData"));
            Assert.Throws<InvalidDataException>(() => Query.PbfHeader(memoryStream_WrongType));

            using MemoryStream memoryStream_Truncated = new([0, 0, 0, 5, 1, 2]);
            Assert.Throws<InvalidDataException>(() => Query.PbfHeader(memoryStream_Truncated));
        }
    }
}
