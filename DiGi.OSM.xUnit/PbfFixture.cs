using OsmSharp;
using OsmSharp.Streams;
using OsmSharp.Tags;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DiGi.OSM.xUnit
{
    /// <summary>
    /// Builds the OpenStreetMap PBF input the DiGi.OSM facts read: the committed fixture bytes, standalone header blobs for the <see cref="DiGi.OSM.Query.PbfHeader(System.IO.Stream)"/> facts, and a generic blob wrapper for the failure paths.
    /// <para>The fixture cannot be produced by OsmSharp's <c>PBFOsmStreamTarget</c> alone - its <c>HeaderBlock</c> contract omits the <c>osmosis_replication_*</c> fields the reader has to prove - so this class writes the file's first blob itself (raw or zlib, with or without the replication fields) and appends the data blocks OsmSharp produced. Regeneration is a manual step driven by the <c>Footprints_Pbf_FixtureRegenerate</c> fact through the <c>DIGI_OSM_FIXTURE_WRITE</c> environment variable; facts never regenerate the committed bytes.</para>
    /// </summary>
    public static class PbfFixture
    {
        /// <summary>
        /// The replication timestamp the fixture header declares (UTC).
        /// </summary>
        public static readonly DateTimeOffset Timestamp = new(2026, 11, 4, 21, 21, 3, TimeSpan.Zero);

        /// <summary>
        /// The replication sequence the fixture header declares.
        /// </summary>
        public const long ReplicationSequence = 5123;

        /// <summary>
        /// The replication base URL the fixture header declares.
        /// </summary>
        public const string ReplicationBaseUrl = "https://download.geofabrik.de/openstreetmap/poland-updates";

        /// <summary>
        /// The writing program the fixture header declares.
        /// </summary>
        public const string WritingProgram = "DiGi.OSM fixture writer";

        /// <summary>
        /// Builds the whole fixture file: a self-written OSMHeader blob (zlib, carrying the replication fields) followed by the data blocks written by OsmSharp's PBF target.
        /// </summary>
        /// <returns>The complete PBF file bytes.</returns>
        public static byte[] Bytes()
        {
            byte[] dataBytes = Data();

            int FirstBlobEnd(byte[] bytes)
            {
                int length = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                int offset = 4;
                int end = 4 + length;
                long dataLength = -1;

                while (offset < end)
                {
                    (int field, int wireType, ulong value, int next) = ReadField(bytes, offset);
                    offset = next;

                    if (wireType == 0 && field == 3)
                    {
                        dataLength = (long)value;
                    }
                    else if (wireType == 2)
                    {
                        offset += (int)value;
                    }
                }

                if (dataLength < 0)
                {
                    throw new InvalidDataException("The OsmSharp target output carries no blob data length.");
                }

                return end + (int)dataLength;
            }

            int dataStart = FirstBlobEnd(dataBytes);
            byte[] headerBytes = HeaderBlob();

            byte[] result = new byte[headerBytes.Length + (dataBytes.Length - dataStart)];
            Array.Copy(headerBytes, 0, result, 0, headerBytes.Length);
            Array.Copy(dataBytes, dataStart, result, headerBytes.Length, dataBytes.Length - dataStart);
            return result;
        }

        /// <summary>
        /// Builds a standalone OSMHeader blob with the fixture's replication state.
        /// </summary>
        /// <param name="includeReplication">Whether to carry the osmosis replication fields (timestamp, sequence, base URL).</param>
        /// <param name="compress">Whether to zlib-compress the payload (Geofabrik compresses; a raw blob is legal too).</param>
        /// <param name="blobType">The blob type to declare; a failure-path fact passes something other than <c>OSMHeader</c>.</param>
        /// <returns>The blob bytes, including the four-byte length prefix and the BlobHeader.</returns>
        public static byte[] HeaderBlob(bool includeReplication = true, bool compress = true, string blobType = "OSMHeader")
        {
            byte[] payload = HeaderBlock(includeReplication);

            if (compress)
            {
                payload = Zlib(payload);
            }

            return Blob(blobType, payload, compress);
        }

        /// <summary>
        /// Wraps a payload into a PBF blob: the <c>Blob</c> message, the <c>BlobHeader</c> message and the four-byte big-endian length prefix.
        /// </summary>
        /// <param name="blobType">The blob type to declare.</param>
        /// <param name="payload">The payload bytes.</param>
        /// <param name="payloadCompressed">Whether <paramref name="payload"/> is a zlib stream, which is stated as the blob's compression.</param>
        /// <returns>The blob bytes.</returns>
        public static byte[] Blob(string blobType, byte[] payload, bool payloadCompressed)
        {
            using MemoryStream stream = new();

            if (payloadCompressed)
            {
                // Blob: field 3 (zlib_data).
                VarInt(stream, (3UL << 3) | 2);
                VarInt(stream, (ulong)payload.Length);
                stream.Write(payload, 0, payload.Length);
            }
            else
            {
                // Blob: field 1 (raw).
                VarInt(stream, (1UL << 3) | 2);
                VarInt(stream, (ulong)payload.Length);
                stream.Write(payload, 0, payload.Length);
            }

            byte[] blob = stream.ToArray();

            using MemoryStream headerStream = new();
            // BlobHeader: field 1 (type), field 3 (datasize).
            byte[] typeBytes = Encoding.UTF8.GetBytes(blobType);
            VarInt(headerStream, (1UL << 3) | 2);
            VarInt(headerStream, (ulong)typeBytes.Length);
            headerStream.Write(typeBytes, 0, typeBytes.Length);
            VarInt(headerStream, (3UL << 3) | 0);
            VarInt(headerStream, (ulong)blob.Length);

            byte[] header = headerStream.ToArray();

            byte[] result = new byte[4 + header.Length + blob.Length];
            result[0] = (byte)(header.Length >> 24);
            result[1] = (byte)(header.Length >> 16);
            result[2] = (byte)(header.Length >> 8);
            result[3] = (byte)header.Length;
            Array.Copy(header, 0, result, 4, header.Length);
            Array.Copy(blob, 0, result, 4 + header.Length, blob.Length);
            return result;
        }

        /// <summary>
        /// Reads the top-level protobuf field at the given offset: its field number, wire type, its value for varint fields or its length for length-delimited fields, and the offset of the next field.
        /// </summary>
        /// <param name="bytes">The buffer to read.</param>
        /// <param name="offset">The offset of the field key.</param>
        /// <returns>The field number, wire type, value and next offset.</returns>
        /// <exception cref="InvalidDataException">Thrown when the buffer ends inside the field.</exception>
        public static (int Field, int WireType, ulong Value, int Next) ReadField(byte[] bytes, int offset)
        {
            ulong key = 0;
            int shift = 0;
            while (offset < bytes.Length && shift < 64)
            {
                byte current = bytes[offset];
                offset++;
                key |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
            }

            int field = (int)(key >> 3);
            int wireType = (int)(key & 7);

            if (wireType == 0)
            {
                ulong value = 0;
                shift = 0;
                while (offset < bytes.Length && shift < 64)
                {
                    byte current = bytes[offset];
                    offset++;
                    value |= (ulong)(current & 0x7F) << shift;
                    if ((current & 0x80) == 0)
                    {
                        break;
                    }

                    shift += 7;
                }

                return (field, wireType, value, offset);
            }

            if (wireType == 2)
            {
                ulong length = 0;
                shift = 0;
                while (offset < bytes.Length && shift < 64)
                {
                    byte current = bytes[offset];
                    offset++;
                    length |= (ulong)(current & 0x7F) << shift;
                    if ((current & 0x80) == 0)
                    {
                        break;
                    }

                    shift += 7;
                }

                return (field, wireType, length, offset);
            }

            throw new InvalidDataException($"Unsupported wire type {wireType} in the fixture.");
        }

        private static byte[] HeaderBlock(bool includeReplication)
        {
            using MemoryStream stream = new();

            // field 2 (required_features), twice.
            BytesField(stream, 2, Encoding.UTF8.GetBytes("OsmSchema-V0.6"));
            BytesField(stream, 2, Encoding.UTF8.GetBytes("DenseNodes"));

            // field 4 (writingprogram).
            BytesField(stream, 4, Encoding.UTF8.GetBytes(WritingProgram));

            if (includeReplication)
            {
                // field 32 (osmosis_replication_timestamp), field 33 (osmosis_replication_sequence_number), field 34 (osmosis_replication_base_url).
                VarIntField(stream, 32, (ulong)Timestamp.ToUnixTimeSeconds());
                VarIntField(stream, 33, (ulong)ReplicationSequence);
                BytesField(stream, 34, Encoding.UTF8.GetBytes(ReplicationBaseUrl));
            }

            return stream.ToArray();
        }

        private static void VarIntField(MemoryStream stream, int field, ulong value)
        {
            VarInt(stream, (ulong)field << 3);
            VarInt(stream, value);
        }

        private static void BytesField(MemoryStream stream, int field, byte[] value)
        {
            VarInt(stream, ((ulong)field << 3) | 2);
            VarInt(stream, (ulong)value.Length);
            stream.Write(value, 0, value.Length);
        }

        private static void VarInt(MemoryStream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }

            stream.WriteByte((byte)value);
        }

        private static byte[] Zlib(byte[] data)
        {
            using MemoryStream stream = new();
            stream.WriteByte(0x78);
            stream.WriteByte(0x9C);

            using (DeflateStream deflateStream = new(stream, CompressionLevel.Optimal, true))
            {
                deflateStream.Write(data, 0, data.Length);
            }

            uint a = 1;
            uint b = 0;
            foreach (byte current in data)
            {
                a = (a + current) % 65521;
                b = (b + a) % 65521;
            }

            uint adler = (b << 16) | a;
            stream.WriteByte((byte)(adler >> 24));
            stream.WriteByte((byte)(adler >> 16));
            stream.WriteByte((byte)(adler >> 8));
            stream.WriteByte((byte)adler);
            return stream.ToArray();
        }

        private static byte[] Data()
        {
            List<OsmGeo> osmGeos = OsmGeos();

            using MemoryStream memoryStream = new();
            PBFOsmStreamTarget target = new(memoryStream);
            target.RegisterSource(new OsmEnumerableStreamSource(osmGeos));
            target.Pull();
            target.Flush();
            target.Close();
            return memoryStream.ToArray();
        }

        private static List<OsmGeo> OsmGeos()
        {
            Node Node(long id, double latitude, double longitude)
            {
                return new Node { Id = id, Latitude = latitude, Longitude = longitude };
            }

            Way Way(long id, long[] nodes, TagsCollection? tags)
            {
                return new Way { Id = id, Nodes = nodes, Tags = tags };
            }

            Relation Relation(long id, TagsCollection tags, params RelationMember[] members)
            {
                return new Relation { Id = id, Tags = tags, Members = members };
            }

            TagsCollection Tags(params string[] keyValues)
            {
                TagsCollection tags = new();
                for (int i = 0; i + 1 < keyValues.Length; i += 2)
                {
                    tags.Add(keyValues[i], keyValues[i + 1]);
                }

                return tags;
            }

            List<OsmGeo> osmGeos = [];

            // Cluster A around 16.93 E, 51.13 N (Wroclaw-ish): the closed building way 1.
            osmGeos.Add(Node(1, 51.1300, 16.9300));
            osmGeos.Add(Node(2, 51.1300, 16.9301));
            osmGeos.Add(Node(3, 51.1301, 16.9301));
            osmGeos.Add(Node(4, 51.1301, 16.9300));

            // Way 2: an unclosed building way.
            osmGeos.Add(Node(10, 51.1320, 16.9320));
            osmGeos.Add(Node(11, 51.1320, 16.9321));
            osmGeos.Add(Node(12, 51.1321, 16.9321));

            // Way 4: node 32 is deliberately absent from the extract.
            osmGeos.Add(Node(30, 51.1330, 16.9330));
            osmGeos.Add(Node(31, 51.1330, 16.9331));

            // Way 5: a closed way with only two distinct nodes.
            osmGeos.Add(Node(40, 51.1340, 16.9340));
            osmGeos.Add(Node(41, 51.1340, 16.9341));

            // Cluster B around 21.01 E, 52.24 N (Warsaw-ish): the closed building way 6.
            osmGeos.Add(Node(50, 52.2400, 21.0100));
            osmGeos.Add(Node(51, 52.2400, 21.0101));
            osmGeos.Add(Node(52, 52.2401, 21.0101));
            osmGeos.Add(Node(53, 52.2401, 21.0100));

            // Relation 101: one outer ring (nodes 60-63) with one hole (nodes 70-73); the member ways carry no building tag of their own.
            osmGeos.Add(Node(60, 51.1310, 16.9310));
            osmGeos.Add(Node(61, 51.1310, 16.9314));
            osmGeos.Add(Node(62, 51.1314, 16.9314));
            osmGeos.Add(Node(63, 51.1314, 16.9310));
            osmGeos.Add(Node(70, 51.1311, 16.9311));
            osmGeos.Add(Node(71, 51.1311, 16.9313));
            osmGeos.Add(Node(72, 51.1313, 16.9313));
            osmGeos.Add(Node(73, 51.1313, 16.9311));

            // Relation 102: two outer rings (nodes 80-83 and 90-93) and one hole inside the first (nodes 84-87).
            osmGeos.Add(Node(80, 51.1300, 16.9400));
            osmGeos.Add(Node(81, 51.1300, 16.9402));
            osmGeos.Add(Node(82, 51.1302, 16.9402));
            osmGeos.Add(Node(83, 51.1302, 16.9400));
            osmGeos.Add(Node(84, 51.13005, 16.94005));
            osmGeos.Add(Node(85, 51.13005, 16.94015));
            osmGeos.Add(Node(86, 51.13015, 16.94015));
            osmGeos.Add(Node(87, 51.13015, 16.94005));
            osmGeos.Add(Node(90, 51.1300, 16.9450));
            osmGeos.Add(Node(91, 51.1300, 16.9452));
            osmGeos.Add(Node(92, 51.1302, 16.9452));
            osmGeos.Add(Node(93, 51.1302, 16.9450));

            // Relation 103: a triangle outer ring; its second member way (999999) is absent from the extract.
            osmGeos.Add(Node(100, 51.1310, 16.9460));
            osmGeos.Add(Node(101, 51.1310, 16.9462));
            osmGeos.Add(Node(102, 51.1312, 16.9461));

            // Relation 106: a single open member way, whose ring can never close.
            osmGeos.Add(Node(120, 51.1310, 16.9470));
            osmGeos.Add(Node(121, 51.1310, 16.9471));
            osmGeos.Add(Node(122, 51.1311, 16.9472));

            // Way 8: node 500 carries a latitude outside [-90, 90].
            osmGeos.Add(Node(500, 95.0, 16.9350));
            osmGeos.Add(Node(501, 51.1350, 16.9351));
            osmGeos.Add(Node(502, 51.1351, 16.9351));

            osmGeos.Add(Way(1, [1, 2, 3, 4, 1], Tags("building", "yes", "building:levels", "2", "source", "orthophoto", "height", "7")));
            osmGeos.Add(Way(2, [10, 11, 12], Tags("building", "yes")));
            osmGeos.Add(Way(3, [20, 21, 20], Tags("building", "no")));
            osmGeos.Add(Way(4, [30, 31, 32, 30], Tags("building", "yes")));
            osmGeos.Add(Way(5, [40, 41, 40], Tags("building", "yes")));
            osmGeos.Add(Way(6, [50, 51, 52, 53, 50], Tags("building", "yes")));
            osmGeos.Add(Way(7, [1, 2, 1], Tags("building:part", "yes")));
            osmGeos.Add(Way(8, [500, 501, 502, 500], Tags("building", "yes")));

            // Relation member ways, without tags of their own; way 206 belongs to the non-building relation 104, way 207 is open.
            osmGeos.Add(Way(200, [60, 61, 62, 63, 60], null));
            osmGeos.Add(Way(201, [70, 71, 72, 73, 70], null));
            osmGeos.Add(Way(202, [80, 81, 82, 83, 80], null));
            osmGeos.Add(Way(203, [90, 91, 92, 93, 90], null));
            osmGeos.Add(Way(204, [84, 85, 86, 87, 84], null));
            osmGeos.Add(Way(205, [100, 101, 102, 100], null));
            osmGeos.Add(Way(206, [110, 111, 112, 110], null));
            osmGeos.Add(Way(207, [120, 121, 122], null));

            osmGeos.Add(Relation(101, Tags("type", "multipolygon", "building", "yes"), new RelationMember(200, "outer", OsmGeoType.Way), new RelationMember(201, "inner", OsmGeoType.Way)));
            osmGeos.Add(Relation(102, Tags("type", "multipolygon", "building", "yes"), new RelationMember(202, "outer", OsmGeoType.Way), new RelationMember(203, "outer", OsmGeoType.Way), new RelationMember(204, "inner", OsmGeoType.Way)));
            osmGeos.Add(Relation(103, Tags("type", "multipolygon", "building", "yes"), new RelationMember(205, "outer", OsmGeoType.Way), new RelationMember(999999, "outer", OsmGeoType.Way)));
            osmGeos.Add(Relation(104, Tags("type", "multipolygon", "landuse", "forest"), new RelationMember(206, "outer", OsmGeoType.Way)));
            osmGeos.Add(Relation(105, Tags("type", "multipolygon", "building", "no"), new RelationMember(205, "outer", OsmGeoType.Way)));
            osmGeos.Add(Relation(106, Tags("type", "multipolygon", "building", "yes"), new RelationMember(207, "outer", OsmGeoType.Way)));

            return osmGeos;
        }
    }
}
