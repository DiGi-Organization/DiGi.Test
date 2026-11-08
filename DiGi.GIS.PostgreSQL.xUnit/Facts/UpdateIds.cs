using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.GIS.PostgreSQL.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Modify.UpdateIds(AdministrativeAreal2D, IEnumerable{AdministrativeAreal2D}, double)"/> picks the source
        /// whose polygon holds the destination and copies its whole ancestor chain.
        /// </summary>
        [Fact]
        public void UpdateIds_Containment()
        {
            AdministrativeAreal2D administrativeAreal2D_Country = Create_AdministrativeAreal2D(1, "Country", GIS.Enums.AdministrativeDivisionType.country, Create_Rectangle(0, 0, 1000, 1000), null);
            AdministrativeAreal2D administrativeAreal2D_Voivodeship = Create_AdministrativeAreal2D(2, "Voivodeship", GIS.Enums.AdministrativeDivisionType.voivodeship, Create_Rectangle(0, 0, 500, 1000), administrativeAreal2D_Country);
            AdministrativeAreal2D administrativeAreal2D_County = Create_AdministrativeAreal2D(3, "County", GIS.Enums.AdministrativeDivisionType.county, Create_Rectangle(0, 0, 500, 500), administrativeAreal2D_Voivodeship);
            AdministrativeAreal2D administrativeAreal2D_Municipality_1 = Create_AdministrativeAreal2D(4, "Municipality 1", GIS.Enums.AdministrativeDivisionType.municipality, Create_Rectangle(0, 0, 250, 250), administrativeAreal2D_County);
            AdministrativeAreal2D administrativeAreal2D_Municipality_2 = Create_AdministrativeAreal2D(5, "Municipality 2", GIS.Enums.AdministrativeDivisionType.municipality, Create_Rectangle(250, 0, 500, 250), administrativeAreal2D_County);

            AdministrativeAreal2D administrativeAreal2D_Subdivision = Create_AdministrativeAreal2D(6, "Subdivision", GIS.Enums.AdministrativeDivisionType.district_or_delegation, Create_Rectangle(50, 50, 150, 150), null);

            Assert.True(Modify.UpdateIds(administrativeAreal2D_Subdivision, [administrativeAreal2D_Municipality_1, administrativeAreal2D_Municipality_2]));

            Assert.Equal(4, administrativeAreal2D_Subdivision.MunicipalityId);
            Assert.Equal(3, administrativeAreal2D_Subdivision.CountyId);
            Assert.Equal(2, administrativeAreal2D_Subdivision.VoivodeshipId);
            Assert.Equal(1, administrativeAreal2D_Subdivision.CountryId);
        }

        /// <summary>
        /// Verifies the Poznan (3064) shape, where BDOT10k holds no municipality feature inside the city at all.
        /// <para>The neighbouring municipality shares a border with the destination but does not cover it, so the municipality level must
        /// report no parent - that is what lets the caller walk one level up - and the county level must then resolve it, leaving
        /// <c>MunicipalityId</c> null. See https://github.com/DiGi-Organization/DiGi.GIS.PostgreSQL/issues/14.</para>
        /// </summary>
        [Fact]
        public void UpdateIds_AncestorFallback()
        {
            AdministrativeAreal2D administrativeAreal2D_Country = Create_AdministrativeAreal2D(1, "Country", GIS.Enums.AdministrativeDivisionType.country, Create_Rectangle(0, 0, 1000, 1000), null);
            AdministrativeAreal2D administrativeAreal2D_Voivodeship = Create_AdministrativeAreal2D(2, "Voivodeship", GIS.Enums.AdministrativeDivisionType.voivodeship, Create_Rectangle(0, 0, 1000, 500), administrativeAreal2D_Country);
            AdministrativeAreal2D administrativeAreal2D_County = Create_AdministrativeAreal2D(3, "City county", GIS.Enums.AdministrativeDivisionType.county, Create_Rectangle(0, 0, 500, 500), administrativeAreal2D_Voivodeship);

            // The only municipality in the data starts where the city county ends.
            AdministrativeAreal2D administrativeAreal2D_Municipality = Create_AdministrativeAreal2D(4, "Neighbouring municipality", GIS.Enums.AdministrativeDivisionType.municipality, Create_Rectangle(500, 0, 1000, 500), null);

            // A city district touching the shared border - the closest a subdivision can get to that municipality.
            AdministrativeAreal2D administrativeAreal2D_Subdivision = Create_AdministrativeAreal2D(5, "District", GIS.Enums.AdministrativeDivisionType.district_or_delegation, Create_Rectangle(410, 50, 500, 150), null);

            Assert.False(Modify.UpdateIds(administrativeAreal2D_Subdivision, [administrativeAreal2D_Municipality]));

            Assert.Null(administrativeAreal2D_Subdivision.MunicipalityId);
            Assert.Null(administrativeAreal2D_Subdivision.CountyId);

            Assert.True(Modify.UpdateIds(administrativeAreal2D_Subdivision, [administrativeAreal2D_County]));

            Assert.Equal(3, administrativeAreal2D_Subdivision.CountyId);
            Assert.Equal(2, administrativeAreal2D_Subdivision.VoivodeshipId);
            Assert.Equal(1, administrativeAreal2D_Subdivision.CountryId);
            Assert.Null(administrativeAreal2D_Subdivision.MunicipalityId);
        }

        /// <summary>
        /// Verifies the border sliver shape, where the sample point lands in a gap left between two source polygons.
        /// <para>Both sources hold the sample point inside their bounding box and neither holds it inside its polygon, so the containment
        /// search finds nothing. The destination must then go to the source covering the majority of its area.</para>
        /// </summary>
        [Fact]
        public void UpdateIds_OverlapFallback()
        {
            // The gap both source polygons leave around the destination's internal point (85, 50).
            PolygonalFace2D polygonalFace2D_Gap = Create_Rectangle(80, 45, 90, 55);

            AdministrativeAreal2D administrativeAreal2D_County = Create_AdministrativeAreal2D(1, "County", GIS.Enums.AdministrativeDivisionType.county, Create_Rectangle(0, 0, 300, 100), null);

            AdministrativeAreal2D administrativeAreal2D_Municipality_1 = Create_AdministrativeAreal2D(2, "Municipality 1", GIS.Enums.AdministrativeDivisionType.municipality, Create_Rectangle(0, 0, 120, 100, polygonalFace2D_Gap), administrativeAreal2D_County);
            AdministrativeAreal2D administrativeAreal2D_Municipality_2 = Create_AdministrativeAreal2D(3, "Municipality 2", GIS.Enums.AdministrativeDivisionType.municipality, Create_Rectangle(60, 0, 200, 100, polygonalFace2D_Gap), administrativeAreal2D_County);

            // Overlaps municipality 1 by 1 500 and municipality 2 by 1 300 out of its own 1 800.
            AdministrativeAreal2D administrativeAreal2D_Subdivision = Create_AdministrativeAreal2D(4, "Settlement", GIS.Enums.AdministrativeDivisionType.town_in_urban_rural_municipality, Create_Rectangle(40, 40, 130, 60), null);

            // The containment search has to come back empty, otherwise this exercises the wrong path: both bounding
            // boxes hold the sample point, so neither source is picked outright, and neither polygon holds it.
            Point2D? point2D = administrativeAreal2D_Subdivision.ToDiGi()?.PolygonalFace2D?.GetInternalPoint();
            Assert.NotNull(point2D);
            Assert.True(administrativeAreal2D_Municipality_1.BoundingBox2D?.InRange(point2D, Core.Constants.Tolerance.MacroDistance));
            Assert.True(administrativeAreal2D_Municipality_2.BoundingBox2D?.InRange(point2D, Core.Constants.Tolerance.MacroDistance));
            Assert.False(administrativeAreal2D_Municipality_1.ToDiGi()?.PolygonalFace2D?.InRange(point2D, Core.Constants.Tolerance.MacroDistance));
            Assert.False(administrativeAreal2D_Municipality_2.ToDiGi()?.PolygonalFace2D?.InRange(point2D, Core.Constants.Tolerance.MacroDistance));

            Assert.True(Modify.UpdateIds(administrativeAreal2D_Subdivision, [administrativeAreal2D_Municipality_1, administrativeAreal2D_Municipality_2]));

            Assert.Equal(2, administrativeAreal2D_Subdivision.MunicipalityId);
            Assert.Equal(1, administrativeAreal2D_Subdivision.CountyId);
        }

        /// <summary>
        /// Verifies that the overlap fallback refuses a source that covers less than half of the destination, so the caller drops a level
        /// rather than filing the destination under a neighbour it merely borders.
        /// </summary>
        [Fact]
        public void UpdateIds_OverlapFallback_BelowMajority()
        {
            AdministrativeAreal2D administrativeAreal2D_County = Create_AdministrativeAreal2D(1, "County", GIS.Enums.AdministrativeDivisionType.county, Create_Rectangle(0, 0, 300, 100), null);

            // Covers 400 of the destination's 1 800.
            AdministrativeAreal2D administrativeAreal2D_Municipality = Create_AdministrativeAreal2D(2, "Municipality", GIS.Enums.AdministrativeDivisionType.municipality, Create_Rectangle(0, 0, 60, 100), administrativeAreal2D_County);

            AdministrativeAreal2D administrativeAreal2D_Subdivision = Create_AdministrativeAreal2D(3, "Settlement", GIS.Enums.AdministrativeDivisionType.town_in_urban_rural_municipality, Create_Rectangle(40, 40, 130, 60), null);

            Assert.False(Modify.UpdateIds(administrativeAreal2D_Subdivision, [administrativeAreal2D_Municipality]));

            Assert.Null(administrativeAreal2D_Subdivision.MunicipalityId);
            Assert.Null(administrativeAreal2D_Subdivision.CountyId);
            Assert.Null(administrativeAreal2D_Subdivision.VoivodeshipId);
            Assert.Null(administrativeAreal2D_Subdivision.CountryId);
        }

        /// <summary>
        /// Builds a stored administrative areal record with the given identifier, geometry and parent, the way the BDOT10k import does.
        /// </summary>
        /// <param name="id">The identifier to assign to the record.</param>
        /// <param name="name">The name of the administrative area.</param>
        /// <param name="administrativeDivisionType">The administrative division type, which drives the stored type identifier.</param>
        /// <param name="polygonalFace2D">The geometry of the administrative area.</param>
        /// <param name="administrativeAreal2D_Parent">The parent record whose ancestor chain is inherited, or null to leave every parent identifier unset.</param>
        /// <returns>The stored administrative areal record.</returns>
        private static AdministrativeAreal2D Create_AdministrativeAreal2D(int id, string name, GIS.Enums.AdministrativeDivisionType administrativeDivisionType, PolygonalFace2D polygonalFace2D, AdministrativeAreal2D? administrativeAreal2D_Parent)
        {
            GIS.Classes.AdministrativeDivision administrativeDivision = new(Guid.NewGuid(), $"REFERENCE_{id}", id.ToString(CultureInfo.CurrentCulture), polygonalFace2D, administrativeDivisionType, name);

            AdministrativeAreal2D? result = Convert.ToPostgreSQL(administrativeDivision);
            Assert.NotNull(result);

            result.Id = id;

            Modify.UpdateIds(result, administrativeAreal2D_Parent);

            return result;
        }

        /// <summary>
        /// Creates a rectangular polygonal face, optionally with a rectangular hole punched out of it.
        /// </summary>
        /// <param name="minX">The minimum X coordinate.</param>
        /// <param name="minY">The minimum Y coordinate.</param>
        /// <param name="maxX">The maximum X coordinate.</param>
        /// <param name="maxY">The maximum Y coordinate.</param>
        /// <param name="polygonalFace2D_Hole">The face whose external edge becomes a hole, or null for a solid rectangle.</param>
        /// <returns>The rectangular polygonal face.</returns>
        private static PolygonalFace2D Create_Rectangle(double minX, double minY, double maxX, double maxY, PolygonalFace2D? polygonalFace2D_Hole = null)
        {
            List<Point2D?> point2Ds = [new Point2D(minX, minY), new Point2D(maxX, minY), new Point2D(maxX, maxY), new Point2D(minX, maxY)];

            Polygon2D? polygon2D_External = Geometry.Planar.Create.Polygon2D(point2Ds);
            Assert.NotNull(polygon2D_External);

            List<IPolygonal2D>? internalEdges = null;
            if (polygonalFace2D_Hole is not null)
            {
                Assert.NotNull(polygonalFace2D_Hole.ExternalEdge);
                internalEdges = [polygonalFace2D_Hole.ExternalEdge];
            }

            PolygonalFace2D? result = Geometry.Planar.Create.PolygonalFace2D(polygon2D_External, internalEdges);
            Assert.NotNull(result);

            return result;
        }
    }
}
