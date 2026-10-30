using DiGi.GIS.PostgreSQL.Classes;
using System;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="PostgreSQLBuildingDataExternalComponentsUpdateTask"/> rejects a null converter manager and initializes its defaults.
        /// <para>The options are the contract when nothing is set: a 600 second timeout well above the 30 second Npgsql default, a null county set scoping the run to every county, and every counter at zero before a run has happened.</para>
        /// </summary>
        [Fact]
        public void PostgreSQLBuildingDataExternalComponentsUpdateTask_Constructor()
        {
            Assert.Throws<ArgumentNullException>(() => new PostgreSQLBuildingDataExternalComponentsUpdateTask(null!));

            GISPostgreSQLConverterManager gISPostgreSQLConverterManager = new();
            PostgreSQLBuildingDataExternalComponentsUpdateTask postgreSQLBuildingDataExternalComponentsUpdateTask = new(gISPostgreSQLConverterManager);

            Assert.NotNull(postgreSQLBuildingDataExternalComponentsUpdateTask.PostgreSQLBuildingDataExternalComponentsUpdateOptions);
            Assert.Equal(600, postgreSQLBuildingDataExternalComponentsUpdateTask.PostgreSQLBuildingDataExternalComponentsUpdateOptions.CommandTimeout);
            Assert.Null(postgreSQLBuildingDataExternalComponentsUpdateTask.PostgreSQLBuildingDataExternalComponentsUpdateOptions.CountyIds);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.FailedCountyCount);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.OpenEnvelopeCount);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.ProcessedCountyCount);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.ProcessedModelCount);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.SkippedComponentCount);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.UpdatedRowCount);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.PartyWallBuildingCount);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.PartyWallFailedBatchCount);
            Assert.Equal(0, postgreSQLBuildingDataExternalComponentsUpdateTask.MissingNeighbourModelCount);
        }
    }
}
