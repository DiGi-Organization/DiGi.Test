namespace DiGi.Tas.xUnit
{
    /// <summary>
    /// Managed test double of the TIC <c>InternalCondition</c> COM interface. It carries a name and description only; internal gain and thermostat are absent.
    /// </summary>
    public class TestInternalCondition : global::TIC.InternalCondition
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestInternalCondition"/> class with the specified name.
        /// </summary>
        /// <param name="name">The name of the internal condition.</param>
        public TestInternalCondition(string name)
        {
            this.name = name;
        }

        /// <summary>
        /// Gets or sets the name of the internal condition.
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// Gets or sets the description of the internal condition.
        /// </summary>
        public string description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether solar radiation is included in the mean radiant temperature.
        /// </summary>
        public bool includeSolarInMRT { get; set; }

        /// <summary>
        /// Returns a checksum derived from the name.
        /// </summary>
        /// <returns>The checksum string.</returns>
        public string CalculateDataChecksum()
        {
            return "checksum_" + name;
        }

        /// <summary>
        /// Returns no internal gain.
        /// </summary>
        /// <returns>Always null.</returns>
        public global::TIC.InternalGain? GetInternalGain()
        {
            return null;
        }

        /// <summary>
        /// Returns the lower limit.
        /// </summary>
        /// <returns>Always zero.</returns>
        public float GetLowerLimit()
        {
            return 0;
        }

        /// <summary>
        /// Returns no thermostat.
        /// </summary>
        /// <returns>Always null.</returns>
        public global::TIC.Thermostat? GetThermostat()
        {
            return null;
        }

        /// <summary>
        /// Returns the upper limit.
        /// </summary>
        /// <returns>Always zero.</returns>
        public float GetUpperLimit()
        {
            return 0;
        }
    }
}
