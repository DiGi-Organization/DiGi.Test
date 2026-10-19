using DiGi.GML.Classes;
using DiGi.GML.xUnit.Classes;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DiGi.GML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that a BDOT10k road geometry (<c>gml:Curve</c> with one <c>gml:LineStringSegment</c>) parses into a <see cref="Curve"/> with its single segment, its <c>posList</c> in order and its SRS attributes.
        /// </summary>
        [Fact]
        public void Curve_SingleSegment()
        {
            string xml =
                "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\" xmlns:t=\"urn:digi:test\" gml:id=\"fc\">" +
                "<gml:featureMember>" +
                "<t:TestFeature gml:id=\"f1\">" +
                "<t:geometria>" +
                "<gml:Curve gml:id=\"id-1-0\" srsName=\"EPSG:2180\" srsDimension=\"2\">" +
                "<gml:segments>" +
                "<gml:LineStringSegment>" +
                "<gml:posList>301061.69 373800.74 301106.04 373789.27</gml:posList>" +
                "</gml:LineStringSegment>" +
                "</gml:segments>" +
                "</gml:Curve>" +
                "</t:geometria>" +
                "</t:TestFeature>" +
                "</gml:featureMember>" +
                "</gml:FeatureCollection>";

            using MemoryStream memoryStream = new(Encoding.UTF8.GetBytes(xml));

            List<FeatureCollection>? featureCollections = Convert.ToGML<FeatureCollection>(memoryStream);
            Assert.NotNull(featureCollections);
            FeatureCollection featureCollection = Assert.Single(featureCollections);

            TestFeature testFeature = Assert.IsType<TestFeature>(Assert.Single(featureCollection.featureMember));
            Curve curve = Assert.IsType<Curve>(testFeature.geometria);

            Assert.Equal("id-1-0", curve.id);
            Assert.Equal("EPSG:2180", curve.srsName);
            Assert.Equal((ushort)2, curve.srsDimension);

            Assert.NotNull(curve.segments);
            LineStringSegment lineStringSegment = Assert.IsType<LineStringSegment>(Assert.Single(curve.segments));
            Assert.Equal([301061.69, 373800.74, 301106.04, 373789.27], lineStringSegment.posList);
        }

        /// <summary>
        /// Verifies that every <c>gml:LineStringSegment</c> under one <c>gml:segments</c> element is kept, in document order.
        /// <para>Regression check for a list wrapper that used to keep only its first child element.</para>
        /// </summary>
        [Fact]
        public void Curve_Segments()
        {
            string xml =
                "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\" xmlns:t=\"urn:digi:test\">" +
                "<gml:featureMember>" +
                "<t:TestFeature>" +
                "<t:geometria>" +
                "<gml:Curve srsName=\"EPSG:2180\" srsDimension=\"2\">" +
                "<gml:segments>" +
                "<gml:LineStringSegment><gml:posList>0 0 1 0</gml:posList></gml:LineStringSegment>" +
                "<gml:LineStringSegment><gml:posList>1 0 1 1</gml:posList></gml:LineStringSegment>" +
                "<gml:LineStringSegment><gml:posList>1 1 2 1.5</gml:posList></gml:LineStringSegment>" +
                "</gml:segments>" +
                "</gml:Curve>" +
                "</t:geometria>" +
                "</t:TestFeature>" +
                "</gml:featureMember>" +
                "</gml:FeatureCollection>";

            using MemoryStream memoryStream = new(Encoding.UTF8.GetBytes(xml));

            List<FeatureCollection>? featureCollections = Convert.ToGML<FeatureCollection>(memoryStream);
            Assert.NotNull(featureCollections);
            FeatureCollection featureCollection = Assert.Single(featureCollections);

            TestFeature testFeature = Assert.IsType<TestFeature>(Assert.Single(featureCollection.featureMember));
            Curve curve = Assert.IsType<Curve>(testFeature.geometria);

            Assert.NotNull(curve.segments);
            Assert.Equal(3, curve.segments.Count);
            Assert.Equal([0, 0, 1, 0], Assert.IsType<LineStringSegment>(curve.segments[0]).posList);
            Assert.Equal([1, 0, 1, 1], Assert.IsType<LineStringSegment>(curve.segments[1]).posList);
            Assert.Equal([1, 1, 2, 1.5], Assert.IsType<LineStringSegment>(curve.segments[2]).posList);
        }
    }
}
