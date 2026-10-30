using DiGi.Analytical.Building;
using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Interfaces;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Building.Regulation;
using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Analytical.Building.Regulation.Enums;
using DiGi.Core.Classes;
using DiGi.Core.Parameter.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the <see cref="ConstructionFlavour.TypicalAsBuilt"/> sets of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> against the TABULA web tool values (DiGi-Organization/DiGi.Analytical#15).
        /// <para>Spot values are checked on both sides of the 1945/1946 and 2008/2009 period boundaries, an apartment block before 1967 must take the multi-family constructions, and every set must hold exactly the ten roles TABULA covers.</para>
        /// </summary>
        [Fact]
        public void Create_ConstructionLibrary_PL_TypicalAsBuilt()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();

            static DateTimeOffset Date(int year, int month, int day)
            {
                return new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
            }

            double Value(DateTimeOffset dateTimeOffset, BuildingType buildingType, ConstructionRole constructionRole, Enum @enum)
            {
                ConstructionSet? constructionSet = constructionLibrary.GetConstructionSet(dateTimeOffset, buildingType, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
                IConstruction? construction = constructionLibrary.GetConstruction(constructionSet, constructionRole);
                return construction is not null && construction.TryGetValue(new EnumParameterDefinition(@enum), out double value) ? value : double.NaN;
            }

            (DateTimeOffset DateTimeOffset, BuildingType BuildingType, ConstructionRole ConstructionRole, Enum Enum, double Value)[] expecteds =
            [
                (Date(1850, 6, 1), BuildingType.SingleFamily, ConstructionRole.ExternalWall, PhysicalComponentConstructionParameter.ThermalTransmittance, 1.55),
                (Date(1945, 12, 31), BuildingType.SingleFamily, ConstructionRole.Window, OpeningConstructionParameter.ThermalTransmittance, 5.0),
                (Date(1945, 12, 31), BuildingType.SingleFamily, ConstructionRole.Window, OpeningConstructionParameter.TotalSolarEnergyTransmittance, 0.85),
                (Date(1945, 12, 31), BuildingType.SingleFamily, ConstructionRole.ExternalDoor, OpeningConstructionParameter.ThermalTransmittance, 5.1),
                (Date(1946, 1, 1), BuildingType.SingleFamily, ConstructionRole.ExternalDoor, OpeningConstructionParameter.ThermalTransmittance, 4.5),
                (Date(1945, 12, 31), BuildingType.Terraced, ConstructionRole.ExternalWall, PhysicalComponentConstructionParameter.ThermalTransmittance, 1.95),
                (Date(1946, 1, 1), BuildingType.Terraced, ConstructionRole.ExternalWall, PhysicalComponentConstructionParameter.ThermalTransmittance, 1.55),
                (Date(1975, 6, 1), BuildingType.MultiFamily, ConstructionRole.ExternalWall, PhysicalComponentConstructionParameter.ThermalTransmittance, 1.3),
                (Date(1975, 6, 1), BuildingType.MultiFamily, ConstructionRole.FloorOverUnheated, PhysicalComponentConstructionParameter.ThermalTransmittance, 1.3),
                (Date(1975, 6, 1), BuildingType.ApartmentBlock, ConstructionRole.FlatRoof, PhysicalComponentConstructionParameter.ThermalTransmittance, 0.33),
                (Date(2008, 12, 31), BuildingType.SingleFamily, ConstructionRole.Window, OpeningConstructionParameter.ThermalTransmittance, 1.2),
                (Date(2009, 1, 1), BuildingType.SingleFamily, ConstructionRole.Window, OpeningConstructionParameter.ThermalTransmittance, 1.1),
                (Date(2009, 1, 1), BuildingType.SingleFamily, ConstructionRole.Window, OpeningConstructionParameter.TotalSolarEnergyTransmittance, 0.5),
                (Date(2025, 1, 1), BuildingType.MultiFamily, ConstructionRole.StaircaseEntranceDoor, OpeningConstructionParameter.ThermalTransmittance, 1.4),
            ];

            foreach ((DateTimeOffset dateTimeOffset, BuildingType buildingType, ConstructionRole constructionRole, Enum @enum, double value) in expecteds)
            {
                Assert.True(value == Value(dateTimeOffset, buildingType, constructionRole, @enum), $"{dateTimeOffset:yyyy-MM-dd} {buildingType} {constructionRole} {@enum}: {Value(dateTimeOffset, buildingType, constructionRole, @enum)} instead of {value}");
            }

            // A window construction is a window construction for every glazed role, without an outline; a roof construction serves both roof roles.
            ConstructionSet? constructionSet_SingleFamily = constructionLibrary.GetConstructionSet(Date(1990, 1, 1), BuildingType.SingleFamily, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
            Assert.IsType<WindowConstruction>(constructionLibrary.GetConstruction(constructionSet_SingleFamily, ConstructionRole.BalconyDoor));
            Assert.IsType<DoorConstruction>(constructionLibrary.GetConstruction(constructionSet_SingleFamily, ConstructionRole.ExternalDoor));
            Assert.IsType<RoofConstruction>(constructionLibrary.GetConstruction(constructionSet_SingleFamily, ConstructionRole.FlatRoof));
            Assert.IsType<FloorConstruction>(constructionLibrary.GetConstruction(constructionSet_SingleFamily, ConstructionRole.GroundFloor));
            Assert.Null((constructionLibrary.GetConstruction(constructionSet_SingleFamily, ConstructionRole.Window) as WindowConstruction)?.Geometry);
            Assert.Null(constructionLibrary.GetConstruction(constructionSet_SingleFamily, ConstructionRole.PartyWall));

            // TABULA has no apartment block before 1967: the multi-family constructions stand in for it.
            ConstructionSet? constructionSet_ApartmentBlock = constructionLibrary.GetConstructionSet(Date(1950, 1, 1), BuildingType.ApartmentBlock, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
            ConstructionSet? constructionSet_MultiFamily = constructionLibrary.GetConstructionSet(Date(1950, 1, 1), BuildingType.MultiFamily, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
            Assert.NotNull(constructionSet_ApartmentBlock);
            Assert.NotNull(constructionSet_MultiFamily);
            Assert.Contains("PL.N.MFH.02.Gen.ReEx.001", constructionSet_ApartmentBlock.Source);
            foreach (ConstructionRole constructionRole in constructionSet_MultiFamily.GetConstructionRoles())
            {
                Assert.True(Core.Query.Equals(constructionSet_MultiFamily.GetConstructionReference(constructionRole), constructionSet_ApartmentBlock.GetConstructionReference(constructionRole)), constructionRole.ToString());
            }

            HashSet<ConstructionRole> constructionRoles_Expected =
            [
                ConstructionRole.ExternalWall, ConstructionRole.Roof, ConstructionRole.FlatRoof, ConstructionRole.GroundFloor, ConstructionRole.FloorOverUnheated,
                ConstructionRole.Window, ConstructionRole.RoofWindow, ConstructionRole.BalconyDoor, ConstructionRole.ExternalDoor, ConstructionRole.StaircaseEntranceDoor,
            ];

            int count = 0;
            foreach (ConstructionSet constructionSet in constructionLibrary.GetConstructionSets())
            {
                if (constructionSet.ConstructionFlavour != ConstructionFlavour.TypicalAsBuilt)
                {
                    continue;
                }

                count++;
                Assert.Null(constructionSet.ClimateZone);
                Assert.True(constructionRoles_Expected.SetEquals(constructionSet.GetConstructionRoles()), $"{constructionSet.BuildingType} {constructionSet.StartDate:yyyy}: {string.Join(", ", constructionSet.GetConstructionRoles())}");
                foreach (ConstructionRole constructionRole in constructionSet.GetConstructionRoles())
                {
                    Assert.NotNull(constructionLibrary.GetConstruction(constructionSet, constructionRole));
                }
            }

            Assert.Equal(7 * 4, count);
        }

        /// <summary>
        /// Tests the <see cref="ConstructionFlavour.Regulation"/> sets of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> against the regulated heat transfer coefficients manager (DiGi-Organization/DiGi.Analytical#15).
        /// <para>Every construction's declared U must equal the stage's U(max) for its role, building type and zone, and every role with a limit must have a construction; a role without one (multi-family walls 1998-2008, ground floors before 2009) must have none. Before 2014 a zone IV or V set must win for windows, and before 1998-04-29 there is no set.</para>
        /// </summary>
        [Fact]
        public void Create_ConstructionLibrary_PL_Regulation()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();

            static DateTimeOffset Date(int year, int month, int day)
            {
                return new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
            }

            static double Declared(IConstruction? construction)
            {
                Enum @enum = construction is IOpeningConstruction ? OpeningConstructionParameter.ThermalTransmittance : PhysicalComponentConstructionParameter.ThermalTransmittance;
                return construction is not null && construction.TryGetValue(new EnumParameterDefinition(@enum), out double value) ? value : double.NaN;
            }

            double Value(DateTimeOffset dateTimeOffset, BuildingType buildingType, ClimateZone climateZone, ConstructionRole constructionRole)
            {
                ConstructionSet? constructionSet = constructionLibrary.GetConstructionSet(dateTimeOffset, buildingType, climateZone, ConstructionFlavour.Regulation);
                return Declared(constructionLibrary.GetConstruction(constructionSet, constructionRole));
            }

            List<ConstructionRole> constructionRoles = [.. (ConstructionRole[])Enum.GetValues(typeof(ConstructionRole))];
            constructionRoles.Remove(ConstructionRole.Undefined);

            int count = 0;
            foreach (ConstructionSet constructionSet in constructionLibrary.GetConstructionSets())
            {
                if (constructionSet.ConstructionFlavour != ConstructionFlavour.Regulation)
                {
                    continue;
                }

                count++;
                IRegulatedHeatTransferCoefficients? regulatedHeatTransferCoefficients = regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(constructionSet.StartDate.DateTime);
                Assert.NotNull(regulatedHeatTransferCoefficients);
                Assert.Contains(regulatedHeatTransferCoefficients.RegulationAct!.Reference!, constructionSet.Source);

                ClimateZone climateZone = constructionSet.ClimateZone ?? ClimateZone.III;
                foreach (ConstructionRole constructionRole in constructionRoles)
                {
                    double expected = regulatedHeatTransferCoefficients.RegulatedHeatTransferCoefficient(constructionRole, constructionSet.BuildingType, climateZone, true);
                    double declared = Declared(constructionLibrary.GetConstruction(constructionSet, constructionRole));
                    string message = $"{constructionSet.StartDate:yyyy-MM-dd} {constructionSet.BuildingType} {constructionSet.ClimateZone} {constructionRole}: {declared} instead of {expected}";
                    if (double.IsNaN(expected))
                    {
                        Assert.True(constructionSet.GetConstructionReference(constructionRole) is null, message);
                        continue;
                    }

                    Assert.True(declared.IsCompliant(expected, 1e-9), message);
                    Assert.True(expected == declared, message);
                }
            }

            // Six stages x four building types, and zones IV and V for the three stages before 2014,
            // except the 1997 multi-family building and apartment block, which have no limit to depend on a zone.
            Assert.Equal((6 * 4) + (3 * 4 * 2) - (2 * 2), count);

            // Before the 1997 annex came into force there is no regulation set: callers fall back to TypicalAsBuilt.
            Assert.Null(constructionLibrary.GetConstructionSet(Date(1998, 4, 28), BuildingType.SingleFamily, ClimateZone.III, ConstructionFlavour.Regulation));
            Assert.Equal(0.3, Value(Date(1998, 4, 29), BuildingType.SingleFamily, ClimateZone.III, ConstructionRole.ExternalWall));

            // The 1997 annex has no multi-family table: the set exists and is empty.
            ConstructionSet? constructionSet_1997 = constructionLibrary.GetConstructionSet(Date(2000, 1, 1), BuildingType.MultiFamily, ClimateZone.III, ConstructionFlavour.Regulation);
            Assert.NotNull(constructionSet_1997);
            Assert.Empty(constructionSet_1997.GetConstructionRoles());

            // 2002: a multi-family building has window limits but no wall limit; the zone IV set wins for windows.
            Assert.True(double.IsNaN(Value(Date(2005, 1, 1), BuildingType.MultiFamily, ClimateZone.III, ConstructionRole.ExternalWall)));
            Assert.Equal(2.6, Value(Date(2005, 1, 1), BuildingType.MultiFamily, ClimateZone.II, ConstructionRole.Window));
            Assert.Equal(2.0, Value(Date(2005, 1, 1), BuildingType.MultiFamily, ClimateZone.IV, ConstructionRole.Window));
            Assert.Equal(2.6, Value(Date(2005, 1, 1), BuildingType.MultiFamily, ClimateZone.Undefined, ConstructionRole.Window));

            // A floor on ground had no U(max) before 2009.
            Assert.True(double.IsNaN(Value(Date(2008, 12, 31), BuildingType.SingleFamily, ClimateZone.III, ConstructionRole.GroundFloor)));
            Assert.Equal(0.45, Value(Date(2009, 1, 1), BuildingType.SingleFamily, ClimateZone.III, ConstructionRole.GroundFloor));
            Assert.Equal(1.7, Value(Date(2010, 1, 1), BuildingType.SingleFamily, ClimateZone.V, ConstructionRole.Window));

            // 2014 onwards: one set for every zone.
            Assert.Equal(0.25, Value(Date(2014, 6, 1), BuildingType.SingleFamily, ClimateZone.III, ConstructionRole.ExternalWall));
            Assert.Equal(1.3, Value(Date(2014, 6, 1), BuildingType.SingleFamily, ClimateZone.V, ConstructionRole.Window));
            Assert.Equal(0.9, Value(Date(2021, 6, 1), BuildingType.MultiFamily, ClimateZone.III, ConstructionRole.Window));
            Assert.Equal(0.15, Value(Date(2021, 6, 1), BuildingType.ApartmentBlock, ClimateZone.III, ConstructionRole.FlatRoof));
            Assert.Equal(1.3, Value(Date(2021, 6, 1), BuildingType.ApartmentBlock, ClimateZone.III, ConstructionRole.DoorToUnheatedSpace));
        }

        /// <summary>
        /// Tests the embedded copy of the Polish construction library against <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/>, and the library's serialization and import into a building model (DiGi-Organization/DiGi.Analytical#15).
        /// <para>The fact writes the factory output to <c>reports/ConstructionLibrary_PL.json</c>. When it fails on the comparison, copy that file to <c>DiGi.Analytical.Building.Regulation/Resources/</c> and rebuild: the embedded file must be the factory's output, byte for byte.</para>
        /// </summary>
        [Fact]
        public void ConstructionLibrary_PL_Resource()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();
            string? json = Core.Convert.ToSystem_String(constructionLibrary);
            Assert.False(string.IsNullOrEmpty(json));

            string? path_Reports = Core.xUnit.Query.ReportsDirectory(Assembly.GetExecutingAssembly());
            Assert.NotNull(path_Reports);
            File.WriteAllText(System.IO.Path.Combine(path_Reports, "ConstructionLibrary_PL.json"), json);

            // The identifiers are derived from stable keys, so a second library serializes to the same text.
            Assert.Equal(json, Core.Convert.ToSystem_String(Building.Regulation.Create.ConstructionLibrary_PL()));

            using (Stream? stream = typeof(Building.Regulation.Query).Assembly.GetManifestResourceStream(Building.Regulation.Query.EmbeddedConstructionLibrary_PL_ResourceName))
            {
                Assert.True(stream is not null, $"{Building.Regulation.Query.EmbeddedConstructionLibrary_PL_ResourceName} is not embedded");
                using StreamReader streamReader = new(stream);
                Assert.True(json == streamReader.ReadToEnd(), "The embedded library differs from Create.ConstructionLibrary_PL(): copy reports/ConstructionLibrary_PL.json to DiGi.Analytical.Building.Regulation/Resources/ and rebuild.");
            }

            ConstructionLibrary? constructionLibrary_Embedded = Building.Regulation.Query.EmbeddedConstructionLibrary_PL();
            Assert.NotNull(constructionLibrary_Embedded);
            Assert.Equal(json, Core.Convert.ToSystem_String(constructionLibrary_Embedded));
            Assert.Equal(Building.Regulation.Create.ConstructionLibrary_PL_Version, constructionLibrary_Embedded.Version);
            Assert.Equal(Core.Create.Guid("PL.ConstructionLibrary"), constructionLibrary_Embedded.Guid);

            Assert.Equal(json, Core.Convert.ToSystem_String(new ConstructionLibrary(constructionLibrary)));
            Core.xUnit.Query.SerializationCheck(constructionLibrary);

            // A library construction imports into a building model, and its declared U is what the model reports.
            ConstructionSet? constructionSet = constructionLibrary_Embedded.GetConstructionSet(new DateTimeOffset(1975, 6, 1, 0, 0, 0, TimeSpan.Zero), BuildingType.MultiFamily, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
            IPhysicalComponentConstruction? physicalComponentConstruction = constructionLibrary_Embedded.GetConstruction(constructionSet, ConstructionRole.ExternalWall) as IPhysicalComponentConstruction;
            IOpeningConstruction? openingConstruction = constructionLibrary_Embedded.GetConstruction(constructionSet, ConstructionRole.Window) as IOpeningConstruction;
            Assert.NotNull(physicalComponentConstruction);
            Assert.NotNull(openingConstruction);

            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Update(constructionLibrary_Embedded.ConstructionRelationCluster, physicalComponentConstruction));
            Assert.True(buildingModel.Update(constructionLibrary_Embedded.ConstructionRelationCluster, openingConstruction));
            Assert.Equal(1.3, buildingModel.ThermalTransmittance(physicalComponentConstruction, HeatFlowDirection.Horizontal));
            Assert.Equal(2.6, buildingModel.ThermalTransmittance(openingConstruction));
        }
    }
}
