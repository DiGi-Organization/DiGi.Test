using DiGi.GIS.WebAPI.Classes;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.GIS.WebAPI.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Exposes the protected run of <see cref="OrtoDatasFromDatabasePostTask"/> so a fact can await one pass over the queue directly, without the background task's start and event machinery.
        /// </summary>
        private sealed class OrtoDatasFromDatabasePostTaskProbe : OrtoDatasFromDatabasePostTask
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="OrtoDatasFromDatabasePostTaskProbe"/> class.
            /// </summary>
            /// <param name="GISWebAPIManager">The manager that creates the HTTP clients the task uses.</param>
            public OrtoDatasFromDatabasePostTaskProbe(GISWebAPIManager GISWebAPIManager)
                : base(GISWebAPIManager)
            {
            }

            /// <summary>
            /// Runs the task until the queue returns no references.
            /// </summary>
            /// <param name="cancellationToken">A token to cancel the run.</param>
            /// <returns>The value the task's run returns.</returns>
            public Task<bool> RunAsync(CancellationToken cancellationToken = default)
            {
                return ExecuteAsync(new Progress<long>(), cancellationToken);
            }
        }
    }
}
