// Ignore Spelling: dbn

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Columns;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

namespace Databento.CSharpApiClient.Caching.UnitTests.Client
{
    /// <summary>
    /// Writes a DBN v2 stream of a schema's records through the client's <see cref="DbnWriter"/>, as Databento answers a DBN request: its
    /// metadata maps each requested raw symbol to an instrument id of its own over the request's days, and each record carries its
    /// symbol's instrument id; a record of a symbol not asked for carries an id the metadata does not map
    /// </summary>
    internal static class FakeDbnStream
    {
        public static byte[] Build(FakeRequest request, string schema, IReadOnlyList<IRecord> records)
        {
            return schema switch
            {
                Schema.ConsolidatedBBO1Sec => Build(DayFileFormats.Cbbo1Sec, request, records),
                Schema.ConsolidatedBBO1Min => Build(DayFileFormats.Cbbo1Min, request, records),
                Schema.Bbo1Sec => Build(DayFileFormats.Bbo1Sec, request, records),
                Schema.Bbo1Min => Build(DayFileFormats.Bbo1Min, request, records),
                Schema.Tcbbo => Build(DayFileFormats.Tcbbo, request, records),
                Schema.Trades => Build(DayFileFormats.Trades, request, records),
                Schema.Ohlcv1Sec => Build(DayFileFormats.Ohlcv1Sec, request, records),
                Schema.Ohlcv1Min => Build(DayFileFormats.Ohlcv1Min, request, records),
                Schema.Ohlcv1Hour => Build(DayFileFormats.Ohlcv1Hour, request, records),
                Schema.Ohlcv1Day => Build(DayFileFormats.Ohlcv1Day, request, records),
                Schema.Statistics => Build(DayFileFormats.Statistics, request, records),
                _ => throw new ArgumentOutOfRangeException(nameof(schema), schema, "No DBN for this schema."),
            };
        }

        private static byte[] Build<TSource, TRecord>(DayFileFormat<TSource, TRecord> format, FakeRequest request, IReadOnlyList<IRecord> records)
            where TSource : class, IRecord
            where TRecord : class, TSource
        {
            string[] symbols = request.Symbols;
            Dictionary<string, uint> instrumentIds = symbols.Select((symbol, i) => new KeyValuePair<string, uint>(symbol, (uint)(1001 + i))).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            DateTime firstDay = request.StartUtc.UtcDateTime.Date;
            DateTime endDay = request.EndUtc.UtcDateTime.AddTicks(-1).Date.AddDays(1);
            DbnMetadata metadata = new DbnMetadata
            {
                Version = 2,
                Dataset = request.Fields["dataset"],
                Schema = format.DbnSchema,
                Start = request.StartUtc,
                End = request.EndUtc,
                STypeIn = DbnSType.RawSymbol,
                STypeOut = DbnSType.InstrumentId,
                Symbols = symbols.ToList(),
                Mappings = symbols.Select(symbol => new DbnSymbolMapping
                {
                    RawSymbol = symbol,
                    Intervals = new List<DbnMappingInterval>
                    {
                        new DbnMappingInterval
                        {
                            StartDate = ToYyyyMmDd(firstDay),
                            EndDate = ToYyyyMmDd(endDay),
                            Symbol = Invariant($"{instrumentIds[symbol]}"),
                        },
                    },
                }).ToList(),
            };

            // a copy of each record, so the fake's own keeps its instrument id
            TRecord[] converted = records.Select(record =>
            {
                TRecord copy = format.RecordSchema.CreateRecord(record.Symbol);
                foreach(ArchiveColumn<TSource, TRecord> column in format.RecordSchema.Columns)
                {
                    column.Copy((TSource)record, copy);
                }

                typeof(TRecord).GetProperty(nameof(IRecord.InstrumentId)).SetValue(copy, instrumentIds.GetValueOrDefault(record.Symbol, 9999u));
                return copy;
            }).ToArray();

            using(MemoryStream stream = new MemoryStream())
            {
                format.WriteDbn(stream, metadata, ToAsyncEnumerable(converted), default).GetAwaiter().GetResult();
                return stream.ToArray();
            }
        }

        private static async IAsyncEnumerable<TRecord> ToAsyncEnumerable<TRecord>(IEnumerable<TRecord> records)
        {
            foreach(TRecord record in records)
            {
                yield return record;
            }

            await Task.CompletedTask;
        }

        private static uint ToYyyyMmDd(DateTime date)
        {
            return (uint)((date.Year * 10000) + (date.Month * 100) + date.Day);
        }
    }
}
