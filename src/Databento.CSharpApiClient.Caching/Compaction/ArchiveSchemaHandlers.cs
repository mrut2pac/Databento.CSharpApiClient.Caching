using System;
using System.Collections.Generic;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

namespace Databento.CSharpApiClient.Caching.Compaction
{
    /// <summary>
    /// The Databento schemas the archive has columns for, by name. Another schema is a contained addition: its record interface, archive
    /// schema and day file format.
    /// </summary>
    internal static class ArchiveSchemaHandlers
    {
        private static readonly IReadOnlyDictionary<string, IArchiveSchemaHandler> Handlers = Create();

        /// <summary>
        /// Gets the handler of a Databento schema
        /// </summary>
        /// <param name="schema">Databento schema, as its folder is named, e.g. <c>cbbo-1m</c></param>
        /// <param name="handler">The handler</param>
        /// <returns><c>false</c> when the archive has no columns for the schema</returns>
        public static bool TryGet(string schema, out IArchiveSchemaHandler handler)
        {
            return Handlers.TryGetValue(schema, out handler);
        }

        private static IReadOnlyDictionary<string, IArchiveSchemaHandler> Create()
        {
            IArchiveSchemaHandler[] handlers =
            {
                new ArchiveSchemaHandler<ICbboRecord, CbboRecordDbn>(DayFileFormats.Cbbo1Sec),
                new ArchiveSchemaHandler<ICbboRecord, CbboRecordDbn>(DayFileFormats.Cbbo1Min),
                new ArchiveSchemaHandler<IBboRecord, BboRecordDbn>(DayFileFormats.Bbo1Sec),
                new ArchiveSchemaHandler<IBboRecord, BboRecordDbn>(DayFileFormats.Bbo1Min),
                new ArchiveSchemaHandler<ITcbboRecord, TcbboRecordDbn>(DayFileFormats.Tcbbo),
                new ArchiveSchemaHandler<ITradeRecord, TradeRecordDbn>(DayFileFormats.Trades),
                new ArchiveSchemaHandler<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Sec),
                new ArchiveSchemaHandler<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Min),
                new ArchiveSchemaHandler<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Hour),
                new ArchiveSchemaHandler<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Day),
                new ArchiveSchemaHandler<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.OhlcvEod),
                new ArchiveSchemaHandler<IStatisticsRecord, StatisticsRecordDbn>(DayFileFormats.Statistics),
            };

            Dictionary<string, IArchiveSchemaHandler> byName = new Dictionary<string, IArchiveSchemaHandler>(StringComparer.Ordinal);
            foreach(IArchiveSchemaHandler handler in handlers)
            {
                byName.Add(handler.Schema, handler);
            }

            return byName;
        }
    }
}
