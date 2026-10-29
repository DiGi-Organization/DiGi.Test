using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Classes;
using DiGi.Analytical.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests that a pane or frame layer created for an opening construction is held by the cluster and returned per part with its material (DiGi-Organization/DiGi.Analytical#8).
        /// <para>The opening overload of <c>Create.StructureLayer</c> used to relate the layer without adding it to the cluster, so the part's layers could never be read back.</para>
        /// </summary>
        [Fact]
        public void ConstructionRelationCluster_OpeningStructureLayer()
        {
            WindowConstruction windowConstruction = new("Window", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));
            TransparentMaterial transparentMaterial = new("Float glass") { Conductivity = 1.0 };
            OpaqueMaterial opaqueMaterial = new("Pine") { Conductivity = 0.13 };

            ConstructionRelationCluster constructionRelationCluster = new([windowConstruction, transparentMaterial, opaqueMaterial]);

            StructureLayer? structureLayer_Pane_1 = Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Pane, transparentMaterial, 0.004);
            StructureLayer? structureLayer_Pane_2 = Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Pane, transparentMaterial, 0.004);
            StructureLayer? structureLayer_Frame = Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Frame, opaqueMaterial, 0.068);
            Assert.NotNull(structureLayer_Pane_1);
            Assert.NotNull(structureLayer_Pane_2);
            Assert.NotNull(structureLayer_Frame);

            List<IStructureLayer>? structureLayers_Pane = constructionRelationCluster.GetStructureLayers(windowConstruction, OpeningConstructionPart.Pane);
            Assert.NotNull(structureLayers_Pane);
            Assert.Equal([0, 1], structureLayers_Pane.ConvertAll(x => x.Index));
            Assert.Equal("Float glass", constructionRelationCluster.GetMaterial(structureLayers_Pane[0])?.Name);

            List<IStructureLayer>? structureLayers_Frame = constructionRelationCluster.GetStructureLayers(windowConstruction, OpeningConstructionPart.Frame);
            Assert.NotNull(structureLayers_Frame);
            Assert.Single(structureLayers_Frame);
            Assert.Equal(0.068, structureLayers_Frame[0].Thickness);
            Assert.Equal("Pine", constructionRelationCluster.GetMaterial(structureLayers_Frame[0])?.Name);

            Assert.Null(constructionRelationCluster.GetStructureLayers(windowConstruction, OpeningConstructionPart.Undefined));
        }

        /// <summary>
        /// Tests that <see cref="ConstructionRelationCluster.GetStructureLayers(IPhysicalComponentConstruction?)"/> returns the layers ordered by <see cref="IStructureLayer.Index"/> - from the outside (index 0) inwards - whatever the order the relations were made in (DiGi-Organization/DiGi.Analytical#8).
        /// </summary>
        [Fact]
        public void ConstructionRelationCluster_StructureLayerOrder()
        {
            WallConstruction wallConstruction = new() { Name = "Wall" };
            OpaqueMaterial opaqueMaterial = new("Brick") { Conductivity = 0.77 };
            ConstructionRelationCluster constructionRelationCluster = new([wallConstruction, opaqueMaterial]);

            int[] indexes = [2, 0, 1];
            foreach (int index in indexes)
            {
                StructureLayer structureLayer = new(index, 0.1 * (index + 1));
                Assert.True(constructionRelationCluster.Add(structureLayer));
                Assert.NotNull(constructionRelationCluster.AddRelation(wallConstruction, structureLayer));
                Assert.NotNull(constructionRelationCluster.AddRelation(opaqueMaterial, structureLayer));
            }

            List<IStructureLayer>? structureLayers = constructionRelationCluster.GetStructureLayers(wallConstruction);
            Assert.NotNull(structureLayers);
            Assert.Equal([0, 1, 2], structureLayers.ConvertAll(x => x.Index));
            Assert.Equal(0.1, structureLayers[0].Thickness, 12);
        }

        /// <summary>
        /// Tests <see cref="BuildingModel.Update(ConstructionRelationCluster?, IConstruction?)"/> (DiGi-Organization/DiGi.Analytical#8): a layered wall construction and a window construction with pane and frame layers arrive in the model with their layers (ordered) and materials, a material shared by two constructions keeps one identifier, and re-importing replaces the layers instead of duplicating them.
        /// <para>The model survives a JSON round trip with the layered constructions (<see cref="Core.xUnit.Query.SerializationCheck"/>).</para>
        /// </summary>
        [Fact]
        public void BuildingModel_UpdateConstruction()
        {
            OpaqueMaterial opaqueMaterial_Plaster = new("Lime plaster") { Conductivity = 0.70 };
            OpaqueMaterial opaqueMaterial_Brick = new("Full brick") { Conductivity = 0.77 };
            TransparentMaterial transparentMaterial_Glass = new("Float glass") { Conductivity = 1.0 };

            WallConstruction wallConstruction = new() { Name = "Full brick 450 mm, plastered" };
            WallConstruction wallConstruction_Party = new() { Name = "Full brick 250 mm, plastered" };
            WindowConstruction windowConstruction = new("Window", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));

            ConstructionRelationCluster constructionRelationCluster = new([opaqueMaterial_Plaster, opaqueMaterial_Brick, transparentMaterial_Glass, wallConstruction, wallConstruction_Party, windowConstruction]);
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Plaster, 0.015));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Brick, 0.45));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial_Plaster, 0.015));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction_Party, opaqueMaterial_Brick, 0.25));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Pane, transparentMaterial_Glass, 0.004));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, windowConstruction, OpeningConstructionPart.Frame, opaqueMaterial_Brick, 0.068));

            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Update(constructionRelationCluster, wallConstruction));
            Assert.True(buildingModel.Update(constructionRelationCluster, wallConstruction_Party));
            Assert.True(buildingModel.Update(constructionRelationCluster, windowConstruction));

            List<IStructureLayer>? structureLayers = buildingModel.GetStructureLayers(wallConstruction);
            Assert.NotNull(structureLayers);
            Assert.Equal([0, 1, 2], structureLayers.ConvertAll(x => x.Index));
            Assert.Equal([0.015, 0.45, 0.015], structureLayers.ConvertAll(x => x.Thickness));
            Assert.Equal(["Lime plaster", "Full brick", "Lime plaster"], structureLayers.ConvertAll(x => buildingModel.GetMaterial(x)?.Name));

            // The brick of both walls and of the window frame is one material, by identifier.
            IMaterial? material_Brick_Wall = buildingModel.GetMaterial(structureLayers[1]);
            IMaterial? material_Brick_Party = buildingModel.GetMaterial(buildingModel.GetStructureLayers(wallConstruction_Party)?.FirstOrDefault());
            Assert.NotNull(material_Brick_Wall);
            Assert.Equal(opaqueMaterial_Brick.Guid, material_Brick_Wall.Guid);
            Assert.Equal(material_Brick_Wall.Guid, material_Brick_Party?.Guid);

            Assert.Equal("Float glass", buildingModel.GetMaterial(buildingModel.GetStructureLayers(windowConstruction, OpeningConstructionPart.Pane)?.Single())?.Name);
            Assert.Equal(0.068, buildingModel.GetStructureLayers(windowConstruction, OpeningConstructionPart.Frame)?.Single().Thickness);

            // The getters return clones.
            Assert.NotSame(structureLayers[0], buildingModel.GetStructureLayers(wallConstruction)?[0]);

            // Re-import replaces: the layers are not duplicated, and a changed material arrives.
            opaqueMaterial_Brick.Conductivity = 0.80;
            Assert.True(constructionRelationCluster.Add(opaqueMaterial_Brick));
            Assert.True(buildingModel.Update(constructionRelationCluster, wallConstruction));
            Assert.True(buildingModel.Update(constructionRelationCluster, windowConstruction));
            Assert.Equal(3, buildingModel.GetStructureLayers(wallConstruction)?.Count);
            Assert.Single(buildingModel.GetStructureLayers(windowConstruction, OpeningConstructionPart.Pane) ?? []);
            OpaqueMaterial? opaqueMaterial_Brick_Updated = buildingModel.GetMaterial(buildingModel.GetStructureLayers(wallConstruction)?[1]) as OpaqueMaterial;
            Assert.Equal(0.80, opaqueMaterial_Brick_Updated?.Conductivity);

            // Nothing to copy from.
            Assert.False(buildingModel.Update(null, wallConstruction));
            Assert.False(buildingModel.Update(constructionRelationCluster, null));

            BuildingModel? buildingModel_Clone = Core.Query.Clone(buildingModel);
            Assert.NotNull(buildingModel_Clone);
            Assert.Equal(3, buildingModel_Clone.GetStructureLayers(wallConstruction)?.Count);
            Assert.Equal("Full brick", buildingModel_Clone.GetMaterial(buildingModel_Clone.GetStructureLayers(wallConstruction)?[1])?.Name);

            Core.xUnit.Query.SerializationCheck(buildingModel);
        }
    }
}
