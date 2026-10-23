using DiGi.PostgreSQL.Classes;
using System.Threading.Tasks;

namespace DiGi.PostgreSQL.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Reproduces DiGi-Organization/DiGi.PostgreSQL#18: <see cref="Query.TableExistsAsync(ConnectionData, string, int, System.Threading.CancellationToken)"/> must give its connection back after every call.
        /// <para>The calls run through a pool of exactly one connection with a two-second acquisition timeout. A call that keeps its connection open leaves nothing for the next one, which then waits out the timeout and throws - so before the fix the second call fails, after it all five pass. On the deployed API each leaked call kept a server backend open, and about ninety terrain requests exhausted <c>max_connections</c>.</para>
        /// <para>Read-only: the statement is <c>to_regclass</c>. Figures describe the development database resolved from <c>user files/PostgreSQL_PartitionReference.conf</c> (localhost), never production.</para>
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [SkippableFact]
        public async Task TableExistsAsync_ConnectionData_ReleasesConnection()
        {
            if (!Create.IsAvailable(Enums.StorageMethod.PartitionReference, out ConnectionData? connectionData) || connectionData is null)
            {
                return;
            }

            // A separate pool (the connection string differs) of one connection, so a leak shows on the second call instead of the hundredth.
            ConnectionData connectionData_SinglePool = new(connectionData.Host, connectionData.Username, connectionData.Password, connectionData.Database, connectionData.Port)
            {
                MaximumPoolSize = 1,
                PoolTimeout = 2,
            };

            for (int i = 0; i < 5; i++)
            {
                Assert.True(await Query.TableExistsAsync(connectionData_SinglePool, "partitions"));
                Assert.False(await Query.TableExistsAsync(connectionData_SinglePool, "xunit_missing_table_tableexistsasync"));
            }
        }
    }
}
