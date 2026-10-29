using DiGi.BDOT10k.Geometry.Classes;
using DiGi.Geometry.Planar.Classes;
using DiGi.PRG.Geometry.Classes;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The radius around a footprint within which a road with the address point's ULIC counts for the same-ULIC rule. Reference: DiGi.Test#5.
        /// </summary>
        private const double frontFacade_SameULICDistance = 50.0;

        /// <summary>
        /// The radius around a footprint searched for the nearest road of any class, and for the road distance metrics.
        /// </summary>
        private const double frontFacade_SearchDistance = 200.0;

        /// <summary>
        /// The side of a cell of the road and footprint grid indexes, in metres.
        /// </summary>
        private const double frontFacade_CellSize = 100.0;

        /// <summary>
        /// Tests the four front-facade rules of the road assessment (DiGi.Test#5) on the 0262 fixtures and on a synthetic footprint around a real address point.
        /// <para>The address point pl. Słowiański 7 in Legnica (ULIC 20295) lies 31 m from the BDOT10k carriageway <c>OT_SKJZ_L</c> <c>fd53462e-…</c> of the same square (see <see cref="Point2D_AxisOrder"/>), and the nearest other fixture road is about 1 km away. A 12 m by 8 m footprint is placed with the point 1 m inside its eastern edge, so the expected edges follow from the construction: the address-point rule picks the eastern edge, the longest-edge rule a long edge, and both road rules the edge whose outward normal faces the carriageway, which must be the same edge because only one road is in range. Collinear halves of one facade count as the same side. pl. Słowiański 5 lies 83 m from the same carriageway, beyond the 50 m same-ULIC radius, so its footprint finds no same-ULIC road. This is the regression guard of the ULIC join between PRG and BDOT10k.</para>
        /// </summary>
        [Fact]
        public void FrontFacade_Rules()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            SlownikObiektowGeometrycznych slownikObiektowGeometrycznych = new();
            Assert.True(BDOT10k.Geometry.Modify.Load(slownikObiektowGeometrycznych, Core.xUnit.Query.FilePath(assembly, "0262_BDOT10k_Roads.xml")));

            List<(Point2D Start, Point2D End, string? ULIC)> roadSegments = FrontFacade_RoadSegments(slownikObiektowGeometrycznych);
            Assert.NotEmpty(roadSegments);
            Assert.Contains(roadSegments, x => x.ULIC == "20295");

            Dictionary<(long, long), List<int>> roadIndex = FrontFacade_Index(roadSegments);

            List<PunktAdresowy>? punktyAdresowe = Create.PunktyAdresowe(Core.xUnit.Query.FilePath(assembly, "0262_PRG_AddressPoints.xml"));
            Assert.NotNull(punktyAdresowe);

            PunktAdresowy? punktAdresowy_7 = punktyAdresowe.Find(x => x.identyfikatorULIC == "20295" && x.numerPorzadkowy == "7");
            Point2D? point2D_7 = punktAdresowy_7?.Geometry;
            Assert.NotNull(point2D_7);

            // Counter-clockwise 12 m (east-west) by 8 m (north-south) rectangle, the point 1 m inside the eastern edge (index 1)
            double east = point2D_7.X + 1.0;
            double north = point2D_7.Y + 4.0;
            List<Point2D> ring = [new(east - 12.0, north - 8.0), new(east, north - 8.0), new(east, north), new(east - 12.0, north)];

            Assert.Equal(1, FrontFacade_NearestEdge(ring, point2D_7));

            int edge_Longest = FrontFacade_LongestEdge(ring);
            Assert.True(edge_Longest == 0 || edge_Longest == 2);

            (Point2D? point2D_SameULIC, double distance_SameULIC) = FrontFacade_NearestRoad(ring, roadSegments, roadIndex, "20295", frontFacade_SameULICDistance);
            Assert.NotNull(point2D_SameULIC);
            Assert.InRange(distance_SameULIC, 0.0, frontFacade_SameULICDistance);

            (Point2D? point2D_Nearest, double distance_Nearest) = FrontFacade_NearestRoad(ring, roadSegments, roadIndex, null, frontFacade_SearchDistance);
            Assert.NotNull(point2D_Nearest);
            Assert.Equal(distance_SameULIC, distance_Nearest, 6);

            int edge_SameULIC = FrontFacade_FacingEdge(ring, point2D_SameULIC);
            Assert.InRange(edge_SameULIC, 0, ring.Count - 1);
            Assert.Equal(edge_SameULIC, FrontFacade_FacingEdge(ring, point2D_Nearest));

            // The chosen edge faces the road: its outward normal points towards the road point
            Point2D start = ring[edge_SameULIC];
            Point2D end = ring[(edge_SameULIC + 1) % ring.Count];
            double normalX = end.Y - start.Y;
            double normalY = start.X - end.X;
            Assert.True((normalX * (point2D_SameULIC.X - (start.X + end.X) / 2.0)) + (normalY * (point2D_SameULIC.Y - (start.Y + end.Y) / 2.0)) > 0.0);

            // pl. Słowiański 5 lies 83 m from the carriageway: no same-ULIC road within 50 m of a footprint around it
            Point2D? point2D_5 = punktyAdresowe.Find(x => x.identyfikatorULIC == "20295" && x.numerPorzadkowy == "5")?.Geometry;
            Assert.NotNull(point2D_5);
            List<Point2D> ring_5 = [new(point2D_5.X - 4.0, point2D_5.Y - 4.0), new(point2D_5.X + 4.0, point2D_5.Y - 4.0), new(point2D_5.X + 4.0, point2D_5.Y + 4.0), new(point2D_5.X - 4.0, point2D_5.Y + 4.0)];
            (Point2D? point2D_SameULIC_5, _) = FrontFacade_NearestRoad(ring_5, roadSegments, roadIndex, "20295", frontFacade_SameULICDistance);
            Assert.Null(point2D_SameULIC_5);

            // Two collinear halves of one facade face the same side; opposite and perpendicular edges do not
            List<Point2D> ring_Split = [new(0.0, 0.0), new(6.0, 0.0), new(12.0, 0.0), new(12.0, 8.0), new(0.0, 8.0)];
            Assert.True(FrontFacade_SameSide(ring_Split, 0, 1));
            Assert.True(FrontFacade_SameSide(ring_Split, 2, 2));
            Assert.False(FrontFacade_SameSide(ring_Split, 0, 2));
            Assert.False(FrontFacade_SameSide(ring_Split, 0, 3));
            Assert.False(FrontFacade_SameSide(ring_Split, 0, -1));

            // A clockwise ring is reoriented, so the eastern edge keeps facing east
            List<Point2D>? ring_Clockwise = FrontFacade_Ring([ring[0], ring[3], ring[2], ring[1], ring[0]]);
            Assert.NotNull(ring_Clockwise);
            Assert.Equal(4, ring_Clockwise.Count);
            Assert.True(FrontFacade_SignedArea(ring_Clockwise) > 0.0);
        }

        /// <summary>
        /// Returns the points of a footprint ring counter-clockwise, without a closing point repeating the first, or null when fewer than three remain.
        /// </summary>
        /// <param name="point2Ds">The ring points, in either orientation, closed or open.</param>
        /// <returns>The counter-clockwise open ring, or null.</returns>
        private static List<Point2D>? FrontFacade_Ring(List<Point2D>? point2Ds)
        {
            if (point2Ds is null)
            {
                return null;
            }

            List<Point2D> result = [.. point2Ds];
            if (result.Count > 1 && result[0].X == result[^1].X && result[0].Y == result[^1].Y)
            {
                result.RemoveAt(result.Count - 1);
            }

            if (result.Count < 3)
            {
                return null;
            }

            if (FrontFacade_SignedArea(result) < 0.0)
            {
                result.Reverse();
            }

            return result;
        }

        /// <summary>
        /// Returns the signed area of an open ring: positive when it runs counter-clockwise (X east, Y north).
        /// </summary>
        /// <param name="ring">The open ring.</param>
        /// <returns>The signed area.</returns>
        private static double FrontFacade_SignedArea(List<Point2D> ring)
        {
            double result = 0.0;
            for (int i = 0; i < ring.Count; i++)
            {
                Point2D point2D_1 = ring[i];
                Point2D point2D_2 = ring[(i + 1) % ring.Count];
                result += (point2D_1.X * point2D_2.Y) - (point2D_2.X * point2D_1.Y);
            }

            return result / 2.0;
        }

        /// <summary>
        /// Returns the distance from a point to a segment and the closest point of the segment.
        /// </summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <param name="start">The segment start.</param>
        /// <param name="end">The segment end.</param>
        /// <returns>The distance and the closest point coordinates.</returns>
        private static (double Distance, double X, double Y) FrontFacade_Closest(double x, double y, Point2D start, Point2D end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length_Squared = (dx * dx) + (dy * dy);
            double parameter = length_Squared <= 0.0 ? 0.0 : Math.Clamp((((x - start.X) * dx) + ((y - start.Y) * dy)) / length_Squared, 0.0, 1.0);
            double closestX = start.X + (parameter * dx);
            double closestY = start.Y + (parameter * dy);
            return (Math.Sqrt(((x - closestX) * (x - closestX)) + ((y - closestY) * (y - closestY))), closestX, closestY);
        }

        /// <summary>
        /// Returns the index of the ring edge nearest a point (the address-point rule). Edge <c>i</c> runs from point <c>i</c> to point <c>i + 1</c>.
        /// </summary>
        /// <param name="ring">The counter-clockwise open ring.</param>
        /// <param name="point2D">The point.</param>
        /// <returns>The edge index.</returns>
        private static int FrontFacade_NearestEdge(List<Point2D> ring, Point2D point2D)
        {
            int result = -1;
            double distance_Min = double.MaxValue;
            for (int i = 0; i < ring.Count; i++)
            {
                double distance = FrontFacade_Closest(point2D.X, point2D.Y, ring[i], ring[(i + 1) % ring.Count]).Distance;
                if (distance < distance_Min)
                {
                    distance_Min = distance;
                    result = i;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the index of the longest ring edge (the fallback rule of DiGi.GIS.Analytical#2).
        /// </summary>
        /// <param name="ring">The counter-clockwise open ring.</param>
        /// <returns>The edge index.</returns>
        private static int FrontFacade_LongestEdge(List<Point2D> ring)
        {
            int result = -1;
            double length_Max = -1.0;
            for (int i = 0; i < ring.Count; i++)
            {
                Point2D end = ring[(i + 1) % ring.Count];
                double length = Math.Sqrt(((end.X - ring[i].X) * (end.X - ring[i].X)) + ((end.Y - ring[i].Y) * (end.Y - ring[i].Y)));
                if (length > length_Max)
                {
                    length_Max = length;
                    result = i;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the index of the ring edge facing a target point: among the edges whose outward normal points towards the target (positive dot product with the vector from the edge midpoint), the one nearest the target; the nearest edge when no edge faces it.
        /// </summary>
        /// <param name="ring">The counter-clockwise open ring.</param>
        /// <param name="target">The target point, a point of the road.</param>
        /// <returns>The edge index.</returns>
        private static int FrontFacade_FacingEdge(List<Point2D> ring, Point2D target)
        {
            int result = -1;
            double distance_Min = double.MaxValue;
            for (int i = 0; i < ring.Count; i++)
            {
                Point2D start = ring[i];
                Point2D end = ring[(i + 1) % ring.Count];

                // Outward normal of a counter-clockwise ring
                double normalX = end.Y - start.Y;
                double normalY = start.X - end.X;
                if ((normalX * (target.X - ((start.X + end.X) / 2.0))) + (normalY * (target.Y - ((start.Y + end.Y) / 2.0))) <= 0.0)
                {
                    continue;
                }

                double distance = FrontFacade_Closest(target.X, target.Y, start, end).Distance;
                if (distance < distance_Min)
                {
                    distance_Min = distance;
                    result = i;
                }
            }

            return result == -1 ? FrontFacade_NearestEdge(ring, target) : result;
        }

        /// <summary>
        /// Returns whether two ring edges face the same side of the building: the same edge, or outward normals at most <paramref name="angle_Max"/> degrees apart. BDOT10k footprints often split one facade into several collinear edges, which an exact index comparison would count as different.
        /// </summary>
        /// <param name="ring">The counter-clockwise open ring.</param>
        /// <param name="index_1">The first edge index.</param>
        /// <param name="index_2">The second edge index.</param>
        /// <param name="angle_Max">The largest angle between the outward normals, in degrees.</param>
        /// <returns>True when both edges face the same side.</returns>
        private static bool FrontFacade_SameSide(List<Point2D> ring, int index_1, int index_2, double angle_Max = 20.0)
        {
            if (index_1 == index_2)
            {
                return true;
            }

            if (index_1 < 0 || index_2 < 0)
            {
                return false;
            }

            Point2D start_1 = ring[index_1];
            Point2D end_1 = ring[(index_1 + 1) % ring.Count];
            Point2D start_2 = ring[index_2];
            Point2D end_2 = ring[(index_2 + 1) % ring.Count];

            // Cosine of the angle between the edge directions equals that between their outward normals
            double x_1 = end_1.X - start_1.X;
            double y_1 = end_1.Y - start_1.Y;
            double x_2 = end_2.X - start_2.X;
            double y_2 = end_2.Y - start_2.Y;
            double length = Math.Sqrt(((x_1 * x_1) + (y_1 * y_1)) * ((x_2 * x_2) + (y_2 * y_2)));
            return length > 0.0 && ((x_1 * x_2) + (y_1 * y_2)) / length >= Math.Cos(angle_Max * Math.PI / 180.0);
        }

        /// <summary>
        /// Returns every segment of every SKDR, SKJZ and SKRP road, with the road's ULIC (null for SKDR, which carries none).
        /// </summary>
        /// <param name="slownikObiektowGeometrycznych">The loaded BDOT10k objects.</param>
        /// <returns>The road segments.</returns>
        private static List<(Point2D Start, Point2D End, string? ULIC)> FrontFacade_RoadSegments(SlownikObiektowGeometrycznych slownikObiektowGeometrycznych)
        {
            List<(Point2D Start, Point2D End, string? ULIC)> result = [];

            void Add(Polyline2D? polyline2D, string? uLIC)
            {
                List<Point2D>? point2Ds = polyline2D?.GetPoints();
                if (point2Ds is null)
                {
                    return;
                }

                string? uLIC_Valid = string.IsNullOrWhiteSpace(uLIC) ? null : uLIC.Trim();
                for (int i = 0; i < point2Ds.Count - 1; i++)
                {
                    result.Add((point2Ds[i], point2Ds[i + 1], uLIC_Valid));
                }
            }

            slownikObiektowGeometrycznych.GetObiektyGeometryczne<SKDR_L>()?.ForEach(x => Add(x.Geometry, null));
            slownikObiektowGeometrycznych.GetObiektyGeometryczne<SKJZ_L>()?.ForEach(x => Add(x.Geometry, x.identyfikatorULIC));
            slownikObiektowGeometrycznych.GetObiektyGeometryczne<SKRP_L>()?.ForEach(x => Add(x.Geometry, x.identyfikatorULIC));

            return result;
        }

        /// <summary>
        /// Builds a one-pass grid index of segments: each cell lists the segments whose bounding box touches it.
        /// </summary>
        /// <param name="segments">The segments.</param>
        /// <returns>The cell to segment indexes map.</returns>
        private static Dictionary<(long, long), List<int>> FrontFacade_Index(List<(Point2D Start, Point2D End, string? ULIC)> segments)
        {
            Dictionary<(long, long), List<int>> result = [];
            for (int i = 0; i < segments.Count; i++)
            {
                (Point2D start, Point2D end, _) = segments[i];
                for (long cellX = FrontFacade_Cell(Math.Min(start.X, end.X)); cellX <= FrontFacade_Cell(Math.Max(start.X, end.X)); cellX++)
                {
                    for (long cellY = FrontFacade_Cell(Math.Min(start.Y, end.Y)); cellY <= FrontFacade_Cell(Math.Max(start.Y, end.Y)); cellY++)
                    {
                        if (!result.TryGetValue((cellX, cellY), out List<int>? indexes))
                        {
                            indexes = [];
                            result[(cellX, cellY)] = indexes;
                        }

                        indexes.Add(i);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the grid cell of a coordinate.
        /// </summary>
        /// <param name="value">The coordinate.</param>
        /// <returns>The cell.</returns>
        private static long FrontFacade_Cell(double value)
        {
            return (long)Math.Floor(value / frontFacade_CellSize);
        }

        /// <summary>
        /// Returns the road point nearest a footprint, among the segments within <paramref name="distance_Max"/> of it, and that distance; null when none is in range.
        /// <para>With a <paramref name="uLIC"/>, only segments carrying that ULIC count (the same-ULIC rule); with null, every segment (the nearest-road rule). The distance between a segment and the ring is the smallest of the ring vertices to the segment and the segment ends to the ring edges, so the returned point is either the segment point closest to a ring vertex or a segment end.</para>
        /// </summary>
        /// <param name="ring">The counter-clockwise open ring.</param>
        /// <param name="segments">The road segments.</param>
        /// <param name="index">The grid index of <paramref name="segments"/>.</param>
        /// <param name="uLIC">The ULIC a segment must carry, or null for any.</param>
        /// <param name="distance_Max">The search radius around the footprint.</param>
        /// <returns>The road point and its distance, or null and <see cref="double.MaxValue"/>.</returns>
        private static (Point2D? Point2D, double Distance) FrontFacade_NearestRoad(List<Point2D> ring, List<(Point2D Start, Point2D End, string? ULIC)> segments, Dictionary<(long, long), List<int>> index, string? uLIC, double distance_Max)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;
            foreach (Point2D point2D in ring)
            {
                minX = Math.Min(minX, point2D.X);
                minY = Math.Min(minY, point2D.Y);
                maxX = Math.Max(maxX, point2D.X);
                maxY = Math.Max(maxY, point2D.Y);
            }

            HashSet<int> visited = [];
            Point2D? result = null;
            double distance_Min = double.MaxValue;
            for (long cellX = FrontFacade_Cell(minX - distance_Max); cellX <= FrontFacade_Cell(maxX + distance_Max); cellX++)
            {
                for (long cellY = FrontFacade_Cell(minY - distance_Max); cellY <= FrontFacade_Cell(maxY + distance_Max); cellY++)
                {
                    if (!index.TryGetValue((cellX, cellY), out List<int>? indexes))
                    {
                        continue;
                    }

                    foreach (int i in indexes)
                    {
                        if (!visited.Add(i))
                        {
                            continue;
                        }

                        (Point2D start, Point2D end, string? uLIC_Segment) = segments[i];
                        if (uLIC is not null && uLIC_Segment != uLIC)
                        {
                            continue;
                        }

                        foreach (Point2D point2D in ring)
                        {
                            (double distance, double x, double y) = FrontFacade_Closest(point2D.X, point2D.Y, start, end);
                            if (distance < distance_Min)
                            {
                                distance_Min = distance;
                                result = new Point2D(x, y);
                            }
                        }

                        foreach (Point2D point2D_End in (Point2D[])[start, end])
                        {
                            for (int j = 0; j < ring.Count; j++)
                            {
                                double distance = FrontFacade_Closest(point2D_End.X, point2D_End.Y, ring[j], ring[(j + 1) % ring.Count]).Distance;
                                if (distance < distance_Min)
                                {
                                    distance_Min = distance;
                                    result = point2D_End;
                                }
                            }
                        }
                    }
                }
            }

            return distance_Min <= distance_Max ? (result, distance_Min) : (null, double.MaxValue);
        }
    }
}
