using DiGi.CityGML.Classes;
using DiGi.CityGML.Interfaces;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace DiGi.CityGML.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that every polygon of a CityGML file survives the conversion into a <see cref="CityModel"/>.
        /// <para>The expected counts come from an independent walk of the same file, so the fixtures rather than the parser define the baseline.</para>
        /// <para><see cref="Building"/> stores its surfaces in a dictionary keyed on UniqueId, so a surface carrying no identifier is dropped and two surfaces sharing one overwrite each other - both silently. The fixtures cover the two parse paths that feed that dictionary: the boundedBy path, which reads the identifier off the boundary surface, and the lod1Solid fallback, which has to fall back to the identifier of the polygon child.</para>
        /// </summary>
        /// <param name="fileName">The fixture to load.</param>
        /// <param name="count_Buildings">The number of buildings expected in the fixture.</param>
        /// <param name="count_Surfaces">The number of surfaces expected across all buildings of the fixture.</param>
        [Theory]
        [InlineData("0201_M-33-19-B-d-3-2.gml", 2, 12)]
        [InlineData("2862_N-34-77-D-b-1-1.gml", 3, 20)]
        [InlineData("2476_CityGML.zip", 6, 200)]
        public void CityModels_SurfaceCount(string fileName, int count_Buildings, int count_Surfaces)
        {
            List<Building> buildings = CityGML_Buildings(fileName);
            Dictionary<string, List<double>> areas_ByUniqueId = CityGML_PolygonAreas(fileName);

            Assert.Equal(count_Buildings, buildings.Count);
            Assert.Equal(count_Buildings, areas_ByUniqueId.Count);

            int count = 0;

            foreach (Building building in buildings)
            {
                string? uniqueId = building.UniqueId;

                Assert.False(string.IsNullOrWhiteSpace(uniqueId));
                Assert.True(areas_ByUniqueId.ContainsKey(uniqueId!), string.Format(CultureInfo.CurrentCulture, "Building {0} of {1} has no counterpart in the source file.", uniqueId, fileName));

                List<ISurface>? surfaces = building.Surfaces?.ToList();

                Assert.NotNull(surfaces);
                Assert.Equal(areas_ByUniqueId[uniqueId!].Count, surfaces.Count);

                count += surfaces.Count;
            }

            Assert.Equal(count_Surfaces, count);
        }

        /// <summary>
        /// Tests that every converted surface carries geometry that downstream code can use.
        /// <para>A surface reaching a consumer with a null plane, a degenerate ring or a NaN area is worse than a missing surface, because nothing upstream reports it - <see cref="Convert.ToCityGML_PolygonalFace3D(XmlNode?, double)"/> returns null on a ring it cannot read, and the surface is then simply absent rather than malformed.</para>
        /// <para>The hole count is asserted per building rather than per surface, because a surface identifier is the boundary surface on the boundedBy path but the polygon on the lod1Solid fallback path, so the two cannot be joined by identifier.</para>
        /// </summary>
        /// <param name="fileName">The fixture to load.</param>
        [Theory]
        [InlineData("0201_M-33-19-B-d-3-2.gml")]
        [InlineData("2862_N-34-77-D-b-1-1.gml")]
        [InlineData("2476_CityGML.zip")]
        public void CityModels_SurfaceGeometryValid(string fileName)
        {
            List<Building> buildings = CityGML_Buildings(fileName);
            Dictionary<string, int> counts_InteriorRing = CityGML_InteriorRingCounts(fileName);

            foreach (Building building in buildings)
            {
                string? uniqueId = building.UniqueId;
                Assert.False(string.IsNullOrWhiteSpace(uniqueId));

                List<ISurface>? surfaces = building.Surfaces?.ToList();
                Assert.NotNull(surfaces);
                Assert.NotEmpty(surfaces);

                int count_InternalEdge = 0;

                foreach (ISurface surface in surfaces)
                {
                    string message = string.Format(CultureInfo.CurrentCulture, "Surface {0} of building {1} in {2}", surface.UniqueId, uniqueId, fileName);

                    // Geometry clones on every get, so it is taken once and reused below.
                    IPolygonalFace3D? polygonalFace3D = surface.Geometry;

                    Assert.True(polygonalFace3D is not null, message);
                    Assert.True(polygonalFace3D!.Plane is not null, message);

                    IPolygonalFace2D? polygonalFace2D = polygonalFace3D.Geometry2D;
                    Assert.True(polygonalFace2D is not null, message);

                    List<IPolygonal3D>? polygonal3Ds = polygonalFace3D.Edges;
                    Assert.True(polygonal3Ds is not null && polygonal3Ds.Count != 0, message);

                    IPolygonal3D? polygonal3D_ExternalEdge = polygonalFace3D.ExternalEdge;
                    Assert.True(polygonal3D_ExternalEdge is not null, message);

                    foreach (IPolygonal3D polygonal3D in polygonal3Ds!)
                    {
                        List<Point3D>? point3Ds = polygonal3D.GetPoints();

                        Assert.True(point3Ds is not null && point3Ds.Count >= 3, message);

                        foreach (Point3D point3D in point3Ds!)
                        {
                            Assert.True(point3D is not null, message);
                            Assert.True(CityGML_IsFinite(point3D!), message);
                        }
                    }

                    List<IPolygonal2D>? polygonal2Ds = polygonalFace2D!.Edges;
                    Assert.True(polygonal2Ds is not null && polygonal2Ds.Count == polygonal3Ds!.Count, message);

                    foreach (IPolygonal2D polygonal2D in polygonal2Ds!)
                    {
                        // A ring crossing itself has no meaningful inside, so the area and the containment of the face are undefined.
                        Assert.False(Geometry.Planar.Query.SelfIntersect(polygonal2D), message);
                    }

                    List<IPolygonal3D>? polygonal3Ds_InternalEdges = polygonalFace3D.InternalEdges;
                    if (polygonal3Ds_InternalEdges is not null)
                    {
                        count_InternalEdge += polygonal3Ds_InternalEdges.Count;
                    }

                    double area = polygonalFace3D.GetArea();

                    // GetArea returns NaN rather than zero when the 2D geometry is missing.
                    Assert.False(double.IsNaN(area) || double.IsInfinity(area), message);
                    Assert.True(area > Core.Constants.Tolerance.Distance, message);

                    List<Triangle3D>? triangle3Ds = polygonalFace3D.Triangulate();
                    Assert.True(triangle3Ds is not null && triangle3Ds.Count != 0, message);
                }

                Assert.Equal(counts_InteriorRing[uniqueId!], count_InternalEdge);
            }
        }

        /// <summary>
        /// Tests that the area of every surface survives the conversion.
        /// <para>Each ring is read straight from the file and measured with the area weighted normal of the closed ring, then compared against the area reported by the converted face.</para>
        /// <para>The comparison matters because <see cref="Geometry.Spatial.Create.Polygon3D(IEnumerable{Point3D?}?, double)"/> fits a plane to the ring and projects every vertex onto it; a ring which is not exactly planar therefore changes shape slightly, and this test bounds how much.</para>
        /// <para>The two sets are sorted rather than joined by identifier, because a surface identifier is the boundary surface on the boundedBy path but the polygon on the lod1Solid fallback path.</para>
        /// </summary>
        /// <param name="fileName">The fixture to load.</param>
        [Theory]
        [InlineData("0201_M-33-19-B-d-3-2.gml")]
        [InlineData("2862_N-34-77-D-b-1-1.gml")]
        [InlineData("2476_CityGML.zip")]
        public void CityModels_SurfaceArea(string fileName)
        {
            List<Building> buildings = CityGML_Buildings(fileName);
            Dictionary<string, List<double>> areas_ByUniqueId = CityGML_PolygonAreas(fileName);

            foreach (Building building in buildings)
            {
                string? uniqueId = building.UniqueId;
                Assert.False(string.IsNullOrWhiteSpace(uniqueId));

                List<ISurface>? surfaces = building.Surfaces?.ToList();
                Assert.NotNull(surfaces);

                List<double> areas_Converted = [];
                foreach (ISurface surface in surfaces)
                {
                    IPolygonalFace3D? polygonalFace3D = surface.Geometry;
                    Assert.NotNull(polygonalFace3D);

                    areas_Converted.Add(polygonalFace3D.GetArea());
                }

                List<double> areas_Source = [.. areas_ByUniqueId[uniqueId!]];

                Assert.Equal(areas_Source.Count, areas_Converted.Count);

                areas_Converted.Sort();
                areas_Source.Sort();

                double area_Converted = 0;
                double area_Source = 0;

                for (int i = 0; i < areas_Source.Count; i++)
                {
                    area_Converted += areas_Converted[i];
                    area_Source += areas_Source[i];

                    double difference = Math.Abs(areas_Converted[i] - areas_Source[i]) / areas_Source[i];

                    // Measured worst case across the three fixtures: 5.4E-12 for 0201, 2.6E-09 for 2862 and 2.8E-05 for
                    // 2476, the last of these on a face of four square metres losing 0.115 square millimetres. The bound
                    // is set an order of magnitude above that, so it holds the projection to a change nobody can measure
                    // on site while still catching a face which genuinely comes out the wrong shape.
                    Assert.True(difference < 1E-04, string.Format(CultureInfo.CurrentCulture, "Surface area of building {0} in {1} changed by {2:E3} - source {3}, converted {4}.", uniqueId, fileName, difference, areas_Source[i], areas_Converted[i]));
                }

                // Over a whole building the deviations cancel rather than accumulate - the measured worst case is 1.7E-07.
                Assert.True(Math.Abs(area_Converted - area_Source) / area_Source < 1E-06, string.Format(CultureInfo.CurrentCulture, "Total area of building {0} in {1} changed - source {2}, converted {3}.", uniqueId, fileName, area_Source, area_Converted));
            }
        }

        /// <summary>
        /// Tests that no converted ring holds a repeated vertex, and that each one keeps exactly the corners the source file gives it.
        /// <para>A gml:LinearRing repeats its first position as its last. A polygon stores its ring open - GetSegments adds the closing segment itself - so that repeat has to go, otherwise the ring carries a segment of no length.</para>
        /// <para>That segment used to make <see cref="Geometry.Planar.Query.SelfIntersect(Geometry.Planar.Interfaces.ISegmentable2D?, double)"/> report true for every surface of every fixture, and pushed a CityGML triangle, stored as four points, onto the four point branch of triangulation where it produced a second, degenerate triangle. Both are asserted away here.</para>
        /// <para>The ring point counts are compared against an independent walk of the same file rather than a constant, so the fixtures define how many corners each ring is supposed to keep.</para>
        /// </summary>
        /// <param name="fileName">The fixture to load.</param>
        /// <param name="count_Rings">The number of rings expected across all buildings of the fixture.</param>
        [Theory]
        [InlineData("0201_M-33-19-B-d-3-2.gml", 12)]
        [InlineData("2862_N-34-77-D-b-1-1.gml", 20)]
        [InlineData("2476_CityGML.zip", 204)]
        public void CityModels_NoDuplicateVertex(string fileName, int count_Rings)
        {
            List<Building> buildings = CityGML_Buildings(fileName);
            Dictionary<string, List<int>> counts_ByUniqueId = CityGML_RingPointCounts(fileName);

            int count = 0;

            foreach (Building building in buildings)
            {
                string? uniqueId = building.UniqueId;
                Assert.False(string.IsNullOrWhiteSpace(uniqueId));

                List<ISurface>? surfaces = building.Surfaces?.ToList();
                Assert.NotNull(surfaces);

                List<int> counts_Converted = [];

                foreach (ISurface surface in surfaces)
                {
                    List<IPolygonal2D>? polygonal2Ds = surface.Geometry?.Geometry2D?.Edges;
                    Assert.NotNull(polygonal2Ds);

                    foreach (IPolygonal2D polygonal2D in polygonal2Ds)
                    {
                        string message = string.Format(CultureInfo.CurrentCulture, "Surface {0} of building {1} in {2}", surface.UniqueId, uniqueId, fileName);

                        List<Point2D>? point2Ds = polygonal2D.GetPoints();
                        Assert.True(point2Ds is not null && point2Ds.Count >= 3, message);

                        Assert.True(point2Ds![0].Distance(point2Ds[point2Ds.Count - 1]) > Core.Constants.Tolerance.Distance, message);

                        for (int i = 0; i < point2Ds.Count; i++)
                        {
                            Point2D point2D_Next = point2Ds[i == point2Ds.Count - 1 ? 0 : i + 1];

                            Assert.True(point2Ds[i].Distance(point2D_Next) > Core.Constants.Tolerance.Distance, message);
                        }

                        Assert.False(Geometry.Planar.Query.SelfIntersect(polygonal2D), message);

                        counts_Converted.Add(point2Ds.Count);
                        count++;
                    }
                }

                List<int> counts_Source = [.. counts_ByUniqueId[uniqueId!]];

                counts_Converted.Sort();
                counts_Source.Sort();

                Assert.Equal(counts_Source, counts_Converted);
            }

            Assert.Equal(count_Rings, count);
        }

        /// <summary>
        /// Tests that a parsed city model survives a JSON round trip with its surface geometry intact.
        /// <para>The existing round trip covers only the 2862 fixture, so the LOD1 file - whose surfaces are all untyped and reach the model through the lod1Solid fallback - was never exercised, nor was a model holding a face with holes.</para>
        /// </summary>
        /// <param name="fileName">The fixture to load.</param>
        [Theory]
        [InlineData("0201_M-33-19-B-d-3-2.gml")]
        [InlineData("2862_N-34-77-D-b-1-1.gml")]
        [InlineData("2476_CityGML.zip")]
        public void CityModels_SerializationCheck(string fileName)
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), fileName);

            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path));

            List<CityModel>? cityModels = Create.CityModels(path);

            Assert.NotNull(cityModels);
            Assert.Single(cityModels);

            Core.xUnit.Query.SerializationCheck(cityModels[0]);
        }

        /// <summary>
        /// Loads a CityGML fixture and returns every building it holds.
        /// </summary>
        /// <param name="fileName">The name of the fixture in the shared files folder.</param>
        /// <returns>The buildings of the fixture.</returns>
        private static List<Building> CityGML_Buildings(string fileName)
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), fileName);

            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path));

            List<CityModel>? cityModels = Create.CityModels(path);

            Assert.NotNull(cityModels);
            Assert.NotEmpty(cityModels);

            List<Building> result = [];
            foreach (CityModel cityModel in cityModels)
            {
                IEnumerable<Building>? buildings = cityModel?.Buildings;
                if (buildings is null)
                {
                    continue;
                }

                result.AddRange(buildings);
            }

            return result;
        }

        /// <summary>
        /// Reads a CityGML fixture as a raw XML document, unwrapping the archive when the fixture is zipped.
        /// </summary>
        /// <param name="fileName">The name of the fixture in the shared files folder.</param>
        /// <returns>The XML document of the fixture.</returns>
        private static XmlDocument CityGML_XmlDocument(string fileName)
        {
            string? path = Core.xUnit.Query.FilePath(Assembly.GetExecutingAssembly(), fileName);

            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.True(File.Exists(path));

            XmlDocument result = new();

            if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                result.Load(path!);

                return result;
            }

            using FileStream fileStream = new(path!, FileMode.Open, FileAccess.Read);
            using ZipArchive zipArchive = new(fileStream, ZipArchiveMode.Read);

            ZipArchiveEntry? zipArchiveEntry_Gml = null;
            foreach (ZipArchiveEntry zipArchiveEntry in zipArchive.Entries)
            {
                if (zipArchiveEntry.Name.EndsWith(".gml", StringComparison.OrdinalIgnoreCase))
                {
                    zipArchiveEntry_Gml = zipArchiveEntry;
                    break;
                }
            }

            Assert.NotNull(zipArchiveEntry_Gml);

            using Stream stream = zipArchiveEntry_Gml.Open();
            result.Load(stream);

            return result;
        }

        /// <summary>
        /// Collects every descendant node carrying the given local name, ignoring namespace prefixes.
        /// <para>Matching nodes are not descended into, which is safe for the building and polygon names this is used with.</para>
        /// </summary>
        /// <param name="xmlNode">The node to search below.</param>
        /// <param name="localName">The local name to match.</param>
        /// <returns>The matching descendant nodes.</returns>
        private static List<XmlNode> CityGML_XmlNodes(XmlNode? xmlNode, string localName)
        {
            List<XmlNode> result = [];

            XmlNodeList? xmlNodeList = xmlNode?.ChildNodes;
            if (xmlNodeList is null)
            {
                return result;
            }

            foreach (XmlNode xmlNode_Temp in xmlNodeList)
            {
                if (xmlNode_Temp.LocalName == localName)
                {
                    result.Add(xmlNode_Temp);
                    continue;
                }

                result.AddRange(CityGML_XmlNodes(xmlNode_Temp, localName));
            }

            return result;
        }

        /// <summary>
        /// Reads the gml:id of a node without relying on the library under test.
        /// </summary>
        /// <param name="xmlNode">The node to read.</param>
        /// <returns>The identifier if the node carries one; otherwise, null.</returns>
        private static string? CityGML_UniqueId(XmlNode? xmlNode)
        {
            XmlAttributeCollection? xmlAttributes = xmlNode?.Attributes;
            if (xmlAttributes is null)
            {
                return null;
            }

            foreach (XmlAttribute xmlAttribute in xmlAttributes)
            {
                if (xmlAttribute?.LocalName == "id")
                {
                    return xmlAttribute.InnerText;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads the vertices of a gml:LinearRing, accepting both the posList and the pos spelling.
        /// <para>The repeated closing vertex is dropped so the ring holds each corner once.</para>
        /// </summary>
        /// <param name="xmlNode_LinearRing">The linear ring node.</param>
        /// <returns>The corners of the ring.</returns>
        private static List<Point3D> CityGML_Point3Ds(XmlNode? xmlNode_LinearRing)
        {
            List<Point3D> result = [];

            XmlNodeList? xmlNodeList = xmlNode_LinearRing?.ChildNodes;
            if (xmlNodeList is null)
            {
                return result;
            }

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                if (xmlNode.LocalName != "posList" && xmlNode.LocalName != "pos")
                {
                    continue;
                }

                string[] values = xmlNode.InnerText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i + 2 < values.Length; i += 3)
                {
                    result.Add(new Point3D(double.Parse(values[i], System.Globalization.CultureInfo.InvariantCulture), double.Parse(values[i + 1], System.Globalization.CultureInfo.InvariantCulture), double.Parse(values[i + 2], System.Globalization.CultureInfo.InvariantCulture)));
                }
            }

            if (result.Count >= 2 && CityGML_AlmostEquals(result[0], result[result.Count - 1]))
            {
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        /// <summary>
        /// Measures the area of a closed ring in space using the magnitude of its area weighted normal.
        /// <para>This holds for any ring, planar or not, and is independent of the plane fitting the library performs.</para>
        /// </summary>
        /// <param name="point3Ds">The corners of the ring.</param>
        /// <returns>The area of the ring, or zero when the ring holds fewer than three corners.</returns>
        private static double CityGML_Area(List<Point3D> point3Ds)
        {
            int count = point3Ds.Count;
            if (count < 3)
            {
                return 0;
            }

            double x = 0;
            double y = 0;
            double z = 0;

            for (int i = 0; i < count; i++)
            {
                Point3D point3D_Current = point3Ds[i];
                Point3D point3D_Next = point3Ds[i == count - 1 ? 0 : i + 1];

                x += (point3D_Current.Y - point3D_Next.Y) * (point3D_Current.Z + point3D_Next.Z);
                y += (point3D_Current.Z - point3D_Next.Z) * (point3D_Current.X + point3D_Next.X);
                z += (point3D_Current.X - point3D_Next.X) * (point3D_Current.Y + point3D_Next.Y);
            }

            return Math.Sqrt((x * x) + (y * y) + (z * z)) / 2.0;
        }

        /// <summary>
        /// Reads the net area of every polygon of every building straight from the file.
        /// <para>The net area of a polygon is the area of its exterior ring less the area of each interior ring, which is what a polygonal face reports.</para>
        /// </summary>
        /// <param name="fileName">The name of the fixture in the shared files folder.</param>
        /// <returns>The polygon areas of each building, keyed on the gml:id of the building.</returns>
        private static Dictionary<string, List<double>> CityGML_PolygonAreas(string fileName)
        {
            Dictionary<string, List<double>> result = [];

            XmlDocument xmlDocument = CityGML_XmlDocument(fileName);

            foreach (XmlNode xmlNode_Building in CityGML_XmlNodes(xmlDocument, "Building"))
            {
                string? uniqueId = CityGML_UniqueId(xmlNode_Building);

                Assert.False(string.IsNullOrWhiteSpace(uniqueId));
                Assert.False(result.ContainsKey(uniqueId!), string.Format(CultureInfo.CurrentCulture, "Building {0} of {1} is declared more than once.", uniqueId, fileName));

                List<double> areas = [];

                foreach (XmlNode xmlNode_Polygon in CityGML_XmlNodes(xmlNode_Building, "Polygon"))
                {
                    double area = 0;

                    foreach (XmlNode xmlNode in xmlNode_Polygon.ChildNodes)
                    {
                        if (xmlNode.LocalName != "exterior" && xmlNode.LocalName != "interior")
                        {
                            continue;
                        }

                        foreach (XmlNode xmlNode_LinearRing in CityGML_XmlNodes(xmlNode, "LinearRing"))
                        {
                            double area_Ring = CityGML_Area(CityGML_Point3Ds(xmlNode_LinearRing));

                            area += xmlNode.LocalName == "exterior" ? area_Ring : -area_Ring;
                        }
                    }

                    areas.Add(area);
                }

                result[uniqueId!] = areas;
            }

            return result;
        }

        /// <summary>
        /// Counts the interior rings of every building straight from the file.
        /// </summary>
        /// <param name="fileName">The name of the fixture in the shared files folder.</param>
        /// <returns>The interior ring count of each building, keyed on the gml:id of the building.</returns>
        private static Dictionary<string, int> CityGML_InteriorRingCounts(string fileName)
        {
            Dictionary<string, int> result = [];

            XmlDocument xmlDocument = CityGML_XmlDocument(fileName);

            foreach (XmlNode xmlNode_Building in CityGML_XmlNodes(xmlDocument, "Building"))
            {
                string? uniqueId = CityGML_UniqueId(xmlNode_Building);

                Assert.False(string.IsNullOrWhiteSpace(uniqueId));

                int count = 0;

                foreach (XmlNode xmlNode_Polygon in CityGML_XmlNodes(xmlNode_Building, "Polygon"))
                {
                    foreach (XmlNode xmlNode in xmlNode_Polygon.ChildNodes)
                    {
                        if (xmlNode.LocalName != "interior")
                        {
                            continue;
                        }

                        count += CityGML_XmlNodes(xmlNode, "LinearRing").Count;
                    }
                }

                result[uniqueId!] = count;
            }

            return result;
        }

        /// <summary>
        /// Counts the corners of every ring of every building straight from the file.
        /// <para>Uses the same reader as the area baseline, which drops the repeated closing position, so the counts are what each ring is expected to hold once it has been normalised.</para>
        /// </summary>
        /// <param name="fileName">The name of the fixture in the shared files folder.</param>
        /// <returns>The corner count of each ring of each building, keyed on the gml:id of the building.</returns>
        private static Dictionary<string, List<int>> CityGML_RingPointCounts(string fileName)
        {
            Dictionary<string, List<int>> result = [];

            XmlDocument xmlDocument = CityGML_XmlDocument(fileName);

            foreach (XmlNode xmlNode_Building in CityGML_XmlNodes(xmlDocument, "Building"))
            {
                string? uniqueId = CityGML_UniqueId(xmlNode_Building);

                Assert.False(string.IsNullOrWhiteSpace(uniqueId));

                List<int> counts = [];

                foreach (XmlNode xmlNode_Polygon in CityGML_XmlNodes(xmlNode_Building, "Polygon"))
                {
                    foreach (XmlNode xmlNode in xmlNode_Polygon.ChildNodes)
                    {
                        if (xmlNode.LocalName != "exterior" && xmlNode.LocalName != "interior")
                        {
                            continue;
                        }

                        foreach (XmlNode xmlNode_LinearRing in CityGML_XmlNodes(xmlNode, "LinearRing"))
                        {
                            counts.Add(CityGML_Point3Ds(xmlNode_LinearRing).Count);
                        }
                    }
                }

                result[uniqueId!] = counts;
            }

            return result;
        }

        /// <summary>
        /// Determines whether every coordinate of a point is a finite number.
        /// </summary>
        /// <param name="point3D">The point to check.</param>
        /// <returns>True when the point holds no NaN and no infinity; otherwise, false.</returns>
        private static bool CityGML_IsFinite(Point3D point3D)
        {
            return !double.IsNaN(point3D.X) && !double.IsInfinity(point3D.X)
                && !double.IsNaN(point3D.Y) && !double.IsInfinity(point3D.Y)
                && !double.IsNaN(point3D.Z) && !double.IsInfinity(point3D.Z);
        }

        /// <summary>
        /// Determines whether two points hold the same coordinates within the distance tolerance.
        /// </summary>
        /// <param name="point3D_1">The first point.</param>
        /// <param name="point3D_2">The second point.</param>
        /// <returns>True when the points coincide; otherwise, false.</returns>
        private static bool CityGML_AlmostEquals(Point3D point3D_1, Point3D point3D_2)
        {
            return Math.Abs(point3D_1.X - point3D_2.X) < Core.Constants.Tolerance.Distance
                && Math.Abs(point3D_1.Y - point3D_2.Y) < Core.Constants.Tolerance.Distance
                && Math.Abs(point3D_1.Z - point3D_2.Z) < Core.Constants.Tolerance.Distance;
        }
    }
}
