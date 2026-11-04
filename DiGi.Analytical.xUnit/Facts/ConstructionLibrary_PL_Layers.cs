using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Analytical.Building.Regulation.Enums;
using DiGi.Analytical.Classes;
using DiGi.Analytical.Interfaces;
using DiGi.Core.Parameter.Classes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the material catalogue of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> (DiGi-Organization/DiGi.Analytical#18): every <see cref="OpaqueMaterial"/> has λ, ρ and c above zero and a description naming its norm table and secondary source, or DEFAULT.
        /// <para>The identifiers are derived from stable keys, so a second library build gives the same material identifiers.</para>
        /// </summary>
        [Fact]
        public void ConstructionLibrary_PL_Materials()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();
            List<OpaqueMaterial> opaqueMaterials = constructionLibrary.ConstructionRelationCluster.GetValues<OpaqueMaterial>() ?? [];
            Assert.True(opaqueMaterials.Count >= 15, $"{opaqueMaterials.Count} materials");

            foreach (OpaqueMaterial opaqueMaterial in opaqueMaterials)
            {
                Assert.True(opaqueMaterial.Conductivity > 0, opaqueMaterial.Name);
                Assert.True(opaqueMaterial.Density > 0, opaqueMaterial.Name);
                Assert.True(opaqueMaterial.SpecificHeat > 0, opaqueMaterial.Name);
                Assert.False(string.IsNullOrWhiteSpace(opaqueMaterial.Name));
                string description = opaqueMaterial.Description ?? string.Empty;
                Assert.True(description.Contains("as reproduced in", StringComparison.Ordinal) || description.Contains("DEFAULT", StringComparison.Ordinal), $"{opaqueMaterial.Name}: {description}");
                Assert.Contains("Not checked against the original norm.", description);
            }

            HashSet<Guid> guids = [.. opaqueMaterials.ConvertAll(x => x.Guid)];
            List<OpaqueMaterial> opaqueMaterials_Second = Building.Regulation.Create.ConstructionLibrary_PL().ConstructionRelationCluster.GetValues<OpaqueMaterial>() ?? [];
            Assert.True(guids.SetEquals(opaqueMaterials_Second.ConvertAll(x => x.Guid)));
        }

        /// <summary>
        /// Tests the layered assemblies of the <see cref="ConstructionFlavour.TypicalAsBuilt"/> sets of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> (DiGi-Organization/DiGi.Analytical#18).
        /// <para>Every wall, roof and floor construction carrying a TABULA U has layers, and the PN-EN ISO 6946 U of the layers alone (walls horizontal, roofs upward, floors downward heat flow, no ground contact) is within max(0.02 W/(m2K), 5 %) of the declared U. InternalWall, PartyWall and IntermediateFloor have layers and no declared U.</para>
        /// <para>The fact writes the deviation of every assembly to <c>reports/ConstructionLayers.md</c> for the wiki, and reports every failing assembly at once.</para>
        /// </summary>
        [Fact]
        public void ConstructionLibrary_PL_Layers()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();
            ConstructionRelationCluster constructionRelationCluster = constructionLibrary.ConstructionRelationCluster;

            static HeatFlowDirection Direction(ConstructionRole constructionRole)
            {
                return constructionRole switch
                {
                    ConstructionRole.Roof or ConstructionRole.FlatRoof => HeatFlowDirection.Upward,
                    ConstructionRole.GroundFloor or ConstructionRole.FloorOverUnheated or ConstructionRole.IntermediateFloor => HeatFlowDirection.Downward,
                    _ => HeatFlowDirection.Horizontal,
                };
            }

            double LayersThermalTransmittance(IPhysicalComponentConstruction physicalComponentConstruction, HeatFlowDirection heatFlowDirection)
            {
                List<IStructureLayer>? structureLayers = constructionRelationCluster.GetStructureLayers(physicalComponentConstruction);
                if (structureLayers is null || structureLayers.Count == 0)
                {
                    return double.NaN;
                }

                return Building.Query.ThermalTransmittance(structureLayers.ConvertAll(x => (x, constructionRelationCluster.GetMaterial(x))), heatFlowDirection, false);
            }

            string Layers(IPhysicalComponentConstruction physicalComponentConstruction)
            {
                List<IStructureLayer> structureLayers = constructionRelationCluster.GetStructureLayers(physicalComponentConstruction) ?? [];
                return string.Join(" / ", structureLayers.ConvertAll(x => string.Format(CultureInfo.InvariantCulture, "{0} {1}", constructionRelationCluster.GetMaterial(x)?.Name, Math.Round(x.Thickness * 1000.0, 1))));
            }

            List<ConstructionRole> constructionRoles_Declared = [ConstructionRole.ExternalWall, ConstructionRole.Roof, ConstructionRole.GroundFloor];
            List<ConstructionRole> constructionRoles_Default = [ConstructionRole.InternalWall, ConstructionRole.PartyWall, ConstructionRole.IntermediateFloor];

            HashSet<Guid> guids = [];
            List<string> failures = [];
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("# Construction layers (DiGi-Organization/DiGi.Analytical#18)");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Library {0}. Tolerance: |U_calc - U_declared| ≤ max({1} W/(m²K), {2} %). U_calc per PN-EN ISO 6946 from the layers alone, no ground contact; layers outside to inside, thickness in mm.", Building.Regulation.Create.ConstructionLibrary_PL_Version, Building.Regulation.Create.ConstructionLibrary_PL_LayerTolerance_Absolute, Building.Regulation.Create.ConstructionLibrary_PL_LayerTolerance_Relative * 100.0));
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Construction | Heat flow | Layers | U declared | U calc | Δ | Δ % |");
            stringBuilder.AppendLine("|---|---|---|---|---|---|---|");

            foreach (ConstructionSet constructionSet in constructionLibrary.GetConstructionSets())
            {
                if (constructionSet.ConstructionFlavour != ConstructionFlavour.TypicalAsBuilt)
                {
                    continue;
                }

                foreach (ConstructionRole constructionRole in constructionRoles_Declared)
                {
                    IPhysicalComponentConstruction? physicalComponentConstruction = constructionLibrary.GetConstruction(constructionSet, constructionRole) as IPhysicalComponentConstruction;
                    Assert.NotNull(physicalComponentConstruction);
                    if (!guids.Add(physicalComponentConstruction.Guid))
                    {
                        continue;
                    }

                    Assert.True(physicalComponentConstruction.TryGetValue(new EnumParameterDefinition(PhysicalComponentConstructionParameter.ThermalTransmittance), out double thermalTransmittance_Declared));
                    HeatFlowDirection heatFlowDirection = Direction(constructionRole);
                    double thermalTransmittance_Calculated = LayersThermalTransmittance(physicalComponentConstruction, heatFlowDirection);
                    double difference = thermalTransmittance_Calculated - thermalTransmittance_Declared;
                    double tolerance = Math.Max(Building.Regulation.Create.ConstructionLibrary_PL_LayerTolerance_Absolute, Building.Regulation.Create.ConstructionLibrary_PL_LayerTolerance_Relative * thermalTransmittance_Declared);
                    if (double.IsNaN(thermalTransmittance_Calculated) || Math.Abs(difference) > tolerance)
                    {
                        failures.Add(string.Format(CultureInfo.InvariantCulture, "{0}: U declared {1}, U calc {2}", physicalComponentConstruction.Name, thermalTransmittance_Declared, thermalTransmittance_Calculated));
                    }

                    // The declared U still wins in the cluster overload (D1 of DiGi-Organization/DiGi.Analytical#18).
                    Assert.Equal(thermalTransmittance_Declared, Building.Query.ThermalTransmittance(constructionRelationCluster, physicalComponentConstruction, heatFlowDirection, false));

                    stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2} | {3} | {4:0.000} | {5:+0.000;-0.000;0.000} | {6:+0.0;-0.0;0.0} |", physicalComponentConstruction.Name, heatFlowDirection, Layers(physicalComponentConstruction), thermalTransmittance_Declared, thermalTransmittance_Calculated, difference, difference / thermalTransmittance_Declared * 100.0));
                }

                foreach (ConstructionRole constructionRole in constructionRoles_Default)
                {
                    IPhysicalComponentConstruction? physicalComponentConstruction = constructionLibrary.GetConstruction(constructionSet, constructionRole) as IPhysicalComponentConstruction;
                    Assert.NotNull(physicalComponentConstruction);
                    Assert.Contains("DEFAULT", physicalComponentConstruction.Description);
                    Assert.Contains(constructionRole.ToString(), constructionSet.Source);
                    Assert.False(physicalComponentConstruction.TryGetValue(new EnumParameterDefinition(PhysicalComponentConstructionParameter.ThermalTransmittance), out double _));
                    Assert.False(double.IsNaN(LayersThermalTransmittance(physicalComponentConstruction, Direction(constructionRole))), physicalComponentConstruction.Name);
                }
            }

            string? path_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(path_Reports);
            File.WriteAllText(Path.Combine(path_Reports, "ConstructionLayers.md"), stringBuilder.ToString());

            Assert.True(guids.Count > 30, $"{guids.Count} assemblies");
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Tests the layered assemblies of the <see cref="ConstructionFlavour.Regulation"/> sets of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> (DiGi-Organization/DiGi.Analytical#18): every wall, roof and floor construction has layers whose PN-EN ISO 6946 U satisfies <see cref="Building.Regulation.Query.IsCompliant(double, double, double)"/> against its declared U(max).
        /// </summary>
        [Fact]
        public void ConstructionLibrary_PL_RegulationLayers()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();
            ConstructionRelationCluster constructionRelationCluster = constructionLibrary.ConstructionRelationCluster;

            HashSet<Guid> guids = [];
            List<string> failures = [];
            foreach (ConstructionSet constructionSet in constructionLibrary.GetConstructionSets())
            {
                if (constructionSet.ConstructionFlavour != ConstructionFlavour.Regulation)
                {
                    continue;
                }

                foreach (ConstructionRole constructionRole in constructionSet.GetConstructionRoles())
                {
                    if (constructionLibrary.GetConstruction(constructionSet, constructionRole) is not IPhysicalComponentConstruction physicalComponentConstruction || !guids.Add(physicalComponentConstruction.Guid))
                    {
                        continue;
                    }

                    HeatFlowDirection heatFlowDirection = constructionRole switch
                    {
                        ConstructionRole.Roof or ConstructionRole.FlatRoof => HeatFlowDirection.Upward,
                        ConstructionRole.GroundFloor or ConstructionRole.FloorOverUnheated => HeatFlowDirection.Downward,
                        _ => HeatFlowDirection.Horizontal,
                    };

                    Assert.True(physicalComponentConstruction.TryGetValue(new EnumParameterDefinition(PhysicalComponentConstructionParameter.ThermalTransmittance), out double regulatedHeatTransferCoefficient));
                    List<IStructureLayer>? structureLayers = constructionRelationCluster.GetStructureLayers(physicalComponentConstruction);
                    double thermalTransmittance = structureLayers is null || structureLayers.Count == 0 ? double.NaN : Building.Query.ThermalTransmittance(structureLayers.ConvertAll(x => (x, constructionRelationCluster.GetMaterial(x))), heatFlowDirection, false);
                    if (!Building.Regulation.Query.IsCompliant(thermalTransmittance, regulatedHeatTransferCoefficient))
                    {
                        failures.Add(string.Format(CultureInfo.InvariantCulture, "{0}: U(max) {1}, U(layers) {2}", physicalComponentConstruction.Name, regulatedHeatTransferCoefficient, thermalTransmittance));
                    }
                }
            }

            Assert.True(guids.Count > 20, $"{guids.Count} assemblies");
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// Tests that the layered constructions of a <see cref="ConstructionFlavour.TypicalAsBuilt"/> set of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> import into a <see cref="BuildingModel"/> with their layers and shared materials (DiGi-Organization/DiGi.Analytical#18): every <see cref="BuildingModel.Update(ConstructionRelationCluster?, IConstruction?)"/> succeeds, and a material used by several layers arrives once, under one identifier.
        /// </summary>
        [Fact]
        public void ConstructionLibrary_PL_ImportLayers()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();
            ConstructionSet? constructionSet = constructionLibrary.GetConstructionSet(new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero), BuildingType.Terraced, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
            Assert.NotNull(constructionSet);

            BuildingModel buildingModel = new();
            HashSet<Guid> guids_Material = [];
            int count_StructureLayer = 0;
            foreach (ConstructionRole constructionRole in constructionSet.GetConstructionRoles())
            {
                IConstruction? construction = constructionLibrary.GetConstruction(constructionSet, constructionRole);
                Assert.NotNull(construction);
                Assert.True(buildingModel.Update(constructionLibrary.ConstructionRelationCluster, construction), constructionRole.ToString());
                if (construction is IPhysicalComponentConstruction physicalComponentConstruction)
                {
                    List<IStructureLayer>? structureLayers = buildingModel.GetStructureLayers(physicalComponentConstruction);
                    Assert.NotNull(structureLayers);
                    Assert.NotEmpty(structureLayers);
                    count_StructureLayer += structureLayers.Count;
                    foreach (IStructureLayer structureLayer in structureLayers)
                    {
                        IMaterial? material = buildingModel.GetMaterial(structureLayer);
                        Assert.NotNull(material);
                        guids_Material.Add(material.Guid);
                    }
                }
            }

            // Brick and cement-lime plaster serve the external, internal and party walls of the set.
            Assert.True(guids_Material.Count < count_StructureLayer, $"{guids_Material.Count} materials for {count_StructureLayer} layers");
        }

        /// <summary>
        /// Tests the identifier constructors <c>OpaqueMaterial(Guid, string?)</c> and <c>StructureLayer(Guid, int, double)</c> (DiGi-Organization/DiGi.Analytical#18): the identifier is the one given, and both survive a JSON round trip.
        /// </summary>
        [Fact]
        public void OpaqueMaterial_StructureLayer_Guid()
        {
            Guid guid_Material = Core.Create.Guid("Test.Material");
            OpaqueMaterial opaqueMaterial = new(guid_Material, "Solid brick") { Conductivity = 0.77, Density = 1800, SpecificHeat = 880, Description = "Test" };
            Assert.Equal(guid_Material, opaqueMaterial.Guid);
            Assert.Equal("Solid brick", opaqueMaterial.Name);
            Assert.Equal(0.77, opaqueMaterial.Conductivity);
            Assert.Equal(guid_Material, new OpaqueMaterial(opaqueMaterial).Guid);
            Core.xUnit.Query.SerializationCheck(opaqueMaterial);

            Guid guid_StructureLayer = Core.Create.Guid("Test.StructureLayer");
            StructureLayer structureLayer = new(guid_StructureLayer, 2, 0.25);
            Assert.Equal(guid_StructureLayer, structureLayer.Guid);
            Assert.Equal(2, structureLayer.Index);
            Assert.Equal(0.25, structureLayer.Thickness);
            Core.xUnit.Query.SerializationCheck(structureLayer);

            WallConstruction wallConstruction = new() { Name = "Wall" };
            ConstructionRelationCluster constructionRelationCluster = new([opaqueMaterial, wallConstruction]);
            StructureLayer? structureLayer_Created = Building.Create.StructureLayer(constructionRelationCluster, wallConstruction, opaqueMaterial, 0.25, guid_StructureLayer);
            Assert.NotNull(structureLayer_Created);
            Assert.Equal(guid_StructureLayer, structureLayer_Created.Guid);
            Assert.Equal(0, structureLayer_Created.Index);
        }
    }
}
