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
    /// A Databento schema as the cached client serves it: its archive columns, its day files' and batch files' readers, the timestamp
    /// Databento filters a range on, which also decides a record's UTC day, and its download calls in either transport
    /// </summary>
    /// <typeparam name="TSource">Record interface the calls return</typeparam>
    /// <typeparam name="TRecord">Record type an archive read returns</typeparam>
    /// <typeparam name="TJson">JSON record type, which day files hold and downloads are turned into</typeparam>
    internal sealed class CachedSchema<TSource, TRecord, TJson> : ICachedSchema
        where TSource : class, IRecord
        where TRecord : class, TSource
        where TJson : class, TSource
    {
        public CachedSchema(
            string name,
            ArchiveRecordSchema<TSource, TRecord> archiveSchema,
            Func<Stream, CancellationToken, IAsyncEnumerable<TJson>> readJson,
            Func<Stream, CancellationToken, IAsyncEnumerable<TJson>> readDbn,
            Func<TSource, DateTime?> getFilterTime,
            Action<TJson, string> setSymbol,
            Func<DatabentoJsonClient, RangeRequest, Task<TJson[]>> downloadJson,
            Func<DatabentoClient, RangeRequest, Task<TJson[]>> downloadDbn)
        {
            this.Name = name;
            this.ArchiveSchema = archiveSchema;
            this.ReadDayFile = readJson;
            this.ReadDbn = readDbn;
            this.GetFilterTime = getFilterTime;
            this.SetSymbol = setSymbol;
            this.DownloadJson = downloadJson;
            this.DownloadDbn = downloadDbn;
        }

        /// <summary>
        /// Gets Databento's name of the schema, e.g. <c>cbbo-1m</c>, which its folder carries.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the archive columns of the schema.
        /// </summary>
        public ArchiveRecordSchema<TSource, TRecord> ArchiveSchema { get; }

        /// <summary>
        /// Gets the reader of JSON records, an array or one a line, decompressed: a day file's, or a JSON batch file's.
        /// </summary>
        public Func<Stream, CancellationToken, IAsyncEnumerable<TJson>> ReadDayFile { get; }

        /// <summary>
        /// Gets the reader of a DBN stream's records as JSON records, each carrying the symbol the stream's metadata maps it to.
        /// </summary>
        public Func<Stream, CancellationToken, IAsyncEnumerable<TJson>> ReadDbn { get; }

        /// <summary>
        /// Gets the timestamp Databento filters a range on for the schema: <c>ts_recv</c> where the schema has one, else <c>ts_event</c>.
        /// </summary>
        public Func<TSource, DateTime?> GetFilterTime { get; }

        /// <summary>
        /// Gets the setter of a JSON record's symbol, for batch records that carry none and are mapped through the symbology lookup.
        /// </summary>
        public Action<TJson, string> SetSymbol { get; }

        /// <summary>
        /// Gets the download of a range as JSON records.
        /// </summary>
        public Func<DatabentoJsonClient, RangeRequest, Task<TJson[]>> DownloadJson { get; }

        /// <summary>
        /// Gets the download of a range as DBN records, converted to JSON records.
        /// </summary>
        public Func<DatabentoClient, RangeRequest, Task<TJson[]>> DownloadDbn { get; }

        public Task<BatchCounts> IngestAsync(BatchIngestion ingestion, BatchFolder folder, CancellationToken cancellationToken)
        {
            return ingestion.IngestAsync(this, folder, cancellationToken);
        }

        public Task<bool> ContainsAsync(SymbolDayStore store, CacheMonthFolder folder, string symbol, DateTime day, bool strict, CancellationToken cancellationToken)
        {
            return store.ContainsAsync(this, folder, symbol, day, strict);
        }
    }
}
