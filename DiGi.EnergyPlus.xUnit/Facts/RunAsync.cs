using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Energy;
using DiGi.Analytical.Building.Energy.Classes;
using DiGi.EnergyPlus.Classes;
using System.Reflection;

namespace DiGi.EnergyPlus.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Modify.RunAsync(string?, string?, string?, string?, CancellationToken)"/> and <see cref="Convert.ToDiGi_EnergyPlusResult(string?)"/> on a real EnergyPlus run of the in-code box with the Warsaw IMGW weather file (DiGi-Organization/DiGi.EnergyPlus#1).
        /// <para>The run completes with no severe error and the version pinned in <see cref="Constants.Default.Version"/>; its one warning is the zone EnergyPlus does not see as enclosed, since the thermal-bridge surface stands apart and the volume is given. EnergyPlus's own film coefficients put the U of the walls and the roof within 0.02 W/(m2K) of DiGi's 0.30 and 0.25, and the glass U of both windows within 0.01 of the declared 1.3. January needs more heat than July and the annual need is the sum of the months.</para>
        /// <para>A missing weather file fails without running, and a cancelled token cancels. The run directory is <c>EnergyPlus_Box</c> in the reports directory; its three files read here are the committed fixture of <see cref="ToDiGi_EnergyPlusResult"/>.</para>
        /// <para>Needs EnergyPlus: without <c>user files/EnergyPlus_Executable.conf</c> naming an existing executable the fact returns without asserting anything.</para>
        /// </summary>
        /// <returns>A task that completes when the fact has run.</returns>
        [MediumFact]
        public async Task RunAsync_Box()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? directory_UserFiles = Core.xUnit.Query.UserFilesDirectory(assembly);
            string? path_Executable = Query.ExecutablePath(directory_UserFiles);
            if (path_Executable is null)
            {
                // No conf is a machine without EnergyPlus; a conf that names nothing usable is a broken setup, and passing would hide it.
                Assert.False(directory_UserFiles is not null && File.Exists(Path.Combine(directory_UserFiles, Constants.FileName.ExecutableConfiguration)), Constants.FileName.ExecutableConfiguration + " exists but names no existing executable under " + Constants.Name.ExecutablePathKey + ".");
                return;
            }

            string? path_EPW = Core.xUnit.Query.FilePath(assembly, "POL_MZ_Warszawa-Chopina.Intl.AP.123750_IMGW.epw");
            Assert.NotNull(path_EPW);

            string? path_Reports = Core.xUnit.Query.ReportsDirectory(assembly);
            Assert.NotNull(path_Reports);
            string directory = Path.Combine(path_Reports, "EnergyPlus_Box");

            (BuildingModel buildingModel, Dictionary<string, Guid> guids) = EnergyPlus_Model(false, false, false, false);
            MonthlyClimate monthlyClimate = EnergyPlus_Climate();

            HeatDemandResult? heatDemandResult = buildingModel.HeatDemandResult(monthlyClimate, energyPlus_TypologyData);
            Assert.NotNull(heatDemandResult);

            string? text = buildingModel.ToEnergyPlus_String(null, monthlyClimate, energyPlus_TypologyData, heatDemandResult, null);
            Assert.NotNull(text);

            Assert.False(await Modify.RunAsync(text, Path.Combine(path_Reports, "missing.epw"), path_Executable, directory));

            using (CancellationTokenSource cancellationTokenSource = new())
            {
                await cancellationTokenSource.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Modify.RunAsync(text, path_EPW, path_Executable, directory, cancellationTokenSource.Token));
            }

            Assert.True(await Modify.RunAsync(text, path_EPW, path_Executable, directory));

            EnergyPlusResult? energyPlusResult = Convert.ToDiGi_EnergyPlusResult(directory);
            Assert.NotNull(energyPlusResult);

            Assert.StartsWith(Constants.Default.Version + ".", energyPlusResult.EnergyPlusVersion);
            Assert.Equal(0, energyPlusResult.SevereErrorCount);
            Assert.Equal(1, energyPlusResult.WarningCount);

            List<double>? heatingEnergies = energyPlusResult.HeatingEnergies;
            Assert.NotNull(heatingEnergies);
            Assert.Equal(12, heatingEnergies.Count);
            Assert.True(heatingEnergies[0] > heatingEnergies[6]);
            Assert.Equal(heatingEnergies.Sum(), energyPlusResult.HeatingEnergy, 6);
            Assert.Single(energyPlusResult.SystemHeatingEnergies ?? []);

            Dictionary<string, double>? thermalTransmittances = energyPlusResult.ThermalTransmittances;
            Assert.NotNull(thermalTransmittances);
            foreach (string name in new[] { "South", "East", "North", "West" })
            {
                Assert.Equal(0.30, thermalTransmittances[guids[name].ToString("D")], 0.02);
            }

            Assert.Equal(0.25, thermalTransmittances[guids["Roof"].ToString("D")], 0.02);
            Assert.Equal(1.3, thermalTransmittances[guids["Window Pane"].ToString("D")], 0.01);
            Assert.Equal(1.3, thermalTransmittances[guids["Window NoPane"].ToString("D")], 0.01);
        }
    }
}
