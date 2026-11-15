using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Solar;
using DiGi.Core.Classes;
using DiGi.EPW.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.Analytical.Solar;
using DiGi.GIS.WebAPI.UI.Classes;
using DiGi.Solar.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.GIS.WebAPI.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the stored form of a solve, <c>GIS.Analytical.Solar.Create.SurfaceSolarRadiations(ShadingModel, …)</c>, carries exactly the summary values this host's <c>Create.SurfaceSolarRadiationResults(ShadingModel, …)</c> reports for the same shaded box and EPW year, and a shading factor for every hour of the solar reference year.
        /// <para>Every surface has stored factors - 8 760 bytes, none missing for a sun-up hour - so a solved building can be stored; night hours are 0. The box is shaded by a screen, so the factors are not trivially 0. Reference: DiGi.GIS.WebAPI.UI#74. The pipeline itself, its rebuild and its hourly series are covered in <c>DiGi.GIS.Analytical.Solar.xUnit</c> (DiGi.GIS.WebAPI.UI#87).</para>
        /// </summary>
        [MediumFact]
        public void Create_SurfaceSolarRadiations()
        {
            EPWFile ePWFile = SolarFixture_EPWFile();

            // Both shading models come from one building model, so their receivers carry the same references.
            BuildingModel buildingModel = SolarFixture_Box();
            (ShadingModel shadingModel_1, Dictionary<string, Vector3D> normals) = Create_SurfaceSolarRadiations_ShadedBox(buildingModel);
            List<SurfaceSolarRadiationResult>? surfaceSolarRadiationResults = shadingModel_1.SurfaceSolarRadiationResults(normals, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiationResults);

            (ShadingModel shadingModel_2, _) = Create_SurfaceSolarRadiations_ShadedBox(buildingModel);
            List<GIS.Classes.SurfaceSolarRadiation>? surfaceSolarRadiations = shadingModel_2.SurfaceSolarRadiations(normals, ePWFile, SolarFixture_ShadingSolverOptions());
            Assert.NotNull(surfaceSolarRadiations);
            Assert.Equal(normals.Count, surfaceSolarRadiations.Count);
            Assert.Equal(surfaceSolarRadiationResults.Count, surfaceSolarRadiations.Count);

            int count_Shaded = 0;
            foreach (GIS.Classes.SurfaceSolarRadiation surfaceSolarRadiation in surfaceSolarRadiations)
            {
                SurfaceSolarRadiationResult surfaceSolarRadiationResult = surfaceSolarRadiationResults.Single(x => x.Reference == surfaceSolarRadiation.Reference);
                Assert.Equal(surfaceSolarRadiationResult.Area, surfaceSolarRadiation.Area);
                Assert.Equal(surfaceSolarRadiationResult.Irradiation, surfaceSolarRadiation.Irradiation);
                Assert.Equal(surfaceSolarRadiationResult.Beam, surfaceSolarRadiation.Beam);
                Assert.Equal(surfaceSolarRadiationResult.Diffuse, surfaceSolarRadiation.Diffuse);
                Assert.Equal(surfaceSolarRadiationResult.Ground, surfaceSolarRadiation.Ground);
                Assert.Equal(surfaceSolarRadiationResult.IrradiationUnshaded, surfaceSolarRadiation.IrradiationUnshaded);
                Assert.Equal(surfaceSolarRadiationResult.Energy, surfaceSolarRadiation.Energy);
                Assert.Equal(surfaceSolarRadiationResult.SkyVisibility, surfaceSolarRadiation.SkyVisibility);
                Assert.Equal(surfaceSolarRadiationResult.GroundVisibility, surfaceSolarRadiation.GroundVisibility);
                Assert.NotNull(surfaceSolarRadiation.Normal);

                byte[]? shadingFactors = surfaceSolarRadiation.ShadingFactors;
                Assert.NotNull(shadingFactors);
                Assert.Equal(GIS.Analytical.Solar.Constants.Default.SolarReferenceHourCount, shadingFactors.Length);
                count_Shaded += shadingFactors.Count(x => x > 0);

                // Midnight on 1 January is a night hour.
                Assert.Equal(0, shadingFactors[0]);
            }

            Assert.True(count_Shaded > 500, $"{count_Shaded} shaded receiver-hours");
        }

        // A stamped box shaded by a screen south of it, as a fresh shading model: the solver writes its results into the
        // model, so every solve of a fact gets its own.
        private static (ShadingModel ShadingModel, Dictionary<string, Vector3D> Normals) Create_SurfaceSolarRadiations_ShadedBox(BuildingModel buildingModel)
        {
            Dictionary<string, Vector3D>? normals = buildingModel.SolarReceiverNormals();
            Assert.NotNull(normals);

            ShadingModel? shadingModel = buildingModel.ToSolar(null, x => new GuidReference(x).ToString() is string reference && normals.ContainsKey(reference));
            Assert.NotNull(shadingModel);

            PolygonalFace3D? polygonalFace3D_Screen = Geometry.Spatial.Create.PolygonalFace3D(new Plane(new Point3D(SolarFixture_Origin.X + 5, SolarFixture_Origin.Y - 5, 20), new Vector3D(0, 1, 0)), [new Point2D(-40, -40), new Point2D(40, -40), new Point2D(40, 40), new Point2D(-40, 40)]);
            Assert.NotNull(polygonalFace3D_Screen);
            Assert.True(shadingModel.Update(new ShadingElement("Screen", polygonalFace3D_Screen, true)));

            return (shadingModel, normals);
        }
    }
}
