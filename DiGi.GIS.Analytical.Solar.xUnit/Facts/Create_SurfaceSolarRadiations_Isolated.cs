using DiGi.EPW.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Solar.Classes;
using System;
using System.Collections.Generic;

namespace DiGi.GIS.Analytical.Solar.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The relative deviation from the open-sky irradiation within which a surface counts as neither shaded nor blocked by its own building.
        /// </summary>
        private const double Create_SurfaceSolarRadiations_Isolated_Tolerance = 0.005;

        /// <summary>
        /// Verifies the isolated-building solve (<see cref="Constants.Default.SolarRadiusIsolated"/>) of a convex box: no surface can shade or block the view of another, so every wall and the roof receive their open-sky irradiation (<c>IrradiationUnshaded</c>) within <see cref="Create_SurfaceSolarRadiations_Isolated_Tolerance"/>.
        /// <para>The invariant that makes <c>IrradiationUnshaded</c> the isolated result for convex buildings only (DiGi-Organization/DiGi.GIS.Analytical#13, DiGi-Organization/DiGi.GIS.PostgreSQL#107).</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiations_Isolated_ConvexBox()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            (ShadingModel shadingModel, Dictionary<string, Vector3D> normals) = SolarFixture_IsolatedShadingModel(SolarFixture_Box());
            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations = shadingModel.SurfaceSolarRadiations(normals, ePWFile, Create.SolarShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiations);
            Assert.Equal(5, surfaceSolarRadiations.Count);

            foreach (GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation in surfaceSolarRadiations)
            {
                Assert.True(Create_SurfaceSolarRadiations_Isolated_Deviation(surfaceSolarRadiation) <= Create_SurfaceSolarRadiations_Isolated_Tolerance, Create_SurfaceSolarRadiations_Isolated_Text(surfaceSolarRadiation));
            }
        }

        /// <summary>
        /// Verifies the isolated-building solve of an L-shaped building: its two inner walls, which face the other wing, receive less than their open-sky irradiation by more than <see cref="Create_SurfaceSolarRadiations_Isolated_Tolerance"/>, while its outer south and west walls and its roof - which nothing of the building faces - match it within that tolerance.
        /// <para>So the isolated solve is not the open-sky <c>IrradiationUnshaded</c> for a concave building (DiGi-Organization/DiGi.GIS.Analytical#13).</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiations_Isolated_LShape()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            (ShadingModel shadingModel, Dictionary<string, Vector3D> normals) = SolarFixture_IsolatedShadingModel(SolarFixture_LShape());
            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations = shadingModel.SurfaceSolarRadiations(normals, ePWFile, Create.SolarShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiations);
            Assert.Equal(7, surfaceSolarRadiations.Count);

            // Walls of one direction are told apart by area (SolarFixture_LShape).
            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_InnerNorth = SolarFixture_Surface(surfaceSolarRadiations, new Vector3D(0, 1, 0), 140);
            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_InnerEast = SolarFixture_Surface(surfaceSolarRadiations, new Vector3D(1, 0, 0), 80);
            Assert.True(Create_SurfaceSolarRadiations_Isolated_Deviation(surfaceSolarRadiation_InnerNorth) > Create_SurfaceSolarRadiations_Isolated_Tolerance, Create_SurfaceSolarRadiations_Isolated_Text(surfaceSolarRadiation_InnerNorth));
            Assert.True(Create_SurfaceSolarRadiations_Isolated_Deviation(surfaceSolarRadiation_InnerEast) > Create_SurfaceSolarRadiations_Isolated_Tolerance, Create_SurfaceSolarRadiations_Isolated_Text(surfaceSolarRadiation_InnerEast));
            Assert.True(surfaceSolarRadiation_InnerNorth.SkyVisibility < 1);
            Assert.True(surfaceSolarRadiation_InnerEast.SkyVisibility < 1);

            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_South = SolarFixture_Surface(surfaceSolarRadiations, new Vector3D(0, -1, 0), 240);
            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_West = SolarFixture_Surface(surfaceSolarRadiations, new Vector3D(-1, 0, 0), 200);
            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_East = SolarFixture_Surface(surfaceSolarRadiations, new Vector3D(1, 0, 0), 120);
            GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation_Roof = SolarFixture_Surface(surfaceSolarRadiations, new Vector3D(0, 0, 1), 368);
            foreach (GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation in new[] { surfaceSolarRadiation_South, surfaceSolarRadiation_West, surfaceSolarRadiation_East, surfaceSolarRadiation_Roof })
            {
                Assert.True(Create_SurfaceSolarRadiations_Isolated_Deviation(surfaceSolarRadiation) <= Create_SurfaceSolarRadiations_Isolated_Tolerance, Create_SurfaceSolarRadiations_Isolated_Text(surfaceSolarRadiation));
            }
        }

        // The relative shortfall of a surface's irradiation below its open-sky irradiation.
        private static double Create_SurfaceSolarRadiations_Isolated_Deviation(GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation)
        {
            Assert.True(surfaceSolarRadiation.IrradiationUnshaded > 0);
            return (surfaceSolarRadiation.IrradiationUnshaded - surfaceSolarRadiation.Irradiation) / surfaceSolarRadiation.IrradiationUnshaded;
        }

        private static string Create_SurfaceSolarRadiations_Isolated_Text(GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation)
        {
            return FormattableString.Invariant($"normal {surfaceSolarRadiation.Normal}, area {surfaceSolarRadiation.Area} m²: irradiation {surfaceSolarRadiation.Irradiation:F2}, open sky {surfaceSolarRadiation.IrradiationUnshaded:F2} kWh/m², sky visibility {surfaceSolarRadiation.SkyVisibility:F4}, ground visibility {surfaceSolarRadiation.GroundVisibility:F4}, deviation {Create_SurfaceSolarRadiations_Isolated_Deviation(surfaceSolarRadiation):P3}");
        }
    }
}
