using DiGi.GIS.PostgreSQL.Classes;
using DiGi.PostgreSQL.Classes;
using System.IO;
using System.Reflection;

namespace DiGi.GIS.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that the converter manager registers the open building converter on the Storage database and the county parts it files against on the Main database (DiGi.GIS.PostgreSQL#168).
        /// <para>The manager is built from the two test configuration files through the overload that takes the file names, so no host configuration is read. The open building converter must answer the connection of the storage test database and not that of the main one, while the administrative area converter answers the main one - the registration that puts the largest data of the repository on the volume built for it. A registration moved back to Main fails the first assertion. Medium test (a live database server is probed before the fact runs).</para>
        /// </summary>
        [MediumSkippableFact]
        public void GISPostgreSQLConverterManager_OpenBuildingIsOnStorage()
        {
            (ConnectionData connectionData_Main, ConnectionData connectionData_Storage) = TestConnectionDatas();

            string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Assert.NotNull(directory);

            GISPostgreSQLConverterManager? gISPostgreSQLConverterManager = Create.GISPostgreSQLConverterManager(directory, fileName_PostgreSQL_Main_Test, fileName_PostgreSQL_Storage_Test);
            Assert.NotNull(gISPostgreSQLConverterManager);

            OpenBuildingPostgreSQLConverter? openBuildingPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<OpenBuildingPostgreSQLConverter>();
            Assert.NotNull(openBuildingPostgreSQLConverter);
            Assert.True(IsSameDatabase(connectionData_Storage, openBuildingPostgreSQLConverter.ConnectionData), "the open building converter is not on the storage database.");
            Assert.False(IsSameDatabase(connectionData_Main, openBuildingPostgreSQLConverter.ConnectionData), "the open building converter is on the main database.");

            AdministrativeAreal2DPostgreSQLConverter? administrativeAreal2DPostgreSQLConverter = gISPostgreSQLConverterManager.GetPostgreSQLConverter<AdministrativeAreal2DPostgreSQLConverter>();
            Assert.NotNull(administrativeAreal2DPostgreSQLConverter);
            Assert.True(IsSameDatabase(connectionData_Main, administrativeAreal2DPostgreSQLConverter.ConnectionData), "the county parts are not on the main database.");

            // A missing name or directory builds nothing.
            Assert.Null(Create.GISPostgreSQLConverterManager(directory, null, fileName_PostgreSQL_Storage_Test));
            Assert.Null(Create.GISPostgreSQLConverterManager(Path.Combine(directory, "no-such-directory"), fileName_PostgreSQL_Main_Test, fileName_PostgreSQL_Storage_Test));
        }
    }
}
