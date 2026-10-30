using DiGi.Analytical.Building.Classes;
using DiGi.Analytical.Building.Enums;
using DiGi.Analytical.Building.Interfaces;
using DiGi.Analytical.Building.Regulation.Classes;
using DiGi.Analytical.Building.Regulation.Enums;
using DiGi.Core.Parameter.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the serialization and copying of <see cref="TypologyData"/> and of a <see cref="ConstructionSet"/> carrying one (DiGi-Organization/DiGi.Analytical#17).
        /// </summary>
        [Fact]
        public void TypologyData_Serialization()
        {
            TypologyData typologyData = new(0.3, 0.1, FloorBoundary.Cellar, 0.3, RoofType.Flat, 0.5, 0.9, 1, "TABULA building PL.N.TH.05.Gen.ReEx.001");
            Assert.Equal(0.3, typologyData.FrameAreaFraction);
            Assert.Equal(0.1, typologyData.ThermalBridgingSurcharge);
            Assert.Equal(FloorBoundary.Cellar, typologyData.FloorBoundary);
            Assert.Equal(0.3, typologyData.AdditionalFloorResistance);
            Assert.Equal(RoofType.Flat, typologyData.RoofType);
            Assert.Equal(0.5, typologyData.FloorTemperatureReductionFactor);
            Assert.Equal(0.9, typologyData.NonPerpendicularIncidenceFactor);
            Assert.Equal(1, typologyData.AttachedNeighbours);
            Assert.Equal("TABULA building PL.N.TH.05.Gen.ReEx.001", typologyData.Source);

            TypologyData typologyData_Copy = new(typologyData);
            Assert.Equal(Core.Convert.ToSystem_String(typologyData), Core.Convert.ToSystem_String(typologyData_Copy));
            Assert.Equal(typologyData.AttachedNeighbours, typologyData_Copy.AttachedNeighbours);
            Assert.Equal(typologyData.RoofType, typologyData_Copy.RoofType);

            string? json = Core.Convert.ToSystem_String(typologyData);
            TypologyData? typologyData_Json = Core.Convert.ToDiGi<TypologyData>(json)?.FirstOrDefault();
            Assert.NotNull(typologyData_Json);
            Assert.Equal(FloorBoundary.Cellar, typologyData_Json.FloorBoundary);
            Assert.Equal(0.9, typologyData_Json.NonPerpendicularIncidenceFactor);
            Core.xUnit.Query.SerializationCheck(typologyData);

            ConstructionSet constructionSet = new(ConstructionFlavour.TypicalAsBuilt, BuildingType.Terraced, null, new DateTimeOffset(1993, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2003, 1, 1, 0, 0, 0, TimeSpan.Zero), "TABULA", typologyData);
            constructionSet.SetConstruction(ConstructionRole.ExternalWall, new WallConstruction(Guid.NewGuid(), null));
            Assert.Equal(Core.Convert.ToSystem_String(typologyData), Core.Convert.ToSystem_String(constructionSet.TypologyData));

            ConstructionSet constructionSet_Copy = new(constructionSet);
            Assert.Equal(Core.Convert.ToSystem_String(constructionSet), Core.Convert.ToSystem_String(constructionSet_Copy));
            Assert.Equal(0.3, constructionSet_Copy.TypologyData?.AdditionalFloorResistance);
            Core.xUnit.Query.SerializationCheck(constructionSet);

            ConstructionSet constructionSet_Regulation = new(ConstructionFlavour.Regulation, BuildingType.Terraced, ClimateZone.IV, new DateTimeOffset(2009, 1, 1, 0, 0, 0, TimeSpan.Zero), null, null, null);
            Assert.Null(constructionSet_Regulation.TypologyData);
            Assert.Null(new ConstructionSet(constructionSet_Regulation).TypologyData);
        }

        /// <summary>
        /// Tests the <see cref="TypologyData"/> of the <see cref="ConstructionFlavour.TypicalAsBuilt"/> sets of <see cref="Building.Regulation.Create.ConstructionLibrary_PL"/> against the TABULA records, and the floor U-values without the cellar resistance (DiGi-Organization/DiGi.Analytical#17).
        /// <para>Every TypicalAsBuilt set carries typology data and no Regulation set does. Over an unheated cellar the floor construction carries its own U, and 1 / (1 / U + R) gives back the U TABULA publishes for that building. Every TABULA construction code gives exactly one construction.</para>
        /// </summary>
        [Fact]
        public void ConstructionLibrary_PL_TypologyData()
        {
            ConstructionLibrary constructionLibrary = Building.Regulation.Create.ConstructionLibrary_PL();

            static DateTimeOffset Date(int year, int month, int day)
            {
                return new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
            }

            int count = 0;
            foreach (ConstructionSet constructionSet in constructionLibrary.GetConstructionSets())
            {
                TypologyData? typologyData = constructionSet.TypologyData;
                if (constructionSet.ConstructionFlavour != ConstructionFlavour.TypicalAsBuilt)
                {
                    Assert.Null(typologyData);
                    continue;
                }

                count++;
                Assert.NotNull(typologyData);
                string message = $"{constructionSet.BuildingType} {constructionSet.StartDate:yyyy}";
                Assert.Equal(0.3, typologyData.FrameAreaFraction);
                Assert.Equal(0.5, typologyData.FloorTemperatureReductionFactor);
                Assert.Equal(0.9, typologyData.NonPerpendicularIncidenceFactor);
                Assert.True(typologyData.ThermalBridgingSurcharge is >= 0.05 and <= 0.15, message);
                Assert.NotEqual(FloorBoundary.Undefined, typologyData.FloorBoundary);
                Assert.NotEqual(RoofType.Undefined, typologyData.RoofType);
                Assert.Equal(typologyData.FloorBoundary == FloorBoundary.Cellar ? 0.3 : 0.0, typologyData.AdditionalFloorResistance);
                Assert.Equal(constructionSet.BuildingType == BuildingType.Terraced ? 1 : 0, typologyData.AttachedNeighbours);
                Assert.Contains("TABULA building PL.N.", typologyData.Source);
                Assert.Contains("TypologyData", constructionSet.Source);
            }

            Assert.Equal(7 * 4, count);

            double Value(DateTimeOffset dateTimeOffset, BuildingType buildingType, ConstructionRole constructionRole)
            {
                ConstructionSet? constructionSet = constructionLibrary.GetConstructionSet(dateTimeOffset, buildingType, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
                IConstruction? construction = constructionLibrary.GetConstruction(constructionSet, constructionRole);
                return construction is not null && construction.TryGetValue(new EnumParameterDefinition(PhysicalComponentConstructionParameter.ThermalTransmittance), out double value) ? value : double.NaN;
            }

            // PL.Floor.ReEx.05.01 has U = 1.0 on soil (single-family) and over a cellar (terraced); TABULA publishes 0.769 for the terraced building.
            Assert.Equal(1.0, Value(Date(1995, 1, 1), BuildingType.SingleFamily, ConstructionRole.GroundFloor));
            Assert.Equal(1.0, Value(Date(1995, 1, 1), BuildingType.Terraced, ConstructionRole.GroundFloor));

            (DateTimeOffset DateTimeOffset, BuildingType BuildingType, double Value)[] publisheds =
            [
                (Date(1930, 1, 1), BuildingType.MultiFamily, 1.16883116883117),
                (Date(1975, 1, 1), BuildingType.MultiFamily, 0.935251798561151),
                (Date(1975, 1, 1), BuildingType.ApartmentBlock, 0.935251798561151),
                (Date(1995, 1, 1), BuildingType.Terraced, 0.769230769230769),
                (Date(2005, 1, 1), BuildingType.MultiFamily, 0.508474576271186),
                (Date(2015, 1, 1), BuildingType.ApartmentBlock, 0.434782608695652),
            ];

            foreach ((DateTimeOffset dateTimeOffset, BuildingType buildingType, double published) in publisheds)
            {
                ConstructionSet? constructionSet = constructionLibrary.GetConstructionSet(dateTimeOffset, buildingType, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
                double additionalFloorResistance = constructionSet?.TypologyData?.AdditionalFloorResistance ?? double.NaN;
                double floor = Value(dateTimeOffset, buildingType, ConstructionRole.FloorOverUnheated);
                Assert.True(System.Math.Abs((1.0 / ((1.0 / floor) + additionalFloorResistance)) - published) < 1e-9, $"{dateTimeOffset:yyyy} {buildingType}: U = {floor}, R = {additionalFloorResistance}");
            }

            // The apartment block of 1967-1985 keeps the German roof the Polish record refers to, and says so.
            ConstructionSet? constructionSet_ApartmentBlock = constructionLibrary.GetConstructionSet(Date(1975, 1, 1), BuildingType.ApartmentBlock, ClimateZone.III, ConstructionFlavour.TypicalAsBuilt);
            Assert.Contains("DE.Roof.SyAv.09.01", constructionSet_ApartmentBlock?.TypologyData?.Source);
            Assert.Equal(0.33, Value(Date(1975, 1, 1), BuildingType.ApartmentBlock, ConstructionRole.FlatRoof));

            // One construction per TABULA construction code: the name ends with the code in parentheses.
            List<string> codes = [];
            foreach (IConstruction construction in constructionLibrary.ConstructionRelationCluster.GetValues<IConstruction>())
            {
                string? name = (construction as Core.Interfaces.INamedObject)?.Name;
                if (construction.Description is null || !construction.Description.StartsWith("TABULA construction ", StringComparison.Ordinal) || name is null)
                {
                    continue;
                }

                codes.Add(name.Substring(name.LastIndexOf('(') + 1).TrimEnd(')'));
            }

            Assert.NotEmpty(codes);
            Assert.Equal(codes.Count, codes.Distinct().Count());
        }
    }
}
