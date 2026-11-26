using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// A schema the cached client serves, by name: what the batch calls, which take the schema as Databento names it, need of it
    /// </summary>
    internal interface ICachedSchema
    {
        /// <summary>
        /// Gets Databento's name of the schema, e.g. <c>cbbo-1m</c>.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Saves a batch folder of this schema as day files
        /// </summary>
        /// <param name="ingestion">The ingestion that saves it</param>
        /// <param name="folder">The folder, its metadata read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>What was saved</returns>
        Task<BatchCounts> IngestAsync(BatchIngestion ingestion, BatchFolder folder, CancellationToken cancellationToken);

        /// <summary>
        /// Tells whether the cache holds a symbol-day of this schema, from the day file's name or the archive's index, reading no records
        /// </summary>
        /// <param name="store">The cache</param>
        /// <param name="folder">Month folder of the day, as listed</param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="day">UTC day</param>
        /// <param name="strict">Whether an archive file that cannot be read throws, rather than counting as not holding the day</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns><c>true</c> when a day file or the archive holds the day, empty or not</returns>
        Task<bool> ContainsAsync(SymbolDayStore store, CacheMonthFolder folder, string symbol, DateTime day, bool strict, CancellationToken cancellationToken);

        /// <summary>
        /// Makes sure the cache holds a range of this schema, reading no cached record
        /// </summary>
        /// <param name="timeseries">The downloads that save what is missing</param>
        /// <param name="dataset">Databento dataset</param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The symbol-days found cached, saved and not saved</returns>
        Task<EnsureCachedReport> EnsureCachedAsync(CachedTimeseries timeseries, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
    }
}
