using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.WebAPI.UI.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a degenerate model - the shape of the sliver buildings of the 2026-09-24 production run, walls without a roof - gets no results rather than results for surfaces whose outward side is unknown.
        /// <para>Three components of one space cannot close an envelope (a closed solid needs at least four faces), so <c>GetExternalShell</c> answers null and no surface has an outward normal. The calculation answers null, which the controller turns into a 422. An unstamped model (no coordinates, so no sun) answers null too.</para>
        /// </summary>
        [Fact]
        public void Create_SurfaceSolarRadiationResults_Degenerate()
        {
            List<IComponent> components = SolarFixture_BoxComponents(SolarFixture_Origin.X, SolarFixture_Origin.Y, 10);

            // West wall, east wall and floor: the north and south walls and the roof are left out.
            BuildingModel buildingModel = SolarFixture_BuildingModel([components[0], components[1], components[3]], new Point3D(SolarFixture_Origin.X + 5, SolarFixture_Origin.Y + 5, 5));
            Assert.True(GIS.Analytical.Modify.UpdateBuildingInformation(buildingModel));

            Assert.Null(buildingModel.SolarReceiverNormals());
            Assert.Null(buildingModel.SurfaceSolarRadiationResults(null, SolarFixture_EPWFile(), SolarFixture_ShadingSolverOptions()));

            // A complete box that was never stamped has an envelope but no location.
            BuildingModel buildingModel_Unstamped = SolarFixture_BuildingModel(SolarFixture_BoxComponents(SolarFixture_Origin.X, SolarFixture_Origin.Y, 10), new Point3D(SolarFixture_Origin.X + 5, SolarFixture_Origin.Y + 5, 5));
            Assert.NotNull(buildingModel_Unstamped.SolarReceiverNormals());
            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults_Unstamped = buildingModel_Unstamped.SurfaceSolarRadiationResults(null, SolarFixture_EPWFile(), SolarFixture_ShadingSolverOptions());
            Assert.Null(surfaceSolarRadiationResults_Unstamped);
        }
    }
}
