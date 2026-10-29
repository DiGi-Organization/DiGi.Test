using DiGi.Analytical.Building.HVAC.Classes;
using DiGi.Analytical.Building.HVAC.Interfaces;

namespace DiGi.Analytical.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Tests the copy constructor of <see cref="RegulatedHeatTransferCoefficientsManager"/> (DiGi-Organization/DiGi.Analytical#13): the copy answers every date with the same stage, values and minimum thermal resistances as the source, through instances of its own.
        /// <para>The constructor used to iterate its own values, still empty while it runs, so every copy was empty and answered null for any date. <see cref="Core.xUnit.Query.SerializationCheck"/> cannot catch this, because <c>Clone()</c> goes through the JSON constructor.</para>
        /// </summary>
        [Fact]
        public void RegulatedHeatTransferCoefficientsManager_Copy()
        {
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager = Building.HVAC.Create.RegulatedHeatTransferCoefficientsManager();
            RegulatedHeatTransferCoefficientsManager regulatedHeatTransferCoefficientsManager_Copy = new(regulatedHeatTransferCoefficientsManager);

            DateTime[] dateTimes = [new DateTime(1998, 4, 28), new DateTime(2000, 6, 1), new DateTime(2005, 6, 1), new DateTime(2010, 6, 1), new DateTime(2015, 6, 1), new DateTime(2017, 6, 1), new DateTime(2021, 6, 1)];
            foreach (DateTime dateTime in dateTimes)
            {
                IRegulatedHeatTransferCoefficients? regulatedHeatTransferCoefficients = regulatedHeatTransferCoefficientsManager.GetRegulatedHeatTransferCoefficients(dateTime);
                IRegulatedHeatTransferCoefficients? regulatedHeatTransferCoefficients_Copy = regulatedHeatTransferCoefficientsManager_Copy.GetRegulatedHeatTransferCoefficients(dateTime);

                if (regulatedHeatTransferCoefficients is null)
                {
                    Assert.Null(regulatedHeatTransferCoefficients_Copy);
                    continue;
                }

                Assert.NotNull(regulatedHeatTransferCoefficients_Copy);
                Assert.NotSame(regulatedHeatTransferCoefficients, regulatedHeatTransferCoefficients_Copy);
                Assert.Equal(regulatedHeatTransferCoefficients.GetType(), regulatedHeatTransferCoefficients_Copy.GetType());
                Assert.Equal(regulatedHeatTransferCoefficients.RegulationAct?.Reference, regulatedHeatTransferCoefficients_Copy.RegulationAct?.Reference);
                Assert.Equal(regulatedHeatTransferCoefficients.RegulationAct?.EffectiveDate, regulatedHeatTransferCoefficients_Copy.RegulationAct?.EffectiveDate);

                Assert.Equal(regulatedHeatTransferCoefficients.GetNames(), regulatedHeatTransferCoefficients_Copy.GetNames());
                foreach (string name in regulatedHeatTransferCoefficients.GetNames())
                {
                    Assert.True(regulatedHeatTransferCoefficients.TryGetValue(name, out double value));
                    Assert.True(regulatedHeatTransferCoefficients_Copy.TryGetValue(name, out double value_Copy), $"{dateTime:yyyy-MM-dd}, {name}: missing in the copy");
                    Assert.Equal(value, value_Copy);
                }

                Assert.Equal(regulatedHeatTransferCoefficients.GetMinimumThermalResistanceNames(), regulatedHeatTransferCoefficients_Copy.GetMinimumThermalResistanceNames());
                foreach (string name in regulatedHeatTransferCoefficients.GetMinimumThermalResistanceNames())
                {
                    Assert.True(regulatedHeatTransferCoefficients.TryGetMinimumThermalResistance(name, out double minimumThermalResistance));
                    Assert.True(regulatedHeatTransferCoefficients_Copy.TryGetMinimumThermalResistance(name, out double minimumThermalResistance_Copy), $"{dateTime:yyyy-MM-dd}, {name}: missing in the copy");
                    Assert.Equal(minimumThermalResistance, minimumThermalResistance_Copy);
                }
            }

            // The public-authority date survives the copy.
            Assert.Equal(new DateTime(2019, 1, 1), regulatedHeatTransferCoefficientsManager_Copy.GetRegulatedHeatTransferCoefficients(new DateTime(2021, 1, 1))?.RegulationAct?.PublicAuthorityEffectiveDate);
            Assert.NotNull(regulatedHeatTransferCoefficientsManager_Copy.GetRegulatedHeatTransferCoefficients(new DateTime(2019, 6, 1), true));

            Core.xUnit.Query.SerializationCheck(regulatedHeatTransferCoefficientsManager_Copy);
        }
    }
}
