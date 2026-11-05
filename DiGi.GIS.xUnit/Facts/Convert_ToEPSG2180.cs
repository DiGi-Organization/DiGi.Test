using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using GeoAPI.CoordinateSystems;
using GeoAPI.CoordinateSystems.Transformations;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;

namespace DiGi.GIS.xUnit
{
    public partial class Facts
    {
        // The WGS 84 position of the bounding-box centre of the 55417 fixture building in Warsaw, as returned by the EPSG:2180 to WGS 84 conversion in DiGi.GIS.Analytical.xUnit.
        private const double WarsawLongitude = 20.91081434062408;
        private const double WarsawLatitude = 52.25432670046006;
        private const double WarsawEasting = 630388.5747769627;
        private const double WarsawNorthing = 489307.7984585427;

        // Vienna, well inside the transverse Mercator surface but outside the Polish domain of use.
        private const double ViennaLongitude = 16.373748;
        private const double ViennaLatitude = 48.208176;

        private const double CentreEasting = 4321000.0;
        private const double CentreNorthing = 3210000.0;

        private const double GRS80SemiMajorAxis = 6378137.0;
        private const double GRS80InverseFlattening = 298.257222101;
        private const double DegreesToRadians = Math.PI / 180.0;

        /// <summary>
        /// Verifies <see cref="Create.EPSG2180TransformationFromEPSG4326"/> and <see cref="Create.EPSG2180TransformationFromEPSG3035"/>: both are built, both report EPSG:2180 as their target, both are conversions rather than datum transformations, the EPSG:3035 handle carries the Lambert Azimuthal Equal Area mathematics this library adds, and asking a held handle for its opposite direction leaves the direction the run converts in intact.
        /// <para>Reference: DiGi.GIS#21 (EPSG:3035 and polygon footprints to EPSG:2180).</para>
        /// </summary>
        [Fact]
        public void Create_EPSG2180Transformation()
        {
            ICoordinateTransformation? coordinateTransformation_EPSG4326 = Create.EPSG2180TransformationFromEPSG4326();
            Assert.NotNull(coordinateTransformation_EPSG4326);
            Assert.Equal(2180, coordinateTransformation_EPSG4326.AuthorityCode);
            Assert.Equal(TransformType.Conversion, coordinateTransformation_EPSG4326.TransformType);
            Assert.Equal("ETRS89 / Poland CS92", coordinateTransformation_EPSG4326.TargetCS.Name);
            Assert.Equal("WGS 84", coordinateTransformation_EPSG4326.SourceCS.Name);

            ICoordinateTransformation? coordinateTransformation_EPSG3035 = Create.EPSG2180TransformationFromEPSG3035();
            Assert.NotNull(coordinateTransformation_EPSG3035);
            Assert.Equal(2180, coordinateTransformation_EPSG3035.AuthorityCode);
            Assert.Equal(TransformType.Conversion, coordinateTransformation_EPSG3035.TransformType);
            Assert.Equal("ETRS89 / LAEA Europe", coordinateTransformation_EPSG3035.SourceCS.Name);
            Assert.Equal("LambertAzimuthalEqualAreaMathTransform", coordinateTransformation_EPSG3035.MathTransform.GetType().Name);
            Assert.Equal(2, coordinateTransformation_EPSG3035.MathTransform.DimSource);
            Assert.Equal(2, coordinateTransformation_EPSG3035.MathTransform.DimTarget);

            double[] eastingNorthing_Warsaw = coordinateTransformation_EPSG4326.MathTransform.Transform([WarsawLongitude, WarsawLatitude]);
            Assert.True(eastingNorthing_Warsaw.Length >= 2);
            Assert.Equal(WarsawEasting, eastingNorthing_Warsaw[0], 3);
            Assert.Equal(WarsawNorthing, eastingNorthing_Warsaw[1], 3);

            coordinateTransformation_EPSG4326.MathTransform.Inverse();
            coordinateTransformation_EPSG3035.MathTransform.Inverse();

            // ProjNET4GeoAPI 1.4.1 reverses the steps of a leg in place when asked for its inverse, so a handle that hands out its own leg stops converting after a single call to Inverse; the handles of this library must not.
            double[] eastingNorthing_Warsaw_Again = coordinateTransformation_EPSG4326.MathTransform.Transform([WarsawLongitude, WarsawLatitude]);
            Assert.Equal(WarsawEasting, eastingNorthing_Warsaw_Again[0], 3);
            Assert.Equal(WarsawNorthing, eastingNorthing_Warsaw_Again[1], 3);

            double[] eastingNorthing_Centre = coordinateTransformation_EPSG3035.MathTransform.Transform([CentreEasting, CentreNorthing]);
            Assert.Equal(-117047.50022083742, eastingNorthing_Centre[0], 3);
            Assert.Equal(497637.45611533895, eastingNorthing_Centre[1], 3);
        }

