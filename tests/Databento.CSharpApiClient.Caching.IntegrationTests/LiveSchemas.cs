using System;
using System.Threading.Tasks;

using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching.IntegrationTests
{
    /// <summary>
    /// Calls the cached client's method for a schema by its Databento name
    /// </summary>
    internal static class LiveSchemas
    {
        public static async Task<IRecord[]> GetAsync(CachedDatabentoClient client, string schema, string dataset, string symbol, DateTimeOffset startUtc, DateTimeOffset endUtc)
        {
            return schema switch
            {
                "cbbo-1s" => await client.GetCbbo1sAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "cbbo-1m" => await client.GetCbbo1mAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "bbo-1s" => await client.GetBbo1sAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "bbo-1m" => await client.GetBbo1mAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "tcbbo" => await client.GetTcbboAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "trades" => await client.GetTradesAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "ohlcv-1s" => await client.GetOhlcv1sAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "ohlcv-1m" => await client.GetOhlcv1mAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "ohlcv-1h" => await client.GetOhlcv1hAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "ohlcv-1d" => await client.GetOhlcv1dAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                "statistics" => await client.GetStatisticsAsync(dataset, symbol, startUtc, endUtc).ConfigureAwait(false),
                _ => throw new ArgumentException(Invariant($"No cached call for {schema}."), nameof(schema)),
            };
        }
    }
}
