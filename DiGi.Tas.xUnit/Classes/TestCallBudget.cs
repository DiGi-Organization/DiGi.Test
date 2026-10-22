using System;

namespace DiGi.Tas.xUnit
{
    /// <summary>
    /// Counts calls made into the test doubles of the TIC COM interfaces and throws once a limit is exceeded, so a traversal that never terminates fails cleanly instead of overflowing the stack.
    /// </summary>
    public class TestCallBudget
    {
        private int count;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestCallBudget"/> class with the specified maximum number of calls.
        /// </summary>
        /// <param name="maxCount">The maximum number of calls allowed before an exception is thrown.</param>
        public TestCallBudget(int maxCount)
        {
            MaxCount = maxCount;
        }

        /// <summary>
        /// Gets the number of calls spent so far.
        /// </summary>
        public int Count => count;

        /// <summary>
        /// Gets the maximum number of calls allowed.
        /// </summary>
        public int MaxCount { get; }

        /// <summary>
        /// Records one call and throws when the budget is exhausted.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the number of calls exceeds <see cref="MaxCount"/>.</exception>
        public void Spend()
        {
            count++;
            if (count > MaxCount)
            {
                throw new InvalidOperationException($"Call budget of {MaxCount} exceeded: the traversal does not terminate.");
            }
        }
    }
}
