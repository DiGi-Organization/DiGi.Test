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
        /// Parses the given GML document and returns its single <see cref="TestFeature"/> member.
        /// </summary>
        /// <param name="xml">The GML document to parse.</param>
        /// <returns>The single <see cref="TestFeature"/> found in the first feature collection.</returns>
        private static TestFeature ParseTestFeature(string xml)
        {
            using MemoryStream memoryStream = new(Encoding.UTF8.GetBytes(xml));

            List<FeatureCollection>? featureCollections = Convert.ToGML<FeatureCollection>(memoryStream);
            Assert.NotNull(featureCollections);
            FeatureCollection featureCollection = Assert.Single(featureCollections);

            return Assert.IsType<TestFeature>(Assert.Single(featureCollection.featureMember));
        }

        /// <summary>
        /// Verifies that an empty property element carrying <c>xlink:href</c> binds its href verbatim to a <c>string</c> property (DiGi-Organization/DiGi.GML#3).
        /// <para>The reference is stored unresolved — resolving it by <c>gml:id</c> is the consumer's job.</para>
        /// </summary>
        [Fact]
        public void Update_Href_String()
        {
            string xml =
                "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\" xmlns:t=\"urn:digi:test\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">" +
                "<gml:featureMember>" +
                "<t:TestFeature gml:id=\"f1\">" +
                "<t:reference xlink:href=\"#PL.ZIPIN.1050.EMUiA_7310b7a5\"/>" +
                "</t:TestFeature>" +
                "</gml:featureMember>" +
                "</gml:FeatureCollection>";

            TestFeature testFeature = ParseTestFeature(xml);

            Assert.Equal("#PL.ZIPIN.1050.EMUiA_7310b7a5", testFeature.reference);
        }

        /// <summary>
        /// Verifies that repeated empty property elements carrying <c>xlink:href</c> bind one href per occurrence to a <c>List&lt;string&gt;</c> property, in document order (DiGi-Organization/DiGi.GML#3).
        /// </summary>
        [Fact]
        public void Update_Href_List()
        {
            string xml =
                "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\" xmlns:t=\"urn:digi:test\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">" +
                "<gml:featureMember>" +
                "<t:TestFeature gml:id=\"f1\">" +
                "<t:references xlink:href=\"#a\"/>" +
                "<t:references xlink:href=\"#b\"/>" +
                "<t:references xlink:href=\"#c\"/>" +
                "</t:TestFeature>" +
                "</gml:featureMember>" +
                "</gml:FeatureCollection>";

            TestFeature testFeature = ParseTestFeature(xml);

            Assert.NotNull(testFeature.references);
            Assert.Equal(["#a", "#b", "#c"], testFeature.references);
        }

        /// <summary>
        /// Verifies that the XLink <b>namespace</b> — never the prefix — decides whether an href binds: the namespace bound to <c>x:</c> still binds, an <c>href</c> in a foreign namespace does not (DiGi-Organization/DiGi.GML#3).
        /// </summary>
        [Fact]
        public void Update_Href_NamespaceDecidesPrefix()
        {
            string xml_Prefix =
                "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\" xmlns:t=\"urn:digi:test\" xmlns:x=\"http://www.w3.org/1999/xlink\">" +
                "<gml:featureMember>" +
                "<t:TestFeature>" +
                "<t:reference x:href=\"#b\"/>" +
                "</t:TestFeature>" +
                "</gml:featureMember>" +
                "</gml:FeatureCollection>";

            TestFeature testFeature_Prefix = ParseTestFeature(xml_Prefix);
            Assert.Equal("#b", testFeature_Prefix.reference);

            string xml_Foreign =
                "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\" xmlns:t=\"urn:digi:test\" xmlns:o=\"urn:digi:test:other\">" +
                "<gml:featureMember>" +
                "<t:TestFeature>" +
                "<t:reference o:href=\"#c\"/>" +
                "</t:TestFeature>" +
                "</gml:featureMember>" +
                "</gml:FeatureCollection>";

            TestFeature testFeature_Foreign = ParseTestFeature(xml_Foreign);
            Assert.Null(testFeature_Foreign.reference);
        }

        /// <summary>
        /// Verifies that an empty property element <b>without</b> an <c>href</c> is still skipped, leaving the property at its default (DiGi-Organization/DiGi.GML#3 regression check).
        /// </summary>
        [Fact]
        public void Update_EmptyWithoutHref_StillSkipped()
        {
            string xml =
                "<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\" xmlns:t=\"urn:digi:test\">" +
                "<gml:featureMember>" +
                "<t:TestFeature>" +
                "<t:reference/>" +
                "<t:references/>" +
                "<t:references/>" +
                "</t:TestFeature>" +
                "</gml:featureMember>" +
                "</gml:FeatureCollection>";

            TestFeature testFeature = ParseTestFeature(xml);

            Assert.Null(testFeature.reference);
            Assert.Null(testFeature.references);
        }
    }
}
