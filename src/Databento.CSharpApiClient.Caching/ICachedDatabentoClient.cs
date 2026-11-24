// Ignore Spelling: Bbo Cbbo Tcbbo Ohlcv

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// The calls of <see cref="CachedDatabentoClient"/>: the client's time-series calls with a <see cref="CacheMode"/>, the batch jobs,
    /// and the event for unreadable archive files. Code written against it can be handed a fake in tests. As an
    /// <see cref="IDatabentoTimeseriesClient"/> it serves every call with <see cref="CacheMode.Use"/>, so code that takes either client
    /// takes the cache too. <see cref="CachedDatabentoClientExtensions"/> adds the one-symbol and synchronous forms of each call.
    /// </summary>
    /// <remarks>
    /// An implementation writes only the calls with a <see cref="CacheMode"/>: default implementations here serve the client's own calls
    /// through them with <see cref="CacheMode.Use"/>. Unlike the Databento clients, the cache refuses <c>ALL_SYMBOLS</c> with an
    /// <see cref="ArgumentException"/>: it keeps each symbol's days apart, so it needs the raw symbols. Each time-series call returns a new
    /// array the caller owns. A call added in a later version comes with a default implementation that throws
    /// <see cref="NotSupportedException"/>, so implementations written against this version keep compiling.
    /// </remarks>
    public interface ICachedDatabentoClient : IDatabentoTimeseriesClient
    {
        /// <summary>
        /// Raised once per archive file per UTC day when an archive file cannot be read, for the host to alarm on
        /// </summary>
        event EventHandler<ArchiveFileUnreadableEventArgs> ArchiveFileUnreadable;

        /// <summary>
        /// Gets consolidated BBO 1-second snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<ICbboRecord[]> GetCbbo1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets consolidated BBO 1-minute snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<ICbboRecord[]> GetCbbo1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets BBO 1-second snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<IBboRecord[]> GetBbo1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets BBO 1-minute snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<IBboRecord[]> GetBbo1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets trades, each with the consolidated BBO just before it, for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<ITcbboRecord[]> GetTcbboAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets trades for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<ITradeRecord[]> GetTradesAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets 1-second OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<IOhlcvRecord[]> GetOhlcv1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets 1-minute OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<IOhlcvRecord[]> GetOhlcv1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets 1-hour OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<IOhlcvRecord[]> GetOhlcv1hAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets daily OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<IOhlcvRecord[]> GetOhlcv1dAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets statistics, such as settlement prices and open interest, for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        Task<IStatisticsRecord[]> GetStatisticsAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the symbol-days a range lacks through Databento batch jobs, and returns once they are saved
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="schema">Databento schema the cache holds, e.g. <c>cbbo-1m</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive; its whole UTC day is asked for</param>
        /// <param name="endUtc">End of the range, exclusive; its whole UTC day is asked for</param>
        /// <param name="maxCostUsd">The most the new jobs may cost together, USD, as Databento prices them before they are submitted</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>What was saved, the jobs and what they cost</returns>
        Task<BatchReport> RunBatchJobAsync(string dataset, string schema, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, decimal maxCostUsd, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves a Databento batch download folder made by any tool as day files
        /// </summary>
        /// <param name="folder">The folder, with the <c>metadata.json</c> and <c>condition.json</c> Databento puts in it</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>What was saved, and the days left out</returns>
        Task<BatchReport> IngestBatchFolderAsync(string folder, CancellationToken cancellationToken = default);

        // as the client's own interface, every call reads the cache
        Task<ICbboRecord[]> IDatabentoTimeseriesClient.GetCbbo1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetCbbo1sAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<ICbboRecord[]> IDatabentoTimeseriesClient.GetCbbo1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetCbbo1mAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<IBboRecord[]> IDatabentoTimeseriesClient.GetBbo1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetBbo1sAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<IBboRecord[]> IDatabentoTimeseriesClient.GetBbo1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetBbo1mAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<ITcbboRecord[]> IDatabentoTimeseriesClient.GetTcbboAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetTcbboAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<ITradeRecord[]> IDatabentoTimeseriesClient.GetTradesAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetTradesAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<IOhlcvRecord[]> IDatabentoTimeseriesClient.GetOhlcv1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetOhlcv1sAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<IOhlcvRecord[]> IDatabentoTimeseriesClient.GetOhlcv1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetOhlcv1mAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<IOhlcvRecord[]> IDatabentoTimeseriesClient.GetOhlcv1hAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetOhlcv1hAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<IOhlcvRecord[]> IDatabentoTimeseriesClient.GetOhlcv1dAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetOhlcv1dAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }

        Task<IStatisticsRecord[]> IDatabentoTimeseriesClient.GetStatisticsAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return this.GetStatisticsAsync(dataset, symbols, startUtc, endUtc, CacheMode.Use, cancellationToken);
        }
    }
}
