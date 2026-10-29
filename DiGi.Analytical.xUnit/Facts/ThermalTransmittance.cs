using DiGi.Analytical.Building;
using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Classes;
using DiGi.Analytical.Interfaces;
using DiGi.Geometry.Planar.Classes;
using DiGi.Geometry.Planar.Interfaces;
using DiGi.Geometry.Spatial.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the PN-EN ISO 6946 surface and air-layer resistances (DiGi-Organization/DiGi.Analytical#8): Rsi 0.10 / 0.13 / 0.17 by heat flow direction and Rse 0.04; the tabulated unventilated air-layer values, linear interpolation between them, and no value above 0.3 m or for an undefined direction.
        /// </summary>
        [Fact]
        public void Query_SurfaceResistance_AirLayerThermalResistance()
        {
            Assert.Equal(0.10, HeatFlowDirection.Upward.SurfaceResistance(true));
            Assert.Equal(0.13, HeatFlowDirection.Horizontal.SurfaceResistance(true));
            Assert.Equal(0.17, HeatFlowDirection.Downward.SurfaceResistance(true));
            Assert.Equal(0.04, HeatFlowDirection.Upward.SurfaceResistance(false));
            Assert.Equal(0.04, HeatFlowDirection.Downward.SurfaceResistance(false));
            Assert.True(double.IsNaN(HeatFlowDirection.Undefined.SurfaceResistance(true)));

            Assert.Equal(0.18, Building.Query.AirLayerThermalResistance(0.025, HeatFlowDirection.Horizontal), 12);
            Assert.Equal(0.16, Building.Query.AirLayerThermalResistance(0.025, HeatFlowDirection.Upward), 12);
            Assert.Equal(0.19, Building.Query.AirLayerThermalResistance(0.025, HeatFlowDirection.Downward), 12);
            Assert.Equal(0.23, Building.Query.AirLayerThermalResistance(0.3, HeatFlowDirection.Downward), 12);
            Assert.Equal(0.175, Building.Query.AirLayerThermalResistance(0.020, HeatFlowDirection.Horizontal), 12);
            Assert.Equal(0.0, Building.Query.AirLayerThermalResistance(0.0, HeatFlowDirection.Horizontal), 12);
            Assert.True(double.IsNaN(Building.Query.AirLayerThermalResistance(0.35, HeatFlowDirection.Horizontal)));
            Assert.True(double.IsNaN(Building.Query.AirLayerThermalResistance(-0.01, HeatFlowDirection.Horizontal)));
            Assert.True(double.IsNaN(Building.Query.AirLayerThermalResistance(0.025, HeatFlowDirection.Undefined)));
        }

        /// <summary>
        /// Tests the PN-EN ISO 6946 thermal transmittance of layered constructions (DiGi-Organization/DiGi.Analytical#8).
        /// <para>A 0.45 m full brick wall (λ 0.77) with 15 mm lime plaster (λ 0.70) inside: U = 1 / (0.13 + 0.015/0.70 + 0.45/0.77 + 0.04) ≈ 1.289 W/(m2K), equal through the cluster and the building model. Each heat flow direction gives its own Rsi, ground contact drops Rse, a 50 mm air cavity takes the tabulated 0.18. A layer with no material or no conductivity gives NaN, never an optimistic U, and a declared U wins over the layers.</para>
        /// </summary>
        [Fact]
        public void Query_ThermalTransmittance_Layers()
        {
            OpaqueMaterial opaqueMaterial_Brick = new("Full brick") { Conductivity = 0.77 };
            OpaqueMaterial opaqueMaterial_Plaster = new("Lime plaster") { Conductivity = 0.70 };
            FluidMaterial fluidMaterial_Air = new("Air") { Conductivity = 0.025 };
            OpaqueMaterial opaqueMaterial_Unknown = new("Unknown");

            WallConstruction wallConstruction = new() { Name = "Full brick 450 mm, plastered inside" };
            WallConstruction wallConstruction_Cavity = new() { Name = "Cavity wall 120 + 50 + 120 mm" };
            WallConstruction wallConstruction_Unknown = new() { Name = "Unknown conductivity" };

            ConstructionRelationCluster constructionRelationCluster = new([opaqueMaterial_Brick, opaqueMaterial_Plaster, fluidMaterial_Air, opaqueMaterial_Unknown, wallConstruction, wallConstruction_Cavity, wallConstruction_Unknown]);
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Brick, 0.45));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Plaster, 0.015));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction_Cavity, opaqueMaterial_Brick, 0.12));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction_Cavity, fluidMaterial_Air, 0.05));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction_Cavity, opaqueMaterial_Brick, 0.12));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction_Unknown, opaqueMaterial_Unknown, 0.25));

            double resistance_Layers = (0.45 / 0.77) + (0.015 / 0.70);
            double thermalTransmittance = constructionRelationCluster.ThermalTransmittance(wallConstruction, HeatFlowDirection.Horizontal);
            Assert.Equal(1.0 / (0.13 + resistance_Layers + 0.04), thermalTransmittance, 12);
            Assert.Equal(1.289, thermalTransmittance, 3);

            // Each direction has its own Rsi; ground contact has no Rse.
            Assert.Equal(1.0 / (0.10 + resistance_Layers + 0.04), constructionRelationCluster.ThermalTransmittance(wallConstruction, HeatFlowDirection.Upward), 12);
            Assert.Equal(1.0 / (0.17 + resistance_Layers + 0.04), constructionRelationCluster.ThermalTransmittance(wallConstruction, HeatFlowDirection.Downward), 12);
            Assert.Equal(1.0 / (0.17 + resistance_Layers), constructionRelationCluster.ThermalTransmittance(wallConstruction, HeatFlowDirection.Downward, true), 12);
            Assert.True(double.IsNaN(constructionRelationCluster.ThermalTransmittance(wallConstruction, HeatFlowDirection.Undefined)));

            // An unventilated air cavity takes the tabulated resistance, not d/λ of the gas.
            Assert.Equal(1.0 / (0.13 + (0.12 / 0.77) + 0.18 + (0.12 / 0.77) + 0.04), constructionRelationCluster.ThermalTransmittance(wallConstruction_Cavity, HeatFlowDirection.Horizontal), 12);

            // Missing data gives NaN.
            Assert.True(double.IsNaN(constructionRelationCluster.ThermalTransmittance(wallConstruction_Unknown, HeatFlowDirection.Horizontal)));
            Assert.True(double.IsNaN(constructionRelationCluster.ThermalTransmittance(new WallConstruction() { Name = "No layers" }, HeatFlowDirection.Horizontal)));
            Assert.True(double.IsNaN(Building.Query.ThermalTransmittance([(new StructureLayer(0, 0.2), (IMaterial?)null)], HeatFlowDirection.Horizontal)));
            Assert.True(double.IsNaN(Building.Query.ThermalTransmittance(null, HeatFlowDirection.Horizontal)));

            // The same U through the building model.
            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Update(constructionRelationCluster, wallConstruction));
            Assert.Equal(thermalTransmittance, buildingModel.ThermalTransmittance(wallConstruction, HeatFlowDirection.Horizontal), 12);

            // A declared U wins over the layers.
            Assert.True(wallConstruction.SetValue(PhysicalComponentConstructionParameter.ThermalTransmittance, 1.16));
            Assert.Equal(1.16, constructionRelationCluster.ThermalTransmittance(wallConstruction, HeatFlowDirection.Horizontal));
            Assert.Equal(1.16, buildingModel.ThermalTransmittance(wallConstruction, HeatFlowDirection.Horizontal));
            Assert.Equal(1.16, ((ConstructionRelationCluster?)null).ThermalTransmittance(wallConstruction, HeatFlowDirection.Horizontal));
        }

        /// <summary>
        /// Tests the window thermal transmittance Uw per PN-EN ISO 10077-1 from an outline whose holes are the glazing (DiGi-Organization/DiGi.Analytical#8).
        /// <para>A 1.2 × 1.5 m window with one 1.0 × 1.3 m pane: Ag 1.3, Af 0.5, lg 4.6 m; with Ug 1.1, Uf 1.3 and Ψg 0.04, Uw = (1.3·1.1 + 0.5·1.3 + 4.6·0.04) / 1.8 = 1.2578. The same frame width on a 0.6 × 0.9 m window gives the higher 1.3593, so the value follows the size. Without a hole or without Ψg the declared Uw is returned, and without any value NaN.</para>
        /// </summary>
        [Fact]
        public void Query_ThermalTransmittance_Window()
        {
            WindowConstruction windowConstruction = new("Window 1200 × 1500", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.GlazingThermalTransmittance, 1.1));
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.FrameThermalTransmittance, 1.3));
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.GlazingLinearThermalTransmittance, 0.04));
            Assert.True(windowConstruction.SetValue(OpeningConstructionParameter.ThermalTransmittance, 1.5));
            Assert.Equal(((1.3 * 1.1) + (0.5 * 1.3) + (4.6 * 0.04)) / 1.8, windowConstruction.ThermalTransmittance(), 12);
            Assert.Equal(1.2578, windowConstruction.ThermalTransmittance(), 4);

            WindowConstruction windowConstruction_Small = new("Window 600 × 900", OpeningFixture_Rectangle(0, 0, 0.6, 0.9, (0.1, 0.1, 0.5, 0.8)));
            Assert.True(windowConstruction_Small.SetValue(OpeningConstructionParameter.GlazingThermalTransmittance, 1.1));
            Assert.True(windowConstruction_Small.SetValue(OpeningConstructionParameter.FrameThermalTransmittance, 1.3));
            Assert.True(windowConstruction_Small.SetValue(OpeningConstructionParameter.GlazingLinearThermalTransmittance, 0.04));
            Assert.Equal(((0.28 * 1.1) + (0.26 * 1.3) + (2.2 * 0.04)) / 0.54, windowConstruction_Small.ThermalTransmittance(), 12);
            Assert.True(windowConstruction_Small.ThermalTransmittance() > windowConstruction.ThermalTransmittance());

            // Without a hole, or without Ψg, the declared Uw is the answer.
            WindowConstruction windowConstruction_Declared = new("Box window", OpeningFixture_Rectangle(0, 0, 1.2, 1.5));
            Assert.True(windowConstruction_Declared.SetValue(OpeningConstructionParameter.GlazingThermalTransmittance, 2.8));
            Assert.True(windowConstruction_Declared.SetValue(OpeningConstructionParameter.FrameThermalTransmittance, 2.0));
            Assert.True(windowConstruction_Declared.SetValue(OpeningConstructionParameter.GlazingLinearThermalTransmittance, 0.0));
            Assert.True(windowConstruction_Declared.SetValue(OpeningConstructionParameter.ThermalTransmittance, 5.0));
            Assert.Equal(5.0, windowConstruction_Declared.ThermalTransmittance());

            WindowConstruction windowConstruction_NoPsi = new("No Ψg", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));
            Assert.True(windowConstruction_NoPsi.SetValue(OpeningConstructionParameter.GlazingThermalTransmittance, 1.1));
            Assert.True(windowConstruction_NoPsi.SetValue(OpeningConstructionParameter.FrameThermalTransmittance, 1.3));
            Assert.True(windowConstruction_NoPsi.SetValue(OpeningConstructionParameter.ThermalTransmittance, 2.6));
            Assert.Equal(2.6, windowConstruction_NoPsi.ThermalTransmittance());

            Assert.True(double.IsNaN(new WindowConstruction("Nothing declared", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4))).ThermalTransmittance()));
            Assert.True(double.IsNaN(((IOpeningConstruction?)null).ThermalTransmittance()));
        }

        /// <summary>
        /// Tests the opening surfaces of an outline whose holes are the glazing (DiGi-Organization/DiGi.Analytical#8): <see cref="Building.Query.Surface3D(IOpening, IOpeningConstruction)"/> still returns the whole opening without holes, as hosting and display need it, while <see cref="Building.Query.Surface3Ds(IOpening, IOpeningConstruction, OpeningConstructionPart)"/> returns the frame face with its hole or one face per pane. An outline without holes is all frame and has no panes.
        /// </summary>
        [Fact]
        public void Query_Surface3Ds_Opening()
        {
            Window window = new(OpeningFixture_Plane(2, 0, 0.5));
            WindowConstruction windowConstruction = new("Window", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));

            IPolygonalFace3D? polygonalFace3D_Outline = window.Surface3D(windowConstruction) as IPolygonalFace3D;
            Assert.NotNull(polygonalFace3D_Outline);
            Assert.True(polygonalFace3D_Outline.InternalEdges is null || polygonalFace3D_Outline.InternalEdges.Count == 0);
            Assert.Equal(1.8, polygonalFace3D_Outline.GetArea(), 9);

            List<ISurface3D>? surface3Ds_Frame = window.Surface3Ds(windowConstruction, OpeningConstructionPart.Frame);
            IPolygonalFace3D? polygonalFace3D_Frame = Assert.Single(surface3Ds_Frame ?? []) as IPolygonalFace3D;
            Assert.NotNull(polygonalFace3D_Frame);
            Assert.Single(polygonalFace3D_Frame.InternalEdges ?? []);
            Assert.Equal(0.5, polygonalFace3D_Frame.GetArea(), 9);

            static double Area(List<ISurface3D>? surface3Ds)
            {
                return Assert.IsAssignableFrom<IPolygonalFace3D>(Assert.Single(surface3Ds ?? [])).GetArea();
            }

            Assert.Equal(1.3, Area(window.Surface3Ds(windowConstruction, OpeningConstructionPart.Pane)), 9);
            Assert.Equal(1.8, Area(window.Surface3Ds(windowConstruction, OpeningConstructionPart.Undefined)), 9);

            WindowConstruction windowConstruction_Plain = OpeningFixture_WindowConstruction(1.2, 1.5);
            Assert.Equal(1.8, Area(window.Surface3Ds(windowConstruction_Plain, OpeningConstructionPart.Frame)), 9);
            List<ISurface3D>? surface3Ds_Plain_Pane = window.Surface3Ds(windowConstruction_Plain, OpeningConstructionPart.Pane);
            Assert.NotNull(surface3Ds_Plain_Pane);
            Assert.Empty(surface3Ds_Plain_Pane);

            Assert.Null(((IOpening?)null).Surface3Ds(windowConstruction, OpeningConstructionPart.Frame));
            Assert.Null(window.Surface3Ds(null, OpeningConstructionPart.Pane));
        }

        /// <summary>
        /// Tests <see cref="Building.Create.WindowConstruction(string, IPolygonal2D, IEnumerable{IPolygonal2D}, double)"/> and <see cref="Building.Create.DoorConstruction(string, IPolygonal2D, IEnumerable{IPolygonal2D}, double)"/> (DiGi-Organization/DiGi.Analytical#8): two panes inside the outline give a face with two holes; a pane outside the outline, two overlapping panes and two touching panes give null, because the planar face factory would silently drop a hole and leave the wrong frame area. A door without panes is opaque.
        /// </summary>
        [Fact]
        public void Create_WindowConstruction()
        {
            static Polygon2D Rectangle(double minX, double minY, double maxX, double maxY)
            {
                return new Polygon2D([new Point2D(minX, minY), new Point2D(maxX, minY), new Point2D(maxX, maxY), new Point2D(minX, maxY)]);
            }

            Polygon2D outline = Rectangle(0, 0, 1.5, 1.5);

            WindowConstruction? windowConstruction = Building.Create.WindowConstruction("Two sashes", outline, [Rectangle(0.1, 0.1, 0.7, 1.4), Rectangle(0.8, 0.1, 1.4, 1.4)]);
            Assert.NotNull(windowConstruction);
            PolygonalFace2D? polygonalFace2D = windowConstruction.Geometry as PolygonalFace2D;
            Assert.NotNull(polygonalFace2D);
            Assert.Equal(2, polygonalFace2D.InternalEdges?.Count);
            Assert.Equal("Two sashes", windowConstruction.Name);

            Assert.Null(Building.Create.WindowConstruction("Pane outside", outline, [Rectangle(1.0, 0.1, 1.7, 1.4)]));
            Assert.Null(Building.Create.WindowConstruction("Overlapping panes", outline, [Rectangle(0.1, 0.1, 0.9, 1.4), Rectangle(0.6, 0.1, 1.4, 1.4)]));
            Assert.Null(Building.Create.WindowConstruction("Touching panes", outline, [Rectangle(0.1, 0.1, 0.75, 1.4), Rectangle(0.75, 0.1, 1.4, 1.4)]));
            Assert.Null(Building.Create.WindowConstruction("Pane inside pane", outline, [Rectangle(0.1, 0.1, 1.4, 1.4), Rectangle(0.3, 0.3, 1.2, 1.2)]));
            Assert.Null(Building.Create.WindowConstruction("No outline", null, [Rectangle(0.1, 0.1, 0.7, 1.4)]));

            DoorConstruction? doorConstruction = Building.Create.DoorConstruction("Opaque door", Rectangle(0, 0, 0.9, 2.0));
            Assert.NotNull(doorConstruction);
            Assert.True(((doorConstruction.Geometry as PolygonalFace2D)?.InternalEdges?.Count ?? 0) == 0);

            DoorConstruction? doorConstruction_Glazed = Building.Create.DoorConstruction("Glazed door", Rectangle(0, 0, 0.9, 2.0), [Rectangle(0.15, 1.0, 0.75, 1.85)]);
            Assert.Single((doorConstruction_Glazed?.Geometry as PolygonalFace2D)?.InternalEdges ?? []);
        }
    }
}
