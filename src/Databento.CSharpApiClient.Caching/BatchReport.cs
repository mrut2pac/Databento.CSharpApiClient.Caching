using System;
using System.Collections.Generic;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// What <see cref="CachedDatabentoClient.RunBatchJobAsync"/> or <see cref="CachedDatabentoClient.IngestBatchFolderAsync"/> saved. The records
    /// themselves are served by the time-series calls.
    /// </summary>
    public sealed class BatchReport
    {
        /// <summary>
        /// Initializes the report
        /// </summary>
        /// <param name="jobIds">Jobs submitted</param>
        /// <param name="reusedJobIds">Jobs submitted before and downloaded again</param>
        /// <param name="costUsd">What the jobs submitted cost</param>
        /// <param name="symbolDaysSaved">Symbol-days saved with records</param>
        /// <param name="symbolDaysSavedEmpty">Symbol-days saved as empty</param>
        /// <param name="symbolDaysAlreadyCached">Symbol-days the cache already held</param>
        /// <param name="daysLeftOut">UTC days left out</param>
        public BatchReport(IReadOnlyList<string> jobIds, IReadOnlyList<string> reusedJobIds, decimal costUsd, int symbolDaysSaved, int symbolDaysSavedEmpty, int symbolDaysAlreadyCached, IReadOnlyList<DateTime> daysLeftOut)
        {
            this.JobIds = jobIds;
            this.ReusedJobIds = reusedJobIds;
            this.CostUsd = costUsd;
            this.SymbolDaysSaved = symbolDaysSaved;
            this.SymbolDaysSavedEmpty = symbolDaysSavedEmpty;
            this.SymbolDaysAlreadyCached = symbolDaysAlreadyCached;
            this.DaysLeftOut = daysLeftOut;
        }

        /// <summary>
        /// Gets the ids of the jobs submitted, in submission order; empty when nothing was missing, or for a folder.
        /// </summary>
        public IReadOnlyList<string> JobIds { get; }

        /// <summary>
        /// Gets the ids of jobs submitted before, for the same query and not expired, that were downloaded again at no cost.
        /// </summary>
        public IReadOnlyList<string> ReusedJobIds { get; }

        /// <summary>
        /// Gets what the jobs submitted cost, USD, as Databento billed them; 0 when none was submitted.
        /// </summary>
        public decimal CostUsd { get; }

        /// <summary>
        /// Gets the symbol-days saved as day files with records.
        /// </summary>
        public int SymbolDaysSaved { get; }

        /// <summary>
        /// Gets the symbol-days saved as empty, as <see cref="CachedDatabentoClientOptions.EmptyDays"/> allows.
        /// </summary>
        public int SymbolDaysSavedEmpty { get; }

        /// <summary>
        /// Gets the symbol-days the cache already held, which were left as they were.
        /// </summary>
        public int SymbolDaysAlreadyCached { get; }

        /// <summary>
        /// Gets the UTC days left out, in order: not final yet, or for a folder, not whole in its range or not <c>available</c> in its
        /// <c>condition.json</c>. The time-series calls download them when asked.
        /// </summary>
        public IReadOnlyList<DateTime> DaysLeftOut { get; }
    }
}
