using DiGi.GML.Classes;

namespace DiGi.GML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that looking up a name no loaded type carries returns null instead of throwing, even when an assembly in the host folder cannot load all its types.
        /// <para>A miss scans every loaded assembly. This test host's folder holds <c>System.Private.Windows.Core.dll</c>, which <c>System.Drawing.Common</c> brings in through <c>DiGi.Core.Drawing</c>. That assembly references <c>System.Formats.Nrbf</c>, which is not deployed, so its <c>GetTypes</c> throws <see cref="System.Reflection.ReflectionTypeLoadException"/>. Before the fix, that exception aborted every GML load that met an unsupported element, such as an <c>OT_SKTR_L</c> feature in a BDOT10k file.</para>
        /// </summary>
        [Fact]
        public void TypeManager_UnknownName()
        {
            TypeManager typeManager = Settings.TypeManager;

            Assert.Null(typeManager.GetType("DiGiGmlNoSuchElement", false));
            Assert.Equal(typeof(Curve), typeManager.GetType(nameof(Curve), false));
        }
    }
}
