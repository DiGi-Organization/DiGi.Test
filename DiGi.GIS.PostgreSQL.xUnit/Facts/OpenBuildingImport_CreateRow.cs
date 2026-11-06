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
        /// <para>The three outcome channels of <see cref="OpenBuildingPostgreSQLConverter.CreateRow(Footprint, ICoordinateTransformation?, IDictionary{int, IPolygonal2D}?, IDictionary{int, BoundingBox2D}?, IDictionary{int, PolygonalFace2D}?, ISet{int}?, BoundingBox2D?, double, out UpdateRejectionReason?)"/> are pinned against real geometry: a filed row carries the county, the source identifier, the normalised primary source and an object whose stored source reference system is 2180; a footprint outside the scope box is filtered with no reason; a missing transformation (an unsupported source coordinate reference system) and a null footprint are rejections of their own. The county square sits inside the EPSG:2180 domain of use so the valid footprint survives the domain check the reprojection enforces. The fact needs no database.</para>
        /// </summary>
        [Fact]
        public void OpenBuildingImport_CreateRow_ReprojectFileFilter()
        {
            // A one-hundred-metre county square inside the Polish domain of use, around (630000, 485000).
            AdministrativeAreal2D administrativeAreal2D = AdministrativeAreal2D_Square(10, "9902", 629950, 484950, 100);
            List<AdministrativeAreal2D> administrativeAreal2Ds = [administrativeAreal2D];

            Dictionary<int, IPolygonal2D> polygonal2Ds_ByCountyId = administrativeAreal2Ds.Polygonal2DsByCountyId();
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
            OpenBuildingRow? openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Valid, coordinateTransformation, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out UpdateRejectionReason? updateRejectionReason);
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

            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Outside, coordinateTransformation, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Equal(UpdateRejectionReason.GeometryRejected, updateRejectionReason);

            // Filtered, not rejected: outside the scope box the footprint was never in scope.
            BoundingBox2D boundingBox2D_Scope = new(new Point2D(200000, 200000), new Point2D(300000, 300000));
            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Valid, coordinateTransformation, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, boundingBox2D_Scope, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Null(updateRejectionReason);

            // No transformation for the source coordinate reference system - the readers only emit 4326 and
            // 3035, so this stands for a file the transformation factories could not serve.
            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(footprint_Valid, null, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Equal(UpdateRejectionReason.GeometryRejected, updateRejectionReason);

            // A null footprint has nothing to name but itself.
            openBuildingRow = OpenBuildingPostgreSQLConverter.CreateRow(null, coordinateTransformation, polygonal2Ds_ByCountyId, boundingBox2Ds_ByCountyId, null, countyIds_Scope, null, Core.Constants.Tolerance.MacroDistance, out updateRejectionReason);
            Assert.Null(openBuildingRow);
            Assert.Equal(UpdateRejectionReason.Undefined, updateRejectionReason);
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
