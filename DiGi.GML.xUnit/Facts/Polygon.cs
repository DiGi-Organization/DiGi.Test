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
        /// Verifies that a <c>gml:Polygon</c> with two <c>gml:interior</c> elements keeps its exterior and both interior rings, in document order.
        /// <para>Regression check that list wrappers repeated once per item still produce one item per wrapper.</para>
        /// </summary>
        [Fact]
        public void Polygon_Interior()
        {
            string xml =
                "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\" xmlns:t=\"urn:digi:test\">" +
                "<gml:featureMember>" +
                "<t:TestFeature>" +
                "<t:geometria>" +
                "<gml:Polygon srsName=\"EPSG:2180\" srsDimension=\"2\">" +
                "<gml:exterior><gml:LinearRing><gml:posList>0 0 10 0 10 10 0 10 0 0</gml:posList></gml:LinearRing></gml:exterior>" +
                "<gml:interior><gml:LinearRing><gml:posList>1 1 2 1 2 2 1 1</gml:posList></gml:LinearRing></gml:interior>" +
                "<gml:interior><gml:LinearRing><gml:posList>5 5 6 5 6 6 5 5</gml:posList></gml:LinearRing></gml:interior>" +
                "</gml:Polygon>" +
                "</t:geometria>" +
                "</t:TestFeature>" +
                "</gml:featureMember>" +
                "</gml:FeatureCollection>";

            using MemoryStream memoryStream = new(Encoding.UTF8.GetBytes(xml));

            List<FeatureCollection>? featureCollections = Convert.ToGML<FeatureCollection>(memoryStream);
            Assert.NotNull(featureCollections);
            FeatureCollection featureCollection = Assert.Single(featureCollections);

            TestFeature testFeature = Assert.IsType<TestFeature>(Assert.Single(featureCollection.featureMember));
            Polygon polygon = Assert.IsType<Polygon>(testFeature.geometria);

            Assert.NotNull(polygon.exterior);
            Assert.Equal([0, 0, 10, 0, 10, 10, 0, 10, 0, 0], polygon.exterior.posList);

            Assert.NotNull(polygon.interior);
            Assert.Equal(2, polygon.interior.Count);
            Assert.Equal([1, 1, 2, 1, 2, 2, 1, 1], polygon.interior[0].posList);
            Assert.Equal([5, 5, 6, 5, 6, 6, 5, 5], polygon.interior[1].posList);
        }
    }
}
