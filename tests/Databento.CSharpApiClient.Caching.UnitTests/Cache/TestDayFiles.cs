// Ignore Spelling: Bbo Cbbo Tcbbo Ohlcv

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;

using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

namespace Databento.CSharpApiClient.Caching.UnitTests.Cache
{
    /// <summary>
    /// Writes and reads day files of any schema by its name: DBN day files as the cache saves them, from records of either type, and
    /// legacy day files as format version 1 wrote them, a gzipped JSON array
    /// </summary>
    internal static class TestDayFiles
    {
        /// <summary>
        /// Writes a day file as the cache saves one
        /// </summary>
        /// <returns><c>true</c> when written, <c>false</c> when a file there was kept</returns>
        public static bool Write(string path, string dataset, string schema, string symbol, DateTime date, IEnumerable<IRecord> records, bool overwrite = true)
        {
            return Of(schema).Write(path, dataset, symbol, date, records.ToList(), overwrite);
        }

        /// <summary>
        /// Reads a day file, DBN or legacy by its extension
        /// </summary>
        public static IRecord[] Read(string path, string schema)
        {
            return Of(schema).Read(path);
        }

        /// <summary>
        /// Writes a legacy day file: the records as a JSON array, gzip-compressed
        /// </summary>
        public static void WriteLegacy<TJson>(string path, IReadOnlyList<TJson> records)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(FileStream file = File.Create(path))
            using(GZipStream json = new GZipStream(file, CompressionLevel.Optimal))
            {
                JsonSerializer.Serialize(json, records);
            }
        }

        private static IFormat Of(string schema)
        {
            return schema switch
            {
                Schema.ConsolidatedBBO1Sec => new Format<ICbboRecord, CbboRecordDbn>(DayFileFormats.Cbbo1Sec),
                Schema.ConsolidatedBBO1Min => new Format<ICbboRecord, CbboRecordDbn>(DayFileFormats.Cbbo1Min),
                Schema.Bbo1Sec => new Format<IBboRecord, BboRecordDbn>(DayFileFormats.Bbo1Sec),
                Schema.Bbo1Min => new Format<IBboRecord, BboRecordDbn>(DayFileFormats.Bbo1Min),
                Schema.Tcbbo => new Format<ITcbboRecord, TcbboRecordDbn>(DayFileFormats.Tcbbo),
                Schema.Trades => new Format<ITradeRecord, TradeRecordDbn>(DayFileFormats.Trades),
                Schema.Ohlcv1Sec => new Format<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Sec),
                Schema.Ohlcv1Min => new Format<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Min),
                Schema.Ohlcv1Hour => new Format<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Hour),
                Schema.Ohlcv1Day => new Format<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.Ohlcv1Day),
                Schema.OhlcvEod => new Format<IOhlcvRecord, OhlcvRecordDbn>(DayFileFormats.OhlcvEod),
                Schema.Statistics => new Format<IStatisticsRecord, StatisticsRecordDbn>(DayFileFormats.Statistics),
                _ => throw new ArgumentOutOfRangeException(nameof(schema), schema, "No day files for this schema."),
            };
        }

        private interface IFormat
        {
            bool Write(string path, string dataset, string symbol, DateTime date, IReadOnlyList<IRecord> records, bool overwrite);

            IRecord[] Read(string path);
        }

        private sealed class Format<TSource, TRecord> : IFormat
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            private readonly DayFileFormat<TSource, TRecord> format;

            public Format(DayFileFormat<TSource, TRecord> format)
            {
                this.format = format;
            }

            public bool Write(string path, string dataset, string symbol, DateTime date, IReadOnlyList<IRecord> records, bool overwrite)
            {
                TRecord[] converted = records.Select(record => this.format.RecordSchema.ToRecord((TSource)record)).ToArray();
                return DayFile.WriteAsync(path, this.format, dataset, symbol, date, converted, overwrite, CancellationToken.None).GetAwaiter().GetResult();
            }

            public IRecord[] Read(string path)
            {
                return DayFile.ReadAsync(path, this.format, CancellationToken.None).GetAwaiter().GetResult();
            }
        }
    }
}
