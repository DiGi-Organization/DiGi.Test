using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Classes;
using DiGi.Core.Classes;
using DiGi.Core.Interfaces;
using DiGi.Geometry.Core.Enums;
using DiGi.Geometry.Spatial;
using DiGi.Geometry.Spatial.Classes;
using DiGi.Geometry.Spatial.Interfaces;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that <see cref="BuildingModel.GetExternalShell(Side?, Orientation?, Orientation?, double)"/> turns a wall whose stored face points into the building outward.
        /// <para>The box is closed by six components around one space, and one wall is wound the wrong way so its stored normal points inward. Asked for no side, the method hands the wall back as stored - which is what shows the outward assertion can fail - and asked for <see cref="Side.External"/> it flips exactly that wall, leaves the other five as they are, and every face then carries the reference of its component.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_InwardNormal()
        {
            List<(IComponent Component, Vector3D Normal)> components = BuildingModelGetExternalShell_Box(0, 0, 0, 10, 10, 3, [2]);

            Space space = new(new Point3D(5, 5, 1.5), "Space");

            BuildingModel buildingModel = new();
            foreach ((IComponent component, Vector3D _) in components)
            {
                Assert.True(buildingModel.Assign(component, space));
            }

            // Without a side the stored orientation is kept, so the reversed wall still points inward: the guard below is a real one.
            Shell? shell_Stored = buildingModel.GetExternalShell();
            Assert.NotNull(shell_Stored);
            Assert.Equal(6, shell_Stored.Count);

            Dictionary<Guid, Vector3D> normals_Stored = BuildingModelGetExternalShell_Normals(shell_Stored);
            Assert.True(normals_Stored[components[2].Component.Guid] * components[2].Normal < 0, "The reversed wall was not stored inward, so the fixture proves nothing.");
            Assert.True(normals_Stored[components[0].Component.Guid] * components[0].Normal > 0);

            Shell? shell = buildingModel.GetExternalShell(Side.External);
            Assert.NotNull(shell);
            Assert.Equal(6, shell.Count);
            Assert.True(shell.IsClosed());

            BuildingModelGetExternalShell_AssertOutward(shell, components);
            BuildingModelGetExternalShell_AssertProbes(shell, 0.1, Core.Constants.Tolerance.Distance);

            PolyhedronNormalizationUpdater<Shell> polyhedronNormalizationUpdater = new(Side.External, null, null)
            {
                Value = shell
            };
            Assert.True(polyhedronNormalizationUpdater.Normalized());

            IUniqueReference? uniqueReference_Shell = shell.UniqueReference;
            Assert.True(uniqueReference_Shell is GuidReference guidReference_Shell && guidReference_Shell.Guid == buildingModel.Guid);

            // The model is left as it was: the wall it stores still points inward.
            List<IWall>? walls = buildingModel.GetComponents<IWall>();
            Assert.NotNull(walls);
            IWall? wall_Reversed = walls.Find(x => x.Guid == components[2].Component.Guid);
            Assert.NotNull(wall_Reversed);
            Vector3D? normal_Model = Building.Query.Geometry3D<PolygonalFace3D>(wall_Reversed)?.Plane?.Normal;
            Assert.NotNull(normal_Model);
            Assert.True(normal_Model * components[2].Normal < 0);
        }

        /// <summary>
        /// Tests the outward orientation of a courtyard building, the concave case where a centroid heuristic fails.
        /// <para>The envelope is a ring - a 20 by 20 footprint with a 10 by 10 hole in the middle, three metres high - so the floor and the roof carry a hole and four of the ten walls face the courtyard. Those four are stored inward. The centroid of the model sits in the courtyard, on the wrong side of every courtyard wall, so "away from the centroid" gives the wrong answer for them; ray parity over the closed envelope gives the right one for all ten faces.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_Courtyard()
        {
            List<(IComponent Component, Vector3D Normal)> components = BuildingModelGetExternalShell_Ring(0, 0, 0, 20, 20, 3, 5);

            Space space = new(new Point3D(2.5, 10, 1.5), "Space");

            BuildingModel buildingModel = new();
            foreach ((IComponent component, Vector3D _) in components)
            {
                Assert.True(buildingModel.Assign(component, space));
            }

            Shell? shell = buildingModel.GetExternalShell(Side.External);
            Assert.NotNull(shell);
            Assert.Equal(10, shell.Count);
            Assert.True(shell.IsClosed());

            BuildingModelGetExternalShell_AssertOutward(shell, components);
            BuildingModelGetExternalShell_AssertProbes(shell, 0.1, Core.Constants.Tolerance.Distance);

            // The heuristic the issue names would file every courtyard wall the wrong way round.
            Point3D? centroid = buildingModel.GetBoundingBox()?.GetCentroid();
            Assert.NotNull(centroid);

            List<Face>? faces = shell.PolygonalFaces;
            Assert.NotNull(faces);

            int count_CourtyardWalls = 0;
            foreach (Face face in faces)
            {
                Point3D? point3D = face.GetInternalPoint();
                Vector3D? normal = face.Plane?.Normal;
                Assert.NotNull(point3D);
                Assert.NotNull(normal);

                // Courtyard walls are the vertical faces whose internal point lies within the 5 metre ring of the hole.
                if (Math.Abs(normal.Z) > Core.Constants.Tolerance.Distance || point3D.X < 5 - Core.Constants.Tolerance.Distance || point3D.X > 15 + Core.Constants.Tolerance.Distance || point3D.Y < 5 - Core.Constants.Tolerance.Distance || point3D.Y > 15 + Core.Constants.Tolerance.Distance)
                {
                    continue;
                }

                count_CourtyardWalls++;
                Assert.True(normal * (point3D - centroid) < 0, "A courtyard wall does not disagree with the centroid heuristic, so the fixture proves nothing.");
            }

            Assert.Equal(4, count_CourtyardWalls);
        }

        /// <summary>
        /// Tests which components reach the envelope: every component bounding exactly one space, exactly once, and nothing else.
        /// <para>Two boxes are stacked into two spaces. The slab between them bounds both spaces and is an internal partition, and a stray floor stored without any space bounds none; neither may appear. The ten faces that remain - the lower floor, eight walls and the upper roof - close the envelope, and the four upper walls are stored inward to show they are oriented over that envelope rather than left as stored.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_Coverage()
        {
            List<(IComponent Component, Vector3D Normal)> components_Lower = BuildingModelGetExternalShell_Box(0, 0, 0, 10, 10, 3, []);
            List<(IComponent Component, Vector3D Normal)> components_Upper = BuildingModelGetExternalShell_Box(0, 0, 3, 10, 10, 6, [2, 3, 4, 5]);

            Space space_Lower = new(new Point3D(5, 5, 1.5), "Lower");
            Space space_Upper = new(new Point3D(5, 5, 4.5), "Upper");

            BuildingModel buildingModel = new();

            // The lower box keeps its floor and walls; its roof becomes the slab shared with the upper space.
            for (int i = 0; i < components_Lower.Count; i++)
            {
                if (i == 1)
                {
                    continue;
                }

                Assert.True(buildingModel.Assign(components_Lower[i].Component, space_Lower));
            }

            FaceFloor slab = new(BuildingModel_Footprints_Face([new Point3D(0, 0, 3), new Point3D(10, 0, 3), new Point3D(10, 10, 3), new Point3D(0, 10, 3)]));
            Assert.True(buildingModel.Assign(slab, space_Lower, space_Upper));

            // The upper box keeps its walls and roof; its floor is the slab.
            for (int i = 0; i < components_Upper.Count; i++)
            {
                if (i == 0)
                {
                    continue;
                }

                Assert.True(buildingModel.Assign(components_Upper[i].Component, space_Upper));
            }

            FaceFloor orphan = new(BuildingModel_Footprints_Face([new Point3D(0, 0, -1), new Point3D(0, 10, -1), new Point3D(10, 10, -1), new Point3D(10, 0, -1)]));
            Assert.True(buildingModel.Update(orphan));

            Shell? shell = buildingModel.GetExternalShell(Side.External);
            Assert.NotNull(shell);
            Assert.Equal(10, shell.Count);
            Assert.True(shell.IsClosed());

            List<(IComponent Component, Vector3D Normal)> components_External = [];
            components_External.AddRange(components_Lower.Where((x, i) => i != 1));
            components_External.AddRange(components_Upper.Where((x, i) => i != 0));
            Assert.Equal(10, components_External.Count);

            BuildingModelGetExternalShell_AssertOutward(shell, components_External);
            BuildingModelGetExternalShell_AssertProbes(shell, 0.1, Core.Constants.Tolerance.Distance);

            Dictionary<Guid, Vector3D> normals = BuildingModelGetExternalShell_Normals(shell);
            Assert.False(normals.ContainsKey(slab.Guid), "The slab bounds two spaces and must not reach the envelope.");
            Assert.False(normals.ContainsKey(orphan.Guid), "A component bounding no space must not reach the envelope.");
        }

        /// <summary>
        /// Tests the cases that yield no envelope: a model without any space relation, a space bounded only by non-polygonal geometry, and fewer than the four faces a closed solid needs.
        /// <para>The second case is where <see cref="BuildingModel.GetShells{TSpace}(IEnumerable{TSpace}, Side?, Orientation?, Orientation?, double)"/> throws; the envelope does not visit spaces, so it answers <see langword="null"/> instead.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_Null()
        {
            BuildingModel buildingModel_Empty = new();
            Assert.Null(buildingModel_Empty.GetExternalShell());
            Assert.Null(buildingModel_Empty.GetExternalShell(Side.External));

            Space space_Air = new(new Point3D(5, 5, 0), "Air");
            BuildingModel buildingModel_Air = new();
            Assert.True(buildingModel_Air.Assign(new PointAir(new Point3D(5, 5, 0)), space_Air));
            Assert.Null(buildingModel_Air.GetExternalShell(Side.External));
            Assert.Throws<InvalidOperationException>(() => buildingModel_Air.GetShells<Space>(Side.External));

            List<(IComponent Component, Vector3D Normal)> components = BuildingModelGetExternalShell_Box(0, 0, 0, 10, 10, 3, []);
            Space space_Open = new(new Point3D(5, 5, 1.5), "Open");
            BuildingModel buildingModel_Open = new();
            for (int i = 0; i < 3; i++)
            {
                Assert.True(buildingModel_Open.Assign(components[i].Component, space_Open));
            }

            Assert.Null(buildingModel_Open.GetExternalShell(Side.External));
        }

        /// <summary>
        /// Tests the envelope of the real building models held in the shared fixtures against the per-space path the consumers use today.
        /// <para>Three of the four stored models are single-space and the fourth is a two-storey split with one slab between its spaces. Over the shells of the spaces, a component on exactly one shell face is external and one on two is the partition, so the envelope has to hold exactly the former, with every normal on the same side as the space shell puts it, and none of the latter. The envelope also has to close at some rung of the tolerance ladder <see cref="Building.Query.IsEnclosed(BuildingModel?, bool, double)"/> uses, and a point just inside each face along its normal is inside the envelope while a point just outside is not.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_RealModels()
        {
            List<BuildingModel> buildingModels = [];
            foreach (string fileName in new string[] { "BuildingModel_ValidGeometry.json", "buildingmodel_5072294.json" })
            {
                List<BuildingModel>? buildingModels_File = BuildingModel_Footprints_File(fileName);
                Assert.NotNull(buildingModels_File);
                Assert.NotEmpty(buildingModels_File);

                buildingModels.AddRange(buildingModels_File);
            }

            Assert.Equal(4, buildingModels.Count);

            double[] tolerances = [Core.Constants.Tolerance.Distance, 1E-05, 0.0001, Core.Constants.Tolerance.MacroDistance, 0.01, 0.02, 0.05, 0.1, 0.2];

            List<string> lines = [];

            int count_MultiSpace = 0;
            int count_Probed = 0;
            foreach (BuildingModel buildingModel in buildingModels)
            {
                List<Space>? spaces = buildingModel.GetSpaces<Space>();
                Assert.NotNull(spaces);
                Assert.NotEmpty(spaces);

                Shell? shell = buildingModel.GetExternalShell(Side.External);
                Assert.NotNull(shell);

                List<Shell>? shells_Space = buildingModel.GetShells<Space>(Side.External);
                Assert.NotNull(shells_Space);
                Assert.Equal(spaces.Count, shells_Space.Count);

                // A component on one space shell is external, on two it is the partition between them.
                Dictionary<Guid, Vector3D> normals_External = [];
                HashSet<Guid> guids_Partition = [];
                foreach (Shell shell_Space in shells_Space)
                {
                    foreach (KeyValuePair<Guid, Vector3D> keyValuePair in BuildingModelGetExternalShell_Normals(shell_Space))
                    {
                        if (normals_External.Remove(keyValuePair.Key))
                        {
                            guids_Partition.Add(keyValuePair.Key);
                            continue;
                        }

                        normals_External.Add(keyValuePair.Key, keyValuePair.Value);
                    }
                }

                if (spaces.Count > 1)
                {
                    count_MultiSpace++;
                    Assert.NotEmpty(guids_Partition);
                }

                Dictionary<Guid, Vector3D> normals = BuildingModelGetExternalShell_Normals(shell);

                Assert.Equal(normals_External.Count, normals.Count);
                foreach (KeyValuePair<Guid, Vector3D> keyValuePair in normals_External)
                {
                    Assert.True(normals.TryGetValue(keyValuePair.Key, out Vector3D? normal), "A component on one space shell is missing from the envelope.");
                    Assert.True(normal * keyValuePair.Value > 0, "The envelope and the space shell disagree on the side of a face.");
                }

                foreach (Guid guid in guids_Partition)
                {
                    Assert.False(normals.ContainsKey(guid), "A component on two space shells reached the envelope.");
                }

                double? closingTolerance = shell.ClosingTolerance(tolerances);
                List<double?> closingTolerances_Space = shells_Space.ConvertAll(x => x.ClosingTolerance(tolerances));
                lines.Add(string.Format(CultureInfo.CurrentCulture, "Model {0}: spaces {1}, envelope faces {2}, partitions {3}, envelope closes at {4}, space shells close at {5}.", buildingModel.Guid, spaces.Count, shell.Count, guids_Partition.Count, closingTolerance?.ToString(CultureInfo.CurrentCulture) ?? "none", string.Join(" / ", closingTolerances_Space.Select(x => x?.ToString(CultureInfo.CurrentCulture) ?? "none"))));

                // Closed space shells give a closed envelope: dropping the partition leaves its two storeys pairing their edges with each other instead.
                // The converse does not hold - a storey split that never closed leaves the envelope open too - and such a model is recorded rather than probed.
                if (closingTolerances_Space.TrueForAll(x => x is not null))
                {
                    Assert.NotNull(closingTolerance);
                }

                if (closingTolerance is null)
                {
                    continue;
                }

                count_Probed++;
                BuildingModelGetExternalShell_AssertProbes(shell, 0.1, closingTolerance.Value);
            }

            string? path_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(path_Reports));
            File.WriteAllLines(System.IO.Path.Combine(path_Reports!, "BuildingModelGetExternalShell_RealModels.txt"), lines);

            Assert.Equal(1, count_MultiSpace);
            Assert.True(count_Probed >= 3, string.Format(CultureInfo.CurrentCulture, "Only {0} of the stored models closed, so the probes ran on too few of them.", count_Probed));
        }

        /// <summary>
        /// Measures the envelope of the number of building models the 3D view of the largest area it offers has to carry, against the per-space path in use today.
        /// <para>Both paths are warmed up over the whole set, then timed three times each over the same two thousand four hundred models in alternating order, and the ranges are written to the test reports directory. Two of the three stored models are single-space, where the two paths do the same work, and the third is a two-storey split, where the per-space path builds and orients its slab twice; the slowest envelope run is asserted against the threshold.</para>
        /// <para>Medium test (10.5 s): runs when DIGI_TEST_MAX_DURATION is Medium (the default) or Long.</para>
        /// </summary>
        [MediumFact]
        public void BuildingModelGetExternalShell_Performance()
        {
            const int count_Models = 2400;

            List<BuildingModel>? buildingModels = BuildingModel_Footprints_File("BuildingModel_ValidGeometry.json");
            Assert.NotNull(buildingModels);
            Assert.NotEmpty(buildingModels);

            List<BuildingModel> buildingModels_Temp = [];
            while (buildingModels_Temp.Count < count_Models)
            {
                foreach (BuildingModel buildingModel in buildingModels)
                {
                    if (Core.Query.Clone(buildingModel) is BuildingModel buildingModel_Temp)
                    {
                        buildingModels_Temp.Add(buildingModel_Temp);
                    }
                }

                Assert.NotEmpty(buildingModels_Temp);
            }

            int count_Faces = 0;

            long Envelope()
            {
                count_Faces = 0;

                Stopwatch stopwatch = Stopwatch.StartNew();
                foreach (BuildingModel buildingModel in buildingModels_Temp)
                {
                    Shell? shell = buildingModel.GetExternalShell(Side.External);
                    Assert.NotNull(shell);
                    count_Faces += shell.Count;
                }
                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            }

            long Spaces()
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                foreach (BuildingModel buildingModel in buildingModels_Temp)
                {
                    List<Shell>? shells = buildingModel.GetShells<Space>(Side.External);
                    Assert.NotNull(shells);
                }
                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            }

            // Warm up both paths over the whole set, so tiered compilation of the shared geometry has settled before either is timed.
            _ = Envelope();
            _ = Spaces();

            List<long> durations_Envelope = [];
            List<long> durations_Spaces = [];

            // The order alternates, so neither path is always the one paying for a cold cache.
            for (int i = 0; i < 3; i++)
            {
                if (i % 2 == 0)
                {
                    durations_Envelope.Add(Envelope());
                    durations_Spaces.Add(Spaces());
                }
                else
                {
                    durations_Spaces.Add(Spaces());
                    durations_Envelope.Add(Envelope());
                }
            }

            string? path_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(path_Reports));

            List<string> lines =
            [
                string.Format(CultureInfo.CurrentCulture, "Building models {0}, envelope faces {1}, processors {2}.", buildingModels_Temp.Count, count_Faces, Environment.ProcessorCount),
                string.Format(CultureInfo.CurrentCulture, "GetExternalShell(Side.External): {0} ms ({1:F1} us per model).", string.Join(" / ", durations_Envelope), durations_Envelope.Max() * 1000.0 / buildingModels_Temp.Count),
                string.Format(CultureInfo.CurrentCulture, "GetShells<Space>(Side.External): {0} ms ({1:F1} us per model).", string.Join(" / ", durations_Spaces), durations_Spaces.Max() * 1000.0 / buildingModels_Temp.Count)
            ];

            File.WriteAllLines(System.IO.Path.Combine(path_Reports!, "BuildingModelGetExternalShell_Performance.txt"), lines);

            // Measured 2026-09-18 (Ryzen 9 9950X): 619-680 ms isolated in Release, 1536-1638 ms in-suite in Debug; the threshold clears the latter.
            Assert.True(durations_Envelope.Max() < 5000, string.Format(CultureInfo.CurrentCulture, "BuildingModel.GetExternalShell failed threshold! Elapsed: {0} ms.", durations_Envelope.Max()));
        }

        /// <summary>
        /// Tests that turning the faces of a real model outward keeps every face where it is, on a footprint-extruded two-storey model whose curve walls are all stored inward.
        /// <para>The model is one of the Wroclaw models served by <c>gis/buildingmodel/itemsbycircle</c>: a floor, a slab bound to both storeys, twenty <see cref="CurveWall"/>s and a flat roof. Twenty-one of its twenty-two external faces have to be turned, and a face swept from a segment has its plane origin on its bottom edge, which is where <c>Planar.Flip</c> used to mirror the geometry instead of reversing the normal - the storey walls then hung three metres under the floor and the floor itself moved seven hundred kilometres. After the turn the envelope has to be closed at the default tolerance exactly as it was before, every ring has to hold the points it held before, every shared edge has to be traversed in opposite directions by its two faces, and the signed volume has to equal the footprint area times the height of the two storeys.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_Turned()
        {
            BuildingModel buildingModel = BuildingModelGetExternalShell_Envelope("70680df7-1751-442f-8788-7b6d3cb449d2");

            Assert.Equal(2, buildingModel.GetSpaces<Space>()?.Count);

            Shell? shell_Stored = buildingModel.GetExternalShell();
            Shell? shell = buildingModel.GetExternalShell(Side.External);
            Assert.NotNull(shell_Stored);
            Assert.NotNull(shell);
            Assert.Equal(22, shell_Stored.Count);
            Assert.Equal(22, shell.Count);

            // The fixture stores its swept walls inward, so the turn is exercised on twenty-one faces: the guard below is a real one.
            Assert.Equal(21, BuildingModelGetExternalShell_CountTurned(shell_Stored, shell));
            Assert.True(BuildingModelGetExternalShell_SignedVolume(shell_Stored) < 0, "The stored envelope is not inward, so the fixture proves nothing.");

            Assert.True(shell_Stored.IsClosed());
            Assert.True(shell.IsClosed(), "Turning the faces opened the envelope, so a face was moved rather than turned.");
            BuildingModelGetExternalShell_AssertKeptPoints(shell_Stored, shell);

            (int opposite, List<(int, int)> same, int naked) = BuildingModelGetExternalShell_Winding(shell, Core.Constants.Tolerance.MacroDistance);
            Assert.Equal(100, opposite);
            Assert.Empty(same);
            Assert.Equal(0, naked);

            double area_Floor = 0;
            foreach (Face face in shell.PolygonalFaces!)
            {
                if (face.Plane?.Normal?.Z < -0.999)
                {
                    area_Floor += face.GetArea();
                }
            }
            Assert.True(area_Floor > 100);

            double volume = BuildingModelGetExternalShell_SignedVolume(shell);
            Assert.True(System.Math.Abs(volume - (area_Floor * 6.0)) < 0.01 * area_Floor * 6.0, string.Format(CultureInfo.CurrentCulture, "The signed volume {0} is not the footprint {1} times the six metres of the two storeys.", volume, area_Floor));

            Assert.True(new PolyhedronNormalizationUpdater<Shell>(Side.External, null, null) { Value = shell }.Normalized());
            BuildingModelGetExternalShell_AssertProbes(shell, 0.3, Core.Constants.Tolerance.MacroDistance);
        }

        /// <summary>
        /// Tests the envelope of seven CityGML-derived models split into storeys, as served by <c>gis/buildingmodel/itemsbycircle</c> for Krakow and Warsaw, whose envelopes close.
        /// <para>Each model pins the counts it was downloaded with - storeys, external faces, faces that had to be turned and the coarsest rung the envelope closes at - so that a change in any of them is noticed. Two of the models carry a face with a hole. On every one the envelope is exactly the set of components bound to one space, turning keeps every face where it is and closes the envelope at the same rung as the stored one, every shared edge is traversed in opposite directions by its two faces, the signed volume is positive, and asking for the edge orientations makes every ring follow its normal without moving a point.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_Storeys()
        {
            List<(string Guid, int Spaces, int Faces, int Turned, double ClosingTolerance, bool Hole)> expected =
            [
                ("13acd9ba-6bb8-4ef9-8a6b-ab9101487563", 4, 21, 1, 0.01, false),
                ("a7a1d1a8-0ccb-42f6-9071-31252c2fc093", 3, 21, 0, 0.01, false),
                ("0a43864a-3c99-41b0-ae65-71d8eb7eac2a", 1, 24, 1, 0.01, true),
                ("1a3eeb93-ab06-443f-8381-5b28f34f423f", 6, 26, 0, 0.01, true),
                ("4ff6ae0b-5558-4a65-a63b-f9a1de2e7638", 2, 12, 0, 0.01, false),
                ("b232dd5f-f132-47c2-ba23-c0f0b01219fe", 2, 28, 0, 0.01, false),
                ("8227dca0-7d1a-40d3-a8f7-8c08efe394eb", 2, 24, 4, 0.1, false),
            ];

            double[] tolerances = [Core.Constants.Tolerance.Distance, 1E-05, 0.0001, Core.Constants.Tolerance.MacroDistance, 0.01, 0.02, 0.05, 0.1, 0.2];

            foreach ((string guid, int count_Spaces, int count_Faces, int count_Turned, double closingTolerance_Expected, bool hole) in expected)
            {
                BuildingModel buildingModel = BuildingModelGetExternalShell_Envelope(guid);
                Assert.Equal(count_Spaces, buildingModel.GetSpaces<Space>()?.Count);

                Shell? shell_Stored = buildingModel.GetExternalShell();
                Shell? shell = buildingModel.GetExternalShell(Side.External);
                Assert.NotNull(shell_Stored);
                Assert.NotNull(shell);
                Assert.Equal(count_Faces, shell.Count);
                Assert.Equal(count_Turned, BuildingModelGetExternalShell_CountTurned(shell_Stored, shell));

                BuildingModelGetExternalShell_AssertSelection(buildingModel, shell);

                Assert.Equal(closingTolerance_Expected, shell_Stored.ClosingTolerance(tolerances));
                Assert.Equal(closingTolerance_Expected, shell.ClosingTolerance(tolerances));
                BuildingModelGetExternalShell_AssertKeptPoints(shell_Stored, shell);

                (int opposite, List<(int, int)> same, int naked) = BuildingModelGetExternalShell_Winding(shell, 2.0 * closingTolerance_Expected);
                Assert.True(opposite > 0);
                Assert.Empty(same);
                Assert.Equal(0, naked);
                Assert.True(BuildingModelGetExternalShell_SignedVolume(shell) > 0, "The envelope of " + guid + " has a negative signed volume.");

                Assert.Equal(hole, shell.PolygonalFaces!.Exists(x => x.InternalEdges is List<IPolygonal3D> internalEdges && internalEdges.Count > 0));

                Shell? shell_Oriented = buildingModel.GetExternalShell(Side.External, Orientation.CounterClockwise, Orientation.Clockwise);
                Assert.NotNull(shell_Oriented);
                BuildingModelGetExternalShell_AssertKeptPoints(shell, shell_Oriented);
                BuildingModelGetExternalShell_AssertRingsFollowNormals(shell_Oriented);
            }
        }

        /// <summary>
        /// Documents the one face of a downloaded model that comes back inward: a sliver of 0.41 square metres on an envelope that closes only at five centimetres.
        /// <para>The Krakow model has three storeys and fifty-three external faces, six of which have to be turned. Five are turned correctly; the sixth is a triangular sliver wall whose ray crosses the gaps the envelope still has below five centimetres, so its parity comes out wrong and it ends up pointing into the building. It is the only face of the model whose probes fail and the only face traversing a shared edge in the same direction as a neighbour, which is how a consumer can tell such a face apart. The fact pins that face, so that an orientation that resolves it is noticed here.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_Sliver()
        {
            Guid guid_Sliver = new("5a8c86a3-eb88-4467-8f35-683e76ac2d46");

            BuildingModel buildingModel = BuildingModelGetExternalShell_Envelope("1cbf655b-d516-4a4b-a929-ff8cbe1fb9e5");

            Shell? shell_Stored = buildingModel.GetExternalShell();
            Shell? shell = buildingModel.GetExternalShell(Side.External);
            Assert.NotNull(shell_Stored);
            Assert.NotNull(shell);
            Assert.Equal(53, shell.Count);
            Assert.Equal(6, BuildingModelGetExternalShell_CountTurned(shell_Stored, shell));

            double[] tolerances = [Core.Constants.Tolerance.Distance, 1E-05, 0.0001, Core.Constants.Tolerance.MacroDistance, 0.01, 0.02, 0.05, 0.1, 0.2];
            Assert.Equal(0.05, shell.ClosingTolerance(tolerances));
            BuildingModelGetExternalShell_AssertKeptPoints(shell_Stored, shell);

            List<Face> faces = shell.PolygonalFaces!;
            int index_Sliver = faces.FindIndex(x => x.UniqueReference is GuidReference guidReference && guidReference.Guid == guid_Sliver);
            Assert.True(index_Sliver >= 0);
            Assert.True(faces[index_Sliver].GetArea() < 0.5);

            (int opposite, List<(int, int)> same, int naked) = BuildingModelGetExternalShell_Winding(shell, 0.1);
            Assert.NotEmpty(same);
            Assert.All(same, x => Assert.True(x.Item1 == index_Sliver || x.Item2 == index_Sliver, "A face other than the sliver traverses a shared edge in the same direction as its neighbour."));

            for (int i = 0; i < faces.Count; i++)
            {
                Point3D? point3D = faces[i].GetInternalPoint(Core.Constants.Tolerance.MacroDistance);
                Vector3D? normal = faces[i].Plane?.Normal?.Unit;
                Assert.NotNull(point3D);
                Assert.NotNull(normal);

                bool inside_Behind = shell.Inside(point3D - (normal * 0.05), Core.Constants.Tolerance.MacroDistance);
                bool inside_Front = shell.Inside(point3D + (normal * 0.05), Core.Constants.Tolerance.MacroDistance);

                if (i == index_Sliver)
                {
                    Assert.False(inside_Behind);
                    Assert.True(inside_Front, "The sliver is no longer inward - the limitation this fact documents is gone, update it.");
                    continue;
                }

                Assert.True(inside_Behind);
                Assert.False(inside_Front);
            }
        }

        /// <summary>
        /// Tests two downloaded models whose envelope never closes, where the side of a face cannot be decided by parity and is only kept from moving.
        /// <para>The first is six storeys of one CityGML building left open by a single T-junction: a wall edge that two roof edges meet mid-span. Every face was stored outward and the parity happens to agree, so nothing is turned. The second holds four stacked storeys and three roof-level parts side by side, whose shared walls each bound one space and therefore pass the selection rule as external faces - a consequence of the rule that consumers with such models have to know. On both, the envelope is exactly the set of one-space components, is open before and after the turn at every rung of the ladder, keeps every face where it is, and follows its normals once the edge orientations are asked for.</para>
        /// </summary>
        [Fact]
        public void BuildingModelGetExternalShell_Open()
        {
            double[] tolerances = [Core.Constants.Tolerance.Distance, 1E-05, 0.0001, Core.Constants.Tolerance.MacroDistance, 0.01, 0.02, 0.05, 0.1, 0.2];

            foreach ((string guid, int count_Spaces, int count_Partitions, int count_Faces, int count_Turned) in new List<(string, int, int, int, int)> { ("2d765bad-cd7f-43cd-93a5-1d7f639c8a72", 6, 5, 80, 0), ("d2bdce3a-9fa9-49f4-936e-321f6a1767c7", 7, 3, 74, 1) })
            {
                BuildingModel buildingModel = BuildingModelGetExternalShell_Envelope(guid);
                Assert.Equal(count_Spaces, buildingModel.GetSpaces<Space>()?.Count);

                List<IComponent>? components = buildingModel.GetComponents<IComponent>();
                Assert.NotNull(components);
                Assert.Equal(count_Partitions, components.Count(x => buildingModel.GetSpaces(x)?.Count == 2));

                Shell? shell_Stored = buildingModel.GetExternalShell();
                Shell? shell = buildingModel.GetExternalShell(Side.External);
                Assert.NotNull(shell_Stored);
                Assert.NotNull(shell);
                Assert.Equal(count_Faces, shell.Count);
                Assert.Equal(count_Turned, BuildingModelGetExternalShell_CountTurned(shell_Stored, shell));

                BuildingModelGetExternalShell_AssertSelection(buildingModel, shell);

                Assert.Null(shell_Stored.ClosingTolerance(tolerances));
                Assert.Null(shell.ClosingTolerance(tolerances));
                BuildingModelGetExternalShell_AssertKeptPoints(shell_Stored, shell);

                (int opposite, List<(int, int)> same, int naked) = BuildingModelGetExternalShell_Winding(shell, 0.05);
                Assert.True(opposite > 0);
                Assert.True(naked > 0, "The envelope of " + guid + " has no naked edge although it is open.");

                Shell? shell_Oriented = buildingModel.GetExternalShell(Side.External, Orientation.CounterClockwise, Orientation.Clockwise);
                Assert.NotNull(shell_Oriented);
                BuildingModelGetExternalShell_AssertKeptPoints(shell, shell_Oriented);
                BuildingModelGetExternalShell_AssertRingsFollowNormals(shell_Oriented);
            }
        }

        /// <summary>
        /// Builds the six components of an axis-aligned box wound outward, each paired with the outward normal it should end up with; the walls at the given indexes are wound the other way so their stored normal points inward.
        /// <para>Order: floor, roof, then the walls at the minimum Y, maximum X, maximum Y and minimum X.</para>
        /// </summary>
        private static List<(IComponent Component, Vector3D Normal)> BuildingModelGetExternalShell_Box(double xMin, double yMin, double zMin, double xMax, double yMax, double zMax, int[] indexes_Reversed)
        {
            List<(List<Point3D> Point3Ds, Vector3D Normal)> rings =
            [
                ([new Point3D(xMin, yMin, zMin), new Point3D(xMin, yMax, zMin), new Point3D(xMax, yMax, zMin), new Point3D(xMax, yMin, zMin)], new Vector3D(0, 0, -1)),
                ([new Point3D(xMin, yMin, zMax), new Point3D(xMax, yMin, zMax), new Point3D(xMax, yMax, zMax), new Point3D(xMin, yMax, zMax)], new Vector3D(0, 0, 1)),
                ([new Point3D(xMin, yMin, zMin), new Point3D(xMax, yMin, zMin), new Point3D(xMax, yMin, zMax), new Point3D(xMin, yMin, zMax)], new Vector3D(0, -1, 0)),
                ([new Point3D(xMax, yMin, zMin), new Point3D(xMax, yMax, zMin), new Point3D(xMax, yMax, zMax), new Point3D(xMax, yMin, zMax)], new Vector3D(1, 0, 0)),
                ([new Point3D(xMax, yMax, zMin), new Point3D(xMin, yMax, zMin), new Point3D(xMin, yMax, zMax), new Point3D(xMax, yMax, zMax)], new Vector3D(0, 1, 0)),
                ([new Point3D(xMin, yMax, zMin), new Point3D(xMin, yMin, zMin), new Point3D(xMin, yMin, zMax), new Point3D(xMin, yMax, zMax)], new Vector3D(-1, 0, 0))
            ];

            List<(IComponent Component, Vector3D Normal)> result = [];
            for (int i = 0; i < rings.Count; i++)
            {
                List<Point3D> point3Ds = rings[i].Point3Ds;
                if (indexes_Reversed.Contains(i))
                {
                    point3Ds.Reverse();
                }

                PolygonalFace3D polygonalFace3D = BuildingModel_Footprints_Face(point3Ds);

                IComponent component = i switch
                {
                    0 => new FaceFloor(polygonalFace3D),
                    1 => new SurfaceRoof(polygonalFace3D),
                    _ => new SurfaceWall(polygonalFace3D)
                };

                result.Add((component, rings[i].Normal));
            }

            return result;
        }

        /// <summary>
        /// Builds the ten components of a ring-shaped building - a box with a square courtyard cut through it - each paired with its outward normal. The four courtyard walls are wound inward.
        /// <para>Order: floor and roof (each with the courtyard as a hole), the four outer walls, then the four courtyard walls, whose outward side faces the courtyard.</para>
        /// </summary>
        private static List<(IComponent Component, Vector3D Normal)> BuildingModelGetExternalShell_Ring(double xMin, double yMin, double zMin, double xMax, double yMax, double zMax, double thickness)
        {
            double xMin_Hole = xMin + thickness;
            double yMin_Hole = yMin + thickness;
            double xMax_Hole = xMax - thickness;
            double yMax_Hole = yMax - thickness;

            List<(IComponent Component, Vector3D Normal)> result = [];

            Polygon3D? polygon3D_Floor = Geometry.Spatial.Create.Polygon3D([new Point3D(xMin, yMin, zMin), new Point3D(xMin, yMax, zMin), new Point3D(xMax, yMax, zMin), new Point3D(xMax, yMin, zMin)]);
            Polygon3D? polygon3D_Floor_Hole = Geometry.Spatial.Create.Polygon3D([new Point3D(xMin_Hole, yMin_Hole, zMin), new Point3D(xMin_Hole, yMax_Hole, zMin), new Point3D(xMax_Hole, yMax_Hole, zMin), new Point3D(xMax_Hole, yMin_Hole, zMin)]);
            Assert.NotNull(polygon3D_Floor);
            Assert.NotNull(polygon3D_Floor_Hole);

            PolygonalFace3D? polygonalFace3D_Floor = Geometry.Spatial.Create.PolygonalFace3D(polygon3D_Floor, [polygon3D_Floor_Hole]);
            Assert.NotNull(polygonalFace3D_Floor);
            result.Add((new FaceFloor(polygonalFace3D_Floor), new Vector3D(0, 0, -1)));

            Polygon3D? polygon3D_Roof = Geometry.Spatial.Create.Polygon3D([new Point3D(xMin, yMin, zMax), new Point3D(xMax, yMin, zMax), new Point3D(xMax, yMax, zMax), new Point3D(xMin, yMax, zMax)]);
            Polygon3D? polygon3D_Roof_Hole = Geometry.Spatial.Create.Polygon3D([new Point3D(xMin_Hole, yMin_Hole, zMax), new Point3D(xMax_Hole, yMin_Hole, zMax), new Point3D(xMax_Hole, yMax_Hole, zMax), new Point3D(xMin_Hole, yMax_Hole, zMax)]);
            Assert.NotNull(polygon3D_Roof);
            Assert.NotNull(polygon3D_Roof_Hole);

            PolygonalFace3D? polygonalFace3D_Roof = Geometry.Spatial.Create.PolygonalFace3D(polygon3D_Roof, [polygon3D_Roof_Hole]);
            Assert.NotNull(polygonalFace3D_Roof);
            result.Add((new SurfaceRoof(polygonalFace3D_Roof), new Vector3D(0, 0, 1)));

            // Outer walls wound outward.
            result.Add((new SurfaceWall(BuildingModel_Footprints_Face([new Point3D(xMin, yMin, zMin), new Point3D(xMax, yMin, zMin), new Point3D(xMax, yMin, zMax), new Point3D(xMin, yMin, zMax)])), new Vector3D(0, -1, 0)));
            result.Add((new SurfaceWall(BuildingModel_Footprints_Face([new Point3D(xMax, yMin, zMin), new Point3D(xMax, yMax, zMin), new Point3D(xMax, yMax, zMax), new Point3D(xMax, yMin, zMax)])), new Vector3D(1, 0, 0)));
            result.Add((new SurfaceWall(BuildingModel_Footprints_Face([new Point3D(xMax, yMax, zMin), new Point3D(xMin, yMax, zMin), new Point3D(xMin, yMax, zMax), new Point3D(xMax, yMax, zMax)])), new Vector3D(0, 1, 0)));
            result.Add((new SurfaceWall(BuildingModel_Footprints_Face([new Point3D(xMin, yMax, zMin), new Point3D(xMin, yMin, zMin), new Point3D(xMin, yMin, zMax), new Point3D(xMin, yMax, zMax)])), new Vector3D(-1, 0, 0)));

            // Courtyard walls: the outward side faces the courtyard, and they are wound the other way so their stored normal points into the building.
            result.Add((new SurfaceWall(BuildingModel_Footprints_Face([new Point3D(xMin_Hole, yMin_Hole, zMin), new Point3D(xMax_Hole, yMin_Hole, zMin), new Point3D(xMax_Hole, yMin_Hole, zMax), new Point3D(xMin_Hole, yMin_Hole, zMax)])), new Vector3D(0, 1, 0)));
            result.Add((new SurfaceWall(BuildingModel_Footprints_Face([new Point3D(xMax_Hole, yMin_Hole, zMin), new Point3D(xMax_Hole, yMax_Hole, zMin), new Point3D(xMax_Hole, yMax_Hole, zMax), new Point3D(xMax_Hole, yMin_Hole, zMax)])), new Vector3D(-1, 0, 0)));
            result.Add((new SurfaceWall(BuildingModel_Footprints_Face([new Point3D(xMax_Hole, yMax_Hole, zMin), new Point3D(xMin_Hole, yMax_Hole, zMin), new Point3D(xMin_Hole, yMax_Hole, zMax), new Point3D(xMax_Hole, yMax_Hole, zMax)])), new Vector3D(0, -1, 0)));
            result.Add((new SurfaceWall(BuildingModel_Footprints_Face([new Point3D(xMin_Hole, yMax_Hole, zMin), new Point3D(xMin_Hole, yMin_Hole, zMin), new Point3D(xMin_Hole, yMin_Hole, zMax), new Point3D(xMin_Hole, yMax_Hole, zMax)])), new Vector3D(1, 0, 0)));

            return result;
        }

        /// <summary>
        /// Reads the unit normal of every face of a shell, keyed by the identifier the face's component reference carries. Every face has to carry a <see cref="GuidReference"/> and no component may appear twice.
        /// </summary>
        private static Dictionary<Guid, Vector3D> BuildingModelGetExternalShell_Normals(Shell shell)
        {
            List<Face>? faces = shell.PolygonalFaces;
            Assert.NotNull(faces);

            Dictionary<Guid, Vector3D> result = [];
            foreach (Face face in faces)
            {
                // Face.UniqueReference is a fresh clone on every call: read it once and match on the identifier it carries.
                IUniqueReference? uniqueReference = face.UniqueReference;
                Assert.True(uniqueReference is GuidReference, "A face of the envelope carries no GuidReference of its component.");

                Vector3D? normal = face.Plane?.Normal?.Unit;
                Assert.NotNull(normal);

                Assert.True(result.TryAdd(((GuidReference)uniqueReference!).Guid, normal), "A component appears on two faces of the envelope.");
            }

            return result;
        }

        /// <summary>
        /// Asserts that every given component is on the shell exactly once and that its face normal points the way it is expected to.
        /// </summary>
        private static void BuildingModelGetExternalShell_AssertOutward(Shell shell, IEnumerable<(IComponent Component, Vector3D Normal)> components)
        {
            Dictionary<Guid, Vector3D> normals = BuildingModelGetExternalShell_Normals(shell);

            int count = 0;
            foreach ((IComponent component, Vector3D normal_Expected) in components)
            {
                count++;

                Assert.True(normals.TryGetValue(component.Guid, out Vector3D? normal), string.Format(CultureInfo.CurrentCulture, "The component {0} is missing from the envelope.", component.Guid));
                Assert.True(normal * normal_Expected > 0.99, string.Format(CultureInfo.CurrentCulture, "The face of the component {0} is not oriented outward.", component.Guid));
            }

            Assert.Equal(count, normals.Count);
        }

        /// <summary>
        /// Asserts, for every face of a closed shell, that a point moved from its internal point against the normal lies inside the shell and a point moved along the normal lies outside it.
        /// </summary>
        private static void BuildingModelGetExternalShell_AssertProbes(Shell shell, double offset, double tolerance)
        {
            List<Face>? faces = shell.PolygonalFaces;
            Assert.NotNull(faces);

            foreach (Face face in faces)
            {
                Point3D? point3D = face.GetInternalPoint(tolerance);
                Vector3D? normal = face.Plane?.Normal?.Unit;
                Assert.NotNull(point3D);
                Assert.NotNull(normal);

                Assert.True(shell.Inside(point3D - (normal * offset), tolerance), "A point just behind a face of the envelope is not inside it.");
                Assert.False(shell.Inside(point3D + (normal * offset), tolerance), "A point just in front of a face of the envelope is inside it, so the face points inward.");
            }
        }

        /// <summary>
        /// Loads the model with the given identifier from <c>BuildingModel_Envelope.json</c>, the models downloaded from <c>gis/buildingmodel/itemsbycircle</c> on 2026-09-18 for the envelope facts.
        /// </summary>
        private static BuildingModel BuildingModelGetExternalShell_Envelope(string guid)
        {
            List<BuildingModel>? buildingModels = BuildingModel_Footprints_File("BuildingModel_Envelope.json");
            Assert.NotNull(buildingModels);

            BuildingModel? buildingModel = buildingModels.Find(x => x.Guid == new Guid(guid));
            Assert.NotNull(buildingModel);

            return buildingModel;
        }

        /// <summary>
        /// Counts the faces whose normal in the second shell points the other way than in the first, matched by the component reference each face carries.
        /// </summary>
        private static int BuildingModelGetExternalShell_CountTurned(Shell shell_Stored, Shell shell)
        {
            Dictionary<Guid, Vector3D> normals_Stored = BuildingModelGetExternalShell_Normals(shell_Stored);
            Dictionary<Guid, Vector3D> normals = BuildingModelGetExternalShell_Normals(shell);
            Assert.Equal(normals_Stored.Count, normals.Count);

            int result = 0;
            foreach (KeyValuePair<Guid, Vector3D> keyValuePair in normals)
            {
                Assert.True(normals_Stored.TryGetValue(keyValuePair.Key, out Vector3D? normal_Stored));
                if (normal_Stored * keyValuePair.Value < 0)
                {
                    result++;
                }
            }

            return result;
        }

        /// <summary>
        /// Asserts that the envelope holds exactly the components bound to one space, each once, and nothing bound to two or none.
        /// </summary>
        private static void BuildingModelGetExternalShell_AssertSelection(BuildingModel buildingModel, Shell shell)
        {
            List<IComponent>? components = buildingModel.GetComponents<IComponent>();
            Assert.NotNull(components);

            Dictionary<Guid, Vector3D> normals = BuildingModelGetExternalShell_Normals(shell);

            int count = 0;
            foreach (IComponent component in components)
            {
                if (buildingModel.GetSpaces(component)?.Count == 1)
                {
                    count++;
                    Assert.True(normals.ContainsKey(component.Guid), "A component bound to one space is missing from the envelope.");
                }
                else
                {
                    Assert.False(normals.ContainsKey(component.Guid), "A component bound to two spaces or to none reached the envelope.");
                }
            }

            Assert.Equal(count, normals.Count);
        }

        /// <summary>
        /// Gathers the points of every ring of a face, the external edge first and the holes after it.
        /// </summary>
        private static List<Point3D> BuildingModelGetExternalShell_Points(Face face)
        {
            List<Point3D> result = [];

            if (face.ExternalEdge?.GetPoints() is List<Point3D> point3Ds)
            {
                result.AddRange(point3Ds);
            }

            if (face.InternalEdges is List<IPolygonal3D> internalEdges)
            {
                foreach (IPolygonal3D internalEdge in internalEdges)
                {
                    if (internalEdge.GetPoints() is List<Point3D> point3Ds_Internal)
                    {
                        result.AddRange(point3Ds_Internal);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Asserts that every face of the second shell holds exactly the points its counterpart in the first shell holds, matched by the component reference, so that orienting a face has not moved it.
        /// </summary>
        private static void BuildingModelGetExternalShell_AssertKeptPoints(Shell shell_Before, Shell shell_After)
        {
            Dictionary<Guid, List<Point3D>> point3Ds_Before = [];
            foreach (Face face in shell_Before.PolygonalFaces!)
            {
                Assert.True(face.UniqueReference is GuidReference);
                point3Ds_Before[((GuidReference)face.UniqueReference!).Guid] = BuildingModelGetExternalShell_Points(face);
            }

            foreach (Face face in shell_After.PolygonalFaces!)
            {
                Assert.True(face.UniqueReference is GuidReference);
                Assert.True(point3Ds_Before.TryGetValue(((GuidReference)face.UniqueReference!).Guid, out List<Point3D>? point3Ds));

                List<Point3D> point3Ds_After = BuildingModelGetExternalShell_Points(face);
                Assert.Equal(point3Ds!.Count, point3Ds_After.Count);

                foreach (Point3D point3D in point3Ds_After)
                {
                    double distance = point3Ds.Min(x => x.Distance(point3D));
                    Assert.True(distance < Core.Constants.Tolerance.Distance, string.Format(CultureInfo.CurrentCulture, "The point {0} moved by {1} when its face was oriented.", point3D, distance));
                }
            }
        }

        /// <summary>
        /// Pairs the directed edges of a shell, each ring taken in the direction that follows its face normal, after welding vertices closer than the given distance.
        /// <para>Two faces oriented consistently traverse their shared edge in opposite directions. The result counts the directed edges matched by an opposite one, lists the face index pairs of the edges matched only by an edge in the same direction, and counts the edges matched by nothing. Independent of the ray parity that oriented the shell, so it can judge that orientation.</para>
        /// </summary>
        private static (int Opposite, List<(int, int)> Same, int Naked) BuildingModelGetExternalShell_Winding(Shell shell, double weld)
        {
            List<Face> faces = shell.PolygonalFaces!;

            List<Point3D> vertices = [];
            int Vertex(Point3D point3D)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (vertices[i].Distance(point3D) <= weld)
                    {
                        return i;
                    }
                }

                vertices.Add(point3D);
                return vertices.Count - 1;
            }

            static Vector3D Newell(List<Point3D> point3Ds)
            {
                double x = 0, y = 0, z = 0;
                for (int i = 0; i < point3Ds.Count; i++)
                {
                    Point3D a = point3Ds[i];
                    Point3D b = point3Ds[(i + 1) % point3Ds.Count];
                    x += (a.Y - b.Y) * (a.Z + b.Z);
                    y += (a.Z - b.Z) * (a.X + b.X);
                    z += (a.X - b.X) * (a.Y + b.Y);
                }

                return new Vector3D(x, y, z);
            }

            Dictionary<(int, int), List<int>> edges = [];
            for (int index = 0; index < faces.Count; index++)
            {
                Vector3D? normal = faces[index].Plane?.Normal;
                List<Point3D>? point3Ds = faces[index].ExternalEdge?.GetPoints();
                Assert.NotNull(normal);
                Assert.NotNull(point3Ds);

                List<List<Point3D>> rings = [point3Ds];
                if (faces[index].InternalEdges is List<IPolygonal3D> internalEdges)
                {
                    rings.AddRange(internalEdges.Select(x => x.GetPoints()).OfType<List<Point3D>>());
                }

                // The external ring runs with the normal and each hole against it, which is the winding of a consistently oriented surface.
                bool reverse = Newell(point3Ds) * normal < 0;
                foreach (List<Point3D> ring in rings)
                {
                    List<int> indexes = ring.ConvertAll(Vertex);
                    if (reverse)
                    {
                        indexes.Reverse();
                    }

                    for (int i = 0; i < indexes.Count; i++)
                    {
                        (int, int) key = (indexes[i], indexes[(i + 1) % indexes.Count]);
                        if (key.Item1 == key.Item2)
                        {
                            continue;
                        }

                        if (!edges.TryGetValue(key, out List<int>? indexes_Face))
                        {
                            indexes_Face = [];
                            edges[key] = indexes_Face;
                        }

                        indexes_Face.Add(index);
                    }
                }
            }

            int opposite = 0;
            int naked = 0;
            List<(int, int)> same = [];
            foreach (KeyValuePair<(int, int), List<int>> keyValuePair in edges)
            {
                int count_Opposite = edges.TryGetValue((keyValuePair.Key.Item2, keyValuePair.Key.Item1), out List<int>? indexes_Opposite) ? indexes_Opposite.Count : 0;
                if (keyValuePair.Value.Count == 1 && count_Opposite == 1)
                {
                    opposite++;
                }
                else if (count_Opposite == 0 && keyValuePair.Value.Count == 1)
                {
                    naked++;
                }
                else if (count_Opposite == 0)
                {
                    for (int i = 0; i < keyValuePair.Value.Count; i++)
                    {
                        for (int j = i + 1; j < keyValuePair.Value.Count; j++)
                        {
                            same.Add((keyValuePair.Value[i], keyValuePair.Value[j]));
                        }
                    }
                }
            }

            return (opposite, same, naked);
        }

        /// <summary>
        /// Sums the signed volume of a shell by the divergence theorem, positive when the faces point outward; taken about the centre of the bounding box so that gaps of the size of the closing tolerance do not swamp it at GIS coordinates.
        /// </summary>
        private static double BuildingModelGetExternalShell_SignedVolume(Shell shell)
        {
            BoundingBox3D? boundingBox3D = shell.GetBoundingBox();
            Assert.NotNull(boundingBox3D);

            Point3D centre = new((boundingBox3D.Min.X + boundingBox3D.Max.X) / 2.0, (boundingBox3D.Min.Y + boundingBox3D.Max.Y) / 2.0, (boundingBox3D.Min.Z + boundingBox3D.Max.Z) / 2.0);

            double result = 0;
            foreach (Face face in shell.PolygonalFaces!)
            {
                Vector3D? normal = face.Plane?.Normal?.Unit;
                Point3D? point3D = face.ExternalEdge?.GetPoints()?.FirstOrDefault();
                Assert.NotNull(normal);
                Assert.NotNull(point3D);

                result += (normal * new Vector3D(point3D.X - centre.X, point3D.Y - centre.Y, point3D.Z - centre.Z)) * face.GetArea() / 3.0;
            }

            return result;
        }

        /// <summary>
        /// Asserts that the external ring of every face runs counter-clockwise about its normal and every hole clockwise, which is what a consumer deriving triangle winding from the rings needs and what asking for the edge orientations is meant to give.
        /// </summary>
        private static void BuildingModelGetExternalShell_AssertRingsFollowNormals(Shell shell)
        {
            static double Newell(List<Point3D> point3Ds, Vector3D normal)
            {
                double x = 0, y = 0, z = 0;
                for (int i = 0; i < point3Ds.Count; i++)
                {
                    Point3D a = point3Ds[i];
                    Point3D b = point3Ds[(i + 1) % point3Ds.Count];
                    x += (a.Y - b.Y) * (a.Z + b.Z);
                    y += (a.Z - b.Z) * (a.X + b.X);
                    z += (a.X - b.X) * (a.Y + b.Y);
                }

                return new Vector3D(x, y, z) * normal;
            }

            foreach (Face face in shell.PolygonalFaces!)
            {
                Vector3D? normal = face.Plane?.Normal;
                List<Point3D>? point3Ds = face.ExternalEdge?.GetPoints();
                Assert.NotNull(normal);
                Assert.NotNull(point3Ds);
                Assert.True(Newell(point3Ds, normal) > 0, "The external ring of a face runs against its normal.");

                if (face.InternalEdges is List<IPolygonal3D> internalEdges)
                {
                    foreach (IPolygonal3D internalEdge in internalEdges)
                    {
                        List<Point3D>? point3Ds_Internal = internalEdge.GetPoints();
                        Assert.NotNull(point3Ds_Internal);
                        Assert.True(Newell(point3Ds_Internal, normal) < 0, "A hole of a face runs with its normal.");
                    }
                }
            }
        }
    }
}
