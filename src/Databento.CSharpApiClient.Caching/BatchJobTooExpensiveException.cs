using System;
using System.Globalization;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// Thrown by <see cref="CachedDatabentoClient.RunBatchJobAsync"/> when Databento prices the jobs it would submit above the caller's
    /// ceiling. Nothing was submitted, so nothing was billed.
    /// </summary>
    public sealed class BatchJobTooExpensiveException : Exception
    {
        /// <summary>
        /// Initializes the exception
        /// </summary>
        /// <param name="estimatedCostUsd">Databento's price for the jobs, USD</param>
        /// <param name="maxCostUsd">The caller's ceiling, USD</param>
        public BatchJobTooExpensiveException(decimal estimatedCostUsd, decimal maxCostUsd)
            : base(string.Format(CultureInfo.InvariantCulture, "The batch jobs would cost {0} USD, above the ceiling of {1} USD; nothing was submitted.", estimatedCostUsd, maxCostUsd))
        {
            this.EstimatedCostUsd = estimatedCostUsd;
            this.MaxCostUsd = maxCostUsd;
        }

        /// <summary>
        /// Gets Databento's price for the jobs, USD.
        /// </summary>
        public decimal EstimatedCostUsd { get; }

        /// <summary>
        /// Gets the caller's ceiling, USD.
        /// </summary>
        public decimal MaxCostUsd { get; }
    }
}
