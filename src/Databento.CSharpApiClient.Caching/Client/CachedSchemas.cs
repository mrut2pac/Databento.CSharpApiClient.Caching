// Ignore Spelling: Bbo Cbbo Tcbbo Ohlcv

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// The schemas the cached client serves, one per schema the archive has columns for. A record's day is the UTC date of
    /// <c>ts_recv</c> where the schema has it and of <c>ts_event</c> for OHLCV, the timestamp Databento filters a range on and cuts
    /// a batch job's daily files by.
    /// </summary>
    internal static class CachedSchemas
    {
        public static readonly CachedSchema<ICbboRecord, CbboRecordDbn> Cbbo1Sec = new CachedSchema<ICbboRecord, CbboRecordDbn>(
            DayFileFormats.Cbbo1Sec,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Cbbo1Sec, client.GetCbbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetCbbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<ICbboRecord, CbboRecordDbn> Cbbo1Min = new CachedSchema<ICbboRecord, CbboRecordDbn>(
            DayFileFormats.Cbbo1Min,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Cbbo1Min, client.GetCbbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetCbbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<IBboRecord, BboRecordDbn> Bbo1Sec = new CachedSchema<IBboRecord, BboRecordDbn>(
            DayFileFormats.Bbo1Sec,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Bbo1Sec, client.GetBbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetBbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<IBboRecord, BboRecordDbn> Bbo1Min = new CachedSchema<IBboRecord, BboRecordDbn>(
            DayFileFormats.Bbo1Min,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Bbo1Min, client.GetBbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetBbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<ITcbboRecord, TcbboRecordDbn> Tcbbo = new CachedSchema<ITcbboRecord, TcbboRecordDbn>(
            DayFileFormats.Tcbbo,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Tcbbo, client.GetTcbboAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetTcbboAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<ITradeRecord, TradeRecordDbn> Trades = new CachedSchema<ITradeRecord, TradeRecordDbn>(
            DayFileFormats.Trades,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Trades, client.GetTradesAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetTradesAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn> Ohlcv1Sec = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn>(
            DayFileFormats.Ohlcv1Sec,
            record => record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Ohlcv1Sec, client.GetOhlcv1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetOhlcv1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn> Ohlcv1Min = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn>(
            DayFileFormats.Ohlcv1Min,
            record => record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Ohlcv1Min, client.GetOhlcv1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetOhlcv1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn> Ohlcv1Hour = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn>(
            DayFileFormats.Ohlcv1Hour,
            record => record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Ohlcv1Hour, client.GetOhlcv1hAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetOhlcv1hAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn> Ohlcv1Day = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn>(
            DayFileFormats.Ohlcv1Day,
            record => record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Ohlcv1Day, client.GetOhlcv1dAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetOhlcv1dAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        public static readonly CachedSchema<IStatisticsRecord, StatisticsRecordDbn> Statistics = new CachedSchema<IStatisticsRecord, StatisticsRecordDbn>(
            DayFileFormats.Statistics,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => FromJsonAsync(DayFileFormats.Statistics, client.GetStatisticsAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken)),
            (client, request) => client.GetStatisticsAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken));

        private static readonly Dictionary<string, ICachedSchema> ByName = new ICachedSchema[] { Cbbo1Sec, Cbbo1Min, Bbo1Sec, Bbo1Min, Tcbbo, Trades, Ohlcv1Sec, Ohlcv1Min, Ohlcv1Hour, Ohlcv1Day, Statistics }.ToDictionary(schema => schema.Name, StringComparer.Ordinal);

        /// <summary>
        /// Gets the names of the schemas served, e.g. <c>cbbo-1m</c>.
        /// </summary>
        public static IEnumerable<string> Names => ByName.Keys;

        /// <summary>
        /// Finds a schema served by its Databento name
        /// </summary>
        /// <returns><c>false</c> for a schema the cache does not hold</returns>
        public static bool TryGet(string name, out ICachedSchema schema)
        {
            return ByName.TryGetValue(name ?? string.Empty, out schema);
        }

        /// <summary>
        /// Converts the JSON records of a download to the DBN record type
        /// </summary>
        private static async Task<TRecord[]> FromJsonAsync<TSource, TRecord, TJson>(DayFileFormat<TSource, TRecord> format, Task<TJson[]> download)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            TJson[] records = await download.ConfigureAwait(false);
            return Array.ConvertAll(records, record => format.RecordSchema.ToRecord(record));
        }
    }
}
