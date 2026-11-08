using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.Geometry.Spatial;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace DiGi.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Method 1: the vertex-welding implementation that shipped before the weld-free rewrite, kept verbatim so the benchmark can still measure what was replaced.
        /// <para>Face vertices are welded into shared indices using a tolerance-sized spatial hash and each resulting edge is counted. This is the algorithm whose non-monotonicity is the subject of DiGi.Geometry issue 1 - it is retained for comparison only and must never be called outside this file.</para>
        /// </summary>
        /// <typeparam name="TPolygonalFace3D">The type of the polygonal face.</typeparam>
        /// <param name="polyhedron">The polyhedron to evaluate.</param>
        /// <param name="manifold">When true, every edge must be used exactly twice.</param>
        /// <param name="tolerance">The distance tolerance used to weld coincident vertices.</param>
        /// <returns>True if the polyhedron is closed.</returns>
        private static bool IsClosed_VertexWeld<TPolygonalFace3D>(Polyhedron<TPolygonalFace3D>? polyhedron, bool manifold, double tolerance = DiGi.Core.Constants.Tolerance.Distance) where TPolygonalFace3D : IPolygonalFace3D
        {
            if (polyhedron is null || polyhedron.Count < 4)
            {
                return false;
            }

            double tolerance_Temp = tolerance > 0.0 ? tolerance : DiGi.Core.Constants.Tolerance.MicroDistance;

            double invTolerance = 1.0 / tolerance_Temp;
            double toleranceSquared = tolerance_Temp * tolerance_Temp;

            // The cell grid is only an accelerator that narrows which vertices get compared - a match is always confirmed
            // by the squared-distance test below. An extreme coordinate-to-tolerance ratio can therefore degrade the grid
            // (in the limit every vertex lands in one cell, or coincident vertices land more than one cell apart) and
            // cost time or report open, but it can never fabricate a match and report a false closure.
            // Welded vertices are held as flat X, Y, Z triplets so no Point3D is allocated per vertex. Cells hold the
            // head of a chain threaded through indexes_Next rather than a List per cell, which would allocate one list
            // object for every occupied cell - and cells are mostly occupied by a single vertex.
            List<double> coordinates = new(polyhedron.Count * 3);
            List<int> indexes_Next = new(polyhedron.Count);
            Dictionary<(long X, long Y, long Z), int> index_ByCell = [];

            int IndexInCell(long cellX, long cellY, long cellZ, double x, double y, double z)
            {
                if (!index_ByCell.TryGetValue((cellX, cellY, cellZ), out int index))
                {
                    return -1;
                }

                while (index >= 0)
                {
                    int offset = index * 3;

                    double dx = coordinates[offset] - x;
                    double dy = coordinates[offset + 1] - y;
                    double dz = coordinates[offset + 2] - z;

                    if ((dx * dx) + (dy * dy) + (dz * dz) <= toleranceSquared)
                    {
                        return index;
                    }

                    index = indexes_Next[index];
                }

                return -1;
            }

            int Index(double x, double y, double z)
            {
                long cellX = (long)System.Math.Floor(x * invTolerance);
                long cellY = (long)System.Math.Floor(y * invTolerance);
                long cellZ = (long)System.Math.Floor(z * invTolerance);

                // Nearly every match lands in the centre cell, so it is probed before the 26 neighbours.
                int index = IndexInCell(cellX, cellY, cellZ, x, y, z);
                if (index >= 0)
                {
                    return index;
                }

                for (long neighbourX = cellX - 1; neighbourX <= cellX + 1; neighbourX++)
                {
                    for (long neighbourY = cellY - 1; neighbourY <= cellY + 1; neighbourY++)
                    {
                        for (long neighbourZ = cellZ - 1; neighbourZ <= cellZ + 1; neighbourZ++)
                        {
                            if (neighbourX == cellX && neighbourY == cellY && neighbourZ == cellZ)
                            {
                                continue;
                            }

                            index = IndexInCell(neighbourX, neighbourY, neighbourZ, x, y, z);
                            if (index >= 0)
                            {
                                return index;
                            }
                        }
                    }
                }

                int index_New = indexes_Next.Count;
                coordinates.Add(x);
                coordinates.Add(y);
                coordinates.Add(z);

                // The new vertex becomes the head of its cell's chain, pointing at whatever was there before.
                (long X, long Y, long Z) cell = (cellX, cellY, cellZ);
                indexes_Next.Add(index_ByCell.TryGetValue(cell, out int index_Head) ? index_Head : -1);
                index_ByCell[cell] = index_New;

                return index_New;
            }

            // ValueTuple keys are used deliberately: their seeded rotate-combine hash distributes the highly regular edge
            // index pairs of structured models far better than a packed 64-bit key, whose default hash (low ^ high)
            // degenerates into long collision chains.
            Dictionary<(int, int), int> counts_ByEdge = new(polyhedron.Count * 2);

            // One buffer grown on demand and reused by every ring of every face.
            int[] indexes_Ring = new int[16];

            for (int i = 0; i < polyhedron.Count; i++)
            {
                TPolygonalFace3D? polygonalFace3D = polyhedron.GetPolygonalFace3D<TPolygonalFace3D>(i);
                if (polygonalFace3D is null)
                {
                    return false;
                }

                Plane? plane = polygonalFace3D.Plane;
                if (plane is null)
                {
                    return false;
                }

                Point3D? point3D_Origin = plane.Origin;
                Spatial.Classes.Vector3D? vector3D_AxisX = plane.AxisX;
                Spatial.Classes.Vector3D? vector3D_AxisY = plane.AxisY;

                if (point3D_Origin is null || vector3D_AxisX is null || vector3D_AxisY is null)
                {
                    return false;
                }

                // Plane components are cached once per face in locals, keeping the projection below allocation free.
                double originX = point3D_Origin.X;
                double originY = point3D_Origin.Y;
                double originZ = point3D_Origin.Z;

                double axisXX = vector3D_AxisX.X;
                double axisXY = vector3D_AxisX.Y;
                double axisXZ = vector3D_AxisX.Z;

                double axisYX = vector3D_AxisY.X;
                double axisYY = vector3D_AxisY.Y;
                double axisYZ = vector3D_AxisY.Z;

                IPolygonalFace2D? polygonalFace2D = polygonalFace3D.Geometry2D;
                if (polygonalFace2D is null)
                {
                    return false;
                }

                List<IPolygonal2D>? polygonal2Ds = polygonalFace2D.Edges;
                if (polygonal2Ds is null || polygonal2Ds.Count == 0)
                {
                    return false;
                }

                for (int j = 0; j < polygonal2Ds.Count; j++)
                {
                    IPolygonal2D? polygonal2D = polygonal2Ds[j];
                    if (polygonal2D is null)
                    {
                        return false;
                    }

                    // Segmentable2D exposes a non-cloning GetPoints overload, avoiding one full copy of the ring.
                    // Rectangle2D implements IPolygonal2D without deriving from Segmentable2D, hence the fallback.
                    // The returned list is owned by the geometry and is only read here.
                    List<Point2D>? point2Ds = polygonal2D is Segmentable2D segmentable2D ? segmentable2D.GetPoints(false) : polygonal2D.GetPoints();
                    if (point2Ds is null || point2Ds.Count < 3)
                    {
                        return false;
                    }

                    if (indexes_Ring.Length < point2Ds.Count)
                    {
                        indexes_Ring = new int[point2Ds.Count];
                    }

                    for (int k = 0; k < point2Ds.Count; k++)
                    {
                        Point2D? point2D = point2Ds[k];
                        if (point2D is null)
                        {
                            return false;
                        }

                        double x = point2D.X;
                        double y = point2D.Y;

                        indexes_Ring[k] = Index(
                            originX + (axisYX * y) + (axisXX * x),
                            originY + (axisYY * y) + (axisXY * x),
                            originZ + (axisYZ * y) + (axisXZ * x));
                    }

                    for (int k = 0; k < point2Ds.Count; k++)
                    {
                        int index_Start = indexes_Ring[k];
                        int index_End = indexes_Ring[k == point2Ds.Count - 1 ? 0 : k + 1];

                        // Welding collapses a degenerate edge onto a single vertex.
                        if (index_Start == index_End)
                        {
                            continue;
                        }

                        (int, int) key = index_Start < index_End ? (index_Start, index_End) : (index_End, index_Start);

                        counts_ByEdge.TryGetValue(key, out int count);
                        if (manifold && count >= 2)
                        {
                            return false;
                        }

                        counts_ByEdge[key] = count + 1;
                    }
                }
            }

            if (counts_ByEdge.Count == 0)
            {
                return false;
            }

            foreach (KeyValuePair<(int, int), int> keyValuePair in counts_ByEdge)
            {
                if (manifold ? keyValuePair.Value != 2 : (keyValuePair.Value & 1) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Latest Proposal (§5): Single uniform weld-free edge compatibility predicate with component perfect matching.
        /// <para>1. Emits each face-ring edge as a half-edge; zero-length in-face edges are pruned at MicroDistance.</para>
        /// <para>2. Broad-phase midpoint spatial hash (27-cell neighborhood).</para>
        /// <para>3. Builds connected compatibility components and verifies that every component has a perfect matching.</para>
        /// <para>4. For manifold: true, verifies that parity is closed and every component has exactly 2 half-edges.</para>
        /// </summary>
        /// <typeparam name="TPolygonalFace3D">The type of the polygonal face.</typeparam>
        /// <param name="polyhedron">The polyhedron to evaluate.</param>
        /// <param name="manifold">When true, requires strict 2-manifold surface.</param>
        /// <param name="tolerance">The distance tolerance.</param>
        /// <returns>True if the polyhedron is closed.</returns>
        private static bool IsClosed_ComponentMatching<TPolygonalFace3D>(Polyhedron<TPolygonalFace3D>? polyhedron, bool manifold, double tolerance = DiGi.Core.Constants.Tolerance.Distance) where TPolygonalFace3D : IPolygonalFace3D
        {
            if (polyhedron is null || polyhedron.Count < 4)
            {
                return false;
            }

            double tolerance_Temp = tolerance > 0.0 ? tolerance : DiGi.Core.Constants.Tolerance.MicroDistance;
            double invTolerance = 1.0 / tolerance_Temp;
            double toleranceSquared = tolerance_Temp * tolerance_Temp;
            double microDistanceSquared = DiGi.Core.Constants.Tolerance.MicroDistance * DiGi.Core.Constants.Tolerance.MicroDistance;

            // Flat arrays for half-edge geometry: Start(X,Y,Z), End(X,Y,Z), Midpoint(X,Y,Z)
            List<double> coordinates = new(polyhedron.Count * 12);
            List<int> faceIndices = new(polyhedron.Count * 4);
            List<int> indexes_Next = new(polyhedron.Count * 4);
            Dictionary<(long X, long Y, long Z), int> index_ByCell = [];

            int edgeCount = 0;

            for (int i = 0; i < polyhedron.Count; i++)
            {
                TPolygonalFace3D? polygonalFace3D = polyhedron.GetPolygonalFace3D<TPolygonalFace3D>(i);
                if (polygonalFace3D is null)
                {
                    return false;
                }

                Plane? plane = polygonalFace3D.Plane;
                if (plane is null)
                {
                    return false;
                }

                Point3D? point3D_Origin = plane.Origin;
                Spatial.Classes.Vector3D? vector3D_AxisX = plane.AxisX;
                Spatial.Classes.Vector3D? vector3D_AxisY = plane.AxisY;

                if (point3D_Origin is null || vector3D_AxisX is null || vector3D_AxisY is null)
                {
                    return false;
                }

                double originX = point3D_Origin.X;
                double originY = point3D_Origin.Y;
                double originZ = point3D_Origin.Z;

                double axisXX = vector3D_AxisX.X;
                double axisXY = vector3D_AxisX.Y;
                double axisXZ = vector3D_AxisX.Z;

                double axisYX = vector3D_AxisY.X;
                double axisYY = vector3D_AxisY.Y;
                double axisYZ = vector3D_AxisY.Z;

                IPolygonalFace2D? polygonalFace2D = polygonalFace3D.Geometry2D;
                if (polygonalFace2D is null)
                {
                    return false;
                }

                List<IPolygonal2D>? polygonal2Ds = polygonalFace2D.Edges;
                if (polygonal2Ds is null || polygonal2Ds.Count == 0)
                {
                    return false;
                }

                for (int j = 0; j < polygonal2Ds.Count; j++)
                {
                    IPolygonal2D? polygonal2D = polygonal2Ds[j];
                    if (polygonal2D is null)
                    {
                        return false;
                    }

                    List<Point2D>? point2Ds = polygonal2D is Segmentable2D segmentable2D ? segmentable2D.GetPoints(false) : polygonal2D.GetPoints();
                    if (point2Ds is null || point2Ds.Count < 3)
                    {
                        return false;
                    }

                    int count_Points = point2Ds.Count;
                    double prevX = 0, prevY = 0, prevZ = 0;
                    bool hasPrev = false;
                    double firstX = 0, firstY = 0, firstZ = 0;

                    for (int k = 0; k < count_Points; k++)
                    {
                        Point2D? point2D = point2Ds[k];
                        if (point2D is null)
                        {
                            return false;
                        }

                        double px = point2D.X;
                        double py = point2D.Y;
                        double currX = originX + (axisYX * py) + (axisXX * px);
                        double currY = originY + (axisYY * py) + (axisXY * px);
                        double currZ = originZ + (axisYZ * py) + (axisXZ * px);

                        if (!hasPrev)
                        {
                            firstX = currX;
                            firstY = currY;
                            firstZ = currZ;
                            prevX = currX;
                            prevY = currY;
                            prevZ = currZ;
                            hasPrev = true;
                            continue;
                        }

                        double dx = currX - prevX;
                        double dy = currY - prevY;
                        double dz = currZ - prevZ;

                        if ((dx * dx) + (dy * dy) + (dz * dz) > microDistanceSquared)
                        {
                            AddEdge(prevX, prevY, prevZ, currX, currY, currZ, i);
                        }

                        prevX = currX;
                        prevY = currY;
                        prevZ = currZ;
                    }

                    // Closing edge of the ring
                    double dx_Close = firstX - prevX;
                    double dy_Close = firstY - prevY;
                    double dz_Close = firstZ - prevZ;

                    if ((dx_Close * dx_Close) + (dy_Close * dy_Close) + (dz_Close * dz_Close) > microDistanceSquared)
                    {
                        AddEdge(prevX, prevY, prevZ, firstX, firstY, firstZ, i);
                    }
                }
            }

            void AddEdge(double startX, double startY, double startZ, double endX, double endY, double endZ, int faceIndex)
            {
                int edgeIndex = edgeCount++;
                double midX = (startX + endX) * 0.5;
                double midY = (startY + endY) * 0.5;
                double midZ = (startZ + endZ) * 0.5;

                coordinates.Add(startX);
                coordinates.Add(startY);
                coordinates.Add(startZ);
                coordinates.Add(endX);
                coordinates.Add(endY);
                coordinates.Add(endZ);
                coordinates.Add(midX);
                coordinates.Add(midY);
                coordinates.Add(midZ);

                faceIndices.Add(faceIndex);

                long cellX = (long)System.Math.Floor(midX * invTolerance);
                long cellY = (long)System.Math.Floor(midY * invTolerance);
                long cellZ = (long)System.Math.Floor(midZ * invTolerance);
                (long X, long Y, long Z) cell = (cellX, cellY, cellZ);

                indexes_Next.Add(index_ByCell.TryGetValue(cell, out int index_Head) ? index_Head : -1);
                index_ByCell[cell] = edgeIndex;
            }

            if (edgeCount < 6 || (edgeCount & 1) != 0)
            {
                return false;
            }

            // Build compatibility adjacency lists
            List<int>[] adj = new List<int>[edgeCount];
            for (int i = 0; i < edgeCount; i++)
            {
                adj[i] = [];
            }

            for (int i = 0; i < edgeCount; i++)
            {
                int offset_I = i * 9;
                double startX_I = coordinates[offset_I];
                double startY_I = coordinates[offset_I + 1];
                double startZ_I = coordinates[offset_I + 2];
                double endX_I = coordinates[offset_I + 3];
                double endY_I = coordinates[offset_I + 4];
                double endZ_I = coordinates[offset_I + 5];
                double midX_I = coordinates[offset_I + 6];
                double midY_I = coordinates[offset_I + 7];
                double midZ_I = coordinates[offset_I + 8];
                int face_I = faceIndices[i];

                long cellX = (long)System.Math.Floor(midX_I * invTolerance);
                long cellY = (long)System.Math.Floor(midY_I * invTolerance);
                long cellZ = (long)System.Math.Floor(midZ_I * invTolerance);

                for (long nx = cellX - 1; nx <= cellX + 1; nx++)
                {
                    for (long ny = cellY - 1; ny <= cellY + 1; ny++)
                    {
                        for (long nz = cellZ - 1; nz <= cellZ + 1; nz++)
                        {
                            if (!index_ByCell.TryGetValue((nx, ny, nz), out int candHead))
                            {
                                continue;
                            }

                            while (candHead >= 0)
                            {
                                if (candHead > i)
                                {
                                    int face_J = faceIndices[candHead];
                                    if (face_J != face_I)
                                    {
                                        int offset_J = candHead * 9;
                                        double startX_J = coordinates[offset_J];
                                        double startY_J = coordinates[offset_J + 1];
                                        double startZ_J = coordinates[offset_J + 2];
                                        double endX_J = coordinates[offset_J + 3];
                                        double endY_J = coordinates[offset_J + 4];
                                        double endZ_J = coordinates[offset_J + 5];

                                        // Opposite orientation (standard manifold)
                                        double dx1 = startX_I - endX_J;
                                        double dy1 = startY_I - endY_J;
                                        double dz1 = startZ_I - endZ_J;
                                        double d1 = (dx1 * dx1) + (dy1 * dy1) + (dz1 * dz1);

                                        double dx2 = endX_I - startX_J;
                                        double dy2 = endY_I - startY_J;
                                        double dz2 = endZ_I - startZ_J;
                                        double d2 = (dx2 * dx2) + (dy2 * dy2) + (dz2 * dz2);

                                        double distOppSq = d1 > d2 ? d1 : d2;

                                        // Same orientation (inverted face)
                                        double dx3 = startX_I - startX_J;
                                        double dy3 = startY_I - startY_J;
                                        double dz3 = startZ_I - startZ_J;
                                        double d3 = (dx3 * dx3) + (dy3 * dy3) + (dz3 * dz3);

                                        double dx4 = endX_I - endX_J;
                                        double dy4 = endY_I - endY_J;
                                        double dz4 = endZ_I - endZ_J;
                                        double d4 = (dx4 * dx4) + (dy4 * dy4) + (dz4 * dz4);

                                        double distSameSq = d3 > d4 ? d3 : d4;
                                        double distSq = distOppSq < distSameSq ? distOppSq : distSameSq;

                                        if (distSq <= toleranceSquared)
                                        {
                                            adj[i].Add(candHead);
                                            adj[candHead].Add(i);
                                        }
                                    }
                                }

                                candHead = indexes_Next[candHead];
                            }
                        }
                    }
                }
            }

            // Component decomposition & Perfect Matching verification
            bool[] visited = new bool[edgeCount];
            List<int> component = [];

            for (int i = 0; i < edgeCount; i++)
            {
                if (visited[i])
                {
                    continue;
                }

                component.Clear();
                Queue<int> queue = new();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    int curr = queue.Dequeue();
                    component.Add(curr);

                    List<int> neighbors = adj[curr];
                    for (int n = 0; n < neighbors.Count; n++)
                    {
                        int next = neighbors[n];
                        if (!visited[next])
                        {
                            visited[next] = true;
                            queue.Enqueue(next);
                        }
                    }
                }

                int compSize = component.Count;

                // Any isolated edge -> open
                if (compSize < 2 || (compSize & 1) != 0)
                {
                    return false;
                }

                // In manifold mode: every component must be exactly 2 half-edges
                if (manifold && compSize != 2)
                {
                    return false;
                }

                // Fast check for size 2
                if (compSize == 2)
                {
                    if (adj[component[0]].Count == 0)
                    {
                        return false;
                    }
                    continue;
                }

                // Verify perfect matching for component of size 4, 6, 8, ...
                if (!HasPerfectMatching(component, adj))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether the given connected component has a perfect matching in general (non-bipartite) graph.
        /// </summary>
        private static bool HasPerfectMatching(List<int> component, List<int>[] adj)
        {
            int n = component.Count;
            if ((n & 1) != 0 || n == 0)
            {
                return false;
            }

            if (n == 2)
            {
                int u = component[0];
                int v = component[1];
                return adj[u].Contains(v);
            }

            if (n > 32)
            {
                // Fallback for extreme cases: size check
                return false;
            }

            int[] neighborMask = new int[n];
            Dictionary<int, int> localMap = [];
            for (int i = 0; i < n; i++)
            {
                localMap[component[i]] = i;
            }

            for (int i = 0; i < n; i++)
            {
                int mask = 0;
                List<int> neighbors = adj[component[i]];
                for (int j = 0; j < neighbors.Count; j++)
                {
                    if (localMap.TryGetValue(neighbors[j], out int localNeighbor))
                    {
                        mask |= (1 << localNeighbor);
                    }
                }
                neighborMask[i] = mask;
            }

            return CanMatchAll(0, 0, n, neighborMask);
        }

        private static bool CanMatchAll(int matchedMask, int current, int n, int[] neighborMask)
        {
            if (matchedMask == (1 << n) - 1)
            {
                return true;
            }

            while (current < n && (matchedMask & (1 << current)) != 0)
            {
                current++;
            }

            if (current >= n)
            {
                return true;
            }

            int candidateMask = neighborMask[current] & ~matchedMask;
            while (candidateMask != 0)
            {
                int neighbor = System.Numerics.BitOperations.TrailingZeroCount(candidateMask);
                candidateMask &= ~(1 << neighbor);

                int nextMatchedMask = matchedMask | (1 << current) | (1 << neighbor);
                if (CanMatchAll(nextMatchedMask, current + 1, n, neighborMask))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Method 2: Previous Greedy Two-Pass Edge Matching.
        /// </summary>
        private static bool IsClosed_Greedy<TPolygonalFace3D>(Polyhedron<TPolygonalFace3D>? polyhedron, bool manifold, double tolerance = DiGi.Core.Constants.Tolerance.Distance) where TPolygonalFace3D : IPolygonalFace3D
        {
            if (polyhedron is null || polyhedron.Count < 4)
            {
                return false;
            }

            double tolerance_Temp = tolerance > 0.0 ? tolerance : DiGi.Core.Constants.Tolerance.MicroDistance;
            double invTolerance = 1.0 / tolerance_Temp;
            double toleranceSquared = tolerance_Temp * tolerance_Temp;
            double microDistanceSquared = DiGi.Core.Constants.Tolerance.MicroDistance * DiGi.Core.Constants.Tolerance.MicroDistance;

            List<double> coordinates = new(polyhedron.Count * 12);
            List<int> faceIndices = new(polyhedron.Count * 4);
            List<int> indexes_Next = new(polyhedron.Count * 4);
            Dictionary<(long X, long Y, long Z), int> index_ByCell = [];

            int edgeCount = 0;

            for (int i = 0; i < polyhedron.Count; i++)
            {
                TPolygonalFace3D? polygonalFace3D = polyhedron.GetPolygonalFace3D<TPolygonalFace3D>(i);
                if (polygonalFace3D is null)
                {
                    return false;
                }

                Plane? plane = polygonalFace3D.Plane;
                if (plane is null)
                {
                    return false;
                }

                Point3D? point3D_Origin = plane.Origin;
                Spatial.Classes.Vector3D? vector3D_AxisX = plane.AxisX;
                Spatial.Classes.Vector3D? vector3D_AxisY = plane.AxisY;

                if (point3D_Origin is null || vector3D_AxisX is null || vector3D_AxisY is null)
                {
                    return false;
                }

                double originX = point3D_Origin.X;
                double originY = point3D_Origin.Y;
                double originZ = point3D_Origin.Z;

                double axisXX = vector3D_AxisX.X;
                double axisXY = vector3D_AxisX.Y;
                double axisXZ = vector3D_AxisX.Z;

                double axisYX = vector3D_AxisY.X;
                double axisYY = vector3D_AxisY.Y;
                double axisYZ = vector3D_AxisY.Z;

                IPolygonalFace2D? polygonalFace2D = polygonalFace3D.Geometry2D;
                if (polygonalFace2D is null)
                {
                    return false;
                }

                List<IPolygonal2D>? polygonal2Ds = polygonalFace2D.Edges;
                if (polygonal2Ds is null || polygonal2Ds.Count == 0)
                {
                    return false;
                }

                for (int j = 0; j < polygonal2Ds.Count; j++)
                {
                    IPolygonal2D? polygonal2D = polygonal2Ds[j];
                    if (polygonal2D is null)
                    {
                        return false;
                    }

                    List<Point2D>? point2Ds = polygonal2D is Segmentable2D segmentable2D ? segmentable2D.GetPoints(false) : polygonal2D.GetPoints();
                    if (point2Ds is null || point2Ds.Count < 3)
                    {
                        return false;
                    }

                    int count_Points = point2Ds.Count;
                    double prevX = 0, prevY = 0, prevZ = 0;
                    bool hasPrev = false;
                    double firstX = 0, firstY = 0, firstZ = 0;

                    for (int k = 0; k < count_Points; k++)
                    {
                        Point2D? point2D = point2Ds[k];
                        if (point2D is null)
                        {
                            return false;
                        }

                        double px = point2D.X;
                        double py = point2D.Y;
                        double currX = originX + (axisYX * py) + (axisXX * px);
                        double currY = originY + (axisYY * py) + (axisXY * px);
                        double currZ = originZ + (axisYZ * py) + (axisXZ * px);

                        if (!hasPrev)
                        {
                            firstX = currX;
                            firstY = currY;
                            firstZ = currZ;
                            prevX = currX;
                            prevY = currY;
                            prevZ = currZ;
                            hasPrev = true;
                            continue;
                        }

                        double dx = currX - prevX;
                        double dy = currY - prevY;
                        double dz = currZ - prevZ;

                        if ((dx * dx) + (dy * dy) + (dz * dz) > microDistanceSquared)
                        {
                            AddEdge(prevX, prevY, prevZ, currX, currY, currZ, i);
                        }

                        prevX = currX;
                        prevY = currY;
                        prevZ = currZ;
                    }

                    // Closing edge of the ring
                    double dx_Close = firstX - prevX;
                    double dy_Close = firstY - prevY;
                    double dz_Close = firstZ - prevZ;

                    if ((dx_Close * dx_Close) + (dy_Close * dy_Close) + (dz_Close * dz_Close) > microDistanceSquared)
                    {
                        AddEdge(prevX, prevY, prevZ, firstX, firstY, firstZ, i);
                    }
                }
            }

            void AddEdge(double startX, double startY, double startZ, double endX, double endY, double endZ, int faceIndex)
            {
                int edgeIndex = edgeCount++;
                double midX = (startX + endX) * 0.5;
                double midY = (startY + endY) * 0.5;
                double midZ = (startZ + endZ) * 0.5;

                coordinates.Add(startX);
                coordinates.Add(startY);
                coordinates.Add(startZ);
                coordinates.Add(endX);
                coordinates.Add(endY);
                coordinates.Add(endZ);
                coordinates.Add(midX);
                coordinates.Add(midY);
                coordinates.Add(midZ);

                faceIndices.Add(faceIndex);

                long cellX = (long)System.Math.Floor(midX * invTolerance);
                long cellY = (long)System.Math.Floor(midY * invTolerance);
                long cellZ = (long)System.Math.Floor(midZ * invTolerance);
                (long X, long Y, long Z) cell = (cellX, cellY, cellZ);

                indexes_Next.Add(index_ByCell.TryGetValue(cell, out int index_Head) ? index_Head : -1);
                index_ByCell[cell] = edgeIndex;
            }

            if (edgeCount < 6 || (edgeCount & 1) != 0)
            {
                return false;
            }

            int[] mate = new int[edgeCount];
            for (int i = 0; i < edgeCount; i++)
            {
                mate[i] = -1;
            }

            int matchedCount = 0;

            // Pass 1: exact matches (distSq <= 1e-12)
            for (int i = 0; i < edgeCount; i++)
            {
                if (mate[i] >= 0)
                {
                    continue;
                }

                int offset_I = i * 9;
                double startX_I = coordinates[offset_I];
                double startY_I = coordinates[offset_I + 1];
                double startZ_I = coordinates[offset_I + 2];
                double endX_I = coordinates[offset_I + 3];
                double endY_I = coordinates[offset_I + 4];
                double endZ_I = coordinates[offset_I + 5];
                double midX_I = coordinates[offset_I + 6];
                double midY_I = coordinates[offset_I + 7];
                double midZ_I = coordinates[offset_I + 8];
                int face_I = faceIndices[i];

                long cellX = (long)System.Math.Floor(midX_I * invTolerance);
                long cellY = (long)System.Math.Floor(midY_I * invTolerance);
                long cellZ = (long)System.Math.Floor(midZ_I * invTolerance);

                int bestMatch = -1;
                double bestDistSq = double.MaxValue;

                for (long nx = cellX - 1; nx <= cellX + 1; nx++)
                {
                    for (long ny = cellY - 1; ny <= cellY + 1; ny++)
                    {
                        for (long nz = cellZ - 1; nz <= cellZ + 1; nz++)
                        {
                            if (!index_ByCell.TryGetValue((nx, ny, nz), out int candHead))
                            {
                                continue;
                            }

                            while (candHead >= 0)
                            {
                                if (candHead > i && mate[candHead] < 0)
                                {
                                    int face_J = faceIndices[candHead];
                                    if (face_J != face_I)
                                    {
                                        int offset_J = candHead * 9;
                                        double startX_J = coordinates[offset_J];
                                        double startY_J = coordinates[offset_J + 1];
                                        double startZ_J = coordinates[offset_J + 2];
                                        double endX_J = coordinates[offset_J + 3];
                                        double endY_J = coordinates[offset_J + 4];
                                        double endZ_J = coordinates[offset_J + 5];

                                        double dx1 = startX_I - endX_J;
                                        double dy1 = startY_I - endY_J;
                                        double dz1 = startZ_I - endZ_J;
                                        double d1 = (dx1 * dx1) + (dy1 * dy1) + (dz1 * dz1);

                                        double dx2 = endX_I - startX_J;
                                        double dy2 = endY_I - startY_J;
                                        double dz2 = endZ_I - startZ_J;
                                        double d2 = (dx2 * dx2) + (dy2 * dy2) + (dz2 * dz2);

                                        double distOppSq = d1 > d2 ? d1 : d2;

                                        double dx3 = startX_I - startX_J;
                                        double dy3 = startY_I - startY_J;
                                        double dz3 = startZ_I - startZ_J;
                                        double d3 = (dx3 * dx3) + (dy3 * dy3) + (dz3 * dz3);

                                        double dx4 = endX_I - endX_J;
                                        double dy4 = endY_I - endY_J;
                                        double dz4 = endZ_I - endZ_J;
                                        double d4 = (dx4 * dx4) + (dy4 * dy4) + (dz4 * dz4);

                                        double distSameSq = d3 > d4 ? d3 : d4;
                                        double distSq = distOppSq < distSameSq ? distOppSq : distSameSq;

                                        if (distSq <= 1e-12 && distSq < bestDistSq)
                                        {
                                            bestDistSq = distSq;
                                            bestMatch = candHead;
                                        }
                                    }
                                }

                                candHead = indexes_Next[candHead];
                            }
                        }
                    }
                }

                if (bestMatch >= 0)
                {
                    mate[i] = bestMatch;
                    mate[bestMatch] = i;
                    matchedCount += 2;
                }
            }

            // Pass 2: tolerance matches (distSq <= toleranceSquared)
            if (matchedCount < edgeCount)
            {
                for (int i = 0; i < edgeCount; i++)
                {
                    if (mate[i] >= 0)
                    {
                        continue;
                    }

                    int offset_I = i * 9;
                    double startX_I = coordinates[offset_I];
                    double startY_I = coordinates[offset_I + 1];
                    double startZ_I = coordinates[offset_I + 2];
                    double endX_I = coordinates[offset_I + 3];
                    double endY_I = coordinates[offset_I + 4];
                    double endZ_I = coordinates[offset_I + 5];
                    double midX_I = coordinates[offset_I + 6];
                    double midY_I = coordinates[offset_I + 7];
                    double midZ_I = coordinates[offset_I + 8];
                    int face_I = faceIndices[i];

                    long cellX = (long)System.Math.Floor(midX_I * invTolerance);
                    long cellY = (long)System.Math.Floor(midY_I * invTolerance);
                    long cellZ = (long)System.Math.Floor(midZ_I * invTolerance);

                    int bestMatch = -1;
                    double bestDistSq = double.MaxValue;

                    for (long nx = cellX - 1; nx <= cellX + 1; nx++)
                    {
                        for (long ny = cellY - 1; ny <= cellY + 1; ny++)
                        {
                            for (long nz = cellZ - 1; nz <= cellZ + 1; nz++)
                            {
                                if (!index_ByCell.TryGetValue((nx, ny, nz), out int candHead))
                                {
                                    continue;
                                }

                                while (candHead >= 0)
                                {
                                    if (candHead > i && mate[candHead] < 0)
                                    {
                                        int face_J = faceIndices[candHead];
                                        if (face_J != face_I)
                                        {
                                            int offset_J = candHead * 9;
                                            double startX_J = coordinates[offset_J];
                                            double startY_J = coordinates[offset_J + 1];
                                            double startZ_J = coordinates[offset_J + 2];
                                            double endX_J = coordinates[offset_J + 3];
                                            double endY_J = coordinates[offset_J + 4];
                                            double endZ_J = coordinates[offset_J + 5];

                                            double dx1 = startX_I - endX_J;
                                            double dy1 = startY_I - endY_J;
                                            double dz1 = startZ_I - endZ_J;
                                            double d1 = (dx1 * dx1) + (dy1 * dy1) + (dz1 * dz1);

                                            double dx2 = endX_I - startX_J;
                                            double dy2 = endY_I - startY_J;
                                            double dz2 = endZ_I - startZ_J;
                                            double d2 = (dx2 * dx2) + (dy2 * dy2) + (dz2 * dz2);

                                            double distOppSq = d1 > d2 ? d1 : d2;

                                            double dx3 = startX_I - startX_J;
                                            double dy3 = startY_I - startY_J;
                                            double dz3 = startZ_I - startZ_J;
                                            double d3 = (dx3 * dx3) + (dy3 * dy3) + (dz3 * dz3);

                                            double dx4 = endX_I - endX_J;
                                            double dy4 = endY_I - endY_J;
                                            double dz4 = endZ_I - endZ_J;
                                            double d4 = (dx4 * dx4) + (dy4 * dy4) + (dz4 * dz4);

                                            double distSameSq = d3 > d4 ? d3 : d4;
                                            double distSq = distOppSq < distSameSq ? distOppSq : distSameSq;

                                            if (distSq <= toleranceSquared && distSq < bestDistSq)
                                            {
                                                bestDistSq = distSq;
                                                bestMatch = candHead;
                                            }
                                        }
                                    }

                                    candHead = indexes_Next[candHead];
                                }
                            }
                        }
                    }

                    if (bestMatch >= 0)
                    {
                        mate[i] = bestMatch;
                        mate[bestMatch] = i;
                        matchedCount += 2;
                    }
                }
            }

            if (matchedCount != edgeCount)
            {
                return false;
            }

            if (manifold)
            {
                for (int i = 0; i < edgeCount; i++)
                {
                    int mate_I = mate[i];
                    if (mate_I < i)
                    {
                        continue;
                    }

                    int offset_I = i * 9;
                    double startX_I = coordinates[offset_I];
                    double startY_I = coordinates[offset_I + 1];
                    double startZ_I = coordinates[offset_I + 2];
                    double endX_I = coordinates[offset_I + 3];
                    double endY_I = coordinates[offset_I + 4];
                    double endZ_I = coordinates[offset_I + 5];
                    double midX_I = coordinates[offset_I + 6];
                    double midY_I = coordinates[offset_I + 7];
                    double midZ_I = coordinates[offset_I + 8];
                    int face_I1 = faceIndices[i];
                    int face_I2 = faceIndices[mate_I];

                    long cellX = (long)System.Math.Floor(midX_I * invTolerance);
                    long cellY = (long)System.Math.Floor(midY_I * invTolerance);
                    long cellZ = (long)System.Math.Floor(midZ_I * invTolerance);

                    for (long nx = cellX - 1; nx <= cellX + 1; nx++)
                    {
                        for (long ny = cellY - 1; ny <= cellY + 1; ny++)
                        {
                            for (long nz = cellZ - 1; nz <= cellZ + 1; nz++)
                            {
                                if (!index_ByCell.TryGetValue((nx, ny, nz), out int candHead))
                                {
                                    continue;
                                }

                                while (candHead >= 0)
                                {
                                    if (candHead != i && candHead != mate_I)
                                    {
                                        int face_J = faceIndices[candHead];
                                        if (face_J != face_I1 && face_J != face_I2)
                                        {
                                            int offset_J = candHead * 9;
                                            double startX_J = coordinates[offset_J];
                                            double startY_J = coordinates[offset_J + 1];
                                            double startZ_J = coordinates[offset_J + 2];
                                            double endX_J = coordinates[offset_J + 3];
                                            double endY_J = coordinates[offset_J + 4];
                                            double endZ_J = coordinates[offset_J + 5];

                                            double dx1 = startX_I - endX_J;
                                            double dy1 = startY_I - endY_J;
                                            double dz1 = startZ_I - endZ_J;
                                            double d1 = (dx1 * dx1) + (dy1 * dy1) + (dz1 * dz1);

                                            double dx2 = endX_I - startX_J;
                                            double dy2 = endY_I - startY_J;
                                            double dz2 = endZ_I - startZ_J;
                                            double d2 = (dx2 * dx2) + (dy2 * dy2) + (dz2 * dz2);

                                            double distOppSq = d1 > d2 ? d1 : d2;

                                            double dx3 = startX_I - startX_J;
                                            double dy3 = startY_I - startY_J;
                                            double dz3 = startZ_I - startZ_J;
                                            double d3 = (dx3 * dx3) + (dy3 * dy3) + (dz3 * dz3);

                                            double dx4 = endX_I - endX_J;
                                            double dy4 = endY_I - endY_J;
                                            double dz4 = endZ_I - endZ_J;
                                            double d4 = (dx4 * dx4) + (dy4 * dy4) + (dz4 * dz4);

                                            double distSameSq = d3 > d4 ? d3 : d4;
                                            double distSq = distOppSq < distSameSq ? distOppSq : distSameSq;

                                            if (distSq <= 1e-12)
                                            {
                                                return false;
                                            }
                                        }
                                    }

                                    candHead = indexes_Next[candHead];
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Tests the partner-stealing counterexample from Jakub's review (F1).
        /// <para>Verifies that Component-Level Perfect Matching correctly identifies the matching {z-p, q-r} in graph z-p (0.05), p-q (0.02), q-r (0.03) at tolerance 0.05, whereas greedy pairing failed due to order-dependent partner stealing.</para>
        /// </summary>
        [Fact]
        public void Polyhedron_IsClosed_Comparison_PartnerStealing()
        {
            // Synthesize 4 faces whose half-edges form the z-p-q-r chain at z=0..3:
            // z at x=0.00, p at x=0.05 (dist 0.05)
            // q at x=0.07 (dist p-q = 0.02)
            // r at x=0.10 (dist q-r = 0.03)
            // At tolerance = 0.05:
            // Perfect matching {z-p, q-r} exists.
            List<int> comp = [0, 1, 2, 3];
            List<int>[] adj = new List<int>[4];
            adj[0] = [1];       // z connects to p
            adj[1] = [0, 2];    // p connects to z and q
            adj[2] = [1, 3];    // q connects to p and r
            adj[3] = [2];       // r connects to q

            bool hasPerfectMatching = HasPerfectMatching(comp, adj);
            Assert.True(hasPerfectMatching, "Component-level matching must find {z-p, q-r}");
        }

        /// <summary>
        /// Tests the star-component counterexample from Jakub's review (F1 note).
        /// <para>Verifies that an even-cardinality component with no perfect matching (star graph of 1 center and 3 leaves) correctly returns false.</para>
        /// </summary>
        [Fact]
        public void Polyhedron_IsClosed_Comparison_StarComponent()
        {
            // Center 0 connected to leaves 1, 2, 3 (size 4, even)
            List<int> comp = [0, 1, 2, 3];
            List<int>[] adj = new List<int>[4];
            adj[0] = [1, 2, 3];
            adj[1] = [0];
            adj[2] = [0];
            adj[3] = [0];

            bool hasPerfectMatching = HasPerfectMatching(comp, adj);
            Assert.False(hasPerfectMatching, "Star component has no perfect matching and must return false");
        }

        /// <summary>
        /// Tests Jakub's exact slot solid (1.1) demonstrating that manifold mode is scale-relative.
        /// <para>Rectangle 2x1 with slot 0.1 deep, 0.2 wide extruded to z=3 (10 faces). Parity is true across 0.01 to 0.50. Manifold flips at 0.10.</para>
        /// </summary>
        [Fact]
        public void Polyhedron_IsClosed_Comparison_SlotSolid()
        {
            Plane plane = Create.Plane(0)!;
            PolygonalFace3D? face_Slot = Create.PolygonalFace3D(
                plane,
                new Point2D(0, 0),
                new Point2D(2, 0),
                new Point2D(2, 0.4),
                new Point2D(1.9, 0.4), // 0.1 m deep slot
                new Point2D(1.9, 0.6),
                new Point2D(2, 0.6),
                new Point2D(2, 1),
                new Point2D(0, 1));
            Assert.NotNull(face_Slot);

            Polyhedron? poly_Slot = Create.Polyhedron(face_Slot, new Spatial.Classes.Vector3D(0, 0, 3));
            Assert.NotNull(poly_Slot);

            // Parity form is strictly monotonic across all tolerances:
            double[] tolerances = [0.01, 0.05, 0.09, 0.10, 0.15, 0.20, 0.50];
            for (int i = 0; i < tolerances.Length; i++)
            {
                Assert.True(IsClosed_ComponentMatching(poly_Slot, false, tolerances[i]), $"Parity failed at {tolerances[i]}");
            }

            // Manifold form is scale-relative: true at <= 0.09, false at >= 0.11 (4 vertical edges merge into 1 component)
            Assert.True(IsClosed_ComponentMatching(poly_Slot, true, 0.05));
            Assert.True(IsClosed_ComponentMatching(poly_Slot, true, 0.09));
            Assert.False(IsClosed_ComponentMatching(poly_Slot, true, 0.11));
            Assert.False(IsClosed_ComponentMatching(poly_Slot, true, 0.20));
        }


        /// <summary>
        /// Pins the defect reported on DiGi.Geometry issue 1 against the implementation that carried it, and its absence from the shipped one.
        /// <para>The vertex-welding algorithm reports a watertight 9 800-face ellipsoid as open from a tolerance of 0.01 upwards, having reported it closed at every finer value: welding at a tolerance past the edge length of the tessellation collapses whole triangles, and the edge counts stop pairing. The same happens to a solid with a 0.1 m slot once the tolerance passes 0.15.</para>
        /// <para>Neither is a borderline case - both solids are exactly watertight and closed at 1E-06 - which is what makes the old result wrong rather than merely different. The shipped predicate welds nothing and reports both closed at every tolerance on the ladder.</para>
        /// <para>Medium test (18.1 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void Polyhedron_IsClosed_Comparison_Monotonicity()
        {
            Ellipsoid ellipsoid = new(new Point3D(1, 2, 3), 3, 2, 1);
            Polyhedron? polyhedron_Ellipsoid = Create.Polyhedron(ellipsoid, 50, 100);
            Assert.NotNull(polyhedron_Ellipsoid);

            // Both agree the solid is watertight when nothing is welded away.
            Assert.True(IsClosed_VertexWeld(polyhedron_Ellipsoid, false, 0.001));
            Assert.True(polyhedron_Ellipsoid.IsClosed(0.001));

            // The previous implementation loses it as the tolerance is broadened; the shipped one does not.
            Assert.False(IsClosed_VertexWeld(polyhedron_Ellipsoid, false, 0.01));
            Assert.False(IsClosed_VertexWeld(polyhedron_Ellipsoid, false, 0.5));

            Assert.True(polyhedron_Ellipsoid.IsClosed(0.01));
            Assert.True(polyhedron_Ellipsoid.IsClosed(0.5));

            Polyhedron? polyhedron_Slot = Polyhedron_IsClosed_SlotExtrusion();
            Assert.NotNull(polyhedron_Slot);

            Assert.True(IsClosed_VertexWeld(polyhedron_Slot, false, 0.15));
            Assert.False(IsClosed_VertexWeld(polyhedron_Slot, false, 0.2));

            Assert.True(polyhedron_Slot.IsClosed(0.15));
            Assert.True(polyhedron_Slot.IsClosed(0.2));
        }

        /// <summary>
        /// Total half-edge evaluations each benchmark scenario aims for. The repeat count of every row is scaled from it, so all timings are reported per call.
        /// </summary>
        private const int Polyhedron_IsClosed_Benchmark_Operations = 600000;

        /// <summary>
        /// The number of timed batches each measurement takes, of which the fastest is reported.
        /// </summary>
        private const int Polyhedron_IsClosed_Benchmark_Batches = 5;

        /// <summary>
        /// The smallest number of warm-up calls made before a measurement, enough to carry every implementation past the call count at which the runtime promotes it to optimised code. Measuring without it moved the same figure by a factor of three between runs.
        /// </summary>
        private const int Polyhedron_IsClosed_Benchmark_WarmUp = 60;

        /// <summary>
        /// Benchmarks the shipped closure query against the implementation it replaced and the two designs proposed on DiGi.Geometry issue 1.
        /// <para>Four implementations on identical inputs: the vertex welding that shipped previously, the greedy two-pass edge matching of the first proposal, the component perfect matching of the second, and the shipped weld-free predicate.</para>
        /// <para>Each implementation is warmed up before it is timed and the repeat count is scaled by the size of the input. Results are asserted to agree before any timing is taken on the solids that carry no feature at the scale of the tolerance; on the ones that do, the divergence is the point of the exercise and is asserted explicitly instead.</para>
        /// <para>Writes IsClosed_Benchmark.md to the reports directory. Run in Release for figures worth quoting.</para>
        /// <para>Long test (> 30 s): runs when DIGI_TEST_MAX_DURATION is Long.</para>
        /// </summary>
        [LongFact]
        public void Polyhedron_IsClosed_Benchmark()
        {
            System.Text.StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("# Benchmark - Polyhedron closure");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Per call, microseconds. Lower is better.");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Scenario | Half-edges | Vertex weld (previous) | Greedy edge matching | Component matching | Shipped | Shipped / previous |");
            stringBuilder.AppendLine("| :--- | ---: | ---: | ---: | ---: | ---: | ---: |");

            double Best(Func<bool> function, int repeats)
            {
                int repeats_WarmUp = System.Math.Max(repeats, Polyhedron_IsClosed_Benchmark_WarmUp);
                for (int i = 0; i < repeats_WarmUp; i++)
                {
                    _ = function();
                }

                double microseconds_Best = double.MaxValue;

                for (int batch = 0; batch < Polyhedron_IsClosed_Benchmark_Batches; batch++)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    for (int i = 0; i < repeats; i++)
                    {
                        _ = function();
                    }
                    stopwatch.Stop();

                    double microseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0 / repeats;
                    if (microseconds < microseconds_Best)
                    {
                        microseconds_Best = microseconds;
                    }
                }

                return microseconds_Best;
            }

            void Measure(string name, Polyhedron polyhedron, int count_HalfEdges, double tolerance)
            {
                int repeats = System.Math.Max(1, Polyhedron_IsClosed_Benchmark_Operations / count_HalfEdges);

                double microseconds_VertexWeld = Best(() => IsClosed_VertexWeld(polyhedron, false, tolerance), repeats);
                double microseconds_Greedy = Best(() => IsClosed_Greedy(polyhedron, false, tolerance), repeats);
                double microseconds_Component = Best(() => IsClosed_ComponentMatching(polyhedron, false, tolerance), repeats);
                double microseconds_Shipped = Best(() => polyhedron.IsClosed(tolerance), repeats);

                stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"| {name} | {count_HalfEdges} | {microseconds_VertexWeld:F1} | {microseconds_Greedy:F1} | {microseconds_Component:F1} | {microseconds_Shipped:F1} | {microseconds_Shipped / microseconds_VertexWeld:F2} |");
            }

            // 1. Extrusion of a 500-gon - a clean solid where every half-edge has an exact partner.
            Polyhedron? polyhedron_Extrusion = Polyhedron_IsClosed_Extrusion(500);
            Assert.NotNull(polyhedron_Extrusion);

            Assert.True(IsClosed_VertexWeld(polyhedron_Extrusion, false));
            Assert.True(IsClosed_Greedy(polyhedron_Extrusion, false));
            Assert.True(IsClosed_ComponentMatching(polyhedron_Extrusion, false));
            Assert.True(polyhedron_Extrusion.IsClosed());

            Measure("500-gon extrusion", polyhedron_Extrusion, 3000, DiGi.Core.Constants.Tolerance.Distance);

            // 2. A finely tessellated ellipsoid - the largest input, 9 800 triangular faces.
            Ellipsoid ellipsoid = new(new Point3D(1, 2, 3), 3, 2, 1);
            Polyhedron? polyhedron_Ellipsoid = Create.Polyhedron(ellipsoid, 50, 100);
            Assert.NotNull(polyhedron_Ellipsoid);

            Assert.True(IsClosed_VertexWeld(polyhedron_Ellipsoid, false));
            Assert.True(IsClosed_Greedy(polyhedron_Ellipsoid, false));
            Assert.True(IsClosed_ComponentMatching(polyhedron_Ellipsoid, false));
            Assert.True(polyhedron_Ellipsoid.IsClosed());

            Measure("9 800-face ellipsoid", polyhedron_Ellipsoid, 29400, DiGi.Core.Constants.Tolerance.Distance);

            // 3. A cube missing one face - the cost of rejecting an open solid.
            List<IPolygonalFace3D> polygonalFace3Ds_Open = Polyhedron_IsClosed_BoxFaces(0, 10, 0);
            polygonalFace3Ds_Open.RemoveAt(0);

            Polyhedron? polyhedron_Open = Create.Polyhedron(polygonalFace3Ds_Open);
            Assert.NotNull(polyhedron_Open);

            Assert.False(IsClosed_VertexWeld(polyhedron_Open, false));
            Assert.False(IsClosed_Greedy(polyhedron_Open, false));
            Assert.False(IsClosed_ComponentMatching(polyhedron_Open, false));
            Assert.False(polyhedron_Open.IsClosed());

            Measure("Open cube, 5 faces", polyhedron_Open, 20, DiGi.Core.Constants.Tolerance.Distance);

            // 4. The offline reproduction from the report - a 0.03 m step judged at a tolerance of 0.05.
            Polyhedron? polyhedron_Step = Polyhedron_IsClosed_StepExtrusion();
            Assert.NotNull(polyhedron_Step);

            Assert.True(polyhedron_Step.IsClosed(0.05));

            Measure("0.03 m step, tolerance 0.05", polyhedron_Step, 36, 0.05);

            // 5. A 0.1 m deep slot judged at a tolerance of 0.15, above the feature size.
            Polyhedron? polyhedron_Slot = Polyhedron_IsClosed_SlotExtrusion();
            Assert.NotNull(polyhedron_Slot);

            Assert.True(polyhedron_Slot.IsClosed(0.15));

            Measure("0.1 m slot, tolerance 0.15", polyhedron_Slot, 48, 0.15);

            // 6. Two thin slabs 0.02 m apart, judged at a tolerance spanning both of them.
            Polyhedron? polyhedron_Slabs = Polyhedron_IsClosed_ThinSlabSolid();
            Assert.NotNull(polyhedron_Slabs);

            Assert.True(polyhedron_Slabs.IsClosed(0.05));

            Measure("Two thin slabs, tolerance 0.05", polyhedron_Slabs, 48, 0.05);

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Monotonicity of the default criterion across the tolerance ladder (T = closed):");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Scenario | Vertex weld (previous) | Shipped |");
            stringBuilder.AppendLine("| :--- | :--- | :--- |");

            void Sweep(string name, Polyhedron polyhedron)
            {
                System.Text.StringBuilder stringBuilder_Weld = new();
                System.Text.StringBuilder stringBuilder_Shipped = new();

                for (int i = 0; i < Polyhedron_IsClosed_Tolerances.Length; i++)
                {
                    stringBuilder_Weld.Append(IsClosed_VertexWeld(polyhedron, false, Polyhedron_IsClosed_Tolerances[i]) ? "T" : ".");
                    stringBuilder_Shipped.Append(polyhedron.IsClosed(Polyhedron_IsClosed_Tolerances[i]) ? "T" : ".");
                }

                stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"| {name} | `{stringBuilder_Weld}` | `{stringBuilder_Shipped}` |");
            }

            Sweep("500-gon extrusion", polyhedron_Extrusion);
            Sweep("9 800-face ellipsoid", polyhedron_Ellipsoid);
            Sweep("Open cube, 5 faces", polyhedron_Open);
            Sweep("0.03 m step", polyhedron_Step);
            Sweep("0.1 m slot", polyhedron_Slot);
            Sweep("Two thin slabs", polyhedron_Slabs);

            List<IPolygonalFace3D> polygonalFace3Ds_Stack = Polyhedron_IsClosed_BoxFaces(0.0, 10.0, 0.0);
            polygonalFace3Ds_Stack.AddRange(Polyhedron_IsClosed_BoxFaces(0.0, 10.0, 10.0));
            Polyhedron? polyhedron_Stack = Create.Polyhedron(polygonalFace3Ds_Stack);
            Assert.NotNull(polyhedron_Stack);
            Sweep("Two stacked boxes", polyhedron_Stack);

            Polyhedron? polyhedron_Lifted = Create.Polyhedron(Polyhedron_IsClosed_BoxFacesWithLiftedTop(0.0, 10.0, 0.05));
            Assert.NotNull(polyhedron_Lifted);
            Sweep("Box, top lifted 0.05", polyhedron_Lifted);

            stringBuilder.AppendLine();
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"Ladder: {string.Join(", ", Polyhedron_IsClosed_Tolerances)}");

            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Verdicts where the implementations disagree:");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Scenario | Criterion | Vertex weld (previous) | Shipped |");
            stringBuilder.AppendLine("| :--- | :--- | :--- | :--- |");
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"| 0.03 m step, tolerance 0.05 | default | {IsClosed_VertexWeld(polyhedron_Step, false, 0.05)} | {polyhedron_Step.IsClosed(0.05)} |");
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"| 0.03 m step, tolerance 0.05 | manifold | {IsClosed_VertexWeld(polyhedron_Step, true, 0.05)} | {polyhedron_Step.IsClosed(true, 0.05)} |");
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"| 0.1 m slot, tolerance 0.15 | default | {IsClosed_VertexWeld(polyhedron_Slot, false, 0.15)} | {polyhedron_Slot.IsClosed(0.15)} |");
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"| Two thin slabs, tolerance 0.05 | default | {IsClosed_VertexWeld(polyhedron_Slabs, false, 0.05)} | {polyhedron_Slabs.IsClosed(0.05)} |");
            stringBuilder.AppendLine(CultureInfo.CurrentCulture, $"| Two thin slabs, tolerance 0.05 | manifold | {IsClosed_VertexWeld(polyhedron_Slabs, true, 0.05)} | {polyhedron_Slabs.IsClosed(true, 0.05)} |");

            string? directory_Reports = DiGi.Core.xUnit.Query.ReportsDirectory(System.Reflection.Assembly.GetExecutingAssembly());
            Assert.NotNull(directory_Reports);

            File.WriteAllText(Path.Combine(directory_Reports, "IsClosed_Benchmark.md"), stringBuilder.ToString());
        }
    }
}
