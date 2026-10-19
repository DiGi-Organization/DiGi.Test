using DiGi.BDOT10k.Classes;
using DiGi.BDOT10k.Interfaces;
using DiGi.GML.Classes;

namespace DiGi.BDOT10k.Geometry.xUnit.Classes
{
    /// <summary>
    /// A BDOT10k surface object that <see cref="Convert.ToDiGi(IOT_ObiektGeometryczny?)"/> has no branch for, used to prove that an unsupported class returns null instead of throwing.
    /// <para>The GML type manager binds XML elements to types by simple class name, so the name is deliberately not a BDOT10k element name.</para>
    /// </summary>
    public class UnsupportedObiektGeometryczny : OT_ObiektTopograficzny, IOT_PowierzchniowyObiektGeometryczny
    {
        /// <summary>
        /// Gets or sets the surface geometry of the object.
        /// </summary>
        public Surface geometria { get; set; } = new Polygon();
    }
}
