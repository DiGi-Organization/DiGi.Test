using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Solar;
using DiGi.Core.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.WebAPI.UI.Classes;
using DiGi.Solar.Classes;
using System.Collections.Generic;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a caster south of the building shades the south wall and leaves the north wall untouched, by solving the same box twice - alone, and with a 60 m high screen 5 m south of it.
        /// <para>A differential assertion (Coding - Automatic Tests, section 4): the screen is the only difference. It stands behind the north wall's plane, so it can never lie between that wall and the sun, its sky or its ground - the north wall's results must be identical. The south wall loses most of its beam, and since DiGi-Organization/DiGi.GIS.WebAPI.UI#61 also part of its sky diffuse and ground-reflected radiation: the screen blocks part of its sky and, reaching 20 m below ground level, part of its ground. Its open-sky irradiation is unchanged. The screen is added as a shading-only element of the shading model, the kind <c>ToSolar</c> makes of every neighbour.</para>
        /// <para>Alone, the south wall is unshaded up to the solver's direction grouping: hours whose sun lies within <see cref="GIS.Analytical.Solar.Constants.Default.SolarAngleTolerance"/> of each other are solved with one representative direction, so at a grazing hour the representative can fall behind the wall while the hour's own sun is just in front of it, and the box then shades its own wall. Measured, this costs the wall 0.013 of 738 kWh/m²; the bound is 0.1 kWh/m², far below the beam the screen takes.</para>
        /// <para>Medium test (4.0 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiationResults_Surroundings()
        {
            BuildingModel buildingModel = SolarFixture_Box();
            EPWFile ePWFile = SolarFixture_EPWFile();

            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            ShadingModel? shadingModel = buildingModel.ToSolar(null, x => new GuidReference(x).ToString() is string reference && normals.ContainsKey(reference));
            Assert.NotNull(shadingModel);

            ShadingModel? shadingModel_Screen = buildingModel.ToSolar(null, x => new GuidReference(x).ToString() is string reference && normals.ContainsKey(reference));
            Assert.NotNull(shadingModel_Screen);

            // An 80 m square facing north, centred 5 m south of the south wall: it spans at least 35 m either side of the box and rises 60 m above the ground.
            PolygonalFace3D? polygonalFace3D_Screen = Geometry.Spatial.Create.PolygonalFace3D(new Plane(new Point3D(SolarFixture_Origin.X + 5, SolarFixture_Origin.Y - 5, 20), new Vector3D(0, 1, 0)), [new Point2D(-40, -40), new Point2D(40, -40), new Point2D(40, 40), new Point2D(-40, 40)]);
            Assert.NotNull(polygonalFace3D_Screen);
            Assert.True(shadingModel_Screen.Update(new ShadingElement("Screen", polygonalFace3D_Screen, true)));

            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults = shadingModel.SurfaceSolarRadiationResults(normals, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults);

            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults_Screen = shadingModel_Screen.SurfaceSolarRadiationResults(normals, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults_Screen);

            SurfaceSolarRadiationResult south = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(0, -1, 0));
            SurfaceSolarRadiationResult south_Screen = SolarFixture_Result(surfaceSolarRadiationResults_Screen, normals, new Vector3D(0, -1, 0));

            // Unobstructed, the south wall keeps all its beam; behind the screen it loses most of it.
            Assert.True(south.IrradiationUnshaded - south.Irradiation < 0.1, $"south {south.Irradiation} of {south.IrradiationUnshaded} unshaded");
            Assert.True(south_Screen.Beam < south.Beam * 0.5, $"south beam {south.Beam} alone, {south_Screen.Beam} behind the screen");
            Assert.Equal(1.0, south.SkyVisibility);
            Assert.Equal(1.0, south.GroundVisibility);
            Assert.True(south_Screen.SkyVisibility < 0.9, $"south sky visibility {south_Screen.SkyVisibility} behind the screen");
            Assert.True(south_Screen.GroundVisibility < 0.9, $"south ground visibility {south_Screen.GroundVisibility} behind the screen");
            Assert.Equal(south.Diffuse * south_Screen.SkyVisibility, south_Screen.Diffuse, 9);
            Assert.Equal(south.Ground * south_Screen.GroundVisibility, south_Screen.Ground, 9);
            Assert.Equal(south.IrradiationUnshaded, south_Screen.IrradiationUnshaded, 9);

            SurfaceSolarRadiationResult north = SolarFixture_Result(surfaceSolarRadiationResults, normals, new Vector3D(0, 1, 0));
            SurfaceSolarRadiationResult north_Screen = SolarFixture_Result(surfaceSolarRadiationResults_Screen, normals, new Vector3D(0, 1, 0));
            Assert.Equal(north.Irradiation, north_Screen.Irradiation, 9);
            Assert.Equal(north.Beam, north_Screen.Beam, 9);
            Assert.Equal(1.0, north_Screen.SkyVisibility);
            Assert.Equal(1.0, north_Screen.GroundVisibility);
        }
    }
}
