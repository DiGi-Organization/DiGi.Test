using DiGi.GML.Classes;

namespace DiGi.GML.xUnit.Classes
{
    /// <summary>
    /// Minimal GML feature used by the facts to carry a single geometry, in the shape of a BDOT10k object (<c>ot:geometria</c>).
    /// <para>The type manager binds XML elements to types by simple class name, so the facts' XML uses a <c>TestFeature</c> element.</para>
    /// </summary>
    public class TestFeature : AbstractFeature
    {
        /// <summary>
        /// Gets or sets the geometry of the feature.
        /// </summary>
        public GeometricPrimitive? geometria { get; set; }
    }
}
