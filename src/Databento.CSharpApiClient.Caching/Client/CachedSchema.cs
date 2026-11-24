using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.DataModel;

namespace Databento.CSharpApiClient.Caching.Client
{
    /// <summary>
    /// A Databento schema as the cached client serves it: its archive columns, its day files' reader, the timestamp Databento filters a
    /// range on, which also decides a record's UTC day, and its download calls in either transport
    /// </summary>
    /// <typeparam name="TSource">Record interface the calls return</typeparam>
    /// <typeparam name="TRecord">Record type an archive read returns</typeparam>
    /// <typeparam name="TJson">JSON record type, which day files hold and downloads are turned into</typeparam>
    internal sealed class CachedSchema<TSource, TRecord, TJson>
        where TSource : class, IRecord
        where TRecord : class, TSource
        where TJson : class, TSource
    {
        public CachedSchema(
            string name,
            ArchiveRecordSchema<TSource, TRecord> archiveSchema,
            Func<Stream, CancellationToken, IAsyncEnumerable<TJson>> readDayFile,
            Func<TSource, DateTime?> getFilterTime,
            Func<DatabentoJsonClient, RangeRequest, Task<TJson[]>> downloadJson,
            Func<DatabentoClient, RangeRequest, Task<TJson[]>> downloadDbn)
        {
            this.Name = name;
            this.ArchiveSchema = archiveSchema;
            this.ReadDayFile = readDayFile;
            this.GetFilterTime = getFilterTime;
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
        /// Gets the reader of a day file's JSON records, decompressed.
        /// </summary>
        public Func<Stream, CancellationToken, IAsyncEnumerable<TJson>> ReadDayFile { get; }

        /// <summary>
        /// Gets the timestamp Databento filters a range on for the schema: <c>ts_recv</c> where the schema has one, else <c>ts_event</c>.
        /// </summary>
        public Func<TSource, DateTime?> GetFilterTime { get; }

        /// <summary>
        /// Gets the download of a range as JSON records.
        /// </summary>
        public Func<DatabentoJsonClient, RangeRequest, Task<TJson[]>> DownloadJson { get; }

        /// <summary>
        /// Gets the download of a range as DBN records, converted to JSON records.
        /// </summary>
        public Func<DatabentoClient, RangeRequest, Task<TJson[]>> DownloadDbn { get; }
    }
}
