using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Enums;
using DiGi.Analytical.Building.HVAC.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Pins the norm stages of the thermal limits in force before 29 April 1998, PN-53/57 to PN-91/B-02020 (DiGi-Organization/DiGi.Analytical#20).
        /// <para>Every stage of the manager has a regulation act and the stages are ordered by effective date. On both sides of every boundary - 1955-01-01, 1966-01-01, 1976-01-01, 1983-01-01, 1992-01-01 and the 1997 annex on 1998-04-29 - the manager returns the expected stage, and before the first one none. Each norm stage holds exactly the k(max) of the reproduced tables; PN-53/57 and PN-64 limit the wall by climatic zone, 1.25 and 1.0 kcal/(m2·h·°C) converted with 1.163 W/(m2K) per kcal/(m2·h·°C); windows and doors are limited only from PN-82.</para>
        /// <para>Each description cites the norm as reproduced in a named source and records the candidate in-force dates, and every stage survives a copy and a JSON round trip.</para>
        /// </summary>
        [Fact]
        public void RegulatedHeatTransferCoefficientsManager_Pre1998()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();

            // Every stage has a regulation act; the stages come in strictly increasing effective date.
            List<IRegulatedHeatTransferCoefficients> regulatedHeatTransferCoefficientsList = regulatedHeatTransferCoefficientsManager.RegulatedHeatTransferCoefficients<IRegulatedHeatTransferCoefficients>();
            Assert.Equal(11, regulatedHeatTransferCoefficientsList.Count);
            for (int i = 0; i < regulatedHeatTransferCoefficientsList.Count; i++)
            {
                RegulationAct? regulationAct = regulatedHeatTransferCoefficientsList[i].RegulationAct;
                Assert.NotNull(regulationAct);
                Assert.False(string.IsNullOrWhiteSpace(regulationAct.Reference));
                Assert.True(regulationAct.PublicationDate <= regulationAct.EffectiveDate, regulationAct.Reference);
                if (i > 0)
                {
                    Assert.True(regulatedHeatTransferCoefficientsList[i - 1].RegulationAct!.EffectiveDate < regulationAct.EffectiveDate, regulationAct.Reference);
                }
            }

            // Both sides of every boundary.
            (DateTime DateTime, Type? Type)[] stages =
            [
                (new DateTime(1900, 1, 1), null),
                (new DateTime(1954, 12, 31), null),
                (new DateTime(1955, 1, 1), typeof(RegulatedHeatTransferCoefficients_1957)),
                (new DateTime(1965, 12, 31), typeof(RegulatedHeatTransferCoefficients_1957)),
                (new DateTime(1966, 1, 1), typeof(RegulatedHeatTransferCoefficients_1964)),
                (new DateTime(1975, 12, 31), typeof(RegulatedHeatTransferCoefficients_1964)),
                (new DateTime(1976, 1, 1), typeof(RegulatedHeatTransferCoefficients_1974)),
                (new DateTime(1982, 12, 31), typeof(RegulatedHeatTransferCoefficients_1974)),
                (new DateTime(1983, 1, 1), typeof(RegulatedHeatTransferCoefficients_1982)),
                (new DateTime(1991, 12, 31), typeof(RegulatedHeatTransferCoefficients_1982)),
                (new DateTime(1992, 1, 1), typeof(RegulatedHeatTransferCoefficients_1991)),
                (new DateTime(1998, 4, 28), typeof(RegulatedHeatTransferCoefficients_1991)),
                (new DateTime(1998, 4, 29), typeof(RegulatedHeatTransferCoefficients_1997)),
            ];

            foreach ((DateTime dateTime, Type? type) in stages)
            {
                IRegulatedHeatTransferCoefficients? regulatedHeatTransferCoefficients = regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime);
                Assert.True(regulatedHeatTransferCoefficients?.GetType() == type, $"{dateTime:yyyy-MM-dd}: {regulatedHeatTransferCoefficients?.GetType().Name ?? "null"} instead of {type?.Name ?? "null"}");
                Assert.Same(regulatedHeatTransferCoefficients, regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime, true));
            }

            // The norm, its chosen in-force date and the sources it is reproduced in.
            (DateTime DateTime, string Reference, DateTime EffectiveDate, string[] Candidates)[] regulationActs =
            [
                (new DateTime(1960, 1, 1), "PN-53/B-02405; PN-57/B-02405", new DateTime(1955, 1, 1), ["1955-01-01", "1958-07-01", "1957", "1959"]),
                (new DateTime(1970, 1, 1), "PN-64/B-03404", new DateTime(1966, 1, 1), ["1966-01-01", "1968-01-01", "1964"]),
                (new DateTime(1980, 1, 1), "PN-74/B-03404", new DateTime(1976, 1, 1), ["1976-01-01", "1976-07-01", "1974"]),
                (new DateTime(1985, 1, 1), "PN-82/B-02020", new DateTime(1983, 1, 1), ["1983-01-01", "1982", "1991-12-31"]),
                (new DateTime(1995, 1, 1), "PN-91/B-02020", new DateTime(1992, 1, 1), ["1992-01-01", "1991", "1998-04-29", "2002"]),
            ];

            foreach ((DateTime dateTime, string reference, DateTime effectiveDate, string[] candidates) in regulationActs)
            {
                RegulationAct? regulationAct = regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime)?.RegulationAct;
                Assert.NotNull(regulationAct);
                Assert.Equal(reference, regulationAct.Reference);
                Assert.Equal(effectiveDate, regulationAct.EffectiveDate);
                Assert.Null(regulationAct.PublicAuthorityEffectiveDate);
                Assert.False(string.IsNullOrWhiteSpace(regulationAct.Name));
                Assert.NotNull(regulationAct.Description);
                Assert.Contains("as reproduced in A. Lis", regulationAct.Description);
                Assert.Contains("Chosen: ", regulationAct.Description);
                foreach (string candidate in candidates)
                {
                    Assert.True(regulationAct.Description.Contains(candidate), $"{reference}: candidate {candidate} not recorded");
                }
            }

            // The unit conversion: 1.0 and 1.25 kcal/(m2·h·°C) at 1.163 W/(m2K) per kcal/(m2·h·°C).
            const double kcal = 1.163;
            Assert.Equal(1.163, 1.0 * kcal);
            Assert.Equal(1.45375, 1.25 * kcal);

            // Every k(max) of each stage, and no other.
            void Values<TEnum>(DateTime dateTime, (TEnum ExternalPartitionType, double Value)[] values) where TEnum : struct, Enum
            {
                RegulatedHeatTransferCoefficients<TEnum> regulatedHeatTransferCoefficients = Assert.IsAssignableFrom<RegulatedHeatTransferCoefficients<TEnum>>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime));
                foreach ((TEnum externalPartitionType, double value) in values)
                {
                    Assert.True(regulatedHeatTransferCoefficients.TryGetValue(externalPartitionType, out double value_Stored), $"{dateTime:yyyy}, {externalPartitionType}: no k(max)");
                    Assert.True(value_Stored == value, $"{dateTime:yyyy}, {externalPartitionType}: k(max) {value_Stored} instead of {value}");
                }

                Assert.Equal(values.Length, regulatedHeatTransferCoefficients.GetEnums().Count);
                Assert.Equal(Enum.GetValues<TEnum>().Length, values.Length);
                Assert.Empty(((IRegulatedHeatTransferCoefficients)regulatedHeatTransferCoefficients).GetMinimumThermalResistanceNames());
            }

            Values(new DateTime(1960, 1, 1),
            [
                (ExternalPartitionType_1957.Wall_ZoneA, 1.25 * kcal),
                (ExternalPartitionType_1957.Wall_ZoneB, 1.0 * kcal),
                (ExternalPartitionType_1957.Roof_TopCeiling, 1.04),
                (ExternalPartitionType_1957.Roof_Flat, 0.87),
                (ExternalPartitionType_1957.Floor_OverUnheatedSpaces, 1.163),
            ]);

            Values(new DateTime(1970, 1, 1),
            [
                (ExternalPartitionType_1964.Wall_ZoneA, 1.45375),
                (ExternalPartitionType_1964.Wall_ZoneB, 1.163),
                (ExternalPartitionType_1964.Roof_TopCeiling, 1.04),
                (ExternalPartitionType_1964.Roof_Flat, 0.87),
                (ExternalPartitionType_1964.Floor_OverUnheatedSpaces, 1.163),
            ]);

            Values(new DateTime(1980, 1, 1),
            [
                (ExternalPartitionType_1974.Wall, 1.163),
                (ExternalPartitionType_1974.Roof_TopCeiling, 0.93),
                (ExternalPartitionType_1974.Roof_Flat, 0.70),
                (ExternalPartitionType_1974.Floor_OverUnheatedSpaces, 1.163),
            ]);

            Values(new DateTime(1985, 1, 1),
            [
                (ExternalPartitionType_1982.Wall, 0.75),
                (ExternalPartitionType_1982.Roof_TopCeiling, 0.40),
                (ExternalPartitionType_1982.Roof_Flat, 0.45),
                (ExternalPartitionType_1982.Floor_OverUnheatedSpaces, 1.16),
                (ExternalPartitionType_1982.Window_ZoneA, 2.6),
                (ExternalPartitionType_1982.Window_ZoneB, 2.0),
                (ExternalPartitionType_1982.Door, 2.6),
            ]);

            Values(new DateTime(1995, 1, 1),
            [
                (ExternalPartitionType_1991.Wall, 0.55),
                (ExternalPartitionType_1991.Roof_TopCeiling, 0.30),
                (ExternalPartitionType_1991.Roof_Flat, 0.30),
                (ExternalPartitionType_1991.Floor_OverUnheatedSpaces, 0.60),
                (ExternalPartitionType_1991.Window_ZoneA, 2.6),
                (ExternalPartitionType_1991.Window_ZoneB, 2.0),
                (ExternalPartitionType_1991.Door, 2.6),
            ]);

            // PN-64: the zone-dependent wall, looser in zones I-III than in IV-V; PN-74 dropped the zones.
            RegulatedHeatTransferCoefficients_1964 regulatedHeatTransferCoefficients_1964 = Assert.IsType<RegulatedHeatTransferCoefficients_1964>(regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(new DateTime(1970, 1, 1)));
            Assert.True(regulatedHeatTransferCoefficients_1964[ExternalPartitionType_1964.Wall_ZoneA] > regulatedHeatTransferCoefficients_1964[ExternalPartitionType_1964.Wall_ZoneB]);
            Assert.Contains("1.0-1.25 kcal/(m2·h·°C)", regulatedHeatTransferCoefficients_1964.RegulationAct?.Description);

            // The stage descriptions carry no building-type prefix: the norms apply to every building alike.
            foreach (DateTime dateTime in new DateTime[] { new(1960, 1, 1), new(1970, 1, 1), new(1980, 1, 1), new(1985, 1, 1), new(1995, 1, 1) })
            {
                IRegulatedHeatTransferCoefficients regulatedHeatTransferCoefficients = regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime)!;
                foreach (string name in regulatedHeatTransferCoefficients.GetNames())
                {
                    Assert.False(name.StartsWith("Residential building") || name.StartsWith("Public building"), name);
                }

                // A copy and a JSON round trip keep the type, the act and every value.
                IRegulatedHeatTransferCoefficients? regulatedHeatTransferCoefficients_Json = Core.Convert.ToDiGi<IRegulatedHeatTransferCoefficients>(Core.Convert.ToSystem_String(regulatedHeatTransferCoefficients) ?? string.Empty)?.FirstOrDefault();
                Assert.NotNull(regulatedHeatTransferCoefficients_Json);
                Assert.Equal(regulatedHeatTransferCoefficients.GetType(), regulatedHeatTransferCoefficients_Json.GetType());
                Assert.Equal(regulatedHeatTransferCoefficients.RegulationAct?.EffectiveDate, regulatedHeatTransferCoefficients_Json.RegulationAct?.EffectiveDate);
                Assert.Equal(regulatedHeatTransferCoefficients.RegulationAct?.Description, regulatedHeatTransferCoefficients_Json.RegulationAct?.Description);
                foreach (string name in regulatedHeatTransferCoefficients.GetNames())
                {
                    Assert.True(regulatedHeatTransferCoefficients.TryGetValue(name, out double value));
                    Assert.True(regulatedHeatTransferCoefficients_Json.TryGetValue(name, out double value_Json), $"{dateTime:yyyy}, {name}: missing after the round trip");
                    Assert.Equal(value, value_Json);
                }

                Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficients);
            }

            RegulatedHeatTransferCoefficients_1964 regulatedHeatTransferCoefficients_1964_Copy = new(regulatedHeatTransferCoefficients_1964);
            Assert.Equal(1.45375, regulatedHeatTransferCoefficients_1964_Copy[ExternalPartitionType_1964.Wall_ZoneA]);
            Assert.Equal("PN-64/B-03404", regulatedHeatTransferCoefficients_1964_Copy.RegulationAct?.Reference);
            Assert.NotSame(regulatedHeatTransferCoefficients_1964.RegulationAct, regulatedHeatTransferCoefficients_1964_Copy.RegulationAct);

            Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficientsManager);
        }
    }
}
