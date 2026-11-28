// Ignore Spelling: Bbo Cbbo Tcbbo Ohlcv

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// The day file format of each schema the archive has columns for
    /// </summary>
    internal static class DayFileFormats
    {
        public static readonly DayFileFormat<ICbboRecord, CbboRecordDbn> Cbbo1Sec = Cbbo(Schema.ConsolidatedBBO1Sec, DbnSchema.Cbbo1S, RType.Cbbo1S);

        public static readonly DayFileFormat<ICbboRecord, CbboRecordDbn> Cbbo1Min = Cbbo(Schema.ConsolidatedBBO1Min, DbnSchema.Cbbo1M, RType.Cbbo1M);

        public static readonly DayFileFormat<IBboRecord, BboRecordDbn> Bbo1Sec = Bbo(Schema.Bbo1Sec, DbnSchema.Bbo1S, RType.Bbo1S);

        public static readonly DayFileFormat<IBboRecord, BboRecordDbn> Bbo1Min = Bbo(Schema.Bbo1Min, DbnSchema.Bbo1M, RType.Bbo1M);

        public static readonly DayFileFormat<ITcbboRecord, TcbboRecordDbn> Tcbbo = new DayFileFormat<ITcbboRecord, TcbboRecordDbn>(
            Schema.Tcbbo,
            DbnSchema.Tcbbo,
            new TcbboArchiveSchema(RType.Tcbbo),
            JsonRecordReader.ReadTcbboAsync,
            DbnReader.ReadTcbboAsync,
            DbnWriter.WriteTcbboAsync);

        public static readonly DayFileFormat<ITradeRecord, TradeRecordDbn> Trades = new DayFileFormat<ITradeRecord, TradeRecordDbn>(
            Schema.Trades,
            DbnSchema.Trades,
            new TradeArchiveSchema(RType.Mbp0),
            JsonRecordReader.ReadTradesAsync,
            DbnReader.ReadTradesAsync,
            DbnWriter.WriteTradesAsync);

        public static readonly DayFileFormat<IOhlcvRecord, OhlcvRecordDbn> Ohlcv1Sec = Ohlcv(Schema.Ohlcv1Sec, DbnSchema.Ohlcv1S, RType.Ohlcv1S);

        public static readonly DayFileFormat<IOhlcvRecord, OhlcvRecordDbn> Ohlcv1Min = Ohlcv(Schema.Ohlcv1Min, DbnSchema.Ohlcv1M, RType.Ohlcv1M);

        public static readonly DayFileFormat<IOhlcvRecord, OhlcvRecordDbn> Ohlcv1Hour = Ohlcv(Schema.Ohlcv1Hour, DbnSchema.Ohlcv1H, RType.Ohlcv1H);

        public static readonly DayFileFormat<IOhlcvRecord, OhlcvRecordDbn> Ohlcv1Day = Ohlcv(Schema.Ohlcv1Day, DbnSchema.Ohlcv1D, RType.Ohlcv1D);

        public static readonly DayFileFormat<IOhlcvRecord, OhlcvRecordDbn> OhlcvEod = Ohlcv(Schema.OhlcvEod, DbnSchema.OhlcvEod, RType.OhlcvEod);

        public static readonly DayFileFormat<IStatisticsRecord, StatisticsRecordDbn> Statistics = new DayFileFormat<IStatisticsRecord, StatisticsRecordDbn>(
            Schema.Statistics,
            DbnSchema.Statistics,
            new StatisticsArchiveSchema(RType.Statistics),
            JsonRecordReader.ReadStatisticsAsync,
            DbnReader.ReadStatisticsAsync,
            DbnWriter.WriteStatisticsAsync);

        private static DayFileFormat<ICbboRecord, CbboRecordDbn> Cbbo(string schema, DbnSchema dbnSchema, RType recordType)
        {
            return new DayFileFormat<ICbboRecord, CbboRecordDbn>(schema, dbnSchema, new CbboArchiveSchema(recordType), JsonRecordReader.ReadCbboAsync, DbnReader.ReadCbboAsync, DbnWriter.WriteCbboAsync);
        }

        private static DayFileFormat<IBboRecord, BboRecordDbn> Bbo(string schema, DbnSchema dbnSchema, RType recordType)
        {
            return new DayFileFormat<IBboRecord, BboRecordDbn>(schema, dbnSchema, new BboArchiveSchema(recordType), JsonRecordReader.ReadBboAsync, DbnReader.ReadBboAsync, DbnWriter.WriteBboAsync);
        }

        private static DayFileFormat<IOhlcvRecord, OhlcvRecordDbn> Ohlcv(string schema, DbnSchema dbnSchema, RType recordType)
        {
            return new DayFileFormat<IOhlcvRecord, OhlcvRecordDbn>(schema, dbnSchema, new OhlcvArchiveSchema(recordType), JsonRecordReader.ReadOhlcvAsync, DbnReader.ReadOhlcvAsync, DbnWriter.WriteOhlcvAsync);
        }
    }
}
