using DiGi.PostgreSQL.Classes;
using System;
using System.IO;
using System.Reflection;

namespace DiGi.GIS.PostgreSQL.UI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The configuration name of the throwaway main test database, kept in the git-ignored <c>DiGi.Test/user files/</c>.
        /// </summary>
        private const string fileName_PostgreSQL_Main_Test = "GIS_PostgreSQL_Main_Test.conf";

        /// <summary>
        /// Reads the throwaway main test database configuration from <c>DiGi.Test/user files/</c>, skipping the calling fact when it is missing, names the host's database or does not answer.
        /// <para>This is the only way a writing fact in this project reaches a database. The same guards the other test projects carry apply: a test configuration naming the host's database is refused (the writing facts write what they test), because a fact running there would leave what it tested behind in the database the application itself reads (DiGi-Organization/DiGi.GIS.PostgreSQL#131, <c>Coding - PostgreSQL.md</c> §6).</para>
        /// </summary>
        /// <returns>The test connection data, its parsed configuration file, and the path it was read from.</returns>
        private static (ConnectionData ConnectionData, PostgreSQLConfigurationFile PostgreSQLConfigurationFile, string Path) TestConnectionData_Main_Test()
        {
            string? directory = Core.xUnit.Query.UserFilesDirectory(Assembly.GetExecutingAssembly());
            Skip.If(directory is null, "The DiGi.Test/user files directory could not be resolved.");

            string path = Path.Combine(directory!, fileName_PostgreSQL_Main_Test);
            Skip.If(!File.Exists(path), $"{fileName_PostgreSQL_Main_Test} is needed in DiGi.Test/user files. Point it at a throwaway database - never a host's.");

            PostgreSQLConfigurationFile? postgreSQLConfigurationFile = DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(path);
            ConnectionData? connectionData = DiGi.PostgreSQL.Create.ConnectionData(postgreSQLConfigurationFile);
            Skip.If(connectionData is null, $"{fileName_PostgreSQL_Main_Test} is incomplete - it names no database.");

            ConnectionData? connectionData_Host = TestConnectionData(PostgreSQL.Constants.FileName.PostgreSQL_Main);
            Skip.If(IsSameDatabase(connectionData, connectionData_Host), $"{fileName_PostgreSQL_Main_Test} names the database of {PostgreSQL.Constants.FileName.PostgreSQL_Main}; the facts write what they test and refuse to run there.");

            Skip.IfNot(DiGi.PostgreSQL.Query.IsAvailable(connectionData!), "The PostgreSQL test database is not reachable.");

            return (connectionData!, postgreSQLConfigurationFile!, path);
        }

        /// <summary>
        /// Reads a configuration file from <c>DiGi.Test/user files/</c>.
        /// </summary>
        /// <param name="fileName">The name of the configuration file.</param>
        /// <returns>The connection data, or null when the file is missing or incomplete.</returns>
        private static ConnectionData? TestConnectionData(string fileName)
        {
            string? directory = Core.xUnit.Query.UserFilesDirectory(Assembly.GetExecutingAssembly());
            if (directory is null)
            {
                return null;
            }

            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            return DiGi.PostgreSQL.Create.ConnectionData(DiGi.PostgreSQL.Create.PostgreSQLConfigurationFile(path));
        }

        /// <summary>
        /// Answers whether two connection configurations name the same database on the same server: host and database compared case-insensitively, the port exactly with a missing one read as 5432. Credentials are not compared.
        /// </summary>
        /// <param name="connectionData_1">The first connection configuration.</param>
        /// <param name="connectionData_2">The second connection configuration.</param>
        /// <returns>True when both are present and name the same host, port and database; otherwise, false.</returns>
        private static bool IsSameDatabase(ConnectionData? connectionData_1, ConnectionData? connectionData_2)
        {
            if (connectionData_1 is null || connectionData_2 is null)
            {
                return false;
            }

            return string.Equals(connectionData_1.Host?.Trim(), connectionData_2.Host?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(connectionData_1.Database?.Trim(), connectionData_2.Database?.Trim(), StringComparison.OrdinalIgnoreCase)
                && (connectionData_1.Port ?? 5432) == (connectionData_2.Port ?? 5432);
        }
    }
}