        /// <summary>
        /// Verifies <see cref="Convert.ToEPSG2180(Point3D?)"/> against the Warsaw pair the rest of the workspace relies on, that the PL-1992 result returns to the source position through <see cref="Convert.ToEPSG4326(Point2D?)"/>, and its refusals: a null point and a non-finite ordinate give <see langword="null"/>. A point outside the Polish domain still converts, because the callers of this overload - the EPW weather station import and the GIS.ToEPSG2180 Grasshopper component - handle coordinates outside Poland and answer a null by dropping the station or emptying the output.
        /// <para>Reference: DiGi.GIS#21 (EPSG:3035 and polygon footprints to EPSG:2180).</para>
        /// </summary>
        [Fact]
        public void Convert_ToEPSG2180_Point()
        {
            Assert.Null(((Point3D?)null).ToEPSG2180());

            Point2D? point2D_Warsaw = new Point3D(WarsawLongitude, WarsawLatitude, 100.0).ToEPSG2180();
            Assert.NotNull(point2D_Warsaw);
            Assert.Equal(WarsawEasting, point2D_Warsaw.X, 3);
            Assert.Equal(WarsawNorthing, point2D_Warsaw.Y, 3);

            Point3D? point3D_Warsaw_Back = point2D_Warsaw.ToEPSG4326();
            Assert.NotNull(point3D_Warsaw_Back);
            Assert.Equal(WarsawLongitude, point3D_Warsaw_Back.X, 6);
            Assert.Equal(WarsawLatitude, point3D_Warsaw_Back.Y, 6);

            Point2D? point2D_Vienna = new Point3D(ViennaLongitude, ViennaLatitude, 0.0).ToEPSG2180();
            Assert.NotNull(point2D_Vienna);
            Assert.Equal(304948.989709486, point2D_Vienna.X, 3);
            Assert.Equal(41170.07353180833, point2D_Vienna.Y, 3);

            Assert.Null(new Point3D(double.NaN, WarsawLatitude, 0.0).ToEPSG2180());
            Assert.Null(new Point3D(WarsawLongitude, double.PositiveInfinity, 0.0).ToEPSG2180());
        }

