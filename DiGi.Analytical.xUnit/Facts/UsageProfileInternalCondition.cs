using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.HVAC;
using DiGi.Analytical.Building.HVAC.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Core.Classes;
using DiGi.Core.Parameter.Classes;
using System.Linq;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests Building.HVAC.Create.InternalCondition: the thermostat lower limit at the heating set-point, the total internal gain, the infiltration by year built (0.2 1/h after 1995, 0.3 before and when unknown, Dz.U. 2015 poz. 376 eq. 70), the system properties and the provenance parameters (DiGi-Organization/DiGi.Analytical#9).
        /// <para>Occupancy scaling: persons at the standard floor area per person leave the values unchanged, double the persons doubles the hot water and adds one person gain per a_std to the internal gain; without occupancy nothing is scaled.</para>
        /// </summary>
        [Fact]
        public void UsageProfile_InternalCondition()
        {
            const double tolerance = 1e-9;

            Assert.Null(Building.HVAC.Create.InternalCondition(null));

            Building.HVAC.Classes.UsageProfile usageProfile = Building.HVAC.Create.UsageProfile(UsageType.MultiFamily)!;

            InternalCondition internalCondition = usageProfile.InternalCondition(yearBuilt: 2000)!;
            Assert.Equal("MultiFamily", internalCondition.Name);
            Assert.Equal(20.0, internalCondition.Profile(ThermostatProfileType.LowerLimit)!.Values!.Single(), tolerance);
            Assert.Equal(7.1, internalCondition.Profile(InternalGainProfileType.TotalInternalGain)!.Values!.Single(), tolerance);
            Assert.Equal(0.2, internalCondition.Profile(InternalGainProfileType.Infiltration)!.Values!.Single(), tolerance);
            Assert.Null(internalCondition.Profile(InternalGainProfileType.OccupancySensibleGain));

            Building.HVAC.Classes.SystemProperties? systemProperties = internalCondition.SystemProperties();
            Assert.NotNull(systemProperties);
            Assert.Equal(2.00 * 0.90, systemProperties.DomesticHotWater, tolerance);
            Assert.Equal(0.32, systemProperties.OutsideAirPerArea, tolerance);
            Assert.Equal(usageProfile.PersonGain, systemProperties.MetabolicRate, tolerance);

            Assert.True(internalCondition.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.UsageType), out string? usageType, new GetValueSettings(true, false)));
            Assert.Equal("MultiFamily", usageType);
            Assert.True(internalCondition.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.UsageSource), out string? usageSource, new GetValueSettings(true, false)));
            Assert.Contains("PL-376: Table 26", usageSource);
            Assert.Contains("Infiltration: PL-376: eq. 70, 0.2 1/h, built 2000", usageSource);
            Assert.True(internalCondition.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.OccupancyScaled), out bool occupancyScaled, new GetValueSettings(true, false)));
            Assert.False(occupancyScaled);

            Assert.Equal(0.3, usageProfile.InternalCondition(yearBuilt: 1980)!.Profile(InternalGainProfileType.Infiltration)!.Values!.Single(), tolerance);
            Assert.Equal(0.3, usageProfile.InternalCondition(yearBuilt: 1995)!.Profile(InternalGainProfileType.Infiltration)!.Values!.Single(), tolerance);
            Assert.Equal(0.3, usageProfile.InternalCondition()!.Profile(InternalGainProfileType.Infiltration)!.Values!.Single(), tolerance);

            // Persons at the standard occupancy (30 m2 a person) change nothing.
            const double floorArea = 300.0;
            InternalCondition internalCondition_Standard = usageProfile.InternalCondition(occupancy: floorArea / usageProfile.FloorAreaPerPerson, floorArea: floorArea, occupancySource: "test")!;
            Assert.Equal(7.1, internalCondition_Standard.Profile(InternalGainProfileType.TotalInternalGain)!.Values!.Single(), tolerance);
            Assert.Equal(2.00 * 0.90, internalCondition_Standard.SystemProperties()!.DomesticHotWater, tolerance);
            Assert.True(internalCondition_Standard.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.OccupancyScaled), out bool occupancyScaled_Standard, new GetValueSettings(true, false)));
            Assert.True(occupancyScaled_Standard);
            Assert.True(internalCondition_Standard.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.Occupancy), out double occupancy, new GetValueSettings(true, false)));
            Assert.Equal(10.0, occupancy, tolerance);
            Assert.True(internalCondition_Standard.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.OccupancySource), out string? occupancySource, new GetValueSettings(true, false)));
            Assert.Equal("test", occupancySource);

            // Double the persons: twice the hot water, one more person gain per a_std.
            InternalCondition internalCondition_Double = usageProfile.InternalCondition(occupancy: 2 * floorArea / usageProfile.FloorAreaPerPerson, floorArea: floorArea)!;
            Assert.Equal(2 * 2.00 * 0.90, internalCondition_Double.SystemProperties()!.DomesticHotWater, tolerance);
            Assert.Equal(7.1 + (usageProfile.PersonGain / usageProfile.FloorAreaPerPerson), internalCondition_Double.Profile(InternalGainProfileType.TotalInternalGain)!.Values!.Single(), tolerance);

            // An empty building never goes below zero.
            InternalCondition internalCondition_Few = Building.HVAC.Create.UsageProfile(UsageType.Storage)!.InternalCondition(occupancy: 1, floorArea: 1e6)!;
            Assert.True(internalCondition_Few.Profile(InternalGainProfileType.TotalInternalGain)!.Values!.Single() >= 0);

            // The weekly profile is carried as it is.
            InternalCondition internalCondition_Hourly = Building.HVAC.Create.UsageProfile(UsageType.Office, hourly: true)!.InternalCondition()!;
            Assert.Equal(168, internalCondition_Hourly.Profile(InternalGainProfileType.TotalInternalGain)!.Count);

            // TODO [FactorRoundTrip]: SerializationCheck(internalCondition_Standard) once DiGi.Core's Factor reloads its value - the readonly
            // struct's FromJsonObject cannot write its field, so every InternalGain proportion reloads as NaN. Until then the profiles,
            // system properties and provenance are checked by a string round trip.
            InternalCondition? internalCondition_Json = Core.Convert.ToDiGi<InternalCondition>(Core.Convert.ToSystem_String(internalCondition_Standard))?.FirstOrDefault();
            Assert.NotNull(internalCondition_Json);
            Assert.Equal(7.1, internalCondition_Json.Profile(InternalGainProfileType.TotalInternalGain)!.Values!.Single(), tolerance);
            Assert.Equal(20.0, internalCondition_Json.Profile(ThermostatProfileType.LowerLimit)!.Values!.Single(), tolerance);
            Assert.Equal(0.32, internalCondition_Json.SystemProperties()!.OutsideAirPerArea, tolerance);
            Assert.True(internalCondition_Json.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.UsageSource), out string? usageSource_Json, new GetValueSettings(true, false)));
            Assert.True(internalCondition_Standard.TryGetValue(new EnumParameterDefinition(InternalConditionParameter.UsageSource), out string? usageSource_Standard, new GetValueSettings(true, false)));
            Assert.Equal(usageSource_Standard, usageSource_Json);
        }

        /// <summary>
        /// Tests Building.HVAC.Modify.AssignInternalCondition: every space of a three-storey model gets the condition, and Building.HVAC.Query.IndexedDoubles reads the 20 °C lower limit and the weekly total gain over a whole week (DiGi-Organization/DiGi.Analytical#9).
        /// </summary>
        [Fact]
        public void UsageProfile_AssignInternalCondition()
        {
            BuildingModel buildingModel = new();
            Space[] spaces =
            [
                new(new Geometry.Spatial.Classes.Point3D(0, 0, 1), "Storey 0"),
                new(new Geometry.Spatial.Classes.Point3D(0, 0, 4), "Storey 1"),
                new(new Geometry.Spatial.Classes.Point3D(0, 0, 7), "Storey 2"),
            ];

            foreach (Space space in spaces)
            {
                Assert.True(buildingModel.Update(space));
            }

            Assert.False(new BuildingModel().AssignInternalCondition(new InternalCondition("Empty model")));

            InternalCondition internalCondition = Building.HVAC.Create.UsageProfile(UsageType.Office, hourly: true)!.InternalCondition(yearBuilt: 2010)!;
            Assert.True(buildingModel.AssignInternalCondition(internalCondition));

            foreach (Space space in spaces)
            {
                Assert.Equal("Office", buildingModel.GetSpaceInternalConditions(space)?.Single().InternalCondition?.Name);

                IndexedDoubles? indexedDoubles_Thermostat = Building.HVAC.Query.IndexedDoubles(buildingModel, space, new Range<int>(0, 167), ThermostatProfileType.LowerLimit);
                Assert.NotNull(indexedDoubles_Thermostat);
                Assert.Equal(168, indexedDoubles_Thermostat.Count);
                Assert.All(indexedDoubles_Thermostat.Values!, x => Assert.Equal(20.0, x));

                IndexedDoubles? indexedDoubles_Gain = Building.HVAC.Query.IndexedDoubles(buildingModel, space, new Range<int>(0, 167), InternalGainProfileType.TotalInternalGain);
                Assert.NotNull(indexedDoubles_Gain);
                Assert.Equal(168, indexedDoubles_Gain.Count);
                Assert.Equal(5.68, indexedDoubles_Gain.Values!.Average(), 1e-9);
            }
        }

        /// <summary>
        /// Regression facts for the defects fixed with DiGi-Organization/DiGi.Analytical#9.
        /// <para>Building.HVAC.Modify.SetCoolingEmitter was declared as SetHeatingEmitter; Building.HVAC.Query.IndexedDoubles skipped (or, for a one-hour range, never left) the loop filling the hours of a condition without values, now NaN; and the copy constructors of InternalGain and Thermostat iterated the new, empty dictionary, so a copy lost every profile (and InternalGain its radiant proportions and view coefficients).</para>
        /// </summary>
        [Fact]
        public void UsageProfile_Regressions()
        {
            InternalCondition internalCondition = new("Emitters");
            Assert.True(internalCondition.SetCoolingEmitter(new Building.HVAC.Classes.CoolingEmitter { Name = "Cooling" }));
            Assert.Equal("Cooling", internalCondition.CoolingEmitter()?.Name);
            Assert.Null(internalCondition.HeatingEmitter());

            InternalCondition internalCondition_Empty = new("Empty");
            internalCondition_Empty.SetProfile(InternalGainProfileType.TotalInternalGain, new Profile(System.Array.Empty<double>()));

            Space space = new(new Geometry.Spatial.Classes.Point3D(0, 0, 0), "Space");
            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Assign(space, internalCondition_Empty, new Analytical.Classes.HourRange(0, 23)));

            IndexedDoubles? indexedDoubles = Building.HVAC.Query.IndexedDoubles(buildingModel, space, new Range<int>(0, 23), InternalGainProfileType.TotalInternalGain);
            Assert.NotNull(indexedDoubles);
            Assert.Equal(24, indexedDoubles.Count);
            Assert.All(indexedDoubles.Values!, x => Assert.True(double.IsNaN(x)));

            Building.HVAC.Classes.InternalGain internalGain = new() { Name = "Gain" };
            internalGain[InternalGainProfileType.TotalInternalGain] = new Profile([1.0, 2.0]);
            Building.HVAC.Classes.InternalGain internalGain_Copy = new(internalGain);
            Assert.Equal([1.0, 2.0], internalGain_Copy[InternalGainProfileType.TotalInternalGain]?.Values ?? []);
            Assert.Equal(internalGain.OccupantRadiantProportion, internalGain_Copy.OccupantRadiantProportion);

            Building.HVAC.Classes.Thermostat thermostat = new() { Name = "Thermostat" };
            thermostat[ThermostatProfileType.LowerLimit] = new Profile([20.0]);
            Assert.Equal([20.0], new Building.HVAC.Classes.Thermostat(thermostat)[ThermostatProfileType.LowerLimit]?.Values ?? []);
        }
    }
}
