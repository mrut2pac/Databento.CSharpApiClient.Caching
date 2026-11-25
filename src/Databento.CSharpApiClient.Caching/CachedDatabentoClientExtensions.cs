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
    /// The one-symbol and synchronous forms of the <see cref="ICachedDatabentoClient"/> calls. A call without a <see cref="CacheMode"/>
    /// is the client's own, from <see cref="DatabentoTimeseriesClientExtensions"/> in the <c>Databento.CSharpApiClient</c> namespace,
    /// and reads the cache; on <see cref="CachedDatabentoClient"/> its own methods are called instead of these.
    /// </summary>
    public static class CachedDatabentoClientExtensions
    {
        /// <summary>
        /// Gets consolidated BBO 1-second snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<ICbboRecord[]> GetCbbo1sAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetCbbo1sAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets consolidated BBO 1-second snapshots for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static ICbboRecord[] GetCbbo1s(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetCbbo1sAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets consolidated BBO 1-second snapshots for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static ICbboRecord[] GetCbbo1s(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetCbbo1sAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets consolidated BBO 1-minute snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<ICbboRecord[]> GetCbbo1mAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetCbbo1mAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets consolidated BBO 1-minute snapshots for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static ICbboRecord[] GetCbbo1m(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetCbbo1mAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets consolidated BBO 1-minute snapshots for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static ICbboRecord[] GetCbbo1m(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetCbbo1mAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets BBO 1-second snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<IBboRecord[]> GetBbo1sAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetBbo1sAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets BBO 1-second snapshots for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IBboRecord[] GetBbo1s(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetBbo1sAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets BBO 1-second snapshots for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IBboRecord[] GetBbo1s(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetBbo1sAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets BBO 1-minute snapshots for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<IBboRecord[]> GetBbo1mAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetBbo1mAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets BBO 1-minute snapshots for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IBboRecord[] GetBbo1m(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetBbo1mAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets BBO 1-minute snapshots for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IBboRecord[] GetBbo1m(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetBbo1mAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets trades, each with the consolidated BBO just before it, for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<ITcbboRecord[]> GetTcbboAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetTcbboAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets trades, each with the consolidated BBO just before it, for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static ITcbboRecord[] GetTcbbo(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetTcbboAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets trades, each with the consolidated BBO just before it, for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static ITcbboRecord[] GetTcbbo(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetTcbboAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets trades for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<ITradeRecord[]> GetTradesAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetTradesAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets trades for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static ITradeRecord[] GetTrades(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetTradesAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets trades for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static ITradeRecord[] GetTrades(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetTradesAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-second OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<IOhlcvRecord[]> GetOhlcv1sAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetOhlcv1sAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets 1-second OHLCV bars for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IOhlcvRecord[] GetOhlcv1s(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetOhlcv1sAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-second OHLCV bars for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IOhlcvRecord[] GetOhlcv1s(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetOhlcv1sAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-minute OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<IOhlcvRecord[]> GetOhlcv1mAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetOhlcv1mAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets 1-minute OHLCV bars for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IOhlcvRecord[] GetOhlcv1m(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetOhlcv1mAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-minute OHLCV bars for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IOhlcvRecord[] GetOhlcv1m(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetOhlcv1mAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-hour OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<IOhlcvRecord[]> GetOhlcv1hAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetOhlcv1hAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets 1-hour OHLCV bars for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IOhlcvRecord[] GetOhlcv1h(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetOhlcv1hAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets 1-hour OHLCV bars for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IOhlcvRecord[] GetOhlcv1h(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetOhlcv1hAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets daily OHLCV bars for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<IOhlcvRecord[]> GetOhlcv1dAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetOhlcv1dAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets daily OHLCV bars for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IOhlcvRecord[] GetOhlcv1d(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetOhlcv1dAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets daily OHLCV bars for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IOhlcvRecord[] GetOhlcv1d(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetOhlcv1dAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets statistics, such as settlement prices and open interest, for one symbol, from the cache where it holds them
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static Task<IStatisticsRecord[]> GetStatisticsAsync(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetStatisticsAsync(dataset, new[] { symbol }, startUtc, endUtc, cacheMode, cancellationToken);
        }

        /// <summary>
        /// Gets statistics, such as settlement prices and open interest, for one symbol, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbol">Databento raw symbol</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IStatisticsRecord[] GetStatistics(this ICachedDatabentoClient client, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            return client.GetStatisticsAsync(dataset, symbol, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets statistics, such as settlement prices and open interest, for several symbols, from the cache where it holds them, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <param name="cacheMode">Which caches are read</param>
        /// <returns>The range's records, in time order: a new array the caller owns</returns>
        public static IStatisticsRecord[] GetStatistics(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CacheMode cacheMode)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetStatisticsAsync(dataset, symbols, startUtc, endUtc, cacheMode).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Saves the symbol-days a range lacks through Databento batch jobs, synchronously; see
        /// <see cref="ICachedDatabentoClient.RunBatchJobAsync"/>
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="schema">Databento schema the cache holds, e.g. <c>cbbo-1m</c></param>
        /// <param name="symbols">Databento raw symbols</param>
        /// <param name="startUtc">Start of the range, inclusive; its whole UTC day is asked for</param>
        /// <param name="endUtc">End of the range, exclusive; its whole UTC day is asked for</param>
        /// <param name="maxCostUsd">The most the new jobs may cost together, USD</param>
        /// <returns>What was saved, the jobs and what they cost</returns>
        public static BatchReport RunBatchJob(this ICachedDatabentoClient client, string dataset, string schema, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, decimal maxCostUsd)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.RunBatchJobAsync(dataset, schema, symbols, startUtc, endUtc, maxCostUsd).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Saves a Databento batch download folder made by any tool as day files, synchronously; see
        /// <see cref="ICachedDatabentoClient.IngestBatchFolderAsync"/>
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="folder">The folder, with the <c>metadata.json</c> and <c>condition.json</c> Databento puts in it</param>
        /// <returns>What was saved, and the days left out</returns>
        public static BatchReport IngestBatchFolder(this ICachedDatabentoClient client, string folder)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.IngestBatchFolderAsync(folder).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Lists the datasets the API key can reach: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <returns>The dataset codes</returns>
        public static string[] ListDatasets(this ICachedDatabentoClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.ListDatasetsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Lists the schemas of a dataset: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <returns>The schema names</returns>
        public static string[] ListSchemas(this ICachedDatabentoClient client, string dataset)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.ListSchemasAsync(dataset).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Lists Databento's publishers: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <returns>Each publisher: its id, dataset and venue</returns>
        public static PublisherInfo[] ListPublishers(this ICachedDatabentoClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.ListPublishersAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Lists the fields of a schema, with their types: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="schema">Databento schema, e.g. <c>trades</c></param>
        /// <returns>Each field's name and type</returns>
        public static FieldInfo[] ListFields(this ICachedDatabentoClient client, string schema)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.ListFieldsAsync(schema).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Lists a dataset's unit prices, per schema: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <returns>The unit prices of each feed mode</returns>
        public static UnitPriceInfo[] ListUnitPrices(this ICachedDatabentoClient client, string dataset)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.ListUnitPricesAsync(dataset).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets a dataset's data-quality condition on one day, or <c>null</c> when the dataset has no entry for it. Without a date it is the latest day's, which downloads the dataset's whole daily history: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="dateStr">ISO-8601 date, e.g. <c>2026-01-15</c>; <c>null</c> for the latest day</param>
        /// <returns>The day's condition, or <c>null</c></returns>
        public static DatasetCondition GetDatasetCondition(this ICachedDatabentoClient client, string dataset, string dateStr = null)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetDatasetConditionAsync(dataset, dateStr).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets the range a dataset holds data for, and each schema's: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <returns>The dataset's range and each schema's</returns>
        public static DateRange GetDatasetRange(this ICachedDatabentoClient client, string dataset)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetDatasetRangeAsync(dataset).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Lists a dataset's daily data-quality conditions, oldest first: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="startDateStr">ISO-8601 first date, inclusive, or <c>null</c></param>
        /// <param name="endDateStr">ISO-8601 last date, inclusive, or <c>null</c></param>
        /// <returns>Each day's condition</returns>
        public static DatasetCondition[] ListConditions(this ICachedDatabentoClient client, string dataset, string startDateStr = null, string endDateStr = null)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.ListConditionsAsync(dataset, startDateStr, endDateStr).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets the number of records a query would return, without downloading it: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <returns>The record count</returns>
        public static long GetRecordCount(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetRecordCountAsync(dataset, symbols, schema, startUtc, endUtc).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets the billable uncompressed size of a query in bytes, without downloading it: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <returns>The size in bytes</returns>
        public static long GetBillableSize(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetBillableSizeAsync(dataset, symbols, schema, startUtc, endUtc).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets what a query would cost in USD, without downloading it: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="dataset">Databento dataset, e.g. <c>OPRA.PILLAR</c></param>
        /// <param name="symbols">Instrument symbols</param>
        /// <param name="schema">Databento schema, e.g. <c>cbbo-1m</c></param>
        /// <param name="startUtc">Start of the range, inclusive</param>
        /// <param name="endUtc">End of the range, exclusive</param>
        /// <returns>The cost in USD</returns>
        public static double GetCost(this ICachedDatabentoClient client, string dataset, IReadOnlyList<string> symbols, string schema, DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.GetCostAsync(dataset, symbols, schema, startUtc, endUtc).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Resolves symbols from one symbology type to another over a date range: Databento's own answer, not cached, synchronously
        /// </summary>
        /// <param name="client">The cached client</param>
        /// <param name="request">What to resolve: dataset, symbols, symbol types and dates</param>
        /// <returns>Each symbol's mappings, and the symbols not found</returns>
        public static SymbologyResolution ResolveSymbols(this ICachedDatabentoClient client, SymbologyRequest request)
        {
            ArgumentNullException.ThrowIfNull(client);
            return client.ResolveSymbolsAsync(request).GetAwaiter().GetResult();
        }
    }
}
