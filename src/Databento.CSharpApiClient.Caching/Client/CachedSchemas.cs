using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.Caching.Compaction;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;
using Databento.CSharpApiClient.DataModel.Json;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// The schemas the cached client serves, one per schema the archive has columns for. A record's day is the UTC date of
    /// <c>ts_recv</c> where the schema has it and of <c>ts_event</c> for OHLCV, the timestamp Databento filters a range on and cuts
    /// a batch job's daily files by.
    /// </summary>
    internal static class CachedSchemas
    {
        public static readonly CachedSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson> Cbbo1Sec = new CachedSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson>(
            Schema.ConsolidatedBBO1Sec,
            RecordSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson>(Schema.ConsolidatedBBO1Sec),
            JsonRecordReader.ReadCbboAsync,
            (stream, ct) => ToJson(DbnReader.ReadCbboAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetCbbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetCbbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson> Cbbo1Min = new CachedSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson>(
            Schema.ConsolidatedBBO1Min,
            RecordSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson>(Schema.ConsolidatedBBO1Min),
            JsonRecordReader.ReadCbboAsync,
            (stream, ct) => ToJson(DbnReader.ReadCbboAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetCbbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetCbbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IBboRecord, BboRecordDbn, BboRecordJson> Bbo1Sec = new CachedSchema<IBboRecord, BboRecordDbn, BboRecordJson>(
            Schema.Bbo1Sec,
            RecordSchema<IBboRecord, BboRecordDbn, BboRecordJson>(Schema.Bbo1Sec),
            JsonRecordReader.ReadBboAsync,
            (stream, ct) => ToJson(DbnReader.ReadBboAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetBbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetBbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IBboRecord, BboRecordDbn, BboRecordJson> Bbo1Min = new CachedSchema<IBboRecord, BboRecordDbn, BboRecordJson>(
            Schema.Bbo1Min,
            RecordSchema<IBboRecord, BboRecordDbn, BboRecordJson>(Schema.Bbo1Min),
            JsonRecordReader.ReadBboAsync,
            (stream, ct) => ToJson(DbnReader.ReadBboAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetBbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetBbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<ITcbboRecord, TcbboRecordDbn, TcbboRecordJson> Tcbbo = new CachedSchema<ITcbboRecord, TcbboRecordDbn, TcbboRecordJson>(
            Schema.Tcbbo,
            RecordSchema<ITcbboRecord, TcbboRecordDbn, TcbboRecordJson>(Schema.Tcbbo),
            JsonRecordReader.ReadTcbboAsync,
            (stream, ct) => ToJson(DbnReader.ReadTcbboAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetTcbboAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetTcbboAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<ITradeRecord, TradeRecordDbn, TradeRecordJson> Trades = new CachedSchema<ITradeRecord, TradeRecordDbn, TradeRecordJson>(
            Schema.Trades,
            RecordSchema<ITradeRecord, TradeRecordDbn, TradeRecordJson>(Schema.Trades),
            JsonRecordReader.ReadTradesAsync,
            (stream, ct) => ToJson(DbnReader.ReadTradesAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetTradesAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetTradesAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson> Ohlcv1Sec = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(
            Schema.Ohlcv1Sec,
            RecordSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(Schema.Ohlcv1Sec),
            JsonRecordReader.ReadOhlcvAsync,
            (stream, ct) => ToJson(DbnReader.ReadOhlcvAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetOhlcv1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetOhlcv1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson> Ohlcv1Min = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(
            Schema.Ohlcv1Min,
            RecordSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(Schema.Ohlcv1Min),
            JsonRecordReader.ReadOhlcvAsync,
            (stream, ct) => ToJson(DbnReader.ReadOhlcvAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetOhlcv1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetOhlcv1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson> Ohlcv1Hour = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(
            Schema.Ohlcv1Hour,
            RecordSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(Schema.Ohlcv1Hour),
            JsonRecordReader.ReadOhlcvAsync,
            (stream, ct) => ToJson(DbnReader.ReadOhlcvAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetOhlcv1hAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetOhlcv1hAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson> Ohlcv1Day = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(
            Schema.Ohlcv1Day,
            RecordSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(Schema.Ohlcv1Day),
            JsonRecordReader.ReadOhlcvAsync,
            (stream, ct) => ToJson(DbnReader.ReadOhlcvAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetOhlcv1dAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetOhlcv1dAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IStatisticsRecord, StatisticsRecordDbn, StatisticsRecordJson> Statistics = new CachedSchema<IStatisticsRecord, StatisticsRecordDbn, StatisticsRecordJson>(
            Schema.Statistics,
            RecordSchema<IStatisticsRecord, StatisticsRecordDbn, StatisticsRecordJson>(Schema.Statistics),
            JsonRecordReader.ReadStatisticsAsync,
            (stream, ct) => ToJson(DbnReader.ReadStatisticsAsync(stream, ct), record => record.ToJsonRecord(), ct),
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (record, symbol) => record.Symbol = symbol,
            (client, request) => client.GetStatisticsAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetStatisticsAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

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
        /// Gets the archive columns of a schema, as the compactor writes them
        /// </summary>
        private static ArchiveRecordSchema<TSource, TRecord> RecordSchema<TSource, TRecord, TJson>(string schema)
            where TSource : class, IRecord
            where TRecord : class, TSource
            where TJson : class, TSource
        {
            ArchiveSchemaHandlers.TryGet(schema, out IArchiveSchemaHandler handler);
            return ((ArchiveSchemaHandler<TSource, TRecord, TJson>)handler).RecordSchema;
        }

        /// <summary>
        /// Converts DBN records to JSON records as they are decoded; the reader was handed the token already, so it is checked between records
        /// </summary>
        private static async IAsyncEnumerable<TJson> ToJson<TDbn, TJson>(IAsyncEnumerable<TDbn> records, Func<TDbn, TJson> convert, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach(TDbn record in records.ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return convert(record);
            }
        }
    }
}
