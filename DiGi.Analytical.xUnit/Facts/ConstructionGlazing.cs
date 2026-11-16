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
        /// Tests the glass and gas materials of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> (DiGi-Organization/DiGi.Analytical#19): every <see cref="TransparentMaterial"/> has λ above zero and both emissivities, every <see cref="FluidMaterial"/> has conductivity, density, dynamic viscosity, specific heat and molar mass above zero, and each description names its secondary source or DEFAULT.
        /// <para>The identifiers are derived from stable keys, so a second library build, under pl-PL, gives the same material and layer identifiers. At 283 K the ISO 15099 argon reproduces the PN-EN 673 table values λ = 0.01684 W/(mK) and μ = 2.164e-5 Pa·s within 0.5 %.</para>
        /// </summary>
        [Fact]
        public void ConstructionLibrary_PL_GlazingMaterials()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();
            List<TransparentMaterial> transparentMaterials = constructionLibrary.ConstructionRelationCluster.GetValues<TransparentMaterial>() ?? [];
            List<FluidMaterial> fluidMaterials = constructionLibrary.ConstructionRelationCluster.GetValues<FluidMaterial>() ?? [];
            Assert.Equal(2, transparentMaterials.Count);
            Assert.Equal(3, fluidMaterials.Count);

            static void Source(IMaterial material)
            {
                string description = material.Description ?? string.Empty;
                Assert.True(description.Contains("as reproduced in", StringComparison.Ordinal) || description.Contains("DEFAULT", StringComparison.Ordinal), $"{material.Name}: {description}");
                Assert.Contains("Not checked against the original norm.", description);
                Assert.False(string.IsNullOrWhiteSpace(material.Name));
            }

            foreach (TransparentMaterial transparentMaterial in transparentMaterials)
            {
                Assert.True(transparentMaterial.Conductivity > 0, transparentMaterial.Name);
                Assert.NotNull(transparentMaterial.Emissivity);
                Assert.True(transparentMaterial.Emissivity.Internal > 0 && transparentMaterial.Emissivity.External > 0, transparentMaterial.Name);
                Assert.Equal(0.0, transparentMaterial.InfraredTransmittance);
                Source(transparentMaterial);
            }

            foreach (FluidMaterial fluidMaterial in fluidMaterials)
            {
                Assert.True(fluidMaterial.Conductivity > 0 && fluidMaterial.Density > 0 && fluidMaterial.DynamicViscosity > 0 && fluidMaterial.SpecificHeat > 0 && fluidMaterial.MolarMass > 0, fluidMaterial.Name);
                Source(fluidMaterial);
            }

            FluidMaterial fluidMaterial_Argon = fluidMaterials.Find(x => x.Guid == Core.Create.Guid("PL.Material.Argon"))!;
            Assert.NotNull(fluidMaterial_Argon);
            Assert.True(Math.Abs(fluidMaterial_Argon.Conductivity - 0.01684) / 0.01684 < 0.005, fluidMaterial_Argon.Conductivity.ToString(CultureInfo.InvariantCulture));
            Assert.True(Math.Abs(fluidMaterial_Argon.DynamicViscosity - 2.164e-5) / 2.164e-5 < 0.005, fluidMaterial_Argon.DynamicViscosity.ToString(CultureInfo.InvariantCulture));

            // The second build runs under pl-PL: a culture-dependent key would give other identifiers there.
            HashSet<Guid> guids = [.. transparentMaterials.ConvertAll(x => x.Guid), .. fluidMaterials.ConvertAll(x => x.Guid), .. (constructionLibrary.ConstructionRelationCluster.GetValues<StructureLayer>() ?? []).ConvertAll(x => x.Guid)];
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            ConstructionRelationCluster constructionRelationCluster_Second;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
                constructionRelationCluster_Second = Building.Regulation.Create.ConstructionLibrary_PL().ConstructionRelationCluster;
            }
            finally
            {
                CultureInfo.CurrentCulture = cultureInfo;
            }

            HashSet<Guid> guids_Second = [.. (constructionRelationCluster_Second.GetValues<TransparentMaterial>() ?? []).ConvertAll(x => x.Guid), .. (constructionRelationCluster_Second.GetValues<FluidMaterial>() ?? []).ConvertAll(x => x.Guid), .. (constructionRelationCluster_Second.GetValues<StructureLayer>() ?? []).ConvertAll(x => x.Guid)];
            Assert.True(guids.SetEquals(guids_Second));
        }

        /// <summary>
        /// Tests the pane layers of the <see cref="ConstructionFlavour.TypicalAsBuilt"/> window constructions of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> (DiGi-Organization/DiGi.Analytical#19).
        /// <para>Every window construction has <see cref="OpeningConstructionPart.Pane"/> layers, and the PN-EN 673 Ug of the layers alone is within the range stated for its TABULA window name: 5.6 - 5.8 W/(m2K) for single glazing and 1.0 - 1.2 for the 4/16/4 argon low-e unit (Pressglass, "Szklane fiszki cz. 1"), 1.62 ± 0.03 for the 4/12/4 air low-e unit (ift report 11-003309-PR01, type 5), and plausibility ranges for the coupled window and the 7.5 mm double glazing, whose build-ups are DEFAULT. The declared Uw stays the value the cluster overload of <c>Building.Query.ThermalTransmittance</c> returns for a construction without an outline. Every window declares the Uf and Ψg of its frame generation (PN-EN ISO 10077-1 Annex D and Table E.1 through secondary sources), and its description names their source.</para>
        /// <para>The library's materials, rebuilt as the ift report's type 16 (4/12/4, 90 % argon, ε = 0.03 on face 3), give its Ug of 1.28 within ± 0.03, which checks the glass, gas and calculation together against a published EN 673 result. The fact writes Ug per window to <c>reports/ConstructionGlazing.md</c> and reports every failing window at once.</para>
        /// </summary>
        [Fact]
        public void ConstructionLibrary_PL_Glazing()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();
            ConstructionRelationCluster constructionRelationCluster = constructionLibrary.ConstructionRelationCluster;

            Dictionary<string, (double Minimum, double Maximum, string Source)> ranges = new(StringComparer.Ordinal)
            {
                ["single glazed, wood frame"] = (5.6, 5.8, "Pressglass"),
                ["French window"] = (2.6, 3.0, "plausibility, DEFAULT build-up"),
                ["wood frame, double glazed air filled (7,5 mm gap)"] = (3.0, 3.5, "plausibility, DEFAULT panes"),
                ["plastic frame, glass unit (one cell)"] = (1.59, 1.65, "ift 11-003309-PR01 type 5, 1.62 ± 0.03"),
                ["plastic frame, glass unit ( (one cell gas filled)"] = (1.0, 1.2, "Pressglass, ift type 16"),
            };

            // The declared Uf [W/(m2K)] and Ψg [W/(mK)] per TABULA window name: PN-EN ISO 10077-1 Annex D and Table E.1 through their secondary sources.
            Dictionary<string, (double FrameThermalTransmittance, double GlazingLinearThermalTransmittance)> frames = new(StringComparer.Ordinal)
            {
                ["single glazed, wood frame"] = (2.0, 0.0),
                ["French window"] = (1.8, 0.0),
                ["wood frame, double glazed air filled (7,5 mm gap)"] = (1.9, 0.04),
                ["plastic frame, glass unit (one cell)"] = (2.0, 0.06),
                ["plastic frame, glass unit ( (one cell gas filled)"] = (2.0, 0.06),
            };

            string Layers(List<IStructureLayer> structureLayers)
            {
                return string.Join(" / ", structureLayers.ConvertAll(x => string.Format(CultureInfo.InvariantCulture, "{0} {1}", constructionRelationCluster.GetMaterial(x)?.Name, Math.Round(x.Thickness * 1000.0, 1))));
            }

            HashSet<Guid> guids = [];
            HashSet<string> names = [];
            List<string> failures = [];
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("# Construction glazing (DiGi-Organization/DiGi.Analytical#19)");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Library {0}. Ug per PN-EN 673 from the pane layers alone: vertical glazing, Tm = 283 K, ΔT = 15 K. Layers outside to inside, thickness in mm.", Building.Regulation.Create.ConstructionLibrary_PL_Version));
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("| Window construction | Pane layers | Ug | Range | Range source | Uf | Ψg | Uw declared |");
            stringBuilder.AppendLine("|---|---|---|---|---|---|---|---|");

            foreach (ConstructionSet constructionSet in constructionLibrary.GetConstructionSets())
            {
                if (constructionSet.ConstructionFlavour != ConstructionFlavour.TypicalAsBuilt)
                {
                    continue;
                }

                WindowConstruction? windowConstruction = constructionLibrary.GetConstruction(constructionSet, ConstructionRole.Window) as WindowConstruction;
                Assert.NotNull(windowConstruction);
                if (!guids.Add(windowConstruction.Guid))
                {
                    continue;
                }

                string name = windowConstruction.Name ?? string.Empty;
                string name_Tabula = name.Substring(0, name.LastIndexOf(" (", StringComparison.Ordinal));
                names.Add(name_Tabula);

                List<IStructureLayer>? structureLayers = constructionRelationCluster.GetStructureLayers(windowConstruction, OpeningConstructionPart.Pane);
                if (structureLayers is null || structureLayers.Count == 0)
                {
                    failures.Add(string.Format(CultureInfo.InvariantCulture, "{0}: no pane layers", name));
                    continue;
                }

                structureLayers.Sort((x, y) => x.Index.CompareTo(y.Index));
                double glazingThermalTransmittance = Building.Query.GlazingThermalTransmittance(structureLayers.ConvertAll(x => (x, constructionRelationCluster.GetMaterial(x))));
                Assert.True(ranges.TryGetValue(name_Tabula, out (double Minimum, double Maximum, string Source) range), name_Tabula);
                if (double.IsNaN(glazingThermalTransmittance) || glazingThermalTransmittance < range.Minimum || glazingThermalTransmittance > range.Maximum)
                {
                    failures.Add(string.Format(CultureInfo.InvariantCulture, "{0}: Ug {1} outside {2} - {3}", name, glazingThermalTransmittance, range.Minimum, range.Maximum));
                }

                Assert.True(windowConstruction.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.ThermalTransmittance), out double thermalTransmittance_Declared));
                Assert.Equal(thermalTransmittance_Declared, Building.Query.ThermalTransmittance(constructionRelationCluster, windowConstruction));
                Assert.Contains("Pane layers", windowConstruction.Description);

                Assert.True(frames.TryGetValue(name_Tabula, out (double FrameThermalTransmittance, double GlazingLinearThermalTransmittance) frame), name_Tabula);
                if (!windowConstruction.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.FrameThermalTransmittance), out double frameThermalTransmittance) || frameThermalTransmittance != frame.FrameThermalTransmittance)
                {
                    failures.Add(string.Format(CultureInfo.InvariantCulture, "{0}: Uf {1}, expected {2}", name, frameThermalTransmittance, frame.FrameThermalTransmittance));
                }

                if (!windowConstruction.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.GlazingLinearThermalTransmittance), out double glazingLinearThermalTransmittance) || glazingLinearThermalTransmittance != frame.GlazingLinearThermalTransmittance)
                {
                    failures.Add(string.Format(CultureInfo.InvariantCulture, "{0}: Ψg {1}, expected {2}", name, glazingLinearThermalTransmittance, frame.GlazingLinearThermalTransmittance));
                }

                string description = windowConstruction.Description ?? string.Empty;
                Assert.Contains("Frame ", description);
                Assert.True(description.Contains("as reproduced in", StringComparison.Ordinal) || description.Contains("DEFAULT", StringComparison.Ordinal), name);

                stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2:0.000} | {3} - {4} | {5} | {6} | {7} | {8} |", name, Layers(structureLayers), glazingThermalTransmittance, range.Minimum, range.Maximum, range.Source, frameThermalTransmittance, glazingLinearThermalTransmittance, thermalTransmittance_Declared));
            }

            // The ift report's type 16, rebuilt from the library's materials.
            List<IMaterial> materials = constructionRelationCluster.GetValues<IMaterial>() ?? [];
            IMaterial? material_Glass = materials.Find(x => x.Guid == Core.Create.Guid("PL.Material.FloatGlass"));
            IMaterial? material_LowE = materials.Find(x => x.Guid == Core.Create.Guid("PL.Material.FloatGlass_LowE"));
            IMaterial? material_Argon90 = materials.Find(x => x.Guid == Core.Create.Guid("PL.Material.Argon90"));
            Assert.NotNull(material_Glass);
            Assert.NotNull(material_LowE);
            Assert.NotNull(material_Argon90);
            double glazingThermalTransmittance_Ift = Building.Query.GlazingThermalTransmittance([(new StructureLayer(0, 0.004), material_Glass), (new StructureLayer(1, 0.012), material_Argon90), (new StructureLayer(2, 0.004), material_LowE)]);
            stringBuilder.AppendLine();
            stringBuilder.AppendLine(string.Format(CultureInfo.InvariantCulture, "Check against a published EN 673 result: 4/12/4, 90 % argon, ε = 0.03 on face 3, from the library's materials: Ug = {0:0.000}; ift report 11-003309-PR01, type 16: 1.28.", glazingThermalTransmittance_Ift));

            string? path_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(path_Reports);
            File.WriteAllText(Path.Combine(path_Reports, "ConstructionGlazing.md"), stringBuilder.ToString());

            Assert.True(Math.Abs(glazingThermalTransmittance_Ift - 1.28) <= 0.03, glazingThermalTransmittance_Ift.ToString(CultureInfo.InvariantCulture));
            Assert.True(names.SetEquals(ranges.Keys), string.Join(", ", names));
            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
