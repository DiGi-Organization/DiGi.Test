using DiGi.Analytical.Building;
using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Energy;
using DiGi.Analytical.Building.Energy.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Core.Interfaces;
using DiGi.Core.Parameter.Classes;
using DiGi.EnergyPlus.Classes;
using DiGi.GIS.Analytical.Energy.Classes;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace DiGi.EnergyPlus.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Compares the PN-EN ISO 52016-1 monthly heat demand of DiGi with EnergyPlus 26.2 on the committed samples of the four study counties (DiGi-Organization/DiGi.EnergyPlus#1): <c>files/EnergyPlus_Samples</c>, each the <see cref="HeatDemandPreparationResult"/> and the <see cref="BuildingHeatDemandResult"/> captured once through api.digiproject.uk, with the neighbours within the solar radius of a shaded building, and the IMGW weather files they use.
        /// <para>Per building the DiGi result is recomputed from the preparation and must equal the captured one, so the samples cannot drift from the engine unnoticed. The building is written by <see cref="Convert.ToEnergyPlus_String(BuildingModel?, EPW.Classes.EPWFile?, MonthlyClimate?, TypologyData?, HeatDemandResult?, IEnumerable{BuildingModel?}?, double)"/> - neighbours as shading for a shaded building, none for an unshaded one, as the calculation had them - and run.</para>
        /// <para>Compared and written to <c>EnergyPlus_Comparison.md</c> in the reports directory: U per element (DiGi's PN-EN ISO 6946 value against EnergyPlus's U-factor with its own films; a window's glass U, a door's U with film), H_D (Σ U·A over EnergyPlus's external surfaces against DiGi's), and the monthly and annual heating need. Asserted: at least ten samples, every run completes without a severe error, and H_D agrees within 5 % - both sides describe the same envelope, so a larger gap is an export error, not a finding. The heating needs are the finding and are reported, not bounded.</para>
        /// <para>Needs EnergyPlus: without <c>user files/EnergyPlus_Executable.conf</c> the fact returns; a conf naming no executable fails it.</para>
        /// </summary>
        /// <returns>A task that completes when the fact has run.</returns>
        [LongFact]
        public async Task EnergyPlus_Comparison()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            string? path_Executable = Query.ExecutablePath(directory_UserFiles);
            if (path_Executable is null)
            {
                Assert.False(directory_UserFiles is not null && File.Exists(Path.Combine(directory_UserFiles, Constants.FileName.ExecutableConfiguration)), Constants.FileName.ExecutableConfiguration + " exists but names no existing executable under " + Constants.Name.ExecutablePathKey + ".");
                return;
            }

            string? directory_Files = Core.xUnit.Query.FilesDirectory(assembly);
            string? directory_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
            Assert.NotNull(directory_Files);
            Assert.NotNull(directory_Reports);

            // The samples and their IMGW weather files are committed as one zip, extracted for the run.
            string directory_Samples = Path.Combine(directory_Reports, "EnergyPlus_Samples");
            if (Directory.Exists(directory_Samples))
            {
                Directory.Delete(directory_Samples, true);
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(Path.Combine(directory_Files, "EnergyPlus_Samples.zip"), directory_Samples);
            List<string> paths = [.. Directory.GetFiles(directory_Samples, "EnergyPlus_Sample_*.json").Order(StringComparer.Ordinal)];
            Assert.True(paths.Count >= 10, paths.Count.ToString(CultureInfo.InvariantCulture) + " samples");

            static string Format(double value, string format = "0.0")
            {
                return value.ToString(format, CultureInfo.InvariantCulture);
            }

            StringBuilder stringBuilder_Table = new();
            stringBuilder_Table.AppendLine("| Sample | County | Type | Year built | Solar | A_ref [m2] | V / A_ref [m] | H_D DiGi / E+ [W/K] | ΔU opaque max [W/(m2K)] | ΔU window or door max | Q_H,nd DiGi [kWh/(m2a)] | Q_H,nd E+ | Deviation |");
            stringBuilder_Table.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            StringBuilder stringBuilder_Monthly = new();
            stringBuilder_Monthly.AppendLine("| Sample | Source | Jan | Feb | Mar | Apr | May | Jun | Jul | Aug | Sep | Oct | Nov | Dec |");
            stringBuilder_Monthly.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            List<string> failures = [];
            List<double> deviations = [];
            List<string> anomalies = [];

            foreach (string path in paths)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                List<ISerializableObject>? serializableObjects = Core.Convert.ToDiGi<ISerializableObject>(await File.ReadAllTextAsync(path));
                Assert.NotNull(serializableObjects);

                HeatDemandPreparationResult heatDemandPreparationResult = serializableObjects.OfType<HeatDemandPreparationResult>().Single();
                BuildingHeatDemandResult buildingHeatDemandResult = serializableObjects.OfType<BuildingHeatDemandResult>().Single();
                List<BuildingModel> buildingModels_Shading = [.. serializableObjects.OfType<BuildingModel>()];

                BuildingModel? buildingModel = heatDemandPreparationResult.BuildingModel;
                MonthlyClimate? monthlyClimate = heatDemandPreparationResult.MonthlyClimate;
                TypologyData? typologyData = heatDemandPreparationResult.OpeningGenerationResult?.TypologyData;
                Assert.NotNull(buildingModel);
                Assert.NotNull(monthlyClimate);

                // The engine on the preparation is the captured result: the samples cannot drift from it unnoticed.
                HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(monthlyClimate, typologyData);
                Assert.NotNull(heatDemandResult);
                Assert.Equal(Core.Convert.ToSystem_String(buildingHeatDemandResult.HeatDemandResult), Core.Convert.ToSystem_String(heatDemandResult));

                string path_EPW = Path.Combine(directory_Samples, "EnergyPlus_EPW_" + buildingHeatDemandResult.WeatherSourceId + ".epw");
                Assert.True(File.Exists(path_EPW), path_EPW);

                string? text = buildingModel.ToEnergyPlus_String(null, monthlyClimate, typologyData, heatDemandResult, buildingModels_Shading);
                Assert.NotNull(text);

                string directory = Path.Combine(directory_Reports, "EnergyPlus_Comparison", name);
                Assert.True(await Modify.RunAsync(text, path_EPW, path_Executable, directory), name + ": EnergyPlus did not complete");

                EnergyPlusResult? energyPlusResult = Convert.ToDiGi_EnergyPlusResult(directory);
                Assert.NotNull(energyPlusResult);
                Assert.True(energyPlusResult.SevereErrorCount == 0, name + ": " + energyPlusResult.SevereErrorCount.ToString(CultureInfo.InvariantCulture) + " severe errors, see " + Constants.FileName.Error);

                // U per element: DiGi's own value for each external surface EnergyPlus reports.
                Dictionary<Guid, IComponent> components = [];
                foreach (IComponent component in buildingModel.GetComponents<IComponent>() ?? [])
                {
                    components[component.Guid] = component;
                }

                Dictionary<Guid, IOpening> openings = [];
                foreach (IOpening opening in buildingModel.GetOpenings<IOpening>() ?? [])
                {
                    openings[opening.Guid] = opening;
                }

                Dictionary<string, double> thermalTransmittances = energyPlusResult.ThermalTransmittances ?? [];
                Dictionary<string, double> areas = energyPlusResult.Areas ?? [];

                double deviation_Opaque = 0.0;
                double deviation_Window = 0.0;
                double directHeatTransferCoefficient_EnergyPlus = 0.0;
                foreach (KeyValuePair<string, double> keyValuePair in thermalTransmittances)
                {
                    if (areas.TryGetValue(keyValuePair.Key, out double area))
                    {
                        directHeatTransferCoefficient_EnergyPlus += keyValuePair.Value * area;
                    }

                    // A triangulated surface is named after its component with a suffix.
                    if (!Guid.TryParse(keyValuePair.Key.Length >= 36 ? keyValuePair.Key[..36] : keyValuePair.Key, out Guid guid))
                    {
                        continue;
                    }

                    if (components.TryGetValue(guid, out IComponent? component))
                    {
                        double thermalTransmittance = component switch
                        {
                            IWall wall => buildingModel.ThermalTransmittance(buildingModel.GetWallConstruction(wall), HeatFlowDirection.Horizontal),
                            IRoof roof => buildingModel.ThermalTransmittance(buildingModel.GetRoofConstruction(roof), HeatFlowDirection.Upward),
                            IFloor floor => buildingModel.ThermalTransmittance(buildingModel.GetFloorConstruction(floor), HeatFlowDirection.Downward),
                            _ => double.NaN,
                        };

                        if (!double.IsNaN(thermalTransmittance))
                        {
                            deviation_Opaque = System.Math.Max(deviation_Opaque, System.Math.Abs(keyValuePair.Value - thermalTransmittance));
                        }
                    }
                    else if (openings.TryGetValue(guid, out IOpening? opening))
                    {
                        IOpeningConstruction? openingConstruction = buildingModel.GetOpeningConstruction<IOpeningConstruction>(opening);
                        double thermalTransmittance = openingConstruction is not null && openingConstruction.TryGetValue(new EnumParameterDefinition(OpeningConstructionParameter.ThermalTransmittance), out double thermalTransmittance_Declared) ? thermalTransmittance_Declared : buildingModel.ThermalTransmittance(openingConstruction);
                        if (!double.IsNaN(thermalTransmittance))
                        {
                            deviation_Window = System.Math.Max(deviation_Window, System.Math.Abs(keyValuePair.Value - thermalTransmittance));
                        }
                    }
                }

                double directHeatTransferCoefficient_DiGi = heatDemandResult.DirectHeatTransferCoefficient;
                if (System.Math.Abs(directHeatTransferCoefficient_EnergyPlus - directHeatTransferCoefficient_DiGi) > 0.05 * directHeatTransferCoefficient_DiGi)
                {
                    failures.Add(name + ": H_D DiGi " + Format(directHeatTransferCoefficient_DiGi) + " W/K, EnergyPlus " + Format(directHeatTransferCoefficient_EnergyPlus) + " W/K");
                }

                double referenceFloorArea = heatDemandResult.ReferenceFloorArea;
                double heatingEnergyNeed_DiGi = heatDemandResult.HeatingEnergyNeed / referenceFloorArea;
                double heatingEnergyNeed_EnergyPlus = energyPlusResult.HeatingEnergy / referenceFloorArea;
                double deviation = (heatingEnergyNeed_EnergyPlus / heatingEnergyNeed_DiGi) - 1.0;
                double height = heatDemandResult.HeatedVolume / referenceFloorArea;

                // A heated volume of more than 10 m per m2 of floor is no building: both sides compute the same input, which is not a finding of the comparison.
                bool anomaly = height > 10.0;
                if (anomaly)
                {
                    anomalies.Add(name.Replace("EnergyPlus_Sample_", string.Empty, StringComparison.Ordinal) + " (V / A_ref " + Format(height, "0") + " m)");
                }
                else
                {
                    deviations.Add(deviation);
                }

                stringBuilder_Table.AppendLine(string.Join(" | ", [
                    "| " + name.Replace("EnergyPlus_Sample_", string.Empty, StringComparison.Ordinal),
                    buildingHeatDemandResult.CountyId.ToString(CultureInfo.InvariantCulture),
                    buildingHeatDemandResult.BuildingType.ToString(),
                    buildingHeatDemandResult.YearBuilt?.ToString(CultureInfo.InvariantCulture) ?? "-",
                    buildingHeatDemandResult.SolarSource.ToString() + (buildingModels_Shading.Count > 0 ? " (" + buildingModels_Shading.Count.ToString(CultureInfo.InvariantCulture) + " neighbours)" : string.Empty),
                    Format(referenceFloorArea),
                    Format(height) + (anomaly ? " (input anomaly)" : string.Empty),
                    Format(directHeatTransferCoefficient_DiGi) + " / " + Format(directHeatTransferCoefficient_EnergyPlus),
                    Format(deviation_Opaque, "0.000"),
                    Format(deviation_Window, "0.000"),
                    Format(heatingEnergyNeed_DiGi),
                    Format(heatingEnergyNeed_EnergyPlus),
                    Format(100.0 * deviation, "+0.0;-0.0") + " % |"]));

                List<double> heatingEnergyNeeds = heatDemandResult.HeatingEnergyNeeds ?? [];
                List<double> heatingEnergies = energyPlusResult.HeatingEnergies ?? [];
                stringBuilder_Monthly.AppendLine("| " + name.Replace("EnergyPlus_Sample_", string.Empty, StringComparison.Ordinal) + " | DiGi | " + string.Join(" | ", heatingEnergyNeeds.ConvertAll(x => Format(x, "0"))) + " |");
                stringBuilder_Monthly.AppendLine("| | E+ | " + string.Join(" | ", heatingEnergies.ConvertAll(x => Format(x, "0"))) + " |");
            }

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("# EnergyPlus comparison (DiGi-Organization/DiGi.EnergyPlus#1)");
            stringBuilder.AppendLine();
            deviations.Sort();
            double median = deviations.Count == 0 ? double.NaN : deviations.Count % 2 == 1 ? deviations[deviations.Count / 2] : (deviations[(deviations.Count / 2) - 1] + deviations[deviations.Count / 2]) / 2.0;
            stringBuilder.AppendLine("EnergyPlus " + Constants.Default.Version + ", " + paths.Count.ToString(CultureInfo.InvariantCulture) + " samples. Annual heating need, EnergyPlus against DiGi, over the " + deviations.Count.ToString(CultureInfo.InvariantCulture) + " samples without an input anomaly: median " + Format(100.0 * median, "+0.0;-0.0") + " %, range " + Format(100.0 * deviations[0], "+0.0;-0.0") + " % to " + Format(100.0 * deviations[^1], "+0.0;-0.0") + " %." + (anomalies.Count == 0 ? string.Empty : " Input anomalies, left out of these figures: " + string.Join(", ", anomalies) + "."));
            stringBuilder.AppendLine();
            stringBuilder.Append(stringBuilder_Table);
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("Monthly heating need [kWh]:");
            stringBuilder.AppendLine();
            stringBuilder.Append(stringBuilder_Monthly);
            await File.WriteAllTextAsync(Path.Combine(directory_Reports, "EnergyPlus_Comparison.md"), stringBuilder.ToString());

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }
    }
}
