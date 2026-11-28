using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.Caching.Cache;
using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// A Databento schema as the cached client serves it: its day files, which also read batch files, the timestamp Databento filters a
    /// range on, which also decides a record's UTC day, and its download calls in either transport. Records are held as the client's DBN
    /// record type whichever transport brought them.
    /// </summary>
    /// <typeparam name="TSource">Record interface the calls return</typeparam>
    /// <typeparam name="TRecord">DBN record type, which day files and archive reads give and downloads are turned into</typeparam>
    internal sealed class CachedSchema<TSource, TRecord> : ICachedSchema
        where TSource : class, IRecord
        where TRecord : class, TSource
    {
        public CachedSchema(
            DayFileFormat<TSource, TRecord> format,
            Func<TSource, DateTime?> getFilterTime,
            Action<TRecord, string> setSymbol,
            Func<DatabentoJsonClient, RangeRequest, Task<TRecord[]>> downloadJson,
            Func<DatabentoClient, RangeRequest, Task<TRecord[]>> downloadDbn)
        {
            this.Format = format;
            this.GetFilterTime = getFilterTime;
            this.SetSymbol = setSymbol;
            this.DownloadJson = downloadJson;
            this.DownloadDbn = downloadDbn;
        }

        /// <summary>
        /// Gets Databento's name of the schema, e.g. <c>cbbo-1m</c>, which its folder carries.
        /// </summary>
        public string Name => this.Format.Schema;

        /// <summary>
        /// Gets the schema's day files.
        /// </summary>
        public DayFileFormat<TSource, TRecord> Format { get; }

        /// <summary>
        /// Gets the archive columns of the schema.
        /// </summary>
        public ArchiveRecordSchema<TSource, TRecord> ArchiveSchema => this.Format.RecordSchema;

        /// <summary>
        /// Gets the reader of a JSON batch file's records, an array or one a line, decompressed, converted to the DBN record type.
        /// </summary>
        public Func<Stream, CancellationToken, IAsyncEnumerable<TRecord>> ReadJson => this.Format.ReadJsonAsync;

        /// <summary>
        /// Gets the reader of a DBN stream's records, each carrying the symbol the stream's metadata maps it to.
        /// </summary>
        public Func<Stream, CancellationToken, IAsyncEnumerable<TRecord>> ReadDbn => this.Format.ReadDbn;

        /// <summary>
        /// Gets the timestamp Databento filters a range on for the schema: <c>ts_recv</c> where the schema has one, else <c>ts_event</c>.
        /// </summary>
        public Func<TSource, DateTime?> GetFilterTime { get; }

        /// <summary>
        /// Gets the setter of a record's symbol, for batch records that carry none and are mapped through the symbology lookup.
        /// </summary>
        public Action<TRecord, string> SetSymbol { get; }

        /// <summary>
        /// Gets the download of a range as JSON records, converted to the DBN record type.
        /// </summary>
        public Func<DatabentoJsonClient, RangeRequest, Task<TRecord[]>> DownloadJson { get; }

        /// <summary>
        /// Gets the download of a range as DBN records.
        /// </summary>
        public Func<DatabentoClient, RangeRequest, Task<TRecord[]>> DownloadDbn { get; }

        public Task<BatchCounts> IngestAsync(BatchIngestion ingestion, BatchFolder folder, CancellationToken cancellationToken)
        {
            return ingestion.IngestAsync(this, folder, cancellationToken);
        }

        public Task<bool> ContainsAsync(SymbolDayStore store, CacheMonthFolder folder, string symbol, DateTime day, bool strict, CancellationToken cancellationToken)
        {
            return store.ContainsAsync(this, folder, symbol, day, strict);
        }

        public Task<EnsureCachedReport> EnsureCachedAsync(CachedTimeseries timeseries, string dataset, IReadOnlyList<string> symbols, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
        {
            return timeseries.EnsureCachedAsync(this, dataset, symbols, startUtc, endUtc, cancellationToken);
        }
    }
}
