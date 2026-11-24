using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.Geometry.Spatial.Classes;
using DiGi.GIS.IO.Classes;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using GeoAPI.CoordinateSystems.Transformations;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies the per-footprint pipeline of the open building import: a footprint is reprojected once with the run's transformation, filed under a county part and serialized as EPSG:2180, while a footprint the reprojection rejects - here one whose result falls outside the Polish domain - is counted with a rejection reason and never becomes a row (DiGi.GIS.PostgreSQL#125).
        /// <para>The three outcome channels of <see cref="OpenBuildingPostgreSQLConverter.CreateRow(Footprint, ICoordinateTransformation?, IDictionary{int, IPolygonal2D}?, IDictionary{int, CountyFilingGeometry}?, IDictionary{int, BoundingBox2D}?, IDictionary{int, PolygonalFace2D}?, ISet{int}?, BoundingBox2D?, double, out UpdateRejectionReason?)"/> are pinned against real geometry: a filed row carries the county, the source identifier, the normalised primary source and an object whose stored source reference system is 2180; a footprint outside the scope box is filtered with no reason; a missing transformation (an unsupported source coordinate reference system) and a null footprint are rejections of their own. The county square sits inside the EPSG:2180 domain of use so the valid footprint survives the domain check the reprojection enforces. The fact needs no database.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImport_CreateRow_ReprojectFileFilter()
        {
            // A one-hundred-metre county square inside the Polish domain of use, around (630000, 485000).
            AdministrativeAreal2D administrativeAreal2D = AdministrativeAreal2D_Square(10, "9902", 629950, 484950, 100);
            List<AdministrativeAreal2D> administrativeAreal2Ds = [administrativeAreal2D];

            Dictionary<int, IPolygonal2D> polygonal2Ds_ByCountyId = administrativeAreal2Ds.Polygonal2DsByCountyId();

            // The run-local indexes the import builds beside the rings; a square part of four points stays on its
            // ring (the legacy call decides below 17 points), so these facts exercise the fallback path.
            Dictionary<int, CountyFilingGeometry> countyFilingGeometries_ByCountyId = polygonal2Ds_ByCountyId.CountyFilingGeometriesByCountyId();
            Dictionary<int, BoundingBox2D> boundingBox2Ds_ByCountyId = [];

            foreach (KeyValuePair<int, PolygonalFace2D> pair in administrativeAreal2Ds.PolygonalFace2DsById())
            {
                if (pair.Value.GetBoundingBox() is BoundingBox2D boundingBox2D)
                {
                    boundingBox2Ds_ByCountyId[pair.Key] = boundingBox2D;
                }
            }

            ICoordinateTransformation? coordinateTransformation = GIS.Create.EPSG2180TransformationFromEPSG4326();
            Assert.NotNull(coordinateTransformation);

            // The scope every filed call runs under: this county part alone.
            HashSet<int> countyIds_Scope = [10];

            // Where in EPSG:4326 the county square's centre sits, so the footprint arrives from the source
            // system and reprojects back onto it.
            Point3D? point3D_Centre = GIS.Convert.ToEPSG4326(new Point2D(630000, 485000));
            Assert.NotNull(point3D_Centre);

            Footprint? footprint_Valid = OpenBuilding_Footprint("xunit-1", point3D_Centre.X, point3D_Centre.Y, 0.00005);
            Assert.NotNull(footprint_Valid);

            // Filed: reprojected, filed under the county and serialized with the stored source reference system.
            OpenBuildingRow? openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Valid, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out UpdateRejectionReason? updateRejectionReason);
            Assert.NotNull(openBuildingRow);
            Assert.Null(updateRejectionReason);
            Assert.Equal(10, openBuildingRow.CountyId);
            Assert.Equal("xunit-1", openBuildingRow.SourceId);
            Assert.Equal(Constants.PrimarySource.Register, openBuildingRow.PrimarySource);
            Assert.NotNull(openBuildingRow.Provenance);
            Assert.Contains("gov-poland-xunit", openBuildingRow.Provenance);

            JsonObject? jsonObject_Object = JsonNode.Parse(openBuildingRow.Object) as JsonObject;
            Assert.NotNull(jsonObject_Object);
            Assert.Equal(2180, jsonObject_Object["SourceSrid"]?.GetValue<int>());
            Assert.NotNull(jsonObject_Object["Attributes"]);

            // The reprojection's rejection unit: a footprint whose result falls outside the Polish domain
            // (5E, 50N projects far west of every Polish easting) is counted and never written.
            Footprint? footprint_Outside = OpenBuilding_Footprint("xunit-2", 5.0, 50.0, 0.0001);
            Assert.NotNull(footprint_Outside);

            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Outside, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Equal(UpdateRejectionReason.GeometryRejected, updateRejectionReason);

            // Filtered, not rejected: outside the scope box the footprint was never in scope.
            BoundingBox2D boundingBox2D_Scope = new(new Point2D(200000, 200000), new Point2D(300000, 300000));
            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Valid, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, boundingBox2D_Scope, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Null(updateRejectionReason);

            // No transformation for the source coordinate reference system - the readers only emit 4326 and
            // 3035, so this stands for a file the transformation factories could not serve.
            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Valid, null, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Equal(UpdateRejectionReason.GeometryRejected, updateRejectionReason);

            // A null footprint has nothing to name but itself.
            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(null, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Equal(UpdateRejectionReason.Undefined, updateRejectionReason);
        }

        /// <summary>
        /// Verifies the three non-row outcomes the import loop's per-rejection-reason tally counts, so the tally itself is built on reasons the pipeline really answers with (DiGi.GIS.PostgreSQL#172).
        /// <para>Two county parts are loaded - the scratch square and one a hundred kilometres away - and only the scratch square is in scope, so the fact tells the channels apart: a ring of three ordinates (two distinct points, closed by the NetTopologySuite conversion) the reprojection refuses as a whole is <see cref="UpdateRejectionReason.GeometryRejected"/>; a footprint inside a loaded part the scope list does not hold is filtered with no reason at all; a footprint no loaded part reaches is <see cref="UpdateRejectionReason.CountyUnresolved"/> rather than a filter, because nothing was out of scope - it lies outside the digitised territory the run was given. The fact needs no database.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImport_CreateRow_RejectionReasons()
        {
            AdministrativeAreal2D administrativeAreal2D_A = AdministrativeAreal2D_Square(10, "9902", 629950, 484950, 100);
            AdministrativeAreal2D administrativeAreal2D_B = AdministrativeAreal2D_Square(20, "9902", 699950, 499950, 100);
            List<AdministrativeAreal2D> administrativeAreal2Ds = [administrativeAreal2D_A, administrativeAreal2D_B];

            Dictionary<int, IPolygonal2D> polygonal2Ds_ByCountyId = administrativeAreal2Ds.Polygonal2DsByCountyId();
            Dictionary<int, CountyFilingGeometry> countyFilingGeometries_ByCountyId = polygonal2Ds_ByCountyId.CountyFilingGeometriesByCountyId();
            Dictionary<int, BoundingBox2D> boundingBox2Ds_ByCountyId = [];

            foreach (KeyValuePair<int, PolygonalFace2D> pair in administrativeAreal2Ds.PolygonalFace2DsById())
            {
                if (pair.Value.GetBoundingBox() is BoundingBox2D boundingBox2D)
                {
                    boundingBox2Ds_ByCountyId[pair.Key] = boundingBox2D;
                }
            }

            ICoordinateTransformation? coordinateTransformation = GIS.Create.EPSG2180TransformationFromEPSG4326();
            Assert.NotNull(coordinateTransformation);

            HashSet<int> countyIds_Scope = [10];

            // The reprojection's refusal of a ring shorter than four ordinates: the whole footprint is
            // rejected, never half-transformed and never filed.
            Footprint? footprint_ShortRing = OpenBuilding_ShortRingFootprint("xunit-short-ring");
            Assert.NotNull(footprint_ShortRing);

            OpenBuildingRow? openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_ShortRing, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out UpdateRejectionReason? updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Equal(UpdateRejectionReason.GeometryRejected, updateRejectionReason);

            // Inside part B, which is loaded but not in scope: filtered with no reason - the row belongs
            // to another run and nothing was lost.
            Point3D? point3D_OutOfScope = GIS.Convert.ToEPSG4326(new Point2D(700000, 500000));
            Assert.NotNull(point3D_OutOfScope);

            Footprint? footprint_OutOfScope = OpenBuilding_Footprint("xunit-out-of-scope", point3D_OutOfScope.X, point3D_OutOfScope.Y, 0.00005);
            Assert.NotNull(footprint_OutOfScope);

            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_OutOfScope, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Null(updateRejectionReason);

            // Reprojectable and inside the Polish domain, yet between the two loaded parts: no county
            // envelope reaches it, so Query.OpenBuildingCountyId answers rejected = true.
            Point3D? point3D_Unresolved = GIS.Convert.ToEPSG4326(new Point2D(660000, 490000));
            Assert.NotNull(point3D_Unresolved);

            Footprint? footprint_Unresolved = OpenBuilding_Footprint("xunit-unresolved", point3D_Unresolved.X, point3D_Unresolved.Y, 0.00005);
            Assert.NotNull(footprint_Unresolved);

            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Unresolved, coordinateTransformation, polygonal2Ds_ByCountyId, countyFilingGeometries_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Equal(UpdateRejectionReason.CountyUnresolved, updateRejectionReason);
        }

        /// <summary>
        /// Builds the degenerate footprint a ring shorter than four ordinates rejects (DiGi.GIS.PostgreSQL#172): two distinct points of the scratch centre in EPSG:4326, which the NetTopologySuite conversion would close into a ring of three ordinates.
        /// </summary>
        /// <param name="sourceId">The identifier the source dataset gives the footprint.</param>
        /// <returns>The footprint - carrying the polygonal face when the public factory builds one from the degenerate ring, the raw polygon otherwise; both reach the same rejection.</returns>
        private static Footprint? OpenBuilding_ShortRingFootprint(string sourceId)
        {
            Point3D? point3D = GIS.Convert.ToEPSG4326(new Point2D(630000, 485000));
            Assert.NotNull(point3D);

            Polygon2D polygon2D = new(
            [
                new Point2D(point3D.X, point3D.Y),
                new Point2D(point3D.X + 0.00001, point3D.Y),
            ]);

            // The face factory may refuse a ring of two points; the raw polygon reaches the same
            // Geometry Rejected through Reproject's default branch, so either construction is the fixture.
            IGeometry2D? geometry = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            if (geometry is null)
            {
                geometry = polygon2D;
            }

            JsonObject attributes = [];
            attributes["height"] = 12.5;

            JsonObject provenance = [];
            provenance["geometry_source"] = "gov-poland-xunit";

            return new Footprint(sourceId, 4326, geometry, attributes, provenance, null);
        }

        /// <summary>
        /// Builds one footprint in EPSG:4326: a small square around a longitude and latitude, with the attribute bag and provenance the committed EUBUCCO sample carries.
        /// </summary>
        /// <param name="sourceId">The identifier the source dataset gives the footprint.</param>
        /// <param name="longitude">The longitude of the centre, in degrees.</param>
        /// <param name="latitude">The latitude of the centre, in degrees.</param>
        /// <param name="halfSize">The half edge of the square, in degrees.</param>
        /// <returns>The footprint, or null when the square cannot become a polygonal face.</returns>
        private static Footprint? OpenBuilding_Footprint(string sourceId, double longitude, double latitude, double halfSize)
        {
            Polygon2D polygon2D = new(
            [
                new Point2D(longitude - halfSize, latitude - halfSize),
                new Point2D(longitude + halfSize, latitude - halfSize),
                new Point2D(longitude + halfSize, latitude + halfSize),
                new Point2D(longitude - halfSize, latitude + halfSize),
            ]);

            PolygonalFace2D? polygonalFace2D = Geometry.Planar.Create.PolygonalFace2D(polygon2D);
            if (polygonalFace2D is null)
            {
                return null;
            }

            JsonObject attributes = [];
            attributes["height"] = 12.5;
            attributes["name"] = "xunit";

            JsonObject provenance = [];
            provenance["geometry_source"] = "gov-poland-xunit";

            return new Footprint(sourceId, 4326, polygonalFace2D, attributes, provenance, null);
        }
    }
}
