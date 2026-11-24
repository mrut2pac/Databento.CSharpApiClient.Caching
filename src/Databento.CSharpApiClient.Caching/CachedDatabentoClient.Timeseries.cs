using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Client;
using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching
{
    public sealed partial class CachedDatabentoClient
    {
        /// <summary>
        /// Gets consolidated BBO 1-second snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<ICbboRecord[]> GetCbbo1sAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Cbbo1Sec, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets consolidated BBO 1-second snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<ICbboRecord[]> GetCbbo1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Cbbo1Sec, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets consolidated BBO 1-second snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public ICbboRecord[] GetCbbo1s(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetCbbo1sAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets consolidated BBO 1-second snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public ICbboRecord[] GetCbbo1s(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetCbbo1sAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets consolidated BBO 1-minute snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<ICbboRecord[]> GetCbbo1mAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Cbbo1Min, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets consolidated BBO 1-minute snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<ICbboRecord[]> GetCbbo1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Cbbo1Min, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets consolidated BBO 1-minute snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public ICbboRecord[] GetCbbo1m(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetCbbo1mAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets consolidated BBO 1-minute snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public ICbboRecord[] GetCbbo1m(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetCbbo1mAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets BBO 1-second snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IBboRecord[]> GetBbo1sAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Bbo1Sec, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets BBO 1-second snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IBboRecord[]> GetBbo1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Bbo1Sec, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets BBO 1-second snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IBboRecord[] GetBbo1s(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetBbo1sAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets BBO 1-second snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IBboRecord[] GetBbo1s(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetBbo1sAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets BBO 1-minute snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IBboRecord[]> GetBbo1mAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Bbo1Min, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets BBO 1-minute snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IBboRecord[]> GetBbo1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Bbo1Min, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets BBO 1-minute snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IBboRecord[] GetBbo1m(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetBbo1mAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets BBO 1-minute snapshots for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IBboRecord[] GetBbo1m(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetBbo1mAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets trades with the consolidated BBO at each trade (tcbbo) for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<ITcbboRecord[]> GetTcbboAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Tcbbo, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets trades with the consolidated BBO at each trade (tcbbo) for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<ITcbboRecord[]> GetTcbboAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Tcbbo, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets trades with the consolidated BBO at each trade (tcbbo) for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public ITcbboRecord[] GetTcbbo(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetTcbboAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets trades with the consolidated BBO at each trade (tcbbo) for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public ITcbboRecord[] GetTcbbo(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetTcbboAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets trades for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<ITradeRecord[]> GetTradesAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Trades, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets trades for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<ITradeRecord[]> GetTradesAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Trades, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets trades for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public ITradeRecord[] GetTrades(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetTradesAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets trades for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public ITradeRecord[] GetTrades(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetTradesAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-second OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IOhlcvRecord[]> GetOhlcv1sAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Ohlcv1Sec, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets 1-second OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IOhlcvRecord[]> GetOhlcv1sAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Ohlcv1Sec, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets 1-second OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IOhlcvRecord[] GetOhlcv1s(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetOhlcv1sAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-second OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IOhlcvRecord[] GetOhlcv1s(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetOhlcv1sAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-minute OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IOhlcvRecord[]> GetOhlcv1mAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Ohlcv1Min, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets 1-minute OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IOhlcvRecord[]> GetOhlcv1mAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Ohlcv1Min, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets 1-minute OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IOhlcvRecord[] GetOhlcv1m(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetOhlcv1mAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-minute OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IOhlcvRecord[] GetOhlcv1m(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetOhlcv1mAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-hour OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IOhlcvRecord[]> GetOhlcv1hAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Ohlcv1Hour, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets 1-hour OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IOhlcvRecord[]> GetOhlcv1hAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Ohlcv1Hour, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets 1-hour OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IOhlcvRecord[] GetOhlcv1h(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetOhlcv1hAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-hour OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IOhlcvRecord[] GetOhlcv1h(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetOhlcv1hAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets daily OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IOhlcvRecord[]> GetOhlcv1dAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Ohlcv1Day, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets daily OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IOhlcvRecord[]> GetOhlcv1dAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Ohlcv1Day, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets daily OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IOhlcvRecord[] GetOhlcv1d(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetOhlcv1dAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets daily OHLCV bars for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IOhlcvRecord[] GetOhlcv1d(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetOhlcv1dAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets statistics for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IStatisticsRecord[]> GetStatisticsAsync(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Statistics, dataset, new[] { symbol }, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets statistics for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The range's records, in time order</returns>
        public Task<IStatisticsRecord[]> GetStatisticsAsync(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use, CancellationToken ct = default)
        {
            return this.timeseries.GetAsync(CachedSchemas.Statistics, dataset, symbols, startUtc, endUtc, cacheMode, ct);
        }

        /// <summary>
        /// Gets statistics for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IStatisticsRecord[] GetStatistics(string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetStatisticsAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets statistics for several symbols, from the cache where it holds them; only the symbol-days it does not hold are downloaded
        /// </summary>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order</returns>
        public IStatisticsRecord[] GetStatistics(string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode = CacheMode.Use)
        {
            return this.GetStatisticsAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

    }
}
