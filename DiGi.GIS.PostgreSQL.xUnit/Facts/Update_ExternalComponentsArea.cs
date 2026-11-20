using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.IO.Table.Classes;
using DiGi.Core.Parameter.Classes;
using DiGi.GIS.Analytical.Enums;
using DiGi.GIS.PostgreSQL.Classes;
using DiGi.GIS.PostgreSQL.Enums;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Spatial.Classes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The names of the 35 external components area columns, in the order the method under test declares them: the eight wall sectors, the flat roof, the 24 tilted-roof bands, the floor, and the total last.
        /// <para>The names are declared here rather than taken from the shared GIS.IO constants, for the same reason as in the typology facts: this project does not reference that assembly, and only a column's name matters to address a cell.</para>
        /// </summary>
        private static readonly string[] ColumnNames_External =
        [
            "External north wall area",
            "External northeast wall area",
            "External east wall area",
            "External southeast wall area",
            "External south wall area",
            "External southwest wall area",
            "External west wall area",
            "External northwest wall area",
            "External flat roof area",
            "External north tilted roof area up to 20",
            "External northeast tilted roof area up to 20",
            "External east tilted roof area up to 20",
            "External southeast tilted roof area up to 20",
            "External south tilted roof area up to 20",
            "External southwest tilted roof area up to 20",
            "External west tilted roof area up to 20",
            "External northwest tilted roof area up to 20",
            "External north tilted roof area between 20 and 45",
            "External northeast tilted roof area between 20 and 45",
            "External east tilted roof area between 20 and 45",
            "External southeast tilted roof area between 20 and 45",
            "External south tilted roof area between 20 and 45",
            "External southwest tilted roof area between 20 and 45",
            "External west tilted roof area between 20 and 45",
            "External northwest tilted roof area between 20 and 45",
            "External north tilted roof area above 45",
            "External northeast tilted roof area above 45",
            "External east tilted roof area above 45",
            "External southeast tilted roof area above 45",
            "External south tilted roof area above 45",
            "External southwest tilted roof area above 45",
            "External west tilted roof area above 45",
            "External northwest tilted roof area above 45",
            "External floor area",
            "External components area"
        ];

        /// <summary>
        /// The name of the closing tolerance column - the open-envelope signal the method writes beside the 35 area columns.
        /// </summary>
        private const string ColumnName_ClosingTolerance = "Closing tolerance";
        private const string ColumnName_CalculatedHeight = "Calculated height";

        /// <summary>
        /// Builds a polygonal face from a plane and the face's local 2D points.
        /// <para>The plane's origin and normal define the face's position and side in 3D; the points address the face within the plane. The origin is chosen for each stored normal so the face's 3D extent is the intended square, because the plane derives its local basis from the normal.</para>
        /// </summary>
        /// <param name="plane">The plane the face lies on.</param>
        /// <param name="points">The face's points in the plane's local 2D coordinates.</param>
        /// <returns>The polygonal face.</returns>
        private static PolygonalFace3D Face3D(Plane plane, params Point2D[] points)
        {
            PolygonalFace3D? face = DiGi.Geometry.Spatial.Create.PolygonalFace3D(plane, points);
            Assert.NotNull(face);

            return face!;
        }

        /// <summary>
        /// Builds a square wall of the given edge size on the plane defined by the origin and stored normal.
        /// <para>The origin is the face's local (0,0) corner; for a given stored normal the plane's local basis runs in fixed world directions, so the caller picks the origin that makes the face span the intended square of the box it bounds.</para>
        /// </summary>
        /// <param name="origin">The face's local (0,0) corner in 3D.</param>
        /// <param name="normal">The stored normal; outward from the space's interior or its inverse.</param>
        /// <param name="size">The edge length of the square wall in metres.</param>
        /// <returns>The wall component.</returns>
        private static DiGi.Analytical.Building.Classes.SurfaceWall Wall(Point3D origin, Vector3D normal, double size)
        {
            Plane plane = new(origin, normal);
            PolygonalFace3D face = Face3D(plane, new Point2D(0, 0), new Point2D(size, 0), new Point2D(size, size), new Point2D(0, size));
            DiGi.Analytical.Building.Classes.SurfaceWall? wall = DiGi.Analytical.Building.Create.SurfaceWall(face);
            Assert.NotNull(wall);

            return wall!;
        }

        /// <summary>
        /// Builds a square roof of the given edge size on the plane defined by the origin and stored normal.
        /// </summary>
        /// <param name="origin">The face's local (0,0) corner in 3D.</param>
        /// <param name="normal">The stored normal, the roof's upward direction or its inverse.</param>
        /// <param name="size">The edge length of the square roof in metres.</param>
        /// <returns>The roof component.</returns>
        private static DiGi.Analytical.Building.Classes.SurfaceRoof Roof(Point3D origin, Vector3D normal, double size)
        {
            Plane plane = new(origin, normal);
            PolygonalFace3D face = Face3D(plane, new Point2D(0, 0), new Point2D(size, 0), new Point2D(size, size), new Point2D(0, size));
            DiGi.Analytical.Building.Classes.SurfaceRoof? roof = DiGi.Analytical.Building.Create.SurfaceRoof(face);
            Assert.NotNull(roof);

            return roof!;
        }

        /// <summary>
        /// Builds a square floor of the given edge size on the plane defined by the origin and stored normal.
        /// </summary>
        /// <param name="origin">The face's local (0,0) corner in 3D.</param>
        /// <param name="normal">The stored normal; a floor consumes no orientation, so either direction classifies the same area.</param>
        /// <param name="size">The edge length of the square floor in metres.</param>
        /// <returns>The floor component.</returns>
        private static DiGi.Analytical.Building.Classes.FaceFloor Floor(Point3D origin, Vector3D normal, double size)
        {
            Plane plane = new(origin, normal);
            PolygonalFace3D face = Face3D(plane, new Point2D(0, 0), new Point2D(size, 0), new Point2D(size, size), new Point2D(0, size));
            DiGi.Analytical.Building.Classes.FaceFloor? floor = DiGi.Analytical.Building.Create.FaceFloor(face);
            Assert.NotNull(floor);

            return floor!;
        }

        /// <summary>
        /// Builds a rectangular wall of the given local extents on the plane defined by the origin and stored normal.
        /// </summary>
        /// <param name="origin">The face's local (0,0) corner in 3D.</param>
        /// <param name="normal">The stored normal; outward from the space's interior or its inverse.</param>
        /// <param name="a">The face's extent along the plane's local first axis, in metres.</param>
        /// <param name="b">The face's extent along the plane's local second axis, in metres.</param>
        /// <returns>The wall component.</returns>
        private static DiGi.Analytical.Building.Classes.SurfaceWall WallR(Point3D origin, Vector3D normal, double a, double b)
        {
            Plane plane = new(origin, normal);
            PolygonalFace3D face = Face3D(plane, new Point2D(0, 0), new Point2D(a, 0), new Point2D(a, b), new Point2D(0, b));
            DiGi.Analytical.Building.Classes.SurfaceWall? wall = DiGi.Analytical.Building.Create.SurfaceWall(face);
            Assert.NotNull(wall);

            return wall!;
        }

        /// <summary>
        /// Builds an L-shaped floor on the plane defined by the origin and stored normal, from the outline's local 2D points.
        /// </summary>
        /// <param name="origin">The plane's origin in 3D.</param>
        /// <param name="normal">The stored normal; a floor consumes no orientation, so either direction classifies the same area.</param>
        /// <param name="points">The outline's points in the plane's local 2D coordinates.</param>
        /// <returns>The floor component.</returns>
        private static DiGi.Analytical.Building.Classes.FaceFloor FloorL(Point3D origin, Vector3D normal, params Point2D[] points)
        {
            PolygonalFace3D face = Face3D(new Plane(origin, normal), points);
            DiGi.Analytical.Building.Classes.FaceFloor? floor = DiGi.Analytical.Building.Create.FaceFloor(face);
            Assert.NotNull(floor);

            return floor!;
        }

        /// <summary>
        /// Builds an L-shaped roof on the plane defined by the origin and stored normal, from the outline's local 2D points.
        /// </summary>
        /// <param name="origin">The plane's origin in 3D.</param>
        /// <param name="normal">The stored normal, the roof's upward direction or its inverse.</param>
        /// <param name="points">The outline's points in the plane's local 2D coordinates.</param>
        /// <returns>The roof component.</returns>
        private static DiGi.Analytical.Building.Classes.SurfaceRoof RoofL(Point3D origin, Vector3D normal, params Point2D[] points)
        {
            PolygonalFace3D face = Face3D(new Plane(origin, normal), points);
            DiGi.Analytical.Building.Classes.SurfaceRoof? roof = DiGi.Analytical.Building.Create.SurfaceRoof(face);
            Assert.NotNull(roof);

            return roof!;
        }

        /// <summary>
        /// Builds the eight faces of a closed L-shaped volume: the footprint [0,5] x [0,20] union [0,20] x [0,5], the height [0,10] - six walls, an L-shaped floor and an L-shaped roof.
        /// <para>The floor's interior point falls in the long strip, on the far side of the strip-north wall (y = 5, x in [5, 20]) from that wall's outward side. For that wall the interior reference point and the outward side are opposite - exactly where an interior-point heuristic loses - while a shell built over the closed face set still resolves the wall's direction from the face set.</para>
        /// </summary>
        /// <param name="stripNorthStoredInward">When true the strip-north wall is stored with its inward normal (-y); when false with its outward normal (+y).</param>
        /// <returns>The eight components, in the order south, east, notch, strip-north, north, west, floor, roof.</returns>
        private static IComponent[] LShape(bool stripNorthStoredInward)
        {
            const double s = 20.0;
            const double t = 5.0;
            const double h = 10.0;

            Vector3D stripNorthNormal = stripNorthStoredInward ? new Vector3D(0, -1, 0) : new Vector3D(0, 1, 0);
            Point3D stripNorthOrigin = stripNorthStoredInward ? new Point3D(s, t, h) : new Point3D(t, t, h);

            return
            [
                WallR(new Point3D(s, 0, h), new Vector3D(0, -1, 0), s, h),
                WallR(new Point3D(s, t, h), new Vector3D(1, 0, 0), t, h),
                WallR(new Point3D(t, s, h), new Vector3D(1, 0, 0), s - t, h),
                WallR(stripNorthOrigin, stripNorthNormal, s - t, h),
                WallR(new Point3D(0, s, h), new Vector3D(0, 1, 0), t, h),
                WallR(new Point3D(0, 0, h), new Vector3D(-1, 0, 0), s, h),
                FloorL(new Point3D(0, 0, 0), new Vector3D(0, 0, -1), new Point2D(0, 0), new Point2D(0, -s), new Point2D(t, -s), new Point2D(t, -t), new Point2D(s, -t), new Point2D(s, 0)),
                RoofL(new Point3D(0, 0, h), new Vector3D(0, 0, 1), new Point2D(0, 0), new Point2D(s, 0), new Point2D(s, t), new Point2D(t, t), new Point2D(t, s), new Point2D(0, s))
            ];
        }

        /// <summary>
        /// Builds the six faces of a clean closed box [0,10]³ - four walls, a floor and a roof - each spanning the intended unit square of the box.
        /// <para>The box is a closed volume, which is what the shell's ray-parity orientation needs: a wall stored inward is re-oriented outward only when its ray along the stored normal crosses the opposing wall, and that crossing is clean (no coplanar graze) on a true box.</para>
        /// </summary>
        /// <param name="westStoredInward">When true the west wall is stored with its inward normal (+x); when false with its outward normal (-x). The east, south and north walls are always stored outward.</param>
        /// <param name="floorStoredInward">When true the floor is stored with its inward normal (+z); when false with its outward normal (-z). The roof is always stored outward (+z).</param>
        /// <returns>The six components of the box, in the order west, east, south, north, floor, roof.</returns>
        private static IComponent[] Box(bool westStoredInward = false, bool floorStoredInward = false)
        {
            const double s = 10.0;

            Vector3D westNormal = westStoredInward ? new Vector3D(1, 0, 0) : new Vector3D(-1, 0, 0);
            Point3D westOrigin = westStoredInward ? new Point3D(0, s, s) : new Point3D(0, 0, s);
            Vector3D floorNormal = floorStoredInward ? new Vector3D(0, 0, 1) : new Vector3D(0, 0, -1);
            Point3D floorOrigin = floorStoredInward ? new Point3D(0, 0, 0) : new Point3D(0, s, 0);

            return
            [
                Wall(westOrigin, westNormal, s),
                Wall(new Point3D(s, s, s), new Vector3D(1, 0, 0), s),
                Wall(new Point3D(s, 0, s), new Vector3D(0, -1, 0), s),
                Wall(new Point3D(0, s, s), new Vector3D(0, 1, 0), s),
                Floor(floorOrigin, floorNormal, s),
                Roof(new Point3D(0, 0, s), new Vector3D(0, 0, 1), s)
            ];
        }

        /// <summary>
        /// Builds a stored building model envelope: a single space is added, every component is assigned to it, and the result is converted for storage under the reference and county identifier.
        /// </summary>
        /// <param name="reference">The building reference the model is stored under.</param>
        /// <param name="countyId">The county identifier the model is stored under.</param>
        /// <param name="components">The components the model carries; each is assigned to the model's single space.</param>
        /// <returns>The storage envelope.</returns>
        private static BuildingModel Model(string reference, int countyId, params IComponent[] components)
        {
            DiGi.Analytical.Building.Classes.BuildingModel buildingModel = new();
            DiGi.Analytical.Building.Classes.Space space = new(new Point3D(5, 5, 5), "Space 1");
            Assert.True(buildingModel.Update(space));

            foreach (IComponent component in components)
            {
                Assert.True(buildingModel.Assign(component, space));
            }

            Assert.True(buildingModel.SetValue(BuildingModelParameter.Reference, reference, new SetValueSettings(true, false)));

            BuildingModel? envelope = buildingModel.ToPostgreSQL(countyId);
            Assert.NotNull(envelope);

            return envelope!;
        }

        /// <summary>
        /// Reads the value the method under test wrote into a table cell, addressed by the column's name.
        /// </summary>
        /// <param name="table">The table the row belongs to.</param>
        /// <param name="row">The row to read.</param>
        /// <param name="columnName">The name of the column to read.</param>
        /// <returns>The cell's value; 0 when the cell was never written.</returns>
        private static double Value(Table table, Row row, string columnName)
        {
            Assert.True(table.TryGetColumn(columnName, out Column? column_Table), $"column {columnName} is missing from the table");
            Assert.NotNull(column_Table);
            Assert.NotNull(row);

            return row.GetValue<float>(column_Table.Index, 0f);
        }

        /// <summary>
        /// Reads the closing tolerance the method under test wrote into a table cell, addressed by the column's name.
        /// </summary>
        /// <param name="table">The table the row belongs to.</param>
        /// <param name="row">The row to read.</param>
        /// <param name="columnName">The name of the closing tolerance column.</param>
        /// <returns>The cell's value, or null when the cell was never written - the open-envelope signal, distinct from any tolerance.</returns>
        private static float? ClosingToleranceValue(Table table, Row row, string columnName)
        {
            Assert.True(table.TryGetColumn(columnName, out Column? column_Table), $"column {columnName} is missing from the table");
            Assert.NotNull(column_Table);
            Assert.NotNull(row);

            return row.GetValue<float?>(column_Table.Index);
        }

        /// <summary>
        /// Verifies that the four walls of a closed box are each filed under the sector their outward normal's azimuth belongs to, and that nothing leaks across kinds.
        /// <para>The box's walls face north, east, south and west, so the four cardinal sectors are populated and the four diagonal sectors stay empty; a leak into a diagonal or a roof bucket would mean the classification is reading a normal wrong.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_WallSectors()
        {
            BuildingModel model = Model("ref_walls", 5, [.. Box()]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model]);

            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            // One 10 x 10 wall in each cardinal sector, the diagonals empty.
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[0]), 1e-3);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[1]), 1e-9);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[2]), 1e-3);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[3]), 1e-9);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[4]), 1e-3);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[5]), 1e-9);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[6]), 1e-3);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[7]), 1e-9);

            // The flat roof and the floor carry their own 10 x 10 areas; the tilted bands stay empty.
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[8]), 1e-3);
            for (int i = 9; i <= 32; i++)
            {
                Assert.Equal(0.0, Value(table, row, ColumnNames_External[i]), 1e-9);
            }

            Assert.Equal(100.0, Value(table, row, ColumnNames_External[33]), 1e-3);
            Assert.Equal(600.0, Value(table, row, ColumnNames_External[34]), 1e-3);
        }

        /// <summary>
        /// Verifies that a wall stored with its inward normal is filed under the sector of its true outward direction - the regression the issue names.
        /// <para>The west wall of the box is stored pointing into the volume (+x); the shell's external-side orientation re-points it outward (-x), so it lands in the west sector rather than the east one the stored direction would imply. A wall that landed under its stored (inward) direction would be a 180° error.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_InwardStoredNormalLandsOutward()
        {
            BuildingModel model = Model("ref_inward", 5, [.. Box(westStoredInward: true, floorStoredInward: true)]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model]);

            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            // The west wall is filed under west (its true outward sector), not east (its stored direction) -
            // had its stored direction been kept, east would carry the two walls' 200 and west would be empty.
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[6]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[2]), 1e-3);

            // The other two walls and the floor and roof classify as before.
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[0]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[4]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[8]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[33]), 1e-3);
            Assert.Equal(600.0, Value(table, row, ColumnNames_External[34]), 1e-3);
        }

        /// <summary>
        /// Verifies that a wall stored with its inward normal is filed under the sector of its true outward direction even where the building's interior point sits on the other side of the wall - the acceptance case of the issue.
        /// <para>The L-shaped volume's floor interior point falls in the long strip, across the strip-north wall from that wall's outward side. The wall is stored pointing into the volume (-y); the shell's external-side orientation re-points it outward (+y), so it lands in the north sector. The pre-fix interior-point heuristic reads the stored direction as already outward and files the 150 m² wall under south instead - this fact fails on the pre-fix code and passes on this one.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_InwardStoredNormalOnConcaveFootprintLandsOutward()
        {
            BuildingModel model = Model("ref_inward_concave", 5, [.. LShape(true)]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model]);

            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            // The strip-north wall (150, stored inward -y) is filed under north with the north wall's 50 - not under south.
            Assert.Equal(200.0, Value(table, row, ColumnNames_External[0]), 1e-3);
            Assert.Equal(200.0, Value(table, row, ColumnNames_External[4]), 1e-3);

            // The notch wall (150) and the east wall (50) land under east; the wide south and west walls under their own sectors.
            Assert.Equal(200.0, Value(table, row, ColumnNames_External[2]), 1e-3);
            Assert.Equal(200.0, Value(table, row, ColumnNames_External[6]), 1e-3);

            // The diagonal sectors stay empty.
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[1]), 1e-9);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[3]), 1e-9);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[5]), 1e-9);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[7]), 1e-9);

            // The L-shaped flat roof and floor carry their 175 m²; the tilted bands stay empty.
            Assert.Equal(175.0, Value(table, row, ColumnNames_External[8]), 1e-3);
            for (int i = 9; i <= 32; i++)
            {
                Assert.Equal(0.0, Value(table, row, ColumnNames_External[i]), 1e-9);
            }

            Assert.Equal(175.0, Value(table, row, ColumnNames_External[33]), 1e-3);
            Assert.Equal(1150.0, Value(table, row, ColumnNames_External[34]), 1e-3);
        }

        /// <summary>
        /// Verifies that a roof whose normal is flat enough is filed under the flat column, and that a tilted roof is filed under its tilt band crossed with its sector.
        /// <para>The flat band has no directional split, so a near-flat roof must not leak into any tilted column; a 10° roof facing east lands in the east "up to 20" band and no other band or sector.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_RoofTiltBands()
        {
            // A box whose flat roof is replaced by a single 10° roof plane facing east (azimuth 90°).
            IComponent[] components = Box();
            components[5] = Roof(new Point3D(0, 0, 10), new Vector3D(Math.Sin(Math.PI / 180.0 * 90.0) * Math.Sin(Math.PI / 180.0 * 10.0), Math.Cos(Math.PI / 180.0 * 90.0) * Math.Sin(Math.PI / 180.0 * 10.0), Math.Cos(Math.PI / 180.0 * 10.0)), 10);

            BuildingModel model = Model("ref_roofs", 5, components);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model]);

            Assert.Equal(0, result.SkippedComponentCount);

            // A tilted plane over the walls' flat tops leaves the wedge at each wall head open, so this fixture is a genuinely open envelope
            // and the signal records it - the classification under test is unaffected, which is the point of counting rather than failing.
            Assert.Equal(1, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            // The 10° east roof is in the east "up to 20" band and nowhere else.
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[11]), 1e-3);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[8]), 1e-9);

            // Every other tilted band and the other sectors' bands stay empty.
            for (int i = 9; i <= 32; i++)
            {
                if (i != 11)
                {
                    Assert.Equal(0.0, Value(table, row, ColumnNames_External[i]), 1e-9);
                }
            }

            // The walls and floor classify as before.
            Assert.Equal(400.0, Value(table, row, ColumnNames_External[0]) + Value(table, row, ColumnNames_External[2]) + Value(table, row, ColumnNames_External[4]) + Value(table, row, ColumnNames_External[6]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[33]), 1e-3);
            Assert.Equal(600.0, Value(table, row, ColumnNames_External[34]), 1e-3);
        }

        /// <summary>
        /// Verifies that the total column equals the sum of the 34 breakdown columns it is derived from, for a model that populates walls, a roof and a floor at once.
        /// <para>The total is computed from the same accumulators as the breakdowns, so the equality must hold on the stored values - a total that drifted from the sum would mean the two were measured separately and can disagree.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_TotalEqualsSumOfBreakdowns()
        {
            BuildingModel model = Model("ref_total", 5, [.. Box()]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model]);

            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            double sumOfBreakdowns = 0;
            for (int i = 0; i < ColumnNames_External.Length - 1; i++)
            {
                sumOfBreakdowns += Value(table, row, ColumnNames_External[i]);
            }

            double total = Value(table, row, ColumnNames_External[^1]);
            Assert.Equal(sumOfBreakdowns, total, 3);
            Assert.Equal(600.0, total, 1e-3);
        }

        /// <summary>
        /// Verifies that a wall bounding two spaces is an internal partition - excluded from every area bucket and counted in the returned skip count - while each space's own external walls still classify.
        /// <para>Two boxes share the wall between them; that shared wall must not contribute to any external area, and the method must report it as one skipped component. The eight outer walls (four per box) and the two floors and two roofs still land in their buckets.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_SharedComponentIsInternalPartition()
        {
            const double s = 10.0;

            DiGi.Analytical.Building.Classes.BuildingModel buildingModel = new();
            DiGi.Analytical.Building.Classes.Space space_West = new(new Point3D(-5, 5, 5), "Space west");
            DiGi.Analytical.Building.Classes.Space space_East = new(new Point3D(5, 5, 5), "Space east");            Assert.True(buildingModel.Update(space_West));
            Assert.True(buildingModel.Update(space_East));

            // The shared wall between the two boxes, at x = 0.
            DiGi.Analytical.Building.Classes.SurfaceWall sharedWall = Wall(new Point3D(0, 0, s), new Vector3D(1, 0, 0), s);

            // Space west: x in [-10, 0]. Its own walls: x = -10 (outward -x), y = 0 and y = 10.
            // The floor plane's normal (0, 0, -1) runs its local y axis along -y, so the origin corner that spans y in [0, s]
            // is (x, s, 0) - a floor given (x, 0, 0) would sit at y in [-s, 0] and leave the solid open, which no area assertion can see.
            Assert.True(buildingModel.Assign(Wall(new Point3D(-s, 0, s), new Vector3D(-1, 0, 0), s), space_West));
            Assert.True(buildingModel.Assign(Wall(new Point3D(0, 0, s), new Vector3D(0, -1, 0), s), space_West));
            Assert.True(buildingModel.Assign(Wall(new Point3D(-s, s, s), new Vector3D(0, 1, 0), s), space_West));
            Assert.True(buildingModel.Assign(Floor(new Point3D(-s, s, 0), new Vector3D(0, 0, -1), s), space_West));
            Assert.True(buildingModel.Assign(Roof(new Point3D(-s, 0, s), new Vector3D(0, 0, 1), s), space_West));

            // Space east: x in [0, 10]. Its own walls: x = 10 (outward +x), y = 0 and y = 10.
            Assert.True(buildingModel.Assign(Wall(new Point3D(s, s, s), new Vector3D(1, 0, 0), s), space_East));
            Assert.True(buildingModel.Assign(Wall(new Point3D(s, 0, s), new Vector3D(0, -1, 0), s), space_East));
            Assert.True(buildingModel.Assign(Wall(new Point3D(0, s, s), new Vector3D(0, 1, 0), s), space_East));
            Assert.True(buildingModel.Assign(Floor(new Point3D(0, s, 0), new Vector3D(0, 0, -1), s), space_East));
            Assert.True(buildingModel.Assign(Roof(new Point3D(0, 0, s), new Vector3D(0, 0, 1), s), space_East));

            // The shared wall bounds both spaces in one call.
            Assert.True(buildingModel.Assign(sharedWall, space_West, space_East));

            Assert.True(buildingModel.SetValue(BuildingModelParameter.Reference, "ref_shared", new SetValueSettings(true, false)));
            BuildingModel? envelope = buildingModel.ToPostgreSQL(5);
            Assert.NotNull(envelope);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [envelope!]);

            // The one shared wall is the single skipped component, and the two boxes' outer faces tessellate one watertight prism,
            // so the envelope closes at the finest rung and nothing is counted as open.
            Assert.Equal(1, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            // Eight outer walls (four per box) at 100 each: north, south, east, west sectors populated.
            // West box contributes west (x=-10) + its north + south; east box contributes east (x=10) + its north + south.
            double wallTotal = Value(table, row, ColumnNames_External[0]) + Value(table, row, ColumnNames_External[2]) + Value(table, row, ColumnNames_External[4]) + Value(table, row, ColumnNames_External[6]);
            Assert.Equal(600.0, wallTotal, 1e-3);

            // The shared wall must not have leaked into any sector.
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[1]), 1e-9);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[3]), 1e-9);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[5]), 1e-9);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[7]), 1e-9);

            // Two floors and two roofs at 100 each.
            Assert.Equal(200.0, Value(table, row, ColumnNames_External[8]), 1e-3);
            Assert.Equal(200.0, Value(table, row, ColumnNames_External[33]), 1e-3);
            Assert.Equal(1000.0, Value(table, row, ColumnNames_External[34]), 1e-3);
        }

        /// <summary>
        /// Verifies that a component that bounds no space fails its model - recorded with a reason naming the building, and no row - rather than being silently skipped or mis-classified, and that the failure costs that model alone.
        /// <para>A closed box plus an unassigned wall: the wall bounds no space, so the external envelope leaves it out and it cannot be classified. The presence of the box's own (assigned) components does not save the model. A valid box classified in the same call still gets its row, which is what keeps one defective building from dropping the rest of its batch.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_UncoveredComponentFailsModel()
        {
            IComponent[] boxComponents = Box();
            DiGi.Analytical.Building.Classes.SurfaceWall orphanWall = Wall(new Point3D(50, 0, 0), new Vector3D(1, 0, 0), 10);

            DiGi.Analytical.Building.Classes.BuildingModel buildingModel = new();
            DiGi.Analytical.Building.Classes.Space space = new(new Point3D(5, 5, 5), "Space 1");
            Assert.True(buildingModel.Update(space));
            foreach (IComponent component in boxComponents)
            {
                Assert.True(buildingModel.Assign(component, space));
            }

            // The orphan wall is added to the model but assigned to no space.
            Assert.True(buildingModel.Update(orphanWall));

            Assert.True(buildingModel.SetValue(BuildingModelParameter.Reference, "ref_orphan", new SetValueSettings(true, false)));
            BuildingModel? envelope = buildingModel.ToPostgreSQL(5);
            Assert.NotNull(envelope);

            BuildingModel model_Valid = Model("ref_valid", 5, [.. Box()]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [envelope!, model_Valid]);

            Assert.Equal(1, result.FailedModelCount);
            Assert.Equal(0, result.DegenerateModelCount);
            Assert.Equal(0, result.SkippedComponentCount);

            List<string>? references_Failed = result.FailedReferences;
            Assert.NotNull(references_Failed);
            string failure = Assert.Single(references_Failed);
            Assert.StartsWith("ref_orphan: ", failure);
            Assert.Contains("wall", failure);

            // Only the valid box is written; the failed model leaves no partial row.
            Assert.Equal(1, table.RowCount);
            Row? row = table.GetRow(0);
            Assert.NotNull(row);
            Assert.Equal("ref_valid", row.GetValue<string>(table.GetColumnIndex("Reference"), string.Empty));
            Assert.Equal(600.0, Value(table, row, ColumnNames_External[34]), 1e-3);
        }

        /// <summary>
        /// Verifies that when the same building arrives with several stored model versions, the first one - the latest, the converter's order of preference - is the one written, and a different building still gets its own row.
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_LatestModelWins()
        {
            BuildingModel model_First = Model("ref_latest", 5, [.. Box()]);
            BuildingModel model_Second = Model("ref_latest", 5, [.. Box(westStoredInward: true)]);
            BuildingModel model_Other = Model("ref_other", 5, [.. Box()]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model_First, model_Second, model_Other]);

            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Assert.Equal(2, table.RowCount);

            int index_Reference = table.GetColumnIndex("Reference");
            Assert.True(index_Reference >= 0);

            Row? row_First = null;
            Row? row_Other = null;

            for (int i = 0; i < table.RowCount; i++)
            {
                Row? row_Temp = table.GetRow(i);
                Assert.NotNull(row_Temp);

                string? reference = row_Temp.GetValue<string>(index_Reference, string.Empty);

                if (reference == "ref_latest")
                {
                    row_First = row_Temp;
                }
                else if (reference == "ref_other")
                {
                    row_Other = row_Temp;
                }
            }

            Assert.NotNull(row_First);
            Assert.NotNull(row_Other);

            // Both models are identical boxes, so the first record's areas are what must be in the row.
            Assert.Equal(600.0, Value(table, row_First!, ColumnNames_External[34]), 1e-3);
            Assert.Equal(600.0, Value(table, row_Other!, ColumnNames_External[34]), 1e-3);
        }

        /// <summary>
        /// Verifies that a building with a stored model gets a row with zeros in its empty buckets, while envelopes the method cannot address get no row at all.
        /// <para>The zero-versus-null distinction is what lets a reader tell "the building has a stored model" from "the building has no stored model". Envelopes with a blank reference or a missing county identifier are skipped before anything is read from them.</para>
        /// <para>A model with a space but no components is the degenerate case on the other side: it gets its row with every bucket zero and a null closing tolerance, which means "no envelope" rather than "open", so it is not counted as an open envelope either.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_ZeroVsNull()
        {
            BuildingModel model_Box = Model("ref_zeros", 5, [.. Box()]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model_Box]);

            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            // The diagonal wall sectors are present and zero, which is what distinguishes a populated row from an absent one.
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[1]), 1e-9);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[3]), 1e-9);

            double floorArea = Value(table, row, ColumnNames_External[33]);
            Assert.True(floorArea > 0);

            // The total is the four walls' and the roof's 500 on top of the floor's own area.
            Assert.Equal(floorArea, Value(table, row, ColumnNames_External[34]) - 500.0, 1e-3);

            // Envelopes the method cannot address are stepped over without a row and without a skip count.
            BuildingModel envelope_BlankReference = new() { Reference = "   ", CountyId = 5 };
            BuildingModel envelope_MissingCounty = new() { Reference = "ref_orphan", CountyId = null };
            BuildingModel model_Valid = Model("ref_valid", 5, [.. Box()]);

            Table table_Skipped = new();
            ExternalComponentsAreaResult result_Skipped = Modify.Update_ExternalComponentsArea(table_Skipped, [envelope_BlankReference, envelope_MissingCounty, model_Valid]);

            Assert.Equal(0, result_Skipped.SkippedComponentCount);

            // An envelope the method cannot address is not an open envelope either: no shell was built for it, so it is not counted.
            Assert.Equal(0, result_Skipped.OpenEnvelopeCount);
            Assert.Equal(1, table_Skipped.RowCount);

            // A model that carries no components at all still gets its row (all buckets zero), and its null closing tolerance means
            // "no envelope", not "open" - so it too is not counted. The column and the count are the signal a reader acts on.
            BuildingModel model_NoComponents = Model("ref_no_components", 5);

            Table table_NoComponents = new();
            ExternalComponentsAreaResult result_NoComponents = Modify.Update_ExternalComponentsArea(table_NoComponents, [model_NoComponents]);

            Assert.Equal(0, result_NoComponents.SkippedComponentCount);
            Assert.Equal(0, result_NoComponents.OpenEnvelopeCount);
            Assert.Equal(1, table_NoComponents.RowCount);

            Row? row_NoComponents = table_NoComponents.GetRow(0);
            Assert.NotNull(row_NoComponents);

            Assert.Null(ClosingToleranceValue(table_NoComponents, row_NoComponents!, ColumnName_ClosingTolerance));
            for (int i = 0; i < ColumnNames_External.Length; i++)
            {
                Assert.Equal(0.0, Value(table_NoComponents, row_NoComponents, ColumnNames_External[i]), 1e-9);
            }
        }

        /// <summary>
        /// Verifies that a model whose external envelope exists but does not close is still classified, carries a null closing tolerance, and is counted in the result - the open-envelope signal of the row.
        /// <para>The fixture is a box without its roof: four walls and a floor, five faces - enough for the envelope to exist, open along the missing roof. Ray parity over an open face set decides the side of a face whose ray leaves through the gap arbitrarily, so the row's sector values may rest on an arbitrary side; the null closing tolerance and the open-envelope count are the signal a reader acts on, and null - not a sentinel zero - is what distinguishes it from a tolerance.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_OpenEnvelopeSignal()
        {
            // The closed box minus its roof: five faces, an envelope that exists and cannot close at any rung of the ladder.
            IComponent[] boxComponents = Box();
            BuildingModel model = Model("ref_open_envelope", 5, boxComponents[0], boxComponents[1], boxComponents[2], boxComponents[3], boxComponents[4]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model]);

            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(1, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            // The signal: a cell left unwritten reads null, which no rung of the ladder can be confused with.
            Assert.Null(ClosingToleranceValue(table, row!, ColumnName_ClosingTolerance));

            // The model is counted, not failed: its four walls and floor still classify, and the roof column stays zero.
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[0]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[2]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[4]), 1e-3);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[6]), 1e-3);
            Assert.Equal(0.0, Value(table, row, ColumnNames_External[8]), 1e-9);
            Assert.Equal(100.0, Value(table, row, ColumnNames_External[33]), 1e-3);
        }

        /// <summary>
        /// Verifies that a model whose external envelope closes records the finest ladder rung it closes at, and is not counted as open.
        /// <para>An exact box edge-pairs shut at the finest rung of the ladder - the canonical Distance tolerance, which the geometry facts prove directly on the polyhedron; this fact proves that value reaches the row.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_ClosedEnvelopeClosingTolerance()
        {
            BuildingModel model = Model("ref_closed_envelope", 5, [.. Box()]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model]);

            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);
            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            // An exact box closes at the finest rung of the ladder: the canonical Distance tolerance, stored as the float it was written as.
            Assert.Equal((float)DiGi.Core.Constants.Tolerance.Distance, ClosingToleranceValue(table, row!, ColumnName_ClosingTolerance));
        }

        /// <summary>
        /// Verifies that the calculated height of a row is the Z extent of the stored model's bounding box, in meters, and that no component geometry leaves it null rather than 0 (DiGi-Organization/DiGi.GIS.IO#20).
        /// <para>The fixture box spans z from 0 to 10, so its height is 10 whether or not its envelope closes: the open box without its roof still reaches z = 10 through its walls. A model with a space but no components still gets its row and has no bounding box, so its height cell stays unset - a null, distinct from a height of 0. A degenerate model gets no row, so no height. A second call over the same table overwrites the cell and adds no row.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_CalculatedHeight()
        {
            BuildingModel model_Box = Model("ref_height", 5, [.. Box()]);

            Table table = new();
            Modify.Update_ExternalComponentsArea(table, [model_Box]);

            Assert.Equal(1, table.RowCount);

            Row? row = table.GetRow(0);
            Assert.NotNull(row);

            Assert.Equal(10.0f, ClosingToleranceValue(table, row!, ColumnName_CalculatedHeight)!.Value, 1e-3f);

            // The open envelope (no roof) is still classified and still reaches the full wall height.
            IComponent[] boxComponents = Box();
            BuildingModel model_Open = Model("ref_height_open", 5, boxComponents[0], boxComponents[1], boxComponents[2], boxComponents[3], boxComponents[4]);

            Table table_Open = new();
            Modify.Update_ExternalComponentsArea(table_Open, [model_Open]);

            Row? row_Open = table_Open.GetRow(0);
            Assert.NotNull(row_Open);
            Assert.Equal(10.0f, ClosingToleranceValue(table_Open, row_Open!, ColumnName_CalculatedHeight)!.Value, 1e-3f);

            // No component geometry: the row exists and the height is null, never 0.
            BuildingModel model_NoComponents = Model("ref_height_none", 5);

            Table table_NoComponents = new();
            Modify.Update_ExternalComponentsArea(table_NoComponents, [model_NoComponents]);

            Assert.Equal(1, table_NoComponents.RowCount);

            Row? row_NoComponents = table_NoComponents.GetRow(0);
            Assert.NotNull(row_NoComponents);
            Assert.Null(ClosingToleranceValue(table_NoComponents, row_NoComponents!, ColumnName_CalculatedHeight));

            // A degenerate model (three faces) gets no row, so no height.
            BuildingModel model_Degenerate = Model("ref_height_degenerate", 5, boxComponents[0], boxComponents[1], boxComponents[4]);

            Table table_Degenerate = new();
            Modify.Update_ExternalComponentsArea(table_Degenerate, [model_Degenerate]);

            Assert.Equal(0, table_Degenerate.RowCount);

            // A second call over the same table overwrites the cell and adds no row.
            Modify.Update_ExternalComponentsArea(table, [model_Box]);

            Assert.Equal(1, table.RowCount);
            Assert.Equal(10.0f, ClosingToleranceValue(table, table.GetRow(0)!, ColumnName_CalculatedHeight)!.Value, 1e-3f);
        }

        /// <summary>
        /// Verifies that a model whose external components are too few to close an envelope is degenerate - listed in the result and given no row - rather than classified from an open face set or failed, and that it does not stop the other models of the call.
        /// <para>One space with three assigned components - a floor and two walls - yields no external envelope, since a closed solid needs at least four faces, so no component has an outward normal to be classified by. This is the shape of the sliver buildings of the 2026-09-24 production run (one space, three walls, no roof or floor), each of which failed a whole batch and the rest of its county while the method still threw for it.</para>
        /// </summary>
        [Fact]
        public void Update_ExternalComponentsArea_EnvelopeBelowFourFacesIsDegenerate()
        {
            IComponent[] boxComponents = Box();

            // The west and east walls and the floor of the box; the south and north walls and the roof are left out.
            BuildingModel model = Model("ref_three_faces", 5, boxComponents[0], boxComponents[1], boxComponents[4]);
            BuildingModel model_Valid = Model("ref_valid", 5, [.. Box()]);

            Table table = new();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, [model, model_Valid]);

            Assert.Equal(1, result.DegenerateModelCount);
            Assert.Equal(0, result.FailedModelCount);
            Assert.Equal(0, result.SkippedComponentCount);
            Assert.Equal(0, result.OpenEnvelopeCount);

            List<string>? references_Degenerate = result.DegenerateReferences;
            Assert.NotNull(references_Degenerate);
            Assert.Equal("ref_three_faces", Assert.Single(references_Degenerate));

            // Only the valid box is written.
            Assert.Equal(1, table.RowCount);
            Row? row = table.GetRow(0);
            Assert.NotNull(row);
            Assert.Equal("ref_valid", row.GetValue<string>(table.GetColumnIndex("Reference"), string.Empty));
        }

        /// <summary>
        /// Verifies that the classification holds for the currently deployed <c>BuildingModel</c>s saved in PostgreSQL - the development database behind <c>GIS_PostgreSQL_Storage.conf</c>.
        /// <para>This is the end-to-end proof the shell contract is sound on real data: every stored component must be matched to a shell face, and the total must equal the sum of the 34 breakdowns in every row. A single throw or a non-zero skip count here is a data finding to report, not a test to loosen.</para>
        /// <para>The fact is read-only: it classifies the models into an in-memory table and checks the result, but never writes to the database. Skipped by default, because it needs the conf pointing at a database.</para>
        /// </summary>
        [Fact(Skip = "Runs against the deployed BuildingModels. Point GIS_PostgreSQL_Storage.conf at a database before running.")]
        public async Task Update_ExternalComponentsArea_DeployedBuildingModels()
        {
            const int commandTimeout = 600;

            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager();
            Assert.NotNull(gISPostgreSQLConverterManager);

            BuildingModelPostgreSQLConverter? buildingModelPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<BuildingModelPostgreSQLConverter>();
            Assert.NotNull(buildingModelPostgreSQLConverter);

            AdministrativeAreal2DPostgreSQLConverter? administrativeAreal2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<AdministrativeAreal2DPostgreSQLConverter>();
            Assert.NotNull(administrativeAreal2DPostgreSQLConverter);

            List<AdministrativeAreal2DReference>? countyReferences = await administrativeAreal2DPostgreSQLConverter.GetAdministrativeAreal2DReferencesByAdministrativeArealTypeAsync(AdministrativeArealType.County, commandTimeout: commandTimeout);
            Assert.NotNull(countyReferences);

            int? countyId = null;

            foreach (AdministrativeAreal2DReference? countyReference in countyReferences)
            {
                if (countyReference is null)
                {
                    continue;
                }

                HashSet<string>? references_County = await buildingModelPostgreSQLConverter.GetReferencesAsync(countyReference.Id, commandTimeout: commandTimeout);

                if (references_County is not null && references_County.Count > 0)
                {
                    countyId = countyReference.Id;
                    break;
                }
            }

            Assert.NotNull(countyId);

            HashSet<string>? references = await buildingModelPostgreSQLConverter.GetReferencesAsync(countyId, commandTimeout: commandTimeout);
            Assert.NotNull(references);
            Assert.True(references.Count > 0);

            List<BuildingModel>? models = await buildingModelPostgreSQLConverter.GetItemsByReferencesAsync([.. references], countyId, fallbackByReference: false, commandTimeout: commandTimeout);
            Assert.NotNull(models);
            Assert.True(models.Count > 0);

            HashSet<string> references_Seen = [];
            List<BuildingModel> models_Latest = [];

            foreach (BuildingModel? model in models)
            {
                if (model is null || model.Reference is not string reference_Model || string.IsNullOrWhiteSpace(reference_Model))
                {
                    continue;
                }

                if (references_Seen.Add(reference_Model))
                {
                    models_Latest.Add(model);
                }
            }

            Assert.True(models_Latest.Count > 0);

            Table table = new();

            // The ladder bisects IsClosed per model, so the classification time is the cost measurement the issue asks for.
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ExternalComponentsAreaResult result = Modify.Update_ExternalComponentsArea(table, models_Latest);
            stopwatch.Stop();

            Assert.True(table.RowCount > 0);

            // The rungs of the production ladder as the floats they are stored as, for telling a written tolerance from a foreign value.
            float[] rungs_Closing = [(float)DiGi.Core.Constants.Tolerance.Distance, 1e-5f, 1e-4f, (float)DiGi.Core.Constants.Tolerance.MacroDistance, 0.01f, 0.02f, 0.05f, 0.1f, 0.2f];

            int openCount = 0;
            int noEnvelopeCount = 0;
            Dictionary<string, int> histogram_ClosingTolerance = [];

            for (int i = 0; i < table.RowCount; i++)
            {
                Row? row = table.GetRow(i);
                Assert.NotNull(row);

                double sumOfBreakdowns = 0;
                for (int j = 0; j < ColumnNames_External.Length - 1; j++)
                {
                    sumOfBreakdowns += Value(table, row, ColumnNames_External[j]);
                }

                double total = Value(table, row, ColumnNames_External[^1]);
                double tolerance = Math.Max(0.01, 1e-5 * Math.Abs(total));
                Assert.InRange(Math.Abs(total - sumOfBreakdowns), 0, tolerance);

                float? closingTolerance = ClosingToleranceValue(table, row, ColumnName_ClosingTolerance);
                if (closingTolerance is null)
                {
                    // A row that carries areas but closes at no rung is the counted open envelope. A null tolerance on a zero row is
                    // "no envelope" (no components, or only internal partitions), which is deliberately not an open envelope.
                    if (total > 0)
                    {
                        openCount++;
                    }
                    else
                    {
                        noEnvelopeCount++;
                    }
                }
                else
                {
                    Assert.Contains(closingTolerance.Value, rungs_Closing);

                    string rung = closingTolerance.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    histogram_ClosingTolerance[rung] = histogram_ClosingTolerance.TryGetValue(rung, out int count) ? count + 1 : 1;
                }
            }

            // The counted opens are exactly the rows the classification filled but could not close - the count and the column are one signal.
            Assert.Equal(result.OpenEnvelopeCount, openCount);

            // A non-zero skip count on deployed data is a finding to report before deciding whether the decision table needs a case for it.
            Assert.Equal(0, result.SkippedComponentCount);

            // A refused model no longer throws, so the failure list is where a defect on deployed data surfaces now.
            Assert.Equal(0, result.FailedModelCount);

            List<string> reportLines = [$"Update_ExternalComponentsArea_DeployedBuildingModels: {models_Latest.Count} models classified in {stopwatch.ElapsedMilliseconds} ms"];
            reportLines.Add($"open envelopes (null closing tolerance on a row carrying areas): {openCount} of {table.RowCount}");
            reportLines.Add($"rows with no envelope (null closing tolerance, no areas): {noEnvelopeCount}");
            foreach (KeyValuePair<string, int> pair in histogram_ClosingTolerance)
            {
                reportLines.Add($"closing tolerance {pair.Key} m: {pair.Value} rows");
            }

            string? pathReportsDirectory = Core.xUnit.Query.ReportsDirectory(System.Reflection.Assembly.GetExecutingAssembly());
            Assert.False(string.IsNullOrWhiteSpace(pathReportsDirectory));

            System.IO.File.WriteAllLines(System.IO.Path.Combine(pathReportsDirectory!, "Update_ExternalComponentsArea_DeployedBuildingModels.txt"), reportLines);
        }
    }
}
