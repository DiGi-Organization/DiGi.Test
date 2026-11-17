using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DiGi.WebAPI.WindowsService.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// Records what one request to <see cref="RequestAbortedControllerFixture"/> observed: that it started, and whether its token was cancelled and when.
        /// <para>Registered as a singleton, so the fact reads what the controller, activated per request, wrote.</para>
        /// </summary>
        public sealed class RequestAbortedStateFixture
        {
            /// <summary>
            /// Gets the task completed when the action has started waiting.
            /// </summary>
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>
            /// Gets the task completed when the action stops waiting: with the moment its token was cancelled, or <c>null</c> when the wait ran to the end.
            /// </summary>
            public TaskCompletionSource<long?> Ended { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// A controller whose only action waits on its request's <see cref="CancellationToken"/> and records whether the token was cancelled (DiGi-Organization/DiGi.GIS.WebAPI#77).
        /// <para>Nested, so MVC does not discover it on its own; <see cref="ControllerFeatureProviderFixture"/> adds it.</para>
        /// </summary>
        [Route("requestaborted")]
        public sealed class RequestAbortedControllerFixture : ControllerBase
        {
            private readonly RequestAbortedStateFixture requestAbortedStateFixture;

            /// <summary>
            /// Initializes a new instance of the <see cref="RequestAbortedControllerFixture"/> class.
            /// </summary>
            /// <param name="requestAbortedStateFixture">The state the action records into.</param>
            public RequestAbortedControllerFixture(RequestAbortedStateFixture requestAbortedStateFixture)
            {
                this.requestAbortedStateFixture = requestAbortedStateFixture;
            }

            /// <summary>
            /// Waits up to 30 s on the request's token.
            /// </summary>
            /// <param name="cancellationToken">The token MVC binds to <c>HttpContext.RequestAborted</c>.</param>
            /// <returns>An empty 200 when the wait ran to the end; nothing a client could read otherwise.</returns>
            [HttpGet("wait")]
            public async Task<IActionResult> WaitAsync(CancellationToken cancellationToken = default)
            {
                requestAbortedStateFixture.Started.TrySetResult();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    requestAbortedStateFixture.Ended.TrySetResult(Stopwatch.GetTimestamp());
                    throw;
                }

                requestAbortedStateFixture.Ended.TrySetResult(null);
                return Ok();
            }
        }
    }
}