        /// <summary>
        /// Verifies <see cref="Convert.ToEPSG2180(Polygon?, ICoordinateTransformation?)"/> on an asymmetric footprint with one hole: the ring count, the vertex count, the ring closure and the orientation of the shell and of the hole survive, the area stays within a nanometre-scale tolerance, and the same physical footprint reaches the same EPSG:2180 vertices whether it arrives from EPSG:4326 or from EPSG:3035.
        /// <para>Reference: DiGi.GIS#21 (EPSG:3035 and polygon footprints to EPSG:2180).</para>
        /// </summary>
        [Fact]
        public void Convert_ToEPSG2180_Polygon()
        {
            ICoordinateTransformation? coordinateTransformation_EPSG4326 = Create.EPSG2180TransformationFromEPSG4326();
            ICoordinateTransformation? coordinateTransformation_EPSG3035 = Create.EPSG2180TransformationFromEPSG3035();
            Assert.NotNull(coordinateTransformation_EPSG4326);
            Assert.NotNull(coordinateTransformation_EPSG3035);

            GeometryFactory geometryFactory = new();
            Polygon polygon_2180 = Footprint(geometryFactory, WarsawEasting, WarsawNorthing);
            double area_2180 = polygon_2180.Area;

            Polygon polygon_4326 = Reproject(geometryFactory, polygon_2180, coordinateTransformation_EPSG4326.MathTransform.Inverse());
            Polygon polygon_3035 = Reproject(geometryFactory, polygon_2180, coordinateTransformation_EPSG3035.MathTransform.Inverse());

            Polygon? polygon_2180_From4326 = Convert.ToEPSG2180(polygon_4326, coordinateTransformation_EPSG4326);
            Assert.NotNull(polygon_2180_From4326);
            Assert.Single(polygon_2180_From4326.InteriorRings);
            Assert.Equal(polygon_2180.ExteriorRing.NumPoints, polygon_2180_From4326.ExteriorRing.NumPoints);
            Assert.True(polygon_2180_From4326.ExteriorRing.IsClosed);
            Assert.True(polygon_2180_From4326.InteriorRings[0].IsClosed);
            Assert.Equal(Math.Sign(SignedArea(polygon_2180.ExteriorRing.Coordinates)), Math.Sign(SignedArea(polygon_2180_From4326.ExteriorRing.Coordinates)));
            Assert.Equal(Math.Sign(SignedArea(polygon_2180.InteriorRings[0].Coordinates)), Math.Sign(SignedArea(polygon_2180_From4326.InteriorRings[0].Coordinates)));

            Polygon? polygon_2180_From3035 = Convert.ToEPSG2180(polygon_3035, coordinateTransformation_EPSG3035);
            Assert.NotNull(polygon_2180_From3035);
            Assert.Single(polygon_2180_From3035.InteriorRings);
            Assert.Equal(polygon_2180.ExteriorRing.NumPoints, polygon_2180_From3035.ExteriorRing.NumPoints);
            Assert.True(polygon_2180_From3035.ExteriorRing.IsClosed);

            // The footprints arrive through Inverse(), and ProjNet's transverse Mercator inverse is accurate to about a centimetre, so the round trip is checked at centimetre scale; the two routes are then compared to each other, where both carry the same leg and agree to nanometres.
            Assert.True(MaximumVertexDistance(polygon_2180, polygon_2180_From4326) < 0.02, $"The EPSG:4326 route came back {MaximumVertexDistance(polygon_2180, polygon_2180_From4326):E2} m from the source footprint.");
            Assert.True(MaximumVertexDistance(polygon_2180, polygon_2180_From3035) < 0.02, $"The EPSG:3035 route came back {MaximumVertexDistance(polygon_2180, polygon_2180_From3035):E2} m from the source footprint.");
            Assert.Equal(0.0, MaximumVertexDistance(polygon_2180_From4326, polygon_2180_From3035), 6);
            Assert.Equal(1.0, polygon_2180_From4326.Area / area_2180, 6);
            Assert.Equal(1.0, polygon_2180_From3035.Area / area_2180, 6);

            MultiPolygon multiPolygon_3035 = geometryFactory.CreateMultiPolygon([polygon_3035, Reproject(geometryFactory, Footprint(geometryFactory, WarsawEasting + 500.0, WarsawNorthing + 500.0), coordinateTransformation_EPSG3035.MathTransform.Inverse())]);
            MultiPolygon? multiPolygon_2180 = Convert.ToEPSG2180(multiPolygon_3035, coordinateTransformation_EPSG3035);
            Assert.NotNull(multiPolygon_2180);
            Assert.Equal(2, multiPolygon_2180.NumGeometries);
            Assert.Single(((Polygon)multiPolygon_2180.GetGeometryN(0)).InteriorRings);
        }

