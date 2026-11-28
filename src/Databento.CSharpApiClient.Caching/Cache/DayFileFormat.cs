using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Databento.CSharpApiClient.Caching.Archive.Schemas;
using Databento.CSharpApiClient.DataModel;
using Databento.CSharpApiClient.DataModel.Dbn;

namespace Databento.CSharpApiClient.Caching.Cache
{
    /// <summary>
    /// One Databento schema's day files: DBN records written and read through the client's <see cref="DbnWriter"/> and
    /// <see cref="DbnReader"/>, and the JSON records of a legacy day file or a JSON batch file read and converted to the same records
    /// </summary>
    /// <typeparam name="TSource">Record interface</typeparam>
    /// <typeparam name="TRecord">DBN record type the cache holds</typeparam>
    internal sealed class DayFileFormat<TSource, TRecord>
        where TSource : class, IRecord
        where TRecord : class, TSource
    {
        private readonly Func<Stream, CancellationToken, IAsyncEnumerable<TSource>> readJson;

        public DayFileFormat(
            string schema,
            DbnSchema dbnSchema,
            ArchiveRecordSchema<TSource, TRecord> recordSchema,
            Func<Stream, CancellationToken, IAsyncEnumerable<TSource>> readJson,
            Func<Stream, CancellationToken, IAsyncEnumerable<TRecord>> readDbn,
            Func<Stream, DbnMetadata, IAsyncEnumerable<TRecord>, CancellationToken, Task> writeDbn)
        {
            this.Schema = schema;
            this.DbnSchema = dbnSchema;
            this.RecordSchema = recordSchema;
            this.readJson = readJson;
            this.ReadDbn = readDbn;
            this.WriteDbn = writeDbn;
        }

        /// <summary>
        /// Gets Databento's name of the schema, e.g. <c>cbbo-1m</c>, which its folder carries.
        /// </summary>
        public string Schema { get; }

        /// <summary>
        /// Gets the schema as a DBN stream's metadata names it.
        /// </summary>
        public DbnSchema DbnSchema { get; }

        /// <summary>
        /// Gets the archive columns of the schema, which also convert a JSON record to the DBN record type.
        /// </summary>
        public ArchiveRecordSchema<TSource, TRecord> RecordSchema { get; }

        /// <summary>
        /// Gets the reader of a decompressed DBN stream's records, each carrying the symbol the stream's metadata maps it to.
        /// </summary>
        public Func<Stream, CancellationToken, IAsyncEnumerable<TRecord>> ReadDbn { get; }

        /// <summary>
        /// Gets the writer of an uncompressed DBN stream: the metadata, then the records.
        /// </summary>
        public Func<Stream, DbnMetadata, IAsyncEnumerable<TRecord>, CancellationToken, Task> WriteDbn { get; }

        /// <summary>
        /// Reads decompressed JSON records, an array or one a line, as the DBN record type, converted as they are read
        /// </summary>
        /// <exception cref="InvalidDataException">The JSON is no records of the schema, or holds a value the record interface cannot give</exception>
        public async IAsyncEnumerable<TRecord> ReadJsonAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach(TSource record in this.readJson(stream, cancellationToken).ConfigureAwait(false))
            {
                yield return this.RecordSchema.ToRecord(record);
            }
        }
    }
}
