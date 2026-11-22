namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies <see cref="Create.PopulationDensityClass(int?)"/>: every band boundary just inside and just outside, a hidden population read as masked rather than zero, a negative population as undefined, and an empty cell as a real zero.
        /// <para>The bands are the upper bounds of <c>Constants.PopulationDensity</c>: a class holds every value up to and including its maximum, and the next class starts one person above it.</para>
        /// </summary>
        [Fact]
        public void Create_PopulationDensityClass()
        {
            Assert.Equal(Enums.PopulationDensityClass.Masked, Create.PopulationDensityClass(null));
            Assert.Equal(Enums.PopulationDensityClass.Undefined, Create.PopulationDensityClass(-1));
            Assert.Equal(Enums.PopulationDensityClass.Zero, Create.PopulationDensityClass(0));

            Assert.Equal(Enums.PopulationDensityClass.VeryLow, Create.PopulationDensityClass(1));
            Assert.Equal(Enums.PopulationDensityClass.VeryLow, Create.PopulationDensityClass(50));
            Assert.Equal(Enums.PopulationDensityClass.Low, Create.PopulationDensityClass(51));
            Assert.Equal(Enums.PopulationDensityClass.Low, Create.PopulationDensityClass(200));
            Assert.Equal(Enums.PopulationDensityClass.Medium, Create.PopulationDensityClass(201));
            Assert.Equal(Enums.PopulationDensityClass.Medium, Create.PopulationDensityClass(1000));
            Assert.Equal(Enums.PopulationDensityClass.High, Create.PopulationDensityClass(1001));
            Assert.Equal(Enums.PopulationDensityClass.High, Create.PopulationDensityClass(5000));
            Assert.Equal(Enums.PopulationDensityClass.VeryHigh, Create.PopulationDensityClass(5001));
        }
    }
}
