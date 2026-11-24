using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Databento.CSharpApiClient.Caching
{
    public sealed partial class CachedDatabentoClient
    {
        /// <summary>
        /// Saves the symbol-days a range lacks through Databento batch jobs, and returns once they are saved: for ranges too large for the
        /// time-series calls. Only final days are asked for, each run of consecutive missing days apart (DBN, zstd, split by day and symbol);
        /// a job submitted before whose symbols and range cover a query, and not expired, is downloaded again at no cost instead.
        /// </summary>
        /// <remarks>
        /// Runs on one cache root take turns within the process; two processes running the same range at once can both pay for it.
        /// </remarks>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="schema">Databento schema the cache holds, e.g. <c>cbbo-1m</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive; its whole UTC day is asked for</param>
        /// <param name="endUtc">End of the range, exclusive; its whole UTC day is asked for</param>
        /// <param name="maxCostUsd">The most the new jobs may cost together, USD, as Databento prices them before they are submitted</param>
        /// <param name="ct">Cancellation token; a job already submitted keeps running and is reused by the next call</param>
        /// <returns>What was saved, the jobs and what they cost</returns>
        /// <exception cref="BatchJobTooExpensiveException">The new jobs would cost more than <paramref name="maxCostUsd"/>; nothing was submitted</exception>
        /// <exception cref="InvalidOperationException">A job expired before it was downloaded</exception>
        /// <exception cref="InvalidDataException">A downloaded file does not match its SHA-256</exception>
        public Task<BatchReport> RunBatchJobAsync(string dataset, string schema, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, decimal maxCostUsd, CancellationToken ct = default)
        {
            return this.batchJobs.RunAsync(dataset, schema, symbols, startUtc, endUtc, maxCostUsd, ct);
        }

        /// <summary>
        /// Saves the symbol-days a range lacks through Databento batch jobs; see
        /// <see cref="RunBatchJobAsync(string, string, IReadOnlyList{string}, DateTimeOffset, DateTimeOffset, decimal, CancellationToken)"/>
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="schema">Databento schema the cache holds, e.g. <c>cbbo-1m</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive; its whole UTC day is asked for</param>
        /// <param name="endUtc">End of the range, exclusive; its whole UTC day is asked for</param>
        /// <param name="maxCostUsd">The most the new jobs may cost together, USD</param>
        /// <returns>What was saved, the jobs and what they cost</returns>
        public BatchReport RunBatchJob(string dataset, string schema, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, decimal maxCostUsd)
        {
            return this.RunBatchJobAsync(dataset, schema, symbols, startUtc, endUtc, maxCostUsd).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Saves a Databento batch download folder made by any tool as day files: DBN or JSON, zstd or not, split by day, week, month or
        /// none, by symbol or by size. Only the final days its range holds whole are saved; a symbol-day the cache holds is left as it is.
        /// </summary>
        /// <param name="folder">The folder, with the <c>metadata.json</c> and <c>condition.json</c> Databento puts in it</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>What was saved, and the days left out</returns>
        /// <exception cref="InvalidDataException">The folder is not one the cache takes: no metadata, CSV files, symbols other than raw, or a
        /// schema the cache does not hold</exception>
        public Task<BatchReport> IngestBatchFolderAsync(string folder, CancellationToken ct = default)
        {
            return this.batchJobs.IngestFolderAsync(folder, ct);
        }

        /// <summary>
        /// Saves a Databento batch download folder made by any tool as day files; see
        /// <see cref="IngestBatchFolderAsync(string, CancellationToken)"/>
        /// </summary>
        /// <param name="folder">The folder, with the <c>metadata.json</c> and <c>condition.json</c> Databento puts in it</param>
        /// <returns>What was saved, and the days left out</returns>
        public BatchReport IngestBatchFolder(string folder)
        {
            return this.IngestBatchFolderAsync(folder).GetAwaiter().GetResult();
        }
    }
}
