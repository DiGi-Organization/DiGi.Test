using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Interfaces;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Building.Regulation;
using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Analytical.Building.Regulation.Enums;
using DiGi.Analytical.Classes;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests <see cref="Building.Regulation.Query.BuildingRegulation(short?, int)"/> and <see cref="Building.Regulation.Query.ConstructionPeriod(short?)"/> on every boundary year, just inside and just outside (DiGi-Organization/DiGi.Analytical#8).
        /// <para>The regime is chosen for the design year, year built minus the design lag; the TABULA construction period for the year built itself.</para>
        /// </summary>
        [Fact]
        public void Query_BuildingRegulation_ConstructionPeriod()
        {
            (int Year, BuildingRegulation BuildingRegulation)[] buildingRegulations =
            [
                (1927, BuildingRegulation.PL_Before1928), (1928, BuildingRegulation.PL_1928),
                (1960, BuildingRegulation.PL_1928), (1961, BuildingRegulation.PL_1961),
                (1979, BuildingRegulation.PL_1961), (1980, BuildingRegulation.PL_1980),
                (1994, BuildingRegulation.PL_1980), (1995, BuildingRegulation.PL_1994),
                (2002, BuildingRegulation.PL_1994), (2003, BuildingRegulation.PL_WT2002),
                (2008, BuildingRegulation.PL_WT2002), (2009, BuildingRegulation.PL_WT2009),
                (2013, BuildingRegulation.PL_WT2009), (2014, BuildingRegulation.PL_WT2014),
                (2016, BuildingRegulation.PL_WT2014), (2017, BuildingRegulation.PL_WT2017),
                (2020, BuildingRegulation.PL_WT2017), (2021, BuildingRegulation.PL_WT2021),
            ];

            foreach ((int year, BuildingRegulation buildingRegulation) in buildingRegulations)
            {
                Assert.True(Building.Regulation.Query.BuildingRegulation((short)year, 0) == buildingRegulation, $"design year {year}: {Building.Regulation.Query.BuildingRegulation((short)year, 0)} instead of {buildingRegulation}");
                Assert.True(Building.Regulation.Query.BuildingRegulation((short)(year + 2)) == buildingRegulation, $"built {year + 2}, lag 2: {Building.Regulation.Query.BuildingRegulation((short)(year + 2))} instead of {buildingRegulation}");
            }

            Assert.Equal(BuildingRegulation.Undefined, Building.Regulation.Query.BuildingRegulation(null));

            (int Year, ConstructionPeriod ConstructionPeriod)[] constructionPeriods =
            [
                (1945, ConstructionPeriod.Until1945), (1946, ConstructionPeriod.From1946To1966),
                (1966, ConstructionPeriod.From1946To1966), (1967, ConstructionPeriod.From1967To1985),
                (1985, ConstructionPeriod.From1967To1985), (1986, ConstructionPeriod.From1986To1992),
                (1992, ConstructionPeriod.From1986To1992), (1993, ConstructionPeriod.From1993To2002),
                (2002, ConstructionPeriod.From1993To2002), (2003, ConstructionPeriod.From2003To2008),
                (2008, ConstructionPeriod.From2003To2008), (2009, ConstructionPeriod.From2009),
            ];

            foreach ((int year, ConstructionPeriod constructionPeriod) in constructionPeriods)
            {
                Assert.True(Building.Regulation.Query.ConstructionPeriod((short)year) == constructionPeriod, $"{year}: {Building.Regulation.Query.ConstructionPeriod((short)year)} instead of {constructionPeriod}");
            }

            Assert.Equal(ConstructionPeriod.Undefined, Building.Regulation.Query.ConstructionPeriod(null));
        }

        /// <summary>
        /// Tests <see cref="ConstructionSet"/> and <see cref="ConstructionLibrary"/> (DiGi-Organization/DiGi.Analytical#8): construction, a copy and a JSON round trip with every member populated; lookups by date on both sides of every boundary (start inclusive, end exclusive); a set for the building's own climatic zone winning over a set for every zone; and a library construction brought into a building model with its layers.
        /// </summary>
        [Fact]
        public void ConstructionLibrary()
        {
            OpaqueMaterial opaqueMaterial_Brick = new("Hollow ceramic block") { Conductivity = 0.30 };
            OpaqueMaterial opaqueMaterial_EPS = new("EPS") { Conductivity = 0.04 };
            WallConstruction wallConstruction_2014 = new() { Name = "Ceramic block + EPS 120 mm" };
            WallConstruction wallConstruction_2017 = new() { Name = "Ceramic block + EPS 140 mm" };
            WallConstruction wallConstruction_ZoneV = new() { Name = "Ceramic block + EPS 150 mm (zone V)" };
            WindowConstruction windowConstruction = new("PVC window", OpeningFixture_Rectangle(0, 0, 1.2, 1.5, (0.1, 0.1, 1.1, 1.4)));

            ConstructionLibrary constructionLibrary = new("Test library", "0.1", "Two stages and a zone-specific set");
            ConstructionRelationCluster constructionRelationCluster = constructionLibrary.ConstructionRelationCluster;
            Analytical.Interfaces.IAnalyticalGuidObject[] analyticalGuidObjects = [opaqueMaterial_Brick, opaqueMaterial_EPS, wallConstruction_2014, wallConstruction_2017, wallConstruction_ZoneV, windowConstruction];
            foreach (Analytical.Interfaces.IAnalyticalGuidObject analyticalGuidObject in analyticalGuidObjects)
            {
                Assert.True(constructionRelationCluster.Add(analyticalGuidObject));
            }

            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction_2014, opaqueMaterial_EPS, 0.12));
            Assert.NotNull(Building.Create.StructureLayer(constructionRelationCluster, wallConstruction_2014, opaqueMaterial_Brick, 0.25));

            DateTimeOffset dateTimeOffset_2014 = new(2014, 1, 1, 0, 0, 0, TimeSpan.Zero);
            DateTimeOffset dateTimeOffset_2017 = new(2017, 1, 1, 0, 0, 0, TimeSpan.Zero);

            ConstructionSet constructionSet_2014 = new(ConstructionFlavour.Regulation, BuildingType.SingleFamily, null, dateTimeOffset_2014, dateTimeOffset_2017, "Dz.U. 2013 poz. 926, 2014 stage");
            Assert.True(constructionSet_2014.SetConstruction(ConstructionRole.ExternalWall, wallConstruction_2014));
            Assert.True(constructionSet_2014.SetConstruction(ConstructionRole.Window, windowConstruction));
            Assert.False(constructionSet_2014.SetConstruction(ConstructionRole.Undefined, wallConstruction_2014));
            Assert.False(constructionSet_2014.SetConstruction(ConstructionRole.Roof, null));

            ConstructionSet constructionSet_2017 = new(ConstructionFlavour.Regulation, BuildingType.SingleFamily, null, dateTimeOffset_2017, null, "Dz.U. 2013 poz. 926, 2017 stage");
            Assert.True(constructionSet_2017.SetConstruction(ConstructionRole.ExternalWall, wallConstruction_2017));

            ConstructionSet constructionSet_ZoneV = new(ConstructionFlavour.Regulation, BuildingType.SingleFamily, ClimateZone.V, dateTimeOffset_2014, dateTimeOffset_2017, "Zone V variant");
            Assert.True(constructionSet_ZoneV.SetConstruction(ConstructionRole.ExternalWall, wallConstruction_ZoneV));

            Assert.True(constructionLibrary.Add(constructionSet_2014));
            Assert.True(constructionLibrary.Add(constructionSet_2017));
            Assert.True(constructionLibrary.Add(constructionSet_ZoneV));
            Assert.False(constructionLibrary.Add(null));

            // Start inclusive, end exclusive, by calendar date.
            Assert.False(constructionSet_2014.Includes(new DateTimeOffset(2013, 12, 31, 23, 0, 0, TimeSpan.Zero)));
            Assert.True(constructionSet_2014.Includes(dateTimeOffset_2014));
            Assert.True(constructionSet_2014.Includes(new DateTimeOffset(2016, 12, 31, 0, 0, 0, TimeSpan.Zero)));
            Assert.False(constructionSet_2014.Includes(dateTimeOffset_2017));
            Assert.True(constructionSet_2017.Includes(new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero)));

            string? WallName(DateTimeOffset dateTimeOffset, ClimateZone climateZone)
            {
                ConstructionSet? constructionSet = constructionLibrary.GetConstructionSet(dateTimeOffset, BuildingType.SingleFamily, climateZone, ConstructionFlavour.Regulation);
                return constructionLibrary.GetConstruction(constructionSet, ConstructionRole.ExternalWall)?.Name;
            }

            Assert.Null(WallName(new DateTimeOffset(2013, 12, 31, 0, 0, 0, TimeSpan.Zero), ClimateZone.III));
            Assert.Equal("Ceramic block + EPS 120 mm", WallName(dateTimeOffset_2014, ClimateZone.III));
            Assert.Equal("Ceramic block + EPS 120 mm", WallName(new DateTimeOffset(2016, 12, 31, 0, 0, 0, TimeSpan.Zero), ClimateZone.III));
            Assert.Equal("Ceramic block + EPS 140 mm", WallName(dateTimeOffset_2017, ClimateZone.III));

            // The building's own zone wins over every zone; once the zone set ends the general one applies.
            Assert.Equal("Ceramic block + EPS 150 mm (zone V)", WallName(dateTimeOffset_2014, ClimateZone.V));
            Assert.Equal("Ceramic block + EPS 140 mm", WallName(dateTimeOffset_2017, ClimateZone.V));

            // Other flavours and types find nothing.
            Assert.Null(constructionLibrary.GetConstructionSet(dateTimeOffset_2014, BuildingType.SingleFamily, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt));
            Assert.Null(constructionLibrary.GetConstructionSet(dateTimeOffset_2014, BuildingType.MultiFamily, ClimateZone.III, ConstructionFlavour.Regulation));
            Assert.Null(constructionLibrary.GetConstruction(constructionSet_2017, ConstructionRole.Window));

            Assert.Equal(3, constructionLibrary.GetConstructionSets().Count);
            Assert.Equal(2, constructionSet_2014.GetConstructionRoles().Count);

            // A library construction arrives in a building model with its layers.
            IConstruction? construction = constructionLibrary.GetConstruction(constructionLibrary.GetConstructionSet(dateTimeOffset_2014, BuildingType.SingleFamily, ClimateZone.III, ConstructionFlavour.Regulation), ConstructionRole.ExternalWall);
            Assert.NotNull(construction);
            BuildingModel buildingModel = new();
            Assert.True(buildingModel.Update(constructionLibrary.ConstructionRelationCluster, construction));
            Assert.Equal(2, buildingModel.GetStructureLayers(construction as IPhysicalComponentConstruction)?.Count);

            // Copies and JSON keep every member.
            ConstructionSet constructionSet_Copy = new(constructionSet_ZoneV);
            Assert.Equal(ConstructionFlavour.Regulation, constructionSet_Copy.ConstructionFlavour);
            Assert.Equal(BuildingType.SingleFamily, constructionSet_Copy.BuildingType);
            Assert.Equal(ClimateZone.V, constructionSet_Copy.ClimateZone);
            Assert.Equal(dateTimeOffset_2014, constructionSet_Copy.StartDate);
            Assert.Equal(dateTimeOffset_2017, constructionSet_Copy.EndDate);
            Assert.Equal("Zone V variant", constructionSet_Copy.Source);
            Assert.Equal(wallConstruction_ZoneV.Guid, constructionSet_Copy.GetConstructionReference(ConstructionRole.ExternalWall)?.Guid);

            ConstructionSet? constructionSet_Json = Core.Convert.ToDiGi<ConstructionSet>(Core.Convert.ToSystem_String(constructionSet_2014) ?? string.Empty)?.FirstOrDefault();
            Assert.NotNull(constructionSet_Json);
            Assert.Null(constructionSet_Json.ClimateZone);
            Assert.Equal(dateTimeOffset_2017, constructionSet_Json.EndDate);
            Assert.Equal(windowConstruction.Guid, constructionSet_Json.GetConstructionReference(ConstructionRole.Window)?.Guid);

            ConstructionLibrary constructionLibrary_Copy = new(constructionLibrary);
            Assert.Equal(constructionLibrary.Guid, constructionLibrary_Copy.Guid);
            Assert.Equal("Test library", constructionLibrary_Copy.Name);
            Assert.Equal("0.1", constructionLibrary_Copy.Version);
            Assert.Equal("Two stages and a zone-specific set", constructionLibrary_Copy.Description);
            Assert.NotSame(constructionLibrary.ConstructionRelationCluster, constructionLibrary_Copy.ConstructionRelationCluster);
            Assert.Equal("Ceramic block + EPS 150 mm (zone V)", constructionLibrary_Copy.GetConstruction(constructionLibrary_Copy.GetConstructionSet(dateTimeOffset_2014, BuildingType.SingleFamily, ClimateZone.V, ConstructionFlavour.Regulation), ConstructionRole.ExternalWall)?.Name);

            ConstructionLibrary? constructionLibrary_Json = Core.Convert.ToDiGi<ConstructionLibrary>(Core.Convert.ToSystem_String(constructionLibrary) ?? string.Empty)?.FirstOrDefault();
            Assert.NotNull(constructionLibrary_Json);
            Assert.Equal(3, constructionLibrary_Json.GetConstructionSets().Count);
            Assert.Equal(2, constructionLibrary_Json.ConstructionRelationCluster.GetStructureLayers(wallConstruction_2014)?.Count);
            Assert.Equal("Ceramic block + EPS 140 mm", constructionLibrary_Json.GetConstruction(constructionLibrary_Json.GetConstructionSet(dateTimeOffset_2017, BuildingType.SingleFamily, ClimateZone.I, ConstructionFlavour.Regulation), ConstructionRole.ExternalWall)?.Name);

            Core.xUnit.Query.SerializationCheck(constructionSet_2014);
            Core.xUnit.Query.SerializationCheck(constructionSet_ZoneV);
            Core.xUnit.Query.SerializationCheck(constructionLibrary);
        }

        /// <summary>
        /// Tests <see cref="Building.Regulation.Query.RegulatedHeatTransferCoefficient(IRegulatedHeatTransferCoefficients?, ConstructionRole, BuildingType, ClimateZone, bool)"/> against the real stages of the regulated heat transfer coefficients manager, and the tolerance boundary of <see cref="Building.Regulation.Query.IsCompliant(double, double, double)"/> (DiGi-Organization/DiGi.Analytical#8).
        /// <para>1997: single-family walls 0.50, or 0.30 when layered with insulation, windows 2.6 / 2.0 by climatic zone, nothing for a multi-family building. 2002: multi-family walls have no limit (E ≤ E0), multi-family windows do. 2009: the floor on ground 0.45, a window without a zone has no limit. 2014: a door to an unheated space 1.7. 2021: window 0.9. A garage door has no limit in any stage. Missing values are never compliant.</para>
        /// </summary>
        [Fact]
        public void Query_RegulatedHeatTransferCoefficient()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();

            IRegulatedHeatTransferCoefficients? Stage(int year)
            {
                return regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(year, 6, 1));
            }

            (int Year, ConstructionRole ConstructionRole, BuildingType BuildingType, ClimateZone ClimateZone, bool InsulatedLayered, double Expected)[] cases =
            [
                (2000, ConstructionRole.ExternalWall, BuildingType.SingleFamily, ClimateZone.III, false, 0.50),
                (2000, ConstructionRole.ExternalWall, BuildingType.Terraced, ClimateZone.III, true, 0.30),
                (2000, ConstructionRole.Window, BuildingType.SingleFamily, ClimateZone.III, false, 2.6),
                (2000, ConstructionRole.RoofWindow, BuildingType.SingleFamily, ClimateZone.V, false, 2.0),
                (2000, ConstructionRole.ExternalDoor, BuildingType.SingleFamily, ClimateZone.III, false, 2.6),
                (2000, ConstructionRole.ExternalWall, BuildingType.MultiFamily, ClimateZone.III, false, double.NaN),
                (2000, ConstructionRole.Window, BuildingType.ApartmentBlock, ClimateZone.III, false, double.NaN),
                (2005, ConstructionRole.ExternalWall, BuildingType.MultiFamily, ClimateZone.III, false, double.NaN),
                (2005, ConstructionRole.Roof, BuildingType.ApartmentBlock, ClimateZone.III, false, double.NaN),
                (2005, ConstructionRole.Window, BuildingType.MultiFamily, ClimateZone.II, false, 2.6),
                (2005, ConstructionRole.BalconyDoor, BuildingType.MultiFamily, ClimateZone.IV, false, 2.0),
                (2005, ConstructionRole.RoofWindow, BuildingType.SingleFamily, ClimateZone.II, false, 2.0),
                (2005, ConstructionRole.FloorOverUnheated, BuildingType.SingleFamily, ClimateZone.II, false, 0.60),
                (2005, ConstructionRole.GroundFloor, BuildingType.SingleFamily, ClimateZone.II, false, double.NaN),
                (2010, ConstructionRole.GroundFloor, BuildingType.MultiFamily, ClimateZone.III, false, 0.45),
                (2010, ConstructionRole.Window, BuildingType.SingleFamily, ClimateZone.V, false, 1.7),
                (2010, ConstructionRole.Window, BuildingType.SingleFamily, ClimateZone.Undefined, false, double.NaN),
                (2015, ConstructionRole.DoorToUnheatedSpace, BuildingType.MultiFamily, ClimateZone.Undefined, false, 1.7),
                (2015, ConstructionRole.FlatRoof, BuildingType.ApartmentBlock, ClimateZone.Undefined, false, 0.20),
                (2021, ConstructionRole.Window, BuildingType.SingleFamily, ClimateZone.Undefined, false, 0.9),
                (2021, ConstructionRole.StaircaseEntranceDoor, BuildingType.ApartmentBlock, ClimateZone.Undefined, false, 1.3),
                (2021, ConstructionRole.GarageDoor, BuildingType.SingleFamily, ClimateZone.Undefined, false, double.NaN),
                (2021, ConstructionRole.PartyWall, BuildingType.Terraced, ClimateZone.Undefined, false, double.NaN),
            ];

            foreach ((int year, ConstructionRole constructionRole, BuildingType buildingType, ClimateZone climateZone, bool insulatedLayered, double expected) in cases)
            {
                double value = Stage(year).RegulatedHeatTransferCoefficient(constructionRole, buildingType, climateZone, insulatedLayered);
                Assert.True(double.IsNaN(expected) ? double.IsNaN(value) : value == expected, $"{year} {constructionRole} {buildingType} {climateZone} layered {insulatedLayered}: {value} instead of {expected}");
            }

            Assert.True(double.IsNaN(Stage(1990).RegulatedHeatTransferCoefficient(ConstructionRole.ExternalWall, BuildingType.SingleFamily, ClimateZone.III)));
            Assert.True(double.IsNaN(Stage(2021).RegulatedHeatTransferCoefficient(ConstructionRole.ExternalWall, BuildingType.Undefined, ClimateZone.III)));

            // U ≤ U(max) within the tolerance, both sides of the boundary; a missing value never passes.
            double tolerance = 0.001;
            Assert.True(0.2005.IsCompliant(0.20, tolerance));
            Assert.False(0.2015.IsCompliant(0.20, tolerance));
            Assert.True(0.15.IsCompliant(0.20, tolerance));
            Assert.False(double.NaN.IsCompliant(0.20, tolerance));
            Assert.False(0.15.IsCompliant(double.NaN, tolerance));
        }
    }
}
