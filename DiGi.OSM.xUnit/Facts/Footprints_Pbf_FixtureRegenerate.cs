using System;
using System.IO;
using System.Reflection;

namespace DiGi.OSM.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Regenerates the committed fixture <c>OSM_Buildings_Sample.osm.pbf</c> from <see cref="PbfFixture"/> - a manual maintenance step, never part of a normal run: the fact returns without asserting unless the <c>DIGI_OSM_FIXTURE_WRITE</c> environment variable is set, and the committed bytes stay authoritative for every other fact. When it runs, it writes the file and reads its header back as the round-trip check.
        /// <para>Short test (under 2 s); a no-op unless DIGI_OSM_FIXTURE_WRITE is set.</para>
        /// </summary>
        [Fact]
        public void Footprints_Pbf_FixtureRegenerate()
        {
            string? target = Environment.GetEnvironmentVariable("DIGI_OSM_FIXTURE_WRITE");
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            string? directory = Core.xUnit.Query.FilesDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(directory);

            string path = Path.Combine(directory!, "OSM_Buildings_Sample.osm.pbf");
            File.WriteAllBytes(path, PbfFixture.Bytes());

            Classes.PbfHeader? pbfHeader = Query.PbfHeader(path);
            Assert.NotNull(pbfHeader);
            Assert.Equal(PbfFixture.Timestamp, pbfHeader!.Timestamp);
            Assert.Equal(PbfFixture.ReplicationSequence, pbfHeader.ReplicationSequence);
            Assert.Equal(PbfFixture.ReplicationBaseUrl, pbfHeader.ReplicationBaseUrl);
        }
    }
}
