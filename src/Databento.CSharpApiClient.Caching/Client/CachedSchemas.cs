using System.Linq;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.Caching.Compaction;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;
using Databento.CSharpApiClient.DataModel.Json;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// The schemas the cached client serves, one per schema the archive has columns for. A record's day is the UTC date of
    /// <c>ts_recv</c> where the schema has it and of <c>ts_event</c> for OHLCV, the timestamp Databento filters a range on.
    /// </summary>
    internal static class CachedSchemas
    {
        public static readonly CachedSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson> Cbbo1Sec = new CachedSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson>(
            Schema.ConsolidatedBBO1Sec,
            RecordSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson>(Schema.ConsolidatedBBO1Sec),
            JsonRecordReader.ReadCbboAsync,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (client, request) => client.GetCbbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetCbbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson> Cbbo1Min = new CachedSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson>(
            Schema.ConsolidatedBBO1Min,
            RecordSchema<ICbboRecord, CbboRecordDbn, CbboRecordJson>(Schema.ConsolidatedBBO1Min),
            JsonRecordReader.ReadCbboAsync,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (client, request) => client.GetCbbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetCbbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IBboRecord, BboRecordDbn, BboRecordJson> Bbo1Sec = new CachedSchema<IBboRecord, BboRecordDbn, BboRecordJson>(
            Schema.Bbo1Sec,
            RecordSchema<IBboRecord, BboRecordDbn, BboRecordJson>(Schema.Bbo1Sec),
            JsonRecordReader.ReadBboAsync,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (client, request) => client.GetBbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetBbo1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IBboRecord, BboRecordDbn, BboRecordJson> Bbo1Min = new CachedSchema<IBboRecord, BboRecordDbn, BboRecordJson>(
            Schema.Bbo1Min,
            RecordSchema<IBboRecord, BboRecordDbn, BboRecordJson>(Schema.Bbo1Min),
            JsonRecordReader.ReadBboAsync,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (client, request) => client.GetBbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetBbo1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<ITcbboRecord, TcbboRecordDbn, TcbboRecordJson> Tcbbo = new CachedSchema<ITcbboRecord, TcbboRecordDbn, TcbboRecordJson>(
            Schema.Tcbbo,
            RecordSchema<ITcbboRecord, TcbboRecordDbn, TcbboRecordJson>(Schema.Tcbbo),
            JsonRecordReader.ReadTcbboAsync,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (client, request) => client.GetTcbboAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetTcbboAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<ITradeRecord, TradeRecordDbn, TradeRecordJson> Trades = new CachedSchema<ITradeRecord, TradeRecordDbn, TradeRecordJson>(
            Schema.Trades,
            RecordSchema<ITradeRecord, TradeRecordDbn, TradeRecordJson>(Schema.Trades),
            JsonRecordReader.ReadTradesAsync,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (client, request) => client.GetTradesAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetTradesAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson> Ohlcv1Sec = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(
            Schema.Ohlcv1Sec,
            RecordSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(Schema.Ohlcv1Sec),
            JsonRecordReader.ReadOhlcvAsync,
            record => record.TsEventUtc,
            (client, request) => client.GetOhlcv1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetOhlcv1sAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson> Ohlcv1Min = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(
            Schema.Ohlcv1Min,
            RecordSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(Schema.Ohlcv1Min),
            JsonRecordReader.ReadOhlcvAsync,
            record => record.TsEventUtc,
            (client, request) => client.GetOhlcv1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetOhlcv1mAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson> Ohlcv1Hour = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(
            Schema.Ohlcv1Hour,
            RecordSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(Schema.Ohlcv1Hour),
            JsonRecordReader.ReadOhlcvAsync,
            record => record.TsEventUtc,
            (client, request) => client.GetOhlcv1hAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetOhlcv1hAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson> Ohlcv1Day = new CachedSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(
            Schema.Ohlcv1Day,
            RecordSchema<IOhlcvRecord, OhlcvRecordDbn, OhlcvRecordJson>(Schema.Ohlcv1Day),
            JsonRecordReader.ReadOhlcvAsync,
            record => record.TsEventUtc,
            (client, request) => client.GetOhlcv1dAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetOhlcv1dAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

        public static readonly CachedSchema<IStatisticsRecord, StatisticsRecordDbn, StatisticsRecordJson> Statistics = new CachedSchema<IStatisticsRecord, StatisticsRecordDbn, StatisticsRecordJson>(
            Schema.Statistics,
            RecordSchema<IStatisticsRecord, StatisticsRecordDbn, StatisticsRecordJson>(Schema.Statistics),
            JsonRecordReader.ReadStatisticsAsync,
            record => record.TsReceivedUtc ?? record.TsEventUtc,
            (client, request) => client.GetStatisticsAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken),
            async (client, request) => (await client.GetStatisticsAsync(request.Dataset, request.Symbols, request.StartUtc, request.EndUtc, request.CancellationToken).ConfigureAwait(false)).Select(record => record.ToJsonRecord()).ToArray());

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
    }
}
