using DiGi.GML.Classes;
using System.Collections.Generic;

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

        /// <summary>
        /// Gets or sets a by-reference property bound from an empty element carrying <c>xlink:href</c>.
        /// </summary>
        public string? reference { get; set; }

        /// <summary>
        /// Gets or sets a by-reference list property bound from repeated empty elements carrying <c>xlink:href</c>.
        /// </summary>
        public List<string>? references { get; set; }
    }
}