        /// <summary>
        /// Verifies the DiGi-shaped overloads <see cref="Convert.ToEPSG2180(PolygonalFace2D?, ICoordinateTransformation?)"/> and <see cref="Convert.ToEPSG2180(Polygon2D?, ICoordinateTransformation?)"/>: a face keeps its external edge and every internal edge, and a closed curve keeps its vertices without gaining a repeated closing point.
        /// <para>Reference: DiGi.GIS#21 (EPSG:3035 and polygon footprints to EPSG:2180).</para>
        /// </summary>
        [Fact]
        public void Convert_ToEPSG2180_Face_And_ClosedCurve()
        {
            ICoordinateTransformation? coordinateTransformation_EPSG3035 = Create.EPSG2180TransformationFromEPSG3035();
            Assert.NotNull(coordinateTransformation_EPSG3035);

            GeometryFactory geometryFactory = new();
            Polygon polygon_3035 = Reproject(geometryFactory, Footprint(geometryFactory, WarsawEasting, WarsawNorthing), coordinateTransformation_EPSG3035.MathTransform.Inverse());

            PolygonalFace2D? polygonalFace2D = DiGi.Geometry.Planar.Convert.ToDiGi(polygon_3035);
            Assert.NotNull(polygonalFace2D);
            PolygonalFace2D? polygonalFace2D_2180 = Convert.ToEPSG2180(polygonalFace2D, coordinateTransformation_EPSG3035);
            Assert.NotNull(polygonalFace2D_2180);
            Assert.NotNull(polygonalFace2D_2180.ExternalEdge);
            Assert.Single(polygonalFace2D_2180.InternalEdges!);

            List<Point2D> point2Ds_3035 = [];
            Coordinate[] coordinates = polygon_3035.ExteriorRing.Coordinates;
            for (int i = 0; i < coordinates.Length; i++)
            {
                point2Ds_3035.Add(new Point2D(coordinates[i].X, coordinates[i].Y));
            }

            Polygon2D? polygon2D_2180 = Convert.ToEPSG2180(new Polygon2D(point2Ds_3035), coordinateTransformation_EPSG3035);
            Assert.NotNull(polygon2D_2180);
            List<Point2D>? point2Ds_2180 = polygon2D_2180.GetPoints();
            Assert.NotNull(point2Ds_2180);
            Assert.True(point2Ds_2180.Count == 6, $"The closed curve came back with {point2Ds_2180.Count} points instead of the six it went in with.");

            Assert.Null(Convert.ToEPSG2180((PolygonalFace2D?)null, coordinateTransformation_EPSG3035));
            Assert.Null(Convert.ToEPSG2180((Polygon2D?)null, coordinateTransformation_EPSG3035));
        }

        /// <summary>
        /// Verifies that a footprint is the unit of failure for <see cref="Convert.ToEPSG2180(Polygon?, ICoordinateTransformation?)"/>: a missing geometry, a missing transformation and a single non-finite vertex all give <see langword="null"/>, and so does a footprint that lands outside the Polish domain, including a footprint already in EPSG:2180 handed to the EPSG:3035 transformation.
        /// <para>Reference: DiGi.GIS#21 (EPSG:3035 and polygon footprints to EPSG:2180).</para>
        /// </summary>
        [Fact]
        public void Convert_ToEPSG2180_Refusals()
        {
            ICoordinateTransformation? coordinateTransformation_EPSG4326 = Create.EPSG2180TransformationFromEPSG4326();
            ICoordinateTransformation? coordinateTransformation_EPSG3035 = Create.EPSG2180TransformationFromEPSG3035();
            Assert.NotNull(coordinateTransformation_EPSG4326);
            Assert.NotNull(coordinateTransformation_EPSG3035);

            GeometryFactory geometryFactory = new();
            Polygon polygon_2180 = Footprint(geometryFactory, WarsawEasting, WarsawNorthing);

            Assert.Null(Convert.ToEPSG2180((Polygon?)null, coordinateTransformation_EPSG4326));
            Assert.Null(Convert.ToEPSG2180(polygon_2180, null));

            Coordinate[] coordinates_Infinite = [new Coordinate(double.PositiveInfinity, WarsawNorthing), new Coordinate(WarsawEasting + 123.0, WarsawNorthing), new Coordinate(WarsawEasting + 60.0, WarsawNorthing + 92.0), new Coordinate(double.PositiveInfinity, WarsawNorthing)];
            Assert.Null(Convert.ToEPSG2180(geometryFactory.CreatePolygon(geometryFactory.CreateLinearRing(coordinates_Infinite)), coordinateTransformation_EPSG4326));

            // Vienna sits about 280 km south of the Polish window; the transverse Mercator surface takes it, the domain of use does not.
            Polygon polygon_Vienna = (Polygon)Footprint(geometryFactory, 304948.989709486, 41170.07353180833).Copy();
            Assert.Null(Convert.ToEPSG2180(Reproject(geometryFactory, polygon_Vienna, coordinateTransformation_EPSG4326.MathTransform.Inverse()), coordinateTransformation_EPSG4326));

            // A footprint already in the target system must not be converted a second time.
            Assert.Null(Convert.ToEPSG2180(polygon_2180, coordinateTransformation_EPSG3035));
        }

