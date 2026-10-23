using DiGi.PostgreSQL.Classes;
using System.Collections.Generic;

namespace DiGi.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Verifies that <see cref="Query.TableNames(ConnectionData)"/> opens the connection it creates, answers the table names, and gives the connection back (DiGi-Organization/DiGi.PostgreSQL#18).
        /// <para>Before the fix the overload created a connection without opening it, so the reader failed on the closed connection at the first call; it also never disposed it. The calls run through a pool of one connection with a two-second acquisition timeout, so a connection kept open would fail the second call.</para>
        /// <para>Read-only. Figures describe the development database resolved from <c>user files/PostgreSQL_PartitionReference.conf</c> (localhost), never production.</para>
        /// </summary>
        [SkippableFact]
        public void TableNames_ConnectionData_ReleasesConnection()
        {
            if (!Create.IsAvailable(Enums.StorageMethod.PartitionReference, out ConnectionData? connectionData) || connectionData is null)
            {
                return;
            }

            ConnectionData connectionData_SinglePool = new(connectionData.Host, connectionData.Username, connectionData.Password, connectionData.Database, connectionData.Port)
            {
                MaximumPoolSize = 1,
                PoolTimeout = 2,
            };

            for (int i = 0; i < 3; i++)
            {
                List<string>? tableNames = Query.TableNames(connectionData_SinglePool);
                Assert.NotNull(tableNames);
                Assert.Contains("partitions", tableNames);
            }
        }
    }
}
