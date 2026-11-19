using DiGi.EnergyPlus.Classes;
using System.Reflection;

namespace DiGi.EnergyPlus.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Convert.ToDiGi_EnergyPlusResult(string?)"/> on the committed output of one real EnergyPlus 26.2 run of the in-code box with the Warsaw IMGW weather file, <c>files/EnergyPlus_Box</c> (DiGi-Organization/DiGi.EnergyPlus#1).
        /// <para>The monthly heating energy is the CSV's joules over 3.6 MJ/kWh, January 6 923 650 538 J and July 0; the annual is their sum. The version, one warning and no severe error come from the error file, and the seven external surfaces of the envelope summary - four walls of U-factor with film 0.302, the roof 0.25, two windows of glass U 1.299 - from the tables. A run whose error file does not report completion, a directory without the variables file and a null directory give null.</para>
        /// </summary>
        [Fact]
        public void ToDiGi_EnergyPlusResult()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? directory_Files = Core.xUnit.Query.FilesDirectory(assembly);
            Assert.NotNull(directory_Files);
            string directory = Path.Combine(directory_Files, "EnergyPlus_Box");

            EnergyPlusResult? energyPlusResult = Convert.ToDiGi_EnergyPlusResult(directory);
            Assert.NotNull(energyPlusResult);

            Assert.Equal("26.2.0-4bd7a1f26f", energyPlusResult.EnergyPlusVersion);
            Assert.Equal(1, energyPlusResult.WarningCount);
            Assert.Equal(0, energyPlusResult.SevereErrorCount);

            List<double>? heatingEnergies = energyPlusResult.HeatingEnergies;
            Assert.NotNull(heatingEnergies);
            Assert.Equal(12, heatingEnergies.Count);
            Assert.Equal(6923650537.994074 / 3600000.0, heatingEnergies[0], 9);
            Assert.Equal(0.0, heatingEnergies[6], 12);
            Assert.Equal(heatingEnergies.Sum(), energyPlusResult.HeatingEnergy, 9);

            Dictionary<string, List<double>>? systemHeatingEnergies = energyPlusResult.SystemHeatingEnergies;
            Assert.NotNull(systemHeatingEnergies);
            Assert.Equal(heatingEnergies, Assert.Contains("IDEAL LOADS", systemHeatingEnergies));

            Dictionary<string, double>? thermalTransmittances = energyPlusResult.ThermalTransmittances;
            Assert.NotNull(thermalTransmittances);
            Assert.Equal(7, thermalTransmittances.Count);
            Assert.Equal(4, thermalTransmittances.Values.Count(x => x == 0.302));
            Assert.Equal(1, thermalTransmittances.Values.Count(x => x == 0.25));
            Assert.Equal(2, thermalTransmittances.Values.Count(x => x == 1.299));
            Assert.All(thermalTransmittances.Keys, x => Assert.Equal(x.ToLowerInvariant(), x));

            Assert.Null(Convert.ToDiGi_EnergyPlusResult(null));
            Assert.Null(Convert.ToDiGi_EnergyPlusResult(directory_Files));

            // The same output whose error file never reports completion is no result.
            string? path_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
            Assert.NotNull(path_Reports);
            string directory_Failed = Path.Combine(path_Reports, "EnergyPlus_Box_Failed");
            Directory.CreateDirectory(directory_Failed);
            foreach (string fileName in new[] { Constants.FileName.Variables, Constants.FileName.Tables })
            {
                File.Copy(Path.Combine(directory, fileName), Path.Combine(directory_Failed, fileName), true);
            }

            File.WriteAllText(Path.Combine(directory_Failed, Constants.FileName.Error), File.ReadAllText(Path.Combine(directory, Constants.FileName.Error)).Replace("EnergyPlus Completed Successfully", "EnergyPlus Terminated", StringComparison.Ordinal));
            Assert.Null(Convert.ToDiGi_EnergyPlusResult(directory_Failed));
        }

        /// <summary>
        /// Tests the serialization of <see cref="EnergyPlusResult"/> (DiGi-Organization/DiGi.EnergyPlus#1): a result with its monthly and per-system lists populated and no thermal transmittances survives the JSON round trip and <see cref="Core.xUnit.Query.SerializationCheck{TSerializableObject}(TSerializableObject)"/>, and its copy constructor copies every member, lists and dictionaries as copies.
        /// </summary>
        [Fact]
        public void EnergyPlusResult_Serialization()
        {
            List<double> heatingEnergies = [1923.2, 1693.1, 1269.4, 962.4, 377.4, 4.4, 0.0, 25.8, 270.2, 851.4, 1425.8, 1748.7];
            EnergyPlusResult energyPlusResult = new("26.2.0-4bd7a1f26f", heatingEnergies.Sum(), heatingEnergies, new Dictionary<string, List<double>>() { ["IDEAL LOADS"] = heatingEnergies }, null, 1, 0);

            Assert.Equal("26.2.0-4bd7a1f26f", energyPlusResult.EnergyPlusVersion);
            Assert.Equal(heatingEnergies.Sum(), energyPlusResult.HeatingEnergy);
            Assert.Equal(heatingEnergies, energyPlusResult.HeatingEnergies);
            Assert.Null(energyPlusResult.ThermalTransmittances);
            Assert.Equal(1, energyPlusResult.WarningCount);
            Assert.Equal(0, energyPlusResult.SevereErrorCount);

            EnergyPlusResult? energyPlusResult_Json = Core.Convert.ToDiGi<EnergyPlusResult>(Core.Convert.ToSystem_String(energyPlusResult))?.FirstOrDefault();
            Assert.NotNull(energyPlusResult_Json);
            Assert.Equal(heatingEnergies, energyPlusResult_Json.HeatingEnergies);
            Assert.Equal(heatingEnergies, energyPlusResult_Json.SystemHeatingEnergies?["IDEAL LOADS"]);
            Assert.Equal("26.2.0-4bd7a1f26f", energyPlusResult_Json.EnergyPlusVersion);

            Core.xUnit.Query.SerializationCheck(energyPlusResult);

            EnergyPlusResult energyPlusResult_Transmittances = new("26.2.0-4bd7a1f26f", 1.0, heatingEnergies, null, new Dictionary<string, double>() { ["wall"] = 0.302 }, 0, 0);
            Core.xUnit.Query.SerializationCheck(energyPlusResult_Transmittances);

            EnergyPlusResult energyPlusResult_Copy = new(energyPlusResult_Transmittances);
            Assert.Equal(energyPlusResult_Transmittances.EnergyPlusVersion, energyPlusResult_Copy.EnergyPlusVersion);
            Assert.Equal(energyPlusResult_Transmittances.HeatingEnergy, energyPlusResult_Copy.HeatingEnergy);
            Assert.Equal(energyPlusResult_Transmittances.HeatingEnergies, energyPlusResult_Copy.HeatingEnergies);
            Assert.Equal(energyPlusResult_Transmittances.ThermalTransmittances, energyPlusResult_Copy.ThermalTransmittances);
            Assert.Equal(energyPlusResult_Transmittances.WarningCount, energyPlusResult_Copy.WarningCount);
            Assert.Equal(energyPlusResult_Transmittances.SevereErrorCount, energyPlusResult_Copy.SevereErrorCount);

            EnergyPlusResult energyPlusResult_CopySystems = new(energyPlusResult);
            Assert.Equal(energyPlusResult.SystemHeatingEnergies?["IDEAL LOADS"], energyPlusResult_CopySystems.SystemHeatingEnergies?["IDEAL LOADS"]);

            // Every accessor hands out a copy.
            energyPlusResult.HeatingEnergies?.Clear();
            energyPlusResult.SystemHeatingEnergies?["IDEAL LOADS"].Clear();
            Assert.Equal(12, energyPlusResult.HeatingEnergies?.Count);
            Assert.Equal(12, energyPlusResult.SystemHeatingEnergies?["IDEAL LOADS"].Count);
        }
    }
}