        /// <summary>
        /// Verifies the two properties that pin the Lambert Azimuthal Equal Area leg to the definition rather than to itself: the centre of EPSG:3035 falls on the PL-1992 position of (10 E, 52 N), and a one-degree quadrilateral mapped into EPSG:3035 keeps the exact area it has on the authalic sphere of radius a * sqrt(q_p / 2), which is what method 9820 promises and what a formula transcription error breaks.
        /// <para>Reference: DiGi.GIS#21 (EPSG:3035 and polygon footprints to EPSG:2180).</para>
        /// </summary>
        [Fact]
        public void Convert_ToEPSG2180_EqualArea()
        {
            ICoordinateTransformation? coordinateTransformation_EPSG4326 = Create.EPSG2180TransformationFromEPSG4326();
            ICoordinateTransformation? coordinateTransformation_EPSG3035 = Create.EPSG2180TransformationFromEPSG3035();
            Assert.NotNull(coordinateTransformation_EPSG4326);
            Assert.NotNull(coordinateTransformation_EPSG3035);

            double[] eastingNorthing_Centre = coordinateTransformation_EPSG3035.MathTransform.Transform([CentreEasting, CentreNorthing]);
            double[] eastingNorthing_Geographic = coordinateTransformation_EPSG4326.MathTransform.Transform([10.0, 52.0]);
            Assert.Equal(eastingNorthing_Geographic[0], eastingNorthing_Centre[0], 6);
            Assert.Equal(eastingNorthing_Geographic[1], eastingNorthing_Centre[1], 6);

            // The area of the region between (16 E, 50 N) and (17 E, 51 N) on the ellipsoid is exactly the area of the same region on the authalic sphere.
            double eccentricitySquared = 2.0 / GRS80InverseFlattening - 1.0 / (GRS80InverseFlattening * GRS80InverseFlattening);
            double maximumQ = Q(1.0, eccentricitySquared);
            double authalicRadius = GRS80SemiMajorAxis * Math.Sqrt(0.5 * maximumQ);
            double area_Authalic = authalicRadius * authalicRadius * DegreesToRadians * (Q(Math.Sin(51.0 * DegreesToRadians), eccentricitySquared) / maximumQ - Q(Math.Sin(50.0 * DegreesToRadians), eccentricitySquared) / maximumQ);

            // Geographic to EPSG:2180 by the ProjNet leg, then EPSG:2180 to EPSG:3035 by the leg this library adds.
            IMathTransform geographicToEPSG2180 = coordinateTransformation_EPSG4326.MathTransform;
            IMathTransform toEPSG3035 = coordinateTransformation_EPSG3035.MathTransform.Inverse();
            GeometryFactory geometryFactory = new();
            List<Coordinate> coordinates = [];
            double[,] corners = { { 16.0, 50.0 }, { 17.0, 50.0 }, { 17.0, 51.0 }, { 16.0, 51.0 } };
            for (int i = 0; i < 4; i++)
            {
                for (int step = 0; step < 200; step++)
                {
                    double fraction = step / 200.0;
                    double longitude = corners[i, 0] + (corners[(i + 1) % 4, 0] - corners[i, 0]) * fraction;
                    double latitude = corners[i, 1] + (corners[(i + 1) % 4, 1] - corners[i, 1]) * fraction;
                    double[] values = toEPSG3035.Transform(geographicToEPSG2180.Transform([longitude, latitude]));
                    coordinates.Add(new Coordinate(values[0], values[1]));
                }
            }

            coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
            double area_EPSG3035 = geometryFactory.CreatePolygon(geometryFactory.CreateLinearRing([.. coordinates])).Area;
            Assert.Equal(1.0, area_EPSG3035 / area_Authalic, 7);
        }

        /// <summary>
        /// Verifies the reason the polygonal overloads take a transformation the caller built: converting with a handle held across a run is orders of magnitude cheaper per vertex than the convenience point overload, which builds the coordinate systems, parses the WKT and resolves the projection on every call.
        /// <para>Reference: DiGi.GIS#21 (EPSG:3035 and polygon footprints to EPSG:2180).</para>
        /// </summary>
        [Fact]
        public void Convert_ToEPSG2180_HeldTransformationIsCheaper()
        {
            ICoordinateTransformation? coordinateTransformation_EPSG3035 = Create.EPSG2180TransformationFromEPSG3035();
            Assert.NotNull(coordinateTransformation_EPSG3035);

            const int count = 20000;

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                new Point3D(WarsawLongitude + i * 1e-7, WarsawLatitude + (i % 250) * 1e-7, 0.0).ToEPSG2180();
            }

