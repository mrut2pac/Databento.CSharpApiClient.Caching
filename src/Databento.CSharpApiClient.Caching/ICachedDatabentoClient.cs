// Ignore Spelling: Bbo Cbbo Tcbbo Ohlcv

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Metadata;
using Databento.CSharpApiClient.DataModel.Symbology;

namespace Databento.CSharpApiClient.Caching
{
    /// <summary>
    /// The calls of <see cref="CachedDatabentoClient"/>: the client's time-series calls with a <see cref="CacheMode"/>, the batch jobs,
    /// Databento's metadata and symbology calls, passed through uncached, and the event for unreadable archive files. Code written against it can be handed a fake in tests. As an
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

        /// <summary>
        /// Lists the datasets the API key can reach: Databento's own answer, not cached
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The dataset codes</returns>
        Task<string[]> ListDatasetsAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ListDatasetsAsync is not implemented.");
        }

        /// <summary>
        /// Lists the schemas of a dataset: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The schema names</returns>
        Task<string[]> ListSchemasAsync(string dataset, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ListSchemasAsync is not implemented.");
        }

        /// <summary>
        /// Lists Databento's publishers: Databento's own answer, not cached
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Each publisher: its id, dataset and venue</returns>
        Task<PublisherInfo[]> ListPublishersAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ListPublishersAsync is not implemented.");
        }

        /// <summary>
        /// Lists the fields of a schema, with their types: Databento's own answer, not cached
        /// </summary>
        /// <param name="schema">Databento schema, e.g. <c>trades</c></param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Each field's name and type</returns>
        Task<FieldInfo[]> ListFieldsAsync(string schema, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ListFieldsAsync is not implemented.");
        }

        /// <summary>
        /// Lists a dataset's unit prices, per schema: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The unit prices of each feed mode</returns>
        Task<UnitPriceInfo[]> ListUnitPricesAsync(string dataset, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ListUnitPricesAsync is not implemented.");
        }

        /// <summary>
        /// Gets a dataset's data-quality condition on one day, or <c>null</c> when the dataset has no entry for it. Without a date it is the latest day's, which downloads the dataset's whole daily history: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="dateStr">ISO-8601 date, e.g. <c>2026-01-15</c>; <c>null</c> for the latest day</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The day's condition, or <c>null</c></returns>
        Task<DatasetCondition> GetDatasetConditionAsync(string dataset, string dateStr = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetDatasetConditionAsync is not implemented.");
        }

        /// <summary>
        /// Gets the range a dataset holds data for, and each schema's: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The dataset's range and each schema's</returns>
        Task<DateRange> GetDatasetRangeAsync(string dataset, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetDatasetRangeAsync is not implemented.");
        }

        /// <summary>
        /// Lists a dataset's daily data-quality conditions, oldest first: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="startDateStr">ISO-8601 first date, inclusive, or <c>null</c></param>
        /// <param name="endDateStr">ISO-8601 last date, inclusive, or <c>null</c></param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Each day's condition</returns>
        Task<DatasetCondition[]> ListConditionsAsync(string dataset, string startDateStr = null, string endDateStr = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ListConditionsAsync is not implemented.");
        }

        /// <summary>
        /// Gets the number of records a query would return, without downloading it: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The record count</returns>
        Task<long> GetRecordCountAsync(string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetRecordCountAsync is not implemented.");
        }

        /// <summary>
        /// Gets the billable uncompressed size of a query in bytes, without downloading it: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The size in bytes</returns>
        Task<long> GetBillableSizeAsync(string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetBillableSizeAsync is not implemented.");
        }

        /// <summary>
        /// Gets what a query would cost in USD, without downloading it: Databento's own answer, not cached
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The cost in USD</returns>
        Task<double> GetCostAsync(string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("GetCostAsync is not implemented.");
        }

        /// <summary>
        /// Resolves symbols from one symbology type to another over a date range: Databento's own answer, not cached
        /// </summary>
        /// <param name="request">What to resolve: dataset, symbols, symbol types and dates</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Each symbol's mappings, and the symbols not found</returns>
        Task<SymbologyResolution> ResolveSymbolsAsync(SymbologyRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("ResolveSymbolsAsync is not implemented.");
        }
    }
}