            stopwatch.Stop();
            double elapsed_PerCall = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            for (int i = 0; i < count; i++)
            {
                coordinateTransformation_EPSG3035.MathTransform.Transform([CentreEasting + i, CentreNorthing]);
            }

            stopwatch.Stop();
            double elapsed_Held = stopwatch.Elapsed.TotalMilliseconds;

            // Measured at about 57 us per call against 0.3 us per vertex on this machine; the floor is deliberately far below that.
            Assert.True(elapsed_PerCall / Math.Max(elapsed_Held, 1e-9) > 20.0, $"Building a transformation per point took {elapsed_PerCall:F1} ms against {elapsed_Held:F1} ms for the same number of vertices on a held transformation.");
        }

        private static Polygon Footprint(GeometryFactory geometryFactory, double easting, double northing)
        {
            Coordinate[] exterior = [new Coordinate(easting, northing), new Coordinate(easting + 123.0, northing), new Coordinate(easting + 123.0, northing + 45.0), new Coordinate(easting + 71.0, northing + 45.0), new Coordinate(easting + 71.0, northing + 92.0), new Coordinate(easting, northing + 92.0), new Coordinate(easting, northing)];
            Coordinate[] hole = [new Coordinate(easting + 30.0, northing + 20.0), new Coordinate(easting + 60.0, northing + 20.0), new Coordinate(easting + 60.0, northing + 35.0), new Coordinate(easting + 30.0, northing + 35.0), new Coordinate(easting + 30.0, northing + 20.0)];
            return geometryFactory.CreatePolygon(geometryFactory.CreateLinearRing(exterior), [geometryFactory.CreateLinearRing(hole)]);
        }

        private static Polygon Reproject(GeometryFactory geometryFactory, Polygon polygon, IMathTransform mathTransform)
        {
            LinearRing shell = Ring(geometryFactory, polygon.ExteriorRing, mathTransform);
            LinearRing[] holes = Array.ConvertAll(polygon.InteriorRings, ring => Ring(geometryFactory, ring, mathTransform));
            return geometryFactory.CreatePolygon(shell, holes);
        }

        private static LinearRing Ring(GeometryFactory geometryFactory, LineString lineString, IMathTransform mathTransform)
        {
            Coordinate[] coordinates = lineString.Coordinates;
            Coordinate[] transformed = new Coordinate[coordinates.Length];
            for (int i = 0; i < coordinates.Length; i++)
            {
                double[] values = mathTransform.Transform([coordinates[i].X, coordinates[i].Y]);
                transformed[i] = new Coordinate(values[0], values[1]);
            }

            return geometryFactory.CreateLinearRing(transformed);
        }

        // The orientation of a ring, from its coordinates: NetTopologySuite reports the area unsigned, and the signed sum is what must keep its sign through a conversion.
        private static double SignedArea(Coordinate[] coordinates)
        {
            double sum = 0.0;
            for (int i = 0, j = coordinates.Length - 2; i < coordinates.Length - 1; j = i++)
            {
                sum += (coordinates[j].X - coordinates[i].X) * (coordinates[j].Y + coordinates[i].Y);
            }

            return sum / 2.0;
        }

        private static double MaximumVertexDistance(NetTopologySuite.Geometries.Geometry geometry_A, NetTopologySuite.Geometries.Geometry geometry_B)
        {
            Coordinate[] coordinates_A = geometry_A.Coordinates;
            Coordinate[] coordinates_B = geometry_B.Coordinates;

            double maximum = 0.0;
            for (int i = 0; i < Math.Min(coordinates_A.Length, coordinates_B.Length); i++)
            {
                maximum = Math.Max(maximum, Math.Sqrt(Math.Pow(coordinates_A[i].X - coordinates_B[i].X, 2) + Math.Pow(coordinates_A[i].Y - coordinates_B[i].Y, 2)));
            }

            return maximum;
        }

        // q(phi) of the GRS 1980 ellipsoid, as the equal-area property of method 9820 states it.
        private static double Q(double sineLatitude, double eccentricitySquared)
        {
            double eccentricity = Math.Sqrt(eccentricitySquared);
            return (1.0 - eccentricitySquared) * (sineLatitude / (1.0 - eccentricitySquared * sineLatitude * sineLatitude) - 0.5 / eccentricity * Math.Log((1.0 - eccentricity * sineLatitude) / (1.0 + eccentricity * sineLatitude)));
        }
    }
}
